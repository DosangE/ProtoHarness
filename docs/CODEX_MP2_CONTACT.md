# Codex 지시서 — MP2 단계 B (몸싸움과 출발 격자)

> 2026-11-14 작성, 추적 안 함(아트 세션이 같은 트리를 쓰는 중이라 브랜치를 만들지 않았다).
> 원본 요청서: `docs/PROPOSAL_MP2_BOT_RACE.md` 3절·6절(6-3~6-5). 이 지시서는 그 요청서를 Codex 에 넘기는 사본이고, 둘이 다르면 요청서 + 아래 "요청서와 다른 점" 이 맞다.
> 실행 전 Claude 가 할 것: ① 아트 결과 커밋이 끝났는지 확인 ② `dev` 에서 `git switch -c feature/mp2-contact` ③ 이 파일을 scratchpad 로 복사해 그 사본을 `-PromptFile` 로 준다(실행 파일 `.sent.md`·`.out.md`·`.result.json` 이 `docs/` 에 쌓이지 않게) ④ 아래 `---` 아래만 보낸다 ⑤ `tools/codex-unity.ps1 -PromptFile <사본> -Write -Unity -RunId mp2b-1` 을 `run_in_background` 로, 결과 파일을 기다리는 `until` 루프(상한 90분)를 따로(§7-5).
> 줄 번호는 2026-11-14 `dev` `215cffb` 기준이다.

**요청서와 다른 점 (Claude 가 정함, 실행 전 사용자 확인)**
- 출발 자리: 요청서 `:128`(바뀐 설계)·사용자 "a로"(`:18`)·`docs/COURSE.md:283` 대로 **플레이어 포함 시드로 섞는다.** 요청서 `:141` "플레이어는 늘 칸 0" 과 `:153` "출발 순서 섞기 안 함" 은 처음 안이 남은 줄로 보고 따르지 않는다.

---

승인: 2026-11-13 사용자 "승인" — `docs/PROPOSAL_MP2_BOT_RACE.md` 전체(단계 A·B, Codex 구현 + Unity MCP). 6-4 ③ 은 사용자 답 "봉 스윙 중에 부딪히면 끊겨야한다", ①②④⑤ 는 요청서 0절대로 제안대로 구현하고 보고에 결과를 적는다.
브랜치: feature/mp2-contact (Claude 가 `dev` `215cffb` 에서 만들었다)
Unity MCP: 이 실행에서 허용 (Unity_RunCommand, Unity_GetConsoleLogs). 상태를 바꾸는 RunCommand 는 컴파일 새로고침(`AssetDatabase.Refresh`)과 테스트 실행만. 에셋 생성·삭제·씬 저장·ProjectSettings·패키지는 금지.

## 먼저 읽을 것

`CLAUDE.md`(§0 금지선, §5 실패는 시끄럽게), `docs/RULES/VERIFICATION.md` §4-3·§4-4, `docs/RULES/CONVENTIONS.md` §1-2·§1-3, `docs/PROPOSAL_MP2_BOT_RACE.md` 0절·3절·5절·6절, `docs/COURSE.md` §7-2a.
접두사: `R/` = `Assets/_Project/Scripts/Runtime/ChainRush/`, `T/` = `Assets/_Project/Scripts/Tests/PlayMode/`, `E/` = `Assets/_Project/Scripts/Tests/EditMode/`.

## 목표

봇전에서 러너는 출발선에 가로 한 줄로 서고, 같은 틱에 몸이 겹치면 무게 비율로 서로 밀리며 부딪힌 방향으로 속도를 주고받는다. 밀린 러너는 벽을 뚫지 않는다. **혼자 판(타임어택)은 비트 단위로 그대로다.**

## 지금 코드 (확인한 사실)

- 틱: `R/ChainRushGame.cs:197-203` `StepTick(source)` → `RunTick`(`:210-231`: `tick++` → `circuit.PrepareTick()` → `source.Consume()` → `Attack` → `player.Step(input)` → 위험물 → `circuit.Step(tick)`·결승) → `TickCompleted`.
- 필드: `R/Race/RaceField.cs:114-142` `FixedUpdate` 가 `game.StepTick()` 다음 상대마다 `StepTick()`(`:130-131`), 틱 어긋남 검사(`:132-141`), `CollectStandings`(`:155-`).
- 이동: `R/RunnerMotor.cs:378-428` `MoveAndCollide` — `controller.Move`(`:388`), 가드에 닿으면 `racer.SwingTicks = 0`(`:394`), 접지 스냅(`:421`), 그래플 해제(`:422-425`). `CountDownAndCheckFall`(`:430-440`)이 `LastGroundedS`(`:439`).
- `SetPosition`(`:658-665`)은 벽 검사가 없다. `ResetAtSpawn`(`:667-681`)은 `Awake` 에서 잡은 `spawnPosition`(`:222`)으로 돌린다.
- 랩 시작: `R/Race/CircuitRace.cs:145-151` `ResetRace` 가 `lastS = -definition.StartSlot(0)`. `ChainRushGame.ResetRun`(`:278-293`)이 `circuit.ResetRace()` 다음 `player.ResetAtSpawn()`.
- 무게: `R/RunnerProfile.cs:29` `Weight` (ALPHA 1 · BETA 0.9 · DELTA 0.95 · THETA 1.1 · OMEGA 1.2, `Assets/_Project/Data/RunnerProfile_*.asset:19`). 지금 시뮬에서 쓰는 곳이 없다.
- 상대 입력·필드 조립: `R/Flow/RaceAssembler.cs:299-304`. 시드: `R/Flow/RaceSetup.cs:19` `Seed`.
- 바뀌는 테스트 계약: `T/ChainRushRaceFieldTests.cs:237-299` — `TickCompleted` 안에서 상대가 한 틱 뒤인지(`:268-285`), 상대 3명과 달린 플레이어 런 = 혼자 런(`:295`).

## 할 것

1. **틱 분해 (D11)** — `ChainRushGame` 에 공개 단계 메서드 셋: 입력(`tick++`·`PrepareTick`·`Consume`·`Attack`) / 이동(`player.Step`) / 판정(위험물·서킷·결승, 끝에 `TickCompleted`). `StepTick` 은 셋을 같은 순서로 부른다 — **혼자 판의 실행 순서는 한 줄도 바뀌지 않는다.** 단계 사이 `IsRunning` 조기 반환(`:218`, `:222`)도 같게. 단계를 순서 밖으로 부르면 `InvalidOperationException`.
2. **필드 순서** — `RaceField.FixedUpdate` 를 "전원 입력 → 전원 이동 → 접촉 풀기 → 전원 판정 → 순위 수집". 접촉은 달리는 러너끼리만. 공개 속성 `ContactEnabled`(기본 true).
3. **`R/Race/RunnerContact.cs`** (새 파일, static class, 순수 함수) — 입력: 위치·속도·반지름·높이·무게 배열, 반발 계수. 출력: 러너별 수평 변위와 수평 속도 변화. 짝은 인덱스 순서로, 수직 겹침(캡슐 높이)이 있을 때만. 밀어내기는 무게 반비례. 임펄스는 서로 다가오는 짝에만(멀어지는 짝은 위치만), 법선 방향만, 접선 그대로. 반발 계수 상수 `Restitution = 0.3f`. 잘못된 인자(길이 불일치, 무게 ≤ 0, 계수 범위 밖)는 `ArgumentException`.
4. **모터에 밀기 적용** — `RunnerMotor` 에 바깥 밀기 메서드: 위치는 `controller.Move` 로(벽에 막힘), 속도는 `RacerState.Velocity` 에 더한다. 6-3: 풀이 → `Move` → 실제 위치를 다시 읽어 다시 풀이, **고정 3회.** 6-4: ① 접촉이 없는 틱에는 추가 `Move` 없음 ② 밀린 뒤 기존 접지 검사로 다시 보고 접지면 `LastGroundedS` 갱신 ③ **봉 스윙 중 접촉이면 스윙이 끊긴다**(가드와 같게, `:394`) ④ 그래플 줄 유지 ⑤ 타이머·부스터·게이지 진행 없음.
5. **`R/Race/StartGrid.cs`** (새 파일, static class) — (트랙 정의, 인원, 칸 번호) → 뒤 거리·옆 거리. 출발선 가로 한 줄 4칸(8인이면 4 m 뒤 둘째 줄). 간격 = min(2.4 m, (출발 구간 반폭 − 몸 반지름 0.38 − 벽 여유 0.6) × 2 ÷ 3), 1.2 m 미만이면 예외(`docs/COURSE.md:287`). 반폭은 출발선 S −12 ~ +4 m 의 `HalfWidthAt` 최솟값. 칸 배정은 `RaceSetup.Seed` 로 섞는다(플레이어 포함, 같은 시드 = 같은 배정).
6. **격자 적용** — 러너마다 칸을 받아 `ResetAtSpawn` 위치와 `CircuitRace.ResetRace` 의 랩 시작 S 를 같은 칸으로(5절 위험: 위치만 옮기면 `ResetRun` 이 지운다). 칸이 없으면(혼자 판) 지금 그대로. `RaceAssembler` 가 봇전일 때 플레이어·상대 월드에 칸을 준다. 재출발에도 같은 칸.
7. **기존 필드 테스트 계약 변경 (밝힌다)** — `ChainRushRaceFieldTests` 의 "상대가 한 틱 뒤"(`:268-285`)는 D11 로 계약이 바뀌므로 "판정 단계에서 전원 같은 틱" 으로 바꾼다. "상대와 달린 플레이어 런 = 혼자 런"(`:295`)은 **접촉 끔 + 같은 칸에 세운 혼자 런**과 비교하도록. 통과시키려고 고치는 것이 아니라 요청서 3절 "계약이 바뀐다" 줄이다 — 보고에 바꾼 단언을 전후로 적는다. 그 밖의 기존 테스트 단언은 고치지 않는다.

## 새 테스트

- `E/RunnerContactTests.cs`, `E/StartGridTests.cs` (EditMode). 형식은 `E/BotLineupTests.cs` 를 따른다.
- `T/ChainRushContactTests.cs` (PlayMode). 형식은 `T/ChainRushBotRaceTests.cs` 를 따른다. 새 클래스는 `tools/select-tests.ps1` 표에 배정한다.
- 덮을 것(요청서 `:147`, 6-3·6-4): 겹친 두 러너가 다음 틱에 떨어진다 / 무거운 쪽이 덜 밀리고 덜 바뀐다(OMEGA 1.2 대 BETA 0.9) / 뒤에서 박으면 뒤쪽 느려지고 앞쪽 빨라지고 법선 운동량 합 보존(반발 범위 안) / 옆으로 닿으면 갈라진다 / 벽 옆 2인·3인 압박에서 벽 관통 0, 남은 겹침 ≤ 2 cm / 절벽 끝에서 밀려 떨어짐 / 스윙 중 접촉 → 스윙 끊김 / 그래플 중 접촉 → 줄 유지 / 같은 봇전 N틱 두 번 → 전원 위치 해시 같음 / 격자: 러너마다 다른 칸, 랩 시작 S 가 칸과 맞음, 재출발 같은 칸, 같은 시드 같은 배정 / SUMMIT DROP 출발 구간에 4칸이 들어감 / 틱 분해: `T/ChainRushWorldTests.cs:168-196`(완료 이벤트·일시정지 계약)이 그대로 통과.

## 검증 (§4-3·§4-4, 바뀐 부분만)

1. 컴파일 에러 0: 새로고침 전 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 줄 수를 세고 그 뒤의 `Tundra build success/failed` 줄 원문을 적는다. 테스트 전 `EditorApplication.isPlayingOrWillChangePlaymode` 가 true 면 시작하지 않고 막힘으로 보고한다.
2. 지문: `ChainRushReplayTests.Replay_CircuitBotRun_RepeatsEveryTickBitForBit` 의 로그에서 해시 `0xEF92FB76BFD1D767` 을 **직접 읽어** 같은지(요청서 5절 끝 — 테스트는 고정 해시를 단언하지 않는다).
3. 이름으로 골라 돌린다: `ChainRushRollbackTests`, `ChainRushRaceFieldTests`, `ChainRushWorldTests`, `ChainRushBotRaceTests`, `ChainRushContactTests`, `ChainRushFlowTests` 의 Race 테스트, `ChainRushTutorialTests`, `Race_BotLap_WithLobbyEnvironmentAndLook_CompletesOneLap` 의 STADIUM 보통 1건, `NetReconcileTests`(`NetEndReplay` 제외). EditMode: 새 2종 + `BotLineupTests`. 카테고리 `!Device`·`!Enemy`·`!NetEndReplay`. **게이트 전체(메뉴 `Run PlayMode Tests`)는 돌리지 않는다.**
4. 실행마다 결과 XML(`Path.GetTempPath()` 의 `ChainRush-PlayMode-results.xml`)의 `testcasecount`·`result`·`passed`·`failed`·시각을 적고 `tools/test-runs.ps1 -Record` 로 남긴다. 실패는 테스트 이름과 메시지 원문.
5. 같은 문제로 3번 실패하면 멈추고 `CLAUDE-BLOCKED:` 에 시도한 것·에러 원문·다음 후보 2개.

## 안 하는 것

반발 계수 외 접촉 튜닝(마찰·접선 감속), D7 (b), D8 복귀 겹침, 봉·앵커 점유, 봇이 접촉을 피하거나 노리는 판단, 8인 격자 검증(3명만), 봇 난이도·자동 개선 루프, `R/Net/` 동작 변경. 씬·프리팹·`.asset`(트랙 `startSlots` 포함)·`.meta`·ProjectSettings·`Packages/`·`docs/` 를 고치지 않는다. 파일 이동·이름 바꾸기·삭제는 하지 않는다(필요하면 `CLAUDE-QUESTION:`). 빈 `catch`·기본값 fallback 금지, `Update`/`FixedUpdate` 안 할당·LINQ·문자열 결합 금지(접촉 배열은 필드로 캐시).

## 보고

바꾼 파일(`경로:줄`), 틱 분해 전후 `StepTick` 순서 대조, 6-4 ①~⑤ 각 결과, 바꾼 기존 단언 전후, 컴파일 줄 원문, 지문 값, 실행별 XML 값, 남은 것.
