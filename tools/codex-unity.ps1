# Runs one Codex task that may call the Unity MCP tools, and closes that door again however the run ends.
#
# Codex's plugin runs tasks with approvalPolicy "never", so a tool that asks for approval is refused. Unity_RunCommand
# stays refused by default (CLAUDE.md §0). This script adds a per-tool approval to the user's ~/.codex/config.toml for the
# length of one run and removes it in a finally block. Use it only when the user said to let Codex use Unity MCP for this
# task (docs/RULES/SUBAGENTS.md §7-5). Nothing else may leave the block in place.
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
    [switch]$Close
)

$ErrorActionPreference = 'Stop'
$begin = '# >>> protoharness codex-unity (tools/codex-unity.ps1 removes this block)'
$end = '# <<< protoharness codex-unity'
$codexHome = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $HOME '.codex' }
$config = Join-Path $codexHome 'config.toml'
if (-not (Test-Path -LiteralPath $config)) { throw "codex-unity: no Codex config at $config" }
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Test-Open { ([System.IO.File]::ReadAllText($config, $utf8)).Contains($begin) }

function Open-Door {
    if (Test-Open) { throw "codex-unity: the block is already in $config. Another run, or a run that was killed: check, then -Close." }
    $text = [System.IO.File]::ReadAllText($config, $utf8)
    if (-not $text.EndsWith("`n")) { $text += "`n" }
    $block = @($begin,
        '[mcp_servers.unity-mcp.tools.Unity_RunCommand]', 'approval_mode = "approve"',
        '[mcp_servers.unity-mcp.tools.Unity_GetConsoleLogs]', 'approval_mode = "approve"',
        $end) -join "`n"
    [System.IO.File]::WriteAllText($config, $text + $block + "`n", $utf8)
}

function Close-Door {
    $text = [System.IO.File]::ReadAllText($config, $utf8)
    $from = $text.IndexOf($begin)
    if ($from -lt 0) { return }
    $to = $text.IndexOf($end, $from)
    if ($to -lt 0) { throw "codex-unity: the block in $config has no end marker. Fix it by hand." }
    $to += $end.Length
    if ($to -lt $text.Length -and $text[$to] -eq "`n") { $to++ }
    [System.IO.File]::WriteAllText($config, $text.Remove($from, $to - $from), $utf8)
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
