# Waits for one Codex run's result file (tools/codex-unity.ps1 writes <prompt>.<RunId>.result.json last of all) and comes
# back early, and loudly, when nothing has moved for a while — so a run that hangs or thinks for long is reported within
# minutes instead of at the wait's cap (docs/RULES/SUBAGENTS.md §7-5 "기다리기", DECISIONS 2026-11-14 "Codex 무활동 알림").
#
# What counts as activity, checked every -PollSeconds:
#   - the byte size of this run's Codex session log (<CODEX_HOME>/sessions/yyyy/MM/dd/*.jsonl, today's and yesterday's;
#     which log is this run's: see -Thread). The size, not
#     the time: Windows does not update the modified time of a file another process keeps open (measured 2026-11-14:
#     2,765,103 -> 2,776,397 bytes in 20 s while the time stayed at the run's start);
#   - the byte size of the Unity Editor log (the run compiles and tests through Unity MCP);
#   - the repository's `git status --porcelain` (the run edits files);
#   - the byte size of every -Watch file, and of the run's own <prompt>.<RunId>.stderr.log when -Result is a codex-unity
#     result path. A companion review (`adversarial-review`) writes no session log here; redirect its stderr to a file,
#     where it streams one "[codex] Running command / Command completed" line per step, and pass that file as -Watch.
# Not a signal: <CODEX_HOME>/logs_*.sqlite. Other Codex processes write it about every 40 s while no run is going
# (measured 2026-11-14), so it would never look idle.
# Nothing changing for -IdleMinutes is "idle". The script only reports: it never stops Codex (the user decides).
#
#   0 result   the result file appeared; its content is printed
#   3 idle     nothing moved for -IdleMinutes; the newest session log's last event types are printed
#   4 cap      no result after -CapMinutes
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-wait.ps1 -Result <prompt>.<RunId>.result.json [-IdleMinutes 10] [-CapMinutes 90]
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/codex-wait.ps1 -Result <review>.done -Watch <review>.err.log
#     (a review run as `node <cc> adversarial-review ... > <review>.out.md 2> <review>.err.log; echo done > <review>.done`)
#
# Run it with run_in_background next to codex-unity.ps1. On 3, tell the user first ("Codex 멈춤: ...") and start it again
# to keep waiting. The sources can be pointed elsewhere (-SessionsRoot, -EditorLog, -Repo) to test the script itself.
param(
    [Parameter(Mandatory = $true)][string]$Result,
    [double]$IdleMinutes = 10,
    [double]$CapMinutes = 90,
    [double]$PollSeconds = 30,
    [string[]]$Watch = @(),
    # The thread id of the run to watch (a -ResumeLast run continues the thread named in the earlier result's threadId).
    # Without it, only session logs created from two minutes before this script started count: the new thread of a run
    # launched just before. Other Codex sessions write logs too, and counting them would hide this run going quiet.
    [string]$Thread,
    [string]$SessionsRoot,
    [string]$EditorLog,
    [string]$Repo
)

$ErrorActionPreference = 'Stop'
if (-not ($IdleMinutes -gt 0) -or -not ($CapMinutes -gt 0) -or -not ($PollSeconds -gt 0)) {
    throw 'codex-wait: -IdleMinutes, -CapMinutes and -PollSeconds must be positive.'
}
$resultDir = Split-Path -Parent ([System.IO.Path]::GetFullPath($Result))
if (-not (Test-Path -LiteralPath $resultDir -PathType Container)) { throw "codex-wait: no folder for the result file: $resultDir" }
if (-not $SessionsRoot) {
    $codexHome = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $HOME '.codex' }
    $SessionsRoot = Join-Path $codexHome 'sessions'
}
if (-not $EditorLog) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { throw 'codex-wait: LOCALAPPDATA is empty; give -EditorLog.' }
    $EditorLog = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
}
if (-not $Repo) { $Repo = Split-Path -Parent $PSScriptRoot }
# codex-unity.ps1 leaves <prompt>.<RunId>.stderr.log next to the result file it will write last.
if ($Result -match '\.result\.json$') { $Watch = @($Watch) + ($Result -replace '\.result\.json$', '.stderr.log') }

# Today's and yesterday's folders: a run that crosses midnight keeps writing the log it opened the day before.
function Get-SessionLogs {
    $now = Get-Date
    foreach ($day in @($now, $now.AddDays(-1))) {
        $folder = Join-Path $SessionsRoot $day.ToString('yyyy\\MM\\dd')
        if (Test-Path -LiteralPath $folder -PathType Container) { Get-ChildItem -LiteralPath $folder -Filter '*.jsonl' -File }
    }
}

$started = Get-Date
function Get-TargetLogs {
    @(Get-SessionLogs) | Where-Object { if ($Thread) { $_.Name -like "*$Thread*" } else { $_.CreationTime -ge $started.AddMinutes(-2) } }
}

function Get-Signature {
    $sessionBytes = 0L
    foreach ($log in @(Get-TargetLogs)) { $sessionBytes += $log.Length }
    $editorBytes = if (Test-Path -LiteralPath $EditorLog -PathType Leaf) { (Get-Item -LiteralPath $EditorLog).Length } else { -1L }
    $watched = foreach ($path in $Watch) { if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-Item -LiteralPath $path).Length } else { -1L } }
    # stderr is not redirected: Windows PowerShell 5.1 turns redirected native stderr into errors that 'Stop' throws on.
    $status = (& git -C $Repo status --porcelain | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "codex-wait: git status failed in $Repo (exit $LASTEXITCODE)." }
    "$sessionBytes|$editorBytes|$($watched -join ',')|$status"
}

# The last few kilobytes of the newest session log, for the event types the run stopped on.
function Get-LastEvents {
    $newest = @(Get-TargetLogs) | Sort-Object CreationTime -Descending | Select-Object -First 1
    if ($null -eq $newest) { return '(no session log of this run; a review has none - see its -Watch file)' }
    $stream = [System.IO.File]::Open($newest.FullName, 'Open', 'Read', 'ReadWrite')
    try {
        $take = [int][Math]::Min(8192L, $stream.Length)
        [void]$stream.Seek(-$take, 'End')
        $buffer = New-Object byte[] $take
        [void]$stream.Read($buffer, 0, $take)
    }
    finally { $stream.Dispose() }
    $lines = [System.Text.Encoding]::UTF8.GetString($buffer) -split "`n" | Where-Object { $_.Trim() }
    $last = $lines | Select-Object -Last 1
    $types = [regex]::Matches([string]$last, '"type":"([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Select-Object -First 3
    "$($newest.Name): $($types -join ' / ')"
}

$clock = [System.Diagnostics.Stopwatch]::StartNew()
$last = Get-Signature
$quietSince = $clock.Elapsed.TotalSeconds
while ($true) {
    if (Test-Path -LiteralPath $Result -PathType Leaf) {
        Write-Output ("RESULT after {0:N0} s: {1}" -f $clock.Elapsed.TotalSeconds, $Result)
        Get-Content -LiteralPath $Result -Raw -Encoding UTF8
        exit 0
    }
    $age = $clock.Elapsed.TotalSeconds
    if ($age -ge $CapMinutes * 60) {
        Write-Output ("CAP: no result after {0:N1} min: {1}" -f ($age / 60), $Result)
        exit 4
    }
    Start-Sleep -Milliseconds ([int]($PollSeconds * 1000))
    # A result that landed during the sleep wins over a quiet spell that also ended during it.
    if (Test-Path -LiteralPath $Result -PathType Leaf) { continue }
    $now = Get-Signature
    if ($now -ne $last) {
        $last = $now
        $quietSince = $clock.Elapsed.TotalSeconds
        continue
    }
    $quiet = $clock.Elapsed.TotalSeconds - $quietSince
    if ($quiet -ge $IdleMinutes * 60) {
        Write-Output ("IDLE: nothing moved for {0:N1} min (run waited {1:N1} min, no result yet): {2}" -f ($quiet / 60), ($clock.Elapsed.TotalSeconds / 60), $Result)
        Write-Output ("last Codex events: " + (Get-LastEvents))
        exit 3
    }
}
