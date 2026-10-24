# 결정 로그

> **A모드.** "왜 이렇게 했나"는 코드를 읽어도 안 나온다. 여기에만 있다.
> 새 결정은 **맨 위에** 추가한다. 뒤집힌 결정은 지우지 말고 `~~취소선~~` + 사유를 남긴다.

형식: `날짜 · 결정 · 이유 · 검토했으나 버린 대안`

---

## 2026-10-25 · 리팩토링 RF4: `CircuitRace` 의 장면 만들기를 `CircuitScenery` 로 (`feature/refactor-circuit-scenery`)

- **결정 (사용자)**: RF3b 병합 뒤 "다음 작업 ㄱㄱ" → 줄 번호를 다시 확인한 요청서(`docs/NEXT_REQUESTS.md` RF4, 2026-10-22 와 줄 번호 같음)에 "ㄱㄱ 결과 나오면 커밋 병합 푸시까지".
- **한 것**: 노면(`LayRoad`·`LayRange`·`WidthBetween`), 출발선·체크포인트 띠(`PlaceMarkers`·`AddMarker`), 코너·스윙 봉(`PlacePoles`·`VisualPart`)과 그 리스트 셋을 일반 클래스 `Race/CircuitScenery.cs`(176줄)로 옮겼다. 형식 기준은 `Track/RoadPiece.cs`(생성자에서 부모·머티리얼을 받아 null 이면 예외, `Destroy()`). `CircuitRace` 는 343 → 222줄로 랩 판정(`LapCounter`)·앵커(`PlaceAnchors` — 그래플 시뮬이 쓰는 위치라 레이스 쪽에 둠)·스냅샷·틈 조회만 든다.
  - 바꾸지 않은 것: 직렬화 필드(`maxPieceLength`·머티리얼 포함, 씬 참조), 생성 순서(노면 → 앵커 → 표지 → 봉 — `CircuitScenery` 가 세 단계를 따로 내줘 `Initialize` 의 순서를 그대로 둠), 오브젝트 이름과 부모(`CircuitRace` 의 Transform), `PieceCount`·`PoleCount`(새 클래스에 위임, 만들기 전에는 0).
  - 새로 더한 것: 생성자의 인자 검사(`maxPieceLength` 가 양수가 아니면 `ArgumentOutOfRangeException` — 15 m 하한은 그대로 `CircuitRace.OnValidate` 가 알린다).
- **검증** (에디터 6000.3.25f1, 2026-10-25):
  - 컴파일: `isCompiling=False scriptCompilationFailed=False`, Console Error 0(경고는 `Deleting invalid font reference.` 만).
  - EditMode `!Enemy`: `testcasecount="271" result="Passed" passed="271" failed="0"` (19:21:42Z~19:21:46Z).
  - `tools/select-tests.ps1 -Range dev...HEAD` (커밋 `c12ba49`): 바뀐 파일 3개(`CircuitRace.cs`·`CircuitScenery.cs`·`.meta`) 모두 `Race/` 줄 → PlayMode 클래스 11개 + 지문 1건. 도구가 출력한 필터를 그대로 `Unity_RunCommand` 로 실행했다.
  - 고른 PlayMode: `testcasecount="110" result="Passed" passed="110" failed="0" skipped="0"` (19:22:20Z~19:26:39Z, 258.9초). XML 의 클래스 11개(`ChainRushCircuitTests` 7·`ChainRushControllerCostTests` 2·`ChainRushFlowTests` 24·`ChainRushReplayTests` 2·`ChainRushRollbackTests` 9·`ChainRushWorldTests` 5·`NetReconcileTests` 22·`NetRemoteTests` 11·`RaceOrderTests` 9·`RaceStandingsTests` 9·`RaceStandingsTextTests` 10)가 도구 출력과 같다.
  - 동작 불변의 증거: 서킷 지문 `hash 0x18F31E7A2B4F79B3` 그대로(노면 충돌체가 같아야 같다).
- **다음**: `LobbyMenu`(664줄) 조사 — HANDOFF 1절.

## 2026-10-25 · 리팩토링 RF3b: `RunnerMotor` 의 이동 규칙을 `Motion/` 의 상태 없는 클래스로 (`feature/refactor-motor-rules`)

- **결정 (사용자)**: RF3 뒤 "다음 작업 ㄱㄱ" → 요청서의 두 안 중 "A안으로 ㄱㄱ". A = 규칙을 static 클래스로 떼고 `RunnerMotor` 가 직렬화 값으로 `Settings` 를 틱마다 채워 넘긴다. B = 튜닝 필드를 `[Serializable]` 묶음으로 옮긴다(두 씬 `ChainRushCircuit`·`ChainRushTestBench` 에 직렬화된 값 약 45개씩을 옮겨야 해 버림 — SO 이전과 함께 따로 본다).
- **왜 상태 없는 클래스인가**: 시뮬 상태는 롤백 스냅샷(`RacerState.Snapshot`)에 있어야 하므로 규칙 쪽에 상태를 두면 스냅샷에서 빠진다. 튜닝 값을 `Awake` 에서 복사해 두면 `ChainRushCurveTests.cs:182-186`(실행 중 `sideGrip` 변경)과 인스펙터 실시간 조정이 깨지므로, `Settings` 는 매번 직렬화 필드에서 새로 채운다. 규칙이 `RacerState` 를 인자로 받으니 `game.Racer` 에 묶이지 않는다(RF6 여러 레이서의 준비).
- **나눈 것** (`Runtime/ChainRush/Motion/`, 네임스페이스 `ProtoHarness.ChainRush.Motion`, 형식 기준 `Track/LapCounter.cs` 의 머리 주석·중첩 public struct):
  - `RunDrive` (147줄): `FacingOf`/`RightOf`, `TurnLimit`, `Turn`, `Drive`(옆 그립·목표 속력·가속·속도 합성·중력), 중첩 `Settings`(20개)·`Conditions`(그 틱의 플래그 7개). 상수 `GrappleAirAcceleration`·`GrappleReelSpeed`·`MaxFallSpeed` 가 `RunnerMotor` 에서 옮겨 왔다.
  - `Slingshot` (70줄): `IsPulling`, `TryFire`, `Cut`, `SteerToward`(당김점을 돌려줌), `BoostAcceleration`, `RaiseTarget`(옛 `RunTarget` 끝의 carrying → pulling 두 줄, 같은 순서), 중첩 `Settings`.
  - `PoleSwing` (101줄): `Hook`, `SpeedUp`, `Step`(줄 끝 `anchor` 를 out 으로), `End`, 중첩 `Settings`.
  - `GuardContact` (37줄): `Glance`, `Scrape`.
  - `RunnerMotor` 697 → 537줄: 직렬화 필드·`OnValidate`, HUD·봇이 읽는 프로퍼티(시그니처 그대로), `Step` 의 순서, `CharacterController` 처리(`MoveAndCollide`·`SnapToGround`·리스폰·위치 지정·`OnControllerColliderHit`), 효과음, `Settings` 를 채우는 `DriveSettings`·`SlingSettings`·`SwingSettings`.
  - `tools/select-tests.ps1` 표에 `Motion/` 을 "코어 시뮬"(전체)로 넣었다.
  - 새 파일과 폴더의 `.meta` 는 Unity 가 만들었다.
- **동작 불변을 지킨 방법**: 식과 연산 순서를 그대로 옮겼다. `Settings` 에 담긴 값은 옛 코드가 읽던 프로퍼티(`RunSpeed` = `runSpeed * topSpeedScale` 등)의 같은 값이다. 틱 안에서 튜닝이 바뀌는 일은 없으므로 `Step` 머리에서 한 번 채워도 같다. `Conditions.GrappleAttached` 는 옛 코드가 `grapple.IsAttached` 를 읽던 바로 그 자리(`Drive` 직전)에서 채운다.
- **검증** (에디터 6000.3.25f1, 2026-10-25):
  - 컴파일: `isCompiling=False scriptCompilationFailed=False`, Console Error 0.
  - EditMode `!Enemy`: `testcasecount="271" result="Passed" passed="271" failed="0"` (19:06:12Z~19:06:16Z).
  - `tools/select-tests.ps1 -Range dev...HEAD` (커밋 `686269b`): 바뀐 파일 11개 — `Motion/` 8개(코어 시뮬)·`Motion.meta`(표에 없는 경로)·`RunnerMotor.cs`(코어 시뮬) 는 "전체", `tools/select-tests.ps1` 은 "없음" → 게이트 전체.
  - PlayMode 게이트 (`Run PlayMode Tests`): `testcasecount="181" result="Passed" passed="174" failed="0" skipped="7"` (19:06:48Z~19:14:21Z, 453.1초, skipped 7 은 `ChainRushProbeTests` 4·`NetworkRaceTests` 3 의 `[Explicit]`).
  - 동작 불변의 증거: 서킷 지문 `ChainRush replay circuit: 1700 ticks identical, hash 0x18F31E7A2B4F79B3` 그대로, `ChainRushRollbackTests` 9건 모두 통과.
- **남은 것**: 규칙 단위 EditMode 테스트(씬 없이 `RunDrive`·`PoleSwing` 을 시험할 수 있게 됐다)는 요청서에서 뺐다 — 원하면 다음 작업. `Motion.meta` 가 선택 도구에서 "표에 없는 경로"로 잡히는 것은 결과가 같아(전체) 두었다.

## 2026-10-25 · 리팩토링 RF3: `RunnerMotor.Step` 을 단계 메서드로 · 이름 없는 숫자를 상수로 (`feature/refactor-motor-step`)

- **결정 (사용자)**: 무한 모드 삭제 뒤 "다음 ㄱㄱ" — `docs/NEXT_REQUESTS.md` RF3 요청서(단계 11개·상수 8종, 줄 최소 길이 5 추가) 승인. 같은 날 사용자 "확장만 하다보니 꽤 지저분하다" 에서 정한 순서 RF3 → RF3b(객체 분리) → RF4 → `LobbyMenu` 조사의 첫 단계다.
- **한 것**: 160줄 `Step` 을 연산 순서 그대로 `ApplyJump`(코요테·점프) → `CutBoost`(스로틀을 놓으면 부스터 끊기) → `Turn`(조향, 회전 한계를 돌려줌) → `SpeedUpSwing` → `SteerTowardSling` → `Drive`(옆 그립·`RunTarget`·`ForwardAcceleration`·속도 합성·중력) → `MoveAndCollide`(이동·천장·가드·착지·그래플 놓기) → `CountDownAndCheckFall` 로 나눴다. `Step` 에는 리스폰 대기, 입력(조향·주 동작·놓기·체인 액션·봉 걸기·스윙 끝내기), 그 틱의 플래그(`swinging`·`pulling`·`carrying`)만 남았다. 요청서의 11단계 중 "체인 액션·봉 걸기·스윙 끝내기" 는 세 줄이라 `Step` 에 두었고, 목표 속력·가속도·속도 합성은 `Drive` 하나가 부른다.
  - 상수: `RunnerMotor` 의 `CoyoteSeconds` 0.1·`GrappleAirAcceleration` 5·`GrappleReelSpeed` 16·`MaxFallSpeed` 28·`FallDepth` 12·`ReleaseSpeed` 13(놓기와 `OnValidate` 의 `empoweredReleaseSpeed` 하한·로그 문구가 함께 쓴다), `GrappleController` 의 `MissSeconds` 0.75·`MinRopeLength` 5(걸 때와 감을 때 두 곳). 직렬화하지 않으므로 씬 영향이 없다. `RunnerMotor` 에는 이미 직렬화 필드 `minRopeLength`(봉 스윙 줄, 2 m)가 있어 이름이 비슷하지만 클래스가 다르다.
  - 바꾸지 않은 것: 직렬화 필드, public 시그니처, 연산 순서, 단계 사이에 넘기는 값은 매개변수로(새 타입 없음 — 매개변수가 10개인 `Drive`·`RunTarget` 은 RF3b 에서 객체로 묶는다).
- **검증** (에디터 6000.3.25f1, 2026-10-25):
  - 컴파일: `isCompiling=False scriptCompilationFailed=False`, Console Error 0.
  - EditMode `!Enemy`: `testcasecount="271" result="Passed" passed="271" failed="0"` (18:51:14Z~18:51:18Z).
  - `tools/select-tests.ps1 -Range dev...HEAD` (커밋 `e33bca5`): 바뀐 파일 2개(`GrappleController.cs`·`RunnerMotor.cs`) 모두 "전체 (코어 시뮬)" → 게이트 전체.
  - PlayMode 게이트 (`Run PlayMode Tests`): `testcasecount="181" result="Passed" passed="174" failed="0" skipped="7"` (18:51:47Z~18:59:20Z, 453.0초, skipped 7 은 `ChainRushProbeTests` 4·`NetworkRaceTests` 3 의 `[Explicit]`).
  - 동작 불변의 증거: 서킷 지문 `ChainRush replay circuit: 1700 ticks identical, hash 0x18F31E7A2B4F79B3` — 기준 그대로. 롤백 테스트 10건(`Rollback_*`·`StepTick_ByHand_IsTheSameRunAsFixedUpdate`·`Snapshot_OutsideACircuitRaceOrRun_ThrowsLoudly`) 모두 통과.
  - 사용자 플레이 확인: 없다(동작 불변이라 지문으로 갈음).
- **검토했으나 버린 대안**: 단계 사이 값을 묶는 private struct(요청서의 "새 타입 없음" 밖 — RF3b 에서), 틱 플래그를 인스턴스 필드에 두기(롤백 스냅샷에 없는 상태가 늘어난다).

## 2026-10-25 · 무한 모드 삭제: 절차 코스·적 조우를 지우고 아이디어로만 남긴다 (`feature/remove-endless`)

- **결정 (사용자)**: 리팩토링 이야기 중 사용자 "현재 코드도 확장만 하다보니 꽤 지저분하다" → 추천(동결된 무한 모드의 존속 결정을 앞당기면 `ChainRushGame` 분기가 가장 많이 줄어든다)에 "무한모드 삭제하자 추후에 추가해볼 컨텐츠 아이디어로만 남기자". 적도 같이 지울지는 합의 요청서의 추천(같이 지운다)에 "네 추천대로 진행". 2026-10-15 "서킷 집중" 의 동결, RF5 보류("존속이 정해지면 분리와 삭제 중 고른다")가 여기서 "삭제" 로 정해졌다. 처음 목표(2026-10-01)의 ① 무한(쿠키런식) 모드도 지웠다 — `DESIGN.md` §1·§6 에 취소선, 아이디어는 §10.
- **이유**: 무한 모드는 2026-10-15 부터 새 기능 없이 테스트만 끌고 다녔고(`Endless`·`Long`·`Sweep` 카테고리를 게이트에서 빼 둔 채), 런타임에는 `IsEndless` 분기가 `ChainRushGame`·`ChainRushHud`·`GrappleController` 에 퍼져 있었다. 적(`EnemyDirector`)은 직렬화 참조로 `CourseStream` 을 받고, 배치된 씬도 무한 씬 하나라 무한 모드만 지우면 씬도 테스트도 없는 코드가 된다. 아이템전 때는 서킷 기준으로 다시 짜야 한다.
- **지운 것** (파일과 `.meta` 짝, `git rm`): 씬 `ChainRushProcedural.unity`, `Endless/ProceduralCourse`·`CourseStream`, `Track/CourseGenerator`·`CourseTuning`·`CourseModule`·`ModuleKind`, `Combat/EnemyDirector`·`EncounterTuning`, `Data/CourseTuning_Default.asset`·`EncounterTuning_Default.asset`, 테스트 `CourseGeneratorTests`(EditMode 20건)·`EncounterTuningTests`·`ChainRushProceduralTests`·`ChainRushCombatTests`·`CourseBot`, 그리고 `ChainRushReplayTests` 의 절차 리플레이 1건과 `ChainRushSteeringTests` 의 `Guards_ProceduralRoad_AreGuarded`. 지운 테스트는 모두 `Endless`·`Enemy` 카테고리로 이미 게이트 밖이었다.
- **고친 것**: `ChainRushGame`(직렬화 필드 `endlessMode`·`endlessCourse`·`enemies`, `IsEndless`·`Enemies`·`RegisterEnemyHit`·`PlayPresentationCue` 제거 — 직선·서킷 두 갈래만 남음), `ChainRushHud`(무한 문구·`DrawEncounter`), `GrappleController`(직렬화 필드 `chainVisual`, 무한 전용 체인 표시·`SwingBite` 의 난간 걸림·`ShiftOrigin`), `RunnerMotor.ShiftOrigin`(그래플 원점 이동 호출 1줄), `RunnerAnimation`(적 조우 자세). `ChainRushCircuitTests` 의 지운 API 단정 2줄(`IsEndless`·`Enemies`)은 API 가 없어져 뺐다(통과시키려고 고친 것이 아니다). `CircuitBot` 은 지운 `CourseBot` 을 가리키던 점프 거리 근거 주석을 옮겨 받았다.
- **에디터·도구·규칙**: 메뉴 `Create Circuit Scene`(무한 씬을 원본으로 복사하던 1회용, 서킷 씬이 이미 있어 돌지 않던 것)과 `Run Long Tests`·`Run Course Sweep` 를 지웠다. 게이트 필터는 `!Device`·`!Enemy`·`!NetEndReplay`. `tools/select-tests.ps1` 표에서 지운 경로·클래스를 뺐다. `CLAUDE.md` §9-2 와 `docs/RULES/VERIFICATION.md` §4-4 에서 `Endless`·`Long`·`Sweep` 줄과 마일스톤 조건의 `Run Long Tests`·`Run Course Sweep` 를 지웠다. `Long` 묶음(2026-10-14)은 지금 담을 테스트가 없어 같이 없앴다 — 장거리 테스트가 다시 생기면 그때 되살린다.
- **남긴 것**: `SeedHash`(리플레이 지문 `StateTrace` 가 쓴다), `RoadPiece`·`Centerline` 의 원점 이동(`ShiftOrigin` 은 테스트가 러너 순간 이동에 쓴다), `TickInput.AttackPressed`(입력 기록·네트워크 형식), 공격·표적·체력(`Enemy` 카테고리, 테스트 벤치), `SwingLook.RailPoint`(런타임 호출 없음, EditMode 테스트만). 이름이 내용과 어긋난 `ChainRushCircuitSceneBuilder`(이제 SKYWAY 정의만 만든다)도 파일 이름 변경이라 두었다.
- **검증** (에디터 6000.3.25f1, 2026-10-25):
  - 컴파일: `Unity_RunCommand` `isCompiling=False scriptCompilationFailed=False`, 지운 씬은 AssetDatabase 에도 없다. Console Error 0, Warning 6(모두 `Deleting invalid font reference.` / `UnityEditor.ScriptReloadProperties:Load`).
  - EditMode `!Enemy`: `testcasecount="271" result="Passed" passed="271" failed="0" skipped="0"` (18:24:06Z~18:24:10Z). v0.3 의 `!Enemy !Endless` 271 과 같다. 실행 중 Console Error 2건은 `GameSettingsTests.Constructor_StoredValueOutOfRange_LogsAndUsesTheDefaultForThatSettingOnly` 가 기대하는 로그다.
  - `tools/select-tests.ps1` (기본 범위): 지운 PlayMode 테스트 파일 6줄(`ChainRushCombatTests`·`ChainRushProceduralTests`·`CourseBot` 와 `.meta`)에 "테스트 클래스 파일인데 클래스를 못 찾았다" 경고 → 전체. 표 경고(표에 없는 클래스·없는 클래스)는 없다.
  - PlayMode 게이트 (`Run PlayMode Tests`): `testcasecount="181" result="Passed" passed="174" failed="0" skipped="7"` (18:25:08Z~18:32:46Z, 458.0초). skipped 7 은 `[Explicit]` — `ChainRushProbeTests` 4건, `NetworkRaceTests` 3건. v0.3 의 180 보다 1건 많은 것은 그 뒤 `fix/hud-track-name` 이 더한 `ChainRushFlowTests.RaceSetup_MissingTitle_Throws` 다 — 이번 삭제로 게이트 수는 바뀌지 않았다.
  - 서킷 지문: `ChainRush replay circuit: 1700 ticks identical, hash 0x18F31E7A2B4F79B3` — 기준 그대로.
  - 사용자 플레이 확인: 아직 없다(타이틀 → 로비 → 서킷 한 판, 테스트 벤치는 손으로 보지 않았다).
- **`ChainVisual` 과 서킷 씬의 `Grapple Chain` (같은 브랜치, 사용자 "같이 지우자 ㄱㄱ")**: 서킷 씬은 무한 씬의 복사본이라 무한 전용 체인 시각물 `Grapple Chain`(`ChainVisual` + 링크 80개·갈고리, 자식 81개)이 남아 `Awake` 에서 숨기만 하고 있었다. 클래스를 먼저 지우면 Missing Script 가 생기므로 순서를 지켰다: ① 승인받은 `Unity_RunCommand`(전문을 보이고 "ㄱㄱ")로 `result.DestroyObject` — 씬 저장은 §0 이라 하지 않음 ② 사용자가 에디터에서 Ctrl+S ③ `ChainVisual.cs`·`.meta` 를 `git rm`. 저장한 씬은 객체 1866 → 90개(새로 생긴 객체 0, 줄어든 1776개는 그 묶음), 지운 필드의 남은 키(`endlessMode`·`endlessCourse`·`enemies`·`chainVisual`)도 저장 때 빠졌다. `ChainVisual` GUID 를 쓰는 씬·프리팹·에셋은 없다. `MetalChain` 은 `GrappleController` 가 계속 쓴다.
  - 검증: 컴파일 Error 0(경고는 `Deleting invalid font reference.` 만, Missing Script 없음). EditMode `!Enemy` `testcasecount="271" result="Passed" failed="0"` (18:37:43Z~18:37:47Z). PlayMode 게이트 (`Run PlayMode Tests`, 씬을 바꾼 뒤 다시): `testcasecount="181" result="Passed" passed="174" failed="0" skipped="7"` (18:38:07Z~18:45:42Z, 454.2초, skipped 7 은 `ChainRushProbeTests` 4·`NetworkRaceTests` 3 의 `[Explicit]`), 서킷 지문 `hash 0x18F31E7A2B4F79B3` 그대로.
- **검토했으나 버린 대안**: 무한 모드만 지우고 적은 남기기(씬·테스트 없는 코드가 남는다), 모드 분기 추상화(RF5 — 버릴 모드를 추상화하지 않는다는 보류 이유 그대로), 씬 YAML 에서 `Grapple Chain` 을 텍스트로 지우기(§0 금지).

## 2026-10-23 · HUD 트랙 이름: 로비에서 고른 트랙의 제목을 보인다 (`fix/hud-track-name`)

- **결정 (사용자)**: v0.3 기록에서 알린 버그(서킷이면 HUD 왼쪽 위가 늘 `STADIUM`, `ChainRushHud.cs:184`)에 "고쳐봐" → 합의 요청서(`RaceSetup` 에 제목, 파일 4개)에 "ㄱㄱ".
  - `RaceSetup` 에 `Title`(로비 `TrackChoice.title` 그대로)을 넣었다. 생성자 두 번째 인자이고 비었거나 공백이면 `ArgumentException`. `LobbyMenu.BuildSetup` 이 넘긴다.
  - HUD 는 `ChainRushHud.Heading` 을 그린다 — 로비에서 왔으면(`FromLobby`) `assembler.Setup.Title`, 씬을 직접 열었으면 서킷은 `CIRCUIT`, 아니면 `CHAIN RUSH`. HUD 의 `assembler` 참조는 서킷 씬에 이미 연결돼 있어 씬은 고치지 않았다.
- **이유**: 트랙 이름은 로비의 `TrackChoice` 에만 있고 레이스로 가는 길(`RaceSetup`)에 없었다. `TrackDefinition` 에 이름을 두면 두 트랙 에셋을 에디터에서 고쳐야 하고 로비 제목과 두 군데가 된다.
- **검증** (에디터 6000.3.25f1, UTC, 커밋 전 작업 트리):
  - `AssetDatabase.Refresh()` 뒤 Editor.log `*** Tundra build success (3.48 seconds), 29 items updated, 1618 evaluated`, Console Error 0건.
  - EditMode `!Enemy !Endless`: `testcasecount="271" result="Passed" passed="271" failed="0"` (17:22:50Z~17:22:54Z).
  - `tools/select-tests.ps1` (기본 범위) = 바뀐 파일 4개 + `ProtoHarness.slnx`(Unity 가 읽지 않음), PlayMode 클래스 8개(`ChainRushFlowTests`·`ChainRushPresentationTests`·`ChainRushProfileTests`·`NetReconcileTests`·`NetRemoteTests`·`RaceOrderTests`·`RaceStandingsTests`·`RaceStandingsTextTests`) + 지문 1건 → 그대로 실행 `testcasecount="96" result="Passed" passed="96" failed="0" skipped="0"` (17:23:16Z~17:25:42Z). XML 의 클래스가 도구 출력과 같다(RF1 의 95 + 새 테스트 1). 서킷 지문 `hash 0x18F31E7A2B4F79B3` 그대로.
  - 새·바뀐 단언: `RaceSetup_MissingTitle_Throws`(null·빈·공백 거부), 로비가 고른 제목 `STADIUM`·`SKYWAY LOOP`, 로비 경로의 HUD 제목(시험 트랙·`Race_SkywayFromTheLobby_BotFinishesTwoLaps` 의 `SKYWAY LOOP`), 직접 연 씬의 `CIRCUIT`.
  - **고치기 전 실패**: `Heading` 은 이번에 생긴 속성이라 고치기 전 코드로 그 테스트를 돌릴 수는 없다. 고치기 전의 틀린 화면은 v0.3 README 캡처 `docs/images/skyway.png`(SKYWAY 주행에 `STADIUM`)가 근거다.
  - 화면: Play 에서 로비 메뉴를 넘겨(싱글 → SKYWAY → 빠름 → ALPHA) 출발 3초 뒤 캡처 — 왼쪽 위 `SKYWAY LOOP`. 로그 `HudShot: setup title = SKYWAY LOOP`, `HudShot: heading = SKYWAY LOOP`.
- **README 이미지 다시 찍기** (사용자 "셋다 ㄱㄱ" — 커밋·병합, 다시 찍기, 푸시): `race.gif`·`skyway.png` 에도 `STADIUM` 이 찍혀 있어 둘 다 바꿨다. 같은 방식(시험 봇, 게임 시간 0.1초 간격 35초)으로 이번에는 로비 메뉴를 넘겨 들어갔고(`Shots: setup title = SKYWAY LOOP`), 351프레임 모두 왼쪽 위가 `SKYWAY LOOP` 다. GIF 는 처음 200프레임·640px(6.5 MB), 정지 화면은 300번째 프레임. `main` 의 v0.3 이미지는 그대로다(다음 마일스톤에 함께 간다).
- **버린 대안**: `TrackDefinition` 에 표시 이름 필드(위 이유). HUD 에서 트랙 에셋 이름(`Circuit_Skyway`)을 보이기 — 로비 제목과 다르다. 직접 연 씬에서도 `STADIUM` 유지 — 저장된 정의가 바뀌면 다시 틀린다.

## 2026-10-23 · main v0.3: M2 "게임의 뼈대" — `dev` 를 `main` 에 병합하고 태그 `v0.3`

- **결정 (사용자, 2026-10-23)**: "현재까지 dev 들어간거 main으로 올리고 README도 수정하자" → 아래 "M2 범위 조정" 에서 "M2 범위 조정 후 v0.3" 을 고름 → "진행하라. 다만 README에 프로젝트 느낌을 보여줄 이미지나 gif도 넣어줘". 이 문서 브랜치(`docs/m2-close-v0.3`: 범위 조정 기록, README 재작성과 이미지, 이 기록)를 `dev` 에 병합한 뒤 `dev` 를 `main` 에 `--no-ff` 로 병합하고 주석 태그 `v0.3` 을 단다.
- **v0.3 에 든 것** (`main` v0.2 뒤): M2-a1 씬 뼈대(타이틀·로비·Additive 장식 씬)·M2-a2 빌드 씬 목록, M2-b 러너 프로필 5명, 로비 최고 기록, 그래플 앵커 높이·공중 가속·스윙 중 부스터, M2-c 설정·러너 디자인 5종·스타디움 장식, 러너 선택창 프리뷰·입장/이전 버튼·일시정지 메뉴, Night Drift UI·금속 체인, 카트라이더풍 타이틀·로비 설명·카드 버튼, SKYWAY LOOP(원작 윤곽 재배치·구간별 도로 폭·직선 스윙 봉), ↓ 후진·스윙 중 ↑ 가속, 리팩토링 RF1. 각각 이 문서의 같은 이름 항목.
- **마일스톤 조건 (`CLAUDE.md` §9-2 ③)** — 코드는 `dev` `1ffeba1` 그대로(이 문서 브랜치는 `Assets/`·`Packages/`·`ProjectSettings/` 에 닿지 않는다), 에디터 **6000.3.25f1**(Unity MCP `Application.unityVersion`), `ProjectSettings/`·`Packages/packages-lock.json`·`ProtoHarness.slnx` 는 넣지 않는다. 시각은 UTC(결과 XML):
  - 컴파일: `Unity_RunCommand` 컴파일 성공, Console Error 0건 (16:49Z 직전).
  - EditMode (`!Enemy`·`!Endless`): `testcasecount="271" result="Passed" passed="271" failed="0" skipped="0"` (16:49:52Z~16:49:54Z). 실행 중 Console Error 2건은 `GameSettingsTests` 가 `LogAssert.Expect` 로 기대한 `GameSettings: … holds 99, outside 0..10` 와 `… holds 7, neither 0 nor 1`.
  - PlayMode 게이트 1회차 (`Run PlayMode Tests`): `testcasecount="180" result="Passed" passed="173" failed="0" skipped="7"` (16:50:40Z~16:58:12Z, 452.6초).
  - PlayMode 게이트 2회차 (같은 코드): `testcasecount="180" result="Passed" passed="173" failed="0" skipped="7"` (16:58:39Z~17:06:12Z, 453.0초). 두 회차 모두 skipped 7 은 `[Explicit]` — `ChainRushProbeTests` 4건, `NetworkRaceTests` 3건. 서킷 지문 `hash 0x18F31E7A2B4F79B3` (기준과 같다).
  - `Run Device Input Tests` 1회차: `testcasecount="3" result="Failed(Child)" passed="1" failed="2"` (17:06:45Z~17:06:49Z). 실패 `Input_ArrowKeys_StartsSteersJumpsGrapplesAndRestarts`·`Input_ShiftAndCtrl_SwingsOnThePoleAndFiresChainAction` 모두 `Expected: True  But was:  False`. 실행 직전 `isApplicationActive=False` — 에디터가 앞에 없어 가상 장치 이벤트가 전달되지 않았다(이 문서 2026-10-04 "장치 입력 테스트 분리" 의 원인과 같다).
  - `Run Device Input Tests` 2회차 (코드 그대로, 에디터를 앞에 둠, `isApplicationActive=True`): `testcasecount="3" result="Passed" passed="3" failed="0"` (17:07:39Z~17:07:55Z).
  - 사용자 플레이 확인 (바뀐 M2 완료 조건 "러너마다 차이가 느껴진다"): "어 느껴졌어" (2026-10-23).
  - 하지 않은 것: 네트워크 레이스(M7 부터), `Run Long Tests`·`Run Course Sweep`(무한 모드 보류, §9-2).
- **README 이미지** (`docs/images/`): 에디터 Play 중 `ScreenCapture.CaptureScreenshot` 으로 찍었다 — 타이틀·로비(2배 해상도), SKYWAY · 빠름 · ALPHA 주행(시험 봇 `TrackFollower` 와 같은 조향 식, 게임 시간 0.1초 간격 35초). 결승 전에 Play 를 꺼 최고 기록은 저장되지 않았다. `race.gif` 는 처음 20초(200프레임, 640px, 6.5 MB). 캡처 코드는 `Unity_RunCommand` 로만 돌렸고 저장소에 남기지 않았다. 캡처 동안 Game 뷰를 잠시 최대화했다가(메인 창이 세로 모니터라 세로로 찍혀 쓰지 않음) 되돌렸다.
- **찾은 문제 (고치지 않음)**: HUD 왼쪽 위 트랙 이름이 서킷이면 늘 `STADIUM` 이다(`ChainRushHud.cs:184` `game.IsCircuit ? "STADIUM" : "CHAIN RUSH"`). SKYWAY 에서도 STADIUM 으로 보인다 — `docs/images/skyway.png` 에 그대로 찍혔다. `HANDOFF.md` 1절 다음 작업에 넣었다. 타이틀의 버전 표기는 아직 `TitleScreen.Version = "0.0.1"` 이다(태그와 맞출지 사용자 답 없음, 그대로 둠).
- **v0.3 에 없는 것**: 속도 단계 보통·매우빠름(M3 으로 옮김), 새 서킷(M3), 기록·고스트(M4).

## 2026-10-23 · M2 범위 조정: 속도 단계 보통·매우빠름을 M3 으로 — v0.3 을 지금 닫는다

- **결정 (사용자, 2026-10-23)**: "현재까지 dev 들어간거 main으로 올리고 README도 수정하자" → 지금 `dev` 는 M2 완료 조건(`DESIGN.md` §5-1, "러너마다, 속도 단계마다 차이가 느껴진다")을 채우지 못해 `CLAUDE.md` §9-2 ③ 과 충돌한다고 알리고 세 갈래를 냈다. 사용자가 "M2 범위 조정 후 v0.3" 을 골랐다: 속도 단계 보통·매우빠름을 M3 으로 옮기고, 러너 차이 플레이 확인과 ③ 의 테스트를 거친 뒤 `dev → main` `--no-ff` 로 병합하고 태그 `v0.3` 을 단다. README 도 v0.3 기준으로 다시 쓴다.
  - **옮긴 것**: `DESIGN.md` §5-1 의 M2 행에서 속도 단계 보통·매우빠름과 완료 조건의 "속도 단계마다"를 빼서 M3 행으로 옮겼다. `NEXT_REQUESTS.md` 의 같은 항목도 M3 칸으로 옮겼다. 로비의 속도 단계 자리(빠름만 열림)는 M2 에 그대로 있다.
  - **M2 완료 조건 (바뀐 뒤)**: 메뉴로 트랙·러너를 골라 달리고 메뉴로 돌아온다. 러너마다 차이가 느껴진다.
- **이유**: 속도 단계는 처음부터 후순위였다(사용자 2026-10-19). 그 사이 `dev` 에는 타이틀·로비·설정·러너 5명·일시정지·SKYWAY 트랙·후진 등이 쌓였는데 `main` 은 v0.2 에 머물러 있다. 속도 단계는 트랙마다 봇이 코너를 놓치는지 다시 재야 해서, 새 트랙이 들어오는 M3 에서 같이 하는 편이 일이 한 번으로 끝난다.
- **버린 대안**: 태그 없이 `main` 에 중간 동기화 — "마일스톤 하나 = 병합 + 태그 하나"(§9-2 ③, `DESIGN.md` §5-1)에 예외를 만들어야 한다. 속도 단계를 먼저 구현하고 v0.3 — `main` 이 더 늦게 올라간다.

## 2026-10-22 · 리팩토링 RF1: IMGUI 헬퍼 4벌을 `NightUi` 하나로 · 리팩토링 묶음 RF

- **결정 (사용자, `feature/refactor-ui-helpers`)**: "현재 작성되어있는 코드 … 리팩토링하자. 구조와 확장성도 고려해서" → 조사 뒤 낸 4단계 안 중 "R1 ㄱㄱ"(이 문서에서는 R1~R9 와 겹치지 않게 **RF1** 로 부른다).
  - **RF1**: `ChainRushHud`·`LobbyMenu`·`TitleScreen`·`Net/RaceStandingsView` 에 글자 하나까지 같게 복사돼 있던 private `Block` 4개를 지우고 호출 47곳을 `NightUi.Block` 으로. private `Style` 은 화면 글꼴을 넘기는 한 줄 `NightUi.Style(font, …)` 로 남겼다(화면마다 본문 글꼴이 있어 호출을 그대로 두는 편이 짧다). 설정 값(글꼴·크기·굵기·색·여백 0)이 같아 그리는 결과는 같다. `ProtoHarness.Net` 은 이미 `ProtoHarness.Runtime` 을 참조한다(asmdef 그대로).
  - **나머지 RF 는 요청서로** (`docs/NEXT_REQUESTS.md` "RF. 리팩토링"): RF3 `RunnerMotor.Step` 분해, RF2 효과음 번호 이름, RF4 `CircuitRace` 깔기 분리. 공통 제약은 동작·지문 불변, MonoBehaviour 이름·직렬화 필드 그대로(씬·프리팹 YAML 을 손으로 고칠 수 없다, `CLAUDE.md` §0). 조사 중 발견: 롤백 재실행 중에도 효과음이 다시 난다(NEXT_REQUESTS 2-1).
- **검증** (에디터 6000.3.19f1, UTC):
  - `AssetDatabase.Refresh()` 뒤 Editor.log `*** Tundra build success (4.53 seconds), 16 items updated, 1618 evaluated`, 그 빌드에서 `ProtoHarness.Runtime.dll`·`ProtoHarness.Net.dll` 재빌드. Console Error 0건.
  - PlayMode — 도구가 고른 클래스 8개(`ChainRushFlowTests`·`ChainRushPresentationTests`·`ChainRushProfileTests`·`NetReconcileTests`·`NetRemoteTests`·`RaceOrderTests`·`RaceStandingsTests`·`RaceStandingsTextTests`) + 지문 1건, `!Device !Sweep !Enemy !Endless !NetEndReplay`: `testcasecount="95" result="Passed" passed="95" failed="0" skipped="0"` (05:53:36Z~05:56:04Z). XML 의 클래스 목록이 고른 것과 같고, `ChainRushReplayTests` 는 지문 1건만 돌았다.
  - EditMode `!Enemy !Endless`: `testcasecount="271" result="Passed" passed="271" failed="0"` (05:56:21Z~05:56:22Z). 실행 뒤 Console Error 4건은 모두 테스트가 `LogAssert.Expect` 로 기대한 로그(`RaceAssembler … cannot be raced yet`, `RunnerProfile '' …`, `GameSettings …` 2건).
  - **② 병합** (사용자 "다음 해야할 작업 리스트 정리까지도해서 커밋 푸시 병합"): 커밋 `0674719` 에서 `select-tests -Range dev...HEAD` = 바뀐 파일 4개, PlayMode 클래스 8개 + 지문 1건 — 위 PlayMode 실행과 같은 필터다. 그 근거는 위 실행이다: 네 파일의 마지막 수정은 05:51:46Z 로 두 실행보다 앞서고, 커밋 시점 `git diff HEAD -- Assets` 는 비었다. 커밋 뒤 같은 필터로 한 번 더 돌리려 했으나 실행 직후 에디터가 종료돼(Editor.log 마지막 쓰기 05:58:14Z, 정상 종료 순서 `Input System module state changed to: ShutdownInProgress` → `Cleanup mono`, 크래시 표시 없음, 종료 원인은 확인 못 함) MCP 가 `Named pipe socket file not found` 로 실패했다 — 그 재실행 결과는 없다. 그 뒤 바뀐 것은 `docs/` 뿐이다. `Long` 은 Endless 보류로 돌리지 않는다(`CLAUDE.md` §9-2). `ProjectVersion.txt`·`ProjectSettings.asset`·`packages-lock.json`·`ProtoHarness.slnx` 는 이 머신의 에디터 버전 차이라 넣지 않았다.
  - **눈으로 보는 확인 없음**: 타이틀·로비·HUD 캡처는 하지 않았다(그리는 값이 같다는 것은 코드 비교).
- **버린 대안**: 호출 47곳을 `NightUi.Style(font, …)` 로 직접 바꾸기(줄만 길어진다). `using static NightUi`(이 코드베이스에 없는 형식). 이번에 RF2~RF4 까지 한 번에(단계마다 게이트로 나눠 동작 불변을 따로 증명한다).

## 2026-10-22 · ↓ 후진 · 스윙 중 ↑ 가속

- **결정 (사용자, `feature/motor-reverse-swing-throttle`)**: "후진도 넣어보자 방향키 뒷키로", "스윙중에도 앞키로 가속하는 것은 되게해줘. 무조건 속도 고정이 아닌(부스터 상황이 아니더라도 가속 받게)" → 제안값(아래)에 "제안대로 진행".
  - **후진**: `TickInput.Reversing`(누르고 있는 동안 켜짐, ↓ 를 누르고 있고 ↑ 는 안 누름 — 둘 다 누르면 ↑ 가 이긴다). 지면에서 앞으로 가고 있으면 `brakeDeceleration` 20 m/s² 로 감속하고, 멈춘 뒤에는 `reverseAcceleration` 8 m/s² 로 `reverseSpeed` 6 m/s 까지 후진한다. 공중에서는 그냥 ↑ 를 뗀 상태와 같다. ↑ 를 뗀 상태의 한 종류라 부스터·스윙 캐리도 끊는다. ↓ 를 누르는 순간 체인이 풀리는 기존 동작은 그대로 둔다(그래플이 없으면 아무 일도 없다, `GrappleController.Release`).
  - **후진 조향**: 후진 입력 중 뒤로 움직이면 자동차처럼 조향이 반대로 돈다(← 를 누르면 기수가 오른쪽으로 돌고 몸은 왼쪽 뒤로 간다). **후진 입력이 있을 때만** 반전한다 — 처음에는 속도 방향만 봤는데, 달리는 중 방향을 강제로 돌린 `Tilt_ScriptedSteerLeft_TurnsBodyToFaceHeading` 이 `Expected: greater than 90.0f But was: -172.287476f` 로 실패했다. ↓ 를 안 누르는 주행은 이전과 같아야 한다.
  - **입력 바이트**: `TickInputCodec` 의 마지막 빈 비트(128)를 후진에 썼다. 이전 바이트(기록 파일·네트워크)는 후진 안 함으로 읽힌다. **한 바이트가 꽉 찼다** — 모르는 비트를 거부하는 검사는 남아 있지만 거부할 비트가 없다. 아홉 번째 버튼은 형식을 넓혀야 한다(`InputLogFile` 버전 포함). 그래서 모르는 버튼 비트 거부를 검사하던 단언 셋을 지웠다 — `NetRemoteTests` 의 코덱·입력 기록 파일(0x80, 사용자 선택 "테스트를 8버튼으로 갱신"), 그리고 게이트 1회차에서 같은 원인으로 실패한 `NetReconcileTests.Wire_InputsRoundTripAndCorruptOnesThrow` 의 0xFF(`unknown flag bits Expected: <System.IO.InvalidDataException> But was: null`, 사용자 "진행").
  - **스윙 중 ↑**: 부스터가 없을 때 ↑ 를 누르고 있으면 스윙의 걸 때 속도(`SwingHookSpeed`)가 달리기 가속(`RunAcceleration`, 캐릭터 배율 포함)으로 `swingThrottleSpeedScale` 1.2 × 달리기 속도(서킷 20 → 24 m/s)까지 오른다. 줄 길이 비율은 그 위에 그대로 곱한다. ↑ 를 떼면 그 속도가 유지된다(오르지도 내리지도 않는다, 사용자 선택 (a)). 부스터가 있으면 기존 부스터 규칙이 우선이고, 이미 더 빠른 걸 때 속도는 깎지 않는다.
  - **테스트 기준을 바꾼 곳**: 봇(`TrackFollower`)은 ↑ 를 늘 누르고 있으므로 "줄 길이만으로 속도가 정해진다"를 재던 스윙 테스트 3개(걸 때 속도 유지, 줄 줄이기·풀기, 놓을 때 캐리)는 걸고 나서 ↑ 를 떼고 잰다(테스트 파일 안의 `Throttle` 감싸개). 캐리 테스트는 놓는 틱에 ↑ 를 다시 누른다(떼면 캐리가 끊긴다). 가드에서 끝나는 테스트는 걸 때 속도의 마지막 값과 비교한다.
- **검증** (에디터 6000.3.19f1, UTC, 커밋 전 작업 트리):
  - `tools/select-tests.ps1` 기본 범위 → "전체"(`RunnerMotor` 코어 시뮬, `TickInput` 표에 없는 경로). 컴파일 후 Console Error 0건.
  - EditMode `!Enemy !Endless` `testcasecount="271" result="Passed" passed="271" failed="0"` (04:21:27Z~04:21:32Z, 이전 269 + 새 2), 조향 수정 뒤 다시 `testcasecount="271" result="Passed" passed="271" failed="0"` (04:50:22Z~04:50:26Z).
  - 바뀐 세 클래스(`ChainRushInputSourceTests`·`ChainRushCornerSwingTests`·`NetRemoteTests`) `testcasecount="33" result="Failed(Child)" passed="31" failed="2"` (04:21:57Z~04:22:58Z) — 위 조향 실패와 입력 기록 파일의 0x80 단언. 고친 뒤 `testcasecount="33" result="Passed" passed="33" failed="0"` (04:27:20Z~04:28:20Z).
  - 메뉴 `Run PlayMode Tests` 1회차 `testcasecount="180" result="Failed(Child)" passed="172" failed="1" skipped="7"` (04:28:41Z~04:36:37Z) — 위 `NetReconcileTests` 0xFF 단언. 지운 뒤 2회차 `testcasecount="180" result="Passed" passed="173" failed="0" skipped="7"` (04:41:55Z~04:49:54Z). 스타디움 지문 그대로 `1700 ticks identical, hash 0x18F31E7A2B4F79B3`, SKYWAY 2랩 86.08 / 84.88 s 그대로(봇이 이 런에서 ↓ 를 쓰지 않고, 바뀐 스윙 가속이 랩에 닿지 않았다). 새 테스트 값: 후진 −6.00 m/s, ← 로 기수 0 → 6.88°; 스윙 ↑ 걸 때 속도 21.20 → 24.00 m/s.
  - 입력 장치 경로를 바꿔 `Run Device Input Tests` `testcasecount="3" result="Passed" passed="3" failed="0"` (04:52:54Z~04:53:10Z, `isApplicationActive=True`, 사용자가 에디터를 앞에 띄움).
  - **사용자 플레이 확인: 아직** — 후진 6 m/s 와 스윙 ↑ 상한 24 m/s 의 체감은 플레이해 보고 정한다.
  - **② 병합** (사용자 "일단 커밋 병합 푸시 진행"): 커밋 `a203930` 에서 `select-tests -Range dev...HEAD` = 전체(바뀐 파일 14개 — `RunnerMotor` 코어 시뮬, `TickInput` 표에 없는 경로, ProjectSettings 0개). 그 근거는 위 EditMode 2회차·PlayMode 2회차·Device 실행이다 — 그 뒤 커밋까지 바뀐 것은 `docs/` 뿐이다. `Long` 은 Endless 보류로 돌리지 않았다(`CLAUDE.md` §9-2). `ProjectVersion.txt`·`ProjectSettings.asset`·`packages-lock.json`·`ProtoHarness.slnx` 는 이 머신의 에디터 버전 차이라 넣지 않았다.
- **버린 대안**: 속도 방향만 보고 조향 반전(위 실패). ↑ 를 떼면 스윙도 지상처럼 감속(사용자가 (a) 유지를 골랐다). 후진 비트를 기록·네트워크에 싣지 않기 — 리플레이·롤백에서 후진이 사라져 결정성이 깨진다.

## 2026-10-22 · SKYWAY 재배치(원작 윤곽) · 구간별 도로 폭 · 직선 스윙 봉

- **결정 (사용자, `feature/skyway-reshape`)**: "빌리지 고가의 질주를 따온건데 생각보다 비슷하지 않다" → 원작 윤곽에 겹친 수정안에 "지우고 트랙 데이터 바꿔라" + "직선이 엄청 긴 구간 … 도로 너비를 많이 넓게 … 좌우에 스윙 걸 수 있는 봉 둬서 직선에도 스윙하며 게이지 채워가는 맵디자인" → 설계안(반폭 15 m, 봉 40 m 간격 7개)에 "작업 해도 좋다".
  - **배치를 읽은 곳**: 나무위키 "크레이지레이싱 카트라이더/트랙/빌리지" 의 빌리지 고가의 질주 항목 이미지 2장(원작판·러쉬플러스판, 흰 윤곽이 같은 모양). 러쉬플러스판 윤곽(500×256 원본을 6배 확대)을 0.5 m/px 로 따라 그렸다. 같은 문서의 서술 순서 "고가 진입 전 → 까마득한 직선 → 출구 커브 → 깎아지른 내리막 → 마지막 유턴" 과도 맞는다. 겹친 그림은 커밋하지 않았다(대화에 전송).
  - **이전 배치와 다른 점**: 왼쪽의 Ω 고리(좌90 R26 → 우225 R30 → 좌135 R28)와 왼쪽 위의 높은 구역(위쪽 변의 계단 꺾임)이 생겼고, 골인 직전이 좌90 이 아니라 유턴(좌180 R31)이다. 오른쪽 범프(우180 R28)·오른쪽 변·아래 고가 직선은 그대로 닮았다. 31조각, 한 바퀴 1499.37 m(이전 1482.82), 2랩.
  - **반지름을 키운 두 곳** (테스트 기준 5.6 m 는 그대로 두고 코스를 고쳤다, 이전 항목의 ① 과 같은 방식): 처음 짠 값으로 봇 2랩 테스트가 `The bot touched a guard at S 256.8 (segment 5). Expected: less than 5.5999999f But was: 5.60866022f` — 오르막 앞 지그(좌90·우90) R22 → **R26**, 앞 직선 64 → 56, 지그 사이 직선 23 → 15, 오르막 26.5 → 24.22, 그 뒤 직선 13.5 → 15.78 로 닫힘·높이를 다시 맞췄다. 다시 돌리자 `at S 1028.1 (segment 19) … But was: 5.61388254f` — Ω 의 마지막 좌135 R22. 테스트는 가장 나쁜 한 곳만 말하므로 Ω 셋을 한 번에 R24/R28/R22 → **R26/R30/R28** 로 키우고 왼쪽 직선 20.22 → 10.56, 위쪽 변 92.72 → 83.06 으로 닫았다(원작 윤곽 위에 다시 겹쳐 확인, Ω 출구가 약 15 m 오른쪽).
  - **추정한 것**: 높낮이는 윤곽에 없어 이전 항목(영상)의 값을 유지했다 — 오른쪽 변 오르막 0 → 12 m(15%), 출구 내리막 12 → 9 m(−10%), 깎아지른 내리막 9 → 0 m(−20%). 반지름은 윤곽 비율에서 잡되 R22 아래로는 내리지 않았다. 출발선은 이전 항목(영상)의 위치(범프 왼쪽 다리)를 유지했다. 윤곽 위쪽 변에 선이 끊긴 자리가 출발선 표시인지는 확인하지 못했다.
  - **기록**: 지시대로 `Circuit_Skyway` 의 최고 기록(세 속도 단계)을 지웠다. 이 머신에는 원래 기록이 없었다(`had=False` 셋).
  - **구간별 도로 폭**: `TrackDefinition.Segment.endHalfWidth`(0 = 트랙의 `roadHalfWidth`). 경사와 같은 방식으로 앞 조각의 끝 폭에서 이 조각의 끝 폭까지 조각 길이를 따라 **직선으로** 바뀐다(첫 조각은 마지막 조각의 끝 폭에서 시작 — 닫힌 랩). 검증: 0 이거나 [3, 20] m, 1 m 당 0.25 m 이하로만 변한다(가드가 트랙에서 최대 14° 틀어진다 — `RunnerMotor` 가 가드로 받는 60° 안), 틈에서는 폭이 그대로. 앵커·봉의 좌우 위치는 그 S 의 폭 안. `HalfWidthAt(s)`, `StartHalfWidth`/`EndHalfWidth(i)`, `WidestHalfWidth` 공개.
    - 노면: `RoadProfile.EndHalfWidth`(기본 = `HalfWidth`)와 `RoadMeshBuilder` 의 sweep 이 시작 폭 → 끝 폭을 선형 보간한다. `CircuitRace.LayRoad` 는 폭이 일정한 구간을 전처럼 한 번에 깔고, 폭이 변하는 조각만 따로 깐다 — 폭이 6 하나인 스타디움은 조각 나누기·정점이 그대로다(아래 지문 테스트). 출발선·체크포인트 띠도 그 S 의 폭.
  - **직선 스윙 봉**: `TrackDefinition.Pole`(S, 좌우 위치, reach) 목록. `BuildCenterline` 이 닫은 뒤 `Centerline.AddPole(위치, reach)` 로 넣고, `TryFindPole` 이 원호 중심 봉과 같은 규칙(시선 ±`poleMaxAngle`, 가장 가까운 것)으로 고른다 — 원호는 반지름, 추가 봉은 reach 에 모터의 `poleReach` 를 더한 거리까지. 원호 봉의 계산식·순서는 그대로다(동점은 원호 먼저). `GetPoles` 가 원호 다음에 추가 봉을 돌려줘 `CircuitRace.PlacePoles` 가 같은 모양으로 세운다(충돌체 없음). **스윙 규칙(`RunnerMotor`)은 바꾸지 않았다** — 직선 봉도 돈 거리 35 m 당 게이지 1칸.
  - **SKYWAY 값**: 오르막 끝 세 조각에서 반폭 6 → 10 → 13 → 15, 넓은 코너 R60 · 고가 직선 280 m · 넓은 코너 R65 는 15(도로 30 m), 출구 내리막 세 조각에서 15 → 12 → 9 → 6. 직선에 봉 7개: 직선 시작 + 20 m 부터 40 m 간격(S 430.39 … 670.39), 왼쪽부터 번갈아 중심선에서 ±11 m(도로 안), reach 10 m(+ `poleReach` 6 = 16 m 에서 걸린다). 원본은 `ChainRushCircuitSceneBuilder` 의 `SkywaySegments`·`SkywayPoles` 와 상수.
  - **장식 다시 만들기**: `SkywayEnvironmentBuilder` 의 S 범위가 옛 배치에 박혀 있었다(교각 S 410~1250, 마을 건너뜀 S 440~1210). 높이로 바꿨다 — 교각은 노면이 지면에서 충분히 높은 곳 어디나(전부터 있던 높이 조건), 마을은 노면 높이 2 m 넘는 곳 건너뜀. 비울 거리는 그 S 의 반폭 + 7 m, 교각 머리는 그 S 의 도로 폭 − 0.8 m. 새 메뉴 `Rebuild Skyway Exterior` 가 미리보기 씬에서 짓고 같은 경로에 덮어써 프리팹 GUID(장식 씬의 인스턴스)를 지킨다. 결과: 교각 36·마을 22동(이전 42 — 높은 노면 옆과 넓은 구간 옆이 비었다)·가로등 36·충돌체 0, 열린 씬은 dirty 가 되지 않았다(`ChainRushTitle(dirty=False)` 전후).
- **검증** (에디터 6000.3.19f1, UTC, 커밋 전 작업 트리):
  - `tools/select-tests.ps1` 기본 범위 → "전체"(`Centerline`·SO·씬 빌더). 컴파일 후 Console Error 0건(`Unity_GetConsoleLogs`).
  - EditMode `!Enemy !Endless` `testcasecount="269" result="Passed" passed="269" failed="0"` (15:07:22Z~15:07:26Z) — 이전 258 + 새 11(`TrackDefinitionTests` 6, `CenterlineTests` 3, `RoadMeshBuilderTests` 2).
  - 메뉴 `Run PlayMode Tests` 1회차 `testcasecount="178" result="Failed(Child)" passed="170" failed="1" skipped="7"` (15:08:01Z~15:15:36Z) — 실패는 위 "반지름을 키운 두 곳" 의 첫 번째. 지그 R26 뒤 두 테스트만 `testcasecount="2" passed="1" failed="1"` (15:17:02Z~15:17:33Z) — 두 번째(Ω). Ω 를 키우고 장식을 다시 만든 뒤 2회차 `testcasecount="178" result="Passed" passed="171" failed="0" skipped="7"` (15:20:06Z~15:27:39Z). skipped 7 = `[Explicit]` 측정 4 + 네트워크 레이스 3.
  - 그 실행의 값: SKYWAY 2랩 86.08 / 84.88 s(기준 74.97 s ±20%), 한 바퀴 1499.4 m, 최대 옆 거리 5.04 m(S 1172.7). 직선 봉 테스트 — 줄 11.00 m, 65.2° 왼쪽으로 돌고, 12.0 m 돌아 게이지 +0.343, 최대 옆 거리 6.76 m(반폭 15). **스타디움 지문 그대로** `ChainRush replay circuit: 1700 ticks identical, hash 0x18F31E7A2B4F79B3`.
  - `Long` 은 Endless 보류로 돌리지 않았다(`CLAUDE.md` §9-2). **사용자 플레이 확인: 아직** — 직선 슬라럼의 체감(봉 간격 40 m, 걸리는 거리 16 m, 한 번에 ⅓칸 남짓)은 플레이해 보고 정한다.
  - **② 병합** (사용자 "괜찮네 푸쉬 병합까지 완료"): 커밋 `e6900ab` 에서 `select-tests -Range dev...HEAD` = 전체(바뀐 파일 17개 — `Centerline`·SO·프리팹·씬 빌더, ProjectSettings 0개). 그 전체 판정의 근거는 위 2회차 EditMode·PlayMode 실행이다 — 그 뒤 커밋까지 바뀐 것은 `docs/` 뿐이다. `ProtoHarness.slnx` 는 이 머신의 차이라 넣지 않았다.
- **버린 대안**: 봉을 가드 밖에 세우기 — 봉을 중심으로 원을 돌면 봉 쪽 가드에 먼저 닿아 스윙이 캐리 없이 끝난다. 폭을 조각 경계에서 계단처럼 바꾸기 — 넓은 쪽 가드의 끝면이 진행 방향을 막는다. 직선 봉을 위한 새 스윙 규칙(예: 직선에서는 앞으로 끌어 주기) — 이번 요청은 "직선에도 스윙하며 게이지 채우기" 이고 규칙 변경은 체감을 본 뒤에 정한다.

## 2026-10-21 · SKYWAY LOOP 외부 장식과 로비 연결

- **결정 (사용자)**: 기존 스타디움처럼 주행 서킷과 무관한 외부 디자인을 추가한다. "computer use 말고 mcp 써라"에 따라 Unity MCP로 진행했다. 현재 브랜치에서 MCP 씬 저장·빌드 목록 등록·로비 연결까지 하는 이번 작업의 예외 승인: "ㄱㄱ". 이후 "일단 커밋 병합 푸쉬 진행"으로 Git 작업을 승인했다.
- **결과**: `ChainRushTrackSkyway.unity`가 `SkywayExterior.prefab` 하나를 배치하고, 로비 SKYWAY LOOP가 해당 씬을 Additive로 불러온다. 마을 건물 42동·교각 27개·가로등 35개·타워 24동·정원·가로수·입체 간판. 기존 재질을 재사용하며 충돌체·Rigidbody·런타임 스크립트 0개. 실제 서킷 정의·주행 씬·기존 스타디움 씬의 해시는 그대로다.
- **검증** (UTC, 에디터 6000.3.19f1): EditMode `testcasecount="258" result="Passed" passed="258" failed="0" skipped="0"` (08:28:27Z~08:28:33Z). PlayMode `testcasecount="176" result="Passed" passed="169" failed="0" skipped="7"` (08:30:15Z~08:39:03Z). 제외 7건은 기존 Explicit 측정 4건·네트워크 레이스 3건. Flow 22/22, SKYWAY 장식 로드·물리 분리·2랩 완주·로비 복귀 시 장식 해제 포함. 랩 81.12/79.88초, 최대 옆 거리 1.92m. 실제 야간 Main Camera 화면 확인, 편집 상태 복원 후 Console Error 0건. 상세 로그·선택 도구 출력: `ART_SKYWAY.md`.
- **커밋 범위**: SKYWAY 트랙·외부 장식·연결·테스트·문서. 기존 머신 차이 `Packages/packages-lock.json`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/ProjectVersion.txt`, `ProtoHarness.slnx`는 로컬에 보존하고 제외한다. 빌드 씬 목록 변경은 위 승인 범위에 포함한다. → 병합할 때 §9-2("ProjectSettings 변경은 그것만 담은 브랜치로") 에 따라 `feature/settings-skyway-build-scene`(`7cfabcf`)으로 떼어 `dev` 에 먼저 병합했고, 이 브랜치의 커밋은 같은 트리로 새 `dev` 위에 다시 만들었다(`d65b0bb` → `076ac8e`, 푸시 전, 사용자 "네 추천대로"). 사용자 직접 2랩 플레이는 아직 확인하지 않았으므로 `main` 마일스톤 병합은 하지 않는다.

## 2026-10-21 · SKYWAY LOOP — 빌리지 고가의 질주를 옮긴 두 번째 서킷

- **결정 (사용자, 2026-10-21, "빌리지 고가의 질주를 똑같이" → 합의안 "ㄱㄱ", `feature/track-skyway`)**:
  - **배치를 읽은 곳**: 사용자가 준 기록 영상(크레이지레이싱 카트라이더 매우빠름 2랩 1:30.42, 1랩 45.61초)을 받아 1랩을 0.5초 간격으로 읽었고, 카운트다운 화면의 트랙 썸네일(흰 윤곽)로 위상을 맞췄다. 웹 문서(나무위키 등)에는 구간 순서가 없었다. 자동 추출 3가지(미니맵 화살표 각도, 하늘 가로 이동량, 지평선 띠 이동량)는 실패해 멈췄다 — 미니맵이 진행 방향 기준으로 돌고, 화면 고정 HUD 가 이동량을 0 쪽으로 끌었다.
  - **배치** (시계 방향, 출발선에서 북쪽): 출발 직선 71 → ① 우180 R28 고리 → 아치 밑 직선 → ② 좌90 R36 → ③ 우90 R36 → 초반 오르막(15%, 0 → 12 m) → ④ 우90 R27 로 고가 진입 → **고가 서쪽 직선 355.56 m**(한 바퀴에서 가장 긴 직선, 원작 "까마득한 직선") → 완만한 우90 R60 → S자(좌30·우30 R80) → 완만한 우90 R60 → 고가 동쪽 직선 60 → 출구 내리막(−10%, 12 → 9 m) → ⑤ 긴 우90 R40 → ⑥ 좌90 R27 → **깎아자른 내리막**(−20%, 9 → 0 m) → ⑦ 좌90 R25 → 골인. 29조각, 한 바퀴 1482.82 m, 회전 합 +360°.
  - **축척**: 영상 시간 비례(영상 1초 ≈ 28.5 m). 원작 길이(한 바퀴 3.7 km)는 20 m/s 에서 한 바퀴 3분이 넘어 버렸다. 랩 수는 원작대로 2랩(약 2분 30초, 사용자 기본값 수락).
  - **추정한 것**: 코너 각도는 썸네일에서, 고가 위 완만한 커브 두 개는 썸네일 + "회전 합 360°" 에서 거꾸로 정했다(시속 300 km 안팎이라 0.5초 간격 화면에 거의 안 보였다). 꼬리(⑤~⑦)는 썸네일과 영상 시간 비율이 맞지 않아 절충했다. 서쪽 직선 길이는 닫힘을 맞추려고 푼 값(오차 4 mm).
  - **이름**: 원작 이름을 쓰지 않고 로비의 자리표시 `SKYWAY LOOP` 를 채웠다(같은 날 "임시 잠금 트랙" 항목의 두 번째 자리). 에셋 `Circuit_Skyway` — 최고 기록 키가 에셋 이름이다(`BestRecords.cs:63`). ~~장식 씬은 아직 없다(`environmentScene` 빈 값).~~ → 같은 날 다른 세션에서 장식 씬 `ChainRushTrackSkyway` 를 만들어 연결했다(위 "SKYWAY LOOP 외부 장식과 로비 연결").
  - **만드는 법**: 에디터 메뉴 `ProtoHarness > Chain Rush > Create Skyway Track`(`ChainRushCircuitSceneBuilder.SkywaySegments` 가 원본). 새 인스턴스에 채우고 `TryValidate` 를 통과해야 저장하며, 에셋이 이미 있으면 `CopySerialized` 로 덮어써 GUID(로비 참조)와 기록을 지킨다.
  - **① R22 → R28**: 처음 R22 180° 에서 봇이 고리 한가운데(S 111.6)에서 5.62 m 밀려 가드에 닿았다(기준 5.6). R22 90° 인 스타디움은 문제가 없었다 — 180° 를 이어 도는 동안 바깥으로 밀리는 양이 커진다. 테스트 기준은 두고 코스를 고쳤다(사용자 "ㄱㄱ"). R28 에서 가드와 가장 가까운 거리는 1.92 m(S 1456.8, ⑦).
  - **로비 씬**: `LobbyMenu.tracks` 에 항목을 `Unity_RunCommand` 로 넣고, 저장은 사용자가 했다 — §0 이 `Unity_RunCommand` 로 씬 저장을 금지한다. 합의안에는 "RunCommand 로" 라고만 적어 이 금지를 놓쳤고, 실행 전에 알아채 나눴다.
- **버린 대안**: 원작 길이 그대로(한 바퀴 3분 이상). 첫 초안(웹 문서의 특징만으로 짠 배치) — 영상으로 확인하니 골인 직전 코너가 오른쪽 U자가 아니라 왼쪽 90°였고, 연속 90도 코너가 고가 앞에 있었다. ① 을 R26 으로 — 여유가 적다. 에셋을 지우고 다시 만들기 — 삭제는 §0 승인 대상이고 GUID 가 바뀌어 로비 참조가 끊긴다.
- **검증** (에디터 6000.3.19f1, UTC, 커밋 전 작업 트리 = 이 머신 버전 차이 파일 제외):
  - 컴파일 `*** Tundra build success (50.55 seconds), 23 items updated, 1618 evaluated` → 테스트 진단 메시지 `(2.87 seconds), 5 items` → 빌더 덮어쓰기 `(2.52 seconds), 7 items`. `error CS` 0건.
  - 메뉴 실행: `asset=Circuit_Skyway segments=29 lap=1482.818 laps=2 anchors=0 arc1R=28 valid=True guid same=True (1b518dfd3292dce47904e7b029d0c925)`.
  - `ChainRushFlowTests` (R22): `testcasecount="22" result="Failed(Child)" passed="21" failed="1"` (07:26:24Z~07:27:37Z) — 실패 `Race_SkywayFromTheLobby_BotFinishesTwoLaps` "The bot touched a guard. Expected: less than 5.5999999f But was: 5.61562395f". 같은 테스트 단독 재실행 `testcasecount="1" passed="0" failed="1"` (07:29:47Z~07:30:16Z), "at S 111.6 (segment 1)", 같은 값.
  - R28 뒤 단독: `testcasecount="1" result="Passed" passed="1" failed="0"` (07:33:49Z~07:34:18Z), 랩 81.12 s / 79.88 s(기준 74.1 s ±20%), 최대 옆 거리 1.92 m.
  - ① `tools/select-tests.ps1` 기본 범위 → "전체"(새 SO·씬·에디터 빌더, 그리고 이 머신의 버전 차이 파일). EditMode `!Enemy !Endless` `testcasecount="258" result="Passed" passed="258" failed="0"` (07:34:36Z~07:34:40Z). 메뉴 `Run PlayMode Tests` `testcasecount="176" result="Passed" passed="169" failed="0" skipped="7"` (07:35:07Z~07:44:19Z) — skipped 7 = `[Explicit]` 측정 4 + 네트워크 레이스 3. 스타디움 지문 테스트 포함. `Long` 은 모두 무한 모드라 Endless 보류 동안 돌리지 않는다(`CLAUDE.md` §9-2).
  - Console Error 2건은 테스트가 기대하는 로그다: `RaceAssembler: Single at VeryFast cannot be raced yet` (`Race_LockedSpeedClass_…`), `RunnerProfile '': every scale and the weight must be positive.` (`ChainRushProfileTests.cs:138`).
  - **② 병합 게이트** (에디터 6000.3.19f1, 사용자 "검증해보고 괜찮으면 ㄱㄱ" → "네 추천대로"): 다른 세션의 장식 작업까지 담은 커밋 `d65b0bb` 를 먼저 검증 — EditMode `testcasecount="258" result="Passed" passed="258" failed="0"` (14:02:39Z~14:02:45Z), PlayMode 전체 `testcasecount="176" result="Passed" passed="169" failed="0" skipped="7"` (14:03:27Z~14:12:14Z). 빌드 씬 목록을 §9-2 대로 떼어 낸 `feature/settings-skyway-build-scene`(`7cfabcf`): `select-tests -Range dev...HEAD` = 전체(ProjectSettings 1개), EditMode `258 Passed failed="0"` (14:16:52Z~14:16:58Z), PlayMode `testcasecount="174" result="Passed" passed="167" failed="0" skipped="7"` (14:17:41Z~14:26:00Z, 맵이 없는 브랜치라 스카이웨이 2건 빠짐) → `dev` `c587a7e`. 새 `dev` 위의 `076ac8e`(트리 `765e1fe` = `d65b0bb` 와 같음): `select-tests -Range dev...HEAD` = 전체(바뀐 파일 16개, ProjectSettings 0개), EditMode `258 Passed failed="0"` (14:28:56Z~14:29:03Z), PlayMode `testcasecount="176" result="Passed" passed="169" failed="0" skipped="7"` (14:29:46Z~14:38:36Z) — 스카이웨이 2건 통과, 랩 81.12/79.88 s, 최대 옆 거리 1.92 m. skipped 7 = `[Explicit]` 측정 4 + 네트워크 레이스 3. `Long` 은 Endless 보류로 안 돌림.

## 2026-10-21 · 타이틀 카트라이더풍 로고 · 버전/빌드 표기 · 로비 항목 설명 · 임시 잠금 트랙

- **결정 (사용자, 2026-10-21, 요청서 "ㄱㄱ", `feature/ui-title-lobby-info`)**:
  - **타이틀**: `STADIUM / AFTER DARK`, `ALPHA / ARMORED RUNNER`, `CHAIN RUSH / NIGHT DRIFT`, 러너 초상을 지웠다. 카트라이더(원작·드리프트) 타이틀을 참고해 큰 기울임 로고(그림자, RUSH 를 오른쪽으로 밀어 질주감) 뒤로 사선 스피드 스트라이프 4줄(−9°, 직접 만든 `GUI.matrix`), 체커기 띠, 숨 쉬듯 밝아졌다 어두워지는 시작 버튼 테두리. 오른쪽 가운데는 러너 그림이 정해질 때까지 비운다. `TitleScreen.runnerArtwork` 필드를 지웠다 — 타이틀 씬 YAML 의 참조는 고아로 남고(동작 영향 없음), 에디터에서 씬을 다음에 저장할 때 사라진다.
  - **버전·빌드**: 오른쪽 위 `v0.0.1 · TEST BUILD`. 버전은 `TitleScreen.Version` 상수(손으로 올린다). Player Settings `bundleVersion`(0.1.0)은 ProjectSettings 금지선(§0)이라 쓰지 않았다. 빌드 이름은 `Debug.isDebugBuild` 로 자동 — 에디터·개발 빌드 `TEST BUILD`, 릴리스 빌드 `RELEASE`.
  - **로비 설명 패널**: RACE SETUP 패널과 러너 초상을 지우고, 메인·트랙·속도 페이지 오른쪽에 커서가 가리키는 항목의 설명(페이지 태그 · 이름 · 잠김이면 `LOCKED · 준비 중` · 설명 · 트랙이면 최고 기록)을 둔다. 설정·조작 키·러너 페이지에는 없다. `SetupRunner` 는 테스트가 쓰고 있어 남겼다(그리는 곳만 없어짐).
  - **임시 잠금 트랙**: 씬 트랙 뒤에 `NEON HARBOR`, `SKYWAY LOOP` 를 코드 배열(`PlaceholderTracks`)로 붙였다. 정의가 없어 잠그고, 기록 칸은 "준비 중", 결정하면 "새 트랙은 이후 마일스톤에서 열립니다." 씬은 고치지 않았다. 씬 트랙의 설명은 제목으로 찾고(`TrackDescription`), 없으면 `LogError`(트랙은 그대로 달린다).
  - **플레이 피드백 반영** ("배경 위아래 절반이 우측 판과 색이 비슷해 판이 잘 안 보인다", "이전 버튼을 좌측 하단에, 키 설명은 우측 하단으로"): 오른쪽 판(설명·러너 프로필)을 `LobbyMenu.Card` 로 — `Surface` 보다 밝은 채움(24,36,54), 민트 35% 테두리 1px, 오른쪽 아래 그림자, 위쪽 민트 3px. 이전 버튼은 왼쪽 아래(높이−84), 키 설명은 같은 줄 오른쪽 정렬, 알림 줄은 버튼 위(높이−140)로.
  - **카드 확대 · 호버 · 눌림 · UI 사운드 자리** (플레이 피드백 "살짝 띄운 박스 디자인 맘에 든다, 버튼들·선택 요소에도", "호버는 살짝 회색, 클릭은 눌리는 효과", "사운드는 추후에, 세팅만" → 요청서 "진행"): `Card` 를 `NightUi.Card`(모양 `CardLook` Plain/Selected/Locked/Action)로 옮기고, 누를 수 있는 카드 `NightUi.PressableCard`·`CardButton` 을 두었다. 호버는 회색 16% 덮개, 누르는 동안은 면이 그림자 쪽으로 (4,5) 내려앉고 22% 덮개. 눌림 판정은 `GUIUtility.hotControl != 0` 이고 마우스가 그 카드 위일 때(새 입력 의존 없음). 클릭 판정은 원래 칸 전체(`GUI.Button`, 빈 스타일), 글자는 내려앉은 면 위에 그린다. 적용: 로비 목록 칸·러너 칸·이전·레이스 시작, 오른쪽 판(`PanelCard`), 타이틀 시작 버튼·버전 태그, HUD 출발 START RUN·결과 다시 달리기/로비로·일시정지 칸.
  - **`Audio/UiSound`** (static class, 첫 UI 사운드 사례): `Cue` = Move / Confirm / Back / Denied, `Played` 이벤트, `Play(cue)`. 아직 듣는 쪽이 없어 소리는 안 난다 — 나중에 재생기 하나가 `Played` 를 구독하면 된다. 씬을 고치지 않으려고 직렬화 참조 대신 static 이벤트. 부르는 곳: 로비 `Move`(커서가 실제로 움직일 때만)·`Adjust`·`Confirm`(잠김이면 Denied)·`Back`, 타이틀 `Enter`, HUD `MovePause`·`ChoosePause`(로비로는 Back)·마우스 버튼 클릭, 그리고 마우스가 새 카드에 올라갈 때(`PressableCard`). **키보드 경로인 `ChainRushGame.Update`(Enter 출발·재시작, ESC 일시정지)는 장치 입력 경로라 이번에 건드리지 않아 아직 신호가 없다.**
  - 이름 충돌: 판 함수를 처음 `NightUi.Panel` 로 지었다가 같은 이름의 색 필드와 겹쳐 `error CS0102` → `PanelCard`.
- **버린 대안**: UI 사운드를 ScriptableObject 클립 묶음 + 화면별 AudioSource 직렬화로 — 씬 3개를 에디터에서 고쳐야 하고 소리가 아직 없다. 설명을 `TrackChoice` 직렬화 필드로 — 로비 씬을 에디터에서 고쳐야 해서 이번 범위 밖. 버전을 `Application.version` 으로 — ProjectSettings 를 0.0.1 로 바꿔야 한다.
- **검증** (에디터 6000.3.19f1, UTC, 커밋 전 작업 트리 = 이 머신 버전 차이 파일 제외):
  - 컴파일 `*** Tundra build success (62.55 seconds - 0:01:02), 29 items updated, 1618 evaluated`, `error CS` 0건, Console Error 0건.
  - ① 빠른 확인: `tools/select-tests.ps1` 기본 범위는 "전체"(이 머신의 `ProjectSettings`·`packages-lock` 버전 차이 때문). 바뀐 코드가 고르는 `ChainRushFlowTests`·`ChainRushProfileTests` + 서킷 지문으로 돌림 → `testcasecount="27" result="Passed" passed="27" failed="0"` (05:31:08Z~05:33:31Z). 새 테스트 2건(`Lobby_PlaceholderTracks_RefuseWithANoticeAndStay`, `Lobby_MainTrackAndSpeedEntries_EachHaveADescription`) 포함.
  - 화면: 플레이 모드(시작 씬을 타이틀로 잠시 지정, 끝나고 되돌림)에서 타이틀·메인·트랙(NEON HARBOR 잠김 알림)·속도를 `ScreenCapture` 로 찍어 확인. 그때 Console 에 `Deleting invalid font reference.` 경고 20건(14:34:12, 플레이 모드 진입 시)이 있었다 — 이번 변경 전 세션의 `Editor-prev.log` 에도 745건이 있어 기존 경고다(원인은 조사하지 않음).
  - 피드백 반영 뒤: 컴파일 `*** Tundra build success (10.39 seconds), 11 items updated, 1618 evaluated`, 같은 27건 `testcasecount="27" result="Passed" passed="27" failed="0"` (05:44:02Z~05:45:57Z), 메인(멀티 잠김 알림)·러너 페이지 캡처로 판 테두리·버튼 위치 확인, Console Error 0건.
  - 카드·호버·UiSound 뒤: 컴파일 `*** Tundra build success (8.34 seconds), 29 items updated, 1618 evaluated`. `select-tests` "전체"(HUD·NightUi + 버전 차이 파일) → EditMode `testcasecount="258" result="Passed" passed="258" failed="0"` (05:53:32Z~05:53:38Z), 메뉴 `Run PlayMode Tests` `testcasecount="174" result="Passed" passed="167" failed="0" skipped="7"` (05:54:12Z~06:02:57Z) — skipped 7 = `[Explicit]` 측정 4 + 네트워크 레이스 3, 새 `Lobby_MovesChoicesRefusalsAndBack_EachPlayTheirCueOnce` 포함. 타이틀·트랙·레이스 일시정지 캡처로 카드 확인, Console Error 0건. **호버 회색·눌림은 실제 마우스가 필요해 캡처로 확인하지 못했다** — 사용자 플레이 확인 대상.
  - ② 병합 게이트 (커밋 `24e7a22`, 에디터 6000.3.19f1): `tools/select-tests.ps1 -Range dev...HEAD` → 바뀐 파일 7개(UiSound·HUD·LobbyMenu·TitleScreen·NightUi·FlowTests), EditMode 전체, PlayMode 클래스 10개(Combat·Flow·InputSource·Presentation·Profile·NetReconcile·NetRemote·RaceOrder·RaceStandings·RaceStandingsText) + 지문 1건. "전체" 아님 → `Long` 안 돌림. 결과 PlayMode `testcasecount="101" result="Passed" passed="101" failed="0" skipped="0"` (06:10:28Z~06:12:50Z) — XML 클래스 목록은 도구 출력에서 `ChainRushCombatTests` 만 빠졌고, 그 클래스는 통째로 `[Category("Endless")]` 라 `!Endless` 로 제외된 것(`ChainRushCombatTests.cs:21`). EditMode `testcasecount="258" result="Passed" passed="258" failed="0"` (06:13:18Z~06:13:22Z). 입력 장치 경로(`KeyboardMouseInputSource`, `ChainRushGame.Update`)는 안 바꿔 Device 안 돌림. 코스 코드 안 바꿔 Sweep 안 돌림.
  - 사용자 플레이 확인: "좋다 커밋 병합 푸시 ㄱㄱ" — 2026-10-21.

## 2026-10-20 · 러너 선택창 프리뷰 · 입장/이전 버튼 · 인게임 일시정지 메뉴 · R 재시작 제거

- **결정 (사용자, 2026-10-20, `feature/m2-runner-preview` → `feature/m2-lobby-pause-ui`)**:
  - **러너 프리뷰** (요청서 "ㄱㄱ"): 로비 러너 페이지 가운데 칸(1600 기준 364×540)에 커서가 가리키는 러너의 디자인(`RunnerProfile.Look`)을 보이고, 칸 안에서 누른 마우스 드래그로 좌우 회전(0.45°/px, 처음 −30°, 러너를 바꿔도 각도 유지, 자동 회전 없음). `Flow/RunnerPreview`(sealed class)가 무대(턴테이블·카메라·방향 조명, y −1000)를 실행 중에 만들고 디자인 5개를 Awake 에서 한 번만 복제해 `SetActive` 만 바꾼다 — 로비 씬·프리팹·레이어(ProjectSettings)를 건드리지 않으려고(`RemoteGhost` 와 같은 방식). 로비 OnGUI 가 화면 전체를 불투명하게 덮어 씬 안 모형은 안 보이므로 카메라 → RenderTexture(2배 해상도, MSAA 4) → `GUI.DrawTexture`. 로비 씬에 조명이 없어 무대 조명을 만든다. 디자인이 빈 러너는 "디자인 없음", 그릴 것이 없는 디자인은 `ArgumentException` → 로비가 로그를 남기고 꺼진다. 런타임에서 처음으로 `Application.isPlaying` 에 따라 `Destroy`/`DestroyImmediate` 를 고른다(EditMode 테스트가 정리를 확인하려고).
  - **입장 버튼** ("잘 보이도록 디자인및크기"): 러너 줄 클릭은 고르기만(`LobbyMenu.Highlight`), 능력치 칸 아래 민트 "입장   ENTER  →"(420×92, 32pt) 또는 Enter 로 레이스. 다른 페이지의 클릭은 그대로 바로 결정.
  - **RACE SETUP 러너 이름** (플레이 피드백 "러너 이름이 바뀌지않는다"): 입장한 러너만 보여서, 클릭이 고르기만 하게 바뀐 뒤 드러났다. 러너 페이지에서는 커서, 다른 페이지에서는 입장한 러너(`SetupRunner`). 고치기 전 실패 테스트 있음(아래).
  - **이전 버튼** ("esc 기능도 그대로 두고"): 모든 로비 페이지 제목 칸 오른쪽 "←  이전   ESC" = `Back()`. 클릭은 그리기가 끝난 뒤 처리(입장 버튼도) — 2026-10-20 클릭 버그와 같은 이유.
  - **일시정지 메뉴** (요청서 "ㄱㄱ", 기본값 ↑/↓·Enter 포함): ESC 가 화면을 50% 어둡게 하고 PAUSED·지금 랩/시간·**계속 / 재시작 / 로비로**(클릭 또는 ↑/↓ + Enter, ESC 는 계속). 재시작은 `ManualStart` 일 때만(네트워크 레이스 숨김), 로비로는 로비에서 온 레이스만(`ChainRushHud.Offers`). 결과 패널에서 떼어 `DrawPause` 로.
  - **R 재시작 제거** ("인게임에서 r 버튼은 없어져도 되겠다"): `ChainRushGame.Update` 에서 R 을 뺐다. 결과 화면의 재시작은 Enter(원래 동작)·버튼("ENTER / RUN AGAIN"). Device 테스트 `Input_ArrowKeys_…Restarts` 의 R 재시작을 "R 은 아무것도 안 함" + ESC → ↓ → Enter 재시작으로 바꿨다 — 사양 변경으로 사용자 승인. `Input_ManualStartOff_EnterAndRDoNothing` 은 지금 사양에서도 맞아 그대로.
- **브랜치**: 프리뷰를 커밋하지 않은 채 `feature/m2-lobby-pause-ui` 로 뻗어 한 브랜치로 묶었다(검증 비용 1회, 사용자 기본값 3).
- **검증** (에디터 6000.3.19f1, UTC, 커밋 `11a72d4` = 이 머신 버전 차이 파일 3개 제외):
  - 컴파일 `Tundra build success`, `error CS` 0건.
  - RACE SETUP 고치기 전: `ChainRushFlowTests.Lobby_RunnerPage_HighlightPicksWithoutStarting` `result="Failed"` "RACE SETUP names the runner being picked" `ChainRushFlowTests.cs:152` (07:17:01Z). 고친 뒤 통과.
  - `tools/select-tests.ps1 -Range dev...HEAD`: "전체"(`ChainRushGame.cs` → 코어 시뮬). `Long` 은 Endless 보류로 돌리지 않음.
  - EditMode `testcasecount="255" result="Passed" passed="255" failed="0"` (07:29:09Z~07:29:13Z) — 새 `RunnerPreviewTests` 6건.
  - 게이트 `testcasecount="171" result="Passed" passed="164" failed="0" skipped="7"` (07:29:38Z~07:36:49Z) — skipped 7 = `[Explicit]` 측정 4 + 네트워크 레이스 3. 새 PlayMode 4건(프리뷰가 RenderTexture 가운데에 실제로 그리는지 픽셀로 확인 포함).
  - Device (`Run Device Input Tests`, 입력 장치 경로 `ChainRushGame.Update` 를 바꿔서): `testcasecount="3" result="Passed" passed="3" failed="0"` (07:38:06Z~07:38:23Z), 시작 때 `isApplicationActive [True]`. 그 직전 시도는 에디터가 뒤에 있어(`False`) 시작하지 않았다.
  - 서킷 지문 테스트는 게이트 안에서 통과(시뮬 변경 없음, R 키 한 줄만).
  - 사용자 플레이 확인: "다 좋다"(프리뷰·입장·일시정지), "확인했다"(이전 버튼) — 2026-10-20.

## 2026-10-20 · M2-c 설정 · 로비 클릭 버그 · 러너 디자인 5종 연결 (아트 AI 병행)

- **결정 (사용자, 2026-10-20, `feature/m2-settings`)**:
  - M2-c 항목: 추천 1차안 "ㅇㅋ" — 전체·음악·효과음 볼륨(0~10)과 속도 시야 효과(켬/끔), 조작 키는 하위 페이지. 화면(창/전체·해상도·VSync)은 에디터에서 검증할 수 없어 플레이어 빌드 때로, 피격 깜빡임은 서킷에 맞을 대상이 없어 뺐다. 저장은 `PlayerPrefs`(2026-10-19 "저장방식은 그대로").
  - **손상된 저장값**: "1승인" — 생성 시 키·값을 `Debug.LogError` 로 알리고 그 항목만 기본값, 저장값은 덮어쓰지 않는다(`GameSettings.ReadLevel`·`ReadSwitch`). 이유: 예외를 던지면 `static Player` 초기화가 `TypeInitializationException` 이 되어 로비·오디오·카메라가 함께 깨지고 플레이어가 고칠 길이 없다. 잡아서 삼키는 예외가 없고 로그가 남아 §5 위반이 아니다(리뷰). 버린 대안: 예외(위 이유), 지연 로드 후 예외(컴포넌트 Awake 에서 터져 이후 매 프레임 NullReference).
  - 리뷰 권고 "2 a,b,c,d ㄱㄱ": 손상값 테스트, 로비 설정 페이지 PlayMode 테스트, 키 문자열을 생성자에서 한 번, 설정 페이지 하단 "← / →  조절" 안내.
  - `ChainRushFlowTests.Lobby_BackAndSettings_GoPageByPage` 의 설정 페이지 `Confirm()` → `Back()`: 사양 변경(설정에서 Enter 는 항목 선택)으로 사용자 승인.
  - **로비 마우스 클릭 버그** ("1 브랜치 2 ㄱ"): 메인에서 "싱글 레이스"를 클릭하면 `IndexOutOfRangeException at LobbyMenu.cs:433`. `DrawList` 반복 도중 `GUI.Button` → `Confirm()` 이 페이지를 바꿔 남은 줄이 새 페이지의 배열(`trackRecords`)을 읽었다. `caf9338`(로비 최고 기록) 때부터 있었다. 키보드 Enter 는 `Update` 라 나지 않고, 테스트는 `Confirm()` 을 직접 불러 잡지 못했다. 클릭한 줄만 기억했다가 반복 뒤에 `Confirm()` 한다. 실제 마우스 이벤트가 필요해 고치기 전 실패 테스트는 없다 — 사용자가 수동 클릭 확인으로 받아들였다(VERIFICATION §4-4 예외).
  - **러너 디자인 연결**: "진행하라 그리고 각 러너 별로 어울릴만한 디자인을 골라서 적용까지". `RunnerProfile.look`(비면 씬 모형 그대로) → `RaceAssembler` 가 능력치 다음 `RunnerLook.Apply` 로 메시·재질만 옮긴다(자식 순서로 전부 검사한 뒤에만 복사, 어긋나면 `ArgumentException` → 레이스를 끔). 매핑은 능력치로 골랐고 같은 이름 디자인이 맞았다: ALPHA(전부 1.0) 청록 표준형, BETA(가속 1.2·무게 0.9) 주황 등 추진기, DELTA(게이지 1.2) 보라 에너지 링, THETA(최고 속도 1.03) 흰색 날개, OMEGA(무게 1.2) 검정 중장형. 연결은 `Unity_RunCommand`(SerializedObject, 다섯 디자인을 `LoadPrefabContents` 복사본에 먼저 적용해 본 뒤) — 에셋마다 `look:` 1줄.
- **아트 AI 병행** (사용자 "다른 ai 에게 아트적인 요소 작업시킬건데"): 같은 폴더·같은 브랜치에서, 파일 경계(아트 = 장식 씬·`ArmoredRunner`·`Art/`·`docs/ART_UI.md`, 코드 = `Scripts/`)와 에디터 시간 나누기(검증 전 아트가 저장하고 손 뗌, 사용자가 신호 중계)로 진행했다. 다른 브랜치·클론은 §9-2(워크트리 금지)와 Unity MCP 가 어느 에디터에 붙는지 확인 못 한 문제로 쓰지 않았다. 아트의 `Unity_RunCommand` 는 사용자가 코드 전문을 보고 승인했다. 아트가 만든 것: 스타디움 장식(관중석 2,048석·육상 트랙 등, 물리 0), `ArmoredRunner` 모서리 메시, 러너 디자인 5종(`Art/Models/Runner*.prefab`, 계층은 `ArmoredRunner` 와 같음). 커밋은 경로로 나눴다(`47bbe68` 설정, `13c24c5` 아트, `2c2111f` 디자인 연결).
- **M7 할 일**: `RemoteGhost` 는 로컬 `RunnerTilt.Body` 를 복제하므로 멀티에서 상대가 **내** 디자인으로 보인다(리뷰). 멀티가 잠긴 동안은 닿지 않는다.
- **검증** (에디터 6000.3.19f1, UTC, 커밋한 내용 = 3차 검증 때의 작업 트리 + 이 머신 버전 차이 파일 3개 제외):
  - 1차(M2-c): EditMode `testcasecount="243" result="Passed" passed="243"` (04:56:21Z), 게이트 `testcasecount="165" result="Passed" passed="158" failed="0" skipped="7"` (04:57:02Z~05:04:24Z).
  - 2차(클릭 수정 + 장식 씬 확장): EditMode `243 Passed` (05:41:50Z), 게이트 `testcasecount="165" result="Passed" passed="158" failed="0" skipped="7"` (05:42:27Z~05:49:20Z).
  - 3차(디자인 연결): 컴파일 `Tundra build success`, `error CS` 0건. EditMode `testcasecount="249" result="Passed" passed="249" failed="0"` (06:22:19Z~06:22:23Z). 게이트 `testcasecount="167" result="Passed" passed="160" failed="0" skipped="7"` (06:23:03Z~06:30:07Z) — skipped 7 = `[Explicit]` 측정 4 + 네트워크 레이스 3.
  - 서킷 지문 세 번 모두 `0x18F31E7A2B4F79B3` 그대로(설정·디자인·장식은 표현만).
  - 실행 뒤 Console 의 Error 2건은 테스트가 기대한 로그(`cannot be raced yet`, `must be positive`).
  - `tools/select-tests.ps1 -Range dev...HEAD`: "전체"(프로필 `.asset`·장식 씬·`Prefabs/`). `Long` 은 Endless 보류로 돌리지 않음, 입력 장치 경로를 바꾸지 않아 Device 해당 없음.
  - 사용자 플레이 확인: "플레이 확인했다"(2026-10-20 — 클릭, 설정, 디자인 5종).

## 2026-10-19 · 그래플: 앵커 높이 안에서만 건다 · 공중 가속 · NetReconcile 1건 M7 까지 보류

- **결정 (사용자, 2026-10-19 새 세션, `fix/swing-boost-grapple`)**:
  - NetReconcile 1건(아래 항목의 "NetReconcileTests 1건"): B "M7까지 게이트 제외". `NetEndReplay` 카테고리를 붙이고 메뉴 `Run PlayMode Tests`·`tools/select-tests.ps1` 필터·`CLAUDE.md` §9-2·`docs/RULES/VERIFICATION.md` §4-4 의 제외 목록에 넣었다. 같은 파일의 다른 결승 테스트 5개는 게이트에 남는다.
  - 그래플 낙하(보고 2) — 사용자 보충 "떨어지기보단 그래플을 너무 일찍 걸면 그 줄이 길어져서 떨어질 정도의 길이가 된다는 뜻". 사거리 줄이기·앙각 제한을 거쳐(아래) 추천안 "줄 길이 상한" 에 "추천대로", 그 결과(너무 일찍 뛰면 걸 수 없어 떨어진다)에 "추천대로 ㄱㄱ". 빨리 감기는 "속도 이득이 아닌가"(사용자) — 수평 속도 이득은 없지만 강화 그래플 보상과 겹쳐 버렸다. 그래플 타이밍 보상은 추후 아이디어(NEXT_REQUESTS M6).
  - 공중 가속 — "점프일때 속도 고정보단 가속은 먹게 해줘 감속은 아니더라도", "공중 가속이더라도 최대속도엔 걸려야한다", "떼고있을땐 당연히 가속 되면 안된다. 떼면 고정으로".
  - 스윙 순간이동·코너 중간 미걸림(보고 3): "2+3" — 측정은 이 브랜치 밖, 기록만.
- **그래플 측정** (`ChainRushProbeTests` 에서 시작해 `ChainRushCircuitTests.Grapple_JumpedBeforeTheGap_…` 로 옮김; 서킷 첫 그래플 틈 s 314.1~330.1, 앵커 노면 위 10 m, s 약 322): 틈 앞 4.5/8/10/12/14/16 m 에서 뛰어 걸 수 있게 되자마자 걸면 줄 14.8/17.7/19.4/19.8/19.9/20.0 m, 가장 낮은 높이 1.37/1.37/1.37/1.00/−0.22/−1.67 m(러너 중심, 선 높이 1.05). 위험한 경우는 모두 **올라가는 중**(세로 +5~+9 m/s)에 걸었다 — 처음 추정 "내려가는 중" 은 틀렸다. 그래플 중 앞 속도는 약 20 m/s 로 유지돼(`RunnerMotor.Step` 의 그래플 가속 5, 목표 max(16, 달리기)) 긴 줄은 느슨한 채로 러너가 점프 포물선대로 떨어지고, 앵커를 0.5 m 지나는 바닥에서 놓인다.
  - **사거리 17 m** (사용자가 씬에서 바꿈, 뒤에 20 으로 되돌림): 16 m 점프가 −1.99 m 로 더 나빠졌다. 거는 지점이 조금 당겨질 뿐.
  - **앙각 30° 이상만** (코드, 걷어냄): 12/14/16 m 점프가 33° 에서 줄 11.5~12.4 m 로 걸려 0.77/−0.35/−0.90 m. 걸려 있는 동안 러너는 앵커에서 줄 길이보다 멀어질 수 없으므로 가장 낮을 수 있는 높이는 앵커 높이 − 줄 길이다 — 각도로는 보장되지 않는다.
- **결정한 규칙 (`GrappleController`)**: 새 필드 `minHangHeight` 1.05(선 높이). 앵커까지 거리가 (앵커의 노면 위 높이 − `minHangHeight`) 이하일 때만 후보가 되고 걸린다(`SelectCandidate`, `TryAttach` 의 줄 = 그 거리). 이 서킷은 8.95 m. 씬에 값이 없어 코드 기본값을 쓴다(씬 수정 없음). 사거리 `maxRange` 20 은 그대로.
  - 결과(같은 테스트, 공중 가속 뒤): 4.5~14 m 점프는 그 점프에서 줄 8.7~8.9 m 로 걸리고 가장 낮은 높이 1.69~3.50 m. 16·18 m(약 0.8초 이상 일찍)는 앵커가 손에 닿지 않아 **걸 수 없고 떨어져 복귀**한다 — 점프 실수와 같은 종류의 벌이고, 후보 표시(이미 있던 걸 수 있을 때의 강조)가 끝내 안 켜진다. 공중 가속 전에는 경계가 12/14 m 사이였다.
  - 대가: 걸 수 있는 구간이 앵커 앞 약 6 m(s 316~322)로 좁아졌다. 봇의 평소 걸기는 줄 11.5 → 8.6 m.
  - 버린 대안: 긴 줄 빨리 감기(강화 그래플 보상과 겹치고 일찍 걸기의 대가가 사라짐), 놓는 조건 변경(바닥에서 놓이지 않을 뿐 틈 안으로 내려가는 것은 남음), 사거리·앙각(위 측정).
- **공중 가속 (`RunnerMotor`)**: 공중(그래플 아님)에서 ↑ 를 누르면 땅과 같은 가속(`RunAcceleration`, 부스터 유지 중이면 `BoostAcceleration`)으로 목표까지만 빨라진다. 목표는 땅처럼 **아래 노면의 경사**를 반영한다 — 오르막에서 뛰어 평지 속도로 빨라지던 꼼수(2026-10-19 "이륙 속력 유지" 의 이유)는 그대로 막힌다. ↑ 를 떼면 공중에서는 오르지도 줄지도 않는다. 목표보다 빠르면 `airDeceleration`(옛 `airAcceleration`, `[FormerlySerializedAs]` 로 세 씬의 0 을 그대로 읽는다 — 씬 수정 없음)로 줄어든다 — 0 이라 버니합(같은 날 "버니합")은 그대로.
- **테스트**:
  - 새로: `ChainRushCircuitTests.Grapple_JumpedBeforeTheGap_HooksAboveTheRoadOrFromTooEarlyCannotHookAndFalls` — 고치기 전 실패 `jump 12.0 m before the gap: … the runner hung 1.00 m over the track, its feet below the road. Expected: greater than or equal to 1.04999995f But was: 1.00120199f`. `ChainRushSlopeTests.Jump_FromLowSpeedWithTheThrottle_SpeedsUpInTheAirUpToTheRunSpeed` — 고치기 전 `takeoff 0.600 m/s, after 10 air ticks 0.600 m/s (expected 6.600)`, 뒤 `6.600`. `Jump_WithTheThrottleLetGo_KeepsItsSpeedInTheAir`, `Jump_AboveTheRunSpeed_SlowsOnlyByAirDeceleration(0, 5)` — 고치기 전 `RunnerMotor.airDeceleration is missing.`
  - 사양 변경으로 고침(사용자 "ㄱㄱ"): `Jump_OffAnUphill_TheAirSpeedMovesByAirAcceleration` → `Jump_OffAnUphill_TheAirKeepsTheUphillRunTarget`(14.000 → 14.000 m/s). 걸 수 있는 거리가 20 → 약 9 m 가 되어, 정지 상태에서 틈 4 m 앞(작업대 z 44)에서 뛰고 0.25초 뒤 걸던 테스트 5개의 배치만 바꿨다 — 틈 1 m 앞(z 47)에서 ↑ 를 누른 채 뛰고 후보가 뜨면 건다(서킷의 팔 자세 테스트는 틈 4.5 m 앞에서 20 m/s): `ChainRushTests.Grapple_JumpAtGap_…`·`Grapple_SecondPressInTheSameAir_…`·`Input_ArrowKeys_…`(Device), `ChainRushDriftTests.Grapple_ChainActionWhileAttached_…`, `ChainRushPresentationTests.Animation_JumpAndGrapple_…`. 단언 값은 그대로. 세 개는 키보드 입력원이라 ↑ 가 없어 제자리에서 뛰었다(예전엔 사거리 20 m 라 그래도 걸렸다).
  - `ChainRushProbeTests` 의 일찍 걸기 측정은 위 진짜 테스트로 옮기고 지웠다(측정 3개 + 그래플 1개 남음).
- **검증** (에디터 6000.3.25f1, `fix/swing-boost-grapple` 커밋 전, UTC):
  - 컴파일: 리로드마다 `error CS` 없음(Editor.log), Console Error 0건.
  - 게이트 1회차 (`Run PlayMode Tests`): `testcasecount="164" result="Failed(Child)" passed="153" failed="4" skipped="7"` (14:14:40Z~14:21:28Z) — 위 배치 테스트 4개 `Expected: True`(`TryAttach`).
  - 게이트 2회차 (배치 고친 뒤): `testcasecount="164" result="Passed" passed="157" failed="0" skipped="7"` (14:27:01Z~14:33:50Z). skipped 7 = `[Explicit]` 측정 4 + 네트워크 레이스 3. `ClientEnd_WhenTheServerMissed…` 는 결과에 없음(제외 확인), 다른 `ClientEnd_` 5개는 돌았다.
  - 서킷 지문 새 기준 `0x18F31E7A2B4F79B3` (`ChainRush replay circuit: 1700 ticks identical`) — 이전 `0xE3744A27006C0BB2`. 그래플·공중 가속 사양 변경.
  - EditMode (`!Enemy`·`!Endless`): `testcasecount="235" result="Passed" passed="235" failed="0"` (14:34:15Z~14:34:18Z).
  - Device 1회차 `testcasecount="3" result="Failed(Child)" passed="2" failed="1"` (14:34:39Z~14:34:51Z) — `Input_ArrowKeys_…` `ChainRushTests.cs:200` `Expected: True`(`grapple.IsAttached`), 같은 원인. 배치 고친 뒤 2회차 `testcasecount="3" result="Passed" passed="3" failed="0"` (14:35:46Z~14:36:02Z). 이 수정은 Device 테스트 줄만이라 게이트 2회차 뒤에 했다.
  - `tools/select-tests.ps1` 실행: 기본 범위는 "전체"(씬·코어 시뮬), `-Range 75df74d^..75df74d` 출력의 필터에 `"!NetEndReplay"` 가 들어간 것을 확인.
- **남은 것**: 사용자 플레이 확인(걸 수 있는 구간이 좁아진 체감). 스윙 순간이동·코너 중간 미걸림은 NEXT_REQUESTS.

## 2026-10-19 · 스윙 중 부스터가 스윙 속도를 올린다(C), 그래플 낙하·스윙 순간이동은 측정만

- **보고 (사용자, 2026-10-19 플레이)**: "1. 스윙 중에 부스터를 사용하면 제대로 가속 적용이 안되는 경우가 있다. 2. 점프 그래플링 … 특정지점 까지 가면 자동으로 튀어오르게 되는 것 같은데 중간에 가속이 되어버리면 위험하게 떨어질뻔한 상황이 연출된다. 3. 코너에 진입할때 스윙들어가면 현재 좌표가 아닌 줄 길이? 로 순간이동 되는 이슈가 있다." 1 의 선택지(A 스윙을 놓고 부스터 / B 놓은 뒤 시작 / C 스윙 속도를 올림)에 "C를 원한다", 진행에 "3은 기억안난다 일단 해봐라 너무 빠르다싶으면 스윙 속도 관련한 로직을 바꿔야겟다".
- **1 원인**: 스윙 중 Ctrl 은 슬롯을 쓰고 부스터 타이머(당김 0.4초 + 유지 2.6초)를 시작했지만, 스윙 중에는 속도가 "걸 때의 속도 × 줄 비율"로만 정해지고(`SwingStep`) 달리기 가속은 0 이라 부스터가 아무 일도 안 했다. 타이머는 그동안 줄어, 놓을 때까지 남은 만큼만 적용됐다(3초 넘게 잡으면 통째로 사라짐). **고치기 전 재현**: `ChainRushCornerSwingTests.Swing_BoosterWhileSwinging_SpeedsTheSwingUp` 실패 — "hook speed 20.00 -> 20.00 m/s after 0.5 s" (`Expected: greater than 22.0f But was: 20.0f`).
- **1 결정 (C)**: 스윙 중 부스터가 도는 동안 걸 때의 속도(`RacerState.SwingHookSpeed`, 줄 비율이 곱해지는 값)를 부스터 목표(당김 28 → 유지 26, 러너 배율 적용)로 땅의 부스터와 같은 힘(`BoostAcceleration`)으로 올린다. 목표보다 이미 빠르면 깎지 않는다. 스윙 중에는 부스터 당김이 방향을 돌리지 않는다(줄이 방향을 정한다). 고친 뒤 같은 테스트 "20.00 -> 23.16 m/s after 0.5 s". 부스터 28 에 줄을 끝까지 감으면 최대 42 m/s(28 × 1.5) — 너무 빠르면 스윙 속도 규칙을 다시 본다(사용자).
- **2 측정** (`ChainRushProbeTests`, `[Explicit]`, 서킷, 시험용 봇): 그래플은 앵커를 0.5 m 지나면 저절로 놓이고 위로 11.5 m/s 를 받는다. 봇 기준 네 경우 모두 가장 낮은 곳이 노면 위 2.76 m, 놓는 높이 3.59~4.11 m — 위험한 낙하는 재현되지 않았다. 평소: 줄 11.47 m, 19.48 m/s 로 걸고 20.00 m/s 로 놓음. 40 m 전 부스터: 줄 10.63 m, 24.65 m/s 로 걸고 22.95 m/s 로 놓음. 15 m 전 부스터: 10.85 m, 22.56 → 20.66. 강화 그래플: 놓을 때 26.00 m/s. 봇은 틈 4.5 m 앞에서 뛰고 노면 위 2.6 m 를 넘으면 건다 — 사람이 더 멀리(줄 최대 20 m)·낮게 걸면 진자의 바닥이 앵커(노면 위 10 m) 아래로 더 내려가 그 바닥에서 놓일 수 있다는 계산은 남는다(재현 안 함).
- **3 측정** (같은 클래스, 한 바퀴, 봉이 닿는 순간 스윙 0.6초): 8번 걸었고 거는 틱의 줄 길이 = 그 순간 봉까지 거리(오차 0.00 m), 틱 이동량은 속도 범위 안(속도로 설명 안 되는 이동 0.00 m), 카메라와 러너 사이 거리의 프레임 변화 최대 0.39 m(2배속). **위치 순간이동은 재현되지 않았다.** 대신 보인 것: 봉에 닿자마자 걸면 줄이 27.8~28.0 m(코너 반지름 22 + 손 닿는 거리 6)로 걸려 원이 코너 노면의 바깥 가장자리(22 + 6)를 돌고, 거는 틱에 속도·방향이 원의 접선으로 한 번에 바뀐다. 사용자에게 이것이 본 현상인지 묻는다.
- **3 추가 측정** — 사용자 "인코스쪽에있었는데 shift 거니까 외곽으로 순간이동되었다": ① 안쪽 4 m 라인, 코너 안에서 걸기 4번 — 거는 틱 전후 D 그대로(+3.87 → +3.87 → 5틱 뒤 +3.87), 줄 = 거리, 속도 밖 이동 0.00 m. ② 첫 코너(s 140.5~175.0, 봉은 오른쪽), 안쪽 라인, 거는 시점 3 × 부스터 2 × 조향 3 = 18가지(사용자 "ㄱㄱ"): 걸린 12가지 모두 줄 = 거리, 속도 밖 이동 0.00 m — **순간이동 재현 안 됨**. 조향을 봉 반대쪽으로 누르면 10틱(0.2초)에 바깥으로 1.3 m(줄이 초당 6 m 풀림), 봉 쪽이면 안쪽으로 1.2 m. 부스터 중에는 안쪽 라인까지 못 들어가 D +2.43 에서 걸림. **"코너 중간에 걸기" 6가지는 한 번도 걸리지 않았다** — 원인 미확인. 추정: 최소 회전 반지름 25 m 가 안쪽 라인(봉에서 약 18 m)보다 커서 러너 방향이 커브를 못 따라가고, 봉이 앞 방향에서 95° 를 넘어간다.
- **NetReconcileTests 1건** (`ClientEnd_WhenTheServerMissedTheInputOfItsLastTick_…`): 이 변경으로 실패. 테스트가 "스윙은 부스터를 무시한다"는 전제로 입력 경우를 찾는데 그 전제가 바뀌었다. 사용자 승인("1 ㅇㅇ")으로 탐색을 두 번 고쳤으나(줄 미세조정을 그 런의 스윙 N번째 틱에 / 미세조정 조향 0.15) 세 번째도 "No case fits"(659가지, 클라 결승선 앞·서버 뒤 경우 0) — §4-5 에 따라 멈췄다. 남은 결정은 `docs/NEXT_REQUESTS.md` "fix/swing-boost-grapple 남은 결정".
- **Presentation 테스트 1건** (`Presentation_RunPauseRestart_…`): 게이트에서 "걸음 위상 0" 실패. 키보드 입력원으로 출발해 ↑ 를 누르지 않아, 첫 프레임의 입력 읽기 전에 물리 틱이 돌았는지에 따라 움직였다 말았다 했다(`InputLatch.Clear` 는 가속 중으로 둔다). 단독 실행은 통과. 사용자 승인("2 ㅇㅇ")으로 `ChainRushTests` 와 같은 `FullThrottleInputSource` 를 쓰게 고쳤고 통과.
- **검증** (에디터 6000.3.25f1, `fix/swing-boost-grapple` 커밋 전, 진행 중): 컴파일 성공·Console Error 0건. `ChainRushCornerSwingTests` 10/10(고친 뒤). EditMode `testcasecount="235" result="Passed"` (12:33:50Z). 게이트 1회(고치기 전 테스트들) `testcasecount="159" passed="152" failed="2" skipped="5"` (12:34:11Z~12:40:28Z) — 실패 2건은 위 NetReconcile·Presentation, 서킷 지문 `0xE3744A27006C0BB2` 그대로. 게이트 재실행은 NetReconcile 결정 뒤.

## 2026-10-19 · 점프로 부스터 속도가 이어지는 것(버니합)은 그대로 둔다 — 기록만

- **결정 (사용자, 2026-10-19 플레이 확인 "현재 점프하면 그 속도가 고정이 되는 듯한데 부스터 끝나는 시간에 점프를 하면 조금 더 유지가 된다. 이는 버니합같은 버그 겸 기술 요소로 남길까?", 선택지 A 한계 두기·B 그대로·C 막기 중 "3은 그대로 두고 기록해놓자")**: 바꾸지 않는다.
- **원인 (코드 읽기, 실측 안 함)**: 서킷 씬 `airAcceleration` 0(2026-10-19 "이륙 속력 유지" 결정) 이라 공중에서는 수평 속력이 오르지도 내리지도 않는다(`RunnerMotor.Step` 의 가속 선택, 공중이면 `airAcceleration`). 땅에서 목표보다 빠르면 `overspeedFalloff` 8 m/s² 로 줄어든다. 그래서 부스터 끝 무렵(26 m/s 근처) 점프하면 그 속력을 착지까지(점프 체공 약 1초 = 2 × 11.5 / 22) 가져가고, 착지 직후 다시 뛰면 땅에 닿은 틱마다 0.16 m/s 정도만 잃으므로 연속 점프로 오래 끌 수 있을 것으로 계산된다. 실제로 몇 초 이어지는지는 재지 않았다.
- **나중에 다시 볼 때의 후보**: A 공중에서도 목표 초과분을 천천히(예: `swingCarryFalloff` 3 m/s²) 줄인다 — 점프 한 번의 이득은 남고 연속 점프로 끝없이는 못 끈다. C 공중도 땅처럼 8 m/s² — 기술 요소가 없어진다. 어느 쪽이든 시뮬 변경이라 서킷 지문이 바뀐다.

## 2026-10-19 · 로비: 트랙 선택에 최고 기록, 러너 이름 잘림 고침

- **결정 (사용자, 2026-10-19 플레이 확인 "씬 처음부터 들어가서 정상 작동 확인했다", "Runner 고를때 우측에 스탯과 이름이 뜨는데 그 이름 하단이 잘려보인다", "싱글플레이에서 맵을 고를때 최고기록을 남겼으면 좋겠다. 아직 기록이 없다면 존재하지않다고 표기", 추천안(트랙 × 속도 단계별 1개 + 세운 러너, `PlayerPrefs`)에 "1ㄱㄱ 2ㄱㄱ")**:
  - **최고 기록** (`Flow/BestRecords`): 트랙(정의 에셋 이름, 예 `Circuit_Stadium`) × 속도 단계마다 3랩 총 틱 수(`LapCounter.TotalTicks`)와 세운 러너 이름을 `PlayerPrefs` 에 둔다(키 앞머리 `ChainRush.Best.`). 더 빠를 때만 바꾸고 같으면 옛 기록을 둔다. 로비에서 시작한 레이스(`RaceAssembler`)가 완주할 때마다 낸다(R 로 다시 달려 완주해도 다시 낸다). 씬을 바로 연 런은 내지 않는다. 테스트는 자기 키 앞머리를 쓰고 지운다.
  - **로비 트랙 페이지**: 줄마다 오른쪽에 `최고 기록  01:19.4  ·  BETA` 또는 `기록 없음`(고른 속도 단계 기준, 지금은 빠름). 트랙 페이지를 열 때 다시 읽는다. 시간 형식은 HUD 와 같다(`ChainRushHud.FormatTime` 을 `internal` 로).
  - **러너 이름 잘림**: 능력치 패널의 이름 칸 높이가 32 px 굵은 글꼴보다 낮았다(30) — 44 로 늘리고 막대를 내렸다.
  - 결과 화면에 "새 기록" 표시는 하지 않았다(`RaceAssembler.LastFinishWasBest` 만 있다).
- **이유**: 기록·고스트(M4)의 저장 키는 트랙과 러너가 정해진 뒤 정하기로 했는데(DESIGN §5-1), 트랙 고를 때 보이는 기록 하나는 지금도 정할 수 있다. 러너별 기록과 고스트는 M4 에서 같은 저장소에 더한다.
- **버린 대안**: 트랙 × 러너별 — 트랙 화면에 기록 5개가 나온다(M4 로).
- **검증** (에디터 6000.3.25f1, `feature/lobby-best-record` 커밋 전): 컴파일 성공·Console Error 0건. EditMode `testcasecount="235" result="Passed" passed="235" failed="0"` (12:14:11Z~12:14:14Z, 새 `BestRecordsTests` 5건 포함). `tools/select-tests.ps1`(커밋 전 기본 범위) → 클래스 8개 + 지문 1건(씬·코어 시뮬 변경 없음): `testcasecount="83" result="Passed" passed="83" failed="0" skipped="0"` (12:14:40Z~12:17:04Z, 143.9초 — `ChainRushFlowTests` 11/11 에 새 2건: 트랙 페이지 표시, 봇 3랩 완주가 기록으로 남음), 서킷 지문 `0xE3744A27006C0BB2` 그대로. 테스트 뒤 플레이어 키(`ChainRush.Best.Circuit_Stadium.Fast`)는 비어 있고 테스트 키는 지워져 있음을 확인했다. **검증 안 됨**: 로비 화면 모양(이름 칸·기록 줄), 사용자 플레이.

## 2026-10-19 · M2-b 러너 프로필: ALPHA·BETA·THETA·DELTA·OMEGA, 씬 값에 곱하는 배율

- **결정 (사용자, 2026-10-19 "이름 알파 베타 세타 델타 오메가로 5명 스탯 조금씩 다르게", "속도 배율 밸런스는 1.0(빠름으로 해놓은것) 만 되어있으니 나머지 보통과 매우빠름은 후순위", 추천 배율표에 "네 추천안대로 일단 그렇게 해보자")**:
  - **`RunnerProfile`**(SO, `Data/RunnerProfile_*.asset`): 이름 + 배율 4개. 씬의 `RunnerMotor` 값에 곱한다 — 최고 속도는 `runSpeed`·부스터(`slingTopSpeed` 28·`slingCarrySpeed` 26·`empoweredReleaseSpeed` 26), 가속은 `runAcceleration`, 게이지 충전은 `swingMetersPerSlot` 를 나눈다. 무게는 저장만(몸싸움 M7 C2 까지 효과 없음). 그래플 최저 16·놓을 때 13 같은 고정값과 점프·중력·조향은 그대로.
  - **값** (최고 속도 / 가속 / 게이지 충전 / 무게): ALPHA 1/1/1/1(지금 값), BETA 0.97/1.20/1.00/0.90, THETA 1.03/0.85/1.00/1.10, DELTA 0.98/1.00/1.20/0.95, OMEGA 1.02/1.05/0.85/1.20.
  - **적용**: `RunnerMotor.Configure(RunnerProfile)` — 런 밖에서만(달리는 중·일시정지면 예외), 잘못된 프로필도 예외. 로비 → `RaceSetup.Runner`(이름 → `RunnerProfile`) → `RaceAssembler` 가 출발 전에 부른다. 씬을 바로 연 경우는 프로필 없이 씬 값 그대로.
  - 로비 러너 페이지에 고른 러너의 능력치 막대 4개(0.7~1.3 범위, 배율 숫자).
  - 속도 단계 보통·매우빠름은 계속 잠금(후순위).
- **이유**: 배율이면 ALPHA(모두 1)가 곱해도 비트 단위로 같은 값이라 세 씬의 지문이 그대로이고, 값의 원본이 씬 하나로 남는다. 절대값 프로필은 씬마다 다른 물리값(DECISIONS "다른 씬 서킷 물리값")과 원본이 둘이 된다.
- **버린 대안**: 부스터 속도를 최고 속도와 따로 두기 — 부스터가 달리기보다 빨라야 한다는 규칙(`RunnerMotor.OnValidate`)이 러너마다 깨질 수 있다.
- **하지 않은 것**: 네트워크에 프로필 싣기(M7 — 멀티는 잠금이라 지금은 기본값만 돈다), 입력 기록 파일에 프로필 기록(M4 고스트 때), 러너별 모형·색(M6).
- **씬·에셋 작업**: 프로필 에셋 5개 생성과 씬 편집은 승인받은 `Unity_RunCommand` 로 했다. 첫 실행에서 로비 씬을 연 순간 방금 만든 프로필 객체가 내려가 로비 `runners` 가 빈 연결 5개로 저장됐다(`ChainRushCircuitSceneBuilder` 의 주석과 같은 현상) — 씬을 연 뒤 디스크에서 다시 읽어 넣는 두 번째 실행(승인)으로 고쳤고, 저장된 GUID 5개가 에셋 `.meta` 와 같음을 확인했다.
- **검증** (에디터 6000.3.25f1, `feature/m2-runner-profile` 커밋 전):
  - 컴파일: 성공, 새 타입 로드 확인, Console Error 0건(프로필·씬 편집 전). 씬 편집 중 `LobbyMenu.OnValidate` 의 "runner N is missing" 5줄씩 2번은 위 빈 연결과, 두 번째 실행에서 옛 빈 목록이 열릴 때 난 것이다.
  - `ChainRushProfileTests` 6건: 첫 실행에서 `Configure_NullInvalidOrDuringARun_Throws` 1건 실패 — 테스트가 일부러 넣은 잘못된 값에 `RunnerProfile.OnValidate` 가 `LogError` 를 내고(의도한 동작) 테스트가 그 로그를 예상하지 않았다. 테스트에 `LogAssert.Expect` 를 넣고 다시: `testcasecount="6" result="Passed" passed="6" failed="0"` (11:43:11Z~11:43:39Z). 출발 75틱 거리 BETA 10.94 m > ALPHA 9.12 m > THETA 7.75 m, 700틱 해시 BETA `0xA9251CA0D8EBD1BE`·THETA `0x6761C9D9B6C0C3F0`(각각 두 번 같음, ALPHA 와 다름), ALPHA 는 프로필 없는 씬 값과 비트 단위로 같다.
  - `ChainRushFlowTests` 9/9 (11:42:02Z~, 같은 실행의 Profile 실패 1건 외).
  - EditMode (`!Enemy`·`!Endless`): `testcasecount="230" result="Passed" passed="230" failed="0"` (11:43:45Z~11:43:46Z).
  - PlayMode 게이트 `Run PlayMode Tests` 1회: `testcasecount="154" result="Passed" passed="151" failed="0" skipped="3"` (11:43:59Z~11:49:52Z, 352.8초). 건너뜀 3 = `[Explicit]` `NetworkRaceTests`. 서킷 지문 `0xE3744A27006C0BB2` 그대로.
  - `Run Device Input Tests`: `testcasecount="3" result="Passed" passed="3" failed="0"` (11:50:10Z~11:50:26Z, 시작 때 `isApplicationActive=False` 였으나 통과).
  - **검증 안 됨**: 로비 러너 페이지·능력치 막대의 화면 모양, 사용자 플레이(러너마다 차이가 느껴지는지), 3랩 랩타임 차이(재지 않았다).

## 2026-10-19 · M2-a2 빌드 씬 목록 (`feature/build-scene-list`, `22dd4aa` → `dev` `0432115`)

- **결정 (사용자, 2026-10-19 "씬 목록 정리했다", "커밋 병합 ㄱㄱ")**: 사용자가 에디터 Build Profiles 에서 `ChainRushTitle`(0번)·`ChainRushLobby`·`ChainRushCircuit`·`ChainRushTrackStadium` 을 넣고 템플릿 `SampleScene` 을 껐다. `ProjectSettings/EditorBuildSettings.asset` 만 바뀐 브랜치(§9-2)이고, GUID 4개가 씬 `.meta` 와 같다. 기록은 그 브랜치에 파일 하나만 두려고 이 항목(M2-b 브랜치)에 남긴다.
- **검증**: `select-tests -Range dev...HEAD` 1개 파일 → "전체". `ChainRushFlowTests` 9/9(`Loop_ByName_…` 첫 실행 통과, 11:24:21Z), 게이트 `testcasecount="148" result="Passed" passed="145" failed="0" skipped="3"` (11:24:49Z~11:30:15Z, 326.5초, 지문 `0xE3744A27006C0BB2`), EditMode 230/230 (11:33:06Z). 게이트는 디스크 저장 전, 에디터에 반영된 목록으로 돌았다(끝난 뒤 같은 내용이 저장돼 있었다).

## 2026-10-19 · M2 씬 구성: 타이틀 → 로비 → 달리는 씬 + 서킷 장식 씬(Additive), 시뮬은 데이터로 (M2-a1)

- **결정 (사용자, 2026-10-19 "addictive 씬 구성으로 하자. 타이틀 입장씬과 로비씬이 따로 있고 실제 달리는 씬을 둔 다음 서킷이나 조건(싱글or멀티, 속도(카트라이더의 보통,빠름,매우빠름 같은)) 불러오며 조립하는 식으로", 트랙 쪽 구성 "A", "M2-a1 ㄱㄱ", 기준 속도 단계 "빠름")**:
  - **씬**: `ChainRushTitle`(Enter → 로비) → `ChainRushLobby`(싱글 / 멀티 잠금 / 설정 → 트랙 → 속도 단계 → 러너) → 달리는 씬 `ChainRushCircuit`(Single 로드) + 트랙의 장식 씬 `ChainRushTrackStadium`(Additive) → 결과에서 ESC·"로비로" 버튼 → 로비. 결과 화면의 R 다시 달리기는 그대로.
  - **A안 — 시뮬은 데이터, 장식 씬은 보이는 것만**: 노면·충돌체·앵커·봉은 달리는 씬의 `CircuitRace` 가 `TrackDefinition` 으로 만든다(전과 같다). 로비가 고른 정의는 `RaceAssembler`(실행 순서 -100)가 `CircuitRace.UseDefinition` 으로 회로를 만들기 전에 바꿔 끼운다(만든 뒤에는 `InvalidOperationException`). 장식 씬은 충돌체가 없어야 한다(만들 때 0개 확인). 그래서 `RaceWorld.FromScene`(게임·러너·서킷이 한 씬 루트)과 서버의 `IsolatedLoad` 복사본, 서킷 씬을 통째로 여는 테스트 약 10개 파일이 그대로다.
  - **조건 전달**: `RaceSetup`(readonly struct: 정의·장식 씬 이름·`RaceMode`·`SpeedClass`·러너 이름)을 로비가 `RaceLaunch`(static, 요청 1개, 받으면 비움, 두 번 요청하면 예외, Play 진입 때 비움)에 두고 달리는 씬을 연다. 요청 없이 연 달리는 씬(테스트·에디터)은 저장된 대로 돌고 `Debug.Log` 한 줄을 남긴다.
  - **잠금**: 멀티(M7), 속도 단계 보통·매우빠름(M2-b). 로비는 보이되 고르면 안내만 하고 머문다. `RaceAssembler` 는 싱글·빠름이 아닌 요청을 `LogError` 하고 게임을 끈다. **지금 서킷 값 = 빠름**(사용자). 러너는 하나(`ARMORED RUNNER`, 이름만 — M2-b 에서 프로필).
  - **설정**: 키 안내만(저장 없음, M2-c).
  - **서킷 씬 정리**: 꺼진 루트 `Skyline Course`(옛 직선 코스 Sector 01~09·타워·결승 게이트, 충돌체 포함) 를 지웠다. 지우기 전에 그 안을 가리키는 바깥 참조가 없음을 확인했다. `ChainRushGame.targets` 는 이미 0개였다(그 안의 표적은 판정에 들어간 적이 없다).
  - UI 는 기존처럼 OnGUI(`ChainRushHud` 와 같은 1600×900 비율·글꼴). 새 네임스페이스 `ProtoHarness.ChainRush.Flow`.
- **이유**: 사용자가 씬을 나눠 조립하는 구조를 골랐고, 트랙의 시뮬 부분까지 다른 씬으로 빼면(B안) Additive 씬마다 물리 씬이 따로라 격리 월드·테스트를 고쳐야 하고 멀티가 보류된 지금 네트워크 코드를 건드리게 된다. M3 의 새 트랙은 `TrackDefinition` 1개 + 장식 씬 1개로 늘어난다.
- **버린 대안**: 씬 하나에 메뉴를 겹쳐 그리기(첫 제안) — 사용자가 Additive 구성을 골랐다. B안(서킷 씬에 `CircuitRace`·노면까지) — 위 이유. 정적 전달 대신 `DontDestroyOnLoad` 세션 오브젝트 — 씬 사이 참조를 직렬화할 수 없어 결국 찾아야 하고(`Find*` 금지), 요청 하나를 넘기는 데는 static 이 짧다.
- **빌드 씬 목록**: 지금 목록에는 템플릿 `SampleScene` 하나뿐이라 이름으로 씬을 부르는 흐름(타이틀 → 로비 → 레이스, 장식 씬)은 Play 에서 아직 안 된다. `ProjectSettings` 변경이라 따로 브랜치(M2-a2, §9-2)이고, `Unity_RunCommand` 로 바꾸지 않으므로(§0) 사용자가 에디터에서 넣는다. 그 전까지 `ChainRushFlowTests.Loop_ByName_…` 은 무시(Ignored)로 보고된다.
- **검증** (에디터 6000.3.25f1, `feature/m2-scene-flow` 커밋 전):
  - 컴파일: `Unity_RunCommand` 컴파일 성공, 새 타입 7개 로드 확인, Console Error 0건(테스트 전·후).
  - `tools/select-tests.ps1`(커밋 전 기본 범위): 씬·`Flow.meta`(표에 없는 경로) 때문에 **"전체"**. 새 클래스 `ChainRushFlowTests` 를 표에 넣었다(`Flow/`, `Race/`, `ChainRushHud.cs` 줄). Long·Sweep 은 모두 무한 모드라 보류 동안 돌리지 않는다(§9-2).
  - `ChainRushFlowTests` 단독: `testcasecount="9" result="Skipped:Ignored" passed="8" failed="0" skipped="1"` (10:34:45Z~10:34:49Z). 무시 1건은 `Loop_ByName_…`(빌드 씬 목록 없음, a2 뒤 실행).
  - EditMode (`!Enemy`·`!Endless`): `testcasecount="230" result="Passed" passed="230" failed="0" skipped="0"` (10:35:11Z~10:35:12Z).
  - PlayMode 게이트 `Run PlayMode Tests` 1회: `testcasecount="148" result="Skipped:Ignored" passed="144" failed="0" skipped="4"` (10:35:26Z~10:40:47Z, 320.7초). 건너뜀 4 = `[Explicit]` `NetworkRaceTests` 3건 + `Loop_ByName_…` 1건. 서킷 지문 `0xE3744A27006C0BB2` — v0.2 기준과 같다(옛 직선 코스 제거·조립기 추가가 시뮬을 바꾸지 않았다).
  - `Run Device Input Tests`: `testcasecount="3" result="Passed" passed="3" failed="0" skipped="0"` (10:41:10Z~10:41:26Z, `isApplicationActive=True`).
  - **검증 안 됨**: 타이틀·로비·로딩·결과 버튼의 화면 모양(OnGUI, 캡처하지 않았다), 이름으로 씬을 부르는 흐름 전체(a2 뒤), 사용자 플레이.

## 2026-10-19 · main v0.2: M1 "한 판이 끝까지 돈다" — `dev` 를 `main` 에 병합하고 태그 `v0.2`

- **결정 (사용자, 2026-10-19)**: 완료 조건 플레이 확인 "3랩 완주했다, 막히는 곳 없음". 이어서 추천(마일스톤 조건을 돌리고 통과하면 `dev → main` + 태그 `v0.2`)에 "좋다 네 추천대로 진행하겠다". 이 문서 브랜치(로드맵 재배치 + 이 기록, `docs/` 와 `CLAUDE.md` 만)를 `dev` 에 병합한 뒤 `dev` 를 `main` 에 `--no-ff` 로 병합하고 주석 태그 `v0.2` 를 단다.
- **v0.2 에 든 것** (`main` v0.1 뒤): 서킷 낙하 복귀(R2), 코너 봉 스윙 시뮬·표현·v2(R4·R5·R4b), 사각 서킷 R22(R4c), M1-T 튜닝(20 m/s·조향 2차·그래플 20 m 한 번·점프 중 스윙 없음·↑ 가속·부스터 속도 비례 가속 3초·부스터 게이지 화면), 다른 씬 서킷 물리값, 네모 상대 모형(R9)·작업대 정리(R3), 테스트 3단·`Long` 묶음·틱 대기(R1)·적·무한 모드 테스트 보류. 각각 이 문서의 같은 이름 항목.
- **마일스톤 조건 (`CLAUDE.md` §9-2 ③)** — 코드는 `dev` `c96b291` 그대로(이 문서 브랜치는 `Assets/`·`Packages/`·`ProjectSettings/` 에 닿지 않는다), 에디터 6000.3.19f1(이 머신의 버전 차이, `ProjectSettings/`·`Packages/packages-lock.json`·`ProtoHarness.slnx` 는 넣지 않는다):
  - 컴파일: `Unity_RunCommand` 컴파일 성공, Console Error 0건 (07:16Z 직전).
  - EditMode (`!Enemy`·`!Endless`): `testcasecount="230" result="Passed" passed="230" failed="0" skipped="0"` (07:16:34Z~07:16:37Z).
  - `tools/select-tests.ps1 -Range main...dev`: 72개 파일, **"전체"** (코어 시뮬·씬·표에 없는 경로) → 메뉴 `Run PlayMode Tests`. Long 은 모두 `Endless` 라 보류 동안 돌리지 않는다(§9-2), Sweep 도 같다.
  - PlayMode 게이트 1회차: `testcasecount="139" result="Passed" passed="136" failed="0" skipped="3"` (07:17:00Z~07:22:34Z, 334.0초).
  - PlayMode 게이트 2회차 (같은 코드): `testcasecount="139" result="Passed" passed="136" failed="0" skipped="3"` (07:22:59Z~07:28:33Z, 334.1초). 두 회차 모두 skipped 3 은 `[Explicit]` `NetworkRaceTests` 3건, 서킷 지문 `0xE3744A27006C0BB2`(HANDOFF·DECISIONS "조향 2차" 의 기준과 같다).
  - `Run Device Input Tests`: `testcasecount="3" result="Passed" passed="3" failed="0"` (07:32:15Z~07:32:31Z, `isApplicationActive=True` — 사용자가 에디터를 앞에 두고 Game 뷰를 클릭).
  - 사용자 플레이 확인: 위 "3랩 완주했다, 막히는 곳 없음".
  - 하지 않은 것: 네트워크 레이스(M7 부터), `Run Long Tests`·`Run Course Sweep`(무한 모드 보류).
- **v0.2 에 없는 것**: 타이틀·메뉴·러너 선택(M2), 트랙 2개 이상(M3), 기록·고스트(M4), 서킷 씬의 꺼진 `Sector 01`~`08`·표적 15개 정리(M2 로 넘김).

## 2026-10-19 · 로드맵 재배치: 게임 뼈대(M2)·트랙(M3)을 기록·고스트 앞으로, 폴리시(M6) 추가, 멀티는 M7

- **결정 (사용자, 2026-10-19)**: M1 이 끝난 뒤 연출·아트를 언제 할지 묻자 추천(필요한 마일스톤에 붙이고 전반은 AI 상대 뒤 한 번)에 "좋다 네 추천대로 진행하겠다. 다만 서킷 종류들도 추가하고 싶다". 순서는 "1. 대로 하는데(트랙을 기록·고스트보다 먼저) 게임 전체 구조도 마일스톤에 넣어보자(타이틀화면, 멀티플레이(진입불가 락 걸어두고), 싱글플레이, 설정 등, 러너(개체별로 최대 속도, 가속도, 무게-몸싸움용?, 게이지충전량) 설정이 있으면 좋겠다)", 서킷은 테크니컬·고저차·고속·헤어핀 "각각과 현재 기존 서킷 특성도 포함한 종합 맵도 몇개 만들면 좋을거 같다". 재배치 표에 "ㄱㄱ".
  - **새 순서** (`docs/DESIGN.md` §5-1): M1 v0.2 한 판(끝) → **M2 v0.3 게임의 뼈대**(타이틀, 메인 메뉴 — 싱글 / 멀티 잠금 / 설정, 트랙·러너 선택, 러너 프로필) → **M3 v0.4 고를 거리**(테크니컬·고저차·고속 → T5 봉 모듈 뒤 헤어핀·그래플 → 종합 맵, 장식) → **M4 v0.5 다시 할 이유**(옛 M2: 기록·고스트를 트랙×러너별로, 복귀 연출, 조작 피드백 이펙트) → **M5 v0.6 같이 달리는 맛**(옛 M4: AI 상대, 순위, 상대 달리기 모션) → **M6 폴리시**(새로: 모형·모션, 봉 스윙 이펙트, 사운드, 환경 아트) → **M7 멀티 재개**(옛 M5 + 멀티 메뉴 잠금 해제, C2 에서 러너 무게).
  - **러너 프로필**: 최고 속도·가속·무게·게이지 충전량. 기본 프로필은 지금 서킷 값으로 둬 서킷 지문을 바꾸지 않는다. 무게는 몸싸움(C2)이 생길 때까지 효과가 없다. 형태(SO 등)는 M2 요청서에서 정한다.
  - `CLAUDE.md` §9-2 ③ 의 "네트워크 레이스는 M5(멀티 재개)부터" 를 M7 로 고쳤다(규칙 문서, 이 합의에 포함).
- **이유**: ① 트랙·러너 선택이 들어갈 자리(화면 흐름)가 먼저 있어야 새 트랙을 어떻게 불러올지 한 번에 정한다. ② 기록·고스트는 트랙과 러너가 정해져야 저장 키가 정해진다. ③ 러너 능력치는 시뮬 입력이라 리플레이·네트워크가 알아야 해서, 결정성 테스트와 같이 일찍 넣는다. ④ 연출·아트는 표현 계층이 시뮬 틱 밖이라(2026-10-04 "표현 분리 (P1-3b)", 2026-10-14 "코너 스윙 S1 연출" 의 지문 그대로) 늦게 넣어도 시뮬과 테스트가 깨지지 않고, 트랙(M3)이 정해지기 전에 환경 아트를 하면 다시 한다(노면은 중심선에서 절차로 뽑는다, `COURSE.md` 11절 7).
- **버린 대안**: 옛 순서대로 기록·고스트(옛 M2) 먼저 — 사용자가 트랙을 먼저 고름. 아트만 모은 마일스톤을 바로 다음에 — 트랙·상대가 정해지기 전이라 다시 할 일이 생긴다. 서킷 하나를 더 만들어 v0.2 에 넣기 — M1 완료가 늦어진다.

## 2026-10-19 · 부스터: 속도 비례 가속(정지에서 30, 목표에 가까울수록 약하게)·3초, HUD 속력은 수평만

- **결정 (사용자, 2026-10-19 "속도가 아주 낮을때에 부스터 누르면 거의 바로 26으로 올라가버리는데 이는 내 의도사항이 아니다. 부스터를 썻을때 적당히 가속받으며 올라가야해. 또한 속도가 없는데도 2.4로 나온다 확인해줘", "부스터 가속을 조금 더 올려줘", "낮은 속도에서는 가속이 더 잘붙고 최대속도에 다가올수록 천천히 올라가도록", "부스터 3초로 하는건 어떤가", 각 단계 "ㅇㅇ"·"ㄱㄱ")**:
  - **부스터 가속**: 슬링샷은 당김(0.4초, 목표 28) → 유지(목표 26). 이전에는 당김 `slingPullAcceleration` 45 m/s² 일정(5 m/s 에서 0.8초에 26), 유지는 지상 가속 8. 거쳐 간 값: 씬 값 45 → 12 → 15(일정 가속, 둘 다 게이트 통과·플레이). 최종: `RunnerMotor.BoostAcceleration` — 당김 중, 그리고 땅에서 유지 중 목표보다 느리면 `slingPullAcceleration × (1 − 속력 / 목표)`, 목표보다 빠르면 `overspeedFalloff`. 씬 3개 `slingPullAcceleration` 30. 공중 유지는 그대로(공중 가속).
  - **부스터 3초**: 씬 3개 `slingCarryTime` 2 → 2.6(당김 0.4 + 유지 2.6). 2026-10-18 "M1-T 앞당김 1" 의 유지 2초를 바꾼다.
  - 계산값(0.02초 적분, 실측 안 함): 0 에서 1초 18.0·끝 25.2, 5 에서 끝 25.4, 20 에서 1초 24.4·끝 25.8. 목표에 점근해 26 에 딱 닿지는 않는다. 달리는 중(20)에 쓰면 이전(0.4초에 26)보다 늦게 오른다 — 사용자에게 알렸다.
  - **2.4**: HUD 는 `player.Speed`(속도 벡터 전체 크기)를 보였다. 땅에 서 있으면 `RunnerMotor.cs:170` 이 아래로 −2 m/s 를 넣고 중력 한 틱(22 × 0.02 = 0.44)이 더해져 저장되는 수직 속도가 −2.44 다 — 멈춰도 2.4(계산으로 확인, 정지 상태 값을 찍어 보지는 않았다). `RunnerMotor.HorizontalSpeed`(읽기 전용, 수평 크기)를 추가하고 HUD 만 그것으로. 카메라 시야각·소리·절차 코스의 `Speed` 는 그대로.
  - 씬 저장은 매번 `Unity_RunCommand` + `SerializedObject` + `SaveScene`(코드 전문 보이고 승인). HEAD 대비 최종 diff 는 씬마다 `slingPullAcceleration: 45 → 30`, `slingCarryTime: 2 → 2.6` 두 줄.
- **테스트 사양 변경**:
  - `ChainRushDriftTests.Sling_WithSlot_PullsWellPastRunSpeedThenSettles`: 최고 속력(> 달리기 + 5)을 재는 시간 0.5 → 3초(부스터 전체), 끝난 뒤 기다림 3 → 1초. 단언 값은 그대로.
  - `NetReconcileTests.SlingTicks` 120 → 150(부스터 길이 상수). 1회차 원문: `ClientEnd_WhenTheServerMissedTheInputOfItsLastTick_…` `No case fits: with the rope tweak on none of the swing ticks from the hook on tick 4050 to the release on tick 4126, … (recorded finish 4136)` — 시도 목록의 `P4018:ends<4138` 처럼 F = P + 120 으로 셌다.
- **검증** (`feature/tuning-boost-accel`, 커밋 전, 에디터 6000.3.19f1, UTC): `tools/select-tests.ps1` 판정 EditMode 전체·PlayMode 전체(씬·코어 시뮬), Long 은 무한 모드 보류로, Device 는 입력 장치 경로를 안 바꿔 안 돌림.
  - 값 12(일정): EditMode `testcasecount="230" result="Passed"` (06:13:36Z~06:13:40Z), PlayMode `testcasecount="139" result="Passed" passed="136" failed="0" skipped="3"` (06:14:03Z~06:19:49Z). 값 15(일정): PlayMode 같은 결과 (06:24:58Z~06:30:13Z).
  - 비례 30·3초: 컴파일 `*** Tundra build success (4.13 seconds)`. EditMode `testcasecount="230" result="Passed" passed="230" failed="0"` (06:44:01Z~06:44:05Z). `Run PlayMode Tests` 1회차 `testcasecount="139" result="Failed(Child)" passed="135" failed="1" skipped="3"` (06:44:26Z~06:50:05Z) — 위 NetReconcile 1건.
  - `SlingTicks` 150 뒤 컴파일 `*** Tundra build success (3.38 seconds)`, `NetReconcileTests` 만 `testcasecount="23" result="Passed" passed="23"` (06:51:27Z~06:52:22Z).
  - `Run PlayMode Tests` 2회차(최종 코드) `testcasecount="139" result="Passed" passed="136" failed="0" skipped="3"` (06:52:43Z~06:58:23Z, 340.1초). 서킷 지문 `0xE3744A27006C0BB2` 그대로(리플레이 봇은 슬링샷을 쓰지 않는다), 봇 랩 31.12·30.10·30.10 초.
- **사용자 플레이** (2026-10-19): "좋다".

## 2026-10-19 · 조향 2차: 최소 회전 반지름 25 m, 꺾을 때 감속 0.2 (+ 테스트 봇이 실제 회전 상한으로)

- **결정 (사용자, 2026-10-19 "꺾임이 아직 조금 세다. 꺾임을 조금 줄이고 최대로 꺾었을때 속도도 조금 감속시켜줘", "비례로가 맞고 일단 진행하자", 씬 저장 "ㄱㄱ", 봇 수정 "1"(A안), 창 넓히기 "해보아라")**:
  - 씬 3개(서킷·벤치·절차) Runner `minTurnRadius` 22 → 25(20 m/s 에서 최대 52 → 46°/s), `turnSpeedLoss` 0.1 → 0.2(회전율이 그 순간 상한에 닿으면 목표 20 → 16 m/s, 그 전에는 비례 — 사용자 확인 "비례로가 맞다"). `Unity_RunCommand` + `SerializedObject` + `SaveScene`(코드 전문 보이고 승인), 로그 `minTurnRadius [22] -> [25], turnSpeedLoss [0.1] -> [0.2]` 3줄. diff 는 씬마다 그 두 줄 + Unity 가 처음 기록한 `coastDeceleration: 6`(코드 기본값과 같다).
  - 감속(↑ 뗌) 6 m/s² 는 사용자 "감속은 괜찮다" — 그대로.
  - **`RunnerMotor.TurnLimit`** (읽기 전용 공개 속성 추가): 지금 속력에서의 지상 회전 상한, `Step` 과 같은 `TurnLimitAt` 을 쓴다(계산 순서는 그대로 — 지문 변화는 수치 변경 때문).
  - 서킷 R22 코너는 반지름 25 로는 방향키만으로 다 못 돈다(봉 스윙 설계, R4b).
- **테스트 사양 변경**:
  - `TrackFollower`(테스트 봇): 앞먹임을 상수 120°/s 대신 `runner.TurnLimit` 으로 나눈다. 1회차 실패 원문: `ChainRushCircuitTests.Race_BotRunsThreeLaps_…` `The bot touched a guard. Expected: less than 5.5999999f But was: 5.61933517f`, `ChainRushCurveTests.Curve_BotSteersR30_…` `The runner should leave the curve facing +x. Expected: 0.0f +/- 5.0f But was: -6.34463501f`. 봇이 회전 상한을 120 으로 알아 46°/s 에서 필요한 양의 38% 만 앞먹임했다. 버린 대안: 곡선 테스트만 R40 도로로(봇 모델이 틀린 채 남는다), 반지름을 24 로(테스트에 맞춰 조향감을 정하는 셈). 사용자 질문 "감속을 더 넣으면 넘길 수 있지 않을까" — 느려지면 상한(속력/반지름)도 줄어 비율이 더 나빠진다(16 m/s 31%, 14 m/s 27%) — 실험하지 않았다.
  - `NetReconcileTests` 4건(`:322,345,386,846`)의 입력 하나 빼기 창 700~800 → 600~1000. 봇 수정 뒤 원문 `The bot never changes its steer between ticks 700 and 800. ... Expected: greater than 0 But was: -1` — 그 구간(두 번째 R22 코너 근처, 거리로 추정)에서 앞먹임이 25/22 ≈ 1.14 로 1 에 고정됐다. 단언은 그대로.
- **검증** (`feature/tuning-steer-2`, 커밋 전, 에디터 6000.3.19f1, UTC): `tools/select-tests.ps1`(커밋 전) 판정 EditMode 전체·PlayMode 전체(씬·코어 시뮬), Long 은 무한 모드 보류로 안 돌림, Device 는 입력 장치 경로를 안 바꿔 안 돌림.
  - EditMode 1회차(씬만 바꾼 뒤) `testcasecount="230" result="Passed" passed="230"` (05:36:57Z~05:37:00Z).
  - `Run PlayMode Tests` 1회차(씬만) `testcasecount="139" result="Failed(Child)" passed="134" failed="2" skipped="3"` (05:37:22Z~05:42:58Z) — 위 2건, 지문 `0xC6A91FA9924183E7`, 봇 랩 31.32·30.26·30.26 초.
  - 컴파일 `*** Tundra build success (5.31 seconds)`(`TurnLimit`·봇). 2건만 `testcasecount="2" result="Passed"` (05:47:16Z~05:47:54Z).
  - `Run PlayMode Tests` 2회차 `testcasecount="139" result="Failed(Child)" passed="132" failed="4" skipped="3"` (05:48:17Z~05:53:36Z) — 위 NetReconcile 4건.
  - 컴파일 `*** Tundra build success (2.00 seconds)`(창). 4건만 `testcasecount="4" result="Passed"` (05:56:08Z~05:56:14Z).
  - `Run PlayMode Tests` 3회차(최종 코드) `testcasecount="139" result="Passed" passed="136" failed="0" skipped="3"` (05:56:38Z~06:01:59Z, 321.3초). **서킷 지문 `0x1BB2D21DADD3F85F` → `0xE3744A27006C0BB2`** (새 기준), 봇 랩 29.62·28.44·28.44 → 31.12·30.10·30.10 초.
  - EditMode 2회차(최종 코드) `testcasecount="230" result="Passed" passed="230" failed="0"` (06:02:26Z~06:02:29Z).
- **사용자 플레이** (2026-10-19): "좋다".

## 2026-10-19 · ↑ 없이 Ctrl 이면 슬링샷 부스터를 쓰지 않는다 (게이지 유지)

- **결정 (사용자, 2026-10-19 "앞 방향키 없이 ctrl눌렀을때 그냥 부스터가 안쓰여야한다")**: 아래 "위 방향키가 가속" 의 "Ctrl 을 ↑ 없이 누르면 게이지 1칸을 쓰고 같은 틱에 끊긴다(막지 않았다)" 를 바꾼다. `RunnerMotor.ChainAction(coasting)`: 매달려 있지 않고 `Coasting` 이면 아무것도 하지 않는다(게이지·`SlingTicks` 그대로). **그래플 강화**(매달린 채 Ctrl)는 부스터가 아니라 ↑ 와 무관하게 그대로.
- **테스트 추가**: `ChainRushDriftTests.Sling_WithoutThrottle_DoesNotFireAndKeepsTheSlot`.
- **검증** (`feature/control-throttle-boost`, 커밋 전, 에디터 6000.3.19f1, UTC): 컴파일 `*** Tundra build success (2.18 seconds)`. `tools/select-tests.ps1`(커밋 전) 판정 EditMode 전체·PlayMode 전체(코어 시뮬), Long 은 무한 모드 보류로 안 돌림. Device 는 입력 장치 경로를 바꾸지 않아 안 돌림.
  - EditMode(`!Enemy`·`!Endless`) `testcasecount="230" result="Passed" passed="230" failed="0"` (05:22:22Z~05:22:25Z).
  - `Run PlayMode Tests` `testcasecount="139" result="Passed" passed="136" failed="0" skipped="3"` (05:22:45Z~05:28:08Z, 323.2초). 서킷 지문 `0x1BB2D21DADD3F85F` 그대로, 봇 랩 29.62·28.44·28.44 초.
- **사용자 플레이** (2026-10-19): "됐다".

## 2026-10-19 · 위 방향키가 가속 (자동 달리기 끝), 떼면 부스터가 끊긴다 — 카트라이더식

- **결정 (사용자, 2026-10-19 "자동진행이 아니라 방향키 윗키를 눌러야 가속하도록 해줘 이럴려고 점프키를 뺀거였다", "카트라이더 부스터 중에도 앞키를 빼면 끊긴다", 극성 "B안", 계획 "부스터 변경 계획도 진행", 테스트 입력 "ㄱㄱ")**:
  - `TickInput.Coasting` (↑ 를 **뗀** 상태, 레벨값, 기본 false). `InputLatch` 는 `SwingHeld` 처럼 레벨로 전달, `Clear` 는 false. `TickInputCodec` 비트 64. `KeyboardMouseInputSource`: `coasting = !upArrowKey.isPressed`, 키보드가 없으면 true.
  - `RunnerMotor.Step`: `Coasting` 이면 ① 슬링샷(당김·유지)과 스윙을 놓은 뒤의 남은 속도를 **바로 끊는다**(`SlingTicks`·`SwingCarrySpeed` = 0), ② 땅에서는 목표 속력 0, 감속 `coastDeceleration`(새 `[SerializeField]`, 기본 6 m/s² — 20 m/s 에서 약 3.3초에 멈춤, 씬 값은 안 넣어 세 씬 모두 기본값). 공중·그래플·봉 스윙 자체의 속력은 그대로. HUD 조작 안내 "↑ 가속   ← / → 이동".
  - **극성을 반대로(`Coasting`) 둔 이유**: 이 칸이 없던 입력(봇 `CircuitBot`·`CourseBot`, 스크립트 입력 30여 곳, 리플레이 로그, 네트워크 바이트)이 모두 "↑ 누름" 으로 읽혀 그대로 달린다. 서킷 지문이 그대로인 근거다. **대가**: 장치를 읽는 새 입력 소스(터치 등)가 `coasting` 을 채우지 않으면 계속 달린다 — 그런 소스에는 "↑ 안 누르면 Coasting" 을 확인하는 테스트를 붙인다(키보드는 Device 테스트 `Input_ArrowKeys_…` 의 "출발선에 그대로" 단언).
  - Ctrl(체인 액션)을 ↑ 없이 누르면 게이지 1칸을 쓰고 슬링샷은 같은 틱에 끊긴다(쓰기만 하고 효과 없음). 막지 않았다 — 플레이 뒤 판단.
- **테스트 사양 변경**: `NetRemoteTests` 코덱 테스트가 비트 64 를 "모르는 값" 으로 단언했다 → 7비트 128조합 왕복, 비트 64 없는 바이트는 `Coasting` false, 모르는 값은 0x80. 기본 키보드 입력으로 달리던 테스트 8건(`ChainRushSlopeTests` 6건, `ChainRushTests.StartRun_Ready_RunsAndMovesForward`·`Pause_Running_FreezesMotionAndTimer`)이 출발하지 않아 실패(1회차 원문 아래) → 파일 안 `FullThrottleInputSource`(↑ 만 누른 입력)를 쓰게 했다. 단언 값은 그대로. Device 테스트 2건은 ↑ 를 같이 누르도록, `Input_ArrowKeys_…` 는 ↑ 없이 출발선에 그대로인지 단언을 더하고 ↑ 로 0.7초 달린 뒤 꺾도록(1회차 실패 아래).
- **테스트 추가**: `InputLatchTests.Consume_CoastingLevel_KeepsLatestAndClearResetsIt`, `ChainRushInputSourceTests.Coasting_ScriptedLetGoThenThrottle_StaysThenRunsThenRollsDown`(↑ 없으면 정지, ↑ 1초에 20 m/s, 떼고 1초에 −6 m/s), `ChainRushDriftTests.Sling_ThrottleLetGo_CutsTheBoostAtOnce`.
- **검증** (`feature/control-throttle`, 커밋 전, 에디터 6000.3.19f1, UTC): `tools/select-tests.ps1` 판정 EditMode 전체·PlayMode 전체(코어 시뮬), 입력 장치 경로라 Device 1회, Long 은 무한 모드 보류로 안 돌림.
  - 컴파일 `*** Tundra build success (4.83 seconds)`, 테스트 수정 뒤 `(1.51 seconds)`, Device 수정 뒤 `(2.09 seconds)`.
  - EditMode(`!Enemy`·`!Endless`) `testcasecount="230" result="Passed" passed="230" failed="0"` (04:59:56Z~05:00:00Z).
  - `Run PlayMode Tests` 1회차 `testcasecount="138" result="Failed(Child)" passed="127" failed="8" skipped="3"` (05:00:21Z~05:06:20Z) — 위 8건, 메시지 `Expected: greater than or equal to 32.0d`·`17.0d`·`42.0d`·`40.0d` 등, `But was: 5.0f`·`5.03599977f`(출발선 근처). 새 테스트·고친 코덱 테스트는 통과.
  - `Run PlayMode Tests` 2회차 `testcasecount="138" result="Passed" passed="135" failed="0" skipped="3"` (05:08:50Z~05:14:14Z, 323.7초). **서킷 지문 `0x1BB2D21DADD3F85F` 그대로**, 봇 랩 29.62·28.44·28.44 초.
  - `Run Device Input Tests` 1회차 `testcasecount="3" result="Failed(Child)" passed="2" failed="1"` (05:15:13Z~05:15:25Z, 시작 시 `isApplicationActive=False`) — `Input_ArrowKeys_…` `ChainRushTests.cs:177` `Expected: less than -10.0f But was: -2.43762207f`. 출발(Enter)과 ↑+← 는 전달됐고(2.4° 돎), 정지에서 0.3초 꺾기는 회전 한계 속력/22 m 로 몇 도뿐이다(스크립트 조향 테스트를 0.7초 달린 뒤로 바꾼 "다른 씬도 서킷 물리값" 과 같은 원인). 이 Device 테스트가 그 변경 뒤 `dev` 에서도 실패했는지는 **확인 못 했다**.
  - `Run Device Input Tests` 2회차 `testcasecount="3" result="Passed" passed="3" failed="0"` (05:16:39Z~05:16:56Z, `isApplicationActive=False`). 2회차 PlayMode 뒤에 바뀐 것은 Device 테스트 본문뿐이다(게이트에서 빠지는 테스트).
- **사용자 플레이** (2026-10-19): "괜찮다".
- **버린 대안**: `Throttle`(누름 = true) — 이 칸이 없던 입력 30여 곳과 리플레이 로그가 모두 "안 누름" 이 되어 고치고 다시 녹화해야 한다. 가상 키보드로 ↑ 를 누르게 해 기존 테스트를 살리기 — 장치 이벤트 전달이 흔들려 게이트에서 뺀 방식(2026-10-04).

## 2026-10-19 · 그래플 사거리 20 m·한 체공에 한 번 (M1-T 피드백 #4)

- **결정 (사용자, 2026-10-18 M1-T 피드백 "그래플이 32 m 에서 걸리고 공중에서 반복해 떠 있을 수 있다(→ 20 m, 공중 한 번)", 2026-10-19 요청서에 "진행하라", 씬 저장 코드에 "ㄱㄱ")**:
  - `GrappleController.maxRange` 기본값 32 → 20, 씬 3개(서킷·벤치·절차)의 값도 32 → 20(`Unity_RunCommand` + `SerializedObject` + `SaveScene`, 코드 전문을 보이고 승인. 로그 `maxRange [32] -> [20]` 3줄, diff 는 씬마다 그 한 줄뿐).
  - **한 체공에 한 번**: `RacerState.airGrappleUsed` — `Attach` 가 켜고, `RunnerMotor.Step` 에서 착지(`racer.Grounded`)하면 `ClearAirGrapple` 로 끈다(리스폰도 착지로 끈다). `Reset` 은 끈다. 켜져 있으면 `TryAttach` 는 거절하고 **빗나감(miss) 표시는 하지 않는다**(miss 는 "사거리 안에 앵커 없음" 뜻으로 남김). HUD 후보도 비운다. 스냅샷·`SimSnapshotCodec` 에 필드 추가.
- **테스트 추가**: `RacerStateTests.AirGrapple_AttachThenDetach_StaysUsedUntilClearedOrReset`, `ChainRushTests.Grapple_SecondPressInTheSameAir_IsRefusedWithoutAMissUntilLanding`. 뒤의 것은 1회차에 착지 단언(`ChainRushTests.cs:120`, `Expected: True But was: False`)이 실패했다 — 거절 단언은 통과했고, 점프 직후 (0,1,10) 으로 옮긴 뒤 0.1초 안에 착지하지 않았다. 테스트가 남은 속도를 지우고 1초까지 착지를 기다리도록 고쳤다(시뮬 코드는 안 바꿈). 원래 실패가 남은 상승 속도 때문이었는지는 메시지에 상태를 남기기 전이라 **확인 못 했다**.
- **검증** (`feature/tuning-grapple`, 커밋 전, 에디터 6000.3.19f1, UTC): 컴파일 `*** Tundra build success (11.01 seconds)`, 테스트 수정 뒤 `*** Tundra build success (3.61 seconds)`. `tools/select-tests.ps1`(커밋 전) 판정 EditMode 전체·PlayMode 전체(코어 시뮬), Long 은 무한 모드 보류로 안 돌림.
  - EditMode(`!Enemy`·`!Endless`) `testcasecount="229" result="Passed" passed="229" failed="0"` (04:28:09Z~04:28:12Z).
  - `Run PlayMode Tests` 1회차 `testcasecount="136" result="Failed(Child)" passed="132" failed="1" skipped="3"` (04:35:02Z~04:40:18Z) — 실패는 위 새 테스트 1건.
  - 새 테스트 단독 `testcasecount="1" result="Passed"` (04:41:46Z~04:41:48Z).
  - `Run PlayMode Tests` 2회차(최종 코드) `testcasecount="136" result="Passed" passed="133" failed="0" skipped="3"` (04:42:13Z~04:47:43Z, 330.1초). **서킷 지문 `0x1BB2D21DADD3F85F` 그대로**, 봇 랩 29.62·28.44·28.44 초 — 리플레이 봇의 그래플은 20 m 안에서 걸리고 한 체공에 두 번 걸지 않는다.
  - Device 는 입력 장치 경로를 바꾸지 않아 돌리지 않았다. 절차 코스 그래플 틈(`CourseTuning` 14~18 m)에서 20 m 로 닿는지는 무한 모드 보류로 **확인 안 함**(Sweep 안 돌림).
- **사용자 플레이** (2026-10-19): "괜찮다".
- **버린 대안**: 두 번째 누름을 빗나감으로 표시 — 앵커가 사거리 안에 있는데 "빗나감" 이 뜨면 이유를 오해한다.

## 2026-10-19 · 부스터 게이지 화면: 중앙 하단 막대 + 아이템 칸 2개 (카트라이더식, 표시만)

- **결정 (사용자, 2026-10-18 "카트라이더처럼 화면 중앙 하단 게이지 + 찬 부스터를 아이템 칸처럼 따로", 2026-10-19 요청서에 "ㄱㄱ")**: `ChainRushHud.DrawGauge`(왼쪽 아래 속력 상자 밑 `CHAIN / CTRL` 막대 2개)를 지우고 `DrawBoost` 로 바꿨다 — 상태 줄 바로 위 중앙에 `BOOST / CTRL` 막대(다음 부스터까지 = `RacerState.Gauge` 의 소수 부분, %; 두 칸이 다 차면 MAX)와 그 오른쪽 아이템 칸 `MaxGauge`(2)개(정수 부분만큼 민트 `▶▶`). 게이지 규칙(스윙 35 m 당 1칸, 최대 2, Ctrl 로 1칸)·시뮬·네트워크는 그대로.
- **검증** (`feature/hud-boost-gauge`, 커밋 전, 에디터 6000.3.19f1, UTC): 컴파일 `*** Tundra build success (4.58 seconds)`. `tools/select-tests.ps1`(커밋 전) 이 HUD 로 고른 6 클래스 + 서킷 지문 `testcasecount="66" result="Passed" passed="66" failed="0"` (04:14:06Z~04:15:28Z), 서킷 지문 `0x1BB2D21DADD3F85F` 그대로. 테스트는 그리기를 보지 않는다.
- **사용자 플레이** (2026-10-19): "좋다".
- **버린 대안**: 시뮬을 카트라이더처럼 "막대가 차면 0 으로 돌아가고 아이템이 따로 쌓임" 으로 바꾸기 — 지금의 0~2 연속 게이지와 보이는 것이 같아 시뮬·스냅샷·지문을 바꿀 이유가 없다.

## 2026-10-19 · 다른 씬도 서킷 물리값: 테스트 벤치·절차 코스 Runner 를 서킷 기준으로 (테스트용 빠른 값은 남김)

- **결정 (사용자, 2026-10-19 "더 빠른 테스트 진행을 위해 다른 씬 속력도 20으로 해줘", "서킷기준으로 다 바꿔버리면 안되나? 물리값 기준이 서킷일텐데", "테스트에서 빨리 확인하기위한 치트적인 요소들은 놔둬라", 표와 요청서에 "추천대로 ㄱㄱ")**:
  - 세 씬의 차이는 `RunnerMotor` 뿐이었다(그래플 `maxRange` 32·`retractSpeed` 3·`empoweredRetractSpeed` 12, 카메라 `offset`·`maxYawSpeed` 는 세 씬이 같다 — 씬 grep).
  - **`ChainRushTestBench.unity` Runner**: 서킷 값 11개 — `runSpeed` 10 → 20, `minTurnRadius` 0 → 22, `turnSpeedLoss` 0 → 0.1, `overspeedFalloff` 30 → 8, `airAcceleration` 5 → 0, `guardSpeedLoss` 0.15 → 0.4, `guardScrapeDecel` 0 → 25, `slingTopSpeed` 20 → 28, `slingCarrySpeed` 16 → 26, `slingCarryTime` 0.8 → 2, `empoweredReleaseSpeed` 18 → 26. **남긴 테스트용 값**: `runAcceleration` 30(서킷 8 — 출발 0.67초, 서킷은 2.5초), `turnAcceleration` 1200(서킷 200 — 봇이 곡선을 따라가는 반응).
  - **`ChainRushProcedural.unity` Runner**: 위 11개 + `runAcceleration` 8·`turnAcceleration` 200 — 서킷과 같다(무한 모드 보류로 게이트 테스트가 돌지 않아 테스트용 값을 남길 이유가 없다). 절차 지문 `0xE81912109A2EB020` 은 바뀌었을 것이나 보류 중이라 확인하지 않았다.
  - `Unity_RunCommand` + `SerializedObject` + `SaveScene`(코드 전문을 보이고 사용자 승인). 실행 로그의 "바꾸기 전" 값이 이미 목표와 같았다(`runSpeed 20 -> 20`) — 원인은 확인 못 했다(그 전 시도 2번은 권한 검사에 막혔고 1번은 사용자가 거절). 최종 diff 는 두 씬 모두 Runner 블록뿐이고, 지운 필드 줄(`driftGrip`·`swingSpeed` 등 9줄)이 빠지고 기본값 줄(`poleMaxAngle` 등)이 들어갔다.
  - **권한**: `Unity_RunCommand` 의 씬 저장·테스트 실행이 자동 모드 검사기에 여러 번 막혔다(`[Irreversible Local Destruction]`, `[Modify Shared Resources]` — 같은 종류 호출도 통과하거나 막혀 판단이 일정하지 않다). 사용자 결정으로 개인 설정 `.claude/settings.local.json`(git 무시)에 이 도구의 `ask` 를 넣었다가, 확인 창이 너무 많아 사용자가 "자동화" 를 요청했다(같은 날, `allow` 만 남기는 쪽).
- **테스트 사양 변경** (벤치 10 m/s·옛 물리값을 전제한 단언, 1회차 실패 원문은 아래 검증, 사용자 "ㄱㄱ"):
  - `ChainRushSlopeTests`: `RunSpeed` 10 → 20(경사 ±0.2, 오르막 점프 2개), 내리막 표본 하한 100 → 50(26 m/s 로 36 m 는 약 69틱).
  - `ChainRushDriftTests`: `RunSpeed` 10 → 20. `Sling_WithSlot_PullsPastFifteenThenSettles` → `Sling_WithSlot_PullsWellPastRunSpeedThenSettles`(최고 > 달리기 + 5, 기다림 1.5 → 3초: 당김 0.4 + 유지 2 + 26 → 20 을 초당 8).
  - `ChainRushInputSourceTests` 조향 2개: 최소 회전 반지름 22 m 라 정지 상태에서는 거의 돌지 않는다 — 0.7초 달린 뒤 꺾는다. `Tilt_…TurnsBodyToFaceHeading` 의 경계 넘기 시작 각 −170 → −177(뒤돌아 느려지며 0.2초에 약 8° 돈다, 실측 −177.99).
  - `ChainRushSteeringTests.Guard_AngledHit_…`: 꺾는 시간 0.3 → 0.6초, 회복 속력 > 9.5 → > 19.5(실측 "heading -27.81 deg, speed 2 s later 20.00 m/s").
  - `ChainRushCornerSwingTests.Swing_PressedInTheAir_…`: 20 m/s 점프(약 21 m)가 R30 도로의 원호 전 걸기 구간(약 20 m)보다 길어 원호 안에 착지하고 봉이 95° 밖으로 빠졌다. 이 테스트만 R60 도로(직선 40 + R60 90°, 원호 전 약 27.5 m)로, 원래 시나리오 그대로. 같은 테스트 3번 실패 뒤 멈추고 보고해 사용자가 골랐다(§4-5).
- **검증** (`feature/scenes-circuit-physics`, 커밋 전, 에디터 6000.3.19f1, UTC):
  - EditMode(`!Enemy`, `!Endless`) `testcasecount="228" result="Passed" passed="228" failed="0"` (03:27:26Z~03:27:29Z).
  - `Run PlayMode Tests` 1회차(씬만 바꿈) `testcasecount="135" result="Failed(Child)" passed="121" failed="11" skipped="3"` (03:28:14Z~03:34:35Z): `Slope_ConstantGrade_ScalesRunSpeed(-0.2f)` "Grade -0.2: ds/dt 26.00002 Expected: 13.0f +/- 0.5f", `(0.2f)` "ds/dt 13.99994 Expected: 7.0f", `Jump_OffAnUphill_…(0, 5)` "precondition: the uphill run target. Expected: 7.0f +/- 0.5f But was: 14.0f", `Downhill_TwentyPercent_StaysGrounded` "Too few samples on the slope. Expected: greater than 100 But was: 69", `Sling_WithoutWholeSlot_DoesNothing` "Expected: less than 10.5f But was: 20.0f", `Sling_WithSlot_PullsPastFifteenThenSettles` "Speed returns to the run speed after the carry. Expected: 10.0f +/- 0.5f But was: 26.0f", `Steer_ScriptedLeftThenRight_…`·`Tilt_ScriptedSteerLeft_TurnsBodyToFaceHeading` "Expected: less than -10.0f But was: -3.75010681f", `Guard_AngledHit_…` "Expected: less than -20.0f But was: -13.7498627f", `Swing_PressedInTheAir_…` "precondition: landed before the arc. Expected: less than 30.0d But was: 32.32334303855896d".
  - 빠른 확인(병합 근거 아님): 5 클래스 `testcasecount="29" passed="27" failed="2"` (03:38:29Z~03:40:07Z — 공중 스윙 "A press on the ground hooks. Expected: True But was: False", 경계 넘기 "Expected: greater than 90.0f But was: -177.994217f"), 5 클래스 `passed="28" failed="1"` (03:50:07Z~03:51:42Z — 공중 스윙 "precondition: following the arc, the pole stays in reach"), 코너 스윙 `testcasecount="9" result="Passed"` (03:59:24Z~03:59:59Z, R60 도로).
  - 컴파일 테스트 수정마다 `*** Tundra build success` (8.97·2.59·3.50 초).
  - `Run PlayMode Tests` 2회차 `testcasecount="135" result="Passed" passed="132" failed="0" skipped="3"` (04:00:31Z~04:05:44Z, 313.5초). 서킷 지문 `0x1BB2D21DADD3F85F` 그대로(서킷 씬은 안 바꿈), 봇 랩 29.62·28.44·28.44 초.
- **확인 못 한 것**: 사용자 플레이(테스트 벤치·절차 코스 20 m/s) — 사용자가 플레이 확인 없이 "커밋·병합·푸시" 를 골랐다(2026-10-19). 절차 지문(보류).
- **버린 대안**: 코드 기본값을 서킷 값으로 — 씬은 값을 따로 저장하므로 씬이 안 바뀌고, 코드로 Runner 를 만드는 곳만 바뀐다. 벤치까지 서킷 값 전부(출발 가속 8·조향 200) — 사용자 "테스트에서 빨리 확인하기위한 … 놔둬라". 공중 스윙 테스트를 둘로 나누기(발판 끝 낙하) — 시험용 도로가 하나 더 든다.

## 2026-10-19 · M1-T 점프·스윙: 공중에서는 스윙이 걸리지 않고, 서킷의 점프는 이륙 속력을 유지한다

- **결정 (사용자, 2026-10-19 "점프와 그래플이 alt로 정상작동하는 것 확인했다. 3번에 추가적으로 조절해야할 요소로 점프 중에 스윙(shift)가 걸리는 것이 있다. 점프에 대해서 속도가 어떻게 변하는지 파악하고 조절해야할 필요가 있어보인다", 선택지에 "공중에선 안 걸림"·"이륙 속력 유지", 계획에 "A부터 진행하라")**:
  - **파악한 것** (코드 기준, 플레이 측정 아님): 점프는 `jumpSpeed` 11.5·`gravity` 22 로 평지 체공 약 1.05초·최고 약 3.0 m(20 m/s 면 약 21 m). 공중의 수평 목표는 경사·회전 감속 없이 늘 `runSpeed` 이고, 가속·감속이 **박혀 있던 5 m/s²** 였다. 그래서 서킷에서 오르막(땅 목표 최저 14)이나 꺾는 중(최대 −10%)에 뛰면 공중에서 20 쪽으로 빨라졌고(한 번에 최대 약 +5 m/s), 넘친 속력은 땅(8)보다 천천히(5) 줄었다. 스윙 걸기(`TryHookPole`)는 땅 여부를 보지 않았고 봉 찾기는 수평 거리·각도만 봐서(`Centerline.TryFindPole`) 공중에서도 걸려, 위 속도를 유지한 채 떨어지며 돌았다.
  - **`RunnerMotor`**: `TryHookPole` 은 `CoyoteTime > 0`(땅, 또는 발판을 걸어 나간 뒤 0.1초 안)일 때만 건다. 점프하는 틱은 점프가 코요테를 0 으로 만든 뒤 불리므로 그 틱의 누름도 걸리지 않는다. 공중에서 누른 채 착지해도 걸리지 않는다(누름만 건다는 기존 규칙). 새 필드 `airAcceleration`(기본 5 = 이전 동작): 그래플이 아닌 공중의 수평 가감속. 그래플 중은 5 그대로.
  - **서킷 씬 Runner**: `airAcceleration` 0 — 사용자가 에디터 인스펙터에서 바꾸고 저장했다(`Unity_RunCommand` 저장은 자동 모드 권한 검사에 막혔다). 씬 diff 는 `airAcceleration: 0` 한 줄. 테스트 벤치·절차 코스는 기본 5 그대로.
  - **테스트 추가**: `ChainRushCornerSwingTests.Swing_PressedInTheAir_DoesNotHook_NorWhenHeldIntoTheLanding`, `ChainRushSlopeTests.Jump_OffAnUphill_TheAirSpeedMovesByAirAcceleration`(0 과 5, 플레이 인스턴스에서만 값 변경).
- **검증** (`feature/tuning-jump-swing`, 커밋 전, 에디터 6000.3.19f1, UTC):
  - 컴파일 `*** Tundra build success (31.86 seconds), 14 items updated, 1618 evaluated`.
  - 빠른 확인(병합 근거 아님) 코너 스윙·경사 2 클래스 `testcasecount="15" result="Passed" passed="15" failed="0"` (02:24:13Z~02:25:36Z): "ChainRush uphill jump (air acceleration 0): takeoff 7.000 m/s, after 20 air ticks 7.000 m/s.", "(air acceleration 5): takeoff 7.000 m/s, after 20 air ticks 9.000 m/s."
  - EditMode(`!Enemy`, `!Endless`) `testcasecount="228" result="Passed" passed="228" failed="0"` (02:30:47Z~02:30:50Z).
  - `Run PlayMode Tests` (서킷 씬 값 저장 뒤) `testcasecount="135" result="Passed" passed="132" failed="0" skipped="3"` (02:31:12Z~02:37:22Z, 369.3초, skipped 3 은 `[Explicit]` 네트워크 레이스). **서킷 지문 `0x150CA0904EFFEBDE` → `0x1BB2D21DADD3F85F`** — 리플레이 봇이 점프 틈을 뛰고 그래플을 1회 쓴다(공중 속력 변경, 새 기준). 봇 랩 29.62·28.44·28.44 초(이전과 소수 둘째 자리까지 같다). Long·Sweep 은 무한 모드 보류로, Device 는 입력 장치 경로를 바꾸지 않아 돌리지 않았다.
  - 공중 스윙 테스트가 고치기 전 코드에서 실패하는지는 돌려 보지 않았다(규칙 변경이라 버그 수정 절차가 아니다).
- **사용자 플레이** (2026-10-19): "확인했다. ㄱㄱ"(커밋·병합 지시).
- **버린 대안**: 공중에도 땅 규칙(경사·회전 감속·가속 8) — 공중의 경사 기준이 애매하고 바뀌는 코드가 크다. 착지 순간 누르고 있으면 걸기 — "누름만 건다" 규칙에 예외가 생긴다. 코드 기본값을 0 으로 — 테스트 벤치·절차 지문까지 바뀐다(사용자: 테스트에서 빨리 확인하기 위한 값은 그대로 둔다).

## 2026-10-18 · R4c 사각 서킷(R22 90° 코너 4개) + 카트라이더식 조향·감속

- **결정 (사용자, 2026-10-18 "R4c 진행하면서 방향키로 움직이는 조향?감과 감속 조건들 넣어봐 카트라이더 보면 좌우로 움직이거나 벽에 부딫히면 감속이 된다", 계획에 "사각 서킷 + 계획대로")**:
  - **트랙** (`Circuit_Stadium.asset` 은 `Unity_RunCommand` + `SerializedObject` 로 고치고 `AssetDatabase.SaveAssets`, 사용자 승인): 직선 A 140 m(언덕 + 점프 틈 6 m) → R22 90° → 직선 B 60 m → R22 90° → 직선 C 140 m(그래플 틈 16 m) → R22 90° → 직선 D 60 m → R22 90°. 한 바퀴 538.2 m(이전 531.4), 앵커 S 322.12(이전 318.66), 코너 하나 34.6 m. `TrackDefinition.MinRadius` 30 → 20(무한 모드의 `CourseTuning.MinRadius` 30 은 그대로), 코드 기본 구간 목록도 같은 모양.
  - **`RunnerMotor` 새 수치** (코드 기본값은 이전 동작 그대로, 0 이면 그 분기를 아예 건너뛴다): `runAcceleration`(기본 30 — 박혀 있던 지상 가속), `turnSpeedLoss`(기본 0 — 지금 회전율 / 회전 상한 만큼 달리기 목표를 깎는다, 지상·스윙 밖), `guardScrapeDecel`(기본 0 — 가드에 닿은 틱마다 수평 속력을 이만큼 m/s² 로 깎는다, `ScrapeAlongGuard`).
  - **서킷 씬 Runner** (`Unity_RunCommand`, 승인): `runAcceleration` 8(0 → 20 m/s 2.5초 — 이전 0.7초라 무엇에 부딪혀도 곧 되찾았다), `turnSpeedLoss` 0.1, `guardScrapeDecel` 25, `guardSpeedLoss` 0.15 → 0.4, `turnAcceleration` 1200 → 400. 씬 diff 는 Runner 블록 7줄뿐. 테스트 중 에디터가 저장 여부를 물어 사용자가 "Don't Save" — 파일은 그대로 확인.
  - **테스트 사양 변경** (트랙 모양·수치를 전제한 것):
    - EditMode `TrackDefinitionTests`: 한 바퀴 `2·140 + 2·60 + 2π·22`, `Stadium()` 도우미를 사각으로, 반지름 하한 테스트 30 → 20(`Arc(15, 90)` 거부), 짧은 도움닫기 테스트의 구간 번호(9·10·13 → 11·12·15, 오류는 segment 13), 앵커 318.66 → 322.12, 그래플 틈은 직선 C.
    - `ChainRushWorldTests` 의 공유 물리 대조: 서 있는 러너 자리 S 150 → 100. 150 은 이제 첫 코너 안이고 봇이 중심선 안쪽을 끊어 지나가 대조가 아무 차이도 못 냈다("changed nothing").
    - `NetReconcileTests`: 봇이 놓는 여유 `LongRelease` 40 → 12 m(코너 34.6 m 보다 길면 봉을 한 번도 걸지 않아 "The swings must have filled the gauge. Expected: 2.0f But was: 0.0f"). 끝 보정 탐색(`FindFinishReplayCase`): 부스터(120틱)가 마지막 스윙(약 95틱)보다 길어 P 가 마지막 코너 앞으로 오고, 고정 입력 기록을 재생하면 봉을 거는 자리가 어긋나 일찍 끝났다(`ends<F` 만). 그래서 P 부터의 클라 입력을 **스윙 봇을 그 상태에서 이어 돌려** 만든다(`SwingBotFrom` — 체인 누름 P·F-1 과 줄 조정은 덧입힘). P 창은 `F - SlingTicks - 60 .. + 2`. 중간에 시도한 "줄을 3 m 늘여 스윙을 길게"는 95틱에 그쳐 되돌렸다.
- **검증** (`feature/circuit-r22`, 커밋 전, 에디터 6000.3.19f1, UTC):
  - 컴파일 `*** Tundra build success (4.42 seconds)`, 테스트 수정마다 `success`. 에셋 수정 뒤 `TryValidate` 통과(실패면 저장하지 않게 했다).
  - EditMode(`!Enemy`, `!Endless`) `testcasecount="228" result="Passed" passed="228"` (13:09:32Z~13:09:33Z).
  - `Run PlayMode Tests` 1회차 `testcasecount="132" result="Failed(Child)" passed="125" failed="4" skipped="3"` (13:10:04Z~13:15:26Z): 위 월드 대조 1, NetReconcile 게이지 2(`ClientEnd_CatchingUpAfterTheClientsOwnFinish_…`, `ClientEnd_WhenOnlyTheClientsRunFinished_…`), 끝 보정 1("No case fits … ends<F"). 봇 랩 29.38·28.16·28.16 초.
  - 빠른 확인: NetReconcile + World `testcasecount="28" passed="27" failed="1"` (13:18:27Z~13:19:00Z, 끝 보정만 — 줄 늘이기 시도), NetReconcile `testcasecount="23" result="Passed"` (13:21:33Z~13:22:03Z): "rope tweak on ticks 4071-4072, chain presses on ticks 3959 and 4078 … 0.00221 m past … 0.01947 m short (search 10.7 s)".
  - `Run PlayMode Tests` 2회차 `testcasecount="132" result="Passed" passed="129" failed="0" skipped="3"` (13:22:40Z~13:28:02Z, 322.4초). **서킷 지문 `0x12DE39829FD6B3DB` → `0x2C858BEFE45E2460`**(트랙·수치 변경, 새 기준). 봇 랩 29.38·28.16·28.16 초(이전 27.26·26.92·26.94 — 출발 가속 2.5초와 꺾을 때 감속). Long·Sweep 은 무한 모드 보류로 돌리지 않는다(`CLAUDE.md` §9-2), Device 는 입력 장치 경로를 바꾸지 않아 돌리지 않았다.
- **사용자 플레이** (2026-10-18): "다 좋은데 좌우 방향키로 방향 꺾이는 정도를 좀 더 줄였으면 좋겠다. 또한 점프 키를 alt로 하자 이후 커밋 병합 푸쉬".
  - **조향 더 줄임** (서킷 씬, `Unity_RunCommand`): `turnAcceleration` 400 → 200(최대 회전까지 약 0.26초 — 짧게 누르면 덜 돈다), `minTurnRadius` 20 → 22(20 m/s 에서 최대 57 → 52°/s). 22 m 보다 크게는 하지 않았다 — 코너가 R22 라 봉 없이는 코너를 못 돌게 된다.
  - **점프·그래플 키 = 왼쪽 Alt** (`KeyboardMouseInputSource`): 누름 = 주동작, 뗌 또는 ↓ = 해제(↑ 는 이제 쓰지 않는다). 오른쪽 Alt 는 한국어 자판의 한/영 키라 넣지 않았다. HUD 안내 4줄, Device 테스트의 ↑ → LeftAlt.
  - 검증: 컴파일 `*** Tundra build success (3.75 seconds)`. `Run PlayMode Tests` 3회차 `testcasecount="132" result="Passed" passed="129" failed="0" skipped="3"` (13:35:24Z~13:41:03Z, 338.5초), **서킷 지문 → `0x150CA0904EFFEBDE`**(최종 새 기준), 봇 랩 29.62·28.44·28.44 초, 끝 보정 "rope tweak on ticks 4056-4057, chain presses on ticks 3962 and 4081 … (search 18.0 s)". `Run Device Input Tests` `testcasecount="3" result="Passed" passed="3"` (13:41:37Z~13:41:52Z, `isApplicationActive=True`).
- **확인 못 한 것**: 바꾼 조향과 Alt 키의 사용자 플레이. 수치는 카트라이더 공개 상수가 없어 시작값이다.
- **버린 대안**: 지금 모양에 반지름만 R22(4-1 ②) — 코너 하나 69 m 로 스윙이 여전히 길다. 감속만 넣고 가속은 그대로 — 30 m/s² 로 0.7초면 되찾아 감속이 느껴지지 않는다. 끝 보정 테스트를 `[Ignore]` — 끝 보정이 게이트에서 빠진다.

## 2026-10-18 · 적·아이템·무한 모드 테스트 보류: `Enemy`·`Endless` 카테고리를 게이트와 EditMode 실행에서 뺀다

- **결정 (사용자, 2026-10-18)**: "적 등장에 관련한 것은 제외해도 좋을것 같다. 적과 아이템은 추후에 아이템전 구상하면 넣겠다. 이것 기록해놓고 테스트 요소에서 제외해줘"(계획에 "추천대로 ㄱㄱ"), 이어서 Long 실행 중 적이 보이자 "우리가 무한모드를 계속해서 작업할게 아니고 서킷에 집중하기로 했다. 무한모드를 테스트 돌릴 이유가 없다는것. 당연히 서킷에서도 나오면 안된다", 표현 테스트는 "만약 포함해야한다면 무한모드 테스트가아닌 서킷에서 하자".
  - **`Enemy`** (적·공격·표적, 아이템전 구상 때 되살림): PlayMode `ChainRushCombatTests` 의 `Combat_*` 4, `ChainRushInputSourceTests.Attack_ScriptedPressNearTarget_HitsExactlyOnce`, `ChainRushProceduralTests.Enemy_EncounterOnACurve_…`, `ChainRushTests.Restart_AfterAttackAndFall_ResetsCourseAndState`. EditMode `EncounterTuningTests` 5, `RacerStateTests` 의 공격 쿨다운·표시 3.
  - **`Endless`** (무한 모드 = 절차 코스, 무한 모드를 다시 열 때 되살림): `ChainRushProceduralTests` 전체(게이트 7 · `Long` 시드 3 · `Sweep` 1), `ChainRushCombatTests` 전체(그래플 재시작 1 포함 — 절차 코스 씬에서 돈다), `ChainRushReplayTests.Replay_ProceduralCourseRun_…`(`Long`, 절차 지문), `ChainRushSteeringTests.Guards_ProceduralRoad_AreGuarded`(원래 `Guards_PrototypeDecksAndProceduralRoad_AreGuarded` 에서 절차 도로 부분만 떼어 냄; 테스트 벤치 데크 부분은 `Guards_PrototypeDecks_AreGuarded` 로 게이트에 남음), EditMode `CourseGeneratorTests` 전체 20.
  - **지금 `Long`·`Sweep` 은 모두 `Endless` 다.** 그래서 보류 동안 `Run Long Tests`·`Run Course Sweep` 는 병합·마일스톤 조건에서 돌리지 않는다(`CLAUDE.md` §9-2 `Endless` 줄). 절차 지문 `0xE81912109A2EB020` 은 이제 확인하지 않는다. 마지막 확인은 같은 날 12:41:23Z~12:44:38Z `Run Long Tests` `testcasecount="4" result="Passed"`.
  - **서킷으로 옮긴 것** (무한 모드 씬에서 돌던 공용 기능 테스트): `ChainRushPresentationTests` 3(오디오·관절 동기화, 팔 자세, 효과음 신호) — 씬을 서킷으로, 팔 자세 테스트는 시드 찾기 대신 서킷의 그래플 틈(`CircuitRace.TryGetNextGap(40)`)을 쓴다. 단언은 그대로. `ChainRushRollbackTests.Snapshot_OutsideACircuitRaceOrRun_ThrowsLoudly` 의 "서킷 밖" 씬을 절차 코스에서 테스트 벤치로(같은 `NotSupportedException` 경로, `ChainRushGame.RequireSnapshotSupport`).
  - **서킷에 적은 나오지 않는다** (확인): 서킷 씬에 `EnemyDirector` 가 없다(스크립트 GUID grep 0건). 표적(`CourseTarget`) 15개가 씬에 남아 있지만(절차 씬에서 복사할 때 따라온 직선 코스 `Sector 01`~`08` 의 자식) 에디터 조회로 15개 모두 `activeInHierarchy=False` 였고, 런타임에 `Sector` 를 켜는 코드는 없다(`SetActive(true)` grep — `CourseTarget.cs:63` 은 자기 표시만 켠다). 씬 정리는 이번에 하지 않았다.
  - **바꾼 곳**: 위 테스트의 카테고리, `ChainRushSceneBuilder.RunTests` 필터(`"!Enemy"`, `"!Endless"`), `tools/select-tests.ps1`(PlayMode·EditMode 필터, Long·Sweep 안내), `CLAUDE.md` §9-2(병합 조건 제외 목록, `Enemy`·`Endless` 줄), `docs/RULES/VERIFICATION.md` §4-4(제외 목록, EditMode 실행법, `!` 값끼리는 AND — test-framework `RuntimeTestRunnerFilter.AddFilters` 를 읽어 확인). 에이전트 정의에는 카테고리 목록이 없어 `tools/sync-agents.ps1` 은 돌리지 않았다.
- **검증** (`feature/test-exclude-enemy`, 커밋 전, 에디터 6000.3.19f1, UTC):
  - `Enemy` 만 뺐을 때: 컴파일 `*** Tundra build success (1.84 seconds)`. EditMode(`!Enemy`) `testcasecount="248" result="Passed"` (12:34:56Z~12:35:02Z). `Run PlayMode Tests` `testcasecount="138" result="Passed" passed="135" failed="0" skipped="3"` (12:35:25Z~12:40:52Z). `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (12:41:23Z~12:44:38Z, 절차 지문 그대로 — 이 실행 중 사용자가 적을 보고 무한 모드 제외를 정했다).
  - `Endless` 까지: 컴파일 `*** Tundra build success (4.77 seconds)`. EditMode(`!Enemy`, `!Endless`) `testcasecount="228" result="Passed" passed="228" failed="0"` (12:49:40Z~12:49:43Z, `CourseGeneratorTests` 없음). `Run PlayMode Tests` 1회차 `testcasecount="132" result="Failed(Child)" passed="128" failed="1" skipped="3"` (12:50:06Z~12:55:23Z): 서킷으로 옮긴 `Animation_JumpAndGrapple_ChangesArmPoseWithoutMovingMotor` — "The circuit has no grapple gap with an anchor. Expected: greater than 0.0d But was: 0.0d". 원인: `CircuitRace.TryGetNextGap(40)` 은 S 40 뒤 첫 틈인 첫 직선의 점프 틈(앵커 없음)을 돌려준다. 서킷 테스트(`ChainRushCircuitTests.cs:129`)처럼 S 150 부터 찾게 고쳤다(컴파일 `*** Tundra build success (1.65 seconds)`). 2회차 `testcasecount="132" result="Passed" passed="129" failed="0" skipped="3"` (12:56:59Z~13:02:17Z, 318.3초, 이전 145 − 13), 서킷 지문 `0x12DE39829FD6B3DB` 그대로. 절차 코스 씬을 쓰는 테스트는 결과에 없다(이름에 Procedural 이 남은 `ChainRushRoadTests.Road_NoSteeringIntoR30_ProceduralGuardTurnsRunner` 는 자체 시험 도로).
  - `tools/select-tests.ps1` 실행(`Enemy` 단계): 기본 범위 "전체" + EditMode 안내, `-Range 75df74d^..75df74d` 는 클래스 3개 + 지문 1건에 `categoryNames` 제외 목록. `Endless` 추가 뒤 출력은 아래 병합 보고.
- **버린 대안**: `[Ignore]` — 결과에 skipped 로 남고 되살릴 때 코드를 고쳐야 한다. 테스트 삭제 — 되살릴 때 다시 써야 한다. 무한 모드 테스트에서 적만 끄기 — 적을 끄는 설정을 새로 만들어야 하고 절차 지문이 바뀐다(사용자는 무한 모드 테스트 자체가 필요 없다고 했다). 표현 테스트를 같이 빼기 — 사용자 "포함해야한다면 … 서킷에서".
- **이어진 결정**: 병합도 바뀐 부분만 — 아래 "병합은 바뀐 부분만".

## 2026-10-18 · 병합은 바뀐 부분만: `feature → dev` 는 `select-tests -Range dev...HEAD` 가 고른 테스트

- **결정 (사용자, 2026-10-18 "나는 분명 변경점이 생기면 그 부분에 대해서만 테스트를 돌리는 것을 원했는데", 이어서 "병합 때도 바뀐 부분만 돌리게 규칙 바꿔")**: 병합 조건(②)의 PlayMode 는 **`tools/select-tests.ps1 -Range dev...HEAD`(커밋한 것만)가 고른 테스트 1회 통과**다. 도구가 "전체" 로 판정하면(코어 시뮬·씬·씬 빌더·표에 없는 경로 등) 그때만 게이트 전체(`Run PlayMode Tests`). 도구는 런타임 파일이 바뀌면 서킷 지문 테스트를 늘 함께 고른다. EditMode 는 그대로 전체(`Enemy`·`Endless` 제외, 약 5초). 마일스톤 조건(③, `dev → main`)은 그대로 게이트 전체 2회 등.
- **바꾼 곳**: `CLAUDE.md` §9-2(3단 설명, 병합 조건), `docs/RULES/VERIFICATION.md` §4-4(게이트 전체를 쓰는 때, ①·② 실행법), `tools/select-tests.ps1`(머리말, 출력 제목 — 고르는 표는 그대로), `docs/HANDOFF.md` 의 테스트 3단 줄. 에이전트 정의에는 병합 조건 문구가 없다(grep 0건).
- **이유와 대가**: 바뀐 부분과 무관한 테스트로 병합마다 5~6분을 쓰지 않는다. 대가로 표가 놓친 의존(파일 → 테스트 클래스)은 병합 때 잡히지 않고 마일스톤(③)에서 잡힌다. 표에 없는 `Assets/` 경로는 여전히 "전체" 로 떨어진다.
- **버린 대안**: 지금처럼 병합마다 게이트 전체(2026-10-14 "테스트 3단") — 사용자가 원한 방식이 아니다.

## 2026-10-18 · M1-T 앞당김 1: 서킷 속력 20 m/s, 최소 회전 반지름 20 m, 부스터는 카트라이더 비율, 방향키 조작

- **결정 (사용자, 2026-10-18 R4b 플레이 "속도가 여전히 많이 느리다. 또한 a/d로 조향되는 범위가 너무 많다 … 실제 카트라이더를 참고해봐", 제안에 "추천대로 ㄱㄱ, 씬은 RunCommand로 바꿔라")**:
  - **카트라이더 참고** (공식 물리 상수는 공개되지 않아 팬 위키 수치): 순간 부스터 169→232 km/h(×1.37), 부스터 최고 약 294 km/h, 드리프트 기술로 210~220 → 310~330 km/h(약 ×1.5). 급커브는 드리프트로 돌고 A/D 만으로는 고속에서 크게 돈다(원칙, 수치 확인 못 함). 그래서 부스터는 기본의 약 1.4배, 회전은 속력에 비례해 둔해지게.
  - **`RunnerMotor`** (새 필드의 코드 기본값은 지금 동작 그대로라 다른 씬·절차 지문은 안 바뀐다):
    - `minTurnRadius`(기본 0 = 끔): 회전율 상한 = min(`maxTurnRate`, 수평 속력 / 반지름). 20 m/s 에서 57°/s.
    - `overspeedFalloff`(기본 30): 지상에서 달리기 목표보다 빠를 때만 쓰는 감속(이전에는 가속과 같은 30).
    - 그래플 중 속력 16 고정 → `Max(16, runSpeed)`(달리기보다 느려지지 않게).
  - **`ChainRushCircuit.unity` 의 Runner** (`Unity_RunCommand` + `SerializedObject` + `SaveScene`, 사용자 승인): `runSpeed` 10 → 20, `minTurnRadius` 20, `overspeedFalloff` 8, `slingTopSpeed` 20 → 28, `slingCarrySpeed` 16 → 26, `slingCarryTime` 0.8 → 2, `empoweredReleaseSpeed` 18 → 26. 저장하면서 R4·R4b 때 지운 필드 줄(`driftGrip` 등)이 빠지고 R4b 새 필드 줄이 기본값으로 들어갔다. 씬 diff 는 Runner 블록뿐. 테스트 실행 중 에디터가 저장 여부를 물어 사용자가 "저장 안 함"을 눌렀다(파일은 그대로 확인).
  - **`FollowCamera`**: 화각이 넓어지는 속력 상한 20 → 30 m/s(부스터 때도 넓어지게).
  - **조작을 카트라이더식으로, 마우스 없이** (사용자 플레이 "속도감 좋다 다만 우선 wasd에서 방향키로 옮기고", 키 배치 질문에 "카트라이더식, 마우스 없이"): `KeyboardMouseInputSource` — ←/→ 조향(A/D 제거), ↑ 주동작(점프·그래플, 이전 좌클릭), ↑ 뗌·↓ 해제(이전 좌클릭 뗌·우클릭), Space·Shift·Ctrl 그대로. 클래스 이름은 그대로 뒀다(파일 이름을 바꾸면 `.meta` 이동이 따른다). HUD 안내 4줄, Device 테스트 2개(`Input_KeyboardAndMouse_…` → `Input_ArrowKeys_StartsSteersJumpsGrapplesAndRestarts`, Shift + D → Shift + →)를 같이 바꿨다.
  - **테스트 사양 변경** (10 m/s 를 전제한 단언, 계획서의 "바뀌는 것마다 보고"):
    - `ChainRushCircuitTests.Race_BotRunsThreeLaps_…`: 랩 시간 기대 `LapLength / 10` → `/ 20`(실측 27.26·26.92·26.94 초).
    - `ChainRushReplayTests.Replay_CircuitBotRun_…`: "1700틱은 한 랩이 안 된다(체크포인트 ≥ 2, 완주 0)" → "1700틱은 한 랩과 다음 랩 시작(완주 1)". 틱 수는 다른 1700틱 테스트와 같은 지문이 되도록 그대로 뒀다.
    - `ChainRushWorldTests.IsolatedWorlds_ARunnerStandingOnThePath_…`: "S > 200" → 랩 진행도 > 200(한 랩을 넘겨 S 가 다시 0부터 — 134.3 이 나왔다).
    - `NetReconcileTests` 끝 보정 지레(`FindFinishReplayCase`): 부스터 길이 상수 `SlingTicks` 60 → 120(0.4 + 2 초). 결승 원호에서 놓은 뒤 쏜 첫 부스터는 120틱이 차기 전에 결승선에 닿아(원호 안쪽을 끊는다, `ends<F` 만 나옴) 찾을 수 없었다. 그래서 첫 부스터를 **마지막 스윙 중에** 누른다(스윙 중에는 부스터가 속력을 바꾸지 않고 틱만 줄어든다). 봇은 `ShortRelease`, P 창은 `F - SlingTicks - 12 .. + 2`, 줄 조정은 걸린 틱까지로 제한. 찾은 사례 "rope tweak on ticks 3938-3939, chain presses on ticks 3884 and 4003 … 0.00056 m past … 0.02084 m short (search 2.6 s)".
- **검증** (`feature/pole-swing-v2` 위 커밋 전 작업 트리, 에디터 6000.3.19f1, UTC):
  - 컴파일 `*** Tundra build success (3.98 seconds)`, 테스트 수정마다 `success`(1.91·1.49·1.47 초), `error CS` 없음.
  - 빠른 확인(병합 근거 아님) 서킷·리플레이·롤백·NetReconcile 4 클래스: 1회 `testcasecount="41" result="Failed(Child)" passed="38" failed="3"` (11:19:09Z~11:22:05Z) — 위 랩 시간·리플레이·끝 보정. 2회 `passed="40" failed="1"` (11:27:27Z~11:30:48Z) — 끝 보정 "No case fits … ends<F". NetReconcile 만 `testcasecount="23" result="Passed"` (11:33:59Z~11:34:23Z).
  - EditMode `testcasecount="256" result="Passed" passed="256"` (11:34:41Z~11:34:47Z).
  - `Run PlayMode Tests` 1회차 `testcasecount="145" result="Failed(Child)" passed="141" failed="1" skipped="3"` (11:35:07Z~11:40:55Z): `ChainRushWorldTests.IsolatedWorlds_ARunnerStandingOnThePath_DoesNotChangeTheOtherRun` — "The bot must have run past the spot where the other runner stands. Expected: greater than 200.0d But was: 134.34889221191406d". 원인 위(S 가 랩을 넘김).
  - `Run PlayMode Tests` 2회차 `testcasecount="145" result="Passed" passed="142" failed="0" skipped="3"` (11:42:18Z~11:48:09Z, 350.1초). **서킷 지문 `0xF9A914DA3FAA680B` → `0x12DE39829FD6B3DB`**(사양 변경, 새 기준).
  - `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (11:48:37Z~11:51:52Z), 절차 지문 `0xE81912109A2EB020` 그대로.
  - 방향키 변경 뒤: 컴파일 `*** Tundra build success (4.08 seconds)`. `Run PlayMode Tests` 3회차 `testcasecount="145" result="Passed" passed="142" failed="0" skipped="3"` (12:19:12Z~12:25:02Z, 349.7초), 서킷 지문 `0x12DE39829FD6B3DB` 그대로. `Run Device Input Tests` `testcasecount="3" result="Passed" passed="3" failed="0"` (12:26:17Z~12:26:32Z, `isApplicationActive=True`). 커밋한 코드(`cc50954`)로 `Run Long Tests` 를 다시 돌렸다: `testcasecount="4" result="Passed" passed="4"` (12:29:08Z~12:32:23Z), 절차 지문 그대로. `select-tests -Range dev...HEAD` 는 "전체"(씬·`RunnerMotor`) + Device 1회. 병합 `a4d4981`.
- **사용자 플레이** (2026-10-18): "속도감 좋다". 같은 때 "qa돌리는 일부 테스트용 맵에서 속도는 10.3으로 낮게 나온다" — 이번 값은 서킷 씬에만 넣었고 다른 씬(테스트 벤치·절차 코스)은 코드 기본값 10 m/s 그대로다(10.3 은 경사 보정으로 보인다, 확인 못 함). 후속은 `docs/NEXT_REQUESTS.md` "M1-T 에 넘긴 플레이 피드백".
- **확인 못 한 것**: 사용자 서킷 플레이(속력·회전·부스터 체감). **스윙 시간이 길다**(사용자 같은 날): 지금 코너는 R40 반원이라 20 m/s 로 다 돌면 약 6.3초(10 m/s 때 12.6초)이고, 도로 반폭 6 m 라 줄은 34~46 m 안에서만 바꿀 수 있어(더 감으면 안쪽 가드) 줄로 빨라지는 것도 약 1.18배까지다. R4c(R22 90°)면 약 1.7초. 규칙(스윙 시간 상한 등)은 바꾸지 않았다.
- **버린 대안**: 코드 기본값을 바꾸기 — 무한 모드(동결)·테스트 벤치·절차 지문까지 바뀐다. 카트라이더 km/h 를 그대로 m/s 로(약 55 m/s) — 531 m 랩이 10초가 된다. 리플레이 테스트 틱을 1100 으로 줄여 "한 랩 미만" 유지 — 다른 1700틱 테스트와 같은 지문이라는 연결이 끊긴다.

## 2026-10-18 · R4b 봉 스윙 v2: Shift 스윙이 곧 드리프트, A/D 로 줄 길이, 줄이 짧을수록 빠르고, 스윙 거리만큼 게이지

- **결정 (사용자 승인, 2026-10-18 "진행해보아라", 규칙은 `docs/NEXT_REQUESTS.md` R4b; 범위 밖 테스트 4개는 같은 날 "A ㄱㄱ", 출발선 봉 단언은 "규칙 그대로, 테스트 갱신")**:
  - **입력**: `TickInput.Drift` → `SwingHeld`(왼쪽 Shift 누르고 있음), `ChainActionHeld` → `SwingPressed`(Shift 누른 틱, 래치에서 엣지). Ctrl 은 `ChainActionPressed` 만. 비트 자리는 그대로(8 = 스윙 유지, 32 = 스윙 누름)라 뜻이 바뀌었으므로 `InputLogFile` 형식 버전 2 → 3(옛 파일은 거부).
  - **시뮬 (`RunnerMotor`)**: 드리프트 필드·분기를 지웠다(`driftGrip`·`driftTurnScale`·`driftSpeedScale`·`slideMetersPerSlot`·`IsDrifting`). 봉 스윙은 Shift 누름으로만 건다(`TryHookPole`, 게이지 비용 없음, 그래플 중·스윙 중에는 무시). 범위 좌우 `poleMaxAngle` 95°, 거리 ≤ 반지름 + `poleReach` 6 m. 누르는 동안 유지(누른 틱은 유지로 친다). 스윙 중 조향은 헤딩 대신 줄: 봉 쪽 키는 짧게, 반대는 길게, `ropeRate` 6 m/s, `minRopeLength` 2 m(요청서에 없던 하한 — 0 나눗셈 방지). 속력 = 건 순간 수평 속력 × (건 순간 줄 / 지금 줄), `minSwingSpeedScale` 0.8 ~ `maxSwingSpeedScale` 1.5. 스윙한 거리 `swingMetersPerSlot` 35 m 당 게이지 1칸. 놓으면 그 방향·그 속력으로 직진하고, 달리기 목표를 넘친 부분(`RacerState.SwingCarrySpeed`)은 `swingCarryFalloff` 3 m/s² 로 줄어 목표 아래가 되면 사라진다. 가드는 넘친 속력 없이 끊는다. 옛 출구 던지기(`swingExitSpeed` 17, `swingExitCarryTime` 0.6)는 지웠다. Ctrl 체인 액션은 그래플 강화 → 슬링샷만.
  - **상태**: `RacerState` 에 `SwingHookSpeed`·`SwingHookRadius`·`SwingCarrySpeed`(스냅샷·`SimSnapshotCodec` 에도 — 네트워크 형식 변경). `StateTrace` 에는 넣지 않았다(지문 필드 목록 고정).
  - **HUD**: DRIFT 안내 제거, SWING 안내는 "안쪽 키 줄 짧게 · 바깥 키 길게 · SHIFT 떼면 직진", 봉 표시 `POLE / SHIFT`(걸기에 비용이 없어 늘 민트), 게이지 설명 "filled by swinging".
  - **테스트**: `ChainRushCornerSwingTests` 를 새 규칙으로 다시 썼다(줄·속력 유지와 스윙 거리 게이지, 안쪽/바깥 키와 0.8·1.5 한계, 원호 안 걸기, 범위 밖 누름은 아무 일 없음, 누른 채 범위에 들어오면 안 걸림, 놓으면 직진·초당 3 감속, 가드 끊김, 표현). `ChainRushDriftTests` 는 **지우지 않고 드리프트 테스트 1개만 뺐다** — 나머지 3개(슬링샷 2·그래플 강화 1)가 살아 있는 기능이라서(요청서는 파일 삭제였다. 범위를 좁힌 변경). 롤백 봇은 `DriftBot` → `SwingBot`(슬링·스윙·줄·놓은 뒤 4지점). Device `Input_ShiftAndCtrl_…` 은 R30 테스트 도로에서 Shift 걸기·Shift+D 줄 줄이기·뗌·Ctrl. 네트워크 레이스 C 는 드리프트 구간 → 조향 흔들기 구간(`WeaveWindow`, `[Explicit]` 이라 **돌리지 않았다**, M5).
  - **`NetReconcileTests` 끝 보정 4개의 지레**: 드리프트(한 틱 약 0.012 m)를 대신할 직선의 한 틱 지레가 없어(조향은 0.3 mm 이하) 부스터(슬링샷)로 바꿨다. 게이지는 런 안에서 채운다 — `PoleSwingBot`(서킷 봇 + 원호마다 스윙, 원호 끝 `LongRelease` 40 m 또는 `ShortRelease` 4 m 전에 놓음). 결승선이 마지막 원호 출구라 결승은 달리는 중에 넘는다. 결승 지점의 미세 조정은 마지막 스윙 중 두 틱 봉 쪽 조향(줄 0.24 m 짧게 → 한 틱 약 1.2 mm 더 감)을 놓는 자리로 한다. ① 서버만 결승: P 에 부스터, F-1 에 다시 누름(P 의 부스터가 아직 돌아 무효), 서버는 F 에 그 누름을 반복해 두 번째 부스터로 F 에 결승. ② 클라만 결승: F 에 누름, 서버는 F-1(안 누름) 반복. ③ 교정 재실행이 F 에 끝남: 클라가 D-1 에 봉 쪽, D 에 반대쪽(줄은 그대로), 서버는 D 를 놓쳐 줄이 0.24 m 짧고 빠르다. 지연은 `MissLead + 3` 틱(예측기 창 512 안).
  - **서킷 봉 표시 단언**(`ChainRushCircuitTests.Scene_CornerPoles_…`): 95°·+6 m 에서는 원호 출구 뒤 약 3.5 m 동안 방금 지난 봉이 범위 안이고, 출발선이 마지막 원호 출구라 출발선에서 마지막 봉이 후보다. 사용자 결정으로 규칙을 두고 단언을 바꿨다(**사양 변경**): 출발선 = 마지막 원호의 봉, 직선 한가운데 S 60 = 없음, S 127 = 첫 봉. 출구 직후 Shift 가 지난 봉에 걸리는 것이 플레이에 괜찮은지는 M1-T 에서 본다.
- **이유**: 사용자 설계(요청서 R4b 경위) — 스윙이 이 게임의 드리프트이고, 길게 돌아 게이지를 많이 채울지 짧게 넘길지가 경쟁 포인트. 시뮬 봇들은 드리프트·스윙을 쓰지 않아 지문 두 개가 그대로여야 하고, 그대로였다.
- **검증** (`feature/pole-swing-v2`, 커밋 전 작업 트리, 에디터 6000.3.19f1):
  - 컴파일: Editor.log `*** Tundra build success (34.61 seconds)`, `error CS` 없음. 서킷 테스트 수정 뒤 `*** Tundra build success (51.19 seconds)`.
  - EditMode `testcasecount="256" result="Passed" passed="256" failed="0"` (07:11:43Z~07:11:50Z).
  - 빠른 확인(병합 근거 아님): 코너 스윙·체인·롤백·NetReconcile 4 클래스 `testcasecount="43" result="Passed" passed="43"` (07:12:17Z~07:14:00Z). 찾기 결과: ① "rope tweak on ticks 7706-7707, chain presses on ticks 7819 and 7878: on tick 7879 the server's repeated press fires a second slingshot and finishes 0.00073 m past the line, the client's own controls leave it 0.03057 m short (search 1.9 s)" ② "rope tweak on ticks 7666-7667, chain press on tick 7922: … 0.01613 m short … 0.00194 m past (search 0.8 s)" ③ "the server missed tick 7890 … chain press on tick 7924 … own run stays 0.05850 m short …, the server's repeated controls 0.01608 m short, … corrected … 0.00200 m past (search 0.2 s)", 지연 37 틱. 코너 스윙 "rope 33.09 m, hook speed 10.00 m/s, speed 10.00..10.00 m/s, rope error 0.000 m, … swung 63.8 m, gauge +1.823". 롤백 "sling@10, swing@673, rope@682, carry@1233 replay identically".
  - `tools/select-tests.ps1`(커밋 전 작업 트리 포함): 전체 + "이 브랜치는 Long 도", "병합에 Run Device Input Tests 를 1회".
  - `Run PlayMode Tests` 1회차 `testcasecount="145" result="Failed(Child)" passed="141" failed="1" skipped="3"` (07:14:58Z~07:21:29Z): `Scene_CornerPoles_StandOnEveryArcCenter_AndTheOneInReachIsOffered` — "On the start line both poles are out of reach. S 0.01, D 0.00, … Expected: False But was: True" (`ChainRushCircuitTests.cs:262`). 원인은 위 서킷 봉 표시 단언(95° 규칙의 결과), 사용자 결정 뒤 단언을 바꿨다.
  - `Run Long Tests` `testcasecount="4" result="Passed" passed="4" failed="0"` (07:22:23Z~07:25:39Z), 절차 지문 `0xE81912109A2EB020` 그대로. 1회차 게이트와 서킷 테스트 수정 사이에 돌렸다 — 그 뒤 바뀐 것은 `ChainRushCircuitTests.cs` 뿐(Long 묶음 아님, 런타임 아님).
  - `Run PlayMode Tests` 2회차 `testcasecount="145" result="Passed" passed="142" failed="0" skipped="3"` (07:31:45Z~07:38:27Z, 402.4초, skipped 3 은 `[Explicit]` 네트워크 레이스). 서킷 지문 `0xF9A914DA3FAA680B` 그대로.
  - `Run Device Input Tests` `testcasecount="3" result="Passed" passed="3" failed="0"` (07:51:38Z~07:51:53Z, `isApplicationActive=True`, 사용자가 에디터를 앞에 둠).
- **사용자 서킷 플레이** (2026-10-18): "확인했다. 속도가 여전히 많이 느리다. 또한 a/d로 조향되는 범위가 너무 많다", 이어서 "스윙하는 시간이 너무 길다". 스윙 규칙은 그대로 두고 속력·회전 수치를 M1-T 에서 앞당겨 다룬다(사용자 "추천대로 ㄱㄱ", `feature/tuning-m1`).
- **확인 못 한 것**: 네트워크 레이스 C 의 `WeaveWindow` 가 순위 역전을 만드는지(M5). 무한 모드도 드리프트가 없어지고 Shift 스윙이 됐다(동결, 게이트의 무한 모드 테스트는 통과).
- **버린 대안**: 끝 보정 4개를 `[Ignore]` 로 미루기 — 끝 보정이 게이트에서 빠진 채 병합된다. 조향을 지레로 — 한 틱 효과가 0.3 mm 이하라 찾을 수 없다. 테스트 전용 감속 입력 — 게임에 없는 기능을 테스트 때문에 넣는 것. 봉 범위를 89° 로 — 원호 안에서 걸 수 없게 된다. 지난 봉 제외 규칙 — 범위가 커진다(사용자는 규칙 그대로를 골랐다).

## 2026-10-18 · R5 코너 봉 스윙 P2 (표현): 봉 모델은 실행 중에 원호에서 만들고, 줄은 봉 꼭대기를 문다

- **결정 (사용자, 2026-10-18 "진행하자. 권장대로 하겠다"(5-1 ②), 구체 계획에 "진행해라", `docs/NEXT_REQUESTS.md` R5)**:
  - **봉 위치**: `Centerline.GetPoles(List<Vector3>)` — 원호 조각마다 중심(시뮬의 봉 `TryFindPole` 과 같은 점)을 조각 순서로 붙인다. 읽기만 한다.
  - **봉 모델**: `CircuitRace.Initialize` 의 `PlacePoles` 가 원호마다 `Corner pole i` 를 둔다 — 난간 재질의 기둥(높이 `SwingLook.PoleHeight` 8 m, 두께 `PoleThickness` 0.7 m) + 조명 재질의 머리. 출발선 표지(`AddMarker`)처럼 기본 도형으로 만들고 **충돌체를 끄고 지운다** — 시뮬은 그대로다. 씬·프리팹·새 파일 없음.
  - **줄 끝**: 봉 스윙 중 줄(`LineRenderer`)은 `SwingLook.PoleTop(RacerState.SwingCenter)`(봉 꼭대기)를 문다. **무한 모드는 그대로** 안쪽 난간을 문다(`SwingLook.RailPoint`) — 무한 모드에는 봉 모델이 없고(R6, 동결), 긴 줄을 `ChainVisual` 이 어떻게 그리는지 확인하지 않았다.
  - **걸 수 있는 봉 표시**: `GrappleController.LateUpdate` 가 달리는 중·그래플 아님·스윙 아님·무한 모드 아님일 때 `motor.TryFindPole` 로 `PoleCandidate` 를 남기고, `ChainRushHud` 가 앵커 표시와 같은 꺾쇠 + `POLE / CTRL  43m` 를 봉 꼭대기에 그린다. 게이지 1칸이 있으면 민트, 없으면 흐린 색(눌러도 아무 일 없음).
  - **테스트**: `ChainRushCornerSwingTests.SwingLook_WhileSwinging_…HooksTheInnerRail` → `…HooksThePoleTop` — 줄 끝이 난간이라는 단언을 봉 꼭대기로 바꿨다(**사양 변경**, 통과시키려고 고친 것이 아니다). 새로 `ChainRushCircuitTests.Scene_CornerPoles_StandOnEveryArcCenter_AndTheOneInReachIsOffered`(원호 수 = 봉 수, 위치·머리 위치, 충돌체 0, 출발선에서는 후보 없음, S 127 에서는 첫 봉이 후보), EditMode `CenterlineTests.GetPoles_EveryArcInOrder_StraightsHaveNone`.
- **이유**: 5-1 ② 는 씬 수정이 없고, 트랙을 늘려도(사용자 2026-10-18 "트랙도 최소 3가지") 원호만 있으면 봉이 따라온다. 봉 점이 시뮬과 같은 `Centerline` 에서 나오므로 그려진 봉과 걸리는 봉이 어긋날 수 없다.
- **진행 중 있었던 일**: 새 서킷 테스트 1회차 실패 `Before the first arc its pole must be offered. S 527.34, D 0.00, H 0.94, … velocity (0.00, 0.00, 0.00)` — 테스트가 러너를 S 124 로 옮겼는데 그 자리는 점프 틈(S 119~125) 위라 떨어져 복귀했다. 틈 뒤 S 127(원호 13 m 전, 72°, 42.1 m)로 고쳤다. 코드 결함이 아니라 테스트 위치 잘못이다.
- **검증** (`feature/corner-pole-look`, 커밋 전 작업 트리, 에디터 6000.3.19f1, `isApplicationActive=False`):
  - 컴파일: Editor.log `*** Tundra build success (23.14 seconds)`, 리플렉션으로 새 멤버 4개(`GetPoles`·`PoleTop`·`HasPoleCandidate`·`PoleCount`) 확인, Console Error 0.
  - EditMode `testcasecount="256" result="Passed" passed="256" failed="0"` (04:26:39Z~04:26:45Z).
  - 빠른 확인(병합 근거 아님): 서킷·코너 스윙 두 클래스 `testcasecount="12" Failed(Child) passed=11 failed=1`(위 실패, 04:27:09Z~04:28:57Z) → 고친 뒤 서킷 클래스 `testcasecount="6" result="Passed"` (04:30:21Z~04:31:35Z).
  - `tools/select-tests.ps1`(커밋 전 작업 트리 포함): `GrappleController.cs`·`Centerline.cs` → 전체, "②에서 Run Course Sweep 도 1회".
  - `Run PlayMode Tests` `testcasecount="144" result="Passed" passed="141" failed="0" skipped="3"` (04:32:00Z~04:38:26Z, 386.0초, skipped 3 은 `[Explicit]` 네트워크 레이스). 서킷 지문 `0xF9A914DA3FAA680B` 그대로(리플레이, 격리 월드 두 벌).
  - `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (04:38:54Z~04:42:10Z). 절차 지문 `0xE81912109A2EB020` 그대로.
  - `Run Course Sweep` `testcasecount="1" result="Passed" passed="1" failed="0"` (04:42:39Z~04:58:13Z, 934.0초). 20 시드 모두 1400 m·health 3·escapes 0, 가장 느린 스텝 8.08 ms(시드 15). R4 때는 2.95 ms 였다. 이번 변경은 무한 모드의 틱 경로에 닿지 않는다(봉 모델은 서킷만, 후보 조회는 무한 모드에서 건너뜀). 그래서 에디터 쪽 흔들림으로 보지만 **원인은 확인하지 못했다**.
  - Device 는 돌리지 않았다 — 입력 장치 경로(`KeyboardMouseInputSource`, 메뉴 키)를 바꾸지 않았다(§9-2).
- **2단계 — 봉이 보이지 않았다** (사용자 플레이 2026-10-18 "애초에 서킷에 봉이 보이지 않았었다"):
  - 원인: Play 중 조회로 봉 2개가 맞는 자리에 켜져 있음을 확인했다(`Corner pole 0` 중심 (40, 4, 140)). 하지만 기둥 재질 `M_Frame` 이 RGBA(0.035, 0.090, 0.120) — 배경과 같은 검정 — 이고 0.7 m × 8 m 라, 카메라 캡처에서 출발선에서는 머리카락 굵기, 코너 앞에서는 허공에 뜬 어두운 기둥이었다. 테스트는 오브젝트의 존재·위치만 봤다.
  - 고침: 기둥을 조명 재질(민트 발광)로, 두께 `PoleThickness` 1.6 m, 봉 점 아래 `PoleDepth` 20 m 부터 위 `PoleHeight` 20 m 까지(인필드가 빈 공간이라 떠 보이지 않게), 머리 구 3.2 m. 줄은 꼭대기 대신 `BiteHeight` 3 m 의 어두운 띠(`SwingLook.PoleBite`)를 문다 — 줄이 거의 수평이라 반지름이 보인다. HUD `POLE` 표시도 그 띠에. 사용자에게 실행 중에만 바꾼 미리보기 캡처를 먼저 보였다.
  - 테스트: 서킷 봉 테스트에 크기 단언(기둥 높이 = 20 + 20 m, 두께 ≥ 1.5 m, 꼭대기 = `PoleTop`)과 띠 위치(`PoleBite`)를 더했다. 코너 스윙 테스트의 줄 끝 높이는 `BiteHeight`.
  - 검증 (같은 브랜치, 커밋 전): 컴파일 `Tundra build success (19.42 seconds)`. Play 캡처로 두 지점(출발선, 첫 원호 25 m 전)에서 빛나는 기둥과 띠가 보임. 서킷·코너 스윙 두 클래스 `testcasecount="12" result="Passed"` (06:12:20Z~06:14:11Z). `Run PlayMode Tests` `testcasecount="144" result="Passed" passed="141" failed="0" skipped="3"` (06:14:43Z~06:21:52Z, 429.5초), 서킷 지문 `0xF9A914DA3FAA680B` 그대로. `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (06:22:20Z~06:25:38Z), 절차 지문 `0xE81912109A2EB020` 그대로. Sweep 은 다시 돌리지 않았다 — 2단계는 `Centerline` 을 건드리지 않았고, 1단계의 Sweep(04:42Z) 이 같은 `Centerline` 코드로 통과했다.
- **확인 못 한 것**: 2단계 모양이 플레이에 맞는지 — **사용자 서킷 플레이**. 무한 모드의 봉(R6).
- **같은 날 이어진 설계 (구현은 R4b)**: 플레이 피드백 6건(속도, 그래플 거리·반복, A/D 과함, 드리프트 무의미, 부스터 짧음, 봉 원운동이 안 느껴짐)을 조사하다 봉 스윙 규칙을 다시 설계했다 — `docs/NEXT_REQUESTS.md` R4b.
- **버린 대안**: 봉을 씬에 굽기(5-1 ①) — 씬 수정 브랜치가 되고 트랙마다 다시 굽는다. 봉 모델을 새 컴포넌트·프리팹으로 — 첫 사례 형식이 늘고, 출발선 표지와 같은 방식으로 충분하다. 봉에 충돌체 — 인필드 안이라 닿을 일은 없지만 시뮬과 지문이 바뀔 수 있다. 무한 모드도 줄을 봉으로 — 봉이 보이지 않는 곳으로 줄이 60~120 m 뻗는다(R6 에서 함께).

## 2026-10-15 · R4 코너 봉 스윙 P1 (시뮬): 원호 중심의 봉에 걸고, 줄 길이 고정·관성·Ctrl 유지 동안 돈다

- **결정 (사용자 승인, 2026-10-15 "ㄱㄱ", `docs/NEXT_REQUESTS.md` R4, 규칙은 `docs/PROPOSAL_CORNER_POLE.md` 2절 — 2026-10-14 사용자 결정 7개)**: 지금 코너 스윙(T2d: 드리프트 중 원호 중심에 걸고 14 m/s 로 2초)을 **봉 스윙으로 대체**한다. 표현(봉 모델, 줄 끝을 봉으로, 걸 수 있는 봉 표시)은 R5.
  - **봉 찾기** `Centerline.TryFindPole(world, heading, maxAngle, extraReach, out pole)`: 원호 조각마다 중심에 봉이 하나 있다. 진행 방향 좌우 `poleMaxAngle`(75°) 안, 거리 ≤ 원호 반지름 + `poleReach`(5 m)인 봉 중 수평 거리로 가장 가까운 것을 고르고, 같으면 앞 조각을 고른다. 트랙만 읽는 순수 함수다. 체인 액션 때만 부르므로 조각 전체를 본다.
  - **체인 액션 우선순위** (`RunnerMotor.ChainAction`): 그래플 강화 → 봉 스윙 → 슬링샷. 드리프트·땅 위·안쪽 조향 조건은 없앴다. `RunnerMotor.TryFindPole` 을 공개해 표현(R5)과 테스트 봇이 같은 판정을 쓴다.
  - **도는 동안** (`SwingStep`): 줄 길이 = 건 순간의 수평 거리(`RacerState.SwingRadius`, 이제 양수). 봉 위치는 `RacerState.SwingCenter`(새 필드)이고, 원점 이동 때 함께 옮긴다. 접선 방향은 지금 바라보는 쪽이다. 가속 없음(`acceleration = 0`), **조향 무시**(회전율 0), 시간 제한 없음. `SwingTicks` 는 "남은 틱"에서 "지난 틱"(0 = 스윙 아님)으로 뜻이 바뀌었다.
  - **끊김**: Ctrl 을 떼면 지금처럼 접선 최소 17 m/s + 0.6초 캐리. 가드에 닿으면 가속 없이 끊기고 평소 가드 감속을 받는다. 그래플이 붙으면 지금처럼 끊긴다. 누름은 그 틱에서 누르고 있는 것으로 친다 — 틱 사이에 눌렀다 뗀 탭도 슬롯만 쓰고 끝나지 않게 한다.
  - **입력**: `TickInput.ChainActionHeld`(레벨). `TickInputCodec` 비트 32, `InputLatch`, `KeyboardMouseInputSource`(왼쪽 Ctrl 누르고 있음), `InputLogFile` 버전 2. 버전 1 파일은 거절한다(저장된 `.phil` 파일 0개 확인).
  - **스냅샷·와이어**: `RacerState.Snapshot.SwingCenter`, `SimSnapshotCodec` 이 `SwingRadius` 뒤에 씀.
  - **설정값**: `swingSpeed`·`swingAcceleration`·`swingMaxTime` 을 지우고 `poleMaxAngle`·`poleReach` 를 더했다. 지운 필드는 씬 YAML 에 줄로 남아 있다. Unity 는 무시하고, 다음에 씬을 저장할 때 사라진다. 씬은 이번에 건드리지 않았다.
  - HUD 문구 `SHIFT` → `CTRL`, `COURSE.md` 4-5 ② 규칙 원본.
- **테스트**:
  - `ChainRushCornerSwingTests` 를 다시 썼다(4 → 6건). 원호 직전에서 걸면 줄 길이·속력 유지 / 원호 안에서는 봉이 옆이라 슬링샷 / R + 5 밖은 슬링샷 / Ctrl 을 떼면 17 m/s / 계속 잡으면 원호를 지나 가드에서 가속 없이 끊김 / S1 연출 — 거는 위치만 원호 직전으로 바꿨다.
  - 롤백 테스트의 `DriftBot` 은 봉이 범위에 들면 누르고, 원호를 지나 직선에 오면 뗀다.
  - 코덱 테스트는 6비트 전 조합으로 바꿨다(사양 변경). EditMode 는 봉 찾기 7건(테스트 케이스 12개)과 누름 유지 입력을 더했다.
  - 입력을 복사하는 테스트 3곳(Replay·NetReconcile·NetworkRace)은 새 비트를 그대로 넘긴다.
- **검증** (`feature/corner-pole-sim`, 커밋 전 작업 트리, 에디터 6000.3.19f1):
  - 컴파일 1회차 `KeyboardMouseInputSource.cs(35,79): error CS0103: The name 'chainHeld' does not exist` — 제 일괄 치환이 줄바꿈(CRLF) 때문에 일부만 들어갔다. 고친 뒤 `Tundra build success`, Console Error 0.
  - EditMode `testcasecount="255" result="Passed" passed="255" failed="0"` (06:46:02Z~06:46:14Z).
  - 코너 스윙 6건 1회차 `Failed(Child) passed=5 failed=1` (06:46:50Z~06:47:31Z): `Swing_HookedBeforeTheArc_KeepsItsRopeAndItsSpeed` — `Nothing slows the swing down on flat ground. Expected: greater than 8.76517868f`. 기록은 걸 때 9.07 m/s → 스윙 중 6.02 m/s. **원인**: 스윙 중에도 조향 입력이 헤딩을 먼저 돌려, 속도가 새 헤딩 기준으로 갈리고 옆 그립이 깎았다. 설계서 2-3 "조향 입력은 무시된다" 대로 스윙 중 회전율을 0 으로 고쳤다.
  - 고친 뒤 6건 `Passed 6/6` (06:51:46Z~06:52:20Z): 줄 33.09 m 오차 0.000 m, 속력 9.02~9.07 m/s, 최대 옆 위치 3.09 m. 계속 잡으면 s=99.5(원호 끝 77.1)에서 안쪽 가드(d=5.59)에 닿아 끊김, 그 뒤 5.98 m/s.
  - `Run PlayMode Tests` `testcasecount="143" result="Passed" passed="140" failed="0" skipped="3"` (06:52:44Z~06:59:07Z, 382.7초). 서킷 지문 `0xF9A914DA3FAA680B` 그대로. 롤백 `drift, sling and corner swing snapshots sling@10, swing@570, drift@2149 replay identically`.
  - `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (06:59:33Z~07:02:49Z). 절차 지문 `0xE81912109A2EB020` 그대로. 봇은 체인 액션을 쓰지 않으므로 그대로여야 정상이다(설계서 4절).
  - `Run Device Input Tests` 1회차 `Failed(Child) passed=1 failed=2` (07:03:12Z~07:03:17Z, 에디터 `isApplicationActive=False`): `Input_KeyboardAndMouse_…`(`ChainRushTests.cs:127`)·`Input_ShiftAndCtrl_…`(`:194`) 둘 다 `Expected: True But was: False` — 가상 Enter 뒤 카운트다운이 시작되지 않음. Ctrl 단계 전이고, 메뉴 키 경로는 바꾸지 않았다.
  - 같은 코드로 사용자가 Unity 를 앞에 둔 2회차 `Passed 3/3` (07:04:53Z~07:05:08Z, `isApplicationActive=True`). **판단**: 에디터 포커스가 없으면 가상 키보드 이벤트가 게임에 닿지 않는다(추정. 포커스 유무만 다르고 결과가 갈렸다). 1회차 실패는 지우지 않고 여기 남긴다.
  - `Run Course Sweep`: `Centerline.cs` 를 바꿨으므로 §9-2 가 Sweep 1회를 요구한다(`tools/select-tests.ps1 -Range dev...HEAD` 출력 "참고: ②에서 Run Course Sweep 도 1회"). 1회차는 2026-10-15 07:06:36Z 에 시작했지만, 사용자 "중단하고 나중에 돌리겠다"에 따라 07:16:21Z 에 Play 를 멈춰 중단했다(결과 없음).
  - `Run Course Sweep` 2회차 (커밋 `271b397`, 같은 코드): `testcasecount="1" result="Passed" passed="1" failed="0" skipped="0"` (2026-10-17 06:57:29Z~07:13:03Z, 933.8초, `isApplicationActive=False`). 20 시드 모두 1400 m·health 3·escapes 0, 가장 느린 스텝 2.95 ms(시드 12).
  - **네트워크 레이스·플레이어 재빌드는 하지 않았다** (2026-10-15 로드맵: M5). 와이어 형식이 바뀌었으므로(`SimSnapshotCodec`, `TickInputCodec`) 임시 폴더의 레이스 플레이어(R2 코드로 빌드)는 낡았다. M5 에서 다시 빌드한다.
- **확인 못 한 것**: 실제 느낌(관성만으로 도는 R40 180° 가 재미있는지, 75°·+5 m 가 맞는지) — R5 뒤 사용자 플레이. 서킷에서 봉 스윙이 노면 틈 위를 지날 때. 무한 모드 원점 이동 중 스윙(`SwingCenter` 이동)은 테스트로 돌리지 않았다.
- **버린 대안**: 봉을 트랙 S 로 기억하기 — 루프·절차 코스의 조각 재활용에서 조각 찾기가 더 복잡하다. 스윙 중 조향 허용 — 속력이 새는 것이 테스트로 드러났다. 탭(누름만, 누르고 있음 없음)을 무시하기 — 슬롯만 쓰고 아무 일도 안 일어난다.

## 2026-10-15 · 플레이 경험 순서 로드맵: M1~M5, 실제 네트워크 레이스는 M5 로, 마일스톤에 사용자 플레이 확인

- **결정 (사용자, 2026-10-15 "전반적인 게임 돌아가는 것에 집중을 해보자" → 제안에 "a ㄱㄱ, 추천대로 / b ㅇㅇ / c 승인한다")**:
  - **로드맵** (`docs/DESIGN.md` §5-1): M1 한 판이 끝까지 돈다(v0.2: 낙하 복귀, 코너 봉 스윙, 체감 튜닝) → M2 다시 할 이유(v0.3: 최고 기록, 내 고스트, 복귀 연출) → M3 고를 거리(v0.4: 트랙 2~3개, 트랙 선택) → M4 같이 달리는 맛(v0.5: **오프라인 AI 상대**, 순위) → M5 멀티 재개(실제 네트워크 레이스, C1 마무리, C2~C4). 무한 모드는 동결하고 M3 뒤에 다시 판단한다. 옛 기술 순서(P0~P3)는 §5-2 에 남겼다.
  - **규칙** (`CLAUDE.md` §9-2 ③): 마일스톤 조건에서 네트워크 레이스 1회를 M5 부터로 미루고, **사용자 플레이 확인**(그 마일스톤 완료 조건을 사용자가 직접 플레이해 확인한 말)을 넣었다. ② 병합 조건은 그대로다(결정성 테스트·`Net` 카테고리는 게이트 안).
  - **요청서** (`docs/NEXT_REQUESTS.md`): R4 검증에서 플레이어 재빌드·네트워크 레이스 A·B·C 를 뺐다(와이어 형식이 바뀌어도 M5 로). 체감 튜닝 M1-T 를 표에 넣었다. 후보를 마일스톤별로 다시 묶었다.
- **이유**: 사용자 질문 두 개. "네트워크 관련해서도 결국 싱글플레이를 완성하고 그걸 진행하는 게 낫지 않을까", "전반적인 게임 돌아가는 것에 집중". 확인한 사실:
  - 서킷에는 시작 화면·카운트다운·3랩·랩 시간·결과 화면이 있다(`ChainRushHud.cs:121,245-280`). 하지만 최고 기록 저장이 없고(`PlayerPrefs`·best 관련 코드 0건), 트랙은 `Circuit_Stadium` 하나이며, 오프라인 상대가 없다.
  - 네트워크 검증은 두 층이다. 실제 레이스(플레이어 빌드 약 2분 + 레이스 A·B·C 약 5분, 창 포커스에 흔들림 — 2026-10-14 R2, 2026-10-15 R9 1회차 실패)와 프로세스 안 결정성 테스트(게이트 안 약 2분)다. 앞쪽만 미뤄도 멀티의 바탕인 결정성은 지켜진다.
  - "와이어 형식이 바뀌면 레이스 A·B·C" 는 CLAUDE.md 규칙이 아니라 요청서(R2·R4)의 검증 항목이었다.
- **M4 를 오프라인 AI 상대로 둔 이유**: M2 고스트와 M4 AI 상대는 네트워크 없이 멀티의 재료(입력 재생, 상대 표시, 순위)를 게임 기능으로 쓴다. 그래서 지금까지 만든 결정성 코드가 버려지지 않는다.
- **미뤄 둔 위험**: R2 브랜치의 예측기 고장(서버가 클라를 앞지름, 2026-10-14)과 R9 레이스 A 1회차 실패(2026-10-15)는 풀리지 않았다. M5 의 첫 항목이다. M5 전에 와이어 형식을 바꾸는 작업(R4 등)이 쌓이면, 그 사이의 실제 전송 회귀는 M5 에서 몰아 찾는다.
- **버린 대안**: 실제 레이스를 와이어 변경 때만 A 1개로 줄이기 — R2·R4 처럼 와이어를 바꾸는 작업이 이어져 비용이 남는다. 결정성 테스트까지 빼기 — 비결정 코드가 섞이면 M5 에서 원인을 찾기 가장 비싸다. 멀티를 먼저 — 사용자가 게임 완성을 우선했다.

## 2026-10-15 · R3 맵 정리 3단계 + 네모 통일 2단계: 작업대는 첫 구간만, 러너 모형은 프리팹 `ArmoredRunner` 하나

- **결정 (사용자 승인, 2026-10-15 "ㄱㄱ, 네가 저장" / 작업대 단계 재승인 "A로 진행하라", `docs/NEXT_REQUESTS.md` R3, 3-1 "남긴다", 3-2 ①)**:
  - **첫 프리팹** `Assets/_Project/Prefabs/ArmoredRunner.prefab` (폴더도 새로 만듦): 서킷 씬의 `Armored Runner` 33개 오브젝트를 그대로 담는다. 스크립트는 없고, 꺼진 BoxCollider 25개도 그대로다. 프리팹 형식(이 프로젝트의 첫 사례): 이름은 PascalCase, 모형만 담고 동작 컴포넌트는 씬의 러너 루트에 둔다. 인스턴스 이름은 씬에서 `Armored Runner` 로 둔다.
  - **서킷**: `SaveAsPrefabAssetAndConnect` 로 프리팹을 만들고 그 자리에서 인스턴스로 연결했다. 꺼진 둥근 `Runner Visual` 은 지웠다.
  - **절차**: `ConvertToPrefabInstance`(계층 기준 짝 맞춤, 루트 이름 유지)로 같은 프리팹 인스턴스가 됐다. 참조 10개(`GrappleController.ropeOrigin`, `RunnerAnimation` model·관절 6, `RunnerTilt.body`, `ChainVisual.origin` ×2)가 인스턴스를 가리킨다. 덮어쓴 값은 루트 위치·회전·이름뿐이다. 꺼진 `Runner Visual` 은 지웠다. 두 씬의 `Armored Runner` 는 33개 오브젝트의 이름·켜짐·위치·회전·크기·메시·머티리얼이 같았다(씬 grep 비교).
  - **작업대**: 프리팹 인스턴스를 놓고 `RunnerTilt.body`·`GrappleController.ropeOrigin` 을 다시 연결했다. 둥근 `Runner Visual`, Sector 02~09, 결승 게이트 4개를 지웠다. `GrappleController.anchors` 는 `Link Anchor 1` 하나, `ChainRushGame.targets` 는 (0, 1.1, 18) 하나만 남겼다. 배경 빌딩은 남겼다.
  - **빌더** `ChainRushSceneBuilder.CreateTestBenchScene`: 구간 1개(`Sections = 1`)만 만들고, 결승 게이트는 만들지 않으며, 모형은 프리팹(`RunnerModelPath`)이다. 쓰지 않게 된 `Visor` 머티리얼 생성 줄은 지웠다.
  - **테스트**: `Course_TimedJumpsAndHeldGrapple_ReachesFinish` 삭제 — 직선 결승은 작업대에만 있었고, 완주는 서킷 3랩 테스트가 본다. `ChainRushCornerSwingTests:134`·`ChainRushInputSourceTests:161,189` 는 모형을 `Find("Runner Visual")` 대신 `RunnerTilt.Body` 로 찾는다. 찾는 방법만 바꿨고 단언은 그대로다.
  - 문서: ARCHITECTURE 씬 줄과 열기 메뉴. 열기 메뉴의 `Open Prototype Scene` 은 2026-10-14 이름 변경 때 남은 옛 이름이라 `Open Test Bench Scene` 으로 고쳤다. HANDOFF 씬 줄.
- **이유**: 사용자 "네모로 맞춘다", "테스트하는 걸 보았을 때 원치 않는 동그란 플레이어가 보였다". 작업대에 남긴 Sector 01(발판 -8~48 m, 앵커 (0, 10, 56), 표적 z 18)은 그래플 틈 2건(z 44, x 50), `Restart_AfterAttackAndFall`·`Attack_ScriptedPressNearTarget`(z 15), `Damage_RepeatedContact`, 가드 테스트(발판 1개 이상)가 쓰는 전부라서 테스트를 옮길 필요가 없었다(`ChainRushSceneBuilder.cs` 구간 루프, 각 테스트 본문).
- **진행 중 있었던 일**:
  - 작업대 단계의 첫 `Unity_RunCommand` 는 권한 분류기에 막혔다. 사용자가 다시 승인한 뒤 실행했다.
  - 다시 실행했을 때는 지우기 전 참조 검사가 13건을 잡아 저장 없이 멈췄다. 13건은 모두 부모 Transform 의 `m_Children` 목록, 곧 계층 자체였다. `m_Children`·`m_Father` 를 검사에서 빼고, 씬을 디스크에서 다시 열어(dirty=False 확인) 처음부터 다시 했다.
- **검증** (`feature/scene-test-bench-strip`, 에디터 6000.3.19f1):
  - 컴파일: `Tundra build success (14.96 seconds)`, Console Error 0.
  - 씬 grep: 세 씬 모두 `Runner Visual`·`Helmet` 이름 0개, 프리팹 GUID 참조가 있다. 작업대의 `Finish` 0개.
  - EditMode `testcasecount="242" result="Passed" passed="242" failed="0"` (05:44:53Z~05:44:59Z).
  - `Run PlayMode Tests` `testcasecount="141" result="Passed" passed="138" failed="0" skipped="3"` (05:45:18Z~05:51:34Z, 376.2초 — 142 → 141 은 지운 1건, skipped 3 은 `[Explicit]` 네트워크 레이스). 지문 테스트 포함.
  - `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (05:52:28Z~05:55:44Z, 196.2초).
  - 작업대 `Main Camera` 캡처: 네모 러너, 발판·표적·앵커 각 1개.
- **확인 못 한 것**: 고친 빌더로 작업대를 새로 만들지는 않았다. 빌더 결과가 지금 씬과 정확히 같은지는 모른다. 네트워크 레이스는 이번 변경 경로(`RunnerTilt.Body` → 고스트 복제)가 같아서 다시 돌리지 않았다. 다만 서킷 씬의 모형이 이제 프리팹 인스턴스라서, 고스트가 복제하는 대상도 인스턴스다.

## 2026-10-15 · R9 네모 통일 1단계: 네트워크 상대를 캡슐 대신 내 러너 모형의 복제로 그린다

- **결정 (사용자 승인, 2026-10-15 "셋다 좋다", `docs/NEXT_REQUESTS.md` R9)**: `RemoteGhost.Initialize(interpolator, model)` 가 받은 모형을 복제한다. 위치를 따라가는 빈 루트 `Remote racer` 아래에 원본과 같은 로컬 위치·크기로 놓고, 회전은 루트 정면(identity)으로 둔다. 콜라이더는 끈 뒤 지운다(이전 캡슐과 같다). `RaceClient.Begin` 은 `game.Player` 의 `RunnerTilt.Body` 를 넘긴다. `RunnerTilt` 가 없으면 `InvalidOperationException` 을 던진다. 공개 API 추가: `ChainRushGame.Player`, `RunnerTilt.Body` (읽기 전용).
- **이유**: 사용자 "네모로 맞춘다". 서킷의 `RunnerTilt.body`·`RunnerAnimation.model` 은 둘 다 `Armored Runner` 다(`ChainRushCircuit.unity:114962,114981`). 씬의 모형을 복제하면 새 에셋도 씬 수정도 없고, 모형 원본이 한 곳으로 남는다. `Armored Runner` 부품 25개에는 BoxCollider 가 붙어 있지만 모두 `m_Enabled: 0` 이다(서킷·절차 씬 grep).
- **한계**: 복제본은 복제한 순간의 자세 그대로다. `RunnerAnimation` 이 움직이지 않는다. 상대의 몸 기울기·관절 연출은 범위 밖이다.
- **검증** (`feature/net-boxy-ghost`, 커밋하지 않은 작업 트리, 에디터 6000.3.19f1, 에디터 포커스는 모든 실행에서 `isApplicationActive=False`):
  - 컴파일: Editor.log `*** Tundra build success (25.75 seconds)`. 리플렉션으로 새 멤버 4개가 올라온 것을 확인했다. Console Error 0, Warning 20 은 모두 `Deleting invalid font reference.` 다.
  - `tools/select-tests.ps1` 기본 실행: `ChainRushGame.cs → 전체 (코어 시뮬)`. 빠른 확인 `NetRemoteTests` `testcasecount="11" result="Passed" passed="11"` (05:03:30Z, 새 테스트 `Ghost_CopiesTheLocalModelWithoutColliders_AndFollowsTheRacer` 포함).
  - EditMode `testcasecount="242" result="Passed" passed="242" failed="0"` (05:03:47Z~05:03:55Z).
  - `Run PlayMode Tests` `testcasecount="142" result="Passed" passed="139" failed="0" skipped="3"` (05:04:20Z~05:11:22Z, 421.9초, skipped 3 은 `[Explicit]` 네트워크 레이스).
  - `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (05:12:48Z~05:16:06Z, 198.2초).
  - 플레이어 빌드: `BuildPipeline.BuildPlayer`(서킷 씬만, `BuildOptions.None`) `result=Succeeded errors=0 warnings=485` (05:17:40Z). 빌드가 바꾼 `Assets/Settings/` 3개와 `ProjectSettings/UnityConnectSettings.asset` 은 `git restore -- <파일>` 로 되돌렸다. `ProjectSettings.asset` 은 빌드 전 사본으로 되돌렸다. git status 와 바이트 비교 모두 빌드 전과 같다.
  - **네트워크 레이스 A 1회차 실패** (05:18:27Z~05:20:31Z): `Unhandled log message: '[Error] RACE-CLIENT-FAULT the server's state for tick 336 could not be taken (the client at tick 335): InvalidOperationException: ClientPredictor: the server's state is for tick 336 but the client is at tick 335.'`. 같은 실행 요약: 에디터 프레임 최장 613.3 ms(tick 49 뒤), 50 ms 넘는 프레임 6개 `[1731:61 32:201 49:613 53:84 335:80 335:70]`, 서버 기준 늦은 입력 레이서 0 = 316, 레이서 1(플레이어 프로세스) = 394, 플레이어 클라 보정 197회. 고스트는 정상이었다: 상대 위치 335개 비교 중 어긋남 0, 순간 이동 0, 최대 한 걸음 1.096 m.
  - **네트워크 레이스 A 2회차 통과** (05:21:26Z~05:22:04Z, 같은 코드·같은 빌드): 에디터·플레이어 클라 모두 1001개 비교 보정 0, 상대 위치 1000개 어긋남 0, 늦은 입력 0 / 0, 고스트 2840 프레임 순간 이동 0·최대 한 걸음 0.440 m, 에디터 프레임 최장 44.5 ms.
  - **눈으로 확인 (사용자, 병합 뒤 `dev` `5234be8`, 같은 플레이어 빌드)**: Test Runner 에서 레이스 C(`TwoClients_DifferentRuns_…`)를 직접 돌렸다. 사용자 "문제 없이 내가 원하는 네모 스프라이트였다. 대신 달리는 연출은 없긴 했다". Editor.log 에는 출발·카운트다운·`RACE-CLIENT-ORDER tick=155 0=1 1=2` 까지 남았다. 그 뒤 Play 가 멈춰 `Playmode tests were aborted because the player was stopped` 로 끝났다. **이 실행에는 통과/실패 결과가 없다.** 멈춘 직후 `RaceClient.SendInput` (`RaceClient.cs:179`) NullReferenceException 이 한 번 났다 → `docs/NEXT_REQUESTS.md` 후보.
- **1회차 실패의 판단 (증명 못 함)**: 고장 형태는 HANDOFF 4절의 "서버가 클라를 앞지름" 과 같다. 늦은 입력이 에디터와 별도 플레이어 프로세스 **양쪽에서** 많았고, 플레이어 빌드 바로 다음 실행이었다. 그래서 기계 부하를 의심한다. 이번 변경은 `Begin` 에서 한 번 복제할 뿐이고, 레이스 도중의 일은 바뀌지 않았다(고스트 `Update` 는 위치·회전만 쓴다). 하지만 613 ms 프레임의 원인은 확인하지 못했다. **같은 코드를 다시 돌려 통과한 것으로 실패를 지우지 않는다(§9-2).** 병합 판단은 사용자에게 맡긴다.
- **버린 대안**: 첫 프리팹으로 모형을 빼기 — R3(3-2 ①)에서 한다. 이 경로는 그대로 쓴다. 고스트용 네모 모형을 코드로 따로 만들기 — 모형 원본이 둘이 된다.

## 2026-10-15 · 서킷 집중: 무한 모드와 멀티의 새 기능을 멈추고 서킷(속도전) 싱글을 먼저 다듬는다

- **결정 (사용자, 2026-10-15 "서킷 집중으로 하자", 요청서 A 승인 "셋다 좋다")**: 다음 작업은 **서킷 싱글**(속도전)에 모은다.
  - **무한 모드**: 새 기능을 멈춘다. `docs/NEXT_REQUESTS.md` R6(절차 코스 봉 스윙)은 보류.
  - **멀티**: 새 기능을 멈춘다(C1 마무리 측정, C2 충돌·추월). 예외는 같은 날 승인된 "네트워크 상대를 네모 모형으로"(R9) 하나다 — 기능이 아니라 캐릭터 모양 통일이다.
  - **R7 공격 판정 방향**: 보류. 서킷에는 적·표적이 없다(2026-10-08 "T4" 범위).
  - **테스트·병합 조건은 그대로 둔다.** 무한 모드 테스트, `Net` 카테고리, `Sweep`·`Long` 규칙(`CLAUDE.md` §9-2)을 바꾸지 않는다.
- **이유**: 사용자 질문 "한 모드에 집중해서 개발한 뒤 나머지를 구현하는게 낫지 않나". 확인한 사실: ① 멀티 테스트는 병합 게이트에서 이미 대부분 빠져 있다. 네트워크 레이스는 `[Explicit]` 이고 마일스톤(③)에서만 돈다. 2026-10-15 게이트(04:27:43Z~04:35:52Z, 489.4초)에서 `NetReconcileTests` 가 55.8초, 나머지 Net 클래스는 합쳐 0.2초 미만이다. ② 무한 모드 전용 클래스(`ChainRushCombatTests` 21.8초, `ChainRushProceduralTests` 18.7초, `ChainRushSteeringTests` 13.2초, `ChainRushPresentationTests` 8.1초)는 합쳐 약 62초다. 크게 줄어드는 것은 Sweep(약 933초)을 돌려야 하는 무한 모드 작업 자체다. ③ 서킷은 멀티 첫 레이스의 맵이다(`DESIGN.md` §7-5 "우리 코드에 주는 뜻" 2번). 그래서 서킷 싱글을 다듬은 결과가 나중에 멀티로 그대로 간다.
- **테스트를 그대로 두는 이유**: 멀티는 싱글 시뮬(`RunnerMotor.Step`)을 같은 입력으로 다시 실행한다(`DESIGN.md` §7-5 "우리 코드에 주는 뜻" 1번). 무한 모드는 `Centerline`·`RunnerMotor` 를 서킷과 함께 쓴다. 서킷 작업이 결정성이나 무한 모드를 깨면 그 자리에서 알아야 한다. 테스트를 빼면 다시 돌아왔을 때 몰아서 고친다(`DESIGN.md` §5 의 P1 전제와 같은 위험).
- **새 순서**: R9(같은 날 승인, 먼저 한다) → R2 → R4 → R5 → R3. R8 은 아무 때나. R3 는 R7 선행을 풀고, 네모 통일 2단계(작업대 러너, 꺼진 둥근 모형)를 함께 맡는다.
- **버린 대안**: ① 무한 모드 전용 테스트를 카테고리로 묶어 게이트에서 빼기 — 약 62초를 줄이지만 무한 모드가 조용히 깨진다. 병합 조건 변경이라 따로 합의가 필요하기도 하다. ② 싱글을 끝낸 뒤 멀티 — 멀티의 결정성 테스트를 끄면 시뮬 변경마다 결정성 위험이 쌓인다. ③ 무한 집중 — 무한은 싱글로 남는 모드라(`DESIGN.md` §8 질문 1) 멀티로 이어지지 않는다. Sweep·Long 비용도 그대로다.

## 2026-10-15 · 테스트 대기를 틱으로: 몇 틱만 이어지는 상태를 보는 대기 3곳

- **결정 (사용자 승인, 2026-10-15 "R1 시작하자", `docs/NEXT_REQUESTS.md` R1)**: 게임 시간 초(`WaitForSeconds`)로 기다린 뒤 **잠깐만 이어지는 상태**를 단언하는 테스트를 틱 수로 기다리게 바꾼다. 각 클래스에 비공개 보조 `WaitTicks(int)` 를 두고(이 프로젝트는 `LandOnRoad`·`RunTo` 같은 보조를 클래스마다 둔다), 초 값은 `Ticks.FromSeconds(같은 초)` 로 옮겨 정상 프레임에서 돌던 틱 수를 그대로 쓴다. 단언 값은 바꾸지 않았다.
  - `ChainRushCombatTests.Combat_EachDirection_AttackFiresConnectsAndRetracts`: 공격 뒤 0.07초 → 4틱(그 뒤 0.5초 → 25틱). 비행은 8틱(0.16초)이다.
  - `ChainRushSteeringTests.Guard_AngledHit_TurnsAlongRailAndRecoversSpeed`: 대기 5개 모두. 조향 0.3초가 길어지면 더 꺾여 "아직 가드에 안 닿음" 확인 전에 가드(5.62 m)에 닿는다.
  - `ChainRushInputSourceTests.Primary_ScriptedPressOnGround_JumpsOnce`: 점프 뒤 0.15초 → 8틱. 오르막은 0.52초(11.5 m/s, 중력 22)라 여유가 2틱뿐이었다.
- **이유**: 시뮬은 `FixedUpdate` 한 번에 한 틱이다(`ChainRushGame.cs:191`). 프레임 하나가 길면 코루틴이 깨어나기 전에 여러 틱이 돈다. 한 프레임의 게임 시간은 최대 0.333초다(`TimeManager.asset` Maximum Allowed Timestep). 그래서 `WaitForSeconds(x)` 는 x + 0.333초까지 갈 수 있다. 2026-10-14 `feature/circuit-fall-respawn` 게이트 실패의 하나가 바로 전투 테스트였다(그 브랜치 DECISIONS "실패의 판단").
- **`WaitTicks` 의 가정**: `WaitForFixedUpdate` 는 고정 스텝마다 깨어난다고 보고, 기다린 뒤 `game.Tick` 이 목표와 **정확히 같다고 단언**한다. 가정이 틀리면 조용히 넘치는 대신 그 자리에서 실패한다. 아래 실행에서 모두 같았다.
- **바꾸지 않은 대기 (분류 근거)**: 나머지 `WaitForSeconds` 58곳(67곳 중 9곳을 바꿈) 중 게이트에 드는 것은 **기다릴수록 단언이 더 확실해지는 것**(속도 회복, 착지 뒤 상태, 피해 무적 시간 경과 등), **실시간 일시정지 확인**, **표현 계층**(블렌드·애니메이션·LateUpdate — 틱이 아니라 프레임이 필요하다)이다. 위의 상한 0.333초로 따져 본 여유는 점프 뒤 그래플 4곳(체공 약 1.05초, 사거리 32 m), `Combat_GapAndJump`(점프 0.2초 뒤 공중), 조향 감기기 `Tilt_…_TurnsBodyToFaceHeading`(0.2초 → 최대 64° 회전, 단언은 90° 넘게 남음), 코너 스윙 0.3초(최대 시간 2초)다. `Device` 테스트(가상 장치 입력은 `Update` 에서 들어온다)는 게이트 밖이라 두었다. `Course_TimedJumpsAndHeldGrapple_ReachesFinish` 는 R3 에서 지운다.
- **검증** (`fix/test-frame-timing`, 에디터 6000.3.19f1, Unity 창 포커스 상태는 기록하지 않음): 컴파일 — `AssetDatabase.Refresh` 뒤 Editor.log `*** Tundra build success (7.82 seconds)`(`ProtoHarness.Tests.PlayMode.dll` 포함), Console Error·Warning 0건. 빠른 확인(고친 3개 클래스, `!Device !Sweep`) 3회 연속: `testcasecount="17" result="Passed" passed="17" failed="0"` (04:21:27Z~04:22:27Z, 60.1초) / 같음 (04:23:21Z~04:24:29Z, 68.3초) / 같음 (04:25:19Z~04:26:20Z, 61.4초). EditMode `testcasecount="242" result="Passed" passed="242" failed="0"` (04:26:46Z~04:26:59Z). `Run PlayMode Tests` `testcasecount="141" result="Passed" passed="138" failed="0" skipped="3"` (04:27:43Z~04:35:52Z, 489.4초, skipped 3 은 `[Explicit]` 네트워크 레이스).
- **확인 못 한 것**: **고치기 전 코드가 실패하는 것을 재현하지 않았다.** 긴 프레임이 없으면 옛 대기도 통과하므로, 위 통과는 "고쳐서 바뀐 것이 없다(회귀 없음)" 까지의 증거다. 실패 원인 분석은 위 "이유" 의 계산과 2026-10-14 실패 기록에 기댄다.
- **버린 대안**: 모든 `WaitForSeconds` 를 틱으로 — 표현 테스트는 프레임이 돌아야 하고, 기다릴수록 확실해지는 대기는 바꿀 이유가 없다. 공용 보조 파일 — 새 파일(§1-1)이 필요하고, 기존 보조 `TestRoad` 는 도로 전용이다. `Time.timeScale = 0` + `StepTick` 직접 호출 — 테스트 구조가 크게 바뀐다.

## 2026-10-14 · 서킷 낙하 → 트랙 복귀: 떨어져도 런이 끝나지 않고, 마지막 착지 지점 4 m 뒤로 돌아가 1초 선다 (검증 남음, 병합 보류)

- **결정 (사용자 선택 2026-10-14, 구현 승인 "진행")**: HANDOFF 2절의 첫 후보. ① **서킷만** 복귀한다(직선·무한 모드는 지금처럼 낙하 = 종료). ② 복귀 위치는 **마지막으로 서 있던 S**(`RacerState.LastGroundedS`, 착지한 틱마다 기록)에서 `respawnBackoff` 4 m 뒤의 중심선, 출발 슬롯과 같은 높이(1.05 m 위에서 0.3 m 내려 착지), 트랙 방향, 속도 0. ③ 벌칙은 **복귀 후 1초(50틱) 정지**(`respawnHoldTime`, `RacerState.RespawnHoldTicks`): 그동안 입력을 무시하고 움직이지 않지만 틱(기록)은 흐른다. 게이지는 유지하고 그래플·슬링·스윙은 부스트 없이 끝낸다. 두 번째 낙하가 착지 전에 나면 같은 자리로 돌아간다(`LastGroundedS` 를 복귀 때 바꾸지 않는다). ④ **"낙하 = 런 종료" 경로는 서킷에서 완전히 제거**(스위치를 두지 않음). 서킷에는 공격 대상이 없어 이제 서킷 런이 끝나는 길은 결승뿐이다.
- **구현**: `RunnerMotor.Step`(정지 틱 처리, 낙하 판정에서 서킷이면 `RespawnOnTrack`), `RacerState`(필드 2개, `Snapshot`/`Capture`/`Restore`/`Reset`), `SimSnapshotCodec`(두 필드 직렬화 — 와이어 형식이 바뀌어 레이스 플레이어 재빌드 필요). 판정 기준 H < -12 m 는 그대로. `RaceWire.EndReason.Failed`·`RaceStandings.Status.Failed`·패널 "낙하" 는 지우지 않았다(다른 모드의 HP 0 종료 경로로 남음, 서킷에서는 이제 나오지 않는다).
- **테스트**: `ChainRushCircuitTests` 틈 테스트를 "틈 앞 4 m 로 복귀, 정확히 50틱 정지 후 다음 틱에 다시 달림, 중심선·착지·속도 0·트랙 방향, 랩 없음" 으로 바꿨다(사양 변경). `NetReconcileTests` 의 "서버가 입력을 놓쳐 낙하·종료" 를 "놓친 입력으로 서버 런이 복귀 → 클라 보정 1회 후 복귀·정지까지 비트 단위로 따라감"(지연 5틱과, 보정 재실행이 복귀를 직접 지나는 지연 59틱)으로 바꿨다. "낙하로 끝난 클라가 이전 상태로 되살아남"(`ClientEnd_WhenAnOlderStateBringsAFallenClientBack_CatchesUp`)은 서킷에서 만들 수 없어 **삭제** — C1c-1b 의 같은 경로는 결승 쪽 3건이 계속 다룬다. `ChainRushRollbackTests` 에 낙하 중·정지 중 스냅샷의 되감기 테스트를 추가하고, 롤백 트레이스에 두 필드를 넣었다. 스냅샷 가드(`SnapshotTests`, `Codec_EveryFieldOfTheSnapshotChangesTheBytes`)는 리플렉션이라 고치지 않고 새 필드를 잡았다(34개 필드).
- **검증 (6000.3.19f1, 이 머신, 커밋 전 작업 트리, UTC)**: 컴파일 에러 0(Console Error 0). EditMode `testcasecount="237" result="Passed" passed="237" failed="0"` (05:45:13~05:45:20). 바뀐 PlayMode 5건 `Passed 5/0` (05:44:40~05:44:55): 복귀 틱 657, 지연 59틱 보정 재실행 비트 일치, 낙하 중(633)·정지 중(677) 스냅샷이 857틱까지 동일. **P2 해시 불변**: 세 게이트 모두 서킷 `0xF9A914DA3FAA680B`, 절차 `0xE81912109A2EB020`.
  - 게이트 1회차 `testcasecount="144" result="Failed(Child)" passed="140" failed="1" skipped="3"` (05:45:47~05:56:14). `ChainRushCombatTests.Combat_EachDirection_AttackFiresConnectsAndRetracts` — `Expected: Firing / But was: Retracting` (`:104`).
  - 게이트 2회차 `testcasecount="144" result="Passed" passed="141" failed="0" skipped="3"` (05:58:44~06:10:39).
  - 게이트 3회차 `testcasecount="144" result="Failed(Child)" passed="140" failed="1" skipped="3"` (06:22:55~06:33:47). `ChainRushTests.Course_TimedJumpsAndHeldGrapple_ReachesFinish` — `Course did not finish: position=(0.35, -12.00, 394.14), links=6, health=3`.
  - 레이스 플레이어 재빌드 `Succeeded`, 에러 0(경고 485 는 Sentis 셰이더). 빌드가 바꾼 `Assets/Settings/*.asset` 3개·`UnityConnectSettings.asset` 은 파일을 지정해 되돌렸다.
  - 네트워크 레이스 1차(06:13:30~06:17:47) A·B·C 실패: A 보정 1회(`late 1`, 에디터 프레임 최장 60.3 ms), B·C 는 `the server's state is for tick 909(790) but the client is at tick 908(789)` 로 예측기 고장(에디터 프레임 50 ms 초과 183·241회, 최장 452.7·288.8 ms). 2차(06:19:12~06:22:09) B 통과, A 보정 2회(`late 18`, 50 ms 초과 75회, 최장 274.6 ms), C 같은 고장(틱 577, 50 ms 초과 159회, 최장 388.3 ms).
- **실패의 판단 (추정, 증명하지 못함)**: 실패 5종이 모두 프레임 시간에 기대는 테스트다 — 전투는 게임 시간 0.07초 대기 중 비행 8틱이 끝남(한 프레임 ≥ 약 0.09초), 직선 코스는 프레임마다 점프를 눌러 3배속에서 긴 프레임이 4.5 m 점프 구간을 건너뜀, 네트워크 레이스는 입력이 제때 닿아야 함. 틱 단위로 직접 돌리는 결정성·롤백·재생 테스트와 P2 해시는 매번 같았고, 전투·직선 코스는 이번 변경이 동작을 바꾸지 않는 모드이며, 레이스 C 의 에디터 런은 오프라인에서 낙하가 없다. 실행 내내 Unity 창이 비활성(`isApplicationActive=False`)이었다. 그래서 원인은 비활성 에디터의 프레임 멈춤으로 본다. `dev` 로 같은 조건을 돌리는 대조 실험은 하지 않았다(사용자 "너무 오래걸린다").
- **복귀가 드러낸 것**: 서킷 런이 낙하로 끝나지 않으므로 클라가 고장 나 입력이 끊긴 레이서도 서버에서 계속 달린다(`ran 4285`). 레이스 B 의 에디터 런은 예전엔 657틱 낙하로 끝났는데 이제 1000틱 끝까지 가서, 서버가 클라를 앞지르는 고장(HANDOFF 4절, 2026-10-12)에 더 오래 노출된다.
- **병합 보류**: §9-2 의 "같은 코드에서 연속 2회 통과" 와 네트워크 레이스 통과를 채우지 못했다. 사용자 결정(2026-10-14 "1,2,3 ㄱㄱ")으로 브랜치에 WIP 커밋하고, Unity 창을 앞에 둔 다음 세션에서 게이트 연속 2회 + 네트워크 레이스 A·B·C 를 다시 돌린다.
- **버린 대안**: 복귀 스위치로 낙하 = 종료를 남기기(사용자 "완전히 제거"), 마지막 체크포인트로 복귀(체크포인트가 적어 벌칙이 크고 들쭉날쭉), 정지 없음·2초 정지(사용자 1초 선택), 검증을 건너뛴 병합.
- **2026-10-15 검증과 병합 (사용자 "R2 ㄱㄱㄱ", 이어서 로드맵 재설계 승인)**: `dev`(`cf35438`)를 이 브랜치에 병합했다(`a127e3f`). 충돌은 `DECISIONS.md`·`HANDOFF.md` 뿐이었고 양쪽 내용을 모두 살렸다. 병합 조건은 2026-10-14 "테스트 3단" 뒤의 §9-2 ②(게이트 1회 + 코어 변경이라 Long)로 다시 쟀다. 에디터 6000.3.19f1, 포커스 `isApplicationActive=False`.
  - 컴파일: `Tundra build success (11.32 seconds)`, 리플렉션으로 `RacerState.LastGroundedS`·`RespawnHoldTicks`·`RunnerTilt.Body` 가 올라온 것을 확인, Console Error 0.
  - EditMode `testcasecount="242" result="Passed" passed="242" failed="0"` (06:02:16Z~06:02:23Z).
  - `Run PlayMode Tests` `testcasecount="141" result="Passed" passed="138" failed="0" skipped="3"` (06:02:46Z~06:09:11Z, 384.9초, skipped 3 은 `[Explicit]` 네트워크 레이스). 서킷 지문 `0xF9A914DA3FAA680B` 그대로. `Net` 카테고리(`NetReconcileTests` 포함)는 이 게이트 안에서 돌았다.
  - `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (06:09:37Z~06:12:54Z, 196.9초). 절차 지문 `0xE81912109A2EB020` 그대로.
  - 레이스 플레이어 재빌드 `Succeeded errors=0` (06:14:03Z). 빌드가 바꾼 설정 파일 5개는 되돌렸다.
  - **네트워크 레이스 A·B·C 는 돌리지 않았다.** 같은 날 로드맵 재설계(DECISIONS 2026-10-15 "플레이 경험 순서 로드맵")에서 실제 네트워크 레이스를 멀티 재개(M5)까지 미루기로 했다. 그래서 이 브랜치의 2026-10-14 레이스 실패(예측기 고장 "서버가 클라를 앞지름", 복귀로 런이 길어져 더 자주 드러남)는 **풀리지 않은 채 M5 의 첫 확인 항목으로 넘긴다.**

## 2026-10-14 · 코너 스윙을 봉 스윙으로 대체: S1b·S2 는 하지 않는다 (설계 결정, 구현 전)

- **결정 (사용자, 2026-10-14 "커브 마다 줄을 걸 수 있는 봉을 놓는 것이다(코너마다 한개 뿐) … 웹스윙을 90도 돌린 느낌", 결정 7개는 `docs/PROPOSAL_CORNER_POLE.md` 1절)**: 지금 코너 스윙(드리프트 + 커브 안 Ctrl, 원 중심으로 레일처럼 14 m/s)을 **코너 중앙 봉 + 고정 줄 길이 + 관성 + Ctrl 유지**로 바꾼다. 둘을 공존시키지 않는다. 그래서 `docs/PROPOSAL_CORNER_SWING_LOOK.md` 의 S1b(불꽃·끊어짐·잔상)·S2(스윙 수치)·S3(기둥 진자 판단)은 하지 않는다. S1 의 몸 미끄럼 각·카메라는 남고, 줄 끝은 난간 대신 봉으로 간다(봉 스윙 P2).
- **이유**: S2 가 고치려던 수치(`swingSpeed`)는 봉 스윙에서 없어진다(가속 없이 관성). S1b 의 난간 불꽃은 줄이 난간을 긁는 그림이 전제라 봉에 걸리면 맞지 않는다. 같은 코너에 비슷한 동작이 둘이면 봉으로 난이도를 조절하려는 계획(P4)이 흐려진다.
- **구현**: 아직 없다. `docs/NEXT_REQUESTS.md` R4(P1 시뮬)·R5(P2 표현)·R6(P3 절차 코스), 각각 §3-2 합의. R4 는 같은 파일을 바꾸는 R2(서킷 낙하 복귀) 병합 뒤.
- **버린 대안**: 제안서 8절 — 공존, 봉을 안쪽 난간 꼭짓점에, 쿨다운, 각도 자동 끊김.

## 2026-10-14 · 코너 스윙 S1 연출: 줄은 안쪽 난간에, 몸은 커브 안으로, 카메라는 바깥으로 — 시뮬은 그대로

- **결정 (사용자 승인, 2026-10-14 요청서에 "ㄱㄱ", `docs/PROPOSAL_CORNER_SWING_LOOK.md` S1)**: 표현 계층만 바꾼다. 새 정적 클래스 `Visuals/SwingLook`(계산만), 그것을 부르는 기존 컴포넌트 셋 — `GrappleController`(스윙 중 줄 끝 = 러너 옆 **안쪽 난간 위**, 반폭 `RoadProfile.DeckHalfWidth` 6 m, 높이 `RoadMeshBuilder.GuardRailHeight` 1.1 m), `RunnerTilt`(스윙 중 **몸만** 안쪽으로 25°, 0.15초 블렌드), `FollowCamera`(바깥쪽 0.6 m, 시야각 +4°). 시뮬의 `RunnerMotor.swingAnchor`(원 중심)는 그대로다.
- **제안서와 달라진 것**: ① 새 컴포넌트 대신 정적 클래스 — 컴포넌트를 붙이면 씬 3개를 고쳐 연결해야 한다(씬 수정 = 별도 합의, 동시 1개). 기존 직렬화 참조만으로 된다. ② 안쪽 방향은 트랙 대신 `motor.SwingAnchor`(원 중심)가 진행 방향의 어느 쪽인지로 정한다 — `RunnerTilt` 는 트랙 참조가 없다. ③ 불꽃·끊어지는 연출·잔상·소리는 머티리얼·파티클 에셋이 새로 필요해 **S1b** 로 나눴다. ④ 아크 끝을 지나 트랙 프레임이 직선인데 아직 스윙 중인 프레임에서는 줄 끝을 원 중심(이전 동작)에 둔다(`RailPoint` 는 직선에서 던진다).
- **한계**: 반폭을 상수 6 m 로 쓴다. 서킷 정의(`Circuit_Stadium.asset` `roadHalfWidth`)를 바꾸면 줄 끝이 난간과 어긋난다(그림만).
- **검증** (`feature/corner-swing-look`, 에디터 6000.3.19f1): EditMode `testcasecount="242" result="Passed" passed="242"` (11:32:01Z~11:32:13Z, 새 `SwingLook_*` 5건). 빠른 확인(코너 스윙 클래스 + 지문) `5/5 Passed` (11:32:56Z~11:34:00Z). `Run PlayMode Tests` `testcasecount="141" result="Passed" passed="138" failed="0" skipped="3"` (11:34:32Z~11:41:50Z, 438.1초) — 새 `SwingLook_WhileSwinging_TurnsOnlyTheBodyInsideAndHooksTheInnerRail` 포함. **서킷 지문 `0xF9A914DA3FAA680B` 그대로**(HANDOFF 1절 값) → 연출이 시뮬에 닿지 않았다. 코어 파일(`GrappleController`)이라 "전체" → `Run Long Tests` `4/4 Passed` (11:42:30Z~11:45:48Z, 198.1초), 절차 코스 해시 `0xE81912109A2EB020` 그대로.
- **확인 못 한 것**: 실제 느낌(사용자가 서킷 커브에서 직접 플레이해야 한다). 무한 모드 `chainVisual` 이 난간을 따라 움직이는 끝점을 자연스럽게 그리는지(테스트는 비무한 `LineRenderer` 만 봤다).

## 2026-10-14 · 맵 정리 2단계: `ChainRushPrototype` → `ChainRushTestBench` (이름만)

- **결정 (사용자 승인, 2026-10-14 요청서에 "ㄱㄱ", `docs/PROPOSAL_SCENE_CLEANUP.md` B안 7절 2단계)**: 구 머리 캐릭터가 있는 맵을 플레이용이 아니라 **테스트 작업대**로 이름 붙인다. `Unity_RunCommand` 로 `AssetDatabase.MoveAsset` — GUID `d6b2b0e31af07d844824abd448970241` 그대로, `.meta` 는 Unity 가 함께 옮겼다. 테스트 8개 파일과 `ChainRushSceneBuilder` 의 경로 문자열, 메뉴 `Create/Open Prototype Scene` → `Create/Open Test Bench Scene`, 문서(ARCHITECTURE·README·HANDOFF)를 바꿨다. 빌드 설정에는 이 씬이 없다.
- **이유**: 사용자 "플레이어 머리가 구 모양으로 된 씬은 필요 없는 씬이다"(2026-10-14). 그 씬을 불러오는 PlayMode 테스트 8개 파일 대부분은 코드로 시험 도로를 깔고 씬은 그릇으로만 쓴다(제안서 2절). 지우지 않고 이름을 바꿔 플레이 맵은 Procedural·Circuit 둘로 보이게 한다.
- **하지 않은 것 (3단계, 별도 합의)**: 발판·앵커·결승선·표적 걷어내기, `Course_TimedJumpsAndHeldGrapple_ReachesFinish` 삭제, `Guards_PrototypeDecksAndProceduralRoad_AreGuarded` 축소, 구 머리 캐릭터. 테스트 이름에 남은 `Prototype` 도 그때 정리한다.
- **검증** (`feature/scene-test-bench`, 에디터 6000.3.19f1): EditMode `237/237 Passed` (11:16:46Z~11:16:58Z). `Run PlayMode Tests` `testcasecount="140" result="Passed" passed="137" failed="0" skipped="3"` (11:17:34Z~11:24:47Z, 433.0초) — 서킷 지문(`Replay_CircuitBotRun_…`) 포함. 씬·Editor 변경이라 "전체" → `Run Long Tests` `4/4 Passed` (11:25:24Z~11:28:42Z, 198.0초). 메뉴: `Open Test Bench Scene` 있음, `Open Prototype Scene` 없음.

## 2026-10-14 · Long 묶음: 다른 곳에서 덮이는 장거리 테스트 4건을 병합 게이트에서 조건부로 뺀다

- **결정 (사용자 승인, 2026-10-14 요청서에 "ㄱㄱ")**: `Category("Long")` 4건 — `ChainRushProceduralTests.Course_BotRunsFourteenHundredMetres_GateSeeds(0,1,2)`, `ChainRushReplayTests.Replay_ProceduralCourseRun_RepeatsCourseAndEncountersBitForBit`. 메뉴 `Run PlayMode Tests` 는 `Long` 을 빼고, 새 메뉴 `Run Long Tests` 가 이것만 돈다. 병합(②)에서는 `tools/select-tests.ps1 -Range dev...HEAD` 가 "전체"(코어·Editor·씬·설정 변경)로 판정한 브랜치만 `Long` 도 돌린다. 마일스톤(③)은 늘 돈다. 규칙 본문 `CLAUDE.md` §9-2.
- **이유**: 위 "테스트 3단" 의 측정에서 상위 6건이 게이트의 45% 였다. 그중 ① 게이트 시드 0·1·2 는 `Sweep`(시드 0~19)이 같은 `RunSeed` 로 다시 돈다. 빠지는 단언은 풀 크기(24·10)뿐이다. ② 절차 리플레이는 절차 코스 결정성을 보는 **유일한** 테스트다(롤백은 서킷 전용, 절차 코스에서는 `NotSupportedException`). 그래서 무조건 빼지 않고, 코어를 바꾼 브랜치에서는 돌린다. Net·연출·테스트만 바꾼 브랜치는 절차 시뮬을 바꿀 수 없다.
- **남긴 것**: 서킷 3랩(55초)과 서킷 지문(38초)은 ② 에 그대로 둔다 — 서킷과 결정성의 핵심이고 다른 곳에서 덮이지 않는다.
- **검증** (`feature/test-long-tier`, 에디터 6000.3.19f1): EditMode `testcasecount="237" result="Passed" passed="237"` (11:01:23Z~11:01:35Z). `Run PlayMode Tests` `testcasecount="140" result="Passed" passed="137" failed="0" skipped="3"` (11:02:09Z~11:09:17Z, **427.8초**, 이전 651.2초) — `Long` 4건 없음, skipped 3 은 `[Explicit]` 네트워크 레이스. 이 브랜치는 Editor 파일을 바꿔 "전체" 이므로 `Run Long Tests` `testcasecount="4" result="Passed" passed="4"` (11:09:54Z~11:13:11Z, 197.9초). 오전 WIP 실행에서 실패한 `Course_TimedJumpsAndHeldGrapple_ReachesFinish` 는 `dev` 코드인 이 실행에서 통과했다 → 그 실패는 `feature/circuit-fall-respawn` WIP 쪽이다.
- **발견**: 이 머신(19f1)에서는 에디터가 `ProjectSettings/ProjectSettings.asset`·`ProjectVersion.txt`·`packages-lock.json` 을 자동으로 바꿔, `select-tests.ps1` 기본 실행(커밋 안 한 변경 포함)은 늘 "전체" 가 된다. 병합 판정은 `-Range dev...HEAD` 로 돌린다고 §9-2 에 적었다.
- **버린 대안**: 6건 모두 Long — 서킷 지문을 빼면 코어가 아닌 변경에서도 결정성 경보가 ① 에만 남는다. Long 을 무조건 ② 에서 빼기 — 코어 변경의 절차 코스 결정성 깨짐이 마일스톤까지 숨는다.

## 2026-10-14 · 테스트 3단: 병합은 PlayMode 1회, 2회는 마일스톤으로, 작업 중에는 바꾼 부분만

- **결정 (사용자 승인, 2026-10-14 요청서에 "A+B+C 진행")**: `docs/PROPOSAL_TEST_TIERS.md` 의 3단을 들인다. ① 빠른 확인(작업 중) — `tools/select-tests.ps1` 이 바뀐 파일로 고른 PlayMode 테스트 + EditMode 전체, **병합 근거가 아니다**. ② 병합(`feature → dev`) — PlayMode 전체(`Device`·`Sweep` 제외) **1회**. ③ 마일스톤(`dev → main`·태그 전) — ② + 같은 코드 2회차 + `Sweep` + `Device` + 네트워크 레이스. 규칙 본문은 `CLAUDE.md` §9-2, 실행 방법은 `docs/RULES/VERIFICATION.md` §4-4.
- **이유**: ① 사용자 "테스트만 20분 정도로 상당히 오래 걸린다 … 수정한 부분만 QA 를 돌리거나 시뮬레이션 해 보면 될 것 같다"(2026-10-14). 게이트 1회 약 580~660초 × 2회. ② 2회 규칙(2026-10-02)을 만든 간헐 실패는 가상 장치 테스트였고 `Device` 로 이미 게이트에서 빠졌다(2026-10-13 "병합 2회차 축소" 의 "감수한 것"). ③ 2회는 ③ 에 남겨 마일스톤마다 간헐 실패 경보를 울린다.
- **감수한 것**: 코어 테스트에 새 간헐 실패가 생겨도 `dev` 병합 때는 한 번만 본다. 마일스톤 전까지 늦게 드러날 수 있다. "한 번이라도 실패하면 원문 보고, 원인을 설명 못 하면 병합하지 않는다" 는 그대로다.
- **제안서와 달라진 것**:
  - **카테고리 대신 클래스 이름으로 고른다.** 제안서 6절 3단계(`[Category]` 달기)는 `Assets/` 변경이라 게이트를 치러야 한다. `groupNames` 는 한 배열 안에서 OR(`RuntimeTestRunnerFilter.cs:81,85`)이라 클래스 여러 개 + 지문 테스트 1건을 `Filter` 하나로 고를 수 있다. 제안서가 근거로 든 "`Filter` 여러 개 OR" 는 쓰지 않는다.
  - **매핑을 폴더가 아니라 파일 단위로 나눴다.** 2026-10-14 런타임 타입별 참조 grep 결과, `Track/Centerline`·`TrackFrame`·`TrackCoord` 는 `RunnerMotor`·`GrappleController` 가, `Control/TickInput`·`IInputSource` 는 `RunnerMotor`·`ChainRushGame`·Net 이 쓴다 → 전체. `Track/LapCounter`·`TrackDefinition` 은 서킷 + Net(`RaceOrder`·`SimSnapshotCodec`). `RaceOrderTests`·`RaceStandingsTests`·`RaceStandingsTextTests` 는 이미 `[Category("Net")]` 이고 Net 코드를 시험하므로 Net 묶음이다(제안서 표의 `Race` 가 틀렸다).
  - **지문 테스트는 테스트·Net 밖의 `Assets/` 가 바뀔 때만 넣는다.** `ProtoHarness.Net` 은 시뮬 어셈블리가 참조하지 않아 지문을 바꿀 수 없다(2026-10-13 "병합 2회차 축소" ④).
  - "Net 만 바꾼 브랜치는 2회차를 `Net` 카테고리만" 조항은 ② 에 2회차가 없어져 지웠다.
- **도구 시험 실행** (2026-10-14, 이 머신의 Windows PowerShell 5.1, Unity 없이): 커밋 5개의 diff 에 돌렸다. `fb6798d`(`NetworkRaceTests` 만) → PlayMode 없음 + `[Explicit]` 안내. `8073939`(C1c-3, Net + Net 테스트) → Net 5개 클래스, 지문 없음. `246d433`(`ChainRushGame.cs` 포함) → 전체. `75df74d`(`RunnerTilt.cs`) → Presentation·Combat·InputSource 3개 + 지문. `faeb50a`(docs 만) → PlayMode 없음. 표에서 `ChainRushTests` 를 뺀 사본은 경고 2줄을 내고 전체로 떨어졌다.
- **Unity 실행 확인** (2026-10-14, 에디터 6000.3.19f1, `Assets/` 는 `dev` 와 같음): `75df74d` 출력의 `Filter` 를 그대로 실행 → `testcasecount="18" result="Passed" passed="18" failed="0" skipped="0"` (10:52:42Z~10:54:00Z, 78.4초). Combat 5 + InputSource 9 + Presentation 3 + 지문 1 = 18 로, 고른 것만 돌았다(`ChainRushReplayTests` 의 다른 2건은 돌지 않음).
- **측정 (제안서 6-1)**: 같은 날 06:22:55Z~06:33:47Z 게이트 XML(144건, 651.2초, 140 통과·1 실패·3 skipped). **`feature/circuit-fall-respawn` 의 커밋 전 WIP 위에서 돈 실행이다**(reflog: 15:22~15:33 KST 에 그 브랜치, WIP 커밋은 15:37). 시간 분포를 보는 데만 쓴다. 클래스별: `ChainRushProceduralTests` 163.7초(게이트 시드 3건이 각 약 49초), `ChainRushReplayTests` 104.6초(절차 코스 55.0 · 서킷 지문 38.3 · 한 틱 바꾸기 11.3), `ChainRushCircuitTests` 77.0초(3랩 54.8), `ChainRushRoadTests` 45.4초, `ChainRushTests` 37.1초, 나머지 코어·코스 클래스 각 4~29초, Net 5개 클래스 합 27.3초. 상위 6건(절차 게이트 시드 3, 절차 리플레이, 서킷 3랩, 서킷 지문)이 약 295초로 게이트의 45% 다. 그 실행의 실패 1건(`ChainRushTests.Course_TimedJumpsAndHeldGrapple_ReachesFinish`, `Course did not finish: position=(0.35, -12.00, 394.14)`)은 WIP 쪽 결과라 이 결정과 무관하다 — 그 브랜치를 검증할 때 다룬다.
- **확인 못 한 것**: `Long` 묶음 배정(제안서 5절). 측정값으로 후보는 위 상위 6건이지만, 배정은 `[Category]` 를 다는 `Assets/` 변경이라 별도 합의.
- **버린 대안**: 제안서 7절과 같다. 카테고리 달기 — 지금은 클래스 이름으로 충분하고 `Assets/` 변경이 든다. 필요해지면(예: 한 클래스 안의 일부만 고를 때) 별도 합의.

## 2026-10-13 · main v0.1: `dev` 를 `main` 에 병합하고 첫 태그를 단다

- **결정 (사용자 "메인까지 하는게 나아보이면 해라" — 판단을 맡김)**: `dev`(P3 C1 같은 시각 출발 병합 `f95a184` + 이 문서 브랜치)를 `main` 에 `--no-ff` 로 병합하고 주석 태그 `v0.1` 을 단다. 둘 다 `origin` 에 푸시한다.
- **이유**: `main` 은 `7e4a0a8`(P0·P1-1·P1-2)에 멈춰 있었고 그 뒤 `dev` 에 병합 58개(P1 나머지, P2 결정성, 코스 T3·T4, P3 스파이크·C1a~C1c, 같은 시각 출발)가 쌓였다. P3 C1 의 완료 기준("두 클라가 같은 서버 월드에서 달리고 서로를 본다. 입력 지연이 있어도 조작이 끊기지 않는다", `DESIGN.md` P3)을 2인·같은 PC·RTT 100 ms 에서 만족해 마일스톤이다. 병합 조건은 `f95a184` 와 같은 트리(`246d433`)에서 치렀다(아래 "P3 C1 같은 시각 출발" 의 검증: 게이트 전체 2회 연속 통과 등). 이 문서 브랜치는 `docs/` 만 바꿔 문서 검증으로 병합한다(§9-2).
- **v0.1 에 없는 것**: P3 C1 마무리 측정(3인 이상, 패킷 유실, 모바일/IL2CPP), 트랙 복귀, C2 이후.
- **버린 대안**: C1 마무리 측정 뒤에 올리기 — 측정이 언제 끝날지 정해지지 않았고 `main` 이 너무 오래 뒤처진다.

## 2026-10-13 · P3 C1 같은 시각 출발: 전원 접속 → 출발 신호 → 3·2·1 카운트다운 → 서버는 모든 레이서를 같은 프레임에 출발

- **결정 (사용자 승인, 2026-10-13 "ㄱㄱ 카운트다운까지 넣어서", 싱글 카운트다운 "싱글플레이도 나오면 좋겟다", 수정 설계에 "ㅇㅋ")**: HANDOFF 2절 "진행 중 순위의 남은 것" 중 같은 시각 출발을 한다. 같은 자리의 **앞뒤 거리 표시는 하지 않는다**(사용자 "두 유저 사이의 거리는 필요없을거같다").
  - **왜 따로 출발했나 (사용자 질문 "다르게 출발하게 시키는 이유가 있나?")**: 설계 이유는 없었다. P3 스파이크(서버 1 + 클라 1)의 "클라는 접속하면 달리고, 서버는 그 클라 입력이 버퍼에 차면 출발" 을 C1b 가 레이서마다 복제했고(`RaceServer.Update`), 결승 순위의 틱 수 기준(C1c-2a)·주행 순위의 같은 틱 비교(C1c-3)·테스트의 "먼저 출발한 쪽 줄 빼기" 가 그 어긋남을 보정하며 쌓였다. 카트라이더식 게임은 전원 로딩 뒤 카운트다운으로 같이 출발하고, **C2(충돌·추월)는 같은 시각에 같은 자리에 있어야 성립하므로 이것이 전제다.**
  - **서버(`RaceServer`)**: 모든 자리에 클라가 차면 각 클라에 `race.go`(레이서 수 1바이트, `RaceWire.EncodeGo`)를 자기 `ToOwner` 큐로 1회 보낸다(`RACE-SERVER-GO`). 그 뒤 **클라가 붙어 있는 모든 레이서의 입력이 `bufferTicks` 만큼 쌓인 프레임에 한꺼번에** `StartRun` 한다(`RACE-SERVER racer <i> started on frame <f>`). 출발 순위(standings)는 출발을 모두 등록한 뒤 한 번 보낸다. 신호 뒤에 붙는 클라는 끊는다(신호를 못 받아 모두의 출발을 막는다). 신호 뒤에 나간 레이서는 출발하지 않는다(대기로 남음).
  - **클라(`RaceClient`)**: 접속해도 출발하지 않는다. `race.go` 를 받으면 입력원을 예측기로 바꾸고 `ChainRushGame.StartCountdown()`, `CountdownFinished` 에서 `predictor.AfterTick()`(틱 0). `Begin` 에서 `ManualStart=false` 로 두고 경기 내내 유지한다 → **경기 중 R·RUN AGAIN 이 예측기를 고장 내던 문제(HANDOFF 2절 "네트워크 클라의 HUD 재시작 버튼")도 막힌다.** 두 번째 go 는 버그로 던진다. 타임아웃은 지금처럼 접속부터 잰다(상대 대기 + 카운트다운 3초 포함).
  - **코어 카운트다운(`ChainRushGame`·`ChainRushHud`, 싱글에도 적용)**: `StartCountdown()` 이 런 초기화(지금 `StartRun` 이 하던 것, `ResetRun` 으로 분리)를 하고 `Countdown` 단계로 3초(`CountdownSeconds`, 게임 시간 `Time.deltaTime`)를 센 뒤 `StartRun()`(한 번 더 초기화)을 부르고 `CountdownFinished` 를 낸다. **`StartRun()` 은 그대로 즉시 출발** — 서버 월드·테스트·리플레이·롤백은 이 경로라 P2 해시는 바뀌지 않는다(게이트 1회 XML 의 로그: 서킷 1700틱 `0xF9A914DA3FAA680B`, 절차 2500틱 `0xE81912109A2EB020` — 2026-10-08 "롤백 안전성" 값과 같다. 리플레이 테스트는 해시를 상수와 비교하지 않고 로그로만 남긴다). Enter(대기·결과)·R·HUD 의 START RUN·RUN AGAIN 은 `StartCountdown` 을 부르고, 카운트다운 중 R 은 처음부터 다시 센다. `ManualStart=false` 면 키는 아무것도 안 하고 HUD 는 버튼 대신 "출발 신호를 기다리는 중" 을 보이며 RUN AGAIN 과 아래 줄의 "R 재시작" 을 숨긴다. HUD 는 카운트다운 중 가운데에 큰 3·2·1, 출발 후 1초 "GO!" 를 그린다. "321GO!" 글리프는 HUD 초기화 때 `RequestCharactersInTexture` 로 미리 만든다(C1c-2b 의 첫 그리기 멈춤 약 95 ms 가 출발 프레임에 나지 않게).
  - **순위 로직(`RaceOrder`·`RaceStandings`)은 바꾸지 않았다.** 같이 출발하면 "같은 틱 수" 가 곧 "같은 시각" 이다. 주석만 고쳤다.
  - **R(재시작)의 뜻 (사용자 질문)**: 처음부터 다시(`StartRun` 의 전체 초기화)다. 떨어지면 런이 끝난다(`RunnerMotor.cs:215` → `FailRun`) — 트랙 복귀는 없다. 복귀는 별도 큰 작업으로 HANDOFF 2절 후보에 올렸다.
- **버린 대안**: 카운트다운을 Net 전용 뷰(`RaceCountdownView`, 새 파일)로 — 싱글에도 넣기로 해 코어 기능이 됐다. 서버가 카운트다운 시각을 정해 알리기 — 클라마다 신호 도착이 지연만큼 달라도 서버가 입력이 모두 쌓인 프레임에 출발시키면 공정하다(먼저 준비된 쪽은 서버 버퍼에서 기다릴 뿐). 이번 레이스에서 그 차이는 0~1틱(아래). 경기 중에도 R 을 허용하고 카운트다운 동안만 막기 — 멀티에서 혼자 처음부터 다시는 의미가 없고 예측기를 고장 낸다.
- **검증 (`feature/p3-c1-same-start`, 에디터 6000.3.19f1 — 이 머신의 로컬 버전, 시각은 XML 의 UTC, 코드 `git diff -- Assets` sha1 `6cc816a7`)** — 실행한 것 전부
  - 컴파일: `AssetDatabase.Refresh` 뒤 `ChainRushGame.CountdownSeconds` 를 참조한 `Unity_RunCommand` 가 컴파일·실행됨(값 3), `RaceWire`·`RaceNetwork`·`ChainRushTests` 타입 로드, Console Error 0건.
  - 새 테스트만 먼저(06:13:22Z~06:13:32Z): `testcasecount=3 Passed passed=3 failed=0` — `StartCountdown_CountsThreeTwoOne_ThenStartsTheRunOnce`, `StartCountdown_TheRunBeginsAsOneStartedAtOnce_AndAgainStartsTheCountOver`, `NetRemoteTests.Wire_GoRoundTrips_AndAnythingElseThrows`. 새 동작의 테스트라 고치기 전 실패 실행은 없다(컴파일 불가).
  - EditMode(06:13:45Z~06:13:51Z): `testcasecount=237 Passed passed=237 failed=0`.
  - PlayMode 게이트 1회(전체, Device·Sweep 제외) `testcasecount=144 result=Passed passed=141 failed=0 skipped=3`(06:14:11Z~06:23:58Z, 587.2초). skipped 3 은 `[Explicit]` 네트워크 레이스.
  - `Run Device Input Tests`(메뉴 키 경로를 바꿨으므로, 06:24:38Z~06:24:54Z): `testcasecount=3 Passed passed=3 failed=0`. 기존 2건은 Enter 뒤 바로 `IsRunning` 이던 단언을 "카운트다운 중 → 끝난 뒤 달림" 으로, R 뒤 공격을 카운트다운이 끝난 뒤로 바꿨다(의도한 동작 변경). 새 1건 `Input_ManualStartOff_EnterAndRDoNothing`.
  - 플레이어 재빌드(서킷 씬만): `Build Succeeded: 0 errors`, Editor.log `Build Finished, Result: Success.`, `ProtoHarness.Net.dll` 15:25:35(+0900, 72192 바이트). 자동으로 바뀐 5파일은 되돌렸다(4개 지정 `git restore`, `ProjectSettings.asset` 은 빌드 전 사본, md5 `de64508f` 로 같음).
  - 네트워크 레이스 A·B·C 한 실행(06:26:21Z~06:28:05Z) `testcasecount=3 Passed passed=3 failed=0`. 세 레이스 모두 새 단언 통과: 서버 go 1줄, 두 레이서가 같은 프레임에 출발(A 517, B 621, C 564), 에디터·플레이어 클라 go·카운트다운 각 1회. 출발 때 서버 버퍼: A 4/3, B 2/1, C 3/3(먼저 준비된 쪽의 추가 대기 0~1틱).
    - A: 보정 0 / 0, late 0 / 0, 상대 위치 1001(0 differ, 빈틈 0), **에디터가 상대 첫 위치를 받은 자기 틱 9**(이전 따로 출발 때 80~180틱 늦음 — 새 단언 `0..25`), 고스트 최대 0.318 m, 프레임 최장 31.9 ms.
    - B: 에디터 보정 143, 플레이어 1, 레이서 1 `Failed@657` 를 두 클라가 같게 받음, 에디터 고스트 숨김, 상대 첫 위치 자기 틱 12.
    - C: 순서 `0:0=1 1=1, 155:0=1 1=2, 697:0=2 1=1` 을 서버·두 클라가 그대로 지남, 상대 위치 1001개가 오프라인 런과 비트 일치, 보정 0 / 0, late 0 / 0, 상대 첫 위치 자기 틱 10.
  - PlayMode 게이트 2회(전체 — 코어 `ChainRush/` 를 바꿨으므로 축소 대상 아님): `testcasecount=144 result=Passed passed=141 failed=0 skipped=3`(06:28:44Z~06:38:37Z, 592.2초). 같은 코드(sha1 `6cc816a7`)에서 1회에 이은 연속 통과.
- **한계**: 카운트다운·"GO!"·대기 문구는 화면으로 보지 않았다(레이스 도중 `Unity_RunCommand` 금지, 싱글은 테스트로 상태만 확인). 카운트다운 길이는 3초 상수. 신호 뒤 이탈·늦은 접속 거부 경로는 실제 레이스로 시험하지 않았다. 2인·같은 PC만.
- **하지 않은 것**: 앞뒤 거리 표시(사용자 결정), 트랙 복귀, 소리, 출발 반칙 판정, 3인 이상, `RaceOrder`·`RaceStandings` 로직.

## 2026-10-13 · P3 C1 다른 입력의 두 레이서: 실제 네트워크 레이스에서 주행 중 순위가 뒤집혔다

- **결정 (사용자 승인, 2026-10-13 요청서에 "ㄱㄱ")**: HANDOFF 2절의 "실제 레이스에서 순서가 뒤집히는 실행" 과 "P3 C1 마무리 측정" 의 "서로 다른 입력" 을 `NetworkRaceTests` 레이스 C(`[Explicit]`, `NetRace`)로 한다. 런타임 코드는 바꾸지 않았다(플레이어 클라는 원래 `-raceInputLog` 로 로그 파일을 따로 받는다).
  - **다른 입력 = 같은 봇 + 다른 드리프트 구간.** 드리프트는 지상 달리기 속도를 0.9배로 만든다(`RunnerMotor.cs:36, 150`). 플레이어 클라 레이서는 100–300틱, 에디터 클라 레이서는 400–800틱에 드리프트를 덧씌운다(`NetworkRaceTests.DriftWindow`, 이 파일 안의 중첩 클래스). 봇에는 속도 입력이 없어 이것이 가장 작은 변경이다.
  - **기대값은 오프라인에서 만든다.** 두 로그를 에디터에서 각각 재생해 틱별 상태를 서버와 같은 방식으로 찍고(틱 0 = `StartRun` 직후, 이후 매 틱 — `ServerInputBuffer.AfterTick`), 그 진행 거리를 같은 `RaceOrder`(임계 1 m)에 넣어 순서 변화 목록을 만든다. 같은 틱의 반복 비교는 결과를 바꾸지 않고 비교 틱은 1씩만 오르므로, 출발 시각 차이와 무관하게 서버가 같은 목록을 지나야 한다. 한 레이서만 순위가 있는 줄은 어느 프로세스가 먼저 출발했는지에 달려 빼고 비교한다.
  - **판정**: 오프라인 전제(두 런 모두 1000틱까지 낙하 없음, 에디터 레이서가 앞섰다가 추월당함), 두 클라 보정 0, 늦은 입력 0, 1000틱 안에 런 끝 없음, 에디터가 받은 상대 위치가 상대 오프라인 런과 비트 단위로 같음(틱 0부터, 빈틈 0), 서버·에디터 클라·플레이어 클라의 순서 변화 목록(1000틱 안)이 오프라인 목록과 같음.
  - 레이스 A·B 의 판정은 그대로다. `Scenario` 는 클라별 로그를 받을 수 있게 됐고, 에디터 로그 감시는 `RACE-CLIENT-ORDER` 줄도 모으며 플레이어 클라가 떠날 때까지 붙어 있다(상대가 늦게 출발하면 에디터가 끝난 뒤에도 1000틱 안의 순서가 온다).
- **결과**: 오프라인 `0:0=1 1=1, 155:0=1 1=2, 697:0=2 1=1`(1000틱 진행 거리 에디터 190.82 m, 플레이어 193.86 m). 실제 레이스에서 서버·두 클라가 모두 이 순서를 지났다 — **주행 중 순위가 처음으로 실제로 뒤집혔다.**
- **버린 대안**: 체인 액션(슬링샷)으로 추월 — 게이지 한 칸이 있어야 발동하고(`RunnerMotor.ChainAction`) 봇의 런에서 게이지가 언제 차는지는 확인하지 않았다. 드리프트만으로 안 되면 보고하기로 했던 대안이고, 드리프트만으로 됐다. 봇을 지그재그로 — 진행 거리 손실을 예측할 수 없다. 별도 봇 파일 — 이 테스트에서만 쓰여 새 파일을 만들지 않았다.
- **검증 (`feature/p3-c1-different-inputs`, 에디터 6000.3.19f1 — 이 머신의 로컬 버전, 시각은 XML 의 UTC, 코드 `git diff -- Assets` sha1 `877386a8`)** — 실행한 것 전부
  - 컴파일: `EditorUtility.scriptCompilationFailed=False`, `NetworkRaceTests` 의 새 메서드 로드 확인, Console Error 0건(Warning 19건은 모두 리로드의 `Deleting invalid font reference.`).
  - 플레이어 재빌드(서킷 씬만): Editor.log `Build Finished, Result: Success.`, `ProtoHarness.Net.dll` 14:30:57(+0900, 70144 바이트). 자동으로 바뀐 5파일은 되돌렸다(4개 파일 지정 `git restore`, `ProjectSettings.asset` 은 빌드 전 사본, md5 `de64508f` 로 같음).
  - 레이스 C 1회 05:31:57Z~05:32:28Z **`Failed`**: `racer 0: an input came late. Expected: 0 But was: 7`. `editor frames: longest 136.3 ms after tick 439, over 50 ms 1 [439:136]`, `server racer 0: ran 1243, received 1000, missed 250, late 7`. 그 판정 뒤의 값은 로그로 확인했다: 두 클라 `corrections=0`, 서버 `RACE-SERVER-ORDER` v=2 `tick=0 0=1 1=1` → v=3 `tick=155 0=1 1=2` → v=4 `tick=697 0=2 1=1`, 에디터·플레이어 클라 `RACE-CLIENT-ORDER` 같은 세 줄.
  - 레이스 C 2회 05:33:27Z~05:34:01Z `Passed`: `remote positions: 1161 received, 1001 within 1000 ticks checked against the player's offline run, 0 differ`, `orders: expected 0:0=1 1=1, 155:0=1 1=2, 697:0=2 1=1; server 같음; editor client 같음; player client 같음`, 보정 0 / 0, late 0 / 0, `longest 47.4 ms after tick 168, over 50 ms 0`.
  - 레이스 A·B(한 실행 05:34:25Z~05:35:29Z, `testcasecount=2 Failed(Child) passed=1 failed=1`): A **`Failed`** `racer 0: an input came late. Expected: 0 But was: 5`, `longest 125.7 ms after tick 282, over 50 ms 1 [282:126]`, 보정 0 / 0, 상대 위치 894(0 differ). B `Passed`: 에디터 보정 155, 낙하 없음, order 3줄 마지막 `0=2 1=1`.
  - 레이스 A 재실행 05:47:38Z~05:48:11Z `Passed`: late 0 / 0, `longest 29.7 ms after tick 2, over 50 ms 0 []`.
  - EditMode(05:36:08Z~05:36:14Z): `testcasecount=237 Passed passed=237 failed=0`.
  - PlayMode 게이트 1회(전체, Device·Sweep 제외) `testcasecount=141 result=Passed passed=138 failed=0 skipped=3`(05:36:32Z~05:46:13Z, 581.2초). skipped 3 은 `[Explicit]` 네트워크 레이스 A·B·C.
  - 2회(`^Net$` — 바뀐 `Assets/` 파일은 `NetRace` 카테고리 클래스 `NetworkRaceTests.cs` 하나) `testcasecount=61 Passed passed=61 failed=0 skipped=0`(05:46:46Z~05:47:14Z), `NetworkRaceTests` 0건.
- **실패 두 건의 원인 (증명 못 함)**: 둘 다 레이스 도중 에디터 프레임 한 번이 125~136 ms 멈춘 것과 같이 났다. 입력 버퍼 3틱(약 60 ms)을 넘는 멈춤이라 에디터 입력이 늦게 닿는다. 2026-10-12 "네트워크 레이스 A" 의 한계(같은 증상, 원인 미분리)와 같은 모양이다. 사용자 판단(2026-10-13): **에디터에 포커스가 없어서** 그런 것 같다. 이번 실행 중 포커스 상태는 기록하지 않았고 나누어 시험하지도 않았다. A 경로에서 바뀐 것은 로그 감시의 정규식 하나와 감시를 떼는 시점뿐이다. 같은 코드의 재실행은 각각 통과했다 — 재실행 통과로 실패를 지우지 않는다. 레이스 A·C 는 병합 게이트 밖(`[Explicit]`)이다.
- **한계**: 2인·같은 PC·고정 지연(지터 없음)만. 순서 변화는 두 번(동률 → 앞섬 → 추월)이고 1 m 임계 안에서 오가는 경우는 실제 레이스로 보지 않았다. 패널의 숫자는 화면으로 보지 않았다(레이스 도중 `Unity_RunCommand` 금지).
- **하지 않은 것**: 런타임 코드, 앞뒤 거리 표시, 같은 시각 출발, 3인 이상, 패킷 유실, 모바일/IL2CPP, 에디터 프레임 멈춤 조사.

## 2026-10-13 · 병합 2회차 축소: Net 쪽만 바꾼 브랜치는 PlayMode 2회차를 `Net` 카테고리만 돌린다

- → 2026-10-14 "테스트 3단" 으로 대체: 병합(②)에서 2회차가 없어져 이 조항도 지웠다. 아래 근거(asmdef 경계, 필터 소스)는 그 결정이 그대로 쓴다.
- **결정 (사용자 승인, 2026-10-13 요청서에 "ㄱㄱ" — HANDOFF 3절 #1 의 B안을 어셈블리 경계로 좁힌 안)**: 병합 조건의 PlayMode 는 1회차를 지금처럼 전체(`Device`·`Sweep` 제외)로 돌린다. 2회차는 `git diff --name-only dev...HEAD` 의 `Assets/` 파일이 모두 `Runtime/Net/`, `Tests/EditMode/`, `Net`·`NetRace` 카테고리 테스트 클래스 파일일 때만 `Net` 카테고리로 줄이고, 그 밖이 하나라도 있으면 전체다. 규칙 본문은 `CLAUDE.md` §9-2, 실행 방법은 `docs/RULES/VERIFICATION.md` §4-4.
- **이유**: ① 사용자 질문(2026-10-05 "작업한 부분만 검증하면 안 되나"). 게이트 1회가 이 머신에서 약 584~660초라 병합마다 약 20분이 든다. ② 2026-10-13 14:07 게이트 XML(140건, 584.2초)의 클래스별 시간: `ChainRushProceduralTests` 154초, `ChainRushReplayTests` 99초, `ChainRushCircuitTests` 73초 … `Net` 카테고리 5개 클래스 합계 약 30초. ③ 최근 코드 병합 7건 중 6건(C1c-1 ~ C1c-3)이 `Runtime/Net`·`Tests/PlayMode` 만 바꿨다(C1c-1 은 `DefaultNetworkPrefabs.asset` 도 — 새 규칙에서는 전체 2회차다). ④ asmdef 상 `ProtoHarness.Net` 을 참조하는 것은 PlayMode 테스트 어셈블리뿐이다(`ProtoHarness.Runtime` 은 `Unity.InputSystem` 만, `ProtoHarness.Editor` 는 `ProtoHarness.Runtime`·TestRunner 만 참조). Net 변경은 코어 테스트의 동작을 바꿀 수 없다.
- **감수한 것**: 2회 실행은 간헐 실패를 잡으려고 넣었다(아래 2026-10-02 "병합 조건 강화"). 그때의 간헐 실패는 지금 게이트에서 빠진 `Device` 테스트였다. Net 만 바꾼 브랜치에서는 코어 테스트를 1회만 보므로, 코어 테스트에 새 간헐 실패가 생겨도 그 브랜치에서는 한 번만 볼 수 있다. 그 코드는 이번 변경이 바꿀 수 없는 코드다.
- **필터에서 확인한 것** (test-framework 패키지 소스 `RuntimeTestRunnerFilter.cs`): `categoryNames` 는 정규식(`IsRegex = true`, `:31`)이라 `"Net"` 이 `NetRace` 에도 걸린다 — 2026-10-11 C1c-2a 에서 `Net` 카테고리로 고르면 `[Explicit]` 네트워크 레이스가 같이 돈 원인이다. 그래서 `"^Net$"` 를 쓴다. 필터 종류끼리는 AND(`:43`)라 "카테고리 + 다른 클래스" 를 한 실행으로 고를 수 없어서, 다른 PlayMode 테스트 클래스를 바꾼 브랜치는 2회차 전체로 정했다.
- **시험 실행** (`docs/merge-gate-second-run`, 에디터 6000.3.19f1, 시각은 XML 의 UTC): `categoryNames = { "^Net$" }` → `testcasecount="61" result="Passed" passed="61" failed="0" skipped="0"` (05:19:01Z~05:19:29Z, 28.6초). `NetworkRaceTests` 0건. 61 = 직전 게이트 XML 의 `NetReconcileTests` 24 + `NetRemoteTests` 9 + `RaceOrderTests` 9 + `RaceStandingsTests` 9 + `RaceStandingsTextTests` 10 과 같다.
- **버린 대안**: B 원안("바뀐 영역의 클래스만") — 코어 코드는 거의 모든 테스트가 거쳐 가서 "영역" 을 사람이 판단하게 되고, 그 판단이 조용한 우회가 된다. C(영향 범위만, 1회) — 간헐 실패 경보가 없어진다. D(1400 m 장거리 테스트 배속 3 → 5) — 테스트 동작을 바꾸므로 별도 합의. 2회차용 메뉴 항목 추가 — `Assets/` 변경이 되어 이 브랜치가 게이트를 치러야 하고, `Unity_RunCommand` 한 줄로 충분하다.
- **하지 않은 것**: 코어를 바꾼 브랜치의 2회차 축소, D안, Device·Sweep·네트워크 레이스 규칙.

## 2026-10-13 · P3 C1c-3: 서버가 주행 중 순위를 같은 틱 수의 진행 거리로 정해 알린다

- **결정 (사용자 승인, 2026-10-13 C1c-3 요청서에 "ㄱㄱ" — 기준 A·임계 1 m 추천안)**: HANDOFF 2절의 "진행 중 순위" 를 한다.
  - **기준은 각자 런의 같은 틱에서의 진행 거리**다. 서버의 `RaceOrder`(Net, 순수 로직)가 레이서마다 틱별 `LapCounter.Progress`(출발선부터 랩 포함 미터, 서버가 이미 디코드하는 상태에 들어 있음 — `RaceServer.OnState` 의 `snapshot.Circuit.Laps.Progress`)를 쌓고, 달리는 레이서 모두가 도달한 가장 최근 틱에서 비교한다.
  - **흔들림 막기**: 1 m(`RaceServer.OrderMarginMetres`) 넘게 앞서야 앞선 것으로 바꾼다. 거리가 정확히 같으면 공동이다. 그 사이면 직전 관계를 유지하고, 처음 비교하는 쌍은 더 간 쪽이 앞이다. 순위 = 1 + 앞선 레이서 수(공동은 같은 순위). 달리지 않거나 아직 상태가 없는 레이서는 순위 0.
  - **메시지는 새 `race.order`**(비교 틱 4바이트, 레이서 수 1바이트, 레이서별 순위 1바이트). 순위가 바뀔 때만 모든 클라에 각자의 `ToOwner` 큐로, 같은 사건의 standings 뒤에 보낸다. 디코딩은 엄격하다(순위 > 레이서 수, 비교 틱 < -1, 비교 틱 -1 인데 순위 있음은 던짐). 클라(`RaceClient.Order`·`OrderTick`·`OrderReceived`)는 받아 두기만 하고, 레이서 수가 standings·이전 order 와 다르면 던진다.
  - **표시**: `RaceStandingsText.Lines(entries, order, you)` 오버로드. 주행 중 레이서는 결승자 수 + 1 + (standings 도 주행 중이라고 하는, order 상 앞선 레이서 수)로 숫자를 보이고 그 순서로 줄을 선다. order 가 아직 그 레이서를 매기지 않으면 "–". 두 메시지 사이에 standings 가 이미 끝냈다고 하는 레이서는 standings 줄을 따르고 주행 중 순위 계산에서 빠진다(2인 레이스에 "3" 이 뜨지 않게). 기존 `Lines(entries, you)` 는 order 없이 같은 결과. `RaceStandingsView` 는 order 가 바뀔 때도 줄을 다시 만든다(새 글리프 없음).
- **이유**: 레이서는 각자 입력이 쌓이면 따로 출발하고(`RaceServer.cs` Update) 플레이어 클라는 80~180틱 늦다. 결승 순위가 각자 걸린 틱 수로 정해지므로(`RaceStandings.cs:7-12`), 같은 시계로 비교해야 도중 순위와 결승 순위가 어긋나지 않는다. 지금 순간의 거리로 비교하면 먼저 출발한 쪽이 출발 차이만큼 늘 앞선다(같은 입력이면 항상 1등).
  - 버린 대안: 지금 순간의 거리(위 이유), 같은 시각 출발 후 지금 거리(서버 출발 방식 변경 — 범위 밖), 기존 `race.standings` 의 주행 중 줄에 순위 넣기(와이어 규칙 "주행 중은 순위 0" 과 기존 테스트 기대값 "0=Running 1=Running" 이 바뀐다. 순서는 결승 순위보다 훨씬 자주 바뀌므로 메시지를 나눴다), 임계 없이 매 틱 비교(붙어 달리면 매 틱 메시지).
- **테스트**: `RaceOrderTests` 9개(`Net`, 새 클래스라 고치기 전 실패 실행은 없다) — 같은 틱 비교라 먼저 출발한 것은 앞섬이 아니다, 임계 안에서는 유지·넘으면 교체, 같은 거리는 공동, 비교 틱은 모두가 도달한 가장 최근 틱, 달리지 않거나 상태 없는 레이서는 순위 없음·끝나면 빠짐, 변화 없으면 `false`·버전 유지, 순서가 틀린 사건은 던짐, 와이어 왕복, 손상된 메시지는 던짐. `RaceStandingsTextTests` 4개 추가 — 결승자 다음에 order 순, 공동 순위는 번호순, order 와 standings 가 엇갈리면 standings 를 따름, 크기가 다른 order 는 던짐. `NetworkRaceTests` 단언 추가(기존 단언은 그대로): A 는 서버·두 클라의 마지막 order 가 `0=1 1=1`(같은 입력이라 같은 틱의 거리가 비트 단위로 같다 — 서버는 빠진 입력에 직전 입력을 반복하므로 클라가 멈춘 뒤에도 같다, `ServerInputBuffer.cs:8-10`), B 는 두 클라가 order 를 1개 이상 받았고 거부하지 않았다.
- **검증 (`feature/p3-c1c3-running-order`, 에디터 6000.3.19f1 — 이 머신의 로컬 버전, 시각은 XML 의 UTC, 코드 `git diff -- Assets` + 새 파일 2개 sha1 `da00da1e`)** — 실행한 것 전부
  - 컴파일: `ProtoHarness.Net.dll` 13:40:09(+0900), `ProtoHarness.Tests.PlayMode.dll` 13:40:05 갱신, Editor.log `error CS` 없음, `RaceOrder`·`RaceOrderTests` 타입 로드, Console Error 0건.
  - 단위(`RaceOrderTests`·`RaceStandingsTextTests`·`RaceStandingsTests`, 04:41:32Z): `testcasecount=28 Passed passed=28 failed=0`.
  - 플레이어 재빌드(서킷 씬만): Editor.log `Build Finished, Result: Success.`, `Build Succeeded errors=0`(MCP 는 Sentis 셰이더 경고로 "실패" 라 보고), `ProtoHarness.Net.dll` 64000 → 70144 바이트(13:42:39). 자동으로 바뀐 5파일은 되돌렸다(4개 파일 지정 `git restore`, `ProjectSettings.asset` 은 빌드 전 사본, sha1 `fcef33b8b0b8` 로 같음).
  - 네트워크 레이스 (PC 를 쉬게 두고 에디터를 앞에 둠) — 모두 `testcasecount=1 passed=1 failed=0`:
    - A 04:43:38Z~04:44:21Z `Passed`: `remote positions at the editor: 922 (921 checked …, 0 differ; from its tick 0, 0 breaks; first at the editor's tick 88, the editor ended at tick 1000)`, late 0 / 0, `editor frames: longest 33.7 ms after tick 2, over 50 ms 0 []`. `orders: editor client "0=1 1=1" (2 taken); player client "0=1 1=1" (1 lines, refused False); server: 2 lines, last "0=1 1=1"`.
    - A 04:44:42Z~04:45:13Z `Passed`: 919 (첫 도착 91), late 0 / 0, `longest 29.2 ms`, 50 ms 초과 0. order 같음(서버 2줄).
    - A 04:45:32Z~04:46:04Z `Passed`: 921 (첫 도착 88), late 0 / 0, `longest 30.6 ms`, 50 ms 초과 0. order 같음(서버 2줄).
    - B 04:46:23Z~04:46:50Z `Passed`: 에디터 보정 12, 플레이어 클라 보정 1, `server racer 0: ran 1005, received 1000, missed 50, late 45; racer 1: ran 657, received 657, missed 23, late 23`, 레이서 1 낙하 `Failed@657`(플레이어 클라가 받음), **에디터 클라 고스트 숨김**(`editor client visible False (other ended True)`). 서버 order `v=1 tick=0 0=1 1=-` → `v=2 tick=0 0=1 1=1` → `v=3 tick=742 0=1 1=-`, 두 클라 마지막 `0=1 1=-`(에디터 3개, 플레이어 클라 2줄, 거부 없음). 에디터 상대 위치 657개 중 54개가 자기 런과 달랐지만 같은 틱 진행 거리 차이가 1 m 를 넘지 않아 순서는 끝까지 공동이었다.
  - EditMode(04:47:09Z~04:47:12Z): `testcasecount=237 Passed passed=237 failed=0`.
  - PlayMode 게이트(Device·Sweep 제외) 1회 `testcasecount=140 result=Passed passed=138 failed=0 skipped=2`(04:47:30Z~04:57:11Z, 580.3초), 2회 `140/Passed/138/0/2`(04:57:49Z~05:07:33Z, 584.2초). 게이트 뒤 Console Error 0건. skipped 2 는 `[Explicit]` 네트워크 레이스.
  - 입력 장치 경로와 코스 코드를 바꾸지 않아 Device·Sweep 은 해당 없음.
- **한계**: 실제 네트워크 레이스에서 순서가 **뒤집힌** 적은 없다(A 는 같은 입력이라 공동, B 는 차이가 1 m 안). 교체·임계는 `RaceOrderTests` 로만 확인했다. 패널의 숫자는 화면으로 보지 않았다(레이스 도중 `Unity_RunCommand` 금지) — 글자는 `RaceStandingsTextTests` 로만 확인했다. 3인 이상에서 쌍마다 따로 유지하는 관계가 비이행적이 되면(A>B, B>C, C≈A 를 유지) 순위가 어색할 수 있다(던지지는 않는다). 서버는 레이서마다 틱당 8바이트를 런 끝까지 쌓는다(3랩 약 8000틱 → 64 KB).
- **하지 않은 것**: 앞뒤 거리 표시, 같은 시각 출발, 3인 이상 실험, `RaceStandings`·`race.standings`, HUD·씬·프리팹·asmdef·`ProjectSettings`, 시뮬·예측기 코드, 에디터 프레임 멈춤 조사.

## 2026-10-12 · 네트워크 레이스 A 의 상대 위치 기준: 받은 개수를 출발 시각 차이와 무관하게 판정한다

- **결정 (사용자 승인, 2026-10-12 요청서에 "ㄱㄱ")**: `NetworkRaceTests` A 의 `RemoteSamples > 900`·`RemoteChecked > 900` 을 다섯 판정으로 바꾼다. ① 상대 첫 위치가 에디터 틱 500(= `ticks / 2`) 전에 도착 ② 상대 틱 0 부터 ③ 틱이 하나씩 이어진다(빈틈·중복·역행 0, `RemoteBreaks`) ④ 받은 수 ≥ (에디터 끝 틱 − 첫 도착 때 에디터 틱) − 5 ⑤ `RemoteChecked ≥ RemoteSamples − 1`. `Outcome` 에 `FirstRemoteAt`·`EndAt`·`FirstRemoteTick`·`RemoteBreaks` 를 더하고 `Describe` 에 같은 값을 적는다. B 의 `BRemote > 900` 은 그대로 둔다(플레이어 클라는 약 1010개로 안정).
- **이유 (원인 확인)**: 서버는 레이서마다 입력이 쌓이면 따로 출발시키고(`RaceServer.cs:312-318`) 매 상태를 상대에게 위치로 보낸다(`RaceServer.cs:226-233`). 플레이어 클라는 별도 프로세스라 부팅만큼(Δ ≈ 80~180틱) 늦게 출발하므로 에디터는 약 1000 − Δ 개를 받는다. 실행 12회(2026-10-12): 플레이어 클라 1009~1016개(일정), 에디터 834~921개. 그래서 고정 900 은 받은 흐름이 아니라 상대 프로세스의 부팅 시간을 쟀다(이 머신 변경 전 코드 899, 이전 머신 951~954). 채널은 `ReliableSequenced`(`CustomMessageManager.cs:300` 기본값)이고 지연 큐는 순서를 지키므로(`DelayedSender.cs:40-42`) A(지터 없음)에서는 빈틈 0 을 요구할 수 있다. 자기 틱 0 은 `TickCompleted` 로 보고되지 않아(`ChainRushGame.cs:193` 에서 틱을 올린 뒤 `:183` 호출) 대조 수는 받은 수 − 1 이 최소다. ④ 의 5틱은 첫 도착과 끝 틱을 잰 프레임 차이 몫이다.
  - 버린 대안: 900 을 낮추기(부팅이 더 느린 머신에서 또 깨진다), 서버가 보낸 수와 맞추기(서버 로그에 보낸 수가 없어 서버 코드를 바꿔야 한다 — 범위 밖), 두 클라를 같은 시각에 출발시키기(서버 동작 변경, 진행 중 순위·같은 시각 출발과 함께 정할 일).
- **검증 (`fix/net-race-remote-count`, 에디터 6000.3.19f1 — 이 머신의 로컬 버전, 시각은 XML 의 UTC, 코드 `git diff eb4d0d7 HEAD -- Assets` sha1 `f912746e`, 플레이어 빌드는 `dev`(`eb4d0d7`) 런타임 그대로 2026-10-12 15:27(+0900), `ProtoHarness.Net.dll` 64000 바이트)** — 실행한 것 전부
  - 컴파일: `ProtoHarness.Tests.PlayMode.dll` 16:09:11(+0900) > `NetworkRaceTests.cs` 16:08:52, `NetworkRaceTests` 타입 로드, Console Error 0건(실행 전·후).
  - 2026-10-12 (PC 를 다른 데 쓰는 중): A 3회 — 새 기준 ①~⑤ 는 3회 모두 성립.
    - A 07:11:57Z `Passed` — 925 ≥ 910, 틱 0부터, 빈틈 0, 첫 도착 85, 50 ms 초과 프레임 0.
    - A 07:12:56Z `Failed` — 912 ≥ 897(새 기준 성립). 실패는 바꾸지 않은 판정: `racer 0: an input came late. Expected: 0 But was: 3`. 에디터 50 ms 초과 프레임 5번(최대 93 ms, 틱 54~339).
    - A + B 07:14:12Z 둘 다 `Failed` — A: 919 ≥ 905, `But was: 7`(늦은 입력), 85·88 ms 멈춤. B: 에디터가 600~866 ms 씩 멈춰 `RACE-CLIENT-FAULT the server's state for tick 175 could not be taken (the client at tick 174)`.
    - 멈춤의 원인은 확인 못 했다. 남은 레이스 프로세스는 없었고 PC 에서 Media Player·브라우저 등이 돌고 있었다. 사용자가 중단했다.
  - 2026-10-13 (PC 를 쉬게 두고 에디터를 앞에 둠): A 3회 + B 1회 모두 `Passed`, 각 `testcasecount=1 passed=1 failed=0`.
    - A 04:01:39Z~04:02:31Z: `remote positions at the editor: 894 (893 checked against its own run, 0 differ; from its tick 0, 0 breaks; first at the editor's tick 118, the editor ended at tick 1000)` (기준 877), `server racer 0: … late 0; racer 1: … late 0`, `editor frames: longest 66.0 ms after tick 1000, over 50 ms 2 [5:53 1000:66]`.
    - A 04:03:06Z~04:03:41Z: `887 (886 checked …, 0 differ; from its tick 0, 0 breaks; first at the editor's tick 122, the editor ended at tick 1000)` (기준 873), late 0 / 0, `longest 37.1 ms after tick 2, over 50 ms 0 []`.
    - A 04:04:09Z~04:04:43Z: `893 (892 checked …, 0 differ; from its tick 0, 0 breaks; first at the editor's tick 117, the editor ended at tick 1000)` (기준 878), late 0 / 0, `longest 39.6 ms after tick 3, over 50 ms 0 []`.
    - B 04:05:11Z~04:05:45Z: 에디터 `892 (891 checked …, 0 differ; from its tick 0, 0 breaks; first at the editor's tick 121, the editor ended at tick 1001)`, 플레이어 클라 1015개, 에디터 보정 1, `server racer 0: ran 1277, received 1000, missed 278, late 1; racer 1: … late 0`, `longest 49.0 ms after tick 1000, over 50 ms 0 []`. 낙하 없음(`racer 0 none, racer 1 none`), 고스트 숨김 없음.
  - EditMode(04:06:06Z~04:06:09Z): `testcasecount=237 Passed passed=237 failed=0`.
  - PlayMode 게이트(Device·Sweep 제외) 1회 `testcasecount=127 result=Passed passed=125 failed=0 skipped=2`(04:06:35Z~04:15:59Z, 563.7초), 2회 `127/Passed/125/0/2`(04:17:02Z~04:26:32Z, 570.2초). skipped 2 는 `[Explicit]` 네트워크 레이스(`Needs the race build at NetworkRaceTests.RaceBuildExe and takes about a minute; select it by name.`) — 위에서 이름으로 따로 돌렸다.
  - 입력 장치 경로와 코스 코드를 바꾸지 않아 Device·Sweep 은 해당 없음. 플레이어 빌드를 다시 만들지 않아 설정 파일 자동 변경 없음.
- **한계**: 10-12 의 실패(늦은 입력·예측기 고장)는 바꾸지 않은 판정이고 에디터 프레임 멈춤과 같이 났다. 멈춤의 원인(포커스·백그라운드 스로틀·다른 프로세스 부하)은 나누어 보지 않았다 — PC 를 쉬게 둔 10-13 에는 50 ms 초과가 끝 틱 근처 2번뿐이었다. ④ 는 상대가 늦게 출발한 만큼은 판정하지 않으므로, 부팅이 에디터 틱 500 을 넘기면 ① 로 실패한다.
- **하지 않은 것**: 런타임·서버·클라 코드, 플레이어 재빌드, B 의 판정, 두 클라 같은 시각 출발, 에디터 프레임 멈춤 조사.

## 2026-10-12 · P3 C1c-2b: 클라가 순위를 화면에 보이고, 상대가 끝나면 고스트를 숨긴다

- **결정 (사용자 승인, 2026-10-12 C1c-2b 요청서에 "추천대로해서 진행", 멈춤 측정에 "진행하라", 수정에 "진행하자")**: HANDOFF 의 C1c-2b 중 순위 표시와 고스트 처리를 한다. 진행 중 순위(누가 앞인지)는 하지 않는다.
  - **표시는 `Net` 쪽 런타임 컴포넌트 `RaceStandingsView`(OnGUI)** 로 한다. `RaceClient.Begin` 이 `showRemote` 일 때 고스트와 함께 붙인다. 위치는 HUD 헤더 아래 왼쪽(1600×900 캔버스의 x=32, y=128, 폭 310), 색은 HUD 와 같다. 첫 순위 전에는 그리지 않는다. 줄은 새 순위가 왔을 때만 다시 만든다.
  - 줄은 `RaceStandingsText`(static)가 만든다: 결승자를 순위순으로, 그다음 주행 중 → 대기 → 낙하(같은 무리는 번호순). 순위 칸은 결승만 숫자, 나머지 "–". 이름은 "YOU" / "RACER n". 상태는 "대기"·"주행 중"·"낙하"·"mm:ss.s  잠정|확정"(시간 = `Ticks.ToSeconds(EndTick)`, 순위의 시계와 같은 틱 수).
  - **고스트는 상대의 줄이 결승이나 낙하가 되면 숨긴다**(`RemoteGhost.Hide`, `RACE-CLIENT-GHOST hidden`). 상대 위치에는 레이서 번호가 없어 "상대 = `1 - You`" 이므로, 고스트가 있는데 순위 줄이 2개가 아니면 던진다.
  - **패널의 첫 그리기를 접속 전으로 옮긴다.** 폰트와 글리프는 `Initialize` 에서 만들고(`RequestCharactersInTexture`), 첫 repaint 에서 패널 전체를 투명하게 한 번 그린다. 그리는 시간은 `RACE-STANDINGS-VIEW warmed in … ms`·`first draw in … ms` 로 한 번씩 남긴다. 그래픽 장치가 없으면(`-nographics`) 패널을 끈다.
- **이유**: `ChainRushHud` 는 `ProtoHarness.Runtime` 에 있어 `ProtoHarness.Net`(`RaceClient`)을 볼 수 없다(참조 방향). HUD 를 고치려면 Runtime 에 인터페이스를 새로 만들고 씬도 바꿔야 한다. `RemoteGhost` 와 같은 방식이면 씬·asmdef 를 건드리지 않는다.
  - **첫 그리기를 옮긴 이유 (측정)**: 처음 구현은 첫 순위가 온 프레임에 패널을 처음 그렸다. 그러면 에디터 클라가 그 직후 93~97 ms 멈추고(틱 9 → 14), 고정 지연 레이스 A 에서 에디터 클라 입력이 서버에 1~6개 늦게 닿았다(버퍼 3틱 ≈ 60 ms). 변경 전 `dev` 의 A 2회는 늦은 입력 0 이었다. 글리프만 미리 그리는 1차 수정은 효과가 없었다(94.2 ms, 늦음 1). 첫 그리기 전체를 접속 전으로 옮기자 그 그리기가 131~135 ms 걸렸고, 실제 첫 그리기는 0.4~0.8 ms 가 됐다. 무엇이 무거운지(스타일·재질·텍스트 메시 생성 등)는 나누어 재지 않았다.
  - 버린 대안: HUD 를 직접 고치기(위 이유), 상대가 결승하면 고스트를 결승선에 세워 두기(낙하·결승을 같이 다루는 쪽이 단순하다), 글리프만 미리 그리기(효과 없음, 위 측정).
- **테스트**: `RaceStandingsTextTests` 6개(`Net`) — 상태 4종의 글자, 잠정 → 확정, 결승자는 번호가 아니라 순위순·공동 순위는 번호순, YOU 표시, 순위 없음·모르는 레이서는 던짐, 헤더. 새 클래스라 고치기 전 실패 실행은 없다. `NetworkRaceTests` 에 단언 추가: A 는 에디터 클라에 순위 패널이 있고 두 클라 모두 고스트를 숨기지 않았다. B 는 각 클라의 고스트가 자기 마지막 순위에서 상대가 끝났을 때만 숨겨졌다. 판정 없이 기록만 하는 측정(연결 중 에디터 프레임의 최장 시간과 그 틱, 50 ms 초과 프레임, 첫 순위의 틱)을 `Describe` 에 넣었다.
- **검증 (`feature/p3-c1c2b-standings-view`, 에디터 6000.3.19f1 — 이 머신의 로컬 버전, 시각은 XML 의 UTC, 최종 코드 `git diff -- Assets` sha1 `2de0499a`)** — 실행한 것 전부
  - 컴파일: `ProtoHarness.Net.dll` 15:24:58(+0900) 갱신, Console Error 0건.
  - EditMode(06:31:44Z~06:31:52Z): `testcasecount=237 Passed passed=237 failed=0`.
  - PlayMode 게이트(Device·Sweep 제외) 1회 `testcasecount=127 result=Passed passed=125 failed=0 skipped=2`(06:32:24Z~06:42:15Z, 591.1초), 2회 `127/Passed/125/0/2`(06:42:45Z~06:52:41Z, 595.3초). skipped 2 는 `[Explicit]` 네트워크 레이스.
  - 플레이어 재빌드(서킷 씬만, `Succeeded errors=0`) 후 네트워크 레이스 `testcasecount=2 Passed passed=2 failed=0`(06:30:21Z~06:31:26Z, 65.5초). 그 직전 A 단독 1회도 `Passed`(06:28:55Z~06:29:29Z, 늦음 0, 50 ms 초과 프레임 0, warmed 135.2 ms, first draw 0.6 ms).
    - A: 보정 0, 상대 위치 909개 중 908 비트 일치·0 다름, 고스트 점프 0, 서버 레이서 0·1 late 0. 에디터 프레임 최장 39.3 ms, 50 ms 초과 0. warmed 131.2 ms, first draw 0.4 ms.
    - B: 이번에는 아무도 끝나지 않았다(`racer 0 none, racer 1 none`). 두 클라 마지막 순위 `0=Running 1=Running`, 고스트 숨김 없음(`editor client visible True (other ended False), player client hidden lines 0`). 에디터 클라 50 ms 초과 프레임 0. warmed 129.9 ms, first draw 0.8 ms.
  - 화면: 처음 구현의 A 도중 `ScreenCapture` 1장(리포 밖 PNG)으로 패널을 봤다 — "RACE / 2 RACERS", 민트색 "– YOU 주행 중", "– RACER 1 주행 중", HUD 헤더와 겹치지 않음. 그 장면에는 아래 고장으로 PAUSED 패널이 같이 떠 있었다. 최종 코드로는 찍지 않았다(그리는 코드는 같고 첫 그리기 시점만 다르다).
  - 중간에 실패한 실행(모두 처음 구현 또는 글리프 수정): A 도중 `Unity_RunCommand`(ScreenCapture) 직후 `RACE-CLIENT-FAULT the server's state for tick 230 could not be taken (the client at tick 229)` 1회, 늦은 입력으로 실패 6회(처음 구현 `racer 0: an input came late. But was: 6 / 2 / 1`, 측정 추가 뒤 `3 / 2`, 글리프 수정 뒤 `1`). 변경 전 `dev` 기준 A 2회: `Passed`(late 0) / `Failed`(late 0, `The other racer's positions must keep arriving. Expected: greater than 900 But was: 899`).
  - 빌드가 자동으로 바꾼 5파일은 매번 빌드 전 상태로 되돌렸다(4개는 파일 지정 `git restore`, `ProjectSettings.asset` 은 이 머신의 버전 차이가 이미 있어 빌드 전 사본으로 덮음).
  - 입력 장치 경로와 코스 코드를 바꾸지 않아 Device·Sweep 은 해당 없음.
- **한계**: ~~고스트 숨김은 실제 네트워크 레이스에서 한 번도 일어나지 않았다(이 머신의 B 2회 모두 낙하 없음, 이전 머신은 657 낙하).~~ → 2026-10-12 07:14Z `fix/net-race-remote-count` 의 B(런타임은 C1c-2b 가 병합된 `dev` `eb4d0d7` 빌드)에서 레이서 0 이 낙하(`Failed@657`)해 **플레이어 클라가 고스트를 숨겼다**(`RACE-CLIENT-GHOST hidden: racer 0 Failed@657`). 그 실행은 에디터 멈춤으로 `RACE-CLIENT-FAULT` 실패했으므로 에디터 클라 쪽 숨김과 B 의 숨김 단언 통과는 확인 못 했다(DECISIONS 2026-10-12 "네트워크 레이스 A 의 상대 위치 기준"). → 2026-10-13 C1c-3 의 B(04:46:23Z, `Passed`)에서 레이서 1 이 `Failed@657` 로 낙하해 **에디터 클라가 고스트를 숨겼고** B 의 숨김 단언이 통과했다(`editor client visible False (other ended True)`, DECISIONS 2026-10-13 "P3 C1c-3"). 결승·낙하·잠정·확정 줄은 화면으로 보지 않았고 `RaceStandingsTextTests` 로만 확인했다. A 의 상대 위치 기준(`> 900`)은 이 머신에서 변경 전 코드로도 899 로 떨어진다(이전 머신 951~954) — 이번 변경과 무관한 기존 테스트의 여유 문제다(→ 같은 날 "네트워크 레이스 A 의 상대 위치 기준" 에서 고침). 접속 전 첫 그리기(약 130 ms)는 출발 전이라 런에는 닿지 않지만, 클라를 만든 직후 접속이 그보다 빨리 끝나면 겹칠 수 있다(이번 실행은 모두 `connected False`).
- **하지 않은 것**: 진행 중 순위, 3인 이상 고스트, `ChainRushHud`·씬·프리팹·asmdef·`ProjectSettings`, 서버·예측기·시뮬 코드, A 의 상대 위치 기준(별도 `fix/`), HUD 의 `R / RUN AGAIN` 버튼이 네트워크 클라에서 예측기를 고장 내는 문제(HANDOFF 2절).

## 2026-10-11 · P3 C1c-2a: 서버가 순위를 정해 모든 클라에 알린다 (틱 수 기준, 잠정·확정)

- **결정 (사용자 승인, 2026-10-11 C1c-2a 요청서에 "ㅇㅇㄱㄱ")**: HANDOFF 의 C1c-2(순위 계산·상대에게 종료 알림·표시) 중 **계산과 알림만** 한다. 표시는 C1c-2b 로 나눈다(화면 확인이라 검증 방식이 다르다).
  - **순위 = 각자 출발부터 결승까지의 틱 수.** 서버는 레이서마다 입력이 모이는 대로 따로 출발시키고(`RaceServer.Update`), 네트워크 레이스에서도 두 클라의 접속이 몇 초씩 다르다. 실제 시각으로 재면 늦게 출발한 쪽이 손해다. 같은 틱은 공동 순위(1, 1, 3), 낙하는 순위 없음(확정).
  - **잠정과 확정.** 결승한 레이서의 순위는 다른 레이서가 그보다 적은 틱으로 결승할 수 있는 동안 잠정이다: 아직 대기 중이거나, 달리는 중인데 그 틱을 아직 못 넘긴 레이서가 있을 때. 틱 t 를 넘기고도 달리는 레이서는 t 이하로 끝날 수 없다. 나머지가 모두 끝났거나 그 틱을 넘기면 확정이다.
  - `RaceStandings`(`Runtime/Net/`, 순수 클래스)가 출발·상태(틱을 넘김)·끝을 받아 순위·확정을 다시 계산하고, 줄이 바뀔 때만 `Version` 을 올린다. 순서가 틀린 사건(두 번 출발, 출발 전 끝, 틱 되돌림, 넘긴 틱 이하의 끝)은 던진다.
  - 서버는 바뀔 때마다 `race.standings`(받는 레이서 번호 + 레이서마다 상태·끝 틱·순위·확정, 5+7N 바이트)를 **각 클라의 `ToOwner` 큐**로 보낸다. 같은 순서 보장 큐라 자기 `race.end` 가 그 끝을 담은 순위보다 언제나 먼저 도착한다. 클라가 없는 레이서 몫은 버려지고(`Dropped`) 다음 변경 때 받는다. 디코딩은 엄격하다(길이, 모르는 상태, `RaceStandings` 가 만들 수 없는 줄은 던짐).
  - 클라(`RaceClient`)는 받은 순위를 `Standings`·`You` 로 들고 `RACE-CLIENT-STANDINGS` 를 남긴다. 런은 읽지 않는다. 자기 줄이 목표 틱 안에서 끝났다고 하는데 예측기가 받은 `race.end` 와 다르면(없거나 틱·이유가 다름) 던진다(같은 큐로 먼저 와야 하므로 버그). 고장 난 예측기는 확인하지 않는다.
- **이유**: DESIGN §3 에서 순위는 서버가 소유하는 레이스 상태다. 진행 중 순위가 아니라 결승 순서부터 한 것은 서버가 이미 끝(틱·이유)을 알기 때문이다(C1c-1).
  - 버린 대안: 결승 실제 시각 순(위 불공정), 잠정 없이 들어온 순서대로 확정(늦게 출발한 레이서가 적은 틱으로 결승하면 화면의 순위가 뒤집힌다), 상대 위치 큐(`ToOthers`)로 보내기(메시지 이름이 하나로 고정돼 있고 자기 끝과의 순서를 보장하지 못한다), EditMode 테스트(EditMode asmdef 는 `ProtoHarness.Net` 을 참조하지 않는다. asmdef 를 바꾸지 않으려고 `NetRemoteTests` 처럼 PlayMode `[Test]` 로 뒀다).
- **테스트**: `RaceStandingsTests` 9개(`Net`) — 틱 수 1위와 넘긴 뒤 확정(7923 은 아직, 7924 에서 확정), 늦게 출발한 레이서가 적은 틱으로 잠정 1위를 빼앗음, 같은 틱 공동·다음 순위 건너뜀, 대기 중 레이서가 확정을 막음, 낙하는 순위 없음·확정·결승을 막지 않음, 줄이 안 바뀌는 틱은 변경 아님, 순서 틀린 사건은 던지고 아무것도 안 바꿈, 메시지 왕복(받는 레이서 4가지), 손상 바이트 13가지. 새 클래스라 고치기 전 실패 실행은 없다(컴파일 불가). `NetworkRaceTests` 에 단언 추가: A 는 두 클라의 마지막 순위가 `0=Running 1=Running` 이고 각자 서버가 배정한 레이서 번호를 안다. B 는 목표 틱 안의 서버 끝을 **두 클라 모두**(상대 클라 포함) 같은 틱·이유로 받고, 그렇지 않은 레이서는 달리는 중이거나 목표 틱 뒤에 끝났다. 에디터 클라의 순위는 플레이어 클라가 나간 뒤에 읽는다(자기 런이 끝난 뒤에 오는 상대의 끝까지 받게).
- **검증 (`feature/p3-c1c-standings`, 에디터 6000.3.25f1, 시각은 XML 의 UTC, 코드 `git diff -- Assets` sha1 `48c97826`)** — 실행한 것 전부
  - 컴파일: `AssetDatabase.Refresh` 뒤 `ProtoHarness.Net.dll`·`ProtoHarness.Tests.PlayMode.dll` 07:24:39Z 갱신, Console Error 0건.
  - `RaceStandingsTests` 만(07:25:19Z): `testcasecount=9 Passed passed=9 failed=0`.
  - EditMode(07:25:32Z~07:25:35Z): `testcasecount=237 Passed passed=237 failed=0`.
  - PlayMode 게이트(Device·Sweep 제외) 1회 `testcasecount=121 result=Passed passed=119 failed=0 skipped=2`(07:25:52Z~07:35:07Z, 554.7초), 2회 `121/Passed/119/0/2`(07:35:31Z~07:44:49Z, 558.0초). skipped 2 는 `[Explicit]` 네트워크 레이스.
  - 플레이어 재빌드(서킷 씬만, `Succeeded, errors=0 warnings=0`, 14.7초) 후 네트워크 레이스 `testcasecount=2 Passed passed=2 failed=0`(07:45:39Z~07:46:33Z, 54.0초).
    - A(고정 지연): 두 클라 보정 0, 상대 위치 954개 비트 일치, 고스트 점프 0. 순위: 에디터 클라 `you=0 "0=Running 1=Running"`(2개 받음), 플레이어 클라 `you=1` 같은 줄(1개 — 첫 순위는 접속 전이라 버려짐), 서버 2줄.
    - B(지터): 서버 레이서 1 이 657 에서 낙하. 플레이어 클라(레이서 1) `ended=Failed@657`, 순위 `you=1 "0=Running 1=Failed@657"`(2줄, 거부 없음). **에디터 클라(레이서 0)도 상대의 낙하를 `0=Running 1=Failed@657` 로 받았다**(3개). 서버 3줄. 에디터 클라 보정 144회.
  - 빌드가 자동으로 바꾼 5파일(`ProjectSettings.asset`·`UnityConnectSettings.asset`·`Assets/Settings/` 3개)은 파일을 지정해 `git restore`, 그 뒤 sha1 그대로 `48c97826`.
  - 입력 장치 경로와 코스 코드를 바꾸지 않아 Device·Sweep 은 해당 없음.
- **한계**: 결승은 네트워크 레이스에서 일어나지 않는다(봇 1000틱, 3랩 결승은 약 7924틱). 결승 순위·확정은 `RaceStandings` 단위 테스트로만 확인했다. 아직 출발하지 않은 레이서(접속 안 함)가 있으면 어떤 순위도 확정되지 않는다. 상대 위치 스트림은 여전히 2인 전제다(순위는 N명).
- **하지 않은 것**: 표시(HUD 순위 줄·결과 패널·고스트 처리, C1c-2b), 진행 중 순위, 같은 시각 출발, 3인 이상, `ClientPredictor`·시뮬 코드, 씬·프리팹·`ProjectSettings`, 손상 바이트 디코딩의 고장 처리(HANDOFF 2절).

## 2026-10-11 · P3 C1c 예측기 구멍 4개: 재시작·상태 예외는 고장, 일시정지 중 jolt, 되살아난 클라 따라잡기

- **결정 (사용자 승인, 2026-10-11 `fix/` 요청서에 "진행하라", 기존 테스트 분리는 "케이스별로 분리 (추천)")**: HANDOFF 2절 "C1c-1b 의 남은 것" 중 ①~④ 를 고친다. ⑤(S+L 상한 재시험)는 다른 종료 조건이 없어 이번에 못 하므로 남긴다.
  - ① **네트워크 레이스에는 재시작이 없다.** `ClientPredictor.Clear()` 는 예측기가 live 틱의 입력을 한 번이라도 내준 뒤(`armed`, `Consume` live 경로에서 세움)에 불리면 초기화하지 않고 **고장**(`Faulted`, "the run was restarted after tick N …")이 된다. R·Enter(`ChainRushGame.cs:160-161`)와 HUD 버튼이 부르는 `StartRun` → `inputSource.Clear()`, 경기 중 `SetInputSource` 가 여기에 해당한다. **고장은 더 이상 아무것도 풀지 못한다**(전에는 `Clear` 가 풀었다). 새 경기는 새 예측기로 한다.
    - `armed` 를 `AfterTick` 에서 세웠던 1차 구현은 네트워크 레이스에서 바로 고장 났다: `RaceClient.Begin` 이 예측기를 만들면 `TickCompleted` 를 구독하는데, 접속 전 게임이 다른 입력원으로 도는 틱마다 `AfterTick` 이 불려 무장되고, 접속 때의 정상 `SetInputSource` → `Clear` 가 재시작으로 판정됐다(아래 검증의 `Net` 1회). 그래서 무장 시점을 "자기 입력을 내준 뒤"로 옮기고, 그 경우를 재시작 테스트 앞부분에 단언으로 넣었다.
  - ② **`OnServerState` 에서 나가는 모든 예외는 먼저 고장을 세운다**(원인·틱을 `FaultReason` 에, 예외는 그대로). `OnServerEnd` 와 같은 방식이다. 거부된 상태는 게임을 건드리지 않는다. `RaceClient.OnState` 는 `OnEnd` 처럼 `finally` 에서 `HaltOnFault` 를 부른다.
  - ③ **`LargestJolt` 의 출발 위치는 지금 틱의 기억된 예측 상태**(실행 중 또는 일시정지)다. 기억이 없으면(끝난 런) 재지 않는다. 전에는 Running 이 아니면 원점을 썼고, `RestoreSnapshot` 이 Running 으로 되돌리므로 목표 틱에서 멈춘 뒤의 보정은 원점부터 거리를 기록했다(테스트에서 실제 0.0162 m 가 143.2 m 로 기록됨).
  - ④ **끝난 클라가 끝난 틱보다 이른 서버 상태로 보정돼 다시 달리면, 리플레이 뒤 `ClientEndLead` 만큼 따라잡는다**(`serverTick + lead`, 상한 `catchUpLimit`). C1c-1b 의 "끝난 틱 이후 상태" 경로와 같은 규칙이다.
- **이유**: ①②는 깨진 예측이 조용히 이어지는 경로(§5)이고, ③은 DONE 줄의 `jolt=` 를 근거로 쓸 수 없게 만들었고, ④는 클라가 서버 뒤에 서서 이후 입력이 늦게 도착하게 만든다.
  - ①에서 버린 대안: `ChainRushGame` 의 메뉴 키를 네트워크 레이스에서 막기(입력 장치 경로라 Device 검증이 붙고 `ChainRushGame` 공개 API 가 바뀐다), `Clear` 가 예외를 던지기(`StartRun` 이 반쯤 초기화된 채 `Ready` 에 멈춘다).
- **테스트 (`NetReconcileTests`, 고치기 전 4개 모두 실패 → 고친 뒤 통과)**
  - 고치기 전 실행(05:12:44Z~05:12:48Z) `testcasecount=4 Failed(Child) passed=0 failed=4`, 실패 단언: ③ `Expected: 0.0162162203f But was: 143.223053f`, ② `state 21: a refused state must leave the predictor faulted. Expected: True But was: False`, ① `A restart in the middle of a network race must break the prediction. Expected: True But was: False`, ④ `The client catches up on live controls until it is as far ahead as when its run ended. Expected: (1, 6) But was: (0, 0)`.
  - 새 테스트: `ClientEnd_WhenAnOlderStateBringsAFallenClientBack_CatchesUp`(④: 클라가 누름을 놓쳐 낙하, 서버(참값) 상태 d 가 낙하 뒤 도착 → 되살아나 6틱 따라잡고 참값과 비트 일치, 이후 보정 없음), `Client_AStateThatCannotBeTaken_LeavesThePredictorFaulted`(②), `Client_RestartedDuringARace_IsFaultedAndStaysFaulted`(①, 접속 전 다른 입력원 틱은 무장하지 않음 포함), `Client_ACorrectionWhilePaused_MeasuresTheJoltFromWhereTheRunnerStands`(③).
  - **계약이 바뀌어 고친 기존 테스트 2개**(테스트를 통과시키려는 수정이 아님): `ClientEnd_BeforeTheStateForTheTickBeforeIt_ThrowsLoudly` 의 "Clear 가 고장을 푼다" → "Clear 는 고장을 풀지 못하고 첫 원인이 남는다". `Client_WithAServerStateFromTheFuture_ThrowsLoudly` 는 거부 뒤 같은 예측기로 이어서 비교하던 세 확인(미래 거부·현재 틱 비교·과거 거부)을 케이스마다 새 예측기(`PredictorAt10`)로 나눴다. 단언 내용은 같다.
- **검증 (`fix/p3-c1c-predictor-gaps`, 에디터 6000.3.25f1, 시각은 XML 의 UTC)** — 실행한 것 전부
  - 1차 구현 뒤 `Net` 카테고리(05:15:53Z~05:19:37Z): `testcasecount=35 Failed(Child) passed=33 failed=2`. 실패 2 는 `[Explicit]` 네트워크 레이스 2개(카테고리로 고르면 함께 돈다) — 에디터 클라가 `RACE-CLIENT-FAULT the run was restarted after tick 0 …` 로 멈춤. 위 ① 의 무장 시점 문제였고 고쳤다. 옛 플레이어 빌드 상대였으므로 그 밖의 값은 근거로 쓰지 않는다.
  - 무장 시점 수정 뒤 `NetReconcileTests` 만(05:21:53Z~05:22:18Z): `testcasecount=24 Passed passed=24 failed=0`. 그 뒤 주석 줄바꿈만 고쳤다.
  - 주석만 다듬은 뒤 같은 코드(`git diff -- Assets` sha1 `dea41d9e`)로: 컴파일 Error 0(`Tundra build success`), EditMode `testcasecount=237 Passed passed=237 failed=0`(05:23:18Z~05:23:21Z), PlayMode 게이트(Device·Sweep 제외) 1회 `testcasecount=112 result=Passed passed=110 failed=0 skipped=2`(05:23:36Z~05:32:49Z, 553.4초), 2회 `112/Passed/110/0/2`(05:33:10Z~05:42:23Z, 553.3초). skipped 2 는 `[Explicit]` 네트워크 레이스.
  - 플레이어 재빌드(서킷 씬만, `Succeeded, 0 errors`, Sentis 셰이더 경고 485건) 후 네트워크 레이스 `testcasecount=2 Passed passed=2 failed=0`(05:43:17Z~05:44:10Z, 53.8초): 고정 지연 보정 0·상대 위치 953개 비트 일치·에디터 클라 DONE `jolt=0.0000`, 지터는 에디터 클라 보정 172회·`jolt=0.0111`, 서버 레이서 1 이 657 에서 낙하 → 플레이어 클라 `ended=Failed@657`. 두 클라 모두 `RACE-CLIENT-FAULT` 없음. 빌드가 자동으로 바꾼 5파일(`ProjectSettings.asset`·`UnityConnectSettings.asset`·`Assets/Settings/` 3개)은 파일을 지정해 `git restore`, 그 뒤 `git diff -- Assets` sha1 이 그대로 `dea41d9e`.
  - 입력 장치 경로(`KeyboardMouseInputSource`, `ChainRushGame.Update`)와 코스 코드를 바꾸지 않아 Device·Sweep 은 해당 없음.
- **하지 않은 것**: ⑤ S+L 상한 재시험, 순위(C1c-2), 서버 쪽(`RaceServer`·`ServerInputBuffer`·`RaceWire`), `ChainRushGame`·시뮬 코드, 씬·프리팹·`ProjectSettings`. `RaceClient.OnState`·`OnEnd` 에서 예측기에 넘기기 전 디코딩(`TickOf`·`DecodeEnd`)이 던지는 손상 바이트는 여전히 고장을 세우지 않는다(Netcode 가 예외를 로그로 남긴다).

## 2026-10-11 · P3 C1c-1b: 클라만 먼저 끝나면 서버 상태로 되돌아가 다시 달린다

- **결정 (사용자 승인, 2026-10-11 C1c-1b 요청서에 "A로 ㄱㄱ", 테스트 ③ 은 "③ 빼고 진행")**: 클라 예측 런이 틱 E 에 끝났는데(`HasFailed || HasFinished`, 목표 틱에서 멈춘 Paused 는 아님) 틱 S ≥ E 의 서버 상태가 오면 **반드시 어긋난 것**으로 본다 — 같은 틱에 서버도 끝났다면 상태가 아니라 `race.end` 가 온다(C1c-1). 처리: 서버 상태 S 로 되돌리기 → 기록에 남은 입력으로 `lastInputTick` 까지 재실행 → 끝날 때 기억한 앞섬 `L = lastInputTick − ackedTick` 만큼 live 입력으로 S+L 까지 따라잡기(입력은 평소처럼 기록·전송, 상한은 생성자 `catchUpLimit` = `RaceClient` 의 목표 틱).
  - 전에는 상태 E 에서 끝난 런을 스냅샷하려다(`ChainRushGame.cs:221`), 그 뒤 상태는 "미래 상태"로 던져 클라가 타임아웃으로 끝났다.
  - `ClientPredictor`: 공개 `ClientEndTick`·`ClientEndLead`·`CatchUps`·`CaughtUpTicks`, 재실행 루프를 `ReplayTo` 로 추출(동작 같음). `RaceClient` DONE 줄에 `catchUps=… caughtUp=…`.
- **이유**: 결승선 0.005 m 차이 같은 칼날 사례가 C1c-1 에서 실제로 나왔고, 그 거울(클라만 넘음)도 가능하다. 되돌리기만 하면 클라가 서버 뒤에 서서 이후 입력이 모두 늦게 도착하므로 따라잡기를 넣었다(요청서 A안; B 되돌리기만·C 고장으로 멈춤은 버림).
- **실제로 확인한 것과 한계**
  - 테스트 3개: ① 클라만 결승(클라 0.0073 m 넘음, 서버 반복 입력은 0.0047 m 모자람) → 서버 상태 7923 에서 되돌아가 1틱 따라잡고 7924 에 서버와 같은 자리(틱·이유·위치·진척)에서 끝남, 지연 5·15·40틱 ② 보정 재실행 도중 끝난 클라(서버가 7915·7923 을 놓침) → 남은 기록 입력으로 재실행, 같은 틱을 두 번 보내지 않음 ④ 따라잡기가 목표 틱 상한에서 멈춤.
  - **따라잡기는 지금 서킷에서 최대 1틱이다.** 상태가 순서대로 오면 이 경로는 상태 E 에서만 열리고 E-1 까지 같으므로 차이는 틱 E 입력 하나다. 결승은 다음 틱에 서버도 넘고(틱당 약 0.2 m), 낙하는 한 틱 입력으로 피하지 못했다(C1c-1 의 봇 낙하 3건 × 후보 96개). 서킷 씬의 `ChainRushGame.targets` 는 비어 있어(`targets: []`) 표적 피해 실패도 없다. 그래서 **"S+L 까지 따라잡은 뒤 이어지는 상태가 보정 없이 같다"(③)는 실제 장면으로 만들 수 없어 뺐고, S+L 상한 쪽 경로는 어느 테스트에서도 끝까지 돌지 않는다.** 표적이 있는 모드·충돌(C2) 같은 다른 종료 조건이 생기면 이 안전장치를 다시 시험한다.
  - 리뷰가 확인한 범위 밖 문제(고치지 않음): `LargestJolt` 가 보정 전 런이 Running 이 아니면 원점 기준으로 잰다(`ClientPredictor.cs:212`, 기존 코드 — DONE 줄 `jolt=` 를 근거로 쓰지 않는다). `OnServerState` 에서 나간 예외는 `Faulted` 를 세우지 않는다. L 은 평소 앞섬 +1 로 잡힌다(요청서 정의대로). 끝난 클라가 S < E 상태로 보정돼 되살아나면 따라잡기가 없다.
- **검증 (`feature/p3-c1c-client-end`, 에디터 6000.3.25f1, 시각은 XML 의 UTC)** — 실행한 것 전부
  - 컴파일 Error 0(`Tundra build success`). 새 테스트 3개 단독 `3/3 Passed`(16:55:18Z~16:55:24Z).
  - 리뷰 뒤 코드 변경 없이(작업 트리 diff sha1 `62ff793b`): EditMode `testcasecount=237 Passed passed=237 failed=0`(17:03:39Z~17:03:41Z), PlayMode 게이트(Device·Sweep 제외) 1회 `testcasecount=108 result=Passed passed=106 failed=0 skipped=2`(17:03:58Z~17:13:06Z, 548.3초), 2회 `108/Passed/106/0/2`(17:13:21Z~17:22:29Z, 548.3초). skipped 2 는 `[Explicit]` 네트워크 레이스.
  - 플레이어 재빌드(`Succeeded, 0 errors`) 후 네트워크 레이스 `testcasecount=2 Passed passed=2 failed=0`(17:23:22Z~17:24:21Z, 59.1초): 고정 지연 보정 0·상대 위치 953개 비트 일치, 지터는 서버 레이서 1 이 657 에서 낙하 → 플레이어 클라 `ended=Failed@657`, 두 클라 모두 `catchUps=0`(이 실행에선 클라만 끝나는 일이 없었다). 빌드가 자동으로 바꾼 5파일은 파일 지정 `git restore`.
  - 입력·코스 경로를 안 바꿔 Device·Sweep 은 해당 없음.
- **하지 않은 것**: R·Enter 재시작이 고장을 지우는 문제, 순위(C1c-2), 시뮬 코드·서버 쪽(`RaceServer`·`ServerInputBuffer`·`RaceWire`)·`NetworkRaceTests`, 씬·프리팹·`ProjectSettings`.

## 2026-10-11 · P3 C1c-1: 서버가 런 종료(낙하·결승)를 주인 클라에 알린다

- **결정 (사용자 승인, 2026-10-10 HANDOFF 의 C1c-1 요청서에 "ㄱㄱ", 1차 리뷰 처리안에 "추천대로", 2026-10-11 재리뷰 처리안에 "둘만 고침 (추천)")**: 런이 끝나면 서버는 마지막 상태 대신 **종료 틱 T + 이유(Failed/Finished) + 서버가 T 에 실제로 쓴 입력**을 보낸다(메시지 `race.end`, 10바이트: tick·이유·조향·버튼). 끝난 런은 `CaptureSnapshot` 이 던지므로(`ChainRushGame.cs:221`) 상태로 보내지 않는다. 클라는 결정성(P2)으로 같은 결말을 재현한다.
  - 서버 `ServerInputBuffer.RunEnded`: 런을 끝낸 틱에 한 번. 그 틱 입력을 놓쳤으면 반복한 직전 입력을 싣는다. `RaceServer` 는 상태와 같은 지연 큐(`ToOwner`)로 보내 T-1 상태 뒤에 도착한다(`DelayedSender` 는 순서를 지키고, NGO 사용자 메시지 기본 전달이 `ReliableSequenced`: `CustomMessageManager.cs:300`). 큐에 메시지 이름을 짝지어 넣고 개수 불변식을 검사한다.
  - 클라 `ClientPredictor.OnServerEnd`: T-1 이 비교돼 있어야 한다. 같은 이유로 T 에 끝나 있으면 그대로, 아니면 T-1 로 되감고 서버 입력으로 틱 T 를 다시 돈다(보정 1회). 그래도 결말이 다르면, 또는 `OnServerEnd` 에서 어떤 예외가 나가든 **고장 상태(`Faulted`, 원인·틱)** 가 되고 이후 입력·상태·종료 요청은 모두 던진다. `RaceClient` 는 고장이면 게임을 멈추고 `RACE-CLIENT-FAULT` 를 한 번 남긴다. `Finished` 에 서버 종료를 넣고 DONE 줄에 `ended=<이유>@<틱>`(없으면 `ended=none`)을 붙였다.
- **이유**: C1b 에서 클라가 끝난 런의 상태를 영원히 기다렸다(2026-10-10 "P3 C1b" 발견). 상태 대신 입력을 보내면 시뮬 코드를 고치지 않고 결말을 맞출 수 있다.
- **테스트로 확인한 것**
  - 와이어 왕복과 손상 바이트 예외. 서버가 앞선 입력을 놓쳐 낙하한 런: 보정은 상태 비교에서 1회 일어나고 클라도 같은 틱에 스스로 낙하한다(종료 메시지는 확인만). 같은 3랩 결승: 보정 0. T-1 비교 전 종료: 예외 + 고장.
  - **되감기·재실행 성공 경로**: 서버가 결승 틱의 입력을 놓쳐 직전 입력으로 결승선을 0.00473 m 넘고, 클라는 다른 입력으로 0.00753 m 모자라 아직 달리는 경우를 결정적으로 찾는다(변형 161개 안에서, 찾은 것은 `150+34`, 틱 7923). 한 틱 입력이 진행량을 바꾸는 폭이 약 0.012 m(드리프트 감속)라 낙하로는 못 만든다(봇의 낙하 3건 모두 후보 96개가 다 떨어짐). 지연 5·15·40틱에서 클라가 같은 틱에 결승으로 끝난다.
  - 지터 네트워크 레이스: 클라↔레이서를 id 로 짝지어 각자 자기 레이서의 종료를 받았는지 본다. 이번 실행에서 서버 레이서 1이 657틱에 낙하했고 플레이어 클라가 `ended=Failed@657` 로 타임아웃 없이 끝났다.
- **검증 (`feature/p3-c1c-run-end`, 에디터 6000.3.25f1, 시각은 XML 의 UTC)** — 실행한 것 전부
  - 1차 구현 뒤 `Net` 카테고리만(14:53:47Z~14:55:37Z): `testcasecount=28 Failed(Child) passed=26 failed=2`. 실패 ① 새 재실행 테스트 — 낙하 탐색이 경우를 못 찾음(위 이유) → 결승 탐색으로 바꿈 ② 지터 레이스 — 옛 플레이어 빌드(`ended=` 없음, 서버 레이서 1 ran 0) → 재빌드 대상, 판정에 쓰지 않음.
  - 결승 탐색으로 바꾼 뒤 그 테스트 단독(15:02:01Z~15:02:06Z): `1/1 Passed`. PlayMode 게이트(15:02:32Z~15:11:36Z): `105 Passed passed=103 skipped=2` — 이 코드는 재리뷰로 다시 바뀌어 병합 근거로 쓰지 않는다.
  - 재리뷰 반영(탐색 상한을 시간 → 변형 개수, 종료 처리 예외 → 고장) 뒤, 같은 코드(작업 트리 diff sha1 `a6229af5`)로: 컴파일 Error 0(`Tundra build success`, 그 전 회차에 테스트의 지역 변수 이름 충돌 `CS0136` 1건을 이름만 바꿔 고침), EditMode `testcasecount=237 Passed passed=237 failed=0`(15:44:20Z~15:44:25Z), PlayMode 게이트(Device·Sweep 제외) 1회 `testcasecount=105 result=Passed passed=103 failed=0 skipped=2`(15:44:45Z~15:53:54Z, 549.4초), 2회 `105/Passed/103/0/2`(15:54:15Z~16:03:20Z, 544.4초). skipped 2 는 `[Explicit]` 네트워크 레이스.
  - 플레이어 재빌드(서킷 씬만, `Succeeded, 0 errors`, Sentis 셰이더 경고 485건) 후 네트워크 레이스 `testcasecount=2 Passed passed=2 failed=0`(16:04:22Z~16:05:16Z, 53.9초): 고정 지연 보정 0·상대 위치 951개 비트 일치, 지터 위 내용. 빌드가 자동으로 바꾼 5파일(`ProjectSettings.asset`·`UnityConnectSettings.asset`·`Assets/Settings/` 3개)은 파일을 지정해 `git restore`.
  - 입력·코스 경로를 안 바꿔 Device·Sweep 은 해당 없음.
- **한계·남긴 것 (확인 못 함 / 하지 않음)**
  - **클라만 먼저 끝나는 경우**(서버가 그 틱 입력을 놓쳐 계속 달림): 서버 상태 T 가 오면 `ClientPredictor.cs:158` 이 끝난 런을 스냅샷하려다 던지고 클라는 타임아웃으로 끝난다(시끄럽지만 회복 못 함). 위 결승 사례(0.005 m 차이)의 거울이라 일어날 수 있다. 다음 작업 C1c-1b.
  - R·Enter(`ChainRushGame.cs:160`)로 `StartRun` 하면 `Clear()` 가 고장을 지운다. 네트워크 레이스의 재시작 자체가 정의돼 있지 않다.
  - 종료 보정은 `LargestJolt` 에 넣지 않는다. 큰 메시지가 다른 전송 파이프라인으로 가서 순서가 바뀔 가능성은 `UnityTransport` 매핑까지 확인하지 않았다.
  - 순위·결승 순서(C1c-2), 상대(고스트)에게 종료 알림은 하지 않았다. 시뮬 코드·씬·프리팹·`ProjectSettings` 는 바꾸지 않았다.
- **진행 방식**: 서브에이전트 직렬(구현 → 리뷰 → 반영 → 컴파일 확인 → 재리뷰 → 반영), 테스트 게이트는 메인. 미추적이던 `Assets/DefaultNetworkPrefabs.asset`(+`.meta`, NGO 자동 생성)을 내용 수정 없이 이 브랜치에서 커밋한다(2026-10-10 사용자 "a").

## 2026-10-10 · 서브에이전트 모델·노력 고정: 5종 모두 `claude-opus-5-5` + `effort: high`

- **결정 (사용자 지시, 2026-10-10 "opus 5.5, high로 고정하자" → 요청서에 "그렇게해라 그러면")**: `.claude/agents/*.md` 5개의 frontmatter 를 `model: claude-opus-5-5`, `effort: high` 로 바꾼다. 전에는 explorer·verifier 가 `sonnet`, 나머지가 별칭 `opus` 였고 `effort` 는 없었다. `docs/RULES/SUBAGENTS.md` §7-1 표에 노력 열을 더하고, `tools/sync-agents.ps1` 로 `.codex` 를 재생성했다.
- **이유**: 파견할 때마다 모델·노력을 지정하지 않아도 같은 설정으로 돈다. 별칭 대신 전체 ID 를 쓰면 별칭이 다른 모델을 가리키게 돼도 조용히 바뀌지 않는다. 두 필드가 받는 값(`model` 은 별칭·전체 ID·`inherit`, `effort` 는 `low`~`max`)은 Claude Code 문서 `code.claude.com/docs/en/sub-agents` 의 frontmatter 표로 확인했다.
- **대가**: explorer·verifier 가 sonnet 에서 opus 로 올라가 파견 1회의 비용·시간이 는다.
- **한계 (확인 못 한 것)**: 정의 파일을 고친 것이 이미 열린 세션의 다음 파견에 바로 반영되는지 확인하지 못했다. `effort` 는 `CLAUDE_CODE_EFFORT_LEVEL` 환경변수가 있으면 그 값에 진다(같은 문서). `sync-agents.ps1` 은 `model` 만 주석으로 옮기고 `effort` 는 옮기지 않는다(`tools/sync-agents.ps1:71`) — Codex 쪽 대응 키는 2026-10-10 "하네스 자동화" 이전부터 미확인이다.
- **버린 대안**: explorer·verifier 는 sonnet 유지하고 노력만 high — 사용자가 5종 모두 opus 를 택했다. 파견 때마다 `model`·`effort` 를 넘기기 — 빠뜨리면 조용히 기본값으로 돈다.

## 2026-10-10 · 하네스 자동화: 금지선 일부를 훅으로 집행하고 `sync-agents -Check` 거짓 양성을 고친다

- **결정 (사용자 승인, 2026-10-10 합의 요청서 "ㄱㄱ")**: 문서로만 있던 규칙 중 기계로 판정되는 것을 `.claude/settings.json` 의 훅 3개로 집행한다. 스크립트는 `tools/hooks/`(Assets 바깥, 2026-08-25 "도구·문서는 Assets 바깥에 둔다"와 같다).
  - `session-start.ps1` (SessionStart): 현재 브랜치가 `main`/`dev` 이면, `index/symbols.tsv` 의 `git-head` 가 `git rev-parse HEAD` 와 다르거나 인덱스가 없으면 사실 한 줄씩 컨텍스트에 넣는다. 알릴 것이 없으면 침묵. 읽기 전용(§9-2, `LOOKUP.md` §6-2).
  - `guard.ps1` (PreToolUse, `Bash` 는 `if: "Bash(git *)"` 일 때만 / `PowerShell|Edit|Write|NotebookEdit` 는 항상): **deny** = `git reset --hard`, `git checkout -- .`, `git restore .`(`-- .`·`:/` 포함, `--staged` 만이면 통과), `git clean`, 강제 푸시(`--force`·`--force-with-lease`·`-f`), `main`/`dev` 위에서 `git commit`(§0 Git). **ask** = `.meta`, `Library/ Temp/ obj/ Build/ UserSettings/`, `ProjectSettings/`, `Packages/manifest.json`, `.unity .prefab .asset .inputactions` 에 Edit/Write/NotebookEdit(§0 파일·설정). 그 밖은 판정하지 않는다(평소 권한 흐름).
  - `after-agents-edit.ps1` (PostToolUse, `Edit|Write`): `.claude/agents/*.md` 를 고친 직후 `sync-agents.ps1 -Check` 를 부르고, 어긋나면 stdout JSON `{"decision":"block","reason":...}` 로 알린다(문서상 PostToolUse 는 이 JSON 과 exit 2 + stderr 둘 다 Claude 에게 전달된다. JSON 을 택했다). 쓰지 않는다.
  - **§0 Git 에 `git restore .` 추가** (사용자 승인, 2026-10-10 "다 진행해보아라"): `checkout -- .` 와 같은 일(작업 트리 변경 버리기)을 하는데 목록에 없어 훅의 미탐이었다. `CLAUDE.md` §0 과 `unity-implementer` 정의를 같이 고치고(§7) `.codex` 를 재생성했다.
  - **Bash 만 `if` 로 거른다**: 문서상 `if` 는 Bash 서브커맨드(`&&`, `$()`)를 각각 검사하고 판단이 안 되면 훅을 그냥 돌린다. git 이 아닌 Bash 호출은 PowerShell 을 띄우지 않는다. `PowerShell` 도구와 Edit/Write 에는 걸지 않았다 — `PowerShell(...)` 규칙의 서브커맨드 처리와 `Edit(...)` 가 Write 에도 맞는지 문서에서 확인하지 못했다.
  - `tools/sync-agents.ps1` 비교 전에 `\r\n` → `\n`. 이 머신은 `core.autocrlf=true` 라 작업 트리의 `.codex/agents/*.toml` 이 CRLF 이고(`git ls-files --eol`: `i/lf w/crlf`) 바이트 비교가 항상 "어긋남(exit 1)"을 냈다. 고친 뒤 exit 0.
- **이유**: 금지선은 읽는 쪽이 지켜야만 작동했다. 이번 세션 시작 때 `dev` 위였고 인덱스는 10-02 것이었다(`git-head 9f5a96f`, 현재 HEAD 와 40여 커밋 차이). 되돌릴 수 없는 git 명령과 보호 브랜치 커밋은 호출 직전에 막는 편이 싸다. 훅 실패는 조용하지 않게 한다: 스크립트 오류는 원문을 stderr 에 남기고 exit 1(비차단 오류 표시), 판정 불가를 "허용"으로 바꾸지 않는다(§5).
- **ask 로 둔 이유**: 이 파일들은 "금지"가 아니라 "승인 후"다. 승인한 뒤에도 통과할 길이 있어야 한다. deny 는 승인 후에도 쓸 일이 없는 git 명령으로 좁혔다.
- **한계 (확인 못 한 것·안 한 것)**
  - 셸 명령으로 보호 파일을 바꾸는 것(`sed -i`, 리다이렉트, `rm`, `mv`)은 판정하지 않는다. 파싱으로는 믿을 수 없다.
  - 실세션 확인: `deny` 는 `cd … && git clean -n` 이 막혔다(`if` 적용 전·후 둘 다). `ask` 는 scratchpad 의 `hook-probe.meta` Write 에 승인 프롬프트가 떠서 사용자가 거부했다(평소 프롬프트 없는 위치). 프롬프트에 사유 문장이 보였는지는 확인하지 못했다. PostToolUse 는 `unity-implementer.md` 편집 직후 "`.codex` 사본이 어긋났다 … 갱신 필요 : unity-implementer.toml" 이 Claude 에게 전달됐다. SessionStart 는 `/clear` 뒤 실세션에서 "[하네스] index/symbols.tsv 는 낡았다 (인덱스 git-head 64a1339, 현재 HEAD 2551a03). B모드 전에 tools/reindex.ps1 로 다시 만든다 (LOOKUP.md §6-2)." 가 컨텍스트로 전달됐다(2026-10-10, 트리거 `clear`). 같은 날 `dev`(`be6adb9`, 인덱스 신선) 위에서 "[하네스] 현재 브랜치: dev (보호 브랜치). 직접 커밋 금지 — 브랜치부터 만든다 (CLAUDE.md §9-2)." 한 줄이 트리거 `startup`(사용자가 새로 시작한 세션의 출력을 전달함)과 `fork`(Claude 가 받은 `SessionStart:fork hook success`)에서 왔다. `resume` 트리거는 확인하지 못했다.
  - 훅 스키마는 공식 문서(`code.claude.com/docs/en/hooks`)로 확인했다: PostToolUse 입력에 `tool_input.file_path`(Windows 는 역슬래시 절대 경로), `matcher` 는 Windows 에서 `Bash|PowerShell` 둘 다, exec form(`powershell.exe` + `args`) 예시. 서브에이전트 안의 도구 호출에도 PreToolUse 가 걸린다: 2026-10-10 `dev` 위에서 `unity-explorer` 가 Bash 로 `git commit --dry-run` 을 부르자 메인 세션 대조군과 같은 deny 문구가 돌아왔다(`guard.ps1:78` 의 사유와 같음). 확인한 것은 Bash deny 경로 하나다. `ask`(Edit/Write)·PowerShell·PostToolUse 의 서브에이전트 적용은 확인하지 못했다.
  - 지연(10회 중앙값, 이 머신): guard 약 290–360 ms(PowerShell 기동 약 160 ms 포함), PostToolUse 약 290 ms(에이전트 정의를 고칠 때만 약 690 ms), SessionStart 약 400 ms. 즉 Edit/Write 1회에 약 0.6 초가 더해진다. git 이 아닌 Bash 호출은 `if` 로 0 이 된다(문서상 동작, 시간은 재지 않았다).
  - 오탐: heredoc 커밋 메시지에서 줄 첫머리가 `git clean`·`git reset --hard` 인 문장은 deny 된다(메시지를 고쳐 피한다). 따옴표 안·grep 패턴은 걸리지 않는다. 판정하지 않는 것: `git switch --discard-changes`, `git checkout -f`, `git stash drop` 등 §0 목록 밖의 명령.
  - 인덱스 신선도는 §6-2 문자 그대로 HEAD 와 같은지만 본다. 문서만 바꾼 커밋도 "낡음"으로 표시된다.
- **버린 대안**: 인덱스 자동 **갱신** 훅 — 2026-08-25 에 버렸고(Unity 임포트와 경합 가능, 수동 재생성이 명시적) 이번에도 검사만 한다. / 훅 대신 규칙 문장 추가 — 문서 규칙은 이미 있고 어겨진 것은 읽히지 않아서다. / 보호 파일 전부 deny — 승인 후 작업을 막는다.
- **이어서 후보**: 병합 전 문서 검증(§9-2 ① `§` 참조 해석)의 스크립트화. `.cs` 가 95개로 `LOOKUP.md` §6-3 의 C모드 재논의 트리거(60개)를 넘었다 — 재논의는 별도 합의.

---

## 2026-10-10 · P3 C1b-0·C1b-1: NGO 를 `dev` 에 들이고 2인 네트워크 레이스를 `Net` 으로 이식

- **결정 (사용자 승인, 2026-10-10 C1b 합의 요청서 "추천대로": 패키지는 별도 브랜치, `Spike` → `Net` 이름 변경, 입력 로그 파일 포함, 상대는 런타임 캡슐로 보간 표시)**
  - **C1b-0**: `feature/p3-ngo-install`(스파이크 때의 패키지 커밋 `09da115` 를 현재 `dev` 위에 `cherry-pick` → `35b51ee`)을 `dev` 에 `--no-ff` 로 병합했다(`0b82595`). `manifest.json:11` 에 `com.unity.netcode.gameobjects 2.13.3` 한 줄과 lock 21줄(`com.unity.transport 2.7.4` 포함)뿐이다. 병합 보고: 컴파일 Console Error 0, EditMode `237/237`(08:49:47Z~08:49:52Z), PlayMode 게이트 `76/76` 연속 2회(08:50:10Z~08:59:09Z 539.4초, 08:59:32Z~09:08:33Z 540.7초), 자동으로 바뀐 파일 없음.
  - **C1b-1**: 스파이크의 순수 클래스 4개(`SimSnapshotCodec`, `ClientPredictor`, `ServerInputBuffer`, `DelayedSender`)와 NGO 접착을 `Runtime/Net/`(asmdef `ProtoHarness.Net`)로 옮기고, 서버를 **레이서 N명(격리 월드 N개)** 으로 넓혔다. 상대 위치 전달·보간·표시와 입력 로그 파일을 더했다.
- **옮기며 바꾼 것**
  - `ClientPredictor`·`ServerInputBuffer` 가 `ChainRushGame.TickCompleted` 를 **스스로 구독**한다. 스파이크에서는 호출자가 틱마다 `AfterTick()` 을 불러야 했고 실행 순서(`DefaultExecutionOrder`)에 기댔다. 이제 빠뜨릴 수 없다. `Detach()` 로 구독을 푼다. tick 0(`StartRun` 직후) 만 호출자가 `AfterTick()` 을 한 번 부른다.
  - `TickInputCodec`(`Control/`): 다섯 버튼을 한 바이트로. 와이어와 입력 로그 파일이 같은 규칙(알 수 없는 비트는 `InvalidDataException`, 조향 범위 밖은 `ArgumentOutOfRangeException`)을 쓴다.
  - `InputLogFile`(`Control/`): `"PHIL"` + 버전 + 틱 수 + 틱당 5바이트. 엄격한 디코드(머리말·버전·길이·버튼·조향). P2 후속(로그 저장)의 첫 조각이다. 실제 봇 런이 파일을 거쳐도 같은 1700틱을 재생한다.
  - 부트스트랩 `RaceBootstrap`: `FindFirstObjectByType` 대신 `RaceWorld.FromScene` 의 루트 스캔을 쓴다(스파이크 때의 §0 면제가 필요 없다). `-raceServer`/`-raceClient` 로 같은 플레이어가 서버도 클라이언트도 된다.
  - 서버 `RaceServer`: 슬롯(격리 월드, 입력 버퍼, 지연 큐)이 레이서마다 하나. 한 레이서의 상태는 주인에게 통째로, 위치(틱+Vector3 16바이트)는 다른 클라이언트에게 간다. 서버 플레이어가 첫 서킷 씬을 열고 나머지는 `SceneManager.LoadSceneAsync(이름, IsolatedLoad)` 로 복사본을 로드한다. 서버 월드의 `AudioListener`·`Camera` 는 끈다(켜 두면 Unity 가 매 프레임 로그를 찍는다).
  - 상대 표시: `RemoteInterpolator`(표시 시계가 최신 위치보다 `delayTicks` 4 뒤에서 틱 속도로 가고, 최대 `maxLagTicks` 25 이상 뒤처지지 않으며, 두 위치 사이를 선형 보간) + `RemoteGhost`(런타임에 만든 콜라이더 없는 캡슐, 씬 무수정).
- **증명한 것** (같은 머신·루프백·Mono 스탠드얼론 서버 + Mono 스탠드얼론 클라이언트 + 에디터 클라이언트, 봇이 기록한 같은 1000틱, 시각은 XML 의 UTC, 에디터 6000.3.25f1)
  - **고정 지연(RTT 100 ms, 한쪽 50 ms, 지터 없음, 서버 버퍼 3틱)**: 에디터·플레이어 두 클라이언트 모두 서버 상태 1001개를 비교해 **보정 0회**. 서버의 두 격리 월드 모두 `late 0` 이고 중간 놓침 없음(놓침은 클라가 입력을 그만 보낸 뒤 서버가 더 돈 틱뿐: `ran − received`). 에디터가 받은 상대 위치 937개 중 자기 런과 대조한 936개가 **비트 단위로 모두 같다**(같은 입력이라 상대 위치 = 자기 위치). 고스트는 4571프레임에서 한 프레임 최대 이동 0.272 m, 5 m 넘는 점프 0. 새 빌드로 다시 돌려도 같은 결과(첫 빌드: 최대 0.430 m).
  - **지터(기본 30 ms + 0~120 ms, 버퍼 1틱)**: 서버가 입력을 962~1070회 놓침·333~440회 늦음. 에디터 보정 108~134회. 고스트는 4456~4658프레임에서 최대 0.172 m, 점프 0.
- **발견: 서버가 런이 끝났다고 클라이언트에게 알리지 않는다.** 지터 시나리오에서 서버의 한 레이서가 놓친 입력 때문에 틱 657 에서 `failed True`(낙하)로 끝났고(`server-7794.log:30`), 그 뒤 서버는 그 레이서의 상태를 더 보내지 않는다(`ServerInputBuffer.AfterTick` 은 런이 끝나면 아무것도 안 낸다). 그 클라이언트는 마지막 상태를 영원히 기다린다. 플레이어 클라이언트는 `-raceTimeout`(이번에 넣은 인자)으로 스스로 끝낸다. 결승·순위·낙사를 클라에 알리는 메시지는 **C1b-2 또는 그 뒤의 일**이다. 스파이크의 "보정 크기 약 184 m"(놓친 점프의 낙하 추정)도 같은 현상으로 보인다(확인 못 함).
- **알아둘 것**
  - 통합 테스트(`NetworkRaceTests`)는 `[Explicit]` 이다. 플레이어 빌드(`Path.GetTempPath()/ProtoHarnessRaceBuild/RaceBuild.exe`)가 있어야 하고 약 90초 걸려 병합 게이트에 넣지 않았다. 이름으로 골라 돌린다. 게이트에는 빠른 `Net` 카테고리 단위 테스트 21건(보정·코덱·보간기·와이어·입력 로그 파일)이 들어간다. `CLAUDE.md` §9-2 는 바꾸지 않았다.
  - 플레이어 빌드는 이번에도 Unity 가 `ProjectSettings.asset`·`UnityConnectSettings.asset`·`Assets/Settings/` 에셋 3개를 자동으로 바꿨다(`git status` 확인, 스파이크 때와 같은 파일들). 커밋하지 않고 파일을 지정해 `git restore` 로 되돌렸다. 첫 빌드는 Sentis 셰이더 경고 485건을 냈고 도구가 "실패"로 표시했지만 빌드는 `Succeeded, 0 errors` 였다.
  - 두 번째 클라이언트가 플레이어 프로세스인 이유: `NetworkManager` 는 프로세스당 하나라서 에디터 안에 클라이언트 둘을 둘 수 없다.
- **한계 (확인 못 한 것)**: 같은 머신·루프백, 같은 입력 두 명(서로 다른 입력이 아님), 클라이언트 2명뿐이다. 모바일·IL2CPP·다른 기기, 패킷 유실, 3인 이상, 결승·순위, 상대의 충돌은 없다. 지연은 앱 수준이라 전송 계층의 지터를 모사하지 못한다. 보정 횟수는 지터 시나리오에서 실행마다 다르다(실시간).
- **검증 (C1b-1, `feature/p3-c1b-net`, 시각은 XML 의 UTC)**: 컴파일 Console Error 0. `Net` 카테고리 단위 테스트 `testcasecount=21 Passed passed=21 failed=0`. 네트워크 레이스 `testcasecount=2 Passed passed=2 failed=0` (09:21:01Z~09:22:34Z, 93.4초; 이전 실행: 지터 시나리오가 플레이어 클라이언트의 대기 때문에 `passed=1 failed=1` 이었고, 단언을 "끝까지 가거나 서버가 그 런의 종료를 기록했다"로 바꿔 통과시켰다. 판정 기준(보정 횟수·비트 일치)은 바꾸지 않았다). EditMode `testcasecount=237 Passed passed=237 failed=0` (09:22:49Z~09:22:54Z). PlayMode 게이트(Device·Sweep 제외) 연속 2회: 1회 `testcasecount=99 result=Passed passed=97 failed=0 skipped=2` (09:23:14Z~09:32:28Z, 553.8초), 2회 `99/97/0/2` (09:32:50Z~09:42:00Z, 550.6초). skipped 2 는 `[Explicit]` 네트워크 레이스 테스트이고 97 = 이전 76 + `Net` 21. 시뮬 코드를 안 바꿔서 스윕·Device 는 해당 없다. 
- **하지 않은 것**: 시뮬 코드(`ChainRushGame`·`RunnerMotor`·`GrappleController`) 변경, 씬·프리팹 수정, 의도적인 `ProjectSettings` 변경(위의 자동 변경은 되돌렸다), 결승·순위 메시지, 충돌·아이템(C2·C3), 푸시.

---

## 2026-10-10 · P3 C1a: 격리 월드 — 레이서마다 서킷 씬 복사본과 자기 물리 씬, 틱 완료 이벤트

- **결정 (사용자 승인, 2026-10-10 "추천대로진행" 후 계획을 고쳐 다시 올리고 "추천재로")**: C1 은 "2인, 방해 없음"이라 레이서끼리 닿지 않는다. 그래서 레이서 한 명마다 **서킷 씬 복사본을 `LocalPhysicsMode.Physics3D` 로 추가 로드**해 각자 `ChainRushGame`·`RunnerMotor`·`CircuitRace`·물리 씬을 갖는다. 공유 월드는 충돌이 생기는 C2 에서 필요가 확인된 뒤 설계한다.
- **처음 승인받은 계획(레이서마다 `ChainRushGame` 을 두고 한 월드에서 서로의 콜라이더를 코드로 거르기)을 버린 이유**: 구현 전에 코드를 읽고 발견했다. ① `Centerline` 은 가변 상태다(`SetFocus` → `focusS`, `Centerline.cs:47,210`)이고 `ChainRushGame` 은 `circuit.BuildTrack()` 로 그 객체를 그대로 받는다(`ChainRushGame.cs:119`). 서킷을 공유하면 두 게임이 focus 를 서로 덮어쓴다. ② `CircuitRace` 는 레이서 1명 전용이다(`game`·`player` 직렬화 하나씩 `:18-19`, 랩 카운터·마지막 S 하나). 쪼개려면 월드/레이서 분리와 새 씬(에디터 승인)이 필요해 변경이 6곳 이상이다. 구현은 시작하지 않고 §3-3 대로 멈춰서 수정안을 올렸다.
- **바꾼 것**
  - `RunnerMotor.cs:298-299` `SnapToGround` 의 `Physics.SphereCast` → `gameObject.scene.GetPhysicsScene()` 의 `SphereCast`. `GrappleController.cs:96,106` 앵커 시야 검사의 `Physics.Linecast` → 같은 물리 씬의 `Raycast(origin, offset, offset.magnitude, ...)` (`PhysicsScene` 에는 `Linecast` 가 없다). 메인 씬에서는 기본 물리 씬이라 동작이 같다.
  - `ChainRushGame.cs:183,189,191` 공개 이벤트 `TickCompleted(int tick)`. `StepTick` 은 가드(`IsRunning`)를 지난 뒤 본문(`RunTick`, 기존 코드 그대로)을 돌리고 이벤트를 올린다. 틱이 실제로 돈 경우에만 올라가며 그 틱 중 런이 끝났어도 올라간다. 스파이크에서 찾은 "`Consume` 안에서 찍은 스냅샷은 틱 사이 상태가 아니다" 문제의 근본 해결이다(이벤트 안에서 `CaptureSnapshot().Tick` 이 방금 돈 틱이다).
  - 새 `Race/RaceWorld.cs`: 로드한 서킷 씬에서 게임·러너·그래플·서킷을 찾아 묶고(없거나 둘이면 예외), `IsolatedLoad`(= `Additive` + `Physics3D`)와 `PhysicsWorld` 를 준다.
  - 새 테스트 `ChainRushWorldTests` 5건. 씬 파일·프리팹·`ProjectSettings`·`manifest.json`·`CircuitRace`·`Centerline` 은 건드리지 않았다.
- **증명한 것** (같은 머신·같은 에디터, 서킷, 봇 1700틱)
  - 격리 복사본 둘을 번갈아 한 틱씩 돌리면 각자 **혼자 달린 런과 비트 단위로 같다**(해시 `0xF9A914DA3FAA680B` = P2 서킷 해시 그대로. 단일 레이서 동작이 안 바뀐 증거).
  - **상대 월드의 러너가 내 길 위(트랙 150 m)에 서 있어도 내 런은 솔로와 같다**(상대 게임은 스텝하지 않아 제자리).
  - **음성 대조**: 같은 구성을 물리 씬을 나누지 않고 로드하면(공유 물리 월드) 757틱에서 갈라진다(`posX 0x3F89C4A8 vs 0x3F891288`, `posZ`, `S`). 그래서 위 두 결과가 우연이 아니다.
  - 격리 월드에서 지상·공중·그래플 스냅샷 복원 뒤 60틱이 비트 단위로 같다(롤백이 격리 월드에서도 성립). `Physics.SyncTransforms()`(`RunnerMotor.cs:398`)가 로컬 물리 씬에도 충분한지 미확인이었으나 이 결과로 문제 없음을 확인했다.
  - `TickCompleted`: 5틱에 1..5 가 한 번씩, 정지 중 `StepTick` 은 올리지 않고, 재개하면 다시 올린다. 이벤트 안 상태가 호출이 돌아온 뒤 상태와 같다.
- **먼저 틀렸던 두 테스트 설계 (밝힌다)**: (1) 쌍둥이(같은 자리·같은 입력)는 서로 닿지 않아서 공유 월드에서도 갈라지지 않았고, 처음 대조군이 통과해 버렸다. 그래서 "한쪽이 다른 쪽 길 위에 서 있는" 구성으로 바꿨다. (2) 이 게임의 러너는 입력이 없어도 앞으로 달리는 자동 주행이라 "입력 없음 = 서 있음"이 아니다. 서 있게 하려면 그 게임을 스텝하지 않는다. 쌍둥이 테스트(함께 돌아간다)는 그대로 두었다.
- **알아둘 것**
  - 복사본마다 카메라·`AudioListener` 가 하나씩 있어 두 번째를 켜면 Unity 가 "리스너 2개" 로그를 **매 프레임** 찍는다. 테스트는 두 번째 로드 전에 첫 복사본의 리스너를 꺼서 막았다. 서버용 월드는 카메라·오디오가 필요 없으니 C1b 에서 꺼야 한다.
  - 복사본을 로드하는 코드(에디터는 `EditorSceneManager.LoadSceneAsyncInPlayMode`, 플레이어는 `SceneManager.LoadSceneAsync`)는 이번 범위에 넣지 않았다. `RaceWorld` 는 로드된 씬만 받는다. C1b 서버가 정한다.
  - 격리 월드끼리는 서로 영향을 못 준다. 추월·충돌·밀치기는 이 구조로는 못 한다(C2 에서 공유 월드 설계 필요: `CircuitRace` 의 월드/레이서 분리, `Centerline` focus 의 레이서별 분리).
- **한계 (확인 못 한 것)**: 같은 머신·에디터뿐이다. 여러 복사본의 메모리·시간 비용은 재지 않았다(둘일 때 틱 2배 정도는 일상적이나 수치 없음). 2개를 넘는 복사본, 무한 코스(`LocalPhysicsMode` 에서의 원점 이동·스트리밍), 플레이어 빌드에서의 로컬 물리 씬 로드는 확인하지 않았다. 무한 코스의 `Physics.SyncTransforms()` 호출(`ProceduralCourse.cs:180,340`)과 `CircuitRace.cs:193` 은 전역 호출 그대로다.
- **검증** (시각은 XML 의 UTC, 에디터 **6000.3.25f1**): 컴파일 Console Error 0. 새 테스트 단독 `testcasecount=5 Passed passed=5` (이전 시도: 4건 중 3건 실패(AudioListener 로그), 5건 중 4건 실패(대조군·서 있는 러너), 모두 테스트 설계 문제). EditMode `testcasecount=237 result=Passed passed=237 failed=0` (07:40:55Z~07:40:58Z). PlayMode 게이트(Device·Sweep 제외) 연속 2회: 1회 `testcasecount=76 result=Passed passed=76 failed=0` (07:41:13Z~07:49:59Z, 526.2초), 2회 `76/76 Passed failed=0` (07:50:15Z~07:59:01Z, 526.0초). 76 = 이전 71 + 새 5. 20 시드 스윕(모터·그래플 변경이라 돌렸다) `testcasecount=1 Passed passed=1 failed=0` (07:59:15Z~08:14:48Z, 932.7초): 시드마다 `escapes 0`, `health 3`, 1400 m 완주. 입력 장치 경로는 안 바꿔서 Device 는 해당 없다. 화면 변화 없음.
- **하지 않은 것**: 네트워크(C1b), 충돌·아이템(C2·C3), 공유 월드, 씬·프리팹·`ProjectSettings` 수정, `CircuitRace`·`Centerline` 분리, 복사본 로더, 스파이크 코드(`spike/p3-ngo-prediction`)의 병합.

---

## 2026-10-10 · P3 스파이크: NGO 서버 1 + 클라이언트 1 예측·보정 — 같은 머신에서 프로세스 사이도 비트 일치

- **결정 (사용자 승인, 2026-10-10 "추천대로 ㄱㄱ" 후 "Ok": 서버는 별도 프로세스(방식 A), `manifest.json` 에 NGO 2.13.3 추가, 새 asmdef `ProtoHarness.Spike`, 스파이크 부트스트랩의 `FindFirstObjectByType` 1회 면제, 지연은 앱 수준 큐, 서버 빌드는 리포 밖)**: NGO 를 쓰되 예측·보정은 직접 만들었다. 브랜치 `feature/p3-ngo-package`(커밋 `09da115`, manifest+lock 만) → `spike/p3-ngo-prediction`(병합하지 않음). 이 항목은 `docs/p3-spike-report` 로 `dev` 에 옮겼다(스파이크 브랜치는 병합하지 않는다). 스파이크 코드는 `spike/p3-ngo-prediction`(`cd6186b`)에만 있다.
- **만든 것** (`Assets/_Project/Scripts/Runtime/Spike/`, 10파일): 코덱(`SimSnapshotCodec`: 스냅샷의 모든 필드를 비트 그대로 바이트로, 같은 상태 = 같은 바이트)·와이어(`SpikeWire`)·예측기(`ClientPredictor`: `IInputSource`, 서버 상태와 예측을 바이트로 비교하고 어긋나면 `RestoreSnapshot` 후 기억해 둔 입력으로 현재 틱까지 `StepTick` 재실행)·서버 입력 버퍼(`ServerInputBuffer`: 입력이 늦으면 직전 입력을 반복하고 센다)·지연 큐(`DelayedSender`)는 **NGO 와 무관한 순수 클래스**다. NGO 에 닿는 것은 `SpikeNetwork`·`SpikeServer`·`SpikeClient`·`SpikeBootstrap` 4개뿐이고 메시지는 `CustomMessagingManager` 이름 있는 메시지 2종(입력/상태), 프리팹·`NetworkObject`·씬 수정이 없다. 테스트 2파일(`SpikeReconcileTests` 카테고리 `Spike`, `SpikeNetworkTests` 카테고리 `SpikeNet`)과 기존 테스트 asmdef 참조 1줄.
- **측정** (같은 머신, 루프백, **서버 = Windows 스탠드얼론 Mono 빌드, 클라이언트 = 에디터**, 서킷 씬, 봇이 기록한 입력 1000틱):

| 항목 | 값 |
|---|---|
| 프로세스 사이 일치 (RTT 100 ms: 한쪽 50 ms, 지터 없음, 서버 버퍼 3틱) | 상태 1001개 비교, **보정 0회.** 서버 1003틱·입력 1000 수신·늦음 0 |
| 놓친 입력 한 번의 보정 (단일 시뮬 테스트, 서버 상태가 5/15/40틱 늦게 도착) | 보정 정확히 1회, 재실행 틱 수 = 지연 틱 수(5/15/40), 보정 뒤 서버 런을 비트 단위로 따라감. 재실행 비용 2.000 / 0.456 / 1.211 ms (한 번 더 돌린 값 1.853 / 0.473 / 1.257 ms) |
| 지터 0~120 ms + 기본 30 ms, 버퍼 1틱 (실시간 비결정, 같은 코드 3회) | 서버가 입력을 놓침 362~536회·늦게 도착 358~532회. 클라 보정 **114 / 115 / 166회**, 재실행 1216 / 1225 / 1646틱(가장 긴 재실행 13틱), 합계 49.8 / 51.6 / 68.2 ms → **보정 1회 평균 약 0.4 ms, 재실행 한 틱 약 41 µs** |
| 코덱 | 스냅샷 192바이트(틱 400). 필드 32개 전부 바이트를 바꾼다(반사 테스트) |

- **판정 기준 (`DESIGN.md` 7-4-3, 7-5)**: ① *서로 일치하는가* — 예(위 첫 행). ② *되감아 재실행해도 P2 와 같은 결과인가* — 예(복원한 디코드 스냅샷으로 한 틱 가면 원래 틱 401 과 같은 바이트, 놓친 입력 테스트에서 보정 뒤 서버 런과 비트 일치). ③ *지연 100 ms 에서 조작이 끊기지 않는가* — **직접 재지 않았다.** 구조상 클라는 서버 응답을 기다리지 않고 입력을 즉시 시뮬에 넣지만, 프레임 시간·체감은 안 쟀다. ④ *NGO 에서 예측을 직접 만드는 비용* — 위 순수 클래스 5개와 NGO 접착 4개로 끝났고, 비용의 대부분은 NGO 가 아니라 "스냅샷·비교·재실행" 쪽이었다(**판단**: 그 부분은 Fusion 을 골라도 `CharacterController` 우회와 함께 같은 일이 필요하다).
- **발견 (고치기 전에 실패해서 찾은 것)**
  1. **스냅샷을 `Consume` 안에서 찍으면 틱 사이 상태가 아니다.** `StepTick` 은 `tick++` 와 `circuit.PrepareTick()` 을 한 뒤에 입력을 묻는다(`ChainRushGame.cs:178-186`). 처음엔 `Consume` 에서 직전 상태를 찍었더니 "서버와 일치"인데도 보정이 1686회 났다(비교한 상태 전부). `StepTick` 이 끝난 뒤 찍도록(`AfterTick`) 고쳤다. 서버·클라가 매 틱 끝에 `AfterTick` 을 불러야 하고, 서버/클라 컴포넌트는 `[DefaultExecutionOrder(1000)]` 으로 게임의 `FixedUpdate` 뒤에 돈다. 게임 코드에는 틱 완료 이벤트가 없다.
  2. **`UnityTransport` 의 디버그 시뮬레이터는 이 버전에서 동작하지 않는다**(`UnityTransport.cs:348,969` `Obsolete ... has no effect`; 대안은 Multiplayer Tools 패키지). 그래서 지연·지터는 `DelayedSender`(앱 수준, 유실 없음)로 흉내 냈다.
  3. `NetworkManager` 는 다른 오브젝트 아래에 둘 수 없다(`NotifyUserOfNestedNetworkManager`). `OnEnable` 이 `Application.runInBackground = true` 를 켜므로 `RunInBackground = false` 로 막았다.
  4. **놓친 입력의 보정은 크게 보일 수 있다.** 지터 시나리오의 가장 큰 보정은 화면에서 **약 183.8 ~ 183.9 m** 를 움직였다(3회 모두 첫 불일치 틱 604 부근). 원인은 **확인하지 못했다**(추정: 점프/그래플 입력을 서버가 놓쳐 서버 쪽 러너가 갭에서 떨어진 것). 재실행 틱 수와 무관하다. 놓친 입력을 이전 입력으로 때우는 정책은 이 스파이크의 선택일 뿐이고 정책 비교는 하지 않았다.
  5. 서버는 클라가 입력을 그만 보낸 뒤에도 접속이 끊길 때까지 몇 틱 더 돌아 그 틱이 "놓침"으로 세어진다(서버 `ran 1003, received 1000, missed 3`). 서버 쪽 결함이 아니고 테스트가 `missed == ran − received` 로 가린다.
  6. **플레이어 빌드는 Unity 가 `ProjectSettings` 를 바꾸게 한다**(자동, 내가 쓴 것이 아님): `ProjectSettings.asset` 의 `preloadedAssets`(URP 전역 설정 에셋)·`m_BuildTargetBatching`(Standalone), `UnityConnectSettings.asset` 의 `m_Enabled 0 → 1`, `Assets/Settings/` 의 3개 에셋, `ProtoHarness.slnx` 1줄. 커밋하지 않았고, 사용자 승인(2026-10-10 "ㄱㄱ")으로 파일 5개를 지정해 `git restore` 로 되돌렸다(`ProtoHarness.slnx` 는 Unity 가 다시 만드는 파일이라 그대로 뒀다). 빌드 중 임시 `Assets/Resources/` 가 생겼다가 사라졌다. 이 스파이크 때문에 생긴 `Assets/DefaultNetworkPrefabs.asset`(NGO 가 만드는 빈 목록)도 미추적으로 남아 있다. **스파이크 밖에서 NGO 를 쓰게 되면 `ProjectSettings` 변경을 `CLAUDE.md` §9-2 대로 따로 담은 브랜치로 분리해야 한다.**
- **한계 (확인 못 한 것·증명하지 않는 것)**: 같은 머신·루프백·클라이언트 1명·서킷·한 레이서뿐이다. **다른 기기·OS·IL2CPP·모바일에서의 부동소수 일치는 증명하지 않았다**(여기서 일치한 것은 같은 CPU 의 에디터 Mono 와 스탠드얼론 Mono 뿐이다). 패킷 유실·실제 네트워크 지연·8인·충돌·아이템은 없다. 지연은 앱 수준이라 전송 계층 자체의 지터를 모사하지 못한다. 1000틱(20초)이고 지터 시나리오는 실시간이라 같은 코드로도 보정 횟수가 114~166으로 달랐다. Fusion 은 비교하지 않았다. NGO 2.13.3 이 6000.3.25f1 에서 임포트·컴파일·접속까지 되는 것은 확인했다(레지스트리 `unity: 6000.0`, 매뉴얼의 pre-release 표시와의 차이는 확인 못 함).
- **검증** (시각은 XML 의 UTC, 에디터 **6000.3.25f1**): 컴파일 Console Error 0. 서버 빌드 `Succeeded, 0 errors, 0 warnings, 131125727 bytes`. `Spike` 카테고리 `testcasecount=11 result=Passed passed=11 failed=0` (06:42:17Z~06:42:25Z). `SpikeNet` 카테고리 `testcasecount=2 result=Passed passed=2 failed=0` (06:58:48Z~06:59:38Z, 50.0초). **이전 실행의 실패도 적는다**: `Spike` 1차 `passed=7 failed=4`(조향 구간 오선택 3 + 스냅샷 시점 1), 2차 `8/3`(구간 3), 3차 `7/4`(대조군이 512틱 창을 넘겨 의도한 예외 + 그 예외가 뒤 테스트로 번짐), 4차 통과. `SpikeNet` 1차 `0/2`(NetworkManager 중첩), 2차 `1/1`(로그 파일 공유 위반), 3차 `1/1`(서버 놓침 단언), 4차 통과. 모두 테스트·접착 코드를 고쳐서 통과했고 판정 기준(비트 일치, 보정 횟수)은 바꾸지 않았다. **병합 조건(EditMode·PlayMode 게이트 2회)은 돌리지 않았다**: 병합하지 않는 스파이크이고 시뮬 코드는 바꾸지 않았다(`ChainRushGame`·`RunnerMotor` 무수정).
- **하지 않은 것**: 시뮬 코드 수정, 씬·프리팹 수정, Fusion 비교, 모바일/IL2CPP 빌드, 다인(C1), 스파이크 코드의 `dev` 병합, 푸시.

---

## 2026-10-09 · 코스 T3b (다른 클론의 독립 구현 — 채택하지 않음, 설계 결과만 보존)

- **통합 메모 (2026-10-10, `feature/integrate-origin-dev`)**: 아래는 원격 `origin/dev` 에 들어 있던, 같은 요청서(`9a69945`)에서 다른 클론이 따로 구현한 T3b 의 기록이다(코드 `fb4d6d5`). 로컬 T3b(위 2026-10-08 항목)와 코드·`.meta` GUID 가 모두 달라 통합 때 **로컬을 채택**했다. 이유: `CourseTuning_Default.asset` 이 로컬 `CourseTuning` GUID 를 참조하고, T3c·T3d·T4·P2·P3 가 로컬 생성기 위에서 검증됐다. 원격 코드는 git 히스토리(`fb4d6d5`)에만 남는다. 이 항목의 값·파일 이름은 원격 구현의 것이며 현재 코드와 다르다. 얻어 갈 만한 설계 결과: **앞보기 300m**(후보 끝에서 직선 300m 가 최근 모듈과 R7 거리를 지키는지 확인. 10km 생성 막힘이 앞보기 없음 4/200 시드 → 300m 0/5000 시드), **R5 에서 탈출 직선 면제**(실패 원문 아래). 로컬 생성기에 옮길지는 별도 합의 대상이다(`COURSE.md` 6-6 "알려진 한계").
- **결정 (사용자 승인한 계획, 2026-10-09 "모두 ㄱㄱ", 요청서는 `docs/HANDOFF.md` 2절 당시 판, 병합 `d2c0084`)**: 순수 로직 다섯 타입을 `Runtime/ChainRush/Track/` 에 둔다(새 네임스페이스 없음, `Track/` 11개). ① `SeedHash`(static): SplitMix64. `Next(ref state)` 는 참조 구현과 같은 한 걸음, `Hash(seed, index, salt)` = Mix(Mix(Mix(seed) ^ index) ^ salt), `Unit` = 상위 53비트 → [0, 1), `Range` → [min, max]. ② `ModuleKind`(enum, 10종). ③ `CourseModule`(readonly struct): 조각 최대 3개(길이·반지름·회전·끝 경사), 틈(시작·길이), 앵커(위치·높이·좌우), 열린 가장자리, 탈출 여부. 생성기만 만든다(`internal` 빌더). `AppendTo(Centerline)` 는 선 끝 `S`·경사가 맞지 않으면 예외. 원호 길이는 `Centerline.AppendArc` 와 같은 float 식으로 계산해 `S` 가 비트 단위로 같다. ④ `CourseTuning`(SO, 첫 SO 형식 그대로): 가중치·범위는 (0m, 1400m) 쌍을 선형 보간, 규칙 상수, `FindProblem()`(첫 문제 문자열 또는 null, `OnValidate` 가 LogError, 생성기는 `ArgumentException`). 물리 한계 상수(최소 반지름 30, 점프 틈 7.7, 경사 20%, 도움닫기 15, 쉼터 40)를 넘는 값과 쉼터 간격보다 긴 모듈(영원히 못 놓임)은 거부한다. **`.asset` 은 아직 없다**(T3c). ⑤ `CourseGenerator`(sealed class): 자기 좌표(double)로 모듈을 잇고, 모듈 k 의 후보 a(0~7)를 `Hash(seed, k, a)` 스트림으로 뽑아 규칙을 통과하는 첫 후보를 쓴다. 첫 모듈은 스폰 쉼터 60m.
- **사용자 결정 (추천 수락)**: 튜닝은 SO, 벽 없는 구간은 보수적으로 넣는다(600m 이후, 직선·완만한 커브, 한쪽만 — 완만한 커브는 안쪽, 모듈의 10% 이하, 기회 20%), 앵커는 위치만 계산(오브젝트는 T3c), 그래플 틈은 씬 값(16m, +10m)에서 시작, 폴더는 기존 `Track/`, 난이도는 요청서 표 값에서 시작.
- **요청서에서 바꾼 것 (구현 중 발견, 2026-10-09 사용자 승인)**
  - **R7 판정**: 모듈 수평 상자 겹침 → **5m 간격 중심선 샘플끼리 거리 ≥ 2 × `overlapMargin`(20.6m)**. 상자는 원호의 빈 모서리까지 막아서, 머리핀 뒤에는 탈출 직선까지 두 모듈 전 커브의 상자에 걸려 생성기가 멈췄다(아래 실패 기록). 상자는 넓은 범위 걸러내기로만 쓴다.
  - **앞보기 추가(B 백트래킹 대신)**: 후보를 받으려면 그 끝에서 직선 `lookaheadLength`(300m)가 지금까지의 모든 최근 모듈과 R7 거리를 지켜야 한다. 백트래킹은 이미 내보낸 모듈을 고쳐야 해서 T3c 스트리밍(`Next()` 로 하나씩)과 맞지 않는다. 막힘(10km 생성) 실측: 앞보기 없음 4/200 시드, 40m 18/1000, 80m 8/1000, 120m 3/1000, 200m 0/1000 · 1/5000, **300m 0/1000 · 0/5000**. 모듈 종류 분포(쉼터·급커브 수)는 길이에 따라 거의 바뀌지 않았다. 300m 는 COURSE 7-1 의 "앞쪽 약 300m 미리 생성" 과도 맞는다.
  - **탈출 직선은 R5 에서도 면제**(요청서는 R4 만): 탈출 직선은 회전이 0 이지만 600m 창이 밀리며 반대 회전이 빠지면 순 회전이 한도를 넘는다(실패: `seed 10 #140 Rest S 9612.2 L 40.0 turn 0.0 escape R5 … But was: 191.528`). R5 의 목적(자기 겹침 방지)은 R7·앞보기가 직접 지킨다. 탈출 직선 위에서도 R7 이 막히면 여전히 `InvalidOperationException`.
  - **언덕 값**: 조각 3개(0→+g, +g→−g, −g→0)는 정상 높이가 경사 × 전체 길이 / 4 를 넘지 못해, 0m 의 경사(≤ 8%)·쉼터 간격(200m) 안에서 "정상 3~5m" 가 나올 수 없다. 조각 길이 40~53m, 경사 6~8% → 6~10% 로 정했다(정상 1.8~3.2m, 1400m 이후 최대 4m).
  - **`SeedHash.Range` 계약 [min, max]**: `5 + 3 × (1 − 2⁻⁵³)` 이 double 반올림으로 8.0 이 된다(테스트 `But was: 8.0d`). 생성기 범위 검사는 원래 max 포함.
- **알아둘 점**: 해시는 상태가 없지만 규칙이 최근 기록에 기대므로 모듈 k 는 0 부터 다시 만들어야 한다(10km 1.6ms 실측). R3 은 후보 단위로 적용한다 — 후보가 쉼터 간격을 넘기면 그 후보 자리에 쉼터를 낸다. 그래서 길이가 긴 모듈(완만한 커브 최대 188m)은 쉼터 바로 뒤에서만 놓이고, 20 시드 × 10km 에서 모듈 3010개 중 쉼터가 1018개다(약 3개에 1개).
- **[실패 기록]** 첫 EditMode 14:55 KST `testcasecount="152" failed="3"`: 생성기 2건 `Seed 7: module 69 at S 4613.3 has no candidate and the escape rest overlaps an earlier module.`, `Range` 1건. 원인 진단(읽기 전용 `Unity_RunCommand`): 200 시드 × 10km 에서 15개 막힘, seed 7 은 급커브 119° 뒤 모든 후보·탈출 직선이 두 모듈 전 완만한 커브의 상자에 걸렸다. R7 변경 뒤 4/200 — seed 53 은 탈출 직선 다음 칸에서 다시 막혔다(두 칸 깊이, 800m 동안 150m × 330m 안을 맴돎). 앞보기 300m 뒤 15:03 `failed="1"`(위 R5 건), R5 면제 뒤 통과.
- **검증** (`feature/course-t3b-generator`, **6000.3.19f1**, MCP, KST): 컴파일 확인, Console Error 0(경고는 원인 미확인 `Deleting invalid font reference.` 만). SplitMix64 참조값은 rust-random `rand_xoshiro/src/splitmix64.rs` 의 `reference` 테스트 앞 5개, 파이썬 독립 구현으로도 일치. 생성기 테스트의 규칙 검사기는 생성기 코드를 쓰지 않고 원호를 따로 전개한다. EditMode `testcasecount="152" result="Passed" passed="152" failed="0"` (15:04:51~15:04:55, +21). PlayMode(Device 제외) 1회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="236.15"` (15:05:20~15:09:17), 2회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="238.78"` (15:09:43~15:13:42). 코드 커밋(`fb4d6d5`) 뒤 코드 주석 3줄(규칙 원본을 `COURSE.md` 6-6 으로)만 고치고 다시: EditMode `testcasecount="152" result="Passed" passed="152" failed="0"` (15:19:00~15:19:03), PlayMode 1회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="240.47"` (15:19:31~15:23:31), 2회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="244.47"` (15:23:57~15:28:01). 런타임 동작 무변경. 입력 장치 경로 미변경이라 Device 실행은 해당 없다. 테스트 로그: 20 시드 × 10km 모듈 3010 · 쉼터 1018 · 열린 가장자리 92 · 탈출 2, `AppendTo` 10km 위치 오차 0.0037m · 방향 0.00012°.
- **하지 않은 것**: `CourseTuning` `.asset`, 씬·`EndlessCourse`·`RoadPiece` 연결, 앵커 오브젝트, 봇 완주(T3c), 보충 모듈(T5).

## 2026-10-08 · P3 준비: 컨트롤러 정규화 비용 측정 — 틱 예산 대비 무시할 수준

- **결정 (사용자 승인, 2026-10-08 "ㄱㄱ": 합의 요청서 "컨트롤러 정규화 비용 측정")**: 롤백 안전성에서 넣은 매 틱 `NormalizeController`(`CharacterController` 껐다 켜기, `RunnerMotor.cs`)의 비용을 처음으로 쟀다. **정규화는 그대로 두고, 런타임 코드는 바꾸지 않았다.** `CharacterController` 를 자체 충돌로 바꾸는 선택지는 이 숫자로는 필요하지 않다.
- **측정** (새 테스트 `ChainRushControllerCostTests` 2건, 서킷 씬, `Stopwatch`, **에디터 안 값**):

| 항목 | 값 |
|---|---|
| 껐다 켜기 1회 | 2.94 µs (5묶음×400회 중앙값, 최소 2.80 · 최대 3.13). 틱 예산 20 ms 의 0.015% |
| 봇·기록기를 포함한 한 틱 | 28.2 µs (700틱 평균). 예산의 0.14% |
| 되감아 재생한 한 틱 | 22.9 µs (40틱 되감기 20회 = 18.34 ms, 복원 포함) |
| 프레임당 8틱 재시뮬 환산 | 0.183 ms (20 ms 중) |

- **읽는 법**: 껐다 켜기는 정규화가 이미 든 한 틱 비용(28.2 µs)의 약 10%다. 정규화를 뺀 틱은 코드를 바꾸지 않고는 못 재서, 이 비율은 "단독 비용 ÷ 정규화가 든 틱 비용"이다. 틱 예산 대비로는 어느 값도 1% 미만이라 P3 예측(되감아 재시뮬)의 병목 후보가 아니다.
- **한계 (확인 못 한 것)**: 에디터 안 측정이다. 빌드된 플레이어, 다른 머신·기기, 적·여러 플레이어가 있는 장면은 재지 않았다. "껐다 켜기 단독" 값은 `NormalizeController` 가 private 이라 같은 두 동작을 테스트에서 직접 되풀이한 값이다.
- **테스트의 단언**: 호출당 중앙값과 재생 틱 비용이 각각 틱 예산의 10%(2 ms) 미만인지만 본다. 실측이 이보다 수백 배 낮아서 성능 기준이 아니라 이상 징후 감지용이다. 숫자 자체는 `Debug.Log` 와 이 항목에 있다.
- **검증** (시각 UTC): 컴파일 에러 0, 새 테스트 단독 2건 통과(14:25:22Z, 2.3초). PlayMode 게이트(Device·Sweep 제외) 연속 2회: 1회 `testcasecount=71 result=Passed passed=71 failed=0` (14:26:04Z~14:34:46Z, 521.6초), 2회 `71/71 Passed failed=0` (14:35:14Z~14:43:56Z, 521.5초). 71 = 이전 69 + 새 테스트 2. EditMode `237/237 Passed failed=0` (14:44:23Z). Console 에러 0. 스윕은 코스·모터 코드를 안 바꿔서 돌리지 않았다.
- **검토했으나 버린 대안**: 정규화를 켜고 끈 두 틱 비용을 직접 비교하는 것(런타임에 스위치를 넣어야 해서 범위 밖), 플레이어 빌드 측정(별도 빌드·승인 필요).

## 2026-10-08 · P3 준비: 롤백 안전성 — 한 틱 스냅샷·복원과 되감아 재실행 증명

- **결정 (사용자 승인, 2026-10-08 "ㄱㄱ": 요청서 질문 1~7 전부 추천대로. 같은 날 "한국시간 오후 10시까지 네가 생각하는 대로 자동 진행"을 위임받아, 요청서 밖의 판단이 필요했던 한 건(아래 컨트롤러 정규화)은 가장 나은 쪽으로 정하고 이 항목에 밝힌다)**
  - **서킷(고정 월드)만 대상.** `ChainRushGame.CaptureSnapshot()` / `RestoreSnapshot(in SimSnapshot)` 는 무한(절차)·직선 모드에서 `NotSupportedException`, 런 중이 아니면 `InvalidOperationException` 으로 크게 깨진다(§5). 무한 모드의 월드 이동(원점 이동·스트리밍)은 스냅샷 대상이 아니다.
  - **한 틱 진입점 `ChainRushGame.StepTick(IInputSource)` 공개.** `FixedUpdate` 는 `StepTick(inputSource)` 한 줄이다. 테스트는 `Time.timeScale = 0` 으로 `FixedUpdate` 를 멈추고 직접 부른다. 틱 안의 순서는 그대로다.
  - **접지를 `RacerState.Grounded` 로.** `CharacterController.isGrounded` 는 마지막 `Move` 의 결과라 복원할 수 없는, `RacerState` 밖의 유일한 숨은 상태였다. `RunnerMotor` 가 `controller.Move` 직후(주 이동·`SnapToGround`·`ResetAtSpawn`)와 컨트롤러를 다시 켠 직후(`ShiftOrigin`·`SetPosition`)에 복사하고, 시뮬의 모든 읽기(`IsGrounded`, 틱 시작의 접지, 그래플 해제)가 이 복사본을 쓴다. `Reset()` 은 접지를 지우지 않는다(규칙이 아니라 컨트롤러의 답이다).
  - **스냅샷 타입은 쓰는 곳 가까이**: `RacerState.Snapshot`(모든 필드, 접지 포함)·`LapCounter.Snapshot`(진행도·마지막 S·시작 여부·완료 랩·다음 체크포인트·시작 틱·랩/체크포인트 틱 배열 복사)·`CircuitRace.Snapshot`, 이들을 묶는 `SimSnapshot`(틱·레이서·러너 위치·서킷). 반사 가드 테스트(`SnapshotTests`)가 `RacerState`·`LapCounter` 에 필드가 늘고 스냅샷이 안 따라가면 실패한다.
  - **다른 숨은 상태는 없었다**(코드 조사): `RunnerMotor` 의 `drifting`·`slingTarget`·`hitGuard`·`guardNormal` 은 틱 안에서 다시 계산되고 `swingAnchor` 는 쓰기만 하며(시각용), `GrappleController` 상태는 `RacerState`(앵커 인덱스·줄 길이)에 있다. 서킷 씬은 `targets: []`·적 없음·정적 노면이라 `CourseTarget.destroyed` 같은 상태가 해당 없다.
- **증명이 실패해서 찾은 것: 복원한 위치에서 `CharacterController` 가 다시 출발하면 원래 런과 1 ULP 어긋난다.**
  - 처음 구현(정규화 없음)에서 롤백 재실행 테스트 8건 중 6건이 실패했다(19:15:25~19:15:44 KST, `testcasecount="8" passed="2" failed="6"`). 복원 직후 표본(sample 0)은 비트 단위로 같고, **한 틱 뒤 위치가 1 ULP 어긋났다**: 예) 지상 스냅샷(틱 300) 다음 표본 `posZ 0x425D2CC9 vs 0x425D2CCA`, `S 0x404BA59920000000 vs 0x404BA59940000000`. 접지·속도·게이지·랩 필드는 모두 같았다.
  - 원인: **확인한 것**은 컨트롤러를 껐다 켜서 컨트롤러 위치를 float 위치에 맞추면 어긋남이 사라진다는 것까지다(아래 실험). **확인하지 못한 것**은 컨트롤러 내부가 `transform.position`(float) 보다 정밀한 위치를 들고 있는지 여부다. 읽을 API 가 없고, 그렇다고 보는 것은 이 실험 결과와 맞는 가설일 뿐이다. 그래서 **매 틱 시작에 컨트롤러를 껐다 켜서(`RunnerMotor.NormalizeController`, `Step` 의 첫 동작) 컨트롤러 위치를 항상 float 위치와 같게** 했다. 이러면 원래 런도 복원된 런도 같은 float 위치에서 출발한다. 이 한 줄을 넣자 같은 8건 중 7건이 통과했고(19:16:46~19:17:05, 나머지 1건은 시험 봇의 문제였다) 어긋남이 사라졌다.
  - **이것은 요청서 질문 4 가 말한 "엔진 한계" 후보였다.** 요청서의 기본 지침은 "고치지 않고 측정만 보고하고 멈춘다"였지만, 같은 날 사용자가 자동 진행을 위임했고(위) 고치는 방법이 작고(런타임 두 줄) 증거(어긋남 사라짐 + 되돌렸을 때 실패)가 있어서 고치는 쪽으로 판단했다. **되돌리려면** `NormalizeController` 호출을 지우면 되고, 그러면 롤백 테스트가 다시 같은 모습으로 실패한다.
  - **대가**: ① 매 틱 컨트롤러 껐다 켜기 비용(**측정하지 않았다**. 게이트 소요는 약 500 → 약 520초로 늘었지만 새 테스트 8건분과 구분하지 못한다). ② **P2 의 재생 해시가 바뀌었다**: 서킷 `0x267B2F98692829F7` → `0xF9A914DA3FAA680B`, 절차 코스 `0xA79E26B06BCEB385` → `0xE81912109A2EB020`(위치가 매 틱 ≤1 ULP 흔들리므로 당연하다). 같은 입력이면 같은 결과라는 성질(재생 3건)은 새 해시로 그대로 통과했고, 절차 코스의 격파 14회·원점 이동 1회·모듈 7개는 이전과 같다. 요청서의 "해시 불변" 증거는 **이 한 건에 한해 성립하지 않으며**, 대신 20 시드 스윕·게이트 61건 무수정 통과를 동작 보존의 증거로 쓴다. ③ 컨트롤러를 끄는 순간 `isGrounded` 가 사라지지만 시뮬은 복사본(`RacerState.Grounded`)만 읽으므로 영향이 없다.
  - **네트워크에 주는 뜻**: 서버와 클라이언트가 같은 틱에서 같은 float 위치로 출발하는 것이 예측·보정의 전제이고, 정규화 전에는 "위치를 복원해도 다음 틱이 같지 않다"가 실제로 재현됐다. 정규화 후에는 같은 머신 안에서 성립한다. 다른 기기끼리의 부동소수 일치는 여전히 **증명하지 않는다**(아래).
- **증명한 것** (`ChainRushRollbackTests`, 서킷 씬, 같은 머신·같은 에디터): 스냅샷 → N틱 → 복원 → 같은 입력으로 N틱이 **매 표본 비트 단위로 같다.**
  - 지상(틱 300)·공중(틱 603)·그래플에 매달림(틱 1552)에서 각각 60틱, 지상 스냅샷에서 이어 1400틱.
  - 한 프레임(45번) 안에서 스냅샷을 번갈아 20번 복원하고 40틱씩 재실행해도 모두 같다(프레임 번호가 안 바뀜을 단언).
  - 더 뒤의 상태에서 앞의 스냅샷으로 돌아가도 같다.
  - 랩 이음매: 이음매(틱 2647) 80틱 전(틱 2567) 스냅샷에서 180틱, 이음매를 넘고 1랩 시간·체크포인트 틱까지 같다.
  - 슬링(틱 10)·코너 스윙(틱 673)·드리프트(틱 802) 스냅샷에서 각 80틱.
  - 수동 스텝 1700틱이 실시간 `FixedUpdate`(3배속)로 재생한 같은 로그와 같다.
  - **음성 대조**: 복원 뒤 `Grounded` 만 뒤집으면 첫 표본에서 `grounded 0x0 vs 0x1` 로 어긋난다(접지를 상태로 옮긴 이유가 실제 효과임).
  - 서킷 밖에서 `CaptureSnapshot` 은 `NotSupportedException`, 런 전에는 `InvalidOperationException`.
- **고치기 전 실패**: `SnapshotTests` 를 먼저 쓰고 `error CS1061: 'LapCounter' does not contain a definition for 'Capture' / 'Restore'` 를 확인한 뒤 구현했다. 구현 직후 EditMode 237건이 한 번에 통과했다(19:10:07 KST).
- **증명하지 않는 것(범위 밖)**: 무한(절차) 코스·적·스트리밍의 롤백, 네트워크 지연·입력 지연 보정, 스냅샷 링버퍼·직렬화, 여러 레이서, 다른 기기·OS·IL2CPP 의 부동소수 일치, 컨트롤러 정규화의 비용 측정.
- **검증** (`feature/p3-rollback-safety`, **6000.3.19f1**(이 머신 에디터), MCP, 시각은 XML 의 UTC 에 9시간을 더한 KST): 컴파일 Console Error 0. EditMode `testcasecount="237" result="Passed" passed="237" failed="0"` (19:54:24, 최종 코드, 기존 227 + `SnapshotTests` 10). 롤백 테스트 첫 실행(정규화 전) `testcasecount="8" result="Failed(Child)" passed="2" failed="6"`(19:15:25), 정규화 후 `passed="7" failed="1"`(19:16:46, 나머지 1건은 봇이 항상 드리프트하다 틱 723 에서 떨어짐 → 드리프트를 커브에서만 하고 시작 게이지 2칸을 주도록 시험 봇을 고침, 게임 코드 변경 아님). PlayMode(Device·Sweep 제외) 1회차 `testcasecount="69" result="Passed" passed="69" failed="0" duration="519.70"` (19:20:19~19:28:58), 2회차 `testcasecount="69" result="Passed" passed="69" failed="0" duration="519.61"` (19:29:19~19:37:59). 69 = 이전 61 + 롤백 8. 20 시드 스윕 `testcasecount="1" result="Passed" passed="1" failed="0" duration="932.89"` (19:38:16~19:53:49, 모터·`ChainRushGame` 변경이라 돌렸다: 시드마다 `escapes 0`, `health 3`, 1400 m 완주). 입력 장치 경로 미변경이라 Device 는 해당 없다. 화면 변화가 없어 눈으로 보는 확인은 해당 없다.
- **하지 않은 것**: 네트워크 패키지·`manifest.json`, 스냅샷 링버퍼·직렬화, 무한 모드 롤백, 씬·프리팹 수정, 이동·그래플·드리프트 수치 변경, 기존 테스트 수정(`StateTrace` 는 테스트 도구에 오프셋 비교만 더했다).

## 2026-10-08 · P2: 입력 기록·재생과 결정성 증명 (같은 시드 + 같은 입력 = 같은 런)

- **결정 (사용자 승인, 2026-10-08 "ㄱㄱ": 요청서 질문 1~7 전부 추천대로)**
  - **런타임 부품 3개** (`Runtime/ChainRush/Control/`): `InputLog`(한 런의 틱별 `TickInput` 목록, 항목 i = 틱 i + 1 의 입력, `CopyWith` 로 한 칸 바꾼 사본), `InputRecorder`(다른 `IInputSource` 를 감싸 `Consume()` 때 쌓는다), `InputReplay`(로그를 틱마다 하나씩 돌려주고 끝나면 빈 입력 + `Finished`, `Poll` 은 아무것도 읽지 않는다). 게임이 입력원을 `SetInputSource` 와 `StartRun` 에서 `Clear()` 하므로 `Clear()` 를 "새 런" 신호로 썼다: 기록기는 로그를 비우고 재생기는 처음으로 되감는다. 직렬화·파일 저장·고스트 표시는 하지 않았다(질문 1).
  - **상태 트레이스** (`Tests/PlayMode/StateTrace.cs`, 게임 코드 무변경): 입력원 래퍼가 `Consume()` 때(= 직전 틱이 끝난 상태) 틱 번호, 위치·속도, 방향·회전 속도, 게이지, 코요테·줄 길이, 체력·격파·그래플 수, 앵커·슬링·스윙, 접지, 트랙 `S` 와 시나리오별 추가 값(서킷: 랩 진행도·체크포인트·랩 수; 절차 코스: 모듈 수·원점 이동·조각·앵커·조우 상태)을 **값의 비트 그대로** 저장한다. 두 트레이스는 비트 단위로 비교하고(허용 오차 없음, 질문 2), 어긋나면 첫 어긋난 표본·필드·값을 말한다. 틱마다 값을 `SeedHash.SplitMix64` 로 섞은 64비트 해시도 만든다.
  - **스윕 조건**(질문 7): `CLAUDE.md` §9-2 의 스윕 대상 목록에 `Centerline` 을 더했다("코스 생성·스트리밍·투영").
- **결과: 같은 머신·같은 에디터 안에서 비트 단위로 같다.** 서킷 1700틱(언덕·점프 틈·반원·그래플 틈과 앵커 잡기 포함)과 절차 코스 시드 7 의 2500틱(격파 14회·원점 이동 1회·모듈 스트리밍·조각 재활용·앵커 풀 포함)을 봇이 달린 것을 기록하고 다시 재생했다: ① 같은 세션에서 6배속 ② 씬을 다시 불러온 뒤 2배속, 각각 **모든 표본이 일치**했다. 서로 다른 세 번의 테스트 실행(별도 플레이 세션)에서 해시가 같았다: 서킷 `0x267B2F98692829F7`, 절차 코스 `0xA79E26B06BCEB385`. 틱당 프레임 수가 다른(배속이 다른) 런과 같은 값이라 시뮬이 프레임율에 독립임도 확인됐다. 이로써 DECISIONS 2026-10-04 의 "`CharacterController.Move` 의 재현성은 P2 에서 증명한다(미확인)" 는 **같은 머신·같은 에디터** 범위에서 확인됐다.
- **음성 대조**: 같은 기록에서 틱 300 의 조향만 전부 오른쪽으로 바꾼 로그로 재생하면 첫 어긋난 표본이 정확히 301(그 입력의 효과가 나타나는 첫 표본)이고 그 앞은 일치한다. 바꾸지 않은 로그는 다시 일치한다. 비교기가 실제로 어긋남을 잡고 입력이 실제로 시뮬을 움직인다.
- **숨은 상태·리셋 누락은 나오지 않았다.** 요청서가 짚은 위험(런을 다시 시작해도 지워지지 않는 상태, 방금 다시 지은 노면 콜라이더가 첫 틱에 물리에 등록되는 시점, `RunnerMotor` 의 장면 값, 컨트롤러 내부 상태)은 모두 두 번째 런·씬 재로드 뒤에도 영향이 없었다. 그래서 질문 3 의 사전 승인(작은 리셋 누락을 고치는 것)은 쓰지 않았고 시뮬·코스·모터 코드는 바뀌지 않았다.
- **증명하지 않는 것(범위 밖)**: 다른 기기·다른 OS·IL2CPP/모바일에서의 부동소수 일치(COURSE 9절의 "보장하지 않음" 그대로), 여러 레이서, 네트워크 지연·보정, 에디터가 아닌 빌드, 실제 키보드 입력의 경로(`Poll`/`InputLatch`: 기록되는 것은 틱에 소비된 `TickInput` 이다). 서버가 다른 기기에서 돌면 서버·클라이언트 결과가 비트 단위로 같다는 보장은 없으므로 P3 는 클라이언트 보정(서버 권위 + 예측·조정)을 전제로 설계한다.
- **고치기 전 실패**: `InputLogTests` 를 먼저 쓰고 `error CS0246: The type or namespace name 'InputReplay' / 'InputLog' / 'InputRecorder' could not be found` 를 확인한 뒤 구현했다(18:29 KST). 구현 직후 새 EditMode 14건이 한 번에 통과했다. PlayMode 재생 테스트 3건도 첫 실행에 통과했다(어긋남을 못 잡는 비교인지는 위 음성 대조가 보장한다).
- **검증** (`feature/p2-replay-determinism`, **6000.3.19f1**(이 머신 에디터), MCP, KST): 컴파일 확인, Console Error 0. EditMode `testcasecount="227" result="Passed" passed="227" failed="0"` (18:51:07, 기존 213 + `InputLogTests` 14). 재생 테스트 3건 `testcasecount="3" result="Passed" passed="3" failed="0" duration="97.99"` (18:31:52~18:33:30; 서킷 36.0초, 음성 대조 10.2초, 절차 코스 51.7초). PlayMode(Device·Sweep 제외) 1회차 `testcasecount="61" result="Passed" passed="61" failed="0" duration="500.12"` (18:33:58~18:42:18), 2회차 `testcasecount="61" result="Passed" passed="61" failed="0" duration="499.88"` (18:42:35~18:50:55). 61 = 이전 58 + 재생 3. 게이트가 회당 약 403 → 약 500초(요청서 계산 약 490초). 코스·모터 코드를 안 바꿔 스윕은 돌리지 않았다. 입력 장치 경로 미변경이라 Device 실행은 해당 없다. 화면 변화가 없어 눈으로 보는 확인은 해당 없다.
- **하지 않은 것**: 입력 로그의 파일 저장·직렬화 형식, 고스트 표시(서킷 최고 기록과 겨루기), 서버·네트워크 코드와 패키지, 다른 기기 검증, 여러 레이서, 시뮬·코스·모터 코드 변경, 기존 테스트·씬 수정.

## 2026-10-08 · 코스 T3d: 낡은 직선 무한 코스 정리

- **결정 (사용자 승인, 2026-10-08 "ㄱㄱ": 요청서 질문 1~7 전부 추천대로, 파일 삭제는 지우기 직전 목록 확인 뒤 승인)**: T3c 의 `ChainRushProcedural` 이 무한 모드를 대체했으므로 직선 풀 코스와 그 도구를 지운다. 무한 모드 동작은 바뀌지 않는다(`ProceduralCourse`·`CircuitRace`·`CourseGenerator` 는 안 건드렸다).
  - **지운 것 (12개 파일)**: `Scenes/ChainRushEndless.unity`(31만 줄) · `Endless/EndlessCourse.cs` · 씬 제작 도구 3개(`ChainRushEndlessSceneBuilder`, `ChainRushProceduralSceneBuilder`, `ChainRushPresentationBuilder`) · `Tests/PlayMode/ChainRushEndlessTests.cs` (+ 각 `.meta`). 씬 제작 도구는 Prototype → Endless → Procedural 로 이어진 복사 사슬이었고 두 번째 이후는 원본 씬이 없어지므로 실행할 수 없게 돼 같이 지웠다. **이제 씬은 YAML 이 원본이다**: 다시 만들려면 git 이력의 씬·도구를 되살린다(`Circuit` 빌더는 `ChainRushProcedural` 씬이 있는 한 남아 있다).
  - **테스트 이전 (단언 그대로, 위치 설정만 새 코스 기준)**: 옛 `ChainRushEndlessTests` 의 조우·공격·그래플 5건을 새 `ChainRushCombatTests`(`ChainRushProcedural` 씬)로 옮겼다. 옛 테스트는 `MovePlayer(-5/29/28)` 처럼 옛 발판·틈의 z 값에 섰다: 새 테스트는 스폰(첫 모듈 60m 쉼터, `SetSeed(3)`)에서 시작하거나, 시드를 훑어 찾은 틈 4~10m 앞으로 순간이동한다(`ChainRushProceduralTests` 와 같은 도우미 모양). `ChainRushPresentationTests` 는 씬 경로를 바꾸고 `Animation_JumpAndGrapple_…` 의 시작 위치를 "그래플 틈 4m 앞" 으로. `ChainRushSteeringTests` 의 `Guards_BothScenes_LineEveryDeck` 는 `Guards_PrototypeDecksAndProceduralRoad_AreGuarded` 로: 프로토타입 발판 검사는 그대로, 옛 씬의 "Deck" 대신 절차 노면 조각마다 가드 벽·노면 콜라이더 메시가 있는지 본다(처음 300m 는 벽 없는 가장자리가 없다).
  - **지운 테스트 1건**: `Endless_LongRun_RecyclesRebasesAndRestartsWithoutGrowingPool`. 옛 직선 풀 전용(`RecycledCount`, `PoolSize == 8` 청크, 448m 투영 원점 이동)이라 새 코스에 같은 의미가 없다. 같은 일을 `ChainRushProceduralTests` 의 게이트 시드 3개가 이미 단언한다(1400m 완주, 원점 이동 ≥ 1, 조각 24·앵커 10 풀 크기와 씬 오브젝트 수 불변, 재시작 뒤 초기화). 옛 테스트에만 있던 "조우가 실제로 일어난다(격파 > 3)" 는 `RunSeed` 에 단언으로 옮겼다(전에는 로그만 남겼다).
  - **`CourseStream` 유지**: 구현이 `ProceduralCourse` 하나지만 두 필드 타입과 두 씬의 직렬화 참조를 안 건드린다(주석만 고침).
  - **장식은 이번에도 안 함**: 옛 씬의 네온 타워는 옛 씬과 함께 사라졌다(`ChainRushProcedural`·`ChainRushCircuit` 은 T3c 때부터 장식 없음). 곡선·언덕 코스 옆 장식은 별도 설계라 다음 단계 후보(T3e)로 둔다. `Art/Materials` 의 옛 장식용 재질(`M_City` 등)은 그대로 남겼다.
- **검증** (`feature/course-t3d-cleanup`, **6000.3.19f1**(이 머신 에디터), MCP, KST)
  - 옛 씬이 **남은 채로**, 옮긴 테스트 11건(Combat 5, Presentation 3, Steering 3)을 새 씬에서 실행: `testcasecount="11" result="Passed" passed="11" failed="0" duration="31.30"` (17:21:43~17:22:14). 이 실행에서는 새 단언이 처음부터 통과했다(고치기 전 실패 증거는 해당 없음: 동작을 바꾸지 않는 이전).
  - 삭제 뒤: 컴파일 확인, Console Error 0. EditMode `testcasecount="213" result="Passed" passed="213" failed="0"` (17:23:49). PlayMode(Device·Sweep 제외) 1회차 `testcasecount="58" result="Passed" passed="58" failed="0" duration="402.59"` (17:24:06~17:30:48), 2회차 `testcasecount="58" result="Passed" passed="58" failed="0" duration="402.68"` (17:31:06~17:37:49). 58 = 이전 59 − 지운 1건. `RunSeed` 단언을 바꿨으므로 20 시드 스윕 `testcasecount="1" result="Passed" passed="1" failed="0" duration="932.81"` (17:38:08~17:53:41): 모두 완주, 격파 39~46회(새 단언 > 3 통과), 탈출구 0회, 그래플 합계 37회, 가장 느린 `Step` 1.56ms. 입력 장치 경로 미변경이라 Device 실행은 해당 없다.
  - 눈으로 보는 확인은 하지 않았다(코스·씬 코드는 안 바뀌었고 두 씬 파일은 변경 없음): 검증 안 됨.
- **하지 않은 것**: `ChainRushProcedural`·`ChainRushCircuit`·`ChainRushPrototype` 씬 수정, `ProceduralCourse`·`CircuitRace`·`CourseGenerator` 변경, `CourseStream` 제거, 장식, 안 쓰는 재질 정리, 과거 결정(DECISIONS 옛 항목)·`COURSE.md`/`DESIGN.md` 본문의 옛 줄 번호 고치기(당시 분석이라 둠, `COURSE.md` 진행 줄에 표시), push.

## 2026-10-08 · 코스 T4: 수제 서킷 + 랩 (새 씬 `ChainRushCircuit`)

- **결정 (사용자 승인, 2026-10-08 "ㄱㄱ": 요청서 질문 1~9 전부 추천대로)**
  - **범위**: 단일 러너 타임어택(속도전). 아이템·접촉·적·`CourseTarget`·낙사 복귀·기록 저장 없음. 낙사는 지금처럼 런 종료. 새 씬 `ChainRushCircuit`(`ChainRushProcedural` 복사)이고 기존 씬 3개는 변경 없음.
  - **`Centerline` 루프 모드·초점**: 서킷에서는 기존 투영 규칙("앞에서부터 첫 번째로 지나지 않은 조각", `PieceAt`)이 틀린다. 출발 조각의 뒤쪽에 놓인 귀환 직선의 점이 `along ≤ Length` 라 먼저 걸리기 때문이다(`CenterlineLoopTests.ProjectWithoutLoop_PointOnReturnStraight_…` 가 옛 규칙의 오답 S 70 을 고정해 보여 준다: 실제는 S 335.7). `TryClose`/`Close` 가 끝 = 시작(위치 0.5m, 방향 1°, 높이 0.1m, 끝 경사 0)을 검증해 `IsLoop` 를 켜고, 어긋난 값을 메시지로 말한다(`the end is 1.00 m (level), … away from the start`). 루프에서는 `FrameAt`·`Project`·`Frame` 의 `S` 가 한 바퀴로 감기고 **가장 가까운 조각**(점수 = 옆 거리² + 구간 밖으로 벗어난 거리²)을 고른다. `SetFocus(s)` + `FocusWindow`(150m)는 초점 근처 조각만 보게 하고(루프에서는 이음매 양쪽), 창에 조각이 없으면 전체로 되돌아간다. 루프가 닫히면 `Append`·`TrimBefore` 는 예외, `Clear` 가 다시 연다. 호출부 12곳은 안 바꿨다. 초점·루프를 쓰지 않는 직선·무한 코스는 동작이 같다(아래 회귀).
  - **`TrackDefinition`**(SO, `Track/`): `Segment`(직선/원호/틈, 끝 경사)·`Anchor`(S·옆·높이) 중첩 serializable struct, 체크포인트(랩 비율), 출발 칸, 랩 수, 노면 반폭·두께. `TryValidate` 가 `OnValidate` 로그·`BuildCenterline` 예외·`CircuitRace` 의 공통 원천: 반지름 ≥ 30m, 틈은 평지 직선 + 앞뒤 평지 직선 ≥ 15m(R1·R8), 앵커는 틈 위, 랩 ≥ 100m, 닫힘. 기본값은 **스타디움**: 직선 A 140m(언덕 0 → +10% → −10% → 0, 높이 2m, + 점프 틈 6m), R40 우 반원, 직선 B 140m(그래플 틈 16m, 앵커 S 318.66 · 높이 10m), R40 우 반원. 한 바퀴 531.3m(= 2 × 140 + 2π × 40), 반원 둘이 옆으로 ±80m 라 닫힘이 구성상 정확하다.
  - **`LapCounter`**(순수 로직): 틱마다 접힌 `S` 의 변화를 (−L/2, L/2] 로 접어 누적 진행도에 더한다(한 틱 이동 ≤ 0.4m 라 모호하지 않다). 체크포인트·랩은 지나간 것을 **한 번만** 센다: 역주행은 진행도만 줄이고, 이음매에서 앞뒤로 흔들어도, 출발선 뒤로 갔다가 돌아와도 랩이 늘지 않는다. 랩별 틱·체크포인트 틱·합계.
  - **`CircuitRace`**(`Race/`, 새 네임스페이스 `ProtoHarness.ChainRush.Race`): 정의로 중심선을 만들어 `ChainRushGame.Awake` 에 넘기고(`BuildTrack`, 지연 초기화), 노면을 한 번만 깐다(`RoadPiece` ≤ 50m 12개, 틈 건너뜀). 앵커는 씬의 고정 풀(4개)에 정의 위치로 놓고 남는 것은 끈다. 출발선·체크포인트는 비충돌 발광 띠. 매 틱 `PrepareTick`(초점 = 직전 `S`) → 입력 → 이동 → `Step`(투영 + `LapCounter`). 시계는 `StartRun` 틱 0, 러너는 출발선 위 `S 0`, 1랩 = 선에서 선, 3랩 완주 = `CompleteRun`.
  - **`ChainRushGame`·HUD**: 선택 필드 `circuit`(있으면 중심선·완주 판정·`StartRun` 초기화가 서킷 쪽, 없으면 지금과 같다). HUD 는 서킷 분기만 더했다(제목·안내·윗줄 `LAP 2 / 3  01:23.4`·결과의 랩 시간 표). 씬은 메뉴 `Create Circuit Scene`(`ChainRushCircuitSceneBuilder`)이 만들었다: 복사본에서만 `Procedural World`·적 오브젝트·`EnemyDirector` 를 지우고 `Circuit World` 를 넣는다. `Data/Circuit_Stadium.asset`(스타디움 기본값)도 이 메뉴가 만든다.
- **요청서 밖에서 고친 것 (사용자 승인, 2026-10-08)**: `RunnerAnimation.cs:68` 한 줄. 서킷 씬에는 `EnemyDirector` 가 없는데 러너 애니메이션이 `game.Enemies.State` 를 매 프레임 읽어 null 참조가 난다. `game.Enemies != null ? … : Idle` 로 고쳤다(적이 있는 씬은 동작 같음).
- **요청서와 달라진 것**: 봇은 `CourseBot` 를 일반화하지 않고 별도 `CircuitBot`(약 50줄, 같은 점프 거리: 그래플 틈 4.5m, 점프 틈 0.8m)을 뒀다. `CourseBot` 이 `ProceduralCourse` 에 묶여 있어 일반화가 더 많은 기존 테스트 파일을 건드리기 때문. 앵커 풀은 4개(스타디움 그래플 틈 1개 + 여유).
- **고치기 전 실패 기록**: `CenterlineLoopTests` 를 먼저 쓰고 컴파일해 `error CS1061: 'Centerline' does not contain a definition for 'Close' / 'SetFocus' / 'ClearFocus' / 'HasFocus'`(8건, 15:46 KST)를 확인한 뒤 구현했다. 구현 직후 새 EditMode 19건이 한 번에 통과했다.
- **실측** (스타디움, 봇 3배속): 랩 시간 **52.94 / 52.74 / 52.78 초**(한 바퀴 531.3m ÷ 10 m/s = 53.1초 예측과 일치, 합계 158.46초), 그래플 3회(랩마다 1회), 가드 무접촉(중심선에서 최대 0.04m), 가장 느린 `Step` 0.01ms, 실시간 53초. 3랩 모두 이음매(`S` 가 `L` → 0)를 지나며 랩 카운트가 정확하다.
- **검증** (`feature/course-t4-circuit`, **6000.3.19f1**(이 머신 에디터), MCP, KST)
  - `Centerline` 변경만 넣은 회귀 기준선: EditMode `testcasecount="180" result="Passed" passed="180" failed="0"` (15:47:37), 기존 PlayMode(Device·Sweep 제외) `testcasecount="54" result="Passed" passed="54" failed="0" duration="377.32"` (15:47:55~15:54:12).
  - 최종 코드: 컴파일 확인, Console Error 0. EditMode `testcasecount="213" result="Passed" passed="213" failed="0"` (16:08:36, 기존 161 + 루프 19 + 랩 카운터 14 + 정의 19). 서킷 PlayMode 5건 `testcasecount="5" passed="5" duration="70.30"` (16:05:15~16:06:26). PlayMode(Device·Sweep 제외) 1회차 `testcasecount="59" result="Passed" passed="59" failed="0" duration="447.24"` (16:08:52~16:16:19), 2회차 `testcasecount="59" result="Passed" passed="59" failed="0" duration="447.42"` (16:16:36~16:24:04). 59 = 기존 54 + 서킷 5. `Centerline` 을 바꿨으므로 20 시드 스윕 `testcasecount="1" result="Passed" passed="1" failed="0" duration="932.80"` (16:24:21~16:39:53): 모두 완주, 격파 39~46회, 탈출구 0회, 그래플 합계 37회, 가장 느린 `Step` 1.47ms, 원점 이동 2~3회(T3c 와 같은 값).
  - 눈으로 확인(임시 테스트로 봇이 달리는 중 6장을 찍음, 커밋 안 함): 6장 중 3장(출발 6m, 215m 반원, 그래플 틈 앞)을 봤다. 헤더 `CIRCUIT TRIAL`, 윗줄 `LAP 1 / 3  00:21.7`, 출발 직후 언덕, 가드 달린 반원, 틈 앞 앵커 표식(`LINK / 24m`)과 건너편 노면 정상. 나머지 3장은 보지 않았다. 체감 튜닝은 하지 않았다. 입력 장치 경로 미변경이라 Device 실행은 해당 없다.
- **하지 않은 것**: 아이템·접촉·충돌·순위·고스트·다인 출발(P3), 적·`CourseTarget` 판정의 트랙 프레임화(곡선 위 표적을 놓는 첫 작업에서), 낙사 복귀, 기록 저장, 서킷 에디터 도구, 서킷 여러 개, 다리·교차(교차 서킷은 "가장 가까운 조각" 이라 위·아래층을 구분하지 못한다), 장식, `Track/` 폴더 이동, `CLAUDE.md` 변경(스윕 조건의 대상 목록에 `Centerline` 을 더할지는 사용자 결정).

## 2026-10-08 · 코스 T3c: 무한 모드를 생성기 코스로 전환 (새 씬 `ChainRushProcedural`)

- **결정 (사용자 승인, 2026-10-08 "모두 동의한다": 요청서 질문 1~7 전부 추천대로)**
  - **구조**: `CourseStream`(abstract MonoBehaviour: `Distance`, `Step`, `SeedTrack`, `ResetCourse`, `CanStartEncounter`)을 `EndlessCourse`(직선 풀, 로직 불변, `override` 만 붙임)와 새 `ProceduralCourse` 가 상속한다. `ChainRushGame.endlessCourse` 와 `EnemyDirector.course` 의 필드 타입만 `CourseStream` 으로 바꿨다. 기존 `ChainRushEndless` 씬의 직렬화 참조는 그대로 유효하다: 이 변경만 넣고 기존 PlayMode 를 무수정으로 돌려 확인했다(아래 검증).
  - **새 씬** `Scenes/ChainRushProcedural.unity`: `ChainRushEndless` 를 다른 이름으로 저장한 복사본에서 `Endless World` 를 지우고 `Procedural World`(`ProceduralCourse` + 앵커 10개)를 넣었다. 만든 곳은 메뉴 `Create Procedural Scene`(`ChainRushProceduralSceneBuilder`). 원본 씬은 변경 없음. `Data/CourseTuning_Default.asset` 도 이 메뉴가 만든다(기본값 그대로). 장식 타워는 뺐다(질문 3).
  - **`ProceduralCourse`**: 플레이어 `S` 앞 300m 까지 `generator.Next()` 로 받아 `AppendTo` 로 중심선에 붙이고, 모듈마다 노면 조각을 `RoadPiece` 풀(24칸)로 깐다. 틈 모듈은 틈 앞뒤 두 구간(틈에는 노면 없음), 50m 를 넘는 구간은 나눈다. 틱당 `Build` 최대 1회(시작·재시작 때만 앞쪽 전부). 뒤쪽 60m 밖은 풀로 반환. 앵커 풀 10칸(그래플 모듈 최소 44m, 살아 있는 구간 330m → 최대 8개 + 여유 2; `GrappleController.anchors` 와 같은 배열): `FrameAt(AnchorS)` 의 `TransformPoint(AnchorOffset, AnchorHeight, 0)` 에 놓고, 지나가 30m 뒤이면서 지금 붙어 있지 않으면 끈다. 원점 이동은 플레이어 수평 위치가 원점에서 400m 이상일 때 `-(x, 0, z)`(요청서 질문의 "위치 벡터 기준"). `DistanceToEdge` 는 앞의 가장 가까운 틈 시작까지 `S` 거리. 시드: 런마다 새 무작위 시드(질문 2), `SetSeed` 로 지정하고 `Seed` 로 읽는다. 풀이 모자라면 `InvalidOperationException`. 한 틱 `Step` 시간을 `MaxStepMilliseconds` 로 잰다.
  - **다른 기존 파일**: `GrappleController.SelectCandidate` 가 비활성 앵커를 건너뛴다(풀에서 쉬는 앵커가 후보가 되지 않게). `EnemyDirector` 의 적 회전을 `Entering`·`Vulnerable` 에서 트랙 프레임 기준으로(직선 +z 에서는 값이 같다). `CourseTarget` 은 이번에 안 건드렸다(질문 4, T4).
  - **`Sweep` 카테고리** (질문 5): `CLAUDE.md` §9-2 에 한 문장을 더하고, 메뉴 `Run PlayMode Tests` 는 `Device`·`Sweep` 을 함께 제외한다(`categoryNames` 의 `!` 항목은 AND 로 합쳐진다: `RuntimeTestRunnerFilter.cs:76-90` 소스로 확인). 새 메뉴 `Run Course Sweep`(`ChainRushSceneBuilder.cs`).
- **요청서 밖에서 고친 것 (사용자 승인, 2026-10-08)**: `RoadPiece.Build`. 조각을 `Hide()` 한 같은 프레임에 `Build()` 하면 노면·가드 콜라이더 메시가 **null** 이 된다. T3a 에서 본 콜라이더 null 증상의 다른 경로이고, T3a 테스트는 조각을 숨겼다 다시 만든 적이 없어 드러나지 않았다. 풀이 `StartRun`(전부 숨김 → 같은 프레임 재빌드)과 틱 중 재활용(방금 숨긴 조각을 같은 틱에 재사용)에서 이 패턴을 쓴다. 수정: `root.SetActive(true)` 를 메시 할당 **앞**으로 옮기고(`BuildMeshes` 로 분리), 빌드가 도중에 실패하면 다시 끈다(`IsBuilt` false). 왜 한 프레임 뒤에는 되는지는 **확인 못 했다**(엔진 쪽, 소스 미조사).
- **[실패 기록]**
  - 새 테스트 첫 실행 9건 중 6건 실패(14:25:32~14:25:48 KST): 씬 로드 뒤 첫 `StartRun` 직후 러너가 노면 없이 떨어졌다. `Runner never landed on the procedural road. S 7.97, D 0.00, H -12.32, position (0.00, -12.32, 7.97), velocity (0.00, -23.76, 5.40), grounded False, health 3, hits 0, failed True`. 진단(임시 테스트, 커밋 안 함): 시작 직후 활성 `Road collider` 대부분이 `mesh NULL`(경계 크기 0), 이번 `StartRun` 에서 처음 만든 9·10번 조각만 `mesh 320`. `RoadPiece` 만으로 재현: `rebuild after Hide, same frame: road mesh NULL, wall mesh NULL` / `a frame later: road mesh 416, wall mesh 832`. 고치기 전 실패하는 회귀 테스트 `Piece_HiddenAndBuiltAgainInTheSameFrame_KeepsItsColliderMeshes`: 14:29:28~14:30:08 `testcasecount="4" failed="1"` 원문 `Road collider lost its mesh when its piece was hidden and built again in one frame. Expected: not null But was: null`, 수정 뒤 14:30:45~14:31:25 `testcasecount="4" result="Passed" passed="4"`.
  - 같은 실행의 적 방향 테스트: `The enemy must face along the track, not along world +z. Expected: greater than 0.98 But was: 0.83`. 회전을 `Vulnerable` 다음 틱에야 정했기 때문. `Entering` 에서도 트랙 방향으로 돌렸다(입장 때 커브에서 팝이 없어진다).
  - 수정 뒤 게이트 시드 0 은 695m, 시드 1 은 1078m 에서 낙사(`H -12.23`, 14:32:00~14:33:58 KST, 7/9). 생성기로 그 `S` 를 찍어 보니 둘 다 **점프 틈**(6.7m, 7.2m)이다. 봇이 직선 코스의 규칙(틈 앞 4.5m 에서 점프)을 그대로 써서 가장자리 뒤 5.75m 에 떨어졌다(평지 점프 10.25m, COURSE 6-1). 코스가 아니라 봇 결함: 점프 틈은 가장자리 0.8m 앞에서 뛴다(착지 9.45m, 최대 틈 7.7m 에 1.75m 여유; 계산). 그래플 틈은 4.5m 앞 점프 + 공중에서 `TryAttach` 그대로.
  - 빌더 버그: 처음 만든 씬의 `ProceduralCourse.tuning` 이 null 로 저장됐다(`tuning: {fileID: 0}`). `CreateAsset` 으로 만든 SO 를 들고 `OpenScene` 을 부르면 에디터가 쓰이지 않는 에셋을 내리는 것으로 **추정**(재현으로만 확인: 씬을 연 뒤 에셋을 다시 로드하게 고치자 `tuning: {fileID: 11400000, guid: 7affbf1b…}` 로 저장됨). 잘못 만든 씬 파일 2개를 사용자 승인으로 지우고 다시 만들었다.
- **실측**
  - 20 시드 × 1400m 스윕: 모두 완주, 실시간 46.1~46.9초(3배속), 체력 3 유지, 적 격파 39~46회, 탈출구 0회, 그래플 합계 37회(그래플 0회 시드 2개), 가장 느린 `Step` 1.55ms(틱 20ms 예산), 조각 11~15개 사용. **원점 이동은 시드 4개가 2회**(나머지 3회): 요청서의 "≥3회" 는 곡선 코스에서 성립하지 않는다(원점에서 400m 안쪽을 도는 코스가 있음). 테스트는 "≥1회" 로 단언한다.
  - 그래플 틈 14~18m·앵커 좌우 ±2m(COURSE 6-2 시작값)를 봇이 건넜다. 앵커 높이는 씬 값 10m 그대로(COURSE 의 +3~+8m 로 고치지 않음). 튜닝 값(`.asset`)은 기본값 그대로다.
- **검증** (`feature/course-t3c-procedural`, **6000.3.19f1**(이 머신 에디터), MCP, KST)
  - 회귀 기준선(`CourseStream` 변경만 넣은 상태, 기존 테스트 무수정): PlayMode(Device 제외) `testcasecount="44" result="Passed" passed="44" failed="0" duration="225.05"` (14:17:20~14:21:05).
  - 최종 코드: 컴파일 확인, Console Error 0. EditMode `testcasecount="161" result="Passed" passed="161" failed="0"` (14:54:55~14:54:58, 새 EditMode 없음). PlayMode(Device·Sweep 제외) 1회차 `testcasecount="54" result="Passed" passed="54" failed="0" duration="377.25"` (14:55:14~15:01:31), 2회차 `testcasecount="54" result="Passed" passed="54" failed="0" duration="377.28"` (15:01:47~15:08:05). 54 = 기존 44 + `RoadPiece` 회귀 1 + 절차 코스 9. 20 시드 스윕(`Sweep`) `testcasecount="1" result="Passed" passed="1" failed="0" duration="932.78"` (15:08:22~15:23:55). 앞서 단언 보강 전 코드에서도 같은 스윕이 통과했다(14:37:39~14:53:12, `duration="932.79"`). 입력 장치 경로 미변경이라 Device 실행은 해당 없다.
- **눈으로 확인** (임시 테스트로 시드 5 를 봇이 달리는 중 5장을 찍음, 커밋 안 함): 5장 중 3장(25m, 420m, 그래플 틈 앞)을 봤다. 커브·가드 난간·조명 띠·조각 이음매 정상, 그래플 틈 앞에서 앵커 표식(`LINK / 24m`)과 건너편 노면이 보이고, 커브 위 적이 트랙 방향을 향한다. 장식이 없어 배경은 어두운 빈 공간이다(질문 3). 나머지 2장(200m, 650m)은 보지 않았다. 체감 튜닝은 하지 않았다.
- **하지 않은 것**: 기존 `ChainRushEndless` 씬·`EndlessCourse` 로직·그 테스트 수정과 직선 코스 삭제(T3d), 장식 타워, `CourseTarget` 판정의 트랙 프레임화(T4), 유한 모드·`TrackDefinition`(T4), 보충 모듈(T5), 수치 체감 튜닝, push.

## 2026-10-08 · 코스 T3b: 모듈 카탈로그 + 시드 생성기 (순수 로직)

- **결정 (사용자 승인, 2026-10-08 "다음 작업 진행" 뒤 질문에 "전부 추천대로" + "dev에 병합 후 분기")**: 요청서(`docs/HANDOFF.md` 2절, 커밋 `9a69945`)의 질문 6개를 모두 추천대로 확정. `Runtime/ChainRush/Track/` 에 5개를 둔다. ① `SeedHash`(static): SplitMix64(`prng.di.unimi.it/splitmix64.c` 소스로 확인) 한 걸음을 `(시드, 번호, 솔트)` 에 차례로 먹이는 `Hash`, 상위 53비트 `Unit`, `Range`. ② `ModuleKind`(enum 10종, 값이 `CourseTuning` 가중치 배열 인덱스). ③ `CourseModule`(readonly struct): 조각 1~3개(`Piece` 중첩 struct: 직선 또는 원호 + 끝 경사), 틈 구간, 앵커(트랙 좌표 `AnchorS`/`AnchorOffset`/`AnchorHeight`), 가장자리 열림, `AppendTo(Centerline)`. 모든 모듈은 경사 0 으로 시작하고 끝난다. ④ `CourseTuning`(SO): 가중치(시작·완전) · 범위 18종(시작·완전, Vector2 x=min y=max) · 규칙 상수. 거리 0 → `rampDistance` 1400m 에서 선형. `TryValidate` 가 `OnValidate` 로그와 생성기 `ArgumentException` 의 공통 원천. `.asset` 은 T3c. ⑤ `CourseGenerator`(sealed class): 후보 8개를 차례로 뽑아 규칙을 지키는 첫 후보를 쓰고, 모두 막히면 직선 쉼터 40m(탈출구). 자기 좌표를 double 로 따로 들고 `Centerline` 을 건드리지 않는다.
- **요청서와 달라진 것**
  - **R9 추가**: 후보는 끝에서 탈출구 직선이 이어질 수 있어야 한다(`ExitViable`). 처음 구현(R1~R8 만)은 시드 2 의 모듈 121(S 7609.6)에서 후보 8개가 모두 막히고 탈출구도 옛 도로와 겹쳐 `InvalidOperationException` 이 났다(13:21:47~51 KST, 원문 아래). 테스트를 느슨하게 하지 않고 생성기를 고쳤다. 한 걸음 앞만 보므로 탈출구가 이어 나오면 막다른 길이 남을 수 있다.
  - **언덕 길이는 120~160m, 정상 높이 3.0~4.8m**(요청서: 정상 3~8m). 조각 3개로 만든 언덕은 길이 = 4 × 정상 높이 ÷ 경사라, 8m 는 경사 12% 에서도 267m 가 돼 R3(비쉼터 200m 이내)에 걸려 영영 나오지 못한다. 경사 10~12% · 길이 120~160 에서 정점 = 경사 × 길이 ÷ 4. 더 높은 언덕은 오르막 + 내리막 모듈이 이어 붙으며 만든다.
  - **쉼터 판정**: 종류 `Rest` 뿐 아니라 길이 40m 이상 평지 직선이면 `Straight` 도 쉼터로 센다(COURSE 5-1 의 "평평한 직선 ≥ 40m" 정의). R3 는 후보가 200m 를 넘기면 후보를 버리지 않고 `Rest` 로 바꾼다.
  - **벽 없는 가장자리**: 600m 이후, `Straight`·`GentleCurve` 만, 한쪽만, 확률 25%, 지금까지 모듈 수의 10% 이하(접두사마다 보장).
  - 그래플 앵커는 위치(트랙 좌표)만 계산. 오브젝트 풀과 `GrappleController.anchors` 연결은 T3c.
  - "임의 번호를 바로 계산" 은 해시만의 성질이었다. 규칙이 최근 기록에 기대므로 0 부터 순서대로만 만든다(COURSE 7-1 갱신).
- **규칙 검사는 생성기 밖에서 한다** (`CourseGeneratorTests`): 모듈로 `Centerline` 을 다시 만들어 높이(R6)와 비이웃 도로 구간 간 최소 거리(R7: 중심선 12.6m = 2 × (반폭 6 + 가드 0.3), 생성기는 20.6m 상자 기준이라 여유 8m)를 재고, 나머지는 모듈 데이터를 읽는다. R4 는 세 번째가 탈출구면 예외.
- **[실패 기록] 처음 실행 5건 실패**: 13:21:47~13:21:51 KST EditMode `testcasecount="161" result="Failed(Child)" passed="156" failed="5"`. 5건 모두(`AppendTo_TenKilometres_…`, `Modules_TwentySeedsTenKilometres_…`, `Next_TenKilometresPerSeed_…`, `OpenEdges_…`, `Rules_…`) 같은 원문: `System.InvalidOperationException : CourseGenerator: module 121 (seed 2, S 7609.6) has no valid candidate and the escape rest also breaks a rule (height -2.9..-2.9, net turn, or footprint overlap). Loosen CourseTuning or change the seed.` 그전에 컴파일 에러 2건(`SeedHashTests.cs(18,45)`, `(19,45)` `error CS0220: The operation overflows at compile time in checked mode`)은 테스트 상수식을 `unchecked` 로 감싸 고쳤다.
- **실측** (20 시드 × 10km, 기본 튜닝): 모듈 3185개(10km 당 약 159개, 평균 63m), 탈출구 9회(0.28%, 상한 단언 2%), 벽 없는 가장자리 126개(4.0%), 생성 20코스 합계 13ms(코스당 약 0.7ms). 모듈 종류는 시드 20개 × 1400m 합쳐서 10종 모두 나온다(시드마다는 보장하지 않음). `Centerline` 에 10km 를 붙인 끝은 생성기 좌표와 위치 0.5m·높이 0.05m·방향 0.1° 안에서 같고 `S` 는 1e-6 안.
- **검증** (`feature/course-t3b-generator`, **6000.3.19f1**(이 머신 에디터), MCP, KST): 컴파일 확인, Console Error 0. R9 수정 뒤 EditMode `testcasecount="161" result="Passed" passed="161" failed="0"` (13:26:59~13:27:04, 기존 131 + 신규 30: `SeedHashTests` 10, `CourseGeneratorTests` 20). PlayMode(Device 제외) 1회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="225.05"` (13:27:27~13:31:12), 2회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="225.19"` (13:31:28~13:35:13). 런타임 동작은 바뀌지 않았다. 입력 장치 경로 미변경이라 Device 실행은 해당 없다.
- **하지 않은 것**: 씬·프리팹·에셋 생성(`CourseTuning` `.asset` 포함), `EndlessCourse`·`RoadPiece` 연결, 앵커 오브젝트, 봇 완주 테스트(T3c), 그래플 틈 14~18m 의 봇 통과 확인(T3c), COURSE 5-2 보충 모듈(T5), `TrackDefinition`(T4), 기존 코드 수정.

## 2026-10-07 · 코스 T3a: 절차 노면 — 중심선 + 단면으로 노면·가드 메시를 뽑아낸다

- **결정 (사용자 승인한 계획, 2026-10-07 "ㄱㄱ")**: 런타임 코드에서 메시를 만드는 첫 사례. 세 타입을 `Runtime/ChainRush/Track/` 에 둔다. ① `RoadProfile`(readonly struct): 반폭·두께·좌/우 가드 여부. 씬 발판 값 `Guarded` = 반폭 6, 두께 1.2, 양쪽 가드. 잘못된 크기는 예외, `default` 는 `IsValid` false 라 빌더가 거부한다. ② `RoadMeshBuilder`(static): 단면을 닫힌 상자로 중심선을 따라 쓸어 만든다(윗면·양옆·바닥·양 끝면, 면마다 정점을 따로 둬 모서리 법선이 날카롭다). 샘플 간격 `SampleStep` 1m(R30 원호 처짐 4mm, 20% 종단 곡선 1mm 미만, 계산). 노면 윗면은 중심선 높이에서 수평(뱅크 없음). 가드는 노면 바깥 0.3m 두께, 화면용 난간 1.1m · 충돌벽 4m · 위 조명 띠 0.12 × 0.06(`ChainRushSceneBuilder.AddGuards` 치수 그대로). 정점은 호출자가 준 기준점에 대한 상대값이고, 할당 없이 호출자 버퍼에 이어 쓴다. ③ `RoadPiece`(sealed class, MonoBehaviour 아님, `Centerline` 과 같은 순수 C# 형식): 오브젝트·메시를 한 번 만들고 `Build(line, fromS, toS, profile)` 때마다 메시만 다시 채운다(T3c 풀용). 루트는 `FrameAt(fromS)` 위치, `Hide`·`ShiftOrigin`·`Destroy`. 재질은 만들 때 받는다(씬에서는 기존 `M_Deck`·`M_Frame`·`M_Link` 를 넘길 계획, 새 에셋 없음).
- **사용자 결정 (추천 수락)**: 시각 뱅크는 T3a 에서 뺀다 — 화면만 기울이고 충돌면이 평평하면 10° 에서 가장자리 발 높이가 약 1m 어긋난다(계산). T5 경사 커브·T6 물리 뱅크와 함께 본다. "벽 없는 구간"은 단면의 가드 없음으로만 지금 넣고, 생성기가 언제 쓸지는 T3b 에서 정한다. `Pooled Sector` 대체 여부는 T3c 합의 때 묻는다.
- **콜라이더는 MeshFilter 와 다른 오브젝트에 둔다**: 노면 `MeshCollider` 를 MeshFilter 와 같은 오브젝트에 붙였더니 `Build` 가 메시를 할당한 뒤에도 `sharedMesh` 가 **null** 이었다(아래 실패 기록). MeshFilter 없는 자식에 붙인 가드 벽 콜라이더는 정상이었다. 그래서 노면 콜라이더도 별도 자식 `Road collider` 로 옮겼다. 같은 오브젝트에서 null 이 되는 엔진 쪽 이유는 **확인 못 했다**(소스 미조사). 다시 채울 때는 콜라이더에서 메시를 떼고 → 채우고 → 다시 붙인다.
- **[실패 기록] 노면 PlayMode 처음 2회 3/3 실패**: 12:51:26~12:51:32, 12:53:08~12:53:14 KST. 원문(2회차, 상태를 덧붙인 메시지) `Run ended on the procedural road: S 8.63, D 0.00, H -12.35, position (300.00, -12.35, 8.63), velocity (0.00, -24.20, 6.00), grounded False, failed True, finished False.` 낙하 속도 24.2 m/s 는 시작 높이에서 13.5m 자유낙하와 맞다(√(2×22×13.55) = 24.4, 계산). 노면에 한 번도 닿지 않았다. 임시 진단 테스트(커밋 안 함)에서 노면 콜라이더 `mesh=null`, 가드 벽 `mesh=… v832`, 아래로 쏜 레이 미적중. 수정 후 진단에서 `Road collider … v416`, 레이가 (300.00, 0.00, 5.00) 법선 (0, 1, 0) 에 적중, 노면 테스트 3/3 통과(12:57:40~12:58:21).
- **테스트 착지 확인 보강**: 처음 실패는 "착지했다" 확인을 통과한 뒤에 났다. `StartRun` 직후 `IsGrounded` 가 순간이동 전 스폰 발판의 값을 그대로 돌려주기 때문이다. 새 테스트는 순간이동 뒤 고정 틱을 한 번 돈 다음 착지를 확인한다. 기존 테스트(`ChainRushCurveTests` 등)의 `StartOn` 도 같은 형태지만 노면이 실제로 있어서 결과가 맞았다. 기존 테스트 변경이라 손대지 않았다(필요하면 별도 `fix/` 합의).
- **실측**: 274.5m 를 조각 6개(최대 50m)로 만드는 데 8.80 / 13.14 ms(조각당 1.5~2.2 ms, 첫 빌드 포함). 50m 조각 메시: 노면 416 정점, 양쪽 충돌벽 832 정점. 봇이 커브(R30 우 90°, R50 좌 60°)·언덕(±10%)을 조각 이음매를 넘어 달리는 동안 접지 놓친 틱 0, 가드 무접촉. 테스트 하나가 약 27초라 PlayMode 1회가 약 188 → 240초로 늘었다.
- **검증** (`feature/course-t3a-road-mesh`, **6000.3.19f1**(이 머신 에디터, 결정 버전 25f1 아님), MCP, KST): 컴파일 확인, Console Error/Exception 0. EditMode `testcasecount="131" result="Passed" passed="131" failed="0"` (12:59:13~12:59:16, +14). PlayMode(Device 제외) 1회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="240.29"` (12:59:36~13:03:36), 2회차 `testcasecount="44" result="Passed" passed="44" failed="0" duration="236.09"` (13:04:02~13:07:58). 입력 장치 경로 미변경이라 Device 실행은 해당 없다.
- **하지 않은 것**: 씬·프리팹 수정(씬 코스는 아직 `Pooled Sector` 직선), `EndlessCourse` 스트리밍·풀·원점 이동 벡터화(T3c), 모듈 카탈로그·시드 생성기(T3b), 장식 배치, 뱅크, `TestRoad` 를 `RoadPiece` 로 바꾸기.

## 2026-10-05 · 코스 T2d: 코너 앵커 스윙

- **결정 (사용자 승인한 계획)**: 체인 액션 우선순위 두 번째(그래플 강화 다음, 슬링샷 앞)에 코너 스윙. 조건: 드리프트 중(접지 + Shift), 원호 위, 조향이 그 커브 쪽(`Steer × Curvature > 0`), 스윙 중 아님. 게이지 1칸. 앵커는 원호 **중심**(`TrackFrame.CurveCenter` = 위치 + 오른쪽 / 곡률, 직선에서 읽으면 예외). 줄 길이 = 발동 순간 중심까지 수평 거리(`RacerState.SwingRadius`, 부호 = 커브 방향)로 고정해 지금 라인 그대로 원을 돈다. 스윙 중: 헤딩·속도를 원의 접선(트랙 진행 쪽)으로, 매 틱 원 위로 되돌림, 목표 `swingSpeed` 14 를 `swingAcceleration` 20 m/s² 로, 게이지 충전 없음. 끝: Shift 를 떼거나, 그래플이 붙거나, 원호를 벗어나거나(곡률 0 또는 반대), `swingMaxTime` 2초 → 헤딩 방향 최소 `swingExitSpeed` 17, 이어 슬링샷의 유지 단계로 `swingExitCarryTime` 0.6초 동안 16. 체인 시각물은 스윙 중 앵커(중심, 0.5m 위)까지. HUD 상태 "SWING". 공개 API 추가: `IsSwinging`, `SwingAnchor`, `TrackFrame.CurveCenter`, `RacerState.SwingTicks`·`SwingRadius`. 체인 액션 없으면 기존과 같은 값.
- **테스트 헬퍼**: `ChainRushCurveTests` 안의 조향 봇을 공용 `Tests/PlayMode/TrackFollower.cs` 로 옮기고 드리프트·체인 액션 입력을 더했다(커브 테스트 단언 무변경).
- **구현 중 발견한 버그와 고치기 전 실패**: 처음 구현은 스윙 가속을 원운동 단계에서 따로 더했는데, 같은 틱의 일반 주행 계산이 먼저 드리프트 목표(9 m/s)로 0.6/틱 끌어내려 +0.4/틱 가속과 맞서 **약 9.4 m/s 에서 평형**이 됐다. 느슨한 첫 단언("평지보다 빠르다")은 4.32 s < 4.62 s 로 통과해 놓칠 뻔했다. 단언을 의도대로 강화("스윙 최고 속도 > 13", "평지의 85% 미만")하자 고치기 전 `The swing must speed up toward 14 m/s. Expected: greater than 13.0f But was: 9.39996529f` (21:33:21~21:33:43, 계산한 평형값과 일치). 수정: 스윙 중에는 일반 계산의 목표 속도·가속을 스윙 값으로 바꾸고, 원운동 단계는 방향만 접선으로 돌린다. 수정 후 3/3 통과 (21:34:27~21:34:47).
- **실측** (R30 오른쪽 90° 시험 도로, 커브 시작 + 1m 부터 끝까지): 봇 평지 주행 **4.62 s**, 드리프트 + 코너 스윙 **3.46 s**(25% 빠름), 스윙 최고 속도 14.0 m/s, 중심선에서 최대 0.05 m(가드 무접촉). 두 번의 전체 실행에서 같은 값. 직선에서 드리프트 중 체인 액션은 스윙이 아니라 슬링샷. Shift 를 떼면 다음 틱에 끝나고 앞 속도 > 15.5.
- **검증** (`feature/course-t2d-corner-swing`, 6000.3.25f1, MCP, KST): 컴파일 확인, Console Error/Exception 0. EditMode `testcasecount="117" result="Passed" passed="117" failed="0"` (21:35:05, `CurveCenter` +2). PlayMode(Device 제외) 1회차 `testcasecount="41" result="Passed" passed="41" failed="0" duration="188.58"` (21:35:24~21:38:33), 2회차 `testcasecount="41" result="Passed" passed="41" failed="0" duration="187.76"` (21:38:54~21:42:02). 입력 장치 경로 미변경이라 Device 실행은 해당 없다.
- **하지 않은 것**: 줄 감기로 안쪽 라인 타기, 씬 코스 커브(T3), 수치 체감 튜닝, 스윙 중 다른 레이서와의 상호작용(P3).

## 2026-10-05 · 테스트 수정: 몸 기울기 재시작 단언의 프레임 타이밍 의존 제거

- **문제**: `ChainRushInputSourceTests.Tilt_ScriptedSteerLeft_LeansBodyFromMotorState` 의 마지막 단언("재시작 뒤 몸이 똑바르다", 각도 < 0.01°)이 `StartRun()` 다음 프레임에 시뮬 틱이 끼면 실패했다. 중력 한 틱(`velocity.y` −0.44) × 기울기 공식 −0.9 = 0.396°. 아래 "코스 T2c" 의 실패 기록이 그것이다.
- **결정 (사용자 승인: 커밋 먼저, 수정은 별도 `fix/` 브랜치)**: ① 재시작 뒤 `WaitForFixedUpdate` 로 틱을 **반드시 한 번** 돌려 매 실행 같은 상태를 보게 한다. ② 단언을 의도대로 바꾼다: 조향이 0 이고, 몸 회전이 `RunnerTilt.Evaluate(velocity, 0)`(속도에서 오는 앞뒤 기울기만)과 같다 = 조향 기울기(yaw·roll)가 사라졌다. 앞 단계(조향 중 기울기 단언)는 그대로다.
- **고치기 전 실패 (결정적 재현)**: ①만 넣고 기존 단언으로 실행 → `testcasecount="1" result="Failed(Child)" failed="1"`, `Restart must return the body upright. Expected: less than 0.00999999978f But was: 0.395647019f` (21:16:41~21:16:42). 부분 실행에서 본 값과 같다. ② 적용 후 `testcasecount="1" result="Passed"` (21:17:21~21:17:23).
- **검증** (`fix/tilt-test-timing`, 6000.3.25f1, MCP, KST): PlayMode(Device 제외) 1회차 `testcasecount="38" result="Passed" passed="38" failed="0" duration="166.61"` (21:17:38~21:20:25), 2회차 `testcasecount="38" result="Passed" passed="38" failed="0" duration="166.53"` (21:20:40~21:23:27). 런타임 코드 무변경이라 EditMode·Device 는 해당 없음(입력 장치 경로 미변경).

## 2026-10-05 · 코스 T2c: 드리프트, 체인 게이지, 체인 액션(슬링샷·그래플 강화)

- **사용자 결정**: ① 부스트 대신 **체인 그래플로 표현하는 가속**을 쓴다. ② 제안한 세 안(슬링샷 / 코너 앵커 스윙 / 그래플 강화)을 **모두** 쓴다 → 같은 게이지·같은 키(체인 액션)로 상황별로 나가게 하고, 코너 스윙은 가장 복잡해 T2d 로 나눈다(승인). ③ 키는 카트라이더 배치: 왼쪽 Shift 드리프트, 왼쪽 Ctrl 체인 액션.
- **결정 (구현)**: ① `TickInput` 에 `Drift`(레벨)·`ChainActionPressed`(엣지), 생성자 기본값 인자라 기존 호출 무변경. `InputLatch` 는 드리프트를 최신값으로, 체인 액션을 엣지로 보존. `KeyboardMouseInputSource` 에 왼쪽 Shift·Ctrl. ② `RacerState`: `Gauge`(0..`MaxGauge` 2), `AddGauge`(음수·NaN·무한 예외, 상한에서 자름), `TrySpendGaugeSlot`(한 칸 미만이면 아무것도 안 씀), `ref SlingTicks`, `GrappleEmpowered`(+ `EmpowerGrapple()`, 붙어 있지 않으면 예외, `Detach` 가 지움). `Reset` 이 모두 0. ③ `RunnerMotor`: 드리프트 = 입력 + 접지. 그립 `driftGrip` 12(T2b 측정에서 15 부터 미끄러짐), 회전 ×`driftTurnScale` 1.3, 목표 속도 ×`driftSpeedScale` 0.9, 게이지 += |옆 속도| × dt / `slideMetersPerSlot`(6m). 체인 액션 우선순위: 그래플 중 → 강화(이미 강화면 무시), 그 밖 → 슬링샷(진행 중이면 무시). 슬링샷: `slingPullTime` 0.4초 동안 `slingLead` 18m 앞 중심선 지점으로 헤딩을 `slingTurnRate` 90°/s 까지 돌리고 목표 `slingTopSpeed` 20 을 `slingPullAcceleration` 45 m/s² 로, 이어 `slingCarryTime` 0.8초 동안 목표 `slingCarrySpeed` 16. 공개 API 추가: `IsDrifting`, `Gauge`, `IsSlingPulling`, `SlingTarget`. `AddReleaseBoost()` → `AddReleaseBoost(bool empowered)`(강화면 앞 최소 `empoweredReleaseSpeed` 18). ④ `GrappleController`: 강화면 줄 감기 `empoweredRetractSpeed` 12(기본 3), `Release` 는 `Detach` 전에 강화 여부를 읽어 넘긴다. 슬링샷 당기는 동안 체인 시각물(무한: `ChainVisual`, 프로토타입: 줄)을 당기는 지점까지 보인다(실행 중일 때만). ⑤ `ChainRushHud`: 속도 상자 아래 2칸 게이지, 드리프트 중 상태 문구. 드리프트·체인 액션이 없으면 모든 식이 T2b 와 같은 값을 낸다(곱 1, 가속 30/5 그대로).
- **실측** (`ChainRushDriftTests`): 10 m/s 에서 드리프트 + 최대 조향 1초 → 최대 미끄러짐 각 51.4°, 게이지 0.91칸(두 실행 같은 값). 슬링샷 0.5초 안에 15 m/s 초과, 2초 뒤 10 ± 0.5 로 복귀. 그래플 강화 10틱에 줄 1.5m 이상 감김, 해제 앞 속도 ≥ 18.
- **검증** (`feature/course-t2c-drift`, 6000.3.25f1, MCP, KST): 컴파일 확인, Console Error/Exception 0. EditMode `testcasecount="115" result="Passed" passed="115" failed="0"` (21:01:27, +10). PlayMode(Device 제외) 1회차 `testcasecount="38" result="Passed" passed="38" failed="0" duration="166.40"` (21:03:34~21:06:20), 2회차 `testcasecount="38" result="Passed" passed="38" failed="0" duration="166.60"` (21:06:36~21:09:23). 입력 장치 경로를 바꿨으므로 Device: `testcasecount="2" result="Passed" passed="2" failed="0"` (21:09:38~21:09:42, 신규 `Input_ShiftAndCtrl_DriftsAndFiresChainAction` 포함).
- **[실패 기록] 병합 조건 전 부분 실행에서 기존 테스트 1건 실패**: 드리프트·조향·커브·입력 테스트만 모아 돌린 실행(21:01:47~21:02:39)에서 `ChainRushInputSourceTests.Tilt_ScriptedSteerLeft_LeansBodyFromMotorState` 가 `Restart must return the body upright. Expected: less than 0.00999999978f But was: 0.395647019f` (`ChainRushInputSourceTests.cs:173`). **원인**: 테스트는 `StartRun()` 다음 프레임 끝에 몸 기울기 0 을 기대한다. 그 프레임에 시뮬 틱이 한 번 돌면 정지 상태(`velocity.y` 0, 접지 보정은 음수일 때만)에서 중력 한 틱으로 `velocity.y = −22 × 0.02 = −0.44`, 기울기 공식 `velocity.y × −0.9`(`RunnerTilt.cs`) = 0.396° 가 되어 실패값과 일치한다. 틱이 돌지 않는 프레임이면 0 이라 통과한다. 즉 프레임 타이밍에 따라 결과가 갈리는 **기존 테스트의 비결정성**(P1-3b 부터)이고, T2c 는 이 경로(`velocity.y`, `Steer`, `RunnerTilt`)를 바꾸지 않았다. 이후 전체 실행 2회와 Device 실행에서는 통과했다. 다시 돌려 통과한 것으로 실패를 지우지 않고 여기 남긴다. 테스트를 결정적으로 고치는 것(예: 재시작 직후 조향 성분만 단언)은 기존 단언 변경이라 **별도 승인 대기**.
- **하지 않은 것**: 코너 앵커 스윙(T2d), 게이지의 다른 획득 경로(그래플 스윙 등), 아이템전 대인 체인(P3 C3), 수치 체감 튜닝, 위 Tilt 테스트 수정.

## 2026-10-05 · 코스 T2b: 커브(수평 원호)와 그립 한계 — 카트라이더 방향

- **사용자 방향**: "카트라이더 느낌으로 구현하고 싶다, 판단해 봐"(커브 물리 A/B 판단 위임).
- **판단**: 카트라이더의 핵심은 ① 평소 그립이 강해 깔끔히 돈다 ② 급커브는 드리프트로 미끄러지며 돌고 드리프트가 부스트를 채운다 ③ 벽은 손해다. 그래서 커브 물리는 **B. 그립 한계**로 한다. 러너 자신의 회전이 요구하는 옆 가속(속도 × 회전 속도)이 `sideGrip` 을 넘으면 넘친 만큼 미끄러진다. 이것은 T2a 의 옆 속도 감쇠(`sideGrip`)가 이미 하는 일이라 **새 물리 필드는 만들지 않았다.** 기본 60 을 유지(평소 깔끔), 미끄러짐은 다음 단계 드리프트가 그립을 낮춰 만든다. 버린 대안 A(트랙 곡률로 `c × v²/R` 밀기, COURSE 4-3 원안): 커브 위 직진만으로 밀리고 직선 급회전에는 안 밀려 자유 조향·드리프트와 맞지 않는다.
- **결정 (구현)**: ① `Centerline.AppendArc(radius, degrees[, toGrade])` — 양수 오른쪽, 음수 왼쪽, 한 조각 0 초과 180° 이하, 반지름 양수 유한, 아니면 예외. 조각에 부호 있는 곡률·중심·시작 yaw·끝점과 끝 축을 저장한다. 원호 안의 투영은 중심 기준 각도·거리의 닫힌 식(`D` = 부호 × (R − 거리)), 시작 전과 끝 너머는 접선 직선 연장. 경사(종단 곡선)는 원호 길이를 따라 겹친다. 직선 조각은 이전과 같은 식으로 계산한다. ② `TrackFrame.Curvature`(부호 있는 1/R, 직선·연장 구간 0). ③ 모터·카메라·가드 판정·무한 코스는 이미 그 지점 프레임을 읽어 수정이 필요 없었다.
- **테스트**: 공용 `Tests/PlayMode/TestRoad.cs`(중심선을 따르는 **보이는** 도로 메시 + 선택 가드 박스, URP Lit 재질). `ChainRushSlopeTests` 는 자체 도로 생성 대신 이 헬퍼를 쓴다(단언 무변경, 이제 도로가 보인다). 새 `ChainRushCurveTests` 3: 조향 봇(곡률 피드포워드 + 헤딩·옆 거리 보정)이 R30 90° 를 가드에 닿지 않고 통과(최대 |D| < 5.32, 출구 헤딩 90±5°, 최저 속도 > 8), 조향 없으면 바깥(왼쪽) 가드가 커브를 따라 돌려 낙사 없이 통과, 그립 측정. EditMode `CenterlineTests` +17(원호 위치·방향·곡률, 좌우 대칭, 투영 왕복 4, 직선-원호-직선 연속, 끝 너머 연장, 원호 위 경사, 원점 이동, 잘못된 값 6).
- **그립 측정** (10 m/s 에서 최대 조향 0.5초, 넓은 직선 시험 도로, `SerializedObject` 로 플레이 인스턴스만 변경): 그립 60 → 미끄러짐 0.00 m/s · 0.0°, 30 → 0.00 · 0.0°, 15 → **2.51 m/s · 14.1°**. 두 번의 전체 실행에서 같은 값. 계산과 맞다: 한 틱 회전 2.4° 가 만드는 옆 속도 0.42 m/s 를 그립 × 0.02 가 지우는지(30 → 0.6 지움, 15 → 0.3 만 지움).
- **검증** (`feature/course-t2b-curve`, 6000.3.25f1, MCP, KST): 컴파일 확인, Console Error/Exception 0. EditMode `testcasecount="105" result="Passed" passed="105" failed="0"` (20:19:38). 커브·경사 7 `Passed` (20:20:00~20:20:50). PlayMode(Device 제외) 1회차 `testcasecount="34" result="Passed" passed="34" failed="0" duration="156.04"` (20:21:10~20:23:47), 2회차 `testcasecount="34" result="Passed" passed="34" failed="0" duration="155.88"` (20:24:02~20:26:38). 기존 테스트 단언 무변경. 입력 장치 경로 미변경이라 Device 실행은 해당 없다.
- **고치기 전 실패**: 해당 없음. 버그 수정이 아니라 새 기능이고, 원호 API 가 생기기 전에는 새 테스트가 컴파일되지 않는다.
- **하지 않은 것**: 씬 코스 커브·곡선 노면(T3), 무한 모드 커브·원점 이동 벡터화(T3), 시각 뱅크(T3 노면), `CourseTarget` 판정·적 회전(직선 코스라 T3/T4), 드리프트·부스트(T2c, 입력·HUD 가 바뀌어 별도 합의), 피스 선택의 근접 탐색(지금은 "앞에서부터 첫 번째로 지나지 않은 조각". 머리핀처럼 코스가 자기에게 가까이 돌아오면 틀릴 수 있다 → T3 생성기의 자기 교차 방지와 함께 본다).

## 2026-10-05 · 코스 T2a: 자유 조향과 보이는 가드

- **사용자 결정**: ① 커브 조작은 **자유 조향**이다(직접 틀어야 돈다). 그래서 물리 값이 중요하다. ② 옆 낙사는 아직 배제하고 맵 좌우에 **가드**를 둔다. 그래플 틈 낙사는 유지한다. ③ 가드는 **씬에 보이는 난간**으로 만든다. 나중에 벽이 없는 구간에서는 낙사할 수 있게 한다. ④ 옛 조작 사양(A/D = 옆으로 미끄러짐)을 단언하던 테스트 2개를 새 사양으로 바꾸는 것을 승인. ⑤ 이번 두 씬의 저장을 `Unity_RunCommand` 로 하는 것을 승인(§0, 이번 범위만).
- **결정 (구현)**: ① `RacerState` 에 `Heading`(월드 yaw 도, 0 = +z, 양수 = 오른쪽)·`TurnRate`(도/초)를 `ref` 로 추가, `Reset()` 은 0. ② `RunnerMotor`: 회전 속도는 `입력 × maxTurnRate × (공중이면 airTurnScale)` 를 향해 `turnAcceleration` 으로 따라가고, 헤딩은 [-180, 180) 로 감는다. 전진 목표는 헤딩 방향(경사 보정 포함), 헤딩에 수직인 옆 속도는 `sideGrip`/`airSideGrip` 으로 0 을 향한다. 입력을 놓으면 헤딩은 유지된다. `lateralSpeed` 필드는 지웠다. 해제 부스트·그래플 목표 속도도 헤딩 방향이다. 공개 API 추가: `Heading`, `Facing`, `ForwardSpeed`, `SideSpeed`, `FaceTrack()`. ③ 가드 부딪힘: `OnControllerColliderHit` 에서 법선이 수평(|y| ≤ 0.3)이고 트랙 방향과 가로지르는(|법선·트랙앞| ≤ 0.5) 접촉만 가드로 기록(발판 앞면·턱은 제외). 이동 뒤 벽으로 가는 성분을 없애고 `guardSpeedLoss × (벽으로 가는 속도 / 속도)` 만큼 감속, 헤딩이 가드를 보고 있으면 난간과 나란히(트랙 진행 쪽) 돌린다. ④ `ChainRushGame.StartRun` 은 `racer.Reset()` 뒤 `player.FaceTrack()` 으로 헤딩을 트랙 방향에 맞춘다(리셋이 헤딩을 0 으로 만드므로 순서가 중요). ⑤ `FollowCamera` 는 트랙이 아니라 러너 헤딩 뒤를 따른다. yaw 는 `maxYawSpeed`(180°/s)로 쫓고, 높이 하한은 중심선 높이 + 오프셋. `Snap()` 은 트랙 방향. 이전의 "중심선 쪽으로 0.55 당김"은 없앴다. ⑥ `RunnerAnimation` 은 `ForwardSpeed`·`SideSpeed` 를 쓴다(COURSE 8절 T0 발견 항목 해소).
- **물리 시작값** (`RunnerMotor` 직렬화 필드, T1 의 선택 A 와 같은 자리): `maxTurnRate` 120°/s(R30 에 필요한 19°/s @10 m/s, 31°/s @16 m/s 의 약 4배), `turnAcceleration` 1200°/s²(0.1초에 최대), `airTurnScale` 0.5, `sideGrip` 60 / `airSideGrip` 18(이전 옆 이동 가속 값을 그대로 써서 조향 0 일 때 T1 과 같은 숫자), `guardSpeedLoss` 0.15. 체감 튜닝은 하지 않았다.
- **가드**: `ChainRushSceneBuilder.AddGuards` — 발판의 형제로 양쪽에 `Guard rail`(두께 0.3, 높이 1.1, 발판 길이, `M_Frame`, BoxCollider 를 발판 윗면에서 4m 까지 늘림)과 `Guard light`(`M_Link`, 충돌 없음). 안쪽 면 = 발판 가장자리 ±6m. 빌더가 새 씬에도 만들고, 기존 씬용 메뉴 `ProtoHarness/Chain Rush/Add Deck Guards To Open Scene`(Undo 등록, 이미 있으면 건너뜀). 버린 대안: 트랙에서 보이지 않는 벽을 만드는 방식(씬 변경 없음, 커브 재사용 가능 — 사용자가 보이는 쪽을 택함).
- **씬 수정** (에디터에서, YAML 텍스트 수정 없음): `Unity_RunCommand` 로 씬을 열고 위 메뉴 실행 + `RunnerMotor` SetDirty(지운 `lateralSpeed` 줄 정리) 후 저장(사용자 승인). 프로토타입: 발판 9 → 난간 18 + 조명 18 (19:52:06 저장). 무한: 발판 17 → 34 + 34 (19:52:38 저장). 17 인 이유: 무한 씬에 비활성으로 남은 프로토타입 코스 사본(`Skyline Course`, 발판 9)에도 붙었다. 비활성 루트에서 `GetComponentsInChildren` 이 자식을 돌려주기 때문이다. 보이지도 충돌하지도 않고 프로토타입 씬과 모양이 같아지므로 지우지 않았고, 메뉴 주석을 실제 동작대로 고쳤다. **diff 검증**: 두 씬 모두 이름별 오브젝트 수 차이는 `Guard rail`·`Guard light` 추가뿐, 줄을 정렬해 비교하면 빠진 줄은 `lateralSpeed: 7` 하나. 나머지 큰 diff(+5622/−1544, +14469/−6775)는 Unity 가 문서 순서를 다시 배치한 것이다.
- **테스트 사양 변경 (승인)**: `ChainRushInputSourceTests.Steer_ScriptedLeftThenRight_MovesPlayerWithoutDevices` → `..._TurnsFacingWithoutDevices`(왼쪽 0.3초 → 헤딩 < −10°, x < 0 / 오른쪽 0.4초 → 헤딩 +10° 이상). Device 테스트 `Input_KeyboardAndMouse_...` 의 A/D 단언도 같은 사양으로, 이후 그래플 단계 전에 `FaceTrack()`. 옛 단언은 헤딩 방식에서 어떤 회전 속도로도 성립하지 않는다(D 로 바꿔도 0.2~0.3초는 왼쪽을 보고 있음). `RacerStateTests` 2개에는 새 필드 단언을 **추가**만 했다.
- **고치기 전 실패**: 씬에 가드를 넣기 전 `ChainRushSteeringTests`+`ChainRushInputSourceTests` → `testcasecount="11" result="Failed(Child)" passed="8" failed="3"` (19:37:40~19:37:55). `Guard_SteerHardLeft_StaysOnDeckAndKeepsRunning`: `Run ended while steering into the guard.` / `Guard_AngledHit_TurnsAlongRailAndRecoversSpeed`: `Expected: True` / `Guards_BothScenes_LineEveryDeck`: `Deck under Sector 01 needs a guard on each side.` 가드 저장 후 `testcasecount="11" result="Passed" passed="11" failed="0"` (19:53:52~19:54:08).
- **검증** (`feature/course-t2a-steering`, 6000.3.25f1, MCP, KST): 컴파일 확인, Console Error/Exception 0. EditMode `testcasecount="88" result="Passed" passed="88" failed="0"` (19:53:31). PlayMode(Device 제외) 1회차 `testcasecount="31" result="Passed" passed="31" failed="0" duration="130.57"` (19:54:26~19:56:36), 2회차 `testcasecount="31" result="Passed" passed="31" failed="0" duration="130.52"` (19:56:55~19:59:06). Device 테스트를 고쳤으므로 `Run Device Input Tests` 도 실행: `testcasecount="1" result="Passed" passed="1" failed="0"` (19:59:26~19:59:28). Console 경고는 `Deleting invalid font reference.` 뿐(기존과 같음).
- **하지 않은 것**: 커브(T2b), 원심력 비교, 헤딩 범위 제한(지금은 계속 꺾으면 뒤로도 돈다. 가드에 닿으면 진행 방향으로 돌려진다), 벽 없는 구간, 물리 값 체감 튜닝, `CourseTarget` 의 x/z 판정(직선 코스에서는 맞다, T2b 전).

## 2026-10-05 · 코스 T1: 오르막·내리막 — 종단 곡선, 경사 속도 보정, 땅 붙잡기

- **결정**: ① `Centerline` 조각에 시작·끝 경사를 둔다. 시작 경사는 앞 조각의 끝 경사를 이어받고(경사·높이 연속), 높이는 포물선 닫힌 식, 범위 밖은 끝 경사로 연장한다. `AppendStraight(length, toGrade)` 오버로드를 추가했고 기존 `AppendStraight(length)` 는 현재 경사를 유지한다. 경사는 유한하고 |g| ≤ 1(45°, 씬 `slopeLimit`)이어야 하며 아니면 예외다. `S` 와 조각 축은 수평이고 Up 은 월드 위쪽이다(20% 경사에서 실제 길이와 약 2% 차이). ② `TrackFrame` 에 `Grade` 를 더하고 `Position.y` 에 중심선 높이를 넣었다. `TrackCoord.H` 는 중심선 위 높이다. ③ 낙하 실패는 월드 `y < -12` 대신 트랙 기준 `H < -12` 다(긴 내리막이 낙하로 판정되지 않게). ④ 접지 중 목표 속도 = `runSpeed × clamp(1 − slopeSpeedFactor × grade, minSlopeSpeedScale, maxSlopeSpeedScale)`, 공중은 `runSpeed` 그대로. ⑤ 땅 붙잡기: 직전 틱 접지 + 이동 뒤 접지 놓침 + `velocity.y ≤ 0` + 그래플 아님일 때 캡슐 바닥 구에서 아래로 `SphereCast`(`groundSnapDistance` + skinWidth), 맞으면 그만큼 `Move`. ⑥ `ChainRushGame.SetTrack(Centerline)` 공개 API 추가. null·빈 트랙은 예외, 무한 모드는 `InvalidOperationException`(코스가 트랙을 소유). 테스트 도로와 T4 서킷의 진입점이다.
- **데이터 위치 (사용자 선택 A)**: `slopeSpeedFactor` 1.5, `minSlopeSpeedScale` 0.7, `maxSlopeSpeedScale` 1.3, `groundSnapDistance` 0.3 을 `RunnerMotor` 직렬화 필드로 둔다. `runSpeed`·`jumpSpeed` 와 같은 레이서 이동 수치이고, 새 필드는 씬 YAML 에 값이 없어 코드 기본값을 쓰므로 씬·에셋 변경이 없다. 버린 대안 B: 새 SO(`SlopeTuning`) — `COURSE.md` 4-2 문장과는 맞지만 에셋·두 씬 참조·빌더 수정이 필요했다. 코스별 값이 필요해지면 T4 `TrackDefinition` 으로 옮긴다. 범위 오류는 `OnValidate` 에서 LogError.
- **이유**: `COURSE.md` 4-2·6-3, T1 완료 기준(20% 내리막 접지 유지, 6-1 실측). 씬 코스는 아직 평평한 직선이라 경사 0 에서 모든 식이 T0 과 같은 값을 낸다(EditMode exact 단언).
- **검증 방법**: 새 `Tests/PlayMode/ChainRushSlopeTests.cs` 가 프로토타입 씬 옆(x = 300)에 중심선을 따라 0.5m 간격 메시 콜라이더 도로를 만들고 `SetTrack` 으로 같은 중심선을 넣은 뒤 `ShiftOrigin` 으로 러너를 옮긴다. 씬·에셋은 바꾸지 않고 만든 오브젝트·메시는 TearDown 에서 지운다.
- **고치기 전 실패**: 땅 붙잡기 없이(경사 속도 보정까지만) `ChainRushSlopeTests` 4개 실행 → `testcasecount="4" result="Failed(Child)" passed="3" failed="1"` (19:10:05~19:10:30), 실패 `Downhill_TwentyPercent_StaysGrounded`: `Lost ground on 70 of 139 ticks.` 붙잡기 추가 후 `testcasecount="4" result="Passed" passed="4" failed="0"` (19:11:36~19:12:01).
- **실측** (`COURSE.md` 6-1 갱신): 이륙 속도 10.00 m/s, 정점 2.89m(계산 3.01m), 평지 도달 10.25m(계산 10.5m). 75% 제약은 실측 기준 7.7m 가 됐다. +3m 착지는 도달 못 함. 경사 ±20% 구간 실제 전진 속도는 기대값(13 / 7 m/s) ±0.5 안.
- **검증** (`feature/course-t1-slope`, 6000.3.25f1, MCP, KST): 컴파일 확인, Console Error/Exception 0. EditMode T1a 뒤 `testcasecount="88" result="Passed" passed="88" failed="0"` (19:06:48, `CenterlineTests` +10), T1b 뒤 `testcasecount="88" result="Passed" passed="88" failed="0"` (19:12:11). PlayMode(Device 제외) 1회차 `testcasecount="28" result="Passed" passed="28" failed="0" duration="121.74"` (19:12:28~19:14:30), 2회차 `testcasecount="28" result="Passed" passed="28" failed="0" duration="121.76"` (19:14:45~19:16:46). 기존 테스트 무수정, 신규 4. 입력 장치 경로는 바꾸지 않아 Device 실행은 해당 없다. Console 경고는 도메인 리로드 때 `Deleting invalid font reference.` 28건뿐(기존과 같은 경고).
- **하지 않은 것**: 씬 코스에 실제 경사(노면 절차 생성 전이라 불가), 언덕 정상에서 일부러 뜨는 예외·킥커(T5), 커브(T2), 카메라 상하 평활, 경사 속도 체감 튜닝.

## 2026-10-05 · 코스 T0: 직선 트랙 좌표계 — "앞"은 월드 +z 가 아니라 트랙 접선

- **결정**: ① `Runtime/ChainRush/Track/` (네임스페이스 `ProtoHarness.ChainRush.Track`)에 순수 C# 타입 셋을 둔다. `Centerline`(sealed class: 직선 조각 잇기 `AppendStraight`, 앞 조각 버리기 `TrimBefore`, `ShiftOrigin`, `Clear`, `Project(world) → TrackCoord`, `Frame(world)`·`FrameAt(s) → TrackFrame`), `TrackFrame`(readonly struct: `S`·`Position`·`Forward`·`Right`, `Transform`/`InverseTransform` `Direction`·`Point`), `TrackCoord`(readonly struct: `S` double, `D`, `H`). 로컬 벡터는 Transform 과 같은 배치(x = 오른쪽, y = 위, z = 앞)다. 조각 범위 밖은 양 끝 접선으로 연장한다(완주선을 넘는 순간에도 `S` 가 오른다). 조각 없는 질의, NaN·무한 입력, 0 이하 길이는 예외다(§5). ② `S` 는 레이스 시작부터의 절대 거리(double)이고 원점 이동 뒤에도 이어진다. 조각마다 월드 시작점을 따로 둬서 float 오프셋이 작게 유지된다. ③ `ChainRushGame` 이 `Centerline` 을 소유하고 `Track` 으로 공개한다(공개 API 추가). T0 에서는 두 씬 모두 "월드 원점에서 +z 로 뻗는 직선"을 `Awake` 에서 코드로 만든다. 일반 모드는 길이 `finishZ` 한 조각, 무한 모드는 `EndlessCourse.SeedTrack` 이 56m 한 조각으로 시작하고 `Step` 이 한 풀 길이(448m) 앞까지 이어 붙이고 112m 뒤를 버린다. ④ 트랙 기준으로 바꾼 것: 모터 조향·전진·그래플 해제 판정·해제 부스트, 그래플 후보의 앞쪽·좌우 감점, 카메라 오프셋·바라보는 점, 적 목표·등장 오프셋, 진행도·완주·거리, 무한 코스 재배치·발판 끝 거리·원점 이동. `EndlessCourse` 의 `originDistance` 는 없앴다(트랙 `S` 가 대신한다). ⑤ `FollowCamera` 의 첫 `Snap()` 을 `Awake` 에서 `Start` 로 옮겼다. 트랙은 `ChainRushGame.Awake` 에서 생기고 오브젝트 간 `Awake` 순서는 정해져 있지 않기 때문이다. ⑥ 씬·프리팹·빌더·직렬화 필드는 바꾸지 않았다.
- **이유**: 커브·경사·순환 트랙(T1~T4)이 모두 이 좌표계 위에 선다(`docs/COURSE.md` 3절, 10절). 보이는 변화 없이 좌표계만 바꾸는 단계라서 기존 테스트 무수정 통과가 그대로 회귀 증명이 된다. 축이 정확히 0·1 인 +z 프레임에서는 분해·재조립이 기존 x/z 연산과 같은 숫자를 낸다(EditMode exact 단언으로 확인).
- **`RacerState` 에 `s`·`d` 를 두지 않은 이유**: 위치의 진실 원천이 아직 `Transform`/`CharacterController` 라 사본이 생긴다. 필요할 때 투영한다. 위치를 옮길 때 같이 정한다(`COURSE.md` 8절의 권장은 그때 다시 본다).
- **작은 차이**: 무한 모드 거리는 이전에 `double` 누적값 + float z 였고, 이제 조각의 `StartS`(double) + 조각 안 float 오프셋이다. 같은 값이 수 ulp 범위에서 다를 수 있다. 기존 테스트는 거리를 허용 오차로 단언한다(`ChainRushEndlessTests.cs:150,203`).
- **검증** (`feature/course-t0-track`, 6000.3.25f1, MCP, KST): 컴파일 후 새 타입·메서드 로드 확인, Console Error/Exception 0. EditMode T0a 뒤 `testcasecount="78" result="Passed" passed="78" failed="0"` (18:47:28, 신규 `CenterlineTests` 18), T0b 뒤 `testcasecount="78" result="Passed" passed="78" failed="0"` (18:50:02). PlayMode(Device 제외) 1회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="97.46"` (18:50:22~18:51:59), 2회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="97.40"` (18:52:21~18:53:59). 기존 테스트 무수정. 입력 장치 경로는 바꾸지 않아 Device 실행은 해당 없다. Console 경고는 도메인 리로드(18:52:19) 때의 `Deleting invalid font reference.` 24건뿐이다(P1-3c 항목과 같은 경고, 원인 미확인).
- **발견**: `COURSE.md` 8절 표에 빠진 월드 축 의존 두 곳. `CourseTarget.Touches`·`TryHit` 의 x/z 박스 판정(`CourseTarget.cs:42-55`)과 `RunnerAnimation` 의 달리기 위상 `Velocity.z`(`RunnerAnimation.cs:58`). 직선에서는 값이 같다. 8절에 추가했고 T2 전에 바꾼다.
- **하지 않은 것 (T2 전 확인)**: 원점 이동 조건은 아직 "앞 방향 투영 ≥ 448m" 다. 커브가 들어가면 플레이어 위치 벡터 기준으로 바꾼다(`COURSE.md` 7-1절). 적 회전(`EnemyDirector.cs:83`)은 월드 축 그대로다. 오르막·내리막(T1), 커브·카메라 회전 평활(T2), 시드 생성(T3), 트랙 정의 SO(T4).

## 2026-10-04 · 코스 설계 방향: 트랙 좌표계, 커브·경사, 이어진 도로

- **결정** (사용자, 설계서 `docs/COURSE.md` 11절): ① 커브는 트랙 좌표계(`s` 진행 거리, `d` 좌우, `h` 높이)로 만든다. ② 중심선은 자체 해석 조각(직선·수평 원호·종단 곡선)으로 정의한다. Unity Splines 패키지는 쓰지 않는다. ③ 노면 메시·콜라이더는 중심선 + 단면을 따라 절차 생성하고, 장식만 프리팹이다. ④ 노면은 이어진 도로가 기본이고 틈은 틈 모듈로만 나온다. ⑤ 무한 모드에도 커브를 넣는다. 원점 이동은 벡터 기반으로 바꾸고 자기 교차 방지 제약을 둔다. ⑥ 경사 속도 보정을 넣는다(시작값 k = 1.5, 테스트 후 조절). ⑦ 원심력은 계수를 데이터로 두고(기본 0) 테스트 후 결정한다.
- **미결**: 커브 조작 모델(자동 추종 / 자유 조향). 기본값 자동 추종으로 설계를 진행하고 T2 전에 확정한다.
- **이유**: 최종 목표가 순환 트랙 레이스라서 진행도·순위를 `s` 하나로 재는 트랙 좌표계가 결국 필요하다(교차로 90° 회전이나 화면만 휘기로는 레이스가 안 된다). 해석 조각은 길이·좌표가 닫힌 식이라 결정적이고 패키지가 필요 없다.
- **버린 대안**: 교차로 90° 회전(템플 런식), 커브드 월드 셰이더만 쓰기(장식으로는 나중에 가능), Unity Splines(manifest 변경 + 결정성 미확인).
- **다음**: 구현은 `COURSE.md` 10절의 T0(직선 상태에서 좌표계만 교체, 기존 테스트 무수정 통과)부터 단계마다 합의한다. P1 "시드 기반 코스"는 T3 에 흡수된다.

## 2026-10-04 · 운동 상태 분리 (P1-3c): 모터·그래플 상태를 `RacerState` 로, 접근은 `ref`

- **결정**: ① 다음 필드를 `RacerState` 로 옮겼다. `RunnerMotor` 의 `velocity`·`steer`·`jumpQueued`·`coyoteTime`, `GrappleController` 의 `ropeLength`·`missUntilTick`, 붙은 앵커. 앵커는 `Transform` 대신 `anchors` 배열 인덱스(`int`, `RacerState.NoAnchor` = -1)로 저장한다. ② 운동 값(`Velocity`, `Steer`, `JumpQueued`, `CoyoteTime`, `RopeLength`)은 **`ref` 반환**으로 노출한다(사용자 선택 A). 모터는 `Step` 첫 줄에서 `ref` 로컬로 받고 나머지 코드는 글자 그대로 뒀다. ③ 앵커·빗나감은 메서드로만 바꾼다(`Attach(index, length)`, `Detach()`, `MarkMiss(tick)`, `ClearMiss()`). 음수 인덱스, 0 이하·NaN 줄 길이, 음수 틱은 예외다(§5). ④ `ChainRushGame.Racer` 를 공개했다(공개 API 추가). 모터·그래플의 기존 공개 시그니처(`Velocity`, `Steer`, `Speed`, `IsAttached`, `AnchorPosition`, `RopeLength`, `JustMissed`, `TryAttach`, `Release`, `ConstrainMotion`, `ClearMiss` 등)는 그대로다. ⑤ `Reset()` 은 운동 상태도 0으로 만든다.
- **이유**: 레이서 한 명의 시뮬 상태가 한 객체에 있어야 레이서를 여럿 두고, 나중에 스냅샷·보정(P3)을 할 수 있다(`DESIGN.md` §3). 앵커를 인덱스로 둔 것은 상태가 엔진 참조 없이 값으로만 이뤄지게 하려는 것이다.
- **A 를 고른 이유와 대가**: Step 도중 `Release(true)` → `AddReleaseBoost` 와 `PrimaryAction` 이 같은 속도·점프 예약을 고친다(`RunnerMotor` Step 의 입력 처리, 이동 뒤 해제). 그래서 복사 후 되쓰기(B)는 순서가 엇갈릴 위험이 있었다. `ref` 는 같은 메모리를 가리키므로 순서가 지금과 같다. 대가로 `game.Racer` 를 가진 누구나 운동 값을 바꿀 수 있다(이전에는 모터의 private 필드였다).
- **작은 차이**: `RopeLength` 는 이전에 해제 뒤에도 마지막 값이 남았다. 이제 `Reset()`(새 판 시작)에서 0이 된다. 해제만으로는 값이 유지되는 것은 같다. 붙어 있지 않을 때 이 값을 읽는 곳은 테스트의 실패 메시지 문자열(`ChainRushTests.cs:222`)뿐이다.
- **남긴 것**: 위치(`Transform`·`CharacterController`). 그래플의 `candidate`(매 프레임 계산, HUD 표시), `lastAnchor`·`visualExtension`(표현). 모터의 `spawnPosition`(설정값). `coyoteTime` 은 초 단위 그대로다(틱 환산 안 함).
- **검증** (`feature/p1-racer-motion`, 6000.3.19f1, KST): 컴파일 `Tundra build success (7.69 seconds)`, Console Error/Exception 0. EditMode `testcasecount="60" result="Passed" passed="60" failed="0"` (15:05, `RacerStateTests` 11→17). PlayMode(Device 제외) 1회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="101.73"` (15:05:35~15:07:16). 2회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="101.41"` (15:07:41~15:09:22). 기존 테스트 무수정. 입력 장치 경로는 바꾸지 않아 Device 실행은 해당 없다. 씬·프리팹 변경 없음.
- **Console 경고 (이번 변경과 무관으로 판단)**: 도메인 리로드(15:01:18) 때 `Deleting invalid font reference.` (`UnityEditor.ScriptReloadProperties:Load`) 경고가 20건 이상 나왔다. 같은 경고가 이번 에디터 세션 Editor.log 에서 6419줄부터 261회 나왔다(P1-3a 때부터). 직전 세션 로그(`Editor-prev.log`)에는 0회다. 원인은 확인하지 못했다.
- **하지 않은 것**: 위치 이전, 레이서 여럿, 스냅샷·직렬화, `Phase` 를 레이서별로 나누기.

## 2026-10-04 · 표현 분리 (P1-3b): 몸체 기울이기는 `RunnerTilt` 가 한다

- **결정**: ① `Runtime/ChainRush/Visuals/RunnerTilt.cs`(MonoBehaviour, `[DefaultExecutionOrder(100)]`)가 `LateUpdate` 에서 `body.localRotation = Evaluate(motor.Velocity, motor.Steer)` 를 쓴다. 공식은 기존 `RunnerMotor.Step` 의 `Quaternion.Euler(velocity.y * -0.9f, steer * 12f, steer * -16f)` 를 그대로 옮긴 정적 순수 함수다. ② `RunnerMotor` 에서 `bodyVisual` 필드·검사·쓰기를 지웠다. 대신 `public float Steer` 를 추가했다(공개 API 추가). ③ 실행 순서 100 은 `RunnerAnimation`(120)과 `GrappleController`(150)보다 앞이다. 손에 달린 체인이 그 프레임의 기울기를 보도록 하려는 것이다. ④ 빌더 두 개를 고쳤다. `ChainRushSceneBuilder` 는 러너에 `RunnerTilt` 를 붙인다. `ChainRushPresentationBuilder` 는 기존 몸체를 `RunnerTilt.body` 에서 읽고 새 모델로 바꿔 연결한다.
- **이유**: 시뮬레이션 틱 안에서 시각물 Transform 을 쓰면 두 가지 문제가 생긴다. 헤드리스 서버에는 시각물이 없고, 아트를 교체할 때 시뮬 코드를 건드려야 한다(2026-10-02 P1-2 항목의 "발견"). 모터 상태는 틱에서만 바뀐다. 그래서 매 프레임 다시 계산해도 값은 틱의 값과 같다. 정지·실패 중에는 유지되고, 재시작하면 속도·조향이 0이 되어 identity 가 된다.
- **씬 수정** (에디터에서 했고 YAML 은 텍스트로 수정하지 않았다): `Unity_RunCommand` 로 두 씬의 러너에 `RunnerTilt` 를 붙였다(Undo 등록). `body` 에는 모터의 기존 `bodyVisual` 값을 그대로 연결했다. 프로토타입은 `Runner Visual`(fileID 1122326606), 무한은 `Armored Runner`(fileID 1415225579)다. 저장은 사용자가 Ctrl+S 로 했다(§0).
- **Unity 자동 변경**: 프로토타입 씬을 저장할 때 라이트 하나(GameObject fileID 266071145)에 URP `UniversalAdditionalLightData` 가 기본값으로 붙었다(30줄). 우리가 만든 것이 아니며 그대로 커밋한다.
- **옛 줄 정리**: 코드에서 필드를 지운 뒤에도 두 씬 YAML 에 `bodyVisual:` 줄이 남았다. `Unity_RunCommand` 로 `RunnerMotor` 를 `SetDirty` 하고 사용자가 저장해 Unity 가 다시 쓰게 했다. diff 상 삭제는 두 씬 모두 그 한 줄뿐이다. 텍스트로 지우지 않았다(§0). 참고로 dirty 표시 없이 저장하면 파일이 바뀌지 않는다(실제로 한 번 그랬다).
- **검증** (`feature/p1-body-tilt`, 6000.3.19f1, KST): 컴파일 `Tundra build success`, Console Error/Exception/Warning 0. EditMode `testcasecount="54" result="Passed" passed="54" failed="0"` (14:35, 신규 `RunnerTiltTests` 4). PlayMode(Device 제외) 1회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="112.26"` (14:36:07~14:37:59). 2회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="113.04"` (14:38:31~14:40:24). 옛 줄 정리로 씬이 바뀐 뒤 같은 상태로 다시 돌렸다: 1회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="112.65"` (14:44:36~14:46:28), 2회차 `testcasecount="24" result="Passed" passed="24" failed="0" duration="113.66"` (14:47:00~14:48:53). 신규 `Tilt_ScriptedSteerLeft_LeansBodyFromMotorState` 포함, 기존 테스트 무수정. 입력 장치 경로는 바꾸지 않아 Device 실행은 해당 없다.
- **하지 않은 것**: P1-3c(모터·그래플 운동 상태 이전), 기울이기 공식·수치 변경, `RunnerAnimation` 과 통합.

## 2026-10-04 · 레이서 상태 분리 (P1-3a): 레이서 한 명분 상태를 `RacerState` 로 꺼낸다

- **결정**: ① `Runtime/ChainRush/RacerState.cs` (네임스페이스 `ProtoHarness.ChainRush`, 순수 C# `sealed class`)에 레이서 한 명의 체력·적중 수·그래플 수·무적 마감 틱·공격 시각 마감 틱·다음 공격 가능 틱을 둔다. ② 시간 질의는 모두 현재 틱을 인자로 받는다(`IsInvulnerable(tick)`, `CanAttack(tick)` 등). 시계를 직접 읽지 않는다. ③ 공격의 "쿨다운"(`BeginAttackCooldown`)과 "시각"(`ShowAttack`)은 메서드를 나눴다. 무한 모드는 적이 맞았을 때만 쿨다운을 걸고, 일반 모드는 둘 다 건다(`ChainRushGame.Attack`). ④ `ChainRushGame` 은 필드 6개 대신 `RacerState` 하나를 갖는다. `Health`/`Hits`/`Grapples`/`DamageFlash`/`AttackActive` 등 **공개 시그니처는 그대로**이고 `racer` 에 위임한다. ⑤ 계약 위반(`rules` null, 음수 틱)은 예외로 즉시 던진다(§5).
- **이유**: 한 월드에 레이서가 여럿이 되려면 레이서별 상태가 세션(`ChainRushGame`)에서 떨어져 있어야 한다(`DESIGN.md` C1, §3). 첫 단계는 "우선 한 명이어도 `RacerState` 를 꺼낸다"(`DESIGN.md` P1). 공개 시그니처를 유지해 HUD(`ChainRushHud.cs:95,135,162,204`)와 PlayMode 테스트를 무수정으로 둔다. 무수정 통과가 곧 동작 불변의 증거다.
- **검증** (`feature/p1-racer-state`, `dev` `7feadfa` 위로 리베이스 후, 6000.3.19f1, KST): 컴파일 `Tundra build success`, Console Error/Exception 0. EditMode `testcasecount="50" result="Passed" passed="50" failed="0"` (14:09, 신규 `RacerStateTests` 11). PlayMode(Device 제외) 1회차 `testcasecount="23" result="Passed" passed="23" failed="0" duration="100.81"` (14:10:18~14:11:58). 2회차 `testcasecount="23" result="Passed" passed="23" failed="0" duration="102.67"` (14:12:24~14:14:07). 기존 PlayMode 테스트 무수정.
- **경과**: 리베이스 전 이 브랜치에서 PlayMode(전체 24) 를 3회 돌려 모두 같은 2개가 실패했다. 이 실패는 아래 "장치 입력 테스트 분리" 항목에 기록하고 처리했다. 실패한 두 테스트는 이번 변경이 닿지 않는 키 입력 단계에서 멈췄다.
- **이번에 하지 않은 것 (다음 후보)**: **P1-3b** — `RunnerMotor.Step` 의 `bodyVisual.localRotation` 쓰기(`RunnerMotor.cs:83,114`)를 표현 컴포넌트로 옮긴다. 두 씬과 빌더 두 개(`ChainRushSceneBuilder.cs:174`, `ChainRushPresentationBuilder.cs:55,91`)를 수정해야 하므로 분리했다. **P1-3c** — 모터·그래플 운동 상태(`velocity`, `coyoteTime`, `jumpQueued`, `ropeLength`, `attachedAnchor`)를 레이서 상태로 옮긴다. `Phase`(Running/Failed/Complete)는 세션 상태로 남겼다. 레이서별 분리는 다인 레이스에서 한다.

## 2026-10-04 · 장치 입력 테스트 분리: 가상 키보드 테스트는 병합 조건에서 뺀다

- **결정**: ① `Combat_EachDirection_SpaceFiresConnectsAndRetracts` 는 가상 키보드 대신 스크립트 입력 소스(`SetInputSource`)로 공격을 넣는다. 이름은 `Combat_EachDirection_AttackFiresConnectsAndRetracts`. 방향별 단언(`Firing` → 비행 뒤 적중 1 → 체력 3 → 조우 종료)은 그대로 뒀다. ② `Input_KeyboardAndMouse_StartsSteersJumpsGrapplesAndRestarts` 는 본문을 고치지 않고 `[Category("Device")]` 만 붙였다. ③ 메뉴 `Run PlayMode Tests` 는 `categoryNames = { "!Device" }` 로 Device 를 빼고 돌린다. 새 메뉴 `Run Device Input Tests` 는 Device 만 돌린다. ④ 병합 조건은 "PlayMode(Device 제외) 연속 2회"로 바꾼다. 입력 장치 경로를 바꾼 브랜치는 Device 실행 결과를 붙인다. 규칙 본문은 `CLAUDE.md` §9-2.
- **계기**: P1-3a(`feature/p1-racer-state`, WIP `a65404f`) 검증 중에 PlayMode 가 3회 연속으로 같은 2개 실패를 냈다. 이 머신, 에디터 6000.3.19f1, Input System 1.20.0, KST 기준이다.

  | 시각 | 조건 | 결과 |
  |---|---|---|
  | 13:36:16~13:38:21 | 스크립트 임포트·컴파일 직후 첫 실행, 에디터 비활성 | `testcasecount="24" result="Failed(Child)" passed="22" failed="2" duration="124.42"` |
  | 13:40:02~13:42:07 | 같은 코드, 에디터 비활성 | `testcasecount="24" result="Failed(Child)" passed="22" failed="2" duration="124.97"` |
  | 13:46:23~13:48:16 | 같은 코드, 에디터 활성(`isApplicationActive=True` 확인 후 시작) | `testcasecount="24" result="Failed(Child)" passed="22" failed="2" duration="113.79"` |

  실패 원문은 2026-10-02 기록과 같다. `Combat_…SpaceFires…`: `Expected: Firing / But was: Vulnerable`(`:92`, 첫 방향 `Above` 에서 멈춤). `Input_KeyboardAndMouse_…`: `Expected: True / But was: False`(`:123`, Enter 뒤 `IsRunning`). 사용자에 따르면 원래 환경(6000.3.25f1)에서도 실패한 적이 있다.
- **이유**: 두 테스트가 지키는 것은 대부분 시뮬레이션이다. 장치 이벤트 전달은 그 앞단에서 흔들린다. 전달이 흔들리면 시뮬레이션이 바뀌지 않아도 병합 조건이 막힌다. P1-3a 처럼 입력 경로를 건드리지 않은 변경까지 막는다. 장치 없는 경로는 `ChainRushInputSourceTests` 7개가 이미 같은 환경에서 매번 통과했다.
- **원인은 확인하지 못했다.** 확인한 것: ① 실행 중 Console Error/Exception 0건(그래서 `StartRun` 예외로 `IsRunning=False` 가 된 것은 아니다). ② 설정은 `editorInputBehaviorInPlayMode=PointersAndKeyboardsRespectGameViewFocus`, `backgroundBehavior=ResetAndDisableNonBackgroundDevices`. Input System 은 Play 중 **Game 뷰**에 포커스가 없으면 키보드·포인터 이벤트를 player 업데이트에서 처리하지 않고 editor 업데이트로 넘긴다(`InputManager.cs:3647-3663`, 패키지 소스). ③ 에디터 창이 활성이어도 실패했다(3회차). Game 뷰 포커스 자체는 측정하지 못했으므로 포커스 가설은 기각도 확인도 아니다. ④ **Device 테스트만 단독으로 돌리면 통과했다**(아래 검증, 05:02). 전체 실행 안에서만 실패하는지, 단순 간헐인지는 가르지 못했다.
- **검증** (`fix/playmode-device-input-tests`, 6000.3.19f1): EditMode `testcasecount="39" result="Passed" passed="39" failed="0"` (13:57). PlayMode(Device 제외) 1회차 `testcasecount="23" result="Passed" total="23" passed="23" failed="0" duration="104.70"` (13:57:49~13:59:34). 2회차 `testcasecount="23" result="Passed" total="23" passed="23" failed="0" duration="103.68"` (14:00:00~14:01:44). Device 1회 `testcasecount="1" result="Passed" passed="1" failed="0" duration="3.15"` (14:02:12). 컴파일 `Tundra build success`, Console Error 0.
- **버린 대안**: 두 테스트 삭제(사용자 제안). 키 매핑(`KeyboardMouseInputSource`)과 메뉴 키(Enter/R)를 지키는 유일한 테스트이고, 방향별 전투 순서를 지키는 유일한 테스트라서 버렸다. `InputTestFixture` 도입은 manifest `testables` 변경이 필요할 수 있어(§0) 하지 않았다(필요 여부는 확인 못 함).
- **하지 않은 것**: 가상 장치 실패의 근본 원인 조사. Device 테스트 본문 수정.

## 2026-10-02 · 병합 조건은 Unity 가 읽는 곳을 바꾼 브랜치에만 적용한다

- **결정**: §9-2 병합 조건(컴파일 0 + EditMode + PlayMode 연속 2회)은 변경이 `Assets/`, `Packages/`, `ProjectSettings/` 에 닿을 때 적용한다. 닿지 않는 브랜치는 **문서 검증**(§ 참조 해석, `.codex` 재생성 diff, 이동 시 줄 대조)으로 병합하고, 닿지 않았다는 증거로 `git diff --name-only <병합 대상>...HEAD` 출력을 붙인다. 규칙 본문은 `CLAUDE.md` §9-2.
- **이유**: 병합 조건은 Unity 가 컴파일·실행하는 것을 지키려고 있다. `docs/`, `tools/`, `.claude/` 는 `Assets/` 바깥이라 Unity 가 읽지 않는다(`ARCHITECTURE.md` §6). 그런 브랜치에서 테스트를 돌려도 새 정보가 없다. 반대로 매번 "이번엔 괜찮겠지"로 건너뛰면 조용한 우회가 되므로 조건과 증거를 규칙에 박는다(§5).
- **계기**: `docs/rules-split` 가 첫 사례. 이 머신에는 6000.3.25f1 이 없어(Unity 작업은 다른 로컬 환경) Unity 검증 자체가 불가능했다.
- **버린 대안**: 문서 브랜치도 Unity 검증 요구 — 정보 없는 비용이고, 이 머신에서는 문서 수정이 막힌다. 매번 사용자에게 면제 승인 받기 — 같은 판단을 반복하며, 기준이 기록되지 않는다.
- **같은 브랜치에서 함께 고친 것**: `CLAUDE.md` §2 의 "`.cs` 17개" 같은 변하는 개수를 규칙에서 지웠다(규칙 문서에 변하는 값을 박지 않는다 — 아래 "규칙 분리" 항목과 같은 원칙). 개수가 필요하면 그때 Glob 으로 센다.

## 2026-10-02 · 규칙 분리: CLAUDE.md 는 "항상 지킬 것", `docs/RULES/` 는 "작업별 절차"

- **결정**: CLAUDE.md 에서 특정 작업 때만 필요한 절을 `docs/RULES/` 5개 파일로 **이동**한다 — `CONVENTIONS`(§1-2·1-3), `VERIFICATION`(§4-2·4-2b·4-3·4-4), `LOOKUP`(§6-2·6-3), `SUBAGENTS`(§7-1·7-2 일부·7-4), `BRANCHING`(§9-1·9-3). CLAUDE.md 맨 위에 "이 작업 전에 이 문서를 읽는다" 표를 둔다. 구속력은 CLAUDE.md 와 같다.
- **이유**: CLAUDE.md(458줄, 약 25KB)는 매 세션 전부 로드된다. 그중 MCP 체인·컴파일 절차·인덱스 절차·파견 운영처럼 특정 작업에서만 쓰는 내용이 금지선을 묻는다. 사용자 요청("상위 문서 양이 너무 많다")에 대한 판단과 조건을 사용자가 승인했다.
- **나눈 기준 4가지**: ① 금지·관문·승인·병합 조건은 CLAUDE.md 에 남긴다(문서로 빼면 열어 볼 때만 읽혀 강제력이 약해진다). ② 포인터는 "언제 읽는가"를 박은 트리거형으로 쓴다. ③ § 번호를 바꾸지 않는다(에이전트 정의·AGENTS.md·이 문서가 번호로 인용한다). ④ 복사가 아니라 이동이다(2026-08-25 "사본은 3분 만에 갈라졌다").
- **계획 대비 바뀐 점**: 계획에는 §9-2 병합 보고 상세를 `BRANCHING` 으로 옮긴다고 적었으나 §9-2 는 전부 병합 관문이라 기준 ①에 따라 남겼다. §7-2 의 "병렬 절대 금지"와 §7-3 파견 기준(파견 여부를 정하는 관문)도 같은 이유로 남겼다. §5 는 예시 코드와 규칙이 "위처럼"으로 묶여 있어 통째로 남겼다.
- **로컬 경로**: 머신마다 다른 값(`D:/PCUBE/ProtoHarness`, Hub 경로)을 규칙 문서에 박지 않는다. 프로젝트는 `git rev-parse --show-toplevel`, 버전은 `ProjectVersion.txt`, Unity.exe 는 환경변수 `UNITY_EXE` 우선 + Hub 기본 경로(`docs/RULES/VERIFICATION.md` §4-3). `unity-verifier` 정의도 같이 고쳤다. 이 머신에는 6000.3.25f1 이 **없다**. 기존 Unity 작업은 다른 로컬 환경에서 했다(사용자 확인).
- **버린 대안**: 분량 기준으로 옮기기 — 금지선까지 빠진다. `docs/` 바로 아래에 두기 — 프로젝트 사실 문서(ARCHITECTURE 등)와 작업 규칙이 섞인다. 경로 계산 스크립트(`tools/unity-batch.ps1`) — 더 확실하지만 새 도구라 반복 사용이 생기면 별도 합의한다.
- **같이 합의된 다음 단계 (미착수, 단계마다 별도 승인)**: 기능별 문서 `docs/SYSTEMS/<이름>.md`(ARCHITECTURE §4 의 "3줄 넘으면 분리" 규칙 실행). 조건: 구현된 것만 쓴다(통신은 DESIGN 에 계획으로 둔다), 코드 위치는 파일·타입·메서드까지만(줄번호 금지), 수치는 SO 를 가리킨다, 머리에 기준 커밋, 시스템 코드를 바꾸면 같은 브랜치에서 문서도 고친다. 순서: `Grapple.md` 시범 → 나머지 8개 + ARCHITECTURE 축소.
- **발견 (범위 밖, 고치지 않음)**: `tools/sync-agents.ps1 -Check` 가 git 이 체크아웃한 상태에서는 내용이 같아도 5개 모두 "어긋남"(exit 1)을 냈다. `core.autocrlf=true` 로 작업 폴더의 `.toml` 이 CRLF 인데 스크립트는 LF 로 만든 텍스트와 그대로 비교한다. 재생성하면 파일이 LF 가 되어 exit 0 이 되지만, git 이 다시 체크아웃하면 같은 증상이 돌아온다. 재생성 후 `git diff -- .codex` 가 비는 것으로 내용 일치를 확인했다.

## 2026-10-02 · 병합 조건 강화: PlayMode 연속 2회 통과, 간헐 실패 기록

- → 2026-10-13 "병합 2회차 축소" 로 일부 대체: Net 쪽만 바꾼 브랜치는 2회차를 `Net` 카테고리만 돌린다. 그 밖은 아래 그대로다.
- → 2026-10-14 "테스트 3단" 으로 대체: 같은 코드 2회 통과는 마일스톤(`dev → main`) 조건으로 옮겼다. 병합(`feature → dev`)은 1회. 실패 보고 규칙은 그대로다.
- **결정**: 병합 조건을 "컴파일 0 + EditMode 통과 + **같은 코드에서 PlayMode 연속 2회 통과**"로 바꾼다. 병합 보고에는 각 실행의 결과 XML 값을 모두 적고, 한 번이라도 실패하면 원문과 함께 보고하며 원인을 설명하지 못하면 병합하지 않는다. 규칙 본문은 `CLAUDE.md` §9-2.
- **이유**: P0(`103270b`), P1-1(`6ed7676`), P1-2(`8520a1c`)를 PlayMode **1회 통과**로 병합했다. 그 뒤 같은 코드(`Assets` 차이 0, `git diff 8520a1c HEAD -- Assets`)에서 1회 실패가 나왔다. 아래 표가 근거다.

  | 시각 | 에디터 세션 | PlayMode 결과 | 비고 |
  |---|---|---|---|
  | 01:04 | A | `24/24` | 병합 근거 |
  | 01:22:56~01:24:32 | B(01:21:02 시작), 첫 실행 | `result="Failed(Child)" total="24" passed="22" failed="2"` | 사용자가 평소와 같은 방식으로 실행 |
  | 01:28 | B | `24/24` | `isApplicationActive=False` 에서 시작 |
  | 01:30 | B | `24/24` | 동일 |
  | 01:33:47~01:35:26 | C(01:32:59 시작), 첫 실행 | `24/24` | 에디터 재시작 직후 |

- **실패 원문**: `Combat_EachDirection_SpaceFiresConnectsAndRetracts` — `Expected: Firing / But was: Vulnerable` (`ChainRushEndlessTests.cs:92`). `Input_KeyboardAndMouse_StartsSteersJumpsGrapplesAndRestarts` — `Expected: True / But was: False` (`ChainRushTests.cs:123`, `Enter` 후 `game.IsRunning`). 둘 다 `InputSystem.AddDevice<Keyboard>()` 가상 장치로 이벤트를 넣는 테스트이고, 각자 **첫 키보드 단계**에서 멈췄다.
- **원인은 확인하지 못했다.** 확인된 사실: 입력 설정은 `PointersAndKeyboardsRespectGameViewFocus`(`InputSystem.settings.editorInputBehaviorInPlayMode`로 읽음). Console 에러와 `error CS` 없음. `Enter` 경로(`ChainRushGame.Update` 의 `Keyboard.current` 직접 읽기)는 입력 추상화 이전부터 있던 코드라 P1-2 회귀로 설명되지 않는다.
- **기각된 가설**: ① 에디터 창이 비활성이라서 — 01:28, 01:30 은 비활성에서 시작해도 통과. ② 에디터 재시작 직후 첫 실행이라서 — 01:33 첫 실행이 통과. ③ 도메인 리로드·강제 재컴파일 — 모든 실행(통과 포함)에 똑같이 있음(`Editor-prev.log` 3235, 3711, 4639줄 / `Editor.log` 956줄). ④ 실행 방식 — 사용자 확인: 같은 방식이었다.
- **남은 추정(미확인)**: 실패한 세션은 시작 직후 스크립트 23개를 임포트한 상태였다(`Editor-prev.log`). 첫 프레임 끊김이 가상 장치 이벤트 처리 시점을 흔들었을 가능성이 있으나 실험하지 않았다. 01:22:07 에 뜬 Unity 프로세스 2개는 세션 종료와 함께 사라져 명령줄을 확인하지 못했다(임포트 보조 프로세스로 추정).
- **하지 않은 것**: 테스트 수정, `ProjectSettings` 수정. 재현이 안 되는 상태에서 고치면 효과를 확인할 방법이 없다. 실패가 다시 나오면 그때 별도 합의로 올린다. 실패율 측정을 위한 추가 반복 실행도 하지 않았다.
- **알아둘 점**: 결과 XML 은 EditMode·PlayMode 가 같은 파일(~~`C:\Users\Public\Documents\ESTsoft\CreatorTemp\`~~ `ChainRush-PlayMode-results.xml`)을 덮어쓴다. 실행마다 값을 바로 읽어 기록한다. (2026-10-04 정정: 경로는 당시 머신의 값이다. 머신마다 다르며 Unity 의 `Path.GetTempPath()` 아래다. `ARCHITECTURE.md` "테스트 결과" 행 참조.)

## 2026-10-02 · 입력 추상화 (P1-2): 시뮬은 장치가 아니라 `TickInput` 을 받는다

- **결정**: ① `Runtime/ChainRush/Control/` (네임스페이스 `ProtoHarness.ChainRush.Control`) 에 `TickInput`(readonly struct), `IInputSource`, `InputLatch`, `KeyboardMouseInputSource` 를 둔다. ② `RunnerMotor.Update` 의 장치 읽기를 제거하고 `Step(in TickInput)` 이 입력을 받는다. `ChainRushGame` 이 소스를 갖고 `Update` 에서 `Poll()`(실행 중일 때만), `FixedUpdate` 에서 `Consume()`, `StartRun` 에서 `Clear()` 를 부른다. ③ 기본 소스는 `Awake` 에서 코드로 만든다(직렬화 참조 없음, 씬 수정 없음). `SetInputSource(IInputSource)` 로 교체하며 null 이면 `ArgumentNullException`. ④ 조향은 `float`, 범위 [-1,1] 밖·NaN 은 예외(보정하지 않음, §5). 양자화는 P3 에서 소스의 `Consume()` 안에서 한다. ⑤ 메뉴 입력(R·Enter·Esc·M, HUD 버튼)은 이번에 소스에 넣지 않았다.
- **이유**: 서버에 보낼 입력, 터치, 리플레이가 같은 자리(`TickInput`)에 꽂혀야 한다. 시뮬이 `Keyboard.current` 를 직접 읽으면 서버(헤드리스)에는 입력 장치가 없다. 폴더 이름을 `Input` 이 아닌 `Control` 로 한 것은 `ProtoHarness.ChainRush.Input` 네임스페이스가 하위 코드에서 `UnityEngine.Input` 을 가리기 때문이다.
- **의미가 안 바뀐 것**: 키 매핑(A/D·←/→, 좌클릭, 좌클릭 뗌·우클릭, Space), 실행 중이 아닐 때 눌린 입력은 버림, 한 틱 구간에 누름과 뗌이 같이 오면 "누름 → 뗌" 순서로 둘 다 적용, 입력 지연(프레임에서 읽고 다음 틱에 적용). 공개 시그니처 중 바뀐 것은 이전 커밋에서 만든 `RunnerMotor.Step()` → `Step(in TickInput)` 하나이고 테스트가 직접 부르지 않는다.
- **검증**: EditMode `testcasecount="39" result="Passed" total="39" passed="39" failed="0"` (01:02 KST, 신규 `InputLatchTests` 8 + `TickInputTests` 9). PlayMode `testcasecount="24" result="Passed" total="24" passed="24" failed="0" duration="101.0614821"` (01:04 KST): 기존 17개 **무수정** 통과(실제 `Keyboard`/`Mouse` 장치를 쓰는 `Input_KeyboardAndMouse_*`, `Combat_*_SpaceFires*` 포함) + 신규 `ChainRushInputSourceTests` 7개(장치 없이 스크립트 소스로 조향·점프·공격). Console error 0.
- **이번에 하지 않은 것·미확인**: 터치·온스크린 컨트롤, `InputAction`/리바인딩(`Assets/InputSystem_Actions.inputactions` 는 `EditorBuildSettings.asset:13` 에 등록된 템플릿이고 우리 코드는 쓰지 않는다. §0 때문에 텍스트 수정 불가), 메뉴 입력 추상화, 네트워크 직렬화, 리플레이. 모바일에서 `Keyboard.current` 가 null 일 때와 IMGUI 버튼 동작은 확인 못 했다. `ChainRushHud.cs:207` 버튼의 핸들러는 읽지 않았다.
- **발견 (다음 작업 후보, 이번 범위 밖)**: `RunnerMotor.Step` 이 `bodyVisual.localRotation` 을 직접 쓴다(표현이 시뮬 틱 안에 있음). 헤드리스 서버에는 시각물이 없고 아트 교체 때 시뮬 코드가 건드려진다. RacerState 분리(P1-3)에서 표현 컴포넌트로 빼는 것을 권고한다.

## 2026-10-02 · 고정 틱 (P1-1): 시뮬은 `ChainRushGame.FixedUpdate` 한 곳에서만 진행

- **결정**: ① **정수 틱.** `ChainRushGame.Tick`(int)이 시간이고 `Elapsed = Tick * 0.02f` 는 파생값. 쿨다운·무적·조우 타이머·그래플 빗나감 표시는 틱 마감 값으로 저장. ② **중앙 틱 구동.** `ChainRushGame.FixedUpdate` 만 진입점이고 순서를 코드로 고정(래치 입력 → `RunnerMotor.Step` → 접촉 → 완주 → `EnemyDirector.Step` → `EndlessCourse.Step`). 각 단계 뒤 `IsRunning` 을 다시 본다. ③ **공개 시그니처 유지.** `PrimaryAction`/`TryAttach`/`Release`/`Attack` 은 즉시 실행 그대로. 키보드·마우스 경로만 `Update` 에서 래치하고 다음 틱에 같은 메서드를 호출한다.
- **이유**: 서버·클라가 같은 입력으로 같은 결과를 내려면 시간과 상태 변경 순서가 프레임율에 독립이어야 한다(DESIGN C2). 이전에는 `elapsed`·적 타이머가 `Time.deltaTime`, 모터만 `fixedDeltaTime` 이었고 순서는 `DefaultExecutionOrder` 에 암묵적으로 기댔다.
- **초→틱 환산**: `Ticks.FromSeconds` 는 올림이며, float 나눗셈이 정확한 배수 바로 위로 떨어지는 경우를 막는 허용 오차(1e-3)를 둔다. 이 때문에 값이 최대 1틱(0.02s) 길어진다: 무적 1.25→1.26s(63틱), 공격 쿨다운 0.35→0.36s(18틱), 공격 시각 0.18s(9틱, 불변), 조우 비행 0.15→0.16s(8틱). `0.3→15틱`, `0.4→20틱`, `1.2→60틱` 은 불변. 경계값은 `TicksTests` 로 고정.
- **안전장치**: `Time.fixedDeltaTime` ≠ `Ticks.Seconds` 면 `ChainRushGame` Awake 가 LogError 후 비활성화(ProjectSettings 변경 금지라 값을 맞추지 않고 감지만 한다). `Ticks.FromSeconds` 는 음수·NaN·무한에 `ArgumentOutOfRangeException`.
- **추가된 공개 API**: `ChainRushGame.Tick`, `RunnerMotor.Step()`, `EnemyDirector.Step()`, `EndlessCourse.Step()`(각각 `FixedUpdate`/`Update`/`LateUpdate` 대체), `GrappleController.ClearMiss()`(틱이 매 판 0으로 돌아가므로 이전 판의 빗나감 마감 제거), `RunRules`·`EncounterTuning` 의 `*Ticks` 프로퍼티. 기존 시그니처 변경 없음.
- **제거**: `EndlessCourse` 의 `[DefaultExecutionOrder(100)]`(더 이상 `LateUpdate` 가 없다). `GrappleController` 의 `[DefaultExecutionOrder(150)]` 은 `LateUpdate` 가 남아 있어 그대로 둔다.
- **검증**: EditMode `testcasecount="22" result="Passed" total="22" passed="22" failed="0"` (00:52 KST). PlayMode `testcasecount="17" result="Passed" total="17" passed="17" failed="0" duration="94.8717957"` (00:54 KST), **기존 테스트 무수정**. Console error 0.
- **한계 (확인 못 한 것·안 한 것)**: 틱 밖 직접 호출(테스트나 외부 코드가 `PrimaryAction` 등을 부르는 경우)은 막지 않는다 — 입력 추상화(P1-2)에서 다룬다. `steer` 는 틱마다 샘플한 값이 아니라 `Update` 가 마지막으로 읽은 값이다. 적·발판이 50Hz 로만 갱신되고 카메라는 보간이 없어 화면 끊김이 생길 수 있으나 체감은 확인 못 했다. `CharacterController.Move` 의 재현성은 P2 에서 증명한다(미확인). 같은 입력 → 같은 결과를 보이는 결정성 테스트는 아직 없다.

## 2026-10-02 · 두 번째 SO `RunRules` 를 고정 틱보다 먼저 도입

- **결정**: `ChainRushGame` 의 체력(3), 피격 무적(1.25s), 공격 쿨다운(0.35s), 공격 시각 지속(0.18s)을 `RunRules` SO 로 이동. 형식은 위 `EncounterTuning` 결정을 그대로 따른다. 에셋 `Data/RunRules_Default.asset`, 값은 기존 코드와 동일. 공개 시그니처 불변.
- **이유**: 위 네 값 중 시간 값 셋은 고정 틱 전환(P1)에서 틱 수로 환산할 대상이다. 한 곳에 모아 두면 환산이 한 곳에서 끝난다. 고정 틱은 공개 API 4개(`PrimaryAction`, `TryAttach`, `Release`, `Attack`)와 PlayMode 테스트 3개 파일을 건드리므로 SO 이동과 한 변경에 섞지 않았다. 섞으면 실패 원인을 가를 수 없다.
- **연결**: `ChainRushGame.rules` 직렬화 참조. 비어 있으면 Awake 에서 LogError 후 비활성화. 신규 씬 생성 빌더(`ChainRushSceneBuilder`)는 에셋이 없으면 예외. 기존 씬 2개(`ChainRushPrototype`, `ChainRushEndless`)는 에디터에서 연결했다.
- **검증**: EditMode `testcasecount="4" result="Passed" total="4" passed="4" failed="0"` (2026-10-02 00:41 KST). PlayMode `testcasecount="17" result="Passed" total="17" passed="17" failed="0" duration="94.6350045"` (00:44 KST), 기존 테스트 무수정. Console error 0.
- **씬 변화**: `ChainRushPrototype.unity` 에 `rules` 외에 필드 7줄(`chainVisual`, `endlessMode`, `endlessCourse`, `enemies`, `enhancedPresentation`, `presentationAudio`, `presentationAnimation`)이 Unity 저장 시 기본값으로 추가됐다. 오래된 씬을 재직렬화한 결과이며 동작 변화는 없다(PlayMode 통과).
- **이번에 하지 않은 것**: 이동·그래플 수치(`RunnerMotor`, `GrappleController`), `CourseLayout`, `finishZ`. `CourseLayout` 은 시드 코스(P1)와 함께 한다.
- **조사 기록** (고정 틱 합의의 근거, 코드 변경 없음): 시뮬 상태가 틱 밖에서 바뀌는 곳은 `RunnerMotor.cs:44-58`(입력·점프·그래플·해제를 `Update` 에서 처리), `GrappleController.cs:95-110,124-135`(`TryAttach`/`Release` 가 `Update` 경로에서 상태·속도 변경), `ChainRushGame.cs:102-123`(`elapsed` 가 `Time.deltaTime`, 피격 판정과 공격 입력이 `Update`), `EnemyDirector.cs:47-107`(`timer` 가 `Time.deltaTime`), `EndlessCourse.cs:49-69`(`LateUpdate` 에서 재활용·원점 이동). 이동 적분만 이미 `FixedUpdate` 다(`RunnerMotor.cs:67-97`, 0.02s).

## 2026-10-02 · 브랜치 전략: main / dev / feature 3단

- **결정**: `main`(안정, 마일스톤 태그) ← `dev`(통합) ← `feature/<영역>-<내용>`. 병합은 **`--no-ff`**(기능 단위로 묶임). `main`·`dev` 직접 커밋 금지. 규칙 본문은 `CLAUDE.md` §9, 이 문서는 이유만 기록한다.
- **이유**: 지금까지 `main` 하나로만 작업했다(브랜치·태그·워크트리 없음, `git branch -a` 로 확인). 앞으로 P1 이 고정 틱·입력 추상화·레이서 상태 분리처럼 서로 얽힌 큰 변경이라 검증 전 코드가 기준선에 섞이면 안 된다. 씬 YAML 이 최대 30만 줄이라 병합 충돌 비용이 크다.
- ~~**병합 조건**: 컴파일 0 + EditMode + PlayMode 통과. CI 없이 로컬 검증.~~ → 2026-10-02 "병합 조건 강화" 항목으로 대체(PlayMode 연속 2회).
- **GitHub 보호 설정은 하지 않는다** (사용자 결정, 단독 개발). 저장소는 PUBLIC, 사용자는 ADMIN, `main` 은 현재 보호되지 않음(`Branch not protected`).
- **병합 드라이버는 등록하지 않는다**: `.gitattributes` 가 Unity YAML 에 `merge=unityyamlmerge` 를 지정하지만 git config 에는 드라이버가 없다. 임시 저장소 실험(같은 줄을 양쪽에서 수정, 드라이버 속성만 지정)에서 git 은 기본 텍스트 병합으로 되돌아가 `CONFLICT (content)` 와 `<<<<<<<` 마커를 남겼다. 즉 **조용히 망가지지 않고 시끄럽게 실패**한다(§5). 검증하지 않은 드라이버 설정이 오히려 조용한 오병합 위험이므로 등록하지 않는다. `UnityYAMLMerge.exe` 의 올바른 인자는 확인 못 했다. 씬·프리팹 충돌 때는 병합을 중단하고 에디터에서 다시 작업한다(`CLAUDE.md` §9-3).
- **에이전트**: `unity-implementer` 에 브랜치 관문 추가(`main`/`dev` 이면 거부, 파견 프롬프트의 `브랜치:` 줄과 대조). `CLAUDE.md` §9-2 와 같이 고쳤다.
- **버린 대안**: squash 병합(기능 1커밋으로 압축) — 기능 안의 검증 이력이 사라진다. 워크트리 병렬 작업 — Unity 프로젝트가 둘이 되어 `Library/` 를 따로 만들어야 한다.
- **원격**: `origin/dev` 를 `main`(`9f5a96f`) 에서 분기해 푸시했다. 기본 브랜치는 `main` 유지.

## 2026-10-02 · 첫 ScriptableObject 형식: `EncounterTuning`

- **결정**: SO 형식을 다음으로 정한다. ① `[CreateAssetMenu(menuName = "ProtoHarness/ChainRush/<이름>")]` ② 필드는 `[SerializeField] private` + 읽기 전용 프로퍼티 ③ 검증은 `OnValidate()` 에서 `Debug.LogError(msg, this)` ④ 순수 계산은 SO 의 메서드로 두어 EditMode 에서 테스트 ⑤ 사용처는 직렬화 참조 + 비어 있으면 LogError 후 비활성화(기본값 fallback 없음, §5) ⑥ 에셋 이름 `<타입명>_Default`, 위치 `Assets/_Project/Data/`.
- **적용**: `EnemyDirector` 의 조우 시간 5종과 간격 공식(2.5 / 0.6 / 1200)을 `EncounterTuning` 으로 이동. 공개 시그니처 불변. 에셋 값은 기존 씬 값과 동일.
- **EditMode 도입**: `ProtoHarness.Tests.EditMode` asmdef (키는 Test Framework 1.6.0 샘플 `EditModeTests_11.asmdef` 로 확인).
- **검증**: EditMode `<test-run testcasecount="3" result="Passed" total="3" passed="3" failed="0">` (2026-10-01 23:52 KST). 씬 연결 후 PlayMode `testcasecount="17" result="Passed" total="17" passed="17" failed="0" duration="93.5058803"` (2026-10-02 00:01 KST), 기존 테스트 무수정. Console error 0.
- **알아둘 점**: `ChainRushSceneBuilder` 의 `[InitializeOnLoad]` 콜백이 **모든 테스트 실행**(EditMode 포함)의 결과를 `ChainRush-PlayMode-results.xml` 한 파일에 덮어쓴다. 읽을 때 `testcasecount` 로 어떤 실행인지 구분한다.
- **씬 변화**: Unity 가 저장하며 `ChainRushEndless.unity` 의 고아 필드 5개(`warningDuration` 등)가 사라지고 `tuning` 참조 1줄이 추가됐다.
- **미해결**: `ProjectSettings/ProjectSettings.asset` 에 Unity 6000.3.25f1 이 iOS 열 상태 필드 3개를 자동 추가했다 (§0 대상, 커밋 여부는 사용자 결정).

## 2026-10-01 · 최종 목표는 서버 권위 공유 월드 레이스(C)다

- **결정**: 최종 목표를 카트라이더식 서버 권위 공유 월드 레이스로 한다. 서로 앞지르고 방해하는 요소를 포함한다. 순서는 무한 모드 → SO → 오프라인 결정성(시드·고정 틱·입력 추상화·레이서 상태 분리·리플레이) → 서버 권위 레이스를 최소 형태부터. 상세는 `docs/DESIGN.md`.
- **이유**: 사용자가 방해 요소를 필수로 확정했다. 상호작용이 있으면 클라가 각자 계산하는 방식(B)은 불가능하다.
- **버린 대안**: B(각자 월드 병렬 레이스)를 중간 산출물로 만들기 — 계산 위치가 C 와 반대라 네트워크 부분을 버리게 된다. A(비동기 고스트)는 선택 사항으로 남긴다.
- **확정 (같은 날 사용자)**: 모드는 유한 트랙(기록·점수 경쟁)과 무한(생존) 두 가지. 최대 8인. 상호작용은 아이템과 접촉, 그래플은 카트라이더 자석처럼 아이템으로. 플랫폼 PC + 모바일.
- **서버**: 전용 서버 + 서버 권위 방향을 추천(조사 결과와 근거 등급은 `DESIGN.md` §7). **네트워크 라이브러리는 미선정** — P1 에서 시뮬 코어를 네트워크 비의존으로 만들고, C1 직전 NGO vs Photon Fusion 2 스파이크로 확정. manifest 변경은 별도 승인.
- **모드 확정 (같은 날, 쿠키런 + 카트라이더 참고)**: ① 무한(쿠키런식, 생존 + 점수 먹기) ② 속도전(카트라이더식, 랩타임) ③ 아이템전(카트라이더식, 아이템 경쟁이 주). 앞의 "유한 트랙 / 무한" 2분류를 대체한다.
- **그래플 확정**: 기본(환경) 그래플은 현행 유지. **다른 유저를 대상으로 하는 그래플은 아이템으로만** 가능(카트라이더 자석식).
- **~~미결~~ 2026-10-02 위임 처리**: 무한 모드의 멀티 여부, 순환 트랙(랩), 속도전의 접촉, 아이템 종류, 점수 규칙은 사용자가 "판단하고 추천하는 대로"를 위임해 **권고 기본값**으로 정했다 (싱글+랭킹 / 순환 트랙 신설 / 접촉 ON / 아이템 3종 / 거리+아이템+처치 점수). 요구가 아니라 기본값이므로 뒤집을 수 있다. 근거는 `DESIGN.md` §8.

## 2026-10-01 · 에디터 6000.3.25f1 / Input System 1.20.0 변경을 유지한다

- **결정**: Unity가 자동 반영한 `ProjectVersion.txt`(6000.3.18f1 → 6000.3.25f1), `manifest.json`(inputsystem 1.20.0, collab-proxy 2.13.6, navigation 2.0.14, timeline 1.8.13, visualscripting 1.9.12), `packages-lock.json` 변경을 되돌리지 않고 유지한다. `CLAUDE.md` §4-2, `ARCHITECTURE.md` §3 의 버전 행을 맞췄다.
- **이유**: `Unity_RunCommand` 로 읽은 `Application.unityVersion` 이 `6000.3.25f1` 이고 컴파일 중 아님, Console error 0건. 되돌려도 에디터가 다시 올릴 가능성이 있다. 이 변경은 사용자 승인 하에 반영한다 (§0 금지선 대상 파일).
- **검증**: 6000.3.25f1 / Input System 1.20.0 에서 PlayMode 재실행. XML `<test-run testcasecount="17" result="Passed" total="17" passed="17" failed="0" ... duration="93.2862676">` (2026-10-01 23:21:30 KST), Unity Console `logs: [], errorCount: 0`.
- **함께 커밋**: `Assets/Settings/Mobile_RPAsset.asset` — URP 에셋 자동 마이그레이션(`k_AssetVersion` 12→13, Prefilter/ReflectionProbeAtlas 필드 추가). Unity가 쓴 결과이며 텍스트로 편집하지 않았다. `PC_RPAsset.asset` 은 이미 `k_AssetVersion: 13` 이며 변경 없음 (grep 확인), 즉 Mobile 쪽만 뒤처져 있다가 이번에 따라잡았다.
- **버린 대안**: `git checkout --` 로 되돌리기 — 금지 명령이며 에디터가 다시 바꿀 수 있다.

## 2026-10-01 · `_Project/` 루트와 asmdef 구성을 현 상태로 확정한다

- **결정**: `Assets/_Project/` 루트 채택. asmdef 는 현재 3개 — `ProtoHarness.Runtime`, `ProtoHarness.Editor`, `ProtoHarness.Tests.PlayMode`. `ProtoHarness.Tests.EditMode` 는 아직 없다 (EditMode 테스트가 없으므로 만들지 않음).
- **이유**: 코드·씬·머티리얼 17개 `.cs` 가 이미 이 구조 위에 있다 (`Assets/_Project/Scripts/*/ProtoHarness.*.asmdef`). 미결로 두면 문서와 현실이 어긋난다.
- **버린 대안**: 평평한 `Assets/Scripts/`, 4분할 — 이미 만든 구조를 뒤집을 이점이 확인되지 않았다.

---

## 2026-08-25 · 규칙 사본을 없애고 단일 진실 원천으로 묶는다

- **결정**: `CLAUDE.md` 를 유일한 규칙 원천으로 삼는다. `AGENTS.md` 는 규칙 사본이 아니라 **포인터**(+ Codex 전용 델타)로 줄인다. `.codex/agents/*.toml` 은 `.claude/agents/*.md` 에서 `tools/sync-agents.ps1` 로 **생성**한다.
- **이유**: 사본을 만든 지 **3분 만에** 갈라졌다. §3-1 개정과 §3-4 신설이 `CLAUDE.md` 에만 반영되고 `AGENTS.md` 에는 빠졌으며, 기계적 치환이 `.Codex/agents/*.md`(실제는 `.codex/agents/*.toml`) 같은 사실 오류까지 만들었다. §7-1이 경고한 "한쪽만 고치면 조용히 어긋난다" 가 즉시 실현됐다.
- **효과**: 규칙 변경 시 손대는 파일이 **6개 → 1개**.
- **버린 대안**: 수동 동기화 + 리뷰 체크리스트 — 사람이 6개를 매번 맞추는 방식은 이미 실패가 증명됐다.

## 2026-08-25 · unity-verifier 의 도구 권한을 구조로 강제한다

- **결정**: MCP 도구 이름이 확정되어(`mcp__unity-mcp__...` 7개) `unity-verifier` 의 `tools:` 를 읽기 전용 + MCP 4개로 좁혔다. `Write`/`Edit` 없음, `Unity_AssetGeneration_GenerateAsset` **의도적 제외**.
- **이유**: 프롬프트로만 "쓰지 마라"고 하면 샌다. 도구 목록에서 빼면 호출 자체가 불가능하다. 생성형 에셋 도구는 비용이 발생하므로 §0의 "명시적 요청 시에만" 을 구조로 보장한다.
- **한계**: 이 강제는 **Claude Code 에만** 적용된다. Codex TOML 에서 권한을 제한하는 키를 확인하지 못해 주석으로만 남겼다. → `AGENTS.md`

## 2026-08-25 · 합의 조건을 "개수"에서 "행위"로 바꾼다

- **결정**: §3-1을 파일 개수 기준에서 **행위 기준**으로 재작성하고, §3-4 우회 금지를 신설한다. 에이전트 정의(`unity-implementer`, `unity-architect`)도 같이 고쳤다.
- **이유**: 기존 조건("파일 3개 이상")에 구멍이 4개 있었다.
  1. 신규 파일 1~2개는 무조건 통과 — §2가 "첫 사례가 곧 표준"이라 한 상황에서 가장 위험했다.
  2. **이동·이름변경 조항이 아예 없었다.** `.meta` 없이 옮기면 GUID가 새로 발급되어 씬·프리팹 참조가 조용히 끊긴다. §5가 막으려는 바로 그 실패인데 규칙에 없었다.
  3. 비공개 API 수정은 통과했다.
  4. `Assets/` 바깥(`docs/`, `tools/`, `.claude/`, `.gitignore`)이 어느 조항에도 안 걸렸다 — **규칙 자체를 합의 없이 고칠 수 있었다.**
- **우회 경로도 막았다**: 서브에이전트 파견 프롬프트에 `승인:` 줄이 없으면 에이전트가 생성을 거부한다. `Unity_RunCommand` 로 파일을 쓰는 것은 직접 생성과 동일 취급. "1개씩 3번"은 "3개"로 센다.
- **버린 대안**: 개수 임계를 3에서 1로 낮추기 — 오타 수정까지 합의 대상이 되어 규칙이 무시당한다. 행위 기준이 예외를 정확히 좁힌다.

## 2026-08-25 · [위반 기록] .gitignore 를 사전 합의 없이 수정함

- **무슨 일**: `index/` 를 무시하도록 `.gitignore` 에 2줄을 추가했다. 사후 보고는 했으나 **사전 합의는 없었다.**
- **왜 위반인가**: 그때 받은 승인은 *구축 범위 / C모드 / 에이전트 수* 3건이었고 리포 설정 변경은 그 범위 밖이었다. §3-3 "승인은 그 계획 그 범위에만 유효하다" 위반.
- **처리**: 개정된 §3-1의 "환경 기준"에 `.gitignore` 를 명시해 같은 구멍을 막았다. **되돌릴지 유지할지는 미결.**

## 2026-08-25 · 모드 4종(A/B/C/D)과 서브에이전트 5종 도입

- **결정**: 조회 경로를 A(문서)/B(심볼 인덱스)/C(그래프)/D(하이브리드)로 나누고, 라우터 규칙을 `CLAUDE.md` §7에 둔다. C는 **미구현 보류**.
- **이유**: 토큰 싱크는 ① `.cs` 전면 스윕 ② 씬·프리팹 YAML 통독이다. A/B가 ①을 없앤다. ②는 모드와 무관하게 "YAML 통독 금지, GUID grep만" 규칙으로 막는다.
- **C 보류 사유**: 정밀(Cecil IL 순회)은 도구 제작이 별건이고, 근사(ripgrep 역참조)는 호출 방향·오버로드를 구분 못 해 "호출 그래프"라 부를 수 없다. 리팩터링이 실제로 무서워지는 시점에 재논의.
- **버린 대안**: 처음부터 C1(Cecil) 구축 — 코드 0줄인 지금 투자 대비 효용 없음.

## 2026-08-25 · 빈 인덱스는 사용을 거부한다

- **결정**: `index/symbols.tsv` 헤더에 `git-head`·파일 수·생성 시각을 박고, 조회 전 현재 HEAD와 대조한다. 불일치·빈 인덱스면 **쓰지 않고 시끄럽게 알린다.**
- **이유**: 낡거나 빈 인덱스는 "인덱스에 없음 → 존재하지 않음"이라는 **자신 있는 오답**을 만든다. 인덱스가 아예 없는 것보다 나쁘다. → `CLAUDE.md` §5
- **버린 대안**: 인덱스 자동 갱신 훅 — Unity 임포트와 경합할 수 있어 보류. 수동 재생성이 명시적이라 더 안전.

## 2026-08-25 · `index/` 는 커밋하지 않는다

- **결정**: 생성물이므로 `.gitignore` 에 넣는다. 재생성은 `tools/reindex.ps1` 한 방.
- **이유**: 커밋하면 머지 충돌이 나고, 낡은 인덱스가 리포에 박제된다.
- **버린 대안**: 커밋 후 HEAD 대조로 방어 — 방어가 되더라도 충돌 비용이 남음.

## 2026-08-25 · Unity MCP는 공식 패키지 경로(stdio relay)를 쓴다

- **결정**: `com.unity.ai.assistant` 의 Bridge → `relay_win.exe --mcp` → Claude Code(stdio). 유저 스코프 등록.
- **이유**: 실측으로 체인 전체 확인됨(named pipe 연결 + 도구 7개). 프로젝트 스코프는 Unity가 슬래시 경로 키를 쓰고 Claude Code가 역슬래시 키를 써서 어긋날 위험이 있다.
- **버린 대안**: 커뮤니티판 `http://127.0.0.1:8080/mcp` — 해당 포트에 리스너 없음. 다른 두 프로젝트의 등록은 죽은 설정.

## 2026-08-25 · 도구·문서는 `Assets/` 바깥에 둔다

- **결정**: `docs/`, `index/`, `tools/`, `.claude/` 는 리포 루트 직속.
- **이유**: `Assets/` 안의 `.cs` 는 Unity가 컴파일하고 `.meta` 를 찍는다. 인덱서 스크립트가 게임 어셈블리에 섞이면 안 된다.
- **버린 대안**: `Assets/Editor/Tools/` — 컴파일 대상이 되어 부적합.

---

## 미결 (합의 대기)

| 항목 | 선택지 | 상태 |
|---|---|---|
| ~~`Assets/_Project/` 루트 채택~~ | 2026-10-01 확정 (위 결정 참조) | 해결 |
| ~~asmdef 분할~~ | 2026-10-01 현 3개 구성 확정 (위 결정 참조) | 해결 |
| ~~접두사 규칙~~ | 2026-10-01 `CLAUDE.md` §1-3 확정: 머티리얼 `M_`, 텍스처 `T_..._BaseColor` 만. `Art/Materials/` 15개가 이미 준수 | 해결 |
| ~~`Assets/Editor/HubForceResolve.cs` 삭제~~ | 2026-10-01 의도된 삭제로 판단: Unity Hub 가 주입하는 자기삭제 헬퍼(파일 내 `k_ScriptPath`). 삭제는 커밋 `75d3454`(DosangE)에서 수행, 참조 없음 | 해결 |
| ~~`dev/` (git-lfs 훅 잔재) 제거~~ | 2026-10-01 제거 (git 추적 훅 4개, 참조 없음 확인 후 `git rm`) | 해결 |
| ~~`.gitignore` 의 `/index/` 줄~~ | 2026-10-01 유지 확정 (생성물 미커밋 결정과 일치). "다 승인" 지시를 유지로 해석했으므로 뒤집으려면 알려 달라 | 해결 |
