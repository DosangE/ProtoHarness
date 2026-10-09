# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-09 (T3b 구현·검증). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-09)

- 브랜치: T3b 요청서는 `dev` 에 병합됐다(`d2c0084`, **로컬만, 푸시 안 함**). T3b 구현은 `feature/course-t3b-generator` 에 있고 **`dev` 병합 대기**다(병합 조건 검증은 끝남, DECISIONS 2026-10-09).
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`: 직선 + 수평 원호 + 종단 곡선, 절대 거리 `S`(double), 원점 이동.
  - 이동: 자유 조향(헤딩), 그립 한계, 경사 속도 보정, 내리막 땅 붙잡기, 가드 부딪힘.
  - 카트라이더식: 드리프트(Shift) → 체인 게이지(최대 2칸) → 체인 액션(Ctrl): 그래플 강화 / 코너 스윙 / 슬링샷.
  - 씬: 두 씬 발판 양쪽에 보이는 가드 난간(옆 낙사 없음, 틈 낙사는 유지).
  - T3a 절차 노면 (DECISIONS 2026-10-07): `RoadProfile` · `RoadMeshBuilder` · `RoadPiece`.
  - **T3b 시드 생성기** (DECISIONS 2026-10-09): `SeedHash` · `ModuleKind` · `CourseModule` · `CourseTuning`(SO, `.asset` 없음) · `CourseGenerator`. 연결 규칙 R1~R8, 앞보기 300m, 벽 없는 구간.
- **씬 코스는 아직 직선·평지다.** 절차 노면과 생성기는 테스트에서만 쓴다. 씬 연결은 T3c.
- 테스트: EditMode 152, PlayMode 46(그중 Device 2). 병합 조건 실행은 Device 제외 44건, 약 240초/회.

## 2. 다음 작업: T3c — 무한 모드 전환 (합의 요청서 작성 전)

T3 목표(COURSE 10절): 무한 모드가 시드로 만든 커브·경사 코스를 달린다. 완료 기준은 **시드 20개에서 봇이 1400m 완주(PlayMode)**. 씬을 건드리므로 §3-1 합의 대상이고, 씬·프리팹 브랜치는 동시에 1개만(§9-2).

### 2-1. 시작 전에

1. `feature/course-t3b-generator` 가 `dev` 에 병합됐는지 확인한다. 안 됐으면 사용자에게 먼저 묻는다(병합·푸시는 지시가 있을 때만).
2. `dev` 를 푸시할지 사용자에게 묻는다(`d2c0084` 부터 로컬만).
3. 요청서를 §3-2 형식으로 올린다. 아래 2-2 는 요청서에 넣을 재료다.

### 2-2. T3b 가 T3c 에 넘기는 것

- **`CourseTuning_Default.asset`**: `Assets/_Project/Data/` 에 에디터로 만든다(첫 SO 형식 ⑥). 지금은 코드 기본값만 있다.
- **좌표 맞추기**: 생성기는 원점·+Z·평지에서 시작한다(`EndX/EndZ/EndYaw/EndHeight`, double). 씬 `Centerline` 의 시작점·yaw 를 같게 두고 `CourseModule.AppendTo` 로 붙인다(선 끝 `S`·경사가 다르면 예외). 첫 모듈은 스폰 쉼터 60m.
- **노면**: 틈 모듈은 조각 3개(도움닫기 15 · 틈 · 착지 15)라 `RoadPiece` 를 틈 앞뒤로 나눠 깔면 된다(`GapStart`, `GapLength`). 열린 가장자리(`OpenLeft/OpenRight`)는 `RoadProfile` 의 가드 플래그로 넘긴다.
- **앵커**: `AnchorAlong`(모듈 시작부터), `AnchorHeight`(노면 위 10m), `AnchorSide`(오른쪽 +). 오브젝트 풀과 `GrappleController.anchors`(지금 직렬화 고정 배열) 연결 방식을 정해야 한다.
- **쉼터 = 적 조우 자리**: 20 시드 × 10km 에서 모듈 약 3개 중 1개가 쉼터다(R3 이 긴 모듈 자리에 쉼터를 낸다). 조우 빈도가 지금과 달라지므로 `EnemyDirector` 의 `CanStartEncounter` 와 함께 본다.
- **미리 생성 거리**: COURSE 7-1 의 앞쪽 300m 는 생성기 앞보기 300m 와 같다. 생성기는 상태를 갖고 앞으로만 간다(`Next()`), 되돌리지 않는다.
- **알아둘 점**: 생성기는 탈출 직선 위에서도 겹치면 `InvalidOperationException` 을 던진다. 5000 시드 × 10km 에서 0회였지만 튜닝을 바꾸면 다시 잰다(DECISIONS 2026-10-09 표).

### 2-3. T3c 에서 다룰 기존 부채

- `Centerline` 조각 선택이 "앞에서부터 첫 번째로 지나지 않은 조각"이다. 코스가 자기에게 가까이 돌아오면 틀릴 수 있다 → T3b 의 R7(샘플 거리 20.6m)·앞보기로 줄었다. 실제 문제가 나면 직전 `S` 근처 탐색.
- 무한 모드 원점 이동 조건이 "앞 방향 투영 ≥ 448m" → 위치 벡터 기준(COURSE 7-1).
- `CourseTarget` 접촉·공격 판정이 월드 x/z 박스(`CourseTarget.cs:42-55`), 적 회전이 월드 축 → 트랙 프레임 기준(COURSE 8절).
- 피스 선택·투영은 틱마다 조각을 앞에서부터 훑는다 → 모듈 수가 늘면 비용을 잰다.

## 3. 보류된 사용자 결정

| # | 주제 | 상태 | 참고 |
|---|---|---|---|
| 1 | 병합 조건 검증 줄이기 | 사용자가 질문함("작업한 부분만 검증하면 안 되나"). 제시안: A 그대로 / **B 추천: 전체 1회 + 2회차는 바뀐 영역 클래스만** / C 영향 범위만 / D 1400m 장거리 테스트 배속 3 → 5. **답 대기.** T3a 는 A(전체 2회)로 검증했다. 실행 1회가 약 240초로 늘었다. 바꾸면 `CLAUDE.md` §9-2 + DECISIONS, `docs/` 브랜치, 문서 검증(§9-2) | 2026-10-05 대화 |
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

- `Unity_RunCommand` 는 `System.Reflection` 네임스페이스를 쓰면 실행을 거부하고(`unauthorized namespaces`), `System.Diagnostics.Stopwatch` 는 참조가 없어 컴파일되지 않는다. 시간은 `Time.realtimeSinceStartupAsDouble`, 새 코드 로드 확인은 공개 타입·메서드 이름 조회나 `Assembly.Location` 기록 시각으로 한다(2026-10-09).
- 이 머신(`C:/Users/User/Desktop/PCUBE/ProtoHarness`)의 에디터는 **6000.3.19f1** 이다(결정 버전 25f1 아님). 그래서 `ProjectVersion.txt`·`packages-lock.json`·`ProjectSettings.asset`(iOS 발열 설정 3줄 삭제)이 수정으로 보인다. 로컬 환경 차이라 커밋하지 않는다(사용자 2026-10-04). 검증 보고에는 19f1 에서 돌렸다고 적는다.
- 테스트 결과 XML 은 Unity 의 `Path.GetTempPath()/ChainRush-PlayMode-results.xml` 하나에 덮어쓴다(EditMode 실행도 같은 파일). 이 머신은 `C:\Users\User\AppData\Local\Temp\` (2026-10-07 확인). 실행마다 따로 보관하려면 끝날 때 복사한다.
- Unity MCP: 도메인 리로드마다 브리지가 몇 초 끊긴다(`Unity not detected (no fresh discovery files found)`). `~/.unity/mcp/connections/bridge-*.json` 이 다시 생기면 재시도한다. 에디터가 백그라운드면 스크립트를 자동 임포트하지 않을 수 있다 → `AssetDatabase.Refresh()`. 테스트 전에 새 코드가 로드됐는지(리플렉션 등) 확인한다.
- `.codex/agents/` 가 `git status` 에 수정으로 보이면 `autocrlf` 표시다(내용은 HEAD 와 같음). 손대지 않는다.
- `index/symbols.tsv` 는 낡았다(git-head `9f5a96f`). B모드로 쓰기 전에 재생성한다(§6-2).
- GitHub Desktop 이 켜져 있으면 `.git/index.lock` 이 남을 수 있다. 실행 중인 `git.exe` 가 없을 때만 지운다(2026-10-05 한 번 발생).
- 씬 저장을 `Unity_RunCommand` 로 하려면 매번 승인받는다(§0, §3-3). T2a 의 승인은 1회성이었다.
- 도메인 리로드 때 `Deleting invalid font reference.` 경고가 나온다(P1-3a 부터, 원인 미확인). 변경과 무관.
