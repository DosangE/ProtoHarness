<#
  PreToolUse 훅 — CLAUDE.md §0 금지선 중 기계로 판정되는 것을 도구 호출 직전에 막는다.

  판정만 한다. 파일·git 상태를 바꾸지 않는다.
    deny : 되돌릴 수 없는 git 명령(reset --hard, checkout -- ., restore ., clean, 강제 푸시),
           main/dev 에서의 git commit                                 (§0 Git, §9-2)
    ask  : 승인 후에만 건드릴 수 있는 파일에 Edit/Write/NotebookEdit  (§0 파일·에셋, 설정)
    deny : 같은 파일에 Codex apply_patch. Codex 는 ask 를 지원하지 않는다(오류를 내고 호출을 계속한다)
  해당 없으면 아무것도 출력하지 않고 exit 0 (= 판정 없음, 평소 권한 흐름).

  Claude Code(.claude/settings.json)와 Codex(.codex/hooks.json)가 같이 부른다.
  스키마: 두 쪽 공식 문서 hooks (stdin: tool_name, tool_input.command|file_path, cwd /
          stdout: hookSpecificOutput.permissionDecision). Codex 셸은 tool_name Bash,
          파일 편집은 apply_patch 이고 경로는 tool_input.command 의 패치 머리줄에 있다.
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

# 보호 파일이면 사유를, 아니면 $null 을 돌려준다. 경로는 저장소 기준 상대 경로로 바꿔 본다.
# . 과 .. 를 먼저 풀어 절대 경로로 만든다. 글자 그대로 비교하면 docs/../Packages/manifest.json 이 빠져나간다.
# 풀 수 없는 경로(잘못된 문자, Foo::$DATA 같은 스트림 표기 등)는 보호 파일일 수 있으므로 막는다.
# 던지게 두면 바깥 catch 의 exit 1(비차단)로 끝나 판정 없이 지나간다 — 예전 글자 비교는 이런 경로도 접두사로 잡았다.
function Get-ProtectedReason([string]$path, [string]$root) {
    $rootFull = [System.IO.Path]::GetFullPath($root).TrimEnd([char]92, [char]47)
    try {
        $full = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($rootFull, $path.Trim()))
    }
    catch {
        return "경로를 판정할 수 없어 보호 파일로 본다 ($($_.Exception.Message)). 대상: $path"
    }
    $p = $full
    if ($full.StartsWith($rootFull + [char]92, [System.StringComparison]::OrdinalIgnoreCase)) {
        $p = $full.Substring($rootFull.Length + 1)
    }
    $p = $p.Replace([char]92, [char]47)

    if     ($p -match '\.meta$')                                    { return ".meta 는 직접 만들거나 고치지 않는다. GUID 는 Unity 만 발급한다. 대상: $p" }
    elseif ($p -match '^(Library|Temp|obj|Build|UserSettings)/')    { return "Library/Temp/obj/Build/UserSettings 는 건드리지 않는다. 대상: $p" }
    elseif ($p -match '^ProjectSettings/')                          { return "ProjectSettings/ 변경은 승인 후에만 한다. 대상: $p" }
    elseif ($p -match '^Packages/manifest\.json$')                  { return "Packages/manifest.json 변경은 승인 후에만 한다. 대상: $p" }
    elseif ($p -match '\.(unity|prefab|asset|inputactions)$')       { return "씬·프리팹·에셋·입력 YAML 은 텍스트로 수정하지 않는다. 에디터에서 한다. 대상: $p" }
    return $null
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

        $reason = Get-ProtectedReason $path $root
        if ($reason) {
            Write-Decision 'ask' "$reason (CLAUDE.md §0)."
        }
        exit 0
    }

    # Codex 파일 편집. 패치 한 개가 여러 파일을 고칠 수 있어 머리줄의 경로를 모두 본다.
    if ($tool -eq 'apply_patch') {
        $patch = [string]$in.tool_input.command
        if ([string]::IsNullOrWhiteSpace($patch)) { throw 'apply_patch 인데 tool_input.command 가 비었다.' }

        $paths = @([regex]::Matches($patch, '(?m)^\*\*\* (?:Add File|Update File|Delete File|Move to): (?<p>.+?)\s*$') |
                   ForEach-Object { $_.Groups['p'].Value })
        # 머리줄을 못 찾으면 무엇을 고치는지 판정할 수 없다. 그냥 두면 보호 파일도 지나가므로 막는다.
        if ($paths.Count -eq 0) {
            Write-Decision 'deny' 'apply_patch 의 패치 머리줄(*** Add/Update/Delete File:, *** Move to:)에서 경로를 찾지 못해 판정할 수 없다. 머리줄 형식을 확인하라 (tools/hooks/guard.ps1).'; exit 0
        }

        foreach ($path in $paths) {
            $reason = Get-ProtectedReason $path $cwd
            if ($reason) {
                Write-Decision 'deny' "$reason (CLAUDE.md §0). 승인이 있으면 사용자에게 에디터에서 하거나 직접 고치도록 청한다."; exit 0
            }
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
