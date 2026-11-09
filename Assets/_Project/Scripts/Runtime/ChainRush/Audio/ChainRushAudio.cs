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
        // Steel is recorded, not synthesized: three synthesis rounds never sounded like metal (DECISIONS 2026-11-07).
        [SerializeField] private AudioClip poleHookRecording;
        [SerializeField] private AudioClip grappleRecording;
        [SerializeField] private AudioClip swingRecording;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.65f;
        private const int SampleRate = 44100;
        private const float SwingFromSeconds = 4f;
        private const int SwingSeconds = 4;
        private const float SwingBlendSeconds = 0.25f;
        private static readonly int CueTotal = Enum.GetValues(typeof(RaceCue)).Length;
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
        private bool toldOff;
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
            // Every recording is read and checked before anything is built, so a bad one turns the component off whole.
            string hookFault = ReadRecording(poleHookRecording, nameof(poleHookRecording), 0f, out double[] hook);
            string grappleFault = ReadRecording(grappleRecording, nameof(grappleRecording), 0f, out double[] grapple);
            string swingFault = ReadRecording(swingRecording, nameof(swingRecording), SwingFromSeconds + SwingSeconds + SwingBlendSeconds, out double[] swingSource);
            string fault = hookFault ?? grappleFault ?? swingFault;
            if (fault != null)
            {
                Debug.LogError("ChainRushAudio: " + fault, this);
                enabled = false;
                return;
            }
            cues = new AudioClip[CueTotal];
            for (int cue = 0; cue < cues.Length; cue++)
            {
                if ((RaceCue)cue == RaceCue.PoleHook) cues[cue] = MakeRecordedCue("CR pole hook", hook, poleHookRecording.frequency);
                else if ((RaceCue)cue == RaceCue.Grapple) cues[cue] = MakeRecordedCue("CR grapple", grapple, grappleRecording.frequency);
                else cues[cue] = MakeCue((RaceCue)cue);
            }
            score = MakeScore();
            wind = MakeWind();
            swingLoop = MakeSwingLoop(swingSource, swingRecording.frequency);
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
            AudioClip[] recordings = { poleHookRecording, grappleRecording, swingRecording };
            foreach (AudioClip recording in recordings)
                if (recording != null && recording.loadType != AudioClipLoadType.DecompressOnLoad)
                    Debug.LogError($"ChainRushAudio: {recording.name} must be imported as Decompress On Load to be read (it is {recording.loadType}).", this);
        }

        // Off since Awake: nothing is built, so cues, resets and mute do nothing. The first call says so (Awake said why).
        private bool IsOff()
        {
            if (initialized) return false;
            if (!toldOff)
            {
                Debug.LogError("ChainRushAudio: off since Awake (see the error above); cues, resets and mute do nothing.", this);
                toldOff = true;
            }
            return true;
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
            if (IsOff()) return;
            music.mute = effects.mute = ambience.mute = swing.mute = trackAmbience.mute = muted;
        }

        public void ResetAudio()
        {
            if (IsOff()) return;
            music.Stop(); effects.Stop(); ambience.Stop(); swing.Stop(); trackAmbience.Stop();
            swingGain = 0f; swing.volume = 0f;
            playing = false; paused = false;
        }

        public void PlayCue(RaceCue cue)
        {
            if (cue < RaceCue.Jump || (int)cue >= CueTotal) throw new ArgumentOutOfRangeException(nameof(cue), cue, "ChainRushAudio: no such cue.");
            if (IsOff()) return;
            if (game.IsPaused) return;
            effects.PlayOneShot(cues[(int)cue]);
        }

        public AudioClip GetCue(int index)
        {
            if (index < 0 || index >= cues.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return cues[index];
        }

        private static AudioClip Clip(string name, float[] samples, int rate = SampleRate)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Could not fill audio clip " + name);
            return clip;
        }

        private static AudioClip MakeCue(RaceCue cue)
        {
            if (cue == RaceCue.Boost) return MakeKartBoost();
            if (cue == RaceCue.BoostZone) return MakePassBy();
            if (cue == RaceCue.PoleHook || cue == RaceCue.Grapple) throw new ArgumentException("ChainRushAudio: " + cue + " is recorded, not synthesized.", nameof(cue));
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
                case RaceCue.Boost: return 2f;
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

        // A kart's turbo: the engine climbs from 72 to 150 Hz and the filter opening is what reads as "higher", so the weight
        // stays low (a 4 kHz version was heard as a bird, DECISIONS 2026-11-07).
        private static AudioClip MakeKartBoost()
        {
            var samples = new double[Mathf.RoundToInt(SampleRate * CueSeconds(RaceCue.Boost))];
            var random = new System.Random(4702 + (int)RaceCue.Boost);
            Oscillator first = new Oscillator(0.1343642441d), second = new Oscillator(0.8474337369d);
            Oscillator under = new Oscillator(0.7637746190d), over = new Oscillator(0.2550690257d);
            StateVariableFilter engine = new StateVariableFilter(), wind = new StateVariableFilter();
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                double rise = 1d - Math.Exp(-t / 0.32d);
                double hz = 72d + 78d * rise + 1.6d * Math.Sin(2d * Math.PI * 9d * t);
                double raw = first.Saw(hz) + second.Saw(hz * 1.007d) + 0.6d * under.Saw(hz * 0.5d) + 0.45d * over.Square(hz * 2.003d);
                double body = Math.Tanh(2.2d * engine.LowPass(raw * 0.4d, 320d + 2300d * rise * Math.Exp(-t * 0.45d), 1.5d));
                double air = wind.BandPass(random.NextDouble() * 2d - 1d, 850d + 1300d * rise, 1.1d);
                samples[i] = Math.Min(1d, t / 0.012d) * Math.Exp(-t * 0.6d) * (body + air * 0.45d);
            }
            var punch = new Oscillator(0d);
            for (int i = 0; i < SampleRate / 2; i++)
            {
                double t = (double)i / SampleRate;
                samples[i] += punch.Sine(48d + 92d * Math.Exp(-t / 0.03d)) * Math.Exp(-t / 0.1d) * 0.9d;
            }
            return Clip("CR kart boost", Leveled(samples, SampleRate));
        }

        // A kart going by: pitch and brightness swell for the first third, then fall away behind the player.
        private static AudioClip MakePassBy()
        {
            var samples = new double[Mathf.RoundToInt(SampleRate * CueSeconds(RaceCue.BoostZone))];
            var random = new System.Random(4702 + (int)RaceCue.BoostZone);
            Oscillator first = new Oscillator(0.7933400838d), second = new Oscillator(0.8219540423d);
            StateVariableFilter engine = new StateVariableFilter(), wind = new StateVariableFilter();
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                double climb = Math.Min(1d, t / 0.32d);
                double near = Math.Sin(Math.PI * Math.Min(1d, t / 0.75d));
                double hz = 110d + 220d * climb - 165d * Math.Max(0d, (t - 0.32d) / 0.73d);
                double body = Math.Tanh(1.5d * engine.LowPass((first.Saw(hz) + second.Saw(hz * 1.009d)) * 0.5d, 600d + 2800d * near, 1.6d));
                double air = wind.BandPass(random.NextDouble() * 2d - 1d, 800d + 2400d * near, 1.5d);
                samples[i] = Math.Min(1d, t / 0.02d) * Math.Exp(-t * 1.6d) * (body + air * 0.8d);
            }
            return Clip("CR boost zone pass-by", Leveled(samples, SampleRate));
        }

        // Edges, then the level the takes were compared at, so the game plays what was picked.
        private static float[] Leveled(double[] samples, int rate)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= Math.Min(1d, Math.Min(i / (rate * 0.0015d), (samples.Length - 1 - i) / (rate * 0.04d)));
            return AtLevel(samples, rate);
        }

        // The loudest 300 ms lands on one RMS, unless the peak would pass 0.92 first.
        private static float[] AtLevel(double[] samples, int rate)
        {
            int window = rate * 3 / 10;
            double energy = 0d, loudest = 0d, peak = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                energy += samples[i] * samples[i];
                if (i >= window) energy -= samples[i - window] * samples[i - window];
                loudest = Math.Max(loudest, energy);
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }
            double gain = Math.Min(0.21d / Math.Sqrt(loudest / window), 0.92d / peak);
            var leveled = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) leveled[i] = (float)(samples[i] * gain);
            return leveled;
        }

        // Mono samples of a recording, or why it cannot be used. Only Decompress On Load gives samples back, and the import
        // leaves preload off, so the data is asked for here.
        private static string ReadRecording(AudioClip recording, string field, float minimumSeconds, out double[] mono)
        {
            mono = null;
            if (recording == null) return field + " is empty. Connect it in the inspector.";
            if (recording.loadType != AudioClipLoadType.DecompressOnLoad)
                return $"{field} ({recording.name}) must be imported as Decompress On Load to be read (it is {recording.loadType}).";
            // One sample over the seconds covers the rounding where the swing loop cuts it into samples.
            if (recording.samples < Mathf.CeilToInt(minimumSeconds * recording.frequency) + 1)
                return $"{field} ({recording.name}) is {recording.length:F2} s; it needs {minimumSeconds:F2} s.";
            if (!recording.LoadAudioData()) return $"{field} ({recording.name}) could not be loaded.";
            var interleaved = new float[recording.samples * recording.channels];
            if (!recording.GetData(interleaved, 0)) return $"{field} ({recording.name}) could not be read.";
            var samples = new double[recording.samples];
            double peak = 0d;
            for (int i = 0; i < interleaved.Length; i++) samples[i / recording.channels] += interleaved[i] / (double)recording.channels;
            foreach (double sample in samples) peak = Math.Max(peak, Math.Abs(sample));
            if (peak <= 0d) return $"{field} ({recording.name}) is silent.";
            mono = samples;
            return null;
        }

        // The recordings open with up to 170 ms of room, which would play as input lag on a hook. ReadRecording made sure
        // there is a peak, so the search stops on it at the latest.
        private static AudioClip MakeRecordedCue(string name, double[] mono, int rate)
        {
            double peak = 0d;
            foreach (double sample in mono) peak = Math.Max(peak, Math.Abs(sample));
            int onset = 0;
            while (Math.Abs(mono[onset]) < peak * 0.05d) onset++;
            int start = Math.Max(0, onset - rate / 200);
            var trimmed = new double[mono.Length - start];
            Array.Copy(mono, start, trimmed, 0, trimmed.Length);
            return Clip(name, Leveled(trimmed, rate), rate);
        }

        // Band-limited (polyBLEP) oscillator: a plain saw at these pitches folds back into a metallic fizz.
        private sealed class Oscillator
        {
            private double phase;

            public Oscillator(double phase) { this.phase = phase; }

            public double Saw(double hz)
            {
                double step = Advance(hz);
                return 2d * phase - 1d - Blep(phase, step);
            }

            public double Square(double hz)
            {
                double step = Advance(hz);
                return (phase < 0.5d ? 1d : -1d) + Blep(phase, step) - Blep((phase + 0.5d) % 1d, step);
            }

            public double Sine(double hz)
            {
                Advance(hz);
                return Math.Sin(2d * Math.PI * phase);
            }

            private double Advance(double hz)
            {
                double step = hz / SampleRate;
                phase = (phase + step) % 1d;
                return step;
            }

            private static double Blep(double t, double step)
            {
                if (t < step) { double x = t / step; return x + x - x * x - 1d; }
                if (t > 1d - step) { double x = (t - 1d) / step; return x * x + x + x + 1d; }
                return 0d;
            }
        }

        // Topology-preserving state-variable filter: stays stable while its cutoff sweeps every sample.
        private sealed class StateVariableFilter
        {
            private double first;
            private double second;

            public double LowPass(double value, double hz, double q)
            {
                Run(value, hz, q, out double low, out _);
                return low;
            }

            public double BandPass(double value, double hz, double q)
            {
                Run(value, hz, q, out _, out double band);
                return band;
            }

            private void Run(double value, double hz, double q, out double low, out double band)
            {
                double g = Math.Tan(Math.PI * Math.Min(hz, SampleRate * 0.45d) / SampleRate);
                double a1 = 1d / (1d + g * (g + 1d / q));
                double a2 = g * a1;
                double a3 = g * a2;
                double v3 = value - second;
                band = a1 * first + a2 * v3;
                low = second + a2 * first + a3 * v3;
                first = 2d * band - first;
                second = 2d * low - second;
            }
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
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float edge = Mathf.Min(1f, t / 0.002f, (duration - t) / 0.06f);
                samples[i] = SoftClip(samples[i]) * edge;
            }
            return Clip("CR effect " + (int)cue, samples);
        }

        // Four seconds of the recording. What follows them fades in over their start, so the end runs straight into the start.
        private static AudioClip MakeSwingLoop(double[] source, int rate)
        {
            int from = Mathf.RoundToInt(SwingFromSeconds * rate), length = SwingSeconds * rate, blend = Mathf.RoundToInt(SwingBlendSeconds * rate);
            // ReadRecording already turned a short recording away; reaching this is a bug in that check.
            if (from + length + blend > source.Length) throw new InvalidOperationException("ChainRushAudio: the swing length check let a short recording through.");
            var samples = new double[length];
            Array.Copy(source, from, samples, 0, length);
            for (int i = 0; i < blend; i++)
            {
                double mix = (i + 0.5d) / blend * Math.PI / 2d;
                samples[i] = source[from + i] * Math.Sin(mix) + source[from + length + i] * Math.Cos(mix);
            }
            return Clip("Swing chain - recorded loop", AtLevel(samples, rate), rate);
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
