using UnityEngine;
using WorldTree.Core.Layout;

namespace WorldTree.Game
{
    /// <summary>
    /// Runs <see cref="BoardLayoutSolver"/> whenever the screen or safe area changes, applies the regions to the
    /// HUD and fits the board camera into the board rectangle (06-mobile-layout.md, roadmap D25 → Phase 6).
    /// </summary>
    public sealed class LayoutController : MonoBehaviour
    {
        private HudView _hud;
        private BoardView _board;
        private BoardCameraFitter _fitter;
        private Rect _lastSafe;
        private int _lastWidth;
        private int _lastHeight;

        public void Init(HudView hud, BoardView board, BoardCameraFitter fitter)
        {
            _hud = hud;
            _board = board;
            _fitter = fitter;
            _lastWidth = 0; // force a layout pass
        }

        private void LateUpdate()
        {
            if (_hud == null || _board == null || _board.Width == 0) return;
            var safe = Screen.safeArea;
            if (safe == _lastSafe && Screen.width == _lastWidth && Screen.height == _lastHeight) return;
            _lastSafe = safe;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            float scale = PixelsPerPoint();
            float widthPt = safe.width / scale, heightPt = safe.height / scale;
            var layout = BoardLayoutSolver.Solve(widthPt, heightPt, _board.Width, _board.Height);
            _hud.ApplyLayout(layout, widthPt, heightPt);

            // The board sits on top of the context bar, centred horizontally.
            float boardW = layout.BoardWidth * scale, boardH = layout.BoardHeight * scale;
            float bottom = safe.yMin + layout.ContextHeight * scale;
            _fitter.FitTo(new Rect(safe.center.x - boardW / 2f, bottom, boardW, boardH));
        }

        /// <summary>
        /// Pixels per point. Screen.dpi is unreliable on some Android devices (06-mobile-layout.md §6), so an
        /// implausible value falls back to treating the shorter screen side as 360 pt.
        /// </summary>
        private static float PixelsPerPoint()
        {
            float fallback = Mathf.Min(Screen.width, Screen.height) / 360f;
            float dpi = Screen.dpi;
            if (dpi < 100f || dpi > 800f) return Mathf.Max(0.5f, fallback);
            return dpi / 160f;
        }
    }
}
