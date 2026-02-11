using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoadGenerator {
    private readonly struct Edge {
        public readonly int A, B, W;

        public Edge(int a, int b, int w) {
            A = a;
            B = b;
            W = w;
        }
    }

    private sealed class DSU {
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

    public void ConnectRoomsMstAndPaintRoadHeights(
        MapData map,
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

    private static List<Vector2Int> BuildRoadPathCells(Room a, Room b, System.Random rng, bool randomizeLTurnOrder) {
        Vector2Int doorA = a.PickBestDoorCell(b.center);
        Vector2Int doorB = b.PickBestDoorCell(a.center);

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

        int x = start.x;
        int y = start.y;

        if (outCells.Count == 0 || outCells[^1].x != x || outCells[^1].y != y) {
            outCells.Add(new Vector2Int(x, y));
        }

        while (x != end.x) {
            x += dx;
            outCells.Add(new Vector2Int(x, y));
        }

        while (y != end.y) {
            y += dy;
            outCells.Add(new Vector2Int(x, y));
        }
    }

    private static void ApplyRoadWithConstantStepRise(
        MapData map,
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

        PaintRoadCell(map, roadCells[^1], toHeight, roadWidth);
    }

    private static void PaintRoadCell(MapData map, Vector2Int c, float height, int roadWidth) {
        int halfA = (roadWidth - 1) / 2;
        int halfB = roadWidth / 2;

        for (int oy = -halfA; oy <= halfB; oy++)
        for (int ox = -halfA; ox <= halfB; ox++) {
            int tx = c.x + ox;
            int ty = c.y + oy;
            if (!Utility.InBounds(map, tx, ty)) {
                continue;
            }

            ref Cell cell = ref map.Cells[ty, tx];
            cell.IsWall = false;
            cell.Height = height;
        }
    }
}
