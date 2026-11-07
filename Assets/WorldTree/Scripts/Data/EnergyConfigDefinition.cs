using UnityEngine;

namespace WorldTree.Data
{
    /// <summary>Sap tuning (GDD §5).</summary>
    [CreateAssetMenu(menuName = "WorldTree/Energy Config", fileName = "Energy")]
    public sealed class EnergyConfigDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int cap = 100;
        [SerializeField, Min(1f)] private float regenSeconds = 120f;

        public int Cap => cap;
        public float RegenSeconds => regenSeconds;

        public void Configure(int sapCap, float seconds)
        {
            cap = sapCap;
            regenSeconds = seconds;
        }
    }
}
