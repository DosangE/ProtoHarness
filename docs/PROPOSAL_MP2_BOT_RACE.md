# MP2 봇전 구현 요청서 (§3-2)

> 2026-11-13 작성. 브랜치 `feature/mp2-bot-race`(`dev` `f6be824` 에서 분기). **이 문서는 승인 전 요청서다.**
> 사실 지도: Codex 읽기 전용 탐색(같은 날, 스레드 `01a1d339`) — 아래 근거 `경로:줄` 중 핵심(`ChainRushGame.cs:207-226`, `RaceField.cs:70-92`, `RaceMode.cs`, `ChainRushReplayTests.cs:149`)은 메인이 대조했다.
> 결정 원본: `docs/PROPOSAL_M5_AI_OPPONENTS.md` "답:" 칸, `docs/DECISIONS.md` 2026-11-12 "MP1"·"MP1 구조"·"방향 재설정".
> 접두사 `R/` = `Assets/_Project/Scripts/Runtime/ChainRush/`, `T/` = `Assets/_Project/Scripts/Tests/PlayMode/`, `N/` = `Assets/_Project/Scripts/Runtime/Net/`.

## 0. 이번에 사용자가 정한 것 (2026-11-13)

| 항목 | 답 (원문 요지) | 요청서에 반영 |
|---|---|---|
| D9 봇 구성 | "시드로 무작위" | 플레이어가 고르지 않은 4종에서 3명을 시드로 뽑는다. 시드는 `RaceSetup` 에 들고 로그에 남긴다. 재출발은 같은 구성 |
| D10 로비 | "메인에 '봇전' 줄 추가", RaceMode 는 "추가해야한다. 싱글플레이 중 혼자하는건 기록용, 봇은 말그대로 시나리오 모드나 멀티 대신 하는 용이다." | 메인 화면에 새 줄, `RaceMode.Bots` 를 새로 둔다. Single = 혼자·기록용, Bots = 봇과 겨루기(기록·고스트 없음) |
| D7 끝 규칙 | "a" | 플레이어가 들어오면 끝, 남은 봇은 진행 거리 순 |
| 주행 중 순위 | "플레이 중에도 우측 하단에 1st 2nd 3rd 4th 같은거 보이게 해줘" | 주행 중 순위는 화면 오른쪽 아래에 서수(1st·2nd·3rd·4th)로. A 의 "순위 n / N" 을 대신한다 |
| 스윙 중 접촉 (6-4 ③) | "봉 스윙 중에 부딪히면 끊겨야한다. ㅇㅇ" | B: 봉 스윙 중 부딪히면 스윙이 끊긴다. 6-4 의 나머지(①②④⑤)는 사용자가 따로 답하지 않았다 — 게임 규칙이 아닌 처리 방식이라 제안대로 구현하고 B 보고에 결과를 적는다 |
| A 플레이 확인 | 1 "머리위 이름은 빼자" 2 "순위를 왼쪽 하단에 … 1등은 금색으로 반짝이는 것, 2등은 은색으로 1등보다 덜 반짝이는 것, 3등은 동색으로 2등보다 덜 반짝이는 것, 4등부턴 반짝이지 않는 흰색 텍스트로" 3 "추월 가능했고 그 떄마다 순위도 바뀌었다" 4 "나왔다" 6 "느낌이 있긴하나 아직 난이도 디테일이 없으니 나중에 판단해야할것" 7 "싱글 레이스 안에 타임어택, 봇전 으로 구분하는 곳을 따로 넣자" 8 "아직 봇의 물리값이 없는데 정상인가?"(→ B 에서) 9 "출발선에서 가로로 쭉 있어야한다. 출발 좌표가 같으면 안된다". 고친 뒤 "다 잘 나왔다" | 1·2·7 반영(머리 위 이름 제거, 왼쪽 아래 금·은·동 반짝임, `Flow/Lobby/SingleModePage` — 메인 '봇전' 줄은 없앴다). 9 는 B 의 격자를 "가로 한 줄" 로 바꾼다 |
| B 출발 자리 | "a로. 대신 맵마다 트랙 크기가 다를 것이니 유의 할 것. 트랙 제작 요청서에 기본적으로 출발지점에 최소 너비를 기록해놔" | 출발선에 가로 한 줄(4명, 8인이면 2줄), 자리는 시드로 섞는다(플레이어 포함). 간격은 그 트랙의 실제 반폭으로 계산(최대 2.4 m, 1.2 m 미만이면 예외). 최소 너비 규칙은 `docs/COURSE.md` §7-2a |
| 승인 | "승인" (2026-11-13, 이 요청서 전체 — A·B 두 단계, Codex 구현 + Unity MCP) | 6절 계획 반박이 계획을 바꾸면 바뀐 부분만 다시 묻는다(§3-3) |
| Codex 범위 | "구현 + Unity MCP 둘 다" | 구현은 `tools/codex-unity.ps1 -PromptFile <지시서> -Write`. 지시서에 `Unity MCP: 이 실행에서 허용` |
| D3b 봇 실력 | "일단은 시험 봇과 유사하게. 시험 봇이 쉬움 기준이 될 것이고 추후에 쉬움 포함 난이도 3개 정도로 구상" | 게임용 봇 = 지금의 `CircuitBot` 그대로(= 쉬움). **자동 개선 루프는 이번에 하지 않는다** — 난이도를 늘릴 때로 HANDOFF 에 옮긴다 |

| 봇 이름 | "머리 위에 이름 좋다" | 봇 머리 위에 러너 이름(IMGUI, `ChainRushHud` 형식) |
| D2b 감속 | "부딪힘으로 감속이 아예 없진 않아야할거같은데 서로의 방향대로 밀리는 그런 물리 값 같은 느낌으로" | 위치 밀어내기 + **접촉 법선 방향 속도를 무게(질량)로 주고받는 충돌 임펄스**. 뒤에서 박은 쪽은 느려지고 앞쪽은 밀려 나간다, 옆으로 닿으면 서로 옆으로 튕긴다. 반발 계수 하나(시작 0.3, 플레이로 조정)를 상수로 둔다. 접선 방향 속도는 그대로 |

Claude 가 기본값으로 둔 것
- **D8 복귀 겹침**: 따로 처리하지 않는다(겹치면 접촉 규칙대로 밀린다). 플레이 뒤에 본다.

## 1. 단계 — 두 번 나눠 병합한다

| 단계 | 브랜치 | 끝나면 플레이로 볼 수 있는 것 |
|---|---|---|
| **A 봇전 판** | `feature/mp2-bot-race` | 메인 '봇전' → 봇 3명과 같이 달리고(서로 통과), 주행 중 순위와 결과 화면 전원 순위가 나온다. 기록·고스트 없음 |
| **B 몸싸움** | `feature/mp2-contact` (A 병합 뒤 `dev` 에서) | 출발 격자에 선다, 봇과 닿으면 서로 밀린다(무게 반영), 벽을 뚫지 않는다 |

A 를 먼저 내는 이유: B 의 틱 분해는 공개 API 변경이고 지문을 지나는 위험이 크다. A 만으로도 "봇이 보이는가·겨뤄지는가"(제안서 Q4~Q9)를 먼저 볼 수 있다. A 에서는 모두 같은 출발점에 겹쳐 선다(접촉이 없어 문제없다).

---

## 2. 단계 A — 봇전 판

**목표**: 메인 화면의 '봇전' 줄에서 트랙·속도·러너를 골라 시드로 뽑은 봇 3명과 달리고, 주행 중 내 순위와 결과 화면의 전원 순위·시간을 본다. 봇전은 기록·고스트를 남기지 않는다. 혼자 판은 비트 단위로 그대로다.

### 건드릴 것

| 파일 | 무엇 |
|---|---|
| `T/CircuitBot.cs`, `T/TrackFollower.cs` | **옮긴다** → `R/Bots/` (`.meta` 함께 `git mv`, 아래 "새로 만들 것"). `internal` → `public`. 코드 동작은 바꾸지 않는다 |
| 위 둘을 쓰는 테스트들 (`T/` 의 `using` 줄) | 네임스페이스 `ProtoHarness.ChainRush.Bots` 를 `using` 으로 추가. `ShortcutBot` 은 테스트에 남는다(쉬움 봇이 쓰지 않는다) |
| `N/RaceOrder.cs` | **옮긴다** → `R/Race/RaceOrder.cs` (`.meta` 함께). System 만 쓴다(`N/RaceOrder.cs:1-3`). 네임스페이스 → `ProtoHarness.ChainRush.Race` |
| `N/` 의 `RaceOrder` 사용처 (`RaceServer.cs`, `RaceClient.cs`) 와 그 테스트 | `using` 한 줄씩. Net 동결 방침(컴파일만 유지)의 범위 안이다 |
| `R/Race/RaceField.cs` | 봇 입력원을 받는다(`Initialize` 가 상대마다 `IInputSource` 를 받고 `KeyboardMouseInputSource` 면 거부 — MP1 리뷰 넘김), 매 틱 `RaceOrder.Advance`(`world.Circuit.Laps.Progress`, `N/RaceServer.cs:254` 형식), 결승 틱 기록, 완주자 + 달리는 러너 순위 합성(6-2), 결과 확정은 `FixedUpdate` 끝에서 전원이 그 틱을 마친 뒤(D7 a, 6-1), 재출발 때 순위 초기화. 읽기용 결과 API(순위·이름·결승 틱 또는 진행 거리) |
| `R/Flow/RaceMode.cs` | `Bots` 값 추가(주석: 봇과 겨루기, 기록·고스트 없음). 정수 값이 저장·직렬화되는 곳이 있으면 기존 값의 번호를 바꾸지 않는다(끝에 붙인다) |
| `RaceMode` 로 갈라지는 곳 전부 (`switch`·`==` — 구현 첫 단계에서 Grep 으로 목록을 만들어 보고에 붙인다) | `Bots` 를 어느 쪽으로 보낼지 하나씩 정한다: 트랙·속도·러너 선택 = Single 과 같음, 기록·고스트·최고 기록 비교 = 없음, 튜토리얼 분기 = 해당 없음 |
| `R/Flow/RaceSetup.cs` | 봇전 시드(`int`) 를 든다. `WithOpponents` 는 `Bots` 만 받는다(지금 `Single` 만 받는 검사를 바꾼다, `:42`). `Bots` 인데 상대가 0명이면 예외. MP1 테스트가 `Single` + 상대로 만드는 곳은 `Bots` 로 바꾼다(밝힌다 — 의미를 바꾼 것이지 통과용이 아니다) |
| `R/Flow/RaceAssembler.cs` | 상대 월드의 입력원을 `CircuitBot` 으로(지금은 키보드 기본, `R/ChainRushGame.cs:130`). `Mode == Bots` 이면 `ShowGhost`(`:360`) 거부, `TickCompleted += RecordTick`(`:143`)·`KeepFinish`(`:419`) 를 하지 않는다(D4). MP1 리뷰 넘김 "`GetSceneAt(sceneCount-1)`" → `sceneLoaded` 로 받은 씬 |
| `R/Flow/LobbyMenu.cs`, `R/Flow/Lobby/MainPage.cs`, `LobbyContext.cs` | 메인에 '봇전' 줄 → 같은 속도·트랙·러너 페이지 → `BuildSetup`(`LobbyMenu.cs:236`) 이 `RaceMode.Bots` 로 만들고 시드를 뽑아 `WithOpponents`. 트랙 페이지의 최고 기록 줄은 봇전에서도 혼자 판 기록을 보여 준다(봇전 기록이 없으므로) |
| `R/ChainRushHud.cs` | 주행 중 "순위 2 / 4"(`DrawPlay`, `:328`), 결과 화면 전원 행(`DrawResult`, `:648` — 순위·러너 이름·시간, 못 들어온 봇은 "진행 중 1,234 m"), 봇 머리 위 이름. 봇전이 아니면 지금 화면 그대로 |

3개 이상 수정 + 파일 이동 + 공개 API 추가라 §3-1 합의 대상이다. 씬·프리팹·`.asset` 은 건드리지 않는다(HUD 는 런타임에 `RaceAssembler.Field` 로 필드를 찾는다 — 씬 직렬화 참조가 필요하면 멈추고 보고).

### 새로 만들 것

```
목적: 시험 봇을 게임 레이스의 상대 입력원으로 쓰기 위해 (옮김, 새 코드 아님)
경로: Assets/_Project/Scripts/Runtime/ChainRush/Bots/CircuitBot.cs, TrackFollower.cs (새 폴더 Bots/ — 앞으로 난이도 봇이 모인다)
형태: sealed class (IInputSource), 지금과 같다
```
```
목적: 시드로 봇 3명의 러너를 뽑기 위해 (같은 시드 = 같은 구성)
경로: Assets/_Project/Scripts/Runtime/ChainRush/Bots/BotLineup.cs
형태: static class — 순수 함수 (프로필 목록, 플레이어 러너, 시드) → 봇 프로필 배열
```
```
목적: 봇전의 로비 진입·순위·결과·기록 없음을 증명하기 위해
경로: Assets/_Project/Scripts/Tests/PlayMode/ChainRushBotRaceTests.cs
형태: 테스트 (PlayMode)
```
```
목적: BotLineup 의 시드 재현·중복 없음·플레이어 러너 제외를 증명하기 위해
경로: Assets/_Project/Scripts/Tests/EditMode/BotLineupTests.cs
형태: 테스트 (EditMode)
```

- 기존 참고: 입력원 형식은 `R/Control/InputReplay.cs`·`KeyboardMouseInputSource.cs`, 순수 계산 클래스는 `R/Race/RaceOrder.cs`(옮긴 뒤), 테스트 형식은 `T/ChainRushRaceFieldTests.cs`. 새 PlayMode 테스트 클래스는 `tools/select-tests.ps1` 표에 배정한다(VERIFICATION §4-4).

### 검증 방법

- 컴파일 에러 0 (`Editor.log` 의 `Tundra build success`).
- 지문·월드: `ChainRushReplayTests.Replay_CircuitBotRun_RepeatsEveryTickBitForBit` 의 해시가 `0xEF92FB76BFD1D767` 그대로 (봇 클래스를 옮겨도 입력이 같다는 증거), `ChainRushRaceFieldTests` 전체(상대 3명 + 혼자 런 비트 일치).
- 영향 평가로 고르는 기존 테스트: `ChainRushFlowTests.Race`(로비 출발·결승·기록·고스트 — `RaceAssembler` 를 바꾼다), `ChainRushTutorialTests`(같은 조립 경로), `CircuitBot`·`TrackFollower` 를 쓰는 테스트 중 Lap 1건(`Race_BotLap_WithLobbyEnvironmentAndLook_CompletesOneLap` 의 STADIUM 보통 케이스), `NetReconcileTests`·`RaceOrder` 를 쓰는 Net 테스트(이동 뒤 컴파일·동작). `!Device`·`!Enemy`·`!NetEndReplay`.
- 새 테스트: 로비 '봇전' → 상대 3명 로드(입력원이 `CircuitBot`, 플레이어 러너 제외, 시드 같으면 같은 구성) / 플레이어 결승 때 결과에 4명, 순위가 결승 틱·진행 거리 순서와 같다(H4) / 봇전 결승 뒤 기록 파일·고스트 파일이 생기지 않는다(D4) / 재출발 뒤 순위 초기화. EditMode `BotLineupTests`.
- 사용자 플레이: STADIUM 보통, ALPHA, 봇 3명 1판 — 제안서 4절 Q4~Q9 중 접촉 없는 항목(Q4·Q5·Q6·Q7·Q8·Q9).

### 안 하는 것 (A)

접촉·밀어내기·출발 격자·틱 분해(B), 봇 난이도·자동 개선 루프(뒤로), `ShortcutBot` 의 게임화, 봇 효과음·모션 변경, 네트워크 코드 동작 변경(`using` 외), 씬·프리팹·트랙 `.asset`·ProjectSettings·패키지, D7 (b).

---

## 3. 단계 B — 몸싸움과 출발 격자

**목표**: 봇전에서 러너는 출발 격자에 서고, 같은 틱에 몸이 겹치면 무게 비율로 서로 밀리고 부딪힌 방향으로 속도를 주고받는다(D2a·D2b). 땅·벽·시야 질의는 지금처럼 트랙만 본다(D2d, 격리 월드라 구조상 그렇다). 밀린 러너는 벽을 뚫지 않는다. 혼자 판은 비트 단위로 그대로다.

### 건드릴 것

| 파일 | 무엇 |
|---|---|
| `R/ChainRushGame.cs` | `RunTick`(`:207-226`)을 세 단계 공개 메서드로 나눈다: **입력**(`tick++`·`PrepareTick`·`Consume`·`Attack`, `:209-213`) / **이동**(`player.Step`, `:214`) / **판정**(위험물·서킷·결승, `:215-225`, 끝에 `TickCompleted`). `StepTick` 은 셋을 같은 순서로 부른다 — 혼자 판의 실행 순서는 한 줄도 바뀌지 않는다 |
| `R/Race/RaceField.cs` | `FixedUpdate` 를 "전원 입력 → 전원 이동 → 접촉 풀기 → 전원 판정" 으로(D11). 접촉은 달리는 러너끼리만 |
| `R/RunnerMotor.cs` | 바깥에서 몸을 수평으로 밀고 수평 속도를 바꾸는 메서드 — 위치는 `CharacterController.Move` 로 밀어 벽에 막히게 한다(`SetPosition` 은 벽 검사가 없다, `:658`), 속도는 `RacerState.Velocity`(`R/RacerState.cs:95`)에 충돌 임펄스를 더한다. 스윙·그래플 중인 러너의 속도를 어떻게 받는지, 접지·복귀 기준(`LastGroundedS`, `:428`)은 구현에서 정하고 테스트로 보인다 |
| `R/Race/CircuitRace.cs`, `R/RunnerMotor.cs` 의 리셋 경로 | 격자 칸: 러너마다 출발 위치(뒤 거리·옆 거리)를 받아 `ResetAtSpawn` 과 랩 카운터 초기 S(`CircuitRace.cs:145-147`)를 같은 칸으로. 칸이 없으면(혼자 판·플레이어) 지금 그대로 |
| `R/Flow/RaceAssembler.cs` | 상대 월드에 격자 칸을 준다 |
| `T/ChainRushRaceFieldTests.cs` | **계약이 바뀐다 — 밝힌다**: 지금 테스트는 "플레이어 `TickCompleted` 안에서 상대는 한 틱 뒤" 를 단언한다(`:260,284`). 단계로 나누면 전원이 판정 단계에 같은 틱이다. 테스트를 통과시키려 고치는 것이 아니라 D11 로 계약을 바꾸는 것이고, 새 계약(판정 단계에서 전원 같은 틱)을 단언하도록 바꾼다 |

### 새로 만들 것

```
목적: 같은 틱의 러너 몸 겹침을 무게 비율의 수평 밀어내기와 충돌 임펄스로 풀기 위해 (결정적, 순서 고정)
경로: Assets/_Project/Scripts/Runtime/ChainRush/Race/RunnerContact.cs
형태: static class — 순수 함수 (위치·속도·반지름·높이·무게 배열, 반발 계수) → 러너별 수평 변위와 속도 변화. 짝은 인덱스 순서로, 고정 횟수 반복. 서로 다가오는 짝에만 임펄스(멀어지는 짝은 위치만)
```
```
목적: 출발 격자 칸(뒤 거리·옆 거리)을 트랙 프레임에서 계산하기 위해 (트랙 .asset 을 고치지 않는다)
경로: Assets/_Project/Scripts/Runtime/ChainRush/Race/StartGrid.cs
형태: static class — (트랙, 인원, 칸 번호) → 뒤 거리·옆 거리. 출발선에 가로 한 줄 4칸(8인이면 4 m 뒤에 둘째 줄), 간격 = min(2.4 m, 그 트랙 반폭에서 몸·벽 여유를 뺀 폭 ÷ 3), 1.2 m 미만이면 예외(`docs/COURSE.md` §7-2a). 누가 어느 칸에 서는지는 봇전 시드로 섞는다(플레이어 포함, 사용자 "a로")
```
- **바뀐 설계 (A 플레이 확인 9번과 "a로")**: 처음 안은 "플레이어 칸 0(지금 출발점) + 봇은 뒤 2열" 이었다. 가로 한 줄이면 봇전의 플레이어는 지금 출발점(가운데)이 아닌 자리에 설 수 있다 — 혼자 판(타임어택)은 격자를 쓰지 않으므로 지문은 그대로이고, `ChainRushRaceFieldTests` 의 "상대와 같이 달린 플레이어 런 = 혼자 런" 비교는 같은 칸에 세운 혼자 런과 비교하도록 바꾼다(접촉 끔, 6-5).
```
목적: 밀어내기 계산의 무게 비율·대칭·순서 결정성을 증명하기 위해
경로: Assets/_Project/Scripts/Tests/EditMode/RunnerContactTests.cs, StartGridTests.cs
형태: 테스트 (EditMode)
```
```
목적: 봇전의 접촉·벽·격자·결정성을 실제 월드에서 증명하기 위해
경로: Assets/_Project/Scripts/Tests/PlayMode/ChainRushContactTests.cs
형태: 테스트 (PlayMode)
```

- 격자를 코드로 계산하는 이유: 다섯 트랙 `.asset` 의 `startSlots` 를 고치려면 에디터 작업(§0)이 다섯 번 들고, 옆 오프셋 필드를 새로 넣어야 한다. 규칙 하나면 다섯 트랙에 같다(D6).
- 플레이어는 늘 칸 0(맨 앞 가운데)이다 — 혼자 판 지문을 지키는 가장 단순한 방법. 출발 순서를 섞는 것은 플레이 뒤.

### 검증 방법

- 컴파일 에러 0.
- 지문: `Replay_CircuitBotRun…` 해시 `0xEF92FB76BFD1D767` 그대로, 기존 롤백 테스트(`ChainRushRollbackTests`, `StepTick` 을 지난다), `ChainRushRaceFieldTests`(새 계약).
- 새 테스트: 겹치게 세운 두 러너가 다음 틱에 떨어진다 / 무거운 쪽이 덜 밀리고 속도도 덜 바뀐다(OMEGA 1.2 대 BETA 0.9) / 뒤에서 박으면 뒤쪽은 느려지고 앞쪽은 빨라진다, 법선 방향 운동량 합은 보존된다(반발 계수 범위 안) / 옆으로 닿으면 서로 옆으로 갈라진다 / 벽 옆에서 밀려도 도로 안에 남는다 / 같은 봇전 N틱을 두 번 돌려 전원 위치 해시가 같다 / 격자: 봇마다 다른 칸, 랩 카운터 시작 S 가 칸과 맞다, 재출발에도 같은 칸 / SUMMIT DROP 15 m 안에 4칸이 들어간다. EditMode 2종.
- 영향 평가 추가: `ChainRushFlowTests.Race`, `ChainRushTutorialTests`, Lap 1건(STADIUM 보통), `NetReconcileTests`(롤백이 `StepTick` 을 지난다).
- 사용자 플레이: STADIUM 보통 ALPHA 3판 + BETA 1판 + OMEGA 1판 (제안서 4절 Q1~Q10 전부).

### 안 하는 것 (B)

반발 계수 외의 접촉 튜닝값(마찰·접선 감속), D7 (b), D8 복귀 겹침 처리, 봉·앵커 점유(D2c A 유지), 봇이 접촉을 피하거나 노리는 판단, 출발 순서 섞기, 8인 격자 검증(3명만), 씬·프리팹·트랙 `.asset`·ProjectSettings·패키지.

---

## 4. 진행 방식 — Codex 구현, Claude 대조

각 단계마다:
1. Claude 가 이 요청서의 그 단계를 Codex 지시서(scratchpad 파일)로 옮긴다 — 첫머리 `승인:`·`브랜치:`·`Unity MCP: 이 실행에서 허용 (Unity_RunCommand, Unity_GetConsoleLogs)`, 범위·판정 기준·안 할 것, 금지선(§0: `.meta` 직접 수정 금지 — 파일 이동은 `git mv` 로 `.meta` 와 함께, 씬·프리팹 YAML, ProjectSettings, 패키지, `AssetDatabase.DeleteAsset`, 씬 저장).
2. `tools/codex-unity.ps1 -PromptFile <지시서> -Write` 로 Codex 가 구현 → 컴파일 → 고른 테스트 → `tools/test-runs.ps1 -Record`. 그동안 Claude 는 Unity MCP 를 부르지 않는다(§7-2 병렬 금지 2번).
3. Claude 가 결과 대조: `git diff` 를 범위와 맞춰 보고, 결과 XML 값과 `Editor.log` 를 직접 읽는다(Codex 의 "통과" 를 그대로 옮기지 않는다, §7-4). 범위 밖 변경이 있으면 버리지 않고 보고한다.
4. 사용자 플레이 확인 → 커밋(사용자 지시) → Codex 설계 리뷰(`adversarial-review --base dev`) + `unity-reviewer`("그 밖" 규모) → 병합(사용자 지시).

실패 규칙: Codex 가 같은 문제로 3번 실패하면 멈추고 보고(§4-5). Codex 사용량이 다하면 `Codex 구현 못 함: <원문>` 으로 알리고 Claude 가 이어서 할지 묻는다.

## 5. 위험 (Codex 탐색이 짚은 것)

- 틱 완료 이벤트 시점이 바뀐다(B) — 상대 진행을 보던 테스트·관찰자. 위 "계약이 바뀐다" 줄.
- 밀어내기를 `SetPosition` 으로 하면 벽을 뚫고 접지·복귀 기준과 어긋난다(`R/RunnerMotor.cs:428,658`) → `CharacterController.Move` 로.
- 격자 위치만 옮기면 `StartRun → ResetAtSpawn` 이 지우고 랩 카운터는 칸 0 에서 시작한다(`R/ChainRushGame.cs:258-262`, `R/Race/CircuitRace.cs:147`) → 리셋 경로와 랩 초기 S 를 같이.
- 봇 숨은 상태: `CircuitBot` 은 상태가 없다(그 틱 상태만 읽는다). `ShortcutBot` 은 상태가 있고 `Clear` 가 비어 있다(`T/ShortcutBot.cs:56`) — 이번에는 쓰지 않으므로 스냅샷에 봇 상태를 넣지 않는다.
- 지문 테스트는 고정 해시를 단언하지 않는다(`T/ChainRushReplayTests.cs:149` 는 기록과 재생을 비교, `:69` 는 해시를 로그로만) — 그래서 해시 값 `0xEF92FB76BFD1D767` 을 **로그에서 직접 읽어** 비교한다. 난이도 작업에서 쉬움 봇을 바꾸면 지문 기준선이 움직이므로, 그때 지문용 봇을 따로 고정한다.

## 6. 계획 반박 (Codex adversarial-review)

2026-11-13, `adversarial-review --scope working-tree`, 스레드 `01a1d347`, 판정 `needs-attention` (high 4 · medium 1). 사용자 승인("승인")은 이 결과가 오기 전에 나왔다. A 에 대한 답은 범위 안의 명확화(새 파일·새 동작 없음)라 A 를 진행하고, **B 의 답(6-4)은 게임 규칙이 들어가 B 시작 전에 사용자에게 다시 묻는다.**

| # | 지적 (요지, 근거) | 답 → 요청서 반영 |
|---|---|---|
| 6-1 high | A 에서 결과 확정은 전원 틱 처리 뒤여야 한다 — 플레이어 `StepTick` 이 `TickCompleted` 를 바로 낸다(`R/ChainRushGame.cs:199`), 상대는 그 뒤(`R/Race/RaceField.cs:80-81`). 그 시점에 확정하면 같은 틱에 들어온 봇이 미완주가 된다 | **받는다.** 결승 수집·순위 갱신·결과 동결은 `RaceField.FixedUpdate` 끝(전원이 그 틱을 마친 뒤)에서 한다. 플레이어 결승 틱에도 상대는 그 틱을 마저 돈다. 테스트: 플레이어와 봇이 같은 틱에 결승, 마지막 틱에 봇이 앞선 경우 |
| 6-2 high | `RaceOrder` 만으로는 완주자를 포함한 순위가 안 된다 — 끝난 러너를 빼고 남은 러너를 1부터 센다(`N/RaceOrder.cs:81-91,119-125`). 봇이 먼저 들어오면 실제 2위 플레이어가 "1 / 4" | **받는다.** 순위 = 완주자(결승 틱 순, 같은 틱은 같은 순위) 다음에 달리는 러너(`RaceOrder` 순위 + 완주자 수). 합성은 `RaceField` 안에서 한다(`N/RaceServer.cs:267-268`, `N/RaceStandingsText.cs:55-68` 과 같은 방식, 새 파일 없음). 테스트: 봇이 먼저 들어온 뒤 플레이어 표시가 "2 / 4" |
| 6-3 high (B) | 벽에 막힌 실제 이동량을 접촉 풀이에 되먹여야 한다 — 가상 위치에서 풀어도 벽 쪽 러너가 `Move` 에서 막히면 겹침이 남는다 | **받는다.** 풀이 → `Move` 적용 → 실제 위치를 다시 읽어 다시 풀이, 고정 3회. 벽에 막혀 못 간 몫은 다음 회에 상대가 받는다. 테스트: 벽 옆 2인·3인 압박에서 벽 관통 0, 남은 겹침 ≤ 2 cm |
| 6-4 high (B) | `Step` 뒤의 추가 `Move` 는 모터가 이동 중 처리하는 것(로프·스윙 제약, 벽 닿으면 스윙 해제, 접지, 복귀 기준 — `R/RunnerMotor.cs:385-439`, `Motion/PoleSwing.cs:81-91`)을 지나친다 | **받는다, 규칙은 B 전에 사용자 확인**: 제안 — ① 접촉이 없는 틱에는 추가 `Move` 를 하지 않는다(혼자 판 그대로) ② 밀린 뒤 접지는 기존 접지 검사로 다시 보고, 접지면 `LastGroundedS` 갱신 ③ **봉 스윙 중 부딪히면 스윙이 끊긴다**(벽에 닿을 때와 같다, `RunnerMotor.cs:391-398`) ④ 그래플 줄은 유지(다음 틱 줄 제약이 당긴다) ⑤ 타이머·부스터·게이지는 진행하지 않는다. 테스트: 절벽 끝에서 밀려 떨어짐, 스윙 중 접촉, 그래플 중 접촉 |
| 6-5 medium (B) | 기존 필드 테스트는 틱 번호뿐 아니라 "상대 3명과 달린 플레이어 런 = 혼자 런" 비트 비교를 한다(`T/ChainRushRaceFieldTests.cs:292-294`) — 접촉을 켜면 이 단언과 부딪힌다 | **받는다.** `RaceField` 에 접촉 켜기/끄기(공개 속성, 기본 켬)를 두고, 기존 비트 비교는 접촉 끔으로 유지(필드 조정의 회귀 검출을 지킨다), 접촉 켬은 새 테스트. 틱 분해 검증에 `T/ChainRushWorldTests.cs:168-196`(완료 이벤트·일시정지 계약)을 더한다 |
