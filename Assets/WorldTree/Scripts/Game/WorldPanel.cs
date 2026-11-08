using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTree.Core;
using WorldTree.Core.Story;

namespace WorldTree.Game
{
    /// <summary>
    /// Graybox area view (GDD §7.2): one tile per restoration task, grey-violet while blighted, cross-fading
    /// to a vivid colour when restored, plus the next task and its Restore button. Phase 6 replaces the tiles
    /// with layered corrupted/restored area art.
    /// </summary>
    public sealed class WorldPanel : MonoBehaviour
    {
        private const float FadeSeconds = 0.9f;
        private static readonly Color Corrupted = new Color(0.33f, 0.28f, 0.38f);
        private static readonly Color[] Restored =
        {
            new Color(0.45f, 0.8f, 0.4f), new Color(0.4f, 0.7f, 0.95f), new Color(0.95f, 0.8f, 0.35f),
            new Color(0.9f, 0.5f, 0.6f), new Color(0.6f, 0.9f, 0.75f),
        };

        private GameSession _session;
        private HudView _hud;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _next;
        private Button _restore;
        private Image[] _tiles;
        private float[] _fadeStart;
        private string _shownAreaId;

        public bool IsOpen => gameObject.activeSelf;

        public static WorldPanel Create(Transform parent, HudView hud)
        {
            var rt = HudView.NewRect("WorldPanel", parent, new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.85f));
            HudView.AddImage(rt, new Color(0.07f, 0.1f, 0.09f, 0.96f), true);
            var panel = rt.gameObject.AddComponent<WorldPanel>();
            panel._hud = hud;
            var close = HudView.NewButton("Close", rt, "Close", panel.Close);
            HudView.SetAnchors((RectTransform)close.transform, new Vector2(0.77f, 0.92f), new Vector2(0.97f, 0.985f));
            panel._title = HudView.NewText("Title", rt, new Vector2(0.04f, 0.9f), new Vector2(0.75f, 0.985f), 46, TextAlignmentOptions.Left);

            var grid = HudView.NewRect("Tiles", rt, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.88f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(170, 170);
            layout.spacing = new Vector2(14, 14);
            layout.childAlignment = TextAnchor.MiddleCenter;
            panel._tiles = new Image[10];
            panel._fadeStart = new float[10];
            for (int i = 0; i < panel._tiles.Length; i++)
            {
                var tile = HudView.NewRect("Tile " + i, grid, Vector2.zero, Vector2.one);
                panel._tiles[i] = HudView.AddImage(tile, Corrupted, false);
                panel._fadeStart[i] = -1f;
            }

            panel._next = HudView.NewText("Next", rt, new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.4f), 36, TextAlignmentOptions.TopLeft);
            panel._restore = HudView.NewButton("Restore", rt, "Restore", panel.OnRestore);
            HudView.SetAnchors((RectTransform)panel._restore.transform, new Vector2(0.2f, 0.03f), new Vector2(0.8f, 0.14f));
            rt.gameObject.SetActive(false);
            return panel;
        }

        public void SetSession(GameSession session)
        {
            if (_session != null) _session.TaskRestored -= OnTaskRestored;
            _session = session;
            _session.TaskRestored += OnTaskRestored;
            _shownAreaId = null;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Refresh(snapTiles: true);
        }

        public void Close() => gameObject.SetActive(false);

        private void OnRestore()
        {
            switch (_session.RestoreNextTask())
            {
                case RestoreResult.NotEnoughLifebloom: _hud.ShowMessage("Not enough Lifebloom: complete more orders"); break;
                case RestoreResult.AreaRestored: _hud.ShowMessage("Area restored!", 3f); Close(); break;
            }
            if (IsOpen) Refresh(snapTiles: false);
        }

        private void OnTaskRestored(AreaDef area, RestorationTask task)
        {
            for (int i = 0; i < area.Tasks.Count && i < _tiles.Length; i++)
                if (area.Tasks[i].Id == task.Id) _fadeStart[i] = Time.unscaledTime;
        }

        private void Update()
        {
            if (_session == null) return;
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_fadeStart[i] < 0f) continue;
                float t = Mathf.Clamp01((Time.unscaledTime - _fadeStart[i]) / FadeSeconds);
                _tiles[i].color = Color.Lerp(Corrupted, Restored[i % Restored.Length], t * t * (3f - 2f * t));
                if (t >= 1f) _fadeStart[i] = -1f;
            }
        }

        private void Refresh(bool snapTiles)
        {
            var area = _session.CurrentArea;
            if (area == null)
            {
                _title.text = _session.Story == null ? "World" : "All areas restored";
                _next.text = "More of the World Tree awaits in future updates.";
                _restore.gameObject.SetActive(false);
                foreach (var tile in _tiles) tile.gameObject.SetActive(false);
                return;
            }

            int done = 0;
            foreach (var task in area.Tasks) if (_session.Quest.IsTaskCompleted(task.Id)) done++;
            _title.text = "<b>" + area.Name + "</b>  " + done + "/" + area.Tasks.Count + " restored";
            bool newArea = _shownAreaId != area.Id;
            _shownAreaId = area.Id;
            for (int i = 0; i < _tiles.Length; i++)
            {
                bool exists = i < area.Tasks.Count;
                _tiles[i].gameObject.SetActive(exists);
                if (!exists || (!snapTiles && !newArea) || _fadeStart[i] >= 0f) continue;
                _tiles[i].color = _session.Quest.IsTaskCompleted(area.Tasks[i].Id) ? Restored[i % Restored.Length] : Corrupted;
            }

            var next = _session.NextTask;
            var sb = new StringBuilder();
            sb.Append("Next: <b>").Append(next.Name).Append("</b>\n");
            sb.Append("Cost ").Append(next.LifebloomCost).Append(" Lifebloom   (you have ").Append(_session.Wallet.Lifebloom).Append(')');
            _next.text = sb.ToString();
            _restore.gameObject.SetActive(true);
            _restore.interactable = _session.Wallet.Lifebloom >= next.LifebloomCost;
            _restore.GetComponentInChildren<TextMeshProUGUI>().text = "Restore (" + next.LifebloomCost + " Lifebloom)";
        }
    }
}
