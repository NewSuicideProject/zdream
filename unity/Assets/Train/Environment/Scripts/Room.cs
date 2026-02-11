using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class Room {
    public int id;
    public RectInt bounds;
    public Vector2Int center;
    public int heightLevel;

    public readonly List<Vector2Int> Cells = new();
    public readonly HashSet<int> FloorSet = new();

    public void RebuildFloorSetFromCells() {
        FloorSet.Clear();
        for (int i = 0; i < Cells.Count; i++) {
            Vector2Int c = Cells[i];
            FloorSet.Add(Utility.Pack(c.x, c.y));
        }
    }

    public void RebuildCellsFromFloorSet() {
        Cells.Clear();
        foreach (int p in FloorSet) {
            Utility.Unpack(p, out int x, out int y);
            Cells.Add(new Vector2Int(x, y));
        }
    }

    public bool IsBoundaryCell(Vector2Int c) =>
        !FloorSet.Contains(Utility.Pack(c.x + 1, c.y)) ||
        !FloorSet.Contains(Utility.Pack(c.x - 1, c.y)) ||
        !FloorSet.Contains(Utility.Pack(c.x, c.y + 1)) ||
        !FloorSet.Contains(Utility.Pack(c.x, c.y - 1));

    public Vector2Int PickBestDoorCell(Vector2Int toward) {
        Vector2Int best = Cells[0];
        int bestScore = int.MaxValue;

        for (int i = 0; i < Cells.Count; i++) {
            Vector2Int c = Cells[i];
            if (!IsBoundaryCell(c)) {
                continue;
            }

            int score = Mathf.Abs(c.x - toward.x) + Mathf.Abs(c.y - toward.y);
            if (score < bestScore) {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }
}
