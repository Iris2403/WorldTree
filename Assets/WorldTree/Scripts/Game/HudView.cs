using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTree.Core;
using WorldTree.Core.Board;
using WorldTree.Core.Common;
using WorldTree.Data;

namespace WorldTree.Game
{
    /// <summary>
    /// Graybox HUD (uGUI + TextMeshPro, built in code). Top bar: Sap, Glimmer and the selected item.
    /// Bottom bar: Sell / Store / Undo for the selection, plus Satchel and Codex. Generator wording
    /// follows GDD §4.1: "refresh", never "recharge". Final UI art arrives in Phase 6.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private static readonly Color PanelColor = new Color(0.07f, 0.1f, 0.09f, 0.94f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.42f, 0.3f, 1f);
        private static readonly Color TextColor = new Color(0.95f, 0.93f, 0.85f);

        private GameSession _session;
        private GameDatabase _database;
        private BoardView _board;

        private RectTransform _safeArea;
        private TextMeshProUGUI _sap;
        private TextMeshProUGUI _glimmer;
        private TextMeshProUGUI _selection;
        private TextMeshProUGUI _message;
        private RectTransform _bottomBar;
        private Button _sellButton;
        private Button _storeButton;
        private Button _undoButton;
        private Button _satchelButton;
        private RectTransform _satchelPanel;
        private Button[] _slotButtons;
        private RectTransform _codexPanel;
        private TextMeshProUGUI _codexText;

        private GridPos? _selected;
        private float _messageUntil;
        private float _nextRefresh;
        private Rect _appliedSafeArea;

        /// <summary>True while the Satchel or Codex panel is open; the board ignores input then.</summary>
        public bool IsPanelOpen => _satchelPanel.gameObject.activeSelf || _codexPanel.gameObject.activeSelf;

        public static HudView Create(Transform parent)
        {
            var root = new GameObject("HUD", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var hud = root.AddComponent<HudView>();
            hud._safeArea = NewRect("SafeArea", root.transform, Vector2.zero, Vector2.one);

            // Top bar
            var bar = NewRect("TopBar", hud._safeArea, new Vector2(0f, 1f - BoardCameraFitter.TopReserved), Vector2.one);
            AddImage(bar, PanelColor, false);
            hud._sap = NewText("Sap", bar, new Vector2(0.03f, 0.5f), new Vector2(0.62f, 0.95f), 50, TextAlignmentOptions.Left);
            hud._glimmer = NewText("Glimmer", bar, new Vector2(0.62f, 0.5f), new Vector2(0.97f, 0.95f), 50, TextAlignmentOptions.Right);
            hud._selection = NewText("Selection", bar, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.5f), 38, TextAlignmentOptions.Left);

            // Bottom bar
            hud._bottomBar = NewRect("BottomBar", hud._safeArea, Vector2.zero, new Vector2(1f, BoardCameraFitter.BottomReserved * 0.82f));
            AddImage(hud._bottomBar, PanelColor, true);
            var row = hud._bottomBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 12, 12);
            row.spacing = 12;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            hud._sellButton = NewButton("Sell", hud._bottomBar, "Sell", hud.OnSell);
            hud._storeButton = NewButton("Store", hud._bottomBar, "Store", hud.OnStore);
            hud._undoButton = NewButton("Undo", hud._bottomBar, "Undo", hud.OnUndo);
            hud._satchelButton = NewButton("Satchel", hud._bottomBar, "Satchel", hud.ToggleSatchel);
            NewButton("Codex", hud._bottomBar, "Codex", hud.ToggleCodex);

            hud._message = NewText("Message", hud._safeArea,
                new Vector2(0.04f, BoardCameraFitter.BottomReserved * 0.84f), new Vector2(0.96f, BoardCameraFitter.BottomReserved * 1.25f),
                38, TextAlignmentOptions.Center);
            hud._message.text = string.Empty;

            // Satchel panel
            hud._satchelPanel = NewRect("SatchelPanel", hud._safeArea, new Vector2(0.06f, 0.3f), new Vector2(0.94f, 0.55f));
            AddImage(hud._satchelPanel, PanelColor, true);
            NewText("Title", hud._satchelPanel, new Vector2(0.04f, 0.72f), new Vector2(0.75f, 0.96f), 44, TextAlignmentOptions.Left).text =
                "Satchel: tap an item to put it back";
            var close = NewButton("Close", hud._satchelPanel, "Close", hud.ToggleSatchel);
            SetAnchors((RectTransform)close.transform, new Vector2(0.77f, 0.74f), new Vector2(0.97f, 0.95f));
            var slots = NewRect("Slots", hud._satchelPanel, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.68f));
            var slotRow = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
            slotRow.spacing = 14;
            slotRow.childControlWidth = slotRow.childControlHeight = true;
            slotRow.childForceExpandWidth = slotRow.childForceExpandHeight = true;
            hud._slotButtons = new Button[Core.Economy.EconomyRules.StartingSatchelSlots];
            for (int i = 0; i < hud._slotButtons.Length; i++)
            {
                int slot = i;
                hud._slotButtons[i] = NewButton("Slot " + i, slots, "—", () => hud.OnRetrieve(slot));
                hud._slotButtons[i].GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
            }
            hud._satchelPanel.gameObject.SetActive(false);

            // Codex panel
            hud._codexPanel = NewRect("CodexPanel", hud._safeArea, new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.85f));
            AddImage(hud._codexPanel, PanelColor, true);
            var codexClose = NewButton("Close", hud._codexPanel, "Close", hud.ToggleCodex);
            SetAnchors((RectTransform)codexClose.transform, new Vector2(0.77f, 0.92f), new Vector2(0.97f, 0.985f));
            hud._codexText = NewText("Entries", hud._codexPanel, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.9f), 34, TextAlignmentOptions.TopLeft);
            hud._codexPanel.gameObject.SetActive(false);
            return hud;
        }

        public void SetSession(GameSession session, GameDatabase database, BoardView board)
        {
            if (_session != null) _session.ItemDiscovered -= OnDiscovered;
            _session = session;
            _database = database;
            _board = board;
            _session.ItemDiscovered += OnDiscovered;
            Select(null);
            CloseAllPanels();
        }

        public void Select(GridPos? cell)
        {
            _selected = cell;
            if (_board != null) _board.ShowSelection(cell);
            _nextRefresh = 0f;
        }

        public void ShowMessage(string text, float seconds = 2.5f)
        {
            _message.text = text;
            _messageUntil = Time.unscaledTime + seconds;
        }

        /// <summary>True if the screen point is over HUD controls, so the board must not react to it.</summary>
        public bool IsOverUi(Vector2 screen) =>
            IsPanelOpen || Contains(_bottomBar, screen);

        /// <summary>True if the screen point is over the Satchel button (drop target for storing items).</summary>
        public bool IsOverSatchelButton(Vector2 screen) => Contains((RectTransform)_satchelButton.transform, screen);

        public static string FormatTime(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            int total = (int)Math.Ceiling(t.TotalSeconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        private void Update()
        {
            ApplySafeArea();
            if (_message.text.Length > 0 && Time.unscaledTime > _messageUntil) _message.text = string.Empty;
            if (_session == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            var now = _session.UtcNow;
            int sap = _session.Sap;
            var next = _session.Energy.NextTickUtc;
            _sap.text = "Sap " + sap + " / " + _session.Energy.Config.Cap + (next.HasValue ? "   +1 in " + FormatTime(next.Value - now) : "   Full");
            _glimmer.text = "Glimmer " + _session.Wallet.Glimmer;
            _selection.text = DescribeSelection(now);
            RefreshButtons();
            if (_satchelPanel.gameObject.activeSelf) RefreshSatchel(now);
        }

        private void RefreshButtons()
        {
            ItemInstance item = null;
            bool open = false;
            if (_selected.HasValue && _session.Board.IsInside(_selected.Value))
            {
                item = _session.Board.GetItem(_selected.Value);
                open = _session.Board.GetState(_selected.Value) == CellState.Open;
            }
            bool sellable = item != null && open && !_session.Catalog.GetItem(item.ItemId).IsGenerator;
            SetButton(_sellButton, sellable, sellable ? "Sell +" + _session.Catalog.GetItem(item.ItemId).SellValue : null);
            SetButton(_storeButton, item != null && open, null);
            double undo = _session.SellUndoSecondsLeft;
            SetButton(_undoButton, undo > 0, "Undo (" + Math.Ceiling(undo) + ")");
            SetButton(_satchelButton, true, "Satchel " + _session.Satchel.Count + "/" + _session.Satchel.Capacity);
        }

        private string DescribeSelection(DateTime now)
        {
            if (!_selected.HasValue) return "Tap a generator to grow items. Drag matching items together.";
            var p = _selected.Value;
            var item = _session.Board.GetItem(p);
            if (item == null) return string.Empty;
            var state = _session.Board.GetState(p);
            if (state == CellState.Sealed) return "Sealed by thorny roots. Cleanse the blight next to it to reveal it.";
            var def = _session.Catalog.GetItem(item.ItemId);
            string name = DisplayName(item.ItemId);
            if (state == CellState.Withered) return name + " (Withered). Merge beside it, or drop a matching item on it.";

            if (!def.IsGenerator) return name + " · Lv " + def.Level;
            var g = _session.GetGeneratorState(p);
            string charges = g.Charges + " / " + def.Generator.Capacity;
            if (g.RefreshEndsUtc == null) return name + " Lv " + def.Level + " · " + charges + " · Ready";
            string prefix = g.Charges == 0 ? "Waiting · full in " : "Full refresh in ";
            return name + " Lv " + def.Level + " · " + charges + " · " + prefix + FormatTime(g.RefreshEndsUtc.Value - now);
        }

        private string DisplayName(string itemId)
        {
            if (_database != null && _database.TryGetVisual(itemId, out _, out var level) && !string.IsNullOrEmpty(level.displayName))
                return level.displayName;
            return itemId;
        }

        private void OnSell()
        {
            if (!_selected.HasValue) return;
            var result = _session.SellItem(_selected.Value);
            if (result == SellResult.NotSellable) ShowMessage("Generators can't be sold");
            else if (result == SellResult.Sold) Select(null);
            _nextRefresh = 0f;
        }

        private void OnStore()
        {
            if (!_selected.HasValue) return;
            StoreFromBoard(_selected.Value);
        }

        /// <summary>Shared by the Store button and by dropping an item on the Satchel button.</summary>
        public void StoreFromBoard(GridPos cell)
        {
            var result = _session.StoreInSatchel(cell);
            if (result == StoreResult.SatchelFull) ShowMessage("The satchel is full");
            else if (result == StoreResult.Stored) Select(null);
            _nextRefresh = 0f;
        }

        private void OnUndo()
        {
            var result = _session.UndoLastSale();
            if (result == UndoSellResult.BoardFull) ShowMessage("No room on the board to put it back");
            _nextRefresh = 0f;
        }

        private void OnRetrieve(int slot)
        {
            var result = _session.RetrieveFromSatchel(slot, out _);
            if (result == RetrieveResult.BoardFull) ShowMessage("Board is full: make room first");
            RefreshSatchel(_session.UtcNow);
        }

        private void ToggleSatchel()
        {
            bool show = !_satchelPanel.gameObject.activeSelf;
            CloseAllPanels();
            _satchelPanel.gameObject.SetActive(show);
            if (show) RefreshSatchel(_session.UtcNow);
        }

        private void ToggleCodex()
        {
            bool show = !_codexPanel.gameObject.activeSelf;
            CloseAllPanels();
            _codexPanel.gameObject.SetActive(show);
            if (show) _codexText.text = BuildCodexText();
        }

        private void CloseAllPanels()
        {
            _satchelPanel.gameObject.SetActive(false);
            _codexPanel.gameObject.SetActive(false);
        }

        private void RefreshSatchel(DateTime now)
        {
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                var label = _slotButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (i >= _session.Satchel.Count)
                {
                    label.text = "—";
                    _slotButtons[i].interactable = false;
                    continue;
                }
                var item = _session.Satchel.Items[i];
                string text = DisplayName(item.ItemId) + "\nLv " + _session.Catalog.GetItem(item.ItemId).Level;
                var g = _session.GetSatchelGeneratorState(i);
                if (g != null) text += "\n" + g.Charges + "/" + _session.Catalog.GetItem(item.ItemId).Generator.Capacity;
                label.text = text;
                _slotButtons[i].interactable = true;
            }
        }

        private string BuildCodexText()
        {
            var sb = new StringBuilder();
            sb.Append("<b>Grove Codex</b>  ").Append(_session.Codex.Count).Append(" discovered\n\n");
            foreach (var chain in _session.Catalog.Chains)
            {
                int found = 0;
                foreach (var level in chain.Levels) if (_session.Codex.IsDiscovered(level.Id)) found++;
                string chainName = chain.Levels.Count > 0 ? DisplayName(chain.Levels[0].Id) : chain.Id;
                sb.Append("<b>").Append(chainName).Append(" line</b>  ").Append(found).Append('/').Append(chain.Levels.Count).Append('\n');
                for (int i = 0; i < chain.Levels.Count; i++)
                {
                    var level = chain.Levels[i];
                    sb.Append(i == 0 ? "  " : " · ");
                    sb.Append(_session.Codex.IsDiscovered(level.Id) ? DisplayName(level.Id) : "???");
                }
                sb.Append("\n\n");
            }
            return sb.ToString();
        }

        private void OnDiscovered(string itemId, int reward) =>
            ShowMessage("New discovery: " + DisplayName(itemId) + "!  +" + reward + " Glimmer", 3f);

        private static void SetButton(Button button, bool visible, string text)
        {
            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
            if (visible && text != null)
            {
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label.text != text) label.text = text;
            }
        }

        private void ApplySafeArea()
        {
            var safe = Screen.safeArea;
            if (safe == _appliedSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            _appliedSafeArea = safe;
            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        }

        private static bool Contains(RectTransform rect, Vector2 screen) =>
            rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);

        private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            SetAnchors(rt, anchorMin, anchorMax);
            return rt;
        }

        private static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image AddImage(RectTransform rt, Color color, bool raycast)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent, anchorMin, anchorMax);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = align;
            text.color = TextColor;
            text.raycastTarget = false;
            return text;
        }

        private static Button NewButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect(name, parent, Vector2.zero, Vector2.one);
            var image = AddImage(rt, ButtonColor, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var text = NewText("Label", rt, Vector2.zero, Vector2.one, 36, TextAlignmentOptions.Center);
            text.text = label;
            return button;
        }
    }
}
