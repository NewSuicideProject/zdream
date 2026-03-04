using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace PEG {
    [Serializable]
    public class Room {
        public int id;
        public RectInt bounds;
        public Vector2Int center;
        public int heightLevel;

        public readonly HashSet<Vector2Int> Floors = new();

        public Room() { }

        public Room(
            Map map,
            Random rng,
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

            for (int y = bounds.yMin; y < bounds.yMax; y++) {
                for (int x = bounds.xMin; x < bounds.xMax; x++) {
                    Floors.Add(new Vector2Int(x, y));
                }
            }
        }

        public Room(
            Map map,
            Random rng,
            int id,
            int circleMinR,
            int circleMaxR,
            int minRoomLevel,
            int maxRoomLevel
        ) {
            int r = rng.Next(circleMinR, circleMaxR + 1);

            const int border = 1;
            int maxCx = map.Width - border - r - 1;
            int maxCy = map.Height - border - r - 1;

            if (maxCx < border + r || maxCy < border + r) {
                this.id = -1;
                return;
            }

            int cx = rng.Next(border + r, maxCx + 1);
            int cy = rng.Next(border + r, maxCy + 1);

            this.id = id;
            bounds = new RectInt(cx - r, cy - r, (r * 2) + 1, (r * 2) + 1);
            center = new Vector2Int(cx, cy);
            heightLevel = rng.Next(minRoomLevel, maxRoomLevel + 1);

            int rSq = r * r;

            for (int y = bounds.yMin; y <= bounds.yMax - 1; y++) {
                for (int x = bounds.xMin; x <= bounds.xMax - 1; x++) {
                    int dx = x - cx;
                    int dy = y - cy;
                    if ((dx * dx) + (dy * dy) <= rSq) {
                        Floors.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        public bool IsValid => id >= 0 && Floors.Count > 0;


        public bool IsBorderCell(Vector2Int candidate) {
            if (!Floors.Contains(candidate)) {
                return false;
            }

            foreach (Vector2Int direction in Utility.Cardinal) {
                Vector2Int neighbor = candidate + direction;
                if (!Floors.Contains(neighbor)) {
                    return true;
                }
            }

            return false;
        }

        public List<Vector2Int> GetBorderCells() {
            List<Vector2Int> result = new();

            for (int y = bounds.yMin; y < bounds.yMax; y++) {
                for (int x = bounds.xMin; x < bounds.xMax; x++) {
                    Vector2Int p = new(x, y);
                    if (IsBorderCell(p)) {
                        result.Add(p);
                    }
                }
            }

            return result;
        }

        public int GetNeighborCount(Vector2Int candidate) {
            int count = 0;

            foreach (Vector2Int direction in Utility.Cardinal) {
                if (Floors.Contains(candidate + direction)) {
                    count++;
                }
            }

            return count;
        }

        public Vector2Int GetDoorCell(Vector2Int toward) {
            Vector2Int best = center;
            int bestScore = int.MaxValue;

            foreach (Vector2Int c in Floors) {
                if (!IsBorderCell(c)) {
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
