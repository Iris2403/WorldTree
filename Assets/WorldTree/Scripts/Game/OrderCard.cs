using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorldTree.Game
{
    /// <summary>One order in the orders strip (GDD §6.1): text, Deliver (when everything is on the board) and Swap.</summary>
    public sealed class OrderCard : MonoBehaviour
    {
        private static readonly Color CardColor = new Color(0.16f, 0.22f, 0.19f, 0.95f);
        private static readonly Color ReadyColor = new Color(0.22f, 0.4f, 0.26f, 0.98f);

        private Image _background;
        private TextMeshProUGUI _text;
        private Button _deliver;
        private Button _swap;
        private string _lastText;

        public int? OrderId { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public static OrderCard Create(Transform parent, HudView hud)
        {
            var rt = HudView.NewRect("OrderCard", parent, Vector2.zero, Vector2.one);
            var card = rt.gameObject.AddComponent<OrderCard>();
            card._background = HudView.AddImage(rt, CardColor, true);
            card._text = HudView.NewText("Text", rt, new Vector2(0.05f, 0.27f), new Vector2(0.95f, 0.97f), 28, TextAlignmentOptions.TopLeft);
            card._deliver = HudView.NewButton("Deliver", rt, "Deliver", () => { if (card.OrderId.HasValue) hud.OnDeliverPressed(card.OrderId.Value); });
            HudView.SetAnchors((RectTransform)card._deliver.transform, new Vector2(0.04f, 0.03f), new Vector2(0.62f, 0.24f));
            card._deliver.GetComponentInChildren<TextMeshProUGUI>().fontSize = 28;
            card._swap = HudView.NewButton("Swap", rt, "Swap", () => { if (card.OrderId.HasValue) hud.OnSwapPressed(card.OrderId.Value); });
            HudView.SetAnchors((RectTransform)card._swap.transform, new Vector2(0.66f, 0.03f), new Vector2(0.96f, 0.24f));
            card._swap.GetComponentInChildren<TextMeshProUGUI>().fontSize = 24;
            card.Hide();
            return card;
        }

        public void Show(int orderId, string text, bool canDeliver, bool canSwap)
        {
            OrderId = orderId;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (text != _lastText)
            {
                _text.text = text;
                _lastText = text;
            }
            _deliver.interactable = canDeliver;
            _background.color = canDeliver ? ReadyColor : CardColor;
            if (_swap.gameObject.activeSelf != canSwap) _swap.gameObject.SetActive(canSwap);
        }

        public void Hide()
        {
            OrderId = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
