using System;
using UnityEngine;

namespace ProtoHarness.ChainRush.Flow
{
    // Presentation only: the lobby's runner preview (user 2026-10-20). A stage made here, far from anything else, holds
    // a copy of every runner's look (RunnerProfile.Look) on a turntable, a camera that draws the shown one into Texture
    // and a light of its own (the lobby scene has none), so the scene needs no edit. The copies are made once; showing
    // another runner only switches which is active, and turning only rotates the turntable.
    // The lobby draws Texture over its OnGUI background, which covers the main camera, so the stage needs no layer.
    public sealed class RunnerPreview
    {
        // A drag across the 1600-wide lobby turns the runner about two turns.
        public const float DegreesPerPixel = 0.45f;
        // Seen from the front and a little from the side (the model faces +Z, the camera looks back along -Z).
        public const float StartYaw = -30f;
        private static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);
        private const float FieldOfView = 30f;
        // Room around the largest look, and how far the camera looks down on it.
        private const float Margin = 1.15f;
        private const float Pitch = 8f;

        private readonly GameObject stage;
        private readonly Transform turntable;
        private readonly GameObject[] models;
        private readonly Camera camera;
        private readonly RenderTexture texture;
        private int shown = -1;
        private float yaw = StartYaw;

        // looks: per runner, its look or null (that runner then shows nothing). The texture is width x height, cleared
        // to background.
        public RunnerPreview(GameObject[] looks, int width, int height, Color background)
        {
            if (looks == null) throw new ArgumentNullException(nameof(looks));
            if (width <= 0 || height <= 0) throw new ArgumentException($"RunnerPreview: the texture must be positive, not {width} x {height}.");
            stage = new GameObject("Runner preview");
            stage.transform.position = StagePosition;
            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(stage.transform, false);

            // Each copy is centred on the turntable's axis, measured before any turn.
            models = new GameObject[looks.Length];
            float radius = 0f;
            for (int i = 0; i < looks.Length; i++)
            {
                if (looks[i] == null) continue;
                GameObject model = UnityEngine.Object.Instantiate(looks[i], turntable, false);
                model.name = looks[i].name;
                model.SetActive(true);
                if (!TryMeasure(model, out Bounds bounds))
                {
                    Release();
                    throw new ArgumentException($"RunnerPreview: the look '{looks[i].name}' has nothing to draw.", nameof(looks));
                }
                model.transform.localPosition = turntable.InverseTransformVector(turntable.position - bounds.center);
                radius = Mathf.Max(radius, bounds.extents.magnitude);
                model.SetActive(false);
                models[i] = model;
            }

            texture = new RenderTexture(width, height, 24) { name = "Runner preview", antiAliasing = 4 };
            texture.Create();
            camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = FieldOfView;
            camera.targetTexture = texture;
            camera.enabled = false;
            // Far enough that the largest look fits the narrower of the two angles.
            float halfVertical = FieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * width / height);
            float distance = Mathf.Max(radius, 0.1f) * Margin / Mathf.Sin(Mathf.Min(halfVertical, halfHorizontal));
            camera.transform.localRotation = Quaternion.Euler(Pitch, 180f, 0f);
            camera.transform.localPosition = -(camera.transform.localRotation * Vector3.forward) * distance;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;

            var light = new GameObject("Preview light").AddComponent<Light>();
            light.transform.SetParent(stage.transform, false);
            light.type = LightType.Directional;
            light.transform.localRotation = Quaternion.Euler(35f, 160f, 0f);
            light.intensity = 1.2f;

            Turn(0f);
        }

        // What the camera draws (the shown runner), for the lobby to put on screen.
        public Texture Texture => texture;
        // The runner shown, or -1 before Show.
        public int Shown => shown;
        // The turntable's angle in degrees, in [0, 360).
        public float Yaw => yaw;
        // Whether the camera is drawing (between Show and Hide).
        public bool Rendering => camera.enabled;
        public bool HasModel(int runner) => models[Check(runner)] != null;
        // Whether that runner's copy is the one on the turntable.
        public bool IsVisible(int runner) => models[Check(runner)] != null && models[runner].activeSelf;

        // Puts that runner on the turntable (keeping the angle) and starts drawing.
        public void Show(int runner)
        {
            Check(runner);
            if (shown >= 0 && models[shown] != null) models[shown].SetActive(false);
            shown = runner;
            if (models[shown] != null) models[shown].SetActive(true);
            camera.enabled = true;
        }

        // Stops drawing; the runner stays on the turntable for the next Show.
        public void Hide() => camera.enabled = false;

        // A horizontal drag of pixels (lobby units, 1600 wide): dragging right turns the runner's front to the right.
        public void Turn(float pixels)
        {
            yaw = Mathf.Repeat(yaw - pixels * DegreesPerPixel, 360f);
            turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Destroys the stage and the texture. Safe to call again.
        public void Release()
        {
            if (camera != null) camera.targetTexture = null;
            if (texture != null) texture.Release();
            DestroyNow(texture);
            DestroyNow(stage);
        }

        private int Check(int runner)
        {
            if (runner < 0 || runner >= models.Length)
                throw new ArgumentOutOfRangeException(nameof(runner), runner, $"RunnerPreview: there are {models.Length} runners.");
            return runner;
        }

        // The bounds of every renderer under model, false when it has none.
        private static bool TryMeasure(GameObject model, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }
            return any;
        }

        // The lobby releases the preview while playing; an EditMode test cannot wait for Destroy.
        private static void DestroyNow(UnityEngine.Object thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(thing);
            else UnityEngine.Object.DestroyImmediate(thing);
        }
    }
}
