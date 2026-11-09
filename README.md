# ProtoHarness

체인을 봉에 걸어 코너를 돌고, 스윙으로 모은 게이지를 부스터로 쓰는 3D 레이싱 게임 프로토타입입니다. Unity 6로 만들고 있으며, 지금은 혼자 달리는 싱글 레이스를 플레이할 수 있습니다. 2D 게임 [Chain Rush](https://github.com/DosangE/Chain-Rush)의 조작을 러너 뒤에서 따라가는 3인칭 시점과 카트라이더식 트랙으로 옮겼습니다.

Claude Code와 Codex로 개발하고 있습니다. 에이전트가 지킬 규칙을 문서로 정해 두고, 그 규칙대로 작업하게 하는 방법을 시험하는 것도 이 저장소의 목적입니다.

> **현재 버전: v0.6.** 다음 버전(v0.7)에서 AI 상대를 추가합니다.

![SUMMIT DROP 주행 — 다운힐, 숲 공터의 봉 스윙 지름길](docs/images/race.gif)

| 타이틀 | 로비 | 튜토리얼 |
|---|---|---|
| ![타이틀](docs/images/title.png) | ![로비 메인 메뉴](docs/images/lobby.png) | ![튜토리얼 설명 팝업](docs/images/tutorial.png) |

| PALM COAST | FINGER LANES | 결과 화면 |
|---|---|---|
| ![PALM COAST 해변 도로](docs/images/palm-coast.png) | ![FINGER LANES 연속 U턴](docs/images/finger-lanes.png) | ![결과 화면의 기록 비교](docs/images/result.png) |

<sub>v0.6 에디터에서 캡처. 주행 화면은 테스트용 자동 조종.</sub>

## v0.6에서 바뀐 것

- **새 트랙 2개**: 해변과 다리를 도는 PALM COAST, 산 정상에서 마을까지 내려오는 SUMMIT DROP
- **동작별 효과음**: 부스터·부스트존은 서로 다른 합성음, 봉 걸기·그래플·스윙은 CC0 쇠사슬 녹음. 음원 출처는 [개발 기록](docs/DECISIONS.md)
- **스윙·가속 이펙트**: 봉에 걸 때 불꽃, 스윙을 놓으면 체인이 끊어지며 사라짐, 스윙·부스터·부스트존마다 다른 잔상
- **튜토리얼 6단계**: 로비에서 단계를 골라 이동·그래플·봉 스윙·부스터 연습. 단계마다 그림 설명으로 시작

![봉 스윙 — 봉에 걸고 돌다가 놓으며 가속](docs/images/swing.gif)

## 트랙

| 트랙 | 랩 | 특징 |
|---|---:|---|
| STADIUM | 3 | 야간 경기장을 도는 서킷 |
| SKYWAY LOOP | 2 | 마을과 고가도로를 지나는 고속 코스 |
| FINGER LANES | 2 | 연속 U턴, 봉 스윙으로 질러가는 지름길 |
| PALM COAST | 2 | 해변과 다리, 밟으면 빨라지는 부스트존 |
| SUMMIT DROP | 1 | 정상에서 내려오는 편도 다운힐, 골짜기 틈 점프, 숲 공터의 봉 스윙 지름길 |

## 게임 규칙

- **속도 단계**: 보통 · 빠름 · 매우빠름
- **러너 5명**(ALPHA~OMEGA): 최고 속도·가속·게이지 충전량이 서로 다름
- **최고 기록과 고스트**: 트랙과 속도 조합마다 저장. 다른 러너로 달려도 같은 기록의 고스트와 겨룸
- **멀티 레이스**: 로비에서 잠겨 있음. v0.8 이후 다시 엶
- **결정성**: 고정 틱으로 돌고, 같은 입력을 다시 넣으면 같은 판이 재현됨(리플레이·롤백 테스트로 확인)

| 항목 | 값 |
|---|---|
| 엔진 | Unity 6000.3.25f1 |
| 렌더 | URP 17.3.0 |
| 입력 | Input System 1.20.0 |
| 네트워크 | Netcode for GameObjects 2.13.3 (멀티는 v0.8 이후) |
| 테스트 | Unity Test Framework 1.6.0 (EditMode / PlayMode) |

## 버전 기록

버전마다 완료 조건을 직접 플레이해서 확인한 뒤 `main`에 합치고 태그를 답니다. 마일스톤 정의는 [`docs/DESIGN.md`](docs/DESIGN.md) §5에 있습니다.

| 태그 | 내용 | 상태 |
|---|---|---|
| v0.1 | 기술 기반: 수치 데이터 분리, 고정 틱, 입력 추상화, 트랙 좌표계·경사·커브, 서킷, 입력 기록·재생, 롤백, 2인 네트워크 레이스 시험 | 완료 |
| v0.2 | 한 판 완주: 낙하 복귀, 코너 봉 스윙, 조향·그래플·게이지 튜닝 | 완료 |
| v0.3 | 타이틀·로비: 트랙·러너 선택(러너 5명, 3D 미리보기), 설정, 일시정지, 트랙별 최고 기록, SKYWAY LOOP, 직선 스윙 봉, 후진 | 완료 |
| v0.4 | 트랙·속도 선택: FINGER LANES, 세 트랙 장식, 속도 3단계, 봉 스윙 시간 한도, 화면 설정 | 완료 |
| v0.5 | 기록과 고스트: 트랙×속도 최고 기록과 고스트, 결과 화면 기록 비교, 낙하 복귀 연출, 조작 이펙트 | 완료 |
| v0.6 | 소리와 새 트랙: 효과음, 봉 스윙 이펙트, PALM COAST, SUMMIT DROP, 튜토리얼 | 완료 |
| v0.7 | AI 상대: 부딪히며 겨루는 봇, 상대 달리기 동작 | 다음 |
| v0.8~ | 멀티 레이스 재개 | 예정 |

## 시작하기

1. 저장소를 클론한 뒤 `git lfs pull`로 모델·음원·이미지(Git LFS) 받기
2. Unity Hub에서 이 폴더를 **6000.3.25f1**로 열기
3. `Assets/_Project/Scenes/ChainRushTitle.unity`를 열고 Play
4. 타이틀에서 Enter → 로비에서 `싱글 레이스` → 속도 → 트랙 → 러너 순으로 선택. 조작을 먼저 익히려면 로비의 `튜토리얼`

**조작** (게임 안 `설정 > 조작 키`와 같음)

| 키 | 동작 |
|---|---|
| ↑ | 가속 (스윙 중에도) |
| ↓ 유지 | 브레이크 · 후진 |
| ← / → | 조향 |
| 왼쪽 Alt | 점프 · 공중에서 다시 누르고 유지하면 그래플 (떼거나 ↓로 해제) |
| 왼쪽 Shift 유지 | 봉 스윙 (게이지 충전) |
| 왼쪽 Ctrl | 부스터 (게이지 1칸) |
| Space | 공격 |
| Esc | 일시정지 (계속 · 재시작 · 로비) |
| M | 음소거 |

## AI 에이전트 작업 방식

에이전트가 따르는 규칙은 사람에게도 똑같이 적용합니다.

- **[`CLAUDE.md`](CLAUDE.md)**: 항상 지킬 규칙. 금지 사항(씬·프리팹 YAML 직접 수정, 패키지·프로젝트 설정 변경 등), 새 파일 전 목적·경로·형태 선언, 기존 코드 먼저 검색, 구현 전 합의, 근거(`파일:줄`) 없는 주장 금지, 오류를 숨기지 않기. 작업별 세부 규칙은 [`docs/RULES/`](docs/RULES/). [`AGENTS.md`](AGENTS.md)는 Codex용 입구이고 규칙 본문은 `CLAUDE.md` 하나
- **서브에이전트 7종**: 탐색·구조 분석·리뷰·구현·검증·Blender 모델링·게임 기획. 정의는 [`.claude/agents/`](.claude/agents/), Codex용 정의는 `tools/sync-agents.ps1`로 생성. 역할별 도구와 권한은 [에이전트 운영 규칙](docs/RULES/SUBAGENTS.md)
- **기획·아트 에이전트**: 기획 에이전트는 플레이 피드백을 원인 가설과 플레이 확인 질문으로 정리(파일 수정 없음). Blender 에이전트는 `art-src/`에 원본과 FBX 저장, Unity로 가져오는 것은 승인 뒤 별도 작업
- **훅**: 세션 시작 때 현재 브랜치와 심볼 인덱스가 낡았는지 알림, `main`·`dev`에 직접 커밋 차단
- **Unity MCP 검증**: 컴파일 상태, Console 로그 원문, 테스트 결과 XML 값을 근거로 "된다"고 보고
- **조회 순서**: 문서 → 심볼 인덱스(`index/`) → 코드. 파일을 통째로 읽는 양을 줄여 토큰 절약
- **브랜치와 테스트**: 기능 브랜치에서는 바뀐 부분을 덮는 테스트만 돌리고 `dev`에 병합. `main` 병합 전에만 전체 테스트와 사용자 플레이 확인. 기준은 [`CLAUDE.md`](CLAUDE.md) §9-2

## 테스트

메뉴 `ProtoHarness > Chain Rush`

- `Run PlayMode Tests`: 마일스톤용 PlayMode 전체 (`Device`·`Enemy`·`NetEndReplay` 제외)
- `Run Device Input Tests`: 가상 키보드·마우스 입력
- `tools/select-tests.ps1`: 바뀐 파일을 기준으로 돌릴 테스트 후보 출력
- `tools/test-runs.ps1 -Record`: 결과 XML과 실행 시점 기록. `-Last -Passed`로 마지막 통과 뒤 바뀐 파일 확인. [테스트 실행 절차](docs/RULES/VERIFICATION.md)

## 폴더

```
Assets/_Project/        우리가 만드는 모든 것
  Scripts/Runtime/      런타임 코드 — ChainRush/(Control, Track, Race, Flow, Visuals, Audio …), Net/(네트워크)
  Scripts/Editor/       씬·트랙 생성기, 테스트 메뉴 등 에디터 전용 코드
  Scripts/Tests/        EditMode / PlayMode 테스트
  Data/                 ScriptableObject 인스턴스 (규칙 수치, 러너 프로필, 트랙)
  Scenes/               타이틀, 로비, 레이스, 트랙별 장식 씬, 테스트 작업대
art-src/                Blender 원본과 FBX 후보
docs/                   ARCHITECTURE(구조) · DECISIONS(결정 기록) · DESIGN(로드맵) · HANDOFF(다음 작업) · RULES/(작업별 규칙)
index/, tools/          심볼 인덱스, 인덱스 재생성·테스트 선택·에이전트 동기화 스크립트
```

## 문서

| 문서 | 내용 |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | 시스템 지도, 진입점, 검증 기록 |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | 결정과 그 이유 |
| [`docs/DESIGN.md`](docs/DESIGN.md) | 최종 목표와 마일스톤 |
| [`docs/HANDOFF.md`](docs/HANDOFF.md) | 다음 세션이 이어받을 일 |
| [`CLAUDE.md`](CLAUDE.md) | 항상 지킬 작업 규칙 |
| [`docs/RULES/`](docs/RULES/) | 작업별 규칙(형태·검증·조회·서브에이전트·브랜치). 언제 읽는지는 `CLAUDE.md` 맨 위 표 |
