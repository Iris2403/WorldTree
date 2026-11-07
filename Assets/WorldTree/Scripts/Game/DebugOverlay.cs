using UnityEngine;
using WorldTree.Core;

namespace WorldTree.Game
{
    /// <summary>
    /// Development-only overlay (Editor and development builds): debug actions plus the session
    /// statistics used to measure the GDD §5.1 "seconds per Sap" assumption. IMGUI keeps it free of
    /// any asset dependencies.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private GameSession _session;
        private Bootstrapper _bootstrapper;
        private bool _open;

        public void Init(GameSession session, Bootstrapper bootstrapper)
        {
            _session = session;
            _bootstrapper = bootstrapper;
        }

        private void OnGUI()
        {
            if (_session == null || !(Debug.isDebugBuild || Application.isEditor)) return;
            float scale = Mathf.Max(1f, Screen.height / 1100f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (!_open)
            {
                if (GUI.Button(new Rect(width - 70, height - 36, 64, 30), "Debug")) _open = true;
                return;
            }

            GUILayout.BeginArea(new Rect(width - 250, height - 330, 244, 324), GUI.skin.box);
            var stats = _session.Stats;
            var now = _session.UtcNow;
            double minutes = (now - stats.StartedUtc).TotalMinutes;
            double? secondsPerSap = stats.SecondsPerSap(now);
            GUILayout.Label("Session: " + minutes.ToString("0.0") + " min");
            GUILayout.Label("Taps: " + stats.Taps + "   Merges: " + stats.Merges);
            GUILayout.Label("Moves: " + stats.Moves + "   Swaps: " + stats.Swaps);
            GUILayout.Label("Sap spent: " + stats.SapSpent + (minutes > 0.05 ? "  (" + (stats.SapSpent / minutes).ToString("0.0") + "/min)" : ""));
            GUILayout.Label("Seconds per Sap: " + (secondsPerSap.HasValue ? secondsPerSap.Value.ToString("0.0") : "–"));
            if (GUILayout.Button("Reset statistics")) stats.Reset(now);
            if (GUILayout.Button("+10 Sap")) _session.DebugAddSap(10);
            if (GUILayout.Button("Refresh all generators now")) _session.DebugRefreshAllGenerators();
            if (GUILayout.Button("Reset save (new game)")) _bootstrapper.ResetGame();
            if (GUILayout.Button("Close")) _open = false;
            GUILayout.EndArea();
        }
    }
}
