using TMPro;
using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>Graybox view of one board item: a tinted shape plus a level label. Pooled by <see cref="BoardView"/>.</summary>
    public sealed class ItemView : MonoBehaviour
    {
        private SpriteRenderer _body;
        private TextMeshPro _label;

        public float BaseScale { get; private set; } = 1f;

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
            return view;
        }

        public void Bind(Sprite sprite, Color color, string label, Color labelColor, float scale)
        {
            _body.sprite = sprite;
            _body.color = color;
            _label.text = label;
            _label.color = labelColor;
            BaseScale = scale;
            transform.localScale = Vector3.one * scale;
        }

        public void SetSorting(int order)
        {
            _body.sortingOrder = order;
            _label.sortingOrder = order + 1;
        }
    }
}
