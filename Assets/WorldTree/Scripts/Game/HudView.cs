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
        private TextMeshProUGUI _level;
        private RectTransform _ordersStrip;
        private OrderCard[] _cards;
        private readonly System.Collections.Generic.List<FlyingLabel> _flying = new System.Collections.Generic.List<FlyingLabel>();
        private TextMeshProUGUI _glimmer;
        private TextMeshProUGUI _selection;
        private TextMeshProUGUI _message;
        private RectTransform _bottomBar;
        private Button _sellButton;
        private Button _storeButton;
        private Button _undoButton;
        private Button _satchelButton;
        private Button _skipButton;
        private Button _adSpeedButton;
        private Button _giftButton;
        private RectTransform _giftPanel;
        private TextMeshProUGUI _giftText;
        private Button _claimButton;
        private RectTransform _energyPanel;
        private TextMeshProUGUI _energyText;
        private Button _refillButton;
        private Button _adSapButton;
        private WorldPanel _world;
        private RectTransform _topBar;
        private RectTransform _banner;
        private TextMeshProUGUI _bannerText;
        private RectTransform _settingsPanel;
        private Button[] _settingButtons;

        /// <summary>Raised by every HUD button (used for the click sound).</summary>
        public static event Action ButtonClicked;
        private DialogueView _dialogue;
        private RectTransform _satchelPanel;
        private Button[] _slotButtons;
        private RectTransform _codexPanel;
        private TextMeshProUGUI _codexText;

        private GridPos? _selected;
        private float _messageUntil;
        private float _nextRefresh;
        private Rect _appliedSafeArea;

        /// <summary>Rewarded-ad provider. <see cref="NoAdService"/> until Phase 8, so ad buttons stay hidden.</summary>
        public IAdService Ads { get; set; } = new NoAdService();

        /// <summary>True while any panel is open; the board ignores input then.</summary>
        public bool IsPanelOpen =>
            _satchelPanel.gameObject.activeSelf || _codexPanel.gameObject.activeSelf ||
            _giftPanel.gameObject.activeSelf || _energyPanel.gameObject.activeSelf ||
            _world.IsOpen || _dialogue.IsOpen || _settingsPanel.gameObject.activeSelf;

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
            var bar = NewRect("TopBar", hud._safeArea, new Vector2(0f, 1f - BoardCameraFitter.TopBarShare), Vector2.one);
            hud._topBar = bar;
            AddImage(bar, PanelColor, false);
            hud._sap = NewText("Sap", bar, new Vector2(0.03f, 0.66f), new Vector2(0.62f, 0.97f), 44, TextAlignmentOptions.Left);
            hud._level = NewText("Level", bar, new Vector2(0.45f, 0.66f), new Vector2(0.8f, 0.97f), 40, TextAlignmentOptions.Right);
            var settings = NewButton("Settings", bar, "Settings", hud.ToggleSettings);
            SetAnchors((RectTransform)settings.transform, new Vector2(0.82f, 0.64f), new Vector2(0.985f, 0.96f));
            settings.GetComponentInChildren<TextMeshProUGUI>().fontSize = 28;

            // Scene banner (06-mobile-layout.md §2): area name and progress, only where there is room.
            hud._banner = NewRect("Banner", hud._safeArea, new Vector2(0f, 0.8f), new Vector2(1f, 0.85f));
            AddImage(hud._banner, new Color(0.12f, 0.16f, 0.13f, 0.9f), false);
            hud._bannerText = NewText("Area", hud._banner, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f), 40, TextAlignmentOptions.Center);
            hud._banner.gameObject.SetActive(false);
            hud._glimmer = NewText("Currencies", bar, new Vector2(0.03f, 0.36f), new Vector2(0.97f, 0.66f), 36, TextAlignmentOptions.Left);
            hud._selection = NewText("Selection", bar, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.36f), 34, TextAlignmentOptions.Left);

            // Orders strip (GDD §6.1): up to 4 cards side by side under the top bar.
            hud._ordersStrip = NewRect("Orders", hud._safeArea,
                new Vector2(0f, 1f - BoardCameraFitter.TopReserved), new Vector2(1f, 1f - BoardCameraFitter.TopBarShare));
            var orderRow = hud._ordersStrip.gameObject.AddComponent<HorizontalLayoutGroup>();
            orderRow.padding = new RectOffset(10, 10, 8, 8);
            orderRow.spacing = 10;
            orderRow.childControlWidth = orderRow.childControlHeight = true;
            orderRow.childForceExpandWidth = orderRow.childForceExpandHeight = true;
            hud._cards = new OrderCard[Core.Orders.OrderRules.BaseSlots + 1];
            for (int i = 0; i < hud._cards.Length; i++) hud._cards[i] = OrderCard.Create(hud._ordersStrip, hud);

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
            hud._skipButton = NewButton("Skip", hud._bottomBar, "Skip", hud.OnSkip);
            hud._adSpeedButton = NewButton("AdSpeed", hud._bottomBar, "Ad: ½ time", hud.OnAdSpeedUp);
            NewButton("World", hud._bottomBar, "World", hud.ToggleWorld);
            hud._satchelButton = NewButton("Satchel", hud._bottomBar, "Satchel", hud.ToggleSatchel);
            NewButton("Codex", hud._bottomBar, "Codex", hud.ToggleCodex);
            hud._giftButton = NewButton("Gift", hud._bottomBar, "Gift", hud.ToggleGift);
            NewButton("Sap", hud._bottomBar, "Sap+", hud.ToggleEnergy);

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
                hud._slotButtons[i] = NewButton("Slot " + i, slots, "empty", () => hud.OnRetrieve(slot));
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

            // Daily Gift panel
            hud._giftPanel = NewRect("GiftPanel", hud._safeArea, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.75f));
            AddImage(hud._giftPanel, PanelColor, true);
            var giftClose = NewButton("Close", hud._giftPanel, "Close", hud.ToggleGift);
            SetAnchors((RectTransform)giftClose.transform, new Vector2(0.77f, 0.88f), new Vector2(0.97f, 0.98f));
            hud._giftText = NewText("Days", hud._giftPanel, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.86f), 38, TextAlignmentOptions.TopLeft);
            hud._claimButton = NewButton("Claim", hud._giftPanel, "Claim", hud.OnClaimGift);
            SetAnchors((RectTransform)hud._claimButton.transform, new Vector2(0.25f, 0.04f), new Vector2(0.75f, 0.16f));
            hud._giftPanel.gameObject.SetActive(false);

            // Sap panel: Moonpetal refill and rewarded-ad Sap
            hud._energyPanel = NewRect("SapPanel", hud._safeArea, new Vector2(0.06f, 0.32f), new Vector2(0.94f, 0.6f));
            AddImage(hud._energyPanel, PanelColor, true);
            var energyClose = NewButton("Close", hud._energyPanel, "Close", hud.ToggleEnergy);
            SetAnchors((RectTransform)energyClose.transform, new Vector2(0.77f, 0.78f), new Vector2(0.97f, 0.96f));
            hud._energyText = NewText("Info", hud._energyPanel, new Vector2(0.05f, 0.5f), new Vector2(0.75f, 0.95f), 38, TextAlignmentOptions.TopLeft);
            hud._refillButton = NewButton("Refill", hud._energyPanel, "Refill", hud.OnRefillSap);
            SetAnchors((RectTransform)hud._refillButton.transform, new Vector2(0.05f, 0.08f), new Vector2(0.48f, 0.42f));
            hud._adSapButton = NewButton("AdSap", hud._energyPanel, "Watch ad", hud.OnAdSap);
            SetAnchors((RectTransform)hud._adSapButton.transform, new Vector2(0.52f, 0.08f), new Vector2(0.95f, 0.42f));
            hud._energyPanel.gameObject.SetActive(false);

            // Settings panel (GDD §10 accessibility).
            hud._settingsPanel = NewRect("SettingsPanel", hud._safeArea, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.75f));
            AddImage(hud._settingsPanel, PanelColor, true);
            NewText("Title", hud._settingsPanel, new Vector2(0.06f, 0.86f), new Vector2(0.7f, 0.98f), 44, TextAlignmentOptions.Left).text = "<b>Settings</b>";
            var settingsClose = NewButton("Close", hud._settingsPanel, "Close", hud.ToggleSettings);
            SetAnchors((RectTransform)settingsClose.transform, new Vector2(0.72f, 0.87f), new Vector2(0.96f, 0.98f));
            var kinds = (Core.Settings.SettingKind[])Enum.GetValues(typeof(Core.Settings.SettingKind));
            hud._settingButtons = new Button[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                hud._settingButtons[i] = NewButton(kind.ToString(), hud._settingsPanel, kind.ToString(), () => hud.ToggleSetting(kind));
                float top = 0.82f - i * 0.16f;
                SetAnchors((RectTransform)hud._settingButtons[i].transform, new Vector2(0.08f, top - 0.13f), new Vector2(0.92f, top));
            }
            hud._settingsPanel.gameObject.SetActive(false);

            hud._world = WorldPanel.Create(hud._safeArea, hud);
            hud._dialogue = DialogueView.Create(hud._safeArea);
            return hud;
        }

        public void SetSession(GameSession session, GameDatabase database, BoardView board)
        {
            if (_session != null)
            {
                _session.ItemDiscovered -= OnDiscovered;
                _session.OrderCompleted -= OnOrderCompleted;
                _session.LeveledUp -= OnLeveledUp;
            }
            _session = session;
            _database = database;
            _board = board;
            _session.ItemDiscovered += OnDiscovered;
            _session.OrderCompleted += OnOrderCompleted;
            _session.LeveledUp += OnLeveledUp;
            _world.SetSession(session);
            _dialogue.SetSession(session);
            ApplyTextSize();
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
            IsPanelOpen || Contains(_bottomBar, screen) || Contains(_ordersStrip, screen);

        /// <summary>The order whose card is under the screen point (drop target for delivering one item), or null.</summary>
        public int? OrderAt(Vector2 screen)
        {
            foreach (var card in _cards)
                if (card.OrderId.HasValue && Contains(card.Rect, screen)) return card.OrderId;
            return null;
        }

        /// <summary>Delivers one dragged item to an order card (GDD §6.1).</summary>
        public void ContributeFromBoard(GridPos cell, int orderId)
        {
            switch (_session.ContributeToOrder(cell, orderId))
            {
                case DeliverResult.NotNeeded: ShowMessage("This order doesn't need that item"); break;
                case DeliverResult.Accepted: ShowMessage("Delivered! The order needs a bit more"); break;
            }
            Select(null);
            _nextRefresh = 0f;
        }

        internal void OnDeliverPressed(int orderId)
        {
            if (_session.DeliverOrder(orderId) == DeliverResult.MissingItems) ShowMessage("Some requested items aren't on the board yet");
            _nextRefresh = 0f;
        }

        internal void OnSwapPressed(int orderId)
        {
            switch (_session.SwapOrder(orderId))
            {
                case SwapOrderResult.OnCooldown: ShowMessage("You can swap again in " + FormatTime(_session.OrderSwapCooldownLeft)); break;
                case SwapOrderResult.HasProgress: ShowMessage("Items were already delivered to this order"); break;
            }
            _nextRefresh = 0f;
        }

        private void RefreshOrders()
        {
            var active = _session.Orders.Active;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (i >= active.Count)
                {
                    _cards[i].Hide();
                    continue;
                }
                var order = active[i];
                var sb = new StringBuilder();
                sb.Append("<b>").Append(CustomerName(order.CustomerId)).Append("</b>\n");
                foreach (var r in order.Requirements)
                {
                    // Delivered: struck through. On the board: green. The default TMP font (LiberationSans SDF)
                    // has no check-mark glyph, so colour carries that meaning instead.
                    bool onBoard = !r.Fulfilled && _session.IsOnBoard(r.ItemId);
                    sb.Append(r.Fulfilled ? "<s>" : onBoard ? "<color=#8FE08F>" : "")
                      .Append(DisplayName(r.ItemId))
                      .Append(r.Fulfilled ? "</s>" : onBoard ? "</color>" : "").Append('\n');
                }
                sb.Append("<size=80%>+").Append(order.Glimmer).Append(" Gl  +").Append(order.Lifebloom).Append(" Lb  +").Append(order.Xp).Append(" XP");
                if (order.BonusItemId != null) sb.Append("  +").Append(DisplayName(order.BonusItemId));
                sb.Append("</size>");
                bool canSwap = !order.HasProgress && _session.OrderSwapCooldownLeft <= TimeSpan.Zero;
                _cards[i].Show(order.Id, sb.ToString(), _session.CanDeliver(order.Id), canSwap);
            }
        }

        private static string CustomerName(string id)
        {
            switch (id)
            {
                case "wren": return "Wren";
                case "thalen": return "Elder Thalen";
                case "pip": return "Pip";
                case "marra": return "Marra";
                case "sorrel_fen": return "Sorrel & Fen";
                default: return id;
            }
        }

        private void OnOrderCompleted(Core.Orders.Order order)
        {
            // Reward fly-to-HUD: the reward line rises from the strip to the currency row and fades.
            var label = NewText("Reward", _safeArea, new Vector2(0.15f, 1f - BoardCameraFitter.TopReserved),
                new Vector2(0.85f, 1f - BoardCameraFitter.TopBarShare * 0.6f), 40, TextAlignmentOptions.Center);
            label.text = "+" + order.Glimmer + " Glimmer  +" + order.Lifebloom + " Lifebloom  +" + order.Xp + " XP";
            label.color = new Color(1f, 0.9f, 0.45f);
            _flying.Add(new FlyingLabel { Text = label, Start = Time.unscaledTime });
            ShowMessage(CustomerName(order.CustomerId) + "'s order complete!");
        }

        private void OnLeveledUp(int level) => ShowMessage("Level up! Lv " + level + ": Sap refilled", 3.5f);

        private void AnimateFlyingLabels()
        {
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                float t = (Time.unscaledTime - f.Start) / 1.2f;
                if (t >= 1f || f.Text == null)
                {
                    if (f.Text != null) Destroy(f.Text.gameObject);
                    _flying.RemoveAt(i);
                    continue;
                }
                var rt = f.Text.rectTransform;
                rt.anchoredPosition = new Vector2(0f, 160f * (1f - (1f - t) * (1f - t)));
                var c = f.Text.color;
                c.a = 1f - t * t;
                f.Text.color = c;
            }
        }

        private sealed class FlyingLabel
        {
            public TextMeshProUGUI Text;
            public float Start;
        }

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
            AnimateFlyingLabels();
            // Queued story scenes appear as soon as no other panel is in the way (GDD §8.4).
            if (_session != null && !_dialogue.IsOpen && !_world.IsOpen && !IsPanelOpen) _dialogue.TryShowPending();
            if (_message.text.Length > 0 && Time.unscaledTime > _messageUntil) _message.text = string.Empty;
            if (_session == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            var now = _session.UtcNow;
            int sap = _session.Sap;
            var next = _session.Energy.NextTickUtc;
            _sap.text = "Sap " + sap + " / " + _session.Energy.Config.Cap + (next.HasValue ? "   +1 in " + FormatTime(next.Value - now) : "   Full");
            _glimmer.text = "Glimmer " + _session.Wallet.Glimmer + "   Moonpetals " + _session.Wallet.Moonpetals +
                            "   Lifebloom " + _session.Wallet.Lifebloom;
            _level.text = "Lv " + _session.Progress.Level + "  ·  " + _session.Progress.Xp + "/" + _session.Progress.XpToNextLevel + " XP";
            RefreshOrders();
            RefreshBanner();
            _selection.text = DescribeSelection(now);
            RefreshButtons();
            if (_satchelPanel.gameObject.activeSelf) RefreshSatchel(now);
            if (_giftPanel.gameObject.activeSelf) RefreshGift();
            if (_energyPanel.gameObject.activeSelf) RefreshEnergy();
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
            var itemDef = item != null ? _session.Catalog.GetItem(item.ItemId) : null;
            bool sellable = item != null && open && (!itemDef.IsGenerator || itemDef.Generator.IsConsumable);
            SetButton(_sellButton, sellable, sellable ? "Sell +" + _session.Catalog.GetItem(item.ItemId).SellValue : null);
            SetButton(_storeButton, item != null && open, null);
            double undo = _session.SellUndoSecondsLeft;
            SetButton(_undoButton, undo > 0, "Undo (" + Math.Ceiling(undo) + ")");
            SetButton(_satchelButton, true, "Satchel " + _session.Satchel.Count + "/" + _session.Satchel.Capacity);

            // Generator actions for a selected, refreshing permanent generator (GDD §4.1).
            bool refreshing = open && itemDef != null && itemDef.IsGenerator && !itemDef.Generator.IsConsumable
                              && _session.GetGeneratorState(_selected.Value).RefreshEndsUtc.HasValue;
            int skipCost = refreshing ? _session.SkipCostAt(_selected.Value) : 0;
            SetButton(_skipButton, skipCost > 0, "Skip (" + skipCost + " MP)");
            bool adSpeed = refreshing && Ads.IsRewardedReady && !_session.GetGeneratorState(_selected.Value).AdSpeedUpUsed;
            SetButton(_adSpeedButton, adSpeed, null);
            SetButton(_giftButton, true, _session.CanClaimDailyGift ? "Gift!" : "Gift");
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
            if (state == CellState.Withered)
                return _session.Catalog.GetItem(item.ItemId).IsGenerator
                    ? name + " (Withered). Merge any two items next to it to free it."
                    : name + " (Withered). Drop a matching item on it to free it.";

            if (!def.IsGenerator) return name + " · Lv " + def.Level;
            if (def.Generator.IsConsumable) return name + " · " + _session.GetGeneratorState(p).Charges + " taps left, then it vanishes";
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

        private void ToggleSettings()
        {
            bool show = !_settingsPanel.gameObject.activeSelf;
            CloseAllPanels();
            _settingsPanel.gameObject.SetActive(show);
            if (show) RefreshSettings();
        }

        private void ToggleSetting(Core.Settings.SettingKind kind)
        {
            _session.SetSetting(kind, !SettingValue(kind));
            RefreshSettings();
            ApplyTextSize();
        }

        private bool SettingValue(Core.Settings.SettingKind kind)
        {
            var s = _session.Settings;
            switch (kind)
            {
                case Core.Settings.SettingKind.Music: return s.Music;
                case Core.Settings.SettingKind.Sound: return s.Sound;
                case Core.Settings.SettingKind.Haptics: return s.Haptics;
                case Core.Settings.SettingKind.ReducedMotion: return s.ReducedMotion;
                default: return s.LargeText;
            }
        }

        private void RefreshSettings()
        {
            string[] names = { "Music", "Sound effects", "Vibration", "Reduced motion", "Large text" };
            var kinds = (Core.Settings.SettingKind[])Enum.GetValues(typeof(Core.Settings.SettingKind));
            for (int i = 0; i < kinds.Length; i++)
                _settingButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = names[i] + ": " + (SettingValue(kinds[i]) ? "On" : "Off");
        }

        /// <summary>Large text (GDD §10): bigger selection and dialogue text.</summary>
        private void ApplyTextSize()
        {
            bool large = _session != null && _session.Settings.LargeText;
            _selection.fontSize = large ? 40 : 34;
            _dialogue.SetLargeText(large);
        }

        private void RefreshBanner()
        {
            if (!_banner.gameObject.activeSelf) return;
            var area = _session.CurrentArea;
            if (area == null)
            {
                _bannerText.text = _session.Story == null ? "WORLD TREE" : "The World Tree breathes again";
                return;
            }
            int done = 0;
            foreach (var task in area.Tasks) if (_session.Quest.IsTaskCompleted(task.Id)) done++;
            _bannerText.text = area.Name + "   " + done + "/" + area.Tasks.Count + " restored";
        }

        /// <summary>
        /// Applies the solver's layout (06-mobile-layout.md §2): HUD bar, optional banner, orders strip and context
        /// bar, as fractions of the safe area. Returns the board region as fractions (bottom, top, width).
        /// </summary>
        public void ApplyLayout(Core.Layout.ScreenLayout layout, float safeWidthPt, float safeHeightPt)
        {
            float h = Mathf.Max(1f, safeHeightPt);
            float hud = layout.HudHeight / h, banner = layout.BannerHeight / h, spare = layout.SpareAbove / h;
            float orders = layout.OrdersHeight / h, context = layout.ContextHeight / h;
            SetAnchors(_topBar, new Vector2(0f, 1f - hud), Vector2.one);
            _banner.gameObject.SetActive(banner > 0f);
            if (banner > 0f) SetAnchors(_banner, new Vector2(0f, 1f - hud - banner), new Vector2(1f, 1f - hud));
            float ordersTop = 1f - hud - banner - spare;
            SetAnchors(_ordersStrip, new Vector2(0f, ordersTop - orders), new Vector2(1f, ordersTop));
            SetAnchors(_bottomBar, Vector2.zero, new Vector2(1f, context));
            SetAnchors(_message.rectTransform, new Vector2(0.04f, context), new Vector2(0.96f, context + 0.035f));
            RefreshBanner();
        }

        private void ToggleWorld()
        {
            bool show = !_world.IsOpen;
            CloseAllPanels();
            if (show) _world.Open();
        }

        private void CloseAllPanels()
        {
            _world.Close();
            _settingsPanel.gameObject.SetActive(false);
            _satchelPanel.gameObject.SetActive(false);
            _codexPanel.gameObject.SetActive(false);
            _giftPanel.gameObject.SetActive(false);
            _energyPanel.gameObject.SetActive(false);
        }

        private void OnSkip()
        {
            if (!_selected.HasValue) return;
            if (_session.SkipRefresh(_selected.Value) == SkipResult.NotEnoughMoonpetals) ShowMessage("Not enough Moonpetals");
            _nextRefresh = 0f;
        }

        private void OnAdSpeedUp()
        {
            if (!_selected.HasValue || !Ads.IsRewardedReady) return;
            var cell = _selected.Value;
            Ads.ShowRewarded(watched =>
            {
                if (watched && _session.ApplyAdSpeedUp(cell) == PurchaseResult.Done) ShowMessage("Refresh time halved");
                _nextRefresh = 0f;
            });
        }

        private void ToggleGift()
        {
            bool show = !_giftPanel.gameObject.activeSelf;
            CloseAllPanels();
            _giftPanel.gameObject.SetActive(show);
            if (show) RefreshGift();
        }

        private void OnClaimGift()
        {
            switch (_session.ClaimDailyGift())
            {
                case ClaimResult.Claimed: ShowMessage("Daily Gift claimed!"); break;
                case ClaimResult.NoRoom: ShowMessage("Make room on the board or in the satchel first"); break;
                case ClaimResult.AlreadyClaimedToday: ShowMessage("Come back tomorrow for the next gift"); break;
            }
            RefreshGift();
        }

        private void RefreshGift()
        {
            var sb = new StringBuilder("<b>Daily Gift</b>  (one per day; missing a day doesn't reset it)\n\n");
            for (int day = 0; day < Core.Economy.DailyGift.Days; day++)
            {
                bool next = day == _session.DailyGift.NextDayIndex;
                sb.Append(next ? "<b>> " : "   ").Append("Day ").Append(day + 1).Append(": ");
                var rewards = Core.Economy.DailyGift.RewardsFor(day);
                for (int i = 0; i < rewards.Count; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append(Describe(rewards[i]));
                }
                sb.Append(next ? "</b>\n" : "\n");
            }
            _giftText.text = sb.ToString();
            bool can = _session.CanClaimDailyGift;
            _claimButton.interactable = can;
            _claimButton.GetComponentInChildren<TextMeshProUGUI>().text = can ? "Claim" : "Claimed today";
        }

        private string Describe(Core.Economy.Reward reward)
        {
            switch (reward.Kind)
            {
                case Core.Economy.RewardKind.Sap: return reward.Amount + " Sap";
                case Core.Economy.RewardKind.Glimmer: return reward.Amount + " Glimmer";
                case Core.Economy.RewardKind.Moonpetals: return reward.Amount + " Moonpetals";
                default: return DisplayName(reward.ItemId);
            }
        }

        private void ToggleEnergy()
        {
            bool show = !_energyPanel.gameObject.activeSelf;
            CloseAllPanels();
            _energyPanel.gameObject.SetActive(show);
            if (show) RefreshEnergy();
        }

        private void RefreshEnergy()
        {
            _energyText.text = "<b>Need more Sap?</b>\nSap " + _session.Sap + " / " + _session.Energy.Config.Cap +
                               "   ·   Moonpetals " + _session.Wallet.Moonpetals;
            _refillButton.GetComponentInChildren<TextMeshProUGUI>().text =
                "+" + Core.Economy.EconomyRules.SapRefillAmount + " Sap\nfor " + _session.SapRefillCost + " Moonpetals";
            bool ads = Ads.IsRewardedReady;
            _adSapButton.gameObject.SetActive(ads);
            if (ads)
            {
                int left = _session.AdSapLeftToday;
                _adSapButton.interactable = left > 0;
                _adSapButton.GetComponentInChildren<TextMeshProUGUI>().text =
                    "Watch ad: +" + Core.Economy.EconomyRules.AdSapAmount + " Sap\n(" + left + " left today)";
            }
        }

        private void OnRefillSap()
        {
            if (_session.RefillSap() == PurchaseResult.NotEnoughMoonpetals) ShowMessage("Not enough Moonpetals");
            RefreshEnergy();
        }

        private void OnAdSap()
        {
            if (!Ads.IsRewardedReady) return;
            Ads.ShowRewarded(watched =>
            {
                if (watched && _session.GrantAdSap() == PurchaseResult.DailyLimitReached) ShowMessage("No more ad rewards today");
                RefreshEnergy();
            });
        }

        private void RefreshSatchel(DateTime now)
        {
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                var label = _slotButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (i >= _session.Satchel.Count)
                {
                    label.text = "empty";
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

        internal static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            SetAnchors(rt, anchorMin, anchorMax);
            return rt;
        }

        internal static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        internal static Image AddImage(RectTransform rt, Color color, bool raycast)
        {
            var image = rt.gameObject.AddComponent<Image>();
            // Stand-in UI skin (roadmap D42): rounded 9-slice panels and buttons instead of hard rectangles.
            image.sprite = PlaceholderArt.Panel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static TextMeshProUGUI NewText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent, anchorMin, anchorMax);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = align;
            text.color = TextColor;
            text.raycastTarget = false;
            return text;
        }

        internal static Button NewButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect(name, parent, Vector2.zero, Vector2.one);
            var image = AddImage(rt, ButtonColor, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(() => ButtonClicked?.Invoke());
            var text = NewText("Label", rt, Vector2.zero, Vector2.one, 36, TextAlignmentOptions.Center);
            text.text = label;
            return button;
        }
    }
}
