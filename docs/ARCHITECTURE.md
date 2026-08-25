# ProtoHarness 아키텍처

> **A모드 진입점.** 구조·시스템·진입점을 묻는 질문은 코드를 뒤지기 전에 이 문서부터 읽는다.
> 이 문서가 코드와 다르면 **문서가 틀린 것**이다. 발견 즉시 고치고 §5대로 알린다.

**최종 갱신**: 2026-08-25 · **대상 커밋**: b64f408

---

## 1. 한 줄 정의

Unity 6 URP 기반 3D 하네싱(에이전트 운용) 연습 프로젝트. 게임 자체보다 **에이전트가 규칙 안에서 안전하게 작업하는 절차**를 검증하는 것이 목적이다.

## 2. 현재 상태 — 코드 없음

| 항목 | 수 |
|---|---|
| 우리 런타임 스크립트 | **0** |
| 우리 에디터 스크립트 | **0** |
| 우리 테스트 | **0** |
| 템플릿 잔재 | `Assets/TutorialInfo/Scripts/` 2개 (건드리지 않음) |
| 씬 | `Assets/Scenes/SampleScene.unity` (URP 템플릿 기본) |

**첫 스크립트가 곧 표준이 된다.** → `CLAUDE.md` §2

## 3. 기술 스택 (확정)

| 층 | 선택 | 비고 |
|---|---|---|
| 엔진 | Unity 6000.3.18f1 | |
| 렌더 | URP 17.3.0 | 설정은 `Assets/Settings/` — Unity 템플릿 소유 |
| 입력 | Input System 1.19.0 | **신 입력 시스템.** `Input.GetKey` 금지 |
| 테스트 | Test Framework 1.6.0 | EditMode/PlayMode 둘 다 사용 예정 |
| 직렬화 | Newtonsoft Json 3.2.1 | 패키지 의존으로 이미 존재 |

## 4. 시스템 지도

> 시스템이 생기면 여기에 **1개당 3줄**로 적는다: 책임 한 줄 / 진입점 파일 / 의존 시스템.
> 3줄을 넘기면 `docs/SYSTEMS/<이름>.md` 로 분리하고 여기엔 링크만 남긴다.

_(아직 없음)_

## 5. 진입점

| 무엇 | 어디 |
|---|---|
| 작업 규칙 | `CLAUDE.md` |
| 결정 이력 | `docs/DECISIONS.md` |
| 심볼 인덱스 (B모드) | `index/symbols.tsv` — 생성물, `tools/reindex.ps1` 로 재생성 |
| 서브에이전트 정의 | `.claude/agents/*.md` (5종) |
| 인덱스 재생성 | `tools/reindex.ps1` |
| 부팅 씬 | 미정 |

## 6. 경계

- `Assets/_Project/` — 우리 것
- `Assets/Settings/`, `Assets/TutorialInfo/` — 템플릿 소유, 수정 금지
- `docs/`, `index/`, `tools/`, `.claude/` — Unity 바깥 도구 영역. **Assets 안으로 옮기면 Unity가 컴파일한다**
