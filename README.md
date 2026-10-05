# ProtoHarness

AI 에이전트(Claude Code, Codex)와 함께 개발하는 Unity 6 3D 프로젝트.
이전 프로젝트 [Chain Rush](https://github.com/DosangE/Chain-Rush)를 **후방 3인칭 3D 러너로 옮기고**, 그 위에서 추가 개발을 진행하고 있다.

> **상태: 개발 중인 프로토타입.** 세부 기획은 아직 확정되지 않았다. 아래 "방향"은 합의 대기 중인 초안이다.

## 무엇인가

- 자동 전진하는 러너. 지상에서 점프하고, 공중에서 앵커에 **체인을 걸어 스윙**하며 갭을 건넌다. 적을 체인으로 공격한다.
- 원작의 점프·그래플링·공격 흐름을 3D로 새로 구현했다. 원작의 코드·아트 파일은 복사하지 않았고, 캐릭터·코스는 Unity 기본 도형으로 만든다.
- 별도 **무한 전투 씬**이 있다. 발판을 재활용하며 끝없이 달리고, 상공·좌·우에서 오는 적을 처치한다. 음악과 효과음은 시작 시 코드로 합성하고, 애니메이션은 Transform 관절 절차 애니메이션이다.

| 항목 | 값 |
|---|---|
| 엔진 | Unity 6000.3.25f1 |
| 렌더 | URP 17.3.0 |
| 입력 | Input System 1.20.0 (신 입력 시스템) |
| 테스트 | Unity Test Framework 1.6.0 (EditMode / PlayMode) |

## 진행 상황

`git log` 기준.

| 단계 | 내용 | 상태 |
|---|---|---|
| 3D 이식 | 코스·러너·그래플·공격·체력, 무한 전투 모드, 아트·사운드·애니메이션 | 완료 |
| P0 | 수치를 ScriptableObject로 분리 (`RunRules`, `EncounterTuning`), 첫 EditMode 테스트 | 완료 (dev 병합) |
| P1-1 | 고정 틱 — 시뮬레이션은 `ChainRushGame.FixedUpdate` 한 곳에서만 진행 | 완료 (dev 병합) |
| P1-2 | 입력 추상화 — 시뮬레이션이 장치가 아니라 `TickInput`을 받는다 | 완료 (dev 병합) |
| P1-3a | 레이서 상태 분리 — 레이서 한 명분 상태(체력·적중·쿨다운)를 `RacerState`로 꺼낸다 | 완료 (dev 병합) |
| P1-3b | 표현 분리 — 몸체 기울이기를 시뮬레이션(`RunnerMotor`)에서 표현 컴포넌트 `RunnerTilt`로 옮긴다 | 완료 (dev 병합) |
| P1-3c | 운동 상태 분리 — 모터·그래플의 속도·조향·점프·앵커·줄 길이를 `RacerState`로 옮긴다 (위치 제외) | 완료 (dev 병합) |
| 코스 T0 | 트랙 좌표계 — "앞"을 월드 +z 대신 트랙 중심선 접선으로 (직선만, 보이는 변화 없음). 설계 `docs/COURSE.md` | 완료 (dev 병합) |
| 코스 T1 | 오르막·내리막 — 종단 곡선, 경사 속도 보정, 내리막 땅 붙잡기. 점프 도달 거리 실측. 씬 코스에는 아직 경사 없음(테스트 도로로 검증) | 완료 (dev 병합) |
| 코스 T2a | 자유 조향(A/D 가 진행 방향을 돌림)과 발판 양쪽의 보이는 가드 난간. 옆 낙사 없음, 틈 낙사는 유지 | 완료 (dev 병합) |
| 코스 T2b | 커브(수평 원호)와 그립 한계(카트라이더 방향: 평소 깔끔, 미끄러짐은 드리프트로). 씬 코스는 아직 직선(테스트 도로로 검증) | 완료 (dev 병합) |
| P1 이후 | 코스 T2c(드리프트 + 부스트), T3~T4(시드 생성, 서킷), 적 다변화, 점수·결과 화면 | 예정 |

## 방향 (초안, 합의 대기)

최종 목표 후보는 **카트라이더식 서버 권위 공유 월드 레이스**(최대 8인, PC + 모바일)다. 모드는 쿠키런·카트라이더를 참고해 세 가지를 생각하고 있다.

- **무한** — 오래 살아남고 점수를 먹는다
- **속도전** — 랩타임 경쟁
- **아이템전** — 아이템을 활용한 경쟁

세부 규칙(아이템 종류, 점수, 접촉 여부 등)은 **미정**이다. 지금 하는 일은 그 목표를 위한 전제 작업이다: 수치 데이터화 → 고정 틱·입력 추상화 → 결정성(리플레이) 확보 → 이후 네트워크. 네트워크 라이브러리와 서버 방식은 아직 정하지 않았다. 자세한 내용은 [`docs/DESIGN.md`](docs/DESIGN.md).

## AI 활용 방식

이 프로젝트의 목적 중 하나는 **AI 에이전트를 규칙 안에서 부리는 하네싱 연습**이다. 에이전트가 쓰는 규칙은 사람에게도 똑같이 적용된다.

- [`CLAUDE.md`](CLAUDE.md) — 구속 규칙. 금지선, 만들기 전 목적·경로·형태 선언, 기존 자산 우선 탐색, 구현 전 합의, 추측 금지·실증 의무, 실패는 시끄럽게. ([`AGENTS.md`](AGENTS.md)는 Codex 진입점이며 규칙 본문은 `CLAUDE.md` 하나만 둔다.)
- **서브에이전트 5종** — 탐색·구조 분석·리뷰·구현·검증 (`.claude/agents/`). 역할마다 쓰기 권한을 제한한다.
- **Unity MCP 검증** — 컴파일 상태와 Console 로그 원문을 근거로 "된다"를 말한다.
- **조회 모드** — 문서 → 심볼 인덱스(`index/`) → 코드 순으로 읽어 토큰을 아낀다.
- **브랜치 전략** — `main`(마일스톤) ← `dev`(검증 통과분) ← `feature/*`. 병합은 `--no-ff`, 직접 커밋 금지.

## 시작하기

1. Unity Hub에서 **6000.3.25f1**으로 이 폴더를 연다.
2. 메뉴 `ProtoHarness > Chain Rush`
   - `Create Prototype Scene` (Ctrl+Shift+G) — 기본 테스트 씬
   - `Create Endless Scene` (Ctrl+Shift+E) — 무한 전투 씬
   - `Apply Art Sound Animation` (Ctrl+Shift+J) — 무한 씬에 표현 적용
3. Play → Enter 또는 START RUN 버튼.

씬(`Assets/_Project/Scenes/`)이 이미 저장소에 있으면 열기만 하면 된다. 기존 씬은 덮어쓰지 않는다.

**조작**: A/D 또는 방향키로 진행 방향 틀기(자유 조향, 놓으면 그 방향 유지) · 좌클릭 지상 점프, 공중에서 재클릭 시 그래플(유지하면 스윙, 놓거나 우클릭하면 해제) · Space 공격 · R 재시작 · Esc 일시정지 · M 음소거

**테스트**: 메뉴 `ProtoHarness > Chain Rush > Run PlayMode Tests` (가상 장치 테스트 제외), `Run Device Input Tests` (가상 키보드·마우스 테스트만), 또는 Test Runner 창에서 EditMode / PlayMode 실행.

## 폴더

```
Assets/_Project/        우리가 만드는 모든 것
  Scripts/Runtime/      런타임 코드 (ChainRush/ 아래 Control, Combat, Endless, Visuals, Audio)
  Scripts/Editor/       씬 생성기 등 에디터 전용 코드
  Scripts/Tests/        EditMode / PlayMode 테스트
  Data/                 ScriptableObject 인스턴스
  Scenes/               ChainRushPrototype, ChainRushEndless
docs/                   ARCHITECTURE(구조) · DECISIONS(결정 이력) · DESIGN(로드맵) · RULES/(작업별 규칙)
index/, tools/          심볼 인덱스와 재생성 스크립트
```

## 문서

| 문서 | 내용 |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | 시스템 지도, 진입점, 검증 기록 |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | 왜 이렇게 했나 |
| [`docs/DESIGN.md`](docs/DESIGN.md) | 최종 목표와 단계별 로드맵 (초안) |
| [`CLAUDE.md`](CLAUDE.md) | 작업 규칙 (항상 지킬 것) |
| [`docs/RULES/`](docs/RULES/) | 작업별 규칙 — 형태·검증·조회·서브에이전트·브랜치. 언제 읽는지는 `CLAUDE.md` 맨 위 표 |

원작 참조: [DosangE/Chain-Rush](https://github.com/DosangE/Chain-Rush)
