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

                float baseH = GetWallBaseHeightFromNeighbors(map, x, y);

                Vector3 pos = CellCenterWorld(map, x, y);
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

                bool isRoad = cell.IsRoad;
                bool isRoom = cell.RoomId != -1;

                bool roomTouchesRoad = isRoom && TouchesRoad(map, x, y);

                float thickness =
                    isRoad || roomTouchesRoad
                        ? ComputeSupportThickness(map, x, y, _floorThickness)
                        : _floorThickness;

                float topY = cell.Height;

                Vector3 pos = CellCenterWorld(map, x, y);
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

        private static bool TouchesRoad(MapData map, int x, int y) =>
            IsRoadAt(map, x + 1, y) ||
            IsRoadAt(map, x - 1, y) ||
            IsRoadAt(map, x, y + 1) ||
            IsRoadAt(map, x, y - 1);

        private static bool IsRoadAt(MapData map, int x, int y) {
            if (!map.InBounds(x, y)) {
                return false;
            }

            return map.Cells[y, x].IsRoad;
        }

        private static float ComputeSupportThickness(MapData map, int x, int y, float baseThickness) {
            // Supports only matter where there's a "fall edge" around.
            if (!map.IsExposedEdge(x, y)) {
                return Mathf.Max(0.01f, baseThickness);
            }

            float topY = map.Cells[y, x].Height;

            float minNeighborTop = float.PositiveInfinity;
            Try(x + 1, y);
            Try(x - 1, y);
            Try(x, y + 1);
            Try(x, y - 1);

            if (float.IsPositiveInfinity(minNeighborTop)) {
                return Mathf.Max(0.01f, baseThickness);
            }

            float extraDown = Mathf.Max(0f, topY - minNeighborTop);
            return Mathf.Max(0.01f, baseThickness + extraDown);

            void Try(int nx, int ny) {
                if (!map.InBounds(nx, ny)) {
                    return;
                }

                Cell n = map.Cells[ny, nx];
                if (n.IsWall) {
                    return;
                }

                minNeighborTop = Mathf.Min(minNeighborTop, n.Height);
            }
        }


        public void PlaceSpawnMarkers(MapData map, Transform zombieMarker, Transform targetMarker) {
            if (map.Rooms == null || map.Rooms.Count < 2) {
                Debug.LogWarning("[Visualizer] Not enough rooms for spawn/target.");
                return;
            }

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

            Vector3 zombiePos = CellTopWorld(map, zc.x, zc.y);
            Vector3 targetPos = CellTopWorld(map, tc.x, tc.y);

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

        private static Vector3 CellCenterWorld(MapData map, int x, int y) {
            float wx = map.Origin.x + ((x + 0.5f) * map.CellSize);
            float wz = map.Origin.z + ((y + 0.5f) * map.CellSize);
            return new Vector3(wx, 0f, wz);
        }

        private static Vector3 CellTopWorld(MapData map, int x, int y) {
            Vector3 p = CellCenterWorld(map, x, y);
            p.y = map.InBounds(x, y) ? map.Cells[y, x].Height : 0f;
            return p;
        }

        private static float GetWallBaseHeightFromNeighbors(MapData map, int x, int y) {
            float best = float.NegativeInfinity;

            Try(x + 1, y);
            Try(x - 1, y);
            Try(x, y + 1);
            Try(x, y - 1);

            return float.IsNegativeInfinity(best) ? 0f : best;

            void Try(int nx, int ny) {
                if (!map.InBounds(nx, ny)) {
                    return;
                }

                Cell n = map.Cells[ny, nx];
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
}
