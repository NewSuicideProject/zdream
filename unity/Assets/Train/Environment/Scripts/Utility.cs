using UnityEngine;

public static class Utility {
    // Packs (x,y) into one int. Assumes x,y fit in int16.
    public static int Pack(int x, int y) => (y << 16) ^ (x & 0xFFFF);

    public static void Unpack(int packed, out int x, out int y) {
        x = (short)(packed & 0xFFFF);
        y = packed >> 16;
    }

    public static bool InBounds(MapData map, int x, int y)
        => (uint)x < (uint)map.Width && (uint)y < (uint)map.Height;

    public static bool InBounds(int width, int height, int x, int y)
        => (uint)x < (uint)width && (uint)y < (uint)height;

    public static float LevelToHeight(int level, float levelStepHeight)
        => level * levelStepHeight;
}
