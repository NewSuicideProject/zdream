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

        private readonly List<GameObject> _spawnedWalls = new();
        private readonly List<GameObject> _spawnedFloors = new();
        private readonly List<GameObject> _spawnedRoomInner = new();

        public Visualizer(
            Transform parent,
            GameObject wallPrefab,
            float wallCenterY,
            GameObject floorPrefab,
            float floorThickness,
            Material roomWallMat,
            float roomWallHeight,
            float roomWallThickness // 호환용(안씀)
        ) {
            _parent = parent;
            _wallPrefab = wallPrefab;
            _wallCenterY = wallCenterY;

            _floorPrefab = floorPrefab;
            _floorThickness = Mathf.Max(0.01f, floorThickness);

            _roomWallMat = roomWallMat;
            _roomWallHeight = Mathf.Max(0.01f, roomWallHeight);
        }

        public void Rebuild(Map map) {
            ClearAll();
            RebuildFloors(map);
            RebuildWalls(map);
            RebuildRoomInnerWallsAndChamfers(map);
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

                // 길 옆 벽만 큐브 유지
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

        private void RebuildRoomInnerWallsAndChamfers(Map map) {
            ClearSpawnedRoomInner();

            float h = _roomWallHeight;
            float half = map.CellSize * 0.5f;
            float yCenterOffset = h * 0.5f;

            // 1) 룸 안쪽 면(벽셀의 룸쪽 면) 플레인
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                if (!map.Cells[y, x].isBorder) {
                    continue;
                }

                Vector2Int wall = new(x, y);

                foreach (Vector2Int dir in Utility.Cardinal) {
                    Vector2Int roomCell = wall + dir;
                    if (!map.Bounds.Contains(roomCell)) {
                        continue;
                    }

                    Cell rc = map.GetCell(roomCell);
                    if (rc.roomId == -1) {
                        continue;
                    }

                    if (rc.isWall) {
                        continue;
                    }

                    Vector3 c = CellCenterWorld(map, wall);
                    Vector3 faceCenter = c + (new Vector3(dir.x, 0f, dir.y) * half);

                    float baseY = rc.height;
                    Vector3 pos = faceCenter;
                    pos.y = baseY + yCenterOffset;

                    GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "RoomInnerFace";
                    q.transform.SetParent(_parent, false);

                    Vector3 inward = CellCenterWorld(map, roomCell) - faceCenter;
                    inward.y = 0f;
                    if (inward.sqrMagnitude < 1e-6f) {
                        inward = new Vector3(-dir.x, 0f, -dir.y);
                    }

                    inward.Normalize();

                    q.transform.position = pos + (inward * 0.01f); // z-fight 방지(룸쪽으로 살짝)
                    q.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
                    q.transform.localScale = new Vector3(map.CellSize, h, 1f);

                    ApplyMat(q);

                    _spawnedRoomInner.Add(q);
                }
            }

            // 2) 코너 챔퍼(45도 대각): 룸 셀 기준으로 "두 벽면 중앙"을 잇는 플레인
            // 룸 셀 r에서 (dirA, dirB)가 서로 직각이고 둘 다 벽이면, 두 면 중앙을 연결해 대각 생성
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++) {
                Cell cell = map.Cells[y, x];
                if (cell.roomId == -1 || cell.isWall) {
                    continue;
                }

                Vector2Int r = new(x, y);

                TryChamfer(r, Vector2Int.right, Vector2Int.up);
                TryChamfer(r, Vector2Int.up, Vector2Int.left);
                TryChamfer(r, Vector2Int.left, Vector2Int.down);
                TryChamfer(r, Vector2Int.down, Vector2Int.right);

                void TryChamfer(Vector2Int room, Vector2Int a, Vector2Int b) {
                    Vector2Int wa = room + a;
                    Vector2Int wb = room + b;

                    if (!map.Bounds.Contains(wa) || !map.Bounds.Contains(wb)) {
                        return;
                    }

                    // 코너에 실제로 벽이 있어야(=isBorder) 대각을 만들고, 문이면 자연히 안 만들어짐
                    if (!map.GetCell(wa).isBorder) {
                        return;
                    }

                    if (!map.GetCell(wb).isBorder) {
                        return;
                    }

                    Vector3 roomCenter = CellCenterWorld(map, room);

                    Vector3 pa = roomCenter + (new Vector3(a.x, 0f, a.y) * half);
                    Vector3 pb = roomCenter + (new Vector3(b.x, 0f, b.y) * half);

                    Vector3 ab = pb - pa;
                    float len = ab.magnitude;
                    if (len < 1e-4f) {
                        return;
                    }

                    Vector3 xAxis = ab / len;
                    Vector3 forward = Vector3.Cross(xAxis, Vector3.up);
                    if (Vector3.Dot(forward, roomCenter - ((pa + pb) * 0.5f)) < 0f) {
                        forward = -forward;
                    }

                    Vector3 mid = (pa + pb) * 0.5f;
                    mid.y = cell.height + yCenterOffset;

                    GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "RoomInnerChamfer";
                    q.transform.SetParent(_parent, false);

                    q.transform.position = mid + (forward * 0.01f); // 룸쪽으로 살짝
                    q.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

                    // Quad의 local X가 가로(폭). 우리가 만든 xAxis로 맞춰야 하니까 Y축 회전 보정
                    // 현재 rotation의 right가 xAxis가 되도록 yaw 보정
                    Vector3 right = q.transform.right;
                    float sign = Mathf.Sign(Vector3.Dot(Vector3.Cross(right, xAxis), Vector3.up));
                    float angle = Vector3.Angle(right, xAxis) * sign;
                    q.transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * q.transform.rotation;

                    q.transform.localScale = new Vector3(len, h, 1f);

                    ApplyMat(q);

                    _spawnedRoomInner.Add(q);
                }
            }

            void ApplyMat(GameObject go) {
                if (_roomWallMat == null) {
                    return;
                }

                MeshRenderer r = go.GetComponent<MeshRenderer>();
                if (r != null) {
                    r.sharedMaterial = _roomWallMat;
                }
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
            ClearSpawnedRoomInner();
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

        private void ClearSpawnedRoomInner() {
            for (int i = 0; i < _spawnedRoomInner.Count; i++) {
                if (_spawnedRoomInner[i] != null) {
                    Object.Destroy(_spawnedRoomInner[i]);
                }
            }

            _spawnedRoomInner.Clear();
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
