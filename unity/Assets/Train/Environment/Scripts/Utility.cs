using UnityEngine;

namespace Train.Environment.Scripts {
    public static class Utility {
        public static float LevelToHeight(int level, float levelStepHeight)
            => level * levelStepHeight;

        public static bool InRect(RectInt r, int x, int y)
            => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;
    }
}

public static class GridDirections {
    // 4-directional grid movement (N,E,S,W)
    public static readonly Vector2Int[]
        Cardinal = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    // 8-directional (includes diagonals)
    public static readonly Vector2Int[] All = {
        Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down, new(1, 1), new(1, -1), new(-1, 1),
        new(-1, -1)
    };
}
