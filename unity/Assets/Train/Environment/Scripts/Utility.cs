using UnityEngine;

namespace Train.Environment.Scripts {
    public static class Utility {
        public static float LevelToHeight(int level, float levelStepHeight)
            => level * levelStepHeight;

        public static readonly Vector2Int[]
            Cardinal = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
    }
}
