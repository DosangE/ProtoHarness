using System.Collections;
using System.IO;
using NUnit.Framework;
using ProtoHarness.ChainRush;
using ProtoHarness.ChainRush.Audio;
using ProtoHarness.ChainRush.Endless;
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
    public sealed class ChainRushPresentationTests
    {
        private ChainRushGame game;
        private ChainRushAudio sound;
        private RunnerAnimation animation;
        private RunnerMotor player;
        private ProceduralCourse course;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/ChainRushProcedural.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Fail("Presentation tests require the Unity Editor.");
            yield break;
#endif
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.TryGetComponent(out ChainRushGame g)) { game = g; sound = root.GetComponent<ChainRushAudio>(); }
                if (root.TryGetComponent(out RunnerMotor p)) { player = p; animation = root.GetComponent<RunnerAnimation>(); }
                if (root.TryGetComponent(out ProceduralCourse c)) course = c;
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
            // Four metres before the edge of a grapple gap, as the old deck test stood: jump, then grab the anchor.
            double gapStart = FindGrappleGap();
            Assert.That(gapStart, Is.GreaterThan(0d), "No seed below 100 lays a grapple gap in its first 300 m.");
            TrackFrame frame = game.Track.FrameAt(gapStart - 4d);
            player.ShiftOrigin(frame.TransformPoint(new Vector3(0f, 1.05f, 0f)) - player.transform.position);
            player.FaceTrack();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            float landed = Time.time + 2f;
            while (!player.IsGrounded && Time.time < landed) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "Runner never landed on the procedural road.");
            player.PrimaryAction();
            yield return new WaitForSeconds(0.25f);
            Assert.That(player.GetComponent<GrappleController>().TryAttach(), Is.True);
            yield return new WaitForSeconds(0.2f);
            Assert.That(Quaternion.Angle(animation.RightArmPose, Quaternion.identity), Is.GreaterThan(110f));
            Assert.That(player.transform.position.y, Is.GreaterThan(1f));
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetTempPath(), "ChainRush-art-grapple.png"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Audio_GeneratedClips_HaveSignalAndNoClipping()
        {
            Assert.That(sound.CueCount, Is.EqualTo(9));
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

        // Seeds 0..99 until a grapple gap shows up in the first 300 m, past S 40; returns its start S. The run is started.
        private double FindGrappleGap()
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                course.SetSeed(seed);
                game.StartRun();
                if (course.TryGetNextGap(out double startS, out _, out bool hasAnchor) && hasAnchor && startS > 40d) return startS;
            }
            return 0d;
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
