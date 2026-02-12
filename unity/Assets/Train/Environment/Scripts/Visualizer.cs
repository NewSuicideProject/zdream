using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    public class Visualizer {
        private readonly Transform _parent;
        private readonly GameObject _wallPrefab;
        private readonly float _wallCenterY;

        private readonly GameObject _floorPrefab;
        private readonly float _floorThickness;

        private readonly Material _roomWallMat;
        private readonly float _roomWallHeight;
        private readonly float _roomWallThickness;

        private readonly List<GameObject> _spawnedWalls = new();
        private readonly List<GameObject> _spawnedFloors = new();
        private GameObject _roomWallMesh;

        public Visualizer(
            Transform parent,
            GameObject wallPrefab,
            float wallCenterY,
            GameObject floorPrefab,
            float floorThickness,
            Material roomWallMat,
            float roomWallHeight,
            float roomWallThickness
        ) {
            _parent = parent;
            _wallPrefab = wallPrefab;
            _wallCenterY = wallCenterY;

            _floorPrefab = floorPrefab;
            _floorThickness = Mathf.Max(0.01f, floorThickness);

            _roomWallMat = roomWallMat;
            _roomWallHeight = Mathf.Max(0.01f, roomWallHeight);
            _roomWallThickness = Mathf.Max(0.01f, roomWallThickness);
        }

        public void Rebuild(Map map) {
            ClearAll();
            RebuildFloors(map);
            RebuildRoadWalls(map);
            RebuildRoomWallMesh(map);
        }

        private void RebuildRoadWalls(Map map) {
            ClearSpawnedWalls();

            if (_wallPrefab == null) {
                return;
            }

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                if (!map.Cells[y, x].isBorder) {
                    continue;
                }

                Vector2Int p = new(x, y);
                if (!WallTouchesRoad(map, p)) {
                    continue;
                }

                float baseY = GetWallBaseHeightFromNeighbors(map, p);

                Vector3 pos = CellCenterWorld(map, p);
                pos.y = baseY + _wallCenterY;

                GameObject w = Object.Instantiate(_wallPrefab, pos, Quaternion.identity, _parent);
                w.name = $"Wall_{x}_{y}";
                _spawnedWalls.Add(w);
            }
        }

        private void RebuildRoomWallMesh(Map map) {
            if (_roomWallMesh != null) {
                Object.Destroy(_roomWallMesh);
                _roomWallMesh = null;
            }

            _roomWallMesh = RoomWallMesher.Build(
                map,
                _parent,
                _roomWallMat,
                _roomWallHeight,
                _roomWallThickness
            );
        }

        public void PlaceSpawnMarkers(
            Map map,
            Vector2Int zombieCell,
            Vector2Int targetCell,
            Transform zombieMarker,
            Transform targetMarker
        ) {
            if (zombieMarker != null) {
                zombieMarker.position = CellTopWorld(map, zombieCell);
            }

            if (targetMarker != null) {
                targetMarker.position = CellTopWorld(map, targetCell);
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
                Vector3 pos = CellCenterWorld(map, p);
                pos.y = topY - (_floorThickness * 0.5f);

                GameObject f;
                if (_floorPrefab != null) {
                    f = Object.Instantiate(_floorPrefab, pos, Quaternion.identity, _parent);
                } else {
                    f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    f.transform.SetParent(_parent, true);
                    f.transform.position = pos;
                }

                f.name = $"Floor_{x}_{y}";
                f.transform.localScale = new Vector3(map.CellSize, _floorThickness, map.CellSize);
                _spawnedFloors.Add(f);
            }
        }

        public void ClearAll() {
            ClearSpawnedWalls();
            ClearSpawnedFloors();

            if (_roomWallMesh != null) {
                Object.Destroy(_roomWallMesh);
                _roomWallMesh = null;
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

        private static bool WallTouchesRoad(Map map, Vector2Int p) {
            foreach (Vector2Int d in Utility.Cardinal) {
                Vector2Int n = p + d;
                if (!map.Bounds.Contains(n)) {
                    continue;
                }

                if (map.GetCell(n).isRoad) {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 CellCenterWorld(Map map, Vector2Int p) {
            float wx = map.Origin.x + ((p.x + 0.5f) * map.CellSize);
            float wz = map.Origin.z + ((p.y + 0.5f) * map.CellSize);
            return new Vector3(wx, 0f, wz);
        }

        private static Vector3 CellTopWorld(Map map, Vector2Int p) {
            float wx = map.Origin.x + ((p.x + 0.5f) * map.CellSize);
            float wz = map.Origin.z + ((p.y + 0.5f) * map.CellSize);
            float y = map.Bounds.Contains(p) ? map.Cells[p.y, p.x].height : 0f;
            return new Vector3(wx, y, wz);
        }

        private static float GetWallBaseHeightFromNeighbors(Map map, Vector2Int p) {
            float best = float.NegativeInfinity;
            foreach (Vector2Int d in Utility.Cardinal) {
                Vector2Int n = p + d;
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
    }
}
