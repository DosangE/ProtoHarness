using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoHarness.ChainRush.Flow;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProtoHarness.Tests.EditMode
{
    // The lobby's runner preview (RunnerPreview): one copy per look on a turntable, only the shown one active, centred on
    // the turntable's axis; a drag turns it and the angle stays when another runner is shown; a runner without a look
    // shows nothing; releasing destroys what it made.
    public sealed class RunnerPreviewTests
    {
        private static readonly Color Background = new Color(0.026f, 0.056f, 0.081f, 1f);
        private readonly List<Object> made = new List<Object>();
        private RunnerPreview preview;

        [TearDown]
        public void TearDown()
        {
            preview?.Release();
            preview = null;
            foreach (Object thing in made)
                if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
        }

        [Test]
        public void Show_ARunner_OnlyItsCopyIsVisibleAndTheCameraDraws()
        {
            preview = new RunnerPreview(new[] { Look("A", Vector3.zero), Look("B", Vector3.zero), Look("C", Vector3.zero) }, 64, 96, Background);
            Assert.That(preview.Shown, Is.EqualTo(-1));
            Assert.That(preview.Rendering, Is.False, "Nothing is drawn before Show.");
            for (int i = 0; i < 3; i++) Assert.That(preview.IsVisible(i), Is.False, "runner " + i);

            preview.Show(1);
            Assert.That(preview.Shown, Is.EqualTo(1));
            Assert.That(preview.Rendering, Is.True);
            Assert.That(new[] { preview.IsVisible(0), preview.IsVisible(1), preview.IsVisible(2) }, Is.EqualTo(new[] { false, true, false }));

            preview.Show(2);
            Assert.That(new[] { preview.IsVisible(0), preview.IsVisible(1), preview.IsVisible(2) }, Is.EqualTo(new[] { false, false, true }));

            preview.Hide();
            Assert.That(preview.Rendering, Is.False);
            Assert.That(preview.IsVisible(2), Is.True, "The runner stays on the turntable for the next Show.");
            Assert.That(preview.Texture.width, Is.EqualTo(64));
            Assert.That(preview.Texture.height, Is.EqualTo(96));
        }

        [Test]
        public void Turn_DragRight_TurnsByDegreesPerPixelWrapsAndKeepsTheAngleAcrossRunners()
        {
            preview = new RunnerPreview(new[] { Look("A", Vector3.zero), Look("B", Vector3.zero) }, 64, 96, Background);
            Assert.That(preview.Yaw, Is.EqualTo(Mathf.Repeat(RunnerPreview.StartYaw, 360f)).Within(1e-4f));
            preview.Show(0);
            preview.Turn(100f);
            float expected = Mathf.Repeat(RunnerPreview.StartYaw - 100f * RunnerPreview.DegreesPerPixel, 360f);
            Assert.That(preview.Yaw, Is.EqualTo(expected).Within(1e-3f));
            preview.Show(1);
            Assert.That(preview.Yaw, Is.EqualTo(expected).Within(1e-3f), "Showing another runner keeps the angle.");
            preview.Turn(-360f / RunnerPreview.DegreesPerPixel);
            Assert.That(preview.Yaw, Is.EqualTo(expected).Within(1e-2f), "A whole turn comes back to the same angle.");
            Assert.That(preview.Yaw, Is.GreaterThanOrEqualTo(0f).And.LessThan(360f));
        }

        [Test]
        public void New_LookOffItsOrigin_TheCopyIsCentredOnTheTurntableAxis()
        {
            preview = new RunnerPreview(new[] { Look("Off", new Vector3(5f, 1f, -3f)) }, 64, 96, Background);
            preview.Show(0);
            Vector3 axis = Centre(0);
            for (int turn = 1; turn <= 3; turn++)
            {
                preview.Turn(90f / RunnerPreview.DegreesPerPixel);
                Vector3 centre = Centre(turn);
                Assert.That(centre.x, Is.EqualTo(axis.x).Within(1e-3f), "turn " + turn);
                Assert.That(centre.z, Is.EqualTo(axis.z).Within(1e-3f), "turn " + turn);
            }
        }

        [Test]
        public void New_RunnerWithoutALook_ShowsNothingForIt()
        {
            preview = new RunnerPreview(new[] { Look("A", Vector3.zero), null }, 64, 96, Background);
            Assert.That(preview.HasModel(0), Is.True);
            Assert.That(preview.HasModel(1), Is.False);
            preview.Show(0);
            preview.Show(1);
            Assert.That(preview.IsVisible(0), Is.False, "The previous runner is taken off.");
            Assert.That(preview.IsVisible(1), Is.False);
            Assert.That(preview.Shown, Is.EqualTo(1));
        }

        [Test]
        public void New_BadArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new RunnerPreview(null, 64, 96, Background));
            Assert.Throws<ArgumentException>(() => new RunnerPreview(new GameObject[0], 0, 96, Background));
            GameObject empty = Track(new GameObject("Empty look"));
            var error = Assert.Throws<ArgumentException>(() => new RunnerPreview(new[] { Look("A", Vector3.zero), empty }, 64, 96, Background));
            Assert.That(error.Message, Does.Contain("Empty look"));
            Assert.That(GameObject.Find("Runner preview"), Is.Null, "A refused preview leaves no stage behind.");

            preview = new RunnerPreview(new[] { Look("A", Vector3.zero) }, 64, 96, Background);
            Assert.Throws<ArgumentOutOfRangeException>(() => preview.Show(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => preview.Show(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => preview.HasModel(1));
        }

        [Test]
        public void Release_DestroysTheStageAndTheTexture()
        {
            preview = new RunnerPreview(new[] { Look("A", Vector3.zero) }, 64, 96, Background);
            Assert.That(GameObject.Find("Runner preview"), Is.Not.Null);
            preview.Release();
            Assert.That(GameObject.Find("Runner preview"), Is.Null);
            Assert.That(preview.Texture == null, Is.True, "The texture is destroyed.");
            preview.Release();
            preview = null;
        }

        // ---- helpers -------------------------------------------------------------------------------

        // A look whose only part, a cube, sits at offset from the look's root.
        private GameObject Look(string name, Vector3 offset)
        {
            GameObject root = Track(new GameObject(name));
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            cube.transform.localPosition = offset;
            cube.transform.localScale = new Vector3(1f, 2f, 0.5f);
            return root;
        }

        // The world centre of the shown copy's renderers.
        private Vector3 Centre(int turn)
        {
            GameObject stage = GameObject.Find("Runner preview");
            Assert.That(stage, Is.Not.Null, "turn " + turn);
            Renderer[] renderers = stage.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.EqualTo(1), "Only the shown copy is active.");
            return renderers[0].bounds.center;
        }

        private T Track<T>(T thing) where T : Object
        {
            made.Add(thing);
            return thing;
        }
    }
}
