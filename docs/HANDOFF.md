# 다음 작업 지시서 (인계)

> 작성 2026-10-05, 갱신 2026-10-14 (테스트 3단 규칙·선택 도구 / 2026-10-13: P3 스파이크·C1a 격리 월드·C1b 2인 네트워크 레이스·하네스 훅·C1c-1 런 종료 알림·C1c-1b·예측기 구멍 fix·C1c-2a 순위·C1c-2b 순위 표시·네트워크 레이스 A 기준 fix·C1c-3 진행 중 순위·다른 입력의 두 레이서·같은 시각 출발). 이 문서는 **다음 세션이 이어받을 일**만 적는다. 규칙은 `CLAUDE.md` 와 `docs/RULES/` 가 원본이고 여기서는 § 번호로만 가리킨다. 설계는 `docs/COURSE.md`, 결정 이력은 `docs/DECISIONS.md` 가 원본이다.
> 이 문서는 구현 승인이 아니다. 다음 작업도 §3-2 합의 요청서부터 시작한다.
> 작업을 넘길 때마다 이 문서를 갱신한다. 끝난 항목은 지우고 DECISIONS 로 옮겨진 것을 확인한다.

## 1. 지금 상태 (2026-10-13)

- 브랜치: `dev` 에 P3 준비(조사·롤백 안전성·정규화 비용), **P3 스파이크 보고(`a50d18e`)**, **C1a 격리 월드(`cc01b50`)**, **NGO 2.13.3 패키지(`0b82595`)**, **C1b 2인 네트워크 레이스(`bad7159`)**, C1c-1·C1c-1b·예측기 구멍 fix·C1c-2a 순위(`28a9fc7`)·**C1c-2b 순위 표시(`eb4d0d7`)**·네트워크 레이스 A 기준 fix(`6c08f88`)·**C1c-3 진행 중 순위**(`feature/p3-c1c3-running-order`, 검증은 DECISIONS 2026-10-13 "P3 C1c-3") 가 병합돼 있다. **다른 입력의 두 레이서**(레이스 C, 테스트만, `75b998d`)도 병합돼 있고 `origin/dev` 로 푸시했다(2026-10-13). **같은 시각 출발 + 카운트다운**은 `feature/p3-c1-same-start` 에서 작업 중이다(DECISIONS 2026-10-13 "P3 C1 같은 시각 출발"). 스파이크 브랜치(`spike/p3-ngo-prediction`)와 옛 패키지 브랜치(`feature/p3-ngo-package`)의 코드는 `dev` 에 `Net` 이름으로 이식됐고, 두 브랜치는 2026-10-15 확인 때 로컬·원격 어디에도 없었다(3절 #6).
- 구현된 것 요약
  - 트랙 좌표계 `Centerline`(루프·초점), 이동·드리프트·체인 액션, 절차 노면(T3a)·시드 생성기(T3b)·절차 무한 코스(T3c)·서킷(T4), 직선 무한 코스 정리(T3d).
  - **P2** (DECISIONS 2026-10-08 "P2"): 입력 기록·재생(`InputLog`·`InputRecorder`·`InputReplay`, `Control/`)과 상태 트레이스 비교(`StateTrace`, 테스트). **같은 시드 + 같은 입력 = 같은 런이 같은 머신·같은 에디터에서 비트 단위로 성립한다**: 서킷 1700틱·절차 코스 2500틱을 같은 세션(6배속)·씬 재로드(2배속)·별도 세션에서 모두 재현했다.
  - **롤백 안전성** (DECISIONS 2026-10-08 "롤백 안전성"): 서킷에서 한 틱 스냅샷·복원(`SimSnapshot`, `StepTick`)과 "되감아 다시 해도 같은 결과"를 비트 단위로 증명했다(지상·공중·그래플·슬링·코너 스윙·랩 이음매, 한 프레임 20회 되감기). **`CharacterController` 의 내부 위치가 `transform.position` 보다 정밀해 처음엔 1 ULP 어긋났고, 매 틱 컨트롤러를 껐다 켜서 막았다. 그 대가로 P2 재생 해시가 바뀌었다**(서킷 `0xF9A914DA3FAA680B`, 절차 코스 `0xE81912109A2EB020`). 정규화 비용은 껐다 켜기 1회 2.94 µs, 한 틱 28.2 µs 로 틱 예산(20 ms)의 1% 미만이라 병목이 아니다(DECISIONS "컨트롤러 정규화 비용 측정", 에디터 안 값).
  - **P3 스파이크** (DECISIONS 2026-10-10 "P3 스파이크"): NGO 2.13.3 으로 서버 1(별도 스탠드얼론 프로세스) + 클라 1(에디터)이 서킷에서 같은 입력으로 **비트 단위로 같은 1001개 상태**를 냈고(보정 0회), 서버가 입력을 놓치면 클라가 `RestoreSnapshot` + 입력 재실행으로 보정한다(보정 1회 평균 약 0.4 ms). 다른 기기·IL2CPP·모바일 일치와 100 ms 에서의 체감은 확인하지 못했다.
  - **C1b 2인 네트워크 레이스** (DECISIONS 2026-10-10 "P3 C1b-0·C1b-1"): `Runtime/Net/`(asmdef `ProtoHarness.Net`). 서버(`RaceServer`)는 레이서마다 격리 월드 1개·입력 버퍼·지연 큐를 갖고, 한 레이서의 상태는 주인에게 통째로, 위치는 상대에게 보낸다. 클라(`RaceClient`)는 `ClientPredictor`(`TickCompleted` 구독)로 자기만 예측·보정하고 상대는 `RemoteInterpolator` + `RemoteGhost`(런타임에 내 러너 모형 `RunnerTilt.Body` 를 복제, 2026-10-15 R9 전에는 캡슐)로 그린다. 같은 플레이어 빌드가 `-raceServer`/`-raceClient` 로 서버도 클라도 된다(`RaceBootstrap`). 입력 로그는 `InputLogFile`(`Control/`)로 파일에 저장·로드한다. **고정 지연(RTT 100 ms)에서 에디터·플레이어 클라 둘 다 보정 0회, 상대 위치 936개가 자기 런과 비트 일치, 고스트 최대 한 프레임 0.27 m.**
  - **C1c-1 런 종료 알림** (DECISIONS 2026-10-11 "P3 C1c-1"): 서버가 런이 끝난 틱에 `race.end`(틱·이유·그 틱에 쓴 입력)를 주인 클라에 보내고, 클라(`ClientPredictor.OnServerEnd`)는 같은 결말이면 그대로, 아니면 T-1 로 되감아 서버 입력으로 틱 T 를 다시 돈다. 재현 못 하거나 종료 처리에서 예외가 나면 예측기가 고장(`Faulted`) 상태가 되고 클라가 멈춘다. 지터 레이스에서 낙하한 레이서의 클라가 타임아웃 없이 `ended=Failed@657` 로 끝났다. 클라만 먼저 끝나는 경우는 C1c-1b 에서 막았다(아래).
  - **C1c-1b 클라만 먼저 끝나는 경우** (DECISIONS 2026-10-11 "P3 C1c-1b"): 클라 런이 끝난 뒤 그 틱 이후의 서버 상태가 오면 서버 상태로 되돌아가 기록 입력으로 다시 돌고, 끝날 때의 앞섬만큼 live 입력으로 따라잡는다(목표 틱 상한). 지금 서킷에서는 서버도 다음 틱에 끝나 따라잡기는 최대 1틱이다(S+L 상한 경로는 테스트로 끝까지 돌지 않았다).
  - **예측기 구멍 fix** (DECISIONS 2026-10-11 "P3 C1c 예측기 구멍 4개"): 예측기가 입력을 내준 뒤의 `Clear`(R·Enter·HUD 의 `StartRun`)는 재시작이라 고장이 되고, 고장은 무엇으로도 풀리지 않는다(새 경기 = 새 예측기). `OnServerState` 의 모든 예외도 고장을 세운다. `LargestJolt` 는 일시정지 중에도 그 틱의 예측 상태에서 잰다(DONE 줄 `jolt=` 를 다시 근거로 쓸 수 있다). 끝난 클라가 더 이른 서버 상태로 되살아나도 따라잡는다.
  - **C1c-2a 순위** (DECISIONS 2026-10-11 "P3 C1c-2a"): 서버가 `RaceStandings`(레이서별 대기·달리는 중·결승·낙하, 순위, 확정)를 갖고 바뀔 때마다(출발·끝·확정) 모든 클라에 `race.standings` 를 보낸다(각 클라 자신의 `ToOwner` 큐, 자기 `race.end` 뒤). 순위는 **각자 출발부터 결승까지의 틱 수**, 같은 틱은 공동, 낙하는 순위 없음. 다른 레이서가 아직 그 틱 수 안에 결승할 수 있으면(대기 중이거나 그 틱을 아직 못 넘김) 잠정이다. 클라(`RaceClient.Standings`·`You`)는 받기만 하고 런은 읽지 않는다.
  - **C1c-2b 순위 표시** (DECISIONS 2026-10-12 "P3 C1c-2b"): `RaceClient` 가 `showRemote` 일 때 `RaceStandingsView`(Net, OnGUI, 런타임 추가)를 붙여 HUD 헤더 아래 왼쪽에 순위 줄을 그리고(`RaceStandingsText`), 상대가 결승·낙하하면 고스트를 숨긴다. 패널의 첫 그리기(약 130 ms)는 접속 전에 투명하게 해 둔다 — 첫 순위 때 처음 그리면 에디터 클라가 약 95 ms 멈춰 입력이 서버에 늦게 닿았다. 고스트 숨김은 실제 지터 레이스에서 양쪽 다 일어났다(낙하 `Failed@657`: 2026-10-12 플레이어 클라, 2026-10-13 에디터 클라).
  - **C1c-3 진행 중 순위** (DECISIONS 2026-10-13 "P3 C1c-3"): 서버의 `RaceOrder`(Net)가 달리는 레이서끼리 **각자 런의 같은 틱**에서 진행 거리(`LapCounter.Progress`)를 비교한다(모두가 도달한 가장 최근 틱). 1 m 넘게 앞서야 순서가 바뀌고, 거리가 같으면 공동이다. 바뀔 때만 `race.order` 로 모든 클라에 보낸다(각 클라의 `ToOwner` 큐, standings 뒤). 클라(`RaceClient.Order`)는 받아 두기만 하고, 패널은 주행 중 레이서를 결승자 다음 순위로 숫자를 보인다(`RaceStandingsText`). 결승 순위(`RaceStandings`)와 그 메시지는 바꾸지 않았다.
  - **다른 입력의 두 레이서** (DECISIONS 2026-10-13 "P3 C1 다른 입력의 두 레이서"): `NetworkRaceTests` 레이스 C 가 같은 봇에 드리프트 구간만 달리 준 두 런을 돌린다. 오프라인에서 같은 `RaceOrder` 로 만든 순서 변화(`0:동률 → 155:에디터 앞섬 → 697:추월`)를 서버·두 클라가 그대로 지났다 — **실제 레이스에서 주행 중 순위가 처음 뒤집혔다.** 보정 0 / 0, 상대 위치 1001개가 오프라인 런과 비트 일치. 레이스 C 1회와 A 1회는 에디터 프레임 한 번이 125~136 ms 멈춰 늦은 입력(7, 5)으로 실패했다 — 사용자 판단은 에디터 포커스 없음(미확인).
  - **같은 시각 출발 + 카운트다운** (DECISIONS 2026-10-13 "P3 C1 같은 시각 출발"): 서버는 전원이 접속하면 `race.go` 를 보내고, 모든 레이서의 입력이 쌓인 프레임에 한꺼번에 출발시킨다(레이스 A·B·C 모두 두 레이서 같은 프레임). 클라는 신호를 받으면 3·2·1 카운트다운 뒤 출발하고, 경기 내내 `ManualStart=false` 라 R·RUN AGAIN 이 막힌다. 싱글도 Enter·R·HUD 버튼이 카운트다운을 거친다(`ChainRushGame.StartCountdown`). `StartRun()` 은 그대로 즉시 출발이라 P2 해시는 같다.
  - **C1a 격리 월드** (DECISIONS 2026-10-10 "P3 C1a"): 레이서마다 서킷 씬 복사본을 `RaceWorld.IsolatedLoad`(Additive + Physics3D)로 로드하면 각자 자기 물리 씬을 가지며, 두 복사본을 함께 돌려도 각자 솔로와 비트 단위로 같다(공유 물리 월드 대조군은 757틱에서 갈라진다). `RunnerMotor.SnapToGround`·`GrappleController` 의 물리 조회는 오브젝트가 속한 씬의 물리 월드를 쓴다. `ChainRushGame.TickCompleted(int)` 는 틱이 끝난 틱 사이 상태에서 스냅샷을 찍는 자리다(`Consume` 안에서 찍으면 틱 사이 상태가 아니다). 복사본 로더(에디터/플레이어)는 만들지 않았다.
- 씬 3개: `ChainRushProcedural`(절차 무한), `ChainRushCircuit`(서킷), `ChainRushTestBench`(테스트 작업대, 2026-10-14 `ChainRushPrototype` 에서 이름만 바꿈, 2026-10-15 R3 에서 첫 구간만 남기고 결승선·구 머리 캐릭터를 걷어냄). 세 씬의 러너 모형은 프리팹 `ArmoredRunner` 하나다.
- 테스트: EditMode 237, PlayMode 148(Device 3 + Sweep 1 + `[Explicit]` 네트워크 레이스 3 + 게이트 141, `feature/p3-c1-same-start` 기준). 병합 조건 실행은 Device·Sweep 제외 144건 중 141 통과·3 skipped(Explicit), 이 머신(19f1)에서 약 580~660초/회(이전 머신 약 556초/회, 121건). `Net` 카테고리로 고르면 `[Explicit]` 네트워크 레이스도 같이 돈다(2026-10-11 확인). 20 시드 스윕(`Run Course Sweep`) 약 933초. 네트워크 레이스는 플레이어 빌드(`Path.GetTempPath()/ProtoHarnessRaceBuild/RaceBuild.exe`, `BuildPipeline.BuildPlayer` 로 서킷 씬만)가 있어야 하고 이름으로 골라 돌린다(약 90초).
- **테스트 3단** (DECISIONS 2026-10-14 "테스트 3단", `docs/test-tiers`): 병합(`feature → dev`)은 PlayMode 전체(`Long` 제외, 코어 변경이면 `Long` 도) **1회**, 2회차·Sweep·Device·네트워크 레이스는 마일스톤(`dev → main`)으로 옮겼다(`CLAUDE.md` §9-2). 작업 중 빠른 확인은 `tools/select-tests.ps1` 이 바뀐 파일로 고른 테스트(병합 근거 아님). 도구가 만든 필터는 Unity 에서 실행해 고른 테스트만 도는 것을 확인했다(2026-10-14, 18건 78초, VERIFICATION §4-4).

## 2. 다음 작업 (후보, 사용자 승인 대기)

**2026-10-14: 다음 작업은 `docs/NEXT_REQUESTS.md` 에 합의 요청서(R2~R9, R0 문서·저장소 정리와 R1 테스트 안정화는 2026-10-15 끝남)로 정리했다 — 권장 순서·선행·검증 시간·사용자 결정이 거기 있다.** 아래는 그 전의 후보 목록(배경 설명)이다.

**2026-10-15 서킷 집중** (DECISIONS 같은 날 "서킷 집중"): 무한 모드와 멀티의 새 기능을 멈추고 서킷(속도전) 싱글을 먼저 다듬는다. 테스트·병합 조건은 그대로다. R6·R7 은 보류다. 플레이어 모형은 네모(`Armored Runner`) 하나로 맞춘다 — 네트워크 상대는 R9, 작업대와 꺼진 둥근 모형은 R3. 아래 목록 가운데 무한 모드(공격 판정 방향)·멀티(C1 마무리 측정, C2) 항목은 다시 열 때까지 손대지 않는다.

**2026-10-15 플레이 경험 순서 로드맵** (DECISIONS 같은 날, `docs/DESIGN.md` §5-1): M1 한 판이 끝까지 돈다(v0.2) → M2 다시 할 이유(기록·고스트) → M3 고를 거리(트랙 2~3개) → M4 같이 달리는 맛(AI 상대) → M5 멀티 재개. 지금은 **M1** 이다(`docs/NEXT_REQUESTS.md` R4 → R5 → M1-T). 실제 네트워크 레이스(플레이어 빌드 + 레이스)는 M5 까지 돌리지 않는다. 마일스톤 조건에는 네트워크 레이스 대신 사용자 플레이 확인이 들어갔다(`CLAUDE.md` §9-2 ③).

C1c-3(진행 중 순위)와 다른 입력의 두 레이서(레이스 C)까지 `dev` 에 병합했다(위 1절). 아래는 다음 후보이고, 권장 순서는 위에서 아래다. 새 요청서(§3-2)를 먼저 올린다.

- **설계 제안서 3건의 남은 단계 (모두 Unity 필요)**: ① ~~`Long` 묶음~~ 끝남(DECISIONS 2026-10-14 "Long 묶음": 게이트 651 → 428초, 코어 변경이면 `Run Long Tests` 198초 추가). ② 맵 정리 — 2단계(이름 `ChainRushTestBench`) 끝남, 3단계(발판·결승선·표적 걷기, 테스트 삭제·합치기)가 남았다. ④ **코너 봉 스윙** (`docs/PROPOSAL_CORNER_POLE.md`, 2026-10-14 사용자 결정 7개 반영, 승인 대기): 지금 코너 스윙을 "코너 중앙의 봉 + 고정 줄 길이 + 관성 + Ctrl 유지" 로 대체. P1(시뮬) → P2(표현) → P3(절차 코스). ③ ~~코너 스윙 S1 연출~~ 끝남(DECISIONS 2026-10-14 "코너 스윙 S1 연출"). S1b·S2 는 **하지 않는다** — ④ 봉 스윙으로 대체(DECISIONS 2026-10-14 "코너 스윙을 봉 스윙으로 대체"). 각 단계 §3-2 합의.
- ~~떨어지면 트랙으로 복귀~~: 설계 결정 끝, 구현은 `feature/circuit-fall-respawn`(`docs/NEXT_REQUESTS.md` R2). 남은 후속 후보: 복귀 연출(깜빡임·카메라), 정지 시간 체감 튜닝, 정지 중 공격 입력(지금은 `ChainRushGame.Attack` 이 정지와 무관하게 돈다 — 서킷엔 대상이 없어 링·소리만 난다).
- ~~진행 중 순위의 앞뒤 거리 표시~~: **하지 않는다**(2026-10-13 사용자 "두 유저 사이의 거리는 필요없을거같다"). ~~같은 시각 출발~~ 과 ~~네트워크 클라의 HUD 재시작 버튼~~ 은 `feature/p3-c1-same-start` 가 다룬다(경기 내내 `ManualStart` 를 꺼 R·RUN AGAIN 을 막는다).

- **C1c 의 남은 것**: 따라잡기의 S+L 상한은 표적·충돌 같은 다른 종료 조건이 생기면 다시 시험한다(지금 서킷에서는 만들 수 없다). `RaceClient.OnState`·`OnEnd` 에서 예측기에 넘기기 전 디코딩이 던지는 손상 바이트는 고장을 세우지 않는다(Netcode 가 예외를 로그로 남김).
- **공격 판정 방향 (이슈, 설계 결정 대기)**: 공격 판정이 몸 방향도 트랙 방향도 아닌 **월드 +z** 를 앞으로 본다. `CourseTarget.TryHit`(`CourseTarget.cs:53-54`)은 플레이어 기준 월드 z 로 -0.5 ~ 5 m, |x|·|y| ≤ 2.5 m 상자다. 직선 코스 전제라 자유 조향·커브에서 바라보는 쪽과 맞는 쪽이 갈라진다. 공격 링 `Strike Arc` 는 루트 자식(local z=1.6, `ChainRushSceneBuilder.cs:159-161`)이라 프로토타입에서는 판정과 같은 +z 를 보여 주지만, 서킷은 루트가 출발선 방향으로 돌아가 있어(`ChainRushCircuitSceneBuilder.cs:109`) 링과 판정이 어긋날 수 있다(서킷에 공격 대상이 있는지는 미확인). 링만 몸 방향으로 돌리면 판정과 더 어긋나므로 하지 않았다(2026-10-10, `fix/runner-heading-visual` 에서 몸만 `Heading` 을 보게 함). 정할 것: 판정 기준을 몸 방향(`Heading`)으로 할지 트랙 진행 방향으로 할지. 판정은 시뮬레이션(`ChainRushGame.cs:281` 의 `racer.AddHit`)이다. P2 재생 해시에 영향이 있는지는 확인하지 못했다 → 요청서(§3-2)부터. 링은 같은 기준으로 같이 돌린다.
- **P3 C1 마무리 측정**: (서로 다른 입력의 두 레이서는 레이스 C 로 끝남) 3인 이상 서버, 100 ms 에서의 체감(프레임 시간·입력 지연), 패킷 유실(앱 수준 지연 큐는 유실을 모사하지 못한다), 모바일/IL2CPP 빌드의 일치.
- **P3 C2 (충돌·추월)**: 격리 월드로는 못 한다. 공유 월드가 필요하고 `CircuitRace` 의 월드/레이서 분리, `Centerline` focus 의 레이서별 분리가 선행이다(DECISIONS "P3 C1a" 의 버린 계획 이유).
- **P2 후속(고스트)**: 입력 로그 파일 저장은 `InputLogFile` 로 끝났다. 남은 것은 최고 기록 저장과 겨루기(고스트 재생)와 UI.
- **T3e 장식**, **T5 보충 모듈**(`docs/COURSE.md`).
- 체감 튜닝: 사용자가 직접 달려 본 피드백 대기(원격이라 보류).
- **하네스**: ① SessionStart 의 `resume` 트리거 미확인(`clear`·`startup`·`fork` 는 확인됨). 2026-10-10 백그라운드 세션을 `dev` 위에서 resume 했을 때 transcript 에 `SessionStart:resume` 기록이 없었다. dev 커밋은 PreToolUse guard 가, 낡은 인덱스는 LOOKUP §6-2 가 따로 막으므로 우선순위 낮음 ② 서브에이전트 안의 훅은 Bash deny 만 확인됨(DECISIONS 2026-10-10 "하네스 자동화" 한계). `ask`·PowerShell·PostToolUse 는 미확인 ③ C모드 재논의: 우리 `.cs` 가 95개(2026-10-10 `git ls-files`)로 §6-3 의 재논의 트리거(60개 초과)를 넘었다. 만들지는 별도 합의.

## 3. 보류된 사용자 결정

| # | 주제 | 상태 | 참고 |
|---|---|---|---|
| 1 | ~~병합 조건 검증 줄이기~~ **해결** | 2026-10-14 테스트 3단으로 대체했다: 병합은 PlayMode 1회, 2회는 마일스톤(`CLAUDE.md` §9-2, DECISIONS 2026-10-14 "테스트 3단"). 그 전 2026-10-13 "병합 2회차 축소"(Net 만 바꾸면 2회차 `Net` 만)는 지웠다. D(장거리 테스트 배속)는 하지 않았다 — 원하면 별도 합의 | 2026-10-14 |
| 2 | 물리 값 체감 튜닝 | 전부 계산으로 정한 시작값. 사용자 플레이 피드백 대기 | 아래 표 |
| 3 | 헤딩 범위 제한 | 계속 꺾으면 뒤로도 돈다(가드에 닿으면 앞으로 돌려짐). 막을지 미정 | DECISIONS T2a |
| 4 | 기존 PlayMode 테스트의 착지 확인 | `StartRun` 직후 `IsGrounded` 가 순간이동 전 발판 값을 돌려줘 "착지" 확인이 노면 없이도 통과한다(T3a 에서 발견). 새 테스트만 고정 틱 1회 뒤 확인으로 고쳤다. 기존 테스트(`ChainRushCurveTests` 등 `StartOn`)를 고칠지 **별도 `fix/` 합의 필요** | DECISIONS T3a |
| 5 | ~~원격 `origin/dev` 와 갈라짐~~ **해결** | `feature/integrate-origin-dev`(`138446a`)로 통합하고 `dev` 를 푸시했다(경위는 DECISIONS 2026-10-09 "코스 T3b (다른 클론…)"). 백업 원격 `backup/dev-2026-10-10` 은 `dev` 에 들어 있음을 확인하고 2026-10-15 사용자 지시로 지웠다. 남은 결정: 앞보기 300m 를 로컬 생성기에 옮길지(별도 합의) | 2026-10-15 |
| 6 | ~~끝난 브랜치 정리~~ **해결** | 2026-10-15 사용자 지시로 `dev` 에 병합된 것을 지웠다: 로컬 `docs/design-proposals`·`docs/test-tiers`·`feature/test-long-tier`·`feature/scene-test-bench`·`feature/corner-swing-look`, 원격 `docs/design-proposals`·`backup/dev-2026-10-10`. `spike/p3-ngo-prediction`·`feature/p3-ngo-package`·`feature/course-t3c-endless` 는 그때 이미 없었다. 남은 브랜치 중 `dev` 에 병합 안 된 것은 `feature/circuit-fall-respawn`(R2)뿐이다. 병합됐지만 남았던 로컬 `docs/branch-cleanup-record`·`docs/corner-pole-proposal`·`docs/main-v0.1`·`feature/p3-c1-same-start` 도 같은 날 사용자가 지웠다 | 2026-10-15 |
| 7 | ~~미추적 `Assets/DefaultNetworkPrefabs.asset` (+`.meta`)~~ **해결** | NGO 가 자동 생성한 빈 목록(`IsDefault: 1`). 2026-10-10 사용자 "a" 대로 C1c-1 브랜치(`feature/p3-c1c-run-end`)에서 내용 수정 없이 커밋하고 게이트를 같이 치렀다(DECISIONS 2026-10-11 "P3 C1c-1"). `ProtoHarness.slnx` 는 Unity 가 다시 만드는 파일이라 수정으로 보여도 커밋하지 않는다 | 2026-10-11 |
| 8 | `dev → main` 병합과 태그 | 마일스톤마다만(`CLAUDE.md` §9). 2026-10-13 `main` 을 P3 C1(2인 네트워크 레이스·같은 시각 출발)까지 올리고 첫 태그 `v0.1` 을 달았다(DECISIONS 2026-10-13 "main v0.1"). 그 전 `main` 은 `7e4a0a8`(P0·P1-1·P1-2). 다음 병합도 사용자가 시킬 때만 | 2026-10-13 |
| 9 | `.claude/settings.json` (읽기 전용 허용 8개 + 훅 3개) | 허용 목록은 `/fewer-permission-prompts` 로 만들어 2026-10-10 사용자의 "커밋" 지시로 커밋했다(`.claude/` 변경). 범위가 마음에 안 들면 줄인다. 훅은 `docs/harness-automation` 에서 추가(DECISIONS 2026-10-10 "하네스 자동화") | 2026-10-10 |

튜닝 대상 시작값 (`RunnerMotor`/`FollowCamera`/`GrappleController` 인스펙터, 씬 값이 없으면 코드 기본값):

| 묶음 | 값 |
|---|---|
| 조향 | `maxTurnRate` 120°/s, `turnAcceleration` 1200°/s², `airTurnScale` 0.5, `sideGrip` 60, `airSideGrip` 18 |
| 경사 | `slopeSpeedFactor` 1.5, 속도 배율 0.7 ~ 1.3, `groundSnapDistance` 0.3 |
| 가드 | `guardSpeedLoss` 0.15 |
| 드리프트 | `driftGrip` 12, `driftTurnScale` 1.3, `driftSpeedScale` 0.9, `slideMetersPerSlot` 6 |
| 슬링샷 | `slingLead` 18, 당김 0.4 s · 45 m/s² · 최대 20, `slingTurnRate` 90°/s, 유지 0.8 s · 16 |
| 그래플 강화 | `empoweredRetractSpeed` 12, `empoweredReleaseSpeed` 18 |
| 코너 스윙 | `swingSpeed` 14, `swingAcceleration` 20, `swingMaxTime` 2, `swingExitSpeed` 17, `swingExitCarryTime` 0.6 |
| 카메라 | `maxYawSpeed` 180°/s |

## 4. 환경 메모 (다음 세션이 헷갈릴 수 있는 것)

- 작업 머신이 둘이다. `D:/PCUBE/ProtoHarness`(2026-10-10)의 에디터는 **6000.3.25f1**(결정 버전과 같음)이고 Hub 에 18f1·25f1 이 있다. `C:/Users/User/Desktop/PCUBE/ProtoHarness`(C1c-2b 를 한 곳, 2026-10-12)는 **6000.3.19f1** 이라 열면 `ProjectVersion.txt`·`packages-lock.json`·`ProjectSettings.asset`(키 3개 빠짐)이 바뀌어 보인다 — 커밋하지 않는다. 이 머신의 `Path.GetTempPath()` 는 `C:\Users\User\AppData\Local\Temp\` 이고 Windows 빌드 모듈이 있다. 검증 보고에는 실제로 돌린 에디터 버전을 적는다.
- 네트워크 레이스가 도는 중에는 `Unity_RunCommand` 를 부르지 않는다. 2026-10-12 A 도중 `ScreenCapture` 를 부른 직후 에디터 클라 예측기가 고장 났다(`server's state is for tick 230 but the client is at tick 229`). 에디터가 멈춘 사이 서버가 클라를 앞지른 것으로 보이나 재현 실험은 하지 않았다.
- 서버·클라 겸용 플레이어는 `BuildPipeline.BuildPlayer`(서킷 씬만, 출력은 리포 밖 `Path.GetTempPath()/ProtoHarnessRaceBuild/`)로 만든다. 서버·클라 코드를 바꾸면 다시 빌드해야 통합 테스트에 반영된다. 에디터에는 `NetworkManager` 가 프로세스당 하나뿐이라 통합 테스트의 두 번째 클라는 별도 프로세스다.
- **플레이어 빌드는 Unity 가 `ProjectSettings` 를 자동으로 바꾼다**(`ProjectSettings.asset`·`UnityConnectSettings.asset`·`Assets/Settings/*.asset`, 2026-10-10 스파이크 서버 빌드 때 확인). 커밋하지 않고 파일을 지정해 `git restore` 한다. 빌드 중 임시 `Assets/Resources/` 가 생겼다 사라진다.
- `Assets/DefaultNetworkPrefabs.asset`(+`.meta`, NGO 가 자동 생성한 빈 목록)은 C1c-1 에서 커밋했다(3절 #7). `ProtoHarness.slnx` 가 수정으로 보이면 Unity 가 다시 만든 것이라 커밋하지 않는다. `.claude/settings.json` 은 커밋돼 있다(3절 #9).
- 서브에이전트 5종은 `claude-opus-5-5` + `effort: high` 로 고정돼 있다(DECISIONS 2026-10-10 "서브에이전트 모델·노력 고정"). 이 브랜치가 그 병합(`3495fc6`) 전 `dev` 에서 갈라졌으면 파견 때 `model`·`effort` 를 직접 넘긴다.
- 이 환경에서 검증할 때: 테스트 결과는 XML 을 `Grep` 으로 읽고, 완료 알림은 `Editor.log` 의 `ChainRush tests:` 줄을 쓴다. `Unity_RunCommand` 는 `System.Reflection` 을 막는다(타입 확인은 `System.Type.GetType("...")`). 컴파일 확인은 `EditorApplication.isCompiling` 과 새 타입 로드 여부를 같이 본다(리프레시 직후 `False` 는 아직 이르다). 사용자는 허용 프롬프트가 많은 것을 싫어하므로 읽기·수정은 `Read`/`Grep`/`Edit` 로 하고 Bash 는 최소로 쓴다.
- 테스트 결과 XML 은 Unity 의 `Path.GetTempPath()/ChainRush-PlayMode-results.xml` 하나에 덮어쓴다(EditMode 실행도 같은 파일). 에디터의 `Path.GetTempPath()` 는 이 머신에서 `C:\Users\Public\Documents\ESTsoft\CreatorTemp\` 이다(2026-10-08 Editor.log 의 `ChainRush tests: ... XML=` 줄로 확인. 셸의 `%TEMP%` 와 다르다). 경로는 Editor.log 에서 `ChainRush tests:` 를 찾으면 나온다. EditMode 는 MCP `Unity_RunCommand` 로 `TestRunnerApi.Execute(new Filter { testMode = TestMode.EditMode })`, PlayMode 는 `EditorApplication.ExecuteMenuItem("ProtoHarness/Chain Rush/Run PlayMode Tests")` 로 시작했다. 실행마다 따로 보관하려면 끝날 때 복사한다.
- Unity MCP: 도메인 리로드마다 브리지가 몇 초 끊긴다(`Unity not detected (no fresh discovery files found)`). `~/.unity/mcp/connections/bridge-*.json` 이 다시 생기면 재시도한다. 에디터가 백그라운드면 스크립트를 자동 임포트하지 않을 수 있다 → `AssetDatabase.Refresh()`. 테스트 전에 새 코드가 로드됐는지(리플렉션 등) 확인한다.
- `.codex/agents/` 가 `git status` 에 수정으로 보이면 `autocrlf` 표시다(내용은 HEAD 와 같음). 손대지 않는다.
- `index/symbols.tsv` 를 마지막으로 만든 커밋은 그 헤더의 `git-head` 에 있다. 그 뒤 커밋이 생기면 다시 낡는다. 세션 시작 때 SessionStart 훅(`tools/hooks/session-start.ps1`)이 낡았으면 알린다. B모드로 쓰기 전에 재생성한다(§6-2).
- 훅 3개가 `.claude/settings.json` 에 있다(DECISIONS 2026-10-10 "하네스 자동화"): 되돌릴 수 없는 git 명령·`main`/`dev` 커밋은 deny, 보호 파일 Edit/Write 는 ask, 에이전트 정의를 고치면 `.codex` 어긋남 알림. 훅이 막으면 우회하지 말고 사유를 보고한다.
- GitHub Desktop 이 켜져 있으면 `.git/index.lock` 이 남을 수 있다. 실행 중인 `git.exe` 가 없을 때만 지운다(2026-10-05 한 번 발생).
- 씬 저장을 `Unity_RunCommand` 로 하려면 매번 승인받는다(§0, §3-3). T2a 의 승인은 1회성이었다.
- 도메인 리로드 때 `Deleting invalid font reference.` 경고가 나온다(P1-3a 부터, 원인 미확인). 변경과 무관.
