using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    public sealed class MapData {
        public int Width;
        public int Height;
        public float CellSize;
        public Vector3 Origin;

        public Cell[,] Cells;
        public List<Room> Rooms;

        public void InitializeAllWalls() {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) {
                Cells[y, x] = new Cell(true);
            }
        }

        public void ApplyBorderWalls() {
            for (int x = 0; x < Width; x++) {
                Cells[0, x].IsWall = true;
                Cells[Height - 1, x].IsWall = true;
            }

            for (int y = 0; y < Height; y++) {
                Cells[y, 0].IsWall = true;
                Cells[y, Width - 1].IsWall = true;
            }
        }

        // Cells[y,x].IsBordor 세팅
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
                    if (!Utility.InBounds(this, nx, ny)) {
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
}
