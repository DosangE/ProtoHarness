using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ProtoHarness.Net
{
    // Builds the one NetworkManager a process uses, from code: no prefabs, no scene objects, no scene changes. The four
    // message names are the whole protocol: the client sends inputs; the server sends each racer its own states, then,
    // once that racer's run is over, how and when it ended; and it sends every racer the other racers' positions.
    internal static class RaceNetwork
    {
        public const string InputMessage = "race.input";
        public const string StateMessage = "race.state";
        public const string EndMessage = "race.end";
        public const string RemoteMessage = "race.remote";

        // The manager lives on its own root object (NetworkManager refuses to be nested), created inactive so its
        // settings are in place before its Awake and OnEnable run. It is kept across scene loads, so whoever calls
        // Create destroys manager.gameObject when done (Destroy).
        public static NetworkManager Create(string address, ushort port)
        {
            var host = new GameObject("RaceNetworkManager");
            host.SetActive(false);
            var transport = host.AddComponent<UnityTransport>();
            transport.SetConnectionData(address, port, address);
            var manager = host.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = false,
            };
            // Application.runInBackground is a project-wide setting; the race leaves it as it found it.
            manager.RunInBackground = false;
            host.SetActive(true);
            return manager;
        }

        public static void Destroy(NetworkManager manager)
        {
            if (manager == null) return;
            if (manager.IsListening) manager.Shutdown();
            UnityEngine.Object.Destroy(manager.gameObject);
        }
    }
}
