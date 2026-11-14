using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Bots;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Motion;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    public sealed class ChainRushContactTests
    {
        private const string RacePath = "Assets/_Project/Scenes/ChainRushCircuit.unity";
        private static readonly MethodInfo FieldTick = typeof(RaceField).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ContactPass = typeof(RaceField).GetMethod("ResolveContact", BindingFlags.Instance | BindingFlags.NonPublic);
        private RaceWorld player;
        private RaceAssembler assembler;
        private RaceWorld[] worlds;
        private RaceField Field => assembler.Field;
        private float timeScale;
        private float volume;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            volume = AudioListener.volume;
            AudioListener.volume = 0f;
            Time.timeScale = 0f;
            RaceLaunch.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (worlds != null)
                foreach (RaceWorld world in worlds) if (world.Game != null) world.Game.SetInputSource(Idle());
            RaceLaunch.Clear();
            Time.timeScale = timeScale;
            AudioListener.volume = volume;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Contact_OverlappingRunners_SeparateOnTheNextTickAndHeavierRunnerChangesLess()
        {
            yield return Load();
            Begin();
            ParkUnused();
            Place(worlds[1], new Vector3(-0.3f, 1.05f, 10f), Vector3.right * 8f); // OMEGA 1.2
            Place(worlds[2], new Vector3(0.3f, 1.05f, 10f), Vector3.right * 2f);  // BETA 0.9
            Vector3 a = worlds[1].Player.transform.position, b = worlds[2].Player.transform.position;
            worlds[1].Game.Racer.RespawnHoldTicks = worlds[2].Game.Racer.RespawnHoldTicks = 10;
            Step();
            float heavyMove = Mathf.Abs(worlds[1].Player.transform.position.x - a.x);
            float lightMove = Mathf.Abs(worlds[2].Player.transform.position.x - b.x);
            Assert.That(heavyMove, Is.GreaterThan(0f).And.LessThan(lightMove));
            Assert.That(8f - worlds[1].Player.Velocity.x, Is.LessThan(worlds[2].Player.Velocity.x - 2f));
            AssertSeparated(worlds[1], worlds[2], 0.001f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Contact_RearImpact_SlowsRearAcceleratesFrontAndConservesMomentum()
        {
            yield return Load();
            Begin();
            ParkUnused();
            Place(worlds[1], new Vector3(0f, 1.05f, 10f), Vector3.forward * 8f);
            Place(worlds[2], new Vector3(0f, 1.05f, 10.6f), Vector3.forward * 2f);
            worlds[1].Game.Racer.RespawnHoldTicks = worlds[2].Game.Racer.RespawnHoldTicks = 10;
            float before = 1.2f * 8f + 0.9f * 2f;
            Step();
            float rear = worlds[1].Player.Velocity.z, front = worlds[2].Player.Velocity.z;
            Assert.That(rear, Is.LessThan(8f));
            Assert.That(front, Is.GreaterThan(2f));
            Assert.That(1.2f * rear + 0.9f * front, Is.EqualTo(before).Within(0.00001f));
            Assert.That(front - rear, Is.EqualTo(6f * RunnerContact.Restitution).Within(0.00001f));
            AssertSeparated(worlds[1], worlds[2], 0.001f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Contact_SideImpact_DividesSidewaysAndLeavesForwardVelocityAlone()
        {
            yield return Load();
            Begin();
            ParkUnused();
            Place(worlds[1], new Vector3(-0.3f, 1.05f, 10f), new Vector3(4f, 0f, 7f));
            Place(worlds[2], new Vector3(0.3f, 1.05f, 10f), new Vector3(-4f, 0f, 7f));
            worlds[1].Game.Racer.RespawnHoldTicks = worlds[2].Game.Racer.RespawnHoldTicks = 10;
            Step();
            Assert.That(worlds[1].Player.transform.position.x, Is.LessThan(-0.3f));
            Assert.That(worlds[2].Player.transform.position.x, Is.GreaterThan(0.3f));
            Assert.That(worlds[1].Player.Velocity.z, Is.EqualTo(7f));
            Assert.That(worlds[2].Player.Velocity.z, Is.EqualTo(7f));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Contact_WallPressureWithTwoAndThreeRunners_DoesNotCrossTheWallAndLeavesAtMostTwoCentimetres()
        {
            yield return Load();
            Begin();
            for (int count = 2; count <= 3; count++)
            {
                ParkUnused();
                float width = player.Circuit.Definition.HalfWidthAt(10d);
                for (int i = 0; i < count; i++)
                {
                    float radius = worlds[i].Player.ContactRadius;
                    Place(worlds[i], new Vector3(width - radius - i * (radius * 2f - 0.04f), 1.05f, 10f), Vector3.zero);
                    worlds[i].Game.Racer.RespawnHoldTicks = 10;
                }
                Step();
                for (int i = 0; i < count; i++)
                {
                    CharacterController capsule = worlds[i].Player.GetComponent<CharacterController>();
                    // CharacterController's documented collision envelope includes its skin; no capsule crosses it.
                    Assert.That(worlds[i].Player.transform.position.x + capsule.radius - capsule.skinWidth,
                        Is.LessThanOrEqualTo(width + 0.001f), $"{count} runners: wall, racer {i}");
                    for (int j = i + 1; j < count; j++) AssertSeparated(worlds[i], worlds[j], 0.02f);
                }
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Contact_PushedPastGapEdge_LosesGroundAndFalls()
        {
            yield return Load();
            Begin();
            double edge = player.Circuit.Definition.Gaps()[0].Start;
            TrackFrame frame = player.Game.Track.FrameAt(edge - 0.45d);
            Place(player, frame.TransformPoint(new Vector3(0f, 1.05f, 0f)), Vector3.zero);
            player.Player.ApplyContact(frame.Forward * 1.3f, Vector3.zero);
            Assert.That(player.Player.IsGrounded, Is.False);
            float height = player.Player.transform.position.y;
            for (int i = 0; i < 12; i++) Step();
            Assert.That(player.Player.transform.position.y, Is.LessThan(height - 0.2f));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Contact_SwingBreaks_GrappleStays_AndNoTimerBoosterOrGaugeAdvances()
        {
            yield return Load();
            Begin();
            ParkUnused();
            Place(worlds[1], new Vector3(-0.3f, 4f, 10f), Vector3.zero);
            Place(worlds[2], new Vector3(0.3f, 4f, 10f), Vector3.zero);
            RacerState swing = worlds[1].Game.Racer, grapple = worlds[2].Game.Racer;
            PoleSwing.Hook(swing, worlds[1].Player.transform.position, new Vector3(-5f, 0f, 10f));
            grapple.Attach(0, 9f);
            swing.SlingTicks = 17;
            swing.BoostZoneCarrySpeed = 35f;
            swing.RespawnHoldTicks = 7;
            swing.AddGauge(1.25f);
            int tick = worlds[1].Game.Tick;
            float gauge = swing.Gauge;
            int anchor = grapple.AnchorIndex;
            ContactPass.Invoke(Field, null); // Same three real-position passes used by FixedUpdate, isolated from the tick.
            Assert.That(worlds[1].Player.IsSwinging, Is.False);
            Assert.That(worlds[2].Grapple.IsAttached, Is.True);
            Assert.That(grapple.AnchorIndex, Is.EqualTo(anchor));
            Assert.That(grapple.RopeLength, Is.EqualTo(9f));
            Assert.That(worlds[1].Game.Tick, Is.EqualTo(tick));
            Assert.That(swing.SlingTicks, Is.EqualTo(17));
            Assert.That(swing.RespawnHoldTicks, Is.EqualTo(7));
            Assert.That(swing.BoostZoneCarrySpeed, Is.EqualTo(35f));
            Assert.That(swing.Gauge, Is.EqualTo(gauge));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Contact_NoOverlap_DoesNotMoveAgainOrChangeSwingTimersAndGroundedPushUpdatesReturnS()
        {
            yield return Load();
            Begin();
            ParkUnused();
            RacerState state = player.Game.Racer;
            state.SwingTicks = 12;
            state.SlingTicks = 18;
            state.LastGroundedS = -123d;
            Vector3 position = player.Player.transform.position;
            ContactPass.Invoke(Field, null);
            Assert.That(player.Player.transform.position, Is.EqualTo(position));
            Assert.That(state.LastGroundedS, Is.EqualTo(-123d), "No additional Move/ground update without contact.");
            Assert.That(state.SwingTicks, Is.EqualTo(12));
            Assert.That(state.SlingTicks, Is.EqualTo(18));
            Place(player, new Vector3(0f, 1.05f, 10f), Vector3.zero);
            player.Player.ApplyContact(Vector3.forward * 0.1f, Vector3.zero);
            Assert.That(player.Player.IsGrounded, Is.True);
            Assert.That(state.LastGroundedS, Is.EqualTo(player.Game.Track.Project(player.Player.transform.position).S).Within(0.00001d));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Grid_UniqueSeededSlotsAgreeWithLapSAndSurviveRestart()
        {
            yield return Load();
            int[] slots = StartGrid.Assign(4, assembler.Setup.Seed);
            var starts = new Vector3[4];
            Assert.That(Field.ContactEnabled, Is.True);
            for (int i = 0; i < worlds.Length; i++)
            {
                worlds[i].Game.ReturnToStart();
                starts[i] = worlds[i].Player.transform.position;
                Vector2 slot = StartGrid.Slot(assembler.Setup.Track, 4, slots[i]);
                TrackCoord actual = worlds[i].Game.Track.Project(starts[i]);
                Assert.That(actual.S, Is.EqualTo(-slot.x).Within(0.001d));
                Assert.That(worlds[i].Circuit.LastS, Is.EqualTo(-slot.x));
                Assert.That(worlds[i].Circuit.Laps.Progress, Is.EqualTo(-slot.x));
                Assert.That(actual.D, Is.EqualTo(slot.y).Within(0.001f));
                for (int j = 0; j < i; j++) Assert.That(starts[i], Is.Not.EqualTo(starts[j]));
            }
            Begin();
            for (int i = 0; i < 50; i++) Step();
            foreach (RaceWorld world in worlds) world.Game.StartRun();
            for (int i = 0; i < worlds.Length; i++) Assert.That(worlds[i].Player.transform.position, Is.EqualTo(starts[i]));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Field_SeededBotRaceTwice_RepeatsEveryRunnersPositionHash()
        {
            yield return Load();
            ulong first = RunBots(300);
            yield return Load();
            ulong second = RunBots(300);
            Assert.That(second, Is.EqualTo(first));
            Debug.Log($"ChainRush contact: 4 runners x 300 ticks repeated; hash=0x{first:X16}");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Tick_StageOrderIsEnforced_AndCompletionWaitsForJudgment()
        {
            yield return Load();
            Begin();
            ChainRushGame game = player.Game;
            int completions = 0;
            Action<int> completed = _ => completions++;
            game.TickCompleted += completed;
            try
            {
                Assert.Throws<InvalidOperationException>(() => game.MoveTick());
                Assert.Throws<InvalidOperationException>(() => game.JudgeTick());
                Assert.That(game.InputTick(), Is.True);
                Assert.Throws<InvalidOperationException>(() => game.InputTick());
                Assert.Throws<InvalidOperationException>(() => game.JudgeTick());
                game.MoveTick();
                Assert.Throws<InvalidOperationException>(() => game.MoveTick());
                Assert.That(completions, Is.EqualTo(0));
                game.JudgeTick();
                Assert.That(completions, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => game.JudgeTick());
                var refused = Assert.Throws<InvalidOperationException>(() => game.StepTick(new RefusedInput()));
                Assert.That(refused.Message, Is.EqualTo("The input source refuses this tick."));
                Assert.That(completions, Is.EqualTo(1), "A refused tick does not complete.");
                game.StepTick(Idle());
                Assert.That(completions, Is.EqualTo(2), "The next complete tick must not inherit the refused tick's stage.");
            }
            finally { game.TickCompleted -= completed; }
        }

        private ulong RunBots(int ticks)
        {
            foreach (RaceWorld world in worlds) world.Game.SetInputSource(new CircuitBot(world.Game, world.Player, world.Grapple, world.Circuit));
            Begin();
            ulong hash = 14695981039346656037UL;
            for (int t = 0; t < ticks; t++)
            {
                Step();
                foreach (RaceWorld world in worlds)
                {
                    Vector3 p = world.Player.transform.position;
                    foreach (float value in new[] { p.x, p.y, p.z })
                    {
                        hash ^= BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
                        hash *= 1099511628211UL;
                    }
                    Assert.That(world.Game.IsRunning, Is.True);
                }
            }
            return hash;
        }

        private void Begin()
        {
            player.Game.StartRun();
            Step();
        }

        private void Step() => FieldTick.Invoke(Field, null);

        private void ParkUnused()
        {
            for (int i = 0; i < worlds.Length; i++) Place(worlds[i], new Vector3(-3f + i * 2f, 1.05f, 25f), Vector3.zero);
        }

        private static void Place(RaceWorld world, Vector3 position, Vector3 velocity)
        {
            world.Player.SetPosition(position);
            CharacterController capsule = world.Player.GetComponent<CharacterController>();
            capsule.Move(Vector3.down * 0.3f);
            world.Game.Racer.Grounded = capsule.isGrounded;
            Physics.SyncTransforms();
            world.Game.Racer.Velocity = velocity;
        }

        private static void AssertSeparated(RaceWorld a, RaceWorld b, float tolerance)
        {
            Vector3 delta = a.Player.ContactCenter - b.Player.ContactCenter;
            delta.y = 0f;
            Assert.That(delta.magnitude, Is.GreaterThanOrEqualTo(a.Player.ContactRadius + b.Player.ContactRadius - tolerance), "Residual overlap must stay within the allowed metres.");
        }

        private static IInputSource Idle() => new InputReplay(new InputLog());

        private sealed class RefusedInput : IInputSource
        {
            public void Poll() { }
            public void Clear() { }
            public TickInput Consume() => throw new InvalidOperationException("The input source refuses this tick.");
        }

        private IEnumerator Load()
        {
#if UNITY_EDITOR
            RunnerProfile Profile(string name) => AssetDatabase.LoadAssetAtPath<RunnerProfile>("Assets/_Project/Data/RunnerProfile_" + name + ".asset");
            var definition = AssetDatabase.LoadAssetAtPath<TrackDefinition>("Assets/_Project/Data/Circuit_Stadium.asset");
            RaceLaunch.Request(new RaceSetup(definition, "STADIUM", "", RaceMode.Bots, SpeedClass.Normal, Profile("Alpha"), seed: 13579)
                .WithOpponents(Profile("Omega"), Profile("Beta"), Profile("Theta")));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(RacePath, new LoadSceneParameters(LoadSceneMode.Single));
            Scene scene = SceneManager.GetActiveScene();
            player = RaceWorld.FromScene(scene);
            assembler = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out RaceAssembler found)) assembler = found;
            Assert.That(assembler, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 60f;
            while (assembler.IsAssembling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(assembler.enabled, Is.True);
            Assert.That(assembler.IsAssembling, Is.False);
            Assert.That(Field, Is.Not.Null);
            Field.enabled = false; // Invoke the real field tick synchronously; the frame cannot add extra ticks.
            worlds = new[] { player, Field.Opponents[0], Field.Opponents[1], Field.Opponents[2] };
            foreach (RaceWorld world in worlds) world.Game.SetInputSource(Idle());
#else
            Assert.Fail("Contact tests require the Unity Editor.");
            yield break;
#endif
        }
    }
}
