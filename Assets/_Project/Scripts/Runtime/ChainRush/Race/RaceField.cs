using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtoHarness.ChainRush.Race
{
    // A race of several runners, each in a world of its own (RaceWorld; DECISIONS 2026-11-12 "MP1 구조"): the player's
    // game and one isolated copy of the race scene for every opponent. No game steps itself here (SelfStepping off);
    // every FixedUpdate the field steps all of them one tick, the player's first and then the opponents in their order,
    // so all of them are always on the same tick. The worlds do not touch (contact between runners comes with MP2), so
    // each run is exactly the run it would be alone.
    // The opponents follow the player's game: they stand on the start line while it is ready or counting down, start
    // on the tick it starts, and are paused while it is paused. Once the player's run has ended nobody is stepped.
    // RaceAssembler adds the field when the opponents' worlds are loaded.
    public sealed class RaceField : MonoBehaviour
    {
        private ChainRushGame game;
        private RaceWorld[] opponents;
        private bool staged;

        // The opponents' worlds, in the order they are stepped; empty until Initialize.
        public IReadOnlyList<RaceWorld> Opponents => opponents ?? Array.Empty<RaceWorld>();
        public int RacerCount => opponents == null ? 0 : opponents.Length + 1;

        public void Initialize(ChainRushGame playerGame, IReadOnlyList<RaceWorld> opponentWorlds)
        {
            if (playerGame == null) throw new ArgumentNullException(nameof(playerGame));
            if (opponentWorlds == null) throw new ArgumentNullException(nameof(opponentWorlds));
            if (opponentWorlds.Count == 0) throw new ArgumentException("A field needs at least one opponent's world.", nameof(opponentWorlds));
            if (opponents != null) throw new InvalidOperationException("RaceField: the field is already set up.");
            PhysicsScene playerPhysics = playerGame.gameObject.scene.GetPhysicsScene();
            var worlds = new RaceWorld[opponentWorlds.Count];
            for (int i = 0; i < worlds.Length; i++)
            {
                RaceWorld world = opponentWorlds[i];
                if (world == null) throw new ArgumentException($"Opponent world {i} is missing.", nameof(opponentWorlds));
                if (world.Game == playerGame) throw new ArgumentException($"Opponent world {i} is the player's own world.", nameof(opponentWorlds));
                if (world.PhysicsWorld == playerPhysics)
                    throw new ArgumentException($"Opponent world {i} shares the player's physics scene; load it with RaceWorld.IsolatedLoad.", nameof(opponentWorlds));
                for (int k = 0; k < i; k++)
                    if (worlds[k].Game == world.Game) throw new ArgumentException($"Opponent worlds {k} and {i} are the same world.", nameof(opponentWorlds));
                worlds[i] = world;
            }
            game = playerGame;
            opponents = worlds;
            game.SelfStepping = false;
            for (int i = 0; i < opponents.Length; i++)
            {
                opponents[i].Game.SelfStepping = false;
                // The keyboard starts the player's run only; the field starts the others with it.
                opponents[i].Game.ManualStart = false;
            }
            Stage();
        }

        private void Start()
        {
            if (opponents != null) return;
            Debug.LogError("RaceField: Initialize was not called; the field has no worlds to run.", this);
            enabled = false;
        }

        private void Update()
        {
            if (opponents == null) return;
            MirrorPause();
            if ((game.IsReady || game.IsCountingDown) && !staged) Stage();
        }

        private void FixedUpdate()
        {
            if (opponents == null || !game.IsRunning) return;
            MirrorPause();
            if (game.Tick == 0)
            {
                // The player's run starts on this tick (a countdown ran out, or StartRun): every opponent's does too.
                for (int i = 0; i < opponents.Length; i++) opponents[i].Game.StartRun();
                staged = false;
            }
            game.StepTick();
            for (int i = 0; i < opponents.Length; i++) opponents[i].Game.StepTick();
            int tick = game.Tick;
            for (int i = 0; i < opponents.Length; i++)
            {
                ChainRushGame other = opponents[i].Game;
                // A run that has ended keeps the tick it ended on.
                if (!other.IsRunning || other.Tick == tick) continue;
                Debug.LogError($"RaceField: opponent {i} is at tick {other.Tick} and the player at tick {tick}; the worlds are out of step and the race stops.", this);
                enabled = false;
                return;
            }
        }

        // Puts every opponent on the start line, at rest, as the player's game is before its run.
        private void Stage()
        {
            for (int i = 0; i < opponents.Length; i++) opponents[i].Game.ReturnToStart();
            staged = true;
        }

        // An opponent is paused exactly while the player's game is.
        private void MirrorPause()
        {
            bool paused = game.IsPaused;
            for (int i = 0; i < opponents.Length; i++)
            {
                ChainRushGame other = opponents[i].Game;
                if (paused ? other.IsRunning : other.IsPaused) other.TogglePause();
            }
        }

        private void OnDestroy()
        {
            if (game != null) game.SelfStepping = true;
        }
    }
}
