using System;
using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    [Serializable]
    public class Room {
        public int id;
        public RectInt bounds;
        public Vector2Int center;
        public int heightLevel;

        public readonly HashSet<Vector2Int> FloorSet = new();

        public Room() { }

        public Room(
            MapData map,
            System.Random rng,
            int id,
            int rectMinW,
            int rectMaxW,
            int rectMinH,
            int rectMaxH,
            int minRoomLevel,
            int maxRoomLevel
        ) {
            int roomW = rng.Next(rectMinW, rectMaxW + 1);
            int roomH = rng.Next(rectMinH, rectMaxH + 1);

            const int border = 1;
            int maxLeftX = map.Width - roomW - border;
            int maxBottomY = map.Height - roomH - border;

            if (maxLeftX < border || maxBottomY < border) {
                this.id = -1;
                return;
            }

            int leftX = rng.Next(border, maxLeftX + 1);
            int bottomY = rng.Next(border, maxBottomY + 1);

            this.id = id;
            bounds = new RectInt(leftX, bottomY, roomW, roomH);
            center = new Vector2Int(leftX + (roomW / 2), bottomY + (roomH / 2));
            heightLevel = rng.Next(minRoomLevel, maxRoomLevel + 1);

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++) {
                FloorSet.Add(new Vector2Int(x, y));
            }
        }

        public Room(
            MapData map,
            System.Random rng,
            int id,
            int circleMinR,
            int circleMaxR,
            int minRoomLevel,
            int maxRoomLevel
        ) {
            int r = rng.Next(circleMinR, circleMaxR + 1);

            const int border = 1;
            int maxCX = map.Width - border - r - 1;
            int maxCY = map.Height - border - r - 1;

            if (maxCX < border + r || maxCY < border + r) {
                this.id = -1;
                return;
            }

            int cx = rng.Next(border + r, maxCX + 1);
            int cy = rng.Next(border + r, maxCY + 1);

            this.id = id;
            bounds = new RectInt(cx - r, cy - r, (r * 2) + 1, (r * 2) + 1);
            center = new Vector2Int(cx, cy);
            heightLevel = rng.Next(minRoomLevel, maxRoomLevel + 1);

            int rSq = r * r;

            for (int y = bounds.yMin; y <= bounds.yMax - 1; y++)
            for (int x = bounds.xMin; x <= bounds.xMax - 1; x++) {
                int dx = x - cx;
                int dy = y - cy;
                if ((dx * dx) + (dy * dy) <= rSq) {
                    FloorSet.Add(new Vector2Int(x, y));
                }
            }
        }

        public bool IsValid => id >= 0 && FloorSet.Count > 0;

        public bool ContainsFloor(int x, int y) {
            if (!Utility.InRect(bounds, x, y)) {
                return false;
            }

            return FloorSet.Contains(new Vector2Int(x, y));
        }

        public int CountFloorNeighbors4(Vector2Int c) {
            int n = 0;
            if (ContainsFloor(c.x + 1, c.y)) {
                n++;
            }

            if (ContainsFloor(c.x - 1, c.y)) {
                n++;
            }

            if (ContainsFloor(c.x, c.y + 1)) {
                n++;
            }

            if (ContainsFloor(c.x, c.y - 1)) {
                n++;
            }

            return n;
        }

        public List<Vector2Int> CollectBoundaryCells() {
            List<Vector2Int> result = new();

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++) {
                Vector2Int p = new(x, y);
                if (!FloorSet.Contains(p)) {
                    continue;
                }

                if (CountFloorNeighbors4(p) < 4) {
                    result.Add(p);
                }
            }

            return result;
        }

        public bool TryRemoveFloor(Vector2Int c) {
            if (!ContainsFloor(c.x, c.y)) {
                return false;
            }

            return FloorSet.Remove(c);
        }

        public bool TryAddFloor(Vector2Int c) {
            if (!Utility.InRect(bounds, c.x, c.y)) {
                return false;
            }

            return FloorSet.Add(c);
        }

        public Vector2Int PickBestDoorCell(Vector2Int toward) {
            Vector2Int best = center;
            int bestScore = int.MaxValue;

            foreach (Vector2Int c in FloorSet) {
                if (CountFloorNeighbors4(c) == 4) {
                    continue;
                }

                int score = Mathf.Abs(c.x - toward.x) + Mathf.Abs(c.y - toward.y);
                if (score < bestScore) {
                    bestScore = score;
                    best = c;
                }
            }

            return best;
        }
    }
}
