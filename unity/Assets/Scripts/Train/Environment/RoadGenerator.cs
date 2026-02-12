using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Train.Environment {
    public class RoadGenerator {
        public void ConnectRoomsAndRoadHeight(
            Map map,
            Random rng,
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

                float fromH = Utility.LevelToHeight(a.heightLevel, levelStepHeight);
                float toH = Utility.LevelToHeight(b.heightLevel, levelStepHeight);

                List<Vector2Int> roadPath = BuildRoadPathCells(a, b, rng, randomizeLTurnOrder);

                ApplyRoadWithConstantStepRise(map, roadPath, fromH, toH, roadWidth);
            }
        }

        private static List<Vector2Int> BuildRoadPathCells(
            Room a,
            Room b,
            Random rng,
            bool randomizeLTurnOrder
        ) {
            Vector2Int doorA = a.GetDoorCell(b.center);
            Vector2Int doorB = b.GetDoorCell(a.center);

            bool xThenY = randomizeLTurnOrder ? rng.NextDouble() < 0.5 : true;

            List<Vector2Int> path = new(
                Mathf.Abs(doorA.x - doorB.x) + Mathf.Abs(doorA.y - doorB.y) + 2
            );

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

            // room 셀(door 포함)은 높이를 건드리지 않으니까,
            // 실제로 칠하는 셀만 기준으로 step을 잡아야 마지막 튐이 없어짐.
            List<Vector2Int> paint = new(roadCells.Count);
            for (int i = 0; i < roadCells.Count; i++) {
                Vector2Int p = roadCells[i];
                if (!map.Bounds.Contains(p)) {
                    continue;
                }

                if (map.GetCell(p).roomId != -1) {
                    continue;
                }

                paint.Add(p);
            }

            if (paint.Count == 0) {
                return;
            }

            if (paint.Count == 1) {
                PaintRoadCell(map, paint[0], fromHeight, roadWidth);
                return;
            }

            int segments = paint.Count - 1;
            float stepRise = (toHeight - fromHeight) / segments;

            for (int i = 0; i < paint.Count; i++) {
                float h = fromHeight + (stepRise * i);
                PaintRoadCell(map, paint[i], h, roadWidth);
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

                if (cell.roomId == -1) {
                    cell.isRoad = true;
                    cell.height = roadTopHeight;
                }
            }
        }

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
    }
}
