using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Phase 1 layout (roadmap D25): fits the board into the portrait safe area between the HUD bar
    /// and a bottom margin, centred horizontally. The multi-tier layout of 06-mobile-layout.md is Phase 6.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCameraFitter : MonoBehaviour
    {
        /// <summary>Share of the safe-area height reserved for the HUD bar at the top. Matches <see cref="HudView"/>.</summary>
        public const float TopReserved = 0.14f;

        /// <summary>Share of the safe-area height kept at the bottom for the action bar and messages.</summary>
        public const float BottomReserved = 0.12f;

        private const float WorldMargin = 0.4f;

        private Camera _camera;
        private BoardView _board;
        private int _lastWidth;
        private int _lastHeight;
        private Rect _lastSafeArea;

        public void Init(BoardView board)
        {
            _board = board;
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _lastWidth = 0; // force a refit
        }

        private void LateUpdate()
        {
            if (_board == null) return;
            var safe = Screen.safeArea;
            if (Screen.width == _lastWidth && Screen.height == _lastHeight && safe == _lastSafeArea) return;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastSafeArea = safe;
            Fit(safe);
        }

        private void Fit(Rect safe)
        {
            float availableHeight = safe.height * (1f - TopReserved - BottomReserved);
            float boardWidth = _board.Width + WorldMargin;
            float boardHeight = _board.Height + WorldMargin;

            // Pixels per world unit: the largest scale at which the board fits both ways.
            float ppu = Mathf.Min(safe.width / boardWidth, availableHeight / boardHeight);
            _camera.orthographicSize = Screen.height / (2f * ppu);

            // Put the board centre at the centre of the available region.
            float targetX = safe.center.x;
            float targetY = safe.yMin + safe.height * BottomReserved + availableHeight * 0.5f;
            var boardCentre = _board.transform.position;
            transform.position = new Vector3(
                boardCentre.x + (Screen.width * 0.5f - targetX) / ppu,
                boardCentre.y + (Screen.height * 0.5f - targetY) / ppu,
                -10f);
        }
    }
}
