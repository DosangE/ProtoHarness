# SKYWAY LOOP 외부 장식 작업 기록

2026-10-21 / `feature/track-skyway` / Unity MCP / Unity 6000.3.19f1.

## 결과와 현재 상태

- 저장 완료: `Assets/_Project/Prefabs/SkywayExterior.prefab`.
- 생성 도구: `Assets/_Project/Scripts/Editor/ChainRush/SkywayEnvironmentBuilder.cs`.
- 저장 완료: `Assets/_Project/Scenes/ChainRushTrackSkyway.unity` (프리팹 인스턴스 루트 1개).
- 빌드 씬 목록에 새 씬을 활성화해 추가했고, 로비 SKYWAY LOOP 항목의 `environmentScene`을 `ChainRushTrackSkyway`로 연결해 저장했다.
- 사용자 승인: 외부 장식 진행, 이후 현재 브랜치에서 MCP 씬 저장·빌드 목록 등록·로비 연결 예외 요청에 **"ㄱㄱ"**. 이번 작업에만 적용하며 기존 규칙 자체는 수정하지 않았다.
- 기존 스타디움처럼 장식 전용으로 구성했다. `RaceAssembler.cs:102`의 Additive 로드 구조와 `RaceSetup.cs:9`의 물리 분리 계약을 참고했다.

## 구성

마을 건물 42동, 교각 27개, 가로등 35개, 스카이라인 타워 24동, 정원·가로수, SKYWAY 입체 간판 2개. 기존 재질 9개와 Unity 내장 메시를 재사용한다. 새 재질·텍스처·런타임 스크립트는 없다.

건물·정원·가로등은 트랙 중심선과 수평으로 이격한다. 교각은 노면 두께 1.2m보다 아래에 놓는다. 중앙선·노면·난간·앵커·봉은 장식 프리팹에 포함하지 않는다.

## 검증

Unity MCP 최종 원문:

```text
isCompilationSuccessful: true
isExecutionSuccessful: true
compilationLogs: ""
[Log] Prefab saved=[True]; renderers=[1319]; colliders=[0]; rigidbodies=[0]; scripts=[0]; cameras=[0]; lights=[0]
[Log] scene=[ChainRushLobby]; path=[Assets/_Project/Scenes/ChainRushLobby.unity]; dirty=[False]; roots=[2]
[Log] scene=[ChainRushTrackSkyway]; path=[]; dirty=[True]; roots=[1]
[Log] Editor compiling=[False]
```

최종 Console 조회 원문:

```json
{"logs":[],"totalCount":0,"errorCount":0,"warningCount":0}
```

1m 간격 중심선 샘플과 장식 오브젝트 전체 Renderer AABB 사이의 최소 수평 거리 원문:

```text
[Log] group=[02 Village and shops]; nearest AABB-to-centerline=[12.94555]m; object=[Village building 8]
[Log] group=[03 Gardens and avenue]; nearest AABB-to-centerline=[12.22427]m; object=[Planter tree]
[Log] group=[05 Boulevard lamps]; nearest AABB-to-centerline=[12.75081]m; object=[Lamp S316]
[Log] group=[06 Skyline]; nearest AABB-to-centerline=[43.70811]m; object=[Skyline tower 22]
[Log] group=[07 Skyway landmarks]; nearest AABB-to-centerline=[25.75]m; object=[SKYWAY landmark]
[Log] Final renderers=[1319]; text meshes=[0]; colliders=[0]; root id=[-465140]
```

작업 전·후 SHA256이 동일하다:

| 파일 | SHA256 |
|---|---|
| `Assets/_Project/Data/Circuit_Skyway.asset` | `32C6FBF8DB4D16C3ABE2F7CECE69C9A6A88ADF200E1BE342AE40652BE51FC734` |
| `Assets/_Project/Scenes/ChainRushCircuit.unity` | `378024B6DA8C281AA5A4814392946BB259905A2BC51AD079292D4199729F7328` |
| `Assets/_Project/Scenes/ChainRushTrackStadium.unity` | `FCCB13C96450A80D2BB24230DB4486E4E68C27DB7B9D92D31ABFACE467C2B271` |

MCP 다각도 캡처, 임시 노면을 함께 렌더링한 전체 배치, 마을 주행 시점, 고가 S700 시점으로 확인했다. 검증용 임시 노면·카메라·조명과 비영속 메시 3개는 확인 후 정리했다. 간판의 기본 GUI 폰트가 뒤쪽에서도 비쳐 입체 메시 문자로 교체했고 프리팹에 반영했다. 캡처는 대화에 표시되어 있다. 파일 내보내기는 자동 승인 검토의 `JavaScript execution exceeds the 64000-byte strict auto-review limit`로 거절되어 생략했다.

위 로그는 저장 전 장식 제작 단계의 결과다. 저장·연결 후 검증은 아래에 별도로 기록한다.

## 저장·연결 후 검증

MCP 저장·연결 실행 원문:

```text
isCompilationSuccessful: true
isExecutionSuccessful: true
[Log] Skyway exterior saved, registered and connected.
```

새 씬 GUID `5616c6c90c69d4c42a905add4f90ace5`가 Unity가 발급한 `.meta`와 빌드 목록에서 같다. 기존 빌드 씬은 그대로 두고 새 씬 1개만 추가했다.

`ChainRushFlowTests`의 장식 없음 전제를 실제 새 동작으로 갱신했다. 2랩 테스트는 저장된 로비에서 `BuildSetup()`을 받아 레이스를 열고, 장식 로드 완료·루트 1개·충돌체/Rigidbody/MonoBehaviour 0개·기존 낙하/가드/랩 시간 기준·로비 복귀 후 장식 해제를 확인한다.

EditMode XML: `testcasecount="258" result="Passed" passed="258" failed="0" skipped="0"`, `2026-10-21 08:28:27Z`~`08:28:33Z`, duration `5.7690096`.

PlayMode XML: `testcasecount="176" result="Passed" passed="169" failed="0" skipped="7"`, `2026-10-21 08:30:15Z`~`08:39:03Z`, duration `527.9383131`.

- `ChainRushFlowTests`: `testcasecount="22" result="Passed" passed="22" failed="0" skipped="0"`.
- `Lobby_SkywayTrack_IsChosenWithItsEnvironment`: Passed, `08:33:40Z`~`08:33:42Z`.
- `Race_SkywayFromTheLobby_BotFinishesTwoLaps`: Passed, `08:34:11Z`~`08:34:42Z`, duration `30.602064`.

SKYWAY 2랩 테스트는 장식 로드·물리 분리·2랩 완주·로비 복귀 시 장식 해제까지 포함한다. 제외 7건은 모두 기존 `[Explicit]` 측정 4건과 네트워크 레이스 3건이다. 테스트 기준을 느슨하게 하거나 무조건 Ignore를 추가하지 않았다.

결과 XML 보관 경로:

- `C:/Users/User/.codex/visualizations/2026/10/21/01a15d6d-7914-76e3-99be-a38ab4bead9e/skyway-editmode-results.xml`
- `C:/Users/User/.codex/visualizations/2026/10/21/01a15d6d-7914-76e3-99be-a38ab4bead9e/skyway-playmode-results.xml`

실행 중 테스트가 의도적으로 발생시키는 에러 메시지는 기존 `LogAssert.Expect` 대상이다: `GameSettingsTests.cs:123`의 저장값 오류 2건(EditMode), `ChainRushFlowTests.cs:508`의 잠긴 속도, `ChainRushProfileTests.cs:135`의 양수 제약(PlayMode). 실패 테스트는 두 실행 모두 0개다.

검증 후 저장된 로비의 실제 메뉴를 통해 SKYWAY에 입장해, `ChainRushTrackSkyway` 로드와 `RaceAssembler.IsAssembling == false`를 확인했다. 기존 레이스 조명·안개(`fog=True`, density `0.01`) 아래에서 실제 Main Camera로 출발선의 러너·마을·가로수·타워·전방 노면을 캡처했다. 임시 조명이나 임시 노면을 사용하지 않았다. 화면은 대화에 표시되어 있다.

실제 화면 확인 후 편집 상태 복원 및 최종 Console 원문:

```text
[Log] playing=[False]; paused=[False]; compiling=[False]
[Log] Skyway build index=[4]
[Log] scene=[ChainRushLobby]; dirty=[False]; roots=[2]
[Log] lobby track=[STADIUM]; environment=[ChainRushTrackStadium]
[Log] lobby track=[SKYWAY LOOP]; environment=[ChainRushTrackSkyway]
[Log] scene=[ChainRushTrackSkyway]; dirty=[False]; roots=[1]
[Log] exterior renderers=[1319]; colliders=[0]; scripts=[0]
```

```json
{"logs":[],"totalCount":0,"errorCount":0,"warningCount":0}
```

**검증 안 됨:** 별도 런타임 성능 계측, 사용자 직접 플레이. 테스트와 실제 카메라 확인은 위 범위에서 완료했다.

이번 PlayMode 실행에서 장식을 실제 로드한 SKYWAY 2랩 주행 로그 원문:

```text
ChainRush skyway lap 1: 81.12 s
UnityEngine.Debug:Log (object)
ProtoHarness.Tests.PlayMode.ChainRushFlowTests/<Race_SkywayFromTheLobby_BotFinishesTwoLaps>d__34:MoveNext () (at Assets/_Project/Scripts/Tests/PlayMode/ChainRushFlowTests.cs:650)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

ChainRush skyway lap 2: 79.88 s
UnityEngine.Debug:Log (object)
ProtoHarness.Tests.PlayMode.ChainRushFlowTests/<Race_SkywayFromTheLobby_BotFinishesTwoLaps>d__34:MoveNext () (at Assets/_Project/Scripts/Tests/PlayMode/ChainRushFlowTests.cs:650)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

ChainRush skyway race: 1482.8 m a lap, total 161.00 s, max offset 1.92 m at S 1456.8.
UnityEngine.Debug:Log (object)
ProtoHarness.Tests.PlayMode.ChainRushFlowTests/<Race_SkywayFromTheLobby_BotFinishesTwoLaps>d__34:MoveNext () (at Assets/_Project/Scripts/Tests/PlayMode/ChainRushFlowTests.cs:654)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
```

## 작업 중 오류와 처리

MCP 조회 코드에서 `Mesh`가 MCP 내부 네임스페이스와 충돌했다. `UnityEngine.Mesh`로 명시한 뒤 조회에 성공했다. 실패 원문:

```text
- Error Error CS0118: 'Mesh' is a namespace but is used like a type (Line: 12)
- Error Error CS0118: 'Mesh' is a namespace but is used like a type (Line: 13)
- Error Error CS0118: 'Mesh' is a namespace but is used like a type (Line: 14)
```

첫 생성 실행은 빈 장식 씬을 만든 뒤 중단됐다. 조회 결과 해당 씬이 이미 활성 상태였으므로 활성화 호출을 조건부로 고치고, 기존 빈 씬을 재사용해 두 번째 실행에 성공했다. 실패 원문:

```text
[Error] System.InvalidOperationException: Cannot activate the new decoration scene.
  at ProtoHarness.Editor.ChainRush.SkywayEnvironmentBuilder.CreateExterior () [0x00108] in C:\Users\User\Desktop\PCUBE\ProtoHarness\Assets\_Project\Scripts\Editor\ChainRush\SkywayEnvironmentBuilder.cs:45
  at Unity.AI.Assistant.Agent.Dynamic.Extension.Editor.CommandScript.Execute (Unity.AI.Assistant.Agent.Dynamic.Extension.Editor.ExecutionResult result) [0x00001] in <6fae59af981a45428cba50825d26d64d>:0
  at Unity.AI.Assistant.Editor.RunCommand.AgentRunCommand.Execute (Unity.AI.Assistant.Agent.Dynamic.Extension.Editor.ExecutionResult& executionResult, System.String title) [0x00019] in .\Library\PackageCache\com.unity.ai.assistant@c0127625258e\Editor\Assistant\RunCommand\AgentRunCommand.cs:56
```

## 생성·연결 도구의 기본 동작

일반 지침에서는 `Unity_RunCommand` 씬 저장·ProjectSettings 변경을 금지하고 빌드 설정을 별도 브랜치로 둔다. 이번 작업은 사용자의 위 명시적 예외 승인으로 현재 브랜치에서 저장·등록까지 마쳤다. 생성 도구의 기본 동작에는 자동 씬 저장이나 빌드 목록 변경을 넣지 않았다.

`Create Skyway Exterior`는 새 프리팹을 저장하고 저장 전 장식 씬을 만든다. 기존 프리팹/씬 덮어쓰기는 거부한다. `Connect Saved Skyway Exterior To Lobby`는 저장되어 빌드 목록에 등록된 장식 씬만 연결하며 로비를 자동 저장하지 않는다.

다시 볼 때는 저장된 `ChainRushTrackSkyway.unity`를 연다. 최초 검증 완료 시점에는 커밋·푸시·병합을 하지 않았으며, 이후 사용자가 "일단 커밋 병합 푸쉬 진행"으로 승인했다. 머신별 Unity 버전·패키지 잠금·솔루션 변경은 커밋에서 제외하고 로컬에 보존한다.

## 테스트 선택 도구 출력

실행: `powershell -ExecutionPolicy Bypass -File tools/select-tests.ps1` (저장·연결 및 테스트 수정 후). 아래 선택에 따라 EditMode/PlayMode 전체를 실행했다. Long은 현재 Endless 보류 규칙으로 제외했다.

```text
warning: in the working copy of 'Packages/packages-lock.json', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'ProjectSettings/ProjectVersion.txt', LF will be replaced by CRLF the next time Git touches it
바뀐 파일 (git diff --name-only dev...HEAD + 커밋하지 않은 변경 + 추적하지 않는 파일): 21개
  Assets/_Project/Data/Circuit_Skyway.asset  →  전체 (SO 값·씬은 시뮬에 들어간다)
  Assets/_Project/Data/Circuit_Skyway.asset.meta  →  전체 (SO 값·씬은 시뮬에 들어간다)
  Assets/_Project/Prefabs/SkywayExterior.prefab  →  전체 (표에 없는 경로)
  Assets/_Project/Prefabs/SkywayExterior.prefab.meta  →  전체 (표에 없는 경로)
  Assets/_Project/Scenes/ChainRushLobby.unity  →  전체 (SO 값·씬은 시뮬에 들어간다)
  Assets/_Project/Scenes/ChainRushTrackSkyway.unity  →  전체 (SO 값·씬은 시뮬에 들어간다)
  Assets/_Project/Scenes/ChainRushTrackSkyway.unity.meta  →  전체 (SO 값·씬은 시뮬에 들어간다)
  Assets/_Project/Scripts/Editor/ChainRush/ChainRushCircuitSceneBuilder.cs  →  전체 (씬 빌더·테스트 메뉴)
  Assets/_Project/Scripts/Editor/ChainRush/SkywayEnvironmentBuilder.cs  →  전체 (씬 빌더·테스트 메뉴)
  Assets/_Project/Scripts/Editor/ChainRush/SkywayEnvironmentBuilder.cs.meta  →  전체 (씬 빌더·테스트 메뉴)
  Assets/_Project/Scripts/Runtime/ChainRush/Flow/LobbyMenu.cs  →  ChainRushFlowTests, ChainRushProfileTests (타이틀·로비·레이스 조립 (DECISIONS 2026-10-19 "M2 씬 구성"))
  Assets/_Project/Scripts/Tests/PlayMode/ChainRushFlowTests.cs  →  ChainRushFlowTests (테스트 클래스 파일)
  docs/ARCHITECTURE.md  →  없음 (Unity 가 읽지 않는다)
  docs/ART_SKYWAY.md  →  없음 (Unity 가 읽지 않는다)
  docs/DECISIONS.md  →  없음 (Unity 가 읽지 않는다)
  docs/NEXT_REQUESTS.md  →  없음 (Unity 가 읽지 않는다)
  Packages/packages-lock.json  →  전체 (패키지·프로젝트 설정)
  ProjectSettings/EditorBuildSettings.asset  →  전체 (패키지·프로젝트 설정)
  ProjectSettings/ProjectSettings.asset  →  전체 (패키지·프로젝트 설정)
  ProjectSettings/ProjectVersion.txt  →  전체 (패키지·프로젝트 설정)
  ProtoHarness.slnx  →  없음 (Unity 가 읽지 않는다)

고른 테스트 (① 빠른 확인 / ② 병합은 -Range dev...HEAD 로):
  EditMode: 전체 (Enemy·Endless 카테고리 제외: categoryNames = { "!Enemy", "!Endless" })
  PlayMode: 전체. 메뉴 ProtoHarness > Chain Rush > Run PlayMode Tests + Run Long Tests
② 병합: 이 브랜치는 Long 도 돈다 (Run Long Tests, CLAUDE.md §9-2). 단 무한 모드 보류 중에는 Long 이 모두 Endless 라 돌리지 않는다
```
