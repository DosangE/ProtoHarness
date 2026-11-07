using System;
using System.IO;
using System.Text;
using ProtoHarness.ChainRush.Race;
using UnityEditor;
using UnityEngine;

namespace ProtoHarness.Editor.ChainRush
{
    // Original, fixed-seed synthesis only. All event tails and delay taps wrap round the PCM buffer before encoding.
    public static class TrackSoundBuilder
    {
        private const int SampleRate = 44100;
        private const string Folder = "Assets/_Project/Audio/";
        private enum Theme { Stadium, Skyway, FingerLanes, PalmCoast }
        private enum Voice { Bass, Brass, Drive, Pluck, Keys, Pad, Kick, Snare, Hat, Tom, Shaker }

        [MenuItem("ProtoHarness/Chain Rush/Rebuild Track Sounds")]
        public static void Rebuild()
        {
            ExteriorAssets.RequireIdle();
            // Preflight every destination before writing any sound. These are the existing look asset filenames.
            // Only Finger Lanes is kept: the user heard all four and liked this one (2026-11-05, "손가락맵 음원 말고는 다 별로다").
            // The other themes' code stays for now, unbuilt, until those tracks get their own sounds.
            Theme[] themes = { Theme.FingerLanes };
            var names = new string[themes.Length];
            for (int i = 0; i < themes.Length; i++) names[i] = themes[i].ToString();
            var looks = new TrackLook[names.Length];
            for (int i = 0; i < names.Length; i++)
                looks[i] = ExteriorAssets.Require<TrackLook>("Assets/_Project/Data/TrackLook_" + names[i] + ".asset");
            Directory.CreateDirectory(Folder);
            for (int i = 0; i < names.Length; i++)
            {
                Theme theme = themes[i];
                string musicPath = Folder + names[i] + "_Music.wav";
                string ambiencePath = Folder + names[i] + "_Ambience.wav";
                WriteSound(musicPath, MakeMusic(theme), 2, true);
                WriteSound(ambiencePath, MakeAmbience(theme), 1, false);
                var data = new SerializedObject(looks[i]);
                SerializedProperty music = data.FindProperty("music"), ambience = data.FindProperty("ambience");
                if (music == null || ambience == null) throw new InvalidOperationException("TrackLook's sound fields are missing: " + names[i]);
                music.objectReferenceValue = ExteriorAssets.Require<AudioClip>(musicPath);
                ambience.objectReferenceValue = ExteriorAssets.Require<AudioClip>(ambiencePath);
                if (data.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(looks[i]);
                Debug.Log("Rebuilt original track sounds: " + names[i], looks[i]);
            }
            AssetDatabase.SaveAssets();
        }

        private static float[] MakeMusic(Theme theme)
        {
            int bpm = theme == Theme.Stadium ? 130 : theme == Theme.Skyway ? 140 : theme == Theme.FingerLanes ? 120 : 100;
            int bars = theme == Theme.PalmCoast ? 12 : 16;
            int frames = (int)Math.Round(SampleRate * bars * 240d / bpm);
            var samples = new float[frames * 2];
            // Derive the grid from the rounded buffer length so the last beat meets the first without drift.
            double step = (double)frames / SampleRate / (bars * 16);
            var random = new System.Random(20261105 + (int)theme * 137);
            int[] roots;
            int[] melody;
            switch (theme)
            {
                case Theme.Stadium: roots = new[] { 38, 34, 41, 36 }; melody = new[] { 12, 7, 10, 12, 15, 14, 10, 7 }; break;
                case Theme.Skyway: roots = new[] { 40, 36, 43, 38 }; melody = new[] { 7, 12, 15, 19, 15, 12, 10, 14 }; break;
                case Theme.FingerLanes: roots = new[] { 48, 45, 41, 43 }; melody = new[] { 4, 7, 12, 11, 7, 4, 2, 7 }; break;
                case Theme.PalmCoast: roots = new[] { 41, 38, 43, 36 }; melody = new[] { 7, 11, 14, 16, 14, 11, 7, 4 }; break;
                default: throw new ArgumentOutOfRangeException(nameof(theme));
            }
            for (int bar = 0; bar < bars; bar++)
            {
                int chord = bar % 4;
                int root = roots[chord];
                bool minor = theme == Theme.Stadium ? chord == 0 : theme == Theme.Skyway ? chord != 2 : theme == Theme.FingerLanes ? chord == 1 : chord == 1 || chord == 2;
                int third = minor ? 3 : 4;
                int[] harmony = { 0, third, 7, theme == Theme.PalmCoast ? (minor ? 10 : 11) : 12 };
                double barTime = bar * 16 * step;
                for (int h = 0; h < harmony.Length; h++)
                    Note(samples, barTime, step * 16, root + 12 + harmony[h], Voice.Pad, theme == Theme.Skyway ? 0.055f : 0.045f, -0.7f + h * 0.45f, random);
                for (int s = 0; s < 16; s++)
                {
                    double time = barTime + s * step;
                    // The coast has a late offbeat; the other tracks stay straight and driving.
                    if (theme == Theme.PalmCoast && s % 2 == 1) time += step * 0.16d;
                    bool kick = theme == Theme.Stadium ? s == 0 || s == 6 || s == 8 || s == 14
                        : theme == Theme.Skyway ? s % 4 == 0 : theme == Theme.FingerLanes ? s == 0 || s == 8 : s == 0 || s == 7 || s == 10;
                    if (kick) Note(samples, time, 0.42d, 36, Voice.Kick, theme == Theme.Stadium ? 0.65f : 0.5f, 0f, random);
                    if (s == 4 || s == 12)
                        Note(samples, time, 0.22d, 38, Voice.Snare, theme == Theme.Stadium ? 0.32f : theme == Theme.FingerLanes ? 0.12f : 0.23f, -0.08f, random);
                    if (s % 2 == 0 || theme == Theme.Skyway)
                        Note(samples, time, 0.075d, 80, theme == Theme.PalmCoast || theme == Theme.FingerLanes ? Voice.Shaker : Voice.Hat,
                            s % 4 == 2 ? 0.12f : 0.065f, s % 4 == 0 ? -0.45f : 0.45f, random);
                    if (theme == Theme.Stadium && (s == 2 || s == 10 || (bar % 4 == 3 && s >= 13)))
                        Note(samples, time, 0.3d, s % 2 == 0 ? 43 : 38, Voice.Tom, 0.25f, s % 2 == 0 ? -0.4f : 0.4f, random);

                    bool bass = theme == Theme.Skyway ? s % 2 == 0 : theme == Theme.PalmCoast ? s == 0 || s == 6 || s == 10 || s == 14 : s % 4 == 0 || s == 14;
                    if (bass)
                        Note(samples, time, step * (theme == Theme.PalmCoast ? 2.7d : 1.7d), root - (theme == Theme.FingerLanes ? 12 : 0) + (s == 14 ? 7 : 0), Voice.Bass, 0.32f, 0f, random);

                    if (theme == Theme.FingerLanes && s % 4 == 2)
                    {
                        // Staggered strings and a resonant body, not a single oscillator pretending to be an acoustic.
                        for (int h = 0; h < 4; h++)
                            Note(samples, time + h * 0.012d, 0.65d, root + harmony[h], Voice.Pluck, 0.14f, -0.65f + h * 0.16f, random);
                    }
                    if (theme == Theme.PalmCoast && (s == 2 || s == 9 || s == 14))
                        for (int h = 0; h < 4; h++)
                            Note(samples, time, step * 3d, root + 12 + harmony[h], Voice.Keys, 0.095f, 0.45f - h * 0.2f, random);
                    if (theme == Theme.Skyway && s % 2 == 1)
                        Note(samples, time, step * 0.85d, root + 24 + harmony[(s / 2 + bar) % 4], Voice.Drive, 0.12f, s % 4 == 1 ? -0.55f : 0.55f, random);
                    bool lead = theme == Theme.Stadium ? s % 4 == 0 || s == 10 : theme == Theme.Skyway ? s % 4 == 2
                        : theme == Theme.FingerLanes ? s % 2 == 0 && s != 6 : s == 3 || s == 8 || s == 14;
                    if (lead)
                    {
                        int interval = melody[(s / 2 + bar * 3) % melody.Length];
                        int degree = interval % 12, octave = interval / 12 * 12;
                        if (degree == 3 || degree == 4) interval = octave + third;
                        if (degree == 10 || degree == 11) interval = octave + (minor ? 10 : 11);
                        Voice voice = theme == Theme.Stadium ? Voice.Brass : theme == Theme.Skyway ? Voice.Drive : theme == Theme.FingerLanes ? Voice.Pluck : Voice.Keys;
                        Note(samples, time, step * (theme == Theme.Stadium ? 2.8d : 1.8d), root + 24 + interval, voice, 0.17f, 0.2f, random);
                    }
                }
            }
            Space(samples, step * 3d);
            Master(samples, 0.84f);
            return samples;
        }

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
        private static float Sine(double cycles) => (float)Math.Sin(2d * Math.PI * cycles);

        private static void Note(float[] samples, double start, double duration, int midi, Voice voice, float gain, float pan, System.Random random)
        {
            int frames = samples.Length / 2;
            int offset = (int)Math.Round(start * SampleRate), count = (int)Math.Round(duration * SampleRate);
            float hz = Hz(midi);
            float left = Mathf.Sqrt((1f - pan) * 0.5f), right = Mathf.Sqrt((1f + pan) * 0.5f);
            float filtered = 0f, previous = 0f;
            double phase = random.NextDouble();
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate, end = (float)(duration - t);
                float noise = (float)random.NextDouble() * 2f - 1f;
                filtered += 0.15f * (noise - filtered);
                float high = noise - previous;
                previous = noise;
                float envelope = Mathf.Min(1f, t * 250f, end * 120f);
                float value;
                switch (voice)
                {
                    case Voice.Bass:
                        value = (Sine(hz * t) + 0.3f * Sine(hz * 2d * t) + 0.12f * Sine(hz * 3d * t)) * Mathf.Exp(-t * 2.8f);
                        break;
                    case Voice.Pad:
                        envelope = Mathf.Min(1f, t / 0.16f, end / 0.22f);
                        value = Sine(hz * t + phase) * 0.6f + Sine(hz * 1.003d * t + phase) * 0.3f + Sine(hz * 2d * t) * 0.16f;
                        break;
                    case Voice.Brass:
                    case Voice.Drive:
                        value = 0f;
                        for (int h = 1; h <= 8; h++)
                        {
                            float colour = Mathf.Exp(-h * (voice == Voice.Brass ? 0.23f + t * 1.5f : 0.15f + t * 3f)) / h;
                            value += (Sine(hz * h * (double)t) + 0.4f * Sine(hz * h * 1.004d * t)) * colour;
                        }
                        envelope *= Mathf.Min(1f, t * (voice == Voice.Brass ? 50f : 130f));
                        break;
                    case Voice.Pluck:
                        value = 0f;
                        for (int h = 1; h <= 7; h++)
                            value += Sine(hz * h * Math.Sqrt(1d + h * h * 0.00012d) * t) * Mathf.Exp(-t * (3.5f + h * 1.9f)) / h;
                        value += filtered * Mathf.Exp(-t * 140f) * 0.45f + Sine(185d * t) * Mathf.Exp(-t * 25f) * 0.1f;
                        break;
                    case Voice.Keys:
                        value = (Sine(hz * t + 0.65d * Sine(hz * 2d * t) * Math.Exp(-t * 8d))
                            + 0.25f * Sine(hz * 3d * t) * Mathf.Exp(-t * 12f)) * Mathf.Exp(-t * 3.8f);
                        break;
                    case Voice.Kick:
                        value = Sine(47d * t + 2.8d * (1d - Math.Exp(-t * 42d))) * Mathf.Exp(-t * 12f)
                            + 0.18f * Sine(96d * t) * Mathf.Exp(-t * 22f) + high * Mathf.Exp(-t * 240f) * 0.15f;
                        break;
                    case Voice.Snare:
                        value = (noise - filtered) * Mathf.Exp(-t * 27f) * 0.65f + Sine(174d * t) * Mathf.Exp(-t * 34f) * 0.35f;
                        break;
                    case Voice.Hat: value = high * Mathf.Exp(-t * 75f) * 0.55f; break;
                    case Voice.Shaker: value = high * Mathf.Exp(-t * 48f) * Mathf.Sin(Mathf.PI * t / (float)duration) * 0.65f; break;
                    case Voice.Tom: value = (Sine(hz * t + 0.2d * (1d - Math.Exp(-t * 30d))) + 0.25f * Sine(hz * 1.59d * t)) * Mathf.Exp(-t * 16f); break;
                    default: throw new ArgumentOutOfRangeException(nameof(voice));
                }
                int target = ((offset + i) % frames) * 2;
                samples[target] += value * envelope * gain * left;
                samples[target + 1] += value * envelope * gain * right;
            }
        }

        // Finite circular taps: every reflection of the last bar arrives in the first, already in steady state.
        private static void Space(float[] samples, double beatDelay)
        {
            var dry = (float[])samples.Clone();
            int frames = samples.Length / 2;
            double[] times = { 0.043d, 0.071d, 0.113d, beatDelay, beatDelay * 2d };
            float[] gains = { 0.11f, 0.09f, 0.07f, 0.15f, 0.06f };
            for (int tap = 0; tap < times.Length; tap++)
            {
                int delay = (int)Math.Round(times[tap] * SampleRate);
                for (int i = 0; i < frames; i++)
                {
                    int from = ((i - delay + frames) % frames) * 2;
                    samples[i * 2] += dry[from + (tap % 2)] * gains[tap];
                    samples[i * 2 + 1] += dry[from + (1 - tap % 2)] * gains[tap];
                }
            }
        }

        private static float[] MakeAmbience(Theme theme)
        {
            const int Seconds = 18;
            var samples = new float[SampleRate * Seconds];
            var random = new System.Random(8161 + (int)theme * 137);
            float low = 0f, broad = 0f;
            // Run one extra buffer to settle the noise filters, then overlap the seam below.
            for (int i = -samples.Length; i < samples.Length; i++)
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += 0.012f * (noise - low);
                broad += 0.19f * (noise - broad);
                if (i < 0) continue;
                float t = (float)i / SampleRate;
                float swell = 0.5f + 0.5f * Sine(t / 6d);
                switch (theme)
                {
                    case Theme.Stadium:
                        // Dense, unpitched crowd bed with slow collective cheering waves.
                        samples[i] = low * 1.9f + broad * (0.18f + 0.65f * swell * swell);
                        break;
                    case Theme.Skyway:
                        samples[i] = broad * (0.3f + 0.22f * Sine(t / 9d)) + low * 0.7f;
                        break;
                    case Theme.FingerLanes: samples[i] = low * 0.35f + broad * 0.055f; break;
                    case Theme.PalmCoast:
                        samples[i] = broad * (0.12f + 0.9f * swell * swell) + low * (0.4f + 0.4f * swell);
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(theme));
                }
            }
            switch (theme)
            {
                case Theme.Stadium:
                    for (int n = 0; n < 65; n++)
                        EnvironmentEvent(samples, random.NextDouble() * Seconds, 0.7d + random.NextDouble() * 1.2d, 240f + (float)random.NextDouble() * 370f, 0.025f, theme, random);
                    break;
                case Theme.Skyway:
                    for (int n = 0; n < 5; n++) EnvironmentEvent(samples, n * 3.6d + 0.3d, 3d, 52f + n * 8f, 0.12f, theme, random);
                    break;
                case Theme.FingerLanes:
                    for (int n = 0; n < 22; n++) EnvironmentEvent(samples, random.NextDouble() * Seconds, 0.16d + random.NextDouble() * 0.18d, 1900f + (float)random.NextDouble() * 1600f, 0.1f, theme, random);
                    Bell(samples, 2.3d, 523.25f, 0.17f);
                    Bell(samples, 11.1d, 659.25f, 0.12f);
                    break;
                case Theme.PalmCoast:
                    for (int n = 0; n < 6; n++) EnvironmentEvent(samples, random.NextDouble() * Seconds, 0.9d, 660f + (float)random.NextDouble() * 210f, 0.13f, theme, random);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(theme));
            }
            // Fold a short head/tail crossfade into a shorter buffer: no periodic silence or abrupt noise reset.
            samples = CloseNoiseLoop(samples, SampleRate / 2);
            Master(samples, 0.72f);
            return samples;
        }

        private static void EnvironmentEvent(float[] samples, double start, double duration, float hz, float gain, Theme theme, System.Random random)
        {
            int offset = (int)Math.Round(start * SampleRate), count = (int)Math.Round(duration * SampleRate);
            double phase = 0d;
            float smooth = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate, u = (float)(t / duration);
                float noise = (float)random.NextDouble() * 2f - 1f;
                smooth += 0.08f * (noise - smooth);
                double frequency = theme == Theme.FingerLanes ? hz * (1d + 0.24d * Sine(u * 1.5d) + 0.08d * Sine(t * 26d))
                    : theme == Theme.PalmCoast ? hz * (1d + 0.28d * Sine(u * 0.8d)) : hz * (1.1d - u * 0.2d);
                phase += frequency / SampleRate;
                float value;
                switch (theme)
                {
                    case Theme.Stadium: value = (Sine(phase) * 0.25f + Sine(phase * 1.98d) * 0.12f + smooth * 1.4f) * (0.7f + 0.3f * Sine(t * 6d)); break;
                    case Theme.Skyway: value = Sine(phase) * 0.55f + Sine(phase * 2d) * 0.22f + smooth * 1.1f; break;
                    case Theme.FingerLanes: value = (Sine(phase) + 0.15f * Sine(phase * 2d)) * (0.55f + 0.45f * Sine(t * 19d)); break;
                    case Theme.PalmCoast: value = (Sine(phase) + Sine(phase * 2.03d) * 0.28f) * (0.7f + 0.3f * Sine(t * 8d)); break;
                    default: throw new ArgumentOutOfRangeException(nameof(theme));
                }
                samples[(offset + i) % samples.Length] += value * gain * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
            }
        }

        private static void Bell(float[] samples, double start, float hz, float gain)
        {
            int offset = (int)Math.Round(start * SampleRate);
            for (int i = 0; i < SampleRate * 4; i++)
            {
                float t = (float)i / SampleRate;
                float value = Sine(hz * t) * Mathf.Exp(-t * 1.5f) + 0.5f * Sine(hz * 2.756d * t) * Mathf.Exp(-t * 2.3f)
                    + 0.3f * Sine(hz * 5.404d * t) * Mathf.Exp(-t * 3.8f);
                samples[(offset + i) % samples.Length] += value * gain * Mathf.Min(1f, t * 400f, (4f - t) * 20f);
            }
        }

        private static float[] CloseNoiseLoop(float[] input, int overlap)
        {
            int length = input.Length - overlap;
            var output = new float[length];
            Array.Copy(input, overlap, output, 0, length);
            for (int i = 0; i < overlap; i++)
            {
                float mix = i / (float)(overlap - 1);
                output[length - overlap + i] = input[input.Length - overlap + i] * (1f - mix) + input[i] * mix;
            }
            return output;
        }

        private static void Master(float[] samples, float peak)
        {
            // Remove DC and set a deterministic ceiling; no hard clipping or random mastering gain.
            double mean = 0d;
            for (int i = 0; i < samples.Length; i++) mean += samples[i];
            mean /= samples.Length;
            float maximum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)Math.Tanh((samples[i] - mean) * 1.15d);
                maximum = Mathf.Max(maximum, Mathf.Abs(samples[i]));
            }
            if (!(maximum > 0f)) throw new InvalidOperationException("Synthesized sound has no signal.");
            for (int i = 0; i < samples.Length; i++) samples[i] *= peak / maximum;
        }

        private static void WriteSound(string path, float[] samples, int channels, bool music)
        {
            byte[] wav;
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, Encoding.ASCII, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)channels); writer.Write(SampleRate);
                    writer.Write(SampleRate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                    for (int i = 0; i < samples.Length; i++) writer.Write((short)Math.Round(samples[i] * 32767f));
                }
                wav = memory.ToArray();
            }
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(wav))
            {
                File.WriteAllBytes(path, wav);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("No audio importer for " + path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType load = music ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
            if (settings.compressionFormat == AudioCompressionFormat.Vorbis && settings.loadType == load
                && Mathf.Approximately(settings.quality, 0.8f) && settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate
                && !importer.forceToMono && !importer.ambisonic && settings.preloadAudioData == !music) return;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.loadType = load;
            settings.quality = 0.8f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.ambisonic = false;
            importer.SaveAndReimport();
        }
    }
}
