# ProtoHarness 아키텍처

> **A모드 진입점.** 구조·시스템·진입점을 묻는 질문은 코드를 뒤지기 전에 이 문서부터 읽는다.
> 이 문서가 코드와 다르면 **문서가 틀린 것**이다. 발견 즉시 고치고 §5대로 알린다.

**최종 갱신**: 2026-11-12 · 문서 다이어트(`docs/doc-diet`). 숫자는 그날 `dev` 의 파일 수와 v0.6 마일스톤 실행 XML 이다. 2026-10-01 판의 옛 무한 모드·2026-09 검증 기록은 `docs/archive/ARCHITECTURE-2026-09.md` 로 옮겼다.

---

## 1. 한 줄 정의

Unity 6 URP 3D 레이싱. 체인을 코너의 봉에 걸어 돌고, 스윙으로 모은 게이지를 부스터로 쓴다. 2D 게임 DosangE/Chain-Rush 의 점프·그래플 흐름에서 출발했다. 지금 플레이할 수 있는 것은 싱글 서킷 레이스(트랙 5개)와 튜토리얼이고, v1.0 은 PC 싱글 + 봇전이다(`DESIGN.md` §1, `DECISIONS.md` 2026-11-12 "방향 재설정").

## 2. 현재 상태 (2026-11-12)

| 항목 | 수 |
|---|---|
| 런타임 스크립트 | **97** — `Runtime/ChainRush/` 82(아래 시스템 지도), `Runtime/Net/` 15(네트워크 레이스, M7 까지 동결). 어셈블리 `ProtoHarness.Runtime`, `ProtoHarness.Net`(NGO 참조, Runtime 은 Net 을 참조하지 않는다) |
| ScriptableObject | 4종 — `TrackDefinition`(트랙 5개 `Data/Circuit_*.asset`), `TrackLook`(`Data/TrackLook_*.asset`), `RunnerProfile`(5명 `Data/RunnerProfile_Alpha~Omega.asset`), `RunRules`(`Data/RunRules_Default.asset`) |
| 에디터 스크립트 | **11** (`Editor/ChainRush/`): 씬·트랙 빌더 `ChainRushSceneBuilder`·`ChainRushCircuitSceneBuilder`, 장식 빌더 5개 `<트랙>EnvironmentBuilder` + 공통 `ExteriorKit`·`ExteriorAssets`·`RoadCorridor`, 트랙 소리 `TrackSoundBuilder` |
| 테스트 | EditMode 24파일, PlayMode 30파일(공용 헬퍼 `CircuitBot`·`ShortcutBot`·`TrackFollower`·`StateTrace`·`TestRoad` 포함). v0.6 마일스톤 실행(2026-11-09, `DECISIONS.md` "main v0.6"): EditMode 394, PlayMode 게이트 299(`Device`·`Enemy`·`NetEndReplay` 제외), Device 3. 그 뒤 2026-11-12 `ChainRushRaceFieldTests` 5건 |
| 씬 (`Assets/_Project/Scenes/`) | 흐름: `ChainRushTitle` → `ChainRushLobby` → `ChainRushCircuit`(달리는 씬, 트랙은 데이터로 바꿔 끼운다). 트랙 장식(충돌체 없음, Additive): `ChainRushTrackStadium`·`Skyway`·`FingerLanes`·`PalmCoast`·`SummitDrop` — 각자 `Prefabs/<트랙>Exterior.prefab` 하나. 테스트 작업대: `ChainRushTestBench`(시험 도로를 코드로 까는 PlayMode 테스트의 그릇, 플레이용 아님). 러너 모형은 `Prefabs/ArmoredRunner.prefab` 에 러너별 look(`Art/Models/Runner*.prefab`)을 입힌다 |
| 아트 원본 | `art-src/<Kit>/`(Blender `.blend` + FBX, Unity 밖). Unity 쪽 FBX 는 `Art/Models/`(`RockKit`·`TreeKit`·`RoadsideKit`·`ShortcutSign`) |
| 템플릿 잔재 | `Assets/TutorialInfo/Scripts/` 2개, `Assets/Scenes/SampleScene.unity` (건드리지 않음) |

참조 게임: https://github.com/DosangE/Chain-Rush · 조사 기준 커밋 `17294d85943dfa9fecd6a70272411e035b1ca6bf`. 코드·아트 파일을 복사하지 않고 움직임의 목적을 새로 구현했다.

## 3. 기술 스택 (확정)

| 층 | 선택 | 비고 |
|---|---|---|
| 엔진 | Unity 6000.3.25f1 | 결정 버전(`DECISIONS.md` 2026-10-01). 이 PC(`C:/Users/User/Desktop/PCUBE/ProtoHarness`)의 에디터는 6000.3.19f1 — 검증 보고에 실제 버전을 적는다 |
| 렌더 | URP 17.3.0 | 설정은 `Assets/Settings/` — Unity 템플릿 소유 |
| 입력 | Input System 1.20.0 | **신 입력 시스템.** `Input.GetKey` 금지 |
| 테스트 | Test Framework 1.6.0 | PlayMode / Editor TestRunnerApi |
| 네트워크 | Netcode for GameObjects 2.13.3 | `Runtime/Net/` 만 쓴다. M7 까지 동결(`DECISIONS.md` 2026-11-12 "방향 재설정") |
| 직렬화 | Newtonsoft Json 3.2.1 | 패키지 의존으로 이미 존재 |
| UI | IMGUI (`OnGUI`) | uGUI·UI Toolkit 은 쓰지 않는다. v1.0 까지 유지 |

## 4. 시스템 지도

> 시스템이 생기면 여기에 **1개당 3줄**로 적는다: 책임 한 줄 / 진입점 파일 / 의존 시스템.
> 3줄을 넘기면 `docs/SYSTEMS/<이름>.md` 로 분리하고 여기엔 링크만 남긴다.

런타임 네임스페이스는 `ProtoHarness.ChainRush`, 에디터는 `ProtoHarness.Editor.ChainRush`.

| 책임 | 진입점 (`Assets/_Project/Scripts/` 기준) | 의존 |
|---|---|---|
| 자동 전진·점프·중력·충돌. `Step(in TickInput)` 으로 틱 입력을 받는다 (장치를 읽지 않음, 시각물을 쓰지 않음). 자유 조향: 입력이 헤딩(`RacerState.Heading`)을 돌리고 전진·그립은 헤딩 기준. 가드(난간) 부딪힘은 감속 + 난간과 나란히 돌림. 코너 봉 스윙(R4b: Shift 를 누른 순간 범위 안 봉에 걸고 누르는 동안 돈다, 스윙 중 조향은 줄 길이, 줄이 짧을수록 빠르다, 스윙 거리 35 m 당 게이지 1칸, 놓으면 그 속도로 직진·넘친 속도는 초당 3 m/s 감소. 별도 드리프트는 없다)와 체인 액션(Ctrl: 그래플 강화 → 슬링샷, 게이지 1칸). 접지 중 경사 속도 보정, 내리막 땅 붙잡기(SphereCast), 낙하 실패는 트랙 기준 높이 `H < -12` | `Runtime/ChainRush/RunnerMotor.cs` | CharacterController, Game(`Track`), Grapple, `TickInput` |
| 이동 규칙 (RF3b, 2026-10-25): 상태 없는 static 클래스. 상태는 `RacerState`, 튜닝은 `RunnerMotor` 직렬화 값으로 채운 중첩 `Settings` 를 틱마다 받는다(실행 중 변경이 바로 반영). `RunDrive`(조향·목표 속력·가속·중력, `FacingOf`/`RightOf`), `Slingshot`(새총 발사·끊기·당김 방향·부스터 가속), `PoleSwing`(봉 걸기·원 돌기·스윙 가속·끝내기), `GuardContact`(가드 부딪힘·긁힘). `RunnerMotor` 에는 직렬화 값·`Step` 순서·`CharacterController` 처리(이동·땅 붙잡기·리스폰)·효과음이 남는다 | `Runtime/ChainRush/Motion/` | `RacerState`, `Ticks`, `Centerline`(`Slingshot.SteerToward`) |
| 몸체 기울이기(표현 전용). `LateUpdate` 에서 모터의 `Velocity`·`Steer` 로 `body.localRotation` 을 쓴다. 공식은 정적 `Evaluate`. 코너 스윙 중에는 몸만 커브 안쪽으로 `SwingLook.SlipAngle` 만큼 튼다 | `Runtime/ChainRush/Visuals/RunnerTilt.cs` (`[DefaultExecutionOrder(100)]`) | Motor, 직렬화 `body` Transform (비어 있으면 LogError 후 비활성화) |
| 코너 스윙 연출 계산(표현 전용, 2026-10-14, R5 2026-10-18). 봉 모델 크기(봉 점 위 `PoleHeight` 20 m, 아래 `PoleDepth` 20 m, `PoleThickness` 1.6 m)와 줄이 무는 띠 `PoleBite`(3 m)·꼭대기 `PoleTop`, 안쪽 난간 걸림점 `RailPoint`(옛 무한 모드용, 2026-10-25 뒤로 런타임에서 부르는 곳 없음 — 테스트만), 안쪽 방향 `InsideSign`, 0.15초 블렌드. `GrappleController`(줄 끝)·`CircuitRace`(봉 모델)·`ChainRushHud`(봉 표시)·`RunnerTilt`(몸)·`FollowCamera`(바깥쪽 0.6 m, 시야각 +4°)가 부른다. 시뮬의 `RunnerMotor.SwingAnchor`(원 중심)는 바꾸지 않는다 | `Runtime/ChainRush/Visuals/SwingLook.cs` (static class) | Track(`TrackFrame`, `RoadMeshBuilder.GuardRailHeight`) |
| 틱 한 번의 조작값: 조향 [-1,1] + 주동작·해제·공격 엣지. 범위 밖이면 `ArgumentOutOfRangeException` | `Runtime/ChainRush/Control/TickInput.cs` (readonly struct) | 없음 |
| 입력 출처 계약: `Poll()`(실행 중 프레임마다) / `Consume()`(틱마다) / `Clear()` | `Runtime/ChainRush/Control/IInputSource.cs` | `TickInput` |
| 틱 사이 버튼 엣지 보존, 조향은 최신값 유지. 순수 로직 | `Runtime/ChainRush/Control/InputLatch.cs` | `TickInput` |
| 입력 기록·재생 (P2, 2026-10-08): `InputLog` 가 한 런의 틱별 `TickInput` 을 담고(항목 i = 틱 i + 1 의 입력), `InputRecorder`(`IInputSource` 데코레이터)가 `Consume()` 때 쌓고, `InputReplay` 가 틱마다 하나씩 돌려준다(끝나면 빈 입력, `Finished`). `Clear()` = 새 런(기록은 비우고 재생은 되감는다)이라 `SetInputSource(replay)` + `StartRun()` 이면 처음부터 재생된다. 직렬화·파일 저장은 없다 | `Runtime/ChainRush/Control/InputLog.cs`, `InputRecorder.cs`, `InputReplay.cs` | `TickInput`, `IInputSource` |
| 롤백 안전성 (P3 준비, 2026-10-08): 서킷 한정 한 틱 스냅샷·복원. `ChainRushGame.CaptureSnapshot()` / `RestoreSnapshot(in SimSnapshot)` 가 틱·`RacerState.Snapshot`(접지 `Grounded` 포함)·러너 위치·`CircuitRace.Snapshot`(`LapCounter.Snapshot`)을 값으로 담고 되돌린다. 무한 모드는 `NotSupportedException`. 한 틱 진입점은 `StepTick(IInputSource)`(`FixedUpdate` 가 부른다). `RunnerMotor` 는 접지를 `Move` 직후 `RacerState.Grounded` 에 복사하고, 매 틱 시작에 컨트롤러를 껐다 켜서(`NormalizeController`) 컨트롤러 내부 위치를 float 위치와 같게 한다 | `Runtime/ChainRush/SimSnapshot.cs`, `RacerState.cs`, `Track/LapCounter.cs`, `Race/CircuitRace.cs`, `ChainRushGame.cs`, `RunnerMotor.cs` | `RacerState`, `LapCounter`, `CircuitRace`, `CharacterController` |
| 키보드 매핑 (카트라이더식, 마우스 없음 — 2026-10-18: ←/→ 조향, 왼쪽 Alt 주동작, 왼쪽 Alt 뗌·↓ 해제, Space 공격, 왼쪽 Shift 봉 스윙(누름 + 유지), 왼쪽 Ctrl 체인 액션(누름)) | `Runtime/ChainRush/Control/KeyboardMouseInputSource.cs` | Input System, `InputLatch` |
| 전방 앵커 선택·줄 길이 제약·해제 부스트. 체인 액션으로 강화되면 빨리 감고 세게 던진다. 슬링샷 당김·봉 스윙 동안 체인 시각물 표시(봉 스윙은 봉의 띠). 매 프레임 지금 걸 수 있는 봉(`PoleCandidate`)을 HUD 에 내준다 | `Runtime/ChainRush/GrappleController.cs` | 직렬화 앵커 배열, LineRenderer, Motor, Game |
| 준비·진행·정지·실패·완주 흐름, 공격·피격 판정 진입점. 트랙 중심선을 소유한다(`Track`) | `Runtime/ChainRush/ChainRushGame.cs` | Motor, Grapple, Camera, Targets, AudioSource, RunRules, `RacerState`, `Centerline` |
| 트랙 좌표계: 직선 조각과 수평 원호(`AppendArc`, 곡률 `TrackFrame.Curvature`, 원호 중심 `CurveCenter`)를 이은 중심선, 조각마다 종단 곡선(경사 연속, 높이 포물선). 월드 위치 → `(S, D, H)` 투영, 그 지점의 앞·오른쪽 축·높이·경사(`TrackFrame`). `ChainRushGame.SetTrack` 으로 교체 가능(유한 모드만). `S` 는 레이스 시작부터의 절대 거리(double)라 원점 이동·조각 버리기 뒤에도 이어진다. 범위 밖은 양 끝 접선으로 연장. 순수 로직. 모터·그래플·카메라·적·코스가 "앞"을 여기서 얻는다. T0 은 두 씬 모두 원점에서 +z 직선 | `Runtime/ChainRush/Track/Centerline.cs` (+ `TrackFrame`, `TrackCoord` readonly struct) | 없음 (`ChainRushGame.Awake` 에서 코드로 생성) |
| 닫힌 서킷 지원 (T4, 2026-10-08): `Close`/`TryClose` 가 끝 = 시작(위치 0.5m·방향 1°·높이 0.1m·끝 경사 0)을 검증해 루프로 만든다. 루프에서는 `FrameAt(s)`·`Project`·`Frame` 의 `S` 가 한 바퀴 길이로 감기고, 조각을 "앞에서부터 첫 번째로 지나지 않은 것" 이 아니라 **가장 가까운 것**으로 고른다(귀환 직선이 출발 조각으로 오인되던 문제). `SetFocus(s)`/`FocusWindow`(기본 150m)는 어떤 중심선에서든 질의를 초점 근처 조각으로 좁힌다. 루프가 닫힌 뒤 `Append`·`TrimBefore` 는 예외. 초점·루프를 안 쓰는 직선·무한 코스는 동작이 같다 | `Runtime/ChainRush/Track/Centerline.cs` | 없음 |
| 절차 노면 (T3a, 2026-10-07): 단면(`RoadProfile`: 반폭·두께·좌/우 가드)을 중심선 `[fromS, toS]` 를 따라 쓸어 노면 판·가드 난간·충돌벽·조명 띠 메시를 만든다(`RoadMeshBuilder`, 순수 계산·할당 없음). `RoadPiece` 는 한 구간의 오브젝트·메시를 한 번 만들고 `Build` 로 다시 채운다(콜라이더는 MeshFilter 와 다른 자식). 서킷 노면은 `CircuitScenery.LayRoad` 가 이것으로 깐다 | `Runtime/ChainRush/Track/RoadPiece.cs` (+ `RoadMeshBuilder` static, `RoadProfile` readonly struct) | `Centerline`, 호출자가 넘기는 재질 3개 |
| 시드 해시 (T3b, 2026-10-08): SplitMix64 섞기. 지금은 리플레이 지문(`StateTrace`)이 쓴다. 같은 때 만든 시드 코스 생성기(`CourseGenerator`·`CourseModule`·`ModuleKind`·`CourseTuning`)는 2026-10-25 무한 모드와 함께 지웠다(`DESIGN.md` §10-1) | `Runtime/ChainRush/Track/SeedHash.cs` (static) | 없음 |
| 레이서 한 명분 시뮬 상태(위치 제외): 체력·적중 수·그래플 수·무적/공격 마감 틱, 속도·조향·점프 예약·코요테 시간, 그래플 앵커 인덱스·줄 길이·빗나감 마감 틱. 운동 값은 `ref` 로 노출한다(모터가 성분을 제자리에서 고치고, 그래플의 해제 부스트가 같은 메모리를 고친다). 순수 로직. `ChainRushGame.Racer` 로 접근 | `Runtime/ChainRush/RacerState.cs` | `RunRules` (생성자 인자, null 이면 `ArgumentNullException`) |
| 추적 카메라·속도에 따른 FOV. 러너 헤딩 뒤를 따르고 yaw 회전은 `maxYawSpeed`(180°/s) 상한. 높이 하한은 중심선 높이 기준. 첫 맞춤은 `Start`(트랙이 `Awake` 에서 생기므로) | `Runtime/ChainRush/FollowCamera.cs` | Motor, Game(`Track`), Camera |
| 공격 표적·위험물 접촉·복구 | `Runtime/ChainRush/CourseTarget.cs` | 직렬화 Visual Transform |
| 시작 안내·HUD(체인 게이지 2칸, 앵커·걸 수 있는 봉 표시 포함)·결과 화면 | `Runtime/ChainRush/ChainRushHud.cs` | Game, Motor, Grapple, Camera |
| 게임 흐름 (M2-a1, 2026-10-19, DECISIONS "M2 씬 구성"): 타이틀 씬(`TitleScreen`) → 로비 씬(`LobbyMenu`: 싱글 / 멀티 잠금 / 튜토리얼 / 설정, 트랙·속도 단계·러너 — 러너 페이지는 `RunnerPreview` 가 실행 중에 만든 무대를 RenderTexture 로 그려 드래그로 돌리고, 입장 버튼·Enter 로 출발, 모든 페이지에 이전 버튼, 2026-10-20) → `RaceSetup` 을 `RaceLaunch`(static 전달 자리)에 두고 달리는 씬(`ChainRushCircuit`)을 연다. 달리는 씬의 `RaceAssembler`(실행 순서 -100)가 정의를 바꿔 끼우고(`CircuitRace.UseDefinition`) 장식 씬을 Additive 로 부른 뒤 카운트다운, 결과에서 ESC·버튼으로 로비. 레이스 중 ESC 는 `ChainRushHud` 의 일시정지 메뉴(계속·재시작·로비로, R 재시작은 2026-10-20 제거). 로비에서 온 완주는 `BestRecords`(트랙 × 속도 단계, `PlayerPrefs`)에 내고, 로비 트랙 페이지가 그것을 보인다. 요청 없이 연 씬은 저장된 대로 돈다. 씬은 이름으로 부르므로 빌드 씬 목록에 있어야 한다 | `Runtime/ChainRush/Flow/` | Game, CircuitRace, `TrackDefinition`, SceneManager, HUD(선택 참조) |
| 로비 페이지 (RF7, 2026-10-25): 일반 클래스. 페이지마다 `LobbyPage` 하위 클래스 하나(`MainPage`·`SettingsPage`·`KeysPage`·`TrackPage`·`SpeedPage`·`RunnerPage`·`TutorialPage`)가 줄 이름·잠금·설명, `Confirm`/`Back` 이 돌려주는 이동(`LobbyPage.Step`: 머무름·페이지 열기·출발·타이틀), 줄과 오른쪽 패널 그리기를 든다. 같이 쓰는 상태(트랙·러너·설정·기록·고른 값·프리뷰)는 `LobbyContext`, 스타일·공통 틀·목록·설명 패널은 `LobbyView`(OnGUI 안에서 만든다). `LobbyMenu`(MonoBehaviour)는 입력·커서·효과음·페이지 전환·씬 이동·`Awake` 검사·공개 API 만 든다. 새 페이지 = 하위 클래스 + `LobbyMenu.Page` 값 + `Awake` 의 페이지 목록 한 줄(순서가 enum 과 다르면 `Awake` 가 던진다) | `Runtime/ChainRush/Flow/Lobby/` | `LobbyMenu`, `RunnerPreview`, `GameSettings`, `BestRecords`, `NightUi`, `UiSound` |
| 튜토리얼 (2026-11-06, DECISIONS "튜토리얼"): 레이스가 아닌 미션 맵. `TutorialCourse`(static)가 맵 크기·스테이지·목표 표·판정(`IsMet`)을 들고, `TutorialDirector`(MonoBehaviour, `RaceAssembler` 가 런타임에 붙인다)가 틱마다 목표를 판정하고 빛기둥을 옮기고 스테이지 사이에 설명 팝업을 연다. 팝업을 닫으면 `ChainRushGame.StartRun` 으로 바로 시작한다(카운트다운 없음). 그림은 `TutorialFigures`(static)가 HUD 의 `DrawTarget`·`DrawArrow`·`DrawBoostGauge` 로 그린다. 맵은 봉을 감싸는 고리 중심선(`TrackDefinition.Ring`, 에셋 없음) 위의 원판(`CircuitRace.UseArena`, `CircuitScenery.LayArena`·`PaintArena`)이라 랩·출발선이 없고, 기록·고스트도 없다. 봉은 `RunnerMotor.ReachPolesFrom` 으로 바닥 어디서나 걸린다. 시뮬은 읽기만 한다 | `Runtime/ChainRush/Tutorial/` | Game, Motor, `CircuitRace`, `RaceAssembler`, HUD(`DrawBriefing`·`DrawTutorial`·`DrawTutorialComplete`) |
| 씬 생성·열기·테스트 실행 메뉴. 발판 양쪽 가드 난간(`AddGuards`, 충돌 4m)과 기존 씬용 메뉴 `Add Deck Guards To Open Scene` | `Editor/ChainRush/ChainRushSceneBuilder.cs` | EditorSceneManager, AssetDatabase, TestRunnerApi |
| 수제 서킷 (T4, 2026-10-08): `TrackDefinition`(SO)이 직선·원호·종단 곡선 조각 목록(틈은 노면 없는 평지 직선) + 앵커 + 체크포인트(랩 비율) + 출발 칸 + 랩 수 + 노면 폭을 담고 `TryValidate` 로 규칙(반지름 ≥ 20m, 틈 앞뒤 평지 직선 ≥ 15m, 앵커는 틈 위)과 **닫힘**을 검증한다. 기본값은 사각 서킷(R4c 2026-10-18: 직선 140m 에 언덕 + 점프 틈, R22 90°, 직선 60m, R22 90°, 직선 140m 에 그래플 틈, R22 90°, 직선 60m, R22 90°, 한 바퀴 538.2m). 두 번째 정의 `Data/Circuit_Skyway.asset`(2026-10-21, 고가 서킷 29조각·1482.8m·2랩·앵커 없음)은 에디터 메뉴 `Create Skyway Track`(`ChainRushCircuitSceneBuilder.SkywaySegments`)가 만들고 다시 쓴다. `LapCounter`(순수 로직)가 틱마다 접힌 `S` 를 누적 진행도로 더해 체크포인트·랩 완료·랩 시간을 센다(역주행은 진행도만 줄고 이미 지난 선은 다시 세지 않는다) | `Runtime/ChainRush/Track/TrackDefinition.cs`, `LapCounter.cs` | `Centerline` |
| 서킷 레이스 (T4): `CircuitRace` 가 정의로 닫힌 중심선을 만들어 게임에 넘기고(`BuildTrack`), 노면을 한 번만 깔고(`RoadPiece` ≤ 50m, 틈 건너뜀), 앵커·출발선·체크포인트 표지와 원호마다 코너 봉 모델(충돌체 없음, R5)을 놓고, 매 틱 `PrepareTick`(초점 = 직전 `S`) → 입력 → 이동 → `Step`(투영 + `LapCounter`)을 돈다. 시계는 `StartRun` 틱 0, 러너는 출발선 위 `S 0`, 1랩 = 선에서 선, 완주 = 3랩. 낙하하면 틈 앞 트랙으로 복귀하고 1초 멈춘다(R2, 런은 계속). 적·`CourseTarget`·`EnemyDirector` 없음. `ChainRushCircuit` 씬에서 쓴다 | `Runtime/ChainRush/Race/CircuitRace.cs` | `TrackDefinition`(`Data/Circuit_Stadium.asset`), `LapCounter`, `RoadPiece`, Game, Motor |
| 서킷 장면 (RF4, 2026-10-25): 일반 클래스. `CircuitRace` 가 정의·닫힌 중심선·부모 Transform·머티리얼 3개로 만들고 `LayRoad`(노면 `RoadPiece`, 최대 길이 `maxPieceLength`, 틈 건너뜀, 폭이 바뀌는 조각은 따로) → (앵커는 `CircuitRace`) → `PlaceMarkers`(출발선·체크포인트 띠) → `PlacePoles`(코너·스윙 봉) 순서로 부른다. 띠와 봉은 충돌체 없음. M3 트랙 장식(T3e)이 붙을 자리 | `Runtime/ChainRush/Race/CircuitScenery.cs` | `TrackDefinition`, `Centerline`, `RoadPiece`, `SwingLook` |
| 상대가 있는 레이스 (MP1, 2026-11-12, DECISIONS "MP1 구조"·"MP1"): 상대마다 서킷 씬 복사본을 자기 물리 씬으로 로드하고(`RaceWorld.IsolatedLoad` = Additive + `LocalPhysicsMode.Physics3D`, `RaceWorld.FromScene` 이 게임·러너·그래플·서킷을 묶는다), `RaceField`(MonoBehaviour, `RaceAssembler` 가 런타임에 붙인다)가 모든 게임의 `SelfStepping` 을 끄고 매 `FixedUpdate` 에 플레이어 게임 → 상대들 순서로 `StepTick()` 을 한 번씩 부른다. 같은 틱 출발, 일시정지·출발선을 따라가고, 틱이 어긋나면 `LogError` 뒤 멈춘다. 상대 월드(`RaceSetup.IsOpponentWorld`)는 러너만 보이고 카메라·조명·HUD·소리·기록·고스트가 없다. 러너끼리 접촉은 아직 없다(MP2). 상대가 없으면 조정자도 없다 | `Runtime/ChainRush/Race/RaceField.cs`, `RaceWorld.cs`, `Flow/RaceAssembler.cs`(`LoadOpponents`), `Flow/RaceSetup.cs`(`WithOpponents`·`ForOpponent`) | Game, `RaceLaunch`, SceneManager |
| 트랙 look (2026-10-30): 트랙의 도로·벽·차선 표시 머티리얼, 하늘·지평선 안개, 음악·환경음. 로비 항목 → `RaceSetup.Look` → `CircuitRace.UseLook`·`RaceAssembler.UseSky`. 표현 전용(시뮬은 읽지 않는다) | `Runtime/ChainRush/Race/TrackLook.cs` (SO, `Data/TrackLook_*.asset`) | `CircuitScenery.PaintMarkings` |
| 기록·고스트 (M4, 2026-10-29): 트랙 × 속도 단계마다 최고 기록 하나(`BestRecords`, `PlayerPrefs`)와 그 런의 고스트(`GhostTrace` = 틱마다 위치·방향, `GhostFiles` = 파일, `Application.persistentDataPath/Ghosts`). `GhostRunner` 가 러너 모형의 반투명 복제를 틱에 맞춰 세운다(콜라이더 없음, 입력 재생이 아니다). 결과 화면 비교는 `FinishComparison`. 상대 월드·튜토리얼은 기록이 없다 | `Runtime/ChainRush/Flow/BestRecords.cs`, `GhostFiles.cs`, `GhostTrace.cs`, `FinishComparison.cs`, `Visuals/GhostRunner.cs` | `RaceAssembler`(결승 때 `KeepFinish`) |
| 조작 피드백·이펙트 (M4·M6, 표현 전용): 걸 수 있는 봉·앵커 표시, 부스터·걸림·빗나감 고리(`ControlFeedback`, HUD 가 붙인다), 봉 스윙 불꽃·줄 끊김(`SwingEffects`), 속도 꼬리·바람 선(`SpeedTrails`), 복귀 연출(`RespawnLook`), 상태 가장자리(`StateEdge`), 수치(`FeedbackLook`) | `Runtime/ChainRush/Visuals/` | Game, Motor, Grapple (읽기만) |
| 트랙 장식 (에디터 전용, 2026-10-21~): 트랙마다 `<트랙>EnvironmentBuilder` 가 도형·킷 FBX(`ExteriorKit.KitMesh`)로 장식 프리팹 `Prefabs/<트랙>Exterior.prefab` 을 짓고, 공통 머티리얼·`TrackLook`·장식 전용 검사·저장은 `ExteriorAssets`, 도로 침범 검사는 `RoadCorridor`. 장식에는 콜라이더·런타임 스크립트가 없어 시뮬에 닿지 않는다. 킷 계획은 `docs/ART_REQUEST_PROPS.md` | `Editor/ChainRush/*EnvironmentBuilder.cs`, `ExteriorKit.cs`, `ExteriorAssets.cs`, `RoadCorridor.cs` | `TrackDefinition`, `Centerline` |
| 네트워크 레이스 (P3 C1, 2026-10-10~13, **M7 까지 동결**): NGO 서버 권위. 서버(`RaceServer`)가 레이서마다 격리 월드·입력 버퍼(`ServerInputBuffer`)·지연 큐(`DelayedSender`)를 들고, 클라(`RaceClient`)는 `ClientPredictor` 로 자기만 예측·보정(`SimSnapshotCodec`)하고 상대는 `RemoteInterpolator`·`RemoteGhost` 로 그린다. 순위는 `RaceStandings`(결승)·`RaceOrder`(주행 중). 같은 플레이어 빌드가 `-raceServer`/`-raceClient` 로 둘 다 된다(`RaceBootstrap`). 결정 이력은 `docs/archive/DECISIONS-2026-10.md` 의 "P3" 항목들 | `Runtime/Net/` (asmdef `ProtoHarness.Net`) | Game, `RaceWorld`, `InputLogFile`, NGO |
| 러너 프로필 (M2-b, 2026-10-19): 이름 + 씬의 `RunnerMotor` 값에 곱하는 배율(최고 속도·부스터, 가속, 게이지 충전, 무게 — 무게는 MP2 의 러너끼리 접촉 전까지 효과 없음). ALPHA 가 모두 1 이라 씬 값 그대로. `RunnerMotor.Configure` 가 런 밖에서만 받는다 | `Runtime/ChainRush/RunnerProfile.cs` (SO, `Data/RunnerProfile_Alpha~Omega.asset`) | 없음 (`RaceAssembler` 가 로비의 선택을 `RunnerMotor` 에 넘김) |
| 체력·피격 무적·공격 쿨다운·공격 시각 지속 데이터 | `Runtime/ChainRush/RunRules.cs` (SO, 기본값 `Data/RunRules_Default.asset`) | 없음 (`ChainRushGame` 이 직렬화 참조로 사용, 비어 있으면 LogError 후 비활성화) |
| 신스 음악·14종 효과음·바람·음소거. 봉 걸림·그래플·스윙 루프는 CC0 녹음(`Audio/Chain_*.mp3`)을 읽어 다듬고 나머지는 합성한다 | `Runtime/ChainRush/Audio/ChainRushAudio.cs` | Game, Motor, AudioSource 3개, 녹음 AudioClip 3개 |
| 달리기·점프·착지·그래플·공격 관절 자세 | `Runtime/ChainRush/Visuals/RunnerAnimation.cs` | Game, Motor, Grapple, Audio, 직렬화 관절 Transform |

**고정 틱 (2026-10-02)**: 시뮬레이션의 진입점은 `ChainRushGame.StepTick` 하나이고(`FixedUpdate` 가 부른다, 롤백 안전성 2026-10-08) 한 호출이 한 틱(`Ticks.Seconds` = 0.02s)이다. 틱 안의 순서는 `IInputSource.Consume()` 로 받은 `TickInput` → 공격 → `RunnerMotor.Step(input)`(점프·그래플·해제 포함) → 장애물 접촉 → 완주 판정(서킷이면 `CircuitRace.Step`). (무한 모드의 `EnemyDirector.Step` → `CourseStream.Step` 은 2026-10-25 삭제.) `Update`/`LateUpdate`는 입력 `Poll`(실행 중일 때만), 메뉴 키(R·Enter·Esc, 음소거 M)와 표현(카메라·애니메이션·HUD·체인 시각물·오디오)만 한다. 시뮬은 장치를 직접 읽지 않고 `ChainRushGame.SetInputSource` 로 입력 출처를 바꿀 수 있다(기본값 `KeyboardMouseInputSource`, `Awake` 에서 코드로 생성). 시간은 정수 틱(`ChainRushGame.Tick`)으로 세고 `Elapsed` 는 파생값이다. 초 단위 수치는 `Ticks.FromSeconds` 로 올림 환산한다(`RunRules` 의 `*Ticks` 프로퍼티). 상대가 있는 레이스에서는 `ChainRushGame.SelfStepping` 이 꺼지고 `RaceField` 가 `StepTick()` 을 부른다 — 한 틱은 같은 틱이다. `Time.fixedDeltaTime` 이 `Ticks.Seconds` 와 다르면 `ChainRushGame` 이 LogError 후 비활성화된다.
CharacterController.Move로 충돌을 처리하고, `RunnerMotor.Step`에서 중력과 전진 속도를 적분한다.
그래플링은 길이 제한 구면으로 예상 위치를 투영하고 바깥쪽 방사 속도를 제거한다. 줄은 초당 3m씩 감긴다. 해제 시 최소 상승 속도는 지상 점프와 동일한 11.5m/s이다.
3D Joint 컴포넌트는 사용하지 않는다. 동적 Rigidbody 물체를 끌거나 줄이 장애물에 감기는 동작은 현재 범위 밖이다.
장애물(`CourseTarget`, 서킷에는 없다)은 박스 범위 접촉으로 체력을 줄이며 물리적으로 플레이어를 막지 않는다. 물리 질의(가드 탐지·땅 붙잡기·앵커 시야)는 러너가 속한 씬의 `PhysicsScene` 으로 한다 — 격리 월드의 러너는 다른 월드를 보지 않는다.
게임 상태를 각 시스템이 확인하므로 일시정지에 전역 Time.timeScale 변경이 필요 없다.

## 5. 진입점

| 무엇 | 어디 |
|---|---|
| 작업 규칙 | `CLAUDE.md` (항상 지킬 것) + `docs/RULES/*.md` (작업별, 읽는 시점은 `CLAUDE.md` 맨 위 표) |
| 결정 이력 | `docs/DECISIONS.md`(이번 달) + `docs/archive/DECISIONS-<YYYY-MM>.md`(지난 달, 날짜로 찾는다) |
| 인계 | `docs/HANDOFF.md` — 다음 세션이 이어받을 일만 |
| 설계·로드맵 | `docs/DESIGN.md` (§5-1 마일스톤), 다음 작업 요청서 묶음 `docs/NEXT_REQUESTS.md` |
| 심볼 인덱스 (선택 도구) | `index/symbols.tsv` — 생성물, `tools/reindex.ps1` 로 재생성. 2026-11-13 부터 조회 라우터의 첫 조회가 아니다(`CLAUDE.md` §6-1) |
| 서브에이전트 정의 | `.claude/agents/*.md` (7종) |
| 하네스 비용 측정 | `tools/harness-cost.py` — 이 PC 의 Claude Code 세션 기록에서 토큰·조회·파견·훅 지표를 뽑는다. 읽기만 한다 |
| 테스트 실행 기록 | `tools/test-runs.ps1` — `-Record` 로 실행마다 남기고 `-Last -Passed` 로 그 뒤 바뀐 `Assets/` 를 본다. 기록은 리포 밖(이 PC 의 `LOCALAPPDATA`) |
| 게임 씬 | 플레이: `ChainRushTitle.unity` 에서 Play(타이틀 → 로비 → 달리는 씬). 달리는 씬 `ChainRushCircuit.unity` 를 바로 열면 저장된 대로 돈다. 테스트 작업대: `ChainRushTestBench.unity` |
| 처음 만들기 | Unity 메뉴 `ProtoHarness > Chain Rush > Create Test Bench Scene` (작업대), `Create Circuit Scene` (서킷), `Create <트랙> Track` (트랙 정의), `Create/Rebuild <트랙> Exterior` (장식). 기존 게임 씬을 덮어쓰지 않는다. |
| 실행 | 로비에서 모드 → 속도 → 트랙 → 러너 → 입장(Enter). 달리는 씬을 바로 열었으면 Enter 로 3·2·1 카운트다운 |
| 조작 | ↑ 가속(떼면 감속, 부스터가 끊긴다), ←/→ 조향(봉 스윙 중에는 줄 길이), ↓ 브레이크·후진 / 그래플 해제, 왼쪽 Alt 지상 점프 · 공중 그래플(떼면 해제), 왼쪽 Shift 코너 봉 스윙(누르는 동안), 왼쪽 Ctrl 체인 액션(부스터 / 그래플 중 강화), Space 공격(서킷에는 대상이 없다), Esc 일시정지, M 음소거 (`Control/KeyboardMouseInputSource.cs:31-41`, `ChainRushGame.cs:163-164`, `Audio/ChainRushAudio.cs:162`) |
| 테스트 | `ProtoHarness > Chain Rush > Run PlayMode Tests` (병합 조건용, `Device`·`Enemy`·`NetEndReplay` 카테고리 제외) / `Run Device Input Tests` (가상 키보드 테스트만) |
| 테스트 결과 | Unity 프로세스의 `Path.GetTempPath()` 아래 `ChainRush-PlayMode-results.xml` (EditMode 결과도 같은 파일에 덮어쓴다). 경로는 머신마다 다르므로 실행할 때 `Unity_RunCommand` 로 `Path.GetTempPath()` 를 읽어 확인한다 |

HUD·로비·타이틀은 IMGUI 와 OS 동적 폰트(Malgun Gothic/Arial, 카운트다운은 Bahnschrift), 효과음은 메모리 안에서 합성한 AudioClip 과 CC0 녹음 3개(봉 걸림·그래플·스윙 루프, `Audio/Chain_*.mp3`), 트랙 음악·환경음은 `TrackLook` 이 든다. 러너 애니메이션은 Transform 관절의 절차 애니메이션이고 `Animator` 는 쓰지 않는다.

2026-10-01 판의 옛 직선 코스 수치, 무한 모드 절(2026-10-25 삭제), 2026-09-08~09 검증·아트·사운드·애니메이션 기록은 `docs/archive/ARCHITECTURE-2026-11-12.md`(이 문서의 2026-11-12 다이어트 전 판)에 그대로 있다.

## 6. 경계

- `Assets/_Project/` — 우리 것
- `Assets/Settings/`, `Assets/TutorialInfo/` — 템플릿 소유, 수정 금지
- `docs/`, `index/`, `tools/`, `.claude/` — Unity 바깥 도구 영역. **Assets 안으로 옮기면 Unity가 컴파일한다**
