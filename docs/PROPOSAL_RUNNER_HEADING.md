# 수정 제안서: 러너 본체가 `Heading` 방향을 바라보게 하기

> **상태: B안 변형으로 구현됨 (브랜치 `fix/runner-heading-visual`, 미병합).** 2026-10-10 작성, 같은 날 갱신.
> 권장했던 A안은 결정성을 깨서 버렸다. 아래 "결과" 절을 먼저 본다. 아래 본문(증상~위험)은 처음 제안 그대로다.
> **플레이해서 확인하지 않았다.**

## 결과 (2026-10-10)

- **A안 실패**: 루트를 `LateUpdate` 에서 돌리자 PlayMode 게이트에서 결정성 테스트 3개가 `posY` 1 ULP 로 갈라졌다
  (`Replay_CircuitBotRun_RepeatsEveryTickBitForBit` 샘플 718, `Replay_ProceduralCourseRun_RepeatsCourseAndEncountersBitForBit` 샘플 313,
  `StepTick_ByHand_IsTheSameRunAsFixedUpdate` 샘플 719, `0x3F70A3D6 vs 0x3F70A3D5`). 루트 회전이 물리 계산에 들어가고, 프레임마다 돌리면
  틱과 프레임의 맞물림에 따라 값이 달라진다. PhysX 안의 정확한 지점은 확인하지 못했다. 아래 "확인한 사실"의 "루트를 yaw 로 돌려도 안전한 근거"는 틀렸다.
  "루트 회전은 항상 identity" 도 틀렸다: 서킷 빌더가 루트를 돌려 둔다(`ChainRushCircuitSceneBuilder.cs:109`).
- **B안 변형 채택** (사용자 승인 2026-10-10 "B안 변형으로 진행해줘"): `RunnerTilt` 가 시각물 `Runner Visual` 에만 월드 회전
  `Euler(0, Heading, 0) * Evaluate(...)` 을 준다. 루트는 돌리지 않는다. 시각물 프리미티브는 콜라이더가 꺼져 있어(`ChainRushSceneBuilder.cs:343`) 물리에 닿지 않는다.
  기존 테스트 `Tilt_ScriptedSteerLeft_LeansBodyFromMotorState` 의 기대값을 "헤딩 yaw × 기울기" 로 바꿨다(승인된 사양 변경).
  새 테스트 `Tilt_ScriptedSteerLeft_TurnsBodyToFaceHeading` 는 조향 뒤·±180° 감김 뒤 몸 방향과 루트 불변을 본다(수정 전 31.2° 로 실패 확인).
- **검증** (에디터 6000.3.25f1): EditMode `testcasecount="237" passed="237" failed="0"`. PlayMode 게이트(Device·Sweep 제외) 2회 모두
  `testcasecount="100" result="Passed" passed="98" failed="0" skipped="2"` (13:29:10Z–13:38:19Z, 13:38:40Z–13:47:43Z). 같은 브랜치의 A안 코드 실행 1회는 `failed="3"` 이었다(코드는 버림).
- **남은 것**: 플레이로 몸 방향 확인(사용자). `Strike Arc` 는 루트 자식이라 계속 월드 +z 앞에 뜬다(별도 합의).

## 증상

달리기로 커브길을 돌면 바라보는 방향이 달리는 방향과 다르다.

## 원인

러너 본체(캐릭터 메시)가 `Heading` 으로 회전하지 않는다.

- 시뮬레이션은 바라보는 방향을 `RacerState.Heading`(월드 yaw 각도)으로만 들고 있고, `RunnerMotor.cs:167-168` 에서 입력으로 갱신한다. 이동은 `velocity = facing * …` (`RunnerMotor.cs:198`).
- `Heading` 을 읽는 곳은 `FollowCamera.cs:43` 과 모터 내부뿐이다. 러너 오브젝트의 회전에 쓰는 곳은 `Assets/_Project/Scripts/Runtime` 어디에도 없다 (`.rotation`, `eulerAngles`, `LookAt` grep).
- 러너 쪽 시각 스크립트 `RunnerTilt.cs:23-26` 은 `Runner Visual` 의 `localRotation` 에 기울기만 준다 (`velocity.y * -0.9` pitch, `steer * 12` yaw, `steer * -16` roll). 조향 입력에 따른 ±12° 흔들림이 전부다.
- 그래서 러너는 이동 방향과 상관없이 처음 회전(대개 +Z)을 유지한 채 미끄러지듯 움직인다.

한계: 러너 루트에 이 외에 회전을 주는 컴포넌트가 씬에 붙어 있을 가능성은 배제하지 못했다 (씬 YAML 은 §6-4 에 따라 통독하지 않았다).

### 함께 있는 다른 두 요인 (이번 범위 아님)

- **자유 조향은 설계상 동작이다.** 커브는 플레이어가 직접 꺾어야 돈다 (`docs/COURSE.md` 4-3, `docs/DECISIONS.md` 2026-10-05 "코스 T2a"). 키를 안 누르면 `Heading` 은 직진이다. 사양이므로 바꾸려면 설계 결정부터 해야 한다.
- **카메라가 헤딩을 지연해서 따라간다.** `FollowCamera.cs:43` yaw 는 `maxYawSpeed = 180°/s` 로 제한되고, `:49-50` 회전은 Slerp 다. 급커브에서 화면 기준으로 어긋나 보일 수 있다. 영향은 위 원인보다 작다.

## 확인한 사실 (수정 영향 범위)

- 씬 빌더 `ChainRushSceneBuilder.cs:125-138` 기준 구조:
  - 루트 `Runner` 에 `CharacterController`, `RunnerMotor`, `GrappleController`, `RunnerTilt` 가 붙는다.
  - 모델은 자식 `Runner Visual`. `Chain Origin`(손)은 `Runner Visual` 의 자식, `Strike Arc`(local z=1.6)는 루트의 자식.
  - 루트 회전을 바꾸는 코드는 없어서 항상 identity.
- `RunnerTilt` 는 이미 `LateUpdate` 에서 모터 상태를 읽어 시각물에 반영한다.
- 루트를 yaw 로 돌려도 안전한 근거:
  - 캡슐 `center = 0` (`ChainRushSceneBuilder.cs:130`) 이라 yaw 회전이 캡슐에 영향이 없다.
  - `SnapToGround`(`RunnerMotor.cs:294`)의 `TransformPoint(controller.center)` 도 영향이 없다.
  - `Runtime`·`Tests`·`Editor` 어디에도 `player.transform.forward/right/rotation` 을 읽는 코드가 없다.
  - 카메라는 `target.Heading` 을 직접 읽는다.
- 확인하지 못한 것: `Strike Arc` 를 쓰는 코드, 서킷 레이스(`RaceWorld`)의 다른 러너에도 `RunnerTilt` 가 붙는지.

## 제안 (권장: A안)

### A안: `RunnerTilt` 가 루트도 `Heading` 으로 돌린다

`RunnerTilt.LateUpdate` 에 한 줄을 더한다.

```csharp
private void LateUpdate()
{
    transform.rotation = Quaternion.Euler(0f, motor.Heading, 0f);   // 추가
    body.localRotation = Evaluate(motor.Velocity, motor.Steer);
}
```

- `Evaluate` 시그니처와 `body.localRotation` 값이 그대로라서 기존 테스트 `RunnerTiltTests`(EditMode 4개)와 `Tilt_ScriptedSteerLeft_…`(`ChainRushInputSourceTests.cs:157-181`)는 수정하지 않는다.
- 씬·프리팹 수정이 없다. `RunnerTilt` 는 이미 모든 러너에 붙어 있다.
- 손(`Chain Origin`)과 `Strike Arc` 가 몸과 함께 돌아 체인 발사 위치와 공격 링도 바라보는 방향을 따른다 (부수 효과, 의도에 맞다고 봄).
- 클래스 헤더 주석("leans the body")은 "faces the heading and leans" 로 한 줄 갱신한다.

### B안: `Runner Visual` 의 `localRotation` 에만 yaw 를 곱한다

- `Evaluate` 나 `LateUpdate` 가 yaw 를 포함하게 된다. `Tilt_…` 테스트가 `Evaluate` 결과와 `body.localRotation` 을 직접 비교하므로 헤딩이 0 이 아닐 때 그 테스트를 고쳐야 한다.
- `Strike Arc` 는 계속 world +z 를 향한다.
- 비권장.

## 합의 요청 (§3-2)

```
목표:      러너 본체가 Heading 방향을 바라보게 한다 (A안)
건드릴 것:  Runtime/ChainRush/Visuals/RunnerTilt.cs:23 — 한 줄 추가 + 헤더 주석 갱신
           Tests/PlayMode/ChainRushInputSourceTests.cs — 테스트 추가 1개 (기존 테스트는 수정하지 않음)
새로 만들 것: 없음
검증 방법:  아래 "검증"
안 하는 것:  자유 조향 사양 변경, 모터·시뮬레이션·스냅샷 변경, 카메라 maxYawSpeed·Slerp 조정,
            씬·프리팹 편집, RemoteGhost 수정
```

기존 파일 2개 수정, 새 파일 없음, 공개 API 변경 없음 (§3-1 규모 기준 미만).

## 검증

1. 추가할 PlayMode 테스트: 일정 시간 조향한 뒤 `Vector3.Angle(player.transform.forward, player.Facing)` 가 0.5° 미만인지 확인한다. 헤딩이 ±180° 를 넘어 감기는 경우도 본다.
2. 컴파일 에러 0, EditMode 전체, PlayMode(`Device`·`Sweep` 제외) 연속 2회. 방법은 `docs/RULES/VERIFICATION.md` 를 먼저 읽고 따른다. 병합 보고에는 각 실행의 XML 값을 모두 적는다 (§9-2).
3. 에디터에서 직접 플레이해 커브에서 몸 방향을 눈으로 확인한다 (사용자 확인 필요).

## 브랜치

`dev` 에서 `fix/runner-heading-visual` 로 분기한다 (§9-1). 병합은 `--no-ff`, 병합·푸시는 사용자가 시킬 때만 (§9-2).

## 위험과 한계

- `Heading` 은 틱 단위로만 바뀌므로, 렌더 프레임이 틱보다 잦으면 몸 회전이 계단처럼 보일 수 있다. **확인하지 못했다.** 보이면 `MoveTowardsAngle` 보간을 따로 제안한다. 이번에는 `RunnerTilt` 의 기존 방식("틱 값을 그대로 쓴다")과 맞춰 보간 없이 간다.
- 드리프트 중에는 몸이 속도 방향이 아닌 `Heading` 을 본다. 속도와 몸 방향이 갈라지는 것은 드리프트 연출로 의도된 동작이라고 판단했다.
- 원격 고스트(`RemoteGhost.cs:41`)는 이미 이동 벡터를 바라보므로 이번 범위가 아니다.
