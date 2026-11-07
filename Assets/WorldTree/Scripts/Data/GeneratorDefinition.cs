using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTree.Data
{
    /// <summary>Generator tuning for one generator item level (GDD §4.2).</summary>
    [CreateAssetMenu(menuName = "WorldTree/Generator", fileName = "Gen_New")]
    public sealed class GeneratorDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Drop
        {
            public string itemId;
            [Min(1)] public int weight = 1;
        }

        [Tooltip("Stable ID of the generator item, for example gen.seed_pouch.1. Must exist in a chain.")]
        [SerializeField] private string itemId;
        [SerializeField, Min(1)] private int capacity = 60;
        [SerializeField, Min(0.01f)] private float refreshMinutes = 20f;
        [SerializeField, Min(0)] private int sapCost = 1;
        [SerializeField] private List<Drop> drops = new List<Drop>();

        public string ItemId => itemId;
        public int Capacity => capacity;
        public float RefreshMinutes => refreshMinutes;
        public int SapCost => sapCost;
        public IReadOnlyList<Drop> Drops => drops;

        public void Configure(string id, int chargeCapacity, float minutes, int cost, List<Drop> dropList)
        {
            itemId = id;
            capacity = chargeCapacity;
            refreshMinutes = minutes;
            sapCost = cost;
            drops = dropList;
        }
    }
}
