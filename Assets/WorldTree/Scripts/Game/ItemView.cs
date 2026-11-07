using TMPro;
using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>Graybox view of one board item: a tinted shape plus a level label. Pooled by <see cref="BoardView"/>.</summary>
    public sealed class ItemView : MonoBehaviour
    {
        private SpriteRenderer _body;
        private TextMeshPro _label;
        private TextMeshPro _status;

        private Color _bodyColor;
        private string _lastStatus = string.Empty;

        public float BaseScale { get; private set; } = 1f;

        /// <summary>
        /// Generator status: Ready (charges shown), Refreshing (charges shown), Waiting for refresh
        /// (dimmed, countdown shown). Text is only rebuilt when it changes.
        /// </summary>
        public void SetGeneratorStatus(string text, bool waiting)
        {
            if (text != _lastStatus)
            {
                _status.text = text;
                _lastStatus = text;
            }
            var c = _bodyColor;
            if (waiting) c.a *= 0.45f;
            _body.color = c;
        }

        public static ItemView Create(Transform parent)
        {
            var go = new GameObject("Item");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ItemView>();
            view._body = go.AddComponent<SpriteRenderer>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            view._label = labelGo.AddComponent<TextMeshPro>();
            view._label.alignment = TextAlignmentOptions.Center;
            view._label.fontSize = 4f;
            view._label.fontStyle = FontStyles.Bold;
            view._label.color = new Color(0.08f, 0.1f, 0.12f);
            view._label.rectTransform.sizeDelta = new Vector2(1f, 1f);

            // Small line under generators: charges, or the refresh countdown (GDD §4.1 states).
            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(go.transform, false);
            statusGo.transform.localPosition = new Vector3(0f, -0.36f, 0f);
            view._status = statusGo.AddComponent<TextMeshPro>();
            view._status.alignment = TextAlignmentOptions.Center;
            view._status.fontSize = 2.2f;
            view._status.fontStyle = FontStyles.Bold;
            view._status.color = new Color(0.95f, 0.93f, 0.85f);
            view._status.rectTransform.sizeDelta = new Vector2(1.2f, 0.4f);
            view._status.text = string.Empty;
            return view;
        }

        public void Bind(Sprite sprite, Color color, string label, Color labelColor, float scale)
        {
            _body.sprite = sprite;
            _body.color = color;
            _label.text = label;
            _label.color = labelColor;
            _status.text = string.Empty;
            _lastStatus = string.Empty;
            _bodyColor = color;
            _body.color = color;
            BaseScale = scale;
            transform.localScale = Vector3.one * scale;
        }

        public void SetSorting(int order)
        {
            _body.sortingOrder = order;
            _label.sortingOrder = order + 1;
            _status.sortingOrder = order + 1;
        }
    }
}
