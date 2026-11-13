<#
  SessionStart 훅 — 세션 시작 때 사실 몇 줄을 컨텍스트에 넣는다. 읽기 전용.

    · 현재 브랜치가 main/dev 인가           (CLAUDE.md §9-2)

  인덱스(index/symbols.tsv) 신선도 알림은 2026-11-13 에 뺐다. 인덱스가 라우터의 첫 조회에서
  내려가 선택 도구가 됐다 (DECISIONS 2026-11-13 "하네스 비용 기준선", LOOKUP.md §6-2).
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
