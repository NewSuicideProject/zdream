using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Train.Environment {
    public class RoomGenerator {
        private readonly OrganicShaper _organicShaper;
        public RoomGenerator(OrganicShaper organicShaper) => _organicShaper = organicShaper;

        public void PlaceRooms(
            Map map,
            Random rng,
            int roomCount,
            int maxRoomRerolls,
            float circleRoomChance,
            RectInt rectSizeRange, // x=minW, y=minH, width=maxW, height=maxH
            int circleMinR,
            int circleMaxR,
            int roomPadding,
            int minRoomLevel,
            int maxRoomLevel
        ) {
            int tries = 0;
            int placed = 0;
            int maxTries = maxRoomRerolls * roomCount;

            while (placed < roomCount && tries < maxTries) {
                tries++;

                bool makeCircle = rng.NextDouble() < circleRoomChance;

                Room room = makeCircle
                    ? new Room(map, rng, placed, circleMinR, circleMaxR, minRoomLevel, maxRoomLevel)
                    : new Room(
                        map,
                        rng,
                        placed,
                        rectSizeRange.x,
                        rectSizeRange.width,
                        rectSizeRange.y,
                        rectSizeRange.height,
                        minRoomLevel,
                        maxRoomLevel
                    );

                if (!room.IsValid) {
                    continue;
                }

                if (!RoomFitsAndDoesntOverlap(map, room, roomPadding)) {
                    continue;
                }

                _organicShaper.Organic(room, rng);

                map.Rooms.Add(room);
                placed++;
            }
        }

        public void AssignHeightsByDistanceMst(
            Map map,
            Random rng,
            int minLevel,
            int maxLevel
        ) {
            int n = map.Rooms.Count;
            if (n <= 0) {
                return;
            }

            if (n == 1) {
                map.Rooms[0].heightLevel = Mathf.Clamp(0, minLevel, maxLevel);
                return;
            }

            List<(int a, int b, int w)> edges = new(n * (n - 1) / 2);
            for (int i = 0; i < n; i++) {
                Vector2Int ca = map.Rooms[i].center;
                for (int j = i + 1; j < n; j++) {
                    Vector2Int cb = map.Rooms[j].center;
                    int manhattan = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y);
                    edges.Add((i, j, manhattan));
                }
            }

            edges.Sort((e1, e2) => e1.w.CompareTo(e2.w));

            int[] p = new int[n];
            int[] r = new int[n];
            for (int i = 0; i < n; i++) {
                p[i] = i;
            }

            int Find(int x) {
                while (p[x] != x) {
                    p[x] = p[p[x]];
                    x = p[x];
                }

                return x;
            }

            bool Union(int a, int b) {
                int ra = Find(a), rb = Find(b);
                if (ra == rb) {
                    return false;
                }

                if (r[ra] < r[rb]) {
                    p[ra] = rb;
                } else if (r[ra] > r[rb]) {
                    p[rb] = ra;
                } else {
                    p[rb] = ra;
                    r[ra]++;
                }

                return true;
            }

            List<int>[] adj = new List<int>[n];
            for (int i = 0; i < n; i++) {
                adj[i] = new List<int>(4);
            }

            int picked = 0;
            for (int i = 0; i < edges.Count && picked < n - 1; i++) {
                (int a, int b, int w) e = edges[i];
                if (!Union(e.a, e.b)) {
                    continue;
                }

                picked++;
                adj[e.a].Add(e.b);
                adj[e.b].Add(e.a);
            }

            int root = rng.Next(n);
            for (int i = 0; i < n; i++) {
                map.Rooms[i].heightLevel = int.MinValue;
            }

            map.Rooms[root].heightLevel = rng.Next(minLevel, maxLevel + 1);

            Queue<int> q = new(n);
            q.Enqueue(root);

            while (q.Count > 0) {
                int cur = q.Dequeue();
                int curLevel = map.Rooms[cur].heightLevel;

                for (int k = 0; k < adj[cur].Count; k++) {
                    int nb = adj[cur][k];
                    if (map.Rooms[nb].heightLevel != int.MinValue) {
                        continue;
                    }

                    Vector2Int a = map.Rooms[cur].center;
                    Vector2Int b = map.Rooms[nb].center;
                    int dist = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

                    int allowed = Mathf.Max(1, dist / 4);
                    int delta = rng.Next(-allowed, allowed + 1);

                    map.Rooms[nb].heightLevel =
                        Mathf.Clamp(curLevel + delta, minLevel, maxLevel);

                    q.Enqueue(nb);
                }
            }
        }


        public void WriteRoomToGrid(Map map, Room room, float levelStepHeight) {
            float roomHeight = Utility.LevelToHeight(room.heightLevel, levelStepHeight);

            foreach (Vector2Int c in room.Floors) {
                if (!map.Bounds.Contains(c)) {
                    continue;
                }

                ref Cell cell = ref map.Cells[c.y, c.x];
                cell.isWall = false;
                cell.roomId = room.id;
                cell.height = roomHeight;
            }
        }

        private static RectInt ExpandRect(RectInt r, int pad) =>
            new(r.xMin - pad, r.yMin - pad, r.width + (pad * 2), r.height + (pad * 2));

        private static bool RoomFitsAndDoesntOverlap(Map map, Room room, int roomPadding) {
            RectInt expanded = ExpandRect(room.bounds, roomPadding);

            if (expanded.xMin < 0 || expanded.yMin < 0 || expanded.xMax > map.Width || expanded.yMax > map.Height) {
                return false;
            }

            for (int i = 0; i < map.Rooms.Count; i++) {
                RectInt otherExpanded = ExpandRect(map.Rooms[i].bounds, roomPadding);
                if (expanded.Overlaps(otherExpanded)) {
                    return false;
                }
            }

            return true;
        }
    }
}
