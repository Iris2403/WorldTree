using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTree.Core;
using WorldTree.Core.Common;
using WorldTree.Data;

namespace WorldTree.Game
{
    /// <summary>
    /// Basic Phase 1 HUD (uGUI + TextMeshPro, built in code): Sap with the next +1 timer, the selected
    /// item or generator, and short messages. Generator wording follows GDD §4.1: "refresh", never "recharge".
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;

        private GameSession _session;
        private GameDatabase _database;
        private RectTransform _safeArea;
        private TextMeshProUGUI _sap;
        private TextMeshProUGUI _selection;
        private TextMeshProUGUI _message;
        private GridPos? _selected;
        private float _messageUntil;
        private float _nextRefresh;
        private Rect _appliedSafeArea;
        private BoardView _board;

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

            var hud = root.AddComponent<HudView>();
            hud._safeArea = NewRect("SafeArea", root.transform, Vector2.zero, Vector2.one);

            var bar = NewRect("TopBar", hud._safeArea, new Vector2(0f, 1f - BoardCameraFitter.TopReserved), Vector2.one);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = new Color(0.07f, 0.1f, 0.09f, 0.85f);
            barImage.raycastTarget = false;

            hud._sap = NewText("Sap", bar, new Vector2(0.03f, 0.5f), new Vector2(0.5f, 0.95f), 52, TextAlignmentOptions.Left);
            hud._selection = NewText("Selection", bar, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.5f), 40, TextAlignmentOptions.Left);
            hud._message = NewText("Message", hud._safeArea, new Vector2(0.05f, 0.005f), new Vector2(0.95f, BoardCameraFitter.BottomReserved), 40, TextAlignmentOptions.Center);
            hud._message.text = string.Empty;
            return hud;
        }

        public void SetSession(GameSession session, GameDatabase database, BoardView board)
        {
            _session = session;
            _database = database;
            _board = board;
            Select(null);
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

        private void Update()
        {
            ApplySafeArea();
            if (_message.text.Length > 0 && Time.unscaledTime > _messageUntil) _message.text = string.Empty;
            if (_session == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            var now = _session.UtcNow;
            int sap = _session.Sap;
            var next = _session.Energy.NextTickUtc;
            _sap.text = next.HasValue
                ? "Sap " + sap + " / " + _session.Energy.Config.Cap + "   +1 in " + Format(next.Value - now)
                : "Sap " + sap + " / " + _session.Energy.Config.Cap + "   Full";
            _selection.text = DescribeSelection(now);
        }

        private string DescribeSelection(DateTime now)
        {
            if (!_selected.HasValue) return "Tap a generator to grow items. Drag matching items together.";
            var item = _session.Board.GetItem(_selected.Value);
            if (item == null) return string.Empty;
            var def = _session.Catalog.GetItem(item.ItemId);
            string name = item.ItemId;
            if (_database != null && _database.TryGetVisual(item.ItemId, out _, out var level) && !string.IsNullOrEmpty(level.displayName))
                name = level.displayName;

            if (!def.IsGenerator) return name + " · Lv " + def.Level;
            var state = _session.GetGeneratorState(_selected.Value);
            string charges = state.Charges + " / " + def.Generator.Capacity;
            if (state.RefreshEndsUtc == null) return name + " Lv " + def.Level + " · " + charges + " · Ready";
            string prefix = state.Charges == 0 ? "Refreshes to full in " : "Full refresh in ";
            return name + " Lv " + def.Level + " · " + charges + " · " + prefix + Format(state.RefreshEndsUtc.Value - now);
        }

        private static string Format(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            int total = (int)Math.Ceiling(t.TotalSeconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        private void ApplySafeArea()
        {
            var safe = Screen.safeArea;
            if (safe == _appliedSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            _appliedSafeArea = safe;
            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent, anchorMin, anchorMax);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = align;
            text.color = new Color(0.95f, 0.93f, 0.85f);
            text.raycastTarget = false;
            return text;
        }
    }
}
