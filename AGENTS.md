# ProtoHarness — 에이전트 지침 (Codex 진입점)

## 규칙 본문은 여기 없다

**작업 전에 `CLAUDE.md` 를 읽어라.** 금지선(§0), 목적·경로·형태(§1), 기존 자산 우선(§2),
구현 전 합의(§3), 추측 금지·실증 의무(§4), 실패는 시끄럽게(§5), 조회 모드(§6),
서브에이전트(§7), 응답 형식(§8) 이 전부 거기 있다.
작업별 절차(형태 규칙·검증·인덱스 조회·서브에이전트 운영·브랜치 이름)는 `docs/RULES/` 에 있다.
이것도 사본이 아니라 CLAUDE.md 에서 **옮겨 간 일부**이며, 언제 무엇을 읽는지는 `CLAUDE.md` 맨 위 표가 정한다.

이 파일은 **일부러 사본이 아니다.** 같은 규칙의 사본을 둘로 두면 반드시 어긋난다.
실제로 2026-08-25에 사본을 만든 지 3분 만에 §3-1 개정이 한쪽에만 반영되어 갈라졌다.
→ `docs/DECISIONS.md`

**규칙을 고칠 때는 그 규칙이 있는 한 곳(`CLAUDE.md` 또는 `docs/RULES/`)만 고친다.** 이 파일에 규칙을 옮겨 적지 마라.

---

## Codex 전용 사항

### 에이전트 정의

`.codex/agents/*.toml` 은 **생성물이다. 직접 수정하지 마라.**

- 원본: `.claude/agents/*.md`
- 재생성: `powershell -ExecutionPolicy Bypass -File tools/sync-agents.ps1`
- 어긋남 검사: 같은 명령에 `-Check` (일치 0 / 어긋남 1 로 종료)

에이전트를 고치려면 `.claude/agents/<이름>.md` 를 고치고 위 명령을 돌린다.

### 확인한 것 (2026-10-26, 공식 문서 `learn.chatgpt.com/docs/extend/mcp`·`/docs/hooks` 와 이 머신의 설정)

| 항목 | 내용 |
|---|---|
| MCP 설정 위치 | 사용자 `~/.codex/config.toml` 의 `[mcp_servers.<이름>]`. Codex 앱·CLI·IDE 확장이 같이 쓴다. 프로젝트 `.codex/config.toml` 은 신뢰된 프로젝트에서만 읽힌다. 지금 무엇이 붙어 있는지는 `codex mcp list` 로 구한다(머신마다 다르다, CLAUDE.md 맨 위) |
| MCP 도구 승인 | `[mcp_servers.<이름>.tools.<도구>] approval_mode` — 값은 `auto`·`prompt`·`writes`·`approve`. `Unity_RunCommand` 는 `prompt`(매번 묻는다)로 둔다. CLAUDE.md §0 "상태를 바꾸는 코드는 승인 후에만" |
| 훅 | Codex 에도 `PreToolUse` 훅이 있다(`.codex/hooks.json`, 신뢰된 프로젝트에서 `/hooks` 로 신뢰한 훅만 돈다). 셸은 `tool_name: Bash`, 파일 편집은 전부 `apply_patch` 하나로 오고 경로는 `tool_input.command` 의 패치 본문에 있다. `permissionDecision: "ask"` 는 지원하지 않는다(오류를 내고 호출을 계속한다). 이 레포는 `.codex/hooks.json` 으로 `tools/hooks/guard.ps1` 을 `Bash`·`apply_patch` 에 건다 — 되돌릴 수 없는 git 명령·`main`/`dev` 커밋·보호 파일 편집을 **deny** 한다(Claude 쪽은 보호 파일이 ask). 훅을 고치면 해시가 바뀌어 `/hooks` 로 다시 신뢰해야 돈다 |

### 확인 못 한 것 — 단정하지 마라 (§4-1)

| 항목 | 상태 |
|---|---|
| `tools` / `model` 권한 제한을 Codex 에이전트 TOML 에서 표현하는 키 | **확인 못 함.** `sync-agents.ps1` 은 이 둘을 **주석으로만** 남긴다. 즉 Codex 쪽에서는 도구 권한이 **강제되지 않는다** |
| `approval_mode` 의 `auto`·`approve` 가 정확히 무엇을 하는지 | **확인 못 함.** 문서가 뜻을 적은 값은 `writes`("읽기 전용 표시가 없는 도구는 묻는다")뿐이다 |
| `/codex:review` 가 이 파일과 `CLAUDE.md` 를 읽고 리뷰하는지 | **확인 못 함.** 규칙 체크리스트가 필요하면 `/codex:adversarial-review` 초점에 적는다 (`docs/RULES/SUBAGENTS.md` §7-5) |

위 표의 첫 항목이 중요하다. `unity-verifier` 는 Claude Code 에서 `tools:` 로 쓰기 도구가 구조적으로 차단되지만,
**Codex 에서는 그 차단이 걸려 있지 않다.** 그러니 Codex 로 검증 에이전트를 돌릴 때는
`developer_instructions` 의 금지 조항이 유일한 방어선이다. 더 강한 보장이 필요하면 Codex 쪽 권한 설정을 먼저 확인하라.
