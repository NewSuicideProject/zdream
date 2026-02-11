using System;
using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    [Serializable]
    public sealed class Room {
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
            int minLeftX = border;
            int minBottomY = border;

            int maxLeftX = map.Width - roomW - border;
            int maxBottomY = map.Height - roomH - border;

            if (maxLeftX < minLeftX || maxBottomY < minBottomY) {
                this.id = -1;
                return;
            }

            int leftX = rng.Next(minLeftX, maxLeftX + 1);
            int bottomY = rng.Next(minBottomY, maxBottomY + 1);

            int centerX = leftX + (roomW / 2);
            int centerY = bottomY + (roomH / 2);

            this.id = id;
            bounds = new RectInt(leftX, bottomY, roomW, roomH);
            center = new Vector2Int(centerX, centerY);
            heightLevel = rng.Next(minRoomLevel, maxRoomLevel + 1);

            int rightXEx = leftX + roomW;
            int topYEx = bottomY + roomH;

            for (int y = bottomY; y < topYEx; y++)
            for (int x = leftX; x < rightXEx; x++) {
                FloorSet.Add(new Vector2Int(x, y));
            }
        }

        // Circle Room 생성자
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
            int diameter = (r * 2) + 1;

            const int border = 1;
            int minCenterX = border + r;
            int minCenterY = border + r;

            int maxCenterX = map.Width - border - r - 1;
            int maxCenterY = map.Height - border - r - 1;

            if (maxCenterX < minCenterX || maxCenterY < minCenterY) {
                this.id = -1;
                return;
            }

            int cx = rng.Next(minCenterX, maxCenterX + 1);
            int cy = rng.Next(minCenterY, maxCenterY + 1);

            int left = cx - r;
            int bottom = cy - r;

            this.id = id;
            bounds = new RectInt(left, bottom, diameter, diameter);
            center = new Vector2Int(cx, cy);
            heightLevel = rng.Next(minRoomLevel, maxRoomLevel + 1);

            int rSq = r * r;

            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++) {
                int ox = x - cx;
                int oy = y - cy;
                if ((ox * ox) + (oy * oy) <= rSq) {
                    FloorSet.Add(new Vector2Int(x, y));
                }
            }
        }

        public bool IsValid => id >= 0 && FloorSet.Count > 0;

        public bool IsBoundaryCell(Vector2Int c) =>
            !FloorSet.Contains(new Vector2Int(c.x + 1, c.y)) ||
            !FloorSet.Contains(new Vector2Int(c.x - 1, c.y)) ||
            !FloorSet.Contains(new Vector2Int(c.x, c.y + 1)) ||
            !FloorSet.Contains(new Vector2Int(c.x, c.y - 1));

        public Vector2Int PickBestDoorCell(Vector2Int toward) {
            Vector2Int best = center;
            int bestScore = int.MaxValue;

            foreach (Vector2Int c in FloorSet) {
                if (!IsBoundaryCell(c)) {
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
