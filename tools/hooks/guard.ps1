<#
  PreToolUse 훅 — CLAUDE.md §0 금지선 중 기계로 판정되는 것을 도구 호출 직전에 막는다.

  판정만 한다. 파일·git 상태를 바꾸지 않는다.
    deny : 되돌릴 수 없는 git 명령(reset --hard, checkout -- ., restore ., clean, 강제 푸시),
           main/dev 에서의 git commit                                 (§0 Git, §9-2)
    ask  : 승인 후에만 건드릴 수 있는 파일에 Edit/Write/NotebookEdit  (§0 파일·에셋, 설정)
  해당 없으면 아무것도 출력하지 않고 exit 0 (= 판정 없음, 평소 권한 흐름).

  스키마: 공식 문서 hooks (stdin: tool_name, tool_input.command|file_path, cwd /
          stdout: hookSpecificOutput.permissionDecision).
  한계 (확인 못 한 것): 셸 명령(sed -i, 리다이렉트, rm 등)으로 보호 파일을 바꾸는 것은 판정하지 않는다.

  주의: 이 파일은 UTF-8 with BOM 으로 저장한다. Windows PowerShell 5.1 은 BOM 이 없으면
        .ps1 을 ANSI(cp949)로 읽어 한글이 깨진다.
#>

$ErrorActionPreference = 'Stop'

function Write-Decision([string]$decision, [string]$reason) {
    $obj = @{ hookSpecificOutput = @{
        hookEventName            = 'PreToolUse'
        permissionDecision       = $decision
        permissionDecisionReason = $reason
    } }
    $json  = $obj | ConvertTo-Json -Compress -Depth 5
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($json)
    $out   = [Console]::OpenStandardOutput()
    $out.Write($bytes, 0, $bytes.Length)
    $out.Flush()
}

try {
    # 표준입력을 UTF-8 로 읽는다. 기본 코드페이지로 읽으면 한글 경로가 깨진다.
    $stdin = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), (New-Object System.Text.UTF8Encoding($false)))
    $raw = $stdin.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { throw '표준입력이 비었다.' }
    $in = $raw | ConvertFrom-Json

    $tool = [string]$in.tool_name
    $cwd  = [string]$in.cwd
    if ([string]::IsNullOrWhiteSpace($cwd)) { $cwd = (Get-Location).Path }

    if ($tool -eq 'Bash' -or $tool -eq 'PowerShell') {
        $cmd = [string]$in.tool_input.command

        # 명령 시작 위치에 있는 git 만 본다. 따옴표 안 문장이 우연히 걸리는 것을 줄인다.
        $g = '(?im)(^|[;&|(]\s*)git\s+(-[cC]\s+\S+\s+)*'

        if ($cmd -match ($g + 'reset\s+(\S+\s+)*--hard')) {
            Write-Decision 'deny' 'git reset --hard 는 금지선이다 (CLAUDE.md §0 Git).'; exit 0
        }
        if ($cmd -match ($g + 'checkout\s+(\S+\s+)*--\s+\.')) {
            Write-Decision 'deny' 'git checkout -- . 는 금지선이다 (CLAUDE.md §0 Git).'; exit 0
        }
        # git restore . / -- . / :/ 는 checkout -- . 와 같이 작업 트리 변경을 버린다.
        # --staged 만 준 경우(-W/--worktree 없이)는 인덱스만 되돌리므로 통과시킨다.
        if ($cmd -match ($g + 'restore(?<args>[^;&|\r\n]*)')) {
            $args_ = ' ' + $Matches['args'] + ' '
            $whole = $args_ -match '\s(\.|:/)\s'
            $stagedOnly = ($args_ -match '\s(--staged|-S)\s') -and -not ($args_ -match '\s(--worktree|-W)\s')
            if ($whole -and -not $stagedOnly) {
                Write-Decision 'deny' 'git restore . 는 작업 트리 변경을 버린다. 금지선이다 (CLAUDE.md §0 Git).'; exit 0
            }
        }
        if ($cmd -match ($g + 'clean(\s|$)')) {
            Write-Decision 'deny' 'git clean 은 금지선이다 (CLAUDE.md §0 Git).'; exit 0
        }
        if ($cmd -match ($g + 'push\s+(\S+\s+)*(--force(-with-lease)?(=\S+)?|-f)(\s|$)')) {
            Write-Decision 'deny' '강제 푸시는 금지선이다 (CLAUDE.md §0 Git).'; exit 0
        }
        # commit(\s|$): git commit-tree 는 걸리지 않는다 (트리가 같은 병합용으로 쓴다).
        if ($cmd -match ($g + 'commit(\s|$)')) {
            $branch = (& git -C $cwd branch --show-current 2>&1)
            if ($LASTEXITCODE -ne 0) { throw "현재 브랜치를 읽지 못했다: $branch" }
            $branch = ([string]$branch).Trim()
            if ($branch -eq 'main' -or $branch -eq 'dev') {
                Write-Decision 'deny' "현재 브랜치가 $branch 이다. main/dev 에 직접 커밋하지 않는다. 브랜치부터 만든다 (CLAUDE.md §0 Git, §9-2)."; exit 0
            }
        }
        exit 0
    }

    if ($tool -eq 'Edit' -or $tool -eq 'Write' -or $tool -eq 'NotebookEdit') {
        $path = [string]$in.tool_input.file_path
        if ([string]::IsNullOrWhiteSpace($path)) { $path = [string]$in.tool_input.notebook_path }
        if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }

        $root = $env:CLAUDE_PROJECT_DIR
        if ([string]::IsNullOrWhiteSpace($root)) { $root = $cwd }
        $rootN = $root.Replace([char]92, [char]47).TrimEnd([char]47)
        $p = $path.Replace([char]92, [char]47)
        if ($p.StartsWith($rootN + '/', [System.StringComparison]::OrdinalIgnoreCase)) {
            $p = $p.Substring($rootN.Length + 1)
        }

        $reason = $null
        if     ($p -match '\.meta$')                                    { $reason = '.meta 는 직접 만들거나 고치지 않는다. GUID 는 Unity 만 발급한다' }
        elseif ($p -match '^(Library|Temp|obj|Build|UserSettings)/')    { $reason = 'Library/Temp/obj/Build/UserSettings 는 건드리지 않는다' }
        elseif ($p -match '^ProjectSettings/')                          { $reason = 'ProjectSettings/ 변경은 승인 후에만 한다' }
        elseif ($p -match '^Packages/manifest\.json$')                  { $reason = 'Packages/manifest.json 변경은 승인 후에만 한다' }
        elseif ($p -match '\.(unity|prefab|asset|inputactions)$')       { $reason = '씬·프리팹·에셋·입력 YAML 은 텍스트로 수정하지 않는다. 에디터에서 한다' }

        if ($reason) {
            Write-Decision 'ask' "$reason (CLAUDE.md §0). 대상: $p"
        }
        exit 0
    }

    exit 0
}
catch {
    # 훅이 조용히 죽으면 보호가 없는 줄 모르고 진행한다. 원문을 남기고 비차단 오류(exit 1)로 알린다.
    [Console]::Error.WriteLine("guard.ps1 오류: $($_.Exception.Message)")
    exit 1
}
