using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProtoHarness.ChainRush.Audio
{
    public sealed class ChainRushAudio : MonoBehaviour
    {
        [SerializeField] private ChainRushGame game;
        [SerializeField] private RunnerMotor player;
        [SerializeField] private AudioSource music;
        [SerializeField] private AudioSource effects;
        [SerializeField] private AudioSource ambience;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.65f;
        private const int SampleRate = 22050;
        private AudioClip[] cues;
        private AudioClip score;
        private AudioClip wind;
        private bool paused;
        private bool playing;
        public bool Muted { get; private set; }
        public bool IsPlaying => playing;
        public bool IsPaused => paused;
        public int CueCount => cues.Length;
        public AudioClip MusicClip => score;

        private void Awake()
        {
            if (game == null || player == null || music == null || effects == null || ambience == null)
            {
                Debug.LogError("ChainRushAudio: game, runner and three audio sources are required.", this);
                enabled = false;
                return;
            }
            cues = new AudioClip[9];
            for (int cue = 0; cue < cues.Length; cue++) cues[cue] = MakeCue(cue);
            score = MakeScore();
            wind = MakeWind();
            music.clip = score; music.loop = true;
            ambience.clip = wind; ambience.loop = true;
        }

        private void OnValidate()
        {
            if (musicVolume < 0f || musicVolume > 1f || effectsVolume < 0f || effectsVolume > 1f)
                Debug.LogError("ChainRushAudio: volumes must be within 0..1.", this);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) SetMuted(!Muted);
            if (game.IsPaused)
            {
                if (!paused) { music.Pause(); effects.Pause(); ambience.Pause(); paused = true; }
                return;
            }
            if (paused) { music.UnPause(); effects.UnPause(); ambience.UnPause(); paused = false; }
            if (game.IsRunning && !playing)
            {
                music.Play(); ambience.Play(); playing = true;
            }
            if (!game.IsRunning && playing)
            {
                music.Stop(); ambience.Stop(); playing = false;
            }
            music.volume = Muted ? 0f : musicVolume;
            effects.volume = Muted ? 0f : effectsVolume;
            ambience.volume = Muted ? 0f : Mathf.Lerp(0.08f, 0.2f, Mathf.Clamp01(player.Speed / 20f));
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            music.mute = effects.mute = ambience.mute = muted;
        }

        public void ResetAudio()
        {
            music.Stop(); effects.Stop(); ambience.Stop();
            playing = false; paused = false;
        }

        public void PlayCue(int index)
        {
            if (index < 0 || index >= cues.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (game.IsPaused) return;
            effects.PlayOneShot(cues[index]);
        }

        public AudioClip GetCue(int index)
        {
            if (index < 0 || index >= cues.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return cues[index];
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Could not fill audio clip " + name);
            return clip;
        }

        private static AudioClip MakeCue(int index)
        {
            float duration = index == 4 ? 0.48f : index == 7 ? 0.09f : 0.32f;
            var samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            var random = new System.Random(702 + index);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                float envelope = Mathf.Min(t * 250f, 1f) * Mathf.Pow(1f - t / duration, 2f);
                float value;
                switch (index)
                {
                    case 0: value = Mathf.Sin(2f * Mathf.PI * (160f * t + 700f * t * t)) * 0.4f + noise * 0.12f; break;
                    case 1: value = (Mathf.Sin(t * 3500f) + Mathf.Sin(t * 5179f) * 0.45f) * Mathf.Exp(-t * 12f) * 0.45f; break;
                    case 2: value = noise * 0.55f * Mathf.Exp(-t * 8f) + Mathf.Sin(2f * Mathf.PI * (800f * t - 900f * t * t)) * 0.25f; break;
                    case 3: value = noise * 0.38f + Mathf.Sin(t * 510f) * 0.45f; break;
                    case 4: value = Mathf.Sin(t * 2f * Mathf.PI * 880f) * (Mathf.Repeat(t, 0.16f) < 0.08f ? 0.5f : 0f); break;
                    case 5: value = noise * 0.6f * Mathf.Exp(-t * 7f) + Mathf.Sin(t * 2100f) * 0.22f + Mathf.Sin(t * 3559f) * 0.13f; break;
                    case 6: value = (Mathf.Sin(t * 4600f) * 0.25f + noise * 0.28f) * (0.65f + 0.35f * Mathf.Sin(t * 145f)); break;
                    case 7: value = noise * 0.35f * Mathf.Exp(-t * 25f) + Mathf.Sin(t * 800f) * 0.22f; break;
                    default: value = noise * 0.3f * Mathf.Exp(-t * 15f) + Mathf.Sin(t * 420f) * 0.35f; break;
                }
                samples[i] = value * envelope * 0.8f;
            }
            return Clip("CR effect " + index, samples);
        }

        private static AudioClip MakeScore()
        {
            // Eight bars at 120 BPM, all oscillators fade at note boundaries.
            var samples = new float[SampleRate * 16];
            int[] bass = { 40, 40, 43, 38, 40, 47, 43, 38 };
            int[] arp = { 0, 7, 12, 15, 12, 7, 3, 7 };
            var random = new System.Random(909);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                int bar = (int)(t / 2f);
                float beat = t % 0.5f;
                float step = t % 0.25f;
                float bassHz = 440f * Mathf.Pow(2f, (bass[bar] - 69f) / 12f);
                float bassEnv = Mathf.Sin(Mathf.PI * step / 0.25f);
                float low = (Mathf.Sin(2f * Mathf.PI * bassHz * step) + Mathf.Sin(4f * Mathf.PI * bassHz * step) * 0.25f) * bassEnv * 0.16f;
                float kick = Mathf.Sin(2f * Mathf.PI * (48f * beat + 6f * (1f - Mathf.Exp(-beat * 30f)))) * Mathf.Exp(-beat * 22f) * 0.36f;
                float noise = (float)random.NextDouble() * 2f - 1f;
                float snare = ((int)(t * 2f) % 2 == 1 ? 1f : 0f) * noise * Mathf.Exp(-beat * 32f) * 0.17f;
                float hat = noise * Mathf.Exp(-step * 90f) * 0.07f;
                float highHz = bassHz * 4f * Mathf.Pow(2f, arp[(int)(t * 4f) % arp.Length] / 12f);
                float synth = Mathf.Sin(2f * Mathf.PI * highHz * step) * bassEnv * 0.07f;
                float fade = Mathf.Min(1f, t * 30f, (16f - t) * 30f);
                samples[i] = (low + kick + snare + hat + synth) * fade;
            }
            return Clip("Neon Transit - original synth loop", samples);
        }

        private static AudioClip MakeWind()
        {
            var samples = new float[SampleRate * 4];
            var random = new System.Random(71);
            float smooth = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                smooth = Mathf.Lerp(smooth, (float)random.NextDouble() * 2f - 1f, 0.07f);
                samples[i] = smooth * Mathf.Sin(Mathf.PI * i / (samples.Length - 1));
            }
            return Clip("Rooftop air", samples);
        }

        private void OnDestroy()
        {
            if (cues != null) for (int i = 0; i < cues.Length; i++) if (cues[i] != null) Destroy(cues[i]);
            if (score != null) Destroy(score);
            if (wind != null) Destroy(wind);
        }
    }
}
