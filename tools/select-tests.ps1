<#
  ① 빠른 확인·② 병합용 PlayMode 테스트 선택기 — ProtoHarness

  실행:  powershell -ExecutionPolicy Bypass -File tools/select-tests.ps1
         powershell -ExecutionPolicy Bypass -File tools/select-tests.ps1 -Range 75b998d^..75b998d

  -Range 를 주지 않으면 `dev...HEAD` + 커밋하지 않은 변경 + 추적하지 않는 파일을 본다.
  바뀐 파일마다 아래 표의 위에서부터 처음 맞는 줄을 쓰고, 고른 테스트 클래스·메서드의 합집합을
  Unity_RunCommand 에 넣을 TestRunnerApi 호출 한 줄로 출력한다. 하나라도 "전체" 면 전체다.

  ① 은 기본 범위로, ② 병합은 -Range dev...HEAD(커밋한 것만)로 돌린다 (CLAUDE.md §9-2, docs/RULES/VERIFICATION.md §4-4,
  docs/DECISIONS.md 2026-10-18 "병합은 바뀐 부분만").
  읽기만 하며 아무 파일도 쓰지 않는다. Assets/ 바깥에 있어야 한다(안으로 옮기면 Unity 가 컴파일한다).

  표는 2026-10-14 런타임 타입별 참조 grep 으로 정했다(docs/DECISIONS.md 2026-10-14 "테스트 3단").
  2026-10-26 부터 한 줄이 클래스 일부(테스트 이름 접두사, Tests)만 고를 수 있고, 시뮬이 읽지 않는 경로는
  지문을 넣지 않는다(Fingerprint = $false). 바뀐 것을 읽는 쪽만 고른다 (docs/DECISIONS.md 2026-10-26 "영향 범위 테스트").
  표에 없는 Assets/ 경로는 전체로 떨어진다. 테스트 클래스가 표와 어긋나면 경고하고 전체로 떨어진다.

  주의: 이 파일은 UTF-8 with BOM 으로 저장해야 한다.
        Windows PowerShell 5.1 은 BOM 이 없으면 .ps1 을 ANSI(cp949)로 읽어 한글이 깨진다.
#>

param(
    [string]$Range = ''
)

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$scripts   = 'Assets/_Project/Scripts/'
$runtime   = $scripts + 'Runtime/ChainRush/'
$playDir   = Join-Path $root 'Assets/_Project/Scripts/Tests/PlayMode'
$namespace = 'ProtoHarness.Tests.PlayMode'

# 지문: 서킷 리플레이 해시 (docs/PROPOSAL_TEST_TIERS.md 3-1). 테스트·Net 밖의 Assets/ 가 바뀌면 넣는다
# (Net 은 시뮬 어셈블리가 참조하지 않아 지문을 바꿀 수 없다). 표에서 Fingerprint = $false 인 줄(시뮬 코드가
# 참조하지 않는 타입, 2026-10-26 grep)은 넣지 않는다.
$fingerprint = 'ChainRushReplayTests.Replay_CircuitBotRun_RepeatsEveryTickBitForBit'

$net     = @('NetReconcileTests', 'NetRemoteTests', 'RaceOrderTests', 'RaceStandingsTests', 'RaceStandingsTextTests')
$course  = @('ChainRushCurveTests', 'ChainRushSlopeTests', 'ChainRushRoadTests', 'ChainRushSteeringTests', 'ChainRushDriftTests', 'ChainRushCornerSwingTests')

# 전체 실행에서만 도는 클래스, 이름으로 따로 돌리는 클래스. 표가 모든 클래스를 아는지 확인할 때 쓴다.
$allOnly      = @('ChainRushTests')
$explicitOnly = @('NetworkRaceTests', 'ChainRushProbeTests')

# 위에서부터 처음 맞는 줄을 쓴다. All = 전체, Classes = 돌릴 클래스, Tests = 클래스 일부('클래스.이름접두사정규식'),
# Fingerprint = $false 면 지문을 넣지 않는다, Note = 보고에 붙일 말.
$rules = @(
    @{ Pattern = '^(Packages|ProjectSettings)/';                    All = $true;  Why = '패키지·프로젝트 설정' }
    @{ Pattern = '^Assets/_Project/(Data|Scenes)/';                 All = $true;  Why = 'SO 값·씬은 시뮬에 들어간다' }
    @{ Pattern = '^' + $scripts + 'Editor/';                        All = $true;  Why = '씬 빌더·테스트 메뉴' }
    @{ Pattern = '\.asmdef$';                                       All = $true;  Why = '어셈블리 정의' }
    @{ Pattern = '^' + $scripts + 'Tests/PlayMode/(CircuitBot|ShortcutBot|TestRoad|TrackFollower|StateTrace)\.cs$'; All = $true; Why = '테스트 보조 파일' }
    @{ Pattern = '^' + $scripts + 'Tests/EditMode/';                Classes = @(); Why = 'EditMode 만 (EditMode 전체는 늘 돈다)' }
    @{ Pattern = '^' + $scripts + 'Tests/PlayMode/NetworkRaceTests\.cs$'; Classes = @(); Why = '네트워크 레이스'; Note = 'NetworkRaceTests 는 [Explicit] 이라 여기서 고르지 않는다. 플레이어 빌드 뒤 이름으로 따로 돌린다 (HANDOFF 1절)' }
    @{ Pattern = '^' + $scripts + 'Tests/PlayMode/(\w+)\.cs$';      Own = $true;  Why = '테스트 클래스 파일' }
    @{ Pattern = '^' + $scripts + 'Runtime/Net/';                   Classes = $net; Why = 'ProtoHarness.Net (다른 런타임·에디터 어셈블리가 참조하지 않는다)' }
    @{ Pattern = '^' + $runtime + '(RunnerMotor|GrappleController|ChainRushGame|RacerState|Ticks|SimSnapshot|RunRules|CourseTarget)\.cs$'; All = $true; Why = '코어 시뮬' }
    @{ Pattern = '^' + $runtime + 'Motion/';                        All = $true;  Why = '코어 시뮬 (RunnerMotor 가 부르는 이동 규칙)' }
    @{ Pattern = '^' + $runtime + 'Track/Centerline\.cs$';          All = $true;  Why = 'RunnerMotor·GrappleController 가 쓰는 트랙 좌표계' }
    @{ Pattern = '^' + $runtime + 'Track/(TrackFrame|TrackCoord)\.cs$'; All = $true; Why = 'RunnerMotor·GrappleController 가 쓰는 트랙 좌표계' }
    @{ Pattern = '^' + $runtime + 'Track/(LapCounter|TrackDefinition)\.cs$'; Classes = @('ChainRushCircuitTests') + $net; Why = '서킷 랩·정의 (RaceOrder·SimSnapshotCodec 이 쓴다)' }
    @{ Pattern = '^' + $runtime + 'Track/';                         Classes = $course + @('ChainRushPresentationTests', 'ChainRushReplayTests'); Why = '노면·트랙 조각' }
    @{ Pattern = '^' + $runtime + 'Race/';                          Classes = @('ChainRushCircuitTests', 'ChainRushWorldTests', 'ChainRushControllerCostTests', 'ChainRushReplayTests', 'ChainRushRollbackTests', 'ChainRushFlowTests') + $net; Why = '서킷 레이스·격리 월드 (RaceAssembler 가 정의를 바꿔 끼운다)' }
    @{ Pattern = '^' + $runtime + 'Flow/(GameSettings|ScreenOptions)\.cs$'; Classes = @('ChainRushPresentationTests'); Tests = @('ChainRushFlowTests.Lobby_'); Fingerprint = $false; Why = '설정 값 (로비 설정 페이지가 바꾸고 레이스에서는 ChainRushAudio·FollowCamera 가 읽는다. 시뮬은 읽지 않는다)' }
    @{ Pattern = '^' + $runtime + 'Flow/(Lobby(/|$)|LobbyMenu\.cs$|RunnerPreview\.cs$|TitleScreen\.cs$)'; Tests = @('ChainRushFlowTests.(Lobby|Loop)_', 'ChainRushFlowTests.Race_(Skyway|FingerLanes)FromTheLobby_'); Fingerprint = $false; Why = '타이틀·로비 화면 (로비 메뉴로 트랙을 고르는 레이스 2건 포함, Codex 리뷰 2026-10-26. 시뮬은 읽지 않는다)' }
    @{ Pattern = '^' + $runtime + 'Flow/';                          Classes = @('ChainRushFlowTests', 'ChainRushProfileTests'); Why = '타이틀·로비·레이스 조립 (DECISIONS 2026-10-19 "M2 씬 구성")' }
    @{ Pattern = '^' + $runtime + 'RunnerProfile\.cs$';             Classes = @('ChainRushProfileTests', 'ChainRushFlowTests', 'ChainRushReplayTests'); Why = '러너 프로필 (DECISIONS 2026-10-19 "M2-b 러너 프로필")' }
    @{ Pattern = '^' + $runtime + 'Control/(KeyboardMouseInputSource|InputLatch)\.cs$'; Classes = @('ChainRushInputSourceTests'); Why = '장치 입력 경로'; Note = '②에서 Run Device Input Tests 도 1회 (§9-2)' }
    @{ Pattern = '^' + $runtime + 'Control/(InputLog|InputLogFile|InputRecorder|InputReplay|TickInputCodec)\.cs$'; Classes = @('ChainRushReplayTests', 'ChainRushRollbackTests', 'ChainRushWorldTests') + $net; Why = '입력 기록·재생' }
    @{ Pattern = '^' + $runtime + 'Visuals/(NightUi|RunnerTilt|RunnerLook)\.cs$'; Classes = @('ChainRushPresentationTests', 'ChainRushInputSourceTests'); Fingerprint = $false; Why = '표현 (시뮬 참조는 RunnerProfile 의 RunnerLook 주석뿐)' }
    @{ Pattern = '^' + $runtime + 'Visuals/';                       Classes = @('ChainRushPresentationTests', 'ChainRushInputSourceTests'); Why = '표현 (RunnerTilt 를 쓰는 테스트 포함)' }
    @{ Pattern = '^' + $runtime + 'ChainRushHud\.cs$';              Tests = @('ChainRushFlowTests.Race_FromTheLobby_RacesTheChosenDefinitionAndCountsIn(\(|$)', 'ChainRushFlowTests.Race_PauseMenuFromTheLobby_ContinuesRestartsAndGoesBackToTheLobby(\(|$)', 'ChainRushFlowTests.Race_PauseMenuOpenedDirectly_OffersOnlyWhatTheRaceCanDo(\(|$)', 'ChainRushFlowTests.Race_OpenedDirectly_RunsAsSavedAndWaitsForEnter(\(|$)', 'ChainRushFlowTests.Race_SkywayFromTheLobby_BotFinishesTwoLaps(\(|$)', 'ChainRushFlowTests.Race_FingerLanesFromTheLobby_BotFinishesTwoLaps(\(|$)', 'ChainRushFlowTests.Lobby_TrackPage_'); Fingerprint = $false; Why = 'HUD 레이스 6건·로비 트랙 페이지 (TrackPage.FormatTime, 시뮬 참조는 주석뿐)' }
    @{ Pattern = '^' + $runtime + 'Audio/UiSound\.cs$';              Classes = @('ChainRushPresentationTests'); Fingerprint = $false; Why = 'UI 효과음 (시뮬 참조 없음)' }
    @{ Pattern = '^' + $runtime + '(Audio/|FollowCamera\.cs$)';     Classes = @('ChainRushPresentationTests'); Why = '표현' }
    @{ Pattern = '^Assets/';                                        All = $true;  Why = '표에 없는 경로' }
)

function Invoke-GitLines([string[]]$gitArgs) {
    $out = & git -C $root @gitArgs
    if ($LASTEXITCODE -ne 0) { throw "git $($gitArgs -join ' ') 실패 (exit $LASTEXITCODE)" }
    return $out
}

function Invoke-Git([string[]]$gitArgs) {
    $out = Invoke-GitLines $gitArgs
    return @($out | Where-Object { $_ -ne '' })
}

# 프로젝트의 8칸 멤버 들여쓰기를 따른다. 해석 못 한 형태는 범위를 만들지 않는다.
function Get-TestMethods([string[]]$lines) {
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch '^        \[.+\]\s*$') { continue }
        $start = $i
        $isTest = $false
        while ($i -lt $lines.Count -and $lines[$i] -match '^        \[.+\]\s*$') {
            if ($lines[$i] -match '(?:\[|,\s*)(?:Test|UnityTest)\s*(?:\(|,|\])') { $isTest = $true }
            $i++
        }
        if (-not $isTest -or $i -ge $lines.Count) { continue }
        if ($lines[$i] -notmatch '^        (?:public|private|protected|internal)\s+(?:(?:static|async|virtual|override|sealed|new)\s+)*[\w.<>,\[\]?]+\s+(\w+)\s*\([^{};]*\)\s*$') { continue }
        $name = $Matches[1]
        if ($i + 1 -ge $lines.Count -or $lines[$i + 1] -notmatch '^        \{\s*$') { continue }
        $end = $i + 2
        while ($end -lt $lines.Count -and $lines[$end] -notmatch '^        \}\s*$') {
            # 다른 멤버/특성이 나오면 닫는 괄호를 확정할 수 없다.
            if ($lines[$end] -match '^        \S') { break }
            $end++
        }
        if ($end -ge $lines.Count -or $lines[$end] -notmatch '^        \}\s*$') { continue }
        while ($start -gt 0 -and $lines[$start - 1] -match '^        //') { $start-- }
        [pscustomobject]@{ Name = $name; Start = $start + 1; End = $end + 1 }
        $i = $end
    }
}

function Get-OwnTestSelection([string]$path) {
    $fallback = @{ Tests = @(); Why = '범위를 확정하지 못해 클래스 전체' }
    try {
        if ($Range -ne '') {
            if ($Range -notmatch '^([^\s]+?)(\.{2,3})([^\s]+)$') {
                $fallback.Why = '범위 형식이 a...b/a..b 가 아니어서 클래스 전체'
                return $fallback
            }
            $left = $Matches[1]; $dots = $Matches[2]; $right = $Matches[3]
            if ($left.Contains('..') -or $right.Contains('..') -or $left.StartsWith('-') -or $right.StartsWith('-')) { return $fallback }
            $left = @(Invoke-Git @('rev-parse', '--verify', "$left^{commit}"))[0]
            $right = @(Invoke-Git @('rev-parse', '--verify', "$right^{commit}"))[0]
            $base = if ($dots -eq '...') { @(Invoke-Git @('merge-base', $left, $right))[0] } else { $left }
            $diffArgs = @('diff', '--no-ext-diff', '--no-textconv', '--no-renames', '-U0', $base, $right, '--', $path)
        } else {
            $base = @(Invoke-Git @('merge-base', 'dev', 'HEAD'))[0]
            $diffArgs = @('diff', '--no-ext-diff', '--no-textconv', '--no-renames', '-U0', $base, '--', $path)
        }
        $diff = @(Invoke-GitLines $diffArgs)
        if ($diff -match '^(new file mode|deleted file mode|Binary files)') {
            $fallback.Why = '파일 추가·삭제 또는 바이너리 diff 로 클래스 전체'
            return $fallback
        }
        $hunks = @($diff | ForEach-Object {
            if ($_ -match '^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@') {
                [pscustomobject]@{
                    OldStart = [int]$Matches[1]
                    OldCount = $(if ([string]::IsNullOrEmpty($Matches[2])) { 1 } else { [int]$Matches[2] })
                    Start = [int]$Matches[3]
                    Count = $(if ([string]::IsNullOrEmpty($Matches[4])) { 1 } else { [int]$Matches[4] })
                }
            }
        })
        if ($hunks.Count -eq 0) { return $fallback }
        $lines = if ($Range -ne '') { @(Invoke-GitLines @('show', "$right`:$path")) }
                 else { @(Get-Content -LiteralPath (Join-Path $root $path) -Encoding UTF8) }
        $oldLines = @(Invoke-GitLines @('show', "$base`:$path"))
        $methods = @(Get-TestMethods $lines)
        $oldMethods = @(Get-TestMethods $oldLines)
        if ($methods.Count -eq 0 -or $oldMethods.Count -eq 0) { return $fallback }
        foreach ($oldMethod in $oldMethods) {
            if ($methods.Name -notcontains $oldMethod.Name) {
                $fallback.Why = '테스트 메서드 삭제·이름 변경으로 클래스 전체'
                return $fallback
            }
        }
        $picked = New-Object System.Collections.Generic.List[string]
        foreach ($hunk in $hunks) {
            $first = [Math]::Max(1, $hunk.Start)
            $last = $first + [Math]::Max(1, $hunk.Count) - 1
            if ($last -gt $lines.Count) { return $fallback }
            # 삭제 위치가 직전 테스트의 닫는 괄호에 걸려도, 보조 함수/필드 삭제를 놓치지 않는다.
            for ($oldLine = $hunk.OldStart; $oldLine -lt $hunk.OldStart + $hunk.OldCount; $oldLine++) {
                $oldInside = @($oldMethods | Where-Object { $oldLine -ge $_.Start -and $oldLine -le $_.End })
                if ($oldInside.Count -ne 1) {
                    $fallback.Why = "메서드 밖이 바뀌어 클래스 전체: $first (삭제 전 $oldLine)"
                    return $fallback
                }
            }
            # 새 메서드 전체를 추가한 hunk 앞뒤의 빈 줄만 함께 인정한다(사용자 승인 2026-10-26).
            $added = @($methods | Where-Object {
                $hunk.OldCount -eq 0 -and $_.Start -ge $first -and $_.End -le $last -and $oldMethods.Name -notcontains $_.Name
            })
            for ($line = $first; $line -le $last; $line++) {
                $inside = @($methods | Where-Object { $line -ge $_.Start -and $line -le $_.End })
                if ($inside.Count -eq 1) { $picked.Add($inside[0].Name); continue }
                $separator = $false
                if ($hunk.Count -gt 0 -and [string]::IsNullOrWhiteSpace($lines[$line - 1])) {
                    foreach ($method in $added) {
                        $from = [Math]::Min($line, $method.Start)
                        $to = [Math]::Max($line, $method.End)
                        $onlyBlank = $true
                        for ($n = $from; $n -le $to; $n++) {
                            if ($n -ge $method.Start -and $n -le $method.End) { continue }
                            if (-not [string]::IsNullOrWhiteSpace($lines[$n - 1])) { $onlyBlank = $false; break }
                        }
                        if ($onlyBlank) { $separator = $true; break }
                    }
                }
                if (-not $separator) {
                    $fallback.Why = "메서드 밖이 바뀌어 클래스 전체: $line"
                    return $fallback
                }
            }
        }
        if ($picked.Count -eq 0) { return $fallback }
        return @{ Tests = @($picked | Sort-Object -Unique); Why = '테스트 메서드만 바뀜' }
    } catch {
        Write-Warning "테스트 메서드 선택 실패: $path — $($_.Exception.Message) (클래스 전체로 실행)"
        return $fallback
    }
}

# 1. 바뀐 파일
if ($Range -ne '') {
    $source = "git diff --name-only $Range"
    $changed = Invoke-Git @('diff', '--name-only', $Range)
} else {
    $source = 'git diff --name-only dev...HEAD + 커밋하지 않은 변경 + 추적하지 않는 파일'
    $changed = (Invoke-Git @('diff', '--name-only', 'dev...HEAD')) +
               (Invoke-Git @('diff', '--name-only', 'HEAD')) +
               (Invoke-Git @('ls-files', '--others', '--exclude-standard'))
}
$changed = @($changed | Sort-Object -Unique)

# 2. 표가 PlayMode 테스트 클래스를 모두 아는지 확인한다
$present = @(Get-ChildItem -Path $playDir -Filter *.cs -File | ForEach-Object {
    $m = Select-String -LiteralPath $_.FullName -Pattern '\bclass\s+(\w+Tests)\b' -Encoding UTF8 | Select-Object -First 1
    if ($m) { $m.Matches[0].Groups[1].Value }
})
$known = @($allOnly + $explicitOnly + ($rules | ForEach-Object { $_.Classes; $_.Tests | ForEach-Object { ($_ -split '\.')[0] } }) | Where-Object { $_ } | Sort-Object -Unique)
$warnings = New-Object System.Collections.Generic.List[string]
foreach ($c in $present) { if ($known -notcontains $c) { $warnings.Add("표에 없는 테스트 클래스: $c (표에 배정하기 전까지 전체로 돈다)") } }
foreach ($c in $known)   { if ($present -notcontains $c) { $warnings.Add("표에는 있으나 PlayMode 폴더에 없는 클래스: $c (표를 고친다)") } }

# 3. 파일마다 규칙을 고른다
$all = $warnings.Count -gt 0
$touchesAssets = $false
$touchesNonTest = $false
$selected = New-Object System.Collections.Generic.List[string]
$partial = New-Object System.Collections.Generic.List[string]
$notes = New-Object System.Collections.Generic.List[string]

Write-Output "바뀐 파일 ($source): $($changed.Count)개"
foreach ($path in $changed) {
    $p = $path -replace '\.meta$', ''
    $rule = $null
    foreach ($r in $rules) { if ($p -match $r.Pattern) { $rule = $r; break } }

    if ($rule -eq $null) {
        Write-Output "  $path  →  없음 (Unity 가 읽지 않는다)"
        continue
    }
    if ($p -like 'Assets/*') { $touchesAssets = $true }
    if ($p -like 'Assets/*' -and $p -notlike ($scripts + 'Tests/*') -and $p -notlike ($scripts + 'Runtime/Net/*') -and $rule.Fingerprint -ne $false) { $touchesNonTest = $true }

    if ($rule.All) {
        $all = $true
        Write-Output "  $path  →  전체 ($($rule.Why))"
    } else {
        $classes = @($rule.Classes | Where-Object { $_ })
        $tests = @($rule.Tests | Where-Object { $_ })
        $why = $rule.Why
        if ($rule.Own) {
            $own = $Matches[1]
            if ($present -contains $own) {
                $classes = @($own)
                if ($path -eq $p) {
                    $choice = Get-OwnTestSelection $path
                    $why = $choice.Why
                    if ($choice.Tests.Count -gt 0) {
                        $classes = @()
                        $tests = @($choice.Tests | ForEach-Object { "$own.$_(\(|$)" })
                    }
                }
            }
            else { $all = $true; $warnings.Add("테스트 클래스 파일인데 클래스를 못 찾았다: $path (전체)") }
        }
        foreach ($c in $classes) { $selected.Add($c) }
        foreach ($t in $tests) { $partial.Add($t) }
        $parts = @($classes) + @($tests | ForEach-Object {
            if ($_.EndsWith('(\(|$)')) { $_.Substring(0, $_.Length - 6) } else { "$_*" }
        })
        $shown = if ($parts.Count -gt 0) { $parts -join ', ' } else { 'PlayMode 없음' }
        if ($rule.Fingerprint -eq $false) { $shown += ', 지문 없음' }
        Write-Output "  $path  →  $shown ($why)"
    }
    if ($rule.Note) { $notes.Add($rule.Note) }
}

foreach ($w in $warnings) { Write-Output "경고: $w" }
foreach ($n in ($notes | Sort-Object -Unique)) { Write-Output "참고: $n" }

# 4. 결과
Write-Output ''
Write-Output '고른 테스트 (① 빠른 확인 / ② 병합은 -Range dev...HEAD 로):'
Write-Output '  EditMode: 전체 (Enemy 카테고리 제외: categoryNames = { "!Enemy" })'

if ($all) {
    Write-Output '  PlayMode: 전체. 메뉴 ProtoHarness > Chain Rush > Run PlayMode Tests'
    exit 0
}
if (-not $touchesAssets) {
    Write-Output '  PlayMode: 없음 (Assets/·Packages/·ProjectSettings/ 변경 없음)'
    exit 0
}

$classes = @($selected | Sort-Object -Unique)
# 클래스 전체를 고른 곳의 일부는 뺀다.
$tests = @($partial | Sort-Object -Unique | Where-Object { $classes -notcontains ($_ -split '\.')[0] })
$groups = @($classes | ForEach-Object { "^$namespace.$_`$" }) + @($tests | ForEach-Object { "^$namespace.$_" })
if ($touchesNonTest) { $groups += "^$namespace.$fingerprint`$" }
if ($groups.Count -eq 0) {
    Write-Output '  PlayMode: 없음 (PlayMode 에서 고를 테스트가 없다)'
    exit 0
}
$quoted = ($groups | ForEach-Object { '"' + $_.Replace('\', '\\').Replace('"', '\"') + '"' }) -join ', '

$fpText = if ($touchesNonTest) { ' + 지문 1건' } else { ' (시뮬이 읽는 경로가 바뀌지 않아 지문 없음)' }
$partText = if ($tests.Count -gt 0) { " + 클래스 일부 $($tests.Count)개($($tests -join ', '))" } else { '' }
Write-Output "  PlayMode: 클래스 $($classes.Count)개$partText$fpText"
Write-Output '  Unity_RunCommand 로 실행 (결과 XML 은 메뉴 실행과 같은 파일):'
Write-Output ('  ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, assemblyNames = new[] { "ProtoHarness.Tests.PlayMode" }, groupNames = new[] { ' + $quoted + ' }, categoryNames = new[] { "!Device", "!Enemy", "!NetEndReplay" } }));')
