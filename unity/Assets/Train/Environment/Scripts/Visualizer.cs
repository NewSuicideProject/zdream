using System.Collections.Generic;
using UnityEngine;

public sealed class Visualizer {
    private readonly Transform _parent;
    private readonly GameObject _wallPrefab;
    private readonly float _wallCenterY;

    private readonly GameObject _floorPrefab;
    private readonly float _floorThickness;

    private readonly List<GameObject> _spawnedWalls = new();
    private readonly List<GameObject> _spawnedFloors = new();

    public Visualizer(
        Transform parent,
        GameObject wallPrefab,
        float wallCenterY,
        GameObject floorPrefab,
        float floorThickness
    ) {
        _parent = parent;
        _wallPrefab = wallPrefab;
        _wallCenterY = wallCenterY;

        _floorPrefab = floorPrefab;
        _floorThickness = Mathf.Max(0.01f, floorThickness);
    }

    public void Rebuild(MapData data) {
        ClearAll();
        RebuildFloors(data);
        RebuildWalls(data);
    }

    public void RebuildWalls(MapData data) {
        ClearSpawnedWalls();

        for (int y = 0; y < data.Height; y++)
        for (int x = 0; x < data.Width; x++) {
            if (!data.Cells[y, x].IsBordor) {
                continue;
            }

            float baseH = GetWallBaseHeightFromNeighbors(data, x, y);

            Vector3 pos = CellCenterWorld(data, x, y);
            pos.y = baseH + _wallCenterY;

            GameObject w = Object.Instantiate(_wallPrefab, pos, Quaternion.identity, _parent);
            w.name = $"Wall_{x}_{y}";
            _spawnedWalls.Add(w);
        }
    }

    public void RebuildFloors(MapData data) {
        ClearSpawnedFloors();

        for (int y = 0; y < data.Height; y++)
        for (int x = 0; x < data.Width; x++) {
            if (data.Cells[y, x].IsWall) {
                continue;
            }

            float h = data.Cells[y, x].Height;

            Vector3 pos = CellCenterWorld(data, x, y);
            pos.y = h + (_floorThickness * 0.5f);

            GameObject f;
            if (_floorPrefab != null) {
                f = Object.Instantiate(_floorPrefab, pos, Quaternion.identity, _parent);
            } else {
                f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                f.transform.SetParent(_parent, true);
                f.transform.position = pos;
            }

            f.name = $"Floor_{x}_{y}";
            f.transform.localScale = new Vector3(data.CellSize, _floorThickness, data.CellSize);
            _spawnedFloors.Add(f);
        }
    }

    public void PlaceSpawnMarkers(MapData data, Transform zombieMarker, Transform targetMarker) {
        if (data.Rooms == null || data.Rooms.Count < 2) {
            Debug.LogWarning("[Visualizer] Not enough rooms for spawn/target.");
            return;
        }

        int a = 0, b = 1;
        long best = -1;

        for (int i = 0; i < data.Rooms.Count; i++)
        for (int j = i + 1; j < data.Rooms.Count; j++) {
            Vector2Int c1 = data.Rooms[i].center;
            Vector2Int c2 = data.Rooms[j].center;

            long dx = c1.x - c2.x;
            long dy = c1.y - c2.y;
            long d2 = (dx * dx) + (dy * dy);

            if (d2 > best) {
                best = d2;
                a = i;
                b = j;
            }
        }

        int zx = data.Rooms[a].center.x;
        int zy = data.Rooms[a].center.y;
        int tx = data.Rooms[b].center.x;
        int ty = data.Rooms[b].center.y;

        Vector3 zombiePos = CellTopWorld(data, zx, zy);
        Vector3 targetPos = CellTopWorld(data, tx, ty);

        if (zombieMarker != null) {
            zombieMarker.position = zombiePos;
        }

        if (targetMarker != null) {
            targetMarker.position = targetPos;
        }
    }

    public void ClearAll() {
        ClearSpawnedWalls();
        ClearSpawnedFloors();
    }

    private static Vector3 CellCenterWorld(MapData data, int x, int y) {
        float wx = data.Origin.x + ((x + 0.5f) * data.CellSize);
        float wz = data.Origin.z + ((y + 0.5f) * data.CellSize);
        return new Vector3(wx, 0f, wz);
    }

    private static Vector3 CellTopWorld(MapData data, int x, int y) {
        Vector3 p = CellCenterWorld(data, x, y);
        p.y = Utility.InBounds(data, x, y) ? data.Cells[y, x].Height : 0f; // ✅ changed
        return p;
    }

    private static float GetWallBaseHeightFromNeighbors(MapData data, int x, int y) {
        float best = float.NegativeInfinity;

        Try(x + 1, y);
        Try(x - 1, y);
        Try(x, y + 1);
        Try(x, y - 1);

        return float.IsNegativeInfinity(best) ? 0f : best;

        void Try(int nx, int ny) {
            if (!Utility.InBounds(data, nx, ny)) {
                // ✅ changed
                return;
            }

            Cell n = data.Cells[ny, nx];
            if (n.IsWall) {
                return;
            }

            best = Mathf.Max(best, n.Height);
        }
    }

    private void ClearSpawnedWalls() {
        for (int i = 0; i < _spawnedWalls.Count; i++) {
            if (_spawnedWalls[i] != null) {
                Object.Destroy(_spawnedWalls[i]);
            }
        }

        _spawnedWalls.Clear();
    }

    private void ClearSpawnedFloors() {
        for (int i = 0; i < _spawnedFloors.Count; i++) {
            if (_spawnedFloors[i] != null) {
                Object.Destroy(_spawnedFloors[i]);
            }
        }

        _spawnedFloors.Clear();
    }
}
