# 아트 요청서 — 러너 캐릭터·모션 (블록아웃 B → 리그 + 키 포즈 클립)

> 작성 2026-11-14, `D:/PCUBE/ProtoHarness` 작업 트리(MP2 세션과 같은 트리라 브랜치 전환·커밋 없이, 추적 안 된 파일로 둔다).
> 근거: `docs/HANDOFF.md` 2절 2번, `docs/DECISIONS.md` 2026-11-12 "방향 재설정"(아트 우선순위 1번)·"MP1 구조"(러너 하나 ≈ 150 배치, `:140`)·2026-11-06 "캐릭터 모형은 임시용"(`:554-557`)·2026-11-09 "Blender 시험 제작"·"Blender 아트 에이전트".
> 출발점: 다른 PC 세션이 만든 블록아웃 `art-src/RunnerKit/RunnerKit.blend`(`origin/feature/art-runner-blockout` `aa59c87`, 장면 `RunnerBlockout_A` 7등신 · `RunnerBlockout_B` 2.5등신). 이 트리에서는 LFS 객체를 받아 scratchpad 사본으로 연다(브랜치를 바꾸지 않는다).
> **이 문서는 구현 승인이 아니다.** `blender-artist` 파견(승인 줄·브랜치 줄, `CLAUDE.md` §3-4)과 Unity 반영(§3-2 합의 요청서)을 따로 올린다. `blender-artist` 는 2·3·4·5절과 6절의 해당 차수를 받아 만든다. 절에 없는 치수·이름은 채우지 않고 멈춘다.

## 0. 결정 (사용자 2026-11-14, 이 대화의 답 원문)

| 질문 | 답 |
|---|---|
| Q1 몸·얼굴 | 답: B(2.5등신) 확정. 얼굴은 "두가지 다 만들어놓자" — 닫힌 얼굴(바이저)·열린 얼굴(고글 + 피부) 둘 다 |
| Q2 리그·Animator | 답: "ㅇㅋ" — 이번에 **아마추어 + 한 덩어리 스킨 메시 + 키 포즈 클립**을 만든다. 뼈를 코드로 돌릴지(b) Animator 로 재생할지(c)는 Unity 반영 요청서에서 정한다 |
| Q3 다섯 종 | 답: "몸체 기본 뼈대는 동일하되 색, 겉장식만 바꾸자" |
| Q4 상한 | 답: "ㄱㄱ" — 러너 하나 **렌더러 1 · 머티리얼 2(본체·발광) · 삼각형 3,000 이하** |
| Q5 모션 | 답: 지금 동작 + "봉 스윙 전용과 부스트 전용 추가". 실패 숙임은 "나 로 가자 벽 충돌 혹은 플레이어끼리 충돌에 쓰면 될듯" → **충돌 움찔(Bump)** 로 바꾼다 |
| Q6 파일 위치 | 답: "ㅇㅋ" — 결과는 `art-src/Runner/`(새 폴더, 추적 안 함) |
| 1차 뒤 (2026-11-14) | 답: "1. 고쳐 2. 추천대로 3. 추천대로 4. 추천대로" — ① 접지 포즈의 발바닥을 바닥(z=0)에 맞춰 다시 낸다(5절 "접지 규칙") ② ALPHA 발광은 **민트**(지금 ALPHA 의 `Signal` 색, 다섯 종을 발광색으로 구분해 왔다) ③ 에이전트가 정한 값(8b절: 팔레트 칸 배치·Roughness 0.45·`ChainOrigin` 자리)은 그대로 ④ 결과 커밋은 **MP2 세션이 끝난 뒤 이 PC 에서** `feature/art-runner-blockout` 으로 |
| D9 봇 러너 구성 | 이 요청서에서 정하지 않는다. MP2 결정 "시드로 무작위"(`DECISIONS.md:15`) — 다섯 종 모두 봇으로 나올 수 있어 다섯 종 전부가 필요하다 |

## 1. 지금 러너가 만들어지는 방식 (코드에서 확인)

- 모형은 다섯 종 모두 **`MeshRenderer` 25개 · 머티리얼 25개**의 딱딱한 부품 계층이다(`Assets/_Project/Art/Models/Runner{Alpha,Beta,Theta,Delta,Omega}.prefab`, 머티리얼은 러너마다 `M_Runner<이름>{Armor,Suit,Metal,Signal}` 4종). 관절 이름: `Torso Joint`·`Head Joint`·`Left/Right Arm Joint`·`Left/Right Leg Joint`, 줄 시작점 `Chain Origin`.
- 드로 배치: 러너 모형 하나 ≈ 150 배치, 러너 8명 ≈ 1,200 (`DECISIONS.md:140`). 이 요청서의 상한(Q4)이 겨누는 비용이다.
- 겉모습 교체: `RunnerLook.Apply`(`Runtime/ChainRush/Visuals/RunnerLook.cs:17-53`)가 씬 모형과 **계층·이름·순서가 같은** 프리팹에서 `MeshFilter` 의 메시와 `MeshRenderer` 의 머티리얼만 복사한다. `SkinnedMeshRenderer` 는 비교·복사 대상이 아니다.
- 동작: `RunnerAnimation`(`Visuals/RunnerAnimation.cs:57-73`)이 관절 6개의 `localRotation` 을 매 프레임 계산한다 — 달리기(속도 비례 주기, 다리 ±38°), 공중, 그래플(`grapple.IsAttached`), 착지 눌림, 실패 숙임(`:46-49`). 몸의 방향·기울기·봉 스윙 미끄럼 각은 `RunnerTilt`(`Visuals/RunnerTilt.cs:46-56`)가 모형 루트를 돌려서 낸다.
- 캡슐: 높이 1.8 m · 반경 0.38 m (`Scenes/ChainRushCircuit.unity` 의 `CharacterController`). 블록아웃 B 의 키 1.80 m 와 같다.

## 2. 공통 규칙

| 항목 | 규칙 | 이유 |
|---|---|---|
| 파일 | `art-src/Runner/Runner.blend` + `Runner.fbx` 한 쌍. 렌더 확인 이미지는 같은 폴더 `Render_<내용>.png` | `CONVENTIONS.md` §1-3 (같은 이름 쌍) |
| 원본 | 사용자가 연 `RunnerKit.blend`(scratchpad 사본)의 장면 `RunnerBlockout_B` 를 **읽기만** 한다. 작업은 새 장면 `Runner` 에서, 저장은 `save_as_mainfile(copy=True)` 로 `art-src/Runner/Runner.blend` 에 | `blender-artist` 정의 "쓰기 범위" |
| 단위·키 | m. 서 있는 키 **1.80 m**(발바닥 z=0 ~ 머리 꼭대기). 블록아웃 B 의 부품 치수를 그대로 쓴다 | 캡슐 1.8 m (1절) |
| 앞면·축 | 캐릭터 앞면 = Blender **−Y**(Front 뷰에서 얼굴이 보인다), 위 = +Z. 오른손(`_R`) = Blender −X 쪽 (앞에서 보면 화면 왼쪽) | `DECISIONS.md` 2026-11-09 "Blender 시험 제작" 축 대응 |
| 피벗 | 아마추어 오브젝트와 메시 오브젝트 원점 모두 **발 사이 바닥 (0, 0, 0)**, 회전 0 · 스케일 1 적용 | 러너 루트가 캡슐 바닥 기준으로 선다 — Unity 쪽 높이 맞춤은 반영 때 확인 |
| 스타일 | 블록아웃 B 그대로: 로우폴리 + 플랫 셰이딩, 모서리만 살짝 | `ART_REQUEST_PROPS.md` A1 과 같은 계열 |
| 렌더러 | **한 러너 = 스킨 메시 오브젝트 1개.** 부품을 모두 합친다(뼈마다 따로 두지 않는다) | Q4. Unity 는 오브젝트(렌더러)마다 따로 그린다 |
| 머티리얼 | **슬롯 2개**: `M_RunnerBody`(팔레트 텍스처, 발광 없음) · `M_RunnerGlow`(단색 + 발광). 블록아웃의 `M_Main`·`M_Secondary`·`M_Dark`·`M_Skin` 은 `M_RunnerBody` 의 팔레트 칸으로, `M_Glow` 는 `M_RunnerGlow` 로 | Q4. 슬롯마다 따로 그려진다(블록아웃 B 는 슬롯 34개) |
| 팔레트 텍스처 | 32 × 32 px PNG, 8 px 칸 4 × 4. 칸 0 Main · 1 Secondary · 2 Dark · 3 Skin, 나머지는 칸 0 색으로 채운다. 면마다 UV 를 **그 색 칸 가운데 2 × 2 px 안으로** 모은다. 파일 `art-src/Runner/T_RunnerPalette_<러너>.png`. 다섯 종은 **같은 UV, 다른 팔레트 파일**로 색만 바꾼다 | 렌더러·머티리얼 상한을 지키며 색이 여럿. Unity URP Lit 은 정점 색을 쓰지 않는다(확인 못 함 — 그래서 텍스처로) |
| 삼각형 | 러너 하나 **3,000 이하**(겉장식 포함, 모디파이어 적용 뒤). 블록아웃 B 닫힌 얼굴 2,132 · 열린 얼굴 2,608 (실측) | Q4 |
| 스킨 | **단단한 스킨**: 정점마다 뼈 하나에 가중치 1.0. 부품이 속한 뼈는 3절 표의 "붙는 부품". 휘는 살 없음 | 블록 부품이라 겉보기는 지금과 같고, 렌더러는 1개 |
| 얼굴 두 벌 | 같은 아마추어에 스킨한 **별개 메시 오브젝트 두 개**(`Runner<러너>Closed`, `Runner<러너>Open`). 게임에서는 한쪽만 켠다(렌더러 1) | Q1 |
| 내보내기 | 4-1 의 **리그 전용 설정**. 프로젝트 기본(`bake_space_transform=True`)을 쓰지 않는다 | 4-1 |
| 확인 | `look`: 닫힌·열린 얼굴 각각 앞·옆·뒤 1장, 클립마다 대표 프레임 1장(5절), 블록아웃 B 와 나란히 1장. `get_scene_info` 의 `size, details, materials, topology, weights` | 크기·가중치 누락을 Blender 에서 먼저 잡는다 |

## 3. 뼈대 (아마추어 `RunnerRig`)

뼈 이름 PascalCase, 좌우는 `_L`/`_R`(Blender 대칭 기능이 읽는 접미사). 위치는 블록아웃 B 의 부품 관절 자리(부품 원점)를 따른다.

| 뼈 | 부모 | 붙는 부품 (블록아웃 B 이름) | 변형 |
|---|---|---|---|
| `Root` | — | 없음 (바닥 원점, 이동 키 전용) | 아니오 |
| `Pelvis` | `Root` | `B_Pelvis` | 예 |
| `Torso` | `Pelvis` | `B_Torso`, `B_BackPack` | 예 |
| `Head` | `Torso` | `Head_Closed`+`B_Visor` / `Head_Open`+`Head_Open_Face`+`Head_Open_Goggles` | 예 |
| `UpperArm_L` · `_R` | `Torso` | `B_UpperArm.L` · `.R` | 예 |
| `LowerArm_L` · `_R` | `UpperArm_*` | `B_LowerArm.L` · `.R`, 오른쪽은 `B_ChainGauntlet` 도 | 예 |
| `Hand_L` · `_R` | `LowerArm_*` | `B_Hand.L` · `.R` | 예 |
| `UpperLeg_L` · `_R` | `Pelvis` | `B_UpperLeg.L` · `.R` | 예 |
| `LowerLeg_L` · `_R` | `UpperLeg_*` | `B_LowerLeg.L` · `.R` | 예 |
| `Foot_L` · `_R` | `LowerLeg_*` | `B_Shoe.L` · `.R` | 예 |
| `ChainOrigin` | `LowerArm_R` | 없음 — `B_ChainGauntlet` 앞 끝(줄이 나가는 자리) | **아니오** (그래도 내보낸다) |

- 뼈 17개(처음 판에 "18개" 로 잘못 셌다 — 1차 `blender-artist` 가 지적, 표대로 17개로 만들었다). 겉장식(6절)은 붙는 자리의 뼈에 스킨한다(새 뼈를 만들지 않는다).
- `ChainOrigin` 은 지금 `GrappleController.ropeOrigin`(`GrappleController.cs:18,88-90`)이 가리킬 자리다.

## 4. 내보내기

### 4-1. 리그 전용 FBX 설정 (프로젝트 기본과 다른 값 — 반영 때 실제 적용 값을 보고)

```python
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,              # 작업 장면의 RunnerRig 와 그 메시만 선택
    object_types={'ARMATURE', 'MESH'},
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=False,      # 기본 True 를 쓰지 않는다 (아래)
    axis_forward='-Z', axis_up='Y',
    global_scale=1.0, apply_unit_scale=True,
    add_leaf_bones=False,            # 기본 True: 사슬 끝에 빈 뼈가 붙는다
    use_armature_deform_only=False,  # ChainOrigin(비변형)을 남긴다
    bake_anim=True,
    bake_anim_use_all_actions=True,  # 클립(액션)마다 AnimStack 하나
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
)
```

- **`bake_space_transform=False` 인 이유**: Blender 5.2.3 연산자 설명 원문 — "Bake space transform into object data, avoids getting unwanted rotations to objects when target space is not aligned with Blender's space (**WARNING! experimental option, use at own risk, known to be broken with armatures/animations**)" (2026-11-14 `bpy.ops.export_scene.fbx.get_rna_type()` 에서 읽음).
- 그 대가: 2026-11-09 시험 제작의 첫 FBX 처럼 Unity 루트에 회전 (270, 0, 0) 이 남을 수 있다(`DECISIONS.md` "Blender 시험 제작"). 처리(Unity `ModelImporter.bakeAxisConversion` — 그때 "FBX 마다 설정" 이라 버렸지만 러너 FBX 는 하나다 — 또는 부모에 두고 회전을 덮어쓰지 않기)는 **Unity 반영 요청서**에서 정한다. Blender 단계에서 맞다고 쓰지 않는다.

### 4-2. 클립은 액션으로

- 클립 하나 = 액션 하나, 이름은 5절의 `Runner_<클립>`. 24 fps(블록아웃 파일의 값). 반복 클립은 첫 프레임과 마지막 프레임이 같은 포즈.
- 포즈는 뼈 회전만 키로 한다. `Root` 의 이동은 착지 눌림·위아래 흔들림만(앞으로 가는 이동 없음 — 위치는 시뮬이 정한다).

## 5. 모션 클립 (키 포즈)

> 각도는 **지금 `RunnerAnimation` 의 값**을 출발점으로 적었다(Unity 관절 축 기준, `RunnerAnimation.cs:60-73`). Blender 뼈 축과 부호가 다르므로 **숫자가 아니라 "방향 설명"을 따르고**, 크기는 이 각도에 맞춘다. 블록아웃 B 는 팔꿈치·무릎이 따로 있어 그 굽힘은 새로 정한다(표의 값).

> **접지 규칙 (2026-11-14 1차 뒤 추가)**: 공중 포즈(`Air`·`GrappleHang`)를 뺀 모든 클립의 모든 키에서 **땅을 딛는 발의 가장 낮은 정점이 z = 0** 이다(±5 mm). 아래 표의 `Root` 높이 값(착지 10 cm 하강, 달리기 4.5 cm 상승 등)은 이 규칙보다 뒤다 — 다리를 굽혀 낮아진 만큼 `Root` 를 내려 발을 바닥에 맞추고, 실제 넣은 값을 보고한다. 두 발이 다 뜨는 달리기 중간 프레임(5·13 f)은 낮은 발이 z = 0~5 cm. 숨쉬기·흔들림처럼 몸만 움직이는 것은 `Root` 가 아니라 `Torso` 로 넣는다.
>
> **발바닥 수평 판 (2026-11-14, 사용자 "두가지 버전 다 저장해놔")**: 위 접지 규칙만으로는 무릎을 굽힌 포즈에서 발끝이 내려가 `Root` 를 올려야 했다(8b절). 그래서 두 판을 둔다 — ① `Runner.blend`/`Runner.fbx`: 발목을 돌리지 않은 판(1차 수정 결과 그대로, 덮어쓰지 않는다) ② `RunnerFlatFoot.blend`/`RunnerFlatFoot.fbx`: 접지 규칙이 적용되는 모든 키에서 **딛는 발(그 키에서 가장 낮은 발)과 지나가는 프레임의 두 발은 `Foot_*` 뼈를 돌려 발바닥 밑면을 바닥과 평행(±3°)** 하게 둔 뒤 접지 규칙으로 `Root` 를 다시 정한 판. 공중 포즈(`Air`·`GrappleHang`)와 땅에서 뜬 뒷발은 발목을 돌리지 않는다. 그 밖의 값(각도·팔레트·발광·이름)은 ①과 같다. 어느 판을 쓸지는 렌더를 보고 사용자가 정한다.

| 클립 | 반복 | 길이 | 포즈 (방향 설명 · 각도) | 게임에서 켜는 신호 (반영 때) |
|---|---|---|---|---|
| `Runner_Idle` | 예 | 48 f | 차렷에서 팔을 조금 벌린 선 자세. 24 f 에 가슴이 1 cm 오르내린다 | 카운트다운·출발 전 (지금 동작 없음) |
| `Runner_Run` | 예 | 16 f (키 1·5·9·13·17) | 1 f 왼발 앞 접지: 왼허벅지 앞으로 38° · 오른허벅지 뒤로 38°, 팔은 반대로 30°(앞)·30°(뒤) 에 둘 다 앞으로 12° 더, 팔꿈치 60° 굽힘, 뒷다리 무릎 45° 굽힘. 9 f 는 좌우 반대. 5·13 f 는 다리가 모이고 몸이 4.5 cm 위. 상체 앞으로 8° | 접지 중 (`RunnerAnimation.cs:57-64`) — 주기는 속도에 맞춰 Unity 가 바꾼다 |
| `Runner_Air` | 아니오 | 1 키 | 두 팔 앞으로 35°, 왼허벅지 뒤로 25° · 오른허벅지 앞으로 18°, 무릎 30° 굽힘, 상체 뒤로 5° | 공중 (`:61-65`) |
| `Runner_Land` | 아니오 | 6 f (1 f 최대 → 6 f 중립) | 1 f: 두 허벅지 22° 굽힘 · 무릎 40° · 상체 앞으로 14°, `Root` 10 cm 아래 | 착지 순간 (`:54-56,69-72`) |
| `Runner_GrappleHang` | 아니오 | 1 키 | 오른팔 머리 위로 거의 수직(155°, 블록아웃 `B_Swing` 의 팔), 왼팔 앞으로 75°, 왼허벅지 뒤 35° · 오른허벅지 앞 28°, 상체 뒤로 12° | 그래플에 걸림 (`:66`) |
| `Runner_PoleSwing` **(새)** | 예 | 16 f | 카트가 드리프트하듯 **안쪽(오른쪽)으로 상체 20° 기울임**, 줄을 쥔 오른팔은 옆으로 수평에서 20° 위로 뻗어 봉을 향함, 왼팔은 균형 잡듯 바깥으로 45°, 다리는 넓게 벌린 미끄럼 자세(바깥 다리 펴고 안쪽 무릎 50° 굽힘). 8 f 에 몸이 2 cm 흔들림. **봉이 왼쪽일 때**는 Unity 에서 좌우 반전 여부를 정한다 — 이번에는 오른쪽 한 벌 | 봉 스윙 중 (`RunnerMotor.IsSwinging`, `RunnerMotor.cs:169`). 미끄럼 각은 지금처럼 `RunnerTilt` 가 낸다 |
| `Runner_Boost` **(새)** | 예 | 12 f (`Run` 보다 빠른 주기) | `Run` 과 같은 다리 동작에 보폭 45°, **상체 앞으로 22°**, 두 팔을 뒤로 60° 젖혀 고정(흔들지 않는다), 머리는 앞을 본다(목 뒤로 10°) | 부스터·부스트존 (`SpeedTrails.StyleOf`, `Visuals/SpeedTrails.cs:58-61` 의 Booster·BoostZone). 스윙 + 부스터는 `PoleSwing` 을 쓴다 |
| `Runner_Bump` **(새)** | 아니오 | 8 f (2 f 최대 → 8 f 중립) | 2 f: 상체 뒤로 18° · 옆으로 10°, 머리 뒤로 10°, 두 팔을 가슴 앞으로 올려 막는 자세(팔꿈치 90°), 무릎 20° 굽힘 | 벽 충돌·러너끼리 충돌(Q5 답). 지금 벽 충돌은 소리만 있다. 러너 충돌은 MP2 단계 B |

- 지금의 **실패 숙임**(`RunnerAnimation.cs:46-49`)은 클립으로 만들지 않는다(Q5 답). 실패는 체력 0(`ChainRushGame.cs:315-317`, 체력을 깎던 적은 지웠다)이나 서킷 밖 낙하(`RunnerMotor.cs:433-436`)에서만 나서 서킷에서는 사실상 쓰이지 않는다.

## 6. 다섯 종 — 같은 뼈대, 색·겉장식만 (Q3)

색은 지금 머티리얼 값(`M_Runner<이름>Armor`·`Signal` 의 `_BaseColor`)에서 옮긴다. **이 값은 Unity 인스펙터 값(sRGB)이고 팔레트 PNG 픽셀에 그대로 넣는다.** Blender 셰이더 노드의 색은 선형값이라 숫자가 다르다(블록아웃 B `M_Main` 노드 값 (0.014, 0.462, 0.392)) — 블록아웃 색을 PNG 로 옮길 때 변환하고, 어떻게 변환했는지 보고한다. 겉장식은 지금 라인업(`Assets/_Project/Art/Textures/RunnerLineup.png`)의 특징을 블록아웃 B 비율로 옮긴 것이다.

| 러너 | Main (칸 0) | Glow (`M_RunnerGlow`) | 겉장식 (붙는 뼈) | 차수 |
|---|---|---|---|---|
| ALPHA | **블록아웃 B 색 그대로**(`M_Main` 청록). 지금 ALPHA 는 어두운 청록 (0.13, 0.30, 0.33) | **민트 (0.25, 1.00, 0.76)** sRGB — 1차는 블록아웃 `M_Glow` 주황이었고 1차 뒤 사용자가 민트로 정했다(0절). `M_RunnerGlow` 노드에는 선형으로 바꿔 넣는다 | 블록아웃 B 의 `B_BackPack` 그대로(새 장식 없음) | **1차** |
| BETA | 호박 (0.95, 0.66, 0.16) | 주황빨강 (1.00, 0.27, 0.055) | 머리 위 볏(`Head`), 등 쌍둥이 통(`Torso`) | 2차 |
| THETA | 은백 (0.81, 0.89, 0.95) | 파랑 (0.15, 0.55, 1.00) | 머리 양옆 귀 날개(`Head`), 등 날개 판 2장(`Torso`) | 2차 |
| DELTA | 보라 (0.35, 0.17, 0.55) | 연보라 (0.78, 0.25, 1.00) | 머리 안테나(`Head`), 등 큰 고리(`Torso`) | 2차 |
| OMEGA | 검정 (0.075, 0.09, 0.12) | 빨강 (1.00, 0.10, 0.15) | 두꺼운 어깨 판(`UpperArm_*`), 등 제트팩(`Torso`) | 2차 |

### 6-1. 2차 (BETA·THETA·DELTA·OMEGA) — 사용자 2026-11-14 "1. 1,2 둘다 2. 둘다. 3. 네 추천대로 해봐라"

**판 (답 1·2)**: 4종 메시 8개(러너마다 닫힌·열린 얼굴)를 **① `Runner.*` 와 ② `RunnerFlatFoot.*` 두 판 모두**에 넣는다(메시는 같고 액션만 다르다). ② 판에는 봉 스윙 대안 액션 **`Runner_PoleSwingEdge`**(안쪽 오른발만 평평하게 딛고 바깥 왼발은 발목을 돌리지 않아 모서리로 접지 — 8b 의 "대안")를 더한다. ②의 `Runner_PoleSwing`(두 발 평평)은 그대로 둔다. ① 판은 액션을 바꾸지 않는다.

**공통**
- 몸은 ALPHA 메시와 같다(같은 정점·UV·가중치). 러너마다 바꾸는 것: ① **등 장식이 `B_BackPack` 자리를 대신한다**(ALPHA 만 `B_BackPack`) ② 머리·어깨 장식을 더한다 ③ 팔레트 칸 0(Main)과 발광색.
- 메시 이름 `Runner<러너>Closed`·`Runner<러너>Open`. 머티리얼 슬롯은 2개 그대로, 이름은 `M_RunnerBody_<러너>`·`M_RunnerGlow_<러너>`(ALPHA 는 지금 이름 유지). 팔레트 `T_RunnerPalette_<러너>.png` — 칸 0 만 아래 Main, 칸 1·2·3 은 ALPHA 와 같다, UV 는 ALPHA 와 같다.
- 장식은 단단한 스킨으로 표의 뼈에 붙인다. 장식의 Main 면은 칸 0, 어두운 면은 칸 2(Dark), 발광 면은 `M_RunnerGlow_<러너>`.
- **삼각형**: 열린 얼굴 메시도 3,000 이하(ALPHA 열린 얼굴 2,608 − `B_BackPack` 144 = 2,464 → 장식 합계 **536 이하**).
- **겹침 금지**: 장식은 헬멧(폭 ±0.293, 뒤 y 0.442, 아래 z 1.08)·위팔·몸통을 뚫지 않는다. 붙이는 면은 1 cm 묻는다.
- **붙이는 위치 조정 (2026-11-14 Codex 1회째가 찾은 모순 뒤 추가)**: 표의 위치는 bounds 기준 대략값이다. 붙는 면(몸통 뒤·헬멧 곡면·위팔 위)에 **1 cm 묻히도록 붙는 축 하나로만 위치를 옮겨 맞추고**, 옮긴 양을 장식마다 보고한다. 치수(x×y×z)는 그대로. 헬멧처럼 곡면에 붙는 것은 밑면을 곡면 쪽으로 늘려 떠 보이는 틈이 없게 한다(밑면이 묻히는 부분은 치수 밖이어도 된다). 처음 판의 등 장식 세 개는 앞면이 몸통 뒤 경계 y 0.115 에 맞닿기만 했다(Codex 실측 몸통 뒤 y 0.114895). 확인은 Idle 1 f(rest)와 Run 1 f·PoleSwing 9 f·GrappleHang 1 f 에서 `look` — 팔을 든 포즈에서 어깨 장식이 헬멧을 뚫으면 멈추고 보고.
- 치수는 bounds 전체(m), 위치는 bounds 가운데의 월드 좌표(rest, 앞 −Y · 왼쪽 +X · 위 +Z). 근거: 블록아웃 B 실측(2026-11-14) — `B_Torso` x ±0.167 · y −0.146~0.115 · z 0.62~1.10, `B_BackPack` x ±0.163 · y 0.115~0.275 · z 0.705~1.005, `Head_Closed` x ±0.293 · y −0.289~0.442 · z 1.08~1.80, `B_UpperArm.L` x 0.113~0.291 · z 0.76~1.014.

| 러너 | Main sRGB (칸 0) | 발광 sRGB | 장식 (이름 · 뼈 · 치수 x×y×z · 위치 · 모양) |
|---|---|---|---|
| BETA | 호박 (0.95, 0.66, 0.16) | 주황빨강 (1.00, 0.27, 0.055) | `Crest` · `Head` · 0.06 × 0.40 × 0.12 · (0, 0.05, 1.83) · 헬멧 꼭대기 앞뒤로 긴 볏, 옆에서 보면 앞이 낮고 뒤가 높은 사다리꼴. / `TwinTanks` · `Torso` · 0.26 × 0.11 × 0.30 · (0, 0.17, 0.855) · 세로 원기둥 2개(지름 0.11, x ±0.075, 8각), 위 끝 0.02 m 가 발광 뚜껑 |
| THETA | 은백 (0.81, 0.89, 0.95) | 파랑 (0.15, 0.55, 1.00) | `EarFin_L`·`EarFin_R` · `Head` · 각 0.04 × 0.22 × 0.16 · (±0.305, 0.10, 1.50) · 헬멧 옆면에서 뒤로 30° 젖힌 칼날 판, 앞 모서리에 발광 줄 0.01. / `BackWings` · `Torso` · 0.42 × 0.10 × 0.30 · (0, 0.165, 0.86) · 가운데 받침 상자 0.20 × 0.10 × 0.20 + 위로 바깥 25° 벌어진 날개 판 2장(각 0.04 × 0.06 × 0.30), 날개 끝 z ≤ 1.03 |
| DELTA | 보라 (0.35, 0.17, 0.55) | 연보라 (0.78, 0.25, 1.00) | `Antenna` · `Head` · 0.05 × 0.05 × 0.22 · (0.10, 0.08, 1.88) · 헬멧 왼쪽 위의 막대(0.03 각), 끝에 발광 구 지름 0.05. / `BackRing` · `Torso` · 0.40 × 0.12 × 0.40 · (0, 0.165, 0.86) · 몸통 등에 붙는 받침 0.10 × 0.08 × 0.10(y 0.105~0.185) 뒤에 세운 고리(바깥 지름 0.40, 굵기 0.05, 12각, 고리 면은 XZ 평면, 받침에 1 cm 묻힘), 고리 안쪽 면 발광. 고리 위 끝 z ≤ 1.06 (처음 판의 y 0.22 는 받침이 몸통에서 4.5 cm 떠 있었다) |
| OMEGA | 검정 (0.075, 0.09, 0.12) | 빨강 (1.00, 0.10, 0.15) | `ShoulderPad_L`·`ShoulderPad_R` · `UpperArm_L`·`_R` · 각 0.16 × 0.18 × **0.04** · (±0.21, 0, 위팔 위 1 cm 매립) · 위팔 위의 판(2026-11-14 처음 판 0.07 은 Run 1 f·PoleSwing 9 f 에서 헬멧 밑면을 뚫어 사용자 "추천대로" 로 낮췄다), 바깥 모서리에 발광 줄 0.02. / `JetPack` · `Torso` · 0.30 × 0.16 × 0.40 · (0, 0.195, 0.82) · 몸체 0.30 × 0.16 × 0.32(z 0.70~1.02) + 아래로 노즐 원기둥 2개(지름 0.08, 길이 0.08, x ±0.08), 노즐 안 끝 발광 |

- 장식 이름은 Blender 작업용이다(합친 뒤 한 메시가 되므로 FBX 에는 남지 않는다). OMEGA 의 Main 이 검정이라 Dark 칸과 구분이 약하다 — 렌더를 보고 사용자가 정한다.

- Secondary·Dark·Skin 칸(1·2·3)은 다섯 종이 블록아웃 B 값을 같이 쓴다. 바꿀지는 1차 렌더를 보고 사용자가 정한다.
- **1차** = 뼈대·스킨·클립 8개·ALPHA 닫힌/열린 얼굴 두 메시·ALPHA 팔레트. 흐름 전체를 한 러너로 끝내 보고, 확인 뒤 **2차**(나머지 4종의 겉장식 메시 8개·팔레트 4장)를 따로 승인받는다. 2차의 겉장식 치수는 6-1 에 채웠다(2026-11-14).

## 7. Unity 반영 때 바뀌어야 할 코드 (이번에 구현하지 않는다)

줄 번호는 `dev` `f6be824` 기준. `RaceAssembler.cs` 는 MP2 세션이 작업 트리에서 고치고 있어 `dev` 판으로 적었다.

| 파일:줄 | 지금 | 리그를 들이면 |
|---|---|---|
| `Visuals/RunnerLook.cs:7-74` | 같은 계층의 `MeshFilter`·`MeshRenderer` 메시·머티리얼 복사, 다르면 `ArgumentException` | 스킨 메시는 비교·복사하지 않는다. "겉모습" 이 **켤 메시 하나 + 팔레트 텍스처**로 바뀐다 → 새 방식으로 다시 짠다(공개 API 변경, 합의 대상) |
| `RunnerProfile.cs:20-23,30` | `look` = 씬 모형과 같은 계층의 프리팹 | 무엇을 가리킬지(러너별 프리팹 / 메시 이름 + 팔레트) 다시 정한다. 툴팁·주석 |
| `Flow/RaceAssembler.cs` `TryDress` (`dev` `:133,174-199`) | `RunnerLook.Apply` 호출, 실패하면 레이스를 멈춘다 | 위 방식에 맞춘다 |
| `Visuals/RunnerAnimation.cs:13-18,30,36,60-73,79-84` | 관절 Transform 6개를 코드로 돌린다 | (b) 뼈 Transform 으로 바꾸고 팔꿈치·무릎·봉 스윙·부스트·충돌을 더한다 / (c) Animator 매개변수로 바꾼다(첫 사례: Animator Controller 에셋). `:46-49` 실패 숙임은 지운다(Q5) |
| `Visuals/RunnerTilt.cs:35-40` | 켜져 있는 `Renderer` 를 모아 복귀 깜빡임 | `SkinnedMeshRenderer` 도 `Renderer` 라 그대로 동작할 것 — 꺼 둔 얼굴 메시가 깜빡임에 끌려 켜지지 않는지 **확인 필요** |
| `Visuals/GhostRunner.cs:12-13,41-60` | 모형을 복제, 포즈는 복제 순간 그대로 | 스킨 메시 복제는 같은 방식. (c) 면 복제본의 Animator 를 꺼야 한다 |
| `Flow/RunnerPreview.cs:48,142` | 로비 회전대에 겉모습 프리팹을 복제 | 스킨 메시는 바인드 포즈로 보인다 → `Runner_Idle` 첫 프레임을 입힐지 정한다 |
| `GrappleController.cs:18` (`ropeOrigin`) | 씬 모형의 `Chain Origin` | 새 뼈 `ChainOrigin` 으로 다시 연결(씬 수정) |
| `Net/RemoteGhost.cs:33` | 모형 복제 (Net 동결) | 컴파일만 유지 — 동작은 M7 에서 |
| 씬 `ChainRushCircuit`·`ChainRushTestBench` 의 러너 모형 | 부품 25개 계층 | 새 FBX 로 교체(씬 수정, 사용자 저장). 루트 회전(4-1) 처리 |
| `Art/Models/Runner*.prefab`·`Runner*Geometry.asset`·`RunnerChamfer.asset`·`M_Runner*` 20개 | 지금 모형 | 대체 후 지울지(삭제는 승인 대상) |
| 테스트 `EditMode/RunnerLookTests.cs:38-118`, `PlayMode/ChainRushPresentationTests.cs:74-122`, `PlayMode/ChainRushProfileTests.cs:91-94` | 지금 방식을 시험 | 새 방식에 맞춰 다시 쓴다(테스트를 통과시키려 고치는 것이 아니라 계약이 바뀌는 것 — 반영 요청서에 계약을 먼저 적는다) |
| `tools/select-tests.ps1` | 러너 FBX 판정 없음 | 새 FBX·프리팹 판정 추가 |
| 측정 | 러너 ≈ 150 배치 | 교체 뒤 다시 잰다(같은 조건, `DECISIONS.md:140`) |

- 지시에 있던 **"스윙 잔상(모형 복제)"은 코드에 없다.** 몸 실루엣 잔상은 2026-11-06 에 "러너 모형에 묶인다" 로 버린 대안이다(`DECISIONS.md:557,564`). 리그가 들어온 뒤 다시 볼 후보로만 남긴다.
- 시뮬(캡슐·`RunnerMotor`)은 모형을 읽지 않는다(`RunnerTilt.cs:5-7`, `RunnerLook.cs:10-11`) — 서킷 지문에 닿지 않을 것이다. 반영 때 지문 테스트로 확인한다.

## 8. 확인 방법 (1차)

- `blender-artist` 보고: `get_scene_info`(`size, details, materials, topology, weights`) — 메시마다 렌더러 1·슬롯 2·삼각형 ≤ 3,000·키 1.80 m, 가중치 없는 정점 0.
- `look`: 2절 "확인" 항목. 렌더 PNG 는 `art-src/Runner/Render_*.png`.
- `ls -la art-src/Runner/` — `.blend`·`.fbx`·팔레트 PNG·렌더.
- FBX 안 이름: `RunnerRig`, `RunnerAlphaClosed`, `RunnerAlphaOpen`, 액션 8개 — Blender 에 다시 가져와(새 장면) 확인한다.
- **Unity 에서 맞는지는 이 단계에서 확인하지 않는다**(`blender-artist` 정의).

## 8b. 1차 결과 (2026-11-14, `blender-artist` 1회, 약 129k 토큰·50 도구·9분)

- 산출물 `art-src/Runner/`: `Runner.blend`(255,777 B, 세션 사본이라 블록아웃 장면 A·B 도 들어 있다), `Runner.fbx`(859,164 B, `Kaydara FBX Binary`), `T_RunnerPalette_Alpha.png`(332 B), `Render_*.png` 15장. 세이프 모드는 PNG·렌더·blend·FBX 저장을 막지 않았다.
- 메시: `RunnerAlphaClosed` 0.69 × 0.74 × 1.80 m · 삼각형 2,132 · 슬롯 2 · 가중치 없는 정점 0/1108, `RunnerAlphaOpen` 0.69 × 0.76 × 1.80 m · 2,608 · 2 · 0/1356. non-manifold·boundary·loose 0. 상한(2절) 안.
- 뼈 17개, 액션 8개(24 fps: Idle 1–49, Run 1–17, Air 1, Land 1–6, GrappleHang 1, PoleSwing 1–17, Boost 1–13, Bump 1–8). FBX 를 다시 가져와 뼈 머리 위치 차이 최대 5.1e-05 m.
- **에이전트가 정한 값** (요청서에 없던 것 — 반영 전에 사용자가 볼 것):
  - 팔레트 칸 배치: 이미지 왼쪽 위가 칸 0, 행 우선. 칸 i 의 UV = (((i%4)·8+4)/32, ((3−i//4)·8+4)/32). 발광 슬롯 면도 칸 0. sRGB 8비트: Main (31,181,168) · Secondary (238,242,244) · Dark (31,43,58) · Skin (241,201,165).
  - `M_RunnerBody` Roughness 0.45 하나로(블록아웃 0.35–0.6). `M_RunnerGlow` = 블록아웃 `M_Glow` 그대로(주황, 세기 1.5).
  - `ChainOrigin` 은 `B_ChainGauntlet` 의 손 쪽 끝 단면 가운데(월드 (−0.262, −0.024, 0.664), `Hand_R` 머리와 2 mm).
  - 각도는 rest(블록아웃 `B_Stand`, 팔 15° 벌림·팔꿈치 10°) 기준 상대값, 팔꿈치 굽힘·GrappleHang 155°·PoleSwing 팔은 절대각으로 해석. Bump 의 "옆으로 10°" 는 캐릭터 왼쪽(+X).
- **고칠 것 (요청서 쪽 잘못)**:
  - `Runner_Land` 1 f: 5절 숫자대로면 발이 바닥 아래 12 cm(최저 z −0.123). `Root` 10 cm 하강이 굽힘으로 낮아진 높이보다 크다 → "발바닥이 z=0 에 닿게 `Root` 하강을 정한다" 로 고쳐 다시 내야 한다.
  - `Runner_Idle` 25 f: "가슴 1 cm" 를 `Root` 로 넣어 발이 1 cm 뜬다 → `Torso` 로.
  - 그 밖에 바닥 아래 2~4 cm: Air −2.2, GrappleHang −2.1, PoleSwing −3.6, Bump −4.1 cm(공중 포즈는 문제없음, PoleSwing·Bump 는 접지 포즈).
- **1차 수정 (같은 날, 같은 에이전트 2회째, 약 166k 토큰·18 도구·3분)**: 접지 규칙·Idle 숨쉬기 `Torso`·민트 발광(선형 (0.0509, 1.0, 0.5382)) 반영. 딛는 발 최저 z = 0.0000 (Air·GrappleHang 제외, 키 프레임에서만 — 보간 프레임은 재지 않았다). 지나가는 프레임의 낮은 발은 2.5 cm(에이전트가 정함). PoleSwing 9 f 흔들림도 `Torso` 로 옮겼다. 재가져오기 뼈 17·액션 8·Glow 색 일치. `Runner.blend1`(1차 백업)이 생겼다 — `.gitignore:24` `*.blend1` 로 무시된다.
  - **새로 드러난 것**: 무릎을 허벅지보다 더 굽히면 발끝이 내려가 `Root` 를 **올려야** 접지된다 — Land 1 f +2.3 cm·Bump 2 f +4.1 cm(착지 눌림이 안 보이고 발끝으로 선 모양), Run 은 `Root` 가 한 주기에 12 cm(−5.1 ↔ +7.1), Boost 15 cm(−8.3 ↔ +7.1) 오르내린다(지금 코드의 흔들림은 4.5 cm, `RunnerAnimation.cs:72`). 원인은 발목(`Foot_*`)을 돌리지 않아 발바닥이 정강이를 따라 기우는 것.
- **발바닥 수평 판 ② (같은 날, 같은 에이전트 3회째, 약 209k 토큰·19 도구·4분)**: `RunnerFlatFoot.blend`·`RunnerFlatFoot.fbx`·`Render_FlatFoot_*.png` 12장(클립 8·앞옆 2·①②비교 `CompareLand`·`CompareRun`). ① 파일은 시각 그대로(15:32). 딛는 발 기울기 0.0°, 최저 z 0 (지나가는 프레임 0.025).
  - `Root` ①→②: Run 한 주기 폭 0.121 → **0.092 m**, Boost 0.153 → **0.123 m**, Land 1 f +0.023 → **−0.023**(내려앉는다), Bump 2 f +0.041 → **−0.011**, PoleSwing +0.016 → −0.023. 렌더: ②의 앞발이 바닥에 평평하다(`Render_FlatFoot_CompareRun.png`).
  - **에이전트가 정한 것**: PoleSwing 은 "가장 낮은 발" 이 발을 돌릴 때마다 바뀌어 **두 발 모두 평평하게** 두었다 — 바깥 왼발이 딛고(z=0) 안쪽 오른발은 평평한 채 6.0 cm 뜬다("뜬 발은 돌리지 않는다" 와 어긋나는 한 곳). 대안(오른발만 평평, 왼발 모서리 접지)은 딛는 발이 20° 기운다.
  - 작업 중 작업 트리 브랜치가 `fix/codex-sandbox-contract` → `dev` 로 바뀌었다(다른 세션이 `215cffb` 병합). 이 작업의 파일은 모두 추적 안 됨, 커밋 없음.
- **2차 4종 (같은 날, Codex `tools/codex-unity.ps1 -Write -Blender`, 첫 Codex Blender 작업)**:
  - 1회째(`p2a`, 16:06): 코드 12 `blocked` — 6-1 표의 등 장식 세 개가 몸통 뒤 경계(실측 y 0.114895)에 맞닿기만 해 "1 cm 묻는다" 와 모순, 쓰기 없이 멈춤. 요청서에 "붙이는 위치 조정" 규칙과 DELTA `BackRing` 받침을 고쳐 같은 스레드로 이어 감.
  - 2회째(`p2b`, `-ResumeLast`, 16:20): 코드 0 `done`. 메시 8개 추가(BETA·THETA·DELTA·OMEGA × 닫힌·열린 얼굴), ①·② 두 판 다시 저장(`Runner.fbx` 메시 10·액션 8, `RunnerFlatFoot.fbx` 메시 10·액션 9 = `Runner_PoleSwingEdge` 추가), 팔레트 4장, 렌더 `Render_<러너>_{Closed,Open}_Front.png` 8장·`Render_Lineup5.png`·`Render_FlatFoot_Clip_PoleSwingEdge_f9.png`. ① 검증(1차 수정 보고의 발 높이) 7개 키 모두 ±2 mm 안.
  - 삼각형: BETA 2,088/2,564 · THETA 2,072/2,548 · DELTA 2,156/2,632 · OMEGA 2,104/2,580(닫힌/열린). 키(머리 장식 포함): BETA 1.907 · DELTA 2.002 m.
  - `B_BackPack` 은 블록아웃과 월드 위치가 0.00001 m 안에서 같은 정점 80개(56면·144삼각형)로 가려냈다. 위치 조정량(장식마다)은 Codex 보고 원문 — 등 장식 y −0.010, OMEGA 어깨판 z +0.008, 머리 장식은 밑면을 헬멧 곡면으로 늘림.
  - **남은 시각 문제: OMEGA 어깨판이 Run 1 f·PoleSwing 9 f 에서 헬멧 밑면을 뚫는다**(치수는 지시대로 유지). `Runner_PoleSwingEdge` 는 Root −0.0065, 바깥 왼발 모서리(20° 기운 채) 접지·안쪽 오른발 7.7 cm 뜸.
  - 세이프 모드 거부 1회(`as_pointer`), Codex 가 다시 써서 해결. 저장 중 `.blend1` 백업을 막으려 Blender 의 백업 수를 잠깐 0 으로 두었다 되돌렸다(Codex 보고).
  - **OMEGA 수정 (`p3`, 17:12, 코드 0)**: 사용자 "추천대로" — 어깨판 0.16 × 0.18 × **0.04**, 중심 z 1.0232(위팔 최고점 1.0132 위로 1 cm 매립), 발광 줄 0.02 폭은 두께에 맞춤. Idle 1·Run 1·Run 9·PoleSwing 9·GrappleHang 1·Boost 1 f 에서 닫힌·열린 얼굴 관통 0(look 4방향 + 교차 검사). ① 저장 뒤 ② 값을 다시 넣어 저장 — ② 발 높이 검증 7개 키 ±2 mm 안. 바뀐 파일 7개만(해시 비교, 나머지 40개 같음). 메인이 PoleSwing 9 f 를 look 으로 다시 봤다(관통 없음).
  - 메인 확인: `get_scene_info` 대신 `execute_blender_code` 로 메시 10개의 삼각형·슬롯·가중치 없는 정점·부모를 다시 셌다(보고와 같음), 팔레트 PNG 32 × 32, `Render_Lineup5.png` 를 봤다.
- **헬멧 수정 (2026-11-14, 2차 뒤, `docs/HANDOFF.md` 에 있던 기록을 병합 때 옮김)**: `Runner.*`·`RunnerFlatFoot.*` 모두 5종 × 닫힌/열린 얼굴에 본체 대표색 헬멧 + 중앙 띠(4종 흰색, 은백 THETA 는 회청색). 닫힌 얼굴의 넓은 바이저만 `M_RunnerVisor`(Roughness 0.12, Metallic 0.35, Coat 0.8) + 부드러운 면 법선으로 광택 처리, 열린 얼굴의 작은 고글은 기존 재질 유지. 슬롯은 닫힌 얼굴 3개/열린 얼굴 2개. 확인용 `Runner_VisorSoftbox` 는 Blender 장면에만 추가, FBX 에는 제외. FBX 재가져오기에서 각 메시 10개·뼈 17개·액션 8개/9개(`PoleSwingEdge` 포함), 닫힌 바이저 30면의 재질·부드러운 법선, Roughness 0.12·Metallic 0.35 확인. 확인 렌더 46장 갱신(FlatFoot 5종 정면 10장 포함). `Render_CompareBlockoutB.png` 는 과거 블록아웃 비교 자료로 유지. 작업 전후 `Assets/` 해시 차이 0. Unity 에서의 재질 표현은 미검증. 커밋 `e8a51d0`(아래 OMEGA 와 함께).
- **OMEGA 바이저 색 (2026-11-14, Codex `-Write -Blender`, 20:03~20:18, 코드 0 `done`)**: 헬멧 수정 뒤 검정 헬멧 위 남색 바이저가 묻혀, 사용자 "추천대로 ㄱㄱ codex 시켜서 작업해" — `M_RunnerVisor` 를 복제한 `M_RunnerVisor_Omega`(Base Color **(0.30, 0.02, 0.04)**, Roughness 0.12·Metallic 0.35·Coat 0.8 그대로)를 `RunnerOmegaClosed` 3번째 슬롯에만. ①·② 두 판, FBX 내보내기는 4-1절 설정. 다시 가져오기: 메시 10·뼈 17·액션 8/9, 다른 닫힌 얼굴 4종은 `M_RunnerVisor` 그대로. 렌더 `Render_Omega_Closed_Front.png`·`Render_FlatFoot_Omega_Closed_Front.png`·`Render_Lineup5.png`(라인업은 PNG 메타데이터의 `Runner_Cam`·프레임 1로 복원, 외곽 1 px 이내). 메인 확인: 해시로 `art-src/Runner/` 57개 중 그 7개만 바뀜·`Assets/` 차이 0, 열린 ② 파일의 슬롯·재질 값, 두 렌더를 봤다(바이저가 빨강으로 갈린다). 열린 얼굴 고글은 그대로.
- FBX 의 AnimStack 이름에 `RunnerRig|` 접두사가 붙는다(`RunnerRig|Runner_Idle`) — Unity 에서 클립 이름이 어떻게 보일지는 반영 때 확인.

## 9. 이번에 하지 않는 것

- Unity 반영 전부(복사·임포트·Animator·코드·프리팹·씬), `Assets/`·`Packages/`·`ProjectSettings/`, Unity MCP.
- 2차(BETA·THETA·DELTA·OMEGA 겉장식) — 1차 확인 뒤 따로 승인.
- `docs/HANDOFF.md`·`docs/DECISIONS.md` 기록(MP2 세션과 같은 파일) — 이 문서 0절과 최종 보고에 둔다.
- 블록아웃 A, 봉이 왼쪽일 때의 `PoleSwing` 좌우 반전, D9.
- 브랜치 전환·커밋·푸시.
