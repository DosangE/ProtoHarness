# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-08 (T3b 구현·검증 완료, 다음은 T3c). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-08)

- 브랜치: T3b 는 `dev` 에 병합됐다(`f206d7f`, 로컬만 — `origin/dev` 에는 아직 푸시하지 않았다). 기능 브랜치 `feature/course-t3b-generator` 는 남아 있다.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`: 직선 + 수평 원호 + 종단 곡선, 절대 거리 `S`(double), 원점 이동.
  - 이동: 자유 조향(헤딩), 그립 한계, 경사 속도 보정, 내리막 땅 붙잡기, 가드 부딪힘.
  - 카트라이더식: 드리프트(Shift) → 체인 게이지(최대 2칸) → 체인 액션(Ctrl): 그래플 강화 / 코너 스윙 / 슬링샷.
  - 씬: 두 씬 발판 양쪽에 보이는 가드 난간(옆 낙사 없음, 틈 낙사는 유지).
  - **T3a 절차 노면** (DECISIONS 2026-10-07): `RoadProfile` · `RoadMeshBuilder` · `RoadPiece`.
  - **T3b 모듈 카탈로그 + 시드 생성기** (DECISIONS 2026-10-08): `SeedHash` · `ModuleKind` · `CourseModule` · `CourseTuning`(SO, `.asset` 없음) · `CourseGenerator`. 순수 로직이고 씬·`EndlessCourse` 와 연결돼 있지 않다.
- T3c 합의 요청서(아래 2절)는 승인됐고(2026-10-08), 구현은 `feature/course-t3c-procedural` 에서 한다.
- **씬 코스는 아직 직선·평지다.** 절차 노면은 `ChainRushRoadTests`(x = 300)에서만, 생성기는 EditMode 테스트에서만 쓴다. 씬 연결은 T3c.
- 테스트: EditMode 161, PlayMode 46(그중 Device 2). 병합 조건 실행은 Device 제외 44건, 약 225~240초/회.

## 2. 다음 작업: T3c — 무한 모드 전환 (합의 요청서, 승인됨 2026-10-08)

COURSE 10절 T3c: 무한 모드가 `CourseGenerator` 로 만든 커브·경사·틈 코스를 달린다. 완료 기준: **시드 20개에서 봇이 1400m 완주**(PlayMode). 아래는 §3-2 형식의 합의 요청서다. **사용자가 2026-10-08 "모두 동의한다" 로 2-6 의 질문 1~7 을 전부 추천대로 승인했다.** 구현은 이 범위 안에서만 한다. 씬·프리팹을 건드리므로 이 브랜치는 동시에 1개만 둔다(§9-2).

### 2-1. 목표

`ChainRushGame` 이 시드로 만든 모듈 열을 앞쪽 약 300m 씩 스트리밍하고(중심선 이어 붙이기 + `RoadPiece` 풀 + 그래플 앵커 풀), 지나간 것은 되돌려 쓰며, 코스가 멀어지면 원점을 위치 벡터만큼 옮기는 **새 무한 코스 씬**을 만든다. 기존 `ChainRushEndless` 씬과 그 테스트는 그대로 둔다(2-6 질문 1).

### 2-2. 건드릴 것 (기존 파일)

| 파일 | 바꾸는 것 | 이유 |
|---|---|---|
| `Runtime/ChainRush/ChainRushGame.cs:21` | 직렬화 필드 `endlessCourse` 의 타입 `EndlessCourse` → `CourseStream` | 두 코스 구현을 한 필드로 받는다. 기존 씬의 참조는 컴포넌트 인스턴스를 가리키므로 `EndlessCourse` 가 `CourseStream` 을 상속하면 그대로 유효하다(**씬 로드로 확인한다**, 지금은 확인 못 함) |
| `Runtime/ChainRush/Combat/EnemyDirector.cs:14` | 같은 필드 타입 변경 | 위와 같다 |
| `Runtime/ChainRush/Combat/EnemyDirector.cs:83` | 취약 상태 적의 회전을 월드 축 `Quaternion.Euler(0,0,roll)` → 트랙 프레임 기준(`LookRotation(frame.Forward)` × 롤) | 커브에서 적이 월드 +z 를 보는 것을 막는다. 직선(+z)에서는 값이 같다 |
| `Runtime/ChainRush/GrappleController.cs:97-100` | `SelectCandidate` 가 비활성 앵커를 건너뜀 | 풀에서 쉬는 앵커가 예전 자리에서 후보로 잡히지 않게. 기존 씬 앵커는 항상 활성이라 동작이 같다 |
| `Runtime/ChainRush/Endless/EndlessCourse.cs:6` | `: MonoBehaviour` → `: CourseStream`, 기존 공개 멤버에 `override` 만 붙임. 로직 불변 | 같은 베이스 |

기존 파일 5개 수정이라 합의 대상(§3-1 규모 기준)이다. 공개 API: 기존 멤버 시그니처는 그대로, 베이스로 올라간 것뿐이다.

참고한 기존 자산(§2 검색 3연타, 2026-10-08): `Grep "ProceduralCourse|CourseStream"` → 없음. `Glob Endless/*.cs` → `EndlessCourse.cs` 하나. 가장 비슷한 `EndlessCourse.cs`(114줄)를 끝까지 읽었다: 풀 8칸 재사용(`:59-64`), 원점 이동(`:65-75`, 조건은 앞 방향 투영 ≥ 448m), 쉼터 판정(`:86-103`). 같은 형식으로 맞춘다: `Step()` 은 `ChainRushGame.FixedUpdate` 만 부르고(`ChainRushGame.cs:171`), `SeedTrack`/`ResetCourse`/`Distance`/`CanStartEncounter` 를 같은 의미로 제공한다.

### 2-3. 새로 만들 것 (§1-1 세 줄)

경로는 `Assets/_Project/Scripts/` 기준. 네임스페이스는 폴더를 따른다(`ProtoHarness.ChainRush.Endless` 등).

| # | 목적 | 경로 | 형태 |
|---|---|---|---|
| 1 | `ChainRushGame`/`EnemyDirector` 가 어느 코스 구현이든 같은 방식으로 부르게 하기 위해 (`Distance`, `Step`, `SeedTrack`, `ResetCourse`, `CanStartEncounter`) | `Runtime/ChainRush/Endless/CourseStream.cs` | MonoBehaviour (abstract) |
| 2 | 생성기 모듈을 스트리밍해 중심선·노면 조각·앵커 풀·원점 이동을 관리하기 위해 | `Runtime/ChainRush/Endless/ProceduralCourse.cs` | MonoBehaviour |
| 3 | 새 씬과 `CourseTuning_Default.asset` 을 만드는 메뉴 (`ChainRushEndlessSceneBuilder` 와 같은 패턴) | `Editor/ChainRush/ChainRushProceduralSceneBuilder.cs` | static class (에디터) |
| 4 | 절차 코스 전용 씬. `ChainRushEndless` 씬을 복사해 낡은 `Endless World` 를 빼고 `Procedural World` 를 넣는다 | `Scenes/ChainRushProcedural.unity` | 씬 (빌더가 생성) |
| 5 | 생성기 튜닝 값을 코드 수정 없이 바꾸기 위해 (DECISIONS 2026-10-02 SO 형식 ⑥ 이름) | `Data/CourseTuning_Default.asset` | ScriptableObject 인스턴스 (빌더가 생성) |
| 6 | 중심선을 따라 달리고 틈 앞에서 뛰고 그래플·공격을 쓰는 봇. 시드 지정 | `Tests/PlayMode/CourseBot.cs` | 테스트 헬퍼 (`IInputSource`, `TrackFollower` 와 같은 형식) |
| 7 | 완주·재현·풀 안 늘어남·낙사를 검증하기 위해 | `Tests/PlayMode/ChainRushProceduralTests.cs` | 테스트 |

`Endless/` 는 1개 → 3개. 새 asmdef·새 네임스페이스 없음.

### 2-4. 설계

**스트리밍** (`ProceduralCourse.Step`, 매 틱 `ChainRushGame.FixedUpdate` 에서)
- 대기 모듈 큐: 플레이어 `S` 앞쪽 `aheadDistance`(300m)까지 `generator.Next()` 로 받아 `module.AppendTo(track)` 로 중심선에 붙인다. 뒤쪽 `behindDistance`(60m) 밖이 된 모듈은 큐에서 빼고 `track.TrimBefore` 한다.
- 모듈마다 노면 조각 만들기: 틈이 없으면 `[StartS, EndS]` 한 구간, 틈이 있으면 `[StartS, GapStartS]` 와 `[GapStartS + GapLength, EndS]` 두 구간(틈은 노면이 없다). 한 구간이 50m 를 넘으면 50m 이하로 나눈다(50m 조각 1개를 만드는 데 1.5~2.2ms, DECISIONS T3a). **한 틱에 `Build` 는 최대 1회**. 시작과 재시작 때만 앞쪽 전부를 한꺼번에 만든다. 프로파일은 `RoadProfile(6, 1.2, !LeftOpen, !RightOpen)`.
- 조각 풀 24칸: 앞 300m + 뒤 60m 에 필요한 조각의 최악(조각 최소 15m)이 약 24개. 풀이 모자라면 `InvalidOperationException` 으로 크게 깨진다(§5).
- 앵커 풀 10칸(`GrappleController.anchors` 와 같은 배열): 그래플 틈 모듈이 큐에 들어오면 비어 있는 앵커를 `FrameAt(AnchorS)` 의 `TransformPoint(AnchorOffset, AnchorHeight, 0)` 로 놓고 켠다. 앵커가 지나가 `S` 가 플레이어보다 30m 이상 뒤이고 **지금 붙어 있지 않으면**(`RacerState.AnchorIndex` 확인) 끄고 되돌린다. 크기 근거(계산): 그래플 모듈의 최소 길이는 런웨이 15 + 틈 14 + 런웨이 15 = 44m, 앵커가 살아 있는 구간은 앞 300m + 뒤 30m = 330m 라 최대 ⌈330 ÷ 44⌉ = 8개, 여유 2 를 더해 10칸. 모자라면 `InvalidOperationException`.
- **원점 이동**: 플레이어 수평 위치(`x, z`)가 원점에서 400m 이상이면 `-(x, 0, z)` 만큼 모두 옮긴다(플레이어·카메라·적·중심선·조각 루트·앵커·체인 시각물; 기존 `EndlessCourse.cs:65-75` 와 같은 대상에 조각·앵커만 더함). 조건이 지금의 "앞 방향 투영 ≥ 448m" 에서 위치 벡터로 바뀐다(COURSE 7-1). 이동 뒤 `Physics.SyncTransforms()`.
- `Distance` = `max(0, 플레이어 S − 5)`. `DistanceToEdge` = 큐에서 플레이어 앞의 가장 가까운 틈 시작까지의 `S` 거리(틈이 없으면 앞쪽 끝까지), 틈 위면 −1. `CanStartEncounter` 는 지금과 같은 식(접지 + 그 거리 > `max(10, 속도) × 지속시간 + 5`).
- **시드**: `ProceduralCourse.Seed` 를 노출한다. `ResetCourse` 가 `new CourseGenerator(seed, tuning)` 을 만들고 큐·조각·앵커·원점을 처음 상태로 되돌린다(`track.Clear()` 로 중심선이 생성 원점으로 돌아가므로 플레이어 스폰과 맞다). 기본은 런마다 새 무작위 시드(2-6 질문 2), 테스트는 `Seed` 를 지정한다.
- 생성기는 0 번부터만 만들 수 있으므로 시작 때 한 번 만들고 `Next()` 만 부른다. `InvalidOperationException`(막다른 길)은 잡지 않는다.

**봇** (`CourseBot`): `TrackFollower` 로 중심선을 따라가고, 큐의 다음 틈 앞 4.5m 이내에서 점프(`EndlessTests.cs:200` 과 같은 규칙), 공중에서 높이 > 2.6m 이면 `TryAttach`(`:201`), 적이 취약하면 `Attack`(`:202`). 틈을 지나지 못하는 설계 문제(그래플 틈 14~18m, 앵커 좌우 ±2m)가 있으면 봇 실패로 드러난다. 그때는 **원문을 보고하고** 값(`.asset`)만 조정해 DECISIONS 에 남긴다(코드 규칙은 바꾸지 않는다).

### 2-5. 검증 방법

- **컴파일**: Unity MCP, Console Error 0.
- **EditMode**: 기존 161 그대로(새 EditMode 없음; 순수 로직은 T3b 에서 끝났다).
- **PlayMode `ChainRushProceduralTests`** (새 씬): ① 시드 0·1·2 에서 봇이 1400m 완주(실행 중 접지 놓침·가드 부딪힘 수와 최소 거리 로그) ② 같은 시드로 재시작하면 같은 모듈 열·같은 앵커 위치, 다른 시드는 다르다 ③ 1400m 동안 원점 이동 ≥ 3회, 조각·앵커 풀 크기와 오브젝트 수가 늘지 않는다 ④ 그래플 틈에서 아무것도 안 하면 낙사로 런이 끝난다(H < −12) ⑤ 풀에서 쉬는 앵커는 `Candidate` 로 잡히지 않는다 ⑥ 커브 위 적 조우가 정상 진행한다(공격 가능·타임아웃 피해 1회).
- **20 시드 스윕** (`Sweep` 카테고리, 게이트 제외, 2-6 질문 5): 시드 0~19 봇 1400m 완주. 결과 XML 값을 DECISIONS 에 남긴다.
- **회귀**: 기존 PlayMode 44건은 무수정으로 통과해야 한다(특히 `ChainRushEndlessTests`·`PresentationTests`·`SteeringTests` 가 쓰는 `ChainRushEndless` 씬). 병합 조건(§9-2)은 컴파일 0 + EditMode + PlayMode(Device·Sweep 제외) 연속 2회를 지금 규칙대로 돌린다.
- 시간(계산): 봇 1400m 는 `1400 ÷ 10 m/s ÷ 3배속 ≈ 47초`/시드(경사·드리프트 보정 전). 게이트 3시드는 +약 2.4분/회(지금 약 225초 → 약 370초), 20시드 스윕은 약 16분.
- 수동 확인이 필요한 것: 눈으로 본 모양(조각 이음매, 앵커 위치, 카메라). `Unity_Camera_Capture` 로 몇 장 찍어 DECISIONS 에 적는다. 체감 튜닝은 하지 않는다.

### 2-6. 사용자에게 물을 것 (추천을 받으면 "ㄱㄱ")

1. **씬 전환 방식**: **새 씬 `ChainRushProcedural` + 새 컴포넌트 추천.** 기존 `ChainRushEndless` 씬·`EndlessCourse`·테스트 3개 클래스(`EndlessTests` 6, `PresentationTests`, `SteeringTests`)를 안 건드려 회귀가 그대로 증명되고, 씬을 만든 선례(Prototype → Endless 복사)와 같다. 낡은 직선 코스는 T3d 에서 지운다. 대안: 기존 씬을 제자리에서 바꾼다(코드 한 벌이지만 씬 YAML 편집 + 위치에 기대는 테스트 3개 클래스를 새 배치에 맞게 고쳐야 한다. "통과시키려고 테스트를 고치지 않는다" 규칙과 부딪히기 쉽다).
2. **시드**: **런마다 새 무작위 시드 추천**(프리 런의 변화), 테스트와 재현용으로 `Seed` 를 지정하고 읽을 수 있다. 대안: 고정 시드 1개(매번 같은 코스).
3. **장식**: **T3c 에서는 뺀다 추천.** 지금 직선 코스의 네온 타워는 직선 옆에만 놓인다. 커브·언덕 코스에서 타워가 다른 도로 구간과 겹치지 않게 놓는 일은 별도 설계라 도로·가드만 떠 있는 모양이 된다. 대안: 조각마다 단순 타워 2개를 도로 양옆 25m 에 붙인다.
4. **`CourseTarget` 판정의 트랙 프레임화**: **T4 로 미루기 추천.** 무한 모드 씬은 `targets` 배열이 비어 있어(`ChainRushEndlessSceneBuilder.cs:104`) 영향이 없고, 쓰는 곳은 직선인 유한 코스뿐이다. 곡선 유한 코스(T4 서킷)가 생길 때 고친다. 적 회전(`EnemyDirector.cs:83`)만 이번에 고친다.
5. **게이트에 넣을 봇 시드 수**: **게이트 3시드 + 20시드 스윕은 별도 카테고리 추천.** `Sweep` 카테고리를 만들고 메뉴 `Run Course Sweep` 를 더한다. CLAUDE.md §9-2 에 "Sweep 은 병합 조건에서 빼고, 완료 기준을 증명하는 실행으로 따로 1회 돌려 결과를 남긴다" 한 문장을 더하고 DECISIONS 에 기록한다(`Device` 분리와 같은 방식, DECISIONS 2026-10-04). 대안: 20시드 전부 게이트에 넣는다(+약 16분/회, 2회). 보류 결정 1(병합 조건 줄이기)과 겹치니 함께 결정해도 된다.
6. **비활성 앵커 건너뛰기**(`GrappleController.cs:97-100`): **수정 추천.** 대안: 풀의 앵커를 멀리(−1000m) 치워 둔다(고치지 않지만 어색하다).
7. **봇이 틈을 못 넘을 때**: **`CourseTuning_Default.asset` 값(틈 길이·앵커 위치)만 조정하고 DECISIONS 에 남기는 것을 미리 승인**해 주시면 멈추지 않고 진행한다. 코드 규칙(R1~R9)이나 모터·그래플은 바꾸지 않는다. 대안: 실패하면 멈추고 보고한다.

### 2-7. 안 하는 것

기존 `ChainRushEndless` 씬·`EndlessCourse` 로직·그 테스트 수정, 낡은 직선 코스 삭제(T3d), 장식 타워(질문 3), `CourseTarget` 변경(질문 4), 유한 모드·`TrackDefinition`(T4), 보충 모듈(T5), 수치 체감 튜닝, 새 asmdef·패키지·ProjectSettings 변경.

### 2-8. 승인이 따로 필요한 단계 (§0, §3-3)

- 새 씬·`.asset` 을 만드는 메뉴 실행(`Unity_RunCommand` 로 `ExecuteMenuItem`)은 상태를 바꾸므로 **실행 전에 코드 전문을 보여주고 승인**받는다.
- 씬 저장은 빌더 메뉴 안에서 한다(`Unity_RunCommand` 가 직접 저장하지 않는다).

### 2-9. 승인 뒤 시작 순서

1. `docs/RULES/CONVENTIONS.md`, `VERIFICATION.md`, `BRANCHING.md` 를 읽는다.
2. 이 요청서(`docs/t3c-proposal`)가 `dev` 에 병합됐는지 확인한다. 안 됐으면 사용자에게 먼저 묻는다.
3. `dev` 에서 `feature/course-t3c-procedural` 브랜치를 만든다.
4. 순서: `CourseStream` + `EndlessCourse` 상속 + 필드 타입 변경 → **기존 PlayMode 44건 무수정 통과 확인(회귀 기준선)** → `ProceduralCourse` → 빌더 → 씬 생성(승인) → 봇·테스트 → 스윕.

### 2-10. 기존 부채와의 관계

- `Centerline` 조각 선택(앞에서부터 첫 번째로 지나지 않은 조각): 생성기 R7 이 비이웃 도로를 20.6m(상자 기준) 이상 떼어 놓는다. PlayMode 봇이 접지·`S` 단조 증가를 매 틱 단언해 실제로 틀리는지 본다. 틀리면 멈추고 보고한다.
- 조각 투영 비용: 큐가 길어진 중심선(약 12~20 조각)에서 `Step` 한 틱의 시간을 테스트가 로그로 남긴다.
- `CourseTarget` 월드 박스: 질문 4 (T4 로 미룸).

## 3. 보류된 사용자 결정

| # | 주제 | 상태 | 참고 |
|---|---|---|---|
| 1 | 병합 조건 검증 줄이기 | 사용자가 질문함("작업한 부분만 검증하면 안 되나"). 제시안: A 그대로 / **B 추천: 전체 1회 + 2회차는 바뀐 영역 클래스만** / C 영향 범위만 / D 1400m 장거리 테스트 배속 3 → 5. **답 대기.** T3a 는 A(전체 2회)로 검증했다. 실행 1회가 약 240초로 늘었다. 바꾸면 `CLAUDE.md` §9-2 + DECISIONS, `docs/` 브랜치, 문서 검증(§9-2) | 2026-10-05 대화 |
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
