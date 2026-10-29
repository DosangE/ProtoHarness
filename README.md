# ProtoHarness

AI 에이전트(Claude Code, Codex)와 함께 개발하는 Unity 6 3D 레이싱 프로토타입.
이전 프로젝트 [Chain Rush](https://github.com/DosangE/Chain-Rush)를 **후방 3인칭 3D 러너로 옮기고**, 지금은 그 위에 **카트라이더식 서킷 레이스**를 만들고 있다.

> **상태: 개발 중인 프로토타입 (v0.4 "고를 거리").** 싱글 레이스가 타이틀 → 로비 → 레이스 → 로비로 한 바퀴 돌고, 성격이 다른 트랙 3개와 속도 단계 3개를 고를 수 있다. 멀티는 메뉴에 잠가 두었다.

![SKYWAY LOOP 주행 — 카운트다운, 마을, 고가 진입](docs/images/race.gif)

| 타이틀 | 로비 | SKYWAY 고가 직선의 스윙 봉 |
|---|---|---|
| ![타이틀](docs/images/title.png) | ![로비 메인 메뉴](docs/images/lobby.png) | ![SKYWAY 고가 직선](docs/images/skyway.png) |

<sub>화면은 v0.3 에디터 플레이 중 캡처(주행은 테스트 봇). 아트는 기본 도형 위주의 임시 아트다.</sub>

## 무엇인가

- 러너가 서킷을 달린다. 방향키로 가속·조향하고, 점프하고, 공중에서 앵커에 **체인을 걸어 스윙**한다. 코너와 직선에 박힌 **봉에 체인을 걸고 돌면** 게이지가 차고, 게이지로 **부스터**를 쓴다.
- 트랙 3개: **STADIUM**(야간 경기장 서킷, 3랩), **SKYWAY LOOP**(마을을 지나 고가도로를 달리는 2랩 고속 코스, 카트라이더 "빌리지 고가의 질주" 배치를 참고), **FINGER LANES**(U턴이 이어지는 2랩 테크니컬 코스, 턱에서 속도로 뜨고 봉 스윙으로만 들어가는 지름길 2곳, 카트라이더 "빌리지 손가락" 배치를 참고). 속도 단계는 보통·빠름·매우빠름 3개다. 러너 5명(ALPHA~OMEGA)은 최고 속도·가속·게이지 충전량이 다르다.
- 시뮬레이션은 **고정 틱 + 입력 추상화**라 같은 입력이면 같은 런이 비트 단위로 다시 나온다(리플레이·롤백 테스트로 확인). 서버 권위 멀티의 바탕이고, 2인 네트워크 레이스 스파이크까지 해 두었다(멀티 재개는 M7).
- 원작의 코드·아트 파일은 복사하지 않았다. 음악·효과음은 코드로 합성하고, 애니메이션은 Transform 절차 애니메이션이다.

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
| v0.5 | M4 다시 할 이유 | 최고 기록 저장(트랙×러너)·결과 화면 비교·내 고스트, 복귀 연출, 조작 피드백 이펙트 | 다음 |
| v0.6~ | M5~M7 | AI 상대 → 폴리시(트랙 추가: 고저차·헤어핀·그래플·종합 맵) → 멀티 재개 | 예정 |

## 시작하기

1. Unity Hub에서 **6000.3.25f1**으로 이 폴더를 연다.
2. `Assets/_Project/Scenes/ChainRushTitle` 을 열고 Play. (빌드 씬 목록 0번이다.)
3. 타이틀에서 Enter → 로비 `싱글 레이스` → 트랙 → 속도 → 러너를 고르고 입장한다. 트랙이 붙으면 3·2·1 카운트다운 뒤 출발한다.

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
- **서브에이전트 5종** — 탐색·구조 분석·리뷰·구현·검증 (`.claude/agents/`, Codex 용은 `tools/sync-agents.ps1` 로 생성). 역할마다 쓰기 권한을 제한한다.
- **훅** — 세션 시작 때 보호 브랜치·낡은 인덱스를 알린다.
- **Unity MCP 검증** — 컴파일 상태와 Console 로그 원문, 테스트 결과 XML 값을 근거로 "된다"를 말한다.
- **조회 모드** — 문서 → 심볼 인덱스(`index/`) → 코드 순으로 읽어 토큰을 아낀다.
- **브랜치와 테스트 3단** — `main`(마일스톤 태그) ← `dev`(검증 통과분) ← `feature/*`. 병합은 `--no-ff`, 직접 커밋 금지. 작업 중엔 바꾼 부분만, `dev` 병합엔 바뀐 범위로 고른 테스트, `main` 병합엔 전체 2회 + 사용자 플레이 확인.

## 테스트

메뉴 `ProtoHarness > Chain Rush`

- `Run PlayMode Tests` — 게이트 전체 (`Device`·`Sweep`·`Long`·`Enemy`·`Endless`·`NetEndReplay` 카테고리 제외)
- `Run Device Input Tests` — 가상 키보드 장치 테스트만
- `Run Long Tests`, `Run Course Sweep` — 무한 모드 장거리 테스트 (무한 모드 보류 동안 게이트에서 돌리지 않는다)
- 작업 중에 바꾼 부분만 돌리려면 `tools/select-tests.ps1` 이 고를 테스트를 알려 준다. 언제 무엇을 돌리는지는 `CLAUDE.md` §9-2.

## 폴더

```
Assets/_Project/        우리가 만드는 모든 것
  Scripts/Runtime/      런타임 코드 — ChainRush/(Control, Track, Race, Flow, Visuals, Audio …), Net/(네트워크)
  Scripts/Editor/       씬·트랙 생성기, 테스트 메뉴 등 에디터 전용 코드
  Scripts/Tests/        EditMode / PlayMode 테스트
  Data/                 ScriptableObject 인스턴스 (규칙 수치, 러너 프로필, 트랙)
  Scenes/               ChainRushTitle · ChainRushLobby · ChainRushCircuit(달리는 씬)
                        ChainRushTrackStadium · ChainRushTrackSkyway · ChainRushTrackFingerLanes(Additive 로 붙는 트랙 장식)
                        ChainRushTestBench(테스트 작업대)
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
