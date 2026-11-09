using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Points the orthographic board camera so the board fills a given screen rectangle, which
    /// <see cref="LayoutController"/> computes from the layout solver (06-mobile-layout.md §3).
    /// The constants are only the defaults used before the first layout pass.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCameraFitter : MonoBehaviour
    {
        /// <summary>Default share of the safe-area height for the top bar.</summary>
        public const float TopBarShare = 0.13f;

        /// <summary>Default share of the safe-area height for the orders strip.</summary>
        public const float OrdersShare = 0.15f;

        public const float TopReserved = TopBarShare + OrdersShare;

        /// <summary>Default share of the safe-area height for the context bar and messages.</summary>
        public const float BottomReserved = 0.12f;

        private Camera _camera;
        private BoardView _board;

        public void Init(BoardView board)
        {
            _board = board;
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        /// <summary>Fits the board's cells into <paramref name="screenRect"/> (pixels, origin bottom-left).</summary>
        public void FitTo(Rect screenRect)
        {
            if (_board == null || screenRect.width <= 0f || screenRect.height <= 0f) return;
            float ppu = Mathf.Min(screenRect.width / _board.Width, screenRect.height / _board.Height);
            _camera.orthographicSize = Screen.height / (2f * ppu);
            var centre = _board.transform.position;
            transform.position = new Vector3(
                centre.x + (Screen.width * 0.5f - screenRect.center.x) / ppu,
                centre.y + (Screen.height * 0.5f - screenRect.center.y) / ppu,
                -10f);
        }
    }
}
