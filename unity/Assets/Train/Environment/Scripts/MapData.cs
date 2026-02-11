using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    public class MapData {
        public readonly float CellSize;
        public Vector3 Origin;

        public RectInt Bounds;

        public int Width => Bounds.width;
        public int Height => Bounds.height;

        public Cell[,] Cells;
        public readonly List<Room> Rooms;

        public MapData(int width, int height, float cellSize) {
            CellSize = cellSize;

            Origin = GetGridOrigin(width, height, cellSize);
            Cells = new Cell[height, width];
            Rooms = new List<Room>();
            Bounds = new RectInt(0, 0, width, height);

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) {
                Cells[y, x] = new Cell(true);
            }
        }

        public void ApplyBorderWalls() {
            for (int x = 0; x < Width; x++) {
                Cells[0, x].isWall = true;
                Cells[Height - 1, x].isWall = true;
            }

            for (int y = 0; y < Height; y++) {
                Cells[y, 0].isWall = true;
                Cells[y, Width - 1].isWall = true;
            }
        }

        public void ComputeBorderWalls() {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) {
                ref Cell cell = ref Cells[y, x];

                if (!cell.isWall) {
                    cell.isBorder = false;
                    continue;
                }

                bool touchesFloor = false;
                Vector2Int p = new(x, y);

                foreach (Vector2Int dir in Utility.Cardinal) {
                    Vector2Int n = p + dir;
                    if (!Bounds.Contains(n) || Cells[n.y, n.x].isWall) {
                        continue;
                    }

                    touchesFloor = true;
                    break;
                }

                cell.isBorder = touchesFloor;
            }
        }

        public bool IsExposedEdge(Vector2Int p) {
            foreach (Vector2Int dir in Utility.Cardinal) {
                Vector2Int n = p + dir;
                if (!Bounds.Contains(n) || Cells[n.y, n.x].isWall) {
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
