<#
  PostToolUse 훅 — .claude/agents/ 를 고친 직후 .codex 사본이 어긋났는지 확인한다.

  tools/sync-agents.ps1 -Check 를 부를 뿐 쓰지 않는다. 다른 파일을 고친 호출은 건드리지 않는다.
  어긋나면 stdout JSON {"decision":"block","reason":...} + exit 0 (Claude 에게 전달). 재생성은 사람이 하거나 시킬 때 한다:
    powershell -ExecutionPolicy Bypass -File tools/sync-agents.ps1

  주의: 이 파일은 UTF-8 with BOM 으로 저장한다 (Windows PowerShell 5.1).
#>

$ErrorActionPreference = 'Stop'

try {
    $stdin = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), (New-Object System.Text.UTF8Encoding($false)))
    $raw = $stdin.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { throw '표준입력이 비었다.' }
    $in = $raw | ConvertFrom-Json

    $path = [string]$in.tool_input.file_path
    if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }
    $p = $path.Replace([char]92, [char]47)
    if ($p -notmatch '/\.claude/agents/[^/]+\.md$') { exit 0 }

    $root = $env:CLAUDE_PROJECT_DIR
    if ([string]::IsNullOrWhiteSpace($root)) { $root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
    $script = Join-Path $root 'tools/sync-agents.ps1'

    $result = & powershell -NoProfile -ExecutionPolicy Bypass -File $script -Check 2>&1 | Out-String
    $code = $LASTEXITCODE
    if ($code -eq 0) { exit 0 }

    # PostToolUse 는 공식 문서상 JSON decision "block" + reason 이 Claude 에게 전달되는 형식이다
    # (exit 2 의 PostToolUse 동작은 문서에서 확인하지 못했다). 도구는 이미 실행됐으므로 되돌리지 않는다.
    $reason = "에이전트 정의를 고쳤는데 .codex 사본이 어긋났다 (sync-agents -Check exit $code). CLAUDE.md §7 은 둘을 같이 고치라고 한다.`n" + $result.Trim()
    $json  = @{ decision = 'block'; reason = $reason } | ConvertTo-Json -Compress
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($json)
    $out   = [Console]::OpenStandardOutput()
    $out.Write($bytes, 0, $bytes.Length)
    $out.Flush()
    exit 0
}
catch {
    [Console]::Error.WriteLine("after-agents-edit.ps1 오류: $($_.Exception.Message)")
    exit 1
}
