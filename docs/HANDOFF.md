# 다음 작업 지시서 (인계)

> **다음 세션이 이어받을 일만 적는다.** 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 결정 이력은 `docs/DECISIONS.md`(이번 달)와 `docs/archive/DECISIONS-<YYYY-MM>.md`(지난 달), 설계는 `docs/DESIGN.md`, 구조는 `docs/ARCHITECTURE.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 고친다. **끝난 항목은 지운다** — 그 결정은 DECISIONS 에 있어야 하고, 없으면 지우기 전에 옮긴다. 날짜별 기록을 쌓지 않는다.
> 2026-11-12 문서 다이어트로 다시 썼다. 그 전 판(2026-10-05 ~ 2026-11-12, 283줄)은 `docs/archive/HANDOFF-2026-11-12.md` 에 그대로 있다.

## 1. 지금 상태 (2026-11-14)

- **마일스톤**: M5 (v0.7) "같이 달리는 맛" — MP1 끝, MP2 단계 A(봇전 판)·단계 B(몸싸움·출발 격자, DECISIONS 2026-11-14 "MP2 단계 B", 사용자 "접촉 충돌 잘 되었어") `dev` 병합 끝. `main` 은 v0.6 (M6, 2026-11-09). 순서·완료 조건은 `docs/DESIGN.md` §5-1.
- **MP2 단계 A** (DECISIONS 2026-11-13 "MP2 단계 A", 요청서 `docs/PROPOSAL_MP2_BOT_RACE.md`): `dev` `9a43927` 에 병합하고 2026-11-14 origin 에 푸시했다(기능 브랜치 `feature/mp2-bot-race` `b2389f4`). 사용자 플레이 확인 "다 잘 나왔다", `unity-reviewer` 반영, Codex 설계 리뷰 수정 뒤 봇전·필드 테스트 29/29 통과.
- **Codex 샌드박스 계약** (DECISIONS 2026-11-14 "Codex 샌드박스 계약"): `fix/codex-sandbox-contract` 를 `dev` 에 병합했다(2026-11-14, Codex 병합 전 리뷰 4건 반영). 이제 Codex `-Write` 는 `tools/codex-unity.ps1` 로만 부르고, Unity MCP 승인은 `-Unity` 를 줄 때만 열린다(§7-5).
- **v1.0 = PC 싱글 + 봇전(M5 까지), 하네스 실험이 공동 주목적** (DECISIONS 2026-11-12 "방향 재설정"). 멀티(M7)·모바일·아이템전은 v1.0 밖. UI 는 IMGUI 유지, `Runtime/Net/` 은 동결(컴파일만 유지).
- **MP1 끝** (DECISIONS 2026-11-12 "MP1 구조", "MP1"): 상대마다 서킷 씬 복사본을 격리 로드하고 `Race/RaceField` 가 같은 틱으로 돌린다. 상대 3명을 같이 돌린 플레이어 런이 혼자 런과 비트 일치(서킷 지문 `0xEF92FB76BFD1D767`). `dev` `8ffe3c6` 에 병합(기능 브랜치 `feature/mp1-race-field` `68ded91`), 병합 전 리뷰 지적 중 MP2 로 넘긴 것은 DECISIONS "MP1" 의 "병합 전 리뷰" 줄. 로비는 아직 상대를 주지 않는다 — 플레이로 확인할 것은 MP2 부터.
- **브랜치**: `dev` 는 origin 에 푸시돼 있다(2026-11-14, `feature/art-runner-blockout` 병합 `e60ea0e` 까지). 러너 아트(`art-src/RunnerKit/`, `art-src/Runner/` 5종·①② 두 판·헬멧·OMEGA 빨강 바이저, 요청서 `docs/ART_REQUEST_RUNNER.md`)는 이제 `dev` 에 있다 — 기능 브랜치 `feature/art-runner-blockout` `ee89f8f` 는 끝났다. origin 의 다른 기능 브랜치: `feature/mp2-bot-race`(위). 스파이크 `spike/mp1-isolated-worlds` (`57c54f4`, 복사본 1·4·8개 비용 측정 `ChainRushWorldCostTests`)는 C:/Users/User/Desktop/PCUBE PC 의 로컬에만 있고 `dev` 에 병합하지 않는다 — 프레임을 다시 잴 때·8인으로 늘릴 때 쓴다.
- **두 환경이 같은 작업을 한 일 (2026-11-14)**: D:/PCUBE 세션이 HANDOFF 2절 1번(MP2)을 처음부터 다시 구현했다 — 다른 환경의 단계 A 가 아직 푸시되지 않았었다. 그 작업은 `git stash` 로 D:/PCUBE 에 보관했다(`stash@{0}` "MP2a 중복 구현", 쓸 일이 없으면 지운다). 남길 값은 아래 2절 1번의 지름길 봇 랩타임뿐이다. 큰 구현을 시작하기 전에 다른 환경에 푸시하지 않은 같은 작업이 있는지 사용자에게 묻는다.
- **대기 중인 플레이 확인**: K3 킷 잎이 멀리서 성기게 보이는지(문제면 `blender-artist` 에 넓고 가벼운 잎), SUMMIT DROP 지름길 표지판 화살표가 실제로 어느 쪽이었는지, 튜토리얼 그림(2번 회전 그림의 호가 러너에서 시작하지 않음, 이동 스테이지에도 봉 표시가 떠 있음).
- **2026-10 에 답을 받지 못한 플레이 확인** (그 뒤 조작이 여러 번 바뀌어 다시 물을지는 사용자 몫): SKYWAY 2랩·고가 직선 슬라럼(봉 간격 40 m·걸리는 거리 16 m·한 번에 ⅓칸), 코너 중간 Shift·후진 중 봉 걸림, ↓ 후진 6 m/s·스윙 중 ↑, 그래플 거는 구간(좁으면 `GrappleController.minHangHeight` 1.05 를 낮춘다), 다른 러너로 매우빠름·STADIUM 에서 고스트가 보이는지.

## 2. 다음 작업 (각각 §3-2 요청서부터, 사용자가 고른다)

1. **MP2 봇전** — 요청서 `docs/PROPOSAL_MP2_BOT_RACE.md`. 단계 A·B 와 Codex 샌드박스 계약은 끝났다(1절). 남은 순서:
   - ④ 봇 난이도 3단계(사용자 "추후에 쉬움 포함 난이도 3개 정도", 지금 `CircuitBot` = 쉬움) — 아래 자동 개선 루프를 붙인다. 지문용 봇을 먼저 따로 고정한다(요청서 5절 끝 줄).
   - 참고 값 (2026-11-14, D:/PCUBE 에디터 6000.3.25f1, 보관한 중복 구현의 `RaceBotTests` — `CircuitBot` 에 `ShortcutBot` 의 지름길 스윙을 더한 봇, 빠름·ALPHA 혼자): STADIUM 86.70 s, SKYWAY 170.80 s, FINGER LANES 142.26 s(지름길 4회), PALM COAST 186.34 s, SUMMIT DROP 90.00 s(지름길 1회). 실행 기록 `20261114T055057Z-PlayMode-f6be824.xml`(67/67 통과).
   - **하네스 실험을 붙인다 — 봇 실력 자동 개선 루프** (사용자 2026-11-13 "6번은 붙이는거 핸드오프에 넣어줘"): D3b 를 사람이 값을 고르는 대신, 사람은 **목표(랩타임 목표값)와 금지선**(결정성 유지 — 서킷 지문, 봇이 플레이어가 못 하는 입력·정보를 쓰지 않기)만 정하고, 에이전트가 봇 값·전략 변경 → 배치 실행 → 랩타임 기록 → 채택/폐기를 반복한다. 요청서에서 정할 것: 목표값(사용자 67.16 s 기준 몇 %), 반복 상한(횟수·시간), 한 번에 바꾸는 값의 범위, 반복마다 남길 기록(바꾼 값·랩타임·지문)과 위치. 결과는 DECISIONS 에 반복 횟수·처음/마지막 랩타임·폐기 수로 남긴다(2026-11-13 "하네스 비용 기준선" 과 같은 방식).
2. **러너 캐릭터 Unity 반영** (DECISIONS 2026-11-14 "러너 캐릭터 아트"): 5종 FBX 후보가 `dev` 의 `art-src/Runner/` 에 있다(① `Runner.fbx` 발목 고정 / ② `RunnerFlatFoot.fbx` 발바닥 수평). 요청서 `docs/ART_REQUEST_RUNNER.md` 7절이 바꿀 코드 목록이다. 2026-11-14 헬멧 수정으로 닫힌 얼굴은 머티리얼 슬롯 3개(바이저 `M_RunnerVisor`, OMEGA 만 `M_RunnerVisor_Omega`)·열린 얼굴 2개다 — 기록은 같은 요청서 8절 끝. URP 에서의 바이저 광택은 미검증. §3-2 요청서에서 정할 것: ①② 중 어느 판, 뼈를 코드로(b) 또는 Animator(c, 첫 사례), `RunnerLook` 새 계약(공개 API), Unity 루트 회전 처리(`bake_space_transform=False`), 지금 모형 에셋 20개 삭제 여부. 반영 뒤 드로 배치를 다시 잰다.
3. **K1 RoadsideKit 남은 트랙**: PALM(판 0.947배, 화살표 3개, 판 어둡고 화살표 노랑) → SUMMIT(1.053배, 병합 트랙). 방법은 `docs/ART_REQUEST_PROPS.md` K1. K5·K2·K7·K6 은 보류.
4. **측정**: 트랙별 프레임·드로콜(PALM 렌더러 5,361, STADIUM 정점 약 98만, 재지 않음), MP1 복사본 프레임(에디터가 포커스 밖이라 판정 못 함 — Unity 창을 앞에 두고), 플레이어 빌드.
5. **서킷에서 쓰지 않는 코드 정리**: 공격 입력·체력·`CourseTarget`·`RunRules` 의 공격 값·`Enemy` 테스트 — 아이템전이 v1.0 밖이라 쓸 곳이 없다(삭제는 합의 대상).
6. **작은 후속 (`fix/` 로 묶는다, 테스트 필요)**
   - 튜토리얼 리뷰 후속 ①~⑦ (2026-11-06 `unity-reviewer`): ① 6번 스테이지에서 첫 목표 동안 스윙으로 게이지를 채우고 놓으면 Release 목표에 끝낼 스윙이 없다(다시 걸었다 놓으면 풀린다, `TutorialCourse.cs:177-179`) ② 줄 5 m 감기는 봉 8 m 안에서 건 줄로는 안 된다(`:161`) ③ `TryDress` 실패 때 고리가 해제되지 않는다(`RaceAssembler` 의 `TryDress` 호출, `setup.Mode` 로 판단) ④ 주석이 코드와 다르다(`TutorialDirector.cs:8-9`, `ChainRushHud.cs:147`) ⑤ `ChainRushHud.cs:595` 숫자가 상수를 베꼈다 ⑥ `GoalColor`·`BeaconColor` 중복, `Part`·`VisualPart` 중복 ⑦ 튜토리얼 테스트 이름이 2단(§1-3). 줄 번호는 2026-11-06 기준.
   - 녹음 리뷰 제안 4건 (DECISIONS 2026-11-09 "녹음을 못 쓰면"): `loadType` 메시지 중복, `Update` 의 `initialized` 가드, `UnloadAudioData`(스윙 원본 약 5 MB), WebGL 의 `GetData`(미확인).
   - `TestCallbacks` 가 실행마다 다른 XML 경로에 쓰기(지금은 한 파일을 덮어쓴다, `ChainRushSceneBuilder.cs` 의 `TestCallbacks`).
   - FBX 복사 → 임포트 → 확인 → 렌더 도구, 지름길 표지판(`ShortcutSign.fbx`) 트랙 배치.
7. **하네스·규칙 후보 (`.claude/`·`tools/` 변경, 따로 합의)**:
   - Codex 샌드박스 계약에서 확인 못 한 것(DECISIONS 2026-11-14): 10·11 뒤의 `-ResumeLast` 이어 가기(12 뒤 이어 가기는 2026-11-14 확인), 실제 사용량 소진 때의 출력, `-Unity` 를 준 실제 실행. 다음 Codex 쓰기 작업에서 처음 겪을 때 결과를 그 항목에 덧붙인다.
   - Codex 직접 사용 때 `codex -c mcp_servers.blender.enabled=true`(§7-5, DECISIONS 2026-11-14 "Codex 직접 사용 때 Blender MCP"): 대화형 세션에서 Blender 도구가 실제로 불리는지 미확인. 사용자가 처음 써 본 결과를 그 항목에 적는다.
   - **D:/PCUBE PC 의 Codex 설정에 Unity relay 서버가 둘**(`unity_mcp`·`unity-mcp`)이라 `-Unity` 가 실패한다(DECISIONS 2026-11-14 "Codex 의 Blender MCP" 발견). MP2 단계 B 를 이 PC 에서 Codex + Unity MCP 로 돌리기 전에 하나를 지운다(사용자 설정).
   - 그 밖의 후보: `unity-explorer`·`unity-reviewer` 의 Bash, `select-tests` 하한 규칙과 실제 실행 범위, SessionStart `resume` 트리거 미확인, 서브에이전트 안의 훅은 Bash deny 만 확인됨, 파견 0~1건인 에이전트 3종(`unity-explorer`·`unity-architect`·`unity-verifier`, DECISIONS 2026-11-13 "하네스 비용 기준선").
   - **재측정** (사용자 2026-11-13 "다 네 추천대로 ㄱㄱ"): **2026-11-27 이후, 그리고 2026-11-13 이후 시작한 세션이 10개 이상일 때** `python -I tools/harness-cost.py --since 2026-11-13` 으로 돌려 DECISIONS "하네스 비용 기준선" 의 표와 비교하고, 결과를 같은 항목에 덧붙인다.
     - 비교할 것: 요청당 컨텍스트 중앙값, 20만 넘는 요청의 입력 몫, 조회 분류. 세션마다 작업 종류(코드·아트·문서)를 붙여 같은 종류끼리 본다.
     - B모드 부작용: 인덱스를 내린 뒤 `.cs` Read 가 늘었는지 본다(기준선 385).
     - **먼저 할 것 (도구 변경, §3-2 합의)**: 지금 도구는 세션 경계 규칙(`CLAUDE.md` §8)이 지켜졌는지 세지 않는다 — 보고에 `새 세션 권장:` 이 붙은 횟수와, 그 뒤 실제로 새 세션이 시작됐는지를 지표로 넣는다.
8. **M7 (v1.0 밖, 다시 열 때 볼 것)**: R2 브랜치의 예측기 고장 기록, P3 C1 마무리 측정(3인 이상·100 ms 체감·패킷 유실·모바일/IL2CPP), 롤백 재실행 중 효과음(`docs/NEXT_REQUESTS.md` RF2 결정 2-1), `NetEndReplay` 테스트 탐색 다시 짜기(`CLAUDE.md` §9-2), C1c 의 따라잡기 S+L 상한 시험. MP1 이 격리 월드라 서버를 공유 월드로 다시 짤 필요는 없어졌다.

## 3. 사용자에게서 아직 받지 못한 것

플레이 확인 대기는 1절 끝에 있다(`gameplay-designer` 가 1절을 읽는다).

- **다시 만들 것**: STADIUM·SKYWAY·PALM COAST 트랙 소리(2026-11-05 사용자 "별로다", FINGER LANES 만 들어갔다). PALM COAST 구간별 바닥 2단계(마을 돌길, 다리 콘크리트)는 후보. 쇠사슬 효과음 3개는 들은 그대로 128 kbps 미리듣기 MP3 다 — 음질이 문제되면 원본 WAV(Freesound 로그인)로 바꾸고 씬 참조를 다시 연결한다(DECISIONS 2026-11-07 "효과음"). SUMMIT DROP 리뷰 후속은 DECISIONS 2026-11-06 "열린 코스와 1랩 다운힐".
- **씬 값 저장 (사용자 몫, 에디터에서)**: `ChainRushCircuit`·`ChainRushTestBench` 의 `turnSpeedLoss` 를 0.1 로 저장하면 `RunnerMotor.turnSpeedLossScale` 을 지울 수 있다(지금 인스펙터 0.2 × 배율 0.5). 두 씬의 `slingTurnRate: 90` 줄은 다음 저장 때 사라진다.
- **보류된 설계 결정**: 헤딩 범위 제한(계속 꺾으면 뒤로도 돈다 — 막을지 미정, DECISIONS T2a).
- **한 번 본 에러 (원인 미확인)**: `TrackDefinition: pole 0: the offset -20 must be within the road (6.0 m there).` (2026-11-04 14:18:29, Codex 수정 직후 1건, 재컴파일에서 다시 안 나옴). 다시 나오면 본다.
- **지울 로컬 브랜치** (사용자 2026-11-14 "안쓰는 브랜치 삭제" — D:/PCUBE 는 끝냈다, `feature/mp2a-bot-race` 를 지웠다): C:/Users/User/Desktop/PCUBE PC 에 남은 `docs/v1-direction`·`feature/mp1-race-field`(둘 다 `dev` 에 병합됨)는 그 PC 에서 지운다. `spike/mp1-isolated-worlds` 는 남긴다(1절).

## 4. 작업 흐름 메모 (지켜 온 방식)

- **장식 킷 (blender → Unity)**: `blender-artist` 가 `art-src/<Kit>/` 에 `.blend`·FBX(폴더는 메인이 먼저 만든다 — `save_as_mainfile` 은 폴더를 만들지 않는다) → `unity-implementer` 가 `Assets/_Project/Art/Models/` 로 복사·코드 수정 → 메인이 Unity MCP 로 임포트 확인·재빌드 메뉴·전후 렌더·그 프리팹을 읽는 테스트 → 사용자 플레이 → 커밋 → Codex + `unity-reviewer` → 병합. `ExteriorKit.KitMesh(kit, name)` 는 슬롯 하나·원점 가운데를 검사한다. FBX 는 `use_selection=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True` 로 내보낸다(Blender 앞면 −Y → Unity +Z, DECISIONS 2026-11-09 "Blender 시험 제작").
- **킷에서 배운 것**: 크게 늘려 멀리 두는 소품은 모양을 바꿔도 달리며 안 보인다 — 효과는 길가 소품·실루엣에서. 수백 번 찍히는 오브젝트는 Unity 정점(플랫이면 삼각형 × 3)으로 비용을 세고, 가려지는 디테일에 쓰지 않는다. FINGER·PALM 은 장식을 병합하지 않는다(나무마다 렌더러) — 정점과 렌더러 수를 함께 본다. SUMMIT 은 머티리얼별 한 메시로 병합한다. 비탈에 놓는 넓은 소품은 밑동 맞추기(`SummitDropEnvironmentBuilder.Rock`). 렌더할 소품 자리는 병합 메시 정점에서 찾는다.
- **장식 재빌드**: 프리팹 로컬 fileID 가 새로 나와 큰 diff 가 생기고 `TrackLook` 에 `music`·`ambience` 빈 줄이 붙는다 — 내용이 같으면 커밋하지 않는다. 재빌드 뒤 렌더가 이상하면(데이터는 정상) 에디터부터 다시 켠다.
- **Codex**: 구현·검증을 Codex 에 맡기면 `tools/codex-unity.ps1 -PromptFile <파일> -Write -RunId <새 이름>`(Unity 허락이 있으면 `-Unity`, §7-5). 스크립트를 `run_in_background` 로 띄우고 `tools/codex-wait.ps1 -Result <지시서>.<RunId>.result.json` 을 따로 띄워 기다린다(무활동 10분이면 3 — 보고 첫 줄 `Codex 멈춤:`, 리뷰는 stderr 를 `-Watch`). 종료 코드별로 할 일은 §7-5 표에 있다. Codex 의 Unity MCP 는 사용자가 그 작업에서 허락할 때만 연다("codex로 구현" 만으로는 허락이 아니다). 서버는 이름이 아니라 relay 를 실행하는 서버로 찾는다(PC 마다 `unity_mcp`/`unity-mcp`).
- **트랙 장식을 새로 만들 때 (2026-10-30 PALM COAST 방식)**: 레퍼런스 영상을 scratchpad 에 받아(yt-dlp `--js-runtimes node`) 1 s 접촉 시트로 색·소재를 읽고 → `TrackLook`(도로·벽·차선 표시) + `XxxEnvironmentBuilder`(`PalmCoastEnvironmentBuilder` 형식, 공통은 `ExteriorKit`·`ExteriorAssets`) → 발광 없는 차분한 색, `Hash` 로 자리마다 다르게. 도로 침범 검사는 축 정렬 경계라 대각선 도로에서 회전한 물체는 반경의 1.5배 밖에 둔다. 새 트랙은 빌더의 `BuildLook` 에 `ExteriorAssets.BuildSky` 를 넘긴다. 사용자 몫: 장식 씬 저장, 빌드 씬 목록(`ProjectSettings` 만 담은 브랜치), 로비 항목 저장. 기존 게임 트랙의 배치는 옮기지 않는다(DECISIONS 2026-11-12 "방향 재설정").
- **마일스톤 태그**: 태그마다 `TitleScreen.Version` 을 같이 올린다(DECISIONS 2026-10-29 "M3 범위 조정"). 다음은 v0.7.
- **`dev` 병합**: 훅은 `dev`/`main` 에서의 `git commit` 만 막는다. `git switch dev` 뒤 `git merge --no-ff <브랜치> -F <메시지 파일>` 은 막지 않는다. 충돌이 나면 `git commit` 으로 마무리할 수 없으니, 기능 브랜치에 `dev` 를 먼저 병합해 거기서 푼다.

## 5. 환경 메모 (다음 세션이 헷갈릴 수 있는 것)

- **두 PC 와 버전 차이**: `D:/PCUBE/ProtoHarness` 의 에디터는 6000.3.25f1(결정 버전), `C:/Users/User/Desktop/PCUBE/ProtoHarness` 는 6000.3.19f1 이라 열면 `ProjectVersion.txt`·`packages-lock.json`·`ProjectSettings.asset`·`ProtoHarness.slnx` 가 바뀌어 보인다 — 커밋하지 않는다. 검증 보고에는 실제로 돌린 에디터 버전을 적는다. 끝낼 때마다 푸시하고, 시작할 때 `git fetch` 와 `git status -sb` 로 갈라짐부터 본다(2026-10-30 다른 PC 의 푸시 안 한 v0.5 를 모르고 작업한 일). 원격 브랜치를 셀 때는 `git ls-remote --heads origin` 이나 `git fetch --prune` 뒤에 본다.
- **같은 작업 트리를 다른 세션이 쓸 수 있다**: 브랜치를 바꾸기 전에 `git branch --show-current`·`git status` 로 다른 세션의 커밋 안 한 파일이 없는지 본다(2026-11-09 아트 세션이 리뷰 도중 브랜치를 바꿨다).
- **사용자가 같은 에디터에서 플레이한다**: Refresh·테스트 시작 전에 `EditorApplication.isPlayingOrWillChangePlaymode` 를 확인하고, 플레이 중이면 시작하지 않는다(플레이 중 재로드로 로비 NRE).
- **사용자가 작업 중 에디터를 닫을 수 있다**: `Unity_RunCommand` 가 `Named pipe socket file not found` 로 끊기면 `Temp/UnityLockfile` 이 없는지 보고 배치 모드(`-executeMethod …`, `-runTests -testFilter …`)로 이어 간다(VERIFICATION §4-3 B). 에디터를 다시 열지 말라고 먼저 알린다.
- **테스트 결과 XML**: Unity 의 `Path.GetTempPath()/ChainRush-PlayMode-results.xml` 하나에 덮어쓴다(EditMode 도 같은 파일). PC 마다 다르다 — `C:/Users/User/Desktop/PCUBE` PC 는 `C:\Users\User\AppData\Local\Temp\`(2026-11-12 `Unity_RunCommand` 로 확인), `D:/PCUBE` PC 는 `C:\Users\Public\Documents\ESTsoft\CreatorTemp\`(2026-11-14 `Unity_RunCommand` 의 `Path.GetTempPath()` 로 확인). 실행할 때 `Path.GetTempPath()` 로 구한다. 실행마다 `tools/test-runs.ps1 -Record` 로 남긴다(§4-4). 같은 PC 에서 두 에디터로 동시에 테스트를 돌리지 않는다.
- **Unity MCP**: 도메인 리로드마다 브리지가 몇 초 끊긴다. 에디터가 백그라운드면 스크립트를 자동 임포트하지 않을 수 있다 → `AssetDatabase.Refresh()`. 컴파일 확인은 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 의 `Tundra build success/failed` 줄(새로고침 **전** 줄 수를 세어 둔다). `Unity_RunCommand` 는 `System.Reflection` 을 막는다.
- **네트워크 레이스** (M7): 도는 중에는 `Unity_RunCommand` 를 부르지 않는다(2026-10-12 예측기 고장). 서버·클라 겸용 플레이어는 `BuildPipeline.BuildPlayer`(서킷 씬만, 출력은 리포 밖 `Path.GetTempPath()/ProtoHarnessRaceBuild/`). 에디터에는 `NetworkManager` 가 프로세스당 하나라 두 번째 클라는 별도 프로세스다.
- **플레이어 빌드는 Unity 가 `ProjectSettings` 를 자동으로 바꾼다**(`ProjectSettings.asset`·`UnityConnectSettings.asset`·`Assets/Settings/*.asset`). 커밋하지 않고 파일을 지정해 되돌린다. 빌드 중 임시 `Assets/Resources/` 가 생겼다 사라진다.
- `Assets/DefaultNetworkPrefabs.asset`(NGO 가 만든 빈 목록)은 커밋돼 있다. `.codex/agents/` 가 수정으로 보이면 `autocrlf` 표시다(내용은 HEAD 와 같음).
- **세션 기록 보존**: Claude Code 는 세션 기록(`~/.claude/projects/<리포 경로>/*.jsonl`)을 기본 30일 뒤 지운다(`cleanupPeriodDays`). `tools/harness-cost.py` 의 재측정에 기록이 필요해 사용자 설정 `~/.claude/settings.json` 에 `"cleanupPeriodDays": 365` 를 넣는다 — `C:/Users/User/Desktop/PCUBE` PC 는 2026-11-13, `D:/PCUBE` PC 는 2026-11-14 에 넣었다(사용자 설정이라 리포에 없다).
- `index/symbols.tsv` 는 2026-11-13 부터 선택 도구다(B모드는 코드 `Grep`). SessionStart 훅은 더 이상 인덱스가 낡았다고 알리지 않는다. 쓴다면 먼저 재생성한다(§6-2).
- 훅 3개(`.claude/settings.json`, DECISIONS 2026-10-10 "하네스 자동화"): 되돌릴 수 없는 git 명령·`main`/`dev` 커밋은 deny, 보호 파일 Edit/Write 는 ask, 에이전트 정의를 고치면 `.codex` 어긋남 알림. 훅이 막으면 우회하지 말고 사유를 보고한다.
- GitHub Desktop 이 켜져 있으면 `.git/index.lock` 이 남을 수 있다. 실행 중인 `git.exe` 가 없을 때만 지운다.
- 서브에이전트는 `.claude/agents/*.md` frontmatter 의 `model`·`effort` 가 원본이다(DECISIONS 2026-10-10 "서브에이전트 모델·노력 고정").
