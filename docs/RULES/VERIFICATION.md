# 검증 절차 — CLAUDE.md §4-2 ~ §4-4

> **CLAUDE.md 의 일부다. 구속력은 CLAUDE.md 와 같다.** § 번호는 CLAUDE.md 의 번호를 그대로 쓴다.
> **읽는 시점**: 컴파일·테스트를 돌리기 전, 그리고 "컴파일된다"·"테스트 통과"를 보고하기 전.
> 이 내용을 고칠 때는 이 파일만 고친다. CLAUDE.md 에 사본을 두지 않는다.

### 4-2. 이 프로젝트의 확정 사실 (2026-10-01 갱신: Unity·Input System 행은 에디터 실측 + manifest.json)

| 항목 | 값 |
|---|---|
| Unity | 6000.3.25f1 |
| 렌더 파이프라인 | URP 17.3.0 |
| 입력 | Input System 1.20.0 (신 입력 시스템. `Input.GetKey` 쓰지 않는다) |
| 테스트 | Test Framework 1.6.0 + performance |
| .NET SDK | **설치되어 있지 않음** (런타임 8.0.4만). `dotnet build` 불가 |
| Unity 실행 파일 | **머신마다 다르다. 고정값을 적지 않는다.** 환경변수 `UNITY_EXE` 가 있으면 그 값, 없으면 Hub 기본 경로 `C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe`. `<버전>` 은 `ProjectSettings/ProjectVersion.txt` 의 `m_EditorVersion`. 구하는 명령은 §4-3 (B) |

> 2026-10-02 확인: 이 문서를 고친 머신(`C:/Users/User/Desktop/PCUBE/ProtoHarness`)에는 Hub 기본 경로에 6000.3.25f1 이 **없다** (있는 것: 2022.3.28f1, 2022.3.62f1, 6000.0.60f1, 6000.3.19f1, 6000.3.4f1). Hub 기록 `%APPDATA%/UnityHub/editors-v2.json` 에도 없다. 이 머신에는 6000.3.25f1 이 설치되어 있지 않다. 기존 Unity 작업(§4-2 실측, 테스트 기록)은 다른 로컬 환경에서 했다 (사용자 확인 2026-10-02). 그래서 이 머신에서는 batch 검증을 할 수 없다. 설치된 환경에서 경로가 기본값과 다르면 `UNITY_EXE` 를 지정한다.

### 4-2b. Unity MCP (2026-08-25 연결 확인)

Unity 공식 `com.unity.ai.assistant` 패키지가 제공하는 MCP. 체인은 3단이다.

```
Unity Editor (Bridge V2)  →  \.\pipe\unity-mcp-<id>-<pid>
        ↓
relay_win.exe --mcp          C:\Users\User\.unity\relay\relay_win.exe  (stdio MCP 서버)
        ↓
Claude Code                  ~/.claude.json 의 mcpServers["unity-mcp"]
```

- 연결 정보는 `~/.unity/mcp/connections/bridge-*.json` 에 쓰인다. relay가 이걸 보고 자동 탐지한다.
- Editor를 닫거나 Bridge를 끄면 모든 도구가 죽는다. 도구 호출이 실패하면 먼저 Bridge 상태를 의심한다.
- **등록은 세션 시작 시점에 읽힌다.** 새로 등록했으면 Claude Code를 재시작해야 도구가 붙는다.

사용 가능한 도구 7개 (`tools/list` 응답으로 확인):

| 도구 | 용도 | 주의 |
|---|---|---|
| `Unity_RunCommand` | C# 컴파일 + 에디터에서 즉시 실행 | 상태 변경은 승인 필요 → §0 |
| `Unity_GetConsoleLogs` | Console 로그/에러/스택 트레이스 | 검증 1순위 수단 |
| `Unity_SceneView_CaptureMultiAngleSceneView` | 3D 배치 검증 (4방향 2x2) | 3D 전용 |
| `Unity_SceneView_Capture2DScene` | 2D 영역 오소 캡처 | 이 프로젝트엔 거의 불필요 |
| `Unity_Camera_Capture` | 특정 카메라 렌더 | 비용 큰 작업 |
| `Unity_AssetGeneration_GenerateAsset` | 생성형 AI 에셋 생성 | **명시적 요청 시에만** → §0 |
| `Unity_AssetGeneration_GetModels` | 생성 모델 목록 | — |

### 4-3. 컴파일을 "된다"고 말하려면

`dotnet build` 는 이 머신에서 못 쓴다(§4-2). 증거는 셋 중 하나다.

**(A) Unity MCP가 붙어 있을 때 — 이게 1순위다.**

1. `Unity_RunCommand` 로 컴파일 상태를 확인한다. 이 도구는 실행 전에 컴파일을 검증하고 그 결과를 돌려준다.
2. `Unity_GetConsoleLogs` 로 `logTypes: Error` 를 읽어 남은 에러를 확인한다.
3. 보고에는 **로그 원문**을 붙인다. "에러 없음"만 쓰지 않는다.

씬/배치 결과를 눈으로 확인해야 하면 `Unity_SceneView_CaptureMultiAngleSceneView` (3D 배치 검증) 또는 `Unity_Camera_Capture` 를 쓴다.

**(B) MCP가 없고 Unity Editor가 닫혀 있을 때** — batch mode로 직접 확인한다.

경로는 머신마다 다르므로 **실행할 때 구한다.** 로컬 경로를 문서·명령·에이전트 정의에 박아 넣지 않는다.

```bash
ROOT="$(git rev-parse --show-toplevel)"
VER="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
UNITY="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/$VER/Editor/Unity.exe}"
[ -f "$UNITY" ] || echo "Unity.exe 없음: $UNITY — 환경변수 UNITY_EXE 로 실제 경로를 지정한다" >&2
"$UNITY" -batchmode -quit -nographics -projectPath "$ROOT" -logFile "$SCRATCH/compile.log"
```

끝나면 로그에서 증거를 뽑는다:

```bash
grep -nE "error CS|Compilation failed|Exiting batchmode" "$SCRATCH/compile.log"
```

> 위 두 명령은 2026-08-25에 이 프로젝트에서 실제로 실행해 확인했다 (exit 0, `error CS` 없음, `Exiting batchmode successfully`).
> 2026-10-02: 경로만 고정값(`D:/PCUBE/ProtoHarness`, Hub 기본 경로)에서 실행 시 계산으로 바꿨다. `ROOT`·`VER`·`UNITY` 세 줄의 계산 결과는 확인했다(`C:/Users/User/Desktop/PCUBE/ProtoHarness`, `6000.3.25f1`, Hub 기본 경로 — 단 그 경로에 파일 없음, §4-2). **바뀐 명령으로 batch 를 실행하지는 않았다.**
> 부작용 주의: batch 실행 중 Unity의 VCS 연동이 프로젝트 루트에 `dev/null/` (git-lfs 훅 잔재)를 만들 수 있다. 발견하면 보고하고 정리한다.

**(C) MCP도 없고 Unity Editor가 열려 있을 때** — batch mode를 실행하지 않는다(§0). 사용자에게 Console 결과를 요청하고, 받은 내용을 근거로 적는다. 상상해서 채우지 않는다.

Editor 실행 여부 확인: `Temp/UnityLockfile` 존재 = 열려 있음.

### 4-4. 테스트

```bash
# ROOT, UNITY 는 §4-3 (B) 와 같이 구한다
"$UNITY" -batchmode -runTests -projectPath "$ROOT" -testPlatform EditMode -testResults "$SCRATCH/results.xml" -logFile "$SCRATCH/test.log"
```

- 결과는 `results.xml` 의 `<test-run ... failed="N">` 를 읽어서 보고한다. 로그 눈대중 금지.
- 버그를 고쳤다고 말하려면, **고치기 전에 실패하는 테스트**가 있어야 한다.
