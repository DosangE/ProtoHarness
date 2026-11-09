# ProtoHarness

체인을 걸어 코너를 돌고, 스윙으로 모은 게이지를 부스터에 쓰는 **Unity 6 싱글 레이싱 프로토타입**. 이전 프로젝트 [Chain Rush](https://github.com/DosangE/Chain-Rush)의 조작을 후방 3인칭 3D 러너와 카트라이더식 레이스에 옮겼다. Claude Code와 Codex를 활용해 개발하고 있다.

> **상태: 개발 중인 프로토타입 (v0.6 "폴리시").** 다음은 v0.7 AI 상대다.

![SKYWAY LOOP 주행 — 카운트다운, 마을, 고가 진입](docs/images/race.gif)

| 타이틀 | 로비 | SKYWAY 고가 직선의 스윙 봉 |
|---|---|---|
| ![타이틀](docs/images/title.png) | ![로비 메인 메뉴](docs/images/lobby.png) | ![SKYWAY 고가 직선](docs/images/skyway.png) |

<sub>위 화면은 v0.3 에디터에서 캡처했다. GIF의 주행은 테스트 봇이다.</sub>

## v0.6 변경점

- **트랙 2개 추가** — 해변과 다리를 도는 PALM COAST와 정상에서 내려오는 SUMMIT DROP.
- **동작별 효과음** — 부스터와 부스트존에는 서로 다른 합성음을, 봉 걸림·그래플·스윙에는 CC0 쇠사슬 녹음을 쓴다. 음원 출처는 [개발 기록](docs/DECISIONS.md)에 있다.
- **스윙과 가속 연출** — 봉에 걸면 불꽃이 튀고, 스윙을 놓으면 체인이 갈라지며 사라진다. 스윙·부스터·부스트존은 서로 다른 잔상으로 보인다.
- **튜토리얼 6단계** — 로비에서 단계를 골라 이동·그래플·봉 스윙·부스터를 연습할 수 있다. 각 단계는 그림 설명으로 시작한다.

## 트랙과 기록

| 트랙 | 랩 | 주행 특징 |
|---|---:|---|
| STADIUM | 3 | 야간 경기장을 도는 서킷 |
| SKYWAY LOOP | 2 | 마을과 고가도로를 지나는 고속 코스 |
| FINGER LANES | 2 | 연속 U턴과 봉 스윙 지름길 |
| PALM COAST | 2 | 해변과 다리, 밟으면 가속하는 부스트존 |
| SUMMIT DROP | 1 | 정상에서 내려오는 편도 다운힐, 골짜기 틈 점프와 숲 공터의 스윙 지름길 |

속도는 보통·빠름·매우빠름 중에서 고른다. 러너 5명(ALPHA~OMEGA)은 최고 속도·가속·게이지 충전량이 다르다. 최고 기록과 그 판의 고스트는 트랙과 속도 조합별로 남아서, 다음 판에 다른 러너로도 같은 기록에 도전할 수 있다.

시뮬레이션은 고정 틱과 입력 추상화를 바탕으로 한다. 리플레이·롤백 테스트로 입력을 다시 적용했을 때의 결정성을 검증한다. 멀티 레이스는 로비에서 잠겨 있으며 M7에서 재개한다.

| 항목 | 값 |
|---|---|
| 엔진 | Unity 6000.3.25f1 |
| 렌더 | URP 17.3.0 |
| 입력 | Input System 1.20.0 (신 입력 시스템) |
| 네트워크 | Netcode for GameObjects 2.13.3 (멀티는 M7 까지 보류) |
| 테스트 | Unity Test Framework 1.6.0 (EditMode / PlayMode) |

## 진행 상황

로드맵은 **플레이할 수 있는 단위**로 쌓는다. 마일스톤 하나 = `dev → main` 병합 + 태그 하나, 완료 조건은 사용자가 직접 플레이해서 확인한다. 자세한 내용은 [`docs/DESIGN.md`](docs/DESIGN.md) §5.

| 태그 | 마일스톤 | 들어간 것 | 상태 |
|---|---|---|---|
| v0.1 | 기술 기반 (P0~P3 C1) | 수치 SO 분리, 고정 틱, 입력 추상화, 레이서 상태 분리, 트랙 좌표계·경사·커브·드리프트, 절차 코스·서킷, 입력 기록·재생과 결정성, 롤백, 2인 네트워크 레이스 스파이크(서버 권위 + 클라 예측·보정) | 완료 |
| v0.2 | M1 한 판이 끝까지 돈다 | 낙하 복귀, 코너 봉 스윙, 체감 튜닝(조향·그래플·부스터 게이지) | 완료 |
| v0.3 | M2 게임의 뼈대 | 타이틀·로비 씬, 트랙·러너 선택(러너 5명, 선택창 3D 프리뷰), 설정(볼륨·속도 시야 효과·조작 키), 일시정지 메뉴, 트랙별 최고 기록, SKYWAY LOOP 트랙, 직선 스윙 봉, 후진 | 완료 |
| v0.4 | M3 고를 거리 | FINGER LANES 트랙(U턴·턱·지름길), 세 트랙의 장식, 속도 단계 보통 / 빠름 / 매우빠름, 봉 스윙 한도, 코너 봉 각도 수정, 화면 설정 | 완료 |
| v0.5 | M4 다시 할 이유 | 트랙 × 속도의 최고 기록과 그 고스트(위치 기록 재생), 결과 화면 기록 비교, 낙하 복귀 연출, 조작 피드백 이펙트(스윙 가능 봉 표시·부스터·걸림·빗나감), 로비 순서 속도 → 트랙 → 러너 | 완료 |
| v0.6 | M6 폴리시 | 동작별 효과음, 봉 스윙 이펙트, PALM COAST 해변 코스와 SUMMIT DROP 다운힐 코스, 튜토리얼 | 완료 |
| v0.7 | M5 같이 달리는 맛 | AI 상대, 상대 달리기 연출 | 다음 |
| v0.8~ | M7 | 멀티 재개 | 예정 |

## 시작하기

1. 저장소를 클론한 뒤 `git lfs pull`로 모델·음원 등 Git LFS 에셋을 받는다.
2. Unity Hub에서 이 폴더를 **6000.3.25f1**로 연다.
3. `Assets/_Project/Scenes/ChainRushTitle.unity`를 열고 Play를 누른다.
4. 타이틀에서 Enter를 누른 뒤 로비에서 `싱글 레이스` → 속도 → 트랙 → 러너 순으로 고른다. 조작을 먼저 익히려면 로비에서 `튜토리얼`을 선택한다.

**조작** (카트라이더식, 게임 안 `설정 > 조작 키` 와 같다)

| 키 | 동작 |
|---|---|
| ↑ | 가속 (스윙 중에도) |
| ↓ 유지 | 브레이크 · 후진 |
| ← / → | 조향 |
| 왼쪽 Alt | 점프 · 공중에서 다시 누르고 유지하면 그래플 (떼거나 ↓ 로 해제) |
| 왼쪽 Shift 유지 | 봉 스윙 (게이지 충전) |
| 왼쪽 Ctrl | 부스터 (게이지 1칸) |
| Space | 공격 |
| Esc | 일시정지 (계속 · 재시작 · 로비) |
| M | 음소거 |

## AI 활용 방식

이 프로젝트의 목적 중 하나는 **AI 에이전트를 규칙 안에서 부리는 하네싱 연습**이다. 에이전트가 쓰는 규칙은 사람에게도 똑같이 적용된다.

- [`CLAUDE.md`](CLAUDE.md) — 구속 규칙. 금지선, 만들기 전 목적·경로·형태 선언, 기존 자산 우선 탐색, 구현 전 합의, 추측 금지·실증 의무, 실패는 시끄럽게. 작업별 세부 규칙은 [`docs/RULES/`](docs/RULES/). ([`AGENTS.md`](AGENTS.md)는 Codex 진입점이며 규칙 본문은 `CLAUDE.md` 하나만 둔다.)
- **서브에이전트 7종** — 탐색·구조 분석·리뷰·구현·검증·Blender 모델링·게임 기획. 정의는 [`.claude/agents/`](.claude/agents/)에 있고 `tools/sync-agents.ps1`로 Codex 정의를 생성한다. 역할별 도구와 권한은 [에이전트 운영 규칙](docs/RULES/SUBAGENTS.md)에 정리했다.
- **기획과 아트 작업 분리** — 기획 에이전트는 플레이 피드백을 가설과 확인 기준으로 바꾼다. Blender 에이전트는 `art-src/`에 원본과 FBX를 저장하고, 승인된 Unity 작업에서 `Assets/`로 가져와 확인한다.
- **훅** — 세션 시작 때 보호 브랜치·낡은 인덱스를 알린다.
- **Unity MCP 검증** — 컴파일 상태와 Console 로그 원문, 테스트 결과 XML 값을 근거로 "된다"를 말한다.
- **조회 모드** — 문서 → 심볼 인덱스(`index/`) → 코드 순으로 읽어 토큰을 아낀다.
- **브랜치와 검증** — 기능 브랜치에서 변경을 검증해 `dev`로 합친다. `main` 병합 전에는 마일스톤 전체 테스트와 사용자 플레이 확인을 거친다. 세부 기준은 [`CLAUDE.md`](CLAUDE.md) §9-2에 있다.

## 테스트

메뉴 `ProtoHarness > Chain Rush`

- `Run PlayMode Tests` — 마일스톤 PlayMode 검증 (`Device`·`Enemy`·`NetEndReplay` 제외)
- `Run Device Input Tests` — 가상 키보드·마우스 입력 검증
- `tools/select-tests.ps1` — 변경 범위를 기준으로 작업 중 돌릴 테스트 후보를 출력
- `tools/test-runs.ps1 -Record` — 결과 XML과 실행 기준점 기록. `-Last -Passed`로 마지막 통과 이후 변경을 확인한다. [테스트 실행 절차](docs/RULES/VERIFICATION.md)

## 폴더

```
Assets/_Project/        우리가 만드는 모든 것
  Scripts/Runtime/      런타임 코드 — ChainRush/(Control, Track, Race, Flow, Visuals, Audio …), Net/(네트워크)
  Scripts/Editor/       씬·트랙 생성기, 테스트 메뉴 등 에디터 전용 코드
  Scripts/Tests/        EditMode / PlayMode 테스트
  Data/                 ScriptableObject 인스턴스 (규칙 수치, 러너 프로필, 트랙)
  Scenes/               타이틀, 로비, 레이스, 트랙별 장식 씬, 테스트 작업대
art-src/                Blender 원본과 FBX 후보
docs/                   ARCHITECTURE(구조) · DECISIONS(결정 이력) · DESIGN(로드맵) · HANDOFF(다음 작업) · RULES/(작업별 규칙)
index/, tools/          심볼 인덱스와 재생성·테스트 선택·에이전트 동기화 스크립트
```

## 문서

| 문서 | 내용 |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | 시스템 지도, 진입점, 검증 기록 |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | 왜 이렇게 했나 |
| [`docs/DESIGN.md`](docs/DESIGN.md) | 최종 목표와 마일스톤 로드맵 |
| [`docs/HANDOFF.md`](docs/HANDOFF.md) | 다음 세션이 이어받을 일 |
| [`CLAUDE.md`](CLAUDE.md) | 작업 규칙 (항상 지킬 것) |
| [`docs/RULES/`](docs/RULES/) | 작업별 규칙 — 형태·검증·조회·서브에이전트·브랜치. 언제 읽는지는 `CLAUDE.md` 맨 위 표 |

원작 참조: [DosangE/Chain-Rush](https://github.com/DosangE/Chain-Rush)
