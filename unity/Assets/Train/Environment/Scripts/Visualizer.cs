using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    public class Visualizer {
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

        public void Rebuild(MapData map) {
            ClearAll();
            RebuildFloors(map);
            RebuildWalls(map);
        }

        public void RebuildWalls(MapData map) {
            ClearSpawnedWalls();

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                if (!map.Cells[y, x].IsBordor) {
                    continue;
                }

                float baseH = GetWallBaseHeightFromNeighbors(map, new Vector2Int(x, y));

                Vector3 pos = CellCenterWorld(map, new Vector2Int(x, y));
                pos.y = baseH + _wallCenterY;

                GameObject w = Object.Instantiate(_wallPrefab, pos, Quaternion.identity, _parent);
                w.name = $"Wall_{x}_{y}";
                _spawnedWalls.Add(w);
            }
        }

        public void RebuildFloors(MapData map) {
            ClearSpawnedFloors();

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                ref Cell cell = ref map.Cells[y, x];
                if (cell.IsWall) {
                    continue;
                }

                Vector2Int p = new(x, y);

                bool isRoom = cell.RoomId != -1;
                bool touchesRoad = isRoom && TouchesRoad(map, p);

                bool wantsSupport = cell.IsRoad || touchesRoad;

                float thickness = wantsSupport
                    ? ComputeSupportThickness(map, p, _floorThickness)
                    : _floorThickness;

                float topY = cell.Height;

                Vector3 pos = CellCenterWorld(map, p);
                pos.y = topY - (thickness * 0.5f);

                GameObject f;
                if (_floorPrefab != null) {
                    f = Object.Instantiate(_floorPrefab, pos, Quaternion.identity, _parent);
                } else {
                    f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    f.transform.SetParent(_parent, true);
                    f.transform.position = pos;
                }

                f.name = $"Floor_{x}_{y}";
                f.transform.localScale = new Vector3(map.CellSize, thickness, map.CellSize);
                _spawnedFloors.Add(f);
            }
        }

        private static bool TouchesRoad(MapData map, Vector2Int p) {
            foreach (Vector2Int dir in GridDirections.Cardinal) {
                if (IsRoadAt(map, p + dir)) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRoadAt(MapData map, Vector2Int p) {
            if (!map.InBounds(p)) {
                return false;
            }

            return map.Cells[p.y, p.x].IsRoad;
        }

        private static float ComputeSupportThickness(MapData map, Vector2Int p, float baseThickness) {
            if (!map.IsExposedEdge(p)) {
                return Mathf.Max(0.01f, baseThickness);
            }

            float topY = map.Cells[p.y, p.x].Height;

            float minNeighborTop = float.PositiveInfinity;
            foreach (Vector2Int dir in GridDirections.Cardinal) {
                Try(p + dir);
            }


            if (float.IsPositiveInfinity(minNeighborTop)) {
                return Mathf.Max(0.01f, baseThickness);
            }

            float extraDown = Mathf.Max(0f, topY - minNeighborTop);
            return Mathf.Max(0.01f, baseThickness + extraDown);

            void Try(Vector2Int n) {
                if (!map.InBounds(n)) {
                    return;
                }

                Cell c = map.Cells[n.y, n.x];
                if (c.IsWall) {
                    return;
                }

                minNeighborTop = Mathf.Min(minNeighborTop, c.Height);
            }
        }

        public void PlaceSpawnMarkers(MapData map, Transform zombieMarker, Transform targetMarker) {
            int a = 0, b = 1;
            long best = -1;

            for (int i = 0; i < map.Rooms.Count; i++)
            for (int j = i + 1; j < map.Rooms.Count; j++) {
                Vector2Int c1 = map.Rooms[i].center;
                Vector2Int c2 = map.Rooms[j].center;

                long dx = c1.x - c2.x;
                long dy = c1.y - c2.y;
                long d2 = (dx * dx) + (dy * dy);

                if (d2 > best) {
                    best = d2;
                    a = i;
                    b = j;
                }
            }

            Vector2Int zc = map.Rooms[a].center;
            Vector2Int tc = map.Rooms[b].center;

            Vector3 zombiePos = CellTopWorld(map, zc);
            Vector3 targetPos = CellTopWorld(map, tc);

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

        private static Vector3 CellCenterWorld(MapData map, Vector2Int p) {
            float wx = map.Origin.x + ((p.x + 0.5f) * map.CellSize);
            float wz = map.Origin.z + ((p.y + 0.5f) * map.CellSize);
            return new Vector3(wx, 0f, wz);
        }

        private static Vector3 CellTopWorld(MapData map, Vector2Int p) {
            Vector3 w = CellCenterWorld(map, p);
            w.y = map.InBounds(p) ? map.Cells[p.y, p.x].Height : 0f;
            return w;
        }

        private static float GetWallBaseHeightFromNeighbors(MapData map, Vector2Int p) {
            float best = float.NegativeInfinity;

            foreach (Vector2Int dir in GridDirections.Cardinal) {
                Try(p + dir);
            }


            return float.IsNegativeInfinity(best) ? 0f : best;

            void Try(Vector2Int n) {
                if (!map.InBounds(n)) {
                    return;
                }

                Cell c = map.Cells[n.y, n.x];
                if (c.IsWall) {
                    return;
                }

                best = Mathf.Max(best, c.Height);
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
}
