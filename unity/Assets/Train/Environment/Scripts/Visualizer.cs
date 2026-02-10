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

    public void Rebuild(Environment.MapData data) {
        ClearAll();
        RebuildFloors(data);
        RebuildWalls(data);
    }

    public void RebuildWalls(Environment.MapData data) {
        ClearSpawnedWalls();

        bool[,] wallMatrix = data.WallMatrix;
        for (int y = 0; y < data.Height; y++)
        for (int x = 0; x < data.Width; x++) {
            if (!wallMatrix[y, x]) {
                continue;
            }

            Environment.Cell c = new(x, y);

            float baseH = GetWallBaseHeightFromNeighbors(data, c);
            Vector3 pos = CellCenterWorld(data, c);
            pos.y = baseH + _wallCenterY;

            GameObject w = Object.Instantiate(_wallPrefab, pos, Quaternion.identity, _parent);
            w.name = $"Wall_{x}_{y}";
            _spawnedWalls.Add(w);
        }
    }

    public void RebuildFloors(Environment.MapData data) {
        ClearSpawnedFloors();

        bool[,] wallMatrix = data.WallMatrix;
        float[,] height = data.TileHeight;

        for (int y = 0; y < data.Height; y++)
        for (int x = 0; x < data.Width; x++) {
            if (wallMatrix[y, x]) {
                continue;
            }

            float h = height[y, x];

            Environment.Cell c = new(x, y);
            Vector3 pos = CellCenterWorld(data, c);
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

    public void PlaceSpawnMarkers(Environment.MapData data, Transform zombieMarker, Transform targetMarker) {
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

        Environment.Cell zombieCell = new(data.Rooms[a].center.x, data.Rooms[a].center.y);
        Environment.Cell targetCell = new(data.Rooms[b].center.x, data.Rooms[b].center.y);

        Vector3 zombiePos = CellTopWorld(data, zombieCell);
        Vector3 targetPos = CellTopWorld(data, targetCell);

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

    private static Vector3 CellCenterWorld(Environment.MapData data, Environment.Cell c) {
        float wx = data.Origin.x + ((c.X + 0.5f) * data.CellSize);
        float wz = data.Origin.z + ((c.Y + 0.5f) * data.CellSize);
        return new Vector3(wx, 0f, wz);
    }

    private static Vector3 CellTopWorld(Environment.MapData data, Environment.Cell c) {
        Vector3 p = CellCenterWorld(data, c);
        float h = data.TileHeight != null ? data.TileHeight[c.Y, c.X] : 0f;
        p.y = h;
        return p;
    }

    private static float GetWallBaseHeightFromNeighbors(Environment.MapData data, Environment.Cell c) {
        float best = float.NegativeInfinity;

        Try(new Environment.Cell(c.X + 1, c.Y));
        Try(new Environment.Cell(c.X - 1, c.Y));
        Try(new Environment.Cell(c.X, c.Y + 1));
        Try(new Environment.Cell(c.X, c.Y - 1));

        return float.IsNegativeInfinity(best) ? 0f : best;

        void Try(Environment.Cell n) {
            if (n.X < 0 || n.X >= data.Width || n.Y < 0 || n.Y >= data.Height) {
                return;
            }

            if (data.WallMatrix[n.Y, n.X]) {
                return;
            }

            best = Mathf.Max(best, data.TileHeight[n.Y, n.X]);
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
