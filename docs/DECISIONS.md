# 결정 로그

> **A모드.** "왜 이렇게 했나"는 코드를 읽어도 안 나온다. 여기에만 있다.
> 새 결정은 **맨 위에** 추가한다. 뒤집힌 결정은 지우지 말고 `~~취소선~~` + 사유를 남긴다.

형식: `날짜 · 결정 · 이유 · 검토했으나 버린 대안`

---

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
