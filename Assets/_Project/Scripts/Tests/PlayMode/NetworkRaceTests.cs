using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // The two-racer race over real connections (DESIGN.md P3 C1): a standalone build of the circuit scene is the server
    // (two racer worlds) and, in a second process, a scripted client; the editor is the other client. All three play
    // the same recorded run, so every state the server sends can be checked against what the editor predicted. Real time:
    // a run of N ticks takes N / 50 seconds. Needs the build (ServerExe), so it is Explicit: it never runs with a group
    // or the merge gate, only when selected by name.
    [Explicit("Needs the race build at NetworkRaceTests.RaceBuildExe and takes about a minute; select it by name.")]
    [Category("NetRace")]
    public sealed class NetworkRaceTests
    {
        private const string CircuitScene = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private const int BotTicks = 1700;
        // RaceClient.OnConnected logs it with the id Netcode gave the client (its LocalClientId). The test assembly does not
        // reference Netcode, so for the editor client this line is where its id is read.
        private const string ConnectedPattern = @"RACE-CLIENT connected as (\d+);";
        // RaceClient.OnOrder logs every order it takes; for the editor client this line is where the sequence is read.
        private const string ClientOrderPattern = @"RACE-CLIENT-ORDER tick=(-?\d+) ([^\r\n]+)";
        // RaceServer.OrderMarginMetres (private there): how far ahead a racer must be to pass another.
        private const double OrderMarginMetres = 1d;

        private ChainRushGame game;
        // Listens to the editor's log for the editor client's ConnectedPattern line while a scenario runs; let go in TearDown
        // too, in case the scenario stopped early.
        private Application.LogCallback editorLogWatch;
        private RunnerMotor player;
        private GrappleController grapple;
        private CircuitRace circuit;
        private float timeScale;

        // Where the build goes and is looked for. Built by hand from the editor, outside the project.
        public static string RaceBuildExe => Path.Combine(Path.GetTempPath(), "ProtoHarnessRaceBuild", "RaceBuild.exe");

        private struct Outcome
        {
            public bool Finished;
            public int Corrections;
            public int StatesCompared;
            public int RemoteSamples;
            public int RemoteMismatches;
            public int RemoteChecked;
            public float GhostLargestStep;
            public int GhostJumps;
            public int GhostFrames;
            public string ClientB;
            public int BCorrections;
            public int BCompared;
            public int BRemote;
            public string ServerLog;
            public int[] Ticks;
            public int[] Received;
            public int[] Missed;
            public int[] Late;
            // Whether the server logged that racer i's run ended (a fall or a finish) before its client left.
            public bool[] ServerRunEnded;
            // The end the server sent racer i's client (RACE-SERVER-END), as "<reason>@<tick>"; "none" when there was none within
            // the clients' ticks. ServerEndLines counts every RACE-SERVER-END line for racer i, within the ticks or not.
            public string[] ServerEndOf;
            public int[] ServerEndLines;
            // Which client the server made racer i ("RACE-SERVER client <id> is racer <i>."), and how many such lines it logged.
            public ulong[] RacerClient;
            public int[] RacerClientLines;
            // Server lines (racer assignments or ends) for a racer other than 0 and 1.
            public int StrayRacerLines;
            // The editor client's id, from its "RACE-CLIENT connected as <id>;" line (RaceClient.OnConnected), and how many of
            // those lines it logged.
            public ulong EditorId;
            public int EditorIdLines;
            // The player client's id, from the same line in its log.
            public ulong BId;
            public int BIdLines;
            // The end the editor client took, as "<reason>@<tick>", or "none".
            public string EditorEnded;
            // The player client's DONE line was logged; its ended= field ("<reason>@<tick>" or "none"; null when the line has none,
            // which is a build from before race.end); and whether it logged RACE-CLIENT-TIMEOUT.
            public bool BDone;
            public string BEnded;
            public bool BTimedOut;
            // The newest standings each client holds when the player client has left (RaceStandings.Describe), which racer
            // each says it is, and how many it took. For the player client, its last "RACE-CLIENT-STANDINGS you=<i> <lines>"
            // line; BStandingsRefused is its log holding a standings message the client threw on.
            public string EditorStandings;
            public int EditorYou;
            public int EditorStandingsCount;
            public string BStandings;
            public int BYou;
            public int BStandingsLines;
            public bool BStandingsRefused;
            // How many "RACE-SERVER-STANDINGS" lines the server logged, and its last.
            public int ServerStandingsLines;
            public string ServerLastStandings;
            // Read with EditorStandings: whether the editor client's ghost is still shown, and whether it has a standings panel.
            // BGhostHidden counts the player client's "RACE-CLIENT-GHOST hidden" lines.
            public bool EditorGhostVisible;
            public bool EditorHasStandingsView;
            public int BGhostHidden;
            // The editor's frames while its client is connected: the longest (ms, and the client's tick after it), how many
            // went over 50 ms, the first of those (as "tick:ms"), and the client's tick in the frame its first standings arrived.
            // Only measured, not judged: they tell whether a stall in the editor made its inputs late.
            public float LongestFrameMs;
            public int LongestFrameTick;
            public int SlowFrames;
            public string SlowFrameList;
            public int FirstStandingsTick;
            // The other racer's positions at the editor, as the editor's own clock sees them: its tick when the first arrived
            // and when it finished, the other racer's first tick received, and how many times a tick did not follow the one
            // before by one (a gap, a repeat or a step back). The two racers start together (race.go), so the first arrives a
            // round trip and a buffer after this run started (before the joint start it came later by however long the other
            // process took to start: DECISIONS 2026-10-12 "네트워크 레이스 A").
            public int FirstRemoteAt;
            public int EndAt;
            public int FirstRemoteTick;
            public int RemoteBreaks;
            // The newest order of the running racers each client holds when the player client has left (RaceOrder.Describe),
            // and how many it took; for the player client its last "RACE-CLIENT-ORDER tick=<t> <places>" line, and
            // BOrderRefused is its log holding an order message the client threw on. The server's "RACE-SERVER-ORDER" lines:
            // how many, and the places in its last.
            public string EditorOrder;
            public int EditorOrderCount;
            public string BOrder;
            public int BOrderLines;
            public bool BOrderRefused;
            public int ServerOrderLines;
            public string ServerLastOrder;
            // Every order with a place for both racers, as (compared tick, places): the server's "RACE-SERVER-ORDER" lines, the
            // editor client's "RACE-CLIENT-ORDER" lines (from its log as they happen, until the player client has left) and the
            // player client's. An order with a racer still unplaced comes only between the two racers' first states, in the
            // order the server hands them out, so those are left out.
            public List<(int Tick, string Places)> ServerOrders;
            public List<(int Tick, string Places)> EditorOrders;
            public List<(int Tick, string Places)> BOrders;
            // The other racer's positions as they arrived at the editor, by its tick.
            public List<(int Tick, Vector3 Position)> RemotePositions;
            // The joint start: the server's "RACE-SERVER-GO" lines, the frame each racer started on ("RACE-SERVER racer <i>
            // started on frame <f>", -1 for none) and how many such lines; whether the editor client took the go; the player
            // client's "RACE-CLIENT-GO" and "RACE-CLIENT-COUNTDOWN done" lines.
            public int ServerGoLines;
            public int[] ServerStartFrame;
            public int[] ServerStartLines;
            public bool EditorGo;
            public int BGoLines;
            public int BCountdownLines;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (editorLogWatch != null)
            {
                Application.logMessageReceived -= editorLogWatch;
                editorLogWatch = null;
            }
            if (game != null) game.SetInputSource(new InputReplay(new InputLog()));
            Time.timeScale = timeScale;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // 100 ms round trip (50 ms each way), no jitter, an input buffer of 3 ticks: nothing arrives late, so the server's
        // two worlds and both clients must agree to the bit, and the other racer is drawn without jumps.
        [UnityTest, Timeout(300000)]
        public IEnumerator TwoClients_FixedDelay_AgreeBitForBitAndTheGhostMovesSmoothly()
        {
            const int ticks = 1000;
            Outcome o = default;
            yield return Scenario(7793, ticks, 3, 50, 0, r => o = r);
            Debug.Log($"Net race A (rtt 100 ms, buffer 3): {Describe(o)}");
            Assert.That(o.Finished, Is.True, "The editor client did not get to compare the server's last state. " + o.ServerLog);
            AssertStartedTogether(o);
            Assert.That(o.Corrections, Is.EqualTo(0), "The editor client had to be corrected.");
            Assert.That(o.StatesCompared, Is.GreaterThan(900));
            Assert.That(o.BCorrections, Is.EqualTo(0), "The player client had to be corrected. " + o.ClientB);
            Assert.That(o.BCompared, Is.GreaterThan(900), o.ClientB);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(o.Late[i], Is.EqualTo(0), $"racer {i}: an input came late.");
                Assert.That(o.Missed[i], Is.EqualTo(o.Ticks[i] - o.Received[i]), $"racer {i}: an input was missing in the middle of the run (ticks {o.Ticks[i]}, received {o.Received[i]}, missed {o.Missed[i]}).");
            }
            // The other racer's positions keep arriving: from its first tick, with none missing or out of order (no jitter, an
            // ordered reliable channel), for as long as both ran. Both started together, so the first arrives a round trip and
            // a buffer after this run started (here about 8 ticks); 25 ticks (0.5 s) leave room for frames and for the other
            // client hearing the go a frame or two later. The count is judged against the editor's own ticks since the first
            // arrived; 5 ticks cover frames.
            Assert.That(o.FirstRemoteAt, Is.InRange(0, 25), "The other racer must start with this one.");
            Assert.That(o.FirstRemoteTick, Is.EqualTo(0), "The other racer's positions must arrive from its first tick.");
            Assert.That(o.RemoteBreaks, Is.EqualTo(0), "The other racer's positions must arrive one tick after another.");
            Assert.That(o.RemoteSamples, Is.GreaterThanOrEqualTo(o.EndAt - o.FirstRemoteAt - 5), "The other racer's positions must keep arriving.");
            Assert.That(o.BRemote, Is.GreaterThan(900), o.ClientB);
            // Every position but the one for tick 0 is compared with this racer's own (its own tick 0 is not a completed tick).
            Assert.That(o.RemoteChecked, Is.GreaterThanOrEqualTo(o.RemoteSamples - 1));
            Assert.That(o.RemoteMismatches, Is.EqualTo(0), "Both racers play the same run in separate worlds, so the other racer's position at a tick must be this racer's own.");
            Assert.That(o.GhostJumps, Is.EqualTo(0), $"The ghost jumped (largest step {o.GhostLargestStep:F2} m in a frame).");
            Assert.That(o.ServerRunEnded[0] || o.ServerRunEnded[1], Is.False, "A server run ended with nothing late.");
            // Nobody ended, so both clients' newest standings have both racers running, and each knows which racer it is.
            string standings = Standings(o);
            TestContext.WriteLine("Net race A standings: " + standings);
            Assert.That(o.EditorStandings, Is.EqualTo("0=Running 1=Running"), standings);
            Assert.That(o.BStandings, Is.EqualTo("0=Running 1=Running"), standings + " " + o.ClientB);
            Assert.That(o.EditorYou, Is.EqualTo(RacerOf(o, o.EditorId)), standings);
            Assert.That(o.BYou, Is.EqualTo(RacerOf(o, o.BId)), standings);
            Assert.That(o.BStandingsRefused, Is.False, "The player client refused a standings message. " + o.ClientB);
            // Nobody ended, so neither ghost is hidden; the editor client draws its standings.
            Assert.That(o.EditorHasStandingsView, Is.True, "The editor client has no standings panel.");
            Assert.That(o.EditorGhostVisible, Is.True, "The editor client hid its ghost though the other racer is running. " + standings);
            Assert.That(o.BGhostHidden, Is.EqualTo(0), "The player client hid its ghost though the other racer is running. " + o.ClientB);
            // Both racers play the same run, so on the same tick of their runs they have gone exactly as far: level, both first.
            string orders = Orders(o);
            TestContext.WriteLine("Net race A orders: " + orders);
            Assert.That(o.ServerLastOrder, Is.EqualTo("0=1 1=1"), orders);
            Assert.That(o.EditorOrder, Is.EqualTo("0=1 1=1"), orders);
            Assert.That(o.BOrder, Is.EqualTo("0=1 1=1"), orders + " " + o.ClientB);
            Assert.That(o.BOrderRefused, Is.False, "The player client refused an order message. " + o.ClientB);
        }

        // Jitter of up to 120 ms against a 1-tick buffer: inputs arrive late and the server repeats the last one. A missed jump
        // can make the server's run fall and end; the server then tells that racer's client (race.end), which ends its own run
        // on the same tick. So each client finishes, with no timeout: by comparing the state for its last tick, or by taking the
        // end of a server run that ended within its ticks. Each client takes exactly the end of its own racer, at the same tick
        // and for the same reason, and none ("none") when its racer's run did not end within the ticks. Every correction still
        // needs a missed input on the server to come from.
        // Judged from the logs: the server's "RACE-SERVER client <id> is racer <i>." and "RACE-SERVER-END racer=<i> reason=<r>
        // tick=<t>" lines (t <= the clients' ticks), each client's own "RACE-CLIENT connected as <id>;" line (the editor's read
        // from its log as it happens, the player client's from its log file), the player client's "RACE-CLIENT-DONE ...
        // ended=<r>@<t>|none" line and the absence of "RACE-CLIENT-TIMEOUT" in it, and the editor client's predictor
        // (ServerEnded, EndReason, EndTick). The pairing is written to the test's output on every run.
        [UnityTest, Timeout(300000)]
        public IEnumerator TwoClients_WithJitterBeyondTheBuffer_EachFinishesWithoutTimeoutAndIsToldOfItsServerRunsEnd_AndCorrectionsHaveACause()
        {
            const int ticks = 1000;
            Outcome o = default;
            yield return Scenario(7794, ticks, 1, 30, 120, r => o = r);
            int editorRacer = RacerOf(o, o.EditorId);
            int bRacer = RacerOf(o, o.BId);
            // Written for every run, before any judgement, so the result XML holds which client was which racer and what each was told.
            string pairing = $"server ends within {ticks} ticks: racer 0 {o.ServerEndOf[0]} ({o.ServerEndLines[0]} END lines), racer 1 {o.ServerEndOf[1]} ({o.ServerEndLines[1]} END lines); " +
                             $"racers: 0 = client {o.RacerClient[0]} ({o.RacerClientLines[0]} lines), 1 = client {o.RacerClient[1]} ({o.RacerClientLines[1]} lines), stray racer lines {o.StrayRacerLines}; " +
                             $"editor client {o.EditorId} ({o.EditorIdLines} connected lines) is racer {editorRacer}, ended {o.EditorEnded}; " +
                             $"player client {o.BId} ({o.BIdLines} connected lines) is racer {bRacer}, ended {o.BEnded ?? "(no ended= field)"}.";
            TestContext.WriteLine("Net race B pairing: " + pairing);
            Debug.Log($"Net race B (30 ms + 0..120 ms jitter, buffer 1): {Describe(o)}; {pairing}");
            Assert.That(o.Finished, Is.True, "The editor client neither compared the state for its last tick nor took the server's end. " + o.ServerLog);
            AssertStartedTogether(o);
            Assert.That(o.BTimedOut, Is.False, "The player client timed out. " + o.ClientB);
            Assert.That(o.BDone, Is.True, "The player client did not finish. " + o.ClientB);
            Assert.That(o.BEnded, Is.Not.Null, "The player client's DONE line has no ended= field: the race build predates race.end. Rebuild it. " + o.ClientB);
            // Each client is matched to its racer by its id: its own connected line, and the server's assignment line.
            Assert.That(o.EditorIdLines, Is.EqualTo(1), "The editor client must log exactly one 'RACE-CLIENT connected as' line. " + pairing);
            Assert.That(o.BIdLines, Is.EqualTo(1), "The player client must log exactly one 'RACE-CLIENT connected as' line. " + pairing + " " + o.ClientB);
            Assert.That(o.StrayRacerLines, Is.EqualTo(0), "The server logged a racer other than 0 and 1. " + pairing + " " + o.ServerLog);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(o.RacerClientLines[i], Is.EqualTo(1), $"racer {i} must be given to exactly one client. " + pairing + " " + o.ServerLog);
                Assert.That(o.ServerEndLines[i], Is.LessThanOrEqualTo(1), $"racer {i}'s run can end only once. " + pairing + " " + o.ServerLog);
            }
            Assert.That(o.EditorId, Is.Not.EqualTo(o.BId), "The two clients must have different ids. " + pairing);
            Assert.That(editorRacer, Is.GreaterThanOrEqualTo(0), "The server made no racer of the editor client. " + pairing + " " + o.ServerLog);
            Assert.That(bRacer, Is.GreaterThanOrEqualTo(0), "The server made no racer of the player client. " + pairing + " " + o.ServerLog);
            // Each client takes exactly its own racer's end ("none" for a racer whose run did not end within the ticks).
            Assert.That(o.EditorEnded, Is.EqualTo(o.ServerEndOf[editorRacer]), $"The editor client (racer {editorRacer}) must take its own racer's end and no other. " + pairing + " " + o.ServerLog);
            Assert.That(o.BEnded, Is.EqualTo(o.ServerEndOf[bRacer]), $"The player client (racer {bRacer}) must take its own racer's end and no other. " + pairing + " " + o.ClientB);
            Assert.That(o.Missed[0] + o.Missed[1], Is.GreaterThan(0), "The jitter should have made some inputs late.");
            Assert.That(o.Corrections + o.BCorrections, Is.LessThanOrEqualTo(o.Missed[0] + o.Missed[1]), "A correction without a missed input on the server has no cause.");
            // Both clients, the other racer's too, hear of every end within the ticks as the server had it; a racer whose run
            // did not end within them is running, or ended after them.
            string standings = Standings(o);
            TestContext.WriteLine("Net race B standings: " + standings);
            Assert.That(o.BStandingsRefused, Is.False, "The player client refused a standings message. " + o.ClientB);
            Assert.That(o.EditorYou, Is.EqualTo(editorRacer), standings);
            Assert.That(o.BYou, Is.EqualTo(bRacer), standings);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(FitsTheServersEnd(LineOf(o.EditorStandings, i), o.ServerEndOf[i], ticks), Is.True, $"The editor client's standings for racer {i} against the server's end {o.ServerEndOf[i]}. " + standings + " " + pairing);
                Assert.That(FitsTheServersEnd(LineOf(o.BStandings, i), o.ServerEndOf[i], ticks), Is.True, $"The player client's standings for racer {i} against the server's end {o.ServerEndOf[i]}. " + standings + " " + pairing);
            }
            // Each client's ghost is hidden exactly when its newest standings say the other racer's run has ended.
            bool editorOtherEnded = HasEnded(LineOf(o.EditorStandings, 1 - editorRacer));
            bool bOtherEnded = HasEnded(LineOf(o.BStandings, 1 - bRacer));
            TestContext.WriteLine($"Net race B ghosts: editor client visible {o.EditorGhostVisible} (other ended {editorOtherEnded}), player client hidden lines {o.BGhostHidden} (other ended {bOtherEnded}).");
            Assert.That(o.EditorHasStandingsView, Is.True, "The editor client has no standings panel.");
            Assert.That(o.EditorGhostVisible, Is.EqualTo(!editorOtherEnded), "The editor client's ghost against its standings. " + standings);
            Assert.That(o.BGhostHidden, Is.EqualTo(bOtherEnded ? 1 : 0), "The player client's ghost against its standings. " + standings + " " + o.ClientB);
            // With jitter the runs differ, so the order is only recorded; every order message must still be taken.
            string orders = Orders(o);
            TestContext.WriteLine("Net race B orders: " + orders);
            Assert.That(o.EditorOrderCount, Is.GreaterThan(0), "The editor client took no order. " + orders);
            Assert.That(o.BOrderLines, Is.GreaterThan(0), "The player client took no order. " + orders + " " + o.ClientB);
            Assert.That(o.BOrderRefused, Is.False, "The player client refused an order message. " + o.ClientB);
        }

        // Two different runs: the same bot, but the player client's racer drifts (slower on the ground) early and the editor
        // client's racer later and for longer, so the editor's racer leads first and the other passes it. Both runs are first
        // played offline here; the order the server must reach is what RaceOrder makes of those two runs tick by tick. Then
        // over 100 ms round trip with no jitter (nothing late): both clients are never corrected, the other racer's positions
        // at the editor are its offline run's to the bit, and the server and both clients go through exactly that order.
        [UnityTest, Timeout(300000)]
        public IEnumerator TwoClients_DifferentRuns_TheOrderSwapsAsTheRunsSay_AndEveryoneAgrees()
        {
            const int ticks = 1000;
            const int playerDriftFrom = 100, playerDriftTo = 300, editorDriftFrom = 400, editorDriftTo = 800;
            Outcome o = default;
            List<SimSnapshot> editorRun = null, playerRun = null;
            List<(int Tick, string Places)> expected = null;
            yield return Scenario(7795, ticks, 3, 50, 0, r => o = r, () =>
            {
                InputLog editorLog = RecordBot(BotTicks, editorDriftFrom, editorDriftTo);
                InputLog playerLog = RecordBot(BotTicks, playerDriftFrom, playerDriftTo);
                editorRun = Play(editorLog, ticks);
                playerRun = Play(playerLog, ticks);
                // Offline the editor's racer is 0. The premise: it leads, then the other passes it.
                expected = ExpectedOrders(editorRun, playerRun, ticks);
                string offline = string.Join(", ", expected.ConvertAll(e => $"{e.Tick}:{e.Places}"));
                TestContext.WriteLine($"Net race C offline orders (0 = editor): {offline}; progress at tick {ticks}: editor {editorRun[ticks].Circuit.Laps.Progress:F2} m, player {playerRun[ticks].Circuit.Laps.Progress:F2} m.");
                int led = expected.FindIndex(e => e.Places == "0=1 1=2");
                Assert.That(led, Is.GreaterThanOrEqualTo(0), "Premise: offline, the editor's racer must lead at some point. " + offline);
                Assert.That(expected.FindIndex(led, e => e.Places == "0=2 1=1"), Is.GreaterThan(led), "Premise: offline, the other racer must pass the editor's racer after it led. " + offline);
                return (editorLog, playerLog);
            });
            int editorRacer = RacerOf(o, o.EditorId);
            int bRacer = RacerOf(o, o.BId);
            Debug.Log($"Net race C (rtt 100 ms, buffer 3, different runs): {Describe(o)}; editor client is racer {editorRacer}, player client is racer {bRacer}.");
            Assert.That(o.Finished, Is.True, "The editor client did not get to compare the server's last state. " + o.ServerLog);
            Assert.That(o.BDone, Is.True, "The player client did not finish. " + o.ClientB);
            AssertStartedTogether(o);
            Assert.That(editorRacer, Is.GreaterThanOrEqualTo(0), "The server made no racer of the editor client. " + o.ServerLog);
            Assert.That(bRacer, Is.EqualTo(1 - editorRacer), "The player client must be the other racer. " + o.ServerLog);
            Assert.That(o.Corrections, Is.EqualTo(0), "The editor client had to be corrected.");
            Assert.That(o.StatesCompared, Is.GreaterThan(900));
            Assert.That(o.BCorrections, Is.EqualTo(0), "The player client had to be corrected. " + o.ClientB);
            Assert.That(o.BCompared, Is.GreaterThan(900), o.ClientB);
            for (int i = 0; i < 2; i++) Assert.That(o.Late[i], Is.EqualTo(0), $"racer {i}: an input came late.");
            Assert.That(o.ServerEndOf[0] == "none" && o.ServerEndOf[1] == "none", Is.True, $"No run may end within {ticks} ticks: racer 0 {o.ServerEndOf[0]}, racer 1 {o.ServerEndOf[1]}. " + o.ServerLog);

            // The other racer's positions at the editor are the player's offline run, to the bit, from its first tick.
            int remoteChecked = 0, remoteMismatches = 0;
            string firstMismatch = "";
            foreach ((int tick, Vector3 position) in o.RemotePositions)
            {
                if (tick > ticks) continue;
                remoteChecked++;
                Vector3 offline = playerRun[tick].Position;
                if (BitConverter.SingleToInt32Bits(offline.x) == BitConverter.SingleToInt32Bits(position.x)
                    && BitConverter.SingleToInt32Bits(offline.y) == BitConverter.SingleToInt32Bits(position.y)
                    && BitConverter.SingleToInt32Bits(offline.z) == BitConverter.SingleToInt32Bits(position.z)) continue;
                if (remoteMismatches == 0) firstMismatch = $" first at tick {tick}: received {position:F4}, offline {offline:F4}.";
                remoteMismatches++;
            }
            TestContext.WriteLine($"Net race C remote positions: {o.RemotePositions.Count} received, {remoteChecked} within {ticks} ticks checked against the player's offline run, {remoteMismatches} differ.{firstMismatch}");
            Assert.That(o.FirstRemoteTick, Is.EqualTo(0), "The other racer's positions must arrive from its first tick.");
            Assert.That(o.RemoteBreaks, Is.EqualTo(0), "The other racer's positions must arrive one tick after another.");
            Assert.That(remoteChecked, Is.GreaterThan(ticks / 2), "Too few of the other racer's positions to judge.");
            Assert.That(remoteMismatches, Is.EqualTo(0), "The other racer's positions must be its offline run's." + firstMismatch);

            // The server's order, and each client's, is the offline one (with the server's racer numbers) within the ticks.
            List<(int Tick, string Places)> want = editorRacer == 0 ? expected : expected.ConvertAll(e => (e.Tick, Swapped(e.Places)));
            string sequences = $"expected {Join(want)}; server {Join(Within(o.ServerOrders, ticks))}; editor client {Join(Within(o.EditorOrders, ticks))}; player client {Join(Within(o.BOrders, ticks))}.";
            TestContext.WriteLine("Net race C orders: " + sequences);
            Assert.That(Join(Within(o.ServerOrders, ticks)), Is.EqualTo(Join(want)), "The server's order must be what the two runs make of it. " + sequences);
            Assert.That(Join(Within(o.EditorOrders, ticks)), Is.EqualTo(Join(want)), "The editor client must take every order the server made. " + sequences);
            Assert.That(Join(Within(o.BOrders, ticks)), Is.EqualTo(Join(want)), "The player client must take every order the server made. " + sequences + " " + o.ClientB);
            Assert.That(o.BOrderRefused, Is.False, "The player client refused an order message. " + o.ClientB);
        }

        // Both clients were told to go once, counted down, and the server started both racers in one frame.
        private static void AssertStartedTogether(Outcome o)
        {
            string start = $"server: {o.ServerGoLines} go lines, racer 0 started on frame {o.ServerStartFrame[0]} ({o.ServerStartLines[0]} lines), racer 1 on frame {o.ServerStartFrame[1]} ({o.ServerStartLines[1]} lines); " +
                           $"editor client took the go {o.EditorGo}; player client: {o.BGoLines} go lines, {o.BCountdownLines} countdown lines.";
            TestContext.WriteLine("Start: " + start);
            Assert.That(o.ServerGoLines, Is.EqualTo(1), "The server must tell the racers to go exactly once. " + start + " " + o.ServerLog);
            Assert.That(o.EditorGo, Is.True, "The editor client never took the go. " + start);
            Assert.That(o.BGoLines, Is.EqualTo(1), "The player client must take the go exactly once. " + start + " " + o.ClientB);
            Assert.That(o.BCountdownLines, Is.EqualTo(1), "The player client must count down and start once. " + start + " " + o.ClientB);
            for (int i = 0; i < 2; i++) Assert.That(o.ServerStartLines[i], Is.EqualTo(1), $"racer {i} must start exactly once. " + start + " " + o.ServerLog);
            Assert.That(o.ServerStartFrame[0], Is.EqualTo(o.ServerStartFrame[1]), "Both racers must start in the same server frame. " + start);
        }

        private static List<(int Tick, string Places)> Within(List<(int Tick, string Places)> orders, int ticks) =>
            orders.FindAll(e => e.Tick <= ticks);

        private static string Join(List<(int Tick, string Places)> orders) =>
            string.Join(", ", orders.ConvertAll(e => $"{e.Tick}:{e.Places}"));

        // "0=1 1=2" with the two racers' numbers exchanged: "0=2 1=1".
        private static string Swapped(string places)
        {
            string[] parts = places.Split(' ');
            Assert.That(parts.Length, Is.EqualTo(2), $"Not a two-racer order: \"{places}\".");
            return $"0={parts[1].Substring(2)} 1={parts[0].Substring(2)}";
        }

        // Plays a log offline and keeps the state after every tick (index = tick; tick 0 is the state StartRun leaves), the
        // way the server hands states out (ServerInputBuffer.AfterTick). The run must still be going after `ticks`.
        private List<SimSnapshot> Play(InputLog log, int ticks)
        {
            var replay = new InputReplay(log);
            Time.timeScale = 0f;
            game.SetInputSource(replay);
            game.StartRun();
            var states = new List<SimSnapshot>(ticks + 1) { game.CaptureSnapshot() };
            while (game.IsRunning && game.Tick < ticks)
            {
                game.StepTick(replay);
                if (game.IsRunning) states.Add(game.CaptureSnapshot());
            }
            Assert.That(game.IsRunning, Is.True, $"Premise: the run must still be going after {ticks} ticks; it ended at tick {game.Tick} (failed {game.HasFailed}, finished {game.HasFinished}).");
            Assert.That(states[ticks].Tick, Is.EqualTo(ticks));
            return states;
        }

        // The orders the server's RaceOrder goes through for these two runs (racer 0, racer 1), each when it changes, as
        // (compared tick, places), only those that place both racers, within the ticks.
        private static List<(int Tick, string Places)> ExpectedOrders(List<SimSnapshot> run0, List<SimSnapshot> run1, int ticks)
        {
            var order = new RaceOrder(2, OrderMarginMetres);
            var orders = new List<(int Tick, string Places)>();
            order.Start(0);
            order.Start(1);
            for (int t = 0; t <= ticks; t++)
            {
                for (int racer = 0; racer < 2; racer++)
                {
                    if (!order.Advance(racer, t, (racer == 0 ? run0 : run1)[t].Circuit.Laps.Progress)) continue;
                    if (order[0] > 0 && order[1] > 0) orders.Add((order.ComparedTick, RaceOrder.Describe(order.ToArray())));
                }
            }
            return orders;
        }

        private static string Orders(Outcome o) =>
            $"editor client \"{o.EditorOrder}\" ({o.EditorOrderCount} taken); player client \"{o.BOrder}\" ({o.BOrderLines} lines, refused {o.BOrderRefused}); " +
            $"server: {o.ServerOrderLines} lines, last \"{o.ServerLastOrder}\".";

        // A line of RaceStandings.Describe for a fall or a finish.
        private static bool HasEnded(string line) =>
            line != null && (line.StartsWith("Failed", StringComparison.Ordinal) || line.StartsWith("Finished", StringComparison.Ordinal));

        private static string Standings(Outcome o) =>
            $"editor client: you={o.EditorYou} \"{o.EditorStandings}\" ({o.EditorStandingsCount} taken); player client: you={o.BYou} \"{o.BStandings}\" ({o.BStandingsLines} lines, refused {o.BStandingsRefused}); " +
            $"server: {o.ServerStandingsLines} lines, last \"{o.ServerLastStandings}\".";

        // Racer i's line in RaceStandings.Describe's text ("0=Running 1=Failed@657"), or null.
        private static string LineOf(string standings, int racer)
        {
            if (standings == null) return null;
            string prefix = racer + "=";
            foreach (string part in standings.Split(' ')) if (part.StartsWith(prefix, StringComparison.Ordinal)) return part.Substring(prefix.Length);
            return null;
        }

        // serverEnd: "<reason>@<tick>" within the ticks, or "none". A line fits it when it is that end (a finish with any
        // place), or, for "none", when the racer is running or ended after the ticks.
        private static bool FitsTheServersEnd(string line, string serverEnd, int ticks)
        {
            if (line == null) return false;
            if (serverEnd != "none") return line == serverEnd || line.StartsWith(serverEnd + "#", StringComparison.Ordinal);
            if (line == "Running") return true;
            Match m = Regex.Match(line, @"^(?:Failed|Finished)@(\d+)");
            return m.Success && int.Parse(m.Groups[1].Value) > ticks;
        }

        // The racer the server gave this client, or -1.
        private static int RacerOf(Outcome o, ulong clientId)
        {
            for (int i = 0; i < 2; i++) if (o.RacerClientLines[i] > 0 && o.RacerClient[i] == clientId) return i;
            return -1;
        }

        private static string Describe(Outcome o) =>
            $"editor client: finished {o.Finished}, {o.StatesCompared} states compared, {o.Corrections} corrections; player client: {o.BCompared} compared, {o.BCorrections} corrections, {o.BRemote} remote positions; " +
            $"remote positions at the editor: {o.RemoteSamples} ({o.RemoteChecked} checked against its own run, {o.RemoteMismatches} differ; from its tick {o.FirstRemoteTick}, {o.RemoteBreaks} breaks; first at the editor's tick {o.FirstRemoteAt}, the editor ended at tick {o.EndAt}); ghost: {o.GhostFrames} frames, largest step {o.GhostLargestStep:F3} m, jumps {o.GhostJumps}; " +
            $"server racer 0: ran {o.Ticks?[0]}, received {o.Received?[0]}, missed {o.Missed?[0]}, late {o.Late?[0]}; racer 1: ran {o.Ticks?[1]}, received {o.Received?[1]}, missed {o.Missed?[1]}, late {o.Late?[1]}; " +
            $"editor frames: longest {o.LongestFrameMs:F1} ms after tick {o.LongestFrameTick}, over 50 ms {o.SlowFrames} [{o.SlowFrameList}], first standings at tick {o.FirstStandingsTick}.";

        // record: the editor client's and the player client's logs, made once the circuit is loaded; without it both play the
        // same bot run.
        private IEnumerator Scenario(ushort port, int ticks, int bufferTicks, int delayMs, int jitterMs, Action<Outcome> done,
            Func<(InputLog Editor, InputLog Player)> record = null)
        {
            if (!File.Exists(RaceBuildExe)) Assert.Inconclusive($"No race build at {RaceBuildExe}. Build the circuit scene as a Windows standalone player there first.");
            yield return Load();
            InputLog log, playerLog;
            if (record != null) (log, playerLog) = record();
            else playerLog = log = RecordBot(BotTicks);
            string dir = Path.GetDirectoryName(RaceBuildExe);
            string logFile = Path.Combine(dir, $"input-{port}.bin");
            InputLogFile.Save(logFile, playerLog);
            string serverLog = Path.Combine(dir, $"server-{port}.log");
            string clientLog = Path.Combine(dir, $"client-{port}.log");
            foreach (string stale in new[] { serverLog, clientLog }) if (File.Exists(stale)) File.Delete(stale);

            using Process server = Start($"-batchmode -nographics -logFile \"{serverLog}\" -raceServer -racePort {port} -raceRacers 2 -raceBuffer {bufferTicks} " +
                                         $"-raceDelayMs {delayMs} -raceJitterMs {jitterMs} -raceSeed 7 -raceConnectTimeout 90");
            float listenDeadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < listenDeadline && !server.HasExited && !LogHas(serverLog, "RACE-SERVER listening")) yield return null;
            Assert.That(LogHas(serverLog, "RACE-SERVER listening"), Is.True, "The server did not start listening. " + Tail(serverLog));

            using Process playerClient = Start($"-batchmode -nographics -logFile \"{clientLog}\" -raceClient -racePort {port} -raceInputLog \"{logFile}\" -raceTicks {ticks} " +
                                               $"-raceDelayMs {delayMs} -raceJitterMs {jitterMs} -raceSeed 13 -raceTimeout {(int)(ticks * 0.02f * 1.5f + 20f)}");

            // The editor client: its own positions by tick (to compare with what the server says about the other racer),
            // the other racer's positions as they arrive, and the ghost's position every frame.
            var own = new Dictionary<int, Vector3>(ticks + 64);
            game.TickCompleted += tick => { if (game.IsRunning) own[tick] = game.CaptureSnapshot().Position; };
            var received = new List<(int Tick, Vector3 Position)>(ticks + 64);
            int firstRemoteAt = -1;
            Time.timeScale = 1f;
            var host = new GameObject("RaceClient");
            host.SetActive(false);
            RaceClient client = host.AddComponent<RaceClient>();
            client.Configure(game, new InputReplay(log), "127.0.0.1", port, delayMs / 1000d, jitterMs / 1000d, 11, ticks, showRemote: true, quitWhenDone: false);
            client.RemoteReceived += (tick, position) =>
            {
                if (firstRemoteAt < 0) firstRemoteAt = game.Tick;
                received.Add((tick, position));
            };
            ulong editorId = 0;
            int editorIdLines = 0;
            var editorOrders = new List<(int Tick, string Places)>();
            editorLogWatch = (message, stackTrace, type) =>
            {
                Match order = Regex.Match(message, ClientOrderPattern);
                if (order.Success)
                {
                    AddIfBothPlaced(editorOrders, order);
                    return;
                }
                Match m = Regex.Match(message, ConnectedPattern);
                if (!m.Success) return;
                editorIdLines++;
                editorId = ulong.Parse(m.Groups[1].Value);
            };
            Application.logMessageReceived += editorLogWatch;
            host.SetActive(true);
            client.Begin();

            var outcome = new Outcome { FirstStandingsTick = -1, LongestFrameTick = -1 };
            var slowFrames = new List<string>();
            bool havePrevious = false;
            Vector3 previous = default;
            float deadline = Time.realtimeSinceStartup + ticks * 0.02f * 1.6f + 60f;
            while (!client.Finished && Time.realtimeSinceStartup < deadline && !server.HasExited)
            {
                yield return null;
                if (client.Connected)
                {
                    float frameMs = Time.unscaledDeltaTime * 1000f;
                    if (frameMs > outcome.LongestFrameMs)
                    {
                        outcome.LongestFrameMs = frameMs;
                        outcome.LongestFrameTick = game.Tick;
                    }
                    if (frameMs > 50f)
                    {
                        outcome.SlowFrames++;
                        if (slowFrames.Count < 10) slowFrames.Add($"{game.Tick}:{frameMs:F0}");
                    }
                    if (outcome.FirstStandingsTick < 0 && client.StandingsReceived > 0) outcome.FirstStandingsTick = game.Tick;
                }
                RemoteGhost ghost = client.Ghost;
                if (ghost != null && ghost.HasPosition)
                {
                    if (havePrevious)
                    {
                        float step = Vector3.Distance(previous, ghost.Position);
                        outcome.GhostFrames++;
                        if (step > outcome.GhostLargestStep) outcome.GhostLargestStep = step;
                        // At 30 m/s a frame of 0.1 s is 3 m; more than 6 m in one frame is a jump, not a run.
                        if (step > 6f) outcome.GhostJumps++;
                    }
                    previous = ghost.Position;
                    havePrevious = true;
                }
            }
            outcome.SlowFrameList = string.Join(" ", slowFrames);

            outcome.Finished = client.Finished;
            outcome.EditorGo = client.GoReceived;
            ClientPredictor p = client.Predictor;
            outcome.EditorEnded = "none";
            if (p != null)
            {
                outcome.Corrections = p.Corrections;
                outcome.StatesCompared = p.StatesCompared;
                if (p.ServerEnded) outcome.EditorEnded = $"{p.EndReason}@{p.EndTick}";
            }
            outcome.RemoteSamples = client.RemoteSamples;
            outcome.RemotePositions = received;
            outcome.EndAt = game.Tick;
            outcome.FirstRemoteAt = firstRemoteAt;
            outcome.FirstRemoteTick = received.Count > 0 ? received[0].Tick : -1;
            for (int i = 1; i < received.Count; i++) if (received[i].Tick != received[i - 1].Tick + 1) outcome.RemoteBreaks++;
            foreach ((int tick, Vector3 position) in received)
            {
                if (!own.TryGetValue(tick, out Vector3 mine)) continue;
                outcome.RemoteChecked++;
                if (BitConverter.SingleToInt32Bits(mine.x) != BitConverter.SingleToInt32Bits(position.x)
                    || BitConverter.SingleToInt32Bits(mine.y) != BitConverter.SingleToInt32Bits(position.y)
                    || BitConverter.SingleToInt32Bits(mine.z) != BitConverter.SingleToInt32Bits(position.z)) outcome.RemoteMismatches++;
            }

            // The player client leaves by itself a second after it has finished; then the editor leaves; then the server has no client and quits.
            float exitDeadline = Time.realtimeSinceStartup + 25f;
            while (!playerClient.HasExited && Time.realtimeSinceStartup < exitDeadline) yield return null;
            if (!playerClient.HasExited) { playerClient.Kill(); playerClient.WaitForExit(5000); }
            // Read when the player client has left: until then the editor client still takes standings (an end of the other
            // racer can come after this client finished).
            outcome.EditorStandings = client.Standings == null ? null : RaceStandings.Describe(client.Standings);
            outcome.EditorYou = client.You;
            outcome.EditorStandingsCount = client.StandingsReceived;
            outcome.EditorGhostVisible = client.Ghost != null && client.Ghost.Visible;
            outcome.EditorHasStandingsView = client.StandingsView != null;
            outcome.EditorOrder = client.Order == null ? null : RaceOrder.Describe(client.Order);
            outcome.EditorOrderCount = client.OrderReceived;
            // Watched until here: orders for ticks the other racer reaches after this client finished still come.
            Application.logMessageReceived -= editorLogWatch;
            editorLogWatch = null;
            outcome.EditorId = editorId;
            outcome.EditorIdLines = editorIdLines;
            outcome.EditorOrders = editorOrders;
            client.Disconnect();
            exitDeadline = Time.realtimeSinceStartup + 15f;
            while (!server.HasExited && Time.realtimeSinceStartup < exitDeadline) yield return null;
            if (!server.HasExited) { server.Kill(); server.WaitForExit(5000); }
            UnityEngine.Object.Destroy(host);
            yield return null;

            ReadLogs(serverLog, clientLog, ticks, ref outcome);
            done(outcome);
        }

        private static Process Start(string arguments)
        {
            var start = new ProcessStartInfo(RaceBuildExe, arguments) { UseShellExecute = false, CreateNoWindow = true };
            Process process = Process.Start(start);
            Assert.That(process, Is.Not.Null);
            return process;
        }

        // The processes may still hold their log files open, so they are opened for sharing.
        private static string ReadShared(string path)
        {
            if (!File.Exists(path)) return "";
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static bool LogHas(string path, string text) => ReadShared(path).Contains(text);

        private static string Tail(string path)
        {
            string text = ReadShared(path);
            return text.Length == 0 ? $"(no log at {path})" : "log tail: " + text.Substring(Math.Max(0, text.Length - 1500));
        }

        // ticks: the clients' last tick; a server end after it is one the clients ignore (it ran on controls they never sent).
        private static void ReadLogs(string serverLog, string clientLog, int ticks, ref Outcome outcome)
        {
            string server = ReadShared(serverLog);
            outcome.ServerLog = Tail(serverLog);
            outcome.Ticks = new int[2];
            outcome.Received = new int[2];
            outcome.Missed = new int[2];
            outcome.Late = new int[2];
            outcome.ServerRunEnded = new bool[2];
            foreach (Match m in Regex.Matches(server, @"RACE-SERVER racer (\d+): the run is no longer going"))
            {
                int racer = int.Parse(m.Groups[1].Value);
                if (racer >= 0 && racer <= 1) outcome.ServerRunEnded[racer] = true;
            }
            outcome.ServerEndOf = new[] { "none", "none" };
            outcome.ServerEndLines = new int[2];
            foreach (Match m in Regex.Matches(server, @"RACE-SERVER-END racer=(\d+) reason=(\w+) tick=(\d+)"))
            {
                int racer = int.Parse(m.Groups[1].Value);
                if (racer < 0 || racer > 1)
                {
                    outcome.StrayRacerLines++;
                    continue;
                }
                outcome.ServerEndLines[racer]++;
                if (int.Parse(m.Groups[3].Value) <= ticks) outcome.ServerEndOf[racer] = $"{m.Groups[2].Value}@{m.Groups[3].Value}";
            }
            outcome.RacerClient = new ulong[2];
            outcome.RacerClientLines = new int[2];
            foreach (Match m in Regex.Matches(server, @"RACE-SERVER client (\d+) is racer (\d+)\."))
            {
                int racer = int.Parse(m.Groups[2].Value);
                if (racer < 0 || racer > 1)
                {
                    outcome.StrayRacerLines++;
                    continue;
                }
                outcome.RacerClientLines[racer]++;
                outcome.RacerClient[racer] = ulong.Parse(m.Groups[1].Value);
            }
            foreach (Match m in Regex.Matches(server, @"RACE-SERVER-DONE racer=(\d+) ticks=(\d+) received=(\d+) missed=(\d+) late=(\d+)"))
            {
                int racer = int.Parse(m.Groups[1].Value);
                if (racer < 0 || racer > 1) continue;
                outcome.Ticks[racer] = int.Parse(m.Groups[2].Value);
                outcome.Received[racer] = int.Parse(m.Groups[3].Value);
                outcome.Missed[racer] = int.Parse(m.Groups[4].Value);
                outcome.Late[racer] = int.Parse(m.Groups[5].Value);
            }
            foreach (Match m in Regex.Matches(server, @"RACE-SERVER-STANDINGS v=\d+ ([^\r\n]+)"))
            {
                outcome.ServerStandingsLines++;
                outcome.ServerLastStandings = m.Groups[1].Value;
            }
            string client = ReadShared(clientLog);
            outcome.BYou = -1;
            foreach (Match m in Regex.Matches(client, @"RACE-CLIENT-STANDINGS you=(\d+) ([^\r\n]+)"))
            {
                outcome.BStandingsLines++;
                outcome.BYou = int.Parse(m.Groups[1].Value);
                outcome.BStandings = m.Groups[2].Value;
            }
            outcome.ServerOrders = new List<(int Tick, string Places)>();
            foreach (Match m in Regex.Matches(server, @"RACE-SERVER-ORDER v=\d+ tick=(-?\d+) ([^\r\n]+)"))
            {
                outcome.ServerOrderLines++;
                outcome.ServerLastOrder = m.Groups[2].Value;
                AddIfBothPlaced(outcome.ServerOrders, m);
            }
            outcome.BOrders = new List<(int Tick, string Places)>();
            foreach (Match m in Regex.Matches(client, ClientOrderPattern))
            {
                outcome.BOrderLines++;
                outcome.BOrder = m.Groups[2].Value;
                AddIfBothPlaced(outcome.BOrders, m);
            }
            outcome.BOrderRefused = client.Contains("RaceClient: an order for");
            outcome.BGhostHidden = Regex.Matches(client, @"RACE-CLIENT-GHOST hidden").Count;
            outcome.BStandingsRefused = client.Contains("RaceClient: standings for racer") || client.Contains("RaceClient: the standings say");
            Match done =Regex.Match(client, @"RACE-CLIENT-DONE compared=(\d+) corrections=(\d+) .* remote=(\d+)(?: ended=(\S+))?");
            outcome.ClientB = done.Success ? done.Value : Tail(clientLog);
            outcome.BDone = done.Success;
            outcome.BTimedOut = client.Contains("RACE-CLIENT-TIMEOUT");
            foreach (Match m in Regex.Matches(client, ConnectedPattern))
            {
                outcome.BIdLines++;
                outcome.BId = ulong.Parse(m.Groups[1].Value);
            }
            outcome.ServerGoLines = Regex.Matches(server, @"RACE-SERVER-GO ").Count;
            outcome.ServerStartFrame = new[] { -1, -1 };
            outcome.ServerStartLines = new int[2];
            foreach (Match m in Regex.Matches(server, @"RACE-SERVER racer (\d+) started on frame (\d+)"))
            {
                int racer = int.Parse(m.Groups[1].Value);
                if (racer < 0 || racer > 1)
                {
                    outcome.StrayRacerLines++;
                    continue;
                }
                outcome.ServerStartLines[racer]++;
                outcome.ServerStartFrame[racer] = int.Parse(m.Groups[2].Value);
            }
            outcome.BGoLines = Regex.Matches(client, @"RACE-CLIENT-GO ").Count;
            outcome.BCountdownLines = Regex.Matches(client, @"RACE-CLIENT-COUNTDOWN done").Count;
            if (done.Success)
            {
                outcome.BCompared = int.Parse(done.Groups[1].Value);
                outcome.BCorrections = int.Parse(done.Groups[2].Value);
                outcome.BRemote = int.Parse(done.Groups[3].Value);
                if (done.Groups[4].Success) outcome.BEnded = done.Groups[4].Value;
            }
        }

        // An order line matched with (tick, places) groups; kept when it places every racer (RaceOrder.Describe writes "-" for none).
        private static void AddIfBothPlaced(List<(int Tick, string Places)> orders, Match m)
        {
            string places = m.Groups[2].Value.TrimEnd();
            if (!places.Contains("-")) orders.Add((int.Parse(m.Groups[1].Value), places));
        }

        private IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(CircuitScene, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Network race tests require the Unity Editor.");
            yield break;
#endif
            RaceWorld world = RaceWorld.FromScene(SceneManager.GetActiveScene());
            game = world.Game;
            player = world.Player;
            grapple = world.Grapple;
            circuit = world.Circuit;
            yield return null;
        }

        // driftFrom..driftTo: ticks of the run (from inclusive) on which the bot also holds drift; none by default.
        private InputLog RecordBot(int ticks, int driftFrom = 0, int driftTo = 0)
        {
            var log = new InputLog();
            IInputSource bot = new CircuitBot(game, player, grapple, circuit);
            if (driftTo > driftFrom) bot = new DriftWindow(bot, game, driftFrom, driftTo);
            var recorder = new InputRecorder(bot, log);
            Time.timeScale = 0f;
            game.SetInputSource(recorder);
            game.StartRun();
            while (game.IsRunning && game.Tick < ticks) game.StepTick(recorder);
            Assert.That(game.IsRunning, Is.True, $"The bot's run ended at tick {game.Tick}.");
            return log;
        }

        // Another source's input with drift held on ticks from..to (from inclusive) of the run. Drift slows a grounded
        // runner (RunnerMotor driftSpeedScale), so two runs that drift on different ticks gain and lose ground on each other.
        private sealed class DriftWindow : IInputSource
        {
            private readonly IInputSource inner;
            private readonly ChainRushGame game;
            private readonly int from;
            private readonly int to;

            public DriftWindow(IInputSource inner, ChainRushGame game, int from, int to)
            {
                if (from < 0 || to <= from) throw new ArgumentOutOfRangeException(nameof(to), to, $"DriftWindow: ticks {from}..{to} is not a window.");
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
                this.game = game ?? throw new ArgumentNullException(nameof(game));
                this.from = from;
                this.to = to;
            }

            public void Poll() => inner.Poll();
            public void Clear() => inner.Clear();

            // Read while the tick runs: game.Tick is the tick being run.
            public TickInput Consume()
            {
                TickInput input = inner.Consume();
                bool drift = input.Drift || (game.Tick >= from && game.Tick < to);
                return new TickInput(input.Steer, input.PrimaryPressed, input.ReleasePressed, input.AttackPressed, drift, input.ChainActionPressed);
            }
        }
    }
}
