# 설계 제안서: 맵 정리 — 구 머리 맵(Prototype)을 걷어내고 "기능은 한 곳에서 한 번" 테스트한다

> **상태: 설계 초안, 승인 대기.** 2026-10-14 작성. 씬·코드·테스트는 아직 건드리지 않았다.
> 출발점: 2026-10-14 사용자 "플레이어 머리가 구 모양으로 된 씬은 필요 없는 씬이다", "씬 디자인이 다른 맵이 있고 그것도 중복해서 테스트하는 느낌이 든다."
> 짝 문서: `docs/PROPOSAL_TEST_TIERS.md`(테스트 3단). 이 문서는 **어떤 테스트를 어느 맵에 둘지**를, 그 문서는 **언제 무엇을 돌릴지**를 다룬다.

## 1. 지금 맵 3개 (사실)

| 씬 | 코스 | 캐릭터 | `endlessMode` / `enhancedPresentation` | 게임 모드에 대응 |
|---|---|---|---|---|
| `ChainRushPrototype` | 직선 유한, 발판 + 틈, 결승선 | **구 모양 머리** — `"Helmet"` Sphere (`Editor/ChainRush/ChainRushSceneBuilder.cs:140`) | 0 / 0 | **없음.** 맨 처음 이식본 |
| `ChainRushProcedural` | 시드 절차 무한 코스(커브·경사·틈), 적 | 장갑 러너 | 1 / 1 | 무한 모드 |
| `ChainRushCircuit` | 스타디움 서킷 531 m, 3랩, 노면 반폭 6 m (`Data/Circuit_Stadium.asset:102`) | 장갑 러너 | 0 / 1 | 속도전·아이템전, 네트워크 레이스 |

- `CourseTarget` 컴포넌트는 세 씬 모두에 15개씩 있다(스크립트 GUID `0f45bbe4…` grep). 씬마다 켜져 있는지는 확인하지 못했다. **그래서 Prototype 을 없애도 공격 판정 방향 문제(HANDOFF 2절)는 남는다.**
- **다른 씬은 Prototype 에 기대지 않는다.** 씬 복사 사슬(Prototype → Endless → Procedural)은 T3d 에서 이미 끊겼고(`docs/DECISIONS.md` 2026-10-08 "코스 T3d"), `Circuit` 빌더의 원본은 `ChainRushProcedural` 이다(`ChainRushCircuitSceneBuilder.cs:22`).
- `ChainRushSceneBuilder.cs` 는 Prototype 생성·열기·가드 메뉴(`:27`, `:206`, `:218`)와 함께 **테스트 실행 메뉴**(`Run PlayMode Tests` `:270`, `Run Device Input Tests` `:274`, `Run Course Sweep` `:278`)도 갖고 있다. 파일을 통째로 지우면 안 된다.

## 2. Prototype 은 지금 "테스트 작업대" 다

PlayMode 테스트 파일 8개가 이 씬을 불러온다. 대부분은 이 씬의 코스를 쓰지 않는다. 코드로 시험 도로(`TestRoad`)를 깔고, 씬은 **플레이어·카메라·HUD 를 담는 그릇**으로만 쓴다.

| 테스트 파일 | 이 씬에서 쓰는 것 |
|---|---|
| `ChainRushCornerSwingTests`, `ChainRushDriftTests`, `ChainRushCurveTests`, `ChainRushSlopeTests` | `TestRoad` (그릇만) |
| `ChainRushRoadTests` | 절차 노면 조각 (그릇만) |
| `ChainRushSteeringTests` | 발판 가드 + 절차 노면 가드 (**두 맵을 둘 다** 본다) |
| `ChainRushInputSourceTests` | 그릇 + 표적(`Attack_ScriptedPressNearTarget_HitsExactlyOnce`) |
| `ChainRushTests` | 그릇 + 발판·틈·앵커·결승선·표적 |

## 3. 원칙: 기능은 한 곳에서 한 번

| 무엇을 테스트하나 | 어디서 |
|---|---|
| 이동·조향·드리프트·스윙·경사·가드·입력·카운트다운·일시정지 같은 **기능** | **테스트 작업대 한 곳**(시험 도로를 코드로 깐다) |
| 무한 맵에만 있는 것: 스트리밍, 원점 이동, 적 조우, 시드 생성 | `ChainRushProcedural` |
| 서킷에만 있는 것: 랩·체크포인트, 낙하 복귀, 순위, 네트워크 레이스 | `ChainRushCircuit` |

같은 기능을 두 맵에서 확인하는 테스트는 하나로 줄인다. 지금 눈에 띄는 것:

- `Guards_PrototypeDecksAndProceduralRoad_AreGuarded` — 가드를 발판과 절차 노면 둘 다에서 본다. 발판(Prototype 전용 구조)이 없어지므로 절차 노면 쪽만 남는다.
- 결정성(리플레이·롤백)을 서킷과 절차 코스 양쪽에서 본다. 이것은 **중복이 아니다**. 절차 코스 쪽은 스트리밍·원점 이동의 결정성이라 다른 것을 본다. 지우지 않고 `PROPOSAL_TEST_TIERS.md` 5절대로 마일스톤 단(③)으로 내린다.

## 4. 테스트 작업대를 어떻게 만들까

| 안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **A. 서킷 씬 재사용** | 시험 도로 테스트가 서킷을 불러와 `SetTrack` 으로 바꿔 끼운다 | 씬이 2개로 준다 | 서킷의 `CircuitRace` 가 매 틱 자기 트랙을 돌린다(`ARCHITECTURE.md` 서킷 줄). 시험 도로와 부딪는지 **확인 못 함**. 서킷을 고치면 이동 테스트 전부가 흔들린다 |
| **B. Prototype 을 작업대로 바꾸기 (권장)** | 이름을 `ChainRushTestBench` 로 바꾸고 에디터에서 발판·앵커·결승선·표적을 걷어낸다. 플레이 대상 메뉴에서 뺀다 | 이미 8개 파일이 이 씬으로 돈다. 바뀌는 것은 씬 경로 문자열과 코스를 쓰던 몇 건뿐. 플레이용 맵은 Procedural·Circuit 두 개만 남는다 | 구 머리 캐릭터가 작업대에 남는다(플레이 대상이 아니라 문제는 아니다). 이름 변경은 `.meta` 와 같이 에디터에서 해야 한다(§0, §3-1) |
| C. 씬 없이 코드로 조립 | 테스트가 빈 씬에 플레이어·카메라를 직접 만든다 | 씬 파일 0 | 씬 빌더의 연결 로직을 테스트로 옮겨야 한다. 비용이 가장 크다 |

**B 를 권한다.** 사용자 눈에는 "맵 2개(무한·서킷) + 숨은 작업대 1개" 가 된다.

## 5. 테스트 옮기기 표 (초안)

테스트 **이름**만 보고 정한 초안이다. 구현할 때 각 테스트 본문을 읽고 확정한다. "작업대" 는 B 안의 `ChainRushTestBench`.

| 테스트 | 지금 | 옮길 곳 | 이유 |
|---|---|---|---|
| `ChainRushCornerSwingTests`, `ChainRushDriftTests`, `ChainRushCurveTests`, `ChainRushSlopeTests`, `ChainRushRoadTests` 전부 | Prototype | 작업대 | 시험 도로만 쓴다. 씬 경로만 바뀐다 |
| `ChainRushInputSourceTests` 중 표적 1건 외 | Prototype | 작업대 | 그릇만 쓴다 |
| `Attack_ScriptedPressNearTarget_HitsExactlyOnce` | Prototype | 작업대(시험 표적을 코드로 놓기) 또는 `ChainRushCombatTests` 와 합치기 | 공격 판정 방향을 정할 때(HANDOFF 2절) 같이 정한다 |
| `ChainRushTests`: `StartRun_Ready…`, `Input_*`, `StartCountdown_*`, `Pause_*` | Prototype | 작업대 | 그릇만 쓴다 |
| `ChainRushTests`: `Grapple_JumpAtGap…`, `Grapple_NoAnchorInRange…` | Prototype 발판·앵커 | 작업대(틈·앵커가 있는 시험 도로) | 그래플 기능 테스트 |
| `ChainRushTests`: `Damage_RepeatedContact…` | Prototype | `ChainRushCombatTests`(Procedural) 와 겹치는지 보고 합치거나 작업대 | 확인 필요 |
| `ChainRushTests`: `Restart_AfterAttackAndFall…` | Prototype 표적 + 낙사 | Procedural (낙사 = 런 종료가 남는 맵) | 서킷은 낙하 복귀로 바뀌는 중(`feature/circuit-fall-respawn`) |
| `ChainRushTests`: `Course_TimedJumpsAndHeldGrapple_ReachesFinish` | Prototype 결승선 | **지운다** | 직선 결승은 Prototype 에만 있다. 완주는 서킷 3랩 테스트(`ChainRushCircuitTests`)가 본다 |
| `ChainRushSteeringTests`: `Guards_PrototypeDecksAndProceduralRoad…` | 두 맵 | 절차 노면 쪽만 남긴다 | 3절 |

## 6. 같이 정리할 것

- **메뉴**: `Create Prototype Scene`(Ctrl+Shift+G), `Open Prototype Scene`, `Add Deck Guards To Open Scene` 을 지우거나 작업대용으로 바꾼다. 테스트 실행 메뉴 3개는 남긴다.
- **README 갱신**: README "시작하기" 의 `Create Endless Scene`, `Apply Art Sound Animation` 메뉴는 T3d 에서 이미 지워진 도구다(DECISIONS 2026-10-08 "코스 T3d"). 이 정리 때 같이 고친다.
- **ARCHITECTURE·HANDOFF** 의 씬 목록.

## 7. 순서 (각 단계 §3-2 합의, 씬을 건드리는 브랜치는 동시에 1개, §9-2)

1. 테스트 측정(`PROPOSAL_TEST_TIERS.md` 6-1) — 옮기기 전 기준 시간
2. 작업대 씬 만들기(B: 이름 변경 + 코스 걷기, 에디터에서) + 씬 경로를 바꾼 테스트를 옮기기 — **테스트 내용은 바꾸지 않는다**. 지문(서킷 리플레이 해시)이 그대로여야 한다
3. 지울 테스트·합칠 테스트 — 사양 변경이므로 하나씩 이유를 DECISIONS 에 남긴다
4. 메뉴·문서 정리

## 8. 안 하는 것

- Procedural·Circuit 씬의 디자인 변경
- 공격 판정 방향 결정 (별도 항목)
- 테스트 3단 규칙 (`PROPOSAL_TEST_TIERS.md`)

## 9. 확인 못 한 것

- 각 씬에서 `CourseTarget` 이 켜져 있는지
- A 안에서 `CircuitRace` 와 `SetTrack` 이 부딪는지
- 옮기기 표의 각 테스트가 실제로 씬의 어느 오브젝트에 기대는지 (이름 기준 초안)
- 옮긴 뒤 줄어드는 시간 (1단계 측정 뒤에 계산)
