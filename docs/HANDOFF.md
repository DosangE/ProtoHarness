# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-08 (P3 조사 병합, 롤백 안전성 합의 요청서). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-08)

- 브랜치: P3 준비 조사(`DESIGN.md` §7-5)까지 `dev` 에 병합돼 있다(`5ac8a70`, `origin/dev` 에는 푸시 안 함). 롤백 안전성 합의 요청서(아래 2절)는 승인됐고(2026-10-08), 구현은 `feature/p3-rollback-safety` 에서 한다.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`(루프·초점), 이동·드리프트·체인 액션, 절차 노면(T3a)·시드 생성기(T3b)·절차 무한 코스(T3c)·서킷(T4), 직선 무한 코스 정리(T3d).
  - **P2** (DECISIONS 2026-10-08 "P2"): 입력 기록·재생(`InputLog`·`InputRecorder`·`InputReplay`, `Control/`)과 상태 트레이스 비교(`StateTrace`, 테스트). **같은 시드 + 같은 입력 = 같은 런이 같은 머신·같은 에디터에서 비트 단위로 성립한다**: 서킷 1700틱·절차 코스 2500틱을 같은 세션(6배속)·씬 재로드(2배속)·별도 세션에서 모두 재현했다.
- 씬 3개: `ChainRushPrototype`(직선 유한), `ChainRushProcedural`(절차 무한), `ChainRushCircuit`(서킷).
- 테스트: EditMode 227, PlayMode 64(Device 2 + Sweep 1 + 게이트 61). 병합 조건 실행은 Device·Sweep 제외 61건, 약 500초/회. 20 시드 스윕(`Run Course Sweep`) 약 933초.

## 2. 다음 작업: 롤백 안전성 — 한 틱 스냅샷·복원과 되감아 재실행 증명 (합의 요청서, 승인됨 2026-10-08)

P3 조사(`DESIGN.md` §7-5)의 결론: NGO 는 롤백·재시뮬 예측을 제공하지 않고, Fusion 의 내장 예측도 `CharacterController` 기반 모터에서는 우회가 필요하다. **어느 쪽을 고르든** 서버와 클라이언트는 `RunnerMotor.Step` 을 같은 입력으로 되감아 여러 번 다시 실행해야 하고, 그러려면 한 틱 시작 상태를 **빠짐없이 저장하고 되돌릴 수 있어야** 한다. P2 는 "같은 입력이면 같은 결과" 까지 증명했다. 이 요청서는 그다음, **"되감아 다시 해도 같은 결과"** 를 네트워크 패키지 없이 서킷에서 증명한다. 아래는 §3-2 형식의 합의 요청서다. **사용자가 2026-10-08 "ㄱㄱ" 로 2-6 의 질문 1~7 을 전부 추천대로 승인했다.** 구현은 이 범위 안에서만 한다.

### 2-1. 목표

① 서킷 레이스의 한 틱 시작 상태를 값으로 담는 스냅샷(`SimSnapshot`)과 복원을 만든다. ② 한 틱 진입점을 `FixedUpdate` 밖으로 꺼내(`StepTick`) 테스트가 시간(Unity 프레임) 없이 틱을 직접 돌릴 수 있게 한다. ③ **"스냅샷 → N틱 → 복원 → 같은 입력으로 다시 N틱 → 매 틱 상태가 비트 단위로 같은가"** 를 P2 의 `StateTrace` 로 증명한다. 지상·공중(점프 틈)·그래플에 매달림·코너 스윙 중, 랩 이음매 근처에서 각각 스냅샷을 찍고, **한 프레임 안에서 여러 번** 되감는다. ④ 동작 불변은 P2 의 재생 해시(서킷 `0x267B2F98692829F7`, 절차 코스 `0xA79E26B06BCEB385`)가 **그대로**인 것으로 증명한다.

### 2-2. 건드릴 것 (기존 파일)

| 파일 | 바꾸는 것 | 이유 |
|---|---|---|
| `Runtime/ChainRush/RacerState.cs` | ① 접지 상태 `Grounded` 를 상태에 둔다(`ref bool`). ② `Capture()` → `RacerState.Snapshot`(값 struct), `Restore(in Snapshot)`. 모든 필드(체력·피격·공격 틱, 속도·조향·방향·회전, 코요테, 앵커·줄, 게이지·슬링·스윙, 점프 예약 등) | `Reset()`(`:97-119`)이 아는 필드가 곧 스냅샷 대상이다 |
| `Runtime/ChainRush/RunnerMotor.cs` | 접지의 출처를 `controller.isGrounded`(`:74,136,206,207`)에서 `RacerState.Grounded` 로 바꾸고, **`controller.Move` 호출 직후마다**(`:203`, `SnapToGround` `:296`, `ResetAtSpawn` `:378`) `Grounded = controller.isGrounded` 로 복사한다. `IsGrounded` 는 `Racer.Grounded` 를 돌려준다 | **`CharacterController` 의 접지는 마지막 `Move` 의 결과라서 복원할 수 없다.** 복원 뒤 첫 틱이 `grounded = false` 로 시작하면 어긋난다 |
| `Runtime/ChainRush/ChainRushGame.cs` | ① `FixedUpdate` 의 본문을 `StepTick(IInputSource source)` 로 뺀다(틱 증가 → `circuit.PrepareTick` → `source.Consume()` → 지금 순서 그대로). `FixedUpdate` 는 `StepTick(inputSource)`. ② `CaptureSnapshot()` / `RestoreSnapshot(in SimSnapshot)`: 틱, `RacerState` 스냅샷, 러너 위치, 서킷 상태. **무한(절차) 모드에서는 `NotSupportedException`** | 한 프레임에 여러 틱을 직접 돌리고 되감기 위한 진입점. 순서가 안 바뀌어야 한다 |
| `Runtime/ChainRush/Track/LapCounter.cs` | `Capture()`/`Restore()` + 중첩 `Snapshot`(진행도, 마지막 `S`, 시작 여부, 완료 랩, 다음 체크포인트, 시작 틱, 랩·체크포인트 틱 배열 복사) | 랩 상태도 되감겨야 한다 |
| `Runtime/ChainRush/Race/CircuitRace.cs` | `Capture()`/`Restore()`(마지막 `S` + `LapCounter` 스냅샷 + 중심선 초점) | 서킷 레이스 상태 |

기존 파일 5개 수정(§3-1 규모 기준)이라 합의 대상이다. 공개 API 는 추가만이고 `IsGrounded` 는 같은 이름·같은 의미(Move 직후의 접지)로 남는다.

**코드 조사 결과 (스냅샷에 들어갈 상태, `Runtime/` grep)**
- `RunnerMotor` 의 `RacerState` 밖 필드 `drifting`·`slingTarget`·`swingAnchor`·`guardNormal`·`hitGuard` 는 모두 **틱 안에서 다시 계산되거나 시각용**이다(`drifting` `:149`, `slingTarget` `:171`, `hitGuard` `:202`; `swingAnchor` 는 `:254` 에서 쓰고 시뮬에서는 읽지 않는다). 틱을 넘어 시뮬에 영향을 주는 숨은 상태가 아니다. `spawnPosition` 은 상수다.
- `GrappleController` 의 상태는 모두 `RacerState`(앵커 인덱스·줄 길이)에 있고 나머지(`lastAnchor`·`visualExtension`·`candidate`)는 시각이다.
- **`CharacterController.isGrounded` 하나가 `RacerState` 밖의 진짜 상태다.** 읽는 곳: `RunnerMotor.cs:74,119,136,206,207`, `GrappleController.cs:114`(`TryAttach`), `EnemyDirector.cs:60`, `ProceduralCourse.cs:231`, HUD·`RunnerAnimation`(시각). 쓰는 곳(= `controller.Move`): `RunnerMotor.cs:203,296,378`.
- 서킷에는 적·코스 스트리밍·움직이는 월드가 없다(정적 노면 콜라이더만). 그래서 서킷 한정이면 `EnemyDirector`·`ProceduralCourse` 상태를 스냅샷에 안 넣어도 된다.

참고한 기존 자산(§2 검색 3연타, 2026-10-08): `Grep "Snapshot|Rollback|StepTick|Restore"`(Runtime·Tests) → 없음. `Glob Runtime/ChainRush/*.cs` → 새 스냅샷 타입을 둘 곳. 가장 비슷한 `RacerState.Reset()` 과 `InputReplay`·`StateTrace`(P2)를 읽고 같은 형식(순수 값 struct, 테스트 래퍼)으로 맞춘다.

### 2-3. 새로 만들 것 (§1-1 세 줄)

경로는 `Assets/_Project/Scripts/` 기준.

| # | 목적 | 경로 | 형태 |
|---|---|---|---|
| 1 | 게임 단위 스냅샷(틱, 레이서, 러너 위치, 서킷 상태)을 한 값으로 묶어 넘기기 위해 | `Runtime/ChainRush/SimSnapshot.cs` | readonly struct |
| 2 | `RacerState`·`LapCounter` 스냅샷이 **모든 필드를** 담는지(필드가 늘면 실패하도록) 검증하기 위해 | `Tests/EditMode/SnapshotTests.cs` | 테스트 (반사로 필드 목록을 비교) |
| 3 | 스냅샷 → 재실행 → 복원 → 재실행이 비트 단위로 같은지, 한 프레임 안에서 여러 번 되감아도 같은지 보기 위해 | `Tests/PlayMode/ChainRushRollbackTests.cs` | 테스트 |

`Runtime/ChainRush/` 루트는 지금 `RacerState`·`RunnerMotor`·`ChainRushGame` 등이라 파일이 늘어도 15개를 넘지 않는다(§1-2).

### 2-4. 설계

**수동 스텝** (`StepTick`): 테스트는 `Time.timeScale = 0`(FixedUpdate 가 안 돈다)으로 두고 `game.StepTick(source)` 를 직접 부른다. 게임은 `Running` 단계 그대로라 모터·그래플·서킷의 `IsRunning` 보호가 통과한다. 실시간·프레임 수에 의존하지 않아 정확히 틱 K 에서 스냅샷을 찍을 수 있고 1700틱이 한 프레임에 끝난다(빠르다).

**복원**: `RestoreSnapshot` 은 `controller.enabled = false` → 위치 쓰기 → `enabled = true`(`ShiftOrigin` 과 같은 방법) → `Physics.SyncTransforms()` → `RacerState.Restore`(접지 포함) → 틱·서킷 상태 복원. 스냅샷은 값 복사라 이후 상태 변화가 스냅샷을 바꾸지 않는다.

**증명 시나리오** (`ChainRushRollbackTests`, 서킷 씬)
1. 봇(`CircuitBot`)이 입력을 기록하며 수동 스텝으로 1700틱을 달린다. 틱 K 마다 정해 둔 지점에서 스냅샷을 찍는다: ① 지상 주행(틱 300) ② **공중**(점프 틈 위, 같은 틱에서 `Grounded` 가 false 임을 단언) ③ **그래플에 매달림**(`AnchorIndex != NoAnchor` 단언) ④ 랩 이음매 근처(별도 짧은 런, 시작선 앞 3m). **코너 스윙**(`SwingTicks > 0`)과 슬링샷은 `TrackFollower` 로 드리프트·체인 액션을 스크립트한 짧은 런으로.
2. 각 스냅샷에서: N틱(60)을 더 달려 기준 트레이스 A 를 만든다 → 복원 → 같은 로그(`InputReplay` 를 K 부터)로 다시 N틱 → 트레이스 B. **A 와 B 의 모든 표본이 일치**해야 한다.
3. **한 프레임 안에서 20번** 복원 → 40틱 재실행을 반복하고(중간에 `yield` 없음) 매번 기준과 같음을 확인한다. 한 프레임에 여러 번 `controller.Move` 를 부르는 것이 실시간 `FixedUpdate` 한 틱씩과 같은 결과를 내는지의 증거다.
4. 복원이 **다른 상태에서 되돌아와도** 같다: 틱 600 에서 스냅샷 → 틱 900 까지 → 틱 300 의 스냅샷으로 복원 → 다시 900 까지: 처음 달린 900틱과 같다.
5. 수동 스텝으로 달린 런의 트레이스와 P2 방식(실시간 `FixedUpdate`)으로 기록한 같은 로그의 트레이스가 같다(수동 스텝이 시뮬을 바꾸지 않는다).
6. 음성 대조: 복원할 때 `Grounded` 만 일부러 뒤집으면 재실행이 어긋난다(이 필드를 상태로 옮긴 이유가 실제 효과임을 보인다).

**증명하지 않는 것**: 무한(절차) 코스·적·코스 스트리밍의 롤백(월드가 움직여 별도 설계, 질문 1), 네트워크 지연·입력 지연 보정, 스냅샷 링버퍼·직렬화, 여러 레이서, 다른 기기의 부동소수. P2 와 같이 **같은 머신·같은 에디터** 범위다.

### 2-5. 검증 방법

- **컴파일**: Console Error 0.
- **EditMode**: 기존 227 그대로 + `SnapshotTests`: `RacerState` 를 `Reset()` 한 뒤 모든 필드를 서로 다른 값으로 바꾸고 `Capture` → 또 바꾸고 → `Restore` 하면 원래로 돌아온다(반사로 `RacerState` 의 모든 인스턴스 필드를 훑어 **스냅샷이 빠뜨린 필드가 없음**을 확인; 필드를 추가하고 스냅샷을 안 고치면 이 테스트가 실패한다). `LapCounter` 도 같은 방식 + "중간에 스냅샷 → 복원 → 이어 달리면 끊김 없이 달린 것과 같은 랩 시간". 스냅샷은 값 복사다.
- **PlayMode** `ChainRushRollbackTests` 5~6건(위). 어긋나면 **고치지 않고 먼저 보고**한다: 첫 어긋난 표본·필드·값(질문 4).
- **동작 불변**: P2 의 `ChainRushReplayTests` 3건이 **무수정으로 통과하고 로그의 해시가 그대로**(서킷 `0x267B2F98692829F7`, 절차 코스 `0xA79E26B06BCEB385`)여야 한다. 접지 출처를 바꾸는 `RunnerMotor` 변경이 시뮬을 안 바꿨다는 가장 강한 증거다. 기존 PlayMode 61건도 무수정.
- **병합 조건(§9-2)**: 컴파일 0 + EditMode + PlayMode(Device·Sweep 제외) 연속 2회. 모터·`ChainRushGame` 변경이므로 **20 시드 스윕도 1회**(절차 코스가 접지·틱 순서에 기대는지 확인).
- 시간(계산): 새 PlayMode 는 실시간이 아니라 수동 스텝이라 합계 약 10~20초. 게이트 약 500 → 약 520초/회.
- 눈으로 보는 확인은 해당 없음(화면 변화 없음).

### 2-6. 사용자에게 물을 것 (추천을 받으면 "ㄱㄱ")

1. **범위**: **서킷(고정 월드)만 추천.** 무한(절차) 코스·적·스트리밍은 스냅샷 대상이 아니고 `CaptureSnapshot` 이 `NotSupportedException` 으로 크게 깨지게 한다(§5). 무한 모드는 싱글로 두기로 했다(DESIGN §8 질문 1). 대안: 적·`ProceduralCourse` 상태까지 포함한다(생성기·조각 풀·원점 이동의 복원은 별도 설계라 범위가 크게 는다).
2. **접지 상태**: **`RacerState.Grounded` 로 옮기고 `RunnerMotor` 가 `Move` 직후마다 복사 추천.** 상태가 한곳(`RacerState`)에 모이고 복원이 정확하다. 대안: 복원 때 `CharacterController` 에 아주 작은 이동을 시켜 접지를 다시 얻는다(위치가 비트 단위로 달라질 수 있어 증명이 깨진다).
3. **한 틱 진입점 공개**: **`StepTick(IInputSource)` 를 공개하고 `FixedUpdate` 가 그것을 부르게 하기 추천**(동작 불변은 P2 해시가 증명). 대안: 실시간 `FixedUpdate` 로만 돌리는 테스트(한 프레임 다중 재시뮬과 정확한 틱 위치 스냅샷을 증명할 수 없다).
4. **어긋나면**: **P2 와 같다 — 상태 필드 누락 같은 작은 원인은 같은 브랜치에서 고쳐 테스트로 증명하되 고칠 때마다 보고, 엔진·물리 자체 한계로 보이면 고치지 않고 측정만 보고하고 멈추기.** 대안: 어떤 경우든 고치지 않고 보고만.
5. **스냅샷 타입 위치**: **`RacerState.Snapshot`·`LapCounter.Snapshot` 중첩 struct + 새 `SimSnapshot.cs` 추천**(타입이 쓰는 곳 가까이). 대안: 스냅샷 전용 폴더·네임스페이스를 새로 만든다(새 네임스페이스는 합의 대상이라 이번엔 피한다).
6. **반사 가드 테스트**(필드가 늘면 실패): **추천.** 스냅샷이 조용히 낡는 것을 막는다.
7. **게이트 편입**: **PlayMode 롤백 테스트를 게이트에 넣기 추천**(결정성처럼 매번 지켜야 하는 성질, 비용 약 +20초/회).

### 2-7. 안 하는 것

네트워크·입력 지연 보정·스냅샷 링버퍼·직렬화·여러 레이서, 무한(절차) 코스·적·스트리밍의 롤백, 패키지·`manifest.json`, 씬 수정, 시뮬 규칙(이동·그래플·드리프트 수치) 변경, `CLAUDE.md` 변경.

### 2-8. 승인이 따로 필요한 단계 (§0, §3-3)

- 비결정·롤백 불일치의 원인을 고치는 변경은 (질문 4 의 사전 승인 범위 안이라도) 고친 내용과 이유를 그때 보고한다.
- 진단용 임시 파일을 만들면 그것을 지우는 일은 그때마다 승인받는다(§0).
- 새 씬은 만들지 않는다(`ChainRushCircuit` 사용).

### 2-9. 승인 뒤 시작 순서

1. `docs/RULES/CONVENTIONS.md`, `VERIFICATION.md`, `BRANCHING.md` 를 읽는다.
2. 이 요청서(`docs/rollback-proposal`)가 `dev` 에 병합됐는지 확인한다. 안 됐으면 사용자에게 먼저 묻는다.
3. `dev` 에서 `feature/p3-rollback-safety` 브랜치를 만든다.
4. 순서: **(회귀 기준선)** `RacerState.Grounded` 와 `StepTick` 리팩터만 먼저 넣고 P2 재생 해시·기존 PlayMode 61건 무수정 통과를 확인 → `SnapshotTests`(고치기 전 실패: 컴파일 불가 확인) → `Snapshot`/`Capture`/`Restore` → `ChainRushRollbackTests`(음성 대조부터) → 시나리오 1~5 → 어긋나면 보고(질문 4) → 게이트 2회 + 스윕 1회 → 문서.

### 2-10. 남아 있는 다른 후보

- **P3 스파이크**(이 증명 뒤): `spike/` 브랜치에서 NGO 를 우선으로 서버 1 + 클라이언트 1. `manifest.json` 변경은 §0 금지선이라 별도 승인.
- **P2 후속(고스트)**, **T3e 장식**, **T5 보충 모듈**.
- 체감 튜닝: 사용자가 직접 달려 본 피드백 대기(원격이라 보류).

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
