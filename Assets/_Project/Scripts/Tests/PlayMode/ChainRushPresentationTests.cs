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
        public IEnumerator Audio_Boost_HasSustainedLowBodyAndGuardKeepsItsLength()
        {
            AudioClip boost = sound.GetCue((int)RaceCue.Boost);
            Assert.That(boost.frequency, Is.EqualTo(44100));
            Assert.That(boost.length, Is.GreaterThanOrEqualTo(1.5f));
            var samples = new float[boost.samples];
            Assert.That(boost.GetData(samples, 0), Is.True);
            Assert.That(EnergyBelow(samples, boost.frequency, 300d), Is.GreaterThan(0.5d), "A launch needs a heavy low body, not just a high sweep.");
            double tailEnergy = 0d;
            for (int i = samples.Length / 2; i < samples.Length; i++) tailEnergy += samples[i] * samples[i];
            Assert.That(Math.Sqrt(tailEnergy / (samples.Length - samples.Length / 2)), Is.GreaterThan(0.04d), "The second half must sustain audible pressure.");
            Assert.That(sound.GetCue((int)RaceCue.GuardHit).length, Is.EqualTo(0.32f).Within(1f / 44100f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Audio_BoostsStayLowAndDoNotCrackle_AndZonePassesBy()
        {
            AudioClip boost = sound.GetCue((int)RaceCue.Boost);
            AudioClip zone = sound.GetCue((int)RaceCue.BoostZone);
            float[] a = ReadSamples(boost), b = ReadSamples(zone);
            Spectrum(a, boost.frequency, 0d, boost.length, 3000d, 20000d, out double boostTop);
            Spectrum(b, zone.frequency, 0d, zone.length, 3000d, 20000d, out double zoneTop);
            Spectrum(b, zone.frequency, 0.25d, 0.5d, 800d, 8000d, out double zoneNear);
            Spectrum(b, zone.frequency, 0.8d, 1.03d, 800d, 8000d, out double zoneGone);
            double boostRoughness = DifferenceRms(a), zoneRoughness = DifferenceRms(b);
            Debug.Log($"Sound metrics: above 3 kHz={boostTop:F4}/{zoneTop:F4}; zone bright near/gone={zoneNear:F4}/{zoneGone:F4}; difference RMS={boostRoughness:F6}/{zoneRoughness:F6}");
            Assert.That(boost.length, Is.EqualTo(2f).Within(1f / boost.frequency));
            Assert.That(zone.length, Is.EqualTo(1.05f).Within(1f / zone.frequency));
            // A 4 kHz boost was heard as a bird (DECISIONS 2026-11-07); the picked takes measure 0.9% and 8.1% up there.
            Assert.That(boostTop, Is.LessThan(0.05d), "Boost must not whistle.");
            Assert.That(zoneTop, Is.LessThan(0.1d), "Boost zone must not whistle.");
            Assert.That(zoneNear, Is.GreaterThan(zoneGone), "The pass-by must be brightest while it is next to the player.");
            // Measured on the original 60e2de8 synthesis at 44100 Hz: 0.091363 / 0.075705 full-scale RMS.
            // Caps of 0.050 / 0.045 require at least 45% / 40% less adjacent-sample roughness, including attacks.
            Assert.That(boostRoughness, Is.LessThan(0.05d), "Boost must not crackle.");
            Assert.That(zoneRoughness, Is.LessThan(0.045d), "Boost zone must not crackle.");
            yield return null;
        }

        // The chain sounds are the recordings the user picked, cut so they start on the hit (DECISIONS 2026-11-07).
        [UnityTest]
        public IEnumerator Audio_ChainCues_ComeFromRecordings_AndStartOnTheHit()
        {
            RaceCue[] recorded = { RaceCue.PoleHook, RaceCue.Grapple };
            foreach (RaceCue cue in recorded)
            {
                AudioClip clip = sound.GetCue((int)cue);
                Assert.That(clip.length, Is.InRange(0.4f, 0.66f), cue.ToString());
                float[] samples = ReadSamples(clip);
                float peak = 0f, opening = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
                    if (i < clip.frequency / 50) opening = Mathf.Max(opening, Mathf.Abs(samples[i]));
                }
                Assert.That(opening, Is.GreaterThan(peak * 0.05f), cue + " must be heard within 20 ms of the input.");
            }
            Assert.That(sound.SwingClip.frequency, Is.EqualTo(48000));
            Assert.That(sound.SwingClip.length, Is.EqualTo(4f).Within(1f / 48000f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Audio_RebuiltCues_AreDeterministic_AndSwingSeamIsContinuous()
        {
            RaceCue[] rebuilt = { RaceCue.Boost, RaceCue.BoostZone, RaceCue.PoleHook, RaceCue.Grapple };
            var before = new float[rebuilt.Length][];
            for (int i = 0; i < rebuilt.Length; i++) before[i] = ReadSamples(sound.GetCue((int)rebuilt[i]));
            float[] loop = ReadSamples(sound.SwingClip);
            int edge = sound.SwingClip.frequency / 50;
            double edgeDifference = 0d, edgeEnergy = 0d;
            for (int i = 1; i < edge; i++)
            {
                double head = (double)loop[i] - loop[i - 1];
                double tail = (double)loop[loop.Length - i] - loop[loop.Length - i - 1];
                edgeDifference += head * head + tail * tail;
                edgeEnergy += loop[i] * loop[i] + loop[loop.Length - i] * loop[loop.Length - i];
            }
            double edgeRms = Math.Sqrt(edgeDifference / (2 * (edge - 1)));
            double seamStep = Math.Abs((double)loop[0] - loop[loop.Length - 1]);
            // A wrapped tail must remain audible at the join; its one-sample step must fit the nearby oscillations.
            Assert.That(edgeEnergy / (2 * (edge - 1)), Is.GreaterThan(1e-7d));
            Assert.That(seamStep, Is.LessThan(4d * edgeRms));
            Debug.Log($"Swing seam: step={seamStep:F6}; nearby difference RMS={edgeRms:F6}");

            // Reuse the scene fixture to destroy and rebuild the clips, testing seed determinism through the real Awake.
            yield return SetUp();
            for (int i = 0; i < rebuilt.Length; i++)
                Assert.That(ReadSamples(sound.GetCue((int)rebuilt[i])), Is.EqualTo(before[i]), rebuilt[i].ToString());
            Assert.That(ReadSamples(sound.SwingClip), Is.EqualTo(loop), "Swing synthesis must repeat sample for sample.");
        }

        private static float[] ReadSamples(AudioClip clip)
        {
            var samples = new float[clip.samples];
            Assert.That(clip.GetData(samples, 0), Is.True);
            return samples;
        }

        // Absolute RMS of adjacent-sample differences, in full-scale sample units.
        private static double DifferenceRms(float[] samples)
        {
            double sum = 0d;
            for (int i = 1; i < samples.Length; i++)
            {
                double difference = (double)samples[i] - samples[i - 1];
                sum += difference * difference;
            }
            return Math.Sqrt(sum / (samples.Length - 1));
        }

        // Hann-windowed 2048-point DFTs via Goertzel, spaced by 1024 samples. Fraction uses all non-DC bins.
        private static double Spectrum(float[] samples, int rate, double from, double to, double low, double high, out double fraction)
        {
            const int size = 2048;
            var window = new double[size];
            double band = 0d, weighted = 0d, total = 0d;
            int end = Math.Min(samples.Length, (int)(to * rate));
            for (int offset = (int)(from * rate); offset + size <= end; offset += size / 2)
            {
                for (int i = 0; i < size; i++) window[i] = samples[offset + i] * (0.5d - 0.5d * Math.Cos(2d * Math.PI * i / (size - 1)));
                for (int bin = 1; bin < size / 2; bin++)
                {
                    double coefficient = 2d * Math.Cos(2d * Math.PI * bin / size);
                    double previous = 0d, before = 0d;
                    for (int i = 0; i < size; i++)
                    {
                        double next = window[i] + coefficient * previous - before;
                        before = previous;
                        previous = next;
                    }
                    double energy = Math.Max(0d, previous * previous + before * before - coefficient * previous * before);
                    double hz = bin * (double)rate / size;
                    total += energy;
                    if (hz >= low && hz <= high) { band += energy; weighted += energy * hz; }
                }
            }
            Assert.That(total, Is.GreaterThan(0d));
            Assert.That(band, Is.GreaterThan(0d));
            fraction = band / total;
            return weighted / band;
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
