# FINGER LANES 외부 경관

2026-10-27 / `feature/m3-finger-exterior` / Unity MCP / Unity 6000.3.19f1.

## 목적과 승인

사용자 요청: "fingermap 서킷이 완성되었다. 이전 다른 서킷 디자인했듯이 이 서킷도 외부 디자인만들어봐. 카트라이더의 빌리지 손가락을 레퍼런스 삼았다."

마을·정원·두 지름길의 깃발·석교와 수로·통과형 시계탑을 별도 장식으로 제작하고 로비에 연결하는 계획에 사용자가 "ㄱㄱ"로 승인했다. 이번 작업의 MCP 씬 저장 및 빌드 씬 목록 등록 예외도 같은 계획에 명시해 승인받았다. 규칙 자체는 바꾸지 않았다.

기존 `SkywayEnvironmentBuilder`의 프리뷰 씬 제작·프리팹 저장·물리 분리 검증 방식을 따른다. 원작의 에셋·로고를 가져오지 않고 내장 도형으로 마을을 구성했다.

## 결과 자산

- 생성/재제작 도구: `Assets/_Project/Scripts/Editor/ChainRush/FingerLanesEnvironmentBuilder.cs` (static class).
- 장식 프리팹: `Assets/_Project/Prefabs/FingerLanesExterior.prefab`.
- 추가 로드 씬: `Assets/_Project/Scenes/ChainRushTrackFingerLanes.unity` (프리팹 인스턴스 루트 1개).
- 새 URP Lit 재질 6개: `Art/Materials/M_Finger{Cream,Coral,Blue,Sage,Stone,Water}.mat`.
- 기존 재질 4개: `M_StadiumSportsWhite`, `M_ArtStadiumInk`, `M_ArtStadiumAmber`, `M_StadiumSportsTurfLight`.
- 기존 로비 FINGER LANES의 `environmentScene`만 `ChainRushTrackFingerLanes`로 연결했다.
- 빌드 씬 목록에 새 씬 1개를 활성화해 추가했다. 기존 항목은 유지했다.

생성 메뉴는 `ProtoHarness > Chain Rush > Create Finger Lanes Exterior`, 재제작 메뉴는 `Rebuild Finger Lanes Exterior`다. 재제작은 프리팹의 GUID를 지키며 프리뷰 씬에서 수행한다. 저장·빌드 등록·로비 연결은 생성 메뉴에 숨기지 않고 별도 `SaveRegisterAndConnect()`로 분리했다.

## 디자인과 배치

- 주택·상점 64동: 크림색/흰색/푸른 벽, 산호색/푸른 박공지붕, 창틀, 굴뚝, 줄무늬 차양.
- 정원·가로수 그룹 50개: 손가락 사이 정원과 둥근 수관. 출발 광장에 별도 가로수 2그룹.
- 가로등 38개, 지름길 깃발 4개. 깃발 색으로 두 지름길을 구분한다.
- 시계탑: 실제 좁은 통로 조각 23의 중앙에 배치. 양쪽 기둥과 상부 홀, 앞뒤 시계판, 작은 종탑과 지붕.
- 석교: 기존 도약대 아래 아치, 교대, 노면 바깥 난간. 수로 위 지면은 네 조각으로 나눠 물을 덮지 않는다. 수면은 y -5.2 m, 양쪽과 끝에 돌 옹벽이 있다.
- 출발 광장: 입체 도형 문자로 된 FINGER LANES 간판. 폰트·텍스처 의존 없음.
- 원경: 낮고 둥근 언덕. 카메라·조명은 기존 레이스의 것을 사용한다.

본선과 두 지름길 모두 0.5 m 간격으로 샘플링해 도로 반폭과 배치 여유를 계산한다. 랜드마크·주택·나무·가로 시설물의 Renderer AABB도 별도로 검사한다. 시계탑 상부는 지면 위 9 m 이상, 기초와 물은 기존 도로 아래다. 장식에 노면·충돌체·Rigidbody·런타임 스크립트·카메라·조명을 넣지 않는다.

## 생성 중 발견하고 수정한 것

초기 깃발 배치의 검사 원문:

```text
System.InvalidOperationException: Shortcut flag overlaps a road corridor.
```

지름길 가장자리에서 깃발까지 거리를 2.8 m에서 3.3 m로 늘렸다. 도로 값은 바꾸지 않았다.

초기 수관 검사 원문:

```text
Decoration Rounded crown intrudes into a road corridor: margin 0.66 m at (0.00, 0.00, 19.50).
```

정원 전체 크기의 여유를 늘린 뒤에도 도로변 나무 여러 곳에서 같은 문제가 남았다. 내장 메시를 실제 조회한 결과:

```text
[Log] Mesh=[Cube.fbx]; center=[(0.00, 0.00, 0.00)]; size=[(1.00, 1.00, 1.00)]
[Log] Mesh=[Sphere.fbx]; center=[(0.00, 0.00, 0.00)]; size=[(2.00, 2.00, 2.00)]
[Log] Mesh=[Cylinder.fbx]; center=[(0.00, 0.00, 0.00)]; size=[(2.00, 2.00, 2.00)]
```

`Shape`의 인자를 실제 길이로 통일하고 `mesh.bounds.size`로 스케일을 정규화했다. 검사 기준은 완화하지 않았다. 조회 명령의 첫 컴파일에서는 MCP 내부 네임스페이스의 `Mesh`와 충돌해 `CS0118: 'Mesh' is a namespace but is used like a type`가 났다. 명령에서 `UnityEngine.Mesh`를 명시해 해결했다. 프로젝트 스크립트 컴파일 오류는 없었다.

최초 생성 성공 시 Editor.log 원문:

```text
Finger Lanes decoration clearance: nearest renderer AABB is 1.07 m outside the main/shortcut road edge; tower overhead >= 9 m.
Finger Lanes exterior: 3021 renderers; physics=0; runtime scripts=0; main and shortcut corridors clear.
```

실제 주행 화면에서 간판의 좌우 반전과 아치가 대부분 잠기는 수면 높이를 확인했다. 입체 문자 X 좌표 방향을 고치고 수면을 -2.1 m에서 -5.2 m로 내려 아치를 드러냈다. 수로 끝 옹벽 2개를 더한 최종 재제작은 프리팹 GUID를 유지했다. 최종 Editor.log 원문:

```text
Finger Lanes decoration clearance: nearest renderer AABB is 1.07 m outside the main/shortcut road edge; tower overhead >= 9 m.
Finger Lanes exterior: 3023 renderers; physics=0; runtime scripts=0; main and shortcut corridors clear.
```

최종 프리팹·빌드 등록 조회 원문:

```text
[Log] Prefab GUID=[cf38cd8dee4722146ade671a3d8c3229]; renderers=[3023]; colliders=[0]; rigidbodies=[0]; scripts=[0]; cameras=[0]; lights=[0]
[Log] Exterior build index=[5]
```

저장·연결 MCP 결과 원문:

```text
isCompilationSuccessful: true
isExecutionSuccessful: true
compilationLogs: ""
[Log] Finger Lanes exterior saved, registered and connected.
```

## 보존 확인

다음 SHA-256은 생성·저장·연결 전후 동일하다.

| 파일 | SHA-256 |
|---|---|
| `Data/Circuit_FingerLanes.asset` | `DD39D6FC774009D70B7449BD1C772ECD83F4F6871190AEC3FC73399ADA344D07` |
| `Scenes/ChainRushCircuit.unity` | `5730CA2D337203BA80E38C6B7B038D7AEC1A81F7BF5410A51E3DEC97EF882663` |
| `Scenes/ChainRushTrackSkyway.unity` | `81933328297B4400D03CB6079A1A4FF4DBB0CA055971755F4AA97F2C5EF5EC8B` |
| `Scenes/ChainRushTrackStadium.unity` | `FCCB13C96450A80D2BB24230DB4486E4E68C27DB7B9D92D31ABFACE467C2B271` |

작업 시작 전부터 변경되어 있던 파일: `Packages/packages-lock.json`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/ProjectVersion.txt`, `ProtoHarness.slnx`. 이번 작업에서는 편집하지 않았다.

## 검증

영향 평가: 새 프리팹·장식 씬 → 로비 `TrackChoice.EnvironmentScene` → `RaceAssembler`의 Additive 로드·레이스 종료 시 해제. 기존 `Race_FingerLanesFromTheLobby_BotFinishesTwoLaps`에 경관 로드·루트 1개·물리 및 스크립트 0개·로비 복귀 시 해제 검사를 더했다. 기존 두 랩 완주·가드 거리·랩 시간·도약대 상승량 기준은 유지했다.

`tools/select-tests.ps1`은 새 재질·프리팹·씬·에디터 도구·빌드 씬 목록을 전체 검증으로 판정했다. 작업 전부터 변경되어 있던 패키지/프로젝트 파일도 도구의 입력에 포함되어 있었다. 판정에 따라 EditMode 전체(`!Enemy`)와 프로젝트 PlayMode 전체 메뉴(`!Device`, `!Enemy`, `!NetEndReplay`)를 실행한다.

EditMode 1회: `testcasecount="283" result="Passed" passed="283" failed="0" skipped="0"`, `2026-10-27 06:03:31Z`~`06:03:35Z`, duration `4.2053132`.

XML: `C:/Users/User/.codex/visualizations/2026/10/27/01a17be6-2ae8-7362-92e4-f563525952d2/finger-exterior-editmode-results.xml`.

PlayMode 최초 1회(간판·수면 마감 전): `testcasecount="197" result="Passed" passed="189" failed="0" skipped="8"`, `2026-10-27 06:04:35Z`~`06:17:18Z`, duration `762.8022203`.

- `Race_FingerLanesFromTheLobby_BotFinishesTwoLaps`: Passed, `06:08:19Z`~`06:08:51Z`, duration `32.158416`. 장식 로드·물리 분리·2랩 완주·로비 복귀 시 장식 해제를 포함한다.
- `Race_FingerLanesShortcuts_ASwingingRunnerTakesBothEachLap`: Passed, `06:08:51Z`~`06:09:17Z`, duration `26.401464`.
- 본선 랩 84.06 / 82.82 s, 최대 옆 거리 1.97 m, 도약대 상승 1.15 / 1.38 m. 지름길 랩 74.50 / 73.26 s.
- 제외 8개는 기존 `[Explicit]`인 `ChainRushProbeTests` 5개와 `NetworkRaceTests` 3개다. 새 Ignore나 제외 조건을 추가하지 않았다.
- 리플레이 지문 `0x18F31E7A2B4F79B3` 유지.

XML: `C:/Users/User/.codex/visualizations/2026/10/27/01a17be6-2ae8-7362-92e4-f563525952d2/finger-exterior-playmode-results.xml`.

간판·수면 마감 후 최종 코드의 EditMode 2회차: `testcasecount="283" result="Passed" passed="283" failed="0" skipped="0"`, `2026-10-27 06:23:30Z`~`06:23:35Z`, duration `4.1425501`.

최종 EditMode XML: `C:/Users/User/.codex/visualizations/2026/10/27/01a17be6-2ae8-7362-92e4-f563525952d2/finger-exterior-final-editmode-results.xml`.

최종 코드의 PlayMode 2회차: `testcasecount="197" result="Passed" passed="189" failed="0" skipped="8"`, `2026-10-27 06:24:34Z`~`06:37:23Z`, duration `768.5364807`.

- `Race_FingerLanesFromTheLobby_BotFinishesTwoLaps`: Passed, `06:28:09Z`~`06:28:40Z`, duration `31.322212`.
- `Race_FingerLanesShortcuts_ASwingingRunnerTakesBothEachLap`: Passed, `06:28:40Z`~`06:29:06Z`, duration `25.916772`.
- 제외는 최초 실행과 같은 `ChainRushProbeTests` 5개·`NetworkRaceTests` 3개다.
- 본선 랩 84.06 / 82.82 s, 지름길 랩 74.50 / 73.26 s 유지. 리플레이 1700틱의 지문 `0x18F31E7A2B4F79B3` 유지.
- 두 번의 EditMode 및 두 번의 PlayMode 실행 모두 실패 테스트 0개다. 첫 PlayMode 뒤 바뀐 것은 간판 문자 방향과 수로 깊이·옹벽이며, 두 번째 실행은 그 마감이 반영된 코드와 자산에서 수행했다.

최종 PlayMode XML: `C:/Users/User/.codex/visualizations/2026/10/27/01a17be6-2ae8-7362-92e4-f563525952d2/finger-exterior-final-playmode-results.xml`.

테스트 선택 도구 출력 전체: `C:/Users/User/.codex/visualizations/2026/10/27/01a17be6-2ae8-7362-92e4-f563525952d2/finger-exterior-test-selection.txt`.

## 실제 화면 확인

저장된 로비를 Play 모드로 열어 세 번째 트랙을 고르고 `BuildSetup()`으로 레이스를 열었다. 출력 원문:

```text
[Log] Track=[FINGER LANES]; environment=[ChainRushTrackFingerLanes]
```

마감 후 실제 레이스 카메라로 출발선·시계탑 통로·석교를 1600×900 PNG로 렌더링해 직접 열어 확인했다. 레이스의 기존 조명·안개·노면·봉을 그대로 사용했다. 전체 배치 확인용 overview 컷만 임시 원거리 클리핑 1000 m·안개 off로 촬영했다. 촬영 뒤 카메라 위치·각도·종횡비·클리핑·타깃 RenderTexture·안개 상태를 복원하고 임시 텍스처를 해제했다. 에셋에 촬영 설정을 저장하지 않았다.

캡처 폴더: `C:/Users/User/.codex/visualizations/2026/10/27/01a17be6-2ae8-7362-92e4-f563525952d2/`.

- `finger-exterior-start.png`: 실제 출발 시점, 정방향 FINGER LANES 간판·러너·마을.
- `finger-exterior-clock.png`: 도로 중앙에서 바라본 통과형 시계탑.
- `finger-exterior-bridge.png`: 물 위로 드러난 석조 아치와 수로·난간.
- `finger-exterior-overview.png`: 전체 도로·지름길과 외부 경관의 배치 확인용 컷.

**검증 안 됨:** 사용자 직접 플레이, 별도 프레임 시간/메모리 성능 계측.

## 최종 편집 상태와 Console

편집 상태 복원 명령에서 `SceneManager.SetActiveScene(title)`의 반환값을 검사하다 다음 오류가 났다.

```text
System.InvalidOperationException: Could not restore the title scene.
```

반환값이 false였던 원인은 확인 못 했다. 재실행으로 덮지 않고 실제 상태를 조회한 결과, 의도한 두 씬은 이미 복원되어 있었고 타이틀이 활성 씬이었다. 씬/에셋에 추가 수정을 하지 않았다.

```text
isCompilationSuccessful: true
isExecutionSuccessful: true
compilationLogs: ""
[Log] Playing=[False]; compiling=[False]; active=[ChainRushTitle]
[Log] Scene=[ChainRushTitle]; path=[Assets/_Project/Scenes/ChainRushTitle.unity]; loaded=[True]; dirty=[False]; roots=[2]
[Log] Scene=[ChainRushTrackFingerLanes]; path=[Assets/_Project/Scenes/ChainRushTrackFingerLanes.unity]; loaded=[True]; dirty=[False]; roots=[1]
```

최종 Console 오류 조회의 `totalCount=2`, `errorCount=2`, `warningCount=0`이다. 남은 두 메시지 원문:

```text
RaceAssembler: Multi at Fast cannot be raced yet (multi: M7).
RunnerProfile '': every scale and the weight must be positive.
```

두 메시지는 각각 `ChainRushFlowTests.cs:584`의 `LogAssert.Expect(... "cannot be raced yet")`, `ChainRushProfileTests.cs:136`의 `LogAssert.Expect(... "must be positive")`에 해당한다. 최종 XML에는 실패 테스트가 없다. Console을 지워 오류 0개로 보이게 만들지 않았다.
