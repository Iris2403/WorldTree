using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Synthesized stand-in sounds and music (roadmap D42), generated at startup so the project has no
    /// unlicensed audio. Organic-sounding enough to judge timing and feel; replaced by real recordings
    /// via the AudioService clip slots (docs/art/asset-spec.md).
    /// </summary>
    public static class ProceduralAudio
    {
        private const int Rate = 22050;

        /// <summary>A soft bell-like chime: a few harmonics with an exponential decay.</summary>
        public static AudioClip Chime(string name, float frequency, float seconds = 0.6f, float volume = 0.5f)
        {
            return Build(name, seconds, t =>
            {
                float env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 200f);
                return volume * env * (Mathf.Sin(2f * Mathf.PI * frequency * t)
                                       + 0.35f * Mathf.Sin(2f * Mathf.PI * frequency * 2f * t)
                                       + 0.12f * Mathf.Sin(2f * Mathf.PI * frequency * 3.01f * t));
            });
        }

        /// <summary>A short, soft pop with a falling pitch (spawns, moves).</summary>
        public static AudioClip Pop(string name, float start = 520f, float end = 180f)
        {
            return Build(name, 0.12f, t =>
            {
                float f = Mathf.Lerp(start, end, t / 0.12f);
                return 0.4f * Mathf.Exp(-t * 30f) * Mathf.Sin(2f * Mathf.PI * f * t);
            });
        }

        /// <summary>A rich bell with inharmonic partials (cleansing the blight).</summary>
        public static AudioClip Bell(string name, float frequency)
        {
            return Build(name, 1.4f, t =>
            {
                float env = Mathf.Exp(-t * 2.5f) * Mathf.Clamp01(t * 300f);
                return 0.35f * env * (Mathf.Sin(2f * Mathf.PI * frequency * t)
                                      + 0.5f * Mathf.Sin(2f * Mathf.PI * frequency * 2.76f * t)
                                      + 0.25f * Mathf.Sin(2f * Mathf.PI * frequency * 5.4f * t));
            });
        }

        /// <summary>Two quick rising chimes (order delivered, reward).</summary>
        public static AudioClip Coin(string name) => Sequence(name, 0.09f, 988f, 1319f);

        /// <summary>A rising arpeggio (level-up, area restored).</summary>
        public static AudioClip Fanfare(string name) => Sequence(name, 0.12f, 523f, 659f, 784f, 1047f);

        /// <summary>A low, short thud (rejected drop, not enough Sap).</summary>
        public static AudioClip Thud(string name)
        {
            return Build(name, 0.18f, t => 0.45f * Mathf.Exp(-t * 22f) * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 70f, t / 0.18f) * t));
        }

        /// <summary>
        /// A calm looping pad: slowly alternating chords with a gentle shimmer. <paramref name="chords"/> are root
        /// frequencies; each plays a major or minor triad for half the loop.
        /// </summary>
        public static AudioClip AmbientLoop(string name, float[] chords, bool minor, float seconds = 16f)
        {
            float half = seconds / chords.Length;
            return Build(name, seconds, t =>
            {
                int i = Mathf.Min(chords.Length - 1, (int)(t / half));
                float local = t - i * half;
                // Crossfade at chord boundaries so the loop stays smooth.
                float fade = Mathf.Clamp01(local / 0.8f) * Mathf.Clamp01((half - local) / 0.8f);
                float root = chords[i];
                float third = root * (minor ? 1.1892f : 1.2599f), fifth = root * 1.4983f;
                float lfo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                float pad = Mathf.Sin(2f * Mathf.PI * root * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * third * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * fifth * t);
                float shimmer = 0.15f * Mathf.Sin(2f * Mathf.PI * root * 4f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.5f * t));
                return 0.08f * fade * lfo * (pad + shimmer);
            });
        }

        private static AudioClip Sequence(string name, float step, params float[] notes)
        {
            float length = step * notes.Length + 0.4f;
            return Build(name, length, t =>
            {
                float sum = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float local = t - n * step;
                    if (local < 0f) continue;
                    sum += Mathf.Exp(-local * 8f) * Mathf.Clamp01(local * 200f) * Mathf.Sin(2f * Mathf.PI * notes[n] * local);
                }
                return 0.3f * sum;
            });
        }

        private delegate float Wave(float t);

        private static AudioClip Build(string name, float seconds, Wave wave)
        {
            int samples = Mathf.CeilToInt(seconds * Rate);
            var data = new float[samples];
            for (int i = 0; i < samples; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, samples, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
