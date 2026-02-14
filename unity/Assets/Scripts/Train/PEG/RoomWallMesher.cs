using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Train.PEG {
    public static class RoomWallMesher {
        public static GameObject Build(
            Map map,
            Transform parent,
            Material mat,
            float wallHeight,
            float wallThickness
        ) {
            if (map == null || map.Rooms == null || map.Rooms.Count == 0) {
                return null;
            }

            wallHeight = Mathf.Max(0.01f, wallHeight);
            wallThickness = Mathf.Max(0.01f, wallThickness);

            Dictionary<Vector2Int, List<Vector2Int>> graph = new();
            Dictionary<EdgeKey, float> edgeBaseY = new();

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                ref Cell cell = ref map.Cells[y, x];
                if (cell.roomId == -1 || cell.isWall) {
                    continue;
                }

                Vector2Int p = new(x, y);

                for (int i = 0; i < Utility.Cardinal.Length; i++) {
                    Vector2Int d = Utility.Cardinal[i];
                    Vector2Int n = p + d;

                    bool neighborInBounds = map.Bounds.Contains(n);

                    if (neighborInBounds && map.GetCell(n).isRoad) {
                        continue; // door
                    }

                    bool neighborIsRoomFloor =
                        neighborInBounds &&
                        !map.GetCell(n).isWall &&
                        map.GetCell(n).roomId != -1;

                    if (neighborIsRoomFloor) {
                        continue;
                    }

                    Vector2Int a, b;
                    EdgeCorners(p, d, out a, out b);

                    AddAdj(graph, a, b);

                    EdgeKey k = new(a, b);
                    if (edgeBaseY.TryGetValue(k, out float by)) {
                        edgeBaseY[k] = Mathf.Max(by, cell.height);
                    } else {
                        edgeBaseY[k] = cell.height;
                    }
                }
            }

            if (graph.Count == 0) {
                return null;
            }

            List<List<Vector2Int>> chains = ExtractChains(graph);

            if (chains.Count == 0) {
                return null;
            }

            List<Vector3> verts = new();
            List<int> tris = new();

            float halfT = wallThickness * 0.5f;

            for (int ci = 0; ci < chains.Count; ci++) {
                List<Vector2Int> chain = chains[ci];
                if (chain.Count < 2) {
                    continue;
                }

                for (int i = 0; i < chain.Count - 1; i++) {
                    Vector2Int va = chain[i];
                    Vector2Int vb = chain[i + 1];
                    if (va == vb) {
                        continue;
                    }

                    EdgeKey k = new(va, vb);
                    if (!edgeBaseY.TryGetValue(k, out float baseY)) {
                        baseY = 0f;
                    }

                    Vector3 wa = CornerWorld(map, va);
                    Vector3 wb = CornerWorld(map, vb);

                    Vector3 a = new(wa.x, 0f, wa.z);
                    Vector3 b = new(wb.x, 0f, wb.z);

                    Vector3 dir = b - a;
                    float len = dir.magnitude;
                    if (len < 1e-5f) {
                        continue;
                    }

                    dir /= len;

                    Vector3 mid = (a + b) * 0.5f;
                    mid.y = baseY;

                    Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                    Matrix4x4 m = Matrix4x4.TRS(mid, rot, Vector3.one);

                    int v0 = verts.Count;

                    AddBoxSegment(
                        verts,
                        tris,
                        m,
                        halfT,
                        wallHeight,
                        len * 0.5f
                    );

                    // no need to stitch; segment boxes share space ok
                }
            }

            if (verts.Count == 0 || tris.Count == 0) {
                return null;
            }

            Mesh mesh = new();
            if (verts.Count > 65000) {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new("RoomWalls_Mesh");
            go.transform.SetParent(parent, false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            if (mat != null) {
                mr.sharedMaterial = mat;
            }

            return go;
        }

        private static void AddBoxSegment(
            List<Vector3> verts,
            List<int> tris,
            Matrix4x4 m,
            float halfThickness,
            float height,
            float halfLength
        ) {
            // local: X=thickness, Y=up, Z=length
            Vector3 p0 = m.MultiplyPoint3x4(new Vector3(-halfThickness, 0f, -halfLength));
            Vector3 p1 = m.MultiplyPoint3x4(new Vector3(halfThickness, 0f, -halfLength));
            Vector3 p2 = m.MultiplyPoint3x4(new Vector3(halfThickness, 0f, halfLength));
            Vector3 p3 = m.MultiplyPoint3x4(new Vector3(-halfThickness, 0f, halfLength));

            Vector3 p4 = m.MultiplyPoint3x4(new Vector3(-halfThickness, height, -halfLength));
            Vector3 p5 = m.MultiplyPoint3x4(new Vector3(halfThickness, height, -halfLength));
            Vector3 p6 = m.MultiplyPoint3x4(new Vector3(halfThickness, height, halfLength));
            Vector3 p7 = m.MultiplyPoint3x4(new Vector3(-halfThickness, height, halfLength));

            int i0 = verts.Count;
            verts.Add(p0);
            verts.Add(p1);
            verts.Add(p2);
            verts.Add(p3);
            verts.Add(p4);
            verts.Add(p5);
            verts.Add(p6);
            verts.Add(p7);

            // sides + top (no bottom)
            AddQuad(tris, i0 + 0, i0 + 4, i0 + 5, i0 + 1); // -Z
            AddQuad(tris, i0 + 1, i0 + 5, i0 + 6, i0 + 2); // +X
            AddQuad(tris, i0 + 2, i0 + 6, i0 + 7, i0 + 3); // +Z
            AddQuad(tris, i0 + 3, i0 + 7, i0 + 4, i0 + 0); // -X
            AddQuad(tris, i0 + 4, i0 + 7, i0 + 6, i0 + 5); // top
        }

        private static void AddQuad(List<int> tris, int a, int b, int c, int d) {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
            tris.Add(a);
            tris.Add(c);
            tris.Add(d);
        }

        private static void AddAdj(Dictionary<Vector2Int, List<Vector2Int>> g, Vector2Int a, Vector2Int b) {
            if (!g.TryGetValue(a, out List<Vector2Int> la)) {
                la = new List<Vector2Int>(2);
                g[a] = la;
            }

            if (!g.TryGetValue(b, out List<Vector2Int> lb)) {
                lb = new List<Vector2Int>(2);
                g[b] = lb;
            }

            la.Add(b);
            lb.Add(a);
        }

        private static List<List<Vector2Int>> ExtractChains(Dictionary<Vector2Int, List<Vector2Int>> graph) {
            HashSet<(Vector2Int, Vector2Int)> used = new();
            List<List<Vector2Int>> chains = new();

            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> kv in graph) {
                Vector2Int start = kv.Key;
                List<Vector2Int> outs = kv.Value;

                for (int i = 0; i < outs.Count; i++) {
                    Vector2Int to = outs[i];
                    if (used.Contains((start, to))) {
                        continue;
                    }

                    List<Vector2Int> chain = new(64);
                    Vector2Int prev = start;
                    Vector2Int cur = start;
                    Vector2Int next = to;

                    chain.Add(cur);
                    Mark(used, cur, next);

                    int guard = 0;
                    while (guard++ < 100000) {
                        cur = next;
                        chain.Add(cur);

                        if (!graph.TryGetValue(cur, out List<Vector2Int> list) || list.Count == 0) {
                            break;
                        }

                        Vector2Int picked;
                        if (!PickNext(list, prev, cur, used, out picked)) {
                            break;
                        }

                        prev = cur;
                        next = picked;

                        if (used.Contains((prev, next))) {
                            break;
                        }

                        Mark(used, prev, next);

                        if (next == chain[0]) {
                            chain.Add(chain[0]);
                            break;
                        }
                    }

                    if (chain.Count >= 2) {
                        chains.Add(chain);
                    }
                }
            }

            return chains;

            static void Mark(HashSet<(Vector2Int, Vector2Int)> u, Vector2Int a, Vector2Int b) {
                u.Add((a, b));
                u.Add((b, a));
            }

            static bool PickNext(
                List<Vector2Int> list,
                Vector2Int prev,
                Vector2Int cur,
                HashSet<(Vector2Int, Vector2Int)> u,
                out Vector2Int next
            ) {
                for (int i = 0; i < list.Count; i++) {
                    Vector2Int cand = list[i];
                    if (cand == prev) {
                        continue;
                    }

                    if (u.Contains((cur, cand))) {
                        continue;
                    }

                    next = cand;
                    return true;
                }

                for (int i = 0; i < list.Count; i++) {
                    Vector2Int cand = list[i];
                    if (cand == prev) {
                        continue;
                    }

                    next = cand;
                    return true;
                }

                next = default;
                return false;
            }
        }

        private static void EdgeCorners(Vector2Int cell, Vector2Int dir, out Vector2Int a, out Vector2Int b) {
            int x = cell.x;
            int y = cell.y;

            if (dir == Vector2Int.up) {
                a = new Vector2Int(x, y + 1);
                b = new Vector2Int(x + 1, y + 1);
                return;
            }

            if (dir == Vector2Int.right) {
                a = new Vector2Int(x + 1, y + 1);
                b = new Vector2Int(x + 1, y);
                return;
            }

            if (dir == Vector2Int.down) {
                a = new Vector2Int(x + 1, y);
                b = new Vector2Int(x, y);
                return;
            }

            a = new Vector2Int(x, y);
            b = new Vector2Int(x, y + 1);
        }

        private static Vector3 CornerWorld(Map map, Vector2Int corner) {
            float wx = map.Origin.x + (corner.x * map.CellSize);
            float wz = map.Origin.z + (corner.y * map.CellSize);
            return new Vector3(wx, 0f, wz);
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey> {
            public readonly Vector2Int A;
            public readonly Vector2Int B;

            public EdgeKey(Vector2Int a, Vector2Int b) {
                if (a.x < b.x || (a.x == b.x && a.y <= b.y)) {
                    A = a;
                    B = b;
                } else {
                    A = b;
                    B = a;
                }
            }

            public bool Equals(EdgeKey other) => A.Equals(other.A) && B.Equals(other.B);

            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(A, B);
        }
    }
}
