# 브랜치 이름과 주의점 — CLAUDE.md §9-1 · §9-3

> **CLAUDE.md 의 일부다. 구속력은 CLAUDE.md 와 같다.** § 번호는 CLAUDE.md 의 번호를 그대로 쓴다.
> **읽는 시점**: 브랜치를 만들거나 병합하기 전.
> 브랜치 구조와 **규칙(§9-2: 직접 커밋 금지, `--no-ff`, 병합 조건)** 은 CLAUDE.md §9 에 있다.
> 이 내용을 고칠 때는 이 파일만 고친다. CLAUDE.md 에 사본을 두지 않는다.

### 9-1. 이름

| 접두사 | 용도 | 예 |
|---|---|---|
| `feature/` | 기능 | `feature/p1-fixed-tick` |
| `fix/` | 버그 수정 | `fix/grapple-release-boost` |
| `docs/` | 문서·규칙 | `docs/branch-strategy` |
| `spike/` | 버려도 되는 실험 | `spike/netcode-ngo-vs-fusion` |

### 9-3. 알아둘 것

- 에디터가 열린 채로 브랜치를 전환하면 재임포트가 일어난다. 전환은 에디터가 한가할 때 한다.
- Unity 가 자동으로 바꾸는 파일(`ProjectSettings/`, `ProtoHarness.slnx`, `.codex` 줄바꿈)은 브랜치 전환 때 같이 따라온다.
- `.gitattributes` 는 `.unity/.prefab/.asset/.mat` 에 `merge=unityyamlmerge` 를 지정하지만 git config 에 병합 드라이버가 **등록되어 있지 않다.** 임시 저장소 실험(2026-10-02)에서 미등록 드라이버는 기본 텍스트 병합으로 되돌아가 **일반 충돌 마커를 남겼다.** 그래서 씬·프리팹이 충돌하면 마커를 손으로 고치지 않고 병합을 중단(`git merge --abort`)한 뒤 에디터에서 다시 작업한다. 드라이버 등록은 검증 없이 하지 않는다.
- GitHub 브랜치 보호는 설정하지 않았다 (2026-10-02 결정, 단독 개발).
