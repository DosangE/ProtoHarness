using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Control;
using ProtoHarness.ChainRush.Flow;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using ProtoHarness.ChainRush.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ProtoHarness.Tests.PlayMode
{
    // On the circuit scene (2026-10-18): the endless mode is on hold, and these check presentation the circuit shares.
    public sealed class ChainRushPresentationTests
    {
        // Holds the throttle and nothing else (as ChainRushTests): running needs the throttle since 2026-10-19, and the
        // keyboard source reads it as let go when no key is down. With the keyboard the runner moved only on the ticks
        // before the first frame's poll, so whether it ran at all hung on frame timing (gate 2026-10-19, GaitPhase 0).
        private sealed class FullThrottleInputSource : IInputSource
        {
            public void Poll() { }
            public TickInput Consume() => new TickInput(0f, false, false, false);
            public void Clear() { }
        }

        private ChainRushGame game;
        private ChainRushAudio sound;
        private RunnerAnimation animation;
        private RunnerMotor player;
        private CircuitRace circuit;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/ChainRushCircuit.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Presentation tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) { game = g; sound = root.GetComponent<ChainRushAudio>(); }
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; animation = root.GetComponent<RunnerAnimation>(); }
                if (root.TryGetComponent(out CircuitRace c)) circuit = c;
            }
            Assert.That(game.HasPresentation, Is.True);
            Assert.That(sound, Is.Not.Null);
            Assert.That(animation, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { yield return null; LogAssert.NoUnexpectedReceived(); }

        [UnityTest]
        public IEnumerator Presentation_RunPauseRestart_SynchronizesAudioAndJoints()
        {
            Assert.That(sound.IsPlaying, Is.False);
            game.SetInputSource(new FullThrottleInputSource());
            game.StartRun();
            yield return new WaitForSeconds(0.4f);
            Assert.That(sound.IsPlaying, Is.True);
            Assert.That(animation.GaitPhase, Is.GreaterThan(0f));
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "ChainRush-art-running.png"));
            game.TogglePause();
            yield return null;
            float phase = animation.GaitPhase;
            Quaternion pose = animation.RightArmPose;
            yield return new WaitForSeconds(0.15f);
            Assert.That(sound.IsPaused, Is.True);
            Assert.That(animation.GaitPhase, Is.EqualTo(phase));
            Assert.That(animation.RightArmPose, Is.EqualTo(pose));
            sound.SetMuted(true);
            Assert.That(sound.Muted, Is.True);
            game.StartRun();
            Assert.That(animation.GaitPhase, Is.Zero);
            Assert.That(animation.RightArmPose, Is.EqualTo(Quaternion.identity));
            yield return null;
            Assert.That(sound.IsPaused, Is.False);
            Assert.That(sound.Muted, Is.True, "Restart must preserve mute preference.");
        }

        [UnityTest]
        public IEnumerator Animation_JumpAndGrapple_ChangesArmPoseWithoutMovingMotor()
        {
            // 4.5 m before the edge of a grapple gap at the circuit's running speed (20 m/s), where the test bot jumps: jump,
            // then grab the anchor once it is offered. Since 2026-10-19 an anchor is in reach only within its height over the
            // track less standing height (about 9 m, was 20 m), so the old jump from rest 4 m before the edge never reaches it.
            game.SetInputSource(new FullThrottleInputSource());
            double gapStart = FindGrappleGap();
            Assert.That(gapStart, Is.GreaterThan(0d), "The circuit has no grapple gap with an anchor.");
            TrackFrame frame = game.Track.FrameAt(gapStart - 4.5d);
            player.ShiftOrigin(frame.TransformPoint(new Vector3(0f, 1.05f, 0f)) - player.transform.position);
            player.FaceTrack();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            float landed = Time.time + 2f;
            while (!player.IsGrounded && Time.time < landed) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the circuit road.");
            game.Racer.Velocity = frame.Forward * 20f;
            player.PrimaryAction();
            GrappleController grapple = player.GetComponent<GrappleController>();
            float deadline = Time.time + 1f;
            while (grapple.Candidate == null && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(grapple.Candidate, Is.Not.Null, $"No anchor came within the rope's reach: runner at {player.transform.position}.");
            var heard = new List<RaceCue>();
            game.CuePlayed += heard.Add;
            Assert.That(grapple.TryAttach(), Is.True);
            Assert.That(heard, Has.Exactly(1).EqualTo(RaceCue.Grapple));
            yield return new WaitForSeconds(0.2f);
            Assert.That(Quaternion.Angle(animation.RightArmPose, Quaternion.identity), Is.GreaterThan(110f));
            Assert.That(player.transform.position.y, Is.GreaterThan(1f));
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "ChainRush-art-grapple.png"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Audio_GeneratedClips_HaveSignalAndNoClipping()
        {
            Assert.That(sound.CueCount, Is.EqualTo(14));
            for (int i = 0; i <= sound.CueCount; i++)
            {
                AudioClip clip = i == sound.CueCount ? sound.MusicClip : sound.GetCue(i);
                float[] data = new float[clip.samples];
                Assert.That(clip.GetData(data, 0), Is.True);
                float peak = 0f;
                foreach (float sample in data) { Assert.That(float.IsNaN(sample), Is.False); peak = Mathf.Max(peak, Mathf.Abs(sample)); }
                Assert.That(peak, Is.GreaterThan(0.05f));
                Assert.That(peak, Is.LessThan(0.98f));
            }
            WritePreview(sound.MusicClip, Path.Combine(Path.GetTempPath(), "ChainRush-music-preview.wav"));
            yield return null;
        }

        // ChainRushGame.PlayCue once refused every cue past the first four, here too, where ChainRushAudio has them all.
        [UnityTest]
        public IEnumerator Audio_PlayCue_PlaysEveryCue()
        {
            var heard = new List<RaceCue>();
            game.CuePlayed += heard.Add;
            foreach (RaceCue cue in Enum.GetValues(typeof(RaceCue))) game.PlayCue(cue);
            Assert.That(heard.Count, Is.EqualTo(sound.CueCount));
            yield return null;
        }

        // Moves must be told apart by ear (M6 completion, DECISIONS 2026-11-05 "M6 범위"). This only keeps the waveforms
        // apart: whether they sound different enough is the user's to hear.
        [UnityTest]
        public IEnumerator Audio_MoveCues_HaveUnlikeWaveforms()
        {
            RaceCue[] moves = { RaceCue.Finish, RaceCue.GuardHit, RaceCue.Boost, RaceCue.Grapple, RaceCue.PoleHook, RaceCue.BoostZone, RaceCue.Respawn };
            for (int a = 0; a < moves.Length; a++)
                for (int b = a + 1; b < moves.Length; b++)
                    Assert.That(Likeness(sound.GetCue((int)moves[a]), sound.GetCue((int)moves[b])), Is.LessThan(0.5f), $"{moves[a]} and {moves[b]}");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Audio_Boost_HasSustainedSubBassAndGuardKeepsItsLength()
        {
            AudioClip boost = sound.GetCue((int)RaceCue.Boost);
            Assert.That(boost.frequency, Is.EqualTo(44100));
            Assert.That(boost.length, Is.GreaterThanOrEqualTo(1.5f));
            var samples = new float[boost.samples];
            Assert.That(boost.GetData(samples, 0), Is.True);
            Assert.That(EnergyBelow(samples, boost.frequency, 120d), Is.GreaterThan(0.35d), "A launch needs substantial sub bass, not just a high sweep.");
            double tailEnergy = 0d;
            for (int i = samples.Length / 2; i < samples.Length; i++) tailEnergy += samples[i] * samples[i];
            Assert.That(Math.Sqrt(tailEnergy / (samples.Length - samples.Length / 2)), Is.GreaterThan(0.04d), "The second half must sustain audible pressure.");
            Assert.That(sound.GetCue((int)RaceCue.GuardHit).length, Is.EqualTo(0.32f).Within(1f / 44100f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Audio_SwingState_FadesChainAndObeysPauseMuteEffectsAndStop()
        {
            var settings = new GameSettings("ChainRush.Tests.SoundCues.");
            GameSettings previous = sound.Settings;
            bool wasEnabled = game.enabled;
            try
            {
                settings.Clear();
                sound.Settings = settings;
                sound.SetMuted(false);
                game.StartRun();
                // Freeze ticks, not presentation: stage the motor's public swing state without frame-dependent driving.
                game.enabled = false;
                yield return null;
                Assert.That(sound.SwingVolume, Is.Zero);
                game.Racer.SwingTicks = 1;
                Assert.That(player.IsSwinging, Is.True);
                yield return new WaitForSecondsRealtime(0.25f);
                float held = sound.SwingVolume;
                Assert.That(held, Is.GreaterThan(0.05f));
                var chain = new float[sound.SwingClip.samples];
                Assert.That(sound.SwingClip.GetData(chain, 0), Is.True);
                float peak = 0f;
                foreach (float sample in chain) { Assert.That(float.IsNaN(sample), Is.False); peak = Mathf.Max(peak, Mathf.Abs(sample)); }
                Assert.That(peak, Is.GreaterThan(0.05f).And.LessThan(0.98f));

                game.TogglePause();
                yield return null;
                Assert.That(sound.IsPaused, Is.True);
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(sound.SwingVolume, Is.EqualTo(held).Within(0.001f));
                sound.SetMuted(true);
                yield return null;
                Assert.That(sound.SwingVolume, Is.Zero);
                sound.SetMuted(false);
                settings.SetEffectsLevel(0);
                yield return null;
                Assert.That(sound.SwingVolume, Is.Zero);
                settings.SetEffectsLevel(GameSettings.MaxLevel);
                game.TogglePause();
                yield return null;
                Assert.That(sound.SwingVolume, Is.GreaterThan(0.05f));
                game.Racer.SwingTicks = 0;
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(sound.SwingVolume, Is.LessThan(held));
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(sound.SwingVolume, Is.Zero);
                game.Racer.SwingTicks = 1;
                yield return new WaitForSecondsRealtime(0.25f);
                game.FailRun();
                yield return null;
                Assert.That(sound.IsPlaying, Is.False);
                Assert.That(sound.SwingVolume, Is.Zero);
                sound.ResetAudio();
                Assert.That(sound.SwingVolume, Is.Zero);
            }
            finally
            {
                game.Racer.SwingTicks = 0;
                game.enabled = wasEnabled;
                sound.Settings = previous;
                settings.Clear();
            }
        }

        [UnityTest]
        public IEnumerator Audio_TrackSounds_ReplaceMusicAndNullKeepsCurrentClips()
        {
            AudioClip original = sound.MusicClip;
            AudioClip music = AudioClip.Create("Track music fixture", 44100, 2, 44100, false);
            AudioClip environment = AudioClip.Create("Track environment fixture", 44100, 1, 44100, false);
            try
            {
                game.UseTrackSounds(null, null);
                Assert.That(sound.MusicClip, Is.SameAs(original));
                game.UseTrackSounds(music, environment);
                Assert.That(sound.MusicClip, Is.SameAs(music));
                Assert.That(sound.TrackAmbienceClip, Is.SameAs(environment));
                game.UseTrackSounds(null, null);
                Assert.That(sound.MusicClip, Is.SameAs(music));
                Assert.That(sound.TrackAmbienceClip, Is.SameAs(environment));
                game.UseTrackSounds(original, null);
                yield return null;
                Assert.That(sound.MusicClip, Is.SameAs(original));
            }
            finally
            {
                // These clips belong to the caller; ChainRushAudio only destroys its synthesized clips.
                game.UseTrackSounds(original, null);
                UnityEngine.Object.Destroy(music);
                UnityEngine.Object.Destroy(environment);
            }
        }

        // Exact DFT bins below cutoff via Goertzel, normalized with Parseval; excludes DC, which is not bass.
        private static double EnergyBelow(float[] samples, int rate, double cutoff)
        {
            double total = 0d, low = 0d;
            foreach (float sample in samples) total += (double)sample * sample;
            Assert.That(total, Is.GreaterThan(0d));
            for (int bin = 1; bin * (double)rate / samples.Length < cutoff; bin++)
            {
                double coefficient = 2d * Math.Cos(2d * Math.PI * bin / samples.Length);
                double previous = 0d, before = 0d;
                foreach (float sample in samples)
                {
                    double next = sample + coefficient * previous - before;
                    before = previous;
                    previous = next;
                }
                low += previous * previous + before * before - coefficient * previous * before;
            }
            return 2d * low / (samples.Length * total);
        }

        // How alike two clips are over their common length: 0 unrelated, 1 the same wave.
        private static float Likeness(AudioClip first, AudioClip second)
        {
            var a = new float[first.samples];
            var b = new float[second.samples];
            Assert.That(first.GetData(a, 0), Is.True);
            Assert.That(second.GetData(b, 0), Is.True);
            double both = 0d, onlyA = 0d, onlyB = 0d;
            for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++)
            {
                both += a[i] * b[i];
                onlyA += a[i] * a[i];
                onlyB += b[i] * b[i];
            }
            return Mathf.Abs((float)(both / Math.Sqrt(onlyA * onlyB)));
        }

        // The circuit's grapple gap: the first gap after S 150 (the one before it, on the first straight, is a jump gap with
        // no anchor; ChainRushCircuitTests does the same). Returns its start S, or 0 without an anchor. The run is started.
        private double FindGrappleGap()
        {
            game.StartRun();
            return circuit.TryGetNextGap(150d, out double startS, out _, out bool hasAnchor) && hasAnchor ? startS : 0d;
        }

        private static void WritePreview(AudioClip clip, string path)
        {
            float[] data = new float[clip.samples];
            Assert.That(clip.GetData(data, 0), Is.True);
            using (var file = new BinaryWriter(File.Create(path)))
            {
                file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + data.Length * 2);
                file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); file.Write(16);
                file.Write((short)1); file.Write((short)1); file.Write(clip.frequency); file.Write(clip.frequency * 2);
                file.Write((short)2); file.Write((short)16); file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(data.Length * 2);
                foreach (float sample in data) file.Write((short)(sample * 32767f));
            }
        }
    }
}
