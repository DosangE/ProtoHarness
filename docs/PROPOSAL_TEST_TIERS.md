# 설계 제안서: 테스트를 3단으로 나눠 "바꾼 부분만" 돌린다

> **상태: 일부 확정 (2026-10-14).** 3단 규칙(6절 2단계)과 선택 도구(6절 4단계)를 `docs/test-tiers` 에서 들였다. 결정과 이 문서와 달라진 점(카테고리 대신 클래스 이름, 파일 단위 매핑, Net 은 지문 없음)은 `docs/DECISIONS.md` 2026-10-14 "테스트 3단" 이 원본이다. 6절 1단계(측정)도 끝났다(같은 DECISIONS 항목). 남은 것: 그 값으로 정할 `Long` 묶음, 3단계(`[Category]`)는 필요해질 때까지 보류, 5단계(메뉴).
> 아래 본문은 작성 당시의 초안이다.
> 이 제안은 **규칙 변경**이다(`CLAUDE.md` §9-2 병합 조건, `docs/RULES/VERIFICATION.md` §4-4). 확정하려면 사용자 승인 → `docs/DECISIONS.md` 기록 → 규칙 본문은 그 규칙이 있는 한 곳만 고친다(단일 진실 원천).
> 출발점: 2026-10-14 사용자 "테스트만 20분 정도로 상당히 오래 걸린다. 모든 경우에 대해 테스트를 진행하고 있는 것 같다. 수정한 부분만 QA 를 돌리거나 시뮬레이션 해 보면 될 것 같다."

## 1. 지금 무엇이 시간을 쓰나 (사실)

| 항목 | 값 | 근거 |
|---|---|---|
| PlayMode 게이트 1회 (Device·Sweep 제외) | 약 580~660초 | `docs/HANDOFF.md` 1절 "테스트" |
| 병합 조건 | 같은 코드에서 PlayMode **연속 2회** → 약 20분 | `CLAUDE.md` §9-2 |
| 코스 생성·투영을 바꾸면 추가 | `Run Course Sweep` 약 933초 | `CLAUDE.md` §9-2, HANDOFF |
| EditMode 전체 | 237건 7.2초 (2026-10-14 05:45Z XML) | `%TEMP%/ChainRush-PlayMode-results.xml` |
| 테스트 분류(Category) | `Device` · `Net` · `NetRace` · `Sweep` **네 가지뿐** | `Tests/PlayMode/*.cs` grep |

- **2회차를 둔 이유는 대부분 사라졌다.** 2회 규칙은 2026-10-02 간헐 실패 때문에 들어왔고, `docs/DECISIONS.md` 2026-10-13 "병합 2회차 축소" 가 "그때의 간헐 실패는 지금 게이트에서 빠진 `Device` 테스트였다" 고 적었다. `Device` 는 이미 게이트에서 빠져 있다.
- **분류가 넷뿐이라 "바뀐 부분만" 고를 수단이 없다.** 그래서 Net 만 바꾼 경우를 빼면 늘 전체가 돈다.
- **테스트별 소요 시간은 측정하지 못했다.** 결과 XML 이 실행마다 같은 파일에 덮어써지고, 지금 남은 것은 EditMode 실행이다. → 6절 1단계.

## 2. 예전에 같은 방향을 버린 이유와, 이번에 달라진 것

2026-10-13 "병합 2회차 축소" 는 "바뀐 영역의 클래스만" 안(B 원안)을 버렸다. 이유 둘:

1. **코어 코드는 거의 모든 테스트가 거쳐 간다.** "영역" 을 사람이 판단하게 되고, 그 판단이 조용한 우회가 된다.
2. "카테고리 + 다른 클래스" 를 **한 실행으로 고를 수 없다**(필터 종류끼리 AND, `RuntimeTestRunnerFilter.cs:43`).

이번 설계의 답:

1. → 판단을 없앤다. **경로 → 테스트 묶음의 고정 매핑 표**(4절)를 두고, 표에 없는 경로와 코어 파일은 **전체**로 떨어진다(기본값이 안전한 쪽).
2. → **한 `Filter` 안에서는 AND 지만, `Filter` 여러 개는 OR 로 묶인다.** `ExecutionSettings(params Filter[])` 가 `new OrFilter(filters.Select(...))` 로 합친다(`Library/PackageCache/com.unity.test-framework@bd7f943e9647/UnityEditor.TestRunner/Api/ExecutionSettings.cs:22,95`, 같은 패키지 `TestRun/Tasks/BuildNUnitFilterTask.cs:18`). 그러므로 "카테고리 `^Course$` **또는** 클래스 `ChainRushDriftTests`" 를 한 실행으로 고를 수 있다. 실제로 돌려 확인하지는 않았다(구현 때 검증).

## 3. 3단 테스트

| 단 | 언제 | 무엇을 | 목표 시간 | 병합 근거로 쓰나 |
|---|---|---|---|---|
| **① 빠른 확인** | 작업 중, 수정할 때마다 | EditMode 전체 + **매핑 표가 고른 PlayMode 묶음** + **시뮬 지문**(3-1) | 1~3분 (측정 후 확정) | 아니다. 개발 중 피드백용 |
| **② 병합** | `feature → dev` | EditMode + PlayMode 전체(Device·Sweep 제외) **1회**. Net 만 바꾼 브랜치는 지금처럼 | 약 10분 | 그렇다 (§9-2 를 대체) |
| **③ 마일스톤** | `dev → main`, 태그 전 | 전체 **2회** + `Sweep` + `Device` + 네트워크 레이스(`NetRace`) + ③ 전용 장거리 묶음(5절) | 지금보다 길다 | 그렇다 |

- **②에서 2회 → 1회**: 간헐 실패 경보는 ③의 2회가 맡는다. 감수하는 것: 코어 테스트에 새 간헐 실패가 생기면 마일스톤 전까지 한 번씩만 본다. 대신 **"한 번이라도 실패하면 원문 보고, 원인을 설명 못 하면 병합하지 않는다"(§9-2)는 그대로** 둔다.
- 기존 조건부 추가 실행(입력 장치 경로를 바꾸면 `Run Device Input Tests`, 코스 생성·투영을 바꾸면 `Run Course Sweep`)은 ②에 그대로 남긴다.

### 3-1. 시뮬 지문 — "시뮬레이션 해 보기" 를 기계로

`ChainRushReplayTests` 의 서킷 리플레이(`Replay_CircuitBotRun_RepeatsEveryTickBitForBit`)는 같은 입력으로 서킷을 돌려 상태 해시를 비교한다(현재 기대 해시 `0xF9A914DA3FAA680B`, HANDOFF 1절). 이것을 새 카테고리 `Fingerprint` 로 묶어 ①에 **항상** 넣는다.

| 바꾼 것 | 지문이 | 뜻 |
|---|---|---|
| 동작을 바꾸면 안 되는 수정 (연출·HUD·Net·리팩터링) | **같다** | 게임 동작이 안 바뀌었다는 증명 |
| 동작을 바꾸려는 수정 (물리·수치) | **바뀐다** | 의도한 변화. 새 해시를 보고에 적고 기대값을 갱신한다(사양 변경으로 취급, §0 "테스트를 통과시키려고 고치지 않는다" 와 구분) |
| 동작을 바꾸려 했는데 | 같다 | 수정이 시뮬에 닿지 않았다 — 버그 신호 |

이 테스트 1건의 소요 시간은 측정하지 못했다(1700틱, 리플레이 쪽 배속 확인 필요).

## 4. 경로 → 테스트 묶음 매핑 표 (초안)

`git diff --name-only dev...HEAD` 의 파일마다 아래 표의 **위에서부터 처음 맞는 줄**을 쓴다. 고른 묶음의 합집합을 돈다. 하나라도 "전체" 면 전체.

| 바뀐 경로 | 돌릴 묶음 |
|---|---|
| `Scripts/Runtime/ChainRush/RunnerMotor.cs`, `GrappleController.cs`, `ChainRushGame.cs`, `RacerState.cs`, `Ticks*`, `SimSnapshot*` (코어 시뮬) | **전체** |
| `Assets/_Project/Data/**` (SO 값이 시뮬에 들어간다), `Scenes/**`, `Scripts/Editor/**`, `Packages/`, `ProjectSettings/` | **전체** |
| 테스트 보조 파일 `CourseBot`, `CircuitBot`, `TestRoad`, `TrackFollower`, `StateTrace` | **전체** |
| `Scripts/Runtime/Net/**` | `Net` (지금 규칙과 같다) |
| `Scripts/Runtime/ChainRush/Track/**`, `Endless/**` | `Course` + `Fingerprint` (+ 생성기·스트리밍이면 ②에서 `Sweep`, 지금 규칙) |
| `Scripts/Runtime/ChainRush/Race/**` | `Race` + `Net` + `Fingerprint` |
| `Scripts/Runtime/ChainRush/Combat/**` | `Combat` + `Fingerprint` |
| `Scripts/Runtime/ChainRush/Control/**` | `Input` + `Fingerprint` (+ 장치 경로면 `Device`) |
| `Scripts/Runtime/ChainRush/Visuals/**`, `Audio/**`, `ChainRushHud.cs` | `Presentation` + `Fingerprint` |
| `Scripts/Tests/PlayMode/<클래스>.cs` 만 | 그 클래스 |
| `Scripts/Tests/EditMode/**` | EditMode 만 |
| 위 어디에도 없음 | **전체** |

새 카테고리(제안): `Core`, `Course`, `Race`, `Combat`, `Input`, `Presentation`, `Fingerprint`. 기존 `Device`, `Net`, `NetRace`, `Sweep` 는 그대로. 테스트 클래스 배정 초안:

| 카테고리 | 클래스 (PlayMode, grep 기준 대략 건수) |
|---|---|
| `Core` | `ChainRushTests`(12), `ChainRushRollbackTests`(9), `ChainRushWorldTests`(5), `ChainRushControllerCostTests`(2) |
| `Course` | `ChainRushCurveTests`(3), `ChainRushSlopeTests`(3), `ChainRushRoadTests`(4), `ChainRushSteeringTests`(3), `ChainRushProceduralTests`(8, 그중 1건은 `Sweep`), `ChainRushDriftTests`(4), `ChainRushCornerSwingTests`(3) |
| `Race` | `ChainRushCircuitTests`(5), `RaceOrderTests`(9), `RaceStandingsTests`(9), `RaceStandingsTextTests`(10) |
| `Combat` | `ChainRushCombatTests`(5) |
| `Input` | `ChainRushInputSourceTests`(9) |
| `Presentation` | `ChainRushPresentationTests`(3) |
| `Fingerprint` | `ChainRushReplayTests` 의 서킷 리플레이 1건 |

드리프트·코너 스윙을 `Course` 에 넣은 것은 시험 도로(`TestRoad`) 위에서 도는 이동 테스트라서다. 이동 규칙은 `RunnerMotor`(코어)에 있으므로 그 파일을 바꾸면 어차피 전체다.
`Race` 와 `Net` 은 테스트 클래스 이름으로만 나눴고, 각 클래스의 `Category` 속성은 구현 때 파일을 읽고 확정한다.

## 5. 중복 테스트 정리와 함께 갈 것

맵 중복은 `docs/PROPOSAL_SCENE_CLEANUP.md` 에서 다룬다. 이 문서와 맞물리는 것만 적는다.

- 절차 코스 쪽 결정성 테스트(`Replay_ProceduralCourseRun_…`, 롤백의 절차 코스 쪽)는 스트리밍·원점 이동의 결정성을 본다. 서킷 쪽과 겹치지 않으므로 지우지 않고, **③ 전용 장거리 묶음**으로 내린다(새 카테고리 `Long`, ②에서 제외).
- 어느 테스트를 `Long` 으로 내릴지는 6절 1단계의 측정값으로 정한다. 이름만 보고 정하지 않는다.

## 6. 구현 순서 (각 단계마다 §3-2 합의)

1. **측정**: 다음 PlayMode 게이트 실행 직후 XML 을 따로 복사해 두고, 테스트 묶음·테스트별 `duration` 순위를 뽑는다. 코드 변경 없음.
2. **규칙**: 이 문서와 측정값으로 ②의 2회 → 1회와 ①·③을 `docs/DECISIONS.md` 에 결정으로 기록하고, `CLAUDE.md` §9-2·`VERIFICATION.md` §4-4 를 고친다(`docs/` 브랜치, 문서 검증).
3. **카테고리 달기**: 테스트 클래스에 `[Category]` 를 붙인다. 테스트 동작은 바꾸지 않는다. `Assets/` 변경이므로 게이트를 치른다.
4. **선택 도구**: `tools/` 에 diff → 필터 목록을 출력하는 스크립트(형태: PowerShell, `tools/reindex.ps1` 과 같은 자리). 출력 전체를 작업 보고에 붙인다. 이 도구는 ① 전용이고 병합 근거가 아니다.
5. **메뉴**: ①을 한 번에 돌리는 메뉴 항목(`ChainRushSceneBuilder` 의 기존 `Run …` 메뉴 옆)이 필요한지는 4 뒤에 정한다.

## 7. 버린 대안

| 안 | 버린 이유 |
|---|---|
| ①만으로 병합 | 코어 변경이 매핑에서 전체로 떨어지므로 아끼는 시간이 적고, 매핑 표의 빈틈이 곧 검증 빈틈이 된다 |
| 2회 유지 + 장거리 테스트 배속(옛 D안) | 테스트 동작을 바꾼다. 측정(6-1) 뒤에 따로 볼 수 있다 |
| CI 도입 | 단독 개발, 로컬 Unity 에디터 전제. 지금 범위 밖 |

## 8. 확인 못 한 것

- PlayMode 테스트별 소요 시간 (6-1 에서 측정)
- `Filter` 여러 개 OR 가 `TestRunnerApi.Execute` 의 PlayMode 실행에서도 그대로인지 (소스로만 확인, 실행 안 함)
- 서킷 리플레이 1건의 소요 시간
- 각 테스트 클래스가 실제로 어떤 코드 경로를 지나는지 (카테고리 배정은 클래스 이름·불러오는 씬 기준)
