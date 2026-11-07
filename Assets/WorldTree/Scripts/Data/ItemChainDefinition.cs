using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTree.Data
{
    /// <summary>Authoring data for one item chain (architecture §5). IDs are typed, never derived from asset names.</summary>
    [CreateAssetMenu(menuName = "WorldTree/Item Chain", fileName = "Chain_New")]
    public sealed class ItemChainDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Level
        {
            [Tooltip("Stable ID, for example item.nature.03. Never rename after release.")]
            public string id;
            public string displayName;
            public int sellValue;
            [Tooltip("Optional. Graybox sprites are generated when empty.")]
            public Sprite icon;
        }

        [SerializeField] private string chainId;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private List<Level> levels = new List<Level>();

        public string ChainId => chainId;
        public Color Color => color;
        public IReadOnlyList<Level> Levels => levels;

        /// <summary>Used by the Editor setup menu to fill a new asset.</summary>
        public void Configure(string id, Color chainColor, List<Level> chainLevels)
        {
            chainId = id;
            color = chainColor;
            levels = chainLevels;
        }
    }
}
