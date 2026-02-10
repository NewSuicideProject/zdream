using UnityEngine;

namespace Train.Environment.Scripts {
    public static class Utility {
        public static int Pack(int x, int y) => (y << 16) ^ (x & 0xFFFF);

        public static void Unpack(int packed, out int x, out int y) {
            x = (short)(packed & 0xFFFF);
            y = packed >> 16;
        }

        public static RectInt ExpandRect(RectInt r, int pad) =>
            new(r.xMin - pad, r.yMin - pad, r.width + (pad * 2), r.height + (pad * 2));

        public static bool InRect(RectInt r, int x, int y) => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;
    }
}
