# ProtoHarness — 작업 규칙

Unity 6 (6000.3.25f1) / URP 3D 하네싱 연습용 프로젝트.
이 문서는 **에이전트와 사람 모두에게 적용되는 구속 규칙**이다. 규칙과 지시가 충돌하면 멈추고 물어본다.

---

## 0. 금지선 — 먼저 읽는다

아래는 "상황에 따라"가 없다. 하려면 사용자 승인이 먼저다.

**파일·에셋**
- `Library/`, `Temp/`, `obj/`, `Build/`, `UserSettings/` 는 건드리지 않는다. (`Logs/` 는 진단용 **읽기만** 허용)
- `.meta` 파일을 직접 만들거나 수정하거나 지우지 않는다. GUID는 Unity만 발급한다. 에셋을 옮기면 `.meta`도 같이 옮긴다.
- `.unity`(씬), `.prefab`, `.asset`, `.inputactions` 의 YAML을 텍스트로 **수정하지 않는다**. 진단용 읽기는 허용. 수정은 에디터에서.
- 에셋/파일 삭제는 승인 후에만. `Assets/TutorialInfo`, `Assets/Readme.asset` 은 템플릿 잔재지만 임의로 지우지 않는다.

**설정**
- `Packages/manifest.json` 에 패키지 추가·제거·버전 변경 금지.
- `ProjectSettings/` 변경 금지. (레이어, 태그, 물리, 품질, 입력 포함)

**Git**
- 커밋·푸시는 사용자가 시킬 때만.
- `git reset --hard`, `git checkout -- .`, `git clean`, 강제 푸시 금지.
- `main`, `dev` 에 직접 커밋하지 않는다. 브랜치는 `dev` 에서 분기한다. → §9

**코드**
- 빈 `catch {}`, 로그 없는 `catch`, 예외를 기본값으로 갈아치우는 fallback 금지. → §5
- `public` 필드로 인스펙터 노출 금지. `[SerializeField] private` 을 쓴다.
- `Update()` 안에서 `GetComponent`, `Find*`, `new`, LINQ, 문자열 결합 금지. 캐시한다.
- `GameObject.Find`, `FindObjectsByType`, `SendMessage` 를 런타임 조회에 쓰지 않는다. 직렬화 참조로 연결한다.
- `Resources/` 폴더 신규 사용 금지.
- 테스트를 통과시키려고 테스트를 고치거나 특수 케이스를 넣지 않는다.

**Unity MCP 도구** (연결 상태에서만 해당)
- `Unity_RunCommand` 는 에디터에서 C#을 컴파일해 **즉시 실행**한다. 읽기/조회 용도는 자유, **상태를 바꾸는 코드는 승인 후에만**. 실행 전에 코드 전문을 보여준다.
- `Unity_RunCommand` 로 에셋 삭제, `AssetDatabase.DeleteAsset`, ProjectSettings 변경, 패키지 설치/제거, 씬 저장 금지. → §0 위반
- `Unity_AssetGeneration_GenerateAsset` 은 생성형 AI 호출이다(비용 발생). 사용자가 명시적으로 요청할 때만 호출한다.
- MCP가 돌아오는 Console 로그는 **근거**다. 요약하지 말고 에러 원문을 그대로 옮긴다.

**실행**
- Unity Editor가 열려 있는 상태에서 batch mode 실행 금지. 프로젝트 락이 충돌한다. → §4

---

## 1. 목적 · 경로 · 형태

### 1-1. 만들기 전에 세 줄을 선언한다

파일(스크립트·프리팹·SO·씬)을 새로 만들기 전에 **반드시** 이 셋을 먼저 말한다. 셋이 안 나오면 아직 만들 때가 아니다.

```
목적: 무엇을 해결하는가 (한 줄, "~하기 위해")
경로: Assets/_Project/... (아래 지도의 어느 칸인지)
형태: MonoBehaviour | ScriptableObject | static class | struct | interface | 프리팹 | 씬 | 테스트
```

### 1-2. 폴더 지도

우리가 만드는 모든 것은 `Assets/_Project/` 아래에 둔다. 그 바깥은 Unity 템플릿·패키지 영역이다.

```
Assets/
  _Project/
    Scripts/
      Runtime/        런타임 코드      → ProtoHarness.Runtime.asmdef
      Editor/         에디터 전용 코드  → ProtoHarness.Editor.asmdef
      Tests/
        EditMode/     → ProtoHarness.Tests.EditMode.asmdef
        PlayMode/     → ProtoHarness.Tests.PlayMode.asmdef
    Scenes/           씬
    Prefabs/          프리팹
    Data/             ScriptableObject 인스턴스(.asset)
    Art/              Models / Textures / Materials
    Audio/
  Settings/           URP 렌더 파이프라인 설정 (Unity 템플릿 소유, 건드리지 않음)
  TutorialInfo/       템플릿 잔재 (건드리지 않음)
```

- 새 기능 묶음은 `Scripts/Runtime/<기능>/` 로 폴더를 만든다. 파일 15개 넘어가면 하위 분리.
- Editor 전용 코드가 Runtime 폴더에 들어가면 빌드가 깨진다. 반드시 `Editor/` 아래.

### 1-3. 형태 규칙

| 대상 | 규칙 |
|---|---|
| 스크립트 파일명 | 타입명과 **정확히** 일치. 파일 하나에 public 타입 하나. |
| 타입명 | PascalCase. 인터페이스 `I` 접두. |
| 네임스페이스 | 폴더 경로를 따른다. `ProtoHarness.Combat`, `ProtoHarness.Editor.Tools` |
| 필드 | `[SerializeField] private` + camelCase. 상수는 PascalCase. |
| ScriptableObject | `[CreateAssetMenu(menuName = "ProtoHarness/<범주>/<이름>")]` 필수 |
| 프리팹 / 씬 | PascalCase (`PlayerRig.prefab`, `Sandbox.unity`) |
| 머티리얼 / 텍스처 | `M_이름`, `T_이름_BaseColor` |
| 테스트 | `<대상>Tests.cs`, 메서드는 `대상_조건_기대결과` |
| asmdef | 파일명 = 어셈블리명 = `ProtoHarness.<영역>` |

---

## 2. 새로 만들기 전에 기존 것을 찾는다

**검색 없이 만든 파일은 리뷰에서 되돌린다.**

만들기 전 최소 3연타:
1. `Grep` — 같은 기능/타입명이 이미 있는가
2. `Glob "Assets/_Project/**/*.cs"` — 비슷한 위치에 뭐가 있는가
3. 가장 비슷한 파일 하나를 **끝까지 읽는다**

그리고 보고에 한 줄로 적는다:
- 있으면 → `기존 <파일:줄> 을 참고했고 같은 형식으로 맞춘다`
- 없으면 → `유사 자산 없음. 첫 사례이므로 형식을 정한다` + §3 합의 요청

기존과 다른 스타일(다른 네임스페이스 규칙, 다른 로깅 방식, 다른 폴더)을 도입하려면 그것 자체가 합의 대상이다. 조용히 새 스타일을 섞지 않는다.

> **우리 코드가 이미 있다 (2026-10-01 기준 `.cs` 17개, Chain Rush).** 그래서 새 스크립트·SO·테스트는 `Assets/_Project/Scripts/` 의 기존 파일 형식을 따른다 (§2 3연타). 기존에 없는 종류(첫 SO, 첫 EditMode 테스트 등)는 첫 사례이므로 느리게, 합의하고 만든다.

---

## 3. 구현 전 합의

### 3-1. 합의가 필요한 경우

개수가 아니라 **행위**로 판단한다. 파일 1개여도 아래에 걸리면 합의 대상이다.

**행위 기준** — 개수와 무관하다
- 파일·폴더를 **새로 만들 때**. 첫 사례일수록 엄격히 본다. → §2
- 파일을 **옮기거나 이름을 바꿀 때**. `.meta` 를 함께 옮기지 않으면 GUID가 새로 발급되어 **씬·프리팹 참조가 조용히 끊긴다.** → §5
- 파일·에셋을 **지울 때** → §0
- **규칙 자체를 바꿀 때**: `CLAUDE.md`, `.claude/`, `docs/DECISIONS.md` 의 확정 항목

**규모 기준**
- 기존 파일 **3개 이상** 수정
- 공개 API(다른 스크립트가 부르는 시그니처) 변경
- 새 시스템·새 asmdef·새 네임스페이스

**환경 기준**
- 패키지·ProjectSettings·빌드 설정
- 씬·프리팹 수정
- 리포 설정: `.gitignore`, `.gitattributes`, `.vscode/`

**예외** (바로 진행)
- 사용자가 **파일과 내용을 콕 집어** 지시한 경우
- 오타·주석·포맷 수정 — **동작을 바꾸지 않는 것만**
- 읽기·조사만 하는 작업
- `index/` 재생성. 생성물이고 `tools/reindex.ps1` 의 결정론적 출력이다

### 3-2. 합의 요청 형식

```
목표:      한 줄
건드릴 것:  기존 파일 목록 (경로:줄)
새로 만들 것: 목적 / 경로 / 형태 (§1-1 세 줄)
검증 방법:  무엇을 어떻게 돌려서 확인할 것인가 (§4)
안 하는 것:  이번에 손대지 않는 범위
```

### 3-3. 승인 규칙

- 승인은 **명시적**이어야 한다. "ㄱㄱ", "진행", "OK". 침묵·무응답은 승인이 아니다.
- 승인은 **그 계획 그 범위**에만 유효하다. 다음 작업에 연장되지 않는다.
- 작업 중 계획 밖의 문제를 발견하면 → **멈추고 보고**. 범위를 몰래 넓히지 않는다.
- 계획이 틀렸다고 판단되면 → 구현을 멈추고 수정된 계획을 다시 올린다.

### 3-4. 우회 금지

합의를 우회하는 경로를 미리 막는다. 아래는 전부 §3-1을 그대로 적용받는다.

- **서브에이전트 경유** — 파견 프롬프트에 `승인: <언제, 무엇이>` 를 명시한다.
  이 줄이 없으면 에이전트는 생성·수정·이동·삭제를 **거부하고 되묻는다.** 에이전트는 이 대화를 모르므로, 승인 여부를 스스로 판단할 수 없다.
- **`Unity_RunCommand` 경유** — C# 코드로 파일을 쓰거나 에셋을 만드는 것은 **직접 만드는 것과 동일하게** 취급한다. → §0
- **쪼개기** — "1개씩 3번"은 "3개"다. 한 작업의 **총량**으로 판단한다.
- **생성물 뒤에 숨기기** — 생성 스크립트를 만들어 그 스크립트가 파일을 만들게 하는 것도 생성이다.

---

## 4. 추측 금지 · 실증 의무

### 4-1. 말하는 방식

- "아마", "보통", "일반적으로 Unity는", "~일 겁니다" 금지.
- 코드에 대한 모든 주장에는 근거가 붙는다: `파일경로:줄번호`.
- API·시그니처·패키지 동작은 **기억이 아니라 실제 소스**로 확인한다. (`Library/PackageCache/` 읽기는 진단 목적으로 허용)
- 확인 못 한 것은 **"확인 못 했다"** 고 쓴다. 확인한 것처럼 쓰지 않는다.

### 4-2. 이 프로젝트의 확정 사실 (2026-10-01 갱신: Unity·Input System 행은 에디터 실측 + manifest.json)

| 항목 | 값 |
|---|---|
| Unity | 6000.3.25f1 |
| 렌더 파이프라인 | URP 17.3.0 |
| 입력 | Input System 1.20.0 (신 입력 시스템. `Input.GetKey` 쓰지 않는다) |
| 테스트 | Test Framework 1.6.0 + performance |
| .NET SDK | **설치되어 있지 않음** (런타임 8.0.4만). `dotnet build` 불가 |
| Unity 실행 파일 | `C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe` |

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

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath "D:/PCUBE/ProtoHarness" -logFile "$SCRATCH/compile.log"
```

끝나면 로그에서 증거를 뽑는다:

```bash
grep -nE "error CS|Compilation failed|Exiting batchmode" "$SCRATCH/compile.log"
```

> 위 두 명령은 2026-08-25에 이 프로젝트에서 실제로 실행해 확인했다 (exit 0, `error CS` 없음, `Exiting batchmode successfully`).
> 부작용 주의: batch 실행 중 Unity의 VCS 연동이 프로젝트 루트에 `dev/null/` (git-lfs 훅 잔재)를 만들 수 있다. 발견하면 보고하고 정리한다.

**(C) MCP도 없고 Unity Editor가 열려 있을 때** — batch mode를 실행하지 않는다(§0). 사용자에게 Console 결과를 요청하고, 받은 내용을 근거로 적는다. 상상해서 채우지 않는다.

Editor 실행 여부 확인: `Temp/UnityLockfile` 존재 = 열려 있음.

### 4-4. 테스트

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -runTests -projectPath "D:/PCUBE/ProtoHarness" -testPlatform EditMode -testResults "$SCRATCH/results.xml" -logFile "$SCRATCH/test.log"
```

- 결과는 `results.xml` 의 `<test-run ... failed="N">` 를 읽어서 보고한다. 로그 눈대중 금지.
- 버그를 고쳤다고 말하려면, **고치기 전에 실패하는 테스트**가 있어야 한다.

### 4-5. 막혔을 때

같은 문제로 **3번 실패하면 멈춘다.** 4번째 시도 대신 보고한다: 시도한 것, 나온 에러 원문, 다음 후보 2개.

---

## 5. 실패는 시끄럽게

조용히 굴러가는 잘못된 상태가 최악이다. 깨질 거면 즉시, 크게 깨진다.

**초기화**
```csharp
private void Awake()
{
    if (target == null)
    {
        Debug.LogError($"{nameof(Foo)}: target 이 비어 있습니다. 인스펙터에서 연결하세요.", this);
        enabled = false;   // 잘못된 상태로 Update 돌지 않는다
        return;
    }
}
```
- 직렬화 참조가 null이면 기본값으로 때우지 말고 위처럼 **끄고 알린다**.
- `Debug.LogError` 두 번째 인자에 `this` 를 넘긴다. Console에서 클릭하면 그 오브젝트가 선택된다.

**예외**
- `try/catch` 는 **복구 계획이 있을 때만**. 복구할 수 없으면 잡지 않는다.
- 잡았으면 반드시 `Debug.LogException(e, this)` 또는 원문이 남는 `LogError`. 삼키지 않는다.
- `catch` 안에서 `return null` / `return default` 로 흐름을 이어가지 않는다.

**계약**
- 잘못된 인자는 `ArgumentException`으로 즉시 던진다. 보정하지 않는다.
- `Debug.Assert` 는 빌드에서 사라진다. 런타임에 반드시 잡아야 하면 명시적 `if` + 로그/예외.
- 인스펙터 설정 오류는 `OnValidate()` 에서 에디터 편집 중에 드러낸다.

**에이전트도 시끄럽게**
- 지시가 모호하면 추측해서 진행하지 말고 묻는다.
- 검증을 못 했으면 "검증 안 됨"이라고 눈에 띄게 쓴다. 요약에 묻지 않는다.
- 작업 중 규칙 위반을 발견하면(내 것이든 기존 코드든) 조용히 넘기지 않고 보고한다.

---

## 6. 조회 모드 (A / B / C / D)

목적은 **토큰 절감**이다. 같은 답을 더 싼 경로로 얻는다.

### 6-1. 라우터 — 질문 유형별 첫 조회 대상

| 질문 유형 | 모드 | 첫 조회 | 실패 시 |
|---|---|---|---|
| "구조가 어떻게 되나", "무슨 시스템이 있나" | **A** | `docs/ARCHITECTURE.md` | B로 |
| "왜 이렇게 했나", "이거 왜 안 쓰나" | **A** | `docs/DECISIONS.md` | 사용자에게 질문 |
| "X 어디 있나", "X 쓰는 곳", "이미 있나" | **B** | `index/symbols.tsv` grep | 코드 grep |
| "X 바꾸면 뭐가 깨지나" | **C** | **미구현** | 사용자에게 알리고 B + grep 으로 대체 |
| 위로 안 풀림 | **D** | A → B → 코드 grep → Read 순으로만 확장 | 3단계에서 멈추고 보고 |

**D모드의 규칙**: 위 순서를 건너뛰지 않는다. 코드부터 뒤지는 것이 가장 비싸다.

### 6-2. 인덱스 신선도 — 쓰기 전에 반드시 확인

`index/symbols.tsv` 헤더를 먼저 본다.

- `git-head` 가 현재 `git rev-parse HEAD` 와 **다르면 인덱스를 쓰지 않는다.** 재생성하거나, 못 하면 그 사실을 보고한다.
- `symbols: 0` 이면 **"해당 심볼 없음"이라고 결론내지 않는다.** 코드가 아직 없다는 뜻일 뿐이다.
- 재생성:

```bash
powershell -ExecutionPolicy Bypass -File tools/reindex.ps1
```

> 낡거나 빈 인덱스는 인덱스가 아예 없는 것보다 **나쁘다.** 자신 있는 오답을 만들기 때문이다. → §5

### 6-3. C모드 상태 — 보류 (2026-08-25)

호출/참조 그래프는 만들지 않았다. C모드가 필요한 질문이 오면 **미구현임을 먼저 알린다.**

재논의 트리거: 우리 `.cs` 가 **60개를 넘거나**, 리팩터링 영향 범위가 손으로 감당이 안 될 때.

| 안 | 방식 | 정확도 | 비용 |
|---|---|---|---|
| C1 | mono-cecil 1.11.5 로 `Library/ScriptAssemblies/Assembly-CSharp.dll` IL 순회 (`Unity_RunCommand`) | 진짜 호출 간선 | 도구 제작이 별도 작업 |
| C2 | ripgrep 역참조 | 근사. 호출 방향·오버로드 구분 불가 | 즉시 |

C2를 만들더라도 이름은 **"참조 그래프"** 다. "호출 그래프"라고 부르지 않는다. → §4-1

### 6-4. 모드와 무관하게 항상 적용

- **씬 · 프리팹 · `.asset` YAML을 통독하지 않는다.** GUID나 컴포넌트명으로 **grep만** 한다. 이 프로젝트 최대의 토큰 싱크다.
- `Library/PackageCache/` 는 패키지 API 확인 목적일 때만 읽는다.
- 이미 이 대화에서 읽은 파일을 다시 읽지 않는다.

---

## 7. 서브에이전트

### 7-1. 5종

| 에이전트 | 작업 | 쓰기 | 모델 |
|---|---|---|---|
| `unity-explorer` | 탐색 | ❌ | sonnet |
| `unity-architect` | 구조 분석 | `docs/` 만 | opus |
| `unity-reviewer` | 리뷰 (§0/§1/§5 체크리스트) | ❌ | opus |
| `unity-implementer` | 생성 | `Assets/` | opus |
| `unity-verifier` | 검증 (Unity MCP) | ❌ | sonnet |

정의는 `.claude/agents/<이름>.md`. 규칙을 바꾸면 **CLAUDE.md와 에이전트 정의를 같이** 고친다. 한쪽만 고치면 조용히 어긋난다.

### 7-2. 병렬 / 직렬

**병렬 OK** — 읽기 전용이고 영역이 겹치지 않을 때만.
- 탐색 여러 갈래, 파일별 리뷰, 서로 다른 시스템 조사

**직렬 필수** — 앞의 결과가 뒤의 입력일 때.
- `구조 분석 → 생성 → 검증`. 뒤집으면 근거 없는 코드가 나온다.
- `리뷰 → 수정 → 재검증`

**병렬 절대 금지**
1. 같은 파일에 쓰는 작업 2개 — 충돌한다.
2. `Unity_RunCommand` 동시 호출 — 에디터는 메인 스레드 단일 펌프다. 큐잉되더라도 서로를 모르는 두 에이전트가 에디터 상태를 바꾸면 **결과를 신뢰할 수 없다.**
3. 씬 · 프리팹을 건드리는 작업 2개.

### 7-3. 파견 기준

서브에이전트는 **콜드 스타트**다. 이 대화를 전혀 모르는 상태에서 컨텍스트를 처음부터 다시 쌓는다. 파견 비용이 직접 하는 비용보다 큰 경우가 많다.

**파견한다**
- 독립 영역 **3개 이상**을 훑어야 할 때
- 결론만 필요하고 중간 파일 덤프는 필요 없을 때
- 대량 읽기로 메인 컨텍스트를 더럽히면 안 될 때

**직접 한다**
- 1–2 파일이면 그냥 읽는다
- 이미 이 대화에서 읽은 것
- **사용자가 파견을 지시하지 않았을 때** ← 현재 기본값

### 7-4. 파견할 때 지키는 것

- 에이전트는 이 대화를 모른다. 파견 프롬프트에 **경로·범위·판정 기준·안 할 것**을 전부 적는다. "아까 그거" 같은 지시는 통하지 않는다.
- 돌아온 결과를 그대로 믿지 않는다. **`경로:줄` 근거가 없는 주장은 되묻는다.** → §4-1
- 에이전트가 규칙 위반을 저질렀으면 결과를 버리고 보고한다. 조용히 고쳐 쓰지 않는다.

---

## 8. 응답 형식

작업 보고는 이 순서로. 길이보다 근거.

```
한 것:     무엇을 바꿨나 (파일:줄)
근거:      왜 그렇게 했나 / 참고한 기존 자산
검증:      무엇을 돌렸고 결과가 무엇인가 (또는 "검증 안 됨: 이유")
남은 것:   안 한 것, 확인 필요한 것
```

---

## 9. 브랜치

2026-10-02 도입. 결정 이유는 `docs/DECISIONS.md`.

```
main    안정 기준선. 마일스톤마다만 갱신하고 태그(v0.1 ...)를 단다
 └ dev  통합 브랜치. 검증을 통과한 기능만 모인다
     └ feature/<영역>-<내용>   기능별 작업. dev 에서 분기하고 dev 로 병합한다
```

### 9-1. 이름

| 접두사 | 용도 | 예 |
|---|---|---|
| `feature/` | 기능 | `feature/p1-fixed-tick` |
| `fix/` | 버그 수정 | `fix/grapple-release-boost` |
| `docs/` | 문서·규칙 | `docs/branch-strategy` |
| `spike/` | 버려도 되는 실험 | `spike/netcode-ngo-vs-fusion` |

### 9-2. 규칙

- **`main`, `dev` 에 직접 커밋하지 않는다.** 작업을 시작할 때 현재 브랜치를 확인하고, `main`/`dev` 이면 멈추고 브랜치부터 만든다. (브랜치 생성은 합의 대상이 아니다.)
- **병합은 `--no-ff`** 로 기능 단위가 보이게 묶는다. `feature → dev`, `dev → main` 모두 같다.
- **병합 조건**: 컴파일 에러 0 + EditMode 통과 + PlayMode 통과 (§4). CI 는 없고 로컬에서 검증한다. 검증 못 했으면 병합하지 않는다.
- **병합·푸시는 사용자가 시킬 때만** (§0 Git 과 같다). `dev → main` 병합과 태그도 마찬가지다.
- 리베이스·스쿼시는 **아직 푸시하지 않은 로컬 브랜치에서만**. 푸시한 브랜치의 이력은 고치지 않는다. 강제 푸시 금지 (§0).
- **씬·프리팹을 건드리는 브랜치는 동시에 1개만** (§7-2). 병합 충돌 때 YAML 을 손으로 고치지 않는다.
- `Packages/manifest.json`, `ProjectSettings/` 변경은 **그것만 담은 브랜치**로 분리한다. Unity 가 자동으로 바꾼 파일은 그렇게 기록한다.
- **서브에이전트 파견 프롬프트에 `브랜치: <이름>` 줄을 `승인:` 줄과 함께 적는다** (§3-4). `unity-implementer` 는 `main`/`dev` 이거나 이름이 다르면 거부한다.
- 병렬 작업을 위한 워크트리는 쓰지 않는다. 직렬로 진행한다. (Unity 프로젝트가 둘이 되면 `Library/` 를 따로 만든다.)

### 9-3. 알아둘 것

- 에디터가 열린 채로 브랜치를 전환하면 재임포트가 일어난다. 전환은 에디터가 한가할 때 한다.
- Unity 가 자동으로 바꾸는 파일(`ProjectSettings/`, `ProtoHarness.slnx`, `.codex` 줄바꿈)은 브랜치 전환 때 같이 따라온다.
- `.gitattributes` 는 `.unity/.prefab/.asset/.mat` 에 `merge=unityyamlmerge` 를 지정하지만 git config 에 병합 드라이버가 **등록되어 있지 않다.** 임시 저장소 실험(2026-10-02)에서 미등록 드라이버는 기본 텍스트 병합으로 되돌아가 **일반 충돌 마커를 남겼다.** 그래서 씬·프리팹이 충돌하면 마커를 손으로 고치지 않고 병합을 중단(`git merge --abort`)한 뒤 에디터에서 다시 작업한다. 드라이버 등록은 검증 없이 하지 않는다.
- GitHub 브랜치 보호는 설정하지 않았다 (2026-10-02 결정, 단독 개발).
