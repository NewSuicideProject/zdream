namespace Train.Environment.Scripts {
    public static class Utility {
        public static bool InBounds(MapData map, int x, int y)
            => (uint)x < (uint)map.Width && (uint)y < (uint)map.Height;

        public static float LevelToHeight(int level, float levelStepHeight)
            => level * levelStepHeight;
    }
}
