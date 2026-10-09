# ProtoHarness 아키텍처

> **A모드 진입점.** 구조·시스템·진입점을 묻는 질문은 코드를 뒤지기 전에 이 문서부터 읽는다.
> 이 문서가 코드와 다르면 **문서가 틀린 것**이다. 발견 즉시 고치고 §5대로 알린다.

**최종 갱신**: 2026-10-01 · Chain Rush 3D 무한 전투 프로토타입 (커밋 `4e6f12b` 에 포함). 기술 스택 행은 2026-10-01 에디터 실측.

---

## 1. 한 줄 정의

Unity 6 URP 기반 프로젝트. DosangE/Chain-Rush의 점프·그래플링·공격 흐름을 후방 3인칭 3D 러너로 구현한다.

## 2. 현재 상태 — Chain Rush 3D

| 항목 | 수 |
|---|---|
| 우리 런타임 스크립트 | **31** (`Scripts/Runtime/ChainRush/` 및 하위 폴더). 그중 ScriptableObject 3개(`EncounterTuning`, `RunRules`, `CourseTuning`), 인스턴스는 `Assets/_Project/Data/` (`CourseTuning` 은 아직 인스턴스 없음, T3c) |
| 우리 에디터 스크립트 | **3** (`ChainRushSceneBuilder`, `ChainRushEndlessSceneBuilder`, `ChainRushPresentationBuilder`) |
| 우리 테스트 | PlayMode **12 파일 / 46 테스트** (그중 `Device` 카테고리 2개, 공용 헬퍼 `TestRoad.cs`·`TrackFollower.cs` 포함), EditMode **11 파일 / 152 테스트** (`Scripts/Tests/EditMode/`, `ProtoHarness.Tests.EditMode`). 2026-10-09 실행 XML 기준 |
| 템플릿 잔재 | `Assets/TutorialInfo/Scripts/` 2개 (건드리지 않음) |
| 게임 씬 | `Assets/_Project/Scenes/ChainRushPrototype.unity` (기존 테스트 맵), `ChainRushEndless.unity` (별도 무한 전투 맵) |
| 템플릿 씬 | `Assets/Scenes/SampleScene.unity` |

참조 게임: https://github.com/DosangE/Chain-Rush · 조사 기준 커밋 `17294d85943dfa9fecd6a70272411e035b1ca6bf`.
코드·아트 파일을 복사하지 않고 움직임의 목적을 새로 구현했다. 캐릭터·코스·머티리얼은 Unity 기본 도형으로 제작한다.

## 3. 기술 스택 (확정)

| 층 | 선택 | 비고 |
|---|---|---|
| 엔진 | Unity 6000.3.25f1 | 에디터 실측 `Application.unityVersion` (2026-10-01). 이전 6000.3.18f1 |
| 렌더 | URP 17.3.0 | 설정은 `Assets/Settings/` — Unity 템플릿 소유 |
| 입력 | Input System 1.20.0 | **신 입력 시스템.** `Input.GetKey` 금지. 이전 1.19.0 |
| 테스트 | Test Framework 1.6.0 | PlayMode / Editor TestRunnerApi |
| 직렬화 | Newtonsoft Json 3.2.1 | 패키지 의존으로 이미 존재 |

## 4. 시스템 지도

> 시스템이 생기면 여기에 **1개당 3줄**로 적는다: 책임 한 줄 / 진입점 파일 / 의존 시스템.
> 3줄을 넘기면 `docs/SYSTEMS/<이름>.md` 로 분리하고 여기엔 링크만 남긴다.

런타임 네임스페이스는 `ProtoHarness.ChainRush`, 에디터는 `ProtoHarness.Editor.ChainRush`.

| 책임 | 진입점 (`Assets/_Project/Scripts/` 기준) | 의존 |
|---|---|---|
| 자동 전진·점프·중력·충돌. `Step(in TickInput)` 으로 틱 입력을 받는다 (장치를 읽지 않음, 시각물을 쓰지 않음). 자유 조향: 입력이 헤딩(`RacerState.Heading`)을 돌리고 전진·그립은 헤딩 기준. 가드(난간) 부딪힘은 감속 + 난간과 나란히 돌림. 드리프트(그립 저하·게이지 충전)와 체인 액션(우선순위: 그래플 강화 → 커브 드리프트 중 코너 스윙 → 슬링샷, 게이지 1칸). 접지 중 경사 속도 보정, 내리막 땅 붙잡기(SphereCast), 낙하 실패는 트랙 기준 높이 `H < -12` | `Runtime/ChainRush/RunnerMotor.cs` | CharacterController, Game(`Track`), Grapple, `TickInput` |
| 몸체 기울이기(표현 전용). `LateUpdate` 에서 모터의 `Velocity`·`Steer` 로 `body.localRotation` 을 쓴다. 공식은 정적 `Evaluate` | `Runtime/ChainRush/Visuals/RunnerTilt.cs` (`[DefaultExecutionOrder(100)]`) | Motor, 직렬화 `body` Transform (비어 있으면 LogError 후 비활성화) |
| 틱 한 번의 조작값: 조향 [-1,1] + 주동작·해제·공격 엣지. 범위 밖이면 `ArgumentOutOfRangeException` | `Runtime/ChainRush/Control/TickInput.cs` (readonly struct) | 없음 |
| 입력 출처 계약: `Poll()`(실행 중 프레임마다) / `Consume()`(틱마다) / `Clear()` | `Runtime/ChainRush/Control/IInputSource.cs` | `TickInput` |
| 틱 사이 버튼 엣지 보존, 조향은 최신값 유지. 순수 로직 | `Runtime/ChainRush/Control/InputLatch.cs` | `TickInput` |
| 키보드·마우스 매핑 (A/D·←/→ 조향, 좌클릭 주동작, 좌클릭 뗌·우클릭 해제, Space 공격, 왼쪽 Shift 드리프트, 왼쪽 Ctrl 체인 액션) | `Runtime/ChainRush/Control/KeyboardMouseInputSource.cs` | Input System, `InputLatch` |
| 전방 앵커 선택·줄 길이 제약·해제 부스트. 체인 액션으로 강화되면 빨리 감고 세게 던진다. 슬링샷 당김 동안 체인 시각물 표시 | `Runtime/ChainRush/GrappleController.cs` | 직렬화 앵커 배열, LineRenderer, Motor, Game |
| 준비·진행·정지·실패·완주 흐름, 공격·피격 판정 진입점. 트랙 중심선을 소유한다(`Track`) | `Runtime/ChainRush/ChainRushGame.cs` | Motor, Grapple, Camera, Targets, AudioSource, RunRules, `RacerState`, `Centerline` |
| 트랙 좌표계: 직선 조각과 수평 원호(`AppendArc`, 곡률 `TrackFrame.Curvature`, 원호 중심 `CurveCenter`)를 이은 중심선, 조각마다 종단 곡선(경사 연속, 높이 포물선). 월드 위치 → `(S, D, H)` 투영, 그 지점의 앞·오른쪽 축·높이·경사(`TrackFrame`). `ChainRushGame.SetTrack` 으로 교체 가능(유한 모드만). `S` 는 레이스 시작부터의 절대 거리(double)라 원점 이동·조각 버리기 뒤에도 이어진다. 범위 밖은 양 끝 접선으로 연장. 순수 로직. 모터·그래플·카메라·적·코스가 "앞"을 여기서 얻는다. T0 은 두 씬 모두 원점에서 +z 직선 | `Runtime/ChainRush/Track/Centerline.cs` (+ `TrackFrame`, `TrackCoord` readonly struct) | 없음 (`ChainRushGame.Awake` 에서 코드로 생성) |
| 절차 노면 (T3a, 2026-10-07): 단면(`RoadProfile`: 반폭·두께·좌/우 가드)을 중심선 `[fromS, toS]` 를 따라 쓸어 노면 판·가드 난간·충돌벽·조명 띠 메시를 만든다(`RoadMeshBuilder`, 순수 계산·할당 없음). `RoadPiece` 는 한 구간의 오브젝트·메시를 한 번 만들고 `Build` 로 다시 채운다(풀용, 콜라이더는 MeshFilter 와 다른 자식). **아직 씬에서 쓰지 않는다**(테스트 `ChainRushRoadTests` 만, 씬 연결은 T3c) | `Runtime/ChainRush/Track/RoadPiece.cs` (+ `RoadMeshBuilder` static, `RoadProfile` readonly struct) | `Centerline`, 호출자가 넘기는 재질 3개 |
| 코스 시드 생성 (T3b, 2026-10-09): 시드 하나로 모듈 열(`CourseModule`: 직선·원호 조각 최대 3개, 틈, 앵커, 열린 가장자리)을 결정적으로 만든다(`CourseGenerator.Next()`). 후보는 `SeedHash`(SplitMix64)로 뽑고 연결 규칙 R1~R8 + 앞보기 300m 를 통과한 첫 후보를 쓴다. 가중치·범위·규칙 값은 `CourseTuning`(SO). `AppendTo(Centerline)` 로 중심선에 붙인다. 순수 로직. **아직 씬에서 쓰지 않는다**(EditMode 테스트만, 씬 연결은 T3c) | `Runtime/ChainRush/Track/CourseGenerator.cs` (+ `CourseModule` readonly struct, `CourseTuning` SO, `SeedHash` static, `ModuleKind` enum) | `Centerline`(`AppendTo` 만), `CourseTuning` |
| 레이서 한 명분 시뮬 상태(위치 제외): 체력·적중 수·그래플 수·무적/공격 마감 틱, 속도·조향·점프 예약·코요테 시간, 그래플 앵커 인덱스·줄 길이·빗나감 마감 틱. 운동 값은 `ref` 로 노출한다(모터가 성분을 제자리에서 고치고, 그래플의 해제 부스트가 같은 메모리를 고친다). 순수 로직. `ChainRushGame.Racer` 로 접근 | `Runtime/ChainRush/RacerState.cs` | `RunRules` (생성자 인자, null 이면 `ArgumentNullException`) |
| 추적 카메라·속도에 따른 FOV. 러너 헤딩 뒤를 따르고 yaw 회전은 `maxYawSpeed`(180°/s) 상한. 높이 하한은 중심선 높이 기준. 첫 맞춤은 `Start`(트랙이 `Awake` 에서 생기므로) | `Runtime/ChainRush/FollowCamera.cs` | Motor, Game(`Track`), Camera |
| 공격 표적·위험물 접촉·복구 | `Runtime/ChainRush/CourseTarget.cs` | 직렬화 Visual Transform |
| 시작 안내·HUD(체인 게이지 2칸 포함)·결과 화면 | `Runtime/ChainRush/ChainRushHud.cs` | Game, Motor, Grapple, Camera |
| 씬 생성·열기·테스트 실행 메뉴. 발판 양쪽 가드 난간(`AddGuards`, 충돌 4m)과 기존 씬용 메뉴 `Add Deck Guards To Open Scene` | `Editor/ChainRush/ChainRushSceneBuilder.cs` | EditorSceneManager, AssetDatabase, TestRunnerApi |
| 발판 풀 재배치·원점 이동·중심선 조각 잇기/버리기. 거리는 트랙 `S` | `Runtime/ChainRush/Endless/EndlessCourse.cs` | Game(`Track`), Motor, Camera, 직렬화 구간 배열 |
| 적 경고·세 방향 진입·제한시간 전투 | `Runtime/ChainRush/Combat/EnemyDirector.cs` | Game, Motor, Course, ChainVisual, EncounterTuning |
| 조우 시간 5종·조우 간격 곡선 데이터. 순수 계산 `NextGap(distance)` | `Runtime/ChainRush/Combat/EncounterTuning.cs` (SO, 기본값 `Data/EncounterTuning_Default.asset`) | 없음 (`EnemyDirector` 가 직렬화 참조로 사용, 비어 있으면 LogError 후 비활성화) |
| 체력·피격 무적·공격 쿨다운·공격 시각 지속 데이터 | `Runtime/ChainRush/RunRules.cs` (SO, 기본값 `Data/RunRules_Default.asset`) | 없음 (`ChainRushGame` 이 직렬화 참조로 사용, 비어 있으면 LogError 후 비활성화) |
| 금속 체인 링크·갈고리·발사 및 회수 | `Runtime/ChainRush/Visuals/ChainVisual.cs` | Game, 손 Transform, 미리 만든 링크 배열 |
| 별도 무한 씬 제작 | `Editor/ChainRush/ChainRushEndlessSceneBuilder.cs` | 기존 테스트 씬, 공용 생성기 도형/참조 연결 함수 |
| 신스 음악·9종 효과음·바람·음소거 | `Runtime/ChainRush/Audio/ChainRushAudio.cs` | Game, Motor, AudioSource 3개 |
| 달리기·점프·착지·그래플·공격 관절 자세 | `Runtime/ChainRush/Visuals/RunnerAnimation.cs` | Game, Motor, Grapple, Audio, 직렬화 관절 Transform |
| 아트→사운드→애니메이션 순차 적용 | `Editor/ChainRush/ChainRushPresentationBuilder.cs` | 기존 무한 씬, 공용 도형/머티리얼 생성 함수 |

**고정 틱 (2026-10-02)**: 시뮬레이션의 진입점은 `ChainRushGame.FixedUpdate` 하나이고 한 호출이 한 틱(`Ticks.Seconds` = 0.02s)이다. 틱 안의 순서는 `IInputSource.Consume()` 로 받은 `TickInput` → 공격 → `RunnerMotor.Step(input)`(점프·그래플·해제 포함) → 장애물 접촉 → 완주 판정 → `EnemyDirector.Step` → `EndlessCourse.Step`(재활용·원점 이동). `Update`/`LateUpdate`는 입력 `Poll`(실행 중일 때만), 메뉴 키(R·Enter·Esc, 음소거 M)와 표현(카메라·애니메이션·HUD·체인 시각물·오디오)만 한다. 시뮬은 장치를 직접 읽지 않고 `ChainRushGame.SetInputSource` 로 입력 출처를 바꿀 수 있다(기본값 `KeyboardMouseInputSource`, `Awake` 에서 코드로 생성). 시간은 정수 틱(`ChainRushGame.Tick`)으로 세고 `Elapsed` 는 파생값이다. 초 단위 수치는 `Ticks.FromSeconds` 로 올림 환산한다(`RunRules`, `EncounterTuning` 의 `*Ticks` 프로퍼티). `Time.fixedDeltaTime` 이 `Ticks.Seconds` 와 다르면 `ChainRushGame` 이 LogError 후 비활성화된다.
CharacterController.Move로 충돌을 처리하고, `RunnerMotor.Step`에서 중력과 전진 속도를 적분한다.
그래플링은 길이 제한 구면으로 예상 위치를 투영하고 바깥쪽 방사 속도를 제거한다. 줄은 초당 3m씩 감긴다. 해제 시 최소 상승 속도는 지상 점프와 동일한 11.5m/s이다.
3D Joint 컴포넌트는 사용하지 않는다. 동적 Rigidbody 물체를 끌거나 줄이 장애물에 감기는 동작은 현재 범위 밖이다.
장애물은 박스 범위 접촉으로 체력을 줄이며 물리적으로 플레이어를 막지 않는다. 앵커 시야 검사는 Physics.Linecast를 사용한다.
게임 상태를 각 시스템이 확인하므로 일시정지에 전역 Time.timeScale 변경이 필요 없다.

## 5. 진입점

| 무엇 | 어디 |
|---|---|
| 작업 규칙 | `CLAUDE.md` (항상 지킬 것) + `docs/RULES/*.md` (작업별, 읽는 시점은 `CLAUDE.md` 맨 위 표) |
| 결정 이력 | `docs/DECISIONS.md` |
| 심볼 인덱스 (B모드) | `index/symbols.tsv` — 생성물, `tools/reindex.ps1` 로 재생성 |
| 서브에이전트 정의 | `.claude/agents/*.md` (5종) |
| 인덱스 재생성 | `tools/reindex.ps1` |
| 게임 씬 | `Assets/_Project/Scenes/ChainRushPrototype.unity` |
| 처음 만들기 | Unity 메뉴 `ProtoHarness > Chain Rush > Create Prototype Scene` (Ctrl+Shift+G). 기존 게임 씬을 덮어쓰지 않는다. |
| 열기 | `ProtoHarness > Chain Rush > Open Prototype Scene` |
| 무한 씬 만들기 | `ProtoHarness > Chain Rush > Create Endless Scene` (Ctrl+Shift+E). 기존 테스트 씬을 복사한 후 별도 경로에만 저장. 재생성으로 덮어쓰지 않음. |
| 표현 적용 | `ProtoHarness > Chain Rush > Apply Art Sound Animation` (Ctrl+Shift+J). 무한 씬에 한 번 적용, 중복 적용 시 명시적 오류. |
| 실행 | Play → Enter 또는 START RUN 버튼 |
| 조작 | A/D 또는 방향키로 진행 방향 틀기(자유 조향, 2026-10-05), 좌클릭 지상 점프 / 공중 재클릭 그래플, 유지 스윙 / 놓기·우클릭 해제, Space 공격, 왼쪽 Shift 드리프트(게이지 충전), 왼쪽 Ctrl 체인 액션(슬링샷 / 그래플 중 강화), R 재시작, Esc 일시정지 |
| 테스트 | `ProtoHarness > Chain Rush > Run PlayMode Tests` (병합 조건용, `Device` 카테고리 제외) / `Run Device Input Tests` (가상 키보드·마우스 테스트만) |
| 테스트 결과 | Unity 프로세스의 `Path.GetTempPath()` 아래 `ChainRush-PlayMode-results.xml` (EditMode 결과도 같은 파일에 덮어쓴다). 경로는 머신마다 다르므로 실행할 때 `Unity_RunCommand` 로 `Path.GetTempPath()` 를 읽어 확인한다 |

코스: 9개 플랫폼, 8개 낭떠러지와 앵커, 결승 z=496. 노란 점프선은 각 가장자리 4m 앞이다.
첫 점프는 z=44, 첫 앵커는 (0, 10, 56), 첫 착지 플랫폼은 z=64에서 시작한다.
첫 공격 표적은 (0, 1.1, 18)에 있으며 이후 표적·위험물은 좌우로 배치된다.
PlayMode 테스트는 장면 로드, 전진, 점프/연결/제약/해제, 사거리 실패, 피격 무적, 낙하 후 재시작과 표적 복구, 일시정지, 자동 조작으로 전체 코스 완주를 검증한다. 가상 Keyboard/Mouse의 Input System 이벤트로 Enter·A/D·좌클릭·놓기·R·Space도 검증하고 장치를 복구한다.
시각 검증 캡처는 Unity 프로세스의 임시 폴더에 `ChainRush-ready.png`, `ChainRush-running.png`, `ChainRush-grapple.png`로 저장한다.
HUD는 IMGUI와 OS 동적 폰트(Malgun Gothic/Arial), 효과음은 메모리 내 합성 AudioClip을 사용한다. 정식 아트·음원과 빌드/배포는 포함하지 않는다.

### 무한 전투 모드

- `ChainRushGame.endlessMode`가 켜진 씬만 EndlessCourse/EnemyDirector 참조를 요구한다. 기존 씬은 완주·CourseTarget 공격을 유지한다.
- 검증된 두 번째 발판 기하를 복제해 40m 발판 + 16m 갭을 8개 미리 배치한다. 구간 뒤 56m를 지나면 구간을 448m 앞에 옮긴다. 플레이어가 트랙 앞 방향으로 448m를 넘으면 구간·플레이어·카메라·전투 시각물·트랙 중심선을 함께 이동한다. 거리는 트랙 `S`(double)라 원점 이동 뒤에도 이어진다 (2026-10-05 T0, 이전에는 `EndlessCourse` 의 double 누적값).
- 기존 테스트 월드는 새 씬에서 비활성 보관한다. 런타임에 구간/적/체인을 Instantiate하지 않는다. 배경 건물도 구간과 함께 재활용한다.
- 적은 상공→왼쪽→오른쪽 순환. 0.3초 경고, 0.4초 진입, 1.2초 공격 가능, 0.15초 체인 비행, 0.3초 회수/적 돌진이다. Inspector에서 조정 가능하다.
- 안전 발판 잔여 길이가 전체 조우 시간 × 속도 + 5m를 확보해야 등장한다. 경고·진입·대기 중 점프하면 피해 없이 취소한다. Space는 공격 가능 상태에서만 발사하며 갈고리 도착 때 명중 1회를 기록한다. 제한시간 초과 후 적 돌진이 체력 1칸을 감소시킨다.
- 처치 후 간격은 거리와 함께 2.5초에서 최소 0.6초까지 감소한다. 안전 발판 조건을 우선하며 동시에 한 마리만 등장한다.
- ChainVisual은 각 80개 링크를 재사용한다. 적 공격과 그래플에 각각 배정한다. 금속 링크는 교차하는 두 평면의 닫힌 사각형이며 네온 중심선과 갈고리가 연결을 강조한다.
- 전투 테스트는 세 방향 Space 입력/비행 후 명중/회수, 세 방향 제한시간 실패, 일시정지·재시작, 갭/점프 충돌 방지, 1,400m 자동 주행·원점 이동 3회·오브젝트 수 고정, 그래플 중 재시작 시 체인 즉시 제거를 검증한다.

### 무한 모드 검증 기록 (2026-09-09)

- PlayMode XML 최종 결과: `result="Passed" total="14" passed="14" failed="0"`, 종료 00:12:11 KST. 기존 8개 테스트를 변경하지 않고 신규 6개를 추가했다.
- 그래플 중 재시작 테스트가 수정 전 `Expected: False / But was: True`로 실패했다. 강제 해제 시 ChainVisual과 발사 진행도를 즉시 초기화한 후 같은 테스트 및 전체 테스트를 통과했다.
- 1,400m 연속 자동 주행에서 원점 이동 3회 이상, 16회 이상의 구간 재활용, 실제 적 처치, 씬 Transform 수 고정을 검증했다.
- Unity 렌더 캡처 `ChainRush-enemy-Above/Left/Right.png`, `ChainRush-chain-Above/Left/Right.png`를 Unity 임시 폴더에 생성했다. 한글 공격 안내, 제한시간 바, 드론 코어, 실제 체인 비행 화면을 열어 확인했다.
- 기존 `ChainRushPrototype.unity` SHA-256 보존: `812F25BF5FD6132117D8FC7266378939AC5D3D3D11761B159E90C1E45FCF3293`.
- 이 기록은 표현 확장 전 무한 전투 검증이다. 이후 아트·사운드·애니메이션은 아래 단계에서 적용했다.

### 아트·사운드·애니메이션

- 아트: 무한 씬 전용 장갑/금속/청록/자홍/옥상 머티리얼 5개. 기존 캐릭터를 비활성 보관하고 관절형 장갑 러너를 연결한다. 체인 원점은 오른손 관절 아래로 옮긴다. 발판 환기구, 발광 창문, 구간별 도시 간판, 드론 장갑을 추가하고 보라색 안개로 원경을 정리한다. 장식물에는 충돌을 켜지 않는다.
- 사운드: 120 BPM, 16초/8마디의 원본 신스 루프와 바람 루프, 점프·연결·발사·피격·경고·명중·회수·발소리·착지 9종 효과음을 시작 시 메모리에서 합성한다. 외부 음원/패키지 없이 구현하며 파형 버퍼는 Update에서 생성하지 않는다. M으로 음소거한다. 음악·효과음·바람은 일시정지에 동기화되고 재시작 시 초기화되며 음소거 선택은 유지된다.
- 애니메이션: Transform 관절을 사용하는 절차적 애니메이션. 전진 속도에 따른 팔·다리 교차와 발소리, 공중 자세, 착지 압축, 그래플 시 오른팔 들어 올리기, 체인 발사/회수 반동, 실패 시 상체 숙임. 물리 루트 대신 시각 관절만 바꾼다. Animator/외부 리깅 파일은 사용하지 않는다.
- `ChainRushGame.enhancedPresentation`은 Audio와 RunnerAnimation 참조를 필수로 요구한다. 기존 테스트 씬은 이 기능을 켜지 않아 기존 표현을 유지한다.
- 신규 3개 테스트: 음악/관절의 정지·재시작·음소거 유지, 그래플 팔 자세, 모든 효과음/음악의 유효 파형·피크 검사. 음악 미리듣기는 Unity 임시 경로의 `ChainRush-music-preview.wav`, 시각 캡처는 `ChainRush-art-running.png`, `ChainRush-art-grapple.png`다.
- 최종 검증(2026-09-09 00:36:15 KST): PlayMode XML `result="Passed" total="17" passed="17" failed="0"`. 기존 테스트 맵 SHA-256 유지. Unity Console `logs: [], errorCount: 0`.
- 적용 중 사운드 참조가 필드 이름 변경 후 누락되어 `RunnerAnimation: all runtime and joint references are required.`로 테스트가 실패했다. 에디터에서 실제 참조를 다시 연결하고 저장한 후 전체 테스트를 통과했다. 검증 오류를 무시하거나 테스트 기대치를 변경하지 않았다.
- 최종 간판 크기·외벽 보강 후 전체 테스트를 다시 통과했다. 달리기·그래플 캡처로 관절 자세와 오른손 체인 연결을 확인했다. 음원 검증은 합성 파형과 재생 상태 검사이며, 사람의 청취에 의한 믹싱 검수는 별도다.

### 검증 기록 (2026-09-08)

- Unity MCP 명령 컴파일/실행 성공. 최종 Console 응답: `logs: [], totalCount: 0, errorCount: 0`.
- 최종 PlayMode XML: `result="Passed" total="8" passed="8" failed="0"` (23:41:35 KST).
- ~~이 머신에서 Unity의 임시 경로는 `C:/Users/Public/Documents/ESTsoft/CreatorTemp/`.~~ 당시 머신의 값이다. 경로는 머신마다 다르다(2026-10-04 다른 머신에서 `C:\Users\User\AppData\Local\Temp\` 확인). 위 "테스트 결과" 행대로 실행할 때 확인한다. 결과 파일은 `ChainRush-PlayMode-results.xml`.
- 같은 폴더의 시작·달리기·그래플링 PNG를 실제로 열어 한글 HUD, 캐릭터, 코스, 체인 연결을 확인했다.
- 재현된 착지 실패는 해제 부스트를 지상 점프 속도로 올린 후 완주 테스트로 검증했다. A→D 전환 테스트에서 확인한 느린 반응은 지상 좌우 가속도를 60m/s²로 높여 검증했다.
- Unity가 `ProtoHarness.slnx`에 새 어셈블리를 추가하고 `ProjectSettings/SceneTemplateSettings.json`을 생성했다. 직접 ProjectSettings를 편집하지 않았다. 작업 시작 전부터 수정되어 있던 AI Assistant Settings.json은 유지했다.

## 6. 경계

- `Assets/_Project/` — 우리 것
- `Assets/Settings/`, `Assets/TutorialInfo/` — 템플릿 소유, 수정 금지
- `docs/`, `index/`, `tools/`, `.claude/` — Unity 바깥 도구 영역. **Assets 안으로 옮기면 Unity가 컴파일한다**
