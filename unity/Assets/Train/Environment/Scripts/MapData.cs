using System.Collections.Generic;
using UnityEngine;

public sealed class MapData {
    public int Width;
    public int Height;
    public float CellSize;
    public Vector3 Origin;

    public bool[,] WallMatrix; // true = wall (logical)
    public bool[,] ActiveWallMatrix; // true = active wall (physical/render)

    public int[,] RoomIdMatrix;
    public float[,] TileHeight;
    public List<Room> Rooms;

    public void ComputeActiveWalls() {
        if (WallMatrix == null) {
            return;
        }

        ActiveWallMatrix = new bool[Height, Width];

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++) {
            if (!WallMatrix[y, x]) {
                continue;
            }

            bool touchesFloor = false;

            for (int k = 0; k < 4; k++) {
                int nx = x + dx[k];
                int ny = y + dy[k];

                if ((uint)nx >= (uint)Width || (uint)ny >= (uint)Height) {
                    continue;
                }

                if (!WallMatrix[ny, nx]) {
                    touchesFloor = true;
                    break;
                }
            }

            ActiveWallMatrix[y, x] = touchesFloor;
        }
    }

    public bool IsActiveWall(int y, int x) {
        if (ActiveWallMatrix == null) {
            return false;
        }

        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) {
            return false;
        }

        return ActiveWallMatrix[y, x];
    }
}
