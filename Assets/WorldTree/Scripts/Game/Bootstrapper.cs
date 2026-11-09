using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using WorldTree.Core;
using WorldTree.Core.Common;
using WorldTree.Core.Content;
using WorldTree.Core.Energy;
using WorldTree.Core.Items;
using WorldTree.Core.Save;
using WorldTree.Data;

namespace WorldTree.Game
{
    /// <summary>
    /// The single composition root (architecture rule 6). Builds Core definitions from the
    /// <see cref="GameDatabase"/>, loads or starts a game, and wires the views by hand.
    /// The scene only needs a camera and this component; everything else is created at runtime.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private GameDatabase database;
        [SerializeField] private Camera boardCamera;

        private IClock _clock;
        private Catalog _catalog;
        private BoardLayout _layout;
        private EnergyConfig _energy;
        private Core.Story.StoryCatalog _story;
        private AtomicSaveStore _store;

        private Tweener _tweener;
        private BoardView _board;
        private DragController _drag;
        private HudView _hud;
        private SaveDriver _saveDriver;
        private DebugOverlay _debug;
        private FeedbackService _feedback;

        public GameSession Session { get; private set; }

        /// <summary>
        /// Debug: makes every rewarded "ad" complete instantly, so ad rewards can be tested before the
        /// real ad service exists (Phase 8). Off by default: with no ad service, ad buttons stay hidden.
        /// </summary>
        public bool FakeAdsEnabled
        {
            get => _hud != null && _hud.Ads is FakeAdService;
            set { if (_hud != null) _hud.Ads = value ? (IAdService)new FakeAdService() : new NoAdService(); }
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            // Fallback: the setup menu keeps the database in a Resources folder, so a missing
            // Inspector reference never stops the prototype from starting.
            if (database == null) database = Resources.Load<GameDatabase>(GameDatabase.ResourcesName);
            if (database == null)
            {
                Debug.LogError("[WorldTree] No GameDatabase found. Use the menu bar: WorldTree → Setup → Build Prototype Scene.");
                enabled = false;
                return;
            }
            if (boardCamera == null) boardCamera = Camera.main;

            _clock = new SystemClock();
            try
            {
                _catalog = database.BuildCatalog();
                _layout = database.BuildLayout();
                _energy = database.BuildEnergyConfig();
                _story = database.BuildStory();
                if (_story != null)
                    foreach (var error in _story.Validate(_catalog)) Debug.LogWarning("[WorldTree] Story: " + error);
            }
            catch (Exception e)
            {
                Debug.LogError("[WorldTree] Invalid game data: " + e.Message);
                enabled = false;
                return;
            }

            _store = new AtomicSaveStore(new PhysicalFileSystem(), Path.Combine(Application.persistentDataPath, "save"));
            var load = _store.Load(_clock.UtcNow);
            var session = TryRestore(load) ?? NewGame();

            foreach (var error in BoardLayoutValidator.Validate(_layout, _catalog))
                Debug.LogWarning("[WorldTree] Starting layout: " + error);

            // uGUI buttons need an EventSystem; the Input System's UI module drives it.
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            _tweener = gameObject.AddComponent<Tweener>();
            _board = new GameObject("Board").AddComponent<BoardView>();
            _hud = HudView.Create(transform);
            _drag = gameObject.AddComponent<DragController>();
            _saveDriver = gameObject.AddComponent<SaveDriver>();
            _debug = gameObject.AddComponent<DebugOverlay>();
            var audio = gameObject.AddComponent<AudioService>();
            _feedback = gameObject.AddComponent<FeedbackService>();
            _feedback.Init(audio, Haptics.Create());
            // Explicit null check: '??' bypasses Unity's overloaded null test for destroyed/missing objects.
            var fitter = boardCamera.GetComponent<BoardCameraFitter>();
            if (fitter == null) fitter = boardCamera.gameObject.AddComponent<BoardCameraFitter>();

            StartSession(session);
            fitter.Init(_board);
            _hud.gameObject.AddComponent<LayoutController>().Init(_hud, _board, fitter);

            if (load.RestoreFailed) _hud.ShowMessage("Your save couldn't be restored, so a new game was started.", 6f);
        }

        /// <summary>Debug: discards the current game and starts a new one.</summary>
        public void ResetGame()
        {
            StartSession(NewGame());
            _saveDriver.SaveNow();
        }

        private void StartSession(GameSession session)
        {
            Session = session;
            _board.Init(session, database, _tweener);
            _hud.SetSession(session, database, _board);
            _feedback.SetSession(session, database, _board);
            _drag.Feedback = _feedback;
            _drag.Init(session, _board, _hud, boardCamera);
            _saveDriver.Init(session, _store, _clock, _hud);
            _debug.Init(session, this);
        }

        private GameSession TryRestore(LoadResult load)
        {
            if (load.Data == null) return null;
            try
            {
                var warnings = new List<string>();
                var session = SaveMapper.Restore(load.Data, _catalog, _energy, _clock, warnings, _story);
                foreach (var w in warnings) Debug.LogWarning("[WorldTree] Save: " + w);
                if (load.Source != LoadSource.Main) Debug.Log("[WorldTree] Save recovered from " + load.Source);
                return session;
            }
            catch (FormatException e)
            {
                Debug.LogError("[WorldTree] Save data unusable, starting a new game: " + e.Message);
                return null;
            }
        }

        private GameSession NewGame() =>
            GameSession.NewGame(_catalog, _layout, _energy, _clock, unchecked((uint)DateTime.UtcNow.Ticks), _story);
    }
}
