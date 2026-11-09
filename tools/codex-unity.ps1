# Runs one Codex task that may call the Unity MCP tools, and closes that door again however the run ends.
#
# Codex's plugin runs tasks with approvalPolicy "never", so a tool that asks for approval is refused. Unity_RunCommand
# stays refused by default (CLAUDE.md §0). This script gives those tools an approval in the user's ~/.codex/config.toml for
# the length of one run and takes it back in a finally block. A separate watcher process takes it back too if this script is
# killed before the finally runs. Only a power loss can leave it: then -Status says open and -Close closes it. Use it only
# when the user said to let Codex use Unity MCP for this task (docs/RULES/SUBAGENTS.md §7-5).
#
# The server's name differs between machines (unity_mcp, unity-mcp), so it is found by the relay it runs. A tool that already
# has a table keeps it: TOML refuses a second one, so its value changes in place and a trailing comment remembers the old one.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-unity.ps1 -PromptFile <path> [-Write] [-ResumeLast] [-Effort high]
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-unity.ps1 -Status
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-unity.ps1 -Close
param(
    [string]$PromptFile,
    [switch]$Write,
    [switch]$ResumeLast,
    [string]$Effort = 'high',
    [switch]$Status,
    [switch]$Close,
    # Internal: the watcher waits for this run's process, then closes that run's block.
    [int]$Watch = 0
)

$ErrorActionPreference = 'Stop'
$begin = '# >>> protoharness codex-unity (tools/codex-unity.ps1 removes this block)'
$end = '# <<< protoharness codex-unity'
$inline = '# protoharness codex-unity owner'
$tools = 'Unity_RunCommand', 'Unity_GetConsoleLogs'
$codexHome = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $HOME '.codex' }
$config = Join-Path $codexHome 'config.toml'
if (-not (Test-Path -LiteralPath $config)) { throw "codex-unity: no Codex config at $config" }
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Test-Open {
    $text = [System.IO.File]::ReadAllText($config, $utf8)
    $text.Contains($begin) -or $text.Contains($inline)
}

# The one MCP server that runs the Unity relay. None or several is a fault to fix by hand, not a guess to make.
function Get-Server([string]$text) {
    $pattern = '(?m)^\[mcp_servers\.([^\].]+)\][ \t]*\r?\n(?:(?!\[)[^\r\n]*\r?\n)*?command[ \t]*=[^\r\n]*relay'
    $found = @([regex]::Matches($text, $pattern) | ForEach-Object { $_.Groups[1].Value })
    if ($found.Count -ne 1) { throw "codex-unity: expected one Unity relay server in $config, found $($found.Count): $($found -join ', ')" }
    $found[0]
}

function Open-Door {
    if (Test-Open) { throw "codex-unity: the block is already in $config. Another run, or a run that was killed: check, then -Close." }
    $text = [System.IO.File]::ReadAllText($config, $utf8)
    if (-not $text.EndsWith("`n")) { $text += "`n" }
    $server = Get-Server $text
    $missing = @()
    foreach ($tool in $tools) {
        $table = '(?m)^(\[mcp_servers\.' + [regex]::Escape($server) + '\.tools\.' + $tool + '\][ \t]*\r?\n)'
        if (-not [regex]::IsMatch($text, $table)) { $missing += $tool; continue }
        $value = $table + 'approval_mode[ \t]*=[ \t]*"(\w+)"[ \t]*(?=\r?$)'
        if (-not [regex]::IsMatch($text, $value)) { throw "codex-unity: $server's $tool table does not start with a plain approval_mode. Fix it by hand." }
        $text = [regex]::Replace($text, $value, '$1approval_mode = "approve" ' + "$inline $PID was " + '$2')
    }
    if ($missing.Count -gt 0) {
        $lines = @($begin, "# owner $PID")
        foreach ($tool in $missing) { $lines += "[mcp_servers.$server.tools.$tool]", 'approval_mode = "approve"' }
        $text += (($lines + $end) -join "`n") + "`n"
    }
    [System.IO.File]::WriteAllText($config, $text, $utf8)
}

function Close-Door {
    $text = [System.IO.File]::ReadAllText($config, $utf8)
    $text = [regex]::Replace($text, 'approval_mode = "approve" ' + [regex]::Escape($inline) + ' \d+ was (\w+)', 'approval_mode = "$1"')
    $from = $text.IndexOf($begin)
    if ($from -ge 0) {
        $to = $text.IndexOf($end, $from)
        if ($to -lt 0) { throw "codex-unity: the block in $config has no end marker. Fix it by hand." }
        $to += $end.Length
        if ($to -lt $text.Length -and $text[$to] -eq "`n") { $to++ }
        $text = $text.Remove($from, $to - $from)
    }
    [System.IO.File]::WriteAllText($config, $text, $utf8)
}

# The block's owner, so a watcher that wakes late never closes a later run's block.
function Get-Owner {
    $found = [regex]::Match([System.IO.File]::ReadAllText($config, $utf8), '(?m)(?:^# owner|' + [regex]::Escape($inline) + ') (\d+)')
    if ($found.Success) { [int]$found.Groups[1].Value } else { 0 }
}

if ($Watch -gt 0) {
    Wait-Process -Id $Watch -ErrorAction SilentlyContinue
    if ((Get-Owner) -eq $Watch) { Close-Door }
    exit 0
}
if ($Status) { if (Test-Open) { 'open' } else { 'closed' }; exit 0 }
if ($Close) { Close-Door; 'closed'; exit 0 }
if (-not $PromptFile) { throw 'codex-unity: -PromptFile is required.' }
if (-not (Test-Path -LiteralPath $PromptFile)) { throw "codex-unity: no prompt file at $PromptFile" }

$plugins = Join-Path $HOME '.claude/plugins/cache/openai-codex/codex'
$companion = Get-ChildItem -LiteralPath $plugins -Directory | Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'scripts/codex-companion.mjs' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $companion) { throw "codex-unity: no codex-companion.mjs under $plugins" }

$arguments = @($companion, 'task', '--effort', $Effort, '--prompt-file', $PromptFile)
if ($Write) { $arguments += '--write' }
if ($ResumeLast) { $arguments += '--resume-last' }

# The watcher starts first, so there is no moment with the block in place and nothing to remove it.
Start-Process powershell -WindowStyle Hidden -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Watch', $PID)
Open-Door
$code = 1
try {
    & node @arguments
    $code = $LASTEXITCODE
}
finally {
    Close-Door
    "codex-unity: closed (exit $code)"
}
exit $code
