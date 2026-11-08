using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldTree.Core;
using WorldTree.Core.Content;
using WorldTree.Core.Story;

namespace WorldTree.Game
{
    /// <summary>
    /// Shows queued dialogue scenes (GDD §8.4): speaker and line, Next and Skip. Memory Leaf lines are shown
    /// in gold italics. A scene is dismissed in Core once it is finished or skipped.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        private static readonly Color SpeakerColor = new Color(1f, 0.85f, 0.5f);

        private GameSession _session;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _line;
        private DialogueScene _scene;
        private int _index;

        public bool IsOpen => gameObject.activeSelf;

        public static DialogueView Create(Transform parent)
        {
            var rt = HudView.NewRect("Dialogue", parent, new Vector2(0.03f, 0.13f), new Vector2(0.97f, 0.42f));
            HudView.AddImage(rt, new Color(0.08f, 0.07f, 0.1f, 0.96f), true);
            var view = rt.gameObject.AddComponent<DialogueView>();
            view._speaker = HudView.NewText("Speaker", rt, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.96f), 42, TextAlignmentOptions.Left);
            view._speaker.color = SpeakerColor;
            view._line = HudView.NewText("Line", rt, new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.78f), 40, TextAlignmentOptions.TopLeft);
            var next = HudView.NewButton("Next", rt, "Next", view.OnNext);
            HudView.SetAnchors((RectTransform)next.transform, new Vector2(0.58f, 0.04f), new Vector2(0.95f, 0.22f));
            var skip = HudView.NewButton("Skip", rt, "Skip", view.OnSkip);
            HudView.SetAnchors((RectTransform)skip.transform, new Vector2(0.05f, 0.04f), new Vector2(0.35f, 0.22f));
            rt.gameObject.SetActive(false);
            return view;
        }

        public void SetSession(GameSession session)
        {
            _session = session;
            Close();
        }

        /// <summary>Opens the oldest pending scene, if any. Returns true if a scene is now showing.</summary>
        public bool TryShowPending()
        {
            if (_session == null || IsOpen) return IsOpen;
            _scene = _session.PendingDialogue;
            if (_scene == null) return false;
            _index = 0;
            gameObject.SetActive(true);
            ShowLine();
            return true;
        }

        private void ShowLine()
        {
            var line = _scene.Lines[_index];
            bool leaf = line.SpeakerId == "memory_leaf";
            _speaker.text = StoryContent.Speakers.TryGetValue(line.SpeakerId, out var name) ? name : line.SpeakerId;
            _line.text = leaf ? "<i>" + line.Text + "</i>" : line.Text;
            _line.color = leaf ? SpeakerColor : new Color(0.95f, 0.93f, 0.85f);
        }

        private void OnNext()
        {
            if (_scene == null) return;
            _index++;
            if (_index < _scene.Lines.Count) ShowLine();
            else Finish();
        }

        private void OnSkip() => Finish();

        private void Finish()
        {
            if (_scene != null) _session.DismissDialogue(_scene.Id);
            Close();
        }

        private void Close()
        {
            _scene = null;
            gameObject.SetActive(false);
        }
    }
}
