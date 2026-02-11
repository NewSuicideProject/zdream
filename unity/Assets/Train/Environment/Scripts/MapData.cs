using System.Collections.Generic;
using UnityEngine;

public sealed class MapData {
    public int Width;
    public int Height;
    public float CellSize;
    public Vector3 Origin;

    public Cell[,] Cells;
    public List<Room> Rooms;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public void ComputeBordorWalls() {
        if (Cells == null) {
            return;
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++) {
            ref Cell cell = ref Cells[y, x];

            if (!cell.IsWall) {
                cell.IsBordor = false;
                continue;
            }

            bool touchesFloor = false;

            for (int k = 0; k < 4; k++) {
                int nx = x + dx[k];
                int ny = y + dy[k];
                if (!InBounds(nx, ny)) {
                    continue;
                }

                if (!Cells[ny, nx].IsWall) {
                    touchesFloor = true;
                    break;
                }
            }

            cell.IsBordor = touchesFloor;
        }
    }
}
