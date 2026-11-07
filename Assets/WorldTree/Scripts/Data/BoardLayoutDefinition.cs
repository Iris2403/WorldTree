using System;
using System.Collections.Generic;
using UnityEngine;
using WorldTree.Core.Board;

namespace WorldTree.Data
{
    /// <summary>An authored starting board (GDD §3.1).</summary>
    [CreateAssetMenu(menuName = "WorldTree/Board Layout", fileName = "Board_New")]
    public sealed class BoardLayoutDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Placement
        {
            public int x;
            public int y;
            public string itemId;
            public CellState state = CellState.Open;
        }

        [SerializeField, Min(1)] private int width = 7;
        [SerializeField, Min(1)] private int height = 9;
        [SerializeField] private List<Placement> placements = new List<Placement>();

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<Placement> Placements => placements;

        public void Configure(int boardWidth, int boardHeight, List<Placement> cells)
        {
            width = boardWidth;
            height = boardHeight;
            placements = cells;
        }
    }
}
