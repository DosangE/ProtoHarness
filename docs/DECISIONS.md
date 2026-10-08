# 결정 로그

> **A모드.** "왜 이렇게 했나"는 코드를 읽어도 안 나온다. 여기에만 있다.
> 새 결정은 **맨 위에** 추가한다. 뒤집힌 결정은 지우지 말고 `~~취소선~~` + 사유를 남긴다.

형식: `날짜 · 결정 · 이유 · 검토했으나 버린 대안`

---

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
