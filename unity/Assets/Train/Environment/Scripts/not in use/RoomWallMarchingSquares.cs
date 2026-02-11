using System.Collections.Generic;
using UnityEngine;

// this script isnt in use but developing rn --kretches
public sealed class RoomWallMarchingSquares {
    private const string FillerWallName = "FillerWall";

    private readonly float _cellSize;
    private readonly float _wallHeight;
    private readonly float _wallThickness;
    private readonly Material _material;

    public RoomWallMarchingSquares(float cellSize, float wallHeight, float wallThickness, Material material) {
        _cellSize = cellSize;
        _wallHeight = wallHeight;
        _wallThickness = wallThickness;
        _material = material;
    }

    public void Rebuild(bool[,] roomMask, Vector3 origin, Transform parent) {
        if (parent == null || roomMask == null) {
            return;
        }

        Transform prev = parent.Find(FillerWallName);
        if (prev != null) {
            Object.Destroy(prev.gameObject);
        }

        int h = roomMask.GetLength(0);
        int w = roomMask.GetLength(1);

        List<Segment> segments = MarchCornersOnly(roomMask, w, h, origin);
        if (segments.Count == 0) {
            return;
        }

        Mesh mesh = BuildExtrudedMesh(segments);

        GameObject go = new(FillerWallName);
        go.transform.SetParent(parent, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = _material;

        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    private struct Segment {
        public Vector3 A;
        public Vector3 B;

        public Segment(Vector3 a, Vector3 b) {
            A = a;
            B = b;
        }
    }

    private static readonly Vector2 _e0 = new(0.5f, 0f);
    private static readonly Vector2 _e1 = new(1f, 0.5f);
    private static readonly Vector2 _e2 = new(0.5f, 1f);
    private static readonly Vector2 _e3 = new(0f, 0.5f);

    private List<Segment> MarchCornersOnly(bool[,] mask, int w, int h, Vector3 origin) {
        List<Segment> segs = new();

        for (int y = 0; y < h - 1; y++)
        for (int x = 0; x < w - 1; x++) {
            bool bl = mask[y, x];
            bool br = mask[y, x + 1];
            bool tr = mask[y + 1, x + 1];
            bool tl = mask[y + 1, x];

            int c = 0;
            if (bl) {
                c |= 1;
            }

            if (br) {
                c |= 2;
            }

            if (tr) {
                c |= 4;
            }

            if (tl) {
                c |= 8;
            }

            float ox = origin.x + (x * _cellSize);
            float oz = origin.z + (y * _cellSize);

            Vector3 P(Vector2 p) => new(
                ox + (p.x * _cellSize),
                0f,
                oz + (p.y * _cellSize)
            );

            void AddCorner(Vector2 a, Vector2 b) => segs.Add(new Segment(P(a), P(b)));

            switch (c) {
                case 0:
                case 15:
                    break;

                case 1: AddCorner(_e3, _e0); break;
                case 2: AddCorner(_e0, _e1); break;
                case 4: AddCorner(_e1, _e2); break;
                case 8: AddCorner(_e2, _e3); break;

                case 5:
                    AddCorner(_e3, _e0);
                    AddCorner(_e1, _e2);
                    break;

                case 10:
                    AddCorner(_e0, _e1);
                    AddCorner(_e2, _e3);
                    break;

                case 7: AddCorner(_e3, _e2); break;
                case 11: AddCorner(_e1, _e2); break;
                case 13: AddCorner(_e0, _e1); break;
                case 14: AddCorner(_e3, _e0); break;

                case 3:
                case 6:
                case 9:
                case 12:
                    break;
            }
        }

        return segs;
    }

    private Mesh BuildExtrudedMesh(List<Segment> segments) {
        List<Vector3> verts = new();
        List<int> tris = new();
        float halfT = _wallThickness * 0.5f;

        void AddPrism(Vector3 a, Vector3 b) {
            Vector3 d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-4f) {
                return;
            }

            d /= len;

            Vector3 n = new(-d.z, 0f, d.x);

            Vector3 aL = a - (n * halfT);
            Vector3 aR = a + (n * halfT);
            Vector3 bL = b - (n * halfT);
            Vector3 bR = b + (n * halfT);

            Vector3 aLT = aL + (Vector3.up * _wallHeight);
            Vector3 aRT = aR + (Vector3.up * _wallHeight);
            Vector3 bLT = bL + (Vector3.up * _wallHeight);
            Vector3 bRT = bR + (Vector3.up * _wallHeight);

            int i = verts.Count;

            verts.Add(aL);
            verts.Add(aR);
            verts.Add(bR);
            verts.Add(bL);
            verts.Add(aLT);
            verts.Add(aRT);
            verts.Add(bRT);
            verts.Add(bLT);

            tris.AddRange(new int[] {
                i + 0, i + 2, i + 1, i + 0, i + 3, i + 2, i + 4, i + 5, i + 6, i + 4, i + 6, i + 7, i + 0, i + 4, i + 7,
                i + 0, i + 7, i + 3, i + 1, i + 2, i + 6, i + 1, i + 6, i + 5, i + 0, i + 1, i + 5, i + 0, i + 5, i + 4,
                i + 3, i + 7, i + 6, i + 3, i + 6, i + 2
            });
        }

        foreach (Segment s in segments) {
            AddPrism(s.A, s.B);
        }

        Mesh mesh = new();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
