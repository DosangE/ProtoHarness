using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProtoHarness.ChainRush.Race
{
    // One racer's world: a loaded copy of the circuit scene, with its own game, runner, grapple and circuit. Racers that
    // do not touch each other (DESIGN.md P3 C1) each get a copy loaded with IsolatedLoad, so the copy has its own physics
    // scene and one racer's body, road and sight lines are invisible to the others; the simulation's physics queries
    // run in the physics scene of the runner's own scene. A shared world is for the stage where racers touch (C2).
    public sealed class RaceWorld
    {
        // Load parameters for a copy of the circuit scene that gets a physics scene of its own.
        public static LoadSceneParameters IsolatedLoad => new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics3D);

        private RaceWorld(Scene scene, ChainRushGame game, RunnerMotor player, GrappleController grapple, CircuitRace circuit)
        {
            Scene = scene;
            Game = game;
            Player = player;
            Grapple = grapple;
            Circuit = circuit;
        }

        public Scene Scene { get; }
        public ChainRushGame Game { get; }
        public RunnerMotor Player { get; }
        public GrappleController Grapple { get; }
        public CircuitRace Circuit { get; }
        public PhysicsScene PhysicsWorld => Scene.GetPhysicsScene();

        // Collects the racer's parts from a loaded circuit scene. Throws if a part is missing or doubled, so a scene
        // that is not a circuit race is refused here and not later in the middle of a tick.
        public static RaceWorld FromScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("The scene must be loaded.", nameof(scene));
            ChainRushGame game = null;
            RunnerMotor player = null;
            CircuitRace circuit = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) game = Single(game, g, scene);
                if (root.TryGetComponent(out RunnerMotor p)) player = Single(player, p, scene);
                if (root.TryGetComponent(out CircuitRace c)) circuit = Single(circuit, c, scene);
            }
            if (game == null || player == null || circuit == null)
                throw new InvalidOperationException($"RaceWorld: scene '{scene.name}' must hold a ChainRushGame, a RunnerMotor and a CircuitRace at its roots.");
            if (!player.TryGetComponent(out GrappleController grapple))
                throw new InvalidOperationException($"RaceWorld: the runner in scene '{scene.name}' has no GrappleController.");
            return new RaceWorld(scene, game, player, grapple, circuit);
        }

        private static T Single<T>(T found, T next, Scene scene) where T : Component
        {
            if (found != null) throw new InvalidOperationException($"RaceWorld: scene '{scene.name}' holds two {typeof(T).Name} at its roots.");
            return next;
        }
    }
}
