using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    public class MapData {
        public int Width;
        public int Height;
        public float CellSize;
        public Vector3 Origin;

        public Cell[,] Cells;
        public List<Room> Rooms;

        public MapData(int width, int height, float cellSize) {
            Width = width;
            Height = height;
            CellSize = cellSize;

            Origin = GetGridOrigin(width, height, cellSize);
            Cells = new Cell[height, width];
            Rooms = new List<Room>();

            InitializeAllWalls();
        }

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

        public void ComputeBordorWalls() {
            if (Cells == null) {
                return;
            }

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) {
                ref Cell cell = ref Cells[y, x];

                if (!cell.IsWall) {
                    cell.IsBordor = false;
                    continue;
                }

                bool touchesFloor = false;
                Vector2Int p = new(x, y);

                foreach (Vector2Int dir in GridDirections.Cardinal) {
                    Vector2Int n = p + dir;
                    if (!InBounds(n)) {
                        continue;
                    }

                    if (!Cells[n.y, n.x].IsWall) {
                        touchesFloor = true;
                        break;
                    }
                }

                cell.IsBordor = touchesFloor;
            }
        }

        public bool InBounds(Vector2Int p) => (uint)p.x < (uint)Width && (uint)p.y < (uint)Height;

        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        public bool IsWallOrOob(Vector2Int p) {
            if (!InBounds(p)) {
                return true;
            }

            return Cells[p.y, p.x].IsWall;
        }

        public bool IsExposedEdge(Vector2Int p) {
            foreach (Vector2Int dir in GridDirections.Cardinal) {
                if (IsWallOrOob(p + dir)) {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 GetGridOrigin(int width, int height, float cellWorldSize) {
            float totalW = width * cellWorldSize;
            float totalH = height * cellWorldSize;

            return new Vector3(
                -(totalW * 0.5f),
                0f,
                -(totalH * 0.5f)
            );
        }
    }
}
