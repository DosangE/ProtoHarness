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
- **게이트 전체 PlayMode** (§9-2 ③ 마일스톤에서만, 2026-10-30 "테스트 범위"): 메뉴 `ProtoHarness > Chain Rush > Run PlayMode Tests` (`Device`·`Enemy`·`NetEndReplay` 제외). EditMode 는 `TestRunnerApi` 로 `categoryNames = { "!Enemy" }` 를 주고 돌린다.
- **① 빠른 확인과 ② 병합** (§9-2): 바뀐 기능을 덮는 테스트만 돌린다(2026-10-30 "테스트 범위"). `powershell -ExecutionPolicy Bypass -File tools/select-tests.ps1` 는 참고로 돌린다 — 기본은 `dev...HEAD` + 커밋하지 않은 변경 + 추적하지 않는 파일이고(①), `-Range <a>..<b>` 로 바꿀 수 있다. 병합(②)은 커밋한 범위 `-Range dev...HEAD`. 도구가 목록을 내면 출력된 일반 선택·Lap 선택의 각 `ScriptableObject.CreateInstance<TestRunnerApi>().Execute(...)` 를 `Unity_RunCommand` 로 부른다. **"전체" 가 나오면 위 메뉴(게이트 전체)를 쓰지 않고**, 아래 영향 평가로 바뀐 기능의 테스트를 골라 `groupNames`(정규식, 예 `"ChainRushFlowTests\\.BoostZone_"`) 또는 `testNames`(전체 이름)로 부른다. 시뮬을 바꿨으면 서킷 지문(`ChainRushReplayTests.Replay_CircuitBotRun_RepeatsEveryTickBitForBit`)과 바뀐 곳을 지나는 롤백 테스트를 넣는다. 결과 XML 은 메뉴 실행과 같은 파일에 쓰인다. 보고에는 고른 목록과 이유, XML 값(도구를 돌렸으면 그 출력도)을 붙인다.
  - **다시 돌리는 범위는 마지막 통과 이후 바뀐 것** (사용자 2026-11-09, `docs/DECISIONS.md` 2026-11-09 "테스트 범위: 마지막 통과 이후"): 테스트를 걸기 전에 한 줄을 먼저 쓴다 — 마지막 통과 실행(시각·XML 값) → 그 뒤 `Assets/` 에서 바뀐 파일 → 그것을 읽는 테스트. 바뀐 파일이 없으면 돌리지 않고 그 실행의 값을 쓴다(병합 때는 CLAUDE.md §9-2 "작업 중 실행을 다시 쓴다"). `dev...HEAD` 범위와 도구 출력은 병합 보고의 영향 평가용이고, 다시 돌릴 범위를 정하는 데 쓰지 않는다.
  - **실행 기록** (`docs/DECISIONS.md` 2026-11-09 "테스트 실행 기록"): 실행을 걸기 직전 시각을 적어 두고, 끝나면 바로 `& .\tools\test-runs.ps1 -Record -Xml <결과 XML> -Filter "<돌린 Filter 코드 또는 메뉴 이름>" -StartedAt <그 시각>` 로 남긴다. 실패한 실행도 남긴다. batch 실행이면 `-EditorLog <-logFile 경로>` 를 준다. 위 한 줄의 "마지막 통과 실행" 과 "그 뒤 바뀐 파일" 은 `-Last -Passed` 출력에서 옮긴다(EditMode·PlayMode 를 나눠 보려면 `-Platform`). `-Record` 가 거부하면(XML 이 없거나 이번 실행의 것이 아니면) 그 실행은 기록이 없는 것이고, 거부 원문을 보고한다. 기록은 이 PC 에만 있고 `Assets/` 만 비교한다. 기록이 없으면 DECISIONS 검증 줄과 영향 평가로 정한다.
  - **Lap 선택 (사용자 2026-11-02)**: ① 작업 중·② 병합에서 `Lap` 카테고리는 **맵 구조나 주행 결과를 바꾸는 변경이 있을 때만**, **바뀐 트랙의 칸만** 돌린다(공용 주행 코드면 전 트랙). 대상은 `Assets/_Project/Data/Circuit_*.asset`, `Assets/_Project/Scripts/Editor/ChainRush/ChainRushCircuitSceneBuilder.cs` 의 트랙 생성, `Assets/_Project/Scripts/Runtime/ChainRush/Track/` 의 트랙 좌표·도로 메시/충돌·랩 판정, `Race/CircuitScenery.cs` 의 `LayRoad`·`LayMainRoad`·`LayRange`·`LayPiece`·`LayBay`·`LayDeck`·`LayShortcut` 및 가드 틈 계산, `Race/CircuitRace.cs` 의 트랙/앵커/랩 구성, `RunnerMotor.cs`·`GrappleController.cs`·`Motion/`·`ChainRushGame.cs`·`RacerState.cs`·`Ticks.cs`·`RunRules.cs`·`CourseTarget.cs`·`SimSnapshot.cs` 의 이동/상태 처리, `RunnerProfile.cs`/`Assets/_Project/Data/RunnerProfile_*.asset`의 이동 값, `Control/` 의 컨트롤러 입력 생성·소비, `Flow/RaceAssembler.cs`·`Flow/RaceSetup.cs`·`Flow/SpeedScale.cs` 의 트랙·속도·프로필 전달이다(Race/부터의 상대 경로는 `Assets/_Project/Scripts/Runtime/ChainRush/` 기준). 경로에 닿았다는 이유만으로 고르지 말고 바뀐 코드가 주행에 들어가는지 확인한다. 트랙 빌더에서 한 트랙 생성만 바뀌었으면 그 트랙만이다.
    장식 빌더·장식 씬/프리팹, `Race/TrackLook.cs`·`Assets/_Project/Data/TrackLook_*.asset`, `CircuitScenery.PaintMarkings` 와 그 표현 전용 헬퍼, 차선 칠·머티리얼·텍스처·UV만 바꿨으면 **Lap을 돌리지 않는다**. 해당 장식 빌더의 침범 검사와 영향받는 구조/표현 테스트로 본다. 일반 클래스 선택은 `categoryNames = { "!Device", "!Enemy", "!NetEndReplay", "!^Lap$" }` 로 빼고, 필요한 랩 칸은 별도 `Filter` 에 `categoryNames = { "^Lap$" }` 와 트랙을 한정한 `groupNames`/`testNames` 를 함께 준다(필터 종류끼리 AND). 도구의 Lap 출력은 경로 기반 후보이므로 빌더·공용 파일의 표현만 바뀐 diff는 이 기준으로 제외/축소하고 근거를 보고한다. `Race_FinishFromTheLobby_KeepsTheBestAndItsGhost_AndRunAgainRacesIt` 는 기록·결승 계약을 위한 별도 테스트로 유지하며, 그 기능 변경 때 고른다. **③ 마일스톤 게이트는 변경 없이 Lap 포함 전체**다. 결정 이유·참조 grep 근거는 `docs/DECISIONS.md` 2026-11-02 "테스트: 랩 런 하나로".
  - 고르는 방식: 한 `Filter` 안에서 **같은 종류의 값끼리는 OR**(test-framework 패키지 `RuntimeTestRunnerFilter.cs:81,85`), 단 `!` 로 뺄 값끼리는 AND(같은 `AddFilters`, 2026-10-18 확인), **필터 종류(`testNames`·`groupNames`·`assemblyNames`·`categoryNames`)끼리는 AND**(같은 파일 `:43`)다. 그래서 고른 클래스와 지문 테스트를 `groupNames` 한 배열에 넣고, 위 Lap 선택 항목의 일반 제외 카테고리를 함께 준다.
  - 2026-10-14 확인(에디터 6000.3.19f1, `dev` 와 같은 `Assets/`): `-Range 75df74d^..75df74d` 출력(클래스 3개 + 지문)을 그대로 실행 → `testcasecount="18" result="Passed" passed="18" failed="0" skipped="0"`, 78.4초. 고른 3개 클래스의 17건과 지문 1건만 돌았고, 같은 클래스의 다른 리플레이 테스트 2건은 돌지 않았다. 보고에는 XML 의 테스트 목록이 도구 출력과 맞는지도 적는다.
  - **영향 평가** (`docs/DECISIONS.md` 2026-10-26 "영향 범위 테스트"): 표는 "바뀐 파일 → 그것을 읽는 코드 → 그 코드를 덮는 테스트" 로 정한다. 시뮬 코드가 참조하지 않는 경로는 지문을 넣지 않고(`Fingerprint = $false`), 클래스 일부만 덮으면 테스트 이름 접두사로 고른다(`Tests`). 병합 보고에는 영향 평가 한 줄(바뀐 타입 → 읽는 곳 `경로:줄` → 고른 테스트)을 적는다. 도구가 "전체" 가 아닌 목록을 내면 그것이 **하한**이다. 영향 평가에서 더 필요한 테스트가 보이면 더해 돌리고 이유를 적는다. 도구가 "전체" 로 판정한 경우의 범위는 영향 평가가 정하고, 다른 트랙·기능으로 번진 회귀는 §9-2 ③ 의 게이트 전체가 잡는다. 빼려면 표를 고치는 합의를 한다. 표에서 경로를 나누거나 지문을 뺄 때는 시뮬 참조를 grep 으로 확인한 근거를 DECISIONS 에 남긴다.
  - 테스트 파일을 고치면 바뀐 줄이 모두 테스트 메서드 안일 때 그 메서드만 고르고, 그 밖이거나 확정할 수 없으면 클래스 전체를 고른다(도구가 판단한다. 새 메서드 전체를 추가한 hunk 앞뒤의 빈 줄만 그 메서드에 포함한다).
  - 경로 → 테스트 클래스 표는 도구 안에 있다. PlayMode 테스트 클래스를 새로 만들면 표에 배정한다. 배정하지 않으면 도구가 경고하고 전체로 떨어진다.
  - `categoryNames` 는 **정규식**으로 비교한다(같은 파일 `:31`). `"Net"` 은 `NetRace` 에도 걸려 `[Explicit]` 네트워크 레이스까지 돈다. `Net` 카테고리만 고를 때는 `"^Net$"` 로 쓴다.
- 버그를 고쳤다고 말하려면, **고치기 전에 실패하는 테스트**가 있어야 한다.
