using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment {
    public class Visualizer {
        private readonly GameObject _floorPrefab;
        private readonly float _floorThickness;
        private readonly Transform _parent;
        private readonly float _roomWallHeight;

        // 환경에서 넘겨줘도 지금은 안 씀(큐브 벽으로 복귀)
        private readonly Material _roomWallMat;
        private readonly float _roomWallThickness;
        private readonly List<GameObject> _spawnedFloors = new();

        private readonly List<GameObject> _spawnedWalls = new();
        private readonly float _wallCenterY;
        private readonly GameObject _wallPrefab;

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
            _roomWallHeight = roomWallHeight;
            _roomWallThickness = roomWallThickness;
        }

        public void Rebuild(Map map) {
            ClearAll();
            RebuildFloors(map);
            RebuildWalls(map);
        }

        public void RebuildWalls(Map map) {
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
                float baseY = GetWallBaseHeightFromNeighbors(map, p);

                Vector3 pos = CellCenterWorld(map, p);
                pos.y = baseY + _wallCenterY;

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

        public void ClearAll() {
            ClearSpawnedWalls();
            ClearSpawnedFloors();
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
