using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class Room {
    public int id;
    public RectInt bounds;
    public Vector2Int center;
    public int heightLevel;

    public readonly List<Cell> Cells = new();
    public readonly HashSet<int> FloorSet = new();

    public void RebuildFloorSetFromCells() {
        FloorSet.Clear();
        for (int i = 0; i < Cells.Count; i++) {
            Cell c = Cells[i];
            FloorSet.Add(Utility.Pack(c.X, c.Y));
        }
    }

    public void RebuildCellsFromFloorSet() {
        Cells.Clear();
        foreach (int p in FloorSet) {
            Utility.Unpack(p, out int x, out int y);
            Cells.Add(new Cell(x, y));
        }
    }

    public bool IsBoundaryCell(Cell c) =>
        // same logic as before (4-neighbor missing => boundary)
        !FloorSet.Contains(Utility.Pack(c.X + 1, c.Y)) ||
        !FloorSet.Contains(Utility.Pack(c.X - 1, c.Y)) ||
        !FloorSet.Contains(Utility.Pack(c.X, c.Y + 1)) ||
        !FloorSet.Contains(Utility.Pack(c.X, c.Y - 1));

    public Cell PickBestDoorCell(Vector2Int toward) {
        Cell best = Cells[0];
        int bestScore = int.MaxValue;

        for (int i = 0; i < Cells.Count; i++) {
            Cell c = Cells[i];
            if (!IsBoundaryCell(c)) {
                continue;
            }

            int score = Mathf.Abs(c.X - toward.x) + Mathf.Abs(c.Y - toward.y);
            if (score < bestScore) {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }
}
