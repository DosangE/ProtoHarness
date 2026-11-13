# 결정 로그

> **A모드.** "왜 이렇게 했나"는 코드를 읽어도 안 나온다. 여기와 `docs/archive/DECISIONS-<YYYY-MM>.md` 에만 있다.
> 새 결정은 **맨 위에** 추가한다. 뒤집힌 결정은 지우지 말고 `~~취소선~~` + 사유를 남긴다.
> **이 파일에는 이번 달 항목만 둔다** (2026-11-12 문서 다이어트). 달이 바뀌면 지난 달 항목을 내용 그대로 `docs/archive/DECISIONS-<YYYY-MM>.md` 로 옮긴다(맨 위가 최신 — 이 파일과 같은 순서). 다른 문서가 "`DECISIONS.md` <날짜> "<제목>"" 으로 가리키는 항목은 그 날짜의 달 파일에 같은 제목으로 있다. 항목 안의 `DECISIONS.md:<줄>` 은 옮기기 전 줄 번호다.
> 키워드로 찾을 때는 `docs/DECISIONS.md` 와 `docs/archive/` 를 함께 grep 한다.
> 지난 달: 2026-10 (139항목) → `docs/archive/DECISIONS-2026-10.md`, 2026-08 (9항목) → `docs/archive/DECISIONS-2026-08.md`. 2026-09 에는 결정 항목이 없다(그달의 검증 기록은 `docs/archive/ARCHITECTURE-2026-11-12.md`).

형식: `날짜 · 결정 · 이유 · 검토했으나 버린 대안`

---

## 2026-11-13 · 하네스 비용 기준선: 세션 기록으로 처음 쟀다 · B모드(인덱스)를 라우터에서 내린다 · 세션 경계 한 줄 · C모드는 보류 유지 (`docs/harness-cost-baseline`)

- **배경**: 하네스 실험이 공동 주목적인데(아래 "방향 재설정") 하네스가 무엇에 비용을 쓰는지 잰 적이 없었다. 사용자 "후보에 넣었다면 작업 시작하자"(2026-11-13)로 이 PC 의 Claude Code 세션 기록을 읽어 쟀고, 결과를 보고 "진행해봐" 뒤 질문 대화에서 아래 셋을 골랐다.
- **기준선** (`tools/harness-cost.py`, 2026-11-13, 이 PC 세션 41개·서브에이전트 기록 41개, 2026-10-15 ~ 11-13, 이 세션 일부 포함):

  | 지표 | 값 |
  |---|---|
  | 메인 요청 / 입력 / 출력 | 5,209 / 14.5억 토큰(캐시 읽기 98.9%) / 542만 |
  | 서브에이전트 요청 / 입력 / 출력 | 714 / 4,756만(전체 입력의 3.2%) / 67만 |
  | 요청당 컨텍스트 (메인) | 중앙값 253,731 · 평균 279,274 · 최대 700,075 |
  | 컨텍스트 구간 (요청 수 / 입력 몫) | <100k 7.9% / 2.3% · 100-200k 28.3% / 15.3% · 200-400k 42.3% / 44.3% · 400-700k 21.4% / 38.1% |
  | 세션 첫 요청 (고정 비용) | 중앙값 59,978 (서브에이전트 20,399) |
  | 조회 (메인 + 서브) | 인덱스 14 · Grep 도구 263 · 셸 grep/rg 1,582(파이프 뒤 거르기 제외, 명령 단위) · `.cs` Read 385 |
  | 파견 (메인의 Agent 호출) | `unity-reviewer` 22 · `unity-implementer` 6 · `blender-artist` 5 · `web-fetch` 4 · `codex:codex-rescue` 2 · `gameplay-designer` 2 · `general-purpose` 1 · `unity-verifier` 1 · `unity-explorer`·`unity-architect` 0 |
  | 권한·훅 | 훅 차단(`reject/hook`) 4 — dev 커밋 3, `checkout -- .` 1 · 사용자 거절 문구 8 · 설정 거절 19 |
  | `Unity_RunCommand` | 732회, 오류 41 |

- **결정 (사용자, 2026-11-13 질문 대화의 답)**:
  - **B모드** — "라우터에서 내린다 (Recommended)": `CLAUDE.md` §6-1 의 "X 어디 있나" 첫 조회를 `index/symbols.tsv` 에서 코드 `Grep`(파일 목록부터)으로 바꿨다. 인덱스와 `tools/reindex.ps1` 은 선택 도구로 남긴다(`LOOKUP.md` §6-2). SessionStart 훅의 인덱스 신선도 알림을 뺐고, 에이전트 5종(`unity-explorer`·`unity-implementer`·`unity-reviewer`·`unity-architect`·`gameplay-designer`)의 인덱스 지시를 Grep 으로 바꿨다.
  - **세션 경계** — "넣는다 (Recommended)": `CLAUDE.md` §8 에 "병합·요청 하나가 끝나면 HANDOFF 갱신 뒤 `새 세션 권장:` 을 붙인다" 한 줄.
  - **측정 도구** — "tools/ 에 Python 으로 (Recommended)": `tools/harness-cost.py`. `tools/` 의 첫 Python 이다(나머지는 PowerShell). 이 PC 는 Python 3.14, 다른 PC 는 확인 못 했다.
  - **C모드** (Claude 판단, 이 항목 범위): `.cs` 162개로 재논의 트리거(60개)를 넘었지만 보류를 유지한다 — `LOOKUP.md` §6-3.
- **이유**:
  - B모드: 인덱스는 `.gitignore` 대상이라(`.gitignore:109`) 커밋마다 낡고, 쓰려면 먼저 재생성해야 한다. 기록에서 조회의 1% 안쪽이었다. 인덱스가 Grep 보다 토큰을 줄인다는 측정은 없다. 규칙과 실제가 다르면 규칙이 매 세션 읽히는 비용만 남는다.
  - 세션 경계: 입력의 82.4% 가 컨텍스트 20만을 넘는 요청에서 나왔고(위 표), 42세션에 압축은 5번뿐이었다(1차 측정, 압축은 도구가 세지 않는다). 에이전트는 자기 컨텍스트 크기를 정확히 모르므로 토큰 수가 아니라 사건(병합·요청 끝)으로 정했다.
  - 서브에이전트: 사용자 2026-10-26 "클로드에서 에이전트로 보내는 토큰 사용량이 상당" — 전체로는 3.2% 였다. 세션 하나로는 최대 약 33%(2026-10-20 세션, 1차 측정)까지 갔다.
- **검토했으나 버린 대안**: 인덱스를 SessionStart 에서 자동 재생성해 계속 쓰게 하기(쓰인다는 근거가 없는데 훅이 무거워진다) · 인덱스 도구 삭제(되돌리기 비싸고, 선택 도구로 두는 비용이 거의 없다) · 세션 경계를 토큰 수로 정하기(에이전트가 잴 수 없다) · 측정 도구를 PowerShell 로(기존 형식과 맞지만 다시 써야 하고, 400 MB 기록을 읽는 속도는 재지 않았다).
- **한계 (확인 못 한 것)**: 다른 PC(`D:/PCUBE`) 세션은 없다. 달러 비용은 토큰 종류별 단가를 반영하지 않아 말하지 못한다. Codex 리뷰(이 PC 기록에서 `adversarial-review` 호출 57건)는 사용량을 남기지 않아 Claude 대 Codex 비교를 못 한다. 사용자 교정 횟수는 키워드로 세면 오탐이 많아 뺐다.
- **기록은 사라진다**: 측정하는 사이에 2026-10-13 세션 기록(요청 131건)이 지워졌다 — 파일 날짜가 정확히 30일 전이었고, 기록 정리 기간 설정(`cleanupPeriodDays`)은 사용자·프로젝트 설정 어디에도 없다. 기본 정리 기간이 30일이라는 것은 문서로 확인하지 않았다. 그래서 위 표가 기준선의 원본이고, 같은 데이터로 다시 돌릴 수 없다.
- **재측정**: 2026-11-27 이후 `python -I tools/harness-cost.py --since 2026-11-13` 으로 요청당 컨텍스트 중앙값·200k 넘는 요청의 입력 몫·조회 분류를 비교해 이 항목에 덧붙인다(`docs/HANDOFF.md` 2절 7번).
- **검증**: §9-2 문서 검증 — 병합 보고에 붙인다.

## 2026-11-12 · 문서 다이어트: DECISIONS 는 이번 달만, 지난 달·옛 판은 `docs/archive/` 로 그대로, HANDOFF 는 다음 일만 (`docs/doc-diet`)

- **결정 (사용자, 2026-11-12 "문서 다이어트 (HANDOFF 축소, DECISIONS 분리, ARCHITECTURE 갱신) 하자")**: 같은 날 회고에서 나온 후속 ①(아래 "방향 재설정").
  - **DECISIONS 분리**: `docs/DECISIONS.md` 에는 이번 달(2026-11) 항목과 미결만 둔다. 2026-10(139항목)·2026-08(9항목)은 내용 그대로 `docs/archive/DECISIONS-2026-10.md`·`DECISIONS-2026-08.md` 로 옮겼다. 달이 바뀌면 같은 방식으로 옮긴다(머리말). **파일 이름은 그대로 둔다** — `CLAUDE.md`·`docs/RULES/`·`.claude/agents/`·`tools/` 가 `docs/DECISIONS.md` 를 가리키고, 참조는 모두 "날짜 + 제목" 이라 날짜로 달 파일을 찾는다. 새 결정을 맨 위에 쓰는 규칙(`unity-architect` 정의 포함)도 그대로다.
  - **HANDOFF 축소**: 1절 지금 상태(대기 중인 플레이 확인 포함 — `gameplay-designer` 가 1절을 읽는다), 2절 다음 작업, 3절 아직 받지 못한 것, 4절 작업 흐름, 5절 환경 메모만 남겼다. 날짜별 기록을 쌓지 않는다(머리말). 다이어트 전 판 전체는 `docs/archive/HANDOFF-2026-11-12.md`.
  - **ARCHITECTURE 갱신**: 최종 갱신이 2026-10-01 로 남아 있던 판. §2 숫자·씬·SO·아트 원본, §3 NGO·IMGUI 줄, §4 시스템 지도에 상대가 있는 레이스(`RaceField`·`RaceWorld`)·트랙 look·기록·고스트·조작 피드백·트랙 장식·네트워크 레이스 6줄, §5 진입점(실행·조작·결정 이력) 을 지금 코드대로 고쳤다. 틀렸던 것: 런타임 스크립트 26개(실제 97), 테스트 46/131(v0.6 게이트 299 / EditMode 394), 지운 씬 `ChainRushProcedural`, `RoadPiece` "아직 씬에서 쓰지 않는다", 앵커 시야 `Physics.Linecast`(지금 `PhysicsScene.Raycast`), `EncounterTuning`, 조작의 "R 재시작"(2026-10-20 제거)·↑ 가속 누락, 메뉴에 없는 "Apply Art Sound Animation". 옛 직선 코스 수치·무한 모드 절·2026-09 검증 기록은 다이어트 전 판 전체와 함께 `docs/archive/ARCHITECTURE-2026-11-12.md`.
- **크기**: `DECISIONS.md` 890 KB → 230 KB, `HANDOFF.md` 81 KB(283줄, 한 번에 읽히지 않았다) → 약 16 KB(65줄), `ARCHITECTURE.md` 31 KB → 30 KB.
- **이유**: 세션마다 읽는 인계 문서가 한 번에 안 읽혔고(2026-11-12 회고에서 283줄 중 144줄까지), 결정 로그는 통째로 읽을 수 없는 크기였다. ARCHITECTURE 는 A모드 첫 조회 대상인데 숫자·씬이 6주 전 값이었다.
- **버린 대안**: DECISIONS 를 색인 파일 + 달 파일 전부로 나누기 — 새 결정을 쓰는 곳(`unity-architect` 정의 "DECISIONS.md 는 맨 위에 추가한다")과 규칙 문서의 참조를 바꿔야 한다. 항목마다 색인 줄 두기 — 191줄 약 45 KB 로 다시 무거워지고 참조는 이미 날짜를 담고 있다. HANDOFF 의 끝난 항목을 하나씩 DECISIONS 와 대조하고 지우기 — 283줄을 대조하는 비용이 크고, 옛 판을 통째로 보관하면 잃는 것이 없다.
- **검증** (문서 검증, `CLAUDE.md` §9-2 적용 범위): 아래 병합 보고와 같은 값 — `git diff --name-only dev...HEAD` 는 `docs/` 만, DECISIONS 항목 줄 3,131개가 분리 전후 순서까지 같다(스크립트 대조 `identical True`, 항목 수 191 = 43 + 139 + 9), 보관본 둘은 머리말 7줄 뒤가 다이어트 전 파일과 바이트 단위로 같다, 새로 쓴 § 참조는 모두 `CLAUDE.md`·`docs/RULES/`·`docs/DESIGN.md` 의 제목으로 해석된다. `docs/PROPOSAL_M5_AI_OPPONENTS.md` 의 HANDOFF 참조 2개(줄 번호 `:205`, "1절의 M5 전 정리 후보")를 새 자리로 고쳤다. `docs/NEXT_REQUESTS.md` 의 "HANDOFF 1절" 두 곳은 옛 기록 문맥이라 그대로 두었다.

## 2026-11-12 · MP1: 상대마다 격리 월드를 띄우고 `RaceField` 가 같은 틱으로 돌린다 (`feature/mp1-race-field`)

- **결정 (사용자, 2026-11-12 "모두 승인한다" — 아래 "MP1 구조" 뒤에 올린 §3-2 구현 요청서)**: 상대가 있는 레이스는 상대마다 서킷 씬 복사본을 격리 로드하고(`RaceWorld.IsolatedLoad`), 조정자 `Race/RaceField` 가 매 고정 틱에 플레이어 게임 → 상대들 순서로 한 틱씩 진행시킨다. 상대가 없는 레이스는 지금과 같다(조정자 없음, 게임이 스스로 틱을 돈다).
- **만든 것**
  - `ChainRushGame.SelfStepping`(`:48`, 기본 `true`): 끄면 `FixedUpdate` 가 틱을 돌지 않는다(`:183`). `StepTick()`(`:188`)은 게임의 자기 입력원으로 한 틱. 틱 안의 순서(`RunTick`)는 건드리지 않았다.
  - `Race/RaceField.cs`(새 파일, MonoBehaviour, `RaceAssembler` 가 런타임에 붙인다): `Initialize` 가 플레이어 게임과 상대 월드들을 받아 전부 `SelfStepping` 을 끄고 상대의 `ManualStart` 를 끈다(물리 씬이 같거나 월드가 겹치면 예외). `FixedUpdate`(`:70`) — 플레이어 게임이 달리는 중일 때만: 틱 0 이면 상대 전원 `StartRun`(같은 틱에 출발) → 플레이어 한 틱 → 상대들 한 틱 → 달리는 상대의 틱이 플레이어와 다르면 `LogError` 뒤 멈춘다. `Update`(`:63`) — 플레이어가 일시정지면 상대도 일시정지, 준비·카운트다운이면 상대를 출발선에 세운다. 플레이어 런이 끝나면 아무도 진행하지 않는다(끝 규칙 D7 은 MP2).
  - `Flow/RaceSetup`: `WithOpponents(params RunnerProfile[])`(`:42`, 싱글만·최대 `MaxOpponents` 7·null 거부·목록은 사본), `ForOpponent(i)`(`:57`, 그 상대가 러너인 설정 — 장식 씬·트랙 look 없음, `IsOpponentWorld`), `OpponentCount`·`Opponent(i)`. 기존 생성자와 부르는 곳은 그대로다.
  - `Flow/RaceAssembler`: 상대가 있으면 장식 로드 뒤 `LoadOpponents`(`:238`) — 상대마다 `RaceLaunch.Request(setup.ForOpponent(i))` → 격리 로드 → `RaceWorld.FromScene`, 다 모이면 `RaceField` 를 붙이고 카운트다운. 복사본이 설정을 가져가지 않았거나 레이스 월드가 아니면 `StopAssembling`(`:275`)으로 알리고 레이스를 돌리지 않는다. 상대 월드에서는(`:125,203`) `Awake` 에 카메라·`AudioListener` 를 끄고, `Start` 에 `HideAllButTheRunner`(`:287`) — 러너 아래가 아닌 렌더러·조명을 끄고 `AudioSource` 를 음소거하고, 러너 아래가 아닌 `MonoBehaviour` 는 `ChainRushGame`·`CircuitRace`·`RaceAssembler` 만 남기고 끈다. 상대 월드는 기록·고스트·카운트다운·로비 복귀가 없다(`:326,366`).
- **요청서와 달라진 것 (밝힌다)**
  - 요청서는 틱 순서를 "전원 입력 수집 → 전원 이동 → 접촉 → 판정" 으로 적었다. MP1 은 월드마다 `StepTick` 을 통째로 차례로 부른다 — 월드끼리 닿지 않아 결과가 같고, 단계로 나누려면 `ChainRushGame` 의 틱을 세 메서드로 쪼개야 해서(공개 API 변경) 접촉이 들어오는 MP2 요청서로 넘겼다.
  - 요청서는 조정자가 복사본을 로드한다고 적었다. 로드는 `RaceAssembler` 가 한다 — `Race/` 가 `Flow/`(`RaceSetup`·`RaceLaunch`)를 모르게 하기 위해서다.
  - 공개 API 가 늘었다: `ChainRushGame.SelfStepping`·`StepTick()`, `RaceAssembler.Field`, `RaceSetup` 의 위 멤버.
- **지금 닿는 곳이 없다**: 로비는 상대를 주지 않는다(D10 로비 진입은 MP2). 플레이로 확인할 것이 아직 없고, 완료는 테스트로 본다.
- **검증** (에디터 6000.3.19f1, 기준 `574de5c` + 커밋 전 작업 트리)
  - 컴파일: `*** Tundra build success (14.74 seconds), 31 items updated, 1618 evaluated`, `error CS` 0.
  - 새 테스트 `ChainRushRaceFieldTests` 5건: `testcasecount="5" result="Passed" total="5" passed="5" failed="0" skipped="0"`, 2026-11-12 06:37:59Z ~ 06:38:23Z. 상대 3명(BETA·THETA·DELTA, 각자 봇)을 실시간으로 같이 돌린 플레이어 런 1,700틱이 혼자 런과 비트 일치 — `ChainRush race field: the player's 1700 ticks with three opponents alongside equal its solo run (hash 0xEF92FB76BFD1D767); opponents' progress 607.1 / 638.7 / 609.2 m, the player's 623.9 m.` 플레이어 틱 안에서 상대는 늘 한 틱 뒤, 스텝 사이에는 같은 틱. 상대 월드마다 자기 물리 씬·자기 프로필, 카메라·리스너·조명·HUD 꺼짐, 러너만 그려짐. 일시정지·카운트다운·재출발을 상대가 따라간다.
  - 영향 평가: `ChainRushGame.FixedUpdate`(틱 진입) → 지문·롤백·월드·네트워크 보정이 `StepTick` 과 `FixedUpdate` 를 지난다 / `RaceAssembler.Start`·`Awake`·`Update`·`ShowGhost`, `RaceSetup` → Flow 의 `Race*` 테스트(로비 출발·결승·기록·고스트·로비 복귀)와 튜토리얼(`RaceMode.Tutorial` 조립). 고른 것: `groupNames = { "ChainRushWorldTests", "ChainRushRollbackTests", "ChainRushReplayTests\.Replay_CircuitBotRun_RepeatsEveryTickBitForBit", "ChainRushTutorialTests", "ChainRushFlowTests\.Race", "NetReconcileTests" }`, `categoryNames = { "!Device", "!Enemy", "!NetEndReplay", "!^Lap$" }` → `testcasecount="74" result="Passed" total="74" passed="74" failed="0" skipped="0"`, 06:39:27Z ~ 06:43:27Z, 240.1 s (Flow 19, 지문 1, 롤백 10, 튜토리얼 17, 월드 5, 네트워크 보정 22). 서킷 지문 `0xEF92FB76BFD1D767` 그대로.
  - Lap 은 돌리지 않았다: 혼자 판의 주행에 들어가는 코드는 `FixedUpdate` 의 `if (SelfStepping)`(기본 `true`) 한 줄과 `Start` 의 `CountIn` 을 부르는 자리뿐이고, 지문과 `Race_FinishFromTheLobby…` 가 그 경로를 지난다. 게이트 전체도 돌리지 않았다(§9-2).
  - `tools/test-runs.ps1 -Last -Passed`: "그 뒤 Assets/ 에서 바뀐 파일: 없음", "실행한 코드 = 지금 작업 트리".
- **확인 못 한 것**: 레이스를 떠나는 도중의 복사본 로드(`requestedWorld` 를 비우는 경로)와 `StopAssembling` 세 경로는 테스트가 없다. 상대 러너 아래에 남는 잔상·바람 선(`SpeedTrails`, `ControlFeedback` 이 움직이는데 그것을 껐다)이 어떻게 보이는지. 상대 4명 이상, 다른 트랙, 플레이어 빌드, 프레임.
- **`tools/select-tests.ps1` 표**: `ChainRushRaceFieldTests` 를 `Race/` 줄과 `Flow/(RaceAssembler|RaceSetup|SpeedScale)` 줄에 배정했다(VERIFICATION §4-4 "PlayMode 테스트 클래스를 새로 만들면 표에 배정한다").
- **병합 전 리뷰** ("그 밖" 규모 — 새 파일·공개 시그니처, `Assets/` 추가+삭제 672줄, SUBAGENTS §7-5): 커밋 `3e897f4` 를 두 리뷰가 봤다.
  - `unity-reviewer` (`subagent_tokens` 80,779, 도구 18회, 250 s): 치명 0 / 중대 1 / 보통 0 / 제안 4. §0 위반 없음.
  - Codex 설계 리뷰 (`adversarial-review --base dev`): `needs-attention`, high 1 · medium 2. 5시간 `used_percent` 전후는 읽지 못했다(사용자가 `/status` 를 읽어 줘야 한다).
  - **둘이 같은 결함을 짚었다 — 고쳤다**: 상대 복사본이 스스로 조립을 거부해도(러너 look 이 모형에 맞지 않아 `TryDress` 가 실패하면 그 게임을 끈다) 요청은 이미 가져갔고 `RaceWorld.FromScene` 도 통과해 `RaceField` 가 그 월드를 받았다. 조정자는 `StartRun`/`StepTick` 을 직접 불러 꺼진 게임을 다시 돌리고, 그 복사본은 카메라·리스너·도로가 켜진 채 남았다. 고친 것: 상대 월드는 `Awake` 맨 앞(조립 검사 전)에 카메라·리스너를 끈다, `LoadOpponents` 가 복사본의 `RaceAssembler` 가 `IsLaunched`·`enabled` 이고 게임이 `enabled` 인지 확인한다, `field.Initialize` 가 던지면 `StopAssembling` 으로 모은다(reviewer 제안), `StopAssembling` 이 그때까지 띄운 복사본을 내린다(reviewer 제안). 회귀 테스트 `Race_AnOpponentWhoseWorldFailsToAssemble_StopsTheRaceAndUnloadsTheCopies`(맞지 않는 look = `Art/Models/ShortcutSign.fbx`).
  - **넘긴 것 (MP2)**: Codex medium "상대의 첫 틱은 입력을 Poll 하기 전에 돈다"(`RaceField.cs` 의 틱 0 `StartRun` → `StepTick`) — 지금 상대 입력원은 Poll 이 필요 없는 봇·리플레이뿐이고, 입력 수집 순서는 MP2 의 틱 분해에서 정한다. reviewer 제안 "상대 입력원이 기본 `KeyboardMouseInputSource` 로 남는다" — MP2 에서 `Initialize` 가 입력원을 받거나 거부한다. reviewer 제안 "`GetSceneAt(sceneCount - 1)` 을 방금 로드한 복사본으로 본다" — `sceneLoaded` 로 씬을 직접 받는 쪽으로 MP2 에서.
  - **넘긴 것 (따로)**: Codex medium "조립 실패 뒤 로딩 화면에서 빠져나갈 수 없다"(HUD 가 `DrawLoading` 만 그린다) — 기존 실패 경로(플레이어 `TryDress`·장식 씬 로드 실패)도 같은 동작이라 MP1 이 새로 만든 문제가 아니다.
  - reviewer 판단 메모: `RaceField.FixedUpdate` 의 `LogError` 문자열 보간은 틱이 어긋난 오류 경로에서 한 번뿐이라 §0 "Update 안 문자열 결합" 의 대상이 아니라고 봤다. 규칙 문구에는 오류 경로 예외가 적혀 있지 않다.
- **고친 뒤 검증** (에디터 6000.3.19f1): 회귀 테스트는 고치기 전에 실패했다 — 첫 두 번(07:04:35Z, 07:07Z 무렵, 둘 다 `testcasecount="1" result="Failed(Child)" failed="1"`)은 **테스트 결함**이었다(맞지 않는 look 으로 만든 빈 `GameObject` 가 레이스 씬 Single 로드에 지워져 look 이 비었다 — 진단 로그 `scene2 … runner=RunnerProfile_Theta(Clone) look=-`, 진단 줄은 지웠다). 에셋으로 바꾼 세 번째(07:10:33Z ~ 07:10:46Z, `Failed(Child)` `failed="1"`)가 의도한 실패: `Expected log did not appear: [Error] Regex: RaceAssembler: .*opponent 1.* The race is not run\.` 와 `Unhandled log message: '[Log] There are 2 audio listeners in the scene. …'`. 이 세 실행은 결과 XML 을 다음 실행이 덮어써 `tools/test-runs.ps1 -Record` 로 남기지 못했다 — 값은 이 줄이 원본이다. 고친 뒤 컴파일 `*** Tundra build success (5.08 seconds), 29 items updated, 1618 evaluated`, `error CS` 0. 바뀐 `RaceAssembler.cs`·테스트를 읽는 것: `groupNames = { "ChainRushRaceFieldTests", "ChainRushFlowTests\.Race", "ChainRushTutorialTests" }`, `categoryNames = { "!Device", "!Enemy", "!NetEndReplay", "!^Lap$" }` → `testcasecount="42" result="Passed" total="42" passed="42" failed="0" skipped="0"`, 07:13:19Z ~ 07:15:23Z (상대 월드 6, Flow 19, 튜토리얼 17). 바뀐 줄은 모두 상대 월드 경로(`IsOpponentWorld`·`LoadOpponents`·`StopAssembling`)라 혼자 판 주행을 보는 지문·롤백·Lap 은 다시 돌리지 않았다 — 06:39:27Z 의 74건 통과가 그대로다.
- **남은 것**: 상대가 있는 판의 기록·고스트(D4 — 지금은 플레이어 월드가 혼자 판처럼 기록을 남긴다, MP2). `docs/ARCHITECTURE.md` 시스템 지도에 `RaceField` 줄(문서 다이어트에서).

## 2026-11-12 · MP1 구조: 격리 월드 + 조정자, 첫 범위는 봇 3명 — 2026-10-30 의 공유 월드를 뒤집는다 (스파이크 `spike/mp1-isolated-worlds`)

- **결정 (사용자, 2026-11-12 "1. 권고대로 하겠다.")**: MP1 은 한 씬에 러너 여럿(공유 월드)이 아니라 **러너마다 서킷 씬 복사본(격리 월드, `RaceWorld.IsolatedLoad`)을 유지하고, 조정자가 모든 게임을 같은 틱으로 돌린다.** 러너끼리의 접촉(MP2)은 조정자가 위치로 계산해 각자의 월드에서 밀어낸다(D2a 직접 밀어내기). 첫 범위는 봇 3명(복사본 4개, D1 "첫 판정은 봇 3명"). 8명으로 늘릴 때 복사본의 로드 고정 비용을 줄이는 작업을 따로 한다.
- **뒤집는 것**: 2026-10-30 "멀티 개발 순서" 의 MP1 "한 월드에 러너 여럿(공유 월드)" 과 그 "버린 대안" 의 "레이서마다 격리 월드로 AI — 충돌·추월을 못 한다". 그때의 이유(러너끼리 물리가 닿으려면 공유 월드)는 D2a 직접 밀어내기·D2d 트랙만 보는 질의(아래 "방향 재설정")로 성립하지 않게 됐다 — Unity 물리가 러너끼리의 접촉을 중재하지 않는다. MP 단계·순서·완료 조건은 그대로다.
- **이유**: 핵심 코드(`ChainRushGame`·`RunnerMotor`·`CircuitRace`·`SimSnapshot`)를 러너 여럿용으로 뜯지 않는다. 혼자 판 지문이 구조상 유지된다. 게임마다 단계가 따로라 러너별 결승 상태(D7)가 이미 있다. `Net/` 의 서버가 같은 구조라 M7 에서 다시 짜지 않아도 된다. 대가는 레이스 시작 때의 추가 로드다.
- **측정 (스파이크, 에디터 6000.3.19f1, 이 PC, `ChainRushWorldCostTests` — STADIUM 538 m·PALM COAST 2,001 m × 복사본 1·4·8 × 끈 정도 2가지 = 12칸, 봇 1,700틱)**:

  | 항목 | 복사본 4개 | 복사본 8개 |
  |---|---|---|
  | 혼자 런과 비트 일치 | 전 복사본 일치 | 전 복사본 일치 |
  | 틱 (전체) | 142.3~258.7 µs | 294.7~343.8 µs (틱 20 ms 의 1.47~1.72%) |
  | 메모리 증가, 네이티브 / 매니지드 | +31.5~59.1 MB / +8.5~17.9 MB | +68.3~103.2 MB / +19.8~41.9 MB |
  | 추가 로드 | +2.47~4.07 s | +5.22~8.74 s |

  - 지문: STADIUM `0xEF92FB76BFD1D767`(기존 서킷 지문과 같다), PALM COAST `0xBCD1B1DCFBF12133`, 열두 칸 모두 그 트랙의 값 하나.
  - 복사본 하나의 로드는 746~1,357 ms, 혼자 판 로드는 984~2,166 ms. **요청서에 적은 기준 "로드가 혼자 판의 2배 이내" 는 4개에서도 8개에서도 넘었다.** 그 규칙("4개도 넘치면 공유 월드")대로면 공유 월드였다 — 기준 숫자는 Claude 가 임의로 잡은 것이었고, 사용자가 결과를 보고 격리 월드를 골랐다.
  - 로드는 트랙 길이를 따르지 않는다: PALM 746~1,357 ms 대 STADIUM 822~1,084 ms. 도로 짓기가 아니라 씬마다 드는 고정 비용이라는 것이 Claude 의 판단이다. 어디서 드는지는 재지 않았다.
  - **프레임은 판정하지 못했다**: 에디터가 포커스 밖이었고(`focused=False`) 복사본 1개에서도 p95 가 60.4~108.4 ms 였다(STADIUM 8개 평균 10.58 ms 가 1개 17.71~25.40 ms 보다 빠르게 나왔다).
  - 드로 배치: 카메라·리스너만 끈 8개는 2,031(STADIUM)·3,262(PALM), 도로 렌더러·조명·HUD·소리까지 끈 8개는 1,305·1,539, 1개는 266~286·404~542. 끈 STADIUM 으로 계산하면 러너 모형 하나가 약 150배치((1,305 − 266) ÷ 7)다 — 러너 8명에 약 1,200배치이고, 이 비용은 구조와 무관하다. 캐릭터 아트 요청서에 "렌더러 수가 적은 모형" 을 조건으로 넣을 근거다.
  - 복사본 하나: 렌더러 107(STADIUM)·322(PALM), 조명 1, 콜라이더 54·110, MonoBehaviour 12.
- **검증**: 컴파일 `*** Tundra build success (24.29 seconds), 6 items updated, 1618 evaluated`, `error CS` 0. PlayMode `groupNames = { "ChainRushWorldCostTests" }`: `testcasecount="12" result="Passed" total="12" passed="12" failed="0" skipped="0"`, 2026-11-12 06:17:03Z ~ 06:20:00Z, 177.0 s. 실행 기록 `20261112T061703Z-PlayMode-0d13284.xml`(`tools/test-runs.ps1`). 스파이크 커밋 `57c54f4`(`dev` 에 병합하지 않는다).
- **확인 못 한 것**: 프레임(위), 플레이어 빌드, 복사본 로드의 내역, 러너 8명을 실제로 그릴 때의 프레임, 복사본을 한꺼번에(병렬로) 로드할 때의 시간.
- **다음**: MP1 구현 요청서(§3-2) — 조정자, `ChainRushGame` 의 "스스로 틱 돌지 않기", `RaceAssembler` 의 상대 월드(기록·고스트·카운트다운·장식 없음), 틱 순서(전원 입력 수집 → 전원 이동 → 접촉 풀기 → 랩·결승 판정).

## 2026-11-12 · 방향 재설정: v1.0 = PC 싱글 + 봇전 · 하네스 실험이 공동 목적 · M5 결정 D1~D11 · MP1 은 격리 월드 대안을 스파이크로 먼저 잰다 (`docs/v1-direction`)

- **배경**: 사용자 "우리 현재 프로젝트를 전반적으로 돌이키고 살펴보고 기획, 아트, 구현(프로그래밍) 관련해서 설계 해보자"(2026-11-12). 회고에서 드러난 것 — 문서상 최종 목표(8인 서버 권위·PC+모바일·아이템전, `DESIGN.md` §1)와 지금(PC 키보드 싱글)의 간격이 줄지 않았다: UI 는 전부 IMGUI(`OnGUI` 4파일, uGUI·UI Toolkit 0파일), 터치·게임패드 입력 0파일, 모바일 빌드 미확인, `Net/` 2,721줄은 격리 월드 구조.
- **결정 (사용자, 2026-11-12, 질문 대화의 답 원문)**:
  - **v1.0 도착점** — "1+4": ① PC 싱글 + 봇전(M5 까지)을 v1.0 으로 잡는다 ④ 에이전트 하네스 실험이 공동 주목적이다. 멀티(M7)·모바일·아이템전은 v1.0 밖이고, v1.0 뒤에 범위를 다시 정한다.
  - **다음 순서** — "권고안 일괄, 사람 플레이 없이": `docs/PROPOSAL_M5_AI_OPPONENTS.md` 의 D1~D11 은 권고안으로 채택하고 MP1 설계부터 한다. 다른 사람(지인) 플레이테스트는 하지 않는다 — 플레이 확인은 계속 사용자 한 명이다.
  - **아트 우선순위** — "캐릭터·모션 (Recommended)": 러너 모형·달리기 모션 먼저. 장식 킷은 K1 남은 트랙(PALM·SUMMIT) 뒤 보류(K5·K2·K7·K6).
  - **기존 트랙** — "그대로 유지": 카트라이더 배치를 옮긴 SKYWAY·FINGER LANES·PALM COAST 는 재설계하지 않는다. 새로 만드는 트랙은 기존 게임 트랙을 옮기지 않고 원본으로 설계한다(Claude 가 밝힌 작업 방침).
  - **D2a 몸싸움 방식** — "직접 밀어내기 (Recommended)": 제안서 D2a 에 권고 표시가 없어 따로 물었다. 러너끼리는 Unity 물리에서 서로 빼고 우리 코드가 몸 겹침을 풀어 밀어낸다(무게 반영).
  - **MP1 구조** — "대안 스파이크 먼저 (Recommended)": 아래 "MP1 구조 대안".
- **D1~D11 채택 내용** (제안서 "답:" 칸에 같은 내용): D1 구조 8인·첫 판정 봇 3명 / D2a 직접 밀어내기 / D2b 무게를 쓴다, 감속 여부는 MP2 플레이로 / D2c 봉·앵커 동시 사용 / D2d 땅·벽·시야는 트랙만 / D3a 러버밴딩 없이 시작 / D3b 기록 대조로 고른다 — 확인된 값은 SUMMIT DROP 빠름 사용자 67.16 s 대 시험 봇 90.0 s(제안서 `:132`)라 시험 봇 수준으로는 겨루기가 안 된다는 것이 Claude 의 판단이고, MP2 요청서에서 확정한다 / D4 봇전은 고스트·기록 없음 / D5 순위는 주행 중·결과 둘 다 / D6 규칙은 다섯 트랙 공통, 첫 판정 STADIUM / D7 러너별 달림·결승 상태, 끝 규칙은 MP2 플레이로 / D8 출발은 격자, 복귀 겹침은 플레이 뒤 / D11 틱 순서는 아래. **D9(봇 러너 구성)·D10(로비 진입)은 제안서에 권고가 없어 비어 있다** — MP1 구조에 영향이 없고(제안서 `:20`) MP2 요청서에서 묻는다.
- **MP1 구조 대안 (2026-10-30 "멀티 개발 순서" 의 공유 월드를 다시 본다 — 아직 뒤집지 않았다)**: 그 항목의 이유는 "러너끼리 물리가 닿으려면 공유 월드가 필요하다" 였다. D2a 직접 밀어내기 + D2d 트랙만 보는 질의면 Unity 물리가 러너끼리의 접촉을 중재하지 않으므로, 러너마다 씬 복사본(격리 월드, 2026-10-10 "P3 C1a")을 유지하고 조정자가 N개 게임을 같은 틱으로 돌리며 접촉만 계산하는 구조가 가능하다.
  - 격리 월드 쪽 근거: 번갈아 돌린 복사본이 혼자 런과 비트 일치(2026-10-10 항목), 같은 물리 씬에서는 757틱에 갈라짐(같은 항목의 음성 대조), 서버가 이미 복사본 N개를 로드하고 카메라·리스너를 끈다(`RaceServer.cs:100-106,152-153`), 게임마다 단계가 따로라 러너별 결승 상태(D7)가 이미 있다.
  - 공유 월드 쪽 부담(코드 읽기): `ChainRushGame` 이 러너·`RacerState`·입력을 하나씩 든다(`:15,28,31`), `RunnerMotor` 의 `game.Racer` 30곳·`game.Track` 11곳·`game.PlayCue` 7곳, `CircuitRace` 의 랩 카운터·`lastS`·`SetFocus` 가 한 사람 몫(`:37,41,132`), `RaceWorld.FromScene` 은 러너가 둘이면 예외(`:53-57`), 러너끼리 무시하려면 레이어(ProjectSettings) 또는 `IgnoreCollision` — 매 틱 컨트롤러를 껐다 켜는 구조(`RunnerMotor.cs:596-600`)에서 유지되는지 확인 못 함.
  - 격리 월드 쪽에서 모르는 것: 복사본 4·8개의 로드 시간·메모리·프레임(재지 않았다, 2개 넘는 복사본은 확인된 적 없다), 복사본마다 딸려 오는 조명·도로 렌더러·HUD·`RaceAssembler` 의 장식 씬 로드를 끄는 방법.
  - **정한 것**: 스파이크로 비용을 먼저 잰 뒤 구조를 확정한다. 스파이크도 §3-2 요청서부터.
  - **틱 순서 제안 (D11, 두 구조 공통, 확정 전)**: 전원 입력 수집 → 전원 이동 → 접촉 풀기 → 랩·결승 판정.
- **이 결정으로 바뀌는 방침**: UI 는 IMGUI 를 유지한다(PC 한정 v1.0 에서는 전환이 필수가 아니다 — M5 순위 HUD 도 `ChainRushHud` 형식). `Net/` 은 동결하고 MP1 에서는 컴파일만 유지한다. 마일스톤 게이트에 `Net` 테스트를 계속 둘지는 MP1 설계 때 묻는다.
- **회고에서 나온 후속 후보 (정하지 않았다, 각각 따로 합의)**: ① 문서 다이어트 — `HANDOFF.md` 는 283줄 약 41,600토큰으로 한 번에 읽히지 않았고(문서 자신은 "끝난 항목은 지우고", `:5`), 이 파일은 875KB, `ARCHITECTURE.md` 는 최종 갱신 2026-10-01(스크립트 26개로 적힘, 실제 96 · 지운 씬 `ChainRushProcedural` 이 남음) ② 서킷에서 쓰지 않는 공격·체력·`CourseTarget`·`Enemy` 테스트 정리(아이템전이 v1.0 밖) ③ 트랙별 프레임·드로콜 측정(PALM 렌더러 5,361·STADIUM 정점 약 98만, 재지 않음) ④ 캐릭터 아트 요청서 — 스타일 유지 여부, 스켈레탈 리그·`Animator` 도입 여부(지금 `Animator` 0파일, 첫 사례), 5종 범위.
- **버린 대안**: 원래 목표 유지(8인·PC+모바일을 v1.0 으로) — UI 교체·터치 입력·모바일 성능 예산이 M5 앞으로 온다. PC 소규모 멀티까지를 v1.0 으로. 사람 플레이테스트 병행 — 사용자가 하지 않기로 했다. 세 트랙 재설계.
- **검증**: 문서만 바꿨다(`Assets/`·`Packages/`·`ProjectSettings/` 미접촉). 코드 지표는 2026-11-12 `dev` `0d13284` 에서 잰 값이다 — 런타임 96파일 14,837줄, 에디터 11파일 6,763줄, 테스트 53파일 18,078줄, 병합 제외 커밋 299건 중 `docs` 117·`feat` 77·`fix` 32. 코드 전문 리뷰는 `ChainRushGame`·`RunnerMotor`·`GrappleController`·`CircuitRace`·`RaceWorld`·`SimSnapshot`·`TrackFollower`·`CircuitBot` 만 했다.

## 2026-11-11 · K1 RoadsideKit 2차: SKYWAY 코너 화살표판·광고판 (`feature/art-roadsidekit-skyway`)

- **결정 (사용자, 2026-11-11)**: FINGER 에서 정한 방식을 그대로 SKYWAY 에 쓰는 §3-2 요청서에 "ㄱㄱ". 고속도로 대형 광고판(10.4 m)과 막다른 베이 판은 대상이 아니다. SKYWAY 막대(0.2)는 FINGER(0.22)보다 조금 가늘었지만, 킷 화살표가 한 가지 굵기라 FINGER 와 같은 모양이 된다.
- **연결**: `SkywayEnvironmentBuilder` — 화살표판(`:599-606`): 판 `Box` → `ChevronPanel`(크기 그대로 3.8 × 1.4, `yellow`), 막대 여섯 → `ChevronArrow` 셋(x −1.1 / 0 / +1.1, 판 중앙 높이, FINGER 와 같은 (0.9, 1, 0.12), `black`, 판 앞 `ArrowOut` 0.135). 옛 막대 끝이 `outside > 0` 이면 +x 였으므로 FINGER 와 같은 규칙: `outside > 0` 이면 그대로, 아니면 판 법선(z)으로 180°. 광고판(`:635-646`): 틀 → `BoardFrame`(`ink`), 면 → `BoardFace`, 면·원판·글줄 셋을 `FaceRecess` 0.09 뒤로, 틀 위에 `BoardCap`(`ink`, 높이 `y + 2.8 + 1.6 + 0.125` — SKYWAY 틀 중심이 FINGER 의 3.6 이 아니라 2.8). 다리는 상자 그대로. 메시는 `Build()` 시작에서 `KitMesh`, 상수 `ArrowOut`·`FaceRecess` 는 FINGER 와 같은 형식.
- **통로**: SKYWAY 광고판은 도로 가장자리 1.75 m 뒤. 앞으로 가장 나온 것이 원판(앞면 0.11 — `Shape` 의 크기는 미터라 두께 0.06, `ExteriorKit.cs:64,70-71`)에서 차양(앞면 0.15)으로 바뀌어 4 cm 더 나오고, 가장자리에서 1.64 → 1.60 m(검사 여유 0.7 m). 재빌드의 가장 가까운 렌더러는 전후 모두 0.85 m(광고판이 아니다).
- **측정** (프리팹을 프리뷰 씬에 꺼내 `MeshFilter.sharedMesh.vertexCount` 합): 화살표판 18개 정점 3,888 → 6,696(판마다 216 → 372, 다리 48 포함), 렌더러 162 → 108. 광고판 37개 정점 10,804 → 13,986(판마다 292 → 378), 렌더러 296 → 333. 장식 전체 정점 394,890 → 400,880(+1.5%), 렌더러 4,622 → 4,605. 요청서 추정(+2,808, +3,182, 렌더러 −54·+37)과 같다.
- **검증** (에디터 6000.3.19f1): 컴파일 `*** Tundra build success (2.53 seconds), 2 items updated, 1618 evaluated`, `error CS` 0. 재빌드 `Skyway Exterior clearance: nearest renderer bounds 0.85 m outside a road edge (at least 0.7 m below 9 m over the road); 200 parts under the slab; 190840 vertices checked one by one.`, `SkywayExterior: 4605 renderers; physics=0; runtime scripts=0; road corridor clear.`, Console 에러·경고 0. 전후 렌더(같은 카메라, 판 앞 오른쪽): `Corner chevrons 2` 전후 `<<<`(화살표 회전 0°), `Corner chevrons 0` 전후 `>>>`(180°) — 방향 그대로, 꺾쇠가 한 덩어리. `Advertising board 1`: 면이 틀 안으로 들어가 틀이 액자로 보이고 차양이 생겼다. 프리팹에 RoadsideKit GUID 183회(판 18 + 화살표 54 + 광고판 37 × 3). 테스트: 영향 평가 — 바뀐 `Assets/` = 빌더(에디터 전용)·SKYWAY 장식 프리팹. 프리팹 GUID 를 참조하는 곳은 `ChainRushTrackSkyway.unity` 하나이고 그 씬을 여는 테스트는 없다. 룩 `TrackLook_Skyway.asset` 은 재임포트만 되고 diff 가 없다(그것을 읽는 `ChainRushFlowTests.cs:913` 도 장식 씬 이름 `""` 로 장식을 열지 않는다). 시뮬 참조 없음 → 돌린 테스트 없음. `select-tests -Range dev...HEAD`: 빌더·프리팹 → "PlayMode 없음, 지문 없음", Lap 없음, EditMode "전체"(도구가 전체로 판정 → 영향 평가로 정함, 위 줄).
- **병합 전 리뷰** ("그 밖" 규모 — 프리팹 diff 로 30줄 초과): Codex 설계 리뷰 approve, "No material findings."(Unity MCP 허용 안 됨). `unity-reviewer` 치명·중대 0, 보통 1 — 주석과 이 항목 "통로" 줄이 원판 앞면을 0.14 로 적었으나 `Shape` 크기가 미터라 0.11 이 맞다(차양이 1 cm 가 아니라 4 cm 더 나옴) → 둘 다 고침(동작 그대로, 1.60 m·검사 0.85 m 그대로). 제안 0. reviewer 확인: §0/§1/§5 위반 없음, 화살표 회전이 옛 막대 끝과 같은 방향, 차양 밑면 = 틀 윗변 y+4.4, `FaceRecess` 다섯 곳, 정점·렌더러 산술, 프리팹 RoadsideKit GUID 183회(dev 0회), `Chevron` 64 = 54 + 막다른 베이 막대 10. 비용 `unity-reviewer` 52,718 토큰.
- **사용자 플레이 확인 (2026-11-11, 병합 뒤)**: "플레이감도 확인했다." — 요청서의 확인 질문 "화살표 방향이 읽혔나" 에 대한 답으로 적는다.
- **남은 것 (K1)**: PALM(판 0.947배, 화살표 3개, 판 어둡고 화살표 노랑), SUMMIT(1.053배, 병합 트랙) — 트랙마다 요청서.

## 2026-11-11 · K1 RoadsideKit 1차: FINGER LANES 코너 화살표판·광고판, 광고판 면은 틀 안으로 (`feature/art-roadsidekit`)

- **결정 (사용자, 2026-11-11)**: `blender-artist` 시안 5종(`ChevronArrow`·`ChevronPanel`·`BoardFrame`·`BoardFace`·`BoardCap`)을 지금 모습과 나란히 본 비교, 15 m 도로 높이 비교에서 새 판을 골랐다("이거 말고 이거대로" — 옛 막대 화살표가 아니라 새 꺾쇠). 나머지는 권장대로: 화살표 굵기 시안 그대로(수직 0.245, 옛 0.22), 광고판 면을 틀 안으로, 화살표를 판 앞면에 붙인다. Unity 연결 요청서(첫 트랙 FINGER, 다른 트랙은 따로)에 "ㄱㄱ".
- **시안·FBX**: 삼각형 — 화살표 32, 판 68(뒤 보강대 2개), 틀 48(구멍 6.0 × 2.8), 면 12, 차양 16(오각 단면, 내보내기 전 삼각화). 모두 닫힌 메시, 피벗 가운데, 슬롯 하나. 일부러 목표(S 200 의 1/3)보다 낮다 — K3 교훈, 화살표는 판마다 2~3개씩 찍힌다. `art-src/RoadsideKit/RoadsideKit.blend`·`.fbx`. 임포트: bounds 표와 같음, 가운데 원점, 노드 회전 0·스케일 1, 화살표 끝은 Unity +X(가운데 높이 정점 x 0.09~0.45, 위 끝 −0.45~−0.09). Unity 정점은 삼각형 × 3 보다 적다(같은 면의 사각형이 정점을 공유) — 화살표 60, 판 144, 틀 80, 면 24, 차양 30.
- **연결**: `FingerLanesEnvironmentBuilder` — 화살표판: 판 `Box` → `ChevronPanel`(크기 그대로), 막대 넷 → `ChevronArrow` 둘(x ±0.8, 판 앞 `ArrowOut` 0.135 = 판 앞면 0.075 + 화살표 두께 절반 — 시안의 0.1 은 화살표 뒤 0.035 가 판에 묻혔다), 반대 방향은 판 법선(z)으로 180°(화살표가 위아래 대칭). 광고판: 틀 → `BoardFrame`, 면 → `BoardFace`, 면과 포스터 무늬(눈·눈동자·글줄)를 `FaceRecess` 0.09 뒤로 — 면 앞면이 틀 앞면보다 4 cm 안쪽, 포스터 무늬는 예전처럼 면보다 앞이라 틀보다 최대 5 cm 앞(이전엔 면이 틀보다 5 cm 앞으로 나와 구멍을 덮어 새 틀이 보이지 않았다, 시안 비교), 틀 위에 `BoardCap`(틀 색, 깊이 가운데는 틀과 같게). 다리는 상자 그대로. 메시는 `Build()` 시작에서 `KitMesh`. `select-tests` 의 킷 FBX 줄에 `RoadsideKit` 을 더했다.
- **측정**: 화살표판 54개, 광고판 12개. 장식 정점 197,970 → 206,778(+4.4%), 렌더러 2,865 → 2,769(화살표 막대 넷 → 둘로 −108, 차양 +12).
- **검증**: 컴파일 `*** Tundra build success (2.70 seconds), 7 items updated, 1618 evaluated`, `error CS` 0. 재빌드 `Finger Lanes Exterior clearance: nearest renderer bounds 0.83 m outside a road edge ...; 0 parts under the slab`, `FingerLanesExterior: 2769 renderers; physics=0; runtime scripts=0; road corridor clear.` 전후 렌더: 화살표판 `Corner chevrons 0`(`>`)·`18`(`<`)·`36`, 광고판 `Advertising board 4` — 러너가 오는 쪽 앞에서. 화살표 방향이 전후 같고, 꺾쇠가 한 덩어리로 또렷하다. 광고판은 틀이 액자처럼 보이고 차양이 생겼다. 테스트: 영향 평가 — 바뀐 `Assets/` = 새 킷 FBX·빌더·FINGER 장식 프리팹 → FINGER 장식 프리팹을 읽는 테스트 없음, 룩 에셋은 이번 재빌드에서 바뀌지 않았다, 시뮬 참조 없음 → 돌린 테스트 없음. `select-tests -Range dev...HEAD`(바꾼 패턴으로 실행): `RoadsideKit.fbx`·`.meta` → "PlayMode 없음, 지문 없음 (장식 킷 FBX ...)", 빌더·프리팹 → PlayMode 없음, Lap 없음.
- **병합 전 리뷰** ("그 밖" 규모): Codex 설계 리뷰 approve, "No material findings."(Unity 조회는 Codex 쪽 승인 정책으로 거부돼 렌더 확인은 못 했다 — Unity MCP 를 열지 않은 실행). `unity-reviewer` 치명·중대 0, 보통 1 — 면이 틀보다 "3 cm 안쪽" 은 4 cm 가 맞다 → 고침. 제안 2 — 요청서 K1 표의 낡은 줄 번호 → 고침, 광고판 주석이 포스터 무늬도 틀 안이라고 읽힘(실제로 눈동자는 틀보다 5 cm 앞) → 주석 고침(동작 그대로). reviewer 확인: 화살표 회전이 옛 막대 끝과 같은 방향, 같은 평면 세 곳(화살표 뒤·판 앞, 차양 밑·틀 위, 면 옆·구멍 안벽)은 법선이 반대이고 머티리얼이 모두 뒷면 컬링이라 깜빡임 없음(양면으로 바꾸면 생길 수 있다), `.meta` 는 Unity 기본 임포터 값(TreeKit 과 guid 빼고 같음), 프리팹에 RoadsideKit GUID 198회(화살표 108 + 판 54 + 광고판 36). 비용 `unity-reviewer` 64,205 토큰.
- **남은 것 (K1)**: SKYWAY(화살표판 3개 x ±1.1, 광고판 같은 꼴), PALM(판 3.6 × 1.3 → 0.947배, 화살표 3개 작게 굵기 0.16, 판 어둡고 화살표 노랑), SUMMIT(판 4.0 × 1.5 × 0.2 → 1.053배, 병합 트랙) — 트랙마다 요청서.

## 2026-11-11 · K3 나무 4차: PALM COAST 야자를 TreeKit 으로, 잎 숙임 22° → 10° (`feature/art-treekit-palm`)

- **결정 (사용자, 2026-11-11)**: 각도 비교 렌더(지금 상자 잎 22° / 킷 22° / 10° / −10°)와 비용(+51%)을 보고 "권장대로" — 잎 숙임 10°, 지금 잎 메시 그대로(가볍게 다시 만들지 않음, 플레이로 판단).
- **연결**: `PalmCoastEnvironmentBuilder.Palm()` — 줄기 고리 6개 `Cylinder` → `PalmTrunk`(위치·크기 식 그대로), 잎 7장 상자(1.1 × 0.12 × 4.6, 22°) → `PalmFrond` 실제 크기(1.2 × 1.2 × 4.0, 1배). 잎자루 끝(메시 중심에서 (0, 0.56, −2), Unity 에서 정점으로 잼 — 잎끝은 y −0.57 로 1.2 m 처진다)을 코코넛 높이(정수리 −0.3)·줄기에서 0.35 m 바깥에 고정하고 그 점을 축으로 `FrondTilt` 10° 숙인다. 코코넛 구는 킷에 없어 그대로.
- **각도**: 22° 는 잎 자체 처짐과 겹쳐 접힌 우산처럼 늘어졌고, −10° 는 평평하게 퍼졌다. 10° 는 정수리에서 위로 휘었다 끝이 처지는 아치. 킷 잎은 얇고(0.04) 잎 조각이 갈라져 멀리서 상자 잎보다 가늘고 성기게 보인다 — 플레이에서 판단하기로 했다.
- **측정**: 야자 62그루, 그루당 정점 1,351 → 3,193(줄기 124 → 144 × 6, 잎 24 → 270 × 7), 렌더러 그대로. 장식 전체 정점 223,894 → 338,098(+51%), 렌더러 5,361 그대로. 비율은 K3 중 가장 크나 절대량은 SKYWAY(394,890)보다 적고, 야자는 도로 7 m 옆에 줄지어 서 가려지지 않는다.
- **부산물**: 재빌드가 룩을 다시 저장해 `TrackLook_PalmCoast.asset` 에 `music`·`ambience` 빈 필드 두 줄(SKYWAY 와 같음, 값 변화 없음, 커밋에 넣음). 같은 재빌드 중 `ProjectSettings/ProjectSettings.asset` 에서 iOS 열 상태 필드 3줄이 빠졌다 — 로컬 에디터 6000.3.19f1 과 리포 6000.3.25f1 의 버전 차이라 커밋하지 않았다.
- **검증**: 컴파일 `*** Tundra build success (8.32 seconds), 7 items updated, 1618 evaluated`, `error CS` 0. 재빌드 `Palm Coast Exterior clearance: nearest renderer bounds 0.97 m outside a road edge ...; 0 parts under the slab`, `PalmCoastExterior: 5361 renderers; physics=0; runtime scripts=0; road corridor clear.` 전후 렌더 세 시점(야자 (27.3, −2.9, 221.5)·(151.3, −2.9, 195.8)·(−112.7, −2.9, −53.8), 자리는 프리팹에서 찾음). 테스트: 영향 평가 — 바뀐 `Assets/` = 빌더·PALM 장식 프리팹·PALM 룩(값 변화 없음) → PALM 장식 프리팹을 읽는 테스트 없음, 룩은 로비 항목으로 `ChainRushFlowTests.Lobby_TrackChoices_AreChosenWithTheirSavedSetup("PALM COAST")` 가 읽는다 → 그 1건. 2026-11-11 05:19:57Z `testcasecount=1 result=Passed passed=1 failed=0`(기록함). `select-tests` 가 고른 `Race_WithATrackLook_*` 는 룩을 `CreateInstance` 로 만들어(`ChainRushFlowTests.cs:1478`) 이 에셋을 읽지 않는다.
- **병합 전 리뷰** ("그 밖" 규모): Codex 설계 리뷰 approve, "No material findings." 다음 단계로 — 줄기 기울기를 넣은 잎끝 반경 약 5.71 m 가 배치 반지름 `PalmCrown` 5.2 를 넘는다, dev 의 상자 잎도 약 5.56 m 로 같은 문제, 새 충돌은 확인 못 함. 도로와의 간격은 통로 검사가 실제 bounds 로 잰다(0.97 m) — `PalmCrown` 은 야자끼리·다른 소품과의 예약 간격에만 쓰인다. 이번엔 고치지 않고 주석에 남겼다. `unity-reviewer` 치명·중대·보통 0, 제안 3 — 모두 반영: ① 낡은 "4.6 m" 주석 → 실제 반경과 위 사실 ② 잎자루 위치가 손으로 잰 크기에 기댐 → `FrondSize` 하나로 묶고 `Build()` 에서 메시 크기가 다르면 예외 ③ 0.56 이 윗면 0.58 − 두께 절반이라는 근거를 주석에. 고친 뒤 컴파일 `*** Tundra build success (4.46 seconds)`, 재빌드에서 검사 통과·`PalmCoastExterior: 5361 renderers; ... road corridor clear.`·정점 338,098 그대로. 재빌드한 프리팹은 fileID 와 오브젝트 순서만 달랐다(fileID 를 지우고 줄을 정렬하면 커밋한 것과 같음) — 22만 줄 잡음이라 커밋한 프리팹으로 되돌렸다. 테스트는 다시 돌리지 않았다: 그 뒤 바뀐 `Assets/` 는 에디터 빌더뿐이고 그것을 읽는 테스트가 없다. 비용 `unity-reviewer` 54,076 토큰.
- **K3 끝**: 소나무(SUMMIT)·둥근 나무(FINGER·SKYWAY)·야자(PALM). 다음 킷은 K1(코너 화살표판·광고판, 네 트랙) — Blender 시안부터.

## 2026-11-11 · K3 나무 3차: SKYWAY 둥근 나무를 TreeKit 으로, 수관 구 둘 → RoundCrown 하나 (`feature/art-treekit-skyway`)

- **결정 (사용자, 2026-11-11)**: 요청서(수관 A 구마다 `RoundCrown` / B 두 구를 감싸는 `RoundCrown` 하나, B 권장)에 "ㄱㄱ" → B.
- **연결**: `SkywayEnvironmentBuilder.Tree()` 의 첫 갈래(kind < 0.5) — 화분 `Box` → `Planter`(2.2 × 0.5 × 2.2 그대로 — 킷 2.4 를 가로만 0.92배, 높이는 1배. 요청서의 "균일 0.92배" 와 다르다), 줄기 `Box` → `TreeTrunk`, 수관 `Crown`(4.6 × 4.2 at 4.6)·`Crown top`(2.8 × 2.6 at (0.6, 6, 0.4)) → 두 구를 감싸는 `RoundCrown` 하나(4.6 × 4.8 × 4.6 at 4.9, 모두 × size). 색은 `Crown` 의 고르기(leaf/leafDark, 같은 해시) 그대로, 위 구의 밝은 잎 섞임은 사라졌다. 메시는 `Build()` 시작에서 `KitMesh` 로(FINGER 와 같은 형식). 사이프러스 41·덤불 21무더기는 킷에 맞는 모양이 없어 범위 밖.
- **B 를 고른 이유**: `RoundCrown` 이 이미 덩어리 3~4개가 뭉친 실루엣이라 구 두 개로 내던 "뭉친 수관" 을 하나가 낸다. A 는 정점 +53,476(+13.8%), B 는 +8,526(+2.2%)에 렌더러 −58.
- **측정**: 둥근 나무 58그루, 그루당 정점 926 → 1,073, 렌더러 4 → 3. 장식 전체 정점 386,364 → 394,890(+2.2%), 렌더러 4,680 → 4,622.
- **부산물**: 재빌드 메뉴가 룩도 다시 저장해 `TrackLook_Skyway.asset` 에 `music: {fileID: 0}`·`ambience: {fileID: 0}` 두 줄이 생겼다. 클래스에 이미 있는 필드가 직렬화된 것(FINGER·SUMMIT 룩 에셋에는 이미 있다), 값 변화 없음. 커밋에 넣었다.
- **검증**: 컴파일 `*** Tundra build success (2.68 seconds), 7 items updated, 1618 evaluated`, `error CS` 0. 재빌드 `SkywayExterior: 4622 renderers; physics=0; runtime scripts=0; road corridor clear.`, `Skyway Exterior clearance: nearest renderer bounds 0.85 m outside a road edge ...; 200 parts under the slab; 190840 vertices checked one by one.`, 에러·경고 0. 전후 렌더 세 시점(나무 (−13.6, −1.3, 40.4)·(117.7, −1.3, −33.8)·(−46.4, −1.3, −47.9), 자리는 프리팹에서 찾음): 수관이 덩어리 실루엣으로, 화분에 테두리, 열린 수관 구멍 안 보임. 테스트: 영향 평가 — 바뀐 `Assets/` = 빌더·SKYWAY 장식 프리팹·SKYWAY 룩(값 변화 없음) → SKYWAY 장식 프리팹을 읽는 테스트 없음(`TrackDefinitionTests` 장식 테스트는 SUMMIT·STADIUM 뿐, 프리팹 GUID 참조는 `ChainRushTrackSkyway.unity` 하나), 시뮬 참조 없음. 룩 에셋은 `ChainRushFlowTests.Race_SkywayHighway_ClosesBothEndsAndJoinsRampsWithoutInteriorGuards`(`:913`)가 읽어 그 1건을 돌렸다 — `select-tests` 가 고른 `Race_WithATrackLook_*` 는 PALM 에 테스트용 머티리얼을 써 이 에셋을 읽지 않는다(`:1468-1475`). 2026-11-11 05:03:39Z `testcasecount=1 result=Passed passed=1 failed=0`(기록함, 기준 `7fbad3f`).
- **병합 전 리뷰** ("그 밖" 규모): Codex 설계 리뷰 approve, "No material findings." `unity-reviewer` 치명·중대 0, 보통 1 — 요청서 `Planter` 행이 "균일 0.92배" 로 남아 결과 줄과 어긋남 → 고침. 제안 — 룩을 읽는 테스트 1건을 영향 평가에 적기 → 위 줄. reviewer 는 프리팹에서 `Crown top` 0·`Crown` 58·TreeKit 메시 3종 각 58회를 grep 으로 확인했다.
- **남은 것 (K3)**: PALM 야자(`PalmTrunk`·`PalmFrond`, 잎 숙임 각도는 렌더 보고).

## 2026-11-11 · K3 나무 2차: FINGER LANES 둥근 나무를 TreeKit 으로 (`feature/art-treekit-finger`)

- **결정 (사용자, 2026-11-11)**: Unity 연결 요청서(FINGER 만, SKYWAY·PALM 은 따로)에 "ㄱㄱ". 재빌드·전후 렌더·비용 보고에 "굳".
- **연결**: `FingerLanesEnvironmentBuilder` 의 `Tree()`(Round village tree) 세 도형 — 화분 `Box` → `Planter`, 줄기 `Box` → `TreeTrunk`, 수관 `Sphere` → `RoundCrown`. 위치·크기 식은 그대로. 메시는 `Build()` 시작에서 `KitMesh` 로 한 번 읽는다(SUMMIT 과 같은 형식). Blender 작업은 없었다 — 1차 FBX 에 이미 들어 있다. 모퉁이 화분(원기둥)·덤불·언덕은 이번 범위 밖.
- **비용 (측정)**: 킷 메시 Unity 정점·삼각형 — `RoundCrown` 775 / 261, `TreeTrunk` 128 / 76, `Planter` 170 / 86. 내장 구 439 / 760, 큐브 24 / 12. 나무 한 그루 정점 487 → 1,073, 삼각형 784 → 423, 렌더러 3 그대로. FINGER 는 병합하지 않는 트랙이라 렌더러 수가 비용에 같이 잡힌다 — 2,865 그대로. 나무 65그루, 장식 전체 정점 159,880 → 197,970(+23.8%). 비율은 SUMMIT 1차(+9.9%)보다 크나 전체가 SUMMIT(1,038,780)의 약 1/5 이고, 이 나무들은 도로 옆 5.4 m·정원에 홀로 서 있어 소나무 숲처럼 가려지는 디테일이 아니다 — 그래서 가벼운 수관을 만들지 않았다.
- **열린 수관**: `RoundCrown` 은 겹친 덩어리 안쪽 면을 지워 닫혀 있지 않다. 전후 렌더 세 시점(도로 옆 (37.8, −1.3, −3.2), 정원 (73.5, −0.8, 60.8), 도로 옆 줄 (187.4, −1.3, 5.8), 나무 자리는 프리팹에서 찾음)에서 구멍은 보이지 않았다.
- **검증**: 컴파일 `*** Tundra build success (32.42 seconds), 7 items updated, 1618 evaluated`, `error CS` 0. 재빌드 `FingerLanesExterior: 2865 renderers; physics=0; runtime scripts=0; road corridor clear.`, `Finger Lanes Exterior clearance: nearest renderer bounds 0.83 m outside a road edge ...`, 에러·경고 0. 테스트: 영향 평가 — 바뀐 `Assets/` = 빌더·FINGER 장식 프리팹 → FINGER 장식 프리팹을 읽는 테스트 없음(`TrackDefinitionTests` 의 장식 테스트는 SUMMIT `:201`·STADIUM `:251` 뿐), 장식에 콜라이더가 없어 시뮬·지문 없음 → 돌린 테스트 없음. `select-tests -Range dev...HEAD`: 빌더·프리팹 모두 "PlayMode 없음, 지문 없음", Lap 없음, EditMode 는 "전체" 로 나왔으나 FINGER 프리팹을 읽는 것이 없어 돌리지 않았다(§9-2 ①②).
- **병합 전 리뷰** ("그 밖" 규모 — 프리팹 diff 로 30줄 초과): Codex 설계 리뷰 approve, "No material findings." `unity-reviewer` 치명·중대·보통 0, 제안 2 — ① 요청서 K3 "쓰는 곳" 의 FINGER 줄 번호가 낡음 → 고침 ② `Build()` 의 대입 셋 한 줄 → SUMMIT 과 같은 형식이라 두었다. reviewer 는 프리팹에서 TreeKit GUID 195회(65 × 3)를 grep 으로 확인했다. 비용 `unity-reviewer` 41,530 토큰. Codex 리뷰는 사용량을 남기지 않는다.
- **남은 것 (K3)**: SKYWAY 둥근 나무(`Crown`·`Crown top` 두 구, 화분 2.2·줄기 0.4 — 같은 킷, 사이프러스·덤불은 범위 밖으로 둘지 요청서에서), PALM 야자(`PalmTrunk`·`PalmFrond`, 잎 숙임 각도).

## 2026-11-11 · K3 나무 1차: SUMMIT DROP 소나무를 TreeKit 으로, 잎은 가벼운 판 (`feature/art-treekit`)

- **결정 (사용자, 2026-11-10~11)**: 시안 6개(소나무 잎 한 층·둥근 수관·줄기·야자 줄기·야자 잎·화분)를 이전 모습(Unity 에서 같은 조립 값으로 다시 세워 찍음)과 비교하고 "ㄱㄱ 새것 좋다". Unity 연결 요청서(첫 트랙 SUMMIT DROP 소나무, 다른 트랙은 따로)에 "ㅇㅇ". 무거운 판의 비용(아래)을 보고 "B ㄱㄱ"(가벼운 잎). 플레이 "괜찮다".
- **연결**: `SummitDropEnvironmentBuilder` 의 소나무 잎(`cone`)과 줄기(`Box`)를 `KitMesh("TreeKit", "PineCrownTier"/"TreeTrunk")` 로. 위치·크기 식은 그대로(킷 메시도 bounds 가운데 원점). 코드로 만들던 `ConeMesh` 삭제. `select-tests` 의 킷 FBX 줄을 `(RockKit|TreeKit)` 로 넓혔다.
- **무거운 판 → 가벼운 판**: 처음 시안의 잎 한 층(삼각형 176, Unity 정점 474 — 플랫이라 면마다 정점이 갈라진다)으로는 SUMMIT 장식 정점이 945,280 → 1,436,580(+52%). 그런데 숲에서는 층이 겹쳐 이빨이 대부분 가려져 레이스 시점 렌더가 이전과 거의 같았다. 사용자 질문 "맵 디테일 추가하려는건데 비용이 많이 높은가?"에 대한 판단: PC 만이면 문제 아닐 가능성이 크나 미측정, 목표에 모바일이 있고(`DESIGN.md:137`) SUMMIT 은 그룹을 머티리얼별 한 메시로 병합해 화면 밖 나무까지 늘 그린다 — 보이지 않는 디테일에 비용을 쓰는 것이 문제. 가벼운 판(꼭짓점 + 이빨 7·홈 7, 바닥은 홈 높이에서 닫음, 삼각형 28, Unity 정점 84)으로 바꿔 1,038,780(+9.9%). 굵은 이빨 덕에 레이스 시점에서 오히려 실루엣 차이가 잘 보였다.
- **잎 없는 소나무 (에디터 렌더 문제)**: 가벼운 판 재빌드 뒤 미리보기 렌더에 잎 없이 줄기만 선 소나무가 여럿 보였다. 데이터는 정상이었다(1,020층 모두 제 줄기 위·제 높이, 서브메시 범위·면 방향 정상, 재임포트로도 그대로). 에디터를 다시 켠 뒤에는 정상으로 그려졌다. 재빌드가 기존 메시 에셋을 `CopySerialized` 로 덮어쓸 때(정점 수가 크게 줄었다) 에디터가 그리기용 사본을 갱신하지 않은 것으로 보인다 — 조건은 확인 못 함. K4 첫 재빌드 뒤 "후" 렌더가 "전" 과 비트 단위로 같았던 것도 같은 원인이었을 가능성이 크다. 재빌드 뒤 렌더가 이상하면 에디터부터 다시 켜고 본다.
- **검증**: 임포트 — 메시 6개 bounds·가운데 원점·노드 회전 0·스케일 1, 가벼운 잎 84 정점·28 삼각형 전부 플랫. 재빌드 `Summit Drop placed 340 pines and 26 crags (...)`, `Summit Drop validation PASS: ... 38 renderers; 1038780 vertices; decoration physics=0`. 테스트(영향 평가: 바뀐 `Assets/` = TreeKit.fbx·빌더·SUMMIT 숲 메시 → 장식 프리팹을 읽는 유일한 테스트 `TrackDefinitionTests.cs:204`, 시뮬 참조 없음): `TrackDefinitionTests.SummitDrop_Exterior_NoPoleShaftHangsAboveTheGround` — 무거운 판 2026-11-10 05:51:45Z, 가벼운 판 2026-11-11 04:06:47Z, 둘 다 `testcasecount=1 result=Passed passed=1 failed=0`(기록함). 전후 렌더는 소나무 정점에서 자리를 찾아 같은 시점(S218·S540·S1080).
- **병합 전 리뷰** ("그 밖" 규모, SUBAGENTS §7-5): Codex 설계 리뷰 approve, "No material findings". `unity-reviewer` 치명·중대·보통 0, 제안 4 — 모두 반영: ① `KitMesh` 가 원점이 bounds 가운데인지 검사(슬롯 수 검사와 같은 방식, 어긋나면 예외 — `Shape` 는 원점을 그대로 놓아 어긋난 FBX 는 소나무가 조용히 뜨거나 묻힌다) ② ③ 요청서의 지운 `ConeMesh`·`CragMesh` 설명과 낡은 줄 번호 ④ 아래 검증 원문.
- **검증 원문 (리뷰 반영 뒤 최종 코드)**: 컴파일 `*** Tundra build success (11.69 seconds), 7 items updated, 1618 evaluated`. 재빌드에서 원점 검사가 RockKit·TreeKit 메시 모두 통과, 결과물 변화 없음(`git status` 에 `Assets/` 는 `ExteriorKit.cs` 뿐), `Summit Drop validation PASS: ... 1038780 vertices`. 테스트 2026-11-11 04:12:18Z `testcasecount=1 result=Passed passed=1 failed=0`(기록함). `select-tests -Range dev...HEAD`: `TreeKit.fbx`·`.meta` → "PlayMode 없음, 지문 없음 (장식 킷 FBX (장식 빌더만 읽는다, 시뮬 참조 없음))", 빌더 → 장식 빌더 줄, Lap 없음.
- **비용**: `blender-artist` 시안·FBX·가벼운 판 126,251 토큰(이어서 부른 세 번 합), `unity-implementer` 53,525 + 26,068(덮어쓰기), `unity-reviewer` 57,766. Codex 리뷰는 사용량을 남기지 않는다.
- **남은 것 (K3 나머지, 트랙 하나씩)**: FINGER·SKYWAY 둥근 나무(`RoundCrown`·`TreeTrunk`·`Planter`), PALM 야자(`PalmTrunk`·`PalmFrond` — 코드의 잎 숙임 22° 가 새 잎의 처짐 1.2 m 와 겹쳐 잎끝이 많이 내려간다, 렌더를 보고 각도를 정한다). 둥근 수관 메시는 겹친 덩어리 안쪽 면 59개를 지워 닫혀 있지 않다 — 렌더로 확인. 가벼운 판 교훈대로 FINGER·SKYWAY·PALM 은 병합하지 않으므로(렌더러가 나무마다 남는다) 비용 구조가 SUMMIT 과 다르다 — 정점 수와 렌더러 수를 함께 본다.

## 2026-11-10 · K4 바위: SUMMIT DROP 바위를 RockKit 3종으로, 밑동은 비탈을 따른다 (`feature/art-rockkit`)

- **결정 (사용자, 2026-11-10)**: Unity 연결 합의 요청서에 "ㄱㄱ". 첫 플레이 답: "기존 바위와 뭔 차이가 있는지 모르겠다", "바위 종류가 적어서 그런건지 배치에 있어서 어색하다. 보이기에 공중에 떠있는 바위도 있다". 바닥 맞추기에 "A로 ㄱㄱ", 세 번 실패한 뒤 "비탈에 놓여있으니 크기를 늘려도 안맞는거지 이것에 대한 해결법을 고안해내라" → 밑동 맞추기 설계에 "해결될거같으면 해봐". 두 번째 플레이 "괜찮다 정리해봐".
- **연결**: `ExteriorKit.KitMesh(kit, name)` — `Assets/_Project/Art/Models/<kit>.fbx` 의 메시를 이름으로 찾고 없으면 예외(`ExteriorKit.cs:25-33`). FBX 임포트는 기본 설정 그대로: 메시 bounds (2.00, 1.00, 1.70), 가운데 원점, 노드 회전 0·스케일 1, 각진 면 유지(CragB 134/136 삼각형, `importNormals=Import`), `isReadable=False` 여도 에디터 빌드에서 정점을 읽는다.
- **떠 있던 바위**: 원인은 메시가 아니라 배치였다. 이전·새 메시 모두 바닥이 폭·깊이 100% 로 평평하다(측정). `Place` 가 중심 한 점의 지면만 읽어(`SummitDropEnvironmentBuilder.cs` `Place`) 폭 10~16 m 바닥이 비탈 아래쪽으로 떴다 — 30개 중 29개, 3~12 m, 낭떠러지에서 최대 33 m(바위별 임시 로그로 잼, 로그는 지웠다). 이전 바위도 같은 식이었으니 같은 자리에서 떠 있었을 것이다(이전 화면이 없어 확인 못 함).
- **버린 해결 (실패 셋)**: ① 바위를 통째로 내려 모든 모서리를 묻기 — 30개 모두 내려가고 최대 32.6 m, 통째로 묻힘. ② 위는 두고 바닥만 아래로 늘리기 — 통로 검사 실패 `Crag 20: margin 0.66 m`, `Crag 35: margin 0.70 m`. ③ ② + 높이의 절반까지 — `Crag 35: margin 0.56 m`, 8개는 여전히 1~23 m 뜸. 한 덩어리로 내리거나 늘리면 낮은 쪽을 맞추는 만큼 높은 쪽이 묻히거나 아래 도로에 닿는다. 그 밖에 비탈 방향으로 기울이기(키 큰 바위가 눕는다), 밑동에 돌무더기 메시(새 아트)도 버렸다.
- **고른 해결 (밑동 맞추기)**: 바위마다 킷 메시 사본을 만들어 기울기·크기를 정점에 넣고, 아래 40%(`CragFoot`) 의 정점을 그 밑 지면 −0.45 m 까지 끌어내린다(맨 아래 전부, 40% 지점 0). 위 60% 는 그대로. 낮은 쪽만 내려가 묻히지 않는다. 높이의 60%(`CragReach`) 넘게 늘려야 하는 자리는 낭떠러지라 놓지 않는다 — 바위벽은 건너뛰고 굽이 바위는 다음 후보 거리로, 굽이 넷 중 모든 후보가 낭떠러지인 S1605 는 필수 표지라 첫 빈 자리에 끝까지 늘려 놓는다. 노멀은 사본에서 다시 계산(각진 면). SUMMIT 은 어차피 머티리얼별로 병합하므로 실행 비용은 같다. 바위 그룹 02·05 의 병합 전 검사는 상자 경계가 기운 바위에서 도로 쪽으로 부풀어 정점 검사로 넘겼다(그룹 10 과 같은 이유). 병합 뒤 전체 정점 검사는 그대로다.
- **검증**: 컴파일 `Tundra build success`, 에러 0. 재빌드 `Summit Drop placed 333 pines and 27 crags (26 with a foot reaching down the slope, longest 31.67 m; 6 spots skipped at a drop)`, `Summit Drop validation PASS: main road and shortcut clear; 15 pole shafts clear; 38 renderers; 945081 vertices; decoration physics=0` (이전 932,829). 테스트(영향 평가: 바뀐 `Assets/` = RockKit.fbx·빌더·`ExteriorKit`·SUMMIT 장식 메시 → 장식 프리팹을 읽는 유일한 테스트 `TrackDefinitionTests.cs:204`, 시뮬 참조 없어 지문·Lap 없음): `TrackDefinitionTests.SummitDrop_Exterior_NoPoleShaftHangsAboveTheGround` 1건 2회 — 04:47:54Z(밑동 맞추기 전) `testcasecount=1 result=Passed passed=1 failed=0`, 05:14:07Z(최종 코드) 같은 값. `tools/test-runs.ps1 -Record` 로 남겼다. `select-tests` 실행: `RockKit.fbx → PlayMode 없음, 지문 없음`.
- **렌더**: 처음 고른 전 렌더 세 각도에는 바위가 하나도 없었다(전후 PNG 해시가 같았다) — 바위 자리는 메시 정점에서 찾아야 한다. 이전 모습은 지운 `CragMesh` 를 메모리에 다시 만들어 새 3종과 같은 크기(12 × 26 × 10)·머티리얼·카메라로 따로 찍었다.
- **배운 것**: 단위 크기 소품을 크게 늘려 도로에서 멀리 두면 모양 개선이 달리는 중에 잘 보이지 않는다(첫 플레이). 요청서 K4 "다음 킷에 적용할 것".
- **병합 전 리뷰** (`unity-reviewer` + Codex 설계 리뷰, `Assets/` 새 파일이라 "그 밖" 규모, SUBAGENTS §7-5):
  - Codex (needs-attention, medium 1): `Place` 가 바위를 만들기 전에 자리를 예약해, 낭떠러지로 거절된 후보의 예약이 12 m 옆 정상 후보를 막았다(예약 간격 15.6 m = 1.45r + 1.15r). 그래서 S1605 가 31.67 m 예외 배치로 갔다. 맞았다 — 바위 호출은 `Place(..., reserve: false)` 로 부르고 `Rock` 이 놓은 뒤에만 예약한다. 고친 뒤 예외 배치는 0, 가장 긴 밑동 31.67 → 14.11 m. 풀린 빈 예약 자리에 소나무가 7그루 더 들어갔다(333 → 340). 권한 회귀 테스트는 빌더 내부 테스트가 없어 새 파일이 필요해 하지 않았고, 재빌드 로그로 확인했다.
  - reviewer (치명 0, 중대 1, 보통 2, 제안 6): 중대 — 예외 배치가 로그 없이 합계에 묻힌다 → S 값·길이를 `LogWarning` 으로 남긴다(고친 뒤 이 경로는 쓰이지 않는다). 보통 — `KitMesh` 가 `ExteriorAssets.ModelFolder` 를 다시 적었다, 05 그룹 검사 제외 주석이 표지판을 빠뜨렸다 → 고침. 제안 중 고친 것: 슬롯 하나 계약을 `subMeshCount` 로 검사, 빈 인자 `ArgumentException`, 클래스 머리 주석, 0.45 를 `GroundSink` 하나로. 고치지 않은 것: 바위를 셀 때 쓰는 0.05 m 기준(로그 전용), `cone` 이 `KitMesh` 예외 때 메모리에 남는 것(결과물 오염 없음).
- **최종 값** (리뷰 반영 뒤): `Summit Drop placed 340 pines and 26 crags (24 with a foot reaching down the slope, longest 14.11 m; 11 spots skipped at a drop)`, `Summit Drop validation PASS: ... 38 renderers; 945280 vertices; decoration physics=0`. 테스트 같은 1건 05:30:00Z `testcasecount=1 result=Passed passed=1 failed=0`(기록함).
- **비용**: `unity-implementer` 54,099 토큰·16회, `unity-reviewer` 73,407·20회. Codex 리뷰는 사용량을 남기지 않는다. 나머지(재빌드·렌더·바닥 맞추기 다섯 번)는 메인이 Unity MCP 로 했다.
- **남은 것**: 다음 킷 K3 나무. 프리팹 diff 는 그룹 자식 순서 한 줄(병합 순서가 바뀐 빌더 출력)이라 커밋에 넣었다.

## 2026-11-10 · 장식 소품 아트 요청서: 공통 킷 7종, 모양은 킷이 · 색은 트랙 팔레트가, 첫 킷 바위 (`docs/art-request-props`)

- **결정 (사용자, 2026-11-10)**: M5 기획을 고민하는 동안 "blender로 기존 맵 스프라이트들 작업해보는건 어떤가", "리소스 낭비를 막기 위해 비슷하게 공통으로 사용될 수 있는 것은 공통으로 사용하는 등 하자. 모든 맵에 대해 아트적 디테일 추가해보자". 요청서 `docs/ART_REQUEST_PROPS.md` 의 A1~A6 를 `gameplay-designer`·`blender-artist` 에게 따로 판단시킨 뒤("둘에게 시켜서 판단해봐") 정했다: 첫 킷 "바위부터", 범위 "한킷씩 끝내고 순서대로"(K4 → K3 → K1 → K5 → K2 → K7 → K6), STADIUM 포함 "ㅇㅇ"(→ K7 깃발 천·벤치), 스타일·폴리곤·크기·두 색 "ㅇㅇ". 요청서가 원본이고 여기에는 이유만 둔다.
- **공유 방식**: 장식은 기본 도형에 단색 팔레트 머티리얼을 하나씩 입혀 조립한다(`ExteriorKit.cs:31-60`). 그래서 킷 메시 하나 = 슬롯 하나 = 지금 `Box`/`Shape` 호출 하나를 바꾸는 단위로 두고, 색은 트랙 팔레트가 고른다. 피벗은 모두 bounds 가운데(지금 호출의 위치 값이 도형 가운데라 위치 식이 그대로다). 길이가 배치마다 바뀌는 것(다리·기둥·벽·굴뚝)은 코드 상자로 남긴다.
- **두 에이전트가 고친 것**: 초안의 "모든 트랙이 머티리얼별로 병합" 은 틀렸다 — SUMMIT 전부, SKYWAY 일부만 병합한다(기획). 피벗이 빠진 부품 12개·긴 축이 없는 5개·180° 돌려 쓰는 부품의 위아래 대칭(제작). 고친 목록은 요청서 7절. 첫 킷을 두고 갈렸다: 제작은 바위(피벗이 지금과 같고 무작위 회전이라 축 착오가 드러나지 않음), 기획은 소나무(큰 실루엣, 답을 가르기 쉬움). 사용자는 바위를 골랐다.
- **K4 시안**: `art-src/RockKit/RockKit.blend`·`RockKit.fbx`. CragA·B·C, 플랫 셰이딩, 삼각형 258·136·276, bounds 2 × 1 × 1.7(Unity), `M_Rock` 하나. 사용자 "좋다"(늘린 모습·플랫 vs 부드러운 비교를 보고). FBX 안 문자열에 Crag 3개·`M_Rock` 만 있고 `Cube`·`Light` 없음(grep). 첫 저장은 `art-src/RockKit/` 가 없어 실패했다 — `save_as_mainfile` 은 폴더를 만들지 않는다. 메인이 승인된 경로의 빈 폴더를 만들고 저장만 다시 맡겼다.
- **베벨과 삼각형 목표**: 요청서 표의 "추정" 은 베벨 전 계산이었고, bmesh 1단 베벨이 두세 배 늘렸다(추정 40~160 → 136~276). 목표는 2절의 "상한의 1/3~1/2" 로 두고 추정은 목표가 아니라고 적었다.
- **비용**: `gameplay-designer` 147,783 토큰·도구 46회, `blender-artist` 판단 92,834·14회, 시안·저장·FBX 86,119·41회(이어서 부른 세 번 합).
- **버린 대안**: 트랙 단위로 여러 킷을 한 번에 재빌드 — 확인 횟수는 주지만 답이 어느 킷 때문인지 가를 수 없다(기획). 텍스처 모델 — 팔레트 공유가 깨진다. 거울 모양 따로 만들기 — 음수 스케일을 병합에 넣게 된다.
- **남은 것**: Unity 연결(`feature/art-rockkit`, 합의 요청서 승인 "ㄱㄱ"): `ExteriorKit` 의 킷 메시 읽기, SUMMIT 바위 교체, 재빌드·전후 렌더·플레이 확인. 프레임 측정 코드는 없다(요청서 7절).

## 2026-11-09 · README 갱신: v0.6 화면으로 다시 찍고, 말투를 합니다체 + 개조식으로 (`docs/readme-refresh`)

- **결정 (사용자, 2026-11-09)**: "Readme에 있는 gif같은거나 설명이나 최신화하고 ai말투 같은거 없애고싶다(러너는 달린다, ~ㄴ다 같은 말투 + 추상적인 말투)". 말투 보기 중 ⓒ(소개 문단은 합니다체, 표·목록은 개조식)를 골랐고, 새 화면 목록(주행 GIF·봉 스윙 GIF, PALM COAST·FINGER LANES·튜토리얼·결과 화면)에 "좋다".
- **문장**: "플레이할 수 있는 단위로 쌓는다", "하네싱 연습", "같이 달리는 맛"·"다시 할 이유" 같은 마일스톤 별칭을 빼고, 버전 표는 들어간 기능으로 적었다. "~다" 로 끝나던 설명을 합니다체(소개 문단)와 명사형 개조식(표·목록)으로 바꿨다.
- **화면** (`docs/images/`): v0.6 에디터 Play 중 `ScreenCapture.CaptureScreenshot`. 메뉴는 `LobbyMenu.Highlight`·`Confirm` 으로 실제 경로대로 넘겼고(빠름·ALPHA), 주행은 시험 봇 `TrackFollower`·`ShortcutBot` 과 같은 식을 `Unity_RunCommand` 안에 복사해 붙였다(저장소에 남기지 않음). `Time.captureFramerate = 10`(게임 시간 0.1초 간격).
  - `race.gif`: SUMMIT DROP 48.5~65.5초(부스트 패드 점프 → 표지판 → 봉 스윙 지름길), 170프레임, 588px, 64색, 8.2 MB. 봇 결승 90.0초(4500틱, 지름길 포함).
  - `swing.gif`: FINGER LANES 첫 지름길 봉 스윙 16~20.6초, 46프레임, 2.7 MB.
  - 정지 화면: `title.png`·`lobby.png`·`tutorial.png`(3단계 "후진" 설명 팝업)·`palm-coast.png`·`result.png` 는 2배(1176×748), `finger-lanes.png` 는 1배 프레임(588×374).
  - Game 뷰가 588×374 라 해상도가 낮다. 더 크게 찍으려면 Game 뷰 크기를 바꿔야 한다(이번에는 바꾸지 않았다).
- **기록 보호**: 봇 판은 `RaceAssembler.Records`·`Ghosts` 를 임시 저장소(`BestRecords("ProtoHarness.ReadmeCapture.")`, 스크래치패드 폴더)로 바꿔 끼웠다. SUMMIT DROP 결승(4500틱)은 그 저장소에만 들어갔고 캡처 뒤 지웠다. 사용자 기록(SUMMIT DROP 빠름 3358틱, ALPHA)은 그대로임을 확인했다.
- **남긴 것**: `skyway.png` 는 README 에서 더 쓰지 않지만 이 문서의 v0.3 항목들("HUD 트랙 이름", "main v0.3")이 고치기 전 화면의 증거로 가리켜 지우지 않았다.
- **`main` 에도 올림 (마일스톤 밖 예외)**: 사용자 "Readme 관련해선 메인까지 올려줘". `main` 은 마일스톤에서만 갱신하고(`CLAUDE.md` §9) `dev` 에는 표지판 수정(`Assets/`)도 있어, 방법을 물었고 사용자가 "README만 main에" 를 골랐다. `main` 에서 딴 `docs/readme-to-main` 에 `dev` `4adaffe` 의 `README.md`·`docs/images/` 만 가져와(`398bddc`) `main` 에 `--no-ff` 병합·푸시했다(`e7bbc0d`). 태그는 없다(`v0.6` 그대로). 문서 검증: 바뀐 파일은 README 와 이미지 8개뿐이고 README 의 링크 대상이 `main` 에 모두 있다. 이 항목 자체는 `dev` 에만 있고 다음 `dev → main` 때 올라간다. 그때 README·이미지는 양쪽이 같은 내용이라 충돌하지 않아야 한다.

## 2026-11-09 · SUMMIT DROP 지름길 표지판: 화살표를 지름길 쪽으로, 기둥은 판 뒤로, 1.6배 (`fix/summit-sign-arrow`)

- **결정 (사용자, 2026-11-09)**: 미리보기 렌더로 화살표가 지름길(오른쪽)과 반대(왼쪽)를 가리키는 것을 확인한 뒤 "화살표방향만 바꿔". 이어서 뒷면 확인을 요청하고 "화살표랑 지면에서 올라오는 막대랑 만나는게 이상해보여", "1.5배 이상 크게", "화살표 있는 면에는 막대가 겹쳐보이면 안된다".
- **확인한 것**: 판이 거꾸로 서 있던 것은 아니다. 읽는 면(화살표)은 처음부터 −Z, 즉 러너가 다가오는 쪽이었고 뒷면은 빈 판이었다. 화살촉만 −X(러너 기준 왼쪽)에 있었다. 기둥(0.3 m 각)은 판 가운데를 뚫어 판 앞면보다 0.05 m, 윗면보다 0.1 m 나와 화살대를 세로로 갈랐다.
- **바꾼 것** (`SummitDropEnvironmentBuilder.Fork`): 화살촉을 +X(러너 기준 오른쪽)로. 표지판 전체 1.6배(판 4.48 × 1.6 m, 판 중심 4.8 m), 배치 여유 반경도 1.6배. 기둥은 판 뒤(+Z)에 붙고 판 윗면보다 0.24 m 아래에서 끝난다. 다시 짓기에서 바뀐 것은 표지판 메시 3개(`SummitDropExterior_04_M_Summit{Wood,CutWood,Ink}.asset`)뿐이고 프리팹 GUID 는 그대로다.
- **검증**: 다시 짓기 검사 `Summit Drop validation PASS: main road and shortcut clear; 15 pole shafts clear; 38 renderers; 932829 vertices; decoration physics=0.` 메시 정점으로 기둥이 판 뒷면 뒤·판 윗면 아래임을 확인했다. 미리보기 씬 렌더(앞 5각도·뒤·옆)에서 화살표 면에 기둥이 보이지 않는다(위에서 내려다볼 때만 판 너머로 기둥 끝이 보인다). Play 모드 화면은 아니다. 테스트 — 영향 평가: 장식 빌더 → `SummitDropExterior.prefab` → 이 프리팹을 읽는 `TrackDefinitionTests.SummitDrop_Exterior_NoPoleShaftHangsAboveTheGround`(`TrackDefinitionTests.cs:201-204`). 장식만이라 Lap 은 고르지 않았다(VERIFICATION §4-4). 이 PC 에 통과 기록이 없어 영향 평가로 정했다. EditMode `testcasecount="1" result="Passed" passed="1" failed="0"` (11:34:56Z, 화살촉·크기 수정 뒤), 기둥 높이를 낮춘 뒤 다시 `testcasecount="1" result="Passed" passed="1" failed="0"` (11:39:15Z). 둘 다 `test-runs.ps1 -Record`.
- **알아둘 것**: 다시 지은 직후 같은 에디터 세션의 렌더는 GPU 에 남은 옛 메시를 썼다(첫 확인 캡처가 수정 전과 픽셀까지 같았다). 메시 데이터는 새것이었고, 복사본을 붙여 렌더하면 새 모양이 나왔다. Blender 표지판 FBX(아래 "Blender 시험 제작")를 쓸 때는 빌더의 읽는 면이 −Z 이고 화살표가 +X 라는 이 항목을 기준으로 맞춘다. 표지판은 여전히 도로 왼쪽에 서서 오른쪽 지름길을 가리킨다(위치는 바꾸지 않았다).

## 2026-11-09 · main v0.6: M6 "보고 듣는 맛" — `dev` 를 `main` 에 병합하고 태그 `v0.6`

- **결정 (사용자, 2026-11-09)**: M6 완료 조건 확인을 모두 받았다. 소리 구분 "확인했다 커밋 푸쉬 진행", 새 트랙 SUMMIT DROP "플레이 결과 괜찮았다", 플레이어 빌드에서 고스트 투명·프레임·화면 설정·봉 스윙 이펙트 시야 "확인했다. 괜찮다". 절차는 "main v0.5" 와 같다: `feature/m6-release-prep`(타이틀 버전 표기 `0.6`·README, 사용자 세션) → `dev` → `main` `--no-ff`, 주석 태그 `v0.6`, 병합·푸시는 지시 때만.
- **v0.6 에 든 것** (`main` v0.5 뒤): 동작별 효과음(부스터·부스트존 합성음, 봉 걸림·그래플·스윙 CC0 녹음, 녹음을 못 쓰면 이름과 이유를 남기고 끈다), 봉 스윙 이펙트(불꽃·줄 끊김·상태별 꼬리·바람 선), 고저차 1랩 다운힐 `SUMMIT DROP`(열린 코스 구조·숲과 바위 장식), 벽·부스터가 방향을 강제하지 않는 조향, 속도 km/h·카운트다운 연출, 튜토리얼(스테이지 6개, 마일스톤 밖). 각각 이 문서의 같은 이름 항목.
- **마일스톤 조건 (`CLAUDE.md` §9-2 ③)** — 코드는 `feature/m6-release-prep` `a30b540`(뒤 커밋은 `docs/` 만), 에디터 **6000.3.19f1**(`Application.unityVersion`, 이 PC). 실행마다 `tools/test-runs.ps1 -Record` 로 남겼다(첫 실제 사용, 기록 6건). 시각은 UTC(결과 XML):
  - **1차 시도 (`da766d3`, 사용자가 시킨 Codex 의 Unity MCP 실행)** — 실패한 실행도 적는다:
    - EditMode `testcasecount="394" result="Passed" passed="394" failed="0"` (06:04:51Z~06:04:55Z).
    - PlayMode 게이트 `testcasecount="299" result="Failed(Child)" passed="288" failed="1" skipped="10"` (06:06:03Z~06:21:30Z). 실패 `ChainRushRoadTests.Road_NoSteeringIntoR30_ProceduralGuardTurnsRunner`: `Expected: greater than or equal to 92.123891115188599d But was: 76.974979400634766d`. 단독 재실행 `testcasecount="1" failed="1"` (06:22:40Z~06:23:01Z) `But was: 76.981975555419922d`.
    - `Run Device Input Tests` `testcasecount="3" result="Failed(Child)" passed="1" failed="2"` (06:23:35Z~06:23:39Z). `Input_ArrowKeys_StartsSteersJumpsGrapplesAndRestarts`(`ChainRushTests.cs:354` `game.IsCountingDown` `But was: False`), `Input_ShiftAndCtrl_SwingsOnThePoleAndFiresChainAction`(`:449` `game.IsRunning` `But was: False`). 포커스는 기록되지 않았다.
    - Windows x64 빌드 성공(오류 0, 경고 485 — 경고 내용은 보지 않았다). 빌드가 바꾼 설정 파일 5개는 빌드 전 상태로 되돌렸다. 기록은 `%LOCALAPPDATA%\Temp\ProtoHarness-v0.6-test-records\`.
  - **원인**:
    - R30 — 2026-11-04 "벽·부스터가 방향을 트랙 쪽으로 강제하지 않는다" 때 쌍둥이 `Curve_NoSteering_*` 는 고쳤지만 이 테스트는 그날 고른 PlayMode 74건에 `ChainRushRoadTests` 가 없어 남았다. "벽이 러너를 돌려 커브를 빠져나간다" 는 옛 사양을 단언했고, 러너는 새 사양대로 벽에 붙어 76.97 m 에 섰다. ② 를 바뀐 부분만 돌리고 번진 회귀는 ③ 에서 잡는 설계(2026-10-30 "테스트 범위") 그대로 드러났다. `a30b540` 에서 이름을 `…ProceduralGuardHoldsTheRunnerWithoutTurningIt` 로, 단언을 Curve 테스트(`ChainRushCurveTests.cs:101-104`)와 같게(떨어지지 않는다·바깥 벽에 닿는다·뚫지 않는다·방향 그대로·커브를 끌려 나가지 않는다) 바꿨다. 사용자 "ㄱㄱ".
    - Device — 가상 Enter 뒤 카운트다운이 시작되지 않았다. 2026-10-15 이 PC 의 같은 모양 실패(에디터 포커스 없음 2/3 실패, 포커스 3/3 통과)와 같고, 아래 통과 실행은 포커스가 3초 유지된 뒤에만 시작하도록 걸었다. 1차 실행의 포커스는 기록이 없어 **원인을 포커스로 확정하지는 못했다**.
  - **2차 (`a30b540`, 메인, Unity MCP)** — 모두 통과:
    - 고친 테스트 확인: `ChainRushRoadTests` `testcasecount="4" result="Passed" passed="4" failed="0"` (09:25:59Z~09:26:42Z, 커밋 전 작업 트리 — `test-runs.ps1 -Last -Passed` 가 커밋 뒤 "실행한 코드 = 지금 커밋한 코드" 로 판정했다).
    - EditMode (`!Enemy`): `testcasecount="394" result="Passed" passed="394" failed="0" skipped="0"` (09:27:21Z~09:27:30Z).
    - PlayMode 게이트 1회차 (`Run PlayMode Tests`): `testcasecount="299" result="Passed" passed="289" failed="0" skipped="10"` (09:28:08Z~09:46:01Z, 1072.7초).
    - PlayMode 게이트 2회차 (같은 코드): `testcasecount="299" result="Passed" passed="289" failed="0" skipped="10"` (09:47:04Z~10:04:58Z, 1074.0초). 두 회차 모두 skipped 10 은 `[Explicit]` — `ChainRushProbeTests` 7건, `NetworkRaceTests` 3건. 서킷 지문 `hash 0xEF92FB76BFD1D767` (2026-11-04 기준과 같다).
    - `Run Device Input Tests` 1회: `testcasecount="3" result="Passed" passed="3" failed="0"` (10:29:18Z~10:29:35Z). 에디터에 포커스가 3초 유지되면 시작하도록 걸었다(걸 때 `focused=True`). 시작 순간의 로그 줄은 플레이 진입 때 Console 이 지워져 남지 않았다. 첫 걸기(10:05:34Z)는 10분 동안 포커스가 없어 시작하지 않았다.
  - ② 의 하한: 이 브랜치의 `Assets/` 변경은 `TitleScreen.Version`(읽는 곳은 화면 글자, 시뮬 밖)과 R30 테스트 한 메서드다. 게이트 전체가 포함한다.
  - 사용자 플레이 확인: 위 결정 줄. 빌드는 `da766d3` 에서 만들었고, 그 뒤 바뀐 것은 테스트 어셈블리뿐이라 빌드에 들어가는 코드는 같다.
  - 하지 않은 것: 네트워크 레이스(M7 부터).
- **v0.6 에 없는 것**: SUMMIT DROP 지름길 표지판 화살표 방향(코드로는 갈림 방향과 반대, 화면 확인 답 없음). 표지판 FBX(`Art/Models/ShortcutSign.fbx`)는 트랙에 놓지 않았다. 튜토리얼 리뷰 후속 ①~⑦, 녹음 리뷰 제안 4건은 그대로다.
- **함께 기록**: `feature/art-shortcut-sign` 의 Codex 설계 리뷰는 병합 뒤 끝났다 — `Verdict: approve`, "No material findings"(FBX 설정과 측정 일치, `.codex` 동기화 일치, GUID 참조 없음. 임포터 전체 값의 기본값 일치와 `.meta` 생성 경로는 확정 못 함).

## 2026-11-09 · Blender 시험 제작: 지름길 표지판 한 개로 Blender → `art-src/` → `Assets/` → Unity 확인을 한 번 돌렸다, FBX 는 축·스케일을 적용해 내보낸다 (`feature/art-shortcut-sign`)

- **결정 (사용자)**: 다른 AI 의 점검 3번("소품 하나로 미리보기 → 선택 → FBX 전달 → Unity 에서 크기·방향 확인까지 한 번 끝내 보면 어디가 반복 작업인지 드러난다")을 M6 마감 전에 하기로 했다 — "마감 전에 블렌더 작업 한번 해보겟ㅆ다", 요청서에 "ㄱㄱ". 소품은 SUMMIT DROP 지름길 나무 표지판(비대칭이라 축이 틀리면 화살표가 뒤집혀 보인다). 시안 A(빌더 모양: 상자 판 + 화살표 막대)·B(화살 판자)를 Blender 에서 보고 "A가 낫네". 첫 임포트에서 루트 회전·스케일이 나와 Blender 쪽 설정을 바꿔 보기로 "해봐".
- **FBX 설정 (이 프로젝트의 기본)**: `bpy.ops.export_scene.fbx(use_selection=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)`, 나머지는 Blender 5.2.3 기본값(`axis_forward='-Z'`, `axis_up='Y'`, `global_scale=1.0`, `apply_unit_scale=True` 등, 에이전트가 연산자 속성에서 읽음). Unity 임포트 설정은 기본값 그대로.
  - 기본값으로 낸 첫 FBX: Unity 루트 Transform 이 회전 (270, 0, 0)·스케일 100, `fileScale=0.01`. 놓기만 하면 바로 서지만, 코드가 인스턴스의 회전을 덮어쓰면(빌더의 `SetPositionAndRotation`) −90° 보정이 사라져 눕는다.
  - 위 설정으로 다시 낸 FBX: 루트 회전 (0, 0, 0)·스케일 1, `fileScale=1`, 크기 2.800 × 3.600 × 0.535 m, 피벗 바닥 가운데. 미리보기 씬에서 `SetPositionAndRotation(0, identity)` 뒤에도 크기가 같고 서 있다(렌더 확인).
  - `use_selection=True` 가 필요하다: 에이전트는 사용자의 열린 Blender 에서 새 장면에 작업하는데, `.blend` 의 `copy=True` 저장은 사용자의 기존 장면(Cube·Light·Camera)까지 담는다. FBX 에는 A 만 들어갔다(파일 안 문자열: `ShortcutSign_A` 와 머티리얼 3개, `Cube`·`Light`·B 없음, `Camera` 는 전역 설정 `DefaultCamera` 뿐).
- **축 대응 (측정)**: Blender 앞면(−Y, Front 뷰) → Unity **+Z**, Blender 위(+Z) → Unity +Y. 앞에서 보면 같은 모습이다(화살표가 Blender 에서 +X 를 가리키면 Unity 에서 +Z 쪽에서 볼 때 오른쪽, 좌표로는 −X). 지금 빌더는 표지판의 읽는 면을 −Z 에 둔다(진행 방향을 바라보게 놓고 러너가 뒤에서 본다, `SummitDropEnvironmentBuilder.cs:496-501`). 빌더에 이 모델을 쓰려면 180° 돌리거나 Blender 에서 앞면을 +Y 로 만든다 — 배치할 때 정한다.
- **버린 대안**: Unity 쪽 `ModelImporter.bakeAxisConversion` — FBX 마다 설정해야 하고, 자동으로 하려면 AssetPostprocessor 코드가 새로 생긴다. Blender 쪽 설정은 내보내는 곳 한 곳에서 끝난다. 기본값 그대로 쓰고 코드에서 회전을 곱하기 — 모든 사용처가 보정을 알아야 한다.
- **흐름에서 본 것 (반복 작업 후보)**:
  - `blender-artist` 3회(시안 → 내보내기 → 재내보내기, 같은 에이전트에 이어서). 세이프 모드 거부 2회(변수에 담은 함수 호출, 계산한 `getattr` 이름) — 둘 다 우회하지 않고 다시 썼다. MCP 첫 상태 조회가 `WinError 10053` 으로 한 번 실패했다.
  - 복사(`unity-implementer` 2회, 24k·27k 토큰)는 `cp` 한 줄인데 규칙(CONVENTIONS §1-2)상 에이전트를 거쳤다. 임포트·확인·렌더는 메인이 Unity MCP 로 했다(코드 3개). **복사 → 임포트 → 크기·루트 Transform·축 확인 → 렌더** 가 다음 모델에서도 그대로 반복된다 — 도구로 묶을지는 따로 합의한다.
  - `.blend` 를 다시 저장하면 Blender 가 `ShortcutSign.blend1` 백업을 만든다. `.gitignore` 의 `*.blend1` 로 무시된다.
- **select-tests**: `tools/select-tests.ps1` 에 `ShortcutSign.fbx` 와 `.meta` 판정을 넣었다. `Assets/` 에서 파일명과 GUID `f38d83b018bf0ac48b92e6cbfb318b34` 를 검색했으며, 이 모델을 읽는 코드·씬·프리팹이 없어 PlayMode 와 지문을 고르지 않는다. 배치할 때 참조 경로에 맞춰 이 판정을 다시 본다.
- **에이전트 반영**: 사용자 승인 후 `blender-artist` 정의에 위 내보내기 설정을 프로젝트 기본값으로 넣었다(사용자가 시킨 Codex 작업). Codex 는 `.codex` 동기화와 커밋이 권한 오류로 막혀, 메인이 `sync-agents.ps1` 로 `.codex/agents/blender-artist.toml` 을 다시 만들고(`-Check` "일치") 커밋·병합했다(사용자 "권한에 막힌건 너가 직접해").
- **안 한 것**: SUMMIT DROP 에 배치(빌더·씬), 화살표 방향 고치기.

## 2026-11-09 · 기획 에이전트: `gameplay-designer`, 읽기 전용, 피드백을 가설·변경안·플레이테스트로 (`docs/gameplay-designer-agent`)

- **결정 (사용자, 2026-11-09)**: 다른 AI 의 제안("플레이 피드백을 다음에 시험할 가설로 바꾸고, 구현 전에 성공 기준을 정하는 역할")에 합의 요청서 B 로 "B ㄱㄱ". `.claude/agents/gameplay-designer.md`(+ 생성물 `.codex/agents/gameplay-designer.toml`)를 두고 `docs/RULES/SUBAGENTS.md` §7-1 을 7종으로 했다. 첫 적용으로 `docs/PLAYTEST_SUMMIT_DROP.md`(M6 의 SUMMIT DROP 플레이 확인지)를 만들었다.
- **경계**: 기획 에이전트는 플레이어가 어디서 막히고 어떤 경험이 달라져야 하는지 정리한다. `unity-architect` 는 그 변경이 어느 시스템에 닿는지, `unity-implementer` 는 승인된 범위를 구현한다. 무엇을 고를지는 사용자가 정한다.
- **정의의 규칙**: 원문과 해석을 나눈다. 가설은 2개 이상이고 구별 관찰을 붙인다. 변경안은 최대 2개이고 각각 한 가지만 바꾼다. 현재 값은 `경로:줄` 로 확인한다(`.asset` 은 grep 만). 사람이 판단할 것과 에이전트가 모을 증거를 나눈다. 정해진 결정은 뒤집지 않는다. 피드백 한 줄이 여러 값을 한꺼번에 바꾸는 작업으로 번지지 않게 하려는 것이다.
- **읽기 전용**: 도구는 Read·Grep·Glob 다(처음에는 Bash 도 있었다 — 아래 리뷰 ⑤ 로 뺐다). 초안은 응답으로만 돌려주고 파일에는 메인이 옮긴다. 결과를 둘 곳이 작업마다 달라 에이전트가 새 파일을 만들면 그때마다 승인이 필요해진다.
- **버린 대안**: `unity-architect` 에 기획을 더하기 — 시스템 경계와 경험 판단이 한 응답에 섞인다. `docs/` 쓰기 권한 — 위 이유. 기획·레벨·밸런스로 여러 개 — 먼저 하나로 써 본다.
- **첫 적용 (SUMMIT DROP 확인지)**: 새 에이전트 종류는 세션을 다시 시작해야 등록돼(`Agent type 'gameplay-designer' not found`) 이번에는 general-purpose 에 정의 파일을 읽고 따르라고 보냈다. 도구 제한이 강제되지 않아 쓰기를 지시로 막았고, 에이전트는 쓰기 도구를 쓰지 않았다고 확인했다(`git status` 로도 새 변경 없음). 182,429 토큰, 도구 36회, 약 7.6분.
  - 메인 대조: `RunnerMotor.cs:408,414`(패드는 땅에서 스윙·그래플이 아닐 때만, 앞 속도 `Max(v, 55)`), `ChainRushCircuitSceneBuilder.cs:343-351,194-199`, `SummitDropEnvironmentBuilder.cs:62-63,89,490-502`, `TrackDefinition.cs:133-135`, 테스트 이름·줄 9개가 맞았다.
  - 틀린 것 1건: "select-tests 의 Lap 후보에 SUMMIT DROP 이 없다(DECISIONS 2026-11-06)" — 그 뒤 고쳐져 있다(`select-tests.ps1:52,320,373`). 확인지에서 뺐다.
  - 쓸모: 지름길 표지판 화살표가 갈림 방향(오른쪽)과 반대로 보인다는 의심(코드 읽기, 화면 미확인). 패드가 들어오는 속도와 상관없이 앞 속도를 55 m/s 로 올린다는 것 — ~~그래서 "틈은 진입 속도 부족" 가설이 성립하지 않는다~~ → 리뷰 ① 로 고쳤다: 점프대는 도로 방향 속도를 쓰고 패드 속도는 땅에서 줄어 "점프대 끝 속도 부족" 은 남는다.
- **문서 검증**: 정의와 확인지의 `§` 참조(§3-2·§4-1·§6-4·§7-3)가 CLAUDE.md 또는 `docs/RULES/` 의 제목으로 이어진다. `sync-agents.ps1 -Check` "일치". "6종" 이 남은 곳은 이 파일의 지난 항목뿐이다(grep).
- **리뷰 (사용자가 Codex 에 직접 시킨 리뷰, `81a81f1` 에 대해, "고친 뒤 병합", medium 5·low 3)**: 여덟 건 모두 근거를 대조했고 맞았다. 고친 것:
  - ① 패드 발동과 점프에 필요한 속도를 같은 것으로 봤다. 패드는 바라보는 방향, 점프대는 도로 방향 속도를 쓰고, 패드 뒤 땅에서 속도가 준다(`RunDrive.cs:132-133,147-148`). 확인지에 틈-H3(점프대 끝 속도)을 두고 측정 계획으로 돌렸다.
  - ② 1판 전에 화살표 의심을 알려 첫 발견을 오염시켰고, 2판의 고스트를 1판 기록으로 가정했다. 고스트는 트랙·속도의 최고 기록이다(`RaceAssembler.cs:215-245`). 화살표는 1판 뒤 공개, 판마다 고스트 여부를 적게 했다.
  - ③ 답의 보기에 "미도달·미시도·못 봄" 이 없어 판정할 수 없는 조합이 있었다. 항목마다 보기와 판정 표를 두었다.
  - ④ 변경안 일부가 한 가지만 바꾸지 않았다. 직선을 줄이면 고도차가 바뀌고, 앞쪽 장식을 번지는 것으로 잘못 들었고, 틈 후보에 `A` 가 두 번, `CragMesh` 는 봉 아래 기둥에 쓰이지 않는다. 고유 ID 를 붙이고, 함께 바뀌는 것을 적고, 바위를 출처 둘로 나눴다.
  - ⑤ Bash 가 있어 읽기 전용이 도구로 강제되지 않았다. 정의에서 Bash 를 빼고, 인덱스 대조용 HEAD 는 파견문에 적도록 SUBAGENTS §7-1 에 한 줄을 넣었다. README 에 Claude(도구 목록)와 Codex(sandbox)의 차이를 적었다. Bash 가 있는 다른 읽기 전용 에이전트(`unity-explorer`·`unity-reviewer`)는 이번 범위 밖이다.
  - ⑥ 응답 형식에 되돌리는 방법, 파견문 원문 출처, "번지는 것은 예상(확인은 architect)", 증거 "수집 계획(수집은 verifier·사용자)" 이 빠져 있었다. 읽는 순서에 CLAUDE.md·LOOKUP.md 를 넣었다.
  - ⑦ 사실의 종류(현재 값·과거 실측·계산값)를 섞었다. 앞선 사용자 피드백이 있었다는 것, 10.68 s 가 봉 삭제 전이라는 것, 2.8 s 의 가정, 색은 빌더 입력값이라는 것을 적었다. 씬 `runSpeed: 20`(`ChainRushCircuit.unity:1947`)을 미확인에서 뺐다. 정의에 "사실의 종류를 나눈다" 를 넣었다.
  - ⑧ 역할 경계를 SUBAGENTS 와 정의에 두 번 썼고, HANDOFF·확인지는 결과가 오면 에이전트를 부르는 것처럼 읽혔다. SUBAGENTS 는 쓰는 때·§7-3·HEAD 줄만 남기고 정의를 원본으로 가리킨다. 두 문서는 "보낼지는 사용자가 정한다(§7-3)" 로 고쳤다.

## 2026-11-09 · 테스트 실행 기록: 실행마다 기준점·필터·결과·에디터 버전을 남긴다 (`docs/test-run-records`)

- **결정 (사용자, 2026-11-09)**: 다른 AI 의 하네스 점검("가장 부족한 건 에이전트가 한 작업을 같은 조건에서 다시 검증할 수 있는 실행 기록") 중 1·3번에 "괜찮아보인다", 합의 요청서 A 에 "A ㄱㄱ". `tools/test-runs.ps1` 을 새로 두고 `docs/RULES/VERIFICATION.md` §4-4 에 "실행 기록" 한 줄, `CLAUDE.md` §9-2 "작업 중 실행을 다시 쓴다" 의 근거를 그 출력으로 바꿨다.
- **이유**: 결과 XML 은 한 경로에 덮어쓰인다(`ChainRushSceneBuilder.cs:282`). 그래서 "마지막 통과 실행" 과 "그 뒤 바뀐 파일" 을 사람이 DECISIONS 검증 줄과 git 으로 맞춰 왔고, 범위 지적이 네 번 나왔다(같은 날 "테스트 범위: 마지막 통과 이후"). 그 항목이 버린 대안("select-tests 가 스스로 계산 — XML 이 덮어쓰여 기준점이 흔들린다")의 원인을 기록으로 없앤다.
- **기록하는 것**: XML 사본, `testcasecount`·`result`·`passed`·`failed`·`skipped`·시각, 돌린 필터(호출한 쪽이 준다), HEAD·브랜치, 그때 커밋하지 않은 `Assets/` 파일과 git 해시, 에디터 로그의 `Version is`, 돈 테스트 이름. 위치는 리포 밖 `$env:LOCALAPPDATA\ProtoHarness\test-runs`(PC 마다 에디터 버전이 달라 기록도 PC 마다다. `.gitignore` 를 건드리지 않는다).
- **"바뀐 파일" 계산**: 파일마다 기록 때 내용(그때 커밋하지 않은 파일은 그때 해시, 나머지는 기록 HEAD 의 blob)과 지금 작업 트리 내용을 git 해시로 비교한다. 실행 뒤 같은 내용을 커밋한 것은 바뀐 것이 아니다. `Packages/`·`ProjectSettings/` 는 보지 않는다(이 PC 의 버전 드리프트가 늘 바뀐 것으로 보인다).
- **시끄럽게**: XML 이 없거나, XML 의 `start-time` 이 `-StartedAt` 보다 이르거나(실행이 XML 을 못 쓰고 옛 파일이 남은 경우, 2026-11-02 플레이 모드 중 실행이 그랬다), 에디터 버전을 못 읽거나, 같은 실행을 두 번 기록하면 거부한다. 기록 HEAD 를 저장소에서 못 찾으면(리베이스 등) 계산하지 않고 실패한다.
- **버린 대안**: `TestCallbacks.RunFinished`(Assets/)에서 자동 기록 — 잊지 않지만 콜백은 돌린 필터를 모르고, `Assets/` 변경이라 Unity 검증이 따른다. 기록을 빠뜨리는 일이 생기면 다시 본다. 리포 안 `test-runs/` 에 커밋 — PC 마다 다른 기록이 이력에 섞이고 `.gitignore` 변경이 따른다. `select-tests.ps1` 에 바로 연결 — 기록이 먼저 쌓여야 판단을 맡길 수 있다(점검 1번 "선택기를 자동 실행에 연결하기 전에 이 기록부터").
- **검증 (문서 검증 + 스크립트 실행, `CLAUDE.md` §9-2 적용 범위)**: 스크래치에 `git clone --local` + sparse checkout(`Audio/`)으로 만든 사본과 스크래치 기록 폴더로 돌렸다. 기존 XML(2026-11-09 03:50:41Z, 13건)을 기록 → `-Last -Passed` "바뀐 파일: 없음, 판정: 커밋한 코드". 작업 트리에서 `ChainRushAudio.cs` 를 바꾸면 1개(작업 트리), 커밋하면 1개(커밋됨). `UiSound.cs` 를 고친 채 두 번째 실행을 기록 → "작업 트리와 같다, 아직 커밋 안 함", 같은 내용을 커밋 → "커밋한 코드와 같다". 다시 고치고 새 파일·삭제를 더하면 3개(내용 바뀜·새로 생김·지워짐). 실패 실행(`Failed(Child)`, failed=1)은 `-Last` 에 나오고 `-Last -Passed` 는 건너뛴다. 거부 넷(늦은 `-StartedAt`, 같은 실행 두 번, XML 없음, 모드 둘)이 원문 메시지로 실패했다. `-StartedAt` 의 초 아래(04:59:59.700 → XML 05:00:00)는 받는다. 실제 리포 기본 경로로 `-List`·`-Last -Passed` 는 "기록 없음".
  - 고친 결함: 처음에는 경로를 `git hash-object --stdin-paths` 로 파이프했는데, Windows PowerShell 5.1 이 `$OutputEncoding` 을 BOM 없는 UTF-8 로 두어도 첫 줄에 BOM 을 붙여 `fatal: could not open '﻿Assets/…'` (exit 128)로 실패했다. 인자로 100개씩 넘기게 바꿨다.
  - 확인 못 한 것: LFS 파일(`git lfs ls-files` 24개)의 해시 비교는 sparse 사본에 LFS 파일이 없어 돌지 않았다(`hash-object` 는 clean 필터를 거치므로 pointer 끼리 비교할 것으로 보지만 실측 아님). ~~경로에 ASCII 밖 글자가 있으면 git 출력이 cp949 로 읽혀 깨질 수 있다(지금 `Assets/` 1,045개 중 0개).~~ → 아래 리뷰 ① 로 고쳤다. 실제 Unity 실행 직후의 기록은 다음 테스트 실행에서 처음 한다.
- **리뷰 (Codex 설계 리뷰, `20b6359` 에 대해, `needs-attention` high 2·medium 2)**: 넷 다 실제 결함이거나 실제 한계였다.
  - ① high, 한글 경로: `Invoke-Git` 이 git 의 UTF-8 출력을 cp949 로 읽어 경로가 깨지면 해시가 null 로 남고, 나중 비교가 "같다" 가 될 수 있었다. git 을 부르는 동안만 `[Console]::OutputEncoding` 을 UTF-8 로 둔다. 지우지 않았다는 파일·HEAD 에 있는 파일을 찾지 못하면 null 로 두지 않고 실패한다. 시험: 한글 이름 파일을 고친 채 기록 → "바뀐 것 없음", 다시 고침 → 1개(작업 트리), 커밋 → 1개(커밋됨), 이름이 제대로 나온다.
  - ② high, 뒤의 실패를 숨김: `-Last -Passed` 가 마지막 통과만 골라 그 뒤 실패를 보이지 않았다(§9-2 "마지막 실행이 통과", "원인을 설명하지 못하면 병합하지 않는다"). 고른 통과 뒤의 실패 실행(플랫폼과 무관)을 보이고 판정을 "재사용 불가" 로 한다. 마지막 실행 자체가 실패면 "재사용할 통과가 아니다". 시험: 통과 → 실패 기록 뒤 `-Last -Passed` 가 실패 1건과 "재사용 불가" 를 낸다.
  - ④ medium, LFS: `Assets/` 에 LFS 파일 20개(텍스처 14·오디오 5·TutorialInfo 1)가 있다. clean 필터 해시는 pointer 와 내용이 같아 pointer 상태로 기록한 뒤 내용을 받으면 잡히지 않는다. `Assets/` 에 pointer 가 있으면 `-Record`·`-Last` 를 거부한다. 시험: `GIT_LFS_SKIP_SMUDGE=1` 로 오디오를 받은 사본에서 둘 다 "LFS 파일 5개가 … pointer" 로 거부, 기록 파일 0개. 실제 리포(내용을 받은 상태)에서는 지나간다.
  - ③ medium, 다른 실행이 덮어쓴 XML: 시각 검사는 이번 실행보다 오래된 XML 만 막는다. 이 PC 에서 그 파일 이름을 쓰는 코드는 ProtoHarness 하나다(PCUBE 폴더 grep, `ChainRushSceneBuilder.cs:282`). 실행마다 고유한 결과 경로를 쓰려면 `Assets/` 콜백을 바꿔야 해 이번 범위 밖이다. 한계로 스크립트 머리말과 VERIFICATION §4-4 에 적었다(같은 PC 에서 두 에디터로 동시에 돌리지 않는다). 후속 후보: `TestCallbacks` 가 실행마다 다른 경로에 쓰고 그 경로를 로그에 남기기.
  - 고친 뒤 처음부터 다시 돌린 시나리오 T0~T14 가 모두 위와 같이 나왔다.

## 2026-11-09 · 효과음: 녹음을 못 쓰면 이름과 이유를 남기고 끈다 (`fix/audio-recording-failures`)

- **결정 (사용자, 2026-11-09)**: 효과음 병합 리뷰의 보통 2건에 "1. 보통 2건 수정", "커밋 병합 푸쉬 ㄱㄱ".
- **고친 것** (`ChainRushAudio.cs` 만, `ChainRushGame.cs` 는 그대로):
  - `Awake` 가 녹음 3개를 다른 준비보다 먼저 `ReadRecording` 으로 읽어 비어 있음·가져오기 설정(Decompress On Load)·길이(샘플, 루프가 자르는 것보다 1샘플 더)·무음을 검사하고, 스윙은 루프가 쓰는 4~8.25초의 무음도 본다(`SwingSectionFault`). 틀리면 틀린 녹음을 모두 필드 이름과 이유로 `LogError(…, this)` 하고 끈다. 전에는 `throw` 가 `enabled = false` 를 거치지 않아 반쯤 켜진 채 남았다.
  - 꺼진 상태에서 `PlayCue`·`ResetAudio`·`SetMuted` 는 무시하고 처음 한 번 `off since Awake` 를 `LogError` 한다. `CueCount`·`SwingVolume` 은 0, `GetCue` 는 `InvalidOperationException`. 전에는 시뮬 틱 안(`RunnerMotor.Step` 의 `PlayCue`)에서 NRE 가 났다.
  - `OnValidate` 가 가져오기 설정을 편집 중에 알린다. `PlayCue` 의 `Enum.GetValues` 할당을 정적 `CueTotal` 로 바꿨다.
- **버린 대안**: `ChainRushGame.PlayCue` 에서 오디오가 꺼졌는지 보기 — 코어 시뮬 파일을 건드려 지문까지 돌려야 하고, 꺼진 이유를 아는 쪽은 `ChainRushAudio` 다. `Awake` 를 try/catch 로 감싸기 — 검사로 막을 수 있는 실패를 예외로 다루게 된다.
- **검증** (테스트를 걸기 전에 범위 한 줄을 먼저 썼다, 2026-11-09 "테스트 범위: 마지막 통과 이후"):
  - 수정 전: 새 테스트 `Audio_UnusableSwingRecording_TurnsOffWithItsName_AndLaterCallsDoNotThrow` `testcasecount=1 failed=1`, 2026-11-09 03:25:25Z, 원문 `Unhandled log message: '[Exception] InvalidOperationException: Swing recording is too short: Chain_PoleHook'`.
  - 1차 수정(`0e7dd22`): Presentation 12 + `ChainRushFlowTests.Race_FingerLanesLook_PlaysTheTracksMusicAndAmbience` → `testcasecount=13 result=Passed passed=13 failed=0 skipped=0`, 2026-11-09 03:28:36Z~03:29:00Z.
  - 2차 수정(`292280d`, 리뷰 반영): 같은 13건 → `testcasecount=13 result=Passed passed=13 failed=0 skipped=0`, 2026-11-09 03:50:41Z~03:51:01Z. 시뮬 코드는 바뀌지 않아 지문·Lap 은 뺐다. 그 사이 `dev` 병합(`3b7d94c`)은 문서·에이전트 정의뿐이다.
  - 컴파일 `*** Tundra build success (10.87 seconds), 29 items updated, 1618 evaluated`, Console 에러 0건. `AudioClip.Create` 로 만든 클립의 `loadType` 은 `DecompressOnLoad` 다(실측). 그래서 테스트의 "전체 무음" 경우가 무음 검사를 지난다.
  - 확인 못 한 것: 가져오기 설정이 틀린 녹음은 에셋 설정을 바꿔야 해서 테스트하지 않았다.
- **리뷰** (1차 수정 `0e7dd22` 에 대해): Codex 설계 리뷰 `needs-attention` medium 2 — ① 녹음 전체의 피크만 봐서 스윙 구간만 무음이면 NaN 루프 ② 꺼진 뒤 `CueCount`·`SwingVolume`·`GetCue` 의 NRE. 둘 다 실제 결함이라 2차 수정에서 고쳤다. `unity-reviewer` 치명 0·중대 0·보통 1(Codex ① 과 같음)·제안 8, 68,294 토큰. 제안 중 넣은 것: 틀린 녹음을 모두 알리기, 길이 검사 주석 정정, 테스트를 이유별 메시지로 구별, 짧은 경우 전제를 먼저 확인. 넣지 않은 것: `loadType` 메시지가 `OnValidate`·`ReadRecording` 두 곳에 있음, "see the error above" 가 Awake 전 호출을 가정, `Update` 의 `initialized` 가드, `UnloadAudioData`, WebGL 의 `GetData`. 2차 수정은 다시 리뷰하지 않았다(브랜치당 1회, 다시 부르는 것은 지시가 있을 때). Codex 비용은 `/status` 를 읽지 않아 모른다.
- **알아둘 것**: 리뷰 도중 다른 세션이 같은 작업 트리에서 `docs/blender-art-agent` 를 체크아웃했다(12:31:15). 그 세션이 끝날 때까지 이 브랜치를 멈췄고, 그 사이 Unity 에디터가 12:30:27 에 닫혀 다시 열었다.

## 2026-11-09 · Blender 아트 에이전트: `mcp-for-blender` + 세이프 모드, `art-src/` 까지만 (`docs/blender-art-agent`)

- **결정 (사용자)**: "블렌더 환경을 만들어두고 아트관련 에이전트를 두고 작업을 전담시키고 싶다". 다른 AI 의 추천(1 `mcp-for-blender` + Safe Mode, 2 `art-src/` 경유 후 `unity-implementer` 가 반영, 3 Codex 는 평소 차단, 4 직접 설치)을 받아 합의안을 고쳤고, 설치·등록 7단계 "ㄱㄱ", 실측 뒤 에이전트·문서 "ㄱㄱ".
- **환경 (리포 밖)**: Blender 5.2.3 LTS(`C:\Program Files\Blender Foundation\Blender 5.2`, 사용자 설치 — 처음 받은 5.3 candidate 대신). 애드온 `blender_mcp.py`(애드온 1.8, 프로토콜 15)를 `BLENDERMCP_ADDONS_DIR` 로 5.2 폴더를 지정해 `uvx mcp-for-blender@2.2.7 install-addon`. Claude Code 는 user 범위 `blender` = `uvx mcp-for-blender@2.2.7`, env `BLENDER_MCP_SAFE_MODE=1`·`DISABLE_TELEMETRY=true`. Codex 는 `~/.codex/config.toml` 에 같은 서버를 `enabled = false` 로. 패키지 버전은 고정한다(PyPI 최신 2.2.7, 2026-11-04).
- **실측 (12:37, 세이프 모드)**: `get_addon_status` — `5.2.3 LTS`, `telemetry_consent: false`, 라이브러리·생성기 모두 `off`. ① `bpy.ops.wm.save_as_mainfile(copy=True)` `{'FINISHED'}`, `art-src/_probe/safe_probe.blend` 98,275 B(zstd 머리 `28 B5 2F FD`). ② `bpy.ops.export_scene.fbx` `{'FINISHED'}`, `safe_probe.fbx` 28,908 B(`Kaydara FBX Binary`). ③ 대조군 `open(..., "w")` 거부: `Rejected by safe mode - line 1: open() is forbidden: open() grants raw filesystem access; use bpy operators for file work`, 파일 없음. Codex 는 `codex mcp list` 에 `blender ... disabled` — 새 Codex 세션에서 도구가 안 보이는지는 Codex 를 부르지 않아 확인하지 않았다.
- **에이전트** `blender-artist` (`.claude/agents/blender-artist.md`): 쓰기는 `art-src/` 만, `승인:`·`브랜치:` 관문, 도구는 상태·장면 조회·코드 실행·`look` 만(외부 에셋·생성형 `search_assets`·`import_asset`·`generate_3d` 는 뺐다 — 외부 호출·비용). 사용자의 열린 파일은 `copy=True` 저장으로만, 작업은 새 장면에서. 세이프 모드 거부는 우회하지 않는다. 파일 확인은 Bash `ls`(세이프 모드에서 Python 파일 접근이 막힌다).
- **`.blend` 를 `Assets/` 에 두지 않는다**: Unity 6.3 매뉴얼 "It's best practice to use the .fbx file format whenever possible", 독점 형식은 "files fail to import unless you have the corresponding 3D modeling software installed".
- **버린 대안**: Blender Lab 공식 MCP(5.1 이상, 가드를 켤 수 없다 — "will execute LLM generated code in Blender without any guards"), dcc-mcp-blender(4.5 이상, 도구 200여 개·검증 도구가 있지만 임의 스크립트 차단 변수를 원문으로 확인하지 못함 — 반복 제작 단계에서 다시 볼 후보), 에이전트가 FBX 를 `Art/Models/` 로 바로 내보내기(임포트 시점과 `.meta` 발급을 승인 하나로 묶으려고 뺐다, 반복 제작 때 넓힐 수 있다), 리포 `.mcp.json`(리포 설정 변경 — user 범위로).
- **남은 것**: 첫 모델(요청서·FBX 축/스케일 설정·Unity 임포트·`select-tests` 판정표의 `Art/Models` 정규식) — 따로 합의. Codex 로 Blender 를 부르는 방식. `tools/codex-unity.ps1:38` 이 쓰는 서버 이름 `unity-mcp` 와 설정의 `unity_mcp`(`config.toml:163`)가 다르다 — 확인 못 함, `fix/` 후보. 실측 파일 `art-src/_probe/` 는 커밋하지 않았다.
- **병합 (문서 검증, `CLAUDE.md` §9-2 적용 범위; 사용자 "커밋과 병합 진행하라 네 추천대로 진행")**: 바뀐 파일은 `.claude/agents/blender-artist.md`(새)·`.codex/agents/blender-artist.toml`(새, 생성물)·`CLAUDE.md`·`README.md`·`docs/ARCHITECTURE.md`·`docs/DECISIONS.md`·`docs/RULES/CONVENTIONS.md`·`docs/RULES/SUBAGENTS.md` 뿐이다(`Assets/`·`Packages/`·`ProjectSettings/` 0개). ① 새 정의와 문서의 `§` 참조(§0·§1-3·§3·§3-4·§7·"병렬 절대 금지"·§7-1 "6종")가 모두 `CLAUDE.md` 또는 `docs/RULES/` 의 제목으로 해석된다. ② `tools/sync-agents.ps1` exit 0, `-Check` "동기화 상태: 일치", `.codex` 변경은 새 `blender-artist.toml` 하나. ③ 옮긴 내용은 없다. 작업 트리의 에디터 버전 차이 4개 파일은 넣지 않았다. 새 에이전트를 실제로 파견해 보지는 않았다.

## 2026-11-09 · 테스트 범위: 마지막 통과 이후 바뀐 것만 다시 돌린다 (`docs/test-scope-since-last-pass`)

- **결정 (사용자, 2026-11-09)**: 효과음 병합 직전에 브랜치가 건드린 7개 클래스 전체와 지문을 다시 걸자 "테스트 클래스 전체 돌린다는데 결국 사운드밖에 바뀐게 없는데 돌려서 뭐하나", 이어서 "테스트 돌려보는 것에 대해 반복해서 범위를 과하게 설정하는 것 방지해보자 제발". 합의안에 "ㄱㄱ". `docs/RULES/VERIFICATION.md` §4-4 에 "다시 돌리는 범위는 마지막 통과 이후 바뀐 것" 한 줄을 넣었다.
- **이유**: 같은 종류의 지적이 네 번째다(2026-10-30 "테스트 범위", 2026-11-02 "랩 런 하나로", 2026-11-02 "작업 중 실행을 다시 쓴다", 이번). 이번에는 74건 실행 뒤 `Assets/` 에 바뀐 파일이 없었는데, 범위를 `dev...HEAD`(브랜치 전체)로 잡아 이미 통과한 부분을 다시 골랐다. 규칙은 결과를 다시 쓰라고 했지만 "무엇을 기준으로 범위를 잡나" 가 적혀 있지 않았다.
- **버린 대안**: `tools/select-tests.ps1` 가 마지막 통과 이후를 스스로 계산하기 — 결과 XML 이 실행마다 덮어쓰이고 일부만 돈 실행도 같은 파일에 쓰여 기준점이 흔들린다. 규칙 한 줄로 부족하면 다시 올린다.
- **문서 검증**: 새 줄의 참조(CLAUDE.md §9-2 "작업 중 실행을 다시 쓴다", 이 항목)가 실제 제목·항목으로 이어진다. 같은 규칙이 다른 곳에 없다(CLAUDE.md §9-2 는 병합 때 결과를 다시 쓰는 조건이고, 새 줄은 다시 돌릴 범위를 고르는 기준이다).

## 2026-11-07 · 효과음: 부스터·부스트존은 들어 보고 고른 합성음, 쇠사슬 소리는 CC0 녹음 (`feature/m6-sound-cues`)

- **결정 (사용자)**: 2차 합성을 듣고 2026-11-07 "부스터, 부스트존, 봉걸림, 그래플, 스윙 다 별로다. 부스터 소리는 새소리마냥 너무 높다. 달리는게 아닌 카트가 달리듯이 조금 묵직하면서 높은 소리였으면 좋겟다. 사이버펑크와 쇠사슬의 느낌을 잘 살려봐". 3차 후보 15개를 시청 페이지로 듣고 "부스터는 A 카트터보, 부스트존은 c 패스바이", 쇠사슬 셋은 "모든 소리에 또르르륵 하는 소리가 들리잖나 쇠소리가 없고". 같은 문제의 세 번째 실패라 §4-5 로 멈추고 후보 2개(실제 녹음 / 구조를 바꾼 합성)를 올렸고 "1번 ㄱㄱ 음원 찾아줘". 녹음 27개 중 "봉 걸림 1, 그래플 1, 스윙 루프 7 @4.0s", 합의안 "ㄱㄱ", 2026-11-09 "확인했다 커밋 푸쉬 진행".
- **듣고 고르는 단계를 먼저 둔다**: 1·2차는 들어 보지 못한 채 합성해 게임에 넣었다가 거부됐다. 3차부터 프로젝트 밖에서 후보를 만들어 시청 페이지(비공개 아티팩트)로 고르게 하고, 고른 것만 옮겼다.
- **부스터 "카트 터보"** (`ChainRushAudio.MakeKartBoost`): 톱니파 엔진이 72 → 150 Hz 로 오르고 저역 필터가 열리는 것이 "높은 느낌" 이다. 2차의 4~5.6 kHz 터빈이 새소리로 들린 원인을 뺐다. 길이 2.05 → 2초.
- **부스트존 "패스바이"** (`MakePassBy`): 0.32초 동안 음높이와 밝기가 오르고 지나간 뒤 내려간다.
- **합성 공통**: 밴드 제한(polyBLEP) 오실레이터, TPT 상태변수 필터, 후보와 같은 음량(가장 큰 300 ms 의 RMS 0.21, 피크 0.92 이하). 후보의 파이썬 합성식을 그대로 옮겼다. 인접 샘플 거칠기가 후보 0.01424 / 0.03371, 게임 0.014222 / 0.033911 이다.
- **쇠사슬이 합성으로 안 된 이유** (코드로 확인, 귀로 확인하지 않음): 1~3차 후보가 모두 짧은 타격을 22~62 ms 간격으로 2~5번 잇는 같은 구조였다(또르르륵). 타격 하나가 모드 5개, 잔향 28~120 ms 여서 쇠의 긴 울림과 많은 배음이 없었다. 2차는 1.8 kHz, 3차는 470~820 Hz 로 음높이만 바꿨다.
- **녹음** (`Assets/_Project/Audio/`, 모두 Freesound 페이지에서 CC0 1.0 확인, Git LFS):

  | 소리 | 파일 | 출처 | 올린 사람 |
  |---|---|---|---|
  | 봉 걸림 | `Chain_PoleHook.mp3` | https://freesound.org/s/871034/ "metal_chain_drop2" | funkysandwich |
  | 그래플 | `Chain_Grapple.mp3` | https://freesound.org/s/541975/ "grappling hook" | 16bitstudios |
  | 스윙 루프 | `Chain_Swing.mp3` | https://freesound.org/s/448917/ "Chain Rattling" | Kp69606 |

  - 사용자가 들은 128 kbps 미리듣기 MP3 를 그대로 넣었다. 원본 WAV 는 Freesound 로그인이 있어야 받는다. 음질이 문제되면 원본으로 바꾼다(확장자가 바뀌면 GUID 가 바뀌어 씬 참조를 다시 연결한다).
  - CC0 라 표시 의무는 없지만 출처를 여기에 남긴다.
- **코드** (`ChainRushAudio.cs`): 녹음 `AudioClip` 직렬화 필드 3개(`poleHookRecording`·`grappleRecording`·`swingRecording`), 비면 LogError 후 비활성화(§5). `Awake` 에서 `LoadAudioData` → `GetData` 로 읽어 모노로 합친다. 가져오기 기본값이 Decompress On Load 이고 사전 로드가 꺼져 있어서다. 합성 체인 코드(`MakeChainCue`·`AddChainCluster`·`AddLink`·`LimitChainPeak`, 합성 `MakeSwingLoop`)는 지웠다.
  - **앞 무음 자르기**: 첫 소리가 봉 걸림 70 ms, 그래플 168 ms 뒤에 나와 입력 지연처럼 들린다. 피크의 5% 를 넘는 첫 샘플 5 ms 앞부터 쓴다. 합의안에 없던 처리라 보고했다.
  - **스윙 루프**: 녹음의 4.0초부터 4초. 뒤따르는 0.25초를 등전력으로 머리에 겹쳐 끝이 처음으로 바로 이어진다. 녹음이 48 kHz 라 클립도 48 kHz 다. 스윙 음량 계수 0.22 는 그대로다.
- **씬** (`ChainRushCircuit.unity`, `Unity_RunCommand` + `SerializedObject` + `SaveScene`, 코드 전문을 보이고 승인): 참조 3줄. 저장하며 Unity 가 `Runner` 의 `boostTurnRadiusScale: 2`·`turnSpeedLossScale: 0.5`·`turnFalloffScale: 0.11111111`·`guardTurnRate: 60` 을 쓰고(모두 `RunnerMotor.cs` 기본값, 값 변화 없음) 코드에서 지운 `slingTurnRate: 90` 을 뺐다(2026-11-04 "벽·부스터 방향" 의 예고대로). 합의안의 "다른 변경이 있으면 멈추고 보고" 에 따라 보고했고, 사용자가 그대로 커밋하게 했다.
- **테스트 기준을 바꾼 것** (방향이 바뀌어서다): 부스터 저음 "120 Hz 아래 35% 초과" → "300 Hz 아래 50% 초과"(고른 A 는 120 Hz 아래 24%). 2차의 고역 상승·중심 주파수 단언 대신 3 kHz 위 에너지 상한(부스터 5%, 부스트존 10%; 측정 0.9% / 8.1%)과 "패스바이는 옆일 때 가장 밝다"(0.3622 > 0.0490). 합성 체인의 고역 비율·충돌 간격 단언과 `ImpactIntervalVariation` 을 지우고 `Audio_ChainCues_ComeFromRecordings_AndStartOnTheHit`(길이, 첫 20 ms 안에 소리, 스윙 48 kHz·4초)를 더했다.
- **버린 대안**: 쇠사슬 4차 합성(모드 20~40개, 울림 0.3~1.5초) — 듣지 못하는 채 같은 문제의 네 번째 시도다. 상용 사슬 팩 — 라이선스. 녹음을 `TrackLook` SO 로 넘기기 — 트랙마다 같은 값을 넣게 된다. 녹음을 오디오 편집기로 잘라 넣기 — 이 PC 에 디코더가 없고, 코드로 자르면 구간을 바꾸기 쉽다.
- **검증**:
  - 부스터·부스트존만 바꾼 코드: `ChainRushPresentationTests.Audio_*` `testcasecount=8 result=Passed passed=8 failed=0 skipped=0`, 2026-11-07 09:23:31Z~09:23:55Z.
  - 커밋한 코드(`9639fac`, 실행 뒤 `Assets/` 변경 없음): Presentation 11 + Flow 62 + 서킷 지문 1, `categoryNames = { "!Device", "!Enemy", "!NetEndReplay", "!^Lap$" }` → `testcasecount=74 result=Passed passed=74 failed=0 skipped=0`, 2026-11-07 09:58:47Z~10:01:31Z. 스윙 이음매 샘플 차 0.160, 주변 차 RMS 0.213. 씬을 건드려 Flow 와 지문을 넣었고, 시뮬 코드는 바꾸지 않아 Lap 은 뺐다.
  - 컴파일 `*** Tundra build success (4.69 seconds), 14 items updated, 1618 evaluated`. Console 에러 10건은 모두 `RaceAssembler` 로그이고 호출 스택이 `ChainRushFlowTests` 의 테스트(롤백·깨진 고스트·멀티 미지원)다. `ChainRushAudio` 에러는 없다.
  - 사용자 확인: 2026-11-09 "확인했다 커밋 푸쉬 진행", "튜토리얼도 확인했다병합도 해".
- **병합 검증 (② `feature → dev`)**: `tools/select-tests.ps1 -Range dev...HEAD` 는 "전체"(mp3·씬·코어 시뮬 파일). 영향 평가: 1차 커밋의 시뮬 변경은 `PlayCue` 호출과 순수 함수 `GuardContact.SpeedInto` 뿐이라 이동 결과가 그대로여서 Lap 은 뺐다. EditMode 테스트에는 바뀐 타입(`GuardContact`·`RaceCue`)을 읽는 것이 없다(grep 0건). 실행마다 값:
  - 2차 합성 코드(1차 커밋의 시뮬 변경 포함): Presentation 10, Tutorial 17, Circuit 8, CornerSwing 18, Drift 10, Steering 13, Flow 7, 서킷 지문 1 → `testcasecount=84 result=Passed passed=84 failed=0 skipped=0`, 2026-11-07 07:56:59Z~08:03:53Z.
  - 그 뒤 바뀐 것(`ChainRushAudio.cs`, 그 테스트, 씬 참조, mp3)은 위 8건·74건 실행이 덮는다. 74건 실행이 커밋한 코드와 같다.
  - 병합 직전에 7개 클래스 전체 + 지문을 한 번 더 걸었는데, 사용자 "결국 사운드밖에 바뀐게 없는데 돌려서 뭐하나" 대로 중복이다. 실행 guid 를 받아 두지 않아 취소하지 못했고(`TestRunnerApi.CancelTestRun(guid)`), 병합은 위 결과로 했다. 사용자가 에디터에서 중단했고 결과 XML 은 쓰이지 않았다(결과 없음).
- **리뷰**: `unity-reviewer` 치명 0·중대 0·보통 2·제안 6, 77,551 토큰. ① 녹음 가져오기 설정이 Decompress On Load 가 아니거나 스윙 녹음이 8.25초보다 짧으면 `Awake` 의 `throw` 가 `enabled = false` 를 거치지 않는다(`ChainRushAudio.cs:322,324,472`). ② `ChainRushAudio` 가 스스로 꺼진 뒤에도 `ChainRushGame.PlayCue` 가 넘겨 시뮬 안에서 NRE 가 난다(`ChainRushGame.cs:94,334`, 기존 패턴의 폭이 넓어진 것). 둘 다 지금 음원·설정에서는 일어나지 않아 후속으로 둔다. 제안: 무음 녹음의 NaN, `UnloadAudioData` 없음(스윙 원본 약 5 MB), `RaceCue.Respawn` 상한 고정, 오류 메시지가 빈 필드를 말하지 않음, WebGL 의 `GetData`(미확인), `index/` 재생성. Codex 설계 리뷰 `approve`, 지적 0건(비용: `/status` 를 읽지 않아 모름). 롤백 때 효과음이 다시 나는 것은 `dev` 에도 있던 동작이고 결정성에는 영향이 없다고 봤다.
- **알아둘 것**: 튜토리얼 목표 달성음은 1차 커밋(`60e2de8`)에서 `RaceCue.Action` → `RaceCue.Finish`(1.2초 화음)로 바뀌었다. 사용자가 2026-11-09 튜토리얼을 확인했다.

## 2026-11-07 · codex-unity 는 relay 로 서버를 찾고, 있는 도구 표는 그 자리에서 바꾼다 (`fix/codex-unity-server-name`)

- **문제**: 이 PC 에서 `tools/codex-unity.ps1` 로 보낸 Codex 작업이 시작 약 5초 만에 실패했다. `failed to load configuration: C:\Users\User\.codex\config.toml:174:14: invalid transport`. 스크립트가 `mcp_servers.unity-mcp` 에 승인을 넣었는데 이 PC 의 서버 이름은 `unity_mcp` 여서, 전송 설정이 없는 서버가 새로 생겼다. 이름만 바꾸면 이번에는 `config.toml:167-171` 의 기존 도구 표(`approval_mode = "prompt"`, 2026-10-26 "Codex 역할")와 표가 겹친다.
- **결정 (사용자, 2026-11-07)**: 두 안 중 "1번 ㄱㄱ". 서버는 `command` 에 `relay` 가 있는 것으로 찾는다. 없거나 둘 이상이면 던진다. 도구 표가 있으면 그 줄을 `approve` 로 바꾸고 줄 끝 주석에 원래 값을 적었다가 되돌린다. 표가 없으면 전처럼 블록을 덧붙인다.
- **버린 대안**: Codex 쓰기만 열고 MCP 없이 보내기 — 빠르지만 Codex 가 스스로 테스트하며 고치지 못한다. 설정 파일 전체를 백업했다가 복원하기 — 실행 중 Codex 나 사용자가 바꾼 내용이 사라진다. 사용자 `config.toml` 의 서버 이름 바꾸기 — 다른 프로젝트도 쓰는 파일이다.
- **검증**: `docs/`, `tools/`, `AGENTS.md` 만 바꿨다(문서 검증).
  - 임시 `CODEX_HOME` 과 가짜 `node`: 이 PC 형태(표 있음)와 다른 PC 형태(표 없음)는 열린 동안 값이 `approve` 이고 끝나면 원본과 바이트까지 같다. relay 없음·둘은 `expected one Unity relay server` 로 실패하고 파일은 그대로다. 줄 끝 주석이 남은 설정에서 `-Status` `open`, `-Watch` 뒤 `closed`, 원본과 같다.
  - 실제 실행(읽기 전용, `-Effort low`): 종료 코드 0. Codex 가 `Unity_GetConsoleLogs` 와 `Unity_RunCommand` 를 불렀고 `[Log] codex smoke: playing=[False] compiling=[False]`. 끝난 뒤 `config.toml` 해시가 실행 전과 같고 `-Status` 는 `closed`.
- **한계**: 다른 PC 의 설정은 보지 못했다. `unity-mcp` 라는 이름은 스크립트가 거기서 동작했다는 기록으로 미룬 것이고, `command` 에 `relay` 가 없으면 스크립트가 시끄럽게 실패한다.
- **알아둘 것**: 스크립트 출력을 파이프로 묶으면 안 된다. Codex 브로커가 파이프를 물려받아 `tail` 이 끝나지 않고, 실패 메시지가 나오지 않는다. 파일로 받는다.

## 2026-11-06 · select-tests: 장식 메시는 "전체" 가 아니다 (`fix/select-tests-art-models`)

- **결정 (사용자, 2026-11-06)**: "Art/Models 판정도 고쳐 ㄱㄱ". 표의 장식 줄이 `Models/StadiumExterior_` 만 알아서 뒤에 생긴 메시가 "전체 (표에 없는 경로)" 로 떨어졌다. `Models/(\w+Exterior_|StadiumSports_)` 로 넓혔다. 지문 없음, PlayMode 없음이다.
- **근거 (§4-4, 지문을 빼는 근거)**: GUID 로 찾으니 `SkywayExterior_*`·`SummitDropExterior_*` 표본은 각자의 `*Exterior.prefab` 만 참조하고, 세 장식 프리팹의 충돌체는 0개다(`grep -c 'MeshCollider:\|BoxCollider:'`). 빌더의 `physics=0` 검사와 같다. `StadiumSports_*` 11개는 프리팹·씬·에셋 어디서도 참조하지 않는다. 스크립트에서 이 메시를 읽는 코드는 없다.
- **`Runner*` 는 넣지 않았다**: 러너 외형이고 장식이 아니다. 그대로 "전체" 다.
- **검증**: 실제 범위로 `tools/select-tests.ps1` 실행, exit 0. `1fd3d76^..1fd3d76`(Skyway 메시 40개) 전 "전체" → 뒤 `PlayMode 없음, 지문 없음`, 끝줄이 "범위 확정 못 함" 에서 목록으로 바뀌었다. `7f5925e^1..7f5925e` 도 목록으로 바뀌었다. `13c24c5^..13c24c5` 는 `Runner*` 와 `Art/Models.meta` 가 그대로 "전체".
- **알아둘 것**: 참조가 없는 `StadiumSports_*` 11개는 지우지 않았다(§0).

## 2026-11-06 · SUMMIT DROP 의 묻힌 코너 봉 4개는 그대로 둔다, 안 보여도 스윙은 된다

- **결정 (사용자, 2026-11-06)**: 묻힌 봉을 지웠더니 "서킷 마지막 쯤에 봉이 아예 없어 게이지를 못모으니 부스터를 쓸 수 없다". 이어서 "이미 맵 초반에 장식이 뚫린채로 스윙하는 구간이 있는데 걍 초본으로 되돌리는건 어때" → "ㄱㄱ". 봉 6·10·11·12 번(S 977, 1623, 1660, 1950 의 코너)을 남긴다. 봉은 15개다.
- **이유**: 게이지는 봉 스윙으로만 찬다(`PoleSwing.cs:92`). 네 봉을 지우면 마지막 봉이 S 1623 까지여서 결승까지 약 650 m 동안 게이지를 얻을 수 없다. 남기면 둘째 낙하 코너의 S 2020 까지 걸 수 있어 약 260 m 로 준다. 줄이 장식·지형을 뚫는 것은 2026-11-06 "봉 체인은 18 m" 에서 이미 받아들였고, 스윙 표식은 가림을 검사하지 않아 지형 뒤에서도 그려진다(`ChainRushHud.cs:460`).
- **묻힌 깊이** (저장된 지형, 꼭대기 기준): 6번 19.7 m, 10번 89.9 m, 11번 39.0 m, 12번 7.8 m.
- **버린 대안**:
  - 코너별로 봉을 끄기(`Segment.Arc(..., pole: false)`) — 구현해 테스트까지 통과했다가 게이지 때문에 커밋 전에 버렸다.
  - 12번 코너 안쪽 지형을 낮춰 봉을 보이게 하기 — 봉 둘레 방위 225~270° 에 봉보다 40~46 m 높은 능선이 있고, 그 위로 윗도로(S 720~800, 봉에서 약 61 m, 45 m 위)가 지난다. 능선을 깎으면 윗도로 밑에 구멍이 난다. 줄을 걸 수 있는 구간은 코너의 절반(`poleHookArcShare` 0.5, S 2020)까지인데 그 구간의 봉은 능선에 가린다.
  - 패드로 게이지 채우기 — 이 트랙만 게이지 규칙의 예외가 생긴다. 코너 안쪽 갓길의 새 봉 — 줄이 짧아 벽에 부딪히기 쉽다. 마지막 구간에 코너 추가 — 뒤쪽 코스와 장식을 다시 맞춰야 한다.
- **남긴 것**: `dev` 의 도로 위 슬라롬 봉 3개 삭제, 뜬 봉 아래 바위 기둥은 그대로다.
- **검증**: 되돌린 뒤 `git diff --name-only dev -- Assets` 0개. `Assets/` 가 `dev`(`7f5925e`)와 같아서 테스트는 다시 돌리지 않았다. 그 코드의 값은 아래 "플레이 피드백" 검증 줄이다.

## 2026-11-06 · 플레이 피드백: LED 깜빡임, SUMMIT DROP 봉, 카운트다운 색 (`fix/playtest-visuals`)

- **결정 (사용자, 2026-11-06)**: "수정해봐 1. 스타디움 맵 서킷의 벽쪽에 있는 장식 아직 깜빡거린다. 주황 청록 핑크로 된 거 2. 새로 만든 맵 장식은 괜찮으나 봉이 공중에 떠있는 것이 있고 마지막 쯤에 긴 직선구간에 트랙 안에 봉이 여러개 있는 곳도 있는데 이건 잘못 설계된 것 같다". 앞서 물은 카운트다운의 탁한 색도 이 말로 고쳤다.
- **LED 깜빡임**: `LED screen` 과 `Dark board frame` 의 앞면이 같은 평면이어서(차이 `-9.3E-10`) z-fighting 이 났다. 뒤판 앞면을 5 cm 뒤로 물렸다(`ScreenProud`). 화면 앞면은 그대로 1.6 m 다. 다른 PC 의 `fix/stadium-flicker`(`38f418a`)는 이 PC 에 없어서 내용을 확인하지 못했다. 합칠 때 겹치는지 본다.
- **도로 위 봉**: 슬라롬 봉 3개(S 1716~1796, 도로 중심 ±2.5 m)를 뺐다. 코너와 상관없이 도로 위에 세운 설계였다. 봉 18 → 15개.
- **뜬 봉**: 봉은 ±20 m 고정이고 코너 중심에 서는데, 코너 안쪽이 절벽이면 땅이 더 깊다. 저장된 지형으로 잰 틈은 0번 93.2 m, 5번 37.6 m, 4번 29.4 m, 1번 21.1 m. 장식 그룹 `10` 에 땅에서 봉 아래까지 오는 바위 기둥을 세웠다(`PoleSpires`). 둘레는 그 높이의 도로에서 2 m 떨어지게 줄인다(`RoomBesideRoads`), 그래서 1번은 도로 옆이라 가늘다. 시뮬은 그대로다.
- **카운트다운**: 반투명이면 민트와 트랙의 빨강이 섞여 회색이 된다. 투명도를 없애고 마지막 0.1초에 크기가 0 으로 줄어든다(`CueExitTime`, `CueAlpha` 삭제).
- **버린 대안**: 봉 길이를 땅까지 늘리기 — 런타임 장식이 지형을 모르고, 93 m 민트 기둥이 된다. 코너 봉을 트랙 정의에서 빼기 — 그 코너의 스윙이 없어진다. 슬라롬 봉을 도로 밖으로 옮기기 — 직선에 스윙할 이유가 없다. 카운트다운의 그림자만 빨리 흐리기 — 민트와 빨강이 섞이는 것은 그대로다.
- **검증** (시각은 UTC):
  - 고치기 전 새 테스트 2건 `Failed passed=0 failed=2`(12:58:05Z): `Stadium_Exterior_LedScreensStandProudOfTheirFrames` `Expected: greater than or equal to 0.0399999991f But was: -9.31322575E-10f`, `SummitDrop_Exterior_NoPoleShaftHangsAboveTheGround` 는 봉 0·1·4·5 번.
  - 재빌드: `Summit Drop raised 4 rock spires`, `pole clearance PASS: 15 poles`, `validation PASS … 38 renderers; 932829 vertices`, `StadiumExterior: 1051 renderers; … road corridor clear`.
  - PlayMode(`ChainRushInputSourceTests`·`ChainRushPresentationTests`·`ChainRushTests`·`ChainRushFlowTests.SummitDrop_*`·`Race_FromTheLobby_RacesTheChosenDefinitionAndCountsIn`·`Race_PauseMenu*`·`Race_OpenedDirectly_RunsAsSavedAndWaitsForEnter`) `testcasecount=45 Passed passed=45 failed=0 skipped=0`, 13:01:39Z~13:02:43Z.
  - 랩 SUMMIT DROP 3속도 `testcasecount=3 Passed passed=3 failed=0 skipped=0`, 13:03:06Z~13:04:05Z.
  - EditMode(`!Enemy`) `Passed passed=394 failed=0 skipped=0`, 13:04:41Z 끝.
  - 영향 평가: 시뮬 코드는 안 바꿔 지문을 돌리지 않았다. 트랙 빌더는 SUMMIT DROP 생성만 바뀌어 Lap 은 그 3칸이다. 스타디움은 장식 빌더만이라 Lap 없음(§4-4). `select-tests.ps1` 은 "전체" 로 판정했고, Lap 후보에 SUMMIT DROP 이 없다(도구 표가 낡았다).
  - 캡처: 스타디움 레이스 시점과 비스듬한 근접에서 LED 단색, 줄무늬 없음. 뜬 봉 4곳 바위 기둥 확인, 긴 직선에 도로 위 봉 없음, 카운트다운이 사라지기 직전까지 민트.
  - 사용자 확인: "확인했다 커밋 병합 푸쉬 ㄱㄱ".
- **한계**: 캡처는 Game 뷰 588×374 와 멈춘 프레임으로 봤다. 재빌드가 `TrackLook_Stadium.asset` 에 빈 `music`·`ambience` 줄을 붙였다(내용 변경 없음, 커밋에서 뺀다). `StadiumExterior.prefab` diff 가 큰 것은 로컬 fileID 가 새로 나와서다.
- **찾았지만 안 고친 것**: 봉 6·10·11·12 번은 지형 속에 묻혀 있다(잰 땅이 봉 꼭대기보다 위). 보이지 않는데 스윙이 걸릴 수 있다. 플레이로 확인하지 않았다. 그대로 두기로 했다(위 "묻힌 코너 봉").

## 2026-11-06 · IMGUI 글자는 마우스 아래에서도 제 색을 유지한다 (`fix/playtest-visuals`)

- **증상 (캡처로 발견, 사용자 "해봐")**: 카운트다운의 민트 글자가 처음 커질 때 흰색으로 뿌옇게 나왔다.
- **원인**: `GUI.Label` 은 마우스가 사각형 안에 있으면 hover 상태로 그린다. `NightUi.Style` 은 normal 색만 정해서 hover 는 스킨 기본값 `RGBA(0.9, 0.9, 0.9)` 로 남았다. `ShadowLabel` 도 normal 만 검게 바꿔 그림자까지 흰색이 되어 뿌옇게 보였다. 상태별 글자 색을 다르게 칠하자 흰 글자가 hover 의 빨강으로 나와 확인했다. 나이 0.10초에 마우스 y=657 이 글자 상자(끝 708.8) 안, 0.18초에 y=712.8 로 밖이었다. 글자가 커지면 상자가 마우스까지 닿는다.
- **결정**: `NightUi.Style` 이 hover 도 같은 색으로 두고, `ShadowLabel` 이 두 상태를 함께 바꾼다. 모든 스타일이 `NightUi.Style` 에서 나오므로(`new GUIStyle` 은 `NightUi.cs` 한 곳) HUD·로비·타이틀의 같은 문제가 함께 없어진다.
- **버린 대안**: 카운트다운 스타일 하나만 고치기 — 흰 글자의 검은 그림자, 다른 민트 글자에 같은 문제가 남는다. 일부러 흰색에서 민트로 바뀌는 연출 — 마우스 위치에 따라 나오는 현상이라 연출이 될 수 없다.
- **검증**: 고치기 전 `NightUi_Style_KeepsItsColourUnderTheMouse` `testcasecount=1 Failed(Child) passed=0 failed=1`, `Expected: RGBA(1.000, 0.000, 0.000, 1.000) But was: RGBA(0.900, 0.900, 0.900, 1.000)`. 고친 뒤 PlayMode(`select-tests.ps1` 목록: `ChainRushInputSourceTests`·`ChainRushPresentationTests`·`ChainRushTests`) `testcasecount=31 Passed passed=31 failed=0 skipped=0`, 12:25:44Z~12:26:33Z. EditMode(`!Enemy`) `Passed passed=392 failed=0 skipped=0`, 12:26:58Z 끝. 지문·Lap 은 도구도 고르지 않았다(시뮬이 읽는 경로가 아니다). 캡처: 같은 나이 0.10초를 커서를 움직이지 않은 채(`1602,473`, 흰색이 재현된 지점) 찍어 민트 확인, GO! 도 민트.
- **한계**: Game 뷰 588×374 에서만 봤다. 그림자는 테스트가 없고 캡처로만 봤다.

## 2026-11-06 · 속도 표시는 km/h, 카운트다운은 큰 글자가 줄어들며 나온다 (`feature/hud-kmh-countdown`)

- **결정 (사용자, 2026-11-06)**: "Km/h로 변경작업 ㄱㄱ", "3 2 1 시간 나오는 것도 폰트 바꾸고 액션 추가해줘 … 큰 글자가 축소되서 표현되는".
- **속도**: 표시만 바꾼다. 시뮬 값은 m/s 그대로이고 HUD 가 ×3.6 해서 정수로 보인다(`FeedbackLook.Kmh`, `ChainRushHud.SpeedText`). 달리기 20 m/s 는 72, 부스트존 55 m/s 는 198 이다. 단위 글자는 HUD 의 대문자 표기대로 `KM / H`.
- **카운트다운**: 3·2·1·GO! 를 맑은 고딕 Bold 에서 HUD 숫자와 같은 Bahnschrift BoldAndItalic 으로 바꿨다. 2.6배에서 0.3초에 줄어들고(`CueScale`), ~~0.08초에 나타나 마지막 0.2초에 사라진다(`CueAlpha`)~~ 반투명이 탁해 보여 위 "플레이 피드백" 에서 없앴다. 나이는 `ChainRushGame.CountdownNumberAge`(읽기 전용, 시뮬이 읽지 않는다)와 `game.Elapsed` 로 잰다. 글자는 320 으로 그려 절반으로 줄인다. 160 으로 그려 키우면 커 있는 동안 흐려지기 때문이다.
- **버린 대안**: 글꼴 크기를 프레임마다 바꾸기 — 크기마다 글리프를 새로 구워 프레임이 끊긴다. 시뮬까지 km/h 로 바꾸기 — 지문과 튜닝 값이 전부 바뀌는데 얻는 것이 없다.
- **검증**: 선택 PlayMode 13건 `testcasecount=13 Passed passed=13 failed=0 skipped=0`, 12:05:41Z~12:06:03Z. 고른 것: `ChainRushTests.ChainRushHud_*`·`StartCountdown_*`, `ChainRushFlowTests.Race_FromTheLobby_RacesTheChosenDefinitionAndCountsIn`·`Race_PauseMenu*`·`Race_OpenedDirectly_RunsAsSavedAndWaitsForEnter`. 영향 평가: `CountdownNumberAge` → `ChainRushHud.cs` 만 읽는다 → 시뮬 밖이라 지문·Lap 없음. `select-tests.ps1` 은 `ChainRushGame.cs` 때문에 "전체" 로 판정했다. Console Error·Warning 0건.
- **한계**: ~~화면으로 보지 않았다. 위치·크기·움직임은 사용자 플레이 확인 대기.~~ 병합 뒤 Play 캡처로 위치·크기·축소를 봤고 흰색 버그를 찾았다(위 "IMGUI 글자"). 체감은 사용자 플레이 확인 대기.

## 2026-11-06 · 열린 코스와 1랩 다운힐 SUMMIT DROP · M6 "시뮬 변경 없이" 를 뺀다 (`feature/m6-open-course`)

- **결정 (사용자, 2026-11-06)**: 고저차 트랙은 "위에서 아래로 쮹 가는 맵", "1랩만". 레퍼런스는 "보고 참고하라는거다". "열린코스도 되도록 구조 바꿔". 8구간 구성, 갈림길, 가드레일 없는 구간에 "1 o 2 o 3 o". 2026-11-05 "M6 범위" 의 "시뮬 변경 없이" 는 이 트랙에서 빠진다.
- **구조**: `TrackDefinition` 에 열린 코스(`openCourse`, 결승 뒤 감속 구간 `finishRunout`, 출발 뒤 도로 `startRunway`)와 가드 없는 구간(`OpenEdge`)을 더했다. 열린 코스는 닫힘 검사가 없고 1랩만 받으며, 지름길은 받고 베이는 받지 않는다. `LapLength` 는 결승 S 이고 감속 구간은 들어가지 않는다. 새 시뮬 저장 상태는 없다. 닫힌 코스는 기존 분기 그대로다.
- **트랙**: 2279.96 m, 순낙차 167.10 m, 1랩. 설계 원칙은 약 10초마다 할 일이 바뀌는 것이다 — 절벽 낙하, 스위치백, 벽 없는 좁은 벼랑길, 틈 점프, 갈림길, 지그재그, 두 번째 낙하, 결승 앞 오르막. 배치는 새로 짰고, 레퍼런스에서는 구간 리듬만 참고했다(기록 영상의 속도 변화, 공략 문서의 구간 순서).
- **이름**: `SUMMIT DROP`, 사용자 "1,2 ㅇㅇ".
- **분담**: 구현·검증은 Codex(Unity MCP 사용자 허락), 리뷰와 로비 항목은 Claude. 로비 씬 저장도 사용자 지시로 Claude 가 `Unity_RunCommand` 로 했다("저장은 너가 진행하도록 현재 직접 못다루는 성황", 이 1건만의 승인이다). 씬 변경은 새 항목 4줄뿐이다.
- **장식 (2단계, 사용자 "일단 장식 ㄱㄱ", 테마 "숲과 바위 산악")**: `SummitDropEnvironmentBuilder`. 땅 높이가 하나인 기존 방식과 달리 도로 높이를 따라 내려가는 지형을 만든다. 정상의 암벽, 스위치백 숲, 왼쪽이 트인 벼랑길, 틈 아래 골짜기와 폭포, 갈림길 공터, 지그재그 바위 사이, 터널(S1848–1910), 바닥 마을과 결승 문. 렌더러 37, 정점 932,625. `RoadCorridor` 에 경사를 따라 검사하는 오버로드를 더했다(기존 호출은 그대로). 장식 씬 저장, 빌드 씬 목록, 로비 항목은 사용자 승인으로 Claude 가 `Unity_RunCommand` 로 했다.
- **미룬 것**: 음악·환경음. 플레이어 빌드에서의 프레임은 재지 않았다.
- **버린 대안**: 닫힌 코스 유지(끝에 출발 높이까지 오르막으로 복귀) — 결승이 다시 꼭대기라 다운힐이 아니다. 입체교차 안 — 내려가기만 하는 코스에는 필요 없다.
- **검증** (Codex 실행, 시각은 UTC, 커밋 전 작업 트리):
  - 열린 코스 구조: EditMode 1회차 `testcasecount="197" result="Passed" passed="197" failed="0"`(09:53:36Z), PlayMode 1회차 `testcasecount="14" result="Failed(Child)" passed="13" failed="1"`(09:54:25Z~09:55:25Z, 새 복귀 테스트 `Expected: 106.0d … But was: 110.0d`, 테스트의 준비 상태와 기대값 문제), EditMode 최종 `testcasecount="199" result="Passed" passed="199" failed="0"`(09:58:12Z), PlayMode 최종 `testcasecount="14" result="Passed" passed="14" failed="0"`(09:59:01Z~09:59:59Z), 네 트랙 × 세 속도 `testcasecount="12" result="Passed" passed="12" failed="0"`(10:00:55Z~10:03:49Z, XML 원본 확인).
  - 트랙·지름길: EditMode `testcasecount="169" result="Passed" passed="169" failed="0"`(10:21:43Z), 지문 `testcasecount="1" result="Passed"`(10:22:34Z~10:23:10Z), SummitDrop·OpenCourse `testcasecount="13" result="Passed" passed="13" failed="0"`(10:26:29Z~10:26:41Z), FINGER 3속도·스윙 지름길 `testcasecount="4" result="Passed" passed="4" failed="0"`(10:27:33Z~10:28:34Z), 최종 에셋 도로·봉 간격 `testcasecount="1" result="Passed"`(10:28:54Z, XML 원본 확인).
  - 로비 저장 뒤 `ChainRushFlowTests.Lobby_` (Claude 실행, 자리표시 순번 4 → 5): `testcasecount="17" result="Passed" passed="17" failed="0" skipped="0"`(10:35:57Z~10:36:08Z). Console 에러 0.
  - 장식 포함 (Claude 실행): `Race_BotLap_*` SUMMIT DROP 3칸 `testcasecount="3" result="Passed" passed="3" failed="0"`(11:28:40Z~11:29:38Z), `ChainRushFlowTests.Lobby_` `testcasecount="18" result="Passed" passed="18" failed="0"`(11:30:11Z~11:30:23Z). 첫 랩 실행은 테스트 시작 때 뜬 `Scene(s) Have Been Modified` 대화 상자에 막혀 시작되지 않았다(Cancel 로 닫았고 씬을 다시 저장했다. 저장 내용은 의도한 두 줄뿐이었다). 레이스 화면 11곳을 캡처해 확인했다.
  - 리뷰 수정 뒤 (Claude 실행): 새 봉 테스트 2건 고치기 전 `testcasecount="2" result="Failed(Child)" passed="0" failed="2"`(11:44:05Z), EditMode 전체 `testcasecount="392" result="Passed" passed="392" failed="0"`(11:45:33Z~11:45:34Z), PlayMode 코너 스윙·롤백·지문·열린 코스·SummitDrop `testcasecount="42" result="Passed" passed="42" failed="0"`(11:45:56Z~11:47:53Z).
  - 리뷰 수정 뒤 랩 다섯 트랙 × 세 속도 `testcasecount="15" result="Passed" passed="15" failed="0" skipped="0"`(11:48:16Z~11:52:08Z). 이 실행 뒤 `Assets/` 변경은 임시 `InitTestScene`(`.gitignore`)뿐이다.
  - 영향 평가: `Centerline.InReach` → `TryFindPole` → `RunnerMotor.cs:544,553`, `GrappleController.cs:83` → 코너 스윙·롤백·지문·랩 전 트랙. `RoadCorridor`·빌더는 에디터 전용이라 재빌드 검사로 봤다. `select-tests` 는 "전체" 로 판정했고 게이트 전체는 돌리지 않았다(§9-2). `Device` 는 입력 경로를 안 바꿔 돌리지 않았다.
  - 리뷰: `unity-reviewer` 치명 0·중대 2·보통 5·제안 4, 225,688 토큰. Codex 설계 리뷰 `needs-attention` high 1·medium 1, 실제 결함 2. 봉 높이 결함은 Codex 만 찾았고 빌드 목록·침범 검사 범위는 `unity-reviewer` 만 찾았다. Codex 5시간 사용량은 읽지 못했다.
  - 서킷 지문 그대로 `0xEF92FB76BFD1D767`. 봇 결승: 보통 124.72 s, 빠름 100.68 s, 매우빠름 85.08 s, 복귀 0회, 최대 옆 거리 6.82 m. 빠름 지름길 90.00 s(10.68 s 단축).
- **봉은 기둥 높이 안에서만 걸린다 (병합 전 Codex 리뷰)**: `Centerline.TryFindPole` 은 코스 전체의 봉을 평면 거리로 찾는다. 이 코스는 위아래 구간이 평면으로 겹쳐 S 96 에서 108.1 m 아래 봉이 잡혔다(고치기 전 실패 `Expected: less than or equal to 20.0f But was: 108.147797f`). `PoleVerticalReach` 20 m(기둥이 봉 지점 위아래로 뻗는 길이)를 넘으면 후보에서 뺀다. 닫힌 서킷은 높이 차가 작아 지문이 그대로다.
- **병합 전 리뷰로 함께 고친 것**: 빌더는 프리팹 검사를 통과한 뒤에 룩을 저장한다. `RoadCorridor` 는 열린 코스의 출발 뒤 도로와 감속 구간도 표본으로 잡는다(재빌드 통과, 프리팹·메시 변경 없음). 테스트 이름, 틀린 주석 4곳.
- **남은 것**: 사용자 플레이 확인. 빌드 씬 목록은 §9-2 대로 `ProjectSettings` 만 담은 브랜치로 나눠 함께 병합했다.
- **리뷰 후속 (`unity-reviewer` 제안, 지금 자산에 영향 없음)**: ① 봇 가드 단언 `maxOffset < RoadHalfWidth - 0.4f` 는 폭이 변하는 트랙을 전제하지 않는다(`ChainRushFlowTests.cs:1099`, `HalfWidthAt(maxOffsetS)` 로) ② `OpenEdge` 가 직선 데크 위면 조용히 무시된다 ③ `MergeByMaterial` 은 Skyway 복사본이다. `Lettering` 은 네 번째 변형이다 — `ExteriorKit` 으로 ④ 장식 배치 실패를 세지 않는 `continue` 3곳, 하드코딩 S 값 ⑤ `s % 8d` 는 음수 S 에서 늘 참이라 출발 뒤 차선이 실선이 된다.

## 2026-11-06 · 튜토리얼: 넓은 원형 미션 맵, 스테이지 6개 (`feature/tutorial-stages`)

- **결정 (사용자, 2026-11-06)**: "고저차 트랙 작업은 미루고 튜토리얼부터". 가운데 봉이 있는 아주 넓은 원형 맵에서 목표 지점에 닿으면 다음 목표와 설명을 준다. "골인 지점이 있는 맵이 아닌 단순 튜토리얼 미션용 맵", 완료하면 팝업의 버튼으로 로비로, "esc로 중간에 나갈수도". 요청서의 스테이지형·메인 메뉴 별도 줄·스윙을 부스터보다 먼저 두는 순서에 "좋다". 마일스톤은 답이 없어 추천대로 밖에 뒀다.
- **맵**: 봉을 감싸는 반지름 30 m 고리 중심선(`TrackDefinition.Ring`) 위에 반지름 3~150 m 원판(`CircuitRace.UseArena`, `CircuitScenery.LayArena`). 봉은 원호의 중심에 서고 후보 각도는 트랙 방향에서 재므로 지금 규칙이 그대로 통한다. 에디터 측정(메모리상 고리, 2026-11-06): 봉 걸기 35.9 m 까지 72/72, 36.1 m 부터 0/72, 벽 판정 내적 0.0000, 높이 오차 0.00000(반지름 3.3~149 m).
- **에셋 없이 코드로**: 고리와 스테이지 표를 코드에 두고 `TutorialDirector` 를 런타임에 붙인다(고스트와 같은 방식). 씬·`.asset`·`ProjectSettings` 변경이 없어 사용자가 저장할 것이 없다. 재질과 하늘은 로비 첫 트랙(STADIUM)의 것을 빌린다.
- **스테이지**: ~~시작할 때마다 출발점에서 카운트다운한다.~~ 시작할 때마다 출발점으로 돌아가므로 재시작이 곧 "이 스테이지 다시" 이고 로비에서 아무 스테이지나 고를 수 있다. ~~부스터 스테이지는 게이지가 스윙으로만 차서 1칸을 주고 시작한다.~~ 보통 속도·첫 러너 고정, 랩·기록·고스트 없음. 취소선은 아래 "플레이 피드백" 으로 바뀌었다.
- **플레이 피드백 (사용자, 2026-11-06, 구현·검증은 사용자 지시로 Codex, Claude 는 리뷰)**:
  - **설명 팝업**: "스테이지별로 바로 시작하는 것이 아닌, 팝업(페이지 형식으로 좌우 넘어가며 볼 수 있는)을 띄워 이미지와 함께 설명", "그 팝업창을 닫아야 플레이 시작". 그림은 코드로 그린다(`Tutorial/TutorialFigures`): PNG 는 씬 참조 저장이 따라오고 `Resources/` 는 금지다. 봉 사각형·화살표·게이지는 HUD 의 `DrawTarget`·`DrawArrow`·`DrawBoostGauge` 를 그대로 불러 실제 문양과 같다. 닫기 버튼은 어느 쪽에서나 눌리고, 팝업에서 ESC 는 로비로 나간다.
  - **스윙이 먼저**: "부스터를 바로 주는게 아니라 스윙으로 게이지가 찬다는 것 부터". 게이지 지급을 없앴다. 5번은 스윙 → 놓기 → 부스터 → 목표. 6번은 5번과 겹치지 않게 바깥 목표 → 스윙 → 놓기 → 부스터 → 멈추기.
  - **3·2·1 없앰**: 튜토리얼은 팝업을 닫으면 바로 달린다. 재시작도 팝업·카운트다운 없이 바로 시작한다. "GO!" 도 그리지 않는다. 레이스는 그대로다.
  - **바닥 어디서나 스윙**: "바깥에서도 걸리게". `RunnerMotor.ReachPolesFrom`(기본 0)으로 튜토리얼만 거리를 늘리고, 그때는 원호 비율도 1로 쓴다(0.5 면 반원 경계에서 후보가 빠진다, Codex 가 테스트로 찾았다). 기본값에서는 `TryFindPole` 에 들어가는 값이 그대로다.
  - **바닥 무늬**: "모래지옥 모양". 바닥은 코드상 평면이었고(`TrackFrame` 위쪽 = 월드 위쪽, 경사 0) 동심원·스포크가 깔때기로 보였다고 판단해 평행 격자로 바꿨다. 레이캐스트 144점 높이 차 1 mm 이내를 테스트로 재서 평면을 확인했다. 착시가 없어졌는지는 사용자 플레이 확인이 남았다.
  - **리뷰가 고친 것**: 스윙 중 CTRL 로 게이지를 쓰면 Boost 목표가 막힘(`GoalText` 안내로), 팝업 뒤 불투명(`Dim` 으로), 줄 감기 8 m 가 봉 가까이서 달성 불가(5 m 로).
  - **Codex 의 Unity MCP**: 사용자 "쓸 순 있지만 내가 너에게 명령할때 codex로 구현하고 mcp 활용하라고 사용 허락할때만". 설정에 승인을 두면 허락 없이도 열리므로, `tools/codex-unity.ps1` 이 허락받은 실행 동안만 `approval_mode = "approve"` 를 넣고 끝나면 지운다. 규칙은 `docs/RULES/SUBAGENTS.md` §7-5. 시험: 읽기 전용 호출 성공, 끝난 뒤 `config.toml` 해시가 백업과 같음.
- **버린 대안**: 트랙 폭 제한을 풀어 넓은 도로로 깔기 — 고리 반지름이 커져 봉이 걸리는 거리도 함께 커진다(반지름 + 6 m). `Data/` 에셋 + 로비 씬 참조 — 사용자의 씬 저장이 따라온다.
- **검증 (작업 중 ①, 같은 코드)**: 영향 평가: `CircuitRace.Step`·`Initialize` 분기(튜토리얼에서만 켜짐) → 서킷 지문·`ChainRushCircuitTests`·`ChainRushRollbackTests`, `RaceAssembler`·`ChainRushHud`·로비 → `ChainRushFlowTests` 의 `Lobby_`·`Race_FromTheLobby_`·`Race_PauseMenu`·`Race_OpenedDirectly_`·`Race_WithATrackLook_`·`Race_FinishFromTheLobby_`, 새 기능 → `ChainRushTutorialTests`. 일반 레이스 주행 코드는 바뀌지 않아 `Lap` 은 돌리지 않았다.
  - PlayMode 첫 실행 `testcasecount=53 result=Failed(Child) passed=51 failed=2`, 2026-11-06 07:17:27Z~07:21:01Z. 실패 2건은 `Lobby_BackAndSettings_GoPageByPage`·`Lobby_SettingsPage_AdjustsOnlyTheHighlightedSettingAndOpensTheKeys` 의 `Back on the settings entry. Expected: 2 But was: 3` — 메뉴에 튜토리얼 줄이 들어가 설정 줄이 3이 된 결과라 테스트의 줄 번호를 `MainPage.SettingsRow` 로 바꿨다(같은 이유로 `Move(2)`·`Highlight(2)` 4곳도).
  - 재실행 `Lobby_` `testcasecount=17 result=Passed passed=17 failed=0`, 07:21:57Z~07:22:08Z.
  - `STAGE CLEAR` 위치를 고친 뒤 PlayMode `testcasecount=53 result=Passed passed=53 failed=0 skipped=0`, 07:24:42Z~07:28:17Z (봇이 6스테이지 완주, 서킷 지문 포함). EditMode 전체(`Enemy` 제외) `testcasecount=351 result=Passed passed=351 failed=0 skipped=0`, 07:28:48Z~07:28:49Z.
  - 캡처로 메인 메뉴, 스테이지 페이지, 안내 패널, 스윙, 화면 밖 목표 표시, `STAGE CLEAR`(고치기 전 위치), 완료 팝업을 봤다. 팝업의 Enter·버튼 클릭과 실제 키 입력은 테스트로 돌리지 않았다. 사용자 플레이 확인 대기.
  - 위 값은 첫 구현 때다. **플레이 피드백 반영 뒤 (Codex 실행, XML 은 Claude 가 대조)**: 영향 평가에 `RunnerMotor.TryFindPole` → `ChainRushCornerSwingTests` 를 더했다. 수정 전 재현 `testcasecount=1 result=Failed(Child) failed=1`, 08:28:58Z~08:29:03Z(`Briefing_WaitsAtTheStartWithoutTicks_…`). EditMode `testcasecount=359 result=Failed(Child) passed=358 failed=1`, 08:33:39Z~08:33:40Z(`0 degrees, radius 15`, 반원 경계 후보 누락). 고친 뒤 EditMode 전체(`Enemy` 제외) `testcasecount=359 result=Passed passed=359 failed=0`, 08:36:12Z~08:36:13Z. PlayMode `testcasecount=81 result=Passed passed=81 failed=0 skipped=0`, 08:36:48Z~08:41:24Z(튜토리얼 17, Flow 24, CornerSwing 18, Circuit 11, Rollback 10, 서킷 지문 1). 마지막 코드 수정은 08:35:38Z 로 두 실행 전이다. `Lap` 은 돌리지 않았다(기본값에서 주행 코드 경로가 같다).
  - 팝업의 실제 키(←/→·Enter·ESC)와 마우스 클릭은 테스트로 돌리지 않았다. 캡처는 Game 뷰 588 x 339 를 3배로 키운 것이라 글자가 흐리다. 사용자 "ㅇㅋ 괜찮의 커밋 병합 진행".
  - **병합 ② (작업 중 실행을 다시 쓴다)**: 위 EditMode 359·PlayMode 81 이 병합 코드의 결과다. `Assets/` 의 가장 새 파일은 08:35:38Z 로 두 실행 전이고, 커밋 뒤 `git status` 에 `Assets/` 변경이 없다. `Device` 는 돌리지 않았다(`KeyboardMouseInputSource`·`ChainRushGame.Update` 를 바꾸지 않았다).
  - **리뷰**: `unity-reviewer` 치명 0, 중대 0, 보통 1(테스트 이름 2단, 기존에도 34개), 제안 7(HANDOFF 후속). 일반 레이스 동작 변경·지문 영향 경로 없음. Codex 설계 리뷰 `needs-attention` 1건(high): `tools/codex-unity.ps1` 강제 종료 때 승인이 남는다 → 실제 결함. 감시 프로세스를 더해 고쳤다. 실행별 격리 설정 권고는 브로커가 공유돼 쓰지 않았다. 임시 `CODEX_HOME`·가짜 `node` 로 실행: 정상 종료 restored=True, 실행 오류 exit=7 restored=True, `Stop-Process -Force` restored=True, 늦은 감시가 다른 실행의 승인을 남김 True, `-Close` restored=True, 실제 설정은 백업과 같음. 남은 틈: 전원 차단(`-Status`, `-Close`), 강제 종료 때 이미 뜬 Codex 프로세스는 계속 돈다.

## 2026-11-06 · 캐릭터 모형은 임시용이고 최종적으로 바꾼다 (마일스톤 미정)

- **결정 (사용자, 2026-11-06)**: "캐릭터 모형 최종적으론 바꿀것이다", "지금은 임시용". 2026-11-05 "M6 범위" 의 "러너 디자인 5종 유지" 를 고친다. M6 에서 교체하지 않는 것은 그대로다. 언제 바꿀지는 정하지 않았다.
- **영향**: 지금 러너 모형에 묶이는 표현은 피한다 — 사용자 "러너 수정의 큰 영향 없는 것만 하자". 봉 스윙 잔상의 몸 실루엣 안은 모형을 복제하므로 여기에 걸린다.
## 2026-11-06 · 봉 스윙 이펙트, 상태별 꼬리, 흰 바람 선 (`feature/m6-swing-effects`)

- **결정 (사용자 플레이로 다듬었다, 2026-11-06)**: M6 "봉 스윙 이펙트" 를 표현 계층에서만 넣었다. 시뮬은 바꾸지 않았다.
  - **불꽃·줄 끊김 (`Visuals/SwingEffects`)**: 띠와 손 쪽에서 튄다, 스윙이 끝나면 줄이 둘로 갈라져 0.25 s 에 튕겨 사라진다. 복귀로 끝난 스윙은 끊김을 내지 않는다. 띠 불꽃만으로는 멀어서 사용자 "불꽃은 봉에 달린건가? 잘 모르겠다" — 손 쪽을 더한 뒤 "불꽃도 보인다".
  - **꼬리 (`Visuals/SpeedTrails`, 한 번에 하나)**: 스윙 앰버(0.12), 부스터 민트(0.25, 0.25 s), 스윙 + 부스터 금색(0.325), 부스트존 진한 초록(속도와 함께 약해진다). "꼬리는 부스터만"(2026-10-29)을 사용자 "스윙꼬리 따로 부스터 단일꼬리 따로 스윙+부스터 꼬리 따로, 부스터존에 의한 부스터 꼬리 따로" 로 바꿨다. 스윙 + 부스터는 두 꼬리를 겹쳤을 때 사용자 "안보인다" 여서 전용 꼬리를 뒀다. 겹쳤을 때 안 보인 원인은 확인하지 못했다. 금색 폭은 사용자 "현재와 부스터의 중간".
  - **흰 바람 선**: 부스터가 켜진 동안(부스트존 포함) 양 끝이 뾰족한 흰 선이 러너 둘레 8방향으로 뒤로 벌어진다. 동시에 최대 8개, 0.07 s 마다 대칭 한 쌍, 고정 패턴. 사용자 참고 영상의 5:34~5:35 프레임을 읽어 만들었다.
- **버린 대안**: 파티클 속도 줄기 — "너무 불규칙하게 뻗어나가는거같다". 소닉붐 고리 — "구체느낌", "개수가 너무 많다". 좌우 한 쌍만 — "여러 방향으로 있어야지". 몸 실루엣 잔상 — 러너 모형에 묶인다(모형은 임시용). 머티리얼 에셋으로 가산 발광 — 씬 연결이 따라와 1단계에서 뺐다. 프로젝트에 파티클 셰이더를 쓰는 머티리얼이 없어 코드로 찾으면 플레이어 빌드에서 빠질 수 있다.
- **검증 (병합 ②, 작업 중 실행을 다시 쓴다)**: 영향 평가: `SpeedTrails`·`SwingEffects`·`FeedbackLook`·`ControlFeedback` → `ChainRushHud.Awake`(표현만, 시뮬 참조 없음) → `ChainRushCornerSwingTests`·`ChainRushPresentationTests`·`ChainRushInputSourceTests`·`ControlFeedback_`·서킷 지문(도구 목록 + `ControlFeedback_`).
  - PlayMode `testcasecount=34 result=Passed passed=34 failed=0 skipped=0`, 2026-11-06 06:28:33Z~06:30:26Z. 이 실행 뒤 코드 변경은 `SwingBoostTailWidth` 0.4 → 0.325 뿐이다.
  - 그 변경 뒤 PlayMode `SpeedTrails_` `testcasecount=2 result=Passed passed=2 failed=0 skipped=0`, 06:34:15Z~06:34:19Z.
  - EditMode 전체(`Enemy` 제외) `testcasecount=342 result=Passed passed=342 failed=0 skipped=0`, 06:33:42Z~06:33:47Z.
  - 사용자 플레이 "일단 ㅇㅋ". 플레이어 빌드에서 이 런타임 머티리얼이 보이는지는 확인 못 했다 — M6 마감 빌드에서 본다.

## 2026-11-06 · 봉 체인은 18 m 에 걸고, 장식은 줄 때문에 제한하지 않는다 (`feature/m6-swing-effects`)

- **결정 (사용자, 2026-11-06)**: `SwingLook.BiteHeight` 3 → 18 m. 체인이 위로 뻗어 낮은 장식 위로 지나간다. 사용자 플레이 "괜찮네". 값은 표현 계층 상수 하나고 시뮬은 읽지 않는다(`SwingCenter` 만 쓴다). 20 m 는 띠가 꼭대기 구체에 묻혀(`CircuitScenery.PlacePoles`) 18 m 로 했다.
- **이유**: 사용자 "맵 장식들을 뚫고 줄이 보이지않나". 장식과 줄은 둘 다 물리가 없어(`ExteriorAssets.ValidateDecoration`, `MetalChain`) 달리기에는 영향이 없고, 보이는 문제다. 코너를 드리프트가 아니라 봉에 줄을 걸어 각도를 고정해 지나가는 것이 이 게임의 차이라, 봉 스윙은 그대로 둔다.
- **측정 (장식 프리팹에 임시 충돌체, 봉을 걸 수 있는 중앙선 지점마다 손 → 걸림 띠 레이캐스트)**: 3 m → 18 m 로 STADIUM 1% → 1%, FINGER LANES 18% → 0%, SKYWAY 57% → 8%, PALM COAST 44% → 33%. 남은 겹침은 그대로 둔다.
- **버린 대안**: 줄이 쓸고 가는 부채꼴에서 높은 장식을 빼거나 옮기기 — 사용자 "이건 아닌거같다". 줄이 코너를 도는 동안 겹치는 모든 장식을 막아야 해서다. 체인을 손 쪽만 그리기, 코너 안쪽 난간에 걸기(`RailPoint`)는 하지 않았다. 남은 겹침이 거슬리면 다시 본다.
- **검증**: PlayMode 선택 18건 `testcasecount=18 result=Passed passed=18 failed=0 skipped=0`, 2026-11-06 05:26:01Z~05:27:06Z(`ChainRushInputSourceTests`·`ChainRushPresentationTests`·`SwingLook_WhileSwinging_…`·서킷 지문·`Scene_CornerPoles_…`·`ControlFeedback_…`).

## 2026-11-05 · 트랙별 소리: FINGER LANES 음원만 넣고, 나머지는 다시 만든다 (`feature/m6-finger-sound`)

- **결정 (사용자, 2026-11-05)**: 네 트랙 음악·환경음과 새 효과음을 플레이한 뒤 "손가락맵 음원 말고는 다 별로다. 기록해놓고 손가락맵만 커밋 푸쉬 병합하라."
  - **넣은 것**: `TrackLook` 의 `music`·`ambience`, `ChainRushAudio.UseTrackSounds`(`RaceAssembler` → `ChainRushGame.UseTrackSounds`), 환경음용 런타임 `AudioSource`, `Editor/ChainRush/TrackSoundBuilder.cs`(메뉴 Rebuild Track Sounds), `Assets/_Project/Audio/FingerLanes_Music.wav`·`_Ambience.wav`(새소리·시계탑 종, 뜯는 현 120 BPM). 합성 코드는 Codex 작성.
  - **뺀 것**: STADIUM·SKYWAY·PALM COAST 음악·환경음은 사용자가 "별로다" 라고 했다. 세 트랙은 지금까지의 씬 음악 그대로다. 빌더에 세 테마 코드는 남겼지만 굽지 않는다(`themes` 는 FingerLanes 뿐).
  - **효과음은 이 브랜치에 없다**: 동작별 효과음 배선과 소리는 `feature/m6-sound-cues` 에서 계속한다.
- **효과음 평가 (사용자, 같은 말에서)**: "부스터 소리는 지지직 거리는 소리라 잘못되었다. 속도감이 안느껴진다. 제트 소리 같은 느낌", "스윙 소리는 체인&금속의 소리가 아니라 또르르륵 뭔가 굴러가는 소리", "부스트 존 소리도 … 지지직 … 속도감이 느껴지는 하이톤의 소리".
  - **참고 영상 재측정**: 부스터로 본 소리 12번의 평균에서 음악을 뺀 스펙트럼의 고음 봉우리가 약 4.2 kHz → 4.6 → 4.9 → 5.1 → 5.6 kHz 로 2초 동안 올라간다(+15~20 dB). 넓은 잡음이 아니라 올라가는 좁은 대역이다. 저음은 35~60 Hz 에 머문다. 영상의 소리는 복제하지 않고 구조만 참고한다.
- **검증** (에디터 6000.3.19f1, 커밋 전 작업 트리): 컴파일 `*** Tundra build success (1.75 seconds), 5 items updated, 1618 evaluated`, Console Error 0.
  - EditMode `testcasecount="335" result="Passed" passed="335" failed="0" skipped="0"` (07:09:31Z~07:09:36Z).
  - PlayMode `testcasecount="9" result="Passed" passed="9" failed="0" skipped="0"` (07:08:08Z~07:09:12Z). 영향 평가: `ChainRushAudio`·`TrackLook` → `RaceAssembler`·`ChainRushGame` → Presentation 4, Flow 4(`Race_WithATrackLook*`·`Race_FingerLanesLook_*`·`Race_FinishFromTheLobby_*`), 지문 1. **서킷 지문 그대로** `0xEF92FB76BFD1D767`. 주행과 맵이 바뀌지 않아 `Lap`·Device 는 돌리지 않았다. `select-tests` 는 코어 시뮬 파일이 있어 "전체" 로 판정할 수 있어 이름으로 골랐다(CLAUDE.md §9-2).
  - `Rebuild Track Sounds` 를 다시 돌려 WAV 두 개의 SHA-256 이 같은 것을 확인했다(고정 시드).
  - 사용자 플레이: 네 트랙을 들은 뒤의 위 평가. 분리한 이 브랜치의 코드로는 듣지 않았다.
- **리뷰** (병합 전, `72a9d61` 기준): Codex `adversarial-review --base dev` 판정 approve, 지적 없음(사용량은 읽지 않았다). `unity-reviewer` 치명·중대 0, 경미 2건, 제안 5건(`subagent_tokens` 105033, 도구 14회, 304초).
  - 경미 ① `ChainRushAudio.ResetAudio`·`SetMuted` 는 `Awake` 가 끝까지 돌아야 생기는 `trackAmbience` 에 기댄다. 씬 연결이 깨졌을 때만 닿고 그때는 `LogError` 가 먼저 난다. 경미 ② `MusicClip` 이 `TrackAmbienceClip` 과 달리 `initialized` 를 보지 않는다. 호출처는 `Awake` 뒤의 테스트뿐이다. 둘 다 지금 도달하지 않아 고치지 않고 병합했다.
  - 제안 중 남긴 것: 실제 클립으로 환경음의 재생·일시정지·정지를 보는 테스트가 없다. 빌더에 쓰지 않는 세 테마 코드가 있다(의도).

## 2026-11-05 · 사운드 구분: 동작마다 다른 효과음, 벽은 충돌만 (`feature/m6-sound-cues`)

- **결정 (사용자, 2026-11-05)**: 합의 요청서에 "ㅇㅋ ㄱㄱ". 벽 스침은 무음, 강화 그래플은 부스터와 같은 소리(요청서의 추천 그대로).
  - **효과음 14개** (`RaceCue`): 새로 `Boost`·`Grapple`·`PoleHook`·`BoostZone`·`Respawn` 을 뒤에 붙였다(앞 번호의 클립이 그대로다). 안 쓰던 `Crash` 는 `GuardHit` 으로 쓴다. `Action` 은 결승만 남아 `Finish` 로 이름을 바꿨고 소리는 같다. `Beeps`·`Buzz` 는 그대로 안 쓴다. 모두 기존 합성 방식이다.
  - **벽 충돌음**: 벽으로 파고드는 속도가 `RunnerMotor.GuardHitCueSpeed` 3 m/s 이상인 틱에만 낸다. `GuardContact.Glance` 가 그 속도를 없애므로 틱 사이 상태 없이 충돌마다 한 번이다(정면 충돌 1회, 1·2·3° 스침 0회 — 테스트).
  - **찾은 것**: `ChainRushGame.PlayCue` 의 범위 검사가 강화 표현 분기 앞에 있어 번호 4 이상을 강화 표현에서도 던졌다. 그때까지 그 번호를 이 경로로 부르는 곳이 없어 드러나지 않았다. 검사를 enum 범위로 바꿨고, 강화 표현이 꺼지면 첫 네 개 밖의 소리는 `Finish` 자리 클립을 낸다(그 동작들이 전에 내던 소리다).
  - **`ChainRushGame.CuePlayed`**: 어떤 소리가 났는지 테스트가 듣는 이벤트(`UiSound.Played` 와 같은 방식).
- **버린 대안**: 벽을 긁는 소리 — 스침을 무음으로 정했다. 벽에 붙은 상태를 틱 사이에 저장해 소리 간격을 재기 — 스냅샷에 상태가 늘고, 파고드는 속도만으로 충분했다.
- **남은 것**: 롤백 재실행 중에도 소리가 다시 난다(RF2 의 2-1, M7 전). 새 소리도 같다.
- **검증** (에디터 6000.3.19f1, 커밋 전 작업 트리, 바뀐 부분만): 컴파일 `*** Tundra build success (32.19 seconds)`, `scriptCompilationFailed=False`.
  - EditMode `testcasecount="335" result="Passed" passed="335" failed="0" skipped="0"` (05:31:43Z~05:31:48Z).
  - PlayMode `testcasecount="67" result="Passed" passed="67" failed="0" skipped="0"` (05:32:23Z~05:37:52Z, 328.3초). 영향 평가: `RaceCue`·`ChainRushAudio`·`PlayCue` → 호출하는 `RunnerMotor`·`GrappleController`·`ChainRushGame` → Presentation 5·Drift 10·Steering 10·CornerSwing 13·Circuit 11·Rollback 10 클래스 전체, Flow 7(`BoostZone_PalmCoastZ1_*`·`Race_FinishFromTheLobby_*`), 지문 1. `select-tests` 는 "전체" 라 이름으로 골랐다(CLAUDE.md §9-2). `Lap`·Device 는 주행 결과·입력 장치 경로가 바뀌지 않아 돌리지 않았다.
  - **서킷 지문 그대로** `0xEF92FB76BFD1D767`. Console Error 6건은 모두 `BoostZone_PalmCoastZ1_*` 이 일부러 되감아 내는 `RaceAssembler: tick N came after N` 이고 테스트가 `LogAssert.Expect` 로 기다린다.
  - 소리가 귀로 구분되는지는 테스트가 볼 수 없다(파형이 다른 것까지만 `Audio_MoveCues_HaveUnlikeWaveforms`). 사용자 플레이 확인 전이다(검증 안 됨).


## 2026-11-05 · M6 범위: 사운드 구분·봉 스윙 이펙트·고저차 트랙 1개, 태그 M6 = v0.6 · M5 = v0.7 (`docs/m6-scope`)

- **결정 (사용자, 2026-11-05)**: 범위안의 세 질문(범위, 태그, 빌드 확인)에 "3가지 ㄱㄱ". M6 완료 조건이 "마일스톤에 들어갈 때 정한다" 로 비어 있어 먼저 정했다.
  - **넣는 것**: ① 사운드 구분 — 그래플·부스터·봉·결승이 같은 소리(`RaceCue.Action`, `RaceCue.cs:10`)이고 복귀 효과음이 없어서, 부스터·봉 걸림·그래플·부스트존·벽 충돌·복귀마다 소리를 나눈다. 기존 합성 방식이라 외부 음원이 필요 없다. ② 봉 스윙 이펙트(불꽃·줄 끊김·잔상) — 표현 계층이라 서킷 지문이 그대로다. ③ 고저차 트랙 1개 — 지금 `TrackDefinition` 조각으로, 시뮬 변경 없이, 장식 포함.
  - **뺀 것**: 헤어핀·그래플 트랙(T5)과 종합 맵은 시뮬 변경이 따라와 M5 뒤로(마일스톤 미정). 캐릭터 모형 교체는 하지 않는다(~~러너 디자인 5종 유지~~ → 2026-11-06 지금 5종은 임시용이고 최종적으로 바꾼다). 상대 달리기 모션은 원래 M5 항목이다. 그래플 타이밍 보상은 아이디어로 둔다. 환경 아트는 네 트랙 장식·하늘로 끝난 것으로 닫는다.
  - **선택 (필수 아님)**: 장식 빌더 후속, PALM COAST 구간별 바닥. 새 트랙 장식 전에 장식 빌더 후속 ①(`SaveMesh` 순서)을 고치는 쪽을 권했다.
  - **완료 조건 (사용자 플레이)**: 한 판을 돌 때 주요 동작을 소리만으로 구분할 수 있다. 봉 스윙 이펙트가 보기 좋고 시야를 가리지 않는다. 새 트랙을 완주한다. 플레이어 빌드 1회에서 고스트 투명·프레임·화면 설정을 확인한다.
  - **태그**: M6 = v0.6, M5 = v0.7. 진행 순서가 M4 → M6 → M5 → M7 이라(2026-10-30 "멀티 개발 순서") 번호와 태그 순서를 맞췄다. 마일스톤 번호는 그대로 둔다.
  - **빌드**: §9-2 마일스톤 조건에는 없고 M6 의 완료 조건으로만 넣었다. 고스트 투명 머티리얼의 변형이 빌드에서 빠질 위험(2026-10-29 "M4-3 고스트 재생")은 에디터로 확인할 수 없다.
- **튜토리얼**: 사용자 "우리 튜토리얼도 목표에 넣어야할 것 같다. 추후 목표로". M6 범위가 아니고 마일스톤은 정하지 않았다(`DESIGN.md` §10-3).
- **버린 대안**: 남은 항목(새 트랙 전부, 캐릭터 모형·모션)을 모두 M6 에 넣기 — 시뮬 변경과 에셋 제작까지 끌려와 마일스톤이 길어진다.
- **같은 날 사용자 답**: 2026-11-04 항목의 리뷰 수정 뒤 얕은 각도 벽 스침·벽에 붙어 후진 "플레이확인했다". `TrackDefinition: pole 0` 에러 1건은 "모른다" — 원인 미확인으로 두고 다시 나오면 본다. 빌드는 "빌드 뽑아봐야하나?" 에 M6 마감 때 1회를 권했고 위 완료 조건으로 정해졌다. `fix/title-version`(`df2c5c1`, 타이틀 표기 0.4, `dev` 는 이미 0.5)은 사용자 "낡은 것은 삭제" 로 로컬에서 지웠다(원격에 없었다).
- **검증**: 문서만 바꿨다(CLAUDE.md §9-2 문서 검증). 사용자 플레이는 해당 없음.

## 2026-11-04 · 벽·부스터가 방향을 트랙 쪽으로 강제하지 않는다 (`feature/m6-wall-boost-heading`)

- **결정 (사용자, 2026-11-04)**: 요청서(`docs/NEXT_REQUESTS.md` M6 "벽·부스터 방향")에 "ㄱㄱ 1,2,3 모두 진행" — 씬의 `slingTurnRate: 90` 줄은 그대로 두고, 구현은 Codex 쓰기. 첫 구현(벽을 따라 가까운 쪽으로 나란해지고 정면 ±10° 는 트랙 방향)을 플레이한 뒤 "부스터는 괜찮다 다만 충돌에 관해서 보정(꺾이는거)을 없앴으면 좋겠다" → 보정만 지우면 정면에서 갇힌다는 측정을 보고 고른 세 방식(벽 타고 미끄러지기 / 최소 회전 속도만 / 튕기기) 가운데 "추천대로해봐" — 벽 타고 미끄러지기. 요청서의 "안 하는 것: 앞으로 달리다 비스듬히 닿을 때 즉시 나란해지는 방식 자체" 는 이 말로 뒤집혔다.
- **이유**: 사용자 "역주행 테스트 해보려했는데 벽에 닿을떄마다 확 돌아가 버렸다", "부스터 쓸떄마다 … 도로 진행방향으로 강제로 꺾이는 느낌".
- **찾은 것 (측정)**: 보정만 지운 코드에서 벽 쪽을 보고 닿으면 `sideGrip` 60 이 벽을 따라가는 속도를 먹어 멈췄고(`-150°` 에서 `0.00 m/s`), 조향 한도가 속도 ÷ 최소 회전 반경이라(`RunDrive.TurnLimit`) `0.00 deg/s` 가 되어 2 s 끝까지 꺾어도 방향 `-90.0`, 이동 `0.00 m` — 갇혔다. 벽 보정이 이 문제를 가려 왔다.
- **결과**:
  - 벽은 방향을 돌리지 않는다(`GuardContact.Glance` 는 속도만). 속도는 벽과의 각도로만 정해진다 — 사용자 "수직인 경우 뭐 없겠지만 거기서 살짝 꺾은 상태로 전진키를 누르면 그 꺾은 방향대로 점점 움직이며 … 더 꺾일수록 속도는 늘어날수있겠지". 달리는 힘이 벽으로 밀면(지상, 스윙·그래플 아님) `GuardContact.Slide` 가 달리기 전 속도의 벽 성분을 `달리기 목표 × 바라보는 방향의 벽 성분` 으로 보낸다. 달리기 전 속도를 쓰는 것은 그 틱의 side grip 을 피하려는 것이고, `RunDrive.Drive` 가 목표와 가속을 `Push` 로 돌려준다.
  - 벽은 **이동 전에** 찾는다(`RunnerMotor.TryFindGuardAhead`, 달리는 쪽으로 구체 캐스트 12 m, 벽까지 수직 간격으로 판정, 벽 판정은 충돌 콜백과 같은 `IsGuard`). 벽을 밀며 달리는 틱은 `guardScrapeDecel` 을 걸지 않는다(아래 리뷰). 러너는 벽 약 8 cm 앞(서킷 5.54 m)에서 탄다. 처음에는 이동 뒤에 속도만 고쳤는데, 사용자 플레이 "현재 이동방향이 벽과 겹치면 아예 전진하지않는다" — 측정 -85° 에서 표시 속도 1.24 m/s, 실제 1 s 이동 0.10 m 였다. 이동은 side grip 이 먹은 속도로 일어났고 접촉도 틱마다 끊겼다. 값만 재던 테스트가 놓쳤으므로 `Guard_NearSquare_MovesAlongTheRailByTheAngle` 은 실제 이동 거리를 잰다.
  - 벽에 붙은 틱에는 조향이 최소 `guardTurnRate` 60°/s (`GuardContact.TurnOut`, `RunnerMotor` 새 직렬화 필드, 씬에 값이 없어 기본값이다).
  - 틱 사이 상태를 더하지 않았다 — 벽 탐지는 `SnapToGround` 처럼 위치에서 나오므로 `RacerState`·스냅샷·네트워크 코덱이 그대로다(롤백 테스트로 확인).
  - 최소 미끄러짐 비율(수직이어도 달리기 목표의 30%)은 넣으려다 사용자가 막고 위 말로 바로잡았다. 넣지 않았다.
  - 부스터 조향 (벽 확인 뒤 사용자 "부스터 중에 조향이 너무 잘된다 … 오히려 덜꺽여야", "부스터 중에 조향 하는 중 속도가 줄어들지 않는다"): 회전 반경이 고정이라 빨라질수록 초당 더 돌았고(서킷 계산 달리기 45.8°/s, 부스터 32 m/s 73.3°/s), 조향 감속은 기본 목표에만 걸려 부스터 목표가 `Max` 로 덮었다. 슬링샷이 도는 동안(`SlingTicks > 0`) 최소 회전 반경을 `boostTurnRadiusScale` 2배로(서킷 계산 36.7°/s, 달리기의 0.8배), 조향 감속을 부스터 목표에도(`Slingshot.RaiseTarget` 의 `turnCost`) 건다. 처음 값(달리기와 같은 20%, 서킷 계산 32 → 25.6 m/s)을 플레이한 뒤 사용자 "좋은데 조향으로 인한 감속 절반으로 줄여줘" → 부스터만 10%(서킷 계산 32 → 28.8 m/s), 이어서 "부스터때 조향감속 굳. 일반 상태일때도 감속 절반이어야한다" → 일반 주행과 부스터 공통 `turnSpeedLossScale` 0.5 (`RunnerMotor`, 끝까지 꺾으면 10%, 벤치 일반 주행 16.00 → 18.00 m/s). 씬의 `turnSpeedLoss: 0.2` 는 §0 으로 손대지 않아 인스펙터에는 0.2 로 보이고 실제 값은 0.1 이다 — 에디터에서 씬 값을 0.1 로 저장하면 배율을 지울 수 있다. 부스트존·스윙 뒤 남는 속도는 사용자가 말한 범위가 아니라 그대로다.
  - 조향 감속이 내려가는 속도 (사용자 "최대 감속은 좋으나 감속 정도가 심하다 덜 꺾이게 해줘. 감속 속도를 1/3 정도로"): 10% 바닥은 두고, 조향 때문에만 목표보다 높아진 구간(곧은 길 목표 이하)의 감속을 `turnFalloffScale` 1/3 로 늦춘다(`RunDrive.Drive`). `overspeedFalloff` 8 자체는 오르막·부스터 끝 감속에도 쓰여 그대로 둔다. 곧은 길 목표보다 높은 속도는 전처럼 떨어진다. "덜 꺾이게" 는 속도 곡선으로 읽어 회전 반경은 건드리지 않았다. 이어서 사용자 "여기서 조향에 의한 속도 감속 되는정도 1/3 더 해줘" → 1/3 을 한 번 더 곱해 `turnFalloffScale` 1/9 (약 0.89 m/s², 20 → 18 m/s 약 2.25 s).
  - 서킷 지문 새 기준 `0xEF92FB76BFD1D767` (일반 주행의 조향 감속과 벽 처리가 바뀌어 봇 런이 달라졌다. 그 전 단계 감속 절반 `0x6CBD18FC2CF82B92`, 감속 속도 1/3 `0xD1565C51B5A3AD59`, 1/9 `0x8CFB4E1FE46B947B`). 그 전에 한 번: 조향 감속 곱을 함수(`TurnCost`)로 뺀 것만으로 수식이 같은데 부스터 없는 봇 런의 지문이 `0x1D29056702ADC64F` 로 바뀌었고, 곱을 부르는 자리에 두자 `0x18F31E7A2B4F79B3` 로 돌아왔다. 함수 경계에서 한 번 더 반올림되는 것으로 본다(반올림 자체를 재지는 않았다, 지문 복귀로 확인). 이제 지문이 어차피 바뀌므로 `TurnCost` 로 합쳤다.
  - 그래서 조향 없이 벽을 밀면 커브를 끌려가지 않고 멈춘다. 스윙이 벽에 끊기면(안쪽 키로 줄을 감은 지름길 스윙) 멈춘 자리에서 직접 돌려 나와야 한다 — 전에는 벽이 돌려 줬다.
  - 부스터(`Slingshot`): `SteerToward`·`Settings.TurnRate`·`RunnerMotor.slingTurnRate` 삭제. 방향을 돌리지 않고 바라보는 쪽으로 민다. 체인 표시 점은 `HookPoint` — 바라보는 방향 `slingLead` 앞. 커브에서 코스를 따라 꺾어 주던 도움은 없어졌고, 부스터 중 조향은 그대로다(`RunnerMotor.cs` `RunDrive.Turn`).
  - 씬 `ChainRushCircuit.unity`·`ChainRushTestBench.unity` 에 `slingTurnRate: 90` 이 남는다(§0 으로 손대지 않는다). 다음 에디터 저장 때 diff 에 나오면 Unity 가 지운 것으로 기록한다.
- **구현 분담**: 부스터와 첫 벽 구현은 Codex(`task --write --effort high`) 1회. 지시서에 `승인:`·`브랜치:` 줄, 파일 5개 범위, Unity·git 쓰기·`.meta`·씬·문서 금지. 바뀐 파일은 그 5개뿐이었다(`git status`, Unity 가 다시 쓴 `ProtoHarness.slnx` 제외). 플레이 뒤 벽 재설계·검증·문서는 Claude.
- **리뷰** (병합 전, `2f3bbfe` 기준): `unity-reviewer`(subagent_tokens 141867)와 Codex `adversarial-review --base dev`(5시간 `used_percent` 는 읽지 않았다). 실제 결함 두 리뷰 합쳐 2건 — ① 얕은 각도 벽 접촉 정지(`unity-reviewer` 만, 캐스트 2 m 면 약 3.3° 아래를 못 찾고 서킷 `guardScrapeDecel` 25 가 걸린다, 측정으로 확인) ② 벽에서 후진 조향 부호 반대(둘 다). 규칙 위반 지적 1건 — Lap 기준 변경이 그때 커밋 전이었다(이 커밋에 넣었다, 사용자 답은 "ㅇㅇ"). 고친 것: 캐스트 12 m(약 0.55° 까지), 벽을 밀며 달리는 틱은 `Scrape` 를 걸지 않는다, `RunDrive.SteerSign` 을 `TurnOut` 도 쓴다, 낡은 주석·메시지. 그대로 둔 것(`unity-reviewer` 사소): `ShortcutBot` 은 벽에 끊긴 스윙도 `Taken` 으로 센다, 스윙·부스트존 carry 가 목표일 때 `turnFalloffScale` 판정이 곧은 길 목표와 조금 다르다, `Curve_NoSteering` 제한 8 s.
- **테스트를 고친 것 (사양 변경, 사용자 승인 "추천대로해봐", Lap 기준은 "ㅇㅇ")**: `Race_BotLap_…` 의 가드 기준을 보통 속도에만(위 검증 줄), `Guard_AngledHit_TurnsAlongRailAndRecoversSpeed` → `Guard_AngledHit_SlidesAlongRailKeepingTheFacing`(방향 그대로, 벽 속도 = 20 × cos), `Curve_NoSteering_OuterGuardTurnsRunnerWithoutFall` → `Curve_NoSteering_OuterGuardHoldsTheRunnerWithoutTurningIt`(떨어지지 않고, 방향 그대로, 커브를 끌려가지 않는다). `ShortcutBot`: 스윙이 벽에 끊기면 조향을 되찾는다 — 방향이 88° 돌아야 스윙이 끝났다고 보고 조향을 꺼 둔 채라, 전에는 벽이 돌려 줘서 우연히 풀렸다. `Race_FingerLanesShortcuts_InnerKeyHeld_TheRopeAddsNoSpeed` 의 단언은 그대로이고 `where` 에 진단 값만 더했다.
- **새 테스트**: `Guard_FacingBackward_SlidesBackwardKeepingTheFacing`, `Guard_SquareOnHit_SteeringTurnsTheRunnerAway`, `Sling_AngledFacing_KeepsTheFacingWhilePulling`, `Rollback_SlidingAlongAGuard_ReplaysEveryTickBitForBit`.
- **검증** (에디터 6000.3.25f1, UTC). 실패한 실행도 모두 적는다:
  - **병합 코드** (리뷰 수정 뒤, 같은 코드, 모두 통과): EditMode `testcasecount="335" result="Passed" passed="335" failed="0"` (07:43:17Z~07:43:21Z). 고른 PlayMode `testcasecount="74" result="Passed" passed="74" failed="0"` (07:43:38Z~07:48:59Z) — Flow `Race_` 15, Circuit 11, Rollback 10, Drift 9, Steering 9, CornerSwing 13, Curve 3, 부스터 HUD 3, 지문 1. Lap 전 트랙 `testcasecount="14" result="Passed" passed="14" failed="0"` (07:49:31Z~07:52:55Z). 지문 `0xEF92FB76BFD1D767`, 벽 롤백 스냅샷 `guard@142, guard@162`.
  - 리뷰 수정 전 측정: 얕은 각도(서킷, 벽과 1·2·3°, 1.5 s 뒤) `testcasecount="3" result="Failed(Child)" passed="1" failed="2"` (07:39:33Z~07:39:52Z) — 1.01 / 0.00 / 22.15 m/s. 후진 부호 한 줄만 되돌린 실행 `testcasecount="4" result="Failed(Child)" passed="0" failed="4"` (07:42:00Z~07:42:26Z) — 후진 +27.8°(고친 뒤 -30.0°), 얕은 각도 3건은 22.15~22.17 m/s 로 고쳐졌고 내 "벽에 닿음" 기준 5.55 m 가 빡빡해 실패했다(이동 전 탐지라 5.54 m 에서 탄다, 기준을 5.5 m 로 고쳤다).
  - 리뷰 전 코드 (`turnFalloffScale` 1/9): EditMode `testcasecount="335" result="Passed" passed="335" failed="0"` (07:20:08Z~07:20:12Z). 고른 PlayMode `testcasecount="70" result="Passed" passed="70" failed="0"` (07:20:49Z~07:25:46Z) — Flow `Race_` 15, Rollback 10, Drift 9, Steering 8, Circuit 8, CornerSwing 13, Curve 3, 부스터 HUD 3, 지문 1, 지문 `0x8CFB4E1FE46B947B`. Lap 전 트랙 `testcasecount="14" result="Failed(Child)" passed="12" failed="2"` (07:26:20Z~07:29:42Z): `Race_BotLap_…(Fast,"STADIUM")` `The bot touched a guard. … max offset 5.62 m at S 272.4 (segment 11) Expected: less than 5.5999999f But was: 5.6197896f`, `Race_BotLap_…(Fast,"PALM COAST")` `… max offset 6.82 m at S 1807.9 (segment 42) Expected: less than 6.80000019f But was: 6.8190918f`. 두 값 모두 벽에 닿은 값(반폭 − 캡슐 0.38 m)이다. 조향 감속을 줄여 봇이 코너를 더 빠르게 지나면서, 2026-10-25 매우빠름에서 허용한 스침이 빠름까지 내려온 것으로 본다(이 설명을 따로 재지는 않았다). 후보 셋(빠름도 스침 허용 / 감속 되돌리기 / 봇 고치기) 가운데 사용자 "ㅇㅇ"(추천안에 대한 답으로 읽었다) → `Race_BotLap_…` 의 가드 기준을 보통 속도에만 남겼다. 코너는 봉 스윙으로 도는 설계라 앞선 결정과 같은 이유다. 기준을 고친 뒤 빠름 4칸 `testcasecount="4" result="Passed" passed="4" failed="0"` (07:32:34Z~07:33:31Z). 보통·매우빠름 칸과 지름길은 위 실행에서 통과했고, 고친 줄은 그 칸의 결과를 바꾸지 않는다.
  - 컴파일: `Unity_RunCommand` `isCompilationSuccessful=true`, `HookPoint`·`Slide` 있음, `SteerToward` 없음.
  - 첫 구현(Codex, 벽은 나란해짐): EditMode `testcasecount="335" result="Passed" passed="335" failed="0"` (05:19:37Z~05:19:41Z), PlayMode 고른 테스트 `testcasecount="40" result="Passed" passed="40" failed="0"` (05:20:13Z~05:22:42Z).
  - 고치기 전 실패(런타임 3파일을 잠시 되돌림, 그때의 새 테스트 3건): `testcasecount="3" result="Failed(Child)" passed="0" failed="3"` (05:23:42Z~05:23:49Z). 원문 `The booster must keep the facing. … But was: 1.80000305f`, `Outside the square-on range the nearer facing wins. … But was: 180.0f`, `The guard must keep the backward direction. … But was: 179.858231f`. 지금의 벽 테스트를 원래 코드에서 돌리지는 않았다.
  - 보정만 지운 코드: `testcasecount="11" result="Failed(Child)" passed="7" failed="4"` (05:29:35Z~05:31:13Z) — 위 "찾은 것" 의 측정. `Guard_SquareOnHit…`(`Expected: greater than 30.0f But was: 0.0f`), `Curve_NoSteering…`(`But was: 54.920398712158203d`), `Guard_AngledHit…`(`But was: -21.983139f`), `Guard_AngledContact…(-30)`(`Waited for 100 ticks. Expected: 200 But was: 177` — 지웠다. 앞으로 달리는 경우는 `Guard_AngledHit` 가 덮는다. 벤치 도로 끝으로 보이나 확인하지 않았다).
  - **지금 코드(부스터 조향)**: 고치기 전 `Sling_Steering_*` `testcasecount="2" result="Failed(Child)" passed="0" failed="2"` (06:07:27Z~06:07:37Z, 벤치): 0.5 s 회전 달리기 20.3° / 부스터 27.2°, 1 s 끝까지 꺾은 속도 24.00 → 25.24 m/s. `TurnCost` 판 고른 테스트 `testcasecount="46" result="Passed" passed="46" failed="0"` (06:09:04Z~06:12:02Z) — 지문만 `0x1D29056702ADC64F` 로 바뀌었다(위). `TurnShare` 로 고친 뒤 부스터 조향 2건·지문 `testcasecount="3" result="Passed" passed="3" failed="0"` (06:13:55Z~06:14:40Z): 회전 20.3° / 12.6°, 속도 24.00 → 20.79, 놓고 0.5 s 뒤 23.09 m/s, 지문 `0x18F31E7A2B4F79B3`. 감속 절반 뒤 `Sling_*` 와 지문 `testcasecount="8" result="Passed" passed="8" failed="0"` (06:58:15Z~06:59:13Z): 회전 20.3° / 13.4°, 속도 24.00 → 23.37(목표 23.4), 놓고 0.5 s 뒤 24.53 m/s, 지문 `0x18F31E7A2B4F79B3`. 일반 주행 고치기 전 `Run_FullSteer_*` `testcasecount="1" result="Failed(Child)" passed="0" failed="1"` (07:04:38Z~07:04:42Z, `Expected: 18.0f +/- 0.300000012f But was: 16.0f`). 공통 배율 뒤 `Sling_*`·`Run_FullSteer_*`·지문 `testcasecount="9" result="Passed" passed="9" failed="0"` (07:06:02Z~07:07:04Z): 일반 18.00 m/s, 부스터 23.37 m/s, 회전 20.8° / 13.4°, 지문 `0x6CBD18FC2CF82B92`. 감속 속도 고치기 전 `Run_FullSteer_*` `testcasecount="1" result="Failed(Child)" passed="0" failed="1"` (07:10:05Z~07:10:10Z, 0.4 s 에 이미 18.00 m/s, `Expected: greater than 18.6000004f But was: 18.0f`). 고친 뒤 `Sling_*`·`Run_FullSteer_*`·지문 `testcasecount="9" result="Passed" passed="9" failed="0"` (07:11:11Z~07:12:14Z): 0.4 s 18.89 m/s, 1.5 s 18.00 m/s, 부스터 23.38 m/s, 회전 21.7° / 13.4°, 지문 `0xD1565C51B5A3AD59`. 1/9 뒤 같은 9건 `testcasecount="9" result="Passed" passed="9" failed="0"` (07:16:26Z~07:17:30Z): 0.4 s 19.60 m/s, 2.6 s 18.00 m/s, 부스터 23.39 m/s, 회전 22.2° / 13.6°, 지문 `0x8CFB4E1FE46B947B`. 이 코드로 EditMode·고른 테스트 전체·Lap 은 아직 돌리지 않았다.
  - 이동 전 벽 탐지 코드: 측정 테스트를 넣은 이동 뒤 `Slide` 코드 `testcasecount="3" result="Failed(Child)" passed="1" failed="2"` (05:57:00Z~05:57:14Z, 원하는 값 / 표시 / 1 s 이동: -85° 1.74 / 1.24 / 0.10 m, -75° 5.18 / 4.68 / 3.67 m, -60° 10.00 / 9.50 / 8.76 m). 고친 뒤 Steering·Curve·벽 롤백·스윙 벽·지문 `testcasecount="14" result="Passed" passed="14" failed="0"` (05:59:36Z~06:01:09Z): -85° 1.74 m, -75° 5.18 m, -60° 9.86 m, -150° 벽 속도 -17.32 m/s, -22° 18.55 m/s, 정면 뒤 2 s 조향 방향 16.9·23.10 m 이동, 스냅샷 `guard@143, guard@163`, 지문 `0x18F31E7A2B4F79B3`. 이 코드로 EditMode·고른 테스트 전체·Lap 은 아직 돌리지 않았다(아래 줄들은 그 전 코드).
  - 이동 뒤 `Slide` 코드: PlayMode 고른 테스트 `testcasecount="40" result="Passed" passed="40" failed="0"` (05:36:03Z~05:38:37Z) — CornerSwing 13, Rollback 9, Drift 6, Steering 5, Curve 3, `ChainRushTests` 부스터 HUD 3, 서킷 지문 1. 롤백 새 테스트와 커브 `testcasecount="2" result="Passed" passed="2" failed="0"` (05:39:56Z~05:40:06Z), 스냅샷 `guard@145, guard@165`. EditMode `testcasecount="335" result="Passed" passed="335" failed="0"` (05:40:34Z~05:40:39Z). 이 뒤로 바뀐 것은 테스트 파일뿐이다.
  - 서킷 지문: `0x18F31E7A2B4F79B3` — 그대로다. 이 봇 런(STADIUM 1700틱)의 결과는 바뀌지 않았다.
  - Lap 전 트랙(공용 주행 코드, VERIFICATION §4-4 — 첫 구현 때 뺀 판단을 벽 재설계로 뒤집었다): `testcasecount="14" result="Failed(Child)" passed="13" failed="1"` (05:40:54Z~05:45:13Z). 12칸과 지름길 1건 통과, `Race_FingerLanesShortcuts_InnerKeyHeld_TheRopeAddsNoSpeed` 실패 `The runner did not finish a lap. tick 18016, 0 laps, shortcuts taken 0, hooks missed 0`. 진단 재실행 `testcasecount="1" result="Failed(Child)"` (05:46:27Z~05:47:29Z): `28 swinging ticks … progress 383.7 m, heading 51.3, speed 0.00 m/s, swinging False`. `ShortcutBot` 을 고친 뒤 지름길 `testcasecount="2" result="Passed" passed="2" failed="0"` (05:48:27Z~05:48:57Z): `156 swinging ticks, top 24.00 m/s … shortcuts taken 2, hooks missed 0`(전 기록 157, 24.00). 봇을 고친 뒤 Lap 12칸은 다시 돌리지 않았다 — 그 12칸은 `ShortcutBot` 을 쓰지 않는다.
  - 영향 평가: `GuardContact`·`Slingshot`·`RunDrive.Drive` → 읽는 곳 `RunnerMotor.Step`·`MoveAndCollide` → 고른 테스트 위와 같다. `select-tests` 판정은 "전체"(코어 시뮬), 규칙대로 게이트 전체는 돌리지 않았다.
  - 사용자 플레이: 부스터 "괜찮다". 벽 재설계 뒤는 아직.

---

## 2026-11-03 · STADIUM 대형 경기장과 장식 디테일 (R4, `feature/m6-stadium-detail`)

- **결정 (사용자, 2026-11-03)**: 다음 후보 중 "스타디움 디테일 작업부터 … codex로 구현해봐" → 요청서(LED 보드·결승 갠트리·조명탑·코너·벤치, 0 단계 렌더에서 이미 있는 전광판·관중은 뺌) "ㄱㄱ". 렌더 뒤 "스타디움을 정말 큰 스타디움으로 … 해외축구 경기장 같이 높이로도 꽤 커서 외부 전경이 잘 안보이는" → 요청서(1층 뒤 위층 고리·지붕 고리·전광판·구운 메시 정리) → "웸블리 스타디움, 서울 종합경기운동장 같은 느낌으로 만들어줘 ㄱㄱ". 렌더 뒤 "2층에 생긴 좌석이 어둡다", "2층 좌석과 1층 좌석의 모양이 달라 괴리감" → 1층과 같은 의자로. 최종 "좋다 확인했고 삭제 승인한다. 이후 커밋 병합 푸시 갱신하라".
- **찾은 것 (요청서 전, 에디터 실측)**: STADIUM 은 레퍼런스 영상이 없는 원작 트랙이다. 구운 1층 관중석(`SeatBlue`·`SeatMuted`·`Concrete`)은 도로 가장자리 바깥 o ≤ 17 m(옆)~34 m(끝), 높이 ≤ 9 m, 그 바깥 o 36~58 m 에 가는 조명 기둥·옛 양 끝 전광판(23 m)·바깥 건물·나무 — 트랙에서 9 m 너머 바깥이 다 보였다. 1층 의자 = 앉는 판(줄 방향 0.78 × 깊이 0.68 × 0.16) + 등받이(0.78 × 0.13 × 0.64), 간격 1.65 m, 줄마다 0.65 m 오름.
- **결과** (`StadiumEnvironmentBuilder` 의 `Layout` 만, 트랙·시뮬·TrackLook·하늘 그대로):
  - `05` 바깥 직선 LED 광고 보드(앞면 가장자리 +1.6 m, 노면 위 0.4~1.2 m, 6 m 조각, 민트·호박·자홍), `06` 결승 갠트리(보·시계 9.4 m 위부터, 기둥 가드 밖 1 m)와 사진 판독 카메라, `07` 코너 바깥 허들 64·안쪽 패딩 76, 팀 벤치 2, 엔드존 깃대 12.
  - `08` 위층 스탠드 고리: 트랙 바깥 가장자리 오프셋(o) 36 m 콘코스 벽(출입구 불빛) → 6 m 처마 → 계단 32줄(o 30 → 72, 1층과 같은 콘크리트) 위 1층과 같은 의자 14,880개(같은 크기·간격·재질 비율, 12자리마다 통로) → 뒤 통로 → 바깥으로 벌어지는 흰 외벽(o 76 → 82)과 20 m 마다 갈빗대. 높이는 안장형(옆 44 m · 끝 36 m, 잠실식 곡선 지붕선).
  - `09` 지붕 고리: 위층보다 8 m 위, 안쪽 끝 o 24 까지, 앞 가장자리 아랫면 조명 띠. 그림자를 드리우지 않는다 — 위층 좌석이 지붕 그림자에 묻혀 어두웠다(사용자 "2층 좌석이 어둡다").
  - `10` 흰 격자 아치(웸블리식): x 축을 가로지르는 포물선, 정점 120 m, 다리는 외벽 50 m 바깥. 지붕 뒤 가장자리 위에서 지붕보다 1 m 이상 높지 않으면 던진다(지금 약 5 m). `11` 양 끝 지붕 아래 전광판(발광 판, 글자 없음).
  - 구운 메시 정리: 위층에 묻히는 부분(o > 30), 양 끝 1층 지붕 윗부분(o > 20 · 높이 > 12), 옛 전광판 메시(`Mint`·`Ink` 의 o > 20 전부)를 잘라 `StadiumExterior_<Mesh>.asset` 로 저장. 바깥 잔디(`TurfDark`)는 정점 전부가 o 38~45 m 라 목록에서 뺐고, 잘라 낸 결과가 비면 던진다. 처음 놓은 조명탑 4개(`04`)는 위층에 묻혀 지웠다.
  - 새 메시는 재질별로 합쳐 렌더러 432 → 1051, 정점 약 98만(의자 메시 하나 23 MB). 위층 정점 70만 넘으면 던진다.
- **구현 분담**: Codex(`task --write --effort high`) 세 번 — 디테일, 대형 볼, 의자. 지시서마다 `승인:`·`브랜치:` 줄, 파일 하나 범위, Unity·git 쓰기·`.meta`·문서 금지. 바뀐 파일은 매번 그 하나뿐(`git status`). Claude 가 메뉴 실행·렌더·리뷰를 했고 렌더를 보고 세 곳을 고쳤다: 아치(첫 95 m · 발 13 m 는 옆 지붕 52 m 에 박힘), 옛 전광판(높이 자르기로 "STADIUM" 글자 아래 절반이 남음), 메시 이름(`Bowl` + 재질 이름 `M_Stadium…` → `Bowl_…`).
- **검증** (이 PC 의 에디터 6000.3.19f1 — 프로젝트 버전은 6000.3.25f1, UTC):
  - 최종(리뷰 수정 `eccfe8d` 뒤, 에디터가 닫혀 배치 모드 `-executeMethod …RebuildExterior`): 컴파일 `*** Tundra build success (2.50 seconds), 13 items updated, 1618 evaluated`, 원문 "Stadium Exterior clearance: nearest renderer bounds 0.75 m outside a road edge (at least 0.7 m below 9 m over the road); 0 parts under the slab; 958796 vertices checked one by one." / "StadiumExterior: 1051 renderers; physics=0; runtime scripts=0; road corridor clear." (그 전 에디터 Rebuild 는 1052 renderers — 빈 `TurfDark` 렌더러 포함.)
  - 영향 평가: 바뀐 것은 장식 빌더와 그 생성물(프리팹·메시·재질)뿐 → 읽는 곳은 레이스 씬의 장식 로드(`RaceAssembler`, `ChainRushTrackStadium.unity` 의 프리팹) → 그것을 불러 검사하는 비-Lap 테스트 `ChainRushFlowTests.Race_FinishFromTheLobby_KeepsTheBestAndItsGhost_AndRunAgainRacesIt`(STADIUM 장식 로드 20 s 안, 루트 이름, 렌더러 있음, Collider·Rigidbody·MonoBehaviour 없음, `LoadLapRace` `ChainRushFlowTests.cs:1767-1797`). Lap 은 장식만 바뀌어 돌리지 않았다(VERIFICATION §4-4). 에디터에서 시작한 실행은 사용자가 에디터를 닫아 결과 없이 끊겼고, 배치 모드로 다시 돌렸다(모두 실행):
    - `e5f61b1`: `testcasecount="1" result="Passed" passed="1" failed="0"` (06:30:43Z~06:31:02Z).
    - 리뷰 수정 뒤 Rebuild 한 작업 트리(= `eccfe8d`): `testcasecount="1" result="Passed" passed="1" failed="0"` (06:36:51Z~06:37:13Z). 그 뒤 `Assets/` 변경은 참조 0건이 된 `StadiumExterior_TurfDark.asset` 삭제뿐이고, 커밋 뒤 `git status` 에 `Assets/` 없음.
  - 렌더(미리보기 장면, 밤 하늘·안개, 도로 근사): 트랙에서 위층·지붕 고리가 시야를 감싸 바깥이 안 보인다, 안개 60–300 m 안에서도 맞은편 스탠드가 보여 안개는 그대로.
  - 병합(② → `dev`, 사용자 "커밋 병합 푸시 갱신하라"): 위 영향 평가의 테스트 1건(두 실행 모두 위에 적음), 컴파일 에러 0. Lap·게이트 전체는 돌리지 않았다(§4-4, §9-2 ②).
- **사용자 플레이 확인**: "좋다 확인했고" (의자 수정 뒤). 프레임 수치는 재지 않았다.
- **지운 것**: (사용자 "삭제 승인한다") 작업 중 생겨 아무것도 참조하지 않는 추적 안 된 생성물 18개(+ .meta) — 이름 바꾸기 전 `StadiumExterior_BowlM_*`·`RoofM_*` 10, 색 블록 좌석 메시 `StadiumExterior_Bowl_Bowl{Red,Navy,Pale,Amber}` 4, 재질 `M_StadiumBowl{Red,Navy,Pale,Amber}` 4. (사용자 "지운다") 리뷰 수정으로 고아가 된 빈 메시 `StadiumExterior_TurfDark.asset`. 지우기 전 모두 GUID 참조 0건 확인.
- **리뷰** (병합 전, CLAUDE.md §7-5, "그 밖" 규모 — 파일 1개 +~800줄):
  - unity-reviewer(`subagent_tokens` 118462): 치명 0 · 중대 1 · 제안 5. 중대 = 실제 결함: 바깥 잔디 메시가 정점 0개로 저장되고 빈 렌더러가 남음(§5 조용한 실패) → `eccfe8d` 로 고침. 제안: `Kept`/`Cleared` 구분이 의미를 잃음(같은 커밋에서 `Trimmed` 로 합침), 저장 뒤 검사(아래 Codex ①과 같음), 사진 판독 카메라가 왼쪽 타이밍 기둥 바로 뒤라 결승선 쪽 시야가 가림(디자인 — 그대로), `ScreenBottom` 주석 정밀도, UV 를 늘 Vector4 로(원본에 UV 없어 영향 없음). 체크리스트 §0/§1/§2 위반 없음, 결정성·정점 수 주석(690,220) 일치.
  - Codex 설계 리뷰(비용: `/status` 를 읽지 않아 모름): needs-attention 2건 — ① 메시를 검사 전에 저장해 뒤 검사가 실패하면 메시만 바뀐 채 남는다(실제 위험, 단 이 브랜치 전부터 있는 공통 방식 — `dev` 의 STADIUM `SportsComplex`, `SkywayEnvironmentBuilder.cs:403`. `ExteriorAssets` 공통 수정으로 후속 후보, 실패하면 git 으로 되돌린다). ② 위층 콘코스 벽은 정점이 땅과 12.35 m 에만 있어 정점 단위 침범 검사가 둘 다 높이 예외로 건너뛴다(실제 공백, 단 벽은 도로 가장자리 오프셋 36 m 로 만들어 볼록 루프에서는 도로와 만날 수 없다 — 빌더가 왼쪽 회전을 던진다. 면 단위 검사는 후속 후보). 둘이 겹친 지적은 ①뿐이고, 빈 메시는 unity-reviewer 만, 벽 검사 공백은 Codex 만 찾았다.
- **버린 대안**: 위층을 o 60 m 바깥에(구운 바깥 장식과 안 겹치지만 1층과 위층 사이 땅이 보인다), 위층 좌석을 색 블록으로(사용자 "괴리감"), 안개를 120–420 m 로(렌더에서 필요 없었다), 1층 관중석 재구성(범위 밖).

## 2026-11-02 · 서킷 공통 하늘: TrackLook 의 스카이박스·지평선 안개 (`feature/m6-track-sky`)

- **결정 (사용자, 2026-11-02)**: FINGER 렌더를 보고 "좋은데 최외곽이 살짝 비어보인다. 하늘같은걸 구현해 놓는건 어떤가? 서킷 공통적으로 쓰일만하게" → 요청서(TrackLook 에 하늘·지평선 색·안개 거리, 레이스 시작 때 적용, 내장 Panoramic 셰이더로 커스텀 셰이더 없이) "ㄱㄱ 추천대로, finger 먼저 커밋하고 진행" — FINGER·PALM·SKYWAY 낮, STADIUM 밤.
- **찾은 것 (요청서 전)**: 레이스 씬(`ChainRushCircuit.unity`)은 카메라를 단색 남색으로 지우고, 같은 색 선형 안개 45 → 210 m, 먼 클리핑 350 m 였다. 런타임에서 트랙별로 바꾸는 코드가 없어 네 트랙 모두 남색 밤에 100 m 부터 건물이 남색으로 묻혔다. 그때까지의 장식 미리보기 렌더(파란 단색, 안개 없음)는 이를 반영하지 못했다.
- **결과**:
  - `TrackLook`: `sky`(스카이박스 머티리얼)·`horizon`(안개 색)·`fogStart`·`fogEnd`. 하늘이 있으면 안개 끝이 시작보다 멀어야 한다(`TryValidate`). 하늘이 없는 look 은 씬 그대로.
  - `RaceAssembler`: 직렬화 참조 `viewCamera`(레이스 씬의 `Main Camera`, 사용자가 씬 저장) — `Awake` 에서 필수. `Start`(레이스 씬이 활성 씬이 된 뒤)에서 `UseSky`: `RenderSettings` 의 스카이박스·선형 안개(지평선 색, 시작·끝), 카메라 Skybox 지우기, 먼 클리핑 = 안개 끝. 이 값들은 레이스 씬의 것이라 로비를 부르면 함께 사라진다. 표현만 — 시뮬은 읽지 않는다.
  - `ExteriorAssets.BuildSky`: 위도·경도 파노라마 2048×1024 를 고정 시드로 굽는다 — 지평선 아래는 지평선 색(안개와 이음매 없음), 위로 천정 색까지, 하늘 위 평면에 원근으로 놓인 값 노이즈 구름(지평선 쪽은 고도 사인 0.25 → 0.05 에서 사라짐 — 첫 굽기에서 점 구름 띠), 밤에는 별. `T_<prefix>Skybox.png`(sRGB, 밉맵 없음, 가로 반복·세로 클램프, 고품질 압축) + 내장 `Skybox/Panoramic` 머티리얼 `M_<prefix>Skybox`(틴트 회색 0.5 = 원색). `BuildLook` 은 하늘을 반드시 받는다.
  - 네 빌더: 트랙별 값, 장식은 그대로 두고 look 만 다시 만드는 메뉴 `Rebuild <트랙> Look`(`BuildLook` 을 public 으로 — MCP 가 리플렉션을 막아 계획에 더함). FINGER 낮 구름 42% 안개 160–520 m, PALM 낮 30% 170–560 m, SKYWAY 옅은 낮 50% 180–620 m, STADIUM 밤·별·어두운 구름 22% 60–300 m.
- **고친 것 (작업 중 내 실수)**: 처음 이름 `M_<prefix>Sky` 가 SKYWAY 빌더의 팔레트 머티리얼 `M_SkywaySky`(옅은 하늘색 Lit, `SkywayExterior.prefab` 이 씀)와 같아 그 셰이더를 스카이박스로 덮어썼다. 그 파일 하나만 `git restore` 로 되돌리고(셰이더 `Universal Render Pipeline/Lit` 확인), 이름을 `Skybox` 로 바꿨다. 셰이더에 없는 키워드 `_MAPPING_LATITUDE_LONGITUDE_LAYOUT`(새 머티리얼이 enum 기본값으로 가짐)도 지운다.
- **검증** (이 PC 의 에디터 6000.3.19f1 — 프로젝트 버전은 6000.3.25f1, UTC, 커밋 전 작업 트리): 내장 `Skybox/Panoramic`·`Procedural`·`Cubemap` 이 URP(`PC_RPAsset`)에서 지원됨 확인. 영향 평가: `TrackLook` 검증 → `TrackLookTests`; `RaceAssembler.Awake` 의 카메라 필수 → 레이스 씬을 여는 테스트(Flow·Circuit·Presentation·Replay 등) 중 대표로 `ChainRushPresentationTests`; `UseSky` → 새 테스트와 로비 → 레이스. Lap 은 표현만이라 돌리지 않았다(§4-4) — 랩 런의 `AssertLaysTheLook` 에 하늘·안개 단언을 더했지만 돌리지 않았다.
  - EditMode `TrackLookTests` `testcasecount="8" result="Passed" passed="8" failed="0"` (09:45:05Z).
  - PlayMode `Race_WithATrackLook*` 2(새 `Race_WithATrackLookSky_DrawsTheSkyAndFogsToItsHorizon` 포함)·`Race_FromTheLobby_RacesTheChosenDefinitionAndCountsIn`·`ChainRushPresentationTests` 3 `testcasecount="6" result="Passed" passed="6" failed="0"` (09:45:25Z~09:45:32Z).
  - 네 트랙 렌더(미리보기 장면, 카메라 `Skybox` 컴포넌트 + 활성 씬 안개를 찍는 동안만 트랙 값으로 바꾸고 되돌림, 씬 dirty 없음): 도로 위·바깥쪽 각 1장.
- **사용자 플레이 확인**: "확인했다" (렌더 시트·플레이 뒤, 커밋·병합·푸시 지시와 함께). 프레임 수치는 재지 않았다.
- **지운 것**: 처음 이름으로 만든 고아 생성물 `M_{Finger,Palm,Stadium}Sky.mat`·`T_{Finger,Palm,Skyway,Stadium}Sky.png`(+ .meta, 추적 안 됨, 서로만 참조) — 사용자 "승인한다". 테스트 실행 뒤 `Assets/` 에서 바뀐 것은 이 삭제뿐이고, 이것을 읽는 코드·에셋은 없다.
- **병합 (② `feature/m6-finger-detail` → `dev`, 이어서 `feature/m6-track-sky` → `dev`, 2026-11-02, 사용자 "커밋 병합 푸쉬 하라")**: `tools/select-tests.ps1 -Range dev...HEAD`(두 브랜치 합친 60 파일)는 씬 때문에 "전체", `RaceAssembler` 로 `ChainRushFlowTests`·`ChainRushProfileTests`, Lap 4 트랙을 후보로 냈다. 영향 평가로 줄였다 — 씬 diff 는 `viewCamera` 참조 한 줄(시뮬 값 없음), `RaceAssembler` 는 카메라 필수 검사와 표현만(프로필 전달 그대로), 장식·look·머티리얼은 Lap 제외(§4-4). 작업 중 실행 재사용(CLAUDE.md §9-2): 위 EditMode 8건(09:45:05Z)·PlayMode 6건(09:45:25Z~09:45:32Z). 같은 코드 근거 — 그 뒤 `Assets/` 변경은 커밋에 들지 않은 고아 파일 삭제뿐, 커밋 뒤 `git status -- Assets` 비어 있음. FINGER 는 장식만이라 테스트 없이 침범 검사(위 항목). 사용자가 같은 에디터에서 플레이 중이라 체크아웃 없이 `commit-tree` 로 `--no-ff` 와 같은 병합 커밋을 만들었다.
- **버린 대안**: 커스텀 하늘 셰이더(프로젝트 첫 셰이더 — 내장 Panoramic + 구운 텍스처로 충분), 내장 Procedural(구름 없음), 구름을 장식 메시로(먼 클리핑 안에 둬야 하고 트랙마다 렌더러가 는다), 레이스 씬에 하늘 하나만(트랙별 낮·밤을 못 나눈다), `Camera.main` 으로 카메라 찾기(§0 런타임 조회 금지).

## 2026-11-02 · FINGER LANES 장식 디테일 (R4, `feature/m6-finger-detail`)

- **결정 (사용자, 2026-11-02)**: "그래 그럼 finger부터" → 요청서(길가 띠·코너 화살표판·배너 가로등·광고판·출발 게이트·마을과 땅) "ㄱㄱ". 렌더를 본 뒤 "좋은데 최외곽이 살짝 비어보인다" → 하늘은 다음 항목으로 따로("ㄱㄱ 추천대로, finger 먼저 커밋하고 진행").
- **레퍼런스**: 원작 기록 영상(`hWCUj3y0nzM`)의 1 s 접촉 시트 30컷 — 가드 뒤 황토 돌담과 꽃상자(튤립·주황·라벤더)·나무 격자 울타리·산울타리, 코너 바깥의 노랑·검정 화살표판 줄, 초록 가로등의 보라·초록 배너, 직선의 포스터 광고판, 체크무늬 출발 게이트, 주황 기와 마을, 원경 풍차.
- **결과** (`FingerLanesEnvironmentBuilder` 의 `Layout` 만, 트랙·도로·가드·차선·TrackLook 그대로):
  - 길가 띠: 길이 12 m 이상인 직선마다 양쪽, 가장자리 1.1~2.3 m 에 8 m 조각(Hash 로 사암 담장+꽃상자 45% / 울타리+라벤더 25% / 산울타리 30%). 높이는 노면 위 1.9~2.3 m — 첫 렌더에서 1.25 m 는 1.1 m 가드에 가려 줄로만 보였다. 조각 사이 0.6 m 틈의 네 번째마다 가로등(초록 기둥·랜턴 둘·바깥쪽 배너, 도로 위로 넘지 않음). 두 번째 통로 `near`(여유 0.5 m)로 자리를 잡는다 — 넓은 통로(1.5 m)로는 띠가 놓이지 않는다.
  - 코너(모두 45° 이상): 바깥 가장자리 +2.8 m 에 화살표판(약 13 m 간격, SKYWAY `CornerBoards` 형식), 안쪽 +2.2 m 에 둥근 꽃 화분(회전 없는 원기둥 — 그래도 경계 상자는 정사각이라 모서리가 0.37 m 더 나간다. 첫 재생성에서 +1.8 m 가 여유 0.63~0.69 m 로 막혔다).
  - 40 m 이상 직선의 띠 뒤(+5.2 m)에 눈 모양 포스터 광고판, 출발선 위 체크무늬 게이트(보·글자판은 노면 9.4 m 위부터, 기둥 배너; 옆의 기념비를 대신함).
  - 집: SKYWAY `TownHouse` 형식으로 줄였다(렌더러 약 40 → 20, 주황 기와 70%, 덧창·화분·차양/발코니를 Hash 로). 18 m 마다 양쪽, 앞뒤 9 m 가 직선인 곳에만 — 곡선 위에서 돌아간 집은 경계 상자가 도로 쪽으로 부푼다(첫 재생성 여유 0.43~0.67 m).
  - 땅: 손가락 가운데와 남쪽의 돌길(땅 높이, 도로 슬래브 밑), 꽃밭 정원, 원경 풍차 4개. 머티리얼 13개 추가(`M_Finger{Tile,Apricot,Rose,Sand,Wood,Soil,Tulip,Marigold,Lavender,Hedge,LampGreen,Violet,Chevron}`).
- **검증** (이 PC 의 에디터 6000.3.19f1 — 프로젝트 버전은 6000.3.25f1, UTC): 컴파일 성공, `Rebuild Finger Lanes Exterior` 원문 "Finger Lanes Exterior clearance: nearest renderer bounds 0.83 m outside a road edge (at least 0.7 m below 9 m over the road)" / "FingerLanesExterior: 2865 renderers; physics=0; runtime scripts=0; road corridor clear." (전 3179). 미리보기 렌더 전후 6장 + 게이트. 장식만 바뀌어 테스트는 돌리지 않았다 — 빌더·프리팹을 읽는 테스트가 없고 장식 씬을 부르는 FINGER 테스트는 모두 `Lap`(VERIFICATION §4-4).
- **찾은 것**: 미리보기 렌더는 파란 단색에 안개가 없었지만, 실제 레이스 씬(`ChainRushCircuit.unity`)은 단색 남색 배경, 같은 색 선형 안개 45 → 210 m, 먼 클리핑 350 m 다(런타임에서 트랙별로 바꾸는 코드 없음). 실제 화면은 렌더와 다르다 — 다음 항목(하늘)에서 다룬다.
- **확인 못 한 것**: 사용자 플레이(실제 조명·안개에서의 띠 높이·집 크기), 프레임 시간.
- **버린 대안**: SKYWAY 빌더에서 화살표판·집을 공통 도구로 빼기(SKYWAY 재생성·지문 대조가 따라와 이번 범위 밖), 곡선 위 집을 경계 상자에 맞춰 바깥으로 밀기(검사를 흉내 내는 코드가 `RoadCorridor` 밖에 생긴다).

## 2026-11-02 · 병합 때 작업 중 실행을 다시 쓴다

- **결정 (사용자, 2026-11-02)**: "서킷 구조가 바뀌었으면 테스트를 돌려야하잖아? 그 테스트에 대해서 속도별 랩 1회를 돌리면 될거같다. 또한 병합때도 테스트를 한다고 알고있는데 병합때 중복 테스트를 돌릴 필요는 없지 않을까?" → 제안 문구 "ㄱㄱ". 본문은 CLAUDE.md §9-2 병합 조건 아래 "작업 중 실행을 다시 쓴다" 한 곳.
- **이유**: 작업 중(①) 마지막 실행과 병합(②) 실행이 같은 코드면 같은 신호를 두 번 받는다. 랩 런은 한 칸에 수십 초라 그 비용이 크다.
- **조건**: 같은 코드라는 근거 둘(그 실행 시각 이후 `Assets/` 에서 바뀐 파일 없음, 커밋 뒤 `git status` 에 `Assets/` 변경 없음). 실행 뒤에 바뀐 것이 있으면 그 부분만 다시. 결과 XML 이 실행마다 덮어쓰이므로 값은 그때 DECISIONS 검증 줄에 남긴다. ③ 마일스톤 조건은 그대로.
- **버린 대안**: 병합 때 늘 다시 돌리기(지금까지 — 같은 코드의 중복). 결과 XML 을 실행마다 보관하는 도구 추가(검증 줄 기록으로 충분하고 새 도구가 든다).

## 2026-11-02 · 테스트: 랩 런 하나로(장식 포함·한 바퀴·맵 구조 변경 때만)

- **결정 (사용자, 2026-11-02)**: "장식 테스트도 봇완주에 포함시켜서 랩 돌때만 하자. 그리고 한바퀴만 돌아도 되지않겟나. 결과적으로 맵 구조가 바뀌어 테스트런을 돌려야 할때만 돌리자. 또한 서킷과 서킷+장식런을 따로 보지 말고. codex로 구현하라".
- **선택 규칙의 유일한 본문**: `docs/RULES/VERIFICATION.md` §4-4의 **Lap 선택**. 여기는 구현 이유·단언 이동·조사 근거만 기록한다. 마일스톤 메뉴 코드는 변경하지 않았다.
- **구현**: `ChainRushFlowTests.Race_BotLap_WithLobbyEnvironmentAndLook_CompletesOneLap(speed, title)` = STADIUM·SKYWAY LOOP·FINGER LANES·PALM COAST × Normal·Fast·VeryFast, 12칸. Fast STADIUM을 추가하고 나머지 기존 속도 칸을 모두 유지했다(속도 때문에 틈/코너를 못 지나는 회귀를 잃지 않기 위해). 테스트의 첫 `circuit.Laps.CompletedLaps == 1`에서 끝내며 에셋의 `lapCount`는 바꾸지 않았다. 일반 랩·FINGER 지름길 2런·명시 실행 진단 프로브 3런에 `[Category("Lap")]`.
- **실제 설정 원본**: `LobbyMenu.cs:24-37`의 TrackChoice, `:235-239`의 BuildSetup이 읽는 로비 씬 `tracks` 배열을 `LobbyLapSetup`이 `SerializedObject`로 읽는다(메뉴를 누르지 않는다). 트랙 에셋 동일성·제목의 유일성·장식 씬 이름·look 이름/비어 있지 않음·정의 유효성을 단언한다. `Lobby_TrackChoices_AreChosenWithTheirSavedSetup(title)`는 기존 SKYWAY 메뉴 검사를 네 트랙으로 확장하여 Confirm/Highlight/BuildSetup과 같은 원본의 네 값(Track/Title/EnvironmentScene/Look)을 비교한다. 장식은 로드하지 않는 메뉴 테스트다.
  - 러너는 기존 속도 행렬의 중립 테스트 프로필(모든 배율 1)을 유지한다. 기존 Fast 로비 기본 ALPHA도 `Data/RunnerProfile_Alpha.asset:16-19`의 속도·가속·게이지·무게가 모두 1이다. 기준 20 m/s 및 기존 시간 오차는 그대로다. 이 선택은 트랙/장식/look 원본과 별개이며 러너 프로필별 차이는 기존 ProfileTests가 본다.
- **같은 런의 조립·해제**: `LoadLapRace`는 빌드 씬 목록 검사 → 실제 이름으로 레이스 로드 → 조립 완료(20초)·countdown·HUD 제목·정의·속도 배율·`AssertLaysTheLook` → 장식 루트 1개·렌더러 있음·Collider/Rigidbody/MonoBehaviour 없음을 검사한다. `LeaveLapRace`는 로비 복귀와 그 장식의 언로드를 검사한다. PALM도 이제 같은 장식/look 단언을 받는다.
- **랩 검증과 결승 검증의 분리**: 기록·고스트 저장에는 실제 결승이 필요하므로 기존 `Race_FinishFromTheLobby_KeepsTheBestAndItsGhost_AndRunAgainRacesIt`의 STADIUM 3랩은 유지한다. 이 테스트는 주행 회귀용 Lap 카테고리에 넣지 않는다(기록 기능만 바꿨을 때도 선택해야 한다). 다만 이 런 역시 로비 원본·장식/look 조립을 사용한다. 중복 `ChainRushCircuitTests.Race_BotRunsThreeLaps_CountsThemAndTimesThemExactly`는 삭제하고 결승에만 의미 있는 단언은 이 기록 테스트에 합쳤다.
- **옛 → 새 단언 이동** (새 위치의 M = `Race_BotLap_WithLobbyEnvironmentAndLook_CompletesOneLap`, A = `LobbyLapSetup/LoadLapRace`, U = `LeaveLapRace`, F = 위 기록·고스트 결승 테스트):

| 옛 테스트 | 단언 하나하나 → 새 위치 |
|---|---|
| `Race_SkywayFromTheLobby_BotFinishesTwoLaps` 삭제 | TryValidate·트랙 동일성·장식 씬·look 존재/이름 → A; HUD 제목·서킷 정의·조립 완료 → A; 도로/벽/차선 머티리얼 → A의 기존 AssertLaysTheLook; 장식 로드·루트 1·렌더러 있음·Collider 없음·Rigidbody 없음·MonoBehaviour 없음 → A; 실패 없음·완료 랩 2 → M의 실패 없음·완료 랩 1; 랩 길이/20 ±20%·최대 옆 거리 < DeckHalfWidth−0.4 → M(Fast SKYWAY, RoadHalfWidth=6으로 동일); 로비 활성·장식 언로드 → U. |
| `Race_FingerLanesFromTheLobby_BotFinishesTwoLaps` 삭제 | 메뉴 Runner 페이지(잠금 아님) → Lobby_TrackChoices; setup 트랙·제목·환경·look 존재/이름 → A; HUD·정의·조립 완료·AssertLaysTheLook·환경 로드·루트/렌더러/Collider/Rigidbody/MonoBehaviour → A; 각 킥커 뒤 25 m 이내 lift >0.5 m·두 번째 lift ≥첫 번째×0.9 → M(Fast FINGER); 실패 없음·완료 랩 2 → M의 실패 없음·완료 랩 1; 랩 시간 ±20%·최대 옆 거리 < RoadHalfWidth−0.4 → M; 로비 활성·장식 언로드 → U. |
| `Race_PalmCoastFromTheLobby_BotFinishesTwoLaps` 삭제 | 메뉴 Runner 페이지 → Lobby_TrackChoices; setup 트랙·제목·HUD·서킷 정의·조립 완료 → A; BoostZoneCount=5 → M(PALM); 실패 없음·틱마다 리스폰 없음 → M(이제 모든 칸에서 리스폰 감지); 완료 랩 2·각 랩 시간 ±20% → M의 랩 1·시간 ±20%; 최대 옆 거리 < RoadHalfWidth−0.4 → M; 로비 활성 → U(장식 언로드도 추가). |
| `Race_OtherSpeedClasses_BotFinishesEveryTrack` 삭제 | 트랙 정의·SpeedScale.Of(speed)·조립 완료 → A; 실패 없음·완주 → M의 실패 없음·첫 랩 계수; 각 단계 LapLength/(20×배율) ±20% → M; Normal 가드 거리 < RoadHalfWidth−0.4 유지, VeryFast 가드 접촉 허용/거리 로그 유지 → M. |
| `Race_StadiumFromTheLobby_LaysItsLookAndLoadsItsExterior` 삭제 | setup 트랙·환경·look 존재/이름·조립 완료 → A; AssertLaysTheLook의 도로·벽·차선 → A; 환경 로드·루트 1·루트 이름 StadiumExterior·렌더러 있음·Collider/Rigidbody/MonoBehaviour 없음 → A. |
| `Loop_ByName_RaceAssemblesWithItsEnvironmentAndGoesBackToTheLobby` 삭제 | Title/Lobby/Race/Environment의 빌드 인덱스 → A(없으면 Ignore 대신 실패); 실제 이름 로드 → A; 조립 완료·환경 로드·countdown → A; 로비 활성·환경 언로드 → U. StartRun은 각 랩 런에서, 인위적 FailRun 경로는 기존 Pause/Return 및 실패 테스트에서 유지한다. |
| `Race_BotRunsThreeLaps_CountsThemAndTimesThemExactly` 삭제(CircuitTests) | 진행이 이전−1 m 이상·실패 없음·최대 옆 거리 <6−0.4 → M(STADIUM Fast); IsFinished·HasFinished·완료 3랩·각 시간 ±20%·TotalTicks=각 LapTicks 합·game.Tick=LapEndTick(3)·Elapsed 오차 0.001초·Health=3·3랩×3체크포인트 tick>0 → F. M에서도 첫 랩 체크포인트 tick>0·Health=3 확인. |
| `Race_FingerLanesShortcuts_ASwingingRunnerTakesBothEachLap` → `...TakesBothInOneLap` | 조립 완료·BranchCount=2·실패 없음·Missed=0 유지; HasFinished → 완료 랩 1; Taken=4(2지름길×2랩) → Taken=2(2지름길×1랩); 장식/look·언로드 A/U 추가. 랩 시간은 원래 로그만 있었으므로 한 랩 로그만 유지. |
| `Race_FingerLanesShortcuts_InnerKeyHeld_TheRopeAddsNoSpeed` 유지 | 원래부터 1랩. 실패 없음·완료 ≥1·Taken=2·swingTicks>0·연속 swing 표본 있음·로프 1 m 단축·top≤RunSpeed×1.2+0.05·틱 가속≤RunAcceleration×Ticks.Seconds+0.001 그대로. A/U·Missed=0 추가. |
| `Lobby_SkywayTrack_IsChosenWithItsEnvironment` → `Lobby_TrackChoices_AreChosenWithTheirSavedSetup` | Runner 페이지·트랙·제목·환경 일치 모두 유지, 네 트랙으로 매개변수화·look 원본 일치 추가. 장식 로드 없는 메뉴 검증을 없애지 않았다. |
| `ChainRushProbeTests.Probe_SwingHook_PositionJumpAtEachCorner`·`Probe_SwingHook_FromTheInsideLine`·`Probe_ShortcutSwing_SpeedWithTheInnerKeyHeld` | 기존 측정·hook/swings 및 plain run의 실패/놓친 hook 단언 그대로. 원래의 1랩 창 유지, A/U·완료 랩=1·Lap 카테고리 추가. [Explicit] 유지(게이트에 새로 넣지 않음). 마지막 프로브의 Fast/VeryFast×inner×booster 8런 유지. |

- **줄인 반복의 의미**: 일반 런의 2랩째(STADIUM 옛 속도 런은 3랩째까지) 시간/가드/킥커 반복은 같은 경로를 다시 도는 검사였다. 이제 첫 랩의 같은 수치 단언만 남긴다. 결승 계시 합·마지막 틱·여러 랩 체크포인트는 F에 모두 보존했고, 랩 경계 되감기는 기존 롤백 테스트가 그대로 본다. 지름길 4회 → 2회는 허용치를 느슨하게 한 것이 아니라 1바퀴당 2개라는 기준을 유지한 것이다.
- **남긴 별도 표현 테스트**: `Race_WithATrackLook_LaysTheRoadAndWallsWithItsMaterials`는 장식 로드 검사와 다르다. 합성 머티리얼 주입·M_Deck 잔존 없음·PALM 차선 메시 >30개를 검사하므로 유지했다. 구조 레이캐스트·도로 조각·Net*·1700틱 지문·짧은 롤백 창은 수정하지 않았다.
- **경로 분리의 참조 검색 근거** (PowerShell Select-String, rg 미설치; 낡은 index 헤더 HEAD 불일치로 코드 직접 검색; 아래 Runtime/은 Assets/_Project/Scripts/ 기준):
  - `Runtime/ChainRush/Flow/RaceAssembler.cs:97-99`: Track 정의·Look·SpeedScale/RunnerProfile 전달. `Race/CircuitRace.cs:205-212`: BuildCenterline → CircuitScenery → LayRoad → PaintMarkings.
  - `Race/CircuitScenery.cs:64` LayRoad, `:603` LayRange, `:668` LayShortcut이 도로/가드를 깐다. `Track/RoadPiece.cs:101-119`가 roadCollider/wallCollider.sharedMesh를 연결한다. `RunnerMotor.cs:320` controller.Move, `:502` world.SphereCast, `GrappleController.cs:116` world.Raycast가 그 충돌을 읽는다.
  - `ChainRushGame.cs:192-195` PrepareTick → 입력 소비 → player.Step. `RunnerMotor.cs:252-328` RunDrive·PoleSwing·Slingshot·GuardContact 참조. `Control/TickInput.cs:5-8`는 시뮬이 소비하는 입력이며 InputLatch/KeyboardMouseInputSource가 만든다. 따라서 이들의 주행 변경은 공유 코드 영향이다.
  - Look의 소비처 검색은 LobbyMenu → RaceSetup → RaceAssembler → CircuitRace.UseLook와 CircuitScenery.PaintMarkings뿐이며 RunnerMotor/GrappleController/Motion에는 TrackLook 참조가 없었다. PaintMarkings와 표현 헬퍼(`CircuitScenery.cs:282-592`)는 MeshFilter/Renderer만 만들고, Collider 추가는 하지 않는다. 환경 빌더·ExteriorKit/Assets/RoadCorridor는 Editor/ 아래이고 시뮬에서 참조하지 않는다. 장식/머티리얼 경로의 지문을 뺀 근거다(장식 런의 무충돌 단언도 A로 보존).
- **선택 도구 구현 한계**: 표의 전용 장식/look 경로는 Lap·지문을 제안하지 않는다. CircuitScenery는 diff의 삭제 전/후 hunk가 모두 PaintMarkings~Destroy 사이일 때만 표현 변경으로 판정한다. 공용 빌더·도로 파일은 경로만으로 개별 트랙/UV 변경까지 확정하지 못하므로 후보를 내며 §4-4의 영향 평가로 좁힌다. 표에서 범위 미확정이어도 이제 게이트 메뉴를 실행하라고 출력하지 않는다. 일반 선택의 Lap 제외와 대상 트랙 Lap 필터는 별도 호출이다.
- **예상 시간**: 일반 속도/로비 런 24랩 + CircuitTests 3랩 + 지름길 3랩 = 30랩 → 행렬 12 + 지름길 2 = 14랩. 별도 결승 3랩을 포함하면 33 → 17랩(약 48% 감소). Explicit 진단 10랩은 10랩 그대로다. SKYWAY는 6 → 3랩이며 사용자 실측 합계 약 97~99초의 주행분이 약 절반으로 줄어드는 산술 예상(추가 장식 로드/로비 비용은 미실측)이다. 실제 벽시계 시간이 늘지 않는지는 Claude 실측 필요.
- **버린 대안**: Fast만 유지(속도별 회귀를 잃음), 장식만 따로 로드(중복 지속), 에셋 lapCount=1(게임 규칙 변경), 테스트용 트랙/환경/look 상수 복제(로비 원본과 어긋날 수 있음), 결승 검증도 첫 랩에서 종료(기록·고스트 저장을 검증할 수 없음).
- **검증** (Claude, 에디터 6000.3.25f1, 커밋 전 작업 트리, UTC): 컴파일 성공. PlayMode `Race_BotLap_*` 12칸·`Race_FingerLanesShortcuts_*` 2·`Lobby_TrackChoices_*` 4·`Race_FinishFromTheLobby_KeepsTheBestAndItsGhost_AndRunAgainRacesIt` 1 `testcasecount="19" result="Passed" passed="19" failed="0"` (06:57:33Z~07:01:36Z, 243 s). 랩 칸은 칸당 6~23 s(SKYWAY Normal 22.6 · Fast 18.0 · VeryFast 14.9 s — 전에는 2랩 런 셋이 38.7·33.8·26.7 s). 첫 시도(06:37Z)는 사용자가 플레이 중일 때 시작해 러너가 바로 멈췄다(`This cannot be used during play mode`), 테스트는 돌지 않았다. 선택 도구 `tools/select-tests.ps1` 정상 출력(Lap 칸 별도 필터 `^Lap$`, 일반 선택 `!^Lap$`). 돌리지 않은 것: Explicit 진단 3건, `ChainRushCircuitTests` 남은 8건(삭제만 있고 남은 메서드는 그대로), 게이트 메뉴가 Explicit 을 빼는지(메뉴 코드는 바꾸지 않았다).
- **정적 확인**: 선택기 PowerShell AST 구문 오류 0. 장식/look/메뉴/HUD는 Lap 후보 제외, 트랙/도로/러너/입력은 포함하는 경로 fixture 12개 일치. SKYWAY 정규식은 행렬 12칸 중 SKYWAY 3칸에만 일치. PaintMarkings-only hunk와 LayRoad hunk의 판별 fixture 2개 일치. 수정 C# 3파일의 문자열/주석을 뺀 괄호 짝 일치(컴파일 검증 아님). 6파일 CRLF·선택기 UTF-8 BOM 유지, git diff --check 오류 없음. 기존 2026-11-02 맵 디자인 항목부터 끝까지의 SHA-256이 작업 전과 같아 기존 결정 항목은 바뀌지 않았다.
- **선택 도구 실행**: 요청한 `powershell.exe -NoProfile -File tools/select-tests.ps1`은 이 머신 실행 정책으로 `UnauthorizedAccess / running scripts is disabled on this system`. 저장소의 기존 실행 방식 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/select-tests.ps1`으로 재실행하여 exit 0. 현재 작업 트리(이번 변경 외 기존 맵 작업 포함) 출력 요약:
  ```text
  바뀐 파일: 133개 (dev...HEAD + 커밋하지 않은 변경 + 추적하지 않는 파일)
  EditMode: 전체 (Enemy 제외)
  Lap: STADIUM, SKYWAY LOOP, FINGER LANES, PALM COAST × Normal/Fast/VeryFast
       FINGER 지름길 2건 포함, Explicit 진단 별도
  categoryNames = new[] { "^Lap$" } 와 트랙 이름 정규식 출력
  PlayMode: 표에서 범위 확정 못 함. 영향 평가로 이름 선택
            (게이트 메뉴 실행 금지, 일반 선택은 !^Lap$; 위 Lap 칸은 별도).
  ```
  전 트랙 후보는 기존 CircuitSceneBuilder/CircuitRace/Track 공용 변경 때문이다. git의 사용자 ignore 접근 권한 경고와 packages-lock의 LF→CRLF 경고도 출력되었으며 이번 작업에서 그 파일들은 수정하지 않았다.

## 2026-11-02 · 맵 디자인 보강: 장식 빌더 공통 도구 · 네 트랙의 실제 도로(질감·차선) · STADIUM 육상 경기장 · SKYWAY 램프와 막다른 베이 (`feature/m6-exterior-kit`)

- **R4 SKYWAY 장식 디테일 (2026-11-02, Claude 구현)**: 사용자 "skyway 맵 장식 디테일 살려줘. 맵이 너무 휑하다" → 계획(땅·마을 / 도로 가 / 고가 장비 / 스카이라인) 승인 "네가 구현해보아라". 휑한 원인은 740 × 550 m 어두운 바닥 판 하나(`M_City`)와 지상 구간 옆에만 있던 상자 집이었다. 장식만 바꿨다(트랙·도로 메시·가드·차선 칠 그대로).
  - `SkywayEnvironmentBuilder` 의 `Layout` 을 다시 짰다: 밝은 포장 바닥 + 40 m 격자 잔디 블록, 고가 밑 지상 대로(아스팔트·차선·분홍 보도·주차 차량 — 평평한 것은 모두 땅 위 0.09 m 이하라 어느 도로 슬래브 밑으로도 통과), 회벽 집(기와/슬레이트 박공·덧창·화분·차양·발코니, PALM 의 town house 형식) 두 줄, 둥근 나무·사이프러스·덤불, 교각 노란 띠, 색 아파트 스카이라인(층마다 유리 띠·옥상 물탱크/간판).
  - 도로 가(레퍼런스 `UZWC4qtamfk` 접촉 시트): 체크무늬 출발 아치(SKYWAY 글자), 고가 긴 직선 위 파란 방향 표지 갠트리(출구 앞은 오른쪽 화살표 하나 더), 45° 이상 코너 바깥의 노랑·검정 화살표판, 지상 직선 가드 뒤 광고판, 36 m 간격 가로등(팔은 도로 위)과 배너. 이 셋은 가드 가까이 서므로 두 번째 `RoadCorridor`(여유 1.2 m)로 자리를 잡고, 도로 위로 넘어가는 부분(가로등 팔·갠트리 보·표지·아치 보)은 근처에서 가장 높은 도로보다 9.4 m 위에서 시작한다(침범 검사의 9 m 위 예외). 광고판은 직선에서만(곡선에서 돌린 판은 축 정렬 경계가 도로 쪽으로 부푼다).
  - 가드 너머 장식 고가 상판은 무늬 없는 아스팔트 색(`M_SkywayAsphalt`)으로 — 도로 질감이 96 m 상자의 0..1 UV 에 늘어나 보였다. 새 머티리얼 `M_Skyway*` 25개(`ExteriorAssets.Palette`).
  - **검증**: 컴파일 성공, `RebuildExterior` 의 침범 검사 통과, 렌더러 1151 → **6962**(PALM 5361 보다 많다 — 플레이에서 프레임이 떨어지면 줄인다, 확인 안 됨). 미리보기 렌더 7장(출발·헤어핀·코너·고가·고가 2·내리막·전경). 장식만 바뀌어 랩 런은 돌리지 않았다(§4-4 Lap 선택). 첫 렌더에서 아치 글자가 거울처럼 뒤집혀(`Lettering` 은 랜드마크 쪽에서 읽힌다) 글자 그룹을 X 로 뒤집었다.
  - **외곽 보강** (사용자 "좋은데 맵 외곽쪽이 부족해보인다. 특히 고가도로쪽"): 기존 고속도로를 가드 너머 양쪽 300 m 까지(96 m 에서) 잇고 차선 칠·양방향 차량·가로등·표지 갠트리·교각을 놓았다(교각·기둥은 낮은 도로를 피한다, `ClearOfLowRoad`). 고가 구간 옆에 고가보다 높은 중층 아파트 줄(고가에서는 낮은 집의 지붕만 보였다)과 고속도로를 향한 대형 기둥 광고판 3개. 바닥을 740 × 550 → 1500 × 1200 m 로 넓히고 남은 땅을 44 m 격자 아파트 블록으로 채웠다(트랙에서 멀수록 높게, 15% 는 공원, 창 띠 2개짜리 단순 블록).
    - 렌더러 6962 → **10661**(외곽 블록 3756). 플레이 프레임은 확인하지 않았다 — 떨어지면 외곽 블록을 재질별 한 메시로 합치는 것이 다음 수(침범 검사는 정점 단위 그룹으로).
  - **메시 합치기** (사용자 "메시로 합치고 병합하자"): 땅(바닥·잔디·고가 밑 대로·주차 차량)과 외곽 블록을 재질별 메시 하나로 합쳐 `Art/Models/SkywayExterior_{Land,Outer}_<재질>.asset` 로 저장(`MergeByMaterial`, `ExteriorAssets.SaveMesh` 로 GUID 유지). 렌더러 10661 → **4680**, 프리팹 29.8 → 12.9 MB(합친 메시 에셋 20.7 MB, 바퀴 원기둥이 3.5 MB). 합친 메시는 도로를 둘러싸 경계 검사가 늘 걸리므로 정점 단위로 본다 — `RoadCorridor.ValidateClearance` 에 정점 검사 그룹 여러 개를 받는 오버로드(기존 한 그룹 시그니처는 그대로 넘김). 합친 뒤 렌더 3장이 전과 같았다.
- **병합 (② `feature/m6-exterior-kit` → `dev`, 2026-11-02, 사용자 "병합시키자", "메시로 합치고 병합하자")**:
  - 영향 평가: `tools/select-tests.ps1 -Range dev...HEAD` 는 "표에서 범위 확정 못 함 → 영향 평가". 도로 메시(`RoadMeshBuilder`·`RoadPiece`·`RoadProfile`)·도로 깔기(`CircuitScenery`)·정의(`TrackDefinition` 베이)를 읽는 클래스를 골랐다. `Net*`·`RaceOrder`·`RaceStandings*` 는 뺐다(바뀐 것은 베이뿐, 스냅샷·랩 길이 그대로), `World`·`ControllerCost` 도(CircuitRace 는 look 인자만), Flow 의 로비·설정 화면도(레이스 조립은 랩 런 12칸이 덮는다).
  - 재사용(CLAUDE.md §9-2, 06:02Z 뒤 런타임 코드 그대로): SKYWAY·지문·도로 조각 16건(06:02:37Z, 롤백 1건은 고친 뒤 06:08:24Z), 랩 런·지름길·로비·결승 19건(06:57:33Z~07:01:36Z).
  - 새로 돌린 것: EditMode `!Enemy` `testcasecount="333" result="Passed" passed="333" failed="0"` (07:47:02Z~07:47:07Z). PlayMode Curve·Slope·Steering·Drift·CornerSwing·Rollback·Circuit·Presentation `testcasecount="53" result="Failed(Child)" passed="52" failed="1"` (07:47:29Z~07:51:06Z) — 실패 `ChainRushCircuitTests.Scene_CornerPoles_StandOnEveryArcCenter_AndTheOneInReachIsOffered` "On the start line the last arc's pole is still in reach. S 0.00, D 0.00, H 0.94 … Expected: True But was: False". **원인: dev 에 있던 낡은 단언** — 2026-10-30 "봉 걸기"(f93cdaf)가 "출구 뒤는 없다"로 바꿨는데 그때 이 테스트를 고르지 않았다. 봉 후보 코드(`Centerline.TryFindPole`·`RunnerMotor`·`GrappleController`)는 이 브랜치에서 바뀌지 않았다. 사용자 "이 브랜치에서 고치고 병합" → 출발선에서는 후보 없음으로 단언을 바꿨다(**사양 변경**, 주석에 근거) → `testcasecount="1" result="Passed"` (07:54:02Z).
  - 리뷰: `unity-reviewer`(172,276 토큰) 치명·중대 없음, 보통 1(`TrackLookTests` 의 `ignoreFailingMessages` → 기존 `GameSettingsTests` 형식의 `LogAssert.Expect` 로 고침), 제안 2(`TrackLook.LaneLines` 차선 폭 1 m 미만이면 던지게 고침; `ExteriorKit.Flat` 의 0 벡터 보정은 dev 에서 옮긴 코드라 둠). 고친 뒤 `TrackLookTests` 6/6 (07:52:14Z), `Race_WithATrackLook_*` 1/1 (07:52:34Z). **Codex 리뷰 못 함**: "You’ve hit your usage limit … try again at 6:01 PM." — 사용자가 기다리지 않고 병합을 골랐다.
  - 사용자 플레이 확인: SKYWAY 장식 "좋은데 맵 외곽쪽이 부족해보인다" → 보강 뒤 "좋다. 병합시키자". 렌더러 4680 에서의 프레임은 확인하지 않았다.
- **SKYWAY 고가 합류·출구 수정 (2026-11-02, Codex 구현 · Claude 검증)**:
  - **결정·이유**: 사용자 "오르막길을 통해 고가도로에 합류하는거지 고가도로가 만들어지는 것이 아니다", "고가는 출구 너머 서쪽으로 계속". 기존 `Bay` 에 `straightDeck`·`extension` 을 추가했다. 평지 직선 하나를 사이에 둔 오른쪽 R60/R65 90° 램프에만 허용하는 독립 직선 상판이다. 지상 베이의 생성·검증 경로는 그대로다. 별도 트랙 시스템·새 파일은 만들지 않았다.
  - **값**: 레이스 도로·두 램프 반폭 6 m, 독립 고가는 왼쪽 확장 18 m를 합쳐 30 m. 레이스 중심 기준 가장자리 −24 / +6 m, 고가 중심 −9 m. `Bay.Highway(460.39378, 790.39378, 18, 50)` (S는 에셋에서 float): 직선 주행 330 m, 합류 뒤 동쪽·출구 너머 서쪽에 각각 50 m, 충돌 상판 총 430 m. 양끝 전폭 횡단 가드, 가운데 주행 구간에는 내부 가드 없음. 가드 너머 장식 상판은 양쪽 각 96 m, 충돌체 없이 교각으로 받친다.
  - **접속**: 직선 상판은 레이스가 굽어도 직진한다. 램프 왼쪽 노면을 고가 오른쪽 경계에서 잘라 겹친 상판을 없애고 그 구간의 왼쪽 가드는 만들지 않는다. 고가 오른쪽 가드는 램프가 만나는 곳만 열린다. R60/R65, 반폭 6의 노면 접속 길이는 램프 축 `r * acos((r - 6)/(r + 6))`, 직선 축 `sqrt(4*r*6)` (약 37.95 / 39.50 m). 실제 가드 틈에는 두 도로의 가드 두께 0.3 m를 더해 램프 축 약 37.14 / 38.75 m, 직선 축 약 38.88 / 40.47 m를 연다. 가드 끝이 상대 노면에 튀어나오지 않게 하기 위해서다. 램프 끝의 0폭 삼각 꼭짓점은 양수 폭 계약과 월드 좌표 정밀도를 위해 1 mm 슬리버로 만든다.
  - **차선·봉**: `TrackLook.LaneLines(15)` 의 3.6 m 격자를 고가 중심 −9 m 기준으로 옮기고 노란 이중선도 그곳에 둔다. 램프는 반폭 6의 기존 격자, 양 접속부는 노란 삼각 빗금. 봉 7개는 S `480.39378 + 40*i`, reach 10, **고가 중심에서** 왼쪽부터 ±11 m (레이스 중심 기준 −20 / +2 m). 봉 검증은 이 독립 상판의 전체 폭도 인정한다.
  - **조각·길이**: 중심선·높이·방향은 유지, 폭 테이퍼만 제거. 총 32조각, 한 바퀴 **1599.37 m** (Unity 실측 1599.4 m, 전과 같다). 조각 7·8·9·10·11·12·13·14 의 `EndHalfWidth` 는 모두 기본값 0(반폭 6)으로 바뀐다. 0 직선20.5 / 1 R28 +180° / 2 직선56 / 3 R26 −90° / 4 직선50 / 5 직선15(+0.15) / 6 R26 +90°(+0.15) / 7 직선24.22(+0.15) / 8 직선15(0) / 9 직선15.78 / 10 R60 +90° / 11 직선330 / 12 R65 +90° / 13 직선15(−0.1) / 14 직선15(−0.1) / 15 직선15(0) / 16 직선10.56 / 17 R26 −90° / 18 R30 +120° / 19 R30 +105° / 20 R28 −135° / 21 직선62.5 / 22 R22 +90° / 23 직선7.5 / 24 R30 +45° / 25 R30 −45° / 26 직선83.06 / 27 R22 +90° / 28 직선15(−0.2) / 29 직선30(−0.2) / 30 직선15(0) / 31 R31 −180°. 괄호는 끝 경사, 생략은 0. 지상 베이는 `FromS=205.80530, ToS=255.30530, Width=10.8, Taper=22, Left=true` 그대로다.
  - **버린 대안**: 대칭 폭 6→15→6 (레이스 도로가 고가가 되는 기존 문제). 기존 베이를 곡선 따라 연장 (출구에서 고가까지 북쪽으로 꺾인다). 독립 고가를 장식으로만 (공유 차선·횡단 가드의 충돌을 보장하지 못한다). 일반 베이의 taper=0을 무조건 허용 (접속 근거 없는 수직 확장). 램프 방향·전체 중심선 재배치 (기존 두 곡선이 이미 북쪽으로 합류·분기하며 불필요한 랩 변경).
  - **영향·테스트**: 새 모드는 SKYWAY 에만 설정한다. 베이 없는 트랙의 노면·가드 생성식과 FINGER 오른쪽 지름길 틈은 유지했다. `StartGuard` 는 기본 false라 기존 프로필은 기존 메시 순서 그대로다. EditMode `TrackDefinitionTests` 에 독립 축·전체 폭 봉·잘못된 길이/범위/겹침/폭/램프 방향·접속 공식, `RoadMeshBuilderTests` 에 시작 가드와 조명 검사 추가. 기존 SKYWAY 모양 테스트는 바뀐 사양(30 m 독립 상판, 12 m 램프, 고가 중심 기준 봉)에 맞게 주석과 단언을 수정. PlayMode `Race_SkywayHighway_ClosesBothEndsAndJoinsRampsWithoutInteriorGuards`(전폭 양끝 가드·공유 노면·중앙선·빗금·접속 연속성), `Race_SkywayHighway_RollbackAcrossJoinAndExitRepeatsEveryTick` 추가. 2랩 봇의 최대 옆 거리 단언은 바꾸지 않았다.
  - **검증** (Claude, 에디터 6000.3.25f1, 커밋 전 작업 트리, UTC): 컴파일 성공 → `CreateSkywayTrack`(`TryValidate` 통과, 조각 32·베이 2·봉 7) → `RebuildExterior`(침범 검사 통과, 렌더러 1113 → 1151, 콘솔 오류 0).
    - EditMode `TrackDefinitionTests`·`RoadMeshBuilderTests`·`TrackLookTests` `testcasecount="77" result="Passed" passed="77" failed="0"` (06:02:03Z).
    - PlayMode 16건 `testcasecount="16" result="Failed(Child)" passed="15" failed="1"` (06:02:37Z~06:06:38Z). 실패: `Race_SkywayHighway_RollbackAcrossJoinAndExitRepeatsEveryTick` — `Unhandled log message: '[Error] RaceAssembler: tick 1271 came after 1545; this run will keep no ghost.'`. 되감기에 고스트 기록기가 내는 정해진 에러(`RaceAssembler.RecordTick`)를 새 테스트가 받지 않았다. 기존 되감기 테스트(`ChainRushFlowTests` 의 부스트존 되감기)와 같게 `LogAssert.Expect` 한 줄을 넣었다(시험하는 틱별 비교는 그대로). 다시 돌려 `testcasecount="1" result="Passed" passed="1"` (06:08:24Z).
    - 나머지 15건 통과: SKYWAY 모양·지상 베이·고가 양끝 가드/접속·로비 2랩(91.04 / 89.84 s, 최대 옆 4.97 m — 전과 같다: 봇이 따르는 중심선은 그대로)·속도 단계 Normal(최대 옆 4.37 m)/VeryFast(5.61 m), 서킷 지문 `0x18F31E7A2B4F79B3` 그대로, FINGER 지름길 2·로비, 도로 조각 4, TrackLook 1. 다른 트랙의 메시 바이트 비교는 하지 않았고 지문·FINGER 테스트로만 봤다.
    - 미리보기 렌더(합류·출구 위에서, 램프에서, 고가 위에서 뒤쪽 가드): 8차로 고가에 가운데 노란 이중선, 오른쪽 램프 합류·분기 삼각부 빗금, 양끝 가로 가드와 그 너머 장식 상판. 장식 상판은 `ExteriorKit.Box` 의 0..1 UV 라 아스팔트 질감이 늘어나 보인다(남은 것).
- **발광 키워드 (2026-11-02)**: URP 는 머티리얼을 다시 검사할 때 `_EMISSION` 을 `globalIlluminationFlags` 로 다시 정한다. 그래서 `ExteriorAssets.Palette` 가 발광이 있으면 `RealtimeEmissive`, 없으면 `EmissiveIsBlack` 을 함께 설정한다. 처음에는 이 설정이 빠져 PALM 바다·유리가 재가져오기 뒤 빛을 잃었다. 강제 재가져오기 뒤에도 빛나는 머티리얼 13개가 모두 키워드를 유지하는 것을 확인했다.

- **결정 (사용자, 2026-11-02)**: "맵 디자인 보강 … 해변 서킷처럼 디테일과 서킷 도로와 벽에 대한 디자인이 있어야한다" → 요청서(도로·벽 먼저, 디테일은 한 트랙씩, 원작 영상) 추천대로. STADIUM 빌더를 묻자 "확장성과 재사용성을 생각해서 … 구조개선이 필요하다면 바꿔야" → 공통 도구 분리안 "추천대로 하겠다". 플레이 뒤 피드백 4가지(STADIUM 낙사 구간 식별·명분, 계주용 레인과 미식축구장, SKYWAY "실제 아스팔트 도로" 와 오른쪽 램프·차선, FINGER·PALM 도 아스팔트) → 추천(물웅덩이 + 장대 가로대, 미식축구장, 양방향 고속도로 고가)과 램프 그림 "비슷한데 … 정면은 가드로 막혀있어야", "정면 가드는 지상도로에서 수직으로 일직선", P1~P4 "ㄱㄱ", P3 설계 "ㄱㄱ".
- **R1 공통 도구** (`Editor/ChainRush/`): `ExteriorAssets`(머티리얼 `Palette`·질감 `Surface`·`Grains`·TrackLook·메시 저장·장식 검사·미리보기 씬에서 프리팹 저장과 GUID 지키기), `RoadCorridor`(본선·지름길·베이 샘플, `Clear`/`Available`/`Reserve`, 침범 검사), `ExteriorKit`(도형·`Hash`/`Pick`, static class — 요청서의 sealed class 에서 바꿨다: 상태가 메시 캐시뿐이고 `using static` 으로 빌더의 호출을 그대로 둔다). 세 빌더의 사본을 지웠고, 리팩토링 뒤 다시 지은 세 프리팹의 렌더러(경로·메시·머티리얼·그림자·변환) 지문이 전과 같았다(SKYWAY 1046 · FINGER 3179 · PALM 5361, 다른 줄 0).
  - **SKYWAY 에 침범 검사를 처음 켰더니** 고가 교각이 걸렸다 — 비탈진 대각선 구간에서 캡의 축 정렬 경계가 낮은 도로 점에 닿은 오판(회전한 바닥면으로 재면 교각 꼭대기와 슬래브 밑의 최소 간격 0.000 m). 사용자 "1번 추천대로": 도로 밑 구조물 그룹은 경계 대신 **회전한 바닥면 위 슬래브 밑** 검사(`ValidateUnderRoad`).
  - FINGER 머티리얼의 발광 0.22 는 저장된 키워드가 없어(`m_ValidKeywords: []`) 원래 꺼져 있었다 — 처음 요청서에서 "형광이 남아 있다"고 한 것은 틀렸다. 통합 `Palette` 는 발광 0 으로 같은 화면.
- **R2·P1 실제 도로** (런타임은 표현만, 시뮬 불변):
  - `RoadPiece` 메시에 월드 m 단위 UV(면마다 주축 투영). 충돌은 정점·삼각형만 써서 지문 그대로.
  - 질감: 빌더가 시드 고정으로 만드는 `Art/Textures/T_{Asphalt,Concrete,Rubber}Grain(_Normal).png`(512², 이음매 없는 값 노이즈 + 골재·기공 점, 밝기 맵은 linear, 바이트가 바뀔 때만 씀). 트랙 색은 머티리얼의 색(`GrainMean` 0.82 로 나눔).
  - `TrackLook`: `accent`(노란 페인트)·`laneWidth`·`centreFromHalfWidth`·`doubleCentre`·`solidLanes`, `LaneLines` — 차선은 중심에서 고정 간격이라 **좁아지면 바깥 선이 가장자리에 먹혀 끝나고(차로 감소), 넓어지면 자리가 날 때 생긴다**. 바깥 차로가 반 차로 미만이면 선을 그리지 않는다.
  - `CircuitScenery.PaintMarkings(TrackLook)`: 가장자리 실선, 차선(점선 3 m/5 m 또는 실선), 중앙선(이중 노란 실선 또는 흰 점선), 틈 앞뒤 **이륙판**(1.2 m, 0.8 m 노란 줄), 코너 화살표, 베이(아래).
  - **고친 버그**: 차선 사각형이 같은 정점에 양쪽 감김 삼각형을 두어 `RecalculateNormals` 가 법선을 0 으로 만들고 차선이 검게 그려졌다(PALM 은 약한 발광으로 가려졌던 것으로 본다). 위를 향하는 감김 하나만(`Quad`).
  - 트랙별(원작 기록 영상을 scratchpad 접촉 시트로 읽음, SKYWAY `UZWC4qtamfk`·FINGER `hWCUj3y0nzM`): SKYWAY 회색 아스팔트·콘크리트 방호벽·3.6 m 차선·고가(반폭 15)만 노란 이중 중앙선(사용자 "양방향 고속도로"), FINGER 슬레이트 아스팔트·파란 벽·노란 이중 중앙선, PALM 따뜻한 아스팔트·라벤더 벽·흰 점선 중앙선 4차선(노란 이륙판), STADIUM 붉은 탄성 트랙·1.22 m 흰 실선 레인. 로비 세 항목의 look 은 `Unity_RunCommand` 로 넣고 사용자가 저장.
- **R3·P2 STADIUM**: `StadiumEnvironmentBuilder` · `Prefabs/StadiumExterior.prefab`. 장식 씬은 손으로 놓은 것이었고(활성인 것은 아트 패스가 구운 메시 11개 `Art/Models/StadiumSports_*`, 나머지 58개는 비활성 루트) 생성 코드가 리포에 없어, 구운 메시를 그대로 참조하고 사용자가 장식 씬의 세 루트를 프리팹 하나로 바꿔 저장했다.
  - 안쪽 타원 육상 트랙·작은 축구장을 걷고 **미식축구장**(120 × 53⅓ 야드: 5야드 줄무늬·엔드존 남색/민트·야드 선·해시·골포스트): `Clay`·`TurfLight` 를 빼고 `White`·`Ink`·`Amber` 에서 안쪽 삼각형만 뺀 파생 메시와 안쪽 잔디 메시를 빌더가 저장(`StadiumExterior_*.asset`).
  - **낙사 구간 → 육상 종목** (사용자 "단순히 낙사구간이라기 보단 스타디움에서 어떠한 구간"): 6 m 틈 = 장애물 경주 물웅덩이(청록 물·옆의 흑백 장애물), 16 m 그래플 틈 = 물웅덩이 + **장대높이뛰기 가로대**(앵커 높이에서 도로를 가로지름, 9 m 위라 침범 검사 예외). 틈 앞뒤 노란 이륙판(위). 트랙 모양 그대로.
  - 구운 메시는 서킷을 감싸 경계 검사가 늘 걸려 **정점 단위 검사**(`ValidateByVertex`, 노면 위 0.05 m ~ 9 m 사이, 가장자리 0.7 m 안의 정점을 잡는다; 도로 위 상자로 잡히는 것을 확인; 꼭짓점이 모두 밖이고 면만 도로를 지나는 경우는 못 잡는다).
- **P3 SKYWAY 램프 — 시뮬 변경**: 넓은 지상 도로에서 오른쪽 차선이 그대로 오르막(정식 루트), 왼쪽 차선은 진행 방향에 **수직인 가드**로 막힌다.
  - 새 기능 **베이** `TrackDefinition.Bay`(시작·끝 S, 폭, 벌어지는 길이, 쪽): 본선 옆에 붙는 막다른 확장부. 본선 쪽 가드 없음(본선의 그 쪽 가드도 그 구간에서 연다 — 지름길이 오른쪽 가드를 여는 것을 양쪽으로 일반화, `CircuitScenery.LayRange`), 바깥 가드, 끝에 수직 가드(보이는 벽 + 4 m 충돌체, `RoadProfile.EndGuard`). `RoadProfile` 에 가로 오프셋(`Offset`·`EndOffset`). 검증: 한 폭의 평지 직선, 틈·같은 쪽 다른 베이·지름길 교차와 겹치지 않음, 바깥 가장자리는 한쪽에 `2 × MaxWidthChange` 이하로 벌어짐.
  - `Circuit_Skyway`(빌더 `CreateSkywayTrack` 로 제자리 덮어씀, GUID 유지): 왼쪽 커브 뒤 평지 진입 직선 **50 m** 를 넣고 왼쪽에 베이(폭 10.8 m = 3차선, 벌어짐 22 m, S 205.81..255.31), 고리가 닫히게 고가 직선 280 → **330 m**(조각 10 → 11), 직선 봉 7개는 +50 m 로 같은 상대 위치. 한 바퀴 1499.37 → **1599.37 m**, 조각 31 → 32.
  - 차선: 베이 위로 차선 격자가 이어지고(본선 왼쪽 가장자리 선 대신 베이 바깥 선), 끝 15 m 는 차선을 멈추고 노란 사선 빗금 + 본선 가장자리 흰 실선. 장식: 가드 뒤에 노랑·검정 화살표 표지판(침범 검사가 베이 끝을 반폭 원으로 보므로 반폭 + 0.8 m 뒤).
  - **테스트를 고친 것 (사양 변경)**: `Skyway_ElevatedStretch_IsWideWithSevenAlternatingSwingPoles` — 고가 직선이 조각 11, 330 m. 새로: `Race_SkywayBay_IsOneRoadWithTheLanesAndEndsAgainstAGuard`(베이는 노면 높이의 도로, 본선과 사이에 가드 없음, 곧게 달리면 끝의 수직 가드, 본선은 이어짐), `Race_StadiumFromTheLobby_LaysItsLookAndLoadsItsExterior`, EditMode `TrackLookTests`(6), `TrackDefinitionTests` 베이 2건, `RoadMeshBuilderTests` 오프셋·끝 가드 1건. SKYWAY·FINGER 로비 레이스 테스트에 look 확인(`AssertLaysTheLook`).
- **검증** (에디터 6000.3.25f1, 커밋 전 작업 트리, 바뀐 부분만, UTC):
  - EditMode `TrackDefinitionTests`·`RoadMeshBuilderTests`·`TrackLookTests` `testcasecount="66" result="Passed" passed="66" failed="0"` (05:32:54Z~05:32:55Z).
  - PlayMode 14건 `testcasecount="14" result="Passed" passed="14" failed="0"` (05:33:11Z~05:37:03Z): 도로 조각 4, 서킷 지문 1(`0x18F31E7A2B4F79B3` 그대로), SKYWAY 모양·베이·로비 2랩(91.04 / 89.84 s, 최대 옆 4.97 m)·속도 단계 보통/매우빠름, FINGER 지름길 2·로비 2랩(가드 틈 일반화), TrackLook 1.
  - 앞선 실행: PlayMode 10건(P1, 05:14:02Z~05:16:46Z, 지문 그대로), STADIUM 로비 1건(05:21:24Z, P2 뒤), EditMode `TrackLookTests` 6건(05:10:37Z).
  - 장식 빌더 네 개 모두 침범 검사 통과(SKYWAY 교각 108개 슬래브 밑, STADIUM 정점 약 20만 개).
- **남은 것**: 사용자 플레이 확인(네 트랙의 도로·차선, STADIUM 경기장·물웅덩이·가로대, SKYWAY 램프와 정면 가드). R4 디테일(SKYWAY → FINGER → STADIUM). 게이트 전체는 마일스톤(③)에서 — 다른 트랙 영향은 지문·네 트랙 로비 레이스로만 봤다. 레인 번호(출발선 숫자)는 하지 않았다. 커밋은 지시 때.
- **버린 대안**: STADIUM 은 추가 프리팹만(구운 메시가 검사 밖에 남는다). 수직 가드를 대각 테이퍼로(사용자 "수직으로 일직선"). 넓은 도로를 장식 도로로 계속 잇기(사용자 "정면은 가드로 막혀"). 교각 그룹만 검사에서 빼기(뚫어도 못 잡는다). 차선 수를 폭으로 나눠 고르게(폭이 바뀌면 선이 모두 움직인다).

## 미결 (합의 대기)

| 항목 | 선택지 | 상태 |
|---|---|---|
| ~~`Assets/_Project/` 루트 채택~~ | 2026-10-01 확정 (위 결정 참조) | 해결 |
| ~~asmdef 분할~~ | 2026-10-01 현 3개 구성 확정 (위 결정 참조) | 해결 |
| ~~접두사 규칙~~ | 2026-10-01 `CLAUDE.md` §1-3 확정: 머티리얼 `M_`, 텍스처 `T_..._BaseColor` 만. `Art/Materials/` 15개가 이미 준수 | 해결 |
| ~~`Assets/Editor/HubForceResolve.cs` 삭제~~ | 2026-10-01 의도된 삭제로 판단: Unity Hub 가 주입하는 자기삭제 헬퍼(파일 내 `k_ScriptPath`). 삭제는 커밋 `75d3454`(DosangE)에서 수행, 참조 없음 | 해결 |
| ~~`dev/` (git-lfs 훅 잔재) 제거~~ | 2026-10-01 제거 (git 추적 훅 4개, 참조 없음 확인 후 `git rm`) | 해결 |
| ~~`.gitignore` 의 `/index/` 줄~~ | 2026-10-01 유지 확정 (생성물 미커밋 결정과 일치). "다 승인" 지시를 유지로 해석했으므로 뒤집으려면 알려 달라 | 해결 |
