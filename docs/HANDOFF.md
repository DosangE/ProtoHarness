# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-08 (T3d 구현·검증 완료, 커밋·병합 전). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-08)

- 브랜치: T4 까지 `dev` 에 병합돼 있다(`origin/dev` 에는 푸시 안 함). **T3d 는 `feature/course-t3d-cleanup` 에서 구현·검증을 마쳤고 커밋·병합 전이다**(사용자가 시킬 때).
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`(직선·원호·종단 곡선, 절대 `S`, 루프·초점), 이동(자유 조향·그립·경사·땅 붙잡기·가드), 드리프트 → 체인 게이지 → 체인 액션.
  - T3a 절차 노면, T3b 시드 생성기, T3c 절차 무한 코스(`ProceduralCourse`), T4 서킷(`TrackDefinition`·`LapCounter`·`CircuitRace`).
  - **T3d** (DECISIONS 2026-10-08 "코스 T3d"): 낡은 직선 무한 코스(`EndlessCourse`, `ChainRushEndless` 씬)와 그 제작 도구 3개·테스트를 지웠다. 조우·공격·그래플 테스트는 `ChainRushCombatTests` 로 옮겼다.
- 씬 3개: `ChainRushPrototype`(직선 유한), `ChainRushProcedural`(절차 무한), `ChainRushCircuit`(서킷). **씬은 YAML 이 원본이다**(씬 제작 도구는 `ChainRushSceneBuilder`·`ChainRushCircuitSceneBuilder` 만 남았다. 옛 사슬은 git 이력).
- 테스트: EditMode 213, PlayMode 61(Device 2 + Sweep 1 + 게이트 58). 병합 조건 실행은 Device·Sweep 제외 58건, 약 403초/회. 20 시드 스윕(`Run Course Sweep`) 약 933초.

## 2. 다음 작업 (사용자가 고른다 — 어느 쪽이든 §3-2 합의 요청서부터)

- **T3e 장식 (후보)**: 절차 코스·서킷 옆 배경(옛 네온 타워 같은 것). 곡선·언덕·서킷에서 도로 구간과 겹치지 않게 놓는 규칙이 필요하다. 지금 두 씬은 어두운 빈 공간이다.
- **T5 보충 모듈** (COURSE 5-2): 경사 커브·나선·갈림길 등. 서킷에서도 쓸 수 있다.
- **P2 리플레이·결정성** (DESIGN P2): 같은 시드 + 같은 입력 로그 = 같은 결과를 테스트로 증명. 레이스(P3)의 가장 큰 위험인 재현성을 조기에 확인한다.
- **P3 레이스** (DESIGN 4절): 아이템·접촉·순위·고스트·다인 출발. 서킷의 `startSlots`·체크포인트·`LapCounter` 가 전제다. 이때 `CourseTarget` 판정 트랙 프레임화와 낙사 복귀(마지막 체크포인트)를 같이 정한다.
- **남은 것**
  - 체감 튜닝은 아직이다(`CourseTuning_Default.asset`·`Circuit_Stadium.asset` 모두 계산한 시작값). 사용자가 `ChainRushProcedural`·`ChainRushCircuit` 을 직접 달려 본 피드백이 필요하다.
  - 서킷은 자기 교차가 없는 형태만 지원한다(다리·교차 불가).
  - `CLAUDE.md` §9-2 의 스윕 조건 목록("`CourseGenerator`·`CourseTuning`·`ProceduralCourse` 를 바꾼 브랜치")에 `Centerline` 을 더할지는 사용자 결정(T4 에서 이월).
  - 생성기는 한 걸음 앞(R9)까지만 본다. 막다른 길 예외 가능성(T3c 이월).
  - `docs/COURSE.md`·`DESIGN.md` 본문에 옛 직선 코스를 가리키는 줄과 `EndlessCourse.cs:…` 줄 번호가 당시 분석으로 남아 있다(`COURSE.md` 진행 줄에 표시).

## 3. 보류된 사용자 결정

| # | 주제 | 상태 | 참고 |
|---|---|---|---|
| 1 | 병합 조건 검증 줄이기 | 사용자가 질문함("작업한 부분만 검증하면 안 되나"). 제시안: A 그대로 / **B 추천: 전체 1회 + 2회차는 바뀐 영역 클래스만** / C 영향 범위만 / D 1400m 장거리 테스트 배속 3 → 5. **답 대기.** T3a 는 A(전체 2회)로 검증했다. 실행 1회가 약 240초로 늘었다(T3c 뒤 약 377초). 바꾸면 `CLAUDE.md` §9-2 + DECISIONS, `docs/` 브랜치, 문서 검증(§9-2) | 2026-10-05 대화 |
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
