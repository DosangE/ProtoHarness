<#
  에이전트 정의 동기화 — ProtoHarness

  원본:   .claude/agents/*.md   (YAML frontmatter + 본문)
  생성물: .codex/agents/*.toml

  실행:  powershell -ExecutionPolicy Bypass -File tools/sync-agents.ps1
         powershell -ExecutionPolicy Bypass -File tools/sync-agents.ps1 -Check

  -Check 는 쓰지 않고 어긋난 파일만 보고한다(종료코드 1). 커밋 전 확인용.

  규칙 변경 시 손대는 파일은 .claude/agents/*.md 뿐이다. .codex 는 여기서 파생된다.

  한계 (확인 못 한 것): frontmatter 의 tools / model 을 Codex TOML 스키마의
  어떤 키에 대응시켜야 하는지 확인하지 못했다. 임의로 키를 만들지 않고
  주석으로만 남긴다. Codex 쪽 권한 제한은 별도로 확인해야 한다.

  주의: 이 파일은 UTF-8 with BOM 으로 저장한다. PowerShell 5.1 은 BOM 이 없으면
        .ps1 을 ANSI(cp949)로 읽어 한글이 깨진다.
#>

param([switch]$Check)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $root '.claude/agents'
$dst  = Join-Path $root '.codex/agents'

if (-not (Test-Path $src)) { throw "원본 폴더가 없다: $src" }
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$written = @()
$drift   = @()

foreach ($f in Get-ChildItem -Path $src -Filter *.md -File) {
    $lines = @(Get-Content -LiteralPath $f.FullName -Encoding UTF8)

    if ($lines.Count -lt 3 -or $lines[0].Trim() -ne '---') {
        throw "frontmatter 가 없다: $($f.Name)"
    }
    $end = -1
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { $end = $i; break }
    }
    if ($end -lt 0) { throw "frontmatter 가 닫히지 않았다: $($f.Name)" }

    $meta = @{}
    for ($i = 1; $i -lt $end; $i++) {
        $kv = $lines[$i]
        $c = $kv.IndexOf(':')
        if ($c -gt 0) { $meta[$kv.Substring(0, $c).Trim()] = $kv.Substring($c + 1).Trim() }
    }
    $body = ($lines[($end + 1)..($lines.Count - 1)] -join "`n").Trim()

    $name = $meta['name']
    $desc = $meta['description']
    if ([string]::IsNullOrWhiteSpace($name)) { throw "name 이 비었다: $($f.Name)" }
    if ([string]::IsNullOrWhiteSpace($desc)) { throw "description 이 비었다: $($f.Name)" }

    # TOML 리터럴 문자열은 이스케이프가 없다. 구분자가 본문에 있으면 조용히 깨지므로 즉시 멈춘다.
    $q3 = "'" + "'" + "'"
    if ($body.Contains($q3)) { throw "본문에 TOML 리터럴 구분자가 있다: $($f.Name)" }
    if ($desc.Contains("'"))  { throw "description 에 작은따옴표가 있다: $($f.Name)" }

    $out = New-Object System.Collections.Generic.List[string]
    $out.Add('# 생성물 — 직접 수정하지 말 것.')
    $out.Add("# 원본: .claude/agents/$($f.Name)")
    $out.Add('# 재생성: powershell -ExecutionPolicy Bypass -File tools/sync-agents.ps1')
    if ($meta.ContainsKey('model')) { $out.Add("# model: $($meta['model'])") }
    if ($meta.ContainsKey('tools')) { $out.Add("# tools: $($meta['tools'])") }
    $out.Add('')
    $out.Add("name = '$name'")
    $out.Add("description = '$desc'")
    $out.Add("developer_instructions = $q3")
    $out.Add($body)
    $out.Add($q3)

    $text = ($out -join "`n") + "`n"
    $target = Join-Path $dst ($name + '.toml')

    $same = $false
    if (Test-Path $target) {
        # autocrlf 로 작업 트리가 CRLF 이어도 내용이 같으면 일치로 본다 (줄바꿈만 다른 거짓 양성 방지).
        $existing = [System.IO.File]::ReadAllText($target, $utf8NoBom).Replace("`r`n", "`n")
        if ($existing -eq $text) { $same = $true }
    }

    if ($same) { continue }

    if ($Check) {
        $drift += ($name + '.toml')
    } else {
        [System.IO.File]::WriteAllText($target, $text, $utf8NoBom)
        $written += ($name + '.toml')
    }
}

# 원본이 사라진 생성물 탐지
$srcNames = @(Get-ChildItem -Path $src -Filter *.md -File | ForEach-Object {
    ((Get-Content -LiteralPath $_.FullName -Encoding UTF8 | Where-Object { $_ -match '^name:' }) -replace '^name:\s*', '').Trim()
})
$orphans = @(Get-ChildItem -Path $dst -Filter *.toml -File | Where-Object {
    $srcNames -notcontains $_.BaseName
} | ForEach-Object { $_.Name })

if ($Check) {
    if ($drift.Count -eq 0 -and $orphans.Count -eq 0) {
        Write-Output '동기화 상태: 일치'
        exit 0
    }
    Write-Output '동기화 상태: 어긋남'
    foreach ($d in $drift)   { Write-Output ("  갱신 필요 : " + $d) }
    foreach ($o in $orphans) { Write-Output ("  원본 없음 : " + $o + "  (원본이 지워졌는지 확인하고 수동으로 정리한다)") }
    exit 1
}

Write-Output 'sync-agents 완료'
if ($written.Count -eq 0) { Write-Output '  변경 없음 (이미 일치)' }
foreach ($w in $written) { Write-Output ("  생성/갱신 : " + $w) }
foreach ($o in $orphans) { Write-Output ("  [경고] 원본 없는 생성물 : " + $o + "  — 자동으로 지우지 않는다. 확인 후 수동 정리.") }
