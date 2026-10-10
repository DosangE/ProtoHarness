<#
  SessionStart 훅 — 세션 시작 때 사실 몇 줄을 컨텍스트에 넣는다. 읽기 전용.

    · 현재 브랜치가 main/dev 인가           (CLAUDE.md §9-2)
    · index/symbols.tsv 의 git-head 가 현재 HEAD 와 같은가   (docs/RULES/LOOKUP.md §6-2)

  인덱스를 자동으로 다시 만들지 않는다 (DECISIONS 2026-08-25 "빈 인덱스는 사용을 거부한다").
  출력은 평문 stdout (exit 0). 알릴 것이 없으면 아무것도 출력하지 않는다.

  주의: 이 파일은 UTF-8 with BOM 으로 저장한다 (Windows PowerShell 5.1).
#>

$ErrorActionPreference = 'Stop'

try {
    $root = $env:CLAUDE_PROJECT_DIR
    if ([string]::IsNullOrWhiteSpace($root)) { $root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }

    $lines = New-Object System.Collections.Generic.List[string]

    $branch = (& git -C $root branch --show-current 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "현재 브랜치를 읽지 못했다: $branch" }
    $branch = ([string]$branch).Trim()
    if ($branch -eq 'main' -or $branch -eq 'dev') {
        $lines.Add("[하네스] 현재 브랜치: $branch (보호 브랜치). 직접 커밋 금지 — 브랜치부터 만든다 (CLAUDE.md §9-2).")
    }

    $head = (& git -C $root rev-parse HEAD 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "HEAD 를 읽지 못했다: $head" }
    $head = ([string]$head).Trim()

    $idx = Join-Path $root 'index/symbols.tsv'
    if (-not (Test-Path -LiteralPath $idx)) {
        $lines.Add('[하네스] index/symbols.tsv 가 없다. B모드 전에 tools/reindex.ps1 로 만든다 (LOOKUP.md §6-2).')
    } else {
        $hdr = Get-Content -LiteralPath $idx -Encoding UTF8 -TotalCount 5 | Where-Object { $_ -match 'git-head:' } | Select-Object -First 1
        if (-not $hdr) {
            $lines.Add('[하네스] index/symbols.tsv 헤더에 git-head 가 없다. 쓰지 말고 tools/reindex.ps1 로 다시 만든다 (LOOKUP.md §6-2).')
        } else {
            $idxHead = ($hdr -replace '^.*git-head:\s*', '').Trim()
            if ($idxHead -ne $head) {
                $lines.Add("[하네스] index/symbols.tsv 는 낡았다 (인덱스 git-head $($idxHead.Substring(0, [Math]::Min(7, $idxHead.Length))), 현재 HEAD $($head.Substring(0, 7))). B모드 전에 tools/reindex.ps1 로 다시 만든다 (LOOKUP.md §6-2).")
            }
        }
    }

    if ($lines.Count -gt 0) {
        $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes(($lines -join "`n") + "`n")
        $out = [Console]::OpenStandardOutput()
        $out.Write($bytes, 0, $bytes.Length)
        $out.Flush()
    }
    exit 0
}
catch {
    [Console]::Error.WriteLine("session-start.ps1 오류: $($_.Exception.Message)")
    exit 1
}
