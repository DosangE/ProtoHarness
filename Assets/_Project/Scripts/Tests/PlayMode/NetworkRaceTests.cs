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

        private ChainRushGame game;
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
        // can make the server's run fall and end, and the server does not say so to the client, which then waits for a state
        // that never comes. So each client either finishes, or the server logged that its racer's run ended. Every correction
        // still needs a missed input on the server to come from.
        [UnityTest, Timeout(300000)]
        public IEnumerator TwoClients_WithJitterBeyondTheBuffer_EachFinishesOrItsServerRunEnded_AndCorrectionsHaveACause()
        {
            Outcome o = default;
            yield return Scenario(7794, 1000, 1, 30, 120, r => o = r);
            Debug.Log($"Net race B (30 ms + 0..120 ms jitter, buffer 1): {Describe(o)}; server run ended: racer 0 {o.ServerRunEnded[0]}, racer 1 {o.ServerRunEnded[1]}.");
            bool serverRunEnded = o.ServerRunEnded[0] || o.ServerRunEnded[1];
            Assert.That(o.Finished || serverRunEnded, Is.True, "The editor client did not finish and no server run ended. " + o.ServerLog);
            Assert.That(o.BCompared > 900 || serverRunEnded, Is.True, "The player client did not finish and no server run ended. " + o.ClientB);
            Assert.That(o.Missed[0] + o.Missed[1], Is.GreaterThan(0), "The jitter should have made some inputs late.");
            Assert.That(o.Corrections + o.BCorrections, Is.LessThanOrEqualTo(o.Missed[0] + o.Missed[1]), "A correction without a missed input on the server has no cause.");
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

            outcome.Finished = client.Finished;
            ClientPredictor p = client.Predictor;
            if (p != null)
            {
                outcome.Corrections = p.Corrections;
                outcome.StatesCompared = p.StatesCompared;
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

            ReadLogs(serverLog, clientLog, ref outcome);
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

        private static void ReadLogs(string serverLog, string clientLog, ref Outcome outcome)
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
            Match done = Regex.Match(client, @"RACE-CLIENT-DONE compared=(\d+) corrections=(\d+) .* remote=(\d+)");
            outcome.ClientB = done.Success ? done.Value : Tail(clientLog);
            if (done.Success)
            {
                outcome.BCompared = int.Parse(done.Groups[1].Value);
                outcome.BCorrections = int.Parse(done.Groups[2].Value);
                outcome.BRemote = int.Parse(done.Groups[3].Value);
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
