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

        public void Rebuild(Map map) {
            ClearAll();
            RebuildFloors(map);
            RebuildWalls(map);
        }

        public void RebuildWalls(Map map) {
            ClearSpawnedWalls();

            if (_wallPrefab == null) {
                Debug.LogError("[Visualizer] wallPrefab is null.");
                return;
            }

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                if (!map.Cells[y, x].isBorder) {
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

        public void RebuildFloors(Map map) {
            ClearSpawnedFloors();

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                ref Cell cell = ref map.Cells[y, x];
                if (cell.isWall) {
                    continue;
                }

                Vector2Int p = new(x, y);

                float topY = cell.height;
                float thickness = _floorThickness;

                bool isRoom = cell.roomId != -1;
                bool isRoad = cell.isRoad;
                bool isDoorCell = isRoom && TouchesRoad(map, p);

                if (isDoorCell) {
                    thickness = ComputeDoorSupportThickness(map, p, topY, _floorThickness);
                } else if (isRoad) {
                    thickness = ComputeSupportThicknessToMinNeighborFloor(map, p, topY, _floorThickness);
                } else {
                    thickness = _floorThickness;
                }

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

        private static bool TouchesRoad(Map map, Vector2Int p) {
            foreach (Vector2Int dir in Utility.Cardinal) {
                Vector2Int n = p + dir;
                if (!map.Bounds.Contains(n)) {
                    continue;
                }

                if (map.GetCell(n).isRoad) {
                    return true;
                }
            }

            return false;
        }

        private static float ComputeDoorSupportThickness(Map map, Vector2Int door, float topY, float baseThickness) {
            float minRoadTop = float.PositiveInfinity;

            foreach (Vector2Int dir in Utility.Cardinal) {
                Vector2Int n = door + dir;
                if (!map.Bounds.Contains(n)) {
                    continue;
                }

                Cell c = map.GetCell(n);
                if (c.isWall) {
                    continue;
                }

                if (!c.isRoad) {
                    continue;
                }

                minRoadTop = Mathf.Min(minRoadTop, c.height);
            }

            if (float.IsPositiveInfinity(minRoadTop)) {
                return Mathf.Max(0.01f, baseThickness);
            }

            float extraDown = Mathf.Max(0f, topY - minRoadTop);
            return Mathf.Max(0.01f, baseThickness + extraDown);
        }

        private static float ComputeSupportThicknessToMinNeighborFloor(Map map, Vector2Int p, float topY,
            float baseThickness) {
            float minNeighborTop = float.PositiveInfinity;

            foreach (Vector2Int dir in Utility.Cardinal) {
                Vector2Int n = p + dir;
                if (!map.Bounds.Contains(n)) {
                    continue;
                }

                Cell c = map.GetCell(n);
                if (c.isWall) {
                    continue;
                }

                minNeighborTop = Mathf.Min(minNeighborTop, c.height);
            }

            if (float.IsPositiveInfinity(minNeighborTop)) {
                return Mathf.Max(0.01f, baseThickness);
            }

            float extraDown = Mathf.Max(0f, topY - minNeighborTop);
            return Mathf.Max(0.01f, baseThickness + extraDown);
        }

        public void PlaceSpawnMarkers(Map map, Transform zombieMarker, Transform targetMarker) {
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

        private static Vector3 CellCenterWorld(Map map, Vector2Int p) {
            float wx = map.Origin.x + ((p.x + 0.5f) * map.CellSize);
            float wz = map.Origin.z + ((p.y + 0.5f) * map.CellSize);
            return new Vector3(wx, 0f, wz);
        }

        private static Vector3 CellTopWorld(Map map, Vector2Int p) {
            Vector3 w = CellCenterWorld(map, p);
            w.y = map.Bounds.Contains(p) ? map.Cells[p.y, p.x].height : 0f;
            return w;
        }

        private static float GetWallBaseHeightFromNeighbors(Map map, Vector2Int p) {
            float best = float.NegativeInfinity;

            foreach (Vector2Int dir in Utility.Cardinal) {
                Vector2Int n = p + dir;
                if (!map.Bounds.Contains(n)) {
                    continue;
                }

                Cell c = map.GetCell(n);
                if (c.isWall) {
                    continue;
                }

                best = Mathf.Max(best, c.height);
            }

            return float.IsNegativeInfinity(best) ? 0f : best;
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
