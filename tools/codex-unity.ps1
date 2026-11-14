# Runs one Codex task (every -Write task goes through here), and with -Unity lets it call the Unity MCP tools, closing that
# door again however the run ends.
#
# Codex's plugin runs tasks with approvalPolicy "never", so a tool that asks for approval is refused. Unity_RunCommand
# stays refused by default (CLAUDE.md §0). With -Unity this script gives those tools an approval in the user's
# ~/.codex/config.toml for the length of one run and takes it back in a finally block. A separate watcher process takes it
# back too if this script is killed before the finally runs. Only a power loss can leave it: then -Status says open and
# -Close closes it. Pass -Unity only when the user said to let Codex use Unity MCP for this task (docs/RULES/SUBAGENTS.md §7-5).
#
# The server's name differs between machines (unity_mcp, unity-mcp), so it is found by the relay it runs. A tool that already
# has a table keeps it: TOML refuses a second one, so its value changes in place and a trailing comment remembers the old one.
#
# A -Write run cannot write .git, .codex or .agents and cannot ask for more (approval "never"). So a -Write prompt that tells
# Codex to run a git write or to touch those folders is refused before Codex starts, and every -Write prompt gets the contract
# below in front: report git writes as CLAUDE-GIT lines, never end the turn on a bare question, and end with one CLAUDE-STATUS
# line. The run's last message is sorted into an exit code and a result file, which a caller can wait for (docs/RULES/SUBAGENTS.md §7-5):
#   0 done   2 prompt refused (Codex not started)   10 git writes for Claude   11 question   12 blocked   13 error or exhausted
# Next to <name>.md the run leaves <name>.sent.md (what was sent), <name>.out.md (Codex's last message), <name>.stderr.log and,
# last of all, <name>.result.json. The watcher writes a result file too when this script is killed before it can.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-unity.ps1 -PromptFile <path> [-Write] [-Unity] [-ResumeLast] [-Effort high]
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-unity.ps1 -Status
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-unity.ps1 -Close
param(
    [string]$PromptFile,
    [switch]$Write,
    [switch]$Unity,
    [switch]$ResumeLast,
    [string]$Effort = 'high',
    [switch]$Status,
    [switch]$Close,
    # Internal: the watcher waits for this run's process, then closes that run's block.
    [int]$Watch = 0,
    # Internal: the result file the watcher writes if the run never did.
    [string]$WatchResult
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

# Written to a side file and moved into place, so a caller waiting for the file never reads half of it.
function Write-Result([string]$path, [hashtable]$result) {
    $result.finishedAt = (Get-Date).ToString('o')
    $temp = "$path.tmp"
    [System.IO.File]::WriteAllText($temp, ($result | ConvertTo-Json -Depth 5), $utf8)
    Move-Item -LiteralPath $temp -Destination $path -Force
}

# The contract in front of every -Write prompt. Kept in Korean like the prompts it joins.
$contract = @'
## Claude 와의 계약 (tools/codex-unity.ps1 이 붙였다. 아래 지시서보다 먼저 지킨다)

- 이 실행은 workspace-write 샌드박스다. `.git`·`.codex`·`.agents` 는 읽기 전용이고, 승인 정책이 never 라 권한을 올릴 수 없다.
- git 으로 상태를 바꾸는 명령(add, mv, rm, commit, switch, checkout, stash, restore, reset, merge, fetch 등)을 실행하지 않는다. 필요하면 실행하는 대신 명령마다 `CLAUDE-GIT: <명령>` 한 줄을 적고 그 자리에서 턴을 끝낸다. Claude 가 실행한 뒤 이어 가게 한다. 읽기(git status, diff, log, show)는 해도 된다.
- 파일을 옮기거나 이름을 바꾸지 않는다(`.meta` 를 같이 옮겨야 하고 `git mv` 가 필요하다). 옮길 것은 `CLAUDE-GIT: git mv <원래> <새 경로>` 로 적는다(`.meta` 도 한 줄씩).
- `CLAUDE-GIT-DONE:` 으로 시작하는 줄은 Claude 가 이미 실행한 명령이다. 다시 실행하지 않는다.
- 질문으로 턴을 끝내지 않는다. 지시서 범위 안의 세부는 스스로 정하고, 마지막 보고에 무엇을 왜 그렇게 정했는지 적는다. 지시서 범위 밖이라 정할 수 없을 때만 `CLAUDE-QUESTION: <질문>` 을 적는다.
- 더 나아갈 수 없으면(같은 에러가 3번 반복, 필요한 도구가 없음 등) `CLAUDE-BLOCKED: <막힌 것>` 을 적고, 그 아래에 시도한 것과 에러 원문을 그대로 옮긴다.
- 마지막 메시지의 마지막 줄은 아래 넷 중 정확히 하나다:
  `CLAUDE-STATUS: done` (끝남) · `CLAUDE-STATUS: git` (CLAUDE-GIT 줄을 실행해야 이어 간다) · `CLAUDE-STATUS: question` · `CLAUDE-STATUS: blocked`

---

'@

# Lines of a -Write prompt that the sandbox would refuse anyway. CLAUDE-GIT-DONE lines only tell Codex what already ran.
function Get-ForbiddenLines([string]$text) {
    $gitWrite = '(?i)\bgit\s+(?:-C\s+\S+\s+)?(?:add|mv|rm|commit|switch|checkout|stash|restore|reset|merge|rebase|cherry-pick|revert|push|pull|fetch|tag|clean|apply|am|branch\s+(?!--show-current|--list|-a\b|-r\b|-v)\S)'
    $lockedPath = '(?i)(?<![\w.])\.(?:git|codex|agents)[/\\]'
    $number = 0
    foreach ($line in ($text -split "\r?\n")) {
        $number++
        if ($line -match '^\s*CLAUDE-GIT-DONE:') { continue }
        if ($line -match $gitWrite -or $line -match $lockedPath) { "${number}: $line" }
    }
}

# Sorts Codex's last message by the contract. A run that broke the contract is blocked (12), never done.
function Get-Verdict($run) {
    $verdict = @{ companionExit = $run.Exit }
    $payload = $null
    try { $payload = $run.Out | ConvertFrom-Json }
    catch {
        $verdict.code = 13; $verdict.kind = 'error'
        $verdict.reason = "companion stdout is not JSON: $($_.Exception.Message)"
        $verdict.rawOutput = $run.Out
        return $verdict
    }
    $raw = [string]$payload.rawOutput
    $verdict.rawOutput = $raw
    $verdict.status = $payload.status
    $verdict.threadId = $payload.threadId
    $verdict.touchedFiles = @($payload.touchedFiles | Where-Object { $_ })
    if ($run.Exit -ne 0 -or $payload.status -ne 0 -or -not $raw.Trim()) {
        $verdict.code = 13; $verdict.kind = 'error'
        $verdict.reason = "companion exit $($run.Exit), turn status $($payload.status), last message $(if ($raw.Trim()) { 'present' } else { 'empty' }). Read the stderr log."
        return $verdict
    }
    $lines = @($raw -split "\r?\n" | Where-Object { $_.Trim() })
    $verdict.gitLines = @($lines | ForEach-Object { if ($_ -match '^\s*CLAUDE-GIT:\s*(.+?)\s*$') { $Matches[1] } })
    $verdict.questions = @($lines | ForEach-Object { if ($_ -match '^\s*CLAUDE-QUESTION:\s*(.+?)\s*$') { $Matches[1] } })
    $verdict.blocked = @($lines | ForEach-Object { if ($_ -match '^\s*CLAUDE-BLOCKED:\s*(.+?)\s*$') { $Matches[1] } })
    $last = $lines[-1].Trim()
    $verdict.statusLine = if ($last -match '^CLAUDE-STATUS:\s*(done|git|question|blocked)$') { $Matches[1] } else { $null }
    if ($raw -match '(?i)\.git[/\\]\S*[^\r\n]*Permission denied') { $verdict.sandboxDenied = $true }

    if ($verdict.statusLine -eq 'blocked' -or $verdict.blocked.Count -gt 0) {
        $verdict.code = 12; $verdict.kind = 'blocked'; $verdict.reason = 'Codex reported it is blocked.'
    }
    elseif ($verdict.statusLine -eq 'git' -or $verdict.gitLines.Count -gt 0) {
        if ($verdict.gitLines.Count -eq 0) { $verdict.code = 12; $verdict.kind = 'blocked'; $verdict.reason = 'CLAUDE-STATUS: git without any CLAUDE-GIT line.' }
        else { $verdict.code = 10; $verdict.kind = 'git'; $verdict.reason = "$($verdict.gitLines.Count) git write(s) for Claude." }
    }
    elseif ($verdict.statusLine -eq 'question' -or $verdict.questions.Count -gt 0 -or ((-not $verdict.statusLine) -and $last -match '[?？]$')) {
        $verdict.code = 11; $verdict.kind = 'question'
        $verdict.reason = if ($verdict.questions.Count -gt 0) { 'Codex asked through CLAUDE-QUESTION.' } else { 'Codex ended on a question outside the contract.' }
    }
    elseif ($verdict.statusLine -eq 'done') {
        $verdict.code = 0; $verdict.kind = 'done'; $verdict.reason = 'Codex reported done.'
    }
    else {
        $verdict.code = 12; $verdict.kind = 'blocked'; $verdict.reason = 'The last line is not a CLAUDE-STATUS line, so the run broke the contract.'
    }
    if ($verdict.sandboxDenied -and $verdict.code -eq 0) {
        $verdict.code = 12; $verdict.kind = 'blocked'; $verdict.reason = 'Codex reported done, but its message shows a .git write refused by the sandbox.'
    }
    $verdict
}

# node's own streams, read as UTF-8 so Korean survives (PowerShell 5.1 redirection would re-encode them).
function Invoke-Node([string[]]$argv) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = (Get-Command node -CommandType Application | Select-Object -First 1).Source
    $info.Arguments = ($argv | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join ' '
    $info.WorkingDirectory = (Get-Location).ProviderPath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $utf8
    $info.StandardErrorEncoding = $utf8
    $process = [System.Diagnostics.Process]::Start($info)
    $out = $process.StandardOutput.ReadToEndAsync()
    $err = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    [pscustomobject]@{ Exit = $process.ExitCode; Out = $out.Result; Err = $err.Result }
}

if ($Watch -gt 0) {
    Wait-Process -Id $Watch -ErrorAction SilentlyContinue
    if ((Get-Owner) -eq $Watch) { Close-Door }
    if ($WatchResult -and -not (Test-Path -LiteralPath $WatchResult)) {
        Write-Result $WatchResult @{ code = 13; kind = 'killed'; reason = "codex-unity process $Watch ended without writing a result. Check Codex is not still running." }
    }
    exit 0
}
if ($Status) { if (Test-Open) { 'open' } else { 'closed' }; exit 0 }
if ($Close) { Close-Door; 'closed'; exit 0 }
if (-not $PromptFile) { throw 'codex-unity: -PromptFile is required.' }
if (-not (Test-Path -LiteralPath $PromptFile)) { throw "codex-unity: no prompt file at $PromptFile" }

$promptPath = (Resolve-Path -LiteralPath $PromptFile).ProviderPath
$base = Join-Path (Split-Path -Parent $promptPath) ([System.IO.Path]::GetFileNameWithoutExtension($promptPath))
$sentFile = "$base.sent.md"; $outFile = "$base.out.md"; $errFile = "$base.stderr.log"; $resultFile = "$base.result.json"
foreach ($file in $sentFile, $outFile, $errFile, $resultFile) {
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}

$prompt = [System.IO.File]::ReadAllText($promptPath, $utf8)
if ($Write) {
    $forbidden = @(Get-ForbiddenLines $prompt)
    if ($forbidden.Count -gt 0) {
        Write-Result $resultFile @{ code = 2; kind = 'refused'; reason = 'The prompt asks for git writes or .git/.codex/.agents paths that the -Write sandbox refuses. Run those yourself first, then say so in CLAUDE-GIT-DONE lines.'; lines = $forbidden }
        [Console]::Error.WriteLine("codex-unity: refused, Codex not started. Lines of $promptPath :`n" + ($forbidden -join "`n"))
        exit 2
    }
    $prompt = $contract + $prompt
}
[System.IO.File]::WriteAllText($sentFile, $prompt, $utf8)

$plugins = Join-Path $HOME '.claude/plugins/cache/openai-codex/codex'
$companion = Get-ChildItem -LiteralPath $plugins -Directory | Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'scripts/codex-companion.mjs' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $companion) { throw "codex-unity: no codex-companion.mjs under $plugins" }

$arguments = @($companion, 'task', '--json', '--effort', $Effort, '--prompt-file', $sentFile)
if ($Write) { $arguments += '--write' }
if ($ResumeLast) { $arguments += '--resume-last' }

# The watcher starts first, so there is no moment with the block in place and nothing to remove it. It runs without -Unity
# too: then it only writes the result file of a killed run (it closes a block only when this process owns it).
Start-Process powershell -WindowStyle Hidden -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Watch', $PID, '-WatchResult', "`"$resultFile`"")
$result = $null
$opened = $false
try {
    if ($Unity) { Open-Door; $opened = $true }
    $run = Invoke-Node $arguments
    [System.IO.File]::WriteAllText($errFile, $run.Err, $utf8)
    $result = Get-Verdict $run
}
catch {
    # Recorded as error 13 with the message, and said on stderr: the caller waits for the result file, not for a throw.
    $result = @{ code = 13; kind = 'error'; reason = "codex-unity: $($_.Exception.Message)" }
    [Console]::Error.WriteLine($result.reason)
}
finally {
    # Only a block this run opened is closed: without -Unity, or when Open-Door refused, another run's block is not ours to take.
    try { if ($opened) { Close-Door } }
    catch {
        if ($null -eq $result) { $result = @{ code = 13; kind = 'error'; reason = '' } }
        $result.code = 13; $result.kind = 'error'
        $result.reason = "$($result.reason) Close-Door failed, the approval may still be in $config (check with -Status): $($_.Exception.Message)".Trim()
        [Console]::Error.WriteLine($result.reason)
    }
    if ($null -eq $result) { $result = @{ code = 13; kind = 'error'; reason = 'codex-unity: the run stopped before a verdict.' } }
    if ($result.ContainsKey('rawOutput')) {
        [System.IO.File]::WriteAllText($outFile, [string]$result.rawOutput, $utf8)
        $result.Remove('rawOutput')
    }
    $result.prompt = $promptPath; $result.sent = $sentFile; $result.out = $outFile; $result.stderr = $errFile
    Write-Result $resultFile $result
    "codex-unity: closed, exit $($result.code) ($($result.kind)) - $resultFile"
}
exit $result.code
