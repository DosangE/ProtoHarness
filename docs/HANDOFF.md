# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-08 (T3d 병합, P2 합의 요청서). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-08)

- 브랜치: T3d 까지 `dev` 에 병합돼 있다(`48cf416`, `origin/dev` 에는 푸시 안 함). P2 합의 요청서(아래 2절)는 승인됐고(2026-10-08), 구현은 `feature/p2-replay-determinism` 에서 한다.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`(직선·원호·종단 곡선, 절대 `S`, 루프·초점), 이동(자유 조향·그립·경사·땅 붙잡기·가드), 드리프트 → 체인 게이지 → 체인 액션.
  - T3a 절차 노면, T3b 시드 생성기, T3c 절차 무한 코스(`ProceduralCourse`), T4 서킷(`TrackDefinition`·`LapCounter`·`CircuitRace`).
  - **T3d** (DECISIONS 2026-10-08 "코스 T3d"): 낡은 직선 무한 코스(`EndlessCourse`, `ChainRushEndless` 씬)와 그 제작 도구 3개·테스트를 지웠다. 조우·공격·그래플 테스트는 `ChainRushCombatTests` 로 옮겼다.
- 씬 3개: `ChainRushPrototype`(직선 유한), `ChainRushProcedural`(절차 무한), `ChainRushCircuit`(서킷). **씬은 YAML 이 원본이다**(씬 제작 도구는 `ChainRushSceneBuilder`·`ChainRushCircuitSceneBuilder` 만 남았다. 옛 사슬은 git 이력).
- 테스트: EditMode 213, PlayMode 61(Device 2 + Sweep 1 + 게이트 58). 병합 조건 실행은 Device·Sweep 제외 58건, 약 403초/회. 20 시드 스윕(`Run Course Sweep`) 약 933초.

## 2. 다음 작업: P2 — 입력 기록·재생과 결정성 증명 (합의 요청서, 승인됨 2026-10-08)

DESIGN 5절의 순서(P1 → **P2** → P3)에서 P1 의 전제(시드, 고정 틱, 입력 추상화, 레이서 상태 분리)가 모두 끝났다. P2 는 그 전제가 정말 통하는지, 즉 **같은 시드 + 같은 입력 = 같은 결과** 인지를 테스트로 재는 단계다. 서버 권위 레이스(P3)의 가장 큰 미확인 위험이 재현성이라서(DESIGN 2절 C9, DECISIONS 2026-10-04 "`CharacterController.Move` 의 재현성은 P2 에서 증명한다(미확인)") 네트워크 패키지를 고르기 전에 확인한다. 아래는 §3-2 형식의 합의 요청서다. **사용자가 2026-10-08 "ㄱㄱ" 로 2-6 의 질문 1~7 을 전부 추천대로 승인했다.** 구현은 이 범위 안에서만 한다.

### 2-1. 목표

① 한 런의 틱별 입력을 기록하고(`InputRecorder`) 그대로 다시 넣는(`InputReplay`) 작은 런타임 부품을 만든다. ② 같은 시드·같은 입력으로 다시 돌린 런이 **매 틱의 위치·속도·방향·체력 등과 비트 단위로 같은지** PlayMode 테스트로 잰다 — 같은 세션 안에서, 씬을 다시 불러온 뒤에, 다른 배속에서. ③ 입력을 한 틱만 바꾸면 결과가 갈라지는 **음성 대조**로 비교가 실제로 민감함을 보인다. ④ 어긋나면 첫 어긋난 틱·필드·값을 **측정해 보고**한다(허용 오차로 덮지 않는다).

### 2-2. 건드릴 것 (기존 파일)

없음(기본). 아래 두 가지만 예외다.
- 비결정의 원인이 "런을 시작할 때 지워지지 않는 상태" 같은 작은 리셋 누락으로 밝혀지면 같은 브랜치에서 고친다(2-6 질문 3, 고칠 때마다 보고).
- `CLAUDE.md` §9-2 스윕 조건 목록에 `Centerline` 한 단어 추가(질문 7, T4 에서 이월).

참고한 기존 자산(§2 검색 3연타, 2026-10-08): `Grep "InputRecorder|InputReplay|InputLog|StateTrace"` → 없음. `Glob Control/*.cs` → `IInputSource`·`TickInput`·`InputLatch`·`KeyboardMouseInputSource`. 가장 비슷한 `InputLatch.cs`(50줄)와 `ChainRushInputSourceTests.cs` 의 `ScriptedInputSource` 형식을 읽고 그대로 맞춘다. `ChainRushGame` 이 입력원을 `SetInputSource`(→ `Clear()` 한 번)와 `StartRun`(→ `Clear()`)에서 비운다는 것(`ChainRushGame.cs`)을 이용해, `Clear()` 를 "새 런 시작" 신호로 쓴다.

**코드 조사 결과 (결정성을 깰 수 있는 요소, `Runtime/` 전체 grep)**
- 시뮬 경로에 `Time.deltaTime`·`Time.time`·`UnityEngine.Random`·`DateTime` 이 **없다**. `Time.deltaTime` 은 `GrappleController.cs:75`(체인 시각 효과)뿐, `UnityEngine.Random` 은 `ProceduralCourse` 가 **새 런의 시드를 고를 때**(`SetSeed` 가 없으면)뿐이다. `Stopwatch`(`ProceduralCourse`·`CircuitRace`)는 측정용이라 상태에 영향이 없다.
- `Update`/`LateUpdate` 는 입력 폴링(`ChainRushGame.Update`)과 시각(`FollowCamera`, `GrappleController` 의 후보 표시, 애니메이션)뿐이다. 시뮬은 `ChainRushGame.FixedUpdate` 한 곳이다.
- 시뮬이 입력으로 보는 것은 틱당 `TickInput` 하나(6칸: 조향, 점프/그래플, 해제, 공격, 드리프트, 체인 액션)다.
- **남은 위험**: `CharacterController.Move`/`Physics.Linecast`/`MeshCollider` 가 같은 입력에서 같은 결과를 내는지는 증명된 적이 없다. 런을 다시 시작할 때 지워지지 않는 숨은 상태(예: `RunnerMotor` 의 장면 값, 컨트롤러·콜라이더의 엔진 내부 상태, 방금 다시 지은 노면 콜라이더가 첫 틱에 물리에 등록되는 시점)가 있으면 두 번째 런이 달라진다. `RoadPiece` 의 콜라이더 문제(T3c)도 같은 계열이었다. P2 가 바로 이것을 잰다.

### 2-3. 새로 만들 것 (§1-1 세 줄)

경로는 `Assets/_Project/Scripts/` 기준.

| # | 목적 | 경로 | 형태 |
|---|---|---|---|
| 1 | 한 런의 틱별 `TickInput` 열을 담아 두기 위해 | `Runtime/ChainRush/Control/InputLog.cs` | sealed class (순수 C#, `Add`/`this[i]`/`Count`) |
| 2 | 다른 입력원을 감싸 `Consume()` 가 돌려준 값을 틱마다 `InputLog` 에 쌓기 위해(`Clear()` = 새 기록) | `Runtime/ChainRush/Control/InputRecorder.cs` | sealed class (`IInputSource` 데코레이터) |
| 3 | `InputLog` 를 틱마다 하나씩 돌려주고 끝나면 빈 입력을 돌려주며 `Finished` 를 알리기 위해(`Clear()` = 처음으로) | `Runtime/ChainRush/Control/InputReplay.cs` | sealed class (`IInputSource`) |
| 4 | 기록·재생 부품의 규칙을 검증하기 위해 | `Tests/EditMode/InputLogTests.cs` | 테스트 |
| 5 | 틱마다 상태(위치·속도·방향·체력·그래플·`S` 등)를 표본으로 뽑아 두고 두 런을 비교해 첫 어긋남을 말해 주기 위해 | `Tests/PlayMode/StateTrace.cs` | 테스트 헬퍼 (입력원 래퍼, 게임 코드 무변경) |
| 6 | 서킷·절차 코스에서 기록 런과 재생 런이 같은지·갈라지는 입력은 갈라지는지·배속과 씬 재로드와 무관한지 보기 위해 | `Tests/PlayMode/ChainRushReplayTests.cs` | 테스트 |

`Control/` 은 4 → 7개(15개 넘으면 하위 분리, §1-2).

### 2-4. 설계

**기록·재생** (`Control/`)
- `InputRecorder(IInputSource inner, InputLog log)`: `Poll` 은 안쪽으로 그대로, `Consume` 은 안쪽 값을 받아 `log.Add` 하고 돌려준다. `Clear()` 는 안쪽 `Clear()` + `log` 비우기(= 새 런의 틱 0). 게임이 틱당 한 번만 `Consume` 을 부르므로 `log[i]` 는 틱 `i + 1` 의 입력이다.
- `InputReplay(InputLog log)`: `Poll` 은 아무것도 안 한다(키보드·마우스에 기대지 않는다). `Consume` 은 `log[i++]`, 끝나면 `new TickInput(0, false, false, false)` 와 `Finished = true`. `Clear()` 는 `i = 0`. 게임의 `SetInputSource`·`StartRun` 이 `Clear()` 를 부르므로 `SetInputSource(replay)` + `StartRun()` 이면 처음부터 재생된다.
- 직렬화·파일 저장은 하지 않는다(질문 1). `InputLog` 는 `TickInput` 값 목록이다.

**상태 트레이스** (`StateTrace`, 테스트 헬퍼)
- 입력원 래퍼가 `Consume()` 호출 때(= 틱 시작, 직전 틱이 끝난 상태) 표본을 뽑는다: 틱 번호, 위치 xyz, 속도 xyz, 방향, 회전 속도, 체력·격파·그래플 수, 게이지, 앵커 인덱스, 줄 길이, 트랙 `S`. 값은 `float`/`double` 의 비트 그대로 저장하고 틱마다 하나의 64비트 해시도 만든다(`SeedHash.SplitMix64` 로 섞기).
- 두 트레이스를 비교하면 첫 어긋난 틱·필드 이름·두 값(비트 차이 포함)을 문자열로 돌려준다. 같으면 틱 수와 최종 해시를 로그로 남긴다.
- 서킷에서는 랩 시간(틱)과 체크포인트 틱, 절차 코스에서는 모듈 열(`Modules` 종류·길이)과 원점 이동 횟수도 비교한다.

**테스트 시나리오** (`ChainRushReplayTests`)
1. `Replay_CircuitBotRun_RepeatsEveryTickBitForBit`: 서킷 씬에서 `CircuitBot` + `InputRecorder` + 트레이스로 **1700틱**(34초: 언덕, 점프 틈, 반원, 그래플 틈과 앵커 잡기까지)을 3배속으로 기록 → **같은 세션**에서 `InputReplay` 로 `StartRun()`(6배속) → 비교 → 씬을 **다시 불러와**(2배속) 한 번 더 재생 → 비교. 세 트레이스가 같아야 한다.
2. `Replay_ProceduralCourseRun_RepeatsCourseAndEncounters`: 같은 3단계를 절차 코스(`SetSeed(7)`, `CourseBot`, **2500틱** = 50초, 격파·모듈 스트리밍·조각 재활용·앵커 풀 포함)에서. 모듈 열과 격파·피격 수도 같아야 한다.
3. `Replay_OneTickChanged_DivergesFromThatTickOn`: 위 기록에서 틱 300 의 조향을 바꾼 로그로 재생하면 트레이스가 **틱 300 이후에** 어긋나고 그 앞은 같다(비교기가 어긋남을 실제로 잡고 입력이 실제로 시뮬을 움직인다는 증거).
4. `Replay_IsIndependentOfFrameRate`: 1 의 6배속·2배속 재생이 이 항목을 겸한다(틱당 프레임 수가 다른 두 런이 같다).

**이 테스트가 증명하지 않는 것**: 다른 기기·다른 OS·IL2CPP/모바일에서의 부동소수 일치(COURSE 9절의 "보장하지 않음" 그대로), 여러 레이서, 네트워크 지연·보정. **같은 머신·같은 빌드(에디터)** 안에서의 재현성만 본다.

### 2-5. 검증 방법

- **컴파일**: Console Error 0.
- **EditMode** `InputLogTests`: 기록이 `Consume` 순서와 값(6칸 모두)을 보존한다, `Clear()` 가 새 기록을 시작하고 안쪽 `Clear()` 도 부른다, 재생이 틱마다 하나씩 돌려주고 끝나면 빈 입력 + `Finished`, `Clear()` 가 처음으로 되감는다, 재생은 `Poll` 로 아무것도 읽지 않는다, 널·빈 로그 처리. 기존 213 은 그대로.
- **PlayMode** `ChainRushReplayTests` 3~4건(위). 어긋나면 **고치지 않고 먼저 보고**한다: 첫 어긋난 틱, 필드, 값, 같은 세션/재로드/배속 중 어느 쪽인지(질문 3).
- **회귀**: 기존 PlayMode 58건 무수정 통과. 병합 조건(§9-2)은 컴파일 0 + EditMode + PlayMode(Device·Sweep 제외) 연속 2회를 지금 규칙대로. 코스·모터 코드가 안 바뀌면 스윕은 생략한다(바뀌면 1회).
- 시간(계산): 서킷 1700틱 = 34초 시뮬레이션이라 3배속 기록 약 11초 + 6배속 재생 약 6초 + 2배속 재생 약 17초 ≈ 36초. 절차 코스 2500틱 = 50초라 약 17 + 8 + 25 ≈ 52초. 음성 대조 약 5초. **합계 약 90초** → 게이트 약 403 → 약 490초/회.
- 눈으로 보는 확인은 해당 없음(화면 변화 없음).

### 2-6. 사용자에게 물을 것 (추천을 받으면 "ㄱㄱ")

1. **범위**: **메모리 기록·재생 + 상태 트레이스 비교 + 음성 대조 추천.** 입력 로그의 파일 저장·직렬화 형식, 고스트 표시(서킷 최고 기록과 겨루기), 서버 전송 형식은 이번에 하지 않는다(결정성이 증명된 뒤 별도 요청서). 대안: 파일 저장까지 포함한다(형식·버전 표기를 같이 정해야 해 범위가 커진다).
2. **비교 정밀도**: **비트 단위로 같음을 요구하고 허용 오차를 두지 않기 추천.** 어긋나면 값을 보고하고 멈춘다. 대안: 1e-4 같은 허용 오차(작은 어긋남이 쌓여 커지는 것을 못 잡고, 서버 권위·클라 예측의 보정 크기를 가늠하기 어렵다).
3. **비결정이 발견되면**: **런 시작 때 지워지지 않는 상태 같은 작은 리셋 누락(코드 한두 줄)은 같은 브랜치에서 고쳐 테스트로 증명하되 고칠 때마다 보고, 엔진·물리 자체 한계로 보이면 고치지 않고 측정 결과만 보고하고 멈추기 추천**(그 경우 서버 권위 쪽 설계를 따로 합의). 대안: 어떤 경우든 고치지 않고 보고만 한다.
4. **런타임 부품 위치**: **`Control/` 에 세 클래스(`InputLog`·`InputRecorder`·`InputReplay`) 추천**(서버·고스트가 쓸 같은 자리). 대안: 테스트 전용으로 `Tests/` 안에만 둔다(나중에 옮긴다).
5. **트레이스 표본 위치**: **입력원 래퍼 안에서 `Consume()` 때 표본(게임 코드 무변경) 추천.** 대안: `ChainRushGame` 에 "틱 끝" 이벤트를 추가한다(공개 API 추가).
6. **길이와 게이트 편입**: **서킷 1700틱·절차 코스 2500틱, 게이트에 넣기(+약 90초/회) 추천**(결정성은 매번 지켜야 하는 성질). 대안: `Sweep` 같은 별도 카테고리로 빼고 코스·모터를 바꿀 때만 돌린다.
7. **스윕 조건 목록에 `Centerline` 추가**(이월): **추가 추천** — `Centerline` 은 모든 코스의 투영을 쥐고 있어 T4 에서도 스윕을 돌렸다. `CLAUDE.md` §9-2 한 곳만 고친다(규칙 변경이라 승인 필요). 대안: 지금 목록 그대로(`CourseGenerator`·`CourseTuning`·`ProceduralCourse`).

### 2-7. 안 하는 것

파일 저장·직렬화, 고스트 표시, 서버·네트워크 코드와 패키지, 다른 기기·IL2CPP 검증, 여러 레이서, 시뮬·코스·모터 코드 변경(질문 3 의 작은 리셋 누락만 예외), 기존 테스트·씬 수정, `CLAUDE.md` 의 질문 7 외 변경.

### 2-8. 승인이 따로 필요한 단계 (§0, §3-3)

- 비결정 원인을 고치는 변경은 (질문 3 의 사전 승인 범위 안이라도) 고친 내용과 이유를 그때 보고한다.
- 진단용 임시 파일을 만들면 그것을 지우는 일은 그때마다 승인받는다(§0).
- 새 씬은 만들지 않는다(기존 `ChainRushCircuit`·`ChainRushProcedural` 사용).

### 2-9. 승인 뒤 시작 순서

1. `docs/RULES/CONVENTIONS.md`, `VERIFICATION.md`, `BRANCHING.md` 를 읽는다.
2. 이 요청서(`docs/p2-proposal`)가 `dev` 에 병합됐는지 확인한다. 안 됐으면 사용자에게 먼저 묻는다.
3. `dev` 에서 `feature/p2-replay-determinism` 브랜치를 만든다(`feature/<영역>-<내용>`, §9-1).
4. 순서: `InputLogTests`(고치기 전 실패: 컴파일 불가 확인) → `InputLog`·`InputRecorder`·`InputReplay` → EditMode 통과 → `StateTrace` + 음성 대조부터(비교기가 어긋남을 잡는지 먼저 확인) → 서킷 재생 → 절차 코스 재생 → 어긋나면 보고(질문 3) → 게이트 2회 → 문서.

### 2-10. 남아 있는 다른 후보

- **T3e 장식**, **T5 보충 모듈**, **P3 레이스**(P2 의 결과를 보고 서버 권위·보정 방식을 정한 뒤).
- 체감 튜닝: `CourseTuning_Default.asset`·`Circuit_Stadium.asset` 은 사용자가 직접 달려 본 피드백 대기.

## 3. 보류된 사용자 결정

| # | 주제 | 상태 | 참고 |
|---|---|---|---|
| 1 | 병합 조건 검증 줄이기 | 사용자가 질문함("작업한 부분만 검증하면 안 되나"). 제시안: A 그대로 / **B 추천: 전체 1회 + 2회차는 바뀐 영역 클래스만** / C 영향 범위만 / D 1400m 장거리 테스트 배속 3 → 5. **답 대기.** T3a 는 A(전체 2회)로 검증했다. 실행 1회가 약 240초로 늘었다(T3c 뒤 약 377초). 바꾸면 `CLAUDE.md` §9-2 + DECISIONS, `docs/` 브랜치, 문서 검증(§9-2) | 2026-10-05 대화 |
| 2 | 물리 값 체감 튜닝 | 전부 계산으로 정한 시작값. 사용자 플레이 피드백 대기 | 아래 표 |
| 3 | 헤딩 범위 제한 | 계속 꺾으면 뒤로도 돈다(가드에 닿으면 앞으로 돌려짐). 막을지 미정 | DECISIONS T2a |
| 4 | 기존 PlayMode 테스트의 착지 확인 | `StartRun` 직후 `IsGrounded` 가 순간이동 전 발판 값을 돌려줘 "착지" 확인이 노면 없이도 통과한다(T3a 에서 발견). 새 테스트만 고정 틱 1회 뒤 확인으로 고쳤다. 기존 테스트(`ChainRushCurveTests` 등 `StartOn`)를 고칠지 **별도 `fix/` 합의 필요** | DECISIONS T3a |

튜닝 대상 시작값 (`RunnerMotor`/`FollowCamera`/`GrappleController` 인스펙터, 씬 값이 없으면 코드 기본값):

| 묶음 | 값 |
|---|---|
| 조향 | `maxTurnRate` 120°/s, `turnAcceleration` 1200°/s², `airTurnScale` 0.5, `sideGrip` 60, `airSideGrip` 18 |
| 경사 | `slopeSpeedFactor` 1.5, 속도 배율 0.7 ~ 1.3, `groundSnapDistance` 0.3 |
| 가드 | `guardSpeedLoss` 0.15 |
| 드리프트 | `driftGrip` 12, `driftTurnScale` 1.3, `driftSpeedScale` 0.9, `slideMetersPerSlot` 6 |
| 슬링샷 | `slingLead` 18, 당김 0.4 s · 45 m/s² · 최대 20, `slingTurnRate` 90°/s, 유지 0.8 s · 16 |
| 그래플 강화 | `empoweredRetractSpeed` 12, `empoweredReleaseSpeed` 18 |
| 코너 스윙 | `swingSpeed` 14, `swingAcceleration` 20, `swingMaxTime` 2, `swingExitSpeed` 17, `swingExitCarryTime` 0.6 |
| 카메라 | `maxYawSpeed` 180°/s |

## 4. 환경 메모 (다음 세션이 헷갈릴 수 있는 것)

- 이 머신(`C:/Users/User/Desktop/PCUBE/ProtoHarness`)의 에디터는 **6000.3.19f1** 이다(결정 버전 25f1 아님). 그래서 `ProjectVersion.txt`·`packages-lock.json`·`ProjectSettings.asset`(iOS 발열 설정 3줄 삭제)이 수정으로 보인다. 로컬 환경 차이라 커밋하지 않는다(사용자 2026-10-04). 검증 보고에는 19f1 에서 돌렸다고 적는다.
- 테스트 결과 XML 은 Unity 의 `Path.GetTempPath()/ChainRush-PlayMode-results.xml` 하나에 덮어쓴다(EditMode 실행도 같은 파일). 에디터의 `Path.GetTempPath()` 는 이 머신에서 `C:\Users\Public\Documents\ESTsoft\CreatorTemp\` 이다(2026-10-08 Editor.log 의 `ChainRush tests: ... XML=` 줄로 확인. 셸의 `%TEMP%` 와 다르다). 경로는 Editor.log 에서 `ChainRush tests:` 를 찾으면 나온다. EditMode 는 MCP `Unity_RunCommand` 로 `TestRunnerApi.Execute(new Filter { testMode = TestMode.EditMode })`, PlayMode 는 `EditorApplication.ExecuteMenuItem("ProtoHarness/Chain Rush/Run PlayMode Tests")` 로 시작했다. 실행마다 따로 보관하려면 끝날 때 복사한다.
- Unity MCP: 도메인 리로드마다 브리지가 몇 초 끊긴다(`Unity not detected (no fresh discovery files found)`). `~/.unity/mcp/connections/bridge-*.json` 이 다시 생기면 재시도한다. 에디터가 백그라운드면 스크립트를 자동 임포트하지 않을 수 있다 → `AssetDatabase.Refresh()`. 테스트 전에 새 코드가 로드됐는지(리플렉션 등) 확인한다.
- `.codex/agents/` 가 `git status` 에 수정으로 보이면 `autocrlf` 표시다(내용은 HEAD 와 같음). 손대지 않는다.
- `index/symbols.tsv` 는 낡았다(git-head `9f5a96f`). B모드로 쓰기 전에 재생성한다(§6-2).
- GitHub Desktop 이 켜져 있으면 `.git/index.lock` 이 남을 수 있다. 실행 중인 `git.exe` 가 없을 때만 지운다(2026-10-05 한 번 발생).
- 씬 저장을 `Unity_RunCommand` 로 하려면 매번 승인받는다(§0, §3-3). T2a 의 승인은 1회성이었다.
- 도메인 리로드 때 `Deleting invalid font reference.` 경고가 나온다(P1-3a 부터, 원인 미확인). 변경과 무관.
