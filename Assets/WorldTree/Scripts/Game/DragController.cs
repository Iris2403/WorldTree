using UnityEngine;
using UnityEngine.InputSystem;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;

namespace WorldTree.Game
{
    /// <summary>
    /// Pointer input for the board (architecture §7.3): tap vs drag threshold, lift on touch, follow,
    /// release. Uses two Input System actions created in code, so no .inputactions asset is needed;
    /// <c>&lt;Pointer&gt;</c> covers mouse and touch alike. All game changes go through <see cref="GameSession"/>.
    /// </summary>
    public sealed class DragController : MonoBehaviour
    {
        private const float DragThresholdPoints = 8f;
        private const float TouchLiftCells = 0.35f;

        private InputAction _point;
        private InputAction _press;
        private Camera _camera;
        private BoardView _board;
        private HudView _hud;
        private GameSession _session;

        private bool _pressing;
        private bool _dragging;
        private bool _hasOrigin;
        private GridPos _origin;
        private Vector2 _pressScreen;
        private ItemView _dragView;

        public void Init(GameSession session, BoardView board, HudView hud, Camera boardCamera)
        {
            CancelDrag();
            _session = session;
            _board = board;
            _hud = hud;
            _camera = boardCamera;
        }

        private void Awake()
        {
            _point = new InputAction("Point", InputActionType.PassThrough, "<Pointer>/position");
            _press = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
        }

        private void OnEnable()
        {
            _point.Enable();
            _press.Enable();
        }

        private void OnDisable()
        {
            CancelDrag();
            _point.Disable();
            _press.Disable();
        }

        private void OnDestroy()
        {
            _point.Dispose();
            _press.Dispose();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) CancelDrag();
        }

        private void Update()
        {
            if (_session == null || _camera == null) return;
            Vector2 screen = _point.ReadValue<Vector2>();

            if (_press.WasPressedThisFrame()) Begin(screen);
            if (_pressing && _press.IsPressed()) Hold(screen);
            if (_pressing && _press.WasReleasedThisFrame()) Release(screen);
        }

        private void Begin(Vector2 screen)
        {
            // Presses on HUD buttons or open panels belong to the UI, not the board.
            if (_hud.IsOverUi(screen)) return;
            _pressing = true;
            _dragging = false;
            _pressScreen = screen;
            var cell = _board.WorldToCell(ScreenToWorld(screen));
            _hasOrigin = _session.Board.IsInside(cell)
                         && _session.Board.GetItem(cell) != null
                         && _session.Board.GetState(cell) == CellState.Open;
            _origin = cell;
        }

        private void Hold(Vector2 screen)
        {
            if (!_hasOrigin) return;
            if (!_dragging)
            {
                if ((screen - _pressScreen).magnitude < DragThresholdPixels()) return;
                _dragging = true;
                _dragView = _board.GetView(_origin);
                _board.BeginDrag(_origin);
                _hud.Select(null);
            }

            var world = ScreenToWorld(screen);
            if (_dragView != null)
            {
                var lifted = world;
                if (Pointer.current is Touchscreen) lifted.y += TouchLiftCells; // keep the item visible above the finger
                _dragView.transform.position = lifted;
            }
            // The drop target is the cell under the finger, not under the lifted item (06-mobile-layout.md §5).
            _board.ShowTarget(_board.WorldToCell(world));
        }

        private void Release(Vector2 screen)
        {
            _pressing = false;
            _board.ShowTarget(null);

            if (_dragging)
            {
                _dragging = false;
                var orderId = _hud.OrderAt(screen);
                if (orderId.HasValue)
                {
                    _board.SnapBack(_origin);
                    _board.EndDrag(_dragView);
                    _dragView = null;
                    _hud.ContributeFromBoard(_origin, orderId.Value);
                    return;
                }
                if (_hud.IsOverSatchelButton(screen))
                {
                    _board.SnapBack(_origin);
                    _board.EndDrag(_dragView);
                    _dragView = null;
                    _hud.StoreFromBoard(_origin);
                    return;
                }
                var target = _board.WorldToCell(ScreenToWorld(screen));
                var decision = _session.DragItem(_origin, target);
                if (decision.Kind == MoveKind.Rejected || decision.Kind == MoveKind.NoOp) _board.SnapBack(_origin);
                _board.EndDrag(_dragView);
                _dragView = null;
                return;
            }

            var cell = _board.WorldToCell(ScreenToWorld(screen));
            if (!_session.Board.IsInside(cell) || _session.Board.GetItem(cell) == null)
            {
                _hud.Select(null);
                return;
            }
            _hud.Select(cell);
            // Tapping selects; on a generator it also produces an item (GDD §3.2).
            var def = _session.Catalog.GetItem(_session.Board.GetItem(cell).ItemId);
            if (!def.IsGenerator || _session.Board.GetState(cell) != CellState.Open) return;
            var outcome = _session.TapCell(cell);
            switch (outcome.Result)
            {
                case TapResult.BoardFull: _hud.ShowMessage("Board is full: merge or move items to make room"); break;
                case TapResult.NotEnoughSap: _hud.ShowMessage("Not enough Sap"); break;
                case TapResult.NoCharges: _hud.ShowMessage("Waiting for refresh"); break;
            }
        }

        private void CancelDrag()
        {
            if (_dragging && _board != null)
            {
                _board.SnapBack(_origin);
                _board.EndDrag(_dragView);
                _board.ShowTarget(null);
            }
            _dragging = false;
            _pressing = false;
            _dragView = null;
        }

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            var w = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            w.z = 0f;
            return w;
        }

        private static float DragThresholdPixels()
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            return DragThresholdPoints * dpi / 160f;
        }
    }
}
