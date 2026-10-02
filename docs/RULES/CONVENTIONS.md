# 형태 규칙 — CLAUDE.md §1-2 · §1-3

> **CLAUDE.md 의 일부다. 구속력은 CLAUDE.md 와 같다.** § 번호는 CLAUDE.md 의 번호를 그대로 쓴다.
> **읽는 시점**: 파일·폴더를 새로 만들기 전 (스크립트·SO·프리팹·씬·테스트). §1-1 세 줄 선언의 "경로"·"형태" 는 이 문서를 근거로 쓴다.
> 이 내용을 고칠 때는 이 파일만 고친다. CLAUDE.md 에 사본을 두지 않는다.

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

ScriptableObject 의 나머지 형식(읽기 전용 프로퍼티, `OnValidate`, 에셋 이름·위치, 비어 있을 때 처리)은 `docs/DECISIONS.md` 2026-10-02 "첫 ScriptableObject 형식: `EncounterTuning`" 에 있다.
