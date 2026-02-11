using UnityEngine;

namespace Train.Environment.Scripts {
    public static class Utility {
        public static float LevelToHeight(int level, float levelStepHeight)
            => level * levelStepHeight;

        public static bool InRect(RectInt r, int x, int y)
            => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;
    }
}
