using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment {
    public class Visualizer {
        private readonly GameObject _floorPrefab;
        private readonly float _floorThickness;
        private readonly Transform _wallContainer;
        private readonly Transform _floorContainer;

        private readonly List<GameObject> _spawnedFloors = new();
        private readonly List<GameObject> _spawnedWalls = new();

        private readonly float _wallCenterY;
        private readonly GameObject _wallPrefab;

        private Vector3 _wallPrefabWorldSize = Vector3.one;
        private bool _wallPrefabSizeCached;

        public Visualizer(
            Transform wallContainer,
            GameObject wallPrefab,
            float wallCenterY,
            GameObject floorPrefab,
            Transform floorContainer,
            float floorThickness
        ) {
            _wallContainer = wallContainer;
            _floorContainer = floorContainer;
            _wallPrefab = wallPrefab;
            _wallCenterY = wallCenterY;

            _floorPrefab = floorPrefab;
            _floorThickness = Mathf.Max(0.01f, floorThickness);


            CacheWallPrefabSize();
        }

        public void Rebuild(Map map) {
            ClearAll();
            RebuildFloors(map);
            RebuildWalls(map);
        }

        public void RebuildWalls(Map map) {
            ClearSpawnedWalls();

            if (!_wallPrefabSizeCached) {
                CacheWallPrefabSize();
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

                GameObject w = Object.Instantiate(_wallPrefab, pos, Quaternion.identity, _wallContainer);
                w.name = $"Wall_{x}_{y}";

                FitWallToCellXZ(w.transform, map.CellSize);

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

                GameObject f = Object.Instantiate(_floorPrefab, pos, Quaternion.identity, _floorContainer);

                f.name = $"Floor_{x}_{y}";
                f.transform.localScale = new Vector3(map.CellSize, _floorThickness, map.CellSize);
                _spawnedFloors.Add(f);
            }
        }

        public static void PlaceSpawnMarkers(
            Map map,
            Vector2Int zombieCell,
            Vector2Int targetCell,
            Transform agentTransform,
            Transform targetTransform
        ) {
            agentTransform.position = CellTopWorld(map, zombieCell);
            targetTransform.position = CellTopWorld(map, targetCell);
        }

        public void ClearAll() {
            ClearSpawnedWalls();
            ClearSpawnedFloors();
        }

        private void FitWallToCellXZ(Transform wallTr, float cellSize) {
            float baseX = Mathf.Max(0.0001f, _wallPrefabWorldSize.x);
            float baseZ = Mathf.Max(0.0001f, _wallPrefabWorldSize.z);

            Vector3 s = wallTr.localScale;
            s.x *= cellSize / baseX;
            s.z *= cellSize / baseZ;
            wallTr.localScale = s;
        }

        private void CacheWallPrefabSize() {
            _wallPrefabSizeCached = false;
            _wallPrefabWorldSize = Vector3.one;

            if (_wallPrefab == null) {
                return;
            }

            Renderer r = _wallPrefab.GetComponentInChildren<Renderer>(true);
            if (r == null) {
                return;
            }

            Vector3 size = r.bounds.size;
            if (size.x <= 0f || size.z <= 0f) {
                return;
            }

            _wallPrefabWorldSize = size;
            _wallPrefabSizeCached = true;
        }

        private void ClearSpawnedWalls() {
            for (int i = 0; i < _spawnedWalls.Count; i++) {
                if (_spawnedWalls[i] != null) {
                    _spawnedWalls[i].SetActive(false);
                    Object.Destroy(_spawnedWalls[i]);
                }
            }

            _spawnedWalls.Clear();
        }

        private void ClearSpawnedFloors() {
            for (int i = 0; i < _spawnedFloors.Count; i++) {
                if (_spawnedFloors[i] != null) {
                    _spawnedFloors[i].SetActive(false);
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
