using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTree.Data
{
    /// <summary>A dialogue scene (GDD §8.4). Speaker IDs: wren, thalen, pip, marra, sorrel, fen, memory_leaf.</summary>
    [CreateAssetMenu(menuName = "WorldTree/Dialogue Scene", fileName = "Scene_New")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Line
        {
            public string speaker;
            [TextArea(2, 4)] public string text;
        }

        [Tooltip("Stable ID, for example scene.glade.intro.")]
        [SerializeField] private string sceneId;
        [SerializeField] private List<Line> lines = new List<Line>();

        public string SceneId => sceneId;
        public IReadOnlyList<Line> Lines => lines;

        public void Configure(string id, List<Line> sceneLines)
        {
            sceneId = id;
            lines = sceneLines;
        }
    }
}
