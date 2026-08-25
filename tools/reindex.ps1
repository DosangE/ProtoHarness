<#
  B모드 심볼 인덱스 생성기 — ProtoHarness

  실행:  powershell -ExecutionPolicy Bypass -File tools/reindex.ps1

  생성물: index/symbols.tsv, index/files.tsv
  이 스크립트는 Assets/ 바깥에 있어야 한다. 안으로 옮기면 Unity가 컴파일한다.
  읽기만 하며 Assets/ 는 절대 수정하지 않는다.

  주의: 이 파일은 UTF-8 with BOM 으로 저장해야 한다.
        Windows PowerShell 5.1 은 BOM 이 없으면 .ps1 을 ANSI(cp949)로 읽어 한글이 깨진다.
#>

$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$outDir  = Join-Path $root 'index'
$scanDir = Join-Path $root 'Assets'

# 우리 것이 아닌 영역. 경로는 슬래시로 정규화한 뒤 비교한다.
$excludes = @('/TutorialInfo/')

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$files = @()
if (Test-Path $scanDir) {
    $files = @(Get-ChildItem -Path $scanDir -Filter *.cs -Recurse -File | Where-Object {
        $norm = $_.FullName.Replace([char]92, [char]47)
        $keep = $true
        foreach ($ex in $excludes) { if ($norm -like ('*' + $ex + '*')) { $keep = $false } }
        $keep
    })
}

$reNamespace = '^\s*namespace\s+([A-Za-z_][\w.]*)'
$reType      = '^\s*(?:public\s+|internal\s+|private\s+|protected\s+)?(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)(?:\s*:\s*([^{]+))?'
$reMethod    = '^\s*(?:public|internal|protected)\s+(?:static\s+|virtual\s+|override\s+|async\s+|sealed\s+|readonly\s+)*[A-Za-z_][\w<>,.\[\]]*\s+([A-Za-z_]\w*)\s*\('
$reUnityMsg  = '^\s*(?:private\s+|protected\s+|public\s+)?(?:void|IEnumerator|System.Collections.IEnumerator)\s+(Awake|Start|OnEnable|OnDisable|Update|FixedUpdate|LateUpdate|OnDestroy|OnValidate|OnApplicationQuit)\s*\('
$reSerialize = '\[\s*SerializeField\s*\]'
$reField     = '([A-Za-z_]\w*)\s*(?:=[^=]|;)'
$reCreateSO  = '\[\s*CreateAssetMenu'
$reMenuName  = 'menuName\s*=\s*"([^"]*)"'

$rows     = New-Object System.Collections.Generic.List[string]
$fileRows = New-Object System.Collections.Generic.List[string]

foreach ($f in $files) {
    $rel = $f.FullName.Substring($root.Length + 1).Replace([char]92, [char]47)
    $lines = @(Get-Content -LiteralPath $f.FullName -Encoding UTF8)
    $ns = '-'
    $currentType = '-'
    $pendingSerialize = $false

    $fileRows.Add(("{0}`t{1}" -f $rel, $lines.Count))

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $n = $i + 1

        $m = [regex]::Match($line, $reNamespace)
        if ($m.Success) { $ns = $m.Groups[1].Value; continue }

        # [SerializeField] : 같은 줄 선언과 다음 줄 선언을 모두 처리한다.
        if ($line -match $reSerialize) {
            $rest = [regex]::Replace($line, $reSerialize, '')
            $fm = [regex]::Match($rest, $reField)
            if ($fm.Success) {
                $rows.Add(("serialized`t{0}`t{1}`t{2}`t{3}`t{4}" -f $fm.Groups[1].Value, $currentType, $rel, $n, $line.Trim()))
                $pendingSerialize = $false
            } else {
                $pendingSerialize = $true
            }
            continue
        }
        if ($pendingSerialize) {
            $fm = [regex]::Match($line, $reField)
            if ($fm.Success) {
                $rows.Add(("serialized`t{0}`t{1}`t{2}`t{3}`t{4}" -f $fm.Groups[1].Value, $currentType, $rel, $n, $line.Trim()))
            }
            $pendingSerialize = $false
            continue
        }

        if ($line -match $reCreateSO) {
            $mn = [regex]::Match($line, $reMenuName)
            $menu = '-'
            if ($mn.Success) { $menu = $mn.Groups[1].Value }
            $rows.Add(("createassetmenu`t{0}`t{1}`t{2}`t{3}`t{4}" -f $menu, $ns, $rel, $n, $line.Trim()))
            continue
        }

        $m = [regex]::Match($line, $reType)
        if ($m.Success) {
            $currentType = $m.Groups[2].Value
            $base = $m.Groups[3].Value.Trim()
            if ([string]::IsNullOrWhiteSpace($base)) { $base = '-' }
            $rows.Add(("{0}`t{1}`t{2}`t{3}`t{4}`t{5}" -f $m.Groups[1].Value, $currentType, $ns, $rel, $n, $base))
            continue
        }

        $m = [regex]::Match($line, $reUnityMsg)
        if ($m.Success) {
            $rows.Add(("unitymsg`t{0}`t{1}`t{2}`t{3}`t-" -f $m.Groups[1].Value, $currentType, $rel, $n))
            continue
        }

        $m = [regex]::Match($line, $reMethod)
        if ($m.Success) {
            $rows.Add(("method`t{0}`t{1}`t{2}`t{3}`t-" -f $m.Groups[1].Value, $currentType, $rel, $n))
        }
    }
}

$head = 'no-git'
try { $head = (& git -C $root rev-parse HEAD 2>$null).Trim() } catch { $head = 'no-git' }
if ([string]::IsNullOrWhiteSpace($head)) { $head = 'no-git' }
$stamp = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK')

$header = @(
    '# ProtoHarness B모드 심볼 인덱스 — 생성물. 직접 수정하지 말 것.',
    "# generated: $stamp",
    "# git-head: $head",
    "# cs-files: $($files.Count)",
    "# symbols: $($rows.Count)",
    "# STALE-CHECK: 'git rev-parse HEAD' 가 위 git-head 와 다르면 신뢰하지 말고 재생성한다.",
    '# EMPTY-CHECK: symbols 가 0 이면 이 인덱스로 없다고 결론내지 말 것. 코드가 아직 없다는 뜻일 뿐이다.',
    "kind`tname`tcontainer`tfile`tline`tdetail"
)

Set-Content -Path (Join-Path $outDir 'symbols.tsv') -Value ($header + $rows) -Encoding utf8
Set-Content -Path (Join-Path $outDir 'files.tsv') -Value (@("# generated: $stamp", "# git-head: $head", "file`tlines") + $fileRows) -Encoding utf8

Write-Output 'reindex 완료'
Write-Output ("  cs 파일  : {0}" -f $files.Count)
Write-Output ("  심볼     : {0}" -f $rows.Count)
Write-Output ("  git-head : {0}" -f $head)
Write-Output '  출력     : index/symbols.tsv, index/files.tsv'
if ($rows.Count -eq 0) {
    Write-Output ''
    Write-Output '  [경고] 심볼 0개. 아직 우리 코드가 없다는 뜻이다.'
    Write-Output '         이 인덱스로 해당 심볼 없음 이라고 결론내면 안 된다.'
}
