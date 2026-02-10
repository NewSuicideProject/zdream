using System;
using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    [Serializable]
    public class Room {
        public int id;
        public RectInt bounds;
        public Vector2Int center;
        public int heightLevel;
        public readonly List<Cell> Cells = new();
        public readonly HashSet<int> FloorSet = new();

        public void Initialize() {
            FloorSet.Clear();
            foreach (Cell cell in Cells) {
                FloorSet.Add(Utility.Pack(cell.X, cell.Y));
            }
        }
    }
}
