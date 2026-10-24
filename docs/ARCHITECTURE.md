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
| 우리 런타임 스크립트 | **26** (`Scripts/Runtime/ChainRush/` 및 하위 폴더). 그중 ScriptableObject 2개(`EncounterTuning`, `RunRules`, 2026-10-01 기준 — `EncounterTuning` 은 2026-10-25 무한 모드와 함께 지웠다), 인스턴스는 `Assets/_Project/Data/` |
| 우리 에디터 스크립트 | **3** (`ChainRushSceneBuilder`, `ChainRushCircuitSceneBuilder`, `SkywayEnvironmentBuilder`) |
| 우리 테스트 | PlayMode **12 파일 / 46 테스트** (그중 `Device` 카테고리 2개, 공용 헬퍼 `TestRoad.cs`·`TrackFollower.cs` 포함), EditMode **9 파일 / 131 테스트** (`Scripts/Tests/EditMode/`, `ProtoHarness.Tests.EditMode`). 2026-10-07 실행 XML 기준 |
| 템플릿 잔재 | `Assets/TutorialInfo/Scripts/` 2개 (건드리지 않음) |
| 게임 씬 | `Assets/_Project/Scenes/ChainRushTestBench.unity` (테스트 작업대 — 시험 도로를 코드로 까는 PlayMode 테스트의 그릇. 플레이용이 아니다. 2026-10-14 `ChainRushPrototype` 에서 이름만 바꿈, 2026-10-15 R3 에서 첫 구간(발판·앵커·표적 1개씩)만 남김), `ChainRushProcedural.unity` (절차 무한 전투 맵), `ChainRushCircuit.unity` (서킷 레이스 = 달리는 씬, 2026-10-19 꺼진 옛 직선 코스 `Skyline Course` 를 걷어냄), `ChainRushTitle.unity`·`ChainRushLobby.unity` (타이틀·로비), `ChainRushTrackStadium.unity`·`ChainRushTrackSkyway.unity` (스타디움·SKYWAY 전용 외부 장식, 충돌체 없음, Additive). SKYWAY 장식은 `Prefabs/SkywayExterior.prefab`을 배치하며 제작·연결 도구는 `SkywayEnvironmentBuilder`(2026-10-21, 검증 기록 `ART_SKYWAY.md`). 옛 직선 무한 씬 `ChainRushEndless` 는 T3d(2026-10-08)에서 삭제. 세 씬의 러너 모형은 프리팹 `Assets/_Project/Prefabs/ArmoredRunner.prefab` 하나다(2026-10-15 R3) |
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
| 절차 노면 (T3a, 2026-10-07): 단면(`RoadProfile`: 반폭·두께·좌/우 가드)을 중심선 `[fromS, toS]` 를 따라 쓸어 노면 판·가드 난간·충돌벽·조명 띠 메시를 만든다(`RoadMeshBuilder`, 순수 계산·할당 없음). `RoadPiece` 는 한 구간의 오브젝트·메시를 한 번 만들고 `Build` 로 다시 채운다(풀용, 콜라이더는 MeshFilter 와 다른 자식). **아직 씬에서 쓰지 않는다**(테스트 `ChainRushRoadTests` 만, 씬 연결은 T3c) | `Runtime/ChainRush/Track/RoadPiece.cs` (+ `RoadMeshBuilder` static, `RoadProfile` readonly struct) | `Centerline`, 호출자가 넘기는 재질 3개 |
| 시드 해시 (T3b, 2026-10-08): SplitMix64 섞기. 지금은 리플레이 지문(`StateTrace`)이 쓴다. 같은 때 만든 시드 코스 생성기(`CourseGenerator`·`CourseModule`·`ModuleKind`·`CourseTuning`)는 2026-10-25 무한 모드와 함께 지웠다(`DESIGN.md` §10-1) | `Runtime/ChainRush/Track/SeedHash.cs` (static) | 없음 |
| 레이서 한 명분 시뮬 상태(위치 제외): 체력·적중 수·그래플 수·무적/공격 마감 틱, 속도·조향·점프 예약·코요테 시간, 그래플 앵커 인덱스·줄 길이·빗나감 마감 틱. 운동 값은 `ref` 로 노출한다(모터가 성분을 제자리에서 고치고, 그래플의 해제 부스트가 같은 메모리를 고친다). 순수 로직. `ChainRushGame.Racer` 로 접근 | `Runtime/ChainRush/RacerState.cs` | `RunRules` (생성자 인자, null 이면 `ArgumentNullException`) |
| 추적 카메라·속도에 따른 FOV. 러너 헤딩 뒤를 따르고 yaw 회전은 `maxYawSpeed`(180°/s) 상한. 높이 하한은 중심선 높이 기준. 첫 맞춤은 `Start`(트랙이 `Awake` 에서 생기므로) | `Runtime/ChainRush/FollowCamera.cs` | Motor, Game(`Track`), Camera |
| 공격 표적·위험물 접촉·복구 | `Runtime/ChainRush/CourseTarget.cs` | 직렬화 Visual Transform |
| 시작 안내·HUD(체인 게이지 2칸, 앵커·걸 수 있는 봉 표시 포함)·결과 화면 | `Runtime/ChainRush/ChainRushHud.cs` | Game, Motor, Grapple, Camera |
| 게임 흐름 (M2-a1, 2026-10-19, DECISIONS "M2 씬 구성"): 타이틀 씬(`TitleScreen`) → 로비 씬(`LobbyMenu`: 싱글 / 멀티 잠금 / 설정, 트랙·속도 단계·러너 — 러너 페이지는 `RunnerPreview` 가 실행 중에 만든 무대를 RenderTexture 로 그려 드래그로 돌리고, 입장 버튼·Enter 로 출발, 모든 페이지에 이전 버튼, 2026-10-20) → `RaceSetup` 을 `RaceLaunch`(static 전달 자리)에 두고 달리는 씬(`ChainRushCircuit`)을 연다. 달리는 씬의 `RaceAssembler`(실행 순서 -100)가 정의를 바꿔 끼우고(`CircuitRace.UseDefinition`) 장식 씬을 Additive 로 부른 뒤 카운트다운, 결과에서 ESC·버튼으로 로비. 레이스 중 ESC 는 `ChainRushHud` 의 일시정지 메뉴(계속·재시작·로비로, R 재시작은 2026-10-20 제거). 로비에서 온 완주는 `BestRecords`(트랙 × 속도 단계, `PlayerPrefs`)에 내고, 로비 트랙 페이지가 그것을 보인다. 요청 없이 연 씬은 저장된 대로 돈다. 씬은 이름으로 부르므로 빌드 씬 목록에 있어야 한다 | `Runtime/ChainRush/Flow/` | Game, CircuitRace, `TrackDefinition`, SceneManager, HUD(선택 참조) |
| 씬 생성·열기·테스트 실행 메뉴. 발판 양쪽 가드 난간(`AddGuards`, 충돌 4m)과 기존 씬용 메뉴 `Add Deck Guards To Open Scene` | `Editor/ChainRush/ChainRushSceneBuilder.cs` | EditorSceneManager, AssetDatabase, TestRunnerApi |
| 수제 서킷 (T4, 2026-10-08): `TrackDefinition`(SO)이 직선·원호·종단 곡선 조각 목록(틈은 노면 없는 평지 직선) + 앵커 + 체크포인트(랩 비율) + 출발 칸 + 랩 수 + 노면 폭을 담고 `TryValidate` 로 규칙(반지름 ≥ 20m, 틈 앞뒤 평지 직선 ≥ 15m, 앵커는 틈 위)과 **닫힘**을 검증한다. 기본값은 사각 서킷(R4c 2026-10-18: 직선 140m 에 언덕 + 점프 틈, R22 90°, 직선 60m, R22 90°, 직선 140m 에 그래플 틈, R22 90°, 직선 60m, R22 90°, 한 바퀴 538.2m). 두 번째 정의 `Data/Circuit_Skyway.asset`(2026-10-21, 고가 서킷 29조각·1482.8m·2랩·앵커 없음)은 에디터 메뉴 `Create Skyway Track`(`ChainRushCircuitSceneBuilder.SkywaySegments`)가 만들고 다시 쓴다. `LapCounter`(순수 로직)가 틱마다 접힌 `S` 를 누적 진행도로 더해 체크포인트·랩 완료·랩 시간을 센다(역주행은 진행도만 줄고 이미 지난 선은 다시 세지 않는다) | `Runtime/ChainRush/Track/TrackDefinition.cs`, `LapCounter.cs` | `Centerline` |
| 서킷 레이스 (T4): `CircuitRace` 가 정의로 닫힌 중심선을 만들어 게임에 넘기고(`BuildTrack`), 노면을 한 번만 깔고(`RoadPiece` ≤ 50m, 틈 건너뜀), 앵커·출발선·체크포인트 표지와 원호마다 코너 봉 모델(충돌체 없음, R5)을 놓고, 매 틱 `PrepareTick`(초점 = 직전 `S`) → 입력 → 이동 → `Step`(투영 + `LapCounter`)을 돈다. 시계는 `StartRun` 틱 0, 러너는 출발선 위 `S 0`, 1랩 = 선에서 선, 완주 = 3랩. 낙하하면 틈 앞 트랙으로 복귀하고 1초 멈춘다(R2, 런은 계속). 적·`CourseTarget`·`EnemyDirector` 없음. `ChainRushCircuit` 씬에서 쓴다 | `Runtime/ChainRush/Race/CircuitRace.cs` | `TrackDefinition`(`Data/Circuit_Stadium.asset`), `LapCounter`, `RoadPiece`, Game, Motor |
| 러너 프로필 (M2-b, 2026-10-19): 이름 + 씬의 `RunnerMotor` 값에 곱하는 배율(최고 속도·부스터, 가속, 게이지 충전, 무게 — 무게는 M7 까지 효과 없음). ALPHA 가 모두 1 이라 씬 값 그대로. `RunnerMotor.Configure` 가 런 밖에서만 받는다 | `Runtime/ChainRush/RunnerProfile.cs` (SO, `Data/RunnerProfile_Alpha~Omega.asset`) | 없음 (`RaceAssembler` 가 로비의 선택을 `RunnerMotor` 에 넘김) |
| 체력·피격 무적·공격 쿨다운·공격 시각 지속 데이터 | `Runtime/ChainRush/RunRules.cs` (SO, 기본값 `Data/RunRules_Default.asset`) | 없음 (`ChainRushGame` 이 직렬화 참조로 사용, 비어 있으면 LogError 후 비활성화) |
| 신스 음악·9종 효과음·바람·음소거 | `Runtime/ChainRush/Audio/ChainRushAudio.cs` | Game, Motor, AudioSource 3개 |
| 달리기·점프·착지·그래플·공격 관절 자세 | `Runtime/ChainRush/Visuals/RunnerAnimation.cs` | Game, Motor, Grapple, Audio, 직렬화 관절 Transform |

**고정 틱 (2026-10-02)**: 시뮬레이션의 진입점은 `ChainRushGame.StepTick` 하나이고(`FixedUpdate` 가 부른다, 롤백 안전성 2026-10-08) 한 호출이 한 틱(`Ticks.Seconds` = 0.02s)이다. 틱 안의 순서는 `IInputSource.Consume()` 로 받은 `TickInput` → 공격 → `RunnerMotor.Step(input)`(점프·그래플·해제 포함) → 장애물 접촉 → 완주 판정(서킷이면 `CircuitRace.Step`). (무한 모드의 `EnemyDirector.Step` → `CourseStream.Step` 은 2026-10-25 삭제.) `Update`/`LateUpdate`는 입력 `Poll`(실행 중일 때만), 메뉴 키(R·Enter·Esc, 음소거 M)와 표현(카메라·애니메이션·HUD·체인 시각물·오디오)만 한다. 시뮬은 장치를 직접 읽지 않고 `ChainRushGame.SetInputSource` 로 입력 출처를 바꿀 수 있다(기본값 `KeyboardMouseInputSource`, `Awake` 에서 코드로 생성). 시간은 정수 틱(`ChainRushGame.Tick`)으로 세고 `Elapsed` 는 파생값이다. 초 단위 수치는 `Ticks.FromSeconds` 로 올림 환산한다(`RunRules`, `EncounterTuning` 의 `*Ticks` 프로퍼티). `Time.fixedDeltaTime` 이 `Ticks.Seconds` 와 다르면 `ChainRushGame` 이 LogError 후 비활성화된다.
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
| 게임 씬 | 플레이: `ChainRushCircuit.unity`(서킷). 테스트 작업대: `ChainRushTestBench.unity` |
| 처음 만들기 | Unity 메뉴 `ProtoHarness > Chain Rush > Create Test Bench Scene` (Ctrl+Shift+G, 작업대), `Create Circuit Scene` (서킷). 기존 게임 씬을 덮어쓰지 않는다. |
| 열기 | `ProtoHarness > Chain Rush > Open Test Bench Scene` |
| 표현 적용 | `ProtoHarness > Chain Rush > Apply Art Sound Animation` (Ctrl+Shift+J). (옛 무한 씬에 쓰던 메뉴 — 지금 메뉴 목록에 없음, 확인 2026-10-25) 무한 씬에 한 번 적용, 중복 적용 시 명시적 오류. |
| 실행 | Play → Enter 또는 START RUN 버튼 |
| 조작 | ←/→ 로 진행 방향 틀기(자유 조향, 2026-10-05; 2026-10-18 부터 방향키만), 왼쪽 Alt 지상 점프 / 공중에서 다시 Alt 그래플, Alt 유지 스윙 / Alt 뗌·↓ 해제, Space 공격, 왼쪽 Shift 코너 봉 스윙(유지 동안, 스윙 중 ←/→ 는 줄 길이, 게이지 충전 — R4b 부터 드리프트 대신), 왼쪽 Ctrl 체인 액션(슬링샷 / 그래플 중 강화), R 재시작, Esc 일시정지 |
| 테스트 | `ProtoHarness > Chain Rush > Run PlayMode Tests` (병합 조건용, `Device`·`Enemy`·`NetEndReplay` 카테고리 제외) / `Run Device Input Tests` (가상 키보드 테스트만) |
| 테스트 결과 | Unity 프로세스의 `Path.GetTempPath()` 아래 `ChainRush-PlayMode-results.xml` (EditMode 결과도 같은 파일에 덮어쓴다). 경로는 머신마다 다르므로 실행할 때 `Unity_RunCommand` 로 `Path.GetTempPath()` 를 읽어 확인한다 |

코스: 9개 플랫폼, 8개 낭떠러지와 앵커, 결승 z=496. 노란 점프선은 각 가장자리 4m 앞이다.
첫 점프는 z=44, 첫 앵커는 (0, 10, 56), 첫 착지 플랫폼은 z=64에서 시작한다.
첫 공격 표적은 (0, 1.1, 18)에 있으며 이후 표적·위험물은 좌우로 배치된다.
PlayMode 테스트는 장면 로드, 전진, 점프/연결/제약/해제, 사거리 실패, 피격 무적, 낙하 후 재시작과 표적 복구, 일시정지, 자동 조작으로 전체 코스 완주를 검증한다. 가상 Keyboard 의 Input System 이벤트로 Enter·←/→·Alt·Alt 뗌·R·Space·Shift·Ctrl 도 검증하고 장치를 복구한다.
시각 검증 캡처는 Unity 프로세스의 임시 폴더에 `ChainRush-ready.png`, `ChainRush-running.png`, `ChainRush-grapple.png`로 저장한다.
HUD는 IMGUI와 OS 동적 폰트(Malgun Gothic/Arial), 효과음은 메모리 내 합성 AudioClip을 사용한다. 정식 아트·음원과 빌드/배포는 포함하지 않는다.

### 무한 전투 모드 — 2026-10-25 삭제

- 무한 모드(절차 코스 스트리밍·적 조우)는 지웠다(`DECISIONS.md` 2026-10-25 "무한 모드 삭제"). 무엇이 있었고 어디서 되살리는지는 `DESIGN.md` §10-1. 아래 두 절은 그때의 기록이다.
- 런타임에 구간/체인을 Instantiate하지 않는다(서킷 노면·앵커는 처음 한 번 깐다). 서킷 배경 장식은 스타디움·SKYWAY 전용 씬을 Additive 로 불러온다. SKYWAY 장식은 물리·런타임 스크립트가 없는 `SkywayExterior.prefab` 하나다(`ART_SKYWAY.md`).

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
