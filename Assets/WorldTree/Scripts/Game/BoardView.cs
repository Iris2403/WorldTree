using System;
using UnityEngine;
using UnityEngine.Pool;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Data;

namespace WorldTree.Game
{
    /// <summary>
    /// Renders the board as world-space sprites (architecture §7.1). One world unit = one cell;
    /// cell (0,0) is top-left. Views are projections of the model: they animate in response to
    /// <see cref="GameSession"/> events and can always be rebuilt with <see cref="RebuildAll"/>.
    /// Withered cells show the item under a blight tint; Sealed cells hide it under roots (GDD §3.1).
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const int CellOrder = 0;
        private const int HintOrder = 5;
        private const int HighlightOrder = 6;
        private const int ItemOrder = 10;
        private const int BlightOrder = 20;
        private const int DragOrder = 100;
        private const float MoveSeconds = 0.12f;
        private const float SpawnSeconds = 0.3f;
        private const float StatusInterval = 0.25f;

        private static readonly Color CellColor = new Color(0.17f, 0.23f, 0.2f);
        private static readonly Color BackdropColor = new Color(0.09f, 0.12f, 0.11f);
        private static readonly Color ItemLabelColor = new Color(0.08f, 0.1f, 0.12f);
        private static readonly Color BlightColor = new Color(0.36f, 0.28f, 0.42f, 0.72f);
        private static readonly Color SealColor = new Color(0.2f, 0.15f, 0.12f, 1f);
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.5f);

        private GameSession _session;
        private GameDatabase _database;
        private Tweener _tweener;
        private ItemView[] _views;
        private SpriteRenderer[] _covers;
        private SpriteRenderer[] _hints;
        private ObjectPool<ItemView> _pool;
        private Transform _cellRoot;
        private Transform _itemRoot;
        private SpriteRenderer _targetHighlight;
        private SpriteRenderer _selection;
        private int _width;
        private int _height;
        private float _nextStatus;

        public int Width => _width;
        public int Height => _height;

        public void Init(GameSession session, GameDatabase database, Tweener tweener)
        {
            Unsubscribe();
            _session = session;
            _database = database;
            _tweener = tweener;

            if (_pool == null)
            {
                _itemRoot = new GameObject("Items").transform;
                _itemRoot.SetParent(transform, false);
                _pool = new ObjectPool<ItemView>(
                    () => ItemView.Create(_itemRoot),
                    v => v.gameObject.SetActive(true),
                    v => { _tweener.Stop(v.transform); v.gameObject.SetActive(false); },
                    v => Destroy(v.gameObject),
                    collectionCheck: true, defaultCapacity: 64, maxSize: 256);
            }

            if (_cellRoot == null || _width != session.Board.Width || _height != session.Board.Height)
                BuildCells(session.Board.Width, session.Board.Height);

            _session.ItemMoved += OnMoved;
            _session.ItemsSwapped += OnSwapped;
            _session.ItemsMerged += OnMerged;
            _session.CellStateChanged += OnCellStateChanged;
            _session.ItemSpawned += OnSpawned;
            _session.ItemStored += OnRemoved;
            _session.ItemSold += OnSold;
            _session.ItemRetrieved += OnRetrieved;
            _session.SaleUndone += OnRestored;
            RebuildAll();
        }

        public Vector3 CellToWorld(GridPos p) =>
            transform.position + new Vector3(p.X - (_width - 1) * 0.5f, (_height - 1) * 0.5f - p.Y, 0f);

        public GridPos WorldToCell(Vector3 world)
        {
            var local = world - transform.position;
            return new GridPos(Mathf.RoundToInt(local.x + (_width - 1) * 0.5f), Mathf.RoundToInt((_height - 1) * 0.5f - local.y));
        }

        public ItemView GetView(GridPos p) => _session.Board.IsInside(p) ? _views[Index(p)] : null;

        public void RebuildAll()
        {
            if (_views != null)
                for (int i = 0; i < _views.Length; i++)
                    if (_views[i] != null) _pool.Release(_views[i]);

            _views = new ItemView[_width * _height];
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                var p = new GridPos(x, y);
                var item = _session.Board.GetItem(p);
                if (item != null)
                {
                    var view = Spawn(item);
                    view.transform.position = CellToWorld(p);
                    _views[Index(p)] = view;
                }
                ApplyCover(p);
            }
            ShowMergeHints(null);
            _nextStatus = 0f;
        }

        public void BeginDrag(GridPos p)
        {
            var view = GetView(p);
            if (view == null) return;
            view.SetSorting(DragOrder);
            view.transform.localScale = Vector3.one * (view.BaseScale * 1.15f);
            ShowMergeHints(_session.Board.GetItem(p)?.ItemId, p);
        }

        /// <summary>Returns a dragged view to normal size and sorting; call after the drop is resolved.</summary>
        public void EndDrag(ItemView view)
        {
            ShowMergeHints(null);
            if (view == null || !view.gameObject.activeSelf) return;
            view.SetSorting(ItemOrder);
            view.transform.localScale = Vector3.one * view.BaseScale;
        }

        public void SnapBack(GridPos p)
        {
            var view = GetView(p);
            if (view != null) _tweener.Move(view.transform, CellToWorld(p), MoveSeconds);
        }

        public void ShowTarget(GridPos? p)
        {
            bool show = p.HasValue && _session.Board.IsInside(p.Value);
            _targetHighlight.enabled = show;
            if (show) _targetHighlight.transform.position = CellToWorld(p.Value);
        }

        public void ShowSelection(GridPos? p)
        {
            bool show = p.HasValue && _session.Board.IsInside(p.Value);
            _selection.enabled = show;
            if (show) _selection.transform.position = CellToWorld(p.Value);
        }

        /// <summary>
        /// While dragging, marks every cell the item could merge into: Open items with the same ID, and
        /// Withered items with the same ID (cleansing merge). Max-level items have no targets.
        /// </summary>
        public void ShowMergeHints(string itemId, GridPos? exclude = null)
        {
            bool canMerge = itemId != null && !_session.Catalog.GetItem(itemId).IsMaxLevel;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                var p = new GridPos(x, y);
                bool hint = false;
                if (canMerge && (!exclude.HasValue || exclude.Value != p))
                {
                    var item = _session.Board.GetItem(p);
                    var state = _session.Board.GetState(p);
                    hint = item != null && item.ItemId == itemId && state != CellState.Sealed;
                }
                _hints[Index(p)].enabled = hint;
            }
        }

        private void Update()
        {
            if (_session == null || Time.unscaledTime < _nextStatus) return;
            _nextStatus = Time.unscaledTime + StatusInterval;
            var now = _session.UtcNow;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                var p = new GridPos(x, y);
                var view = _views[Index(p)];
                if (view == null || _session.Board.GetState(p) != CellState.Open) continue;
                var state = _session.GetGeneratorState(p);
                if (state == null) continue;
                bool waiting = state.Charges == 0 && state.RefreshEndsUtc.HasValue;
                view.SetGeneratorStatus(waiting ? HudView.FormatTime(state.RefreshEndsUtc.Value - now) : state.Charges.ToString(), waiting);
            }
        }

        private void OnMoved(GridPos from, GridPos to)
        {
            var view = Take(from);
            Put(to, view);
            if (view != null) _tweener.Move(view.transform, CellToWorld(to), MoveSeconds);
            _nextStatus = 0f;
        }

        private void OnSwapped(GridPos from, GridPos to)
        {
            var a = Take(from);
            var b = Take(to);
            Put(to, a);
            Put(from, b);
            if (a != null) _tweener.Move(a.transform, CellToWorld(to), MoveSeconds);
            if (b != null) _tweener.Move(b.transform, CellToWorld(from), MoveSeconds);
            _nextStatus = 0f;
        }

        private void OnMerged(GridPos from, GridPos to, ItemInstance result)
        {
            var source = Take(from);
            if (source != null) _pool.Release(source);
            var target = Take(to);
            if (target == null) target = _pool.Get();
            Bind(target, result);
            target.transform.position = CellToWorld(to);
            _tweener.Pop(target.transform, target.BaseScale);
            Put(to, target);
            _nextStatus = 0f;
        }

        private void OnCellStateChanged(GridPos p, CellState state)
        {
            ApplyCover(p);
            var view = GetView(p);
            if (view != null) _tweener.Pop(view.transform, view.BaseScale, 0.3f);
        }

        private void OnSpawned(GridPos generator, GridPos target, ItemInstance item)
        {
            var view = Spawn(item);
            view.transform.position = CellToWorld(generator);
            _tweener.Move(view.transform, CellToWorld(target), SpawnSeconds, arcHeight: 0.6f);
            Put(target, view);
            var generatorView = GetView(generator);
            if (generatorView != null) _tweener.Pop(generatorView.transform, generatorView.BaseScale, 0.15f);
            _nextStatus = 0f;
        }

        private void OnRemoved(GridPos p)
        {
            var view = Take(p);
            if (view != null) _pool.Release(view);
        }

        private void OnSold(GridPos p, int glimmer) => OnRemoved(p);

        private void OnRetrieved(GridPos p, ItemInstance item)
        {
            var view = Spawn(item);
            view.transform.position = CellToWorld(new GridPos(_width / 2, _height)); // just below the board
            _tweener.Move(view.transform, CellToWorld(p), SpawnSeconds, arcHeight: 0.4f);
            Put(p, view);
            _nextStatus = 0f;
        }

        private void OnRestored(GridPos p, ItemInstance item)
        {
            var view = Spawn(item);
            view.transform.position = CellToWorld(p);
            _tweener.Pop(view.transform, view.BaseScale);
            Put(p, view);
        }

        private void ApplyCover(GridPos p)
        {
            var state = _session.Board.GetState(p);
            var cover = _covers[Index(p)];
            cover.enabled = state != CellState.Open;
            cover.color = state == CellState.Sealed ? SealColor : BlightColor;
            // Sealed hides the item completely: the view stays, but under an opaque cover.
        }

        private ItemView Spawn(ItemInstance item)
        {
            var view = _pool.Get();
            Bind(view, item);
            return view;
        }

        private void Bind(ItemView view, ItemInstance item)
        {
            var def = _session.Catalog.GetItem(item.ItemId);
            Color color = Color.magenta;
            if (_database != null && _database.TryGetVisual(item.ItemId, out var chain, out _)) color = chain.Color;
            if (def.IsGenerator)
                // Generators are hollow frames over a dark cell, so their label must be light to stay readable.
                view.Bind(PlaceholderArt.Frame, color, "G" + def.Level, Color.Lerp(color, Color.white, 0.6f), 0.9f);
            else
                view.Bind(PlaceholderArt.Circle, Color.Lerp(color, Color.white, (def.Level - 1) * 0.06f), def.Level.ToString(), ItemLabelColor, 0.56f + 0.045f * def.Level);
            view.SetSorting(ItemOrder);
        }

        private ItemView Take(GridPos p)
        {
            int i = Index(p);
            var v = _views[i];
            _views[i] = null;
            return v;
        }

        private void Put(GridPos p, ItemView v)
        {
            int i = Index(p);
            if (_views[i] != null && _views[i] != v) _pool.Release(_views[i]); // defensive: never leak a view
            _views[i] = v;
        }

        private int Index(GridPos p) => p.Y * _width + p.X;

        private void BuildCells(int width, int height)
        {
            if (_cellRoot != null) Destroy(_cellRoot.gameObject);
            _width = width;
            _height = height;
            _cellRoot = new GameObject("Cells").transform;
            _cellRoot.SetParent(transform, false);
            _covers = new SpriteRenderer[width * height];
            _hints = new SpriteRenderer[width * height];

            var backdrop = NewSprite("Backdrop", _cellRoot, PlaceholderArt.Cell, BackdropColor, CellOrder - 1);
            backdrop.transform.localScale = new Vector3(width + 0.3f, height + 0.3f, 1f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new GridPos(x, y);
                var cell = NewSprite("Cell " + x + "," + y, _cellRoot, PlaceholderArt.Cell, CellColor, CellOrder);
                cell.transform.position = CellToWorld(p);
                cell.transform.localScale = Vector3.one * 0.94f;

                var cover = NewSprite("Cover " + x + "," + y, _cellRoot, PlaceholderArt.Cell, BlightColor, BlightOrder);
                cover.transform.position = CellToWorld(p);
                cover.transform.localScale = Vector3.one * 0.94f;
                cover.enabled = false;
                _covers[Index(p)] = cover;

                var hint = NewSprite("Hint " + x + "," + y, _cellRoot, PlaceholderArt.Frame, HintColor, BlightOrder + 1);
                hint.transform.position = CellToWorld(p);
                hint.transform.localScale = Vector3.one * 0.98f;
                hint.enabled = false;
                _hints[Index(p)] = hint;
            }

            _targetHighlight = NewSprite("Drop Target", _cellRoot, PlaceholderArt.Frame, new Color(1f, 0.92f, 0.4f, 0.9f), HighlightOrder);
            _targetHighlight.enabled = false;
            _selection = NewSprite("Selection", _cellRoot, PlaceholderArt.Frame, new Color(1f, 1f, 1f, 0.55f), HighlightOrder);
            _selection.enabled = false;
        }

        private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }

        private void Unsubscribe()
        {
            if (_session == null) return;
            _session.ItemMoved -= OnMoved;
            _session.ItemsSwapped -= OnSwapped;
            _session.ItemsMerged -= OnMerged;
            _session.CellStateChanged -= OnCellStateChanged;
            _session.ItemSpawned -= OnSpawned;
            _session.ItemStored -= OnRemoved;
            _session.ItemSold -= OnSold;
            _session.ItemRetrieved -= OnRetrieved;
            _session.SaleUndone -= OnRestored;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
