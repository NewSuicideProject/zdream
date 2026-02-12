using UnityEngine;

namespace Train.Environment {
    public static class Utility {
        public static readonly Vector2Int[]
            Cardinal = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static float LevelToHeight(int level, float levelStepHeight)
            => level * levelStepHeight;
    }
}
