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
            Outcome o = default;
            yield return Scenario(7793, 1000, 3, 50, 0, r => o = r);
            Debug.Log($"Net race A (rtt 100 ms, buffer 3): {Describe(o)}");
            Assert.That(o.Finished, Is.True, "The editor client did not get to compare the server's last state. " + o.ServerLog);
            Assert.That(o.Corrections, Is.EqualTo(0), "The editor client had to be corrected.");
            Assert.That(o.StatesCompared, Is.GreaterThan(900));
            Assert.That(o.BCorrections, Is.EqualTo(0), "The player client had to be corrected. " + o.ClientB);
            Assert.That(o.BCompared, Is.GreaterThan(900), o.ClientB);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(o.Late[i], Is.EqualTo(0), $"racer {i}: an input came late.");
                Assert.That(o.Missed[i], Is.EqualTo(o.Ticks[i] - o.Received[i]), $"racer {i}: an input was missing in the middle of the run (ticks {o.Ticks[i]}, received {o.Received[i]}, missed {o.Missed[i]}).");
            }
            Assert.That(o.RemoteSamples, Is.GreaterThan(900), "The other racer's positions must keep arriving.");
            Assert.That(o.BRemote, Is.GreaterThan(900), o.ClientB);
            Assert.That(o.RemoteChecked, Is.GreaterThan(900));
            Assert.That(o.RemoteMismatches, Is.EqualTo(0), "Both racers play the same run in separate worlds, so the other racer's position at a tick must be this racer's own.");
            Assert.That(o.GhostJumps, Is.EqualTo(0), $"The ghost jumped (largest step {o.GhostLargestStep:F2} m in a frame).");
            Assert.That(o.ServerRunEnded[0] || o.ServerRunEnded[1], Is.False, "A server run ended with nothing late.");
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
        }

        // The racer the server gave this client, or -1.
        private static int RacerOf(Outcome o, ulong clientId)
        {
            for (int i = 0; i < 2; i++) if (o.RacerClientLines[i] > 0 && o.RacerClient[i] == clientId) return i;
            return -1;
        }

        private static string Describe(Outcome o) =>
            $"editor client: finished {o.Finished}, {o.StatesCompared} states compared, {o.Corrections} corrections; player client: {o.BCompared} compared, {o.BCorrections} corrections, {o.BRemote} remote positions; " +
            $"remote positions at the editor: {o.RemoteSamples} ({o.RemoteChecked} checked against its own run, {o.RemoteMismatches} differ); ghost: {o.GhostFrames} frames, largest step {o.GhostLargestStep:F3} m, jumps {o.GhostJumps}; " +
            $"server racer 0: ran {o.Ticks?[0]}, received {o.Received?[0]}, missed {o.Missed?[0]}, late {o.Late?[0]}; racer 1: ran {o.Ticks?[1]}, received {o.Received?[1]}, missed {o.Missed?[1]}, late {o.Late?[1]}.";

        private IEnumerator Scenario(ushort port, int ticks, int bufferTicks, int delayMs, int jitterMs, Action<Outcome> done)
        {
            if (!File.Exists(RaceBuildExe)) Assert.Inconclusive($"No race build at {RaceBuildExe}. Build the circuit scene as a Windows standalone player there first.");
            yield return Load();
            InputLog log = RecordBot(BotTicks);
            string dir = Path.GetDirectoryName(RaceBuildExe);
            string logFile = Path.Combine(dir, $"input-{port}.bin");
            InputLogFile.Save(logFile, log);
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
            Time.timeScale = 1f;
            var host = new GameObject("RaceClient");
            host.SetActive(false);
            RaceClient client = host.AddComponent<RaceClient>();
            client.Configure(game, new InputReplay(log), "127.0.0.1", port, delayMs / 1000d, jitterMs / 1000d, 11, ticks, showRemote: true, quitWhenDone: false);
            client.RemoteReceived += (tick, position) => received.Add((tick, position));
            ulong editorId = 0;
            int editorIdLines = 0;
            editorLogWatch = (message, stackTrace, type) =>
            {
                Match m = Regex.Match(message, ConnectedPattern);
                if (!m.Success) return;
                editorIdLines++;
                editorId = ulong.Parse(m.Groups[1].Value);
            };
            Application.logMessageReceived += editorLogWatch;
            host.SetActive(true);
            client.Begin();

            var outcome = new Outcome();
            bool havePrevious = false;
            Vector3 previous = default;
            float deadline = Time.realtimeSinceStartup + ticks * 0.02f * 1.6f + 60f;
            while (!client.Finished && Time.realtimeSinceStartup < deadline && !server.HasExited)
            {
                yield return null;
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

            Application.logMessageReceived -= editorLogWatch;
            editorLogWatch = null;
            outcome.EditorId = editorId;
            outcome.EditorIdLines = editorIdLines;
            outcome.Finished = client.Finished;
            ClientPredictor p = client.Predictor;
            outcome.EditorEnded = "none";
            if (p != null)
            {
                outcome.Corrections = p.Corrections;
                outcome.StatesCompared = p.StatesCompared;
                if (p.ServerEnded) outcome.EditorEnded = $"{p.EndReason}@{p.EndTick}";
            }
            outcome.RemoteSamples = client.RemoteSamples;
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
            string client = ReadShared(clientLog);
            Match done = Regex.Match(client, @"RACE-CLIENT-DONE compared=(\d+) corrections=(\d+) .* remote=(\d+)(?: ended=(\S+))?");
            outcome.ClientB = done.Success ? done.Value : Tail(clientLog);
            outcome.BDone = done.Success;
            outcome.BTimedOut = client.Contains("RACE-CLIENT-TIMEOUT");
            foreach (Match m in Regex.Matches(client, ConnectedPattern))
            {
                outcome.BIdLines++;
                outcome.BId = ulong.Parse(m.Groups[1].Value);
            }
            if (done.Success)
            {
                outcome.BCompared = int.Parse(done.Groups[1].Value);
                outcome.BCorrections = int.Parse(done.Groups[2].Value);
                outcome.BRemote = int.Parse(done.Groups[3].Value);
                if (done.Groups[4].Success) outcome.BEnded = done.Groups[4].Value;
            }
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

        private InputLog RecordBot(int ticks)
        {
            var log = new InputLog();
            var recorder = new InputRecorder(new CircuitBot(game, player, grapple, circuit), log);
            Time.timeScale = 0f;
            game.SetInputSource(recorder);
            game.StartRun();
            while (game.IsRunning && game.Tick < ticks) game.StepTick(recorder);
            Assert.That(game.IsRunning, Is.True, $"The bot's run ended at tick {game.Tick}.");
            return log;
        }
    }
}
