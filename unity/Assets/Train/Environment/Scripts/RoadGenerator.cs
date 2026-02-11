using System;
using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    public class RoadGenerator {
        private readonly struct Edge {
            public readonly int A, B, W;

            public Edge(int a, int b, int w) {
                A = a;
                B = b;
                W = w;
            }
        }

        private class DSU {
            private readonly int[] _p;
            private readonly int[] _r;

            public DSU(int n) {
                _p = new int[n];
                _r = new int[n];
                for (int i = 0; i < n; i++) {
                    _p[i] = i;
                }
            }

            private int Find(int x) {
                while (_p[x] != x) {
                    _p[x] = _p[_p[x]];
                    x = _p[x];
                }

                return x;
            }

            public bool Union(int a, int b) {
                int ra = Find(a), rb = Find(b);
                if (ra == rb) {
                    return false;
                }

                if (_r[ra] < _r[rb]) {
                    _p[ra] = rb;
                } else if (_r[ra] > _r[rb]) {
                    _p[rb] = ra;
                } else {
                    _p[rb] = ra;
                    _r[ra]++;
                }

                return true;
            }
        }

        public void ConnectRoomsAndRoadHeight(
            Map map,
            System.Random rng,
            int roadWidth,
            bool randomizeLTurnOrder,
            float levelStepHeight
        ) {
            int n = map.Rooms.Count;
            if (n <= 1) {
                return;
            }

            List<Edge> edges = new(n * (n - 1) / 2);

            for (int i = 0; i < n; i++) {
                Vector2Int ca = map.Rooms[i].center;
                for (int j = i + 1; j < n; j++) {
                    Vector2Int cb = map.Rooms[j].center;
                    int manhattan = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y);
                    edges.Add(new Edge(i, j, manhattan));
                }
            }

            edges.Sort(static (e1, e2) => e1.W.CompareTo(e2.W));

            DSU dsu = new(n);
            int picked = 0;

            for (int i = 0; i < edges.Count && picked < n - 1; i++) {
                Edge e = edges[i];
                if (!dsu.Union(e.A, e.B)) {
                    continue;
                }

                picked++;

                Room a = map.Rooms[e.A];
                Room b = map.Rooms[e.B];

                List<Vector2Int> roadPath = BuildRoadPathCells(a, b, rng, randomizeLTurnOrder);

                float fromH = Utility.LevelToHeight(a.heightLevel, levelStepHeight);
                float toH = Utility.LevelToHeight(b.heightLevel, levelStepHeight);

                ApplyRoadWithConstantStepRise(map, roadPath, fromH, toH, roadWidth);
            }
        }

        private static List<Vector2Int>
            BuildRoadPathCells(Room a, Room b, System.Random rng, bool randomizeLTurnOrder) {
            Vector2Int doorA = a.GetDoorCell(b.center);
            Vector2Int doorB = b.GetDoorCell(a.center);

            bool xThenY = randomizeLTurnOrder ? rng.NextDouble() < 0.5 : true;

            List<Vector2Int> path = new(Mathf.Abs(doorA.x - doorB.x) + Mathf.Abs(doorA.y - doorB.y) + 2);

            if (xThenY) {
                AppendLineCells(path, doorA, new Vector2Int(doorB.x, doorA.y));
                AppendLineCells(path, new Vector2Int(doorB.x, doorA.y), doorB);
            } else {
                AppendLineCells(path, doorA, new Vector2Int(doorA.x, doorB.y));
                AppendLineCells(path, new Vector2Int(doorA.x, doorB.y), doorB);
            }

            return path;
        }

        private static void AppendLineCells(List<Vector2Int> outCells, Vector2Int start, Vector2Int end) {
            int dx = Math.Sign(end.x - start.x);
            int dy = Math.Sign(end.y - start.y);

            Vector2Int p = start;

            if (outCells.Count == 0 || outCells[^1] != p) {
                outCells.Add(p);
            }

            while (p.x != end.x) {
                p = new Vector2Int(p.x + dx, p.y);
                outCells.Add(p);
            }

            while (p.y != end.y) {
                p = new Vector2Int(p.x, p.y + dy);
                outCells.Add(p);
            }
        }

        private static void ApplyRoadWithConstantStepRise(
            Map map,
            List<Vector2Int> roadCells,
            float fromHeight,
            float toHeight,
            int roadWidth
        ) {
            if (roadCells == null || roadCells.Count == 0) {
                return;
            }

            int segments = roadCells.Count - 1;
            if (segments <= 0) {
                PaintRoadCell(map, roadCells[0], fromHeight, roadWidth);
                return;
            }

            float stepRise = (toHeight - fromHeight) / segments;

            for (int i = 0; i < roadCells.Count; i++) {
                float h = fromHeight + (stepRise * i);
                PaintRoadCell(map, roadCells[i], h, roadWidth);
            }
        }

        private static void PaintRoadCell(Map map, Vector2Int c, float roadTopHeight, int roadWidth) {
            int halfA = (roadWidth - 1) / 2;
            int halfB = roadWidth / 2;

            for (int oy = -halfA; oy <= halfB; oy++)
            for (int ox = -halfA; ox <= halfB; ox++) {
                Vector2Int p = new(c.x + ox, c.y + oy);

                if (!map.Bounds.Contains(p)) {
                    continue;
                }

                ref Cell cell = ref map.Cells[p.y, p.x];

                cell.isWall = false;
                cell.isRoad = cell.roomId == -1;

                if (cell.roomId == -1) {
                    cell.height = roadTopHeight;
                }
            }
        }
    }
}
