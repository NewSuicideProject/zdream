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
        private readonly List<GameObject> _spawnedRoomWallParts = new();

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
            RebuildWalls(map);
        }

        public void RebuildWalls(Map map) {
            ClearSpawnedWalls();
            ClearSpawnedRoomWallParts();

            if (_wallPrefab != null) {
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

            BuildRoomWallParts(map);
        }

        private void BuildRoomWallParts(Map map) {
            Dictionary<Vector2Int, List<Vector2Int>> graph = new();

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                ref Cell c = ref map.Cells[y, x];
                if (c.roomId == -1) {
                    continue;
                }

                Vector2Int p = new(x, y);

                foreach (Vector2Int d in Utility.Cardinal) {
                    Vector2Int n = p + d;

                    // 문(road) 방향 면은 비움
                    if (map.Bounds.Contains(n) && map.GetCell(n).isRoad) {
                        continue;
                    }

                    // 바깥/벽 방향 면이면 외곽선
                    if (!map.Bounds.Contains(n) || map.GetCell(n).isWall) {
                        Vector2Int a = EdgeStart(p, d);
                        Vector2Int b = EdgeEnd(p, d);
                        AddEdge(graph, a, b);
                    }
                }
            }

            HashSet<(Vector2Int, Vector2Int)> used = new();

            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> kv in graph) {
                Vector2Int start = kv.Key;
                List<Vector2Int> nexts = kv.Value;

                for (int i = 0; i < nexts.Count; i++) {
                    Vector2Int firstTo = nexts[i];
                    if (used.Contains((start, firstTo))) {
                        continue;
                    }

                    List<Vector2Int> chain = new(64);
                    Vector2Int prev = start;
                    Vector2Int cur = start;
                    Vector2Int next = firstTo;

                    chain.Add(cur);
                    MarkEdgeUsed(used, cur, next);

                    int guard = 0;

                    while (guard++ < 100000) {
                        cur = next;
                        chain.Add(cur);

                        if (!graph.TryGetValue(cur, out List<Vector2Int> list) || list.Count == 0) {
                            break;
                        }

                        if (!TryPickNext(list, prev, cur, used, out Vector2Int picked)) {
                            break;
                        }

                        prev = cur;
                        next = picked;
                        MarkEdgeUsed(used, prev, next);

                        // 닫힌 루프면 마지막 닫는 점 추가하고 종료
                        if (next == chain[0]) {
                            chain.Add(chain[0]);
                            break;
                        }
                    }

                    // 열린 체인도 벽 스폰해야 문 옆 프리팹 벽과 이어짐
                    if (chain.Count >= 2) {
                        SpawnRoomWallSegmentParts(map, chain);
                    }
                }
            }
        }

        private static void MarkEdgeUsed(HashSet<(Vector2Int, Vector2Int)> used, Vector2Int a, Vector2Int b) {
            used.Add((a, b));
            used.Add((b, a));
        }

        // prev만 피하는 게 아니라 "아직 안 쓴 엣지" 우선
        private static bool TryPickNext(
            List<Vector2Int> list,
            Vector2Int prev,
            Vector2Int cur,
            HashSet<(Vector2Int, Vector2Int)> used,
            out Vector2Int next
        ) {
            // 1) prev 아닌 것 중에서 아직 안 쓴 엣지 우선
            for (int i = 0; i < list.Count; i++) {
                Vector2Int cand = list[i];
                if (cand == prev) {
                    continue;
                }

                if (used.Contains((cur, cand))) {
                    continue;
                }

                next = cand;
                return true;
            }

            // 2) 그래도 없으면 prev 아닌 것(마지막 루프 닫기 같은 케이스)
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

        private void SpawnRoomWallSegmentParts(Map map, List<Vector2Int> chain) {
            if (chain.Count < 2) {
                return;
            }

            float t = _roomWallThickness;
            float h = _roomWallHeight;

            for (int i = 0; i < chain.Count - 1; i++) {
                Vector2Int a = chain[i];
                Vector2Int b = chain[i + 1];

                Vector3 wa = GridCornerToWorld(map, a);
                Vector3 wb = GridCornerToWorld(map, b);

                Vector3 flatA = new(wa.x, 0f, wa.z);
                Vector3 flatB = new(wb.x, 0f, wb.z);

                Vector3 dir = flatB - flatA;
                float len = dir.magnitude;
                if (len < 0.0001f) {
                    continue;
                }

                dir /= len;

                float baseY = GetRoomBaseHeightNearCorner(map, a);
                float centerY = baseY + (h * 0.5f);

                Vector3 mid = (flatA + flatB) * 0.5f;
                mid.y = centerY;

                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = "RoomWallPart";
                part.transform.SetParent(_parent, false);
                part.transform.position = mid;
                part.transform.rotation = rot;
                part.transform.localScale = new Vector3(t, h, len);

                if (_roomWallMat != null) {
                    MeshRenderer r = part.GetComponent<MeshRenderer>();
                    if (r != null) {
                        r.sharedMaterial = _roomWallMat;
                    }
                }

                _spawnedRoomWallParts.Add(part);
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
            ClearSpawnedRoomWallParts();
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

        private void ClearSpawnedRoomWallParts() {
            for (int i = 0; i < _spawnedRoomWallParts.Count; i++) {
                if (_spawnedRoomWallParts[i] != null) {
                    Object.Destroy(_spawnedRoomWallParts[i]);
                }
            }

            _spawnedRoomWallParts.Clear();
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

        private static Vector2Int EdgeStart(Vector2Int p, Vector2Int d) =>
            d == Vector2Int.up ? new Vector2Int(p.x, p.y + 1) :
            d == Vector2Int.right ? new Vector2Int(p.x + 1, p.y + 1) :
            d == Vector2Int.down ? new Vector2Int(p.x + 1, p.y) :
            new Vector2Int(p.x, p.y);

        private static Vector2Int EdgeEnd(Vector2Int p, Vector2Int d) =>
            d == Vector2Int.up ? new Vector2Int(p.x + 1, p.y + 1) :
            d == Vector2Int.right ? new Vector2Int(p.x + 1, p.y) :
            d == Vector2Int.down ? new Vector2Int(p.x, p.y) :
            new Vector2Int(p.x, p.y + 1);

        private static void AddEdge(Dictionary<Vector2Int, List<Vector2Int>> g, Vector2Int a, Vector2Int b) {
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

        private static float GetRoomBaseHeightNearCorner(Map map, Vector2Int corner) {
            Vector2Int c0 = new(corner.x, corner.y);
            Vector2Int c1 = new(corner.x - 1, corner.y);
            Vector2Int c2 = new(corner.x, corner.y - 1);
            Vector2Int c3 = new(corner.x - 1, corner.y - 1);

            if (map.Bounds.Contains(c0)) {
                Cell cell = map.GetCell(c0);
                if (cell.roomId != -1) {
                    return cell.height;
                }
            }

            if (map.Bounds.Contains(c1)) {
                Cell cell = map.GetCell(c1);
                if (cell.roomId != -1) {
                    return cell.height;
                }
            }

            if (map.Bounds.Contains(c2)) {
                Cell cell = map.GetCell(c2);
                if (cell.roomId != -1) {
                    return cell.height;
                }
            }

            if (map.Bounds.Contains(c3)) {
                Cell cell = map.GetCell(c3);
                if (cell.roomId != -1) {
                    return cell.height;
                }
            }

            return 0f;
        }

        private static Vector3 GridCornerToWorld(Map map, Vector2Int p) {
            float wx = map.Origin.x + (p.x * map.CellSize);
            float wz = map.Origin.z + (p.y * map.CellSize);
            return new Vector3(wx, 0f, wz);
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
