using System.Collections.Generic;
using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Music per area and pooled sound effects (GDD §10 audio). Merge chimes rise in pitch with the item
    /// level. Respects the Music and Sound settings. Real clips can be assigned in the Inspector; any slot
    /// left empty uses the synthesized stand-in.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const int Voices = 6;
        private const float MusicVolume = 0.6f;
        private const float CrossfadeSeconds = 2f;

        [Tooltip("Optional real music per area, in area order. Empty slots use generated stand-ins.")]
        [SerializeField] private List<AudioClip> areaMusic = new List<AudioClip>();

        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly List<AudioClip> _generatedMusic = new List<AudioClip>();
        private AudioSource[] _voices;
        private AudioSource _musicA;
        private AudioSource _musicB;
        private int _nextVoice;
        private int _musicIndex = -1;
        private float _fadeStart = -1f;

        public bool MusicOn { get; private set; } = true;
        public bool SoundOn { get; set; } = true;

        private void Awake()
        {
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) _voices[i] = NewSource(false);
            _musicA = NewSource(true);
            _musicB = NewSource(true);

            _clips[Sfx.Merge] = ProceduralAudio.Chime("sfx.merge", 523f);
            _clips[Sfx.Spawn] = ProceduralAudio.Pop("sfx.spawn");
            _clips[Sfx.Cleanse] = ProceduralAudio.Bell("sfx.cleanse", 392f);
            _clips[Sfx.Reward] = ProceduralAudio.Coin("sfx.reward");
            _clips[Sfx.LevelUp] = ProceduralAudio.Fanfare("sfx.levelup");
            _clips[Sfx.Reject] = ProceduralAudio.Thud("sfx.reject");
            _clips[Sfx.Button] = ProceduralAudio.Pop("sfx.button", 900f, 600f);

            // Stand-in music: Glade bright (C, F major), Village warm (A minor, F major).
            _generatedMusic.Add(ProceduralAudio.AmbientLoop("music.glade", new[] { 130.8f, 174.6f }, false));
            _generatedMusic.Add(ProceduralAudio.AmbientLoop("music.village", new[] { 110f, 87.3f }, true));
        }

        /// <param name="level">For merges: the new item's level; each level raises the chime by two semitones.</param>
        public void Play(Sfx sfx, int level = 1)
        {
            if (!SoundOn || !_clips.TryGetValue(sfx, out var clip)) return;
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            voice.pitch = sfx == Sfx.Merge ? Mathf.Pow(2f, 2f * (level - 2) / 12f) : 1f;
            voice.PlayOneShot(clip, 0.8f);
        }

        public void SetMusicOn(bool on)
        {
            MusicOn = on;
            if (!on)
            {
                _musicA.Stop();
                _musicB.Stop();
                _musicIndex = -1;
            }
        }

        /// <summary>Crossfades to the music of the given area (the last available track for later areas).</summary>
        public void PlayAreaMusic(int areaIndex)
        {
            if (!MusicOn) return;
            int count = Mathf.Max(areaMusic.Count, _generatedMusic.Count);
            int index = Mathf.Clamp(areaIndex, 0, count - 1);
            if (index == _musicIndex) return;
            _musicIndex = index;
            var clip = index < areaMusic.Count && areaMusic[index] != null ? areaMusic[index] : _generatedMusic[Mathf.Min(index, _generatedMusic.Count - 1)];

            var tmp = _musicA;
            _musicA = _musicB;
            _musicB = tmp;
            _musicA.clip = clip;
            _musicA.volume = 0f;
            _musicA.Play();
            _fadeStart = Time.unscaledTime;
        }

        private void Update()
        {
            if (_fadeStart < 0f) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _fadeStart) / CrossfadeSeconds);
            _musicA.volume = MusicVolume * t;
            _musicB.volume = MusicVolume * (1f - t);
            if (t < 1f) return;
            _musicB.Stop();
            _fadeStart = -1f;
        }

        private AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
