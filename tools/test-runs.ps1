<#
  테스트 실행 기록 — ProtoHarness

  실행이 끝날 때마다 결과 XML 사본과 그 실행의 기준점(HEAD, 커밋하지 않은 Assets/ 파일의 해시), 필터, 에디터 버전을
  묶어 남기고, "마지막 통과 이후 Assets/ 에서 바뀐 파일" 을 계산해 보여 준다 (docs/RULES/VERIFICATION.md §4-4,
  docs/DECISIONS.md 2026-11-09 "테스트 실행 기록").

  실행 (PowerShell 도구에서. Bash 에서 부르면 출력이 cp949 다):
    & .\tools\test-runs.ps1 -Record -Xml <결과 XML> -Filter "<돌린 Filter 코드>" -StartedAt <실행 직전 시각, ISO 8601>
                            [-EditorLog <에디터 로그>]    # batch 실행이면 -logFile 경로
    & .\tools\test-runs.ps1 -Last [-Passed] [-Platform PlayMode|EditMode]
    & .\tools\test-runs.ps1 -List [-Count 10]

  -Record 는 실행이 끝난 직후에 부른다. 실패한 실행도 남긴다(CLAUDE.md §9-2: 병합 보고에는 각 실행의 결과 XML 값을 모두 적는다).
  XML 의 start-time 이 -StartedAt 보다 이르면(이번 실행이 XML 을 쓰지 못하고 옛 파일이 남은 경우) 기록하지 않고 실패한다.
  기록 시점의 작업 트리를 그 실행의 코드로 본다. 실행과 기록 사이에 Assets/ 를 고치지 않는다.

  기록은 리포 밖 이 PC 의 $env:LOCALAPPDATA\ProtoHarness\test-runs 에 둔다(에디터 버전이 PC 마다 달라 기록도 PC 마다 다르다).
  리포의 파일은 읽기만 한다. Assets/ 바깥에 있어야 한다(안으로 옮기면 Unity 가 컴파일한다).

  "바뀐 파일" 은 Assets/ 만 본다(.meta 포함). 파일마다 기록 때의 내용(그때 커밋하지 않은 파일은 그때의 해시, 나머지는
  기록 HEAD 의 blob)과 지금 작업 트리의 내용을 git 해시로 비교한다. 그래서 실행 뒤 같은 내용을 커밋한 것은 바뀐 것으로 치지 않는다.
  한계: Packages/·ProjectSettings/ 는 보지 않는다(이 PC 의 버전 드리프트가 늘 바뀐 것으로 보이기 때문. 바꾼 브랜치는 영향 평가로 본다).

  주의: 이 파일은 UTF-8 with BOM 으로 저장한다. Windows PowerShell 5.1 은 BOM 이 없으면 .ps1 을 ANSI(cp949)로 읽어 한글이 깨진다.
#>

param(
    [switch]$Record,
    [switch]$Last,
    [switch]$List,
    [string]$Xml,
    [string]$Filter,
    [string]$StartedAt,
    [string]$EditorLog,
    [switch]$Passed,
    [ValidateSet('', 'PlayMode', 'EditMode')][string]$Platform = '',
    [int]$Count = 10,
    # 검증용: 다른 작업 트리와 기록 폴더로 돌린다.
    [string]$RepoRoot,
    [string]$StoreDir
)

$ErrorActionPreference = 'Stop'

$modes = @($Record, $Last, $List | Where-Object { $_ })
if ($modes.Count -ne 1) { throw '-Record, -Last, -List 중 하나만 준다.' }

$root = if ($RepoRoot) { (Resolve-Path -LiteralPath $RepoRoot).Path } else { Split-Path -Parent $PSScriptRoot }
if (-not $StoreDir) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { throw 'LOCALAPPDATA 가 비어 있어 기록 폴더를 정하지 못했다. -StoreDir 로 지정한다.' }
    $StoreDir = Join-Path $env:LOCALAPPDATA 'ProtoHarness\test-runs'
}
$repoKey = $root.Replace('\', '/').TrimEnd('/').ToLowerInvariant()

function Invoke-Git([string[]]$gitArgs) {
    $out = & git -C $root -c core.quotepath=false @gitArgs
    if ($LASTEXITCODE -ne 0) { throw "git $($gitArgs -join ' ') 실패 (exit $LASTEXITCODE)" }
    return $out
}

# -z 출력은 NUL 로 나뉜다. PowerShell 은 줄바꿈으로만 나누므로 다시 붙여 NUL 로 자른다.
function Invoke-GitZ([string[]]$gitArgs) {
    $out = Invoke-Git $gitArgs
    return @((@($out) -join "`n").Split([char]0) | Where-Object { $_ -ne '' })
}

# 커밋하지 않은 Assets/ 파일: 경로 → @{ Status; Hash } (지워진 파일은 Hash = $null).
function Get-DirtyAssets {
    $entries = Invoke-GitZ @('status', '--porcelain=v1', '--no-renames', '-uall', '-z', '--', 'Assets')
    $dirty = @{}
    foreach ($e in $entries) {
        if ($e.Length -lt 4) { throw "git status 줄을 해석하지 못했다: '$e'" }
        $dirty[$e.Substring(3)] = @{ Status = $e.Substring(0, 2); Hash = $null }
    }
    $present = @($dirty.Keys | Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf } | Sort-Object)
    $hashes = Get-WorktreeHashes $present
    foreach ($p in $present) { $dirty[$p].Hash = $hashes[$p] }
    return $dirty
}

# 작업 트리 파일의 git blob 해시 (clean 필터 적용 — 커밋하면 들어갈 blob 과 같다).
function Get-WorktreeHashes([string[]]$paths) {
    $map = @{}
    if ($paths.Count -eq 0) { return $map }
    # --stdin-paths 로 파이프하지 않는다: PowerShell 5.1 은 $OutputEncoding 이 BOM 없는 UTF-8 이어도 첫 줄에 BOM 을 붙여
    # 첫 경로를 찾지 못한다(2026-11-09 실측). 인자로 넘기되 명령줄 길이 때문에 나눠서.
    $out = @()
    for ($i = 0; $i -lt $paths.Count; $i += 100) {
        $chunk = @($paths[$i..([Math]::Min($i + 99, $paths.Count - 1))])
        $out += @(& git -C $root hash-object -- @chunk)
        if ($LASTEXITCODE -ne 0) { throw "git hash-object 실패 (exit $LASTEXITCODE)" }
    }
    if ($out.Count -ne $paths.Count) { throw "git hash-object 가 경로 $($paths.Count)개에 해시 $($out.Count)개를 냈다." }
    for ($i = 0; $i -lt $paths.Count; $i++) { $map[$paths[$i]] = $out[$i].Trim() }
    return $map
}

# 커밋의 Assets/ blob: 경로 → 해시.
function Get-TreeHashes([string]$commit) {
    $map = @{}
    foreach ($e in (Invoke-GitZ @('ls-tree', '-r', '-z', $commit, '--', 'Assets'))) {
        # "<mode> blob <hash>\t<path>"
        $tab = $e.IndexOf("`t")
        if ($tab -lt 0) { throw "git ls-tree 줄을 해석하지 못했다: '$e'" }
        $meta = $e.Substring(0, $tab).Split(' ')
        $map[$e.Substring($tab + 1)] = $meta[2]
    }
    return $map
}

function Read-Records {
    if (-not (Test-Path -LiteralPath $StoreDir)) { return @() }
    $all = foreach ($f in (Get-ChildItem -LiteralPath $StoreDir -Filter '*.json' -File)) {
        $r = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($r.repoRoot -eq $repoKey) { $r }
    }
    if ($null -eq $all) { return @() }
    return @($all | Sort-Object { $_.startTime })
}

function Format-Run($r) {
    return "$($r.startTime) ($($r.platform)) testcasecount=$($r.testcasecount) result=$($r.result) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped)"
}

if ($Record) {
    if (-not $Xml) { throw '-Record 에는 -Xml 이 필요하다.' }
    if ([string]::IsNullOrWhiteSpace($Filter)) { throw '-Record 에는 -Filter 가 필요하다 (돌린 Filter 코드 또는 메뉴 이름).' }
    if (-not $StartedAt) { throw '-Record 에는 -StartedAt 이 필요하다 (실행을 걸기 직전 시각).' }
    if (-not (Test-Path -LiteralPath $Xml -PathType Leaf)) { throw "결과 XML 이 없다: $Xml (실행이 XML 을 쓰지 못했으면 기록하지 않고 그 사실을 보고한다)" }

    $started = [DateTimeOffset]::Parse($StartedAt, [Globalization.CultureInfo]::InvariantCulture).ToUniversalTime()
    # XML 의 start-time 은 초 단위라 비교 전에 초 아래를 버린다.
    $startedSec = $started.AddTicks(-($started.Ticks % [TimeSpan]::TicksPerSecond))

    [xml]$doc = Get-Content -LiteralPath $Xml -Raw -Encoding UTF8
    $run = $doc.'test-run'
    if ($null -eq $run) { throw "test-run 요소가 없다: $Xml" }
    $fmt = "yyyy-MM-dd HH:mm:ss'Z'"
    $styles = [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal
    $xmlStart = [DateTimeOffset]::ParseExact($run.'start-time', $fmt, [Globalization.CultureInfo]::InvariantCulture, $styles)
    if ($xmlStart -lt $startedSec) {
        throw "XML 의 start-time $($run.'start-time') 이 -StartedAt $($started.ToString('u')) 보다 이르다. 이번 실행의 XML 이 아니다(실행이 중단됐는지 Console 을 본다)."
    }

    $platformProp = $doc.SelectSingleNode("/test-run/test-suite/properties/property[@name='platform']")
    if ($null -eq $platformProp) { throw "XML 에서 platform 속성을 찾지 못했다: $Xml" }
    $runPlatform = $platformProp.GetAttribute('value')

    $log = if ($EditorLog) { $EditorLog } else { Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log' }
    if (-not (Test-Path -LiteralPath $log -PathType Leaf)) { throw "에디터 로그가 없다: $log (-EditorLog 로 지정한다)" }
    $versionLine = Get-Content -LiteralPath $log -TotalCount 50 | Where-Object { $_ -match "Version is '([^ ']+)" } | Select-Object -First 1
    if ($null -eq $versionLine) { throw "에디터 로그 앞 50줄에서 'Version is' 를 찾지 못했다: $log" }
    $null = $versionLine -match "Version is '([^ ']+)"
    $editorVersion = $Matches[1]

    $startKey = $xmlStart.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $existing = @(Read-Records | Where-Object { $_.startTime -eq $startKey -and $_.platform -eq $runPlatform })
    if ($existing.Count -gt 0) { throw "같은 실행($startKey, $runPlatform)이 이미 기록돼 있다: $($existing[0].xmlCopy)" }

    $head = @(Invoke-Git @('rev-parse', 'HEAD'))[0].Trim()
    $branch = @(Invoke-Git @('branch', '--show-current'))[0]
    $dirty = Get-DirtyAssets
    $dirtyList = @($dirty.Keys | Sort-Object | ForEach-Object { [ordered]@{ path = $_; status = $dirty[$_].Status; hash = $dirty[$_].Hash } })
    $tests = @($doc.SelectNodes('//test-case') | ForEach-Object { $_.GetAttribute('fullname') })

    if (-not (Test-Path -LiteralPath $StoreDir)) { New-Item -ItemType Directory -Path $StoreDir | Out-Null }
    $base = $xmlStart.ToString('yyyyMMddTHHmmssZ') + '-' + $runPlatform + '-' + $head.Substring(0, 7)
    $xmlCopy = Join-Path $StoreDir ($base + '.xml')
    Copy-Item -LiteralPath $Xml -Destination $xmlCopy

    $rec = [ordered]@{
        repoRoot      = $repoKey
        startTime     = $startKey
        endTime       = $run.'end-time'
        startedAt     = $started.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
        recordedAt    = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
        platform      = $runPlatform
        testcasecount = [int]$run.testcasecount
        result        = $run.result
        passed        = [int]$run.passed
        failed        = [int]$run.failed
        skipped       = [int]$run.skipped
        inconclusive  = [int]$run.inconclusive
        duration      = [double]$run.duration
        filter        = $Filter
        head          = $head
        branch        = $branch
        editorVersion = $editorVersion
        editorLog     = $log
        dirtyAssets   = $dirtyList
        tests         = $tests
        xmlCopy       = $xmlCopy
    }
    $json = $rec | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $StoreDir ($base + '.json')), $json, (New-Object System.Text.UTF8Encoding($false)))

    Write-Output "기록했다: $(Format-Run ([pscustomobject]$rec))"
    Write-Output "  기준: $($head.Substring(0, 7)) ($branch), 커밋하지 않은 Assets/ 파일 $($dirtyList.Count)개, 에디터 $editorVersion"
    Write-Output "  XML 사본: $xmlCopy"
    exit 0
}

$records = @(Read-Records)
if ($Platform) { $records = @($records | Where-Object { $_.platform -eq $Platform }) }

if ($List) {
    if ($records.Count -eq 0) { Write-Output "기록 없음 ($StoreDir)"; exit 0 }
    foreach ($r in @($records | Select-Object -Last $Count)) {
        $f = if ($r.filter.Length -gt 80) { $r.filter.Substring(0, 80) + '…' } else { $r.filter }
        Write-Output "$(Format-Run $r) $($r.head.Substring(0, 7)) ($($r.branch)) 커밋 안 한 Assets $(@($r.dirtyAssets).Count)개 | $f"
    }
    exit 0
}

# -Last
if ($Passed) { $records = @($records | Where-Object { $_.result -eq 'Passed' -and [int]$_.failed -eq 0 }) }
if ($records.Count -eq 0) {
    $what = if ($Passed) { '통과한 실행' } else { '실행' }
    Write-Output "기록된 $what 없음 ($StoreDir). 마지막 실행을 기록에서 찾지 못했다 — 범위는 영향 평가로 정한다."
    exit 0
}
$r = $records[-1]
$label = if ($Passed) { '마지막 통과 실행' } else { '마지막 실행' }
Write-Output "${label}: $(Format-Run $r)"
Write-Output "  필터: $($r.filter)"
Write-Output "  기준: $($r.head.Substring(0, 7)) ($($r.branch)), 그때 커밋하지 않은 Assets/ 파일 $(@($r.dirtyAssets).Count)개, 에디터 $($r.editorVersion)"
Write-Output "  XML 사본: $($r.xmlCopy)"

$null = & git -C $root rev-parse --verify --quiet "$($r.head)^{commit}"
if ($LASTEXITCODE -ne 0) { throw "기록의 HEAD $($r.head) 를 이 저장소에서 찾지 못했다(리베이스·다른 clone?). 바뀐 파일을 계산할 수 없다." }

# 기록 때의 내용: 그때 커밋하지 않은 파일은 그때의 해시, 나머지는 기록 HEAD 의 blob.
$then = Get-TreeHashes $r.head
$thenDirty = @{}
foreach ($d in @($r.dirtyAssets)) { $thenDirty[$d.path] = $d.hash; $then[$d.path] = $d.hash }

$nowDirty = Get-DirtyAssets
$candidates = @{}
foreach ($p in (Invoke-GitZ @('diff', '--name-only', '--no-renames', '-z', $r.head, 'HEAD', '--', 'Assets'))) { $candidates[$p] = $true }
foreach ($p in $nowDirty.Keys) { $candidates[$p] = $true }
foreach ($p in $thenDirty.Keys) { $candidates[$p] = $true }

$present = @($candidates.Keys | Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf } | Sort-Object)
$nowHashes = Get-WorktreeHashes $present
$changed = @(foreach ($p in ($candidates.Keys | Sort-Object)) {
    $a = $then[$p]; $b = $nowHashes[$p]
    if ($a -ne $b) {
        $how = if ($null -eq $a) { '새로 생김' } elseif ($null -eq $b) { '지워짐' } else { '내용 바뀜' }
        $where = if ($nowDirty.ContainsKey($p)) { '작업 트리' } else { '커밋됨' }
        "    $p  ($how, $where)"
    }
})

if ($changed.Count -eq 0) { Write-Output '그 뒤 Assets/ 에서 바뀐 파일: 없음' }
else {
    Write-Output "그 뒤 Assets/ 에서 바뀐 파일: $($changed.Count)개"
    $changed | ForEach-Object { Write-Output $_ }
}
Write-Output "지금 커밋하지 않은 Assets/ 변경: $($nowDirty.Count)개"

if ($changed.Count -gt 0) {
    Write-Output '판정: 실행한 코드와 지금 코드가 다르다. 위 파일을 읽는 테스트만 다시 돌린다 (VERIFICATION §4-4).'
} elseif ($nowDirty.Count -gt 0) {
    Write-Output '판정: 실행한 코드 = 지금 작업 트리. 아직 커밋하지 않았다 — 커밋한 뒤 다시 보면 커밋한 코드와 같은지 나온다.'
} else {
    Write-Output '판정: 실행한 코드 = 지금 커밋한 코드 (CLAUDE.md §9-2 "작업 중 실행을 다시 쓴다" 의 근거).'
}
exit 0
