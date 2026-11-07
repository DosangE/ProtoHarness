using System;
using ProtoHarness.ChainRush.Flow;
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
        private const int SampleRate = 44100;
        private AudioClip[] cues;
        private AudioClip score;
        private AudioClip wind;
        private AudioClip swingLoop;
        private AudioSource swing;
        private AudioSource trackAmbience;
        private AudioClip trackMusicClip;
        private AudioClip trackAmbienceClip;
        private float swingGain;
        private bool initialized;
        private bool paused;
        private bool playing;
        // Set in Awake, not here: GameSettings reads PlayerPrefs when it is made, which Unity refuses in a field initializer.
        private GameSettings settings;
        // The player's volumes (lobby settings) that scale musicVolume and effectsVolume; a test gives its own.
        public GameSettings Settings
        {
            get => settings;
            set => settings = value ?? throw new ArgumentNullException(nameof(value));
        }
        public bool Muted { get; private set; }
        public bool IsPlaying => playing;
        public bool IsPaused => paused;
        public int CueCount => cues.Length;
        public AudioClip MusicClip => music.clip;
        public AudioClip TrackAmbienceClip => initialized ? trackAmbience.clip : trackAmbienceClip;
        public AudioClip SwingClip => swingLoop;
        public float SwingVolume => swing.volume;

        private void Awake()
        {
            if (settings == null) settings = GameSettings.Player;
            if (game == null || player == null || music == null || effects == null || ambience == null)
            {
                Debug.LogError("ChainRushAudio: game, runner and three audio sources are required.", this);
                enabled = false;
                return;
            }
            cues = new AudioClip[Enum.GetValues(typeof(RaceCue)).Length];
            for (int cue = 0; cue < cues.Length; cue++) cues[cue] = MakeCue((RaceCue)cue);
            score = MakeScore();
            wind = MakeWind();
            swingLoop = MakeSwingLoop();
            swing = LoopSource(effects);
            swing.clip = swingLoop;
            trackAmbience = LoopSource(ambience);
            trackAmbience.clip = trackAmbienceClip;
            music.clip = trackMusicClip != null ? trackMusicClip : score; music.loop = true;
            ambience.clip = wind; ambience.loop = true;
            initialized = true;
        }

        // A second looping source beside the scene's three, set up like model (the scene cannot be changed for it).
        private AudioSource LoopSource(AudioSource model)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = model.spatialBlend;
            source.outputAudioMixerGroup = model.outputAudioMixerGroup;
            source.priority = model.priority;
            source.panStereo = model.panStereo;
            source.ignoreListenerPause = model.ignoreListenerPause;
            source.ignoreListenerVolume = model.ignoreListenerVolume;
            source.mute = Muted;
            return source;
        }

        // RaceAssembler can supply these before Awake; null deliberately keeps the scene's current sound.
        public void UseTrackSounds(AudioClip trackMusic, AudioClip environment)
        {
            if (trackMusic != null)
            {
                trackMusicClip = trackMusic;
                if (initialized)
                {
                    music.clip = trackMusic;
                    if (playing) { music.Play(); if (paused) music.Pause(); }
                }
            }
            if (environment != null)
            {
                trackAmbienceClip = environment;
                if (initialized)
                {
                    trackAmbience.clip = environment;
                    if (playing) { trackAmbience.Play(); if (paused) trackAmbience.Pause(); }
                }
            }
        }

        private void OnValidate()
        {
            if (musicVolume < 0f || musicVolume > 1f || effectsVolume < 0f || effectsVolume > 1f)
                Debug.LogError("ChainRushAudio: volumes must be within 0..1.", this);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) SetMuted(!Muted);
            // Settings also apply while paused, before the sources resume. The wind counts as an effect.
            music.volume = Muted ? 0f : musicVolume * settings.Master * settings.Music;
            effects.volume = Muted ? 0f : effectsVolume * settings.Master * settings.Effects;
            ambience.volume = Muted ? 0f : Mathf.Lerp(0.08f, 0.2f, Mathf.Clamp01(player.Speed / 20f)) * settings.Master * settings.Effects;
            trackAmbience.volume = Muted ? 0f : 0.24f * settings.Master * settings.Effects;
            if (!game.IsPaused)
                swingGain = Mathf.MoveTowards(swingGain, game.IsRunning && player.IsSwinging ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            swing.volume = Muted ? 0f : swingGain * 0.22f * effectsVolume * settings.Master * settings.Effects;
            if (game.IsPaused)
            {
                if (!paused) { music.Pause(); effects.Pause(); ambience.Pause(); swing.Pause(); trackAmbience.Pause(); paused = true; }
                return;
            }
            if (paused) { music.UnPause(); effects.UnPause(); ambience.UnPause(); swing.UnPause(); trackAmbience.UnPause(); paused = false; }
            if (game.IsRunning && !playing)
            {
                music.Play(); ambience.Play(); swing.Play();
                if (trackAmbience.clip != null) trackAmbience.Play();
                playing = true;
            }
            if (!game.IsRunning && playing)
            {
                music.Stop(); ambience.Stop(); swing.Stop(); trackAmbience.Stop();
                swingGain = 0f; swing.volume = 0f; playing = false;
            }
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            music.mute = effects.mute = ambience.mute = swing.mute = trackAmbience.mute = muted;
        }

        public void ResetAudio()
        {
            music.Stop(); effects.Stop(); ambience.Stop(); swing.Stop(); trackAmbience.Stop();
            swingGain = 0f; swing.volume = 0f;
            playing = false; paused = false;
        }

        public void PlayCue(RaceCue cue)
        {
            if (cue < RaceCue.Jump || (int)cue >= cues.Length) throw new ArgumentOutOfRangeException(nameof(cue), cue, "ChainRushAudio: no such cue.");
            if (game.IsPaused) return;
            effects.PlayOneShot(cues[(int)cue]);
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

        private static AudioClip MakeCue(RaceCue cue)
        {
            // Keep the accepted wall hit and the unused cues' synthesis unchanged.
            if (cue != RaceCue.GuardHit && cue != RaceCue.Attack && cue != RaceCue.Damage && cue != RaceCue.Beeps && cue != RaceCue.Buzz)
                return MakeLayeredCue(cue);
            int index = (int)cue;
            float duration = CueSeconds(cue);
            var samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            var random = new System.Random(702 + index);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                float envelope = Mathf.Min(t * 250f, 1f) * Mathf.Pow(1f - t / duration, 2f);
                float value;
                switch (cue)
                {
                    case RaceCue.Attack: value = noise * 0.55f * Mathf.Exp(-t * 8f) + Mathf.Sin(2f * Mathf.PI * (800f * t - 900f * t * t)) * 0.25f; break;
                    case RaceCue.Damage: value = noise * 0.38f + Mathf.Sin(t * 510f) * 0.45f; break;
                    case RaceCue.Beeps: value = Mathf.Sin(t * 2f * Mathf.PI * 880f) * (Mathf.Repeat(t, 0.16f) < 0.08f ? 0.5f : 0f); break;
                    case RaceCue.GuardHit: value = noise * 0.6f * Mathf.Exp(-t * 7f) + Mathf.Sin(t * 2100f) * 0.22f + Mathf.Sin(t * 3559f) * 0.13f; break;
                    case RaceCue.Buzz: value = (Mathf.Sin(t * 4600f) * 0.25f + noise * 0.28f) * (0.65f + 0.35f * Mathf.Sin(t * 145f)); break;
                    default: throw new ArgumentOutOfRangeException(nameof(cue), cue, "ChainRushAudio: no sound for this cue.");
                }
                samples[i] = value * envelope * 0.8f;
            }
            return Clip("CR effect " + index, samples);
        }

        private static float CueSeconds(RaceCue cue)
        {
            switch (cue)
            {
                case RaceCue.Beeps: return 0.48f;
                case RaceCue.Footstep: return 0.09f;
                case RaceCue.Boost: return 1.8f;
                case RaceCue.Grapple: return 0.5f;
                case RaceCue.PoleHook: return 0.7f;
                case RaceCue.BoostZone: return 1.05f;
                case RaceCue.Respawn: return 0.8f;
                case RaceCue.Jump: return 0.38f;
                case RaceCue.Land: return 0.36f;
                case RaceCue.Finish: return 1.2f;
                default: return 0.32f;
            }
        }

        private static float Tone(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        private static float Decay(float hz, float t, float rate) => Tone(hz, t) * Mathf.Exp(-t * rate);
        private static float SoftClip(float value) => 0.92f * (float)Math.Tanh(value);

        private static float LowPass(float value, ref float state, float hz)
        {
            state += (1f - Mathf.Exp(-2f * Mathf.PI * hz / SampleRate)) * (value - state);
            return state;
        }

        private static AudioClip MakeLayeredCue(RaceCue cue)
        {
            float duration = CueSeconds(cue);
            var samples = new float[Mathf.RoundToInt(SampleRate * duration)];
            var random = new System.Random(702 + (int)cue);
            float lowState = 0f, airLow = 0f, airHigh = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                float rumble = LowPass(noise, ref lowState, 110f);
                float air = LowPass(noise, ref airHigh, 8000f) - LowPass(noise, ref airLow, 3000f);
                float hit = noise * Mathf.Exp(-t * 95f);
                float value;
                switch (cue)
                {
                    case RaceCue.Boost:
                        // A struck launch, sustained sub pressure and a separate incoming 3..8 kHz air layer.
                        value = 0.82f * Decay(53f, t, 0.65f) + 0.38f * Decay(79f, t, 0.9f)
                            + 0.22f * Decay(103f, t, 1.2f) + rumble * 1.2f * Mathf.Exp(-t * 0.8f)
                            + 0.5f * Decay(68f, t, 15f) + hit * 0.5f
                            + air * 0.85f * (1f - Mathf.Exp(-t * 9f)) * Mathf.Exp(-t * 0.9f);
                        break;
                    case RaceCue.BoostZone:
                        value = 0.85f * Decay(46f, t, 4f) + 0.4f * Decay(92f, t, 6f) + hit * 0.4f
                            + air * 1.15f * (1f - Mathf.Exp(-t * 25f)) * Mathf.Exp(-t * 3f)
                            * (0.7f + 0.3f * Mathf.Sin(t * t * 70f)) + rumble * 0.8f * Mathf.Exp(-t * 4f);
                        break;
                    case RaceCue.PoleHook:
                        value = 0.65f * Decay(86f, t, 17f) + 0.22f * Decay(173f, t, 24f) + hit * 0.28f;
                        break;
                    case RaceCue.Grapple:
                        value = 0.3f * Decay(132f, t, 24f) + hit * 0.18f;
                        break;
                    case RaceCue.Respawn:
                        value = 0.65f * Decay(49f, t, 2f) + 0.3f * Decay(73f, t, 3f)
                            + 0.24f * Tone(196f, t) * Tone(0.8f, t) * Mathf.Exp(-t * 3f)
                            + air * 0.55f * Mathf.Sin(Mathf.PI * t / duration);
                        break;
                    case RaceCue.Jump:
                        value = 0.65f * Decay(72f, t, 13f) + 0.28f * Decay(144f, t, 20f)
                            + air * 0.55f * Mathf.Sin(Mathf.PI * t / duration) * Mathf.Exp(-t * 6f) + hit * 0.18f;
                        break;
                    case RaceCue.Land:
                        value = 0.9f * Decay(57f, t, 16f) + 0.3f * Decay(113f, t, 23f)
                            + rumble * 2f * Mathf.Exp(-t * 18f) + hit * 0.45f;
                        break;
                    case RaceCue.Footstep:
                        value = 0.22f * Decay(92f, t, 55f) + rumble * 0.45f * Mathf.Exp(-t * 55f) + hit * 0.18f;
                        break;
                    case RaceCue.Finish:
                        value = 0.55f * Decay(65.406f, t, 2.8f) + 0.24f * Decay(130.813f, t, 3f)
                            + 0.24f * Decay(261.626f, t, 3f) + 0.2f * Decay(329.628f, t, 3.3f)
                            + 0.18f * Decay(391.995f, t, 3.6f) + hit * 0.3f;
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(cue), cue, "No layered sound for this cue.");
                }
                samples[i] = value;
            }
            if (cue == RaceCue.PoleHook || cue == RaceCue.Grapple)
            {
                bool heavy = cue == RaceCue.PoleHook;
                AddLink(samples, 0f, heavy ? 510f : 820f, heavy ? 0.7f : 0.44f, random, false);
                float time = heavy ? 0.055f : 0.035f;
                while (time < duration - 0.1f)
                {
                    float strength = (0.2f + (float)random.NextDouble() * 0.28f) * (1f - time / duration);
                    AddLink(samples, time, (heavy ? 640f : 1050f) * (0.7f + (float)random.NextDouble() * 0.7f), strength, random, false);
                    time += 0.025f + (float)random.NextDouble() * 0.06f;
                }
            }
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float edge = Mathf.Min(1f, t / 0.002f, (duration - t) / 0.06f);
                samples[i] = SoftClip(samples[i]) * edge;
            }
            return Clip("CR effect " + (int)cue, samples);
        }

        // A struck link has four inharmonic steel modes and a tiny noisy contact, with a different pitch and force per link.
        private static void AddLink(float[] samples, float start, float hz, float strength, System.Random random, bool wrap)
        {
            int offset = Mathf.RoundToInt(start * SampleRate);
            for (int i = 0; i < SampleRate / 5; i++)
            {
                int target = offset + i;
                if (!wrap && target >= samples.Length) break;
                float t = (float)i / SampleRate;
                float metal = Decay(hz, t, 28f) + 0.58f * Decay(hz * 1.481f, t, 35f)
                    + 0.36f * Decay(hz * 2.173f, t, 43f) + 0.22f * Decay(hz * 3.127f, t, 58f);
                float contact = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 420f) * 0.65f;
                float edge = Mathf.Min(1f, t * 2000f, (0.2f - t) * 200f);
                samples[target % samples.Length] += (metal * 0.5f + contact) * strength * edge;
            }
        }

        private static AudioClip MakeSwingLoop()
        {
            var samples = new float[SampleRate * 4];
            var random = new System.Random(7193);
            // Circular overlap carries each link's tail across the boundary, with no silent fade at the seam.
            for (float t = 0f; t < 4f; t += 0.035f + (float)random.NextDouble() * 0.11f)
                AddLink(samples, t, 420f + (float)random.NextDouble() * 720f, 0.16f + (float)random.NextDouble() * 0.22f, random, true);
            for (int i = 0; i < samples.Length; i++) samples[i] = SoftClip(samples[i]);
            return Clip("Swing chain - circular steel links", samples);
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
            if (swing != null) Destroy(swing);
            if (trackAmbience != null) Destroy(trackAmbience);
            if (cues != null) for (int i = 0; i < cues.Length; i++) if (cues[i] != null) Destroy(cues[i]);
            if (score != null) Destroy(score);
            if (wind != null) Destroy(wind);
            if (swingLoop != null) Destroy(swingLoop);
        }
    }
}
