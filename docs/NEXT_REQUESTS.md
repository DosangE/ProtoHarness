# 다음 작업 요청서 묶음

> 2026-10-14 작성. 각 항목은 `CLAUDE.md` §3-2 형식의 **합의 요청서**다. **이 문서는 구현 승인이 아니다.** 승인은 항목 번호로 따로 받는다(예: "R1 ㄱㄱ"). 승인은 그 항목 그 범위에만 유효하다(§3-3).
> 시간은 이 머신(6000.3.19f1)의 2026-10-14 실측: EditMode 12초, 게이트 `Run PlayMode Tests` 약 430초, `Run Long Tests` 약 198초, `Net` 카테고리 약 30초, 20 시드 스윕 약 933초, 네트워크 레이스 1개 약 90초(+ 플레이어 빌드).
> 진행하면 끝난 항목을 지우고 `docs/DECISIONS.md` 로 옮긴다. 다음 세션 인계는 여전히 `docs/HANDOFF.md` 가 원본이고, 거기서 이 문서를 가리킨다.

## 0. 한눈에

| # | 작업 | 브랜치 | Unity 검증 | 선행 | 사용자 결정 |
|---|---|---|---|---|---|
| R1 | 프레임 시간 의존 테스트 안정화 | `fix/test-frame-timing` | 약 11분 | — | — |
| R2 | 서킷 낙하 복귀 마무리 (WIP) | `feature/circuit-fall-respawn` | 약 20분 | R1 권장 | — |
| R3 | 맵 정리 3단계 (작업대 비우기) | `feature/scene-test-bench-strip` | 약 11분 | R7 (표적 1건) | 3-1 |
| R4 | 코너 봉 스윙 P1 (시뮬) | `feature/corner-pole-sim` | 약 30분 이상 | **R2** (같은 파일) | — |
| R5 | 코너 봉 스윙 P2 (표현) | `feature/corner-pole-look` | 약 11분 + 사용자 플레이 | R4 | 5-1 |
| R6 | 코너 봉 스윙 P3 (절차 코스) | `feature/corner-pole-procedural` | 약 27분 (스윕 포함) | R5 | 6-1 |
| R7 | 공격 판정 방향 | `fix/attack-facing` | 약 11분 | — | **7-1 (설계)** |
| R8 | 기존 착지 확인 테스트 수정 | `fix/test-landing-check` | 약 11분 | — | — |

**권장 순서**: R1 → R2 → R4 → R5 → R6. R7 은 설계 결정이 나는 대로, R3 은 R7 뒤(표적 테스트가 걸려 있다), R8 은 아무 때나. **씬을 건드리는 R3·R5 는 동시에 하지 않는다**(§9-2).

---

## R1. 프레임 시간 의존 테스트 안정화

```
목표:      한 번에 통과해야 하는 병합 게이트(②)가 간헐 실패로 막히지 않게, 프레임 시간에 기대는 대기를 틱 기준으로 바꾼다
건드릴 것:  Tests/PlayMode/ChainRushCombatTests.cs:103-107 (Combat_EachDirection_AttackFiresConnectsAndRetracts:
             WaitForSeconds(0.07f) 뒤 Firing 단언 — 비행 8틱보다 긴 프레임 하나면 Retracting)
           그 밖에 같은 형태의 대기 (구현 때 grep 으로 목록을 만들어 보고)
새로 만들 것: 없음 (필요하면 테스트 보조에 "N 틱 기다리기" — 기존 WaitForFixedUpdate 반복으로 충분한지 먼저 본다)
검증 방법:  고친 클래스를 ①로 3회 연속(같은 결과), 게이트 1회 + Long (테스트 보조 파일을 바꾸면 "전체")
안 하는 것:  단언 값 바꾸기 (§0 "테스트를 통과시키려고 고치지 않는다" — 기다리는 기준만 바꾼다)
           Course_TimedJumpsAndHeldGrapple_ReachesFinish (R3 에서 지운다)
```

- 근거: `feature/circuit-fall-respawn` 의 게이트 3회 중 2회가 서로 다른 테스트로 실패했고 둘 다 프레임 시간 의존이었다(그 브랜치 DECISIONS 의 "실패의 판단"). `dev` 코드로 돈 오늘 게이트 3회는 모두 통과했다.

## R2. 서킷 낙하 복귀 마무리 (`feature/circuit-fall-respawn`, WIP `086ae19`)

```
목표:      2026-10-14 에 구현하고 검증만 남긴 "서킷 낙하 → 4 m 뒤 복귀 + 1초 정지" 를 새 병합 조건으로 검증해 병합한다
건드릴 것:  그 브랜치에 dev 를 병합 (그 브랜치는 옛 dev 75f3b43 에서 갈라졌다. `git merge-tree` 시험: 코드 충돌 없음,
           docs/DECISIONS.md·docs/HANDOFF.md 만 충돌 — 둘 다 맨 위에 항목을 더해서다. 양쪽 내용을 모두 살려 손으로 푼다)
           그 브랜치의 DECISIONS·HANDOFF 기록을 새 규칙(②: 1회 + Long)에 맞게
새로 만들 것: 없음
검증 방법:  EditMode, 게이트 1회 + Long (RunnerMotor = 코어 → "전체"), Net 카테고리,
           SimSnapshotCodec 와이어 형식이 바뀌었으므로 플레이어 재빌드 + 네트워크 레이스 A·B·C (§9-2 ③ 항목이지만 와이어 변경이라 여기서 돌린다)
           지문 해시 그대로여야 한다 (그 브랜치 기록: 0xF9A914DA3FAA680B, 0xE81912109A2EB020 불변)
안 하는 것:  복귀 규칙 변경 (사용자 결정: 서킷만, 4 m, 1초)
```

- 알려진 위험: 그 브랜치의 네트워크 레이스는 에디터 프레임이 수백 ms 멈출 때 예측기가 고장 났다(HANDOFF 4절의 "서버가 클라를 앞지름"). 복귀로 런이 길어져 더 자주 드러난다. **다시 실패하면 원문을 보고하고 병합하지 않는다** — 그때 예측기 쪽을 별도 요청서로.
- R1 을 먼저 하면 게이트 실패가 이 변경 때문인지 가르기 쉽다.

## R3. 맵 정리 3단계: 테스트 작업대 비우기

```
목표:      ChainRushTestBench 에서 직선 코스 전용 구조(결승선·표적 등)를 걷어내고, 그 구조에 기대던 테스트를 지우거나 옮긴다
건드릴 것:  씬 ChainRushTestBench (에디터에서), Editor/ChainRush/ChainRushSceneBuilder.cs (작업대 생성 내용, 메뉴 Add Deck Guards To Open Scene)
           Tests/PlayMode/ChainRushTests.cs, ChainRushSteeringTests.cs, ChainRushInputSourceTests.cs (각 테스트 본문을 읽고 아래 표 확정)
새로 만들 것: 없음 (그래플 틈 테스트에 필요하면 TestRoad 에 틈·앵커 — 그때 §1-1 선언)
검증 방법:  게이트 1회 + Long, 지문 그대로, 지우는 테스트마다 이유를 DECISIONS 에
안 하는 것:  Procedural·Circuit 씬, 구 머리 캐릭터(작업대라 그대로 둔다)
```

`docs/PROPOSAL_SCENE_CLEANUP.md` 5절 초안(이름 기준):

| 테스트 | 처리 |
|---|---|
| `Course_TimedJumpsAndHeldGrapple_ReachesFinish` | 지운다 — 직선 결승은 이 씬에만 있고 완주는 서킷 3랩이 본다. 프레임 시간 의존 간헐 실패의 하나 |
| `Guards_PrototypeDecksAndProceduralRoad_AreGuarded` | 절차 노면 쪽만 남긴다 |
| `Restart_AfterAttackAndFall…` | Procedural 로 옮긴다 (낙하 = 종료가 남는 맵) |
| `Damage_RepeatedContact…` | `ChainRushCombatTests` 와 겹치는지 보고 합치거나 남긴다 |
| `Grapple_JumpAtGap…`, `Grapple_NoAnchorInRange…` | 발판·앵커를 남길지, 시험 도로에 틈·앵커를 코드로 깔지 |
| `Attack_ScriptedPressNearTarget_HitsExactlyOnce` | **R7 결정 뒤** |

**3-1 사용자 결정**: 그래플 틈 테스트를 위해 작업대에 발판·앵커 한 구간을 남길까(간단), 시험 도로에 틈·앵커를 코드로 만들까(작업대가 완전히 비지만 테스트 보조가 커진다)?

## R4. 코너 봉 스윙 P1 — 시뮬

```
목표:      docs/PROPOSAL_CORNER_POLE.md 2절 규칙으로 지금 코너 스윙을 대체한다 (표현 제외)
건드릴 것:  제안서 3절 표 — RunnerMotor, Centerline(봉 조회), RacerState, TickInput·TickInputCodec(Ctrl 유지 비트),
           InputLogFile(버전 2), KeyboardMouseInputSource·InputLatch, SimSnapshotCodec, ChainRushHud(문구), docs/COURSE.md 4-5
           테스트: ChainRushCornerSwingTests 다시 쓰기, ChainRushRollbackTests 스윙 테스트, TickInput·InputLog EditMode
새로 만들 것: 없음 예상 (봉 조회·범위 판정 순수 함수는 Centerline·RunnerMotor 안. 따로 빼야 하면 그때 §1-1)
검증 방법:  EditMode, 게이트 1회 + Long, Net, Device (장치 입력 경로), 플레이어 재빌드 + 네트워크 레이스 A·B·C,
           **지문 해시 그대로**(봇은 체인 액션을 안 쓴다 — 바뀌면 원인을 찾을 때까지 병합하지 않는다)
안 하는 것:  봉 모델·줄 끝 표현(R5), 절차 코스 봉 표시(R6), 난이도 조절(P4)
```

- 선행: **R2 를 먼저 병합**한다. 둘 다 `RunnerMotor`·`RacerState`·`SimSnapshotCodec` 을 바꾼다.
- 입력 로그 버전 1 파일을 계속 읽을지: 저장된 `.phil` 파일이 있는지 구현 때 찾아 보고 정한다.

## R5. 코너 봉 스윙 P2 — 표현

```
목표:      봉이 보이고, 줄이 봉에 걸리고, 걸 수 있는 봉이 표시된다
건드릴 것:  GrappleController(줄 끝 = 봉 꼭대기), Visuals/SwingLook(RailPoint 제거 또는 대체), ChainRushHud,
           봉 모델 배치 (5-1 에 따라 씬 또는 런타임)
새로 만들 것: 봉 모델 (5-1 에 따라 프리팹 또는 런타임 생성 컴포넌트 — 그때 §1-1 선언)
검증 방법:  게이트 1회 + Long, 지문 그대로, **사용자가 서킷에서 직접 플레이**
안 하는 것:  불꽃·잔상 같은 추가 연출 (플레이 뒤 따로)
```

**5-1 사용자 결정**: 봉을 ① 씬 빌더로 씬에 굽기(씬 수정 브랜치, 코스마다 다시 굽기) ② 실행 중에 `Centerline` 의 원호에서 만들기(씬 수정 없음, 절차 코스와 같은 경로 — **권장**)?

## R6. 코너 봉 스윙 P3 — 절차 코스

```
목표:      절차 코스의 커브에도 봉이 보이고 스트리밍·원점 이동을 따라간다
건드릴 것:  ProceduralCourse·CourseStream (봉 표시 생성·재활용), 필요하면 CourseGenerator
새로 만들 것: 없음 예상
검증 방법:  게이트 1회 + Long, 20 시드 스윕 1회 (§9-2: 코스 생성·스트리밍을 바꾸면)
안 하는 것:  난이도 조절
```

**6-1 사용자 결정**: 완만한 커브(R 60~120)에도 봉을 둘까? 줄이 60~120 m 가 된다.

## R7. 공격 판정 방향 (설계 결정 필요)

```
목표:      공격 판정이 월드 +z 를 보는 문제(HANDOFF 2절)를 고쳐, 자유 조향·커브에서 보는 쪽과 맞는 쪽을 같게 한다
건드릴 것:  Runtime/ChainRush/CourseTarget.cs:53-54 (TryHit), 공격 링 Strike Arc (ChainRushSceneBuilder.cs:159-161 — 루트 자식),
           관련 테스트 (Attack_ScriptedPressNearTarget…, ChainRushCombatTests)
새로 만들 것: 없음
검증 방법:  게이트 1회 + Long (CourseTarget 은 표에 없는 경로 → 전체), 절차 지문이 바뀌는지 확인 — CourseBot 이 공격하므로 바뀔 수 있다.
           바뀌면 사양 변경으로 새 해시를 기록한다
안 하는 것:  적 조우 규칙
```

**7-1 사용자 결정**: 판정 앞쪽을 ① 몸 방향(`Heading`, 자유 조향과 맞음) ② 트랙 진행 방향(커브에서도 앞 표적을 맞힘) 중 무엇으로?

## R8. 기존 PlayMode 테스트의 착지 확인 수정 (HANDOFF 3절 #4)

```
목표:      StartRun 직후 IsGrounded 가 순간이동 전 값을 돌려줘 "착지" 확인이 노면 없이도 통과하는 구멍을 막는다
건드릴 것:  StartOn 류 보조를 쓰는 테스트들 (ChainRushCurveTests 등 — 구현 때 grep 으로 목록)
새로 만들 것: 없음 (새 테스트가 이미 쓰는 "고정 틱 1회 뒤 확인" 형식을 따른다)
검증 방법:  게이트 1회 + Long
안 하는 것:  단언 값 변경
```

---

## 요청서를 아직 쓰지 않은 후보 (설계·측정이 먼저)

- **P3 C1 마무리 측정**: 3인 이상, 100 ms 체감, 패킷 유실, 모바일/IL2CPP 일치.
- **P3 C2 충돌·추월**: 공유 월드 선행.
- **P2 고스트**: 최고 기록 저장, 겨루기, UI.
- **T3e 장식, T5 보충 모듈** (헤어핀 전용 짧은 봉은 여기 — `PROPOSAL_CORNER_POLE.md` 8절).
- **하네스**: SessionStart `resume`, 서브에이전트 안의 훅, C모드 재논의.
- **사용자 결정 대기**: 헤딩 범위 제한(HANDOFF 3절 #3), 물리 값 체감 튜닝(#2).
- **`dev → main` (v0.2)**: 마일스톤 조건(③: 게이트 2회 + Long + Sweep + Device + 네트워크 레이스, 약 45분). 봉 스윙(R4~R5) 뒤가 자연스럽다.
