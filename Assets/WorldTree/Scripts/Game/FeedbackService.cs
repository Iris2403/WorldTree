using System.Collections.Generic;
using UnityEngine;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Core.Settings;
using WorldTree.Data;

namespace WorldTree.Game
{
    /// <summary>
    /// Maps game events to particles, sounds and haptics (architecture §7.2, GDD §3.6). Respects the
    /// Sound, Music, Haptics and Reduced motion settings. Particles are generated stand-ins (roadmap D42).
    /// </summary>
    public sealed class FeedbackService : MonoBehaviour
    {
        private const int PoolSize = 8;

        private GameSession _session;
        private GameDatabase _database;
        private BoardView _board;
        private AudioService _audio;
        private IHaptics _haptics;
        private readonly List<ParticleSystem> _bursts = new List<ParticleSystem>();
        private int _nextBurst;
        private Material _particleMaterial;

        public void Init(AudioService audio, IHaptics haptics)
        {
            _audio = audio;
            _haptics = haptics;
            HudView.ButtonClicked += () => _audio.Play(Sfx.Button);
        }

        public void SetSession(GameSession session, GameDatabase database, BoardView board)
        {
            if (_session != null) Unsubscribe();
            _session = session;
            _database = database;
            _board = board;
            _session.ItemsMerged += OnMerged;
            _session.CellStateChanged += OnCellStateChanged;
            _session.ItemSpawned += OnSpawned;
            _session.OrderCompleted += OnOrderCompleted;
            _session.LeveledUp += OnLeveledUp;
            _session.TaskRestored += OnTaskRestored;
            _session.AreaRestored += OnAreaRestored;
            _session.SettingChanged += OnSettingChanged;
            ApplyAllSettings();
            _audio.PlayAreaMusic(_session.Quest.AreaIndex);
        }

        /// <summary>A drop bounced back or an action was refused.</summary>
        public void Reject()
        {
            _audio.Play(Sfx.Reject);
            Haptic(HapticKind.Warning);
        }

        private void OnMerged(GridPos from, GridPos to, ItemInstance result)
        {
            var def = _session.Catalog.GetItem(result.ItemId);
            _audio.Play(Sfx.Merge, def.Level);
            Haptic(def.IsMaxLevel ? HapticKind.Medium : HapticKind.Light);
            Burst(_board.CellToWorld(to), ChainColor(result.ItemId), 10 + 2 * def.Level);
        }

        private void OnCellStateChanged(GridPos cell, CellState state)
        {
            if (state != CellState.Open) return;
            _audio.Play(Sfx.Cleanse);
            Haptic(HapticKind.Medium);
            Burst(_board.CellToWorld(cell), new Color(0.75f, 0.6f, 0.95f), 18);
        }

        private void OnSpawned(GridPos generator, GridPos target, ItemInstance item) => _audio.Play(Sfx.Spawn);

        private void OnOrderCompleted(Core.Orders.Order order)
        {
            _audio.Play(Sfx.Reward);
            Haptic(HapticKind.Success);
        }

        private void OnLeveledUp(int level)
        {
            _audio.Play(Sfx.LevelUp);
            Haptic(HapticKind.Success);
        }

        private void OnTaskRestored(Core.Story.AreaDef area, Core.Story.RestorationTask task)
        {
            _audio.Play(Sfx.Cleanse);
            Haptic(HapticKind.Medium);
        }

        private void OnAreaRestored(Core.Story.AreaDef area)
        {
            _audio.Play(Sfx.LevelUp);
            Haptic(HapticKind.Success);
            _audio.PlayAreaMusic(_session.Quest.AreaIndex);
        }

        private void OnSettingChanged(SettingKind kind, bool value) => ApplyAllSettings();

        private void ApplyAllSettings()
        {
            var s = _session.Settings;
            _audio.SoundOn = s.Sound;
            if (_audio.MusicOn != s.Music)
            {
                _audio.SetMusicOn(s.Music);
                if (s.Music) _audio.PlayAreaMusic(_session.Quest.AreaIndex);
            }
            Tweener.ReducedMotion = s.ReducedMotion;
        }

        private void Haptic(HapticKind kind)
        {
            if (_session != null && _session.Settings.Haptics) _haptics.Play(kind);
        }

        private Color ChainColor(string itemId) =>
            _database != null && _database.TryGetVisual(itemId, out var chain, out _) ? chain.Color : Color.white;

        private void Burst(Vector3 position, Color color, int count)
        {
            if (_session.Settings.ReducedMotion) return;
            var ps = NextBurst();
            ps.transform.position = position;
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.6f));
            ps.Emit(count);
        }

        private ParticleSystem NextBurst()
        {
            if (_bursts.Count < PoolSize)
            {
                var ps = CreateBurst();
                _bursts.Add(ps);
                return ps;
            }
            var next = _bursts[_nextBurst];
            _nextBurst = (_nextBurst + 1) % PoolSize;
            return next;
        }

        private ParticleSystem CreateBurst()
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.gravityModifier = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var emission = ps.emission;
            emission.enabled = false; // bursts are emitted manually with Emit()
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = ParticleMaterial();
            renderer.sortingOrder = 150;
            return ps;
        }

        private Material ParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;
            // URP particle shader first; fall back to the built-in sprite shader. Builds may strip shaders that are
            // only found by name: verified in Phase 7 (add them to Always Included Shaders if needed).
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _particleMaterial = new Material(shader) { mainTexture = PlaceholderArt.Dot.texture };
            return _particleMaterial;
        }

        private void Unsubscribe()
        {
            _session.ItemsMerged -= OnMerged;
            _session.CellStateChanged -= OnCellStateChanged;
            _session.ItemSpawned -= OnSpawned;
            _session.OrderCompleted -= OnOrderCompleted;
            _session.LeveledUp -= OnLeveledUp;
            _session.TaskRestored -= OnTaskRestored;
            _session.AreaRestored -= OnAreaRestored;
            _session.SettingChanged -= OnSettingChanged;
        }

        private void OnDestroy()
        {
            if (_session != null) Unsubscribe();
        }
    }
}
