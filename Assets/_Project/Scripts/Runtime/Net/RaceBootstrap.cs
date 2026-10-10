using System;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Race;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProtoHarness.Net
{
    // Turns a standalone player into a race server (-raceServer) or a scripted client (-raceClient) from the command line;
    // the editor and every other player ignore this.
    //   server: -racePort, -raceRacers, -raceBuffer (ticks), -raceDelayMs, -raceJitterMs, -raceSeed, -raceConnectTimeout (s, 0 = wait forever)
    //   client: -racePort, -raceInputLog (a file written by InputLogFile), -raceTicks, -raceDelayMs, -raceJitterMs, -raceSeed, -raceTimeout (s, 0 = forever)
    // Both start from the circuit scene the player opened with.
    public static class RaceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool server = Array.IndexOf(args, "-raceServer") >= 0;
            bool client = Array.IndexOf(args, "-raceClient") >= 0;
            if (!server && !client) return;
            try
            {
                if (server && client) throw new ArgumentException("RaceBootstrap: -raceServer and -raceClient are exclusive.");
                // A headless player would otherwise draw frames as fast as it can.
                Application.targetFrameRate = 100;
                if (server) StartServer(args);
                else StartClient(args);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Application.Quit(1);
            }
        }

        private static void StartServer(string[] args)
        {
            var host = new GameObject("RaceServer");
            var server = host.AddComponent<RaceServer>();
            server.Launch(
                IntArg(args, "-raceRacers", 2),
                (ushort)IntArg(args, "-racePort", 7790),
                IntArg(args, "-raceBuffer", 3),
                IntArg(args, "-raceDelayMs", 0) / 1000d,
                IntArg(args, "-raceJitterMs", 0) / 1000d,
                IntArg(args, "-raceSeed", 1),
                IntArg(args, "-raceConnectTimeout", 120));
        }

        private static void StartClient(string[] args)
        {
            string logPath = StringArg(args, "-raceInputLog");
            InputLog log = InputLogFile.Load(logPath);
            RaceWorld world = RaceWorld.FromScene(SceneManager.GetActiveScene());
            var host = new GameObject("RaceClient");
            host.SetActive(false);
            var client = host.AddComponent<RaceClient>();
            client.Configure(world.Game, new InputReplay(log), "127.0.0.1",
                (ushort)IntArg(args, "-racePort", 7790),
                IntArg(args, "-raceDelayMs", 0) / 1000d,
                IntArg(args, "-raceJitterMs", 0) / 1000d,
                IntArg(args, "-raceSeed", 1),
                IntArg(args, "-raceTicks", log.Count),
                showRemote: true, quitWhenDone: true,
                timeoutSeconds: IntArg(args, "-raceTimeout", 0));
            host.SetActive(true);
            client.Begin();
        }

        private static int IntArg(string[] args, string name, int fallback)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0) return fallback;
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int value))
                throw new ArgumentException($"RaceBootstrap: {name} needs an integer value.");
            return value;
        }

        private static string StringArg(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException($"RaceBootstrap: {name} needs a value.");
            return args[index + 1];
        }
    }
}
