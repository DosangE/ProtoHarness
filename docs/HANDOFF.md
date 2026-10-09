# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-09 (T3c-1 구현 중단 지점). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-09, T3c-1 구현 중)

- 브랜치
  - `dev` = `e3a80b2`(T3b, `origin/dev` 와 같음) + `c9573bf`(T3c-1 요청서 병합, **로컬만, 푸시 안 함**).
  - **`feature/course-t3c-endless`** 에 T3c-1 구현이 커밋·푸시돼 있다(**PlayMode 실패 중, `dev` 병합 금지**). 다음 작업은 이 브랜치에서 이어간다. 중단 지점과 다음 순서는 2-10.
  - 병합된 로컬 브랜치 `feature/course-t3b-generator`, `docs/t3b-proposal`, `docs/t3c-proposal` 은 아직 지우지 않았다.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`: 직선 + 수평 원호 + 종단 곡선, 절대 거리 `S`(double), 원점 이동.
  - 이동: 자유 조향(헤딩), 그립 한계, 경사 속도 보정, 내리막 땅 붙잡기, 가드 부딪힘.
  - 카트라이더식: 드리프트(Shift) → 체인 게이지(최대 2칸) → 체인 액션(Ctrl): 그래플 강화 / 코너 스윙 / 슬링샷.
  - 씬: 시제품 씬 발판 양쪽에 보이는 가드 난간(옆 낙사 없음, 틈 낙사는 유지).
  - T3a 절차 노면 (DECISIONS 2026-10-07): `RoadProfile` · `RoadMeshBuilder` · `RoadPiece`.
  - T3b 시드 생성기 (DECISIONS 2026-10-09): `SeedHash` · `ModuleKind` · `CourseModule` · `CourseTuning` · `CourseGenerator`. 규칙과 시작값은 COURSE 6-6.
  - T3c-1 (기능 브랜치만): 무한 씬이 생성 코스·절차 노면·앵커 풀로 바뀌었다. 2-10 참고.
- 테스트 (`dev` 기준): EditMode 152, PlayMode 46(그중 Device 2). 기능 브랜치는 EditMode 162.

## 2. 다음 작업: T3c-1 — 무한 모드를 생성 코스로 (승인됨 2026-10-09, 구현 중)

T3 목표(COURSE 10절): 무한 모드가 시드로 만든 커브·경사 코스를 달린다. 완료 기준은 시드 20개 봇 1400m 완주(PlayMode)다. 바꿀 것이 많아 **둘로 나누는 것을 제안한다**(2-7 질문 1).

- **T3c-1 (이 요청)**: 무한 씬이 생성기 + 절차 노면 + 앵커 풀로 달린다. 원점 이동을 위치 벡터 기준으로. 시드 2개 봇 1400m 완주.
- T3c-2 (다음 요청): 시드 20개 완주 테스트와 그 실행 방식(병합 조건 시간 문제, 2-6), 적 회전을 트랙 프레임으로, 장식 배치, `Centerline` 조각 선택 개선(필요하면).

아래는 T3c-1 의 §3-2 요청서다. **사용자가 2026-10-09 에 질문 1~6 을 추천안대로 승인했다.** 구현 진행 상황은 2-10.

### 2-1. 목표

무한 씬(`ChainRushEndless.unity`)의 발판 풀(56m 마다 40m 발판 + 16m 틈 반복)을 `CourseGenerator` 가 만든 모듈 열로 바꾼다. 노면은 `RoadPiece` 풀, 그래플 앵커는 앵커 풀로 깔고, 원점 이동은 러너 위치 벡터 기준으로 한다. 시드 2개에서 봇이 1400m 를 완주한다.

### 2-2. 건드릴 것 (기존 파일)

| 파일 | 바꿀 것 |
|---|---|
| `Runtime/ChainRush/Endless/EndlessCourse.cs` (114줄) | 거의 다시 쓴다. 지금은 직선만 잇고(`Step` `:50`, `AppendStraight`), 발판을 앞 방향으로 옮기며(`:62`), 원점 이동 조건이 앞 방향 투영 ≥ 448m 다(`:67`). 바꾼 뒤: 2-4 의 `CourseStream` 을 소유하고, `RoadPiece` 풀·앵커 풀을 S 범위에 맞춰 다시 깔고, 수평 거리 기준으로 원점을 옮긴다. 직렬화 필드 `chunks`·`segmentLength`·`deckLength` 를 빼고 `tuning`(CourseTuning), `seed`, 재질 3개, `anchors` 를 넣는다. 공개 API `Distance`·`RebaseCount`·`ShiftOrigin` 흐름·`SeedTrack`·`ResetCourse`·`CanStartEncounter`·`DistanceToEdge` 는 이름을 유지하고 뜻을 2-4 대로 바꾼다. `RecycledCount` 는 노면 조각 재사용 수, `PoolSize` 는 노면 조각 수 |
| `Runtime/ChainRush/GrappleController.cs` (`:95`) | 후보 고르기에서 비활성 앵커를 건너뛴다(한 줄). 앵커 풀이 안 쓰는 앵커를 끄기 때문이다. 시제품 씬 앵커는 늘 켜져 있어 동작이 같다 |
| `Editor/ChainRush/ChainRushEndlessSceneBuilder.cs` | 새 메뉴 "Upgrade Endless Scene (T3c)": `CourseTuning_Default.asset` 이 없으면 만들고, `Pooled Sector 0~7`(발판·도시 타워 포함, 씬 오브젝트)을 지우고, `Link Anchor` 모양을 복사한 앵커 8개를 만들어 `EndlessCourse`·`GrappleController.anchors`(`:106`)에 연결한 뒤 씬을 저장한다. `CreateEndlessScene` 도 같은 구성을 만들도록 맞춘다 |
| `Tests/PlayMode/ChainRushEndlessTests.cs` (228줄) | 고정 z 에 기대는 테스트를 코스 조회로 바꾼다: 틈 앞 위치(`MovePlayer(29f)` `:157`, `MovePlayer(28f)` `:172`), 풀 크기 8(`:212`), 장거리 봇(`:190-219`, 지금은 직진 + 틈 점프 + 그래플). 봇은 `TrackFollower` 조향 + 다음 틈 종류에 따라 점프·그래플. **동작이 의도적으로 바뀌어서 고치는 것**이고, 단언 목록은 2-5 에 적는다 |
| 씬 `ChainRushEndless.unity` | 위 메뉴로만 바꾼다(YAML 직접 수정 없음, §0). 메뉴 실행·씬 저장은 **그때 따로 승인받는다**(§0, HANDOFF 4절) |
| 새 에셋 `Assets/_Project/Data/CourseTuning_Default.asset` | 위 메뉴가 만든다. 값은 코드 기본값 그대로 |

참고한 기존 자산(§2 검색): 풀·원점 이동 흐름은 지금 `EndlessCourse`, 노면 조각 사용법은 `ChainRushRoadTests`(재질을 만들어 `RoadPiece` 에 넘김), 씬 이전 메뉴 형식은 `ChainRushSceneBuilder` 의 "Add Deck Guards To Open Scene", 봇은 `TrackFollower`. 무한 씬의 `targets` 는 비어 있어(`ChainRushEndlessSceneBuilder.cs:104`) **`CourseTarget` 의 월드 박스 판정은 무한 모드와 무관하다**. 그래서 이번 범위에서 뺐다.

### 2-3. 새로 만들 것 (§1-1 세 줄)

| # | 목적 | 경로 | 형태 |
|---|---|---|---|
| 1 | 생성기 → 중심선 → 노면 조각 범위·앵커 위치를 Unity 오브젝트 없이 계산해 EditMode 로 증명하기 위해 | `Runtime/ChainRush/Endless/CourseStream.cs` | sealed class (순수 C#, `Centerline` 과 같은 형식) |
| 2 | `CourseStream` 의 범위 나누기·앵커 위치·조회를 검증하기 위해 | `Tests/EditMode/CourseStreamTests.cs` | 테스트 |

`Endless/` 는 1개 → 2개. 새 네임스페이스 없음(`ProtoHarness.ChainRush.Endless`).

### 2-4. 설계

**`CourseStream`** (순수 로직)
- `CourseGenerator`·`Centerline` 을 받아, 러너 `S` 앞쪽 300m 까지 모듈을 생성해 `AppendTo` 로 잇고, 뒤쪽은 `TrimBefore` 로 버린다. 살아 있는 모듈 목록을 가진다.
- **노면 범위**: 모듈마다 틈을 빼고 나눈다. 한 범위는 최대 50m(T3a 조각 길이). 범위마다 `RoadProfile`: 반폭 6, 두께 1.2, 가드는 모듈의 `OpenLeft/OpenRight` 반대.
- **앵커 위치**: 앵커가 있는 모듈은 `S = 모듈 시작 + AnchorAlong` 의 중심선 프레임에서 오른쪽 `AnchorSide`, 노면 위 `AnchorHeight`.
- **조회**: `ModuleAt(S)`, 다음 틈까지 거리와 그 종류, 지금 쉼터의 남은 길이.

**`EndlessCourse`**
- 노면 조각 풀 크기는 (앞 300m + 뒤 유지 거리) / 50 + 틈 분할 여유로 정하고 **실행 중에는 늘리지 않는다**. 지금 테스트 `CountObjects()` 가 같다고 단언하므로 그대로 둔다.
- 풀이 모자라면 LogError 후 비활성화한다(§5).
- **원점 이동**: 러너의 수평 거리가 448m 를 넘으면, 러너의 수평 위치만큼 노면 조각·앵커·러너·카메라·적·중심선을 같이 옮긴다(COURSE 7-1). 높이는 옮기지 않는다(R6 로 ±40m 안).
- **시드**: 2-7 질문 2.
- **적 조우 가능 조건** `CanStartEncounter`: 2-7 질문 3. `DistanceToEdge` 는 다음 틈 시작까지 거리.
- **스폰**: 생성기 첫 모듈은 스폰 쉼터 60m(S 0~60)다. 씬 러너 스폰 위치가 그 위에 오는지는 **확인 못 했다**(씬 YAML 통독 금지). 구현 때 MCP 로 스폰 좌표를 읽어 맞추고, 필요하면 S < 0 쪽 노면도 깐다.

### 2-5. 검증 방법

- **EditMode `CourseStreamTests`**
  - 노면 범위가 틈과 겹치지 않고, 틈을 뺀 구간을 빈틈없이 덮으며, 각 범위가 50m 이하다.
  - 열린 가장자리 모듈의 범위만 가드가 빠진다.
  - 앵커 월드 위치가 `Centerline.FrameAt` 로 직접 계산한 값과 같다.
  - 앞 300m 유지와 뒤 버리기 뒤에도 `S` 가 이어진다.
  - 원점 이동 뒤 조회 값이 같다.
- **PlayMode `ChainRushEndlessTests`** (고친 뒤)
  - 전투 4개: 조우 시작 조건만 새 코스 조회로 바꾸고 단언은 그대로.
  - 그래플 재시작: 첫 그래플 틈 앞으로 옮겨서 확인.
  - 장거리 봇: 시드 2개 각각 1400m 완주, `RebaseCount ≥ 3`, `Distance` 역행 없음, 적중 > 3, 풀 크기·오브젝트 수 불변, 재시작 뒤 0.
- **시간 추정 (계산, 실측 아님)**: 장거리 봇 1회가 지금 약 47초(1400m ÷ 10m/s ÷ 배속 3)라서, 시드 2개면 PlayMode 1회가 약 240 → 290초로 늘 것으로 본다.
- **병합 조건(§9-2)**: 컴파일 0 + EditMode + PlayMode(Device 제외) 연속 2회. 입력 장치 경로는 바꾸지 않는다.

### 2-6. T3c-2 로 미루는 것과 이유

- **시드 20개 × 1400m**: 위 계산으로 1회 약 16분이라 병합 조건(2회 연속)에 넣으면 실행이 30분을 넘는다. 별도 카테고리(Device 처럼)로 빼려면 CLAUDE.md §9-2 규칙 변경이 필요하다. 보류 결정 1(병합 조건 줄이기)과 같이 정한다.
- **적 회전**: `EnemyDirector.cs:83` 이 월드 축 회전이다(표현만, 판정 아님).
- **장식(도시 타워)**: 커브 코스에서는 다른 구간 노면과 겹칠 수 있어 겹침 검사가 필요하다.
- **`Centerline` 조각 선택 개선**: 장거리 봇의 `Distance` 역행 없음 단언으로 감시한다. T3c-1 에서 재현되면 멈추고 보고한다.

### 2-7. 사용자에게 물을 것 (추천을 받으면 "ㄱㄱ")

1. **범위**: **T3c-1 / T3c-2 로 나누는 것을 추천**한다(위 2-6). 대안: 한 번에(병합 조건 시간 문제를 먼저 정해야 함).
2. **시드 출처**: **`EndlessCourse` 직렬화 값(기본 1) + 테스트용 setter 를 추천**한다. 같은 시드면 같은 코스라 버그 재현이 쉽다. 대안: 매 판 무작위(랭킹·일일 시드는 그때 정함).
3. **적 조우 자리**: **쉼터 모듈 안 + 남은 쉼터 길이가 조우 시간 동안 달릴 거리보다 길 때를 추천**한다(COURSE 5-1 "쉼터 = 적 조우 자리", 커브·경사 위 전투를 피함). 쉼터 40~60m 라 조우 시작 기회는 지금보다 줄어든다(필요 거리 약 28.5m = 10m/s × 2.35s + 5, 계산). 대안: 다음 틈까지 거리(지금 방식, 커브 위에서도 조우).
4. **도시 타워 장식**: **이번에는 빼는 것을 추천**한다(T3c-2 에서 겹침 검사와 함께). 대안: 노면 옆 고정 간격으로 두되 겹침 검사 없이.
5. **옛 발판 풀(`Pooled Sector 0~7`)**: **이전 메뉴가 씬에서 지우는 것을 추천**한다(씬 오브젝트이고 에셋이 아님, 되돌리기는 git). 대안: 끄고 남김.
6. **앵커 연결**: **씬에 앵커 8개 풀 + `GrappleController` 가 꺼진 앵커를 건너뛰는 것을 추천**한다(직렬화 참조 유지). 대안: `GrappleController` 에 앵커 등록 API 를 새로 만듦(공개 API 추가).

### 2-8. 안 하는 것

2-6 의 T3c-2 항목, 시제품 씬(`ChainRushPrototype.unity`)과 `CourseTarget`, 보충 모듈(T5), `TrackDefinition`(T4), 튜닝 값 변경, `manifest.json`·`ProjectSettings` 변경.

### 2-9. 승인 뒤 시작 순서

1. 이 요청서(`docs/t3c-proposal`)를 `dev` 에 병합한다(문서 검증, §9-2).
2. `dev` 에서 `feature/course-t3c-endless` 를 만든다.
3. 구현 → EditMode → 이전 메뉴 실행·씬 저장은 **그때 승인** → PlayMode 2회.

### 2-10. 진행 상황과 다음 순서 (2026-10-09 중단 지점)

**한 것** (`feature/course-t3c-endless`)
- 새 파일: `Endless/CourseStream.cs`, `Tests/EditMode/CourseStreamTests.cs`(10건).
- 수정
  - `EndlessCourse`: 노면 조각 32개 풀, 앵커 풀, 수평 거리 448m 원점 이동, 쉼터 조우, 시드 setter, 스폰 앞 10m 노면.
  - `GrappleController`: 꺼진 앵커는 후보에서 뺀다(한 줄).
  - `ChainRushEndlessSceneBuilder`: 메뉴 "Upgrade Endless Scene (T3c)" 추가, `CreateEndlessScene` 도 같은 구성.
  - `ChainRushEndlessTests`: 틈 근처 조우 거절 z 29 → 40, 그래플 재시작은 그래플 틈이 있는 시드를 찾아서, 장거리 봇은 `TrackFollower` 조향 + 시드 1·2.
- 씬·에셋: 사용자 승인(2026-10-09 "이전 메뉴 실행 승인")으로 이전 메뉴를 실행하고 저장했다.
  - `Pooled Sector 0~7` 삭제, 루트 `Grapple Anchors` 아래 앵커 8개, `CourseTuning_Default.asset` 생성.
  - grep 확인: `Pooled Sector` 0개, `Grapple Anchor N` 8개, 옛 `chunks` 필드 없음.
- 스폰 위치는 씬 grep 으로 `(0, 1.05, 5)` = S 5 를 확인했다.

**검증 (에디터 6000.3.19f1)**
- 컴파일: Error 0.
- EditMode: `testcasecount="162" result="Passed" passed="162" failed="0"` (15:43:40~15:43:45 KST).
- **PlayMode(Device 제외) 1회차: `testcasecount="44" result="Failed(Child)" passed="38" failed="6"`** (15:45:07~15:48:13).
  - 무한 4건. 장거리 봇 원문: `seed 1: failed at 3.0 m; player=(0.00, -12.32, 7.97); health=3`. 전투 3건: `Expected: True But was: False`(`BeginEncounter`).
  - 연출 2건(`Animation_JumpAndGrapple_ChangesArmPoseWithoutMovingMotor`, `Presentation_RunPauseRestart_SynchronizesAudioAndJoints`): `Expected: True But was: False`, `Expected: greater than 0.0f But was: 0.0f`. 같은 원인(러너 추락)으로 보이지만 **확인 못 했다**.

**막힌 곳: 다시 지은 노면 조각의 콜라이더가 비어 있다**
- 진단: `Unity_RunCommand` 로 Play 모드에 들어가 읽기·실험했다. 씬은 저장하지 않았고, 끝난 뒤 편집 모드·`dirty=False` 를 확인했다.
  - 씬을 불러온 직후 노면 콜라이더는 정상이다(조각 0: 정점 296, S −10~25).
  - `game.StartRun()` 뒤에는 **모든 노면 콜라이더의 `sharedMesh` 가 null** 이고, (3, 5, 5) 에서 아래로 쏜 레이가 맞히지 못한다. `StartRun` → `ResetCourse` 가 조각을 숨겼다가 다시 짓기 때문이다.
- 실험 (같은 조각, Play 모드)

  | 실험 | 결과 |
  |---|---|
  | 켜진 상태에서 할당 | 정상 296 |
  | 꺼진 상태에서 null → 같은 메시 | **null**, 켜도 null |
  | 꺼진 상태에서 null → 메시 다시 채움 → 할당 | 정상 296 |

- 결론: `RoadPiece.Build`(T3a)는 루트를 맨 마지막(`root.SetActive(true)`)에 켜서, 풀에서 숨겼다 다시 짓는 조각이 콜라이더를 잃는다. T3a 테스트는 다시 짓지 않아 이 경로를 잡지 못했다. 엔진 쪽 이유는 **확인 못 했다**.
- 제안한 수정(**사용자 승인 대기**, 계획 밖 파일 2개)
  1. `Tests/PlayMode/ChainRushRoadTests.cs` 에 "지음 → 숨김 → 다시 지음 뒤 노면 콜라이더 메시가 있고 아래 레이가 노면을 맞힌다" 1건을 추가한다. 고치기 전 실패를 먼저 확인한다(§4-4).
  2. `Runtime/ChainRush/Track/RoadPiece.cs` 의 `Build`: 콜라이더 메시를 붙이기 전에 루트를 켠다(몇 줄).

**다음 순서**
1. 위 수정 승인을 받는다. 승인 없이 `RoadPiece`·`ChainRushRoadTests` 를 고치지 않는다(§3-3).
2. 테스트 추가 → 실패 확인 → 수정 → EditMode → PlayMode(Device 제외) 연속 2회.
3. 그래도 실패하면 원문을 남기고 멈춘다. 장거리 봇의 `RebaseCount ≥ 3` 은 커브 때문에 수평 이동이 짧아 못 미칠 수 있다(계산 안 함). 그러면 이유와 함께 보고하고 단언 변경을 합의한다.
4. 통과하면 DECISIONS(T3c-1, 위 실패 기록 포함)·COURSE 10절·ARCHITECTURE·이 문서를 갱신하고 병합을 요청한다.

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

- `Unity_RunCommand` 로 Play 모드 진단: `EditorApplication.EnterPlaymode()` 뒤 브리지가 다시 붙으면 다음 호출에서 Play 중인 씬을 읽을 수 있다. 끝나면 `ExitPlaymode()` 로 나오고 `isDirty` 를 확인한다. 명령 코드가 `Unity.AI.Assistant...Editor` 네임스페이스 안에 들어가서 `Mesh` 가 네임스페이스로 해석된다 → `UnityEngine.Mesh` 로 쓴다(2026-10-09).
- `Unity_RunCommand` 는 `System.Reflection` 네임스페이스를 쓰면 실행을 거부하고(`unauthorized namespaces`), `System.Diagnostics.Stopwatch` 는 참조가 없어 컴파일되지 않는다. 시간은 `Time.realtimeSinceStartupAsDouble`, 새 코드 로드 확인은 공개 타입·메서드 이름 조회나 `Assembly.Location` 기록 시각으로 한다(2026-10-09).
- 이 머신(`C:/Users/User/Desktop/PCUBE/ProtoHarness`)의 에디터는 **6000.3.19f1** 이다(결정 버전 25f1 아님). 그래서 `ProjectVersion.txt`·`packages-lock.json`·`ProjectSettings.asset`(iOS 발열 설정 3줄 삭제)이 수정으로 보인다. 로컬 환경 차이라 커밋하지 않는다(사용자 2026-10-04). 검증 보고에는 19f1 에서 돌렸다고 적는다.
- 테스트 결과 XML 은 Unity 의 `Path.GetTempPath()/ChainRush-PlayMode-results.xml` 하나에 덮어쓴다(EditMode 실행도 같은 파일). 이 머신은 `C:\Users\User\AppData\Local\Temp\` (2026-10-07 확인). 실행마다 따로 보관하려면 끝날 때 복사한다.
- Unity MCP: 도메인 리로드마다 브리지가 몇 초 끊긴다(`Unity not detected (no fresh discovery files found)`). `~/.unity/mcp/connections/bridge-*.json` 이 다시 생기면 재시도한다. 에디터가 백그라운드면 스크립트를 자동 임포트하지 않을 수 있다 → `AssetDatabase.Refresh()`. 테스트 전에 새 코드가 로드됐는지(리플렉션 등) 확인한다.
- `.codex/agents/` 가 `git status` 에 수정으로 보이면 `autocrlf` 표시다(내용은 HEAD 와 같음). 손대지 않는다.
- `index/symbols.tsv` 는 낡았다(git-head `9f5a96f`). B모드로 쓰기 전에 재생성한다(§6-2).
- GitHub Desktop 이 켜져 있으면 `.git/index.lock` 이 남을 수 있다. 실행 중인 `git.exe` 가 없을 때만 지운다(2026-10-05 한 번 발생).
- 씬 저장을 `Unity_RunCommand` 로 하려면 매번 승인받는다(§0, §3-3). T2a 의 승인은 1회성이었다.
- 도메인 리로드 때 `Deleting invalid font reference.` 경고가 나온다(P1-3a 부터, 원인 미확인). 변경과 무관.
