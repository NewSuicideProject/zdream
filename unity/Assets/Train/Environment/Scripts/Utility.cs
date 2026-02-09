public static class Utility {
    // This script is the set of utility for environment train
    // If you wanna use it then copy paste this script in your folder
    // Packs (x,y) into one int. Assumes x,y fit in int16.
    public static int Pack(int x, int y) => (y << 16) ^ (x & 0xFFFF);

    public static void Unpack(int packed, out int x, out int y) {
        x = (short)(packed & 0xFFFF);
        y = packed >> 16;
    }
}
