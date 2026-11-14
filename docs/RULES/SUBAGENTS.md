# 서브에이전트 운영 — CLAUDE.md §7-1 · §7-2 · §7-4 · §7-5

> **CLAUDE.md 의 일부다. 구속력은 CLAUDE.md 와 같다.** § 번호는 CLAUDE.md 의 번호를 그대로 쓴다.
> **읽는 시점**: 서브에이전트를 파견하거나 Codex 를 부르기 전.
> 파견할지 말지(§7-3 파견 기준)와 **병렬 절대 금지**는 CLAUDE.md §7 에 있다. 파견 여부는 그쪽에서 먼저 판단한다.
> 이 내용을 고칠 때는 이 파일만 고친다. CLAUDE.md 에 사본을 두지 않는다.

### 7-1. 7종

| 에이전트 | 작업 | 쓰기 | 모델 | 노력 |
|---|---|---|---|---|
| `unity-explorer` | 탐색 | ❌ | `claude-opus-5-5` | high |
| `unity-architect` | 구조 분석 | `docs/` 만 | `claude-opus-5-5` | high |
| `unity-reviewer` | 리뷰 (§0/§1/§5 체크리스트) | ❌ | `claude-opus-5-5` | high |
| `unity-implementer` | 생성 | `Assets/` | `claude-opus-5-5` | high |
| `unity-verifier` | 검증 (Unity MCP) | ❌ | `claude-opus-5-5` | high |
| `blender-artist` | 모델링 (Blender MCP) | `art-src/` 만 | `claude-opus-5-5` | high |
| `gameplay-designer` | 기획 (피드백 → 가설·변경안·플레이테스트 설계·요청서 초안) | ❌ | `claude-opus-5-5` | high |

- `blender-artist` 는 사용자의 열린 Blender 하나에 붙는다. 같은 Blender 를 두 에이전트(Claude·Codex 포함)가 동시에 쓰지 않는다. 결과를 `Assets/` 로 옮기는 것은 승인받은 `unity-implementer` 작업이다 (`docs/DECISIONS.md` 2026-11-09 "Blender 아트 에이전트").

- `gameplay-designer` 를 쓰는 때: 플레이 피드백이 들어왔거나 구현 전에 선택지가 갈릴 때. 파견 여부는 §7-3(사용자가 지시할 때)을 따른다. 파견문에는 `git rev-parse HEAD` 값을 적는다 — 이 에이전트는 Bash 가 없어 인덱스 신선도를 스스로 확인하지 못한다. 역할 경계와 응답 형식은 정의(`.claude/agents/gameplay-designer.md`)가 원본이다 (`docs/DECISIONS.md` 2026-11-09 "기획 에이전트").

- 모델·노력은 `.claude/agents/<이름>.md` frontmatter 의 `model`·`effort` 가 원본이고 이 표는 그 요약이다. 별칭(`opus`) 대신 전체 ID 로 고정해 별칭이 가리키는 모델이 바뀌어도 따라가지 않는다 (`docs/DECISIONS.md` 2026-10-10 "서브에이전트 모델·노력 고정").

### 7-2. 병렬 / 직렬

**병렬 OK** — 읽기 전용이고 영역이 겹치지 않을 때만.
- 탐색 여러 갈래, 파일별 리뷰, 서로 다른 시스템 조사

**직렬 필수** — 앞의 결과가 뒤의 입력일 때.
- `구조 분석 → 생성 → 검증`. 뒤집으면 근거 없는 코드가 나온다.
- `리뷰 → 수정 → 재검증`

### 7-4. 파견할 때 지키는 것

- 에이전트는 이 대화를 모른다. 파견 프롬프트에 **경로·범위·판정 기준·안 할 것**을 전부 적는다. "아까 그거" 같은 지시는 통하지 않는다.
- 돌아온 결과를 그대로 믿지 않는다. **`경로:줄` 근거가 없는 주장은 되묻는다.** → §4-1
- 에이전트가 규칙 위반을 저질렀으면 결과를 버리고 보고한다. 조용히 고쳐 쓰지 않는다.

### 7-5. Codex

`codex` 플러그인(`/codex:*`)은 다른 모델 계열의 에이전트를 부른다. 구현자(Claude)와 다른 눈이 필요한 곳에만 쓴다 (`docs/DECISIONS.md` 2026-10-26 "Codex 역할"). Codex 쪽 설정·훅 사실은 `AGENTS.md` 에 있다.

**쓰는 곳** — 넷 다 읽기 전용이다.

| 용도 | 명령 (`<cc>` = 플러그인의 `scripts/codex-companion.mjs`) | 언제 | 에이전트가 스스로 부르나 |
|---|---|---|---|
| 계획 반박 | `node <cc> adversarial-review --wait <초점>` | §3-2 합의 요청서를 `docs/` 파일로 둔 뒤, 승인 **전에**. 반박과 그에 대한 답을 요청서에 붙여 승인을 받는다 | 사용자가 지시할 때만 |
| 설계·정합성 리뷰 | `node <cc> adversarial-review --wait --base dev <초점>` | `feature → dev` 병합 전. §0/§1/§5 체크리스트는 아래 "리뷰 규모" 대로 맡는다 | **브랜치당 1회는 지시 없이 부른다.** 다시 부르는 것은 지시가 있을 때만 |
| 넓은 탐색 | `node <cc> task --wait "<읽기 전용 …>"` | §7-3 "파견한다" 에 해당할 때(독립 영역 3개 이상 등). 1–2 파일은 직접 읽는다 — Codex 는 호출 한 번의 고정비가 크다(2026-10-26 T1: 파일 2개에 입력 407k) | 사용자가 지시할 때만 (§7-3) |
| 막혔을 때 진단 | `node <cc> task --wait "<읽기 전용 …>"` | §4-5 에서 3번 실패한 뒤, 보고하는 다음 후보 중 하나로 | 사용자가 지시할 때만 |

- **에이전트는 스크립트를 Bash 로 직접 부른다.** 플러그인 에이전트 `codex:codex-rescue` 와 `/codex:rescue` 는 쓰지 않는다. 전달만 하는 데 Claude 토큰이 든다(2026-10-26 T1 약 29k). 게다가 실패하면 "return nothing"(`agents/codex-rescue.md`)이라 빈 결과가 돌아온다. `<cc>` 위치는 플러그인 설치 경로라 머신마다 다르다. 실행할 때 구한다 (CLAUDE.md 맨 위).
- **`task` 에 `--write` 를 붙이지 않는다.** 없으면 `read-only` sandbox 로 돈다(`codex-companion.mjs` `sandbox: request.write ? "workspace-write" : "read-only"`). 리뷰는 늘 `read-only` 다.
- 사용자는 같은 일을 슬래시 명령(`/codex:adversarial-review`·`/codex:review`·`/codex:rescue`)으로 직접 할 수 있다.
- Codex 결과도 §7-4 와 같이 다룬다. `경로:줄` 근거가 없는 주장은 되묻는다.
- **Codex 가 실패하면 시끄럽게** (§5): 호출이 0 이 아닌 코드로 끝나거나, 오류를 내거나(사용량 소진 포함), 결과가 비면 그 원문을 보고에 `Codex 리뷰 못 함: <원문>` 으로 적는다. 리뷰 없이 병합할지는 사용자에게 묻는다. 조용히 건너뛰거나 Claude 리뷰만으로 대신했다고 쓰지 않는다.
- **리뷰 규모**: §0/§1/§5 체크리스트를 누가 맡을지는 `dev...HEAD` 의 `Assets/` 변경(`.meta` 제외)으로 정한다 (`docs/DECISIONS.md` 2026-10-26 "리뷰 규모").
  - **작은 변경** — `git diff --name-status` 가 `M` 뿐이고(새 파일·삭제·이동 없음), 공개 시그니처가 바뀌지 않았고, `git diff --numstat` 의 추가+삭제 합이 30줄 이하: `unity-reviewer` 를 파견하지 않는다. 메인이 `.claude/agents/unity-reviewer.md` 체크리스트를 바뀐 줄에 대조하고 병합 보고에 항목별 결과(`위반 없음` 또는 `경로:줄`)를 적는다. Codex 설계 리뷰 1회는 그대로 한다.
  - **그 밖** — `unity-reviewer` 파견 + Codex 설계 리뷰.
- **시험 중 (리뷰 분담 비교)**: `Assets/` 를 바꾼 병합 보고에 두 리뷰의 지적(실제 결함 / 규칙 위반 / 오탐)과 비용을 나란히 적는다. 비용은 `unity-reviewer` 의 `subagent_tokens`(작은 변경이면 "메인 대조, 별도 수치 없음")와 Codex 리뷰 전후 `/status` 의 5시간 `used_percent`(사용자가 읽어 준다. 움직이지 않으면 "변화 없음")다. 비교는 "그 밖" 브랜치만 센다. 두 브랜치를 비교한 뒤 분담을 다시 정하고 이 줄을 지운다.

**지키는 것**
- **Codex 호출은 서브에이전트 파견이다.** §7-3(사용자가 지시할 때만 — 위 표의 병합 전 리뷰 1회만 예외)과 §3-4(`승인:`·`브랜치:` 줄을 요청 첫머리에)를 그대로 적용한다. 플러그인 에이전트 `codex:codex-rescue` 의 설명("Proactively use")은 이 프로젝트에서 따르지 않는다.
- **쓰기 작업은 맡기지 않는다.** Codex 에 쓰기를 맡기는 것은 그 자체가 §3 합의 대상이다. Codex 기본 지침은 "되돌릴 수 있는 작업과 수정은 허락을 묻지 말고 진행" 쪽이라(2026-10-26 세션 기록 `base_instructions`) §3 과 방향이 반대다.
- **합의해서 쓰기를 맡길 때는 `tools/codex-unity.ps1 -PromptFile <파일> -Write` 로만 부른다** (Unity MCP 허락이 없어도. `docs/DECISIONS.md` 2026-11-14 "Codex 샌드박스 계약"). companion 에 `--write` 를 직접 붙이지 않는다. `--write` 샌드박스는 `.git`·`.codex`·`.agents` 가 읽기 전용이고 승인 정책이 `never` 라 권한을 올릴 수 없기 때문이다.
  - **지시서**: git 쓰기 명령이나 `.git/`·`.codex/`·`.agents/` 경로를 적지 않는다. 적으면 스크립트가 Codex 를 띄우지 않고 종료 코드 2 로 거부한다(`codex-unity.ps1:131`). 파일 이동·이름 바꾸기는 Claude 가 먼저 `git mv` 로 하고(`.meta` 포함), 지시서에는 `CLAUDE-GIT-DONE: <실행한 명령>` 줄로 알린다.
  - **계약**: 스크립트가 `-Write` 지시서 앞에 계약 블록을 붙인다(`codex-unity.ps1:114`). git 쓰기는 실행하지 말고 `CLAUDE-GIT:` 줄로 보고한다. 질문으로 턴을 끝내지 않는다(범위 밖일 때만 `CLAUDE-QUESTION:`). 막히면 `CLAUDE-BLOCKED:` 와 에러 원문을 적는다. 마지막 줄은 `CLAUDE-STATUS: done|git|question|blocked` 하나다.
  - **기다리기**: 실행마다 새 `-RunId <이름>` 을 준다(이어 가기 포함). 스크립트를 `run_in_background` 로 띄우고, `<지시서>.<RunId>.result.json` 이 생길 때까지 도는 `until` 루프를 따로 `run_in_background` 로 둔다. 백그라운드 껍데기의 완료 알림만 믿지 않는다(2026-11-13 Codex 가 끝난 뒤 36분 "돌고 있음").
    - 결과 파일은 맨 마지막에 한 번에 생긴다. 스크립트가 강제 종료되면 감시 프로세스가 `13 killed` 로 남긴다.
    - 같은 자리에 `.sent.md`(보낸 지시서)·`.out.md`(Codex 마지막 메시지)·`.stderr.log` 가 남는다.
    - 같은 `RunId` 의 파일이 이미 있으면 스크립트는 그 파일을 지우지 않고 곧바로 멈춘다. 지시서가 없을 때와 `RunId` 가 잘못됐을 때도 결과 파일 없이 곧바로 멈춘다. 그래서 루프에는 상한 시간을 두고, 껍데기가 결과 파일 없이 끝났으면 그 출력 원문을 13 으로 보고한다.
  - **판정은 `-Write` 실행에만**: 계약을 붙이지 않은 읽기 실행은 답이 오면 0(`answer`), 아니면 13 이다.
  - **종료 코드별 복구** (`codex-unity.ps1:144` 의 판정. 마지막 줄의 `CLAUDE-STATUS` 를 먼저 보고, 그 줄과 표지 줄이 맞지 않으면 12 다)

    | 코드 | 뜻 | Claude 가 하는 것 |
    |---|---|---|
    | 0 | 끝남 | 결과를 §7-4 대로 검토한다 |
    | 2 | 지시서 거부, Codex 안 띄움 | git 쓰기는 직접 하고, 지시서를 고쳐 다시 부른다 |
    | 10 | `CLAUDE-STATUS: git` 과 `CLAUDE-GIT:` 줄 | 승인 범위 안의 명령만 실행하고, 실행한 것을 `CLAUDE-GIT-DONE:` 줄로 적은 지시서를 `-ResumeLast` 로 보낸다. 범위 밖이면 멈추고 보고한다 |
    | 11 | `CLAUDE-STATUS: question` 과 `CLAUDE-QUESTION:` 줄 | 지시서 범위 안의 세부면 답하고 `-ResumeLast` 로 이어 간다. 한 작업에 최대 2회. 범위 밖이거나 3번째면 사용자에게 묻는다 |
    | 12 | 막힘, 또는 계약 위반(마지막 줄이 `CLAUDE-STATUS` 가 아님 — 물음표로 끝난 맨 질문 포함, 상태와 표지 줄이 맞지 않음, `.git` 쓰기 거부가 보이는데 done) | 이어 가지 않고 `.out.md` 원문을 보고한다 |
    | 13 | 오류·사용량 소진·강제 종료 | 이어 가지 않고 `.stderr.log` 와 `reason` 원문을 보고한다(위 "Codex 가 실패하면 시끄럽게") |
- **Unity 에디터는 한 번에 한쪽만 쓴다.** Claude 와 Codex 는 같은 relay 로 같은 에디터에 붙는다. 한쪽이 Unity MCP 를 쓰는 동안 다른 쪽은 쓰지 않는다 (CLAUDE.md §7 병렬 절대 금지 2번).
- **Codex 의 Unity MCP 는 사용자가 그 작업에서 허락할 때만 연다** (사용자 2026-11-06 "쓸 순 있지만 내가 너에게 명령할때 codex로 구현하고 mcp 활용하라고 사용 허락할때만"). "codex로 구현" 만으로는 허락이 아니다. 허락이 있으면 `tools/codex-unity.ps1 -PromptFile <파일> [-Write] -Unity` 로 부르고, 지시서에 `Unity MCP: 이 실행에서 허용` 줄과 쓸 수 있는 도구를 적는다. 허락이 없으면 `-Unity` 를 빼고(쓰기면 위 스크립트, 읽기 전용이면 companion 을 그대로) 지시서에 `Unity MCP: 허용 안 됨` 을 적는다. 스크립트는 `-Unity` 를 준 실행 동안만 `~/.codex/config.toml` 의 도구 승인을 `approve` 로 두고 끝나면 되돌린다. 서버 이름은 머신마다 달라(`unity_mcp`, `unity-mcp`) relay 를 쓰는 서버를 찾아 쓴다. 스크립트가 강제 종료돼도 따로 띄운 감시 프로세스가 지운다. 전원이 끊긴 뒤에는 `-Status` 로 보고 `-Close` 로 닫는다. 승인을 설정에 남겨 두지 않는다.
- **Codex 의 Blender MCP 는 평소 꺼 두고, 사용자가 그 작업에서 허락할 때만 한 실행 동안 연다** (사용자 2026-11-14 "codex로 구현하라" + 방식 "가"). `~/.codex/config.toml` 에 `[mcp_servers.blender]`(`uvx mcp-for-blender@2.2.7`, env `BLENDER_MCP_SAFE_MODE=1`·`DISABLE_TELEMETRY=true`)를 `enabled = false` 로 등록해 둔다(`codex mcp list` 에 `disabled`). 사용자 설정이라 PC 마다 등록한다 — `D:/PCUBE` PC 는 2026-11-14. 허락이 있으면 `tools/codex-unity.ps1 -PromptFile <파일> [-Write] -Blender` 로 부르고, 지시서에 `Blender MCP: 이 실행에서 허용` 줄을 적는다. 스크립트는 그 실행 동안만 서버를 `enabled = true` 로 바꾸고 `blender-artist` 와 같은 도구 4개(`get_addon_status`·`get_scene_info`·`execute_blender_code`·`look`)에 `approve` 를 준 뒤 되돌린다. 외부 에셋·생성형 도구(`search_assets`·`import_asset`·`generate_3d`)는 승인하지 않는다. 닫는 방식(finally·감시 프로세스·`-Status`/`-Close`)과 표지는 `-Unity` 와 같아서, 한쪽이 열려 있으면 다른 실행은 열지 못하고 오류로 끝난다. 설정 파일의 검사·쓰기·복원은 잠금 하나(`Local\protoharness-codex-unity` 뮤텍스)로 묶고 임시 파일을 옮겨 쓴다. 실행의 finally·감시 프로세스는 자기가 소유한 문만 닫고, `-Close` 는 소유와 상관없이 닫는다. Blender 는 하나다 — Codex 가 쓰는 동안 Claude(메인·`blender-artist`)는 Blender 를 부르지 않는다. 지시서에는 `blender-artist` 정의의 쓰기 범위·세이프 모드·저장 규칙을 옮겨 적는다(Codex 는 그 정의를 읽지 않는다).
  - **사용자가 Codex 를 직접 쓸 때**(Claude 한도·토큰 불균형): 설정은 `enabled = false` 그대로 두고 그 세션만 `codex -c mcp_servers.blender.enabled=true` 로 켠다(`-c` 는 그 실행에만 `config.toml` 값을 덮어쓴다). 자주 쓰면 같은 값을 `~/.codex/<이름>.config.toml` 에 두고 `codex -p <이름>` 으로 얹어도 된다. 설정 파일의 `enabled` 를 손으로 `true` 로 바꿔 두지 않는다 — 닫힌 동안에도 `search_assets`·`generate_3d` 가 목록에 남는다. 대화형이라 도구 승인은 사용자가 그때마다 한다. 이때도 Blender 는 하나다 — Claude 는 그동안 Blender 를 부르지 않는다 (`docs/DECISIONS.md` 2026-11-14 "Codex 직접 사용 때 Blender MCP").
  - 실측 (2026-11-14, `D:/PCUBE`, codex-cli 0.165.0, 읽기 실행 `-Effort low`): ① 닫힘 — `blender` 서버가 도구 목록에 없음 ② `-Blender` — `get_addon_status` 성공(`5.2.3 LTS`, 텔레메트리 false, 라이브러리·생성기 off), 승인 안 한 `search_assets` 는 `MCP tool call requires approval, but approval policy is never` ③ 다시 닫힘 — 서버 없음. 실행 뒤 설정 파일은 열기 전과 같았다(`-Status` `closed`).
- **stop 리뷰 게이트(`/codex:setup --enable-review-gate`)는 켜지 않는다.** 파일을 고친 턴마다 끝에서 Codex 리뷰가 돌고 최대 900초를 기다린다(플러그인 `hooks/hooks.json`). 켜려면 합의한다.
