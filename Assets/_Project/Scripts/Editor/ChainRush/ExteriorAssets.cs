using System;
using System.IO;
using ProtoHarness.ChainRush.Race;
using ProtoHarness.ChainRush.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProtoHarness.Editor.ChainRush
{
    // The assets every track's exterior builder makes the same way (user 2026-11-02, "확장성과 재사용성"): the builder's own
    // materials, the track's TrackLook, the decoration-only check, and the prefab built in a preview scene and saved over
    // the old one with its GUID kept. Saving the decoration scene, the build's scene list and the lobby entry stay with
    // the user (DECISIONS 2026-10-27 후속 2).
    public static class ExteriorAssets
    {
        public const string MaterialFolder = "Assets/_Project/Art/Materials/";

        public static void RequireIdle()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play mode and wait for compilation first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scenes first.");
        }

        public static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        public static TrackDefinition RequireDefinition(string path)
        {
            TrackDefinition definition = Require<TrackDefinition>(path);
            if (!definition.TryValidate(out string error)) throw new InvalidOperationException(error);
            return definition;
        }

        // The builder owns its materials (M_<prefix><name>): a rebuild makes the missing ones and sets every one's colour
        // and glow again, keeping its GUID. No glow unless asked: glow on everything made Palm Coast's town look
        // fluorescent (user 2026-10-30, "형광색 건물들도 별로다").
        public static Material Palette(string prefix, string name, Color color, float glow = 0f)
        {
            string path = MaterialFolder + "M_" + prefix + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Require<Material>(MaterialFolder + "M_StadiumSportsWhite.mat").shader;
                material = new Material(shader) { name = "M_" + prefix + name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.16f);
            material.SetFloat("_Metallic", 0f);
            // URP sets _EMISSION again from the global illumination flags whenever it checks the material's keywords (on
            // import, for one), so the flag must agree with the keyword: without it the glow was dropped (found 2026-11-02:
            // Palm Coast's sea and glass lost theirs on a rebuild, and Finger Lanes' never kept it).
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // The grains a road or wall surface is textured with (user 2026-11-02, "실제 아스팔트 도로 같은 디자인").
        public enum Grain { Asphalt, Concrete, Rubber }

        private const string TextureFolder = "Assets/_Project/Art/Textures/";
        private const int GrainSize = 512;
        // The grain's mean brightness: a surface's colour is divided by it, so the textured surface averages that colour.
        private const float GrainMean = 0.82f;

        // A Palette material textured with a grain (Grains) that repeats every metresPerTile on the road pieces' world
        // metre coordinates (RoadPiece), with its bumps as a normal map; it averages color.
        public static Material Surface(string prefix, string name, Color color, Grain grain, float metresPerTile, float smoothness)
        {
            if (!(metresPerTile > 0f)) throw new ArgumentOutOfRangeException(nameof(metresPerTile), metresPerTile, "A tile must be longer than zero.");
            Color tint = new Color(Mathf.Min(1f, color.r / GrainMean), Mathf.Min(1f, color.g / GrainMean), Mathf.Min(1f, color.b / GrainMean), 1f);
            Material material = Palette(prefix, name, tint);
            (Texture2D baseMap, Texture2D normalMap) = Grains(grain);
            var scale = new Vector2(1f / metresPerTile, 1f / metresPerTile);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTexture("_BumpMap", normalMap);
            material.SetTextureScale("_BumpMap", scale);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The grain's brightness map and normal map (Art/Textures/T_<grain>Grain.png and _Normal.png), made from a fixed
        // seed, so a rebuild writes the same files; a file is written only when its bytes change.
        public static (Texture2D BaseMap, Texture2D NormalMap) Grains(Grain grain)
        {
            float[] height = GrainHeights(grain);
            string basePath = TextureFolder + "T_" + grain + "Grain.png";
            string normalPath = TextureFolder + "T_" + grain + "Grain_Normal.png";
            WriteTexture(basePath, Encode(height, v => new Color(v, v, v, 1f)), false);
            float strength = grain == Grain.Asphalt ? 6f : grain == Grain.Rubber ? 4f : 3f;
            WriteTexture(normalPath, NormalPixels(height, strength), true);
            return (Require<Texture2D>(basePath), Require<Texture2D>(normalPath));
        }

        // Brightness per pixel round GrainMean, tileable: value noise on wrapping lattices, plus small stones or pores.
        private static float[] GrainHeights(Grain grain)
        {
            var random = new System.Random(20261102 + (int)grain);
            int n = GrainSize * GrainSize;
            var height = new float[n];
            switch (grain)
            {
                case Grain.Asphalt:
                    // Worn patches, a fine bitumen grain and pale and dark stones of aggregate.
                    Add(height, Noise(6, random), 0.12f);
                    Add(height, Noise(48, random), 0.12f);
                    Add(height, Noise(192, random), 0.14f);
                    Dots(height, random, 7000, 1, 0.16f, 0.65f);
                    break;
                case Grain.Concrete:
                    // Soft cloudy shading and small dark pores.
                    Add(height, Noise(4, random), 0.12f);
                    Add(height, Noise(64, random), 0.06f);
                    Dots(height, random, 1800, 1, 0.12f, 0.1f);
                    break;
                default:
                    // A running track's rubber: an even, fine granule.
                    Add(height, Noise(32, random), 0.06f);
                    Add(height, Noise(256, random), 0.16f);
                    Dots(height, random, 5000, 1, 0.08f, 0.5f);
                    break;
            }
            for (int i = 0; i < n; i++) height[i] = Mathf.Clamp(GrainMean + height[i], 0.4f, 1f);
            return height;
        }

        // Adds amplitude x (value noise - 0.5); cells lattice points across the tile, wrapping, smoothly interpolated.
        private static void Add(float[] height, float[] noise, float amplitude)
        {
            for (int i = 0; i < height.Length; i++) height[i] += (noise[i] - 0.5f) * amplitude;
        }

        private static float[] Noise(int cells, System.Random random)
        {
            var lattice = new float[cells * cells];
            for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)random.NextDouble();
            var noise = new float[GrainSize * GrainSize];
            for (int y = 0; y < GrainSize; y++)
            {
                float fy = (float)y * cells / GrainSize;
                int y0 = (int)fy, y1 = (y0 + 1) % cells;
                float ty = Mathf.SmoothStep(0f, 1f, fy - y0);
                for (int x = 0; x < GrainSize; x++)
                {
                    float fx = (float)x * cells / GrainSize;
                    int x0 = (int)fx, x1 = (x0 + 1) % cells;
                    float tx = Mathf.SmoothStep(0f, 1f, fx - x0);
                    float top = Mathf.Lerp(lattice[y0 * cells + x0], lattice[y0 * cells + x1], tx);
                    float bottom = Mathf.Lerp(lattice[y1 * cells + x0], lattice[y1 * cells + x1], tx);
                    noise[y * GrainSize + x] = Mathf.Lerp(top, bottom, ty);
                }
            }
            return noise;
        }

        // count round dots up to radius pixels (wrapping), each lighter by amount with chance lighter, else darker.
        private static void Dots(float[] height, System.Random random, int count, int radius, float amount, float lighter)
        {
            for (int k = 0; k < count; k++)
            {
                int cx = random.Next(GrainSize), cy = random.Next(GrainSize);
                int r = random.Next(radius + 1);
                float delta = random.NextDouble() < lighter ? amount : -amount;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy > r * r) continue;
                        int x = (cx + dx + GrainSize) % GrainSize, y = (cy + dy + GrainSize) % GrainSize;
                        height[y * GrainSize + x] += delta;
                    }
            }
        }

        private static Color[] Encode(float[] values, Func<float, Color> color)
        {
            var pixels = new Color[values.Length];
            for (int i = 0; i < values.Length; i++) pixels[i] = color(values[i]);
            return pixels;
        }

        // A tangent-space normal map from the heights' wrapping slopes.
        private static Color[] NormalPixels(float[] height, float strength)
        {
            var pixels = new Color[height.Length];
            for (int y = 0; y < GrainSize; y++)
                for (int x = 0; x < GrainSize; x++)
                {
                    float left = height[y * GrainSize + (x + GrainSize - 1) % GrainSize], right = height[y * GrainSize + (x + 1) % GrainSize];
                    float down = height[(y + GrainSize - 1) % GrainSize * GrainSize + x], up = height[(y + 1) % GrainSize * GrainSize + x];
                    Vector3 n = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                    pixels[y * GrainSize + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            return pixels;
        }

        // Writes the pixels as a PNG at path when the bytes differ, and sets it to repeat, with mipmaps; a normal map is
        // imported as one, the grain as linear data (it scales the material's colour).
        private static void WriteTexture(string path, Color[] pixels, bool normalMap)
        {
            var texture = new Texture2D(GrainSize, GrainSize, TextureFormat.RGB24, false);
            byte[] png;
            try
            {
                texture.SetPixels(pixels);
                texture.Apply();
                png = texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            bool changed = !File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(png);
            if (changed)
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("No texture importer for " + path);
            TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType == type && importer.sRGBTexture == false && importer.wrapMode == TextureWrapMode.Repeat
                && importer.mipmapEnabled && importer.anisoLevel == 8) return;
            importer.textureType = type;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }

        // A track's sky (TrackLook.Sky, user 2026-11-02 "하늘 … 서킷 공통적으로"): the skybox material, its horizon colour
        // that the fog thickens to, and the metres where the fog begins and ends.
        public readonly struct Sky
        {
            public readonly Material Material;
            public readonly Color Horizon;
            public readonly float FogStart;
            public readonly float FogEnd;

            public Sky(Material material, Color horizon, float fogStart, float fogEnd)
            {
                Material = material;
                Horizon = horizon;
                FogStart = fogStart;
                FogEnd = fogEnd;
            }
        }

        private const int SkyWidth = 2048;
        private const int SkyHeight = 1024;

        // A sky panorama (Art/Textures/T_<prefix>Skybox.png, latitude-longitude) and the built-in Skybox/Panoramic material
        // drawing it (M_<prefix>Skybox; no shader of our own). From the horizon up it fades from horizon to zenith; below
        // the horizon it stays the horizon colour, the fog's, so land fogged to the horizon meets the sky with no seam.
        // cover (0..1) of the sky overhead has clouds, on a layer seen in perspective so they shrink toward the horizon
        // and thin out before it; at night stars. Painted from a fixed seed, so a rebuild writes the same file.
        public static Sky BuildSky(string prefix, Color zenith, Color horizon, Color cloud, float cover, bool stars, float fogStart, float fogEnd)
        {
            if (!(cover >= 0f && cover <= 1f)) throw new ArgumentOutOfRangeException(nameof(cover), cover, "Cloud cover is a share of the sky, 0 to 1.");
            if (!(fogStart >= 0f && fogEnd > fogStart)) throw new ArgumentOutOfRangeException(nameof(fogEnd), fogEnd, $"The fog must end past its start ({fogStart} m).");
            int seed = 20261102;
            foreach (char c in prefix) seed = seed * 31 + c;
            var pixels = new Color[SkyWidth * SkyHeight];
            for (int y = 0; y < SkyHeight; y++)
            {
                float elevation = ((y + 0.5f) / SkyHeight - 0.5f) * Mathf.PI;
                float up = Mathf.Sin(elevation), across = Mathf.Cos(elevation);
                for (int x = 0; x < SkyWidth; x++)
                {
                    int i = y * SkyWidth + x;
                    if (up <= 0f)
                    {
                        pixels[i] = horizon;
                        continue;
                    }
                    Color colour = Color.Lerp(horizon, zenith, Mathf.Pow(up, 0.55f));
                    // Low over the horizon the layer is so far off that the noise breaks into specks (first bake,
                    // 2026-11-02): the clouds thin out from 0.25 down to 0.05 of the way up and are gone below.
                    if (cover > 0f && up > 0.05f)
                    {
                        float azimuth = (x + 0.5f) / SkyWidth * Mathf.PI * 2f;
                        // Where this ray meets a flat cloud layer overhead.
                        float px = across * Mathf.Cos(azimuth) / up * 1.1f, pz = across * Mathf.Sin(azimuth) / up * 1.1f;
                        float n = Fbm(px, pz, seed);
                        float edge = n - (1f - cover);
                        float density = Mathf.SmoothStep(0f, 1f, edge / 0.16f) * Mathf.SmoothStep(0f, 1f, (up - 0.05f) / 0.2f);
                        Color puff = cloud * (0.84f + 0.16f * Mathf.Clamp01(edge * 4f));
                        colour = Color.Lerp(colour, puff, density * 0.95f);
                    }
                    if (stars && up > 0.06f)
                    {
                        float twinkle = Lattice(x, y, seed + 7);
                        if (twinkle > 0.9975f) colour = Color.Lerp(colour, Color.white, (twinkle - 0.9975f) / 0.0025f * 0.8f + 0.2f);
                    }
                    pixels[i] = new Color(colour.r, colour.g, colour.b, 1f);
                }
            }
            // "Skybox", not "Sky": a builder's palette may already have its own M_<prefix>Sky (SKYWAY's pale blue).
            string texturePath = TextureFolder + "T_" + prefix + "Skybox.png";
            WriteSkyTexture(texturePath, pixels);

            string materialPath = MaterialFolder + "M_" + prefix + "Skybox.mat";
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) throw new InvalidOperationException("The built-in Skybox/Panoramic shader is missing.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_" + prefix + "Skybox" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader) material.shader = shader;
            material.SetTexture("_MainTex", Require<Texture2D>(texturePath));
            // Grey 0.5 is the shader's neutral tint: the panorama's colours as painted, the horizon the fog's.
            material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);
            material.SetFloat("_Mapping", 1f);
            material.SetFloat("_ImageType", 0f);
            material.SetFloat("_MirrorOnBack", 0f);
            // Latitude-longitude is the shader's layout without its one keyword (_MAPPING_6_FRAMES_LAYOUT); a new material
            // also carries the enum's other name, which the shader does not declare (saved as an invalid keyword).
            material.SetFloat("_Layout", 0f);
            material.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            material.DisableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            EditorUtility.SetDirty(material);
            return new Sky(material, horizon, fogStart, fogEnd);
        }

        // Value noise in five octaves, 0..1, not tiling: the cloud layer is endless.
        private static float Fbm(float x, float y, int seed)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            for (int octave = 0; octave < 5; octave++)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float tx = Mathf.SmoothStep(0f, 1f, x - x0), ty = Mathf.SmoothStep(0f, 1f, y - y0);
                float top = Mathf.Lerp(Lattice(x0, y0, seed + octave), Lattice(x0 + 1, y0, seed + octave), tx);
                float bottom = Mathf.Lerp(Lattice(x0, y0 + 1, seed + octave), Lattice(x0 + 1, y0 + 1, seed + octave), tx);
                sum += Mathf.Lerp(top, bottom, ty) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                x *= 2.03f;
                y *= 2.03f;
            }
            return sum / total;
        }

        // A fixed value in 0..1 for a lattice point.
        private static float Lattice(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        // Writes the panorama as a PNG when its bytes differ and imports it as a colour image that wraps round the
        // horizon and not over the poles, without mipmaps (they seam a panorama), compressed at high quality so the
        // gradient does not band.
        private static void WriteSkyTexture(string path, Color[] pixels)
        {
            var texture = new Texture2D(SkyWidth, SkyHeight, TextureFormat.RGB24, false);
            byte[] png;
            try
            {
                texture.SetPixels(pixels);
                texture.Apply();
                png = texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("No texture importer for " + path);
            if (importer.textureType == TextureImporterType.Default && importer.sRGBTexture && !importer.mipmapEnabled
                && importer.wrapModeU == TextureWrapMode.Repeat && importer.wrapModeV == TextureWrapMode.Clamp
                && importer.maxTextureSize == SkyWidth && importer.textureCompression == TextureImporterCompression.CompressedHQ) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = SkyWidth;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // A track's lanes (TrackLook.LaneLines): metres between lines, the half width from which the road has a middle
        // line, whether that is a double line in the accent paint, and solid rather than dashed lines.
        public readonly struct Lanes
        {
            public readonly float Width;
            public readonly float CentreFromHalfWidth;
            public readonly bool DoubleCentre;
            public readonly bool Solid;

            public Lanes(float width, float centreFromHalfWidth, bool doubleCentre, bool solid)
            {
                Width = width;
                CentreFromHalfWidth = centreFromHalfWidth;
                DoubleCentre = doubleCentre;
                Solid = solid;
            }
        }

        // The track's road, wall and paint materials, its lanes (CircuitRace.UseLook) and its sky (RaceAssembler), made at
        // path or set again.
        public static void BuildLook(string path, Material road, Material rail, Material markings, Material accent, Lanes lanes, Sky sky)
        {
            if (sky.Material == null) throw new ArgumentException("Every track's look has a sky (BuildSky).", nameof(sky));
            TrackLook look = AssetDatabase.LoadAssetAtPath<TrackLook>(path);
            if (look == null)
            {
                look = ScriptableObject.CreateInstance<TrackLook>();
                AssetDatabase.CreateAsset(look, path);
            }
            var data = new SerializedObject(look);
            data.FindProperty("road").objectReferenceValue = road;
            data.FindProperty("rail").objectReferenceValue = rail;
            data.FindProperty("markings").objectReferenceValue = markings;
            data.FindProperty("accent").objectReferenceValue = accent;
            data.FindProperty("laneWidth").floatValue = lanes.Width;
            data.FindProperty("centreFromHalfWidth").floatValue = lanes.CentreFromHalfWidth;
            data.FindProperty("doubleCentre").boolValue = lanes.DoubleCentre;
            data.FindProperty("solidLanes").boolValue = lanes.Solid;
            data.FindProperty("sky").objectReferenceValue = sky.Material;
            data.FindProperty("horizon").colorValue = sky.Horizon;
            data.FindProperty("fogStart").floatValue = sky.FogStart;
            data.FindProperty("fogEnd").floatValue = sky.FogEnd;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!look.TryValidate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssets();
        }

        // Saves mesh at path, or copies it over the mesh already there so the asset keeps its GUID; returns the asset.
        // The mesh passed in is the builder's to destroy.
        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                saved = UnityEngine.Object.Instantiate(mesh);
                AssetDatabase.CreateAsset(saved, path);
            }
            else EditorUtility.CopySerialized(mesh, saved);
            saved.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssets();
            return saved;
        }

        // Decoration only: the race's road, physics, camera and lighting are its own, so none of them here.
        public static void ValidateDecoration(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (root.GetComponentsInChildren<Collider>(true).Length != 0
                || root.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException($"{root.name} must contain decoration only: no physics or runtime scripts.");
            if (root.GetComponentsInChildren<Camera>(true).Length != 0 || root.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException($"{root.name} must use the race's camera and lighting.");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null) throw new InvalidOperationException("Missing material: " + renderer.name);
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == null) throw new InvalidOperationException("Missing decoration mesh: " + filter.name);
        }

        // Builds the exterior in a preview scene (the open scenes are not touched), checks it is decoration only and clear
        // of the road (checkClearance), and saves it at prefabPath; a rebuild keeps the prefab's GUID, so the decoration
        // scene's instance stays.
        public static GameObject SavePrefab(string prefabPath, Func<Scene, GameObject> build, Action<GameObject> checkClearance)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (checkClearance == null) throw new ArgumentNullException(nameof(checkClearance));
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = build(preview);
                ValidateDecoration(root);
                checkClearance(root);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success || saved == null) throw new IOException("Could not save the exterior prefab " + prefabPath);
                if (guid.Length != 0 && AssetDatabase.AssetPathToGUID(prefabPath) != guid)
                    throw new InvalidOperationException("Rebuilding changed the exterior prefab GUID: " + prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"{saved.name}: {saved.GetComponentsInChildren<Renderer>(true).Length} renderers; physics=0; runtime scripts=0; road corridor clear.", saved);
                return saved;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // A new, unsaved additive scene named sceneName holding one instance of the prefab, for the user to save.
        public static GameObject OpenDecorationScene(string prefabPath, string sceneName)
        {
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = sceneName;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(prefabPath), scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create " + sceneName);
            EditorSceneManager.MarkSceneDirty(scene);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            Selection.activeGameObject = instance;
            return instance;
        }
    }
}
