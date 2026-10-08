# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-08 (T3b 구현·검증 완료, 다음은 T3c). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-08)

- 브랜치: T3b 는 `feature/course-t3b-generator` 에서 구현·검증을 마쳤다(병합·푸시는 사용자가 시킬 때). `dev` 에는 T3a 와 T3b 합의 요청서(`docs/t3b-proposal`)가 병합돼 있다.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`: 직선 + 수평 원호 + 종단 곡선, 절대 거리 `S`(double), 원점 이동.
  - 이동: 자유 조향(헤딩), 그립 한계, 경사 속도 보정, 내리막 땅 붙잡기, 가드 부딪힘.
  - 카트라이더식: 드리프트(Shift) → 체인 게이지(최대 2칸) → 체인 액션(Ctrl): 그래플 강화 / 코너 스윙 / 슬링샷.
  - 씬: 두 씬 발판 양쪽에 보이는 가드 난간(옆 낙사 없음, 틈 낙사는 유지).
  - **T3a 절차 노면** (DECISIONS 2026-10-07): `RoadProfile` · `RoadMeshBuilder` · `RoadPiece`.
  - **T3b 모듈 카탈로그 + 시드 생성기** (DECISIONS 2026-10-08): `SeedHash` · `ModuleKind` · `CourseModule` · `CourseTuning`(SO, `.asset` 없음) · `CourseGenerator`. 순수 로직이고 씬·`EndlessCourse` 와 연결돼 있지 않다.
- **씬 코스는 아직 직선·평지다.** 절차 노면은 `ChainRushRoadTests`(x = 300)에서만, 생성기는 EditMode 테스트에서만 쓴다. 씬 연결은 T3c.
- 테스트: EditMode 161, PlayMode 46(그중 Device 2). 병합 조건 실행은 Device 제외 44건, 약 225~240초/회.

## 2. 다음 작업: T3c — 무한 모드 전환 (합의 요청서부터)

COURSE 10절 T3c: `EndlessCourse` 가 `CourseGenerator` + `RoadPiece` 풀을 스트리밍하고, 원점 이동을 위치 벡터 기준으로, `CourseTarget`·적 회전을 트랙 프레임으로. 완료 기준: 시드 20개 봇 1400m 완주(PlayMode). **구현 전에 §3-2 합의 요청서를 올린다.** 씬·프리팹을 건드리므로 동시에 1개 브랜치만(§9-2).

T3b 가 T3c 에 넘기는 것:
- `CourseModule.AppendTo(Centerline)` 로 중심선에 붙인다. 앵커·틈은 트랙 좌표(`AnchorS`/`AnchorOffset`/`AnchorHeight`, `GapStartS`/`GapLength`)라서 `Centerline.FrameAt` 로 월드 위치를 구한다. `GrappleController.anchors`(직렬화 고정 배열)와 앵커 오브젝트 풀 연결은 T3c 몫이다.
- 가드 없는 가장자리는 `CourseModule.LeftOpen`/`RightOpen` → `RoadProfile` 의 가드 없음으로 옮긴다.
- 생성기는 0 부터 순서대로만 만든다(`Next()`). 스트리밍은 앞쪽 약 300m 를 미리 `Next()` 로 받아 둔다. `CourseTuning` `.asset`(`Assets/_Project/Data/CourseTuning_Default.asset`)은 T3c 에서 에디터로 만든다.
- 그래플 틈은 새 코스에서 훨씬 드물다(모듈 가중치 2/17~2/20). 틈 값(16m, 앵커 +10m)은 씬 통과 값이고, 14~18m·좌우 ±2m 는 **봇으로 아직 검증하지 않았다**(T3c 봇 완주에서 확인). COURSE 6-2 의 "앵커 +3~+8m" 는 실측 뒤에 고친다.
- 막다른 길: 생성기는 한 걸음 앞(R9)까지만 본다. 탈출구가 이어 나오면 `InvalidOperationException` 이 날 수 있다(20 시드 × 10km 에서는 0건, 탈출구 9회). 스트리밍은 이 예외를 잡지 말고 크게 깨지게 둔다(§5).

### 2-1. 기존 부채 (T3c 에서 다룬다)

- `Centerline` 조각 선택이 "앞에서부터 첫 번째로 지나지 않은 조각"이다. 코스가 자기에게 가까이 돌아오면 틀릴 수 있다. 생성기 R7 이 코스 간 거리를 중심선 20.6m(상자 기준) 이상으로 막지만, 직전 `S` 근처 탐색은 T3c 에서 실제로 문제가 나는지 보고 정한다.
- 무한 모드 원점 이동 조건이 "앞 방향 투영 ≥ 448m" → 위치 벡터 기준(COURSE 7-1).
- `CourseTarget` 접촉·공격 판정이 월드 x/z 박스(`CourseTarget.cs:42-55`), 적 회전이 월드 축 → 트랙 프레임 기준(COURSE 8절).
- 피스 선택·투영은 틱마다 조각을 앞에서부터 훑는다 → 모듈이 늘면 비용을 잰다.

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

- 이 머신(`C:/Users/User/Desktop/PCUBE/ProtoHarness`)의 에디터는 **6000.3.19f1** 이다(결정 버전 25f1 아님). 그래서 `ProjectVersion.txt`·`packages-lock.json`·`ProjectSettings.asset`(iOS 발열 설정 3줄 삭제)이 수정으로 보인다. 로컬 환경 차이라 커밋하지 않는다(사용자 2026-10-04). 검증 보고에는 19f1 에서 돌렸다고 적는다.
- 테스트 결과 XML 은 Unity 의 `Path.GetTempPath()/ChainRush-PlayMode-results.xml` 하나에 덮어쓴다(EditMode 실행도 같은 파일). 에디터의 `Path.GetTempPath()` 는 이 머신에서 `C:\Users\Public\Documents\ESTsoft\CreatorTemp\` 이다(2026-10-08 Editor.log 의 `ChainRush tests: ... XML=` 줄로 확인. 셸의 `%TEMP%` 와 다르다). 경로는 Editor.log 에서 `ChainRush tests:` 를 찾으면 나온다. EditMode 는 MCP `Unity_RunCommand` 로 `TestRunnerApi.Execute(new Filter { testMode = TestMode.EditMode })`, PlayMode 는 `EditorApplication.ExecuteMenuItem("ProtoHarness/Chain Rush/Run PlayMode Tests")` 로 시작했다. 실행마다 따로 보관하려면 끝날 때 복사한다.
- Unity MCP: 도메인 리로드마다 브리지가 몇 초 끊긴다(`Unity not detected (no fresh discovery files found)`). `~/.unity/mcp/connections/bridge-*.json` 이 다시 생기면 재시도한다. 에디터가 백그라운드면 스크립트를 자동 임포트하지 않을 수 있다 → `AssetDatabase.Refresh()`. 테스트 전에 새 코드가 로드됐는지(리플렉션 등) 확인한다.
- `.codex/agents/` 가 `git status` 에 수정으로 보이면 `autocrlf` 표시다(내용은 HEAD 와 같음). 손대지 않는다.
- `index/symbols.tsv` 는 낡았다(git-head `9f5a96f`). B모드로 쓰기 전에 재생성한다(§6-2).
- GitHub Desktop 이 켜져 있으면 `.git/index.lock` 이 남을 수 있다. 실행 중인 `git.exe` 가 없을 때만 지운다(2026-10-05 한 번 발생).
- 씬 저장을 `Unity_RunCommand` 로 하려면 매번 승인받는다(§0, §3-3). T2a 의 승인은 1회성이었다.
- 도메인 리로드 때 `Deleting invalid font reference.` 경고가 나온다(P1-3a 부터, 원인 미확인). 변경과 무관.
