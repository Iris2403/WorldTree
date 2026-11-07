using System;
using System.IO;
using UnityEngine;
using WorldTree.Core;
using WorldTree.Core.Common;
using WorldTree.Core.Save;

namespace WorldTree.Game
{
    /// <summary>
    /// Architecture §6.3: at the end of any frame in which the session changed, write one synchronous
    /// save without a disk flush. When the app goes to the background, write with a disk flush.
    /// </summary>
    public sealed class SaveDriver : MonoBehaviour
    {
        private const int FailuresBeforeWarning = 3;

        private GameSession _session;
        private AtomicSaveStore _store;
        private IClock _clock;
        private HudView _hud;
        private long _savedVersion;
        private bool _lastSaveFlushed = true;
        private int _failures;
        private float _retryAt;

        public void Init(GameSession session, AtomicSaveStore store, IClock clock, HudView hud)
        {
            _session = session;
            _store = store;
            _clock = clock;
            _hud = hud;
            _savedVersion = session.StateVersion;
            _failures = 0;
        }

        /// <summary>Writes the current state now (used after starting a new game).</summary>
        public void SaveNow() => Write(false);

        private void LateUpdate()
        {
            if (_session == null || _session.StateVersion == _savedVersion) return;
            if (_failures > 0 && Time.unscaledTime < _retryAt) return;
            Write(false);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Application.platform == RuntimePlatform.Android) Flush();
        }

        private void OnApplicationQuit() => Flush();

        private void Flush()
        {
            if (_session == null) return;
            if (_session.StateVersion != _savedVersion || !_lastSaveFlushed) Write(true);
        }

        private void Write(bool flushToDisk)
        {
            try
            {
                _store.Save(SaveMapper.Capture(_session), _clock.UtcNow, flushToDisk);
                _savedVersion = _session.StateVersion;
                _lastSaveFlushed = flushToDisk;
                _failures = 0;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _failures++;
                _retryAt = Time.unscaledTime + Mathf.Min(30f, Mathf.Pow(2f, _failures - 1));
                Debug.LogWarning("[WorldTree] Save failed (" + _failures + "): " + e.Message);
                if (_failures == FailuresBeforeWarning && _hud != null)
                    _hud.ShowMessage("Progress couldn't be saved. Check free storage.", 5f);
            }
        }
    }
}
