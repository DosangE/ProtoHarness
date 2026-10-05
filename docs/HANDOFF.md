# 다음 작업 지시서 (인계)

> 작성 2026-10-05. 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-05 마감)

- 브랜치: `dev` = `origin/dev` = `9def280`. 로컬 기능 브랜치 없음(병합 후 삭제).
- 오늘 병합: 코스 T0 → T1 → T2a → T2b → T2c → fix(기울기 테스트) → T2d. 각 결정·검증 값은 DECISIONS 2026-10-05 항목들.
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`: 직선 + 수평 원호 + 종단 곡선, 절대 거리 `S`(double), 원점 이동.
  - 이동: 자유 조향(헤딩), 그립 한계, 경사 속도 보정, 내리막 땅 붙잡기, 가드 부딪힘.
  - 카트라이더식: 드리프트(Shift) → 체인 게이지(최대 2칸) → 체인 액션(Ctrl): 그래플 강화 / 코너 스윙 / 슬링샷.
  - 씬: 두 씬 발판 양쪽에 보이는 가드 난간(옆 낙사 없음, 틈 낙사는 유지).
- **씬 코스는 아직 직선·평지다.** 커브·경사·드리프트 스윙은 테스트 도로(`Tests/PlayMode/TestRoad.cs`, x = 300)에서만 검증했다.
- 테스트: EditMode 117, PlayMode 43(그중 Device 2). 병합 조건 실행은 Device 제외 41건, 약 188초/회.

## 2. 다음 작업: T3 — 곡선 노면 + 모듈 + 시드 생성 (합의 전)

목표(COURSE 10절 T3): 무한 모드가 시드로 만든 커브·경사 코스를 달린다. 완료 기준(안): 시드 20개에서 봇이 1400m 완주, 같은 시드는 같은 모듈 순서(EditMode).

### 2-1. 나누는 안 (T2 처럼 단계마다 합의·병합)

| 단계 | 내용 | 근거 |
|---|---|---|
| **T3a 절차 노면** | 중심선 + 단면(폭 12m, 가장자리 = 가드 / 없음)으로 노면 메시·콜라이더와 가드를 뽑아낸다. 조각(모듈) 단위로 만들고 풀로 재사용. 시각 뱅크 포함 여부 결정 | COURSE 3-2, 4-4, 5 |
| **T3b 모듈 카탈로그 + 시드 생성기** | 5-1 기본 모듈, `SeedHash`(SplitMix64), 6-5 연결 규칙, 난이도 곡선. 순수 로직 + EditMode(같은 시드 = 같은 순서, 규칙 위반 없음) | COURSE 5-1, 6-5, 7-1 |
| **T3c 무한 모드 전환** | `EndlessCourse` 가 생성기 + 절차 노면을 스트리밍. 원점 이동을 플레이어 위치 벡터 기준으로. 봇 완주 테스트(시드 20개) | COURSE 7-1, 아래 2-3 |

### 2-2. 합의 때 사용자에게 물을 것

1. 기존 무한 씬의 `Pooled Sector` 발판(프로토타입 `Sector 02` 복제)을 절차 노면으로 **대체**할지, 공존시킬지. 대체하면 씬 수정 + 저장(§0, 저장은 매번 승인).
2. "벽 없는 구간"(낙사 허용, 사용자 2026-10-05)을 T3 에서 넣을지. 넣으면 단면의 가장자리 종류와 생성 규칙이 필요.
3. 시각 뱅크(커브 노면 기울기 연출)를 T3a 에 넣을지.
4. 노면 아트: 절차 메시에 쓸 재질(기존 `M_Deck` 등 재사용 vs 새 재질 = 에셋 생성).

### 2-3. T3 전에 반드시 다룰 기존 부채 (DECISIONS·COURSE 에 기록됨)

- `Centerline` 조각 선택이 "앞에서부터 첫 번째로 지나지 않은 조각"이다. 코스가 자기 쪽으로 가깝게 돌아오면(머리핀) 틀릴 수 있다 → 직전 `S` 근처 탐색으로 바꾸거나 생성기의 자기 교차 방지로 막는다(COURSE 6-5).
- 무한 모드 원점 이동 조건이 "앞 방향 투영 ≥ 448m" 다 → 커브가 들어가면 위치 벡터 기준(COURSE 7-1).
- `CourseTarget` 접촉·공격 판정이 월드 x/z 박스(`CourseTarget.cs:42-55`), 적 회전이 월드 축(`EnemyDirector.cs` 의 `Quaternion.Euler(0f, 0f, …)`) → 트랙 프레임 기준(COURSE 8절).
- 피스 선택·투영은 틱마다 조각을 앞에서부터 훑는다. 모듈 수가 늘면 비용을 재고 필요하면 힌트 기반으로.

### 2-4. 시작 순서

1. `docs/RULES/CONVENTIONS.md`, `VERIFICATION.md`, `BRANCHING.md` 를 읽는다(CLAUDE.md 표).
2. `git status` 로 `dev` 인지 확인하고 `feature/course-t3a-…` 브랜치를 만든다(§9-2).
3. COURSE 3-2·5·6-5·7-1·8 과 이 문서 2-2·2-3 으로 T3a 합의 요청서(§3-2)를 올린다. 구현은 승인 뒤.

## 3. 보류된 사용자 결정

| # | 주제 | 상태 | 참고 |
|---|---|---|---|
| 1 | 병합 조건 검증 줄이기 | 사용자가 질문함("작업한 부분만 검증하면 안 되나"). 제시안: A 그대로 / **B 추천: 전체 1회 + 2회차는 바뀐 영역 클래스만** / C 영향 범위만 / D 1400m 장거리 테스트 배속 3 → 5. **답 대기.** 바꾸면 `CLAUDE.md` §9-2 + DECISIONS, `docs/` 브랜치, 문서 검증(§9-2) | 2026-10-05 대화 |
| 2 | 물리 값 체감 튜닝 | 전부 계산으로 정한 시작값. 사용자 플레이 피드백 대기 | 아래 표 |
| 3 | 헤딩 범위 제한 | 계속 꺾으면 뒤로도 돈다(가드에 닿으면 앞으로 돌려짐). 막을지 미정 | DECISIONS T2a |

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

- `.codex/agents/` 3개가 `git status` 에 수정으로 보이지만 내용은 HEAD 와 바이트 단위로 같다(`autocrlf` 표시). 손대지 않는다.
- `ProjectSettings.asset` 의 define 순서가 한때 바뀌어 있었고 사용자 결정으로 되돌렸다(2026-10-05). 다시 바뀌면 보고만 한다.
- `index/symbols.tsv` 는 낡았다(git-head `9f5a96f`). B모드로 쓰기 전에 재생성한다(§6-2).
- GitHub Desktop 이 켜져 있으면 `.git/index.lock` 이 남을 수 있다. 실행 중인 `git.exe` 가 없을 때만 지운다(2026-10-05 한 번 발생).
- 씬 저장을 `Unity_RunCommand` 로 한 것은 T2a 두 씬에 한한 1회성 승인이었다. 다음 씬 수정도 저장 전에 다시 승인받는다(§0, §3-3).
- 테스트 결과 XML 은 Unity 의 `Path.GetTempPath()/ChainRush-PlayMode-results.xml` 하나에 덮어쓴다(이 머신 `C:/Users/Public/Documents/ESTsoft/CreatorTemp/`). 실행마다 따로 보관하려면 끝날 때 복사한다.
- 도메인 리로드 때 `Deleting invalid font reference.` 경고가 수십 건 나온다(P1-3a 부터, 원인 미확인). 이번 변경과 무관.
