# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-08 (T4 병합, T3d 합의 요청서). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-08)

- 브랜치: T4 까지 `dev` 에 병합돼 있다(`9ba6d8f`, `origin/dev` 에는 푸시 안 함). T3d 합의 요청서(아래 2절)는 승인됐고(2026-10-08), 구현은 `feature/course-t3d-cleanup` 에서 한다.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`(직선·원호·종단 곡선, 절대 `S`, **T4: 루프 모드·초점**), 이동(자유 조향·그립·경사·땅 붙잡기·가드), 드리프트 → 체인 게이지 → 체인 액션, 두 씬 발판 가드 난간.
  - T3a 절차 노면, T3b 시드 생성기, T3c 절차 무한 코스(`ChainRushProcedural` 씬, `ProceduralCourse`).
  - **T4** (DECISIONS 2026-10-08 "코스 T4"): `TrackDefinition`(SO, 닫힘 검증) · `LapCounter` · `CircuitRace` · 새 씬 `ChainRushCircuit` + `Data/Circuit_Stadium.asset`(스타디움 531m). 봇이 3랩을 완주하고 랩 시간 52.9 / 52.7 / 52.8초.
- 씬: `ChainRushPrototype`(직선 유한), `ChainRushEndless`(직선 무한, 낡음), `ChainRushProcedural`(절차 무한), `ChainRushCircuit`(서킷).
- 테스트: EditMode 213, PlayMode 62(Device 2 + Sweep 1 + 게이트 59). 병합 조건 실행은 Device·Sweep 제외 59건, 약 447초/회. 20 시드 스윕(`Run Course Sweep`) 약 933초.

## 2. 다음 작업: T3d — 낡은 직선 무한 코스 정리 (합의 요청서, 승인됨 2026-10-08)

T3c 가 만든 `ChainRushProcedural` 씬이 무한 모드를 대체했다. 낡은 직선 무한 코스(`EndlessCourse`, `ChainRushEndless` 씬)와 그것을 만들던 도구, 그 씬에 기대는 테스트를 정리한다. 아래는 §3-2 형식의 합의 요청서다. **사용자가 2026-10-08 "ㄱㄱ" 로 2-6 의 질문 1~7 을 전부 추천대로 승인했다.** 구현은 이 범위 안에서만 한다. 파일 삭제는 지우기 직전에 목록을 다시 확인한다(2-8). 씬을 지우는 작업이라 씬·프리팹 브랜치는 동시에 1개만 둔다(§9-2).

### 2-1. 목표

① 위치에 기대는 테스트를 새 코스(`ChainRushProcedural`) 기준으로 **같은 단언을 유지한 채** 옮기고, ② 옮긴 테스트가 새 씬에서 통과하는 것을 확인한 **뒤에**, ③ `ChainRushEndless` 씬·`EndlessCourse`·낡은 씬 제작 도구를 지운다. 무한 모드의 동작은 바뀌지 않는다(`ChainRushProcedural` 은 건드리지 않는다).

### 2-2. 건드릴 것 (기존 파일)

**고치는 것**

| 파일 | 바꾸는 것 | 이유 |
|---|---|---|
| `Tests/PlayMode/ChainRushPresentationTests.cs:27,79` | 씬 경로를 `ChainRushProcedural` 로, `Animation_JumpAndGrapple_…` 의 위치(z = 28, 옛 발판 끝)를 "첫 그래플 틈 12m 앞" 으로 | 3건 중 2건은 위치와 무관하다. 단언은 그대로 |
| `Tests/PlayMode/ChainRushSteeringTests.cs:27,94-112` | `Guards_BothScenes_LineEveryDeck` 가 옛 씬의 `Deck` 오브젝트 대신 **절차 노면 조각의 가드 벽 콜라이더**(활성 조각마다 "Guard wall" 메시가 있다)를 검사. 프로토타입 씬 검사는 그대로 | 옛 씬이 사라진다. "모든 도로에 가드가 있다" 는 의미를 유지 |
| `Tests/PlayMode/ChainRushProceduralTests.cs` | `RunSeed` 에 "적 격파 > 3" 단언 추가 | 옛 `Endless_LongRun_…` 가 지키던 "안전한 곳에서 조우가 실제로 일어난다" 를 이어받는다(지금은 로그만 남기고 단언하지 않는다) |
| `Editor/ChainRush/ChainRushCircuitSceneBuilder.cs` | 원본 씬 경로 상수를 자기 안으로(지금은 `ChainRushProceduralSceneBuilder.ProceduralScenePath`) | 그 빌더를 지운다 |
| `Runtime/ChainRush/Endless/CourseStream.cs` | 주석에서 `EndlessCourse` 언급 제거 | 같은 이유 |
| `docs/ARCHITECTURE.md`, `COURSE.md`, `HANDOFF.md`, `DECISIONS.md`(새 항목) | 옛 코스·도구를 가리키는 줄 | 문서 |

**지우는 것** (§0: 삭제는 승인 후)

| 대상 | 개수 | 비고 |
|---|---|---|
| `Scenes/ChainRushEndless.unity` (+ `.meta`) | 2 | 31만 줄. 가드 난간 발판 8개·네온 타워 포함 |
| `Runtime/ChainRush/Endless/EndlessCourse.cs` (+ `.meta`) | 2 | 114줄. `CourseStream` 의 두 구현 중 하나 |
| `Editor/ChainRush/ChainRushEndlessSceneBuilder.cs` | 2 | Prototype → Endless 복사 도구 |
| `Editor/ChainRush/ChainRushProceduralSceneBuilder.cs` | 2 | 원본이 `ChainRushEndless` 씬이라 지우면 실행할 수 없다. 씬·`CourseTuning_Default.asset` 은 이미 있다 |
| `Editor/ChainRush/ChainRushPresentationBuilder.cs` | 2 | Endless 씬에 음향·애니메이션을 더하던 도구. 그 컴포넌트는 `ChainRushProcedural`·`ChainRushCircuit` 씬 안에 이미 있다 |
| `Tests/PlayMode/ChainRushEndlessTests.cs` | 2 | 새 `ChainRushCombatTests.cs` 로 대체(아래) |

참고한 기존 자산(§2 검색 3연타, 2026-10-08): `Grep "ChainRushEndless|EndlessCourse|EndlessSceneBuilder|EndlessScenePath"` → 코드 8개 파일(위 표) + 문서 5개. `Glob Scenes/*.unity` → 4개. 가장 비슷한 `ChainRushProceduralTests.cs`(씬 로드·`SetSeed`·`FindGapSpot`·`Teleport`·`LandOnRoad` 도우미)를 끝까지 읽었고, 옮기는 테스트가 같은 도우미 형식을 쓴다. `ProjectSettings/EditorBuildSettings.asset` 의 씬 목록에는 `Assets/Scenes/SampleScene.unity` 하나뿐이라 빌드 설정은 영향이 없다.

기존 파일 수정 9개(코드 5 + 문서 4) + 삭제 12개 + 새 파일 2개라 합의 대상(§3-1 규모·행위 기준)이다.

### 2-3. 새로 만들 것 (§1-1 세 줄)

| # | 목적 | 경로 | 형태 |
|---|---|---|---|
| 1 | 옛 `ChainRushEndlessTests` 의 조우·공격·그래플 테스트 5건을 `ChainRushProcedural` 씬에서 같은 단언으로 돌리기 위해 | `Tests/PlayMode/ChainRushCombatTests.cs` | 테스트 (파일 이름에서 "Endless" 를 뺀다) |

### 2-4. 설계

**테스트 이전 표** (단언은 그대로, **위치 설정만** 새 코스 기준)

| 옛 테스트 (`ChainRushEndlessTests`) | 이전 방식 | 새 위치 |
|---|---|---|
| `Combat_EachDirection_AttackFiresConnectsAndRetracts` | `Prepare` 의 `MovePlayer(-5)` 를 없앤다 | 스폰(첫 모듈 = 60m 쉼터, `S` 5)에서 시작. 첫 틈은 `S ≥ 75` 라 `CanStartEncounter`(앞 틈까지 > 28.5m)가 성립. 시드는 `SetSeed(3)` 로 고정 |
| `Combat_EachDirection_TimeoutDamagesExactlyOnce` | 같음 | 같음 |
| `Combat_PauseAndRestart_FreezesWindowAndClearsFlight` | 같음 (`course.Distance ≈ 0` 단언은 `ProceduralCourse` 도 같은 의미) | 같음 |
| `Combat_GapAndJump_DoesNotRequireAttack` | `MovePlayer(29)`(옛 틈 앞)를 "첫 틈 12m 앞으로 순간이동" 으로 | `FindGapSpot`(`ChainRushProceduralTests`)과 같은 시드 탐색 |
| `Grapple_RestartWhileAttached_ClearsOldChainImmediately` | `MovePlayer(28)` 를 "그래플 틈 12m 앞" 으로 | 같음 |
| `Endless_LongRun_RecyclesRebasesAndRestartsWithoutGrowingPool` | **지운다.** 옛 직선 풀 전용(`RecycledCount`, `PoolSize == 8` 청크, 448m 투영 이동)이라 새 코스에는 같은 의미가 없다 | 대신 `ChainRushProceduralTests` 의 게이트 시드 3개가 이미 같은 일을 단언한다: 1400m 완주, 원점 이동 ≥ 1, 조각 24·앵커 10 풀 크기와 씬 오브젝트 수 불변, 재시작 뒤 초기화. 빠져 있던 "조우가 실제로 일어난다(격파 > 3)" 만 `RunSeed` 에 더한다 |

"통과시키려고 테스트를 고치지 않는다"(§0)와의 관계: 고치는 것은 **어디에 서서 시작하는가** 뿐이고 단언(공격이 맞는다·시간 초과 피해 1회·틈 위 조우 없음·재시작 때 체인이 즉시 사라진다)은 바꾸지 않는다. 지우는 한 건은 위 표의 이유와 대체 단언을 DECISIONS 에 적는다. 옮긴 테스트는 **옛 씬을 지우기 전에** 새 씬에서 통과시킨다(2-9).

**삭제의 영향**
- `ChainRushGame`·`EnemyDirector` 는 이미 `CourseStream` 을 받으므로 코드 변경이 없다. `CourseStream` 은 구현이 `ProceduralCourse` 하나가 되지만 그대로 둔다(질문 6).
- 씬은 이제 YAML 이 원본이다. 지우는 도구 3개는 "씬을 코드로 다시 만드는 길" 이었다. 씬을 다시 만들어야 하면 git 이력의 씬을 되살린다.
- `Art/Materials` 의 `M_City` 등 옛 장식용 재질은 그대로 둔다(안 쓰면 따로 정리).

### 2-5. 검증 방법

- **컴파일**: Console Error 0.
- **EditMode**: 213 그대로.
- **PlayMode**: ① 옮기는 테스트(Combat 5건, Presentation 3건(그중 1건은 위치도 변경), Steering 1건(검사 대상 변경))를 옛 씬이 **남아 있는 상태**에서 새 씬으로 돌려 통과시킨다. ② 옛 파일·씬을 지운 뒤 컴파일 0 + EditMode 213 + PlayMode(Device·Sweep 제외) 연속 2회를 지금 규칙대로. 예상 59 → **58건**(지우는 한 건, 새 `ChainRushCombatTests` 5건이 옛 6건을 대체). ③ `RunSeed` 단언을 바꿨으므로 20시드 스윕을 1회(§9-2).
- 문서 검증은 해당 없음(`Assets/` 에 닿는다).
- 시간(계산): 게이트가 옛 `Endless_LongRun_` 만큼 줄어 회당 약 447 → 약 400초(계산: 1400m ÷ 10 m/s ÷ 3배속 ≈ 47초).
- 눈으로 확인: `ChainRushProcedural`·`ChainRushCircuit` 이 그대로임을 몇 장의 임시 스크린샷으로(옛 네온 타워가 사라진 것은 의도된 결과). 임시 파일은 커밋하지 않는다.

### 2-6. 사용자에게 물을 것 (추천을 받으면 "ㄱㄱ")

1. **삭제 범위**: **2-2 의 삭제 표 전부 추천**(씬·`EndlessCourse`·빌더 3개·옛 테스트 파일). 대안: 씬만 지우고 코드·빌더는 남긴다(빌더가 지워진 씬 때문에 실행 불가인 죽은 메뉴가 된다), 또는 모두 그대로 둔다(옛 씬이 계속 31만 줄, 테스트 3개 클래스가 옛 직선 코스에 묶인다).
2. **테스트 이전**: **같은 단언 + 위치만 이전, `Endless_LongRun_` 은 지우고 격파 > 3 단언을 `RunSeed` 로 옮기기 추천.** 대안: 옛 테스트를 전부 지우고 새로 쓴다(조우·그래플 회귀 단언이 줄어든다).
3. **장식(네온 타워)**: **이번에도 안 하기 추천.** 옛 씬의 타워는 직선 발판 옆에만 놓였다. `ChainRushProcedural`·`ChainRushCircuit` 은 이미 장식 없이 달린다(T3c 질문 3). 곡선·언덕 코스 옆 장식은 별도 설계라 다음 단계(T3e 후보)로 미룬다. 대안: 이번에 도로 조각마다 단순 타워 2개를 25m 옆에 붙인다.
4. **씬 제작 도구 삭제**: **삭제 추천**(2-2). 씬은 YAML 이 원본이고, 지우지 않으면 죽은 메뉴가 남는다. 대안: 남겨 두고 메뉴가 "원본 씬이 없다" 예외를 내게 한다.
5. **새 테스트 파일 이름**: **`ChainRushCombatTests.cs` 새 파일 + 옛 파일 삭제 추천.** 대안: 옛 파일 이름(`ChainRushEndlessTests`)을 유지하고 안을 고친다(이름이 거짓이 된다).
6. **`CourseStream`**: **유지 추천**(구현이 하나지만 두 필드 타입·두 씬의 직렬화 참조를 안 건드린다). 대안: `ProceduralCourse` 로 합친다(필드 타입 2곳과 `override` 를 지우는 작은 변경이지만 이번 정리의 위험을 늘린다).
7. **스윕**: **`RunSeed` 단언이 바뀌므로 1회 추천.** 대안: 생략(코스 코드는 안 바뀐다).

### 2-7. 안 하는 것

`ChainRushProcedural`·`ChainRushCircuit`·`ChainRushPrototype` 씬 수정, `ProceduralCourse`·`CircuitRace`·`CourseGenerator` 변경, `CourseStream` 제거, 장식, 사용하지 않는 재질 정리, 문서의 과거 결정(DECISIONS 의 옛 항목)을 고치는 일, 패키지·ProjectSettings.

### 2-8. 승인이 따로 필요한 단계 (§0, §3-3)

- **파일 삭제**: 2-2 의 삭제 표 12개 파일을 지우기 **직전에** 목록을 보여주고 한 번 더 확인한다(승인은 이 요청서의 삭제 표 범위만).
- 진단용 임시 파일을 만들면 그것을 지우는 일도 그때마다 승인받는다.

### 2-9. 승인 뒤 시작 순서

1. `docs/RULES/CONVENTIONS.md`, `VERIFICATION.md`, `BRANCHING.md` 를 읽는다.
2. 이 요청서(`docs/t3d-proposal`)가 `dev` 에 병합됐는지 확인한다. 안 됐으면 사용자에게 먼저 묻는다.
3. `dev` 에서 `feature/course-t3d-cleanup` 브랜치를 만든다.
4. 순서: **옛 씬이 남은 채로** `ChainRushCombatTests`(새)와 Presentation·Steering·`RunSeed` 변경을 `ChainRushProcedural` 로 통과시킨다(옛 `ChainRushEndlessTests` 도 아직 통과하는 상태) → 지울 파일 목록 확인 → 삭제 + 문서 → 컴파일·EditMode·게이트 2회·스윕 1회.

### 2-10. 남아 있는 다른 후보

- **T5 보충 모듈**(COURSE 5-2), **P2 리플레이·결정성**(DESIGN P2), **P3 레이스**(아이템·접촉·순위·고스트·다인 출발, 낙사 복귀, `CourseTarget` 트랙 프레임화).
- 체감 튜닝: `CourseTuning_Default.asset`·`Circuit_Stadium.asset` 은 사용자가 직접 달려 본 피드백 대기.
- `CLAUDE.md` §9-2 의 스윕 조건 목록에 `Centerline` 을 더할지(T4 에서 이월).

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
