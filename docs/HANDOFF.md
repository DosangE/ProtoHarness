# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-08 (T3c 병합, T4 합의 요청서). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-08)

- 브랜치: T3c 까지 `dev` 에 병합돼 있다(`01ac454`, `origin/dev` 에는 푸시 안 함). T4 합의 요청서(아래 2절)는 승인됐고(2026-10-08), 구현은 `feature/course-t4-circuit` 에서 한다.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`, 이동(자유 조향·그립·경사·땅 붙잡기·가드), 드리프트 → 체인 게이지 → 체인 액션(그래플 강화 / 코너 스윙 / 슬링샷), 두 씬 발판 가드 난간.
  - T3a 절차 노면(`RoadProfile` · `RoadMeshBuilder` · `RoadPiece`), T3b 시드 생성기(`SeedHash` · `ModuleKind` · `CourseModule` · `CourseTuning` · `CourseGenerator`).
  - **T3c** (DECISIONS 2026-10-08 "코스 T3c"): `CourseStream`(베이스) ← `EndlessCourse`(직선 풀) / `ProceduralCourse`(생성기 스트리밍, 조각 24칸 · 앵커 10칸 · 원점 이동 400m). 새 씬 `ChainRushProcedural`(+ `Data/CourseTuning_Default.asset`)을 메뉴 `Create Procedural Scene` 이 만들었다. 시드 20개 봇 1400m 완주. `RoadPiece` 의 숨긴 같은 프레임 재빌드 콜라이더 버그를 고쳤다.
- **기존 `ChainRushEndless` 씬(직선)은 그대로 있다.** `EndlessTests`·`PresentationTests`·`SteeringTests` 가 이 씬을 쓴다. 정리는 T3d.
- 테스트: EditMode 161, PlayMode 57(Device 2 + Sweep 1 + 게이트 54). 병합 조건 실행은 Device·Sweep 제외 54건, 약 377초/회. 20 시드 스윕(`Run Course Sweep`) 약 933초.

## 2. 다음 작업: T4 — 수제 서킷 + 랩 (합의 요청서, 승인됨 2026-10-08)

COURSE 10절 T4: `TrackDefinition`(SO), 닫힘 검증, 에디터 빌더, 체크포인트·랩. 완료 기준: **서킷 1개를 봇이 3랩 완주하고 랩 카운트가 정확하다**(PlayMode). 아래는 §3-2 형식의 합의 요청서다. **사용자가 2026-10-08 "ㄱㄱ" 로 2-6 의 질문 1~9 를 전부 추천대로 승인했다.** 구현은 이 범위 안에서만 한다. 씬을 건드리므로 씬·프리팹 브랜치는 동시에 1개만 둔다(§9-2).

### 2-1. 목표

손으로 정의한 **닫힌 순환 트랙**(속도전: 카트라이더식 랩타임, DESIGN 6·8절)을 데이터(`TrackDefinition` SO)로 만들고, 한 명의 러너가 그 위를 3랩 달려 랩 시간·합계 시간을 재는 **새 씬**을 만든다. 상호작용(아이템·접촉·적)은 없다. 무한 모드와 기존 직선 유한 씬은 건드리지 않는다.

### 2-2. 건드릴 것 (기존 파일)

| 파일 | 바꾸는 것 | 이유 |
|---|---|---|
| `Runtime/ChainRush/Track/Centerline.cs` | ① **루프 모드** 추가: `Close(tolerance)` 로 끝 = 시작(위치·방향·높이·경사 0)을 검증하고 `IsLoop` 를 켠다. 루프에서는 `FrameAt(s)` 가 `s` 를 한 바퀴 길이로 감는다. ② **초점**: `SetFocus(s)`/`ClearFocus()`. 초점이 있으면 `Project`/`Frame` 이 초점 ±`FocusWindow`(기본 150m) 안의 조각만 보고, **가장 가까운 조각**을 고른다. 없으면 지금 규칙(앞에서부터 첫 번째로 지나지 않은 조각) 그대로 | 지금 규칙(`:248-262`)은 서킷의 귀환 직선을 출발 조각으로 오인한다(출발 조각의 "뒤쪽"에 놓인 점은 `along ≤ Length` 라 먼저 걸린다). 호출부가 6개 파일 12곳(`FollowCamera` 2, `RunnerMotor` 5, `EnemyDirector` 2, `GrappleController` 1, 코스 2. `ChainRushGame` 내부 호출은 따로)이라 호출을 바꾸지 않고 `Centerline` 안에서 해결한다. 기존 호출·직선·무한 모드는 초점·루프를 쓰지 않으므로 동작이 같다 |
| `Runtime/ChainRush/ChainRushGame.cs` | 선택 필드 `circuit`(`CircuitRace`). 있으면: `Awake` 에서 중심선을 서킷에서 받고(`:106-108`), `FixedUpdate` 맨 앞에서 `track.SetFocus(직전 S)`, 완주 판정을 `S ≥ finishZ`(`:167`) 대신 3랩 완료로, `StartRun` 이 서킷을 초기화한다. 없으면 지금과 같다 | 유한 모드의 완주 판정이 직선(`finishZ`) 전용이다 |
| `Runtime/ChainRush/ChainRushHud.cs` | 서킷 분기: 윗줄 `LAP 2 / 3  01:23.4`, 제목(`:91`), 시작 안내 문구(`:112,117,122,127,128`), 종료 문구(`:218` `ROUTE COMPLETE.` → `RACE COMPLETE.` + 랩 시간 표) | 지금은 무한/유한 두 갈래만 있다 |
| `Tests/PlayMode/TrackFollower.cs` | 변경 없음 (그대로 재사용) | — |

기존 파일 3개 수정 + 공개 API 추가(`Centerline.Close`/`IsLoop`/`SetFocus`/`ClearFocus`)라 합의 대상(§3-1)이다. 기존 시그니처는 바뀌지 않는다.

참고한 기존 자산(§2 검색 3연타, 2026-10-08): `Grep "TrackDefinition|LapCounter|CircuitRace|Checkpoint"` → 없음(`COURSE.md` 문서에만). `Glob Track/*.cs` → 11개(`Centerline`·`CourseModule`·`RoadPiece` 등). 가장 비슷한 `ProceduralCourse.cs`(앵커 풀·조각 깔기)와 `CourseTuning.cs`(SO 형식·`TryValidate`)를 끝까지 읽었다. SO 는 DECISIONS 2026-10-02 형식(`CreateAssetMenu`, 읽기 전용 프로퍼티, `OnValidate` + `TryValidate`)을 따른다. 씬 만들기는 `ChainRushProceduralSceneBuilder` 와 같은 패턴(복사 저장 → 낡은 월드 제거 → 새 월드 연결)을 쓴다.

### 2-3. 새로 만들 것 (§1-1 세 줄)

경로는 `Assets/_Project/Scripts/` 기준.

| # | 목적 | 경로 | 형태 |
|---|---|---|---|
| 1 | 서킷을 직선·원호·종단 곡선 조각 목록 + 앵커 + 체크포인트 + 시작 칸 + 랩 수로 데이터화하고 닫힘을 검증하기 위해 | `Runtime/ChainRush/Track/TrackDefinition.cs` | ScriptableObject (`TrackSegment`, `TrackAnchor` 직렬화 struct 는 같은 파일 중첩 타입) |
| 2 | 진행도를 한 바퀴 길이로 감아 누적하고(역주행·점프·낙사 판정 제외) 체크포인트 순서와 랩 완료·랩 시간을 세기 위해 | `Runtime/ChainRush/Track/LapCounter.cs` | sealed class (순수 C#) |
| 3 | 서킷의 중심선을 만들고, 노면(`RoadPiece`)·앵커·출발선·체크포인트 표지를 깔고, 매 틱 `LapCounter` 를 돌려 HUD·게임에 넘기기 위해 | `Runtime/ChainRush/Race/CircuitRace.cs` | MonoBehaviour (새 폴더 `Race/`) |
| 4 | 서킷 씬과 샘플 서킷 에셋을 만드는 메뉴 | `Editor/ChainRush/ChainRushCircuitSceneBuilder.cs` | static class (에디터) |
| 5 | 서킷 전용 씬: `ChainRushProcedural` 를 복사해 `Procedural World`·`EnemyDirector` 를 빼고 `Circuit World` 를 넣는다 | `Scenes/ChainRushCircuit.unity` | 씬 (빌더가 생성) |
| 6 | 샘플 서킷 "스타디움" | `Data/Circuit_Stadium.asset` | ScriptableObject 인스턴스 (빌더가 생성) |
| 7 | 루프 모드·초점의 수식 | `Tests/EditMode/CenterlineLoopTests.cs` | 테스트 |
| 8 | 랩 카운터의 규칙 | `Tests/EditMode/LapCounterTests.cs` | 테스트 |
| 9 | 정의의 닫힘 검증·중심선 생성 | `Tests/EditMode/TrackDefinitionTests.cs` | 테스트 |
| 10 | 봇이 3랩을 달리고 랩 수·시간이 맞는지, 역주행·낙사·재시작 | `Tests/PlayMode/ChainRushCircuitTests.cs` | 테스트 (봇은 `CourseBot` 를 일반화해 쓴다: 틈 정보를 `CircuitRace` 에서 받는다) |

`Track/` 은 11 → 13개(15개를 넘기지 않는다, §1-2). `Track/` 을 나누는 파일 이동은 하지 않는다(질문 8).

### 2-4. 설계

**서킷 정의** (`TrackDefinition`)
- `TrackSegment { Length, Radius(0 = 직선), Degrees(부호 = 방향), EndGrade, Gap(true 면 이 직선 구간은 노면 없음) }` 목록. `TrackAnchor { S, Offset, Height }` 목록(그래플 틈 위). `checkpoints`(S 목록, 기본 25·50·75%), `startSlots`(출발선 뒤 간격 목록, 단일 러너는 0번만), `lapCount`(3), 노면 반폭·두께·가드(`RoadProfile` 기본), 모듈 단위 가드 열림은 이번엔 없음.
- `TryValidate`(= `OnValidate` 로그 = 빌더·`CircuitRace` 예외의 공통 원천): 조각 길이 > 0, 원호 반지름 ≥ 30m·각도 ≤ 180°, 틈은 직선·평지(경사 0)·앞뒤 평지 직선 ≥ 15m(R1·R8 과 같음), 앵커 `S` 는 틈 안, **닫힘**: 끝 위치가 시작과 0.5m, 방향 1°, 높이 0.1m 안이고 끝 경사 0. 한 줄 메시지에 어긋난 값(위치 m·각도°)을 적어 편집 중에 바로 보인다.
- 중심선: 정의를 `Centerline.AppendStraight/AppendArc` 로 붙이고 `Close` 를 부른다(검증 실패 시 `InvalidOperationException`). 한 바퀴 길이 = `EndS`.

**샘플 서킷 "스타디움"**: 직선 A 140m(언덕 + 점프 틈) → 우회전 반원 R40 → 직선 B 140m(그래플 틈 + 앵커) → 우회전 반원 R40. 한 바퀴 약 531m(계산: 2 × 140 + 2π × 40), 3랩 약 1594m, 닫힘은 구성상 정확(반원 둘이 옆으로 각각 80m 씩 반대로 간다). 언덕은 경사가 0 → +g → −g → 0 이라 높이도 닫힌다.

**랩 카운터** (`LapCounter`, 순수 로직)
- 입력: 매 틱 중심선 `S`(루프라 [0, L) 로 감긴 값)와 틱 번호. 누적 진행도 = 이전 + `wrapDelta`(Δ 를 (−L/2, L/2] 로 접음). 한 틱 이동은 최대 수평 속도 약 20m/s(슬링샷) × 0.02 = 0.4m 라 접힘이 모호하지 않다.
- 랩 완료: 누적 진행도가 `k × L` 이상이 되고 **그 랩의 체크포인트를 순서대로 모두 지났을 때**. 역주행으로 줄었다가 다시 가는 것은 누적이 줄었다 늘 뿐이라 이중 집계가 없다(`k` 번째 선은 한 번만 센다).
- 결과: 랩별 틱·시간(초 = 틱 × 0.02), 합계, 체크포인트 구간 시간, 현재 랩, 완료 여부.

**`CircuitRace`**: `Awake` 에서 정의를 검증하고 중심선을 만들어 `ChainRushGame` 에 넘긴다(`ChainRushGame.Awake` 가 먼저 돌 수 있어 `BuildTrack()` 을 지연 호출형으로 둔다 — `ProceduralCourse.Initialize` 와 같은 방식). 노면은 정의 길이 전체를 50m 이하로 나눠 `RoadPiece` 로 **한 번만** 깐다(풀·스트리밍·원점 이동 없음, 틈 구간은 건너뜀). 앵커는 씬에 고정 배열(빌더가 8개 만들어 `GrappleController.anchors` 와 연결)로 두고 정의 위치로 옮긴다(남는 것은 끈다). 출발선·체크포인트는 얇은 발광 띠 표지(비충돌). 출발은 **출발선 위 `S = 0`**, 스탠딩 스타트, 시계는 `StartRun` 틱 0 부터, 1랩 = 선에서 선(`S = L`)까지.
- 낙사(틈·벽 없음)는 지금처럼 런 종료(`H < −12`, `RunnerMotor.cs:213`). 체크포인트 복귀는 P3 몫(질문 2).
- `ChainRushGame.HasFinished` = 3랩 완료. HUD 가 `CircuitRace` 의 랩 정보를 읽는다.

**제한**: `CourseTarget`·적·`EnemyDirector` 없음(`targets` 빈 배열). 시드·생성기 없음.

### 2-5. 검증 방법

- **컴파일**: Unity MCP, Console Error 0.
- **EditMode** (새 3클래스 + 기존 161 그대로):
  - `CenterlineLoopTests`: 닫힌 서킷 `Close` 성공/실패(어긋남 1m 는 예외), `FrameAt(s)` 감김(`s` 와 `s + L` 같은 프레임), **귀환 직선 위 점**을 초점 없이 투영하면 틀리고(고치기 전 실패하는 테스트로 증명) 초점이 있으면 맞는 `S`, 이음매(`S ≈ 0` / `S ≈ L`) 양쪽에서 연속, 초점 없는 직선·원호는 기존 `CenterlineTests` 가 그대로 통과.
  - `LapCounterTests`: 정상 3랩, 이음매 왕복(앞뒤로 흔들어도 한 번만), 역주행 뒤 정주행, 체크포인트를 건너뛰면 랩 불인정, 한 틱 최대 이동(0.4m), 시간 합계 = 틱 합.
  - `TrackDefinitionTests`: 스타디움 닫힘 허용 오차 안, 값을 어긋나게 하면 `TryValidate` 가 어떤 값이 얼마나 어긋났는지 말한다, 틈·앵커·반지름 규칙, 한 바퀴 길이 ≈ 531m.
- **PlayMode `ChainRushCircuitTests`**: ① 봇이 3랩 완주, 랩 카운트 정확(각 랩 시간이 한 바퀴 길이 ÷ 속도 ± 20% 안, 합계 = 세 랩 합) ② 역주행(헤딩 반대로 선 근처를 왕복)해도 랩이 늘지 않는다 ③ 틈에서 아무것도 안 하면 낙사로 런이 끝나고 랩 불인정 ④ 재시작하면 랩·시간이 0 에서 다시 시작 ⑤ 앵커 위치가 정의와 같다 ⑥ 한 틱 `Step` 시간·접지·가드 접촉을 로그로 남긴다.
- **회귀**: 기존 PlayMode 54건 무수정 통과(특히 `Centerline` 변경 뒤). 병합 조건(§9-2)은 컴파일 0 + EditMode + PlayMode(Device·Sweep 제외) 연속 2회를 지금 규칙대로 돌린다. `Centerline` 을 바꾸므로 **`Run Course Sweep`(20시드)도 1회** 돌려 결과를 붙인다(§9-2 의 `ProceduralCourse` 항과 같은 이유: 투영 변경이 스트리밍 코스에 영향이 없음을 증명).
- 시간(계산): 봇 3랩 1594m ÷ 10 m/s ÷ 3배속 ≈ 53초(경사·그래플 보정 전) → 게이트 +약 1분/회(지금 약 377초 → 약 440초), 스윕은 약 933초 그대로.
- 눈으로 확인: `Unity_Camera_Capture` 대신 T3c 와 같은 임시 스크린샷 테스트로 몇 장(출발선, 반원, 그래플 틈 앞). 임시 파일은 커밋하지 않는다.

### 2-6. 사용자에게 물을 것 (추천을 받으면 "ㄱㄱ")

1. **범위**: **단일 러너 타임어택(속도전) 추천.** 아이템·접촉·적·`CourseTarget` 없음, 순위·고스트·다인 출발은 P3. 대안: 이번에 적 드론도 서킷에 올린다(`EnemyDirector` 의 트랙 의존을 더 풀어야 해 범위가 커진다).
2. **낙사 처리**: **지금처럼 런 종료 추천.** 체크포인트는 구간 시간 표시와 랩 순서 검증에만 쓴다. 대안: 낙사 시 마지막 체크포인트로 복귀(레이스답지만 복귀 위치·속도·무적 규칙을 정해야 해 P3 와 함께 다루는 편이 낫다).
3. **새 씬 방식**: **새 씬 `ChainRushCircuit`(`ChainRushProcedural` 복사) 추천.** 기존 씬 3개와 그 테스트를 안 건드린다. 대안: 기존 `ChainRushPrototype` 씬을 서킷으로 바꾼다(유한 직선 모드 테스트 다수가 이 씬에 기대 깨진다).
4. **출발·1랩 정의**: **출발선 위 `S = 0` 스탠딩 스타트, 1랩 = 선에서 선, 3랩 추천.** 대안: 선 뒤 6m 에서 출발해 처음 선을 지나는 순간 계시 시작(롤링 스타트).
5. **`Centerline` 루프·초점 공개 API 추가 추천**(2-2). 대안: 서킷은 `S` 주변만 탐색하는 별도 투영 클래스를 만들고 `Centerline` 은 안 건드린다 — 12개 호출부를 모두 새 클래스로 돌려야 해 기존 파일을 더 많이 고친다.
6. **`CourseTarget` 트랙 프레임화**: **이번에도 미루기 추천**(질문 1 과 같은 이유: 서킷에 `CourseTarget` 이 없다). 곡선 위에 표적·장애물을 놓는 첫 작업(P3 C3)에서 한다.
7. **기록 저장(최고 랩·로컬 랭킹)**: **이번에는 하지 않기 추천.** 화면 표시만. 저장 형식은 P1/P2 기록 항목에서 한다.
8. **폴더**: **`TrackDefinition`·`LapCounter` 는 `Track/`(13개), `CircuitRace` 는 새 폴더 `Race/` 추천**(새 네임스페이스 `ProtoHarness.ChainRush.Race`). 대안: `Track/` 을 `Track/` 과 `Course/` 로 나누는 이동을 먼저 한다(별도 합의, `.meta` 를 함께 옮겨야 함).
9. **서킷 값**: **스타디움(R40 반원 2개, 직선 140m, 531m)으로 시작 추천**, 체감 튜닝은 달려 본 뒤. 대안: 더 복잡한 모양(S자·헤어핀·교차)은 T5 보충 모듈과 함께.

### 2-7. 안 하는 것

아이템·접촉·충돌·밀치기·순위·고스트·다인 출발(P3), 적·`CourseTarget`·`EnemyDirector` 사용, 낙사 복귀, 기록 저장, 서킷 에디터 도구(씬 뷰 편집·닫힘 자동 맞춤), 서킷 여러 개, 다리·교차(자기 교차 서킷은 `Project` 가 "가장 가까운 조각" 이라 위층 아래층이 구분되지 않는다), 물리 뱅크·경사 커브(T5/T6), 기존 씬·그 테스트 수정, 장식, 패키지·ProjectSettings.

### 2-8. 승인이 따로 필요한 단계 (§0, §3-3)

- 서킷 씬·`.asset` 을 만드는 메뉴 실행(`Unity_RunCommand` 로 `ExecuteMenuItem`)은 상태를 바꾸므로 **실행 전에 코드 전문을 보여주고 승인**받는다. 씬 저장은 빌더 메뉴 안에서만 한다.
- 만든 파일을 지우는 일(진단용 임시 테스트 포함)은 그때마다 승인받는다(§0).

### 2-9. 승인 뒤 시작 순서

1. `docs/RULES/CONVENTIONS.md`, `VERIFICATION.md`, `BRANCHING.md` 를 읽는다.
2. 이 요청서(`docs/t4-proposal`)가 `dev` 에 병합됐는지 확인한다. 안 됐으면 사용자에게 먼저 묻는다.
3. `dev` 에서 `feature/course-t4-circuit` 브랜치를 만든다.
4. 순서: `CenterlineLoopTests`(**고치기 전 실패 확인**) → `Centerline` 루프·초점 → **기존 PlayMode 54건 + EditMode 무수정 통과(회귀 기준선)** → `LapCounter` + 테스트 → `TrackDefinition` + 테스트 → `CircuitRace` + `ChainRushGame`·HUD → 빌더 → 씬 생성(승인) → PlayMode 테스트 → 게이트 2회 + 스윕.

### 2-10. 남아 있는 다른 후보

- **T3d (정리)**: 낡은 직선 무한 코스(`EndlessCourse`, `ChainRushEndless` 씬)를 새 씬으로 대체하고 의존 테스트 3개 클래스(`EndlessTests`, `PresentationTests`, `SteeringTests`)를 옮기거나 지우는 일. 테스트 변경 합의가 필요하다. 장식(타워)도 이때 다시 묻는다.
- T3c 가 남긴 것: 체감 튜닝(`CourseTuning_Default.asset`)은 사용자가 `ChainRushProcedural` 을 직접 달려 본 피드백 대기. 생성기는 한 걸음 앞(R9)까지만 본다.

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
