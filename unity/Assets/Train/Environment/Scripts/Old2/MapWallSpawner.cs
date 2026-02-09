using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapWallSpawner : MonoBehaviour {
    [Header("Scene")] [SerializeField] private Transform basePart;
    [SerializeField] private Transform wallParent;
    [SerializeField] private Transform debugParent;

    [Header("Generator")] [SerializeField] private OrganicRoomShaper roomShaper;
    [SerializeField] private int maxRerolls = 30;

    [Header("BFS")] [SerializeField] private int bfsMaxExpand = 15000;
    [SerializeField] private int doorSamplesPerRoom = 40;
    [SerializeField] private int corridorWidth = 1; // >=1

    [Header("Wall Prefab")] [SerializeField]
    private GameObject wallPrefab;

    [Header("Debug Capsules")] [SerializeField]
    private bool spawnDebugCapsules = true;

    [SerializeField] private float capsuleHeight = 2f;
    [SerializeField] private float capsuleRadius = 0.5f;
    [SerializeField] private float capsuleLift = 1.2f;
    [SerializeField] private Transform zombieSpawnMarker;
    [SerializeField] private Transform targetSpawnMarker;
    [SerializeField] private Material zombieMat;
    [SerializeField] private Material targetMat;

    private bool[,] wall; // true = wall
    private int[,] roomId; // -1 = corridor/wall, >=0 = room floor
    private bool[,] nearFloor; // helper (now used only for debug/optional)
    private IReadOnlyList<OrganicRoomShaper.Room> rooms;

    private System.Random rng;

    private static readonly Vector2Int[] Dir4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private void Awake() {
        rng = new System.Random(Environment.TickCount);
        if (wallParent == null) {
            wallParent = transform;
        }

        if (debugParent == null) {
            debugParent = transform;
        }
    }

    private void Start() => GenerateAll();

    // =========================================================
    // MAIN
    // =========================================================

    private void GenerateAll() {
        if (basePart == null || roomShaper == null || wallPrefab == null) {
            Debug.LogError("[MapWallSpawner] Missing refs: basePart/roomShaper/wallPrefab must be assigned.");
            return;
        }

        ClearChildren(wallParent);
        ClearChildren(debugParent);

        for (int reroll = 0; reroll < maxRerolls; reroll++) {
            roomShaper.Generate();

            wall = (bool[,])roomShaper.Wall.Clone();
            roomId = (int[,])roomShaper.RoomId.Clone();
            rooms = roomShaper.Rooms;

            if (rooms == null || rooms.Count < 2) {
                continue;
            }

            RebuildNearFloor();

            if (!ConnectRoomsSequential()) {
                continue;
            }

            SpawnWalls();
            SpawnDebugCapsules();
            return;
        }

        Debug.LogWarning("Map generation failed (reroll limit). Spawning last attempt walls/debug.");
        if (wall != null) {
            SpawnWalls();
        }

        SpawnDebugCapsules();
    }

    // =========================================================
    // ROOM CONNECTION (BFS)
    // =========================================================

    private bool ConnectRoomsSequential() {
        int current = 0;

        for (int i = 1; i < rooms.Count; i++) {
            if (!ConnectRoomsBfs(current, i)) {
                return false;
            }

            // after carving, update buffer info
            RebuildNearFloor();
            current = i;
        }

        return true;
    }

    private bool ConnectRoomsBfs(int fromRoom, int toRoom) {
        if (!TryGetDoorStartCandidates(rooms[fromRoom], out List<Vector2Int> startDoorWalls)) {
            return false;
        }

        // 여러 번 시도
        for (int i = 0; i < doorSamplesPerRoom; i++) {
            Vector2Int startWall = startDoorWalls[rng.Next(startDoorWalls.Count)];

            if (FindPathBfs(startWall, toRoom, out List<Vector2Int> path, out Vector2Int punchWall)) {
                CarvePathWide(path, corridorWidth);

                // 목표 룸과 맞닿은 벽(punchWall)은 확실히 뚫기
                CarveCellWide(punchWall, corridorWidth);

                // punchWall이 목표룸에 인접하므로 그 지점이 "문"이 됨.
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // BFS CORE
    // =========================================================

    private bool FindPathBfs(
        Vector2Int startWall,
        int targetRoomId,
        out List<Vector2Int> path,
        out Vector2Int punchWall
    ) {
        path = null;
        punchWall = default;

        int w = wall.GetLength(0);
        int h = wall.GetLength(1);

        if (!IsInner(startWall) || !wall[startWall.x, startWall.y]) {
            return false;
        }

        Queue<Vector2Int> q = new();
        bool[,] visited = new bool[w, h];
        Dictionary<Vector2Int, Vector2Int> parent = new();

        q.Enqueue(startWall);
        visited[startWall.x, startWall.y] = true;

        int expanded = 0;

        while (q.Count > 0 && expanded < bfsMaxExpand) {
            Vector2Int cur = q.Dequeue();
            expanded++;

            // 목표 룸의 바닥(= !wall && roomId==target)와 인접한 벽 셀을 찾으면 종료
            if (TouchesTargetRoom(cur, targetRoomId)) {
                punchWall = cur;
                path = Reconstruct(parent, startWall, cur);
                return true;
            }

            foreach (Vector2Int d in Dir4) {
                Vector2Int nxt = cur + d;
                if (!IsInner(nxt)) {
                    continue;
                }

                if (visited[nxt.x, nxt.y]) {
                    continue;
                }

                if (!wall[nxt.x, nxt.y]) {
                    continue; // 벽만 타고 이동
                }

                // ✅ 중요: nearFloor 제한 삭제 (이게 경로를 거의 막아버리는 주범이었음)
                visited[nxt.x, nxt.y] = true;
                parent[nxt] = cur;
                q.Enqueue(nxt);
            }
        }

        return false;
    }

    private bool TouchesTargetRoom(Vector2Int wallCell, int targetRoom) {
        foreach (Vector2Int d in Dir4) {
            Vector2Int n = wallCell + d;
            if (!IsInner(n)) {
                continue;
            }

            if (!wall[n.x, n.y] && roomId[n.x, n.y] == targetRoom) {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // DOOR START CANDIDATES (robust to shaper impl)
    // =========================================================

    private bool TryGetDoorStartCandidates(OrganicRoomShaper.Room room, out List<Vector2Int> startDoorWalls) {
        startDoorWalls = new List<Vector2Int>();

        // room.roomWallCells가 "벽셀"일 수도, "바닥 테두리셀"일 수도 있어서 자동 판별
        List<Vector2Int> src = room.roomWallCells;
        if (src == null || src.Count == 0) {
            return false;
        }

        // 샘플 하나를 보고 wall[x,y]가 true면 "벽셀 리스트"로 간주
        Vector2Int probe = src[0];
        bool srcLooksLikeWallCells = IsInner(probe) && wall[probe.x, probe.y];

        if (srcLooksLikeWallCells) {
            // ✅ 이미 벽셀 리스트면 그대로 door 후보로 사용
            for (int i = 0; i < src.Count; i++) {
                Vector2Int c = src[i];
                if (IsInner(c) && wall[c.x, c.y]) {
                    startDoorWalls.Add(c);
                }
            }
        } else {
            // ✅ 바닥 테두리 리스트면, 그 주변 벽을 door 후보로 수집
            for (int i = 0; i < src.Count; i++) {
                Vector2Int floorEdge = src[i];
                if (!IsInner(floorEdge)) {
                    continue;
                }

                if (wall[floorEdge.x, floorEdge.y]) {
                    continue; // 바닥이어야 함
                }

                foreach (Vector2Int d in Dir4) {
                    Vector2Int w = floorEdge + d;
                    if (!IsInner(w)) {
                        continue;
                    }

                    if (wall[w.x, w.y]) {
                        startDoorWalls.Add(w);
                    }
                }
            }
        }

        // 중복 제거
        if (startDoorWalls.Count > 1) {
            HashSet<Vector2Int> uniq = new();
            List<Vector2Int> cleaned = new(startDoorWalls.Count);
            foreach (Vector2Int c in startDoorWalls) {
                if (uniq.Add(c)) {
                    cleaned.Add(c);
                }
            }

            startDoorWalls = cleaned;
        }

        return startDoorWalls.Count > 0;
    }

    // =========================================================
    // CARVE (corridor width 적용)
    // =========================================================

    private void CarvePathWide(List<Vector2Int> path, int width) {
        if (path == null || path.Count == 0) {
            return;
        }

        width = Mathf.Max(1, width);

        foreach (Vector2Int p in path) {
            CarveCellWide(p, width);
        }
    }

    private void CarveCellWide(Vector2Int c, int width) {
        width = Mathf.Max(1, width);
        int radius = (width - 1) / 2;

        for (int oy = -radius; oy <= radius; oy++) {
            for (int ox = -radius; ox <= radius; ox++) {
                Vector2Int p = new(c.x + ox, c.y + oy);
                if (!IsInner(p)) {
                    continue;
                }

                wall[p.x, p.y] = false;
                roomId[p.x, p.y] = -1; // corridor
            }
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private List<Vector2Int> Reconstruct(
        Dictionary<Vector2Int, Vector2Int> parent,
        Vector2Int start,
        Vector2Int end
    ) {
        List<Vector2Int> path = new();
        Vector2Int cur = end;
        path.Add(cur);

        while (cur != start) {
            if (!parent.TryGetValue(cur, out Vector2Int p)) {
                break; // safety
            }

            cur = p;
            path.Add(cur);
        }

        path.Reverse();
        return path;
    }

    private void RebuildNearFloor() {
        int w = wall.GetLength(0);
        int h = wall.GetLength(1);
        nearFloor = new bool[w, h];

        for (int y = 1; y < h - 1; y++) {
            for (int x = 1; x < w - 1; x++) {
                if (!wall[x, y]) {
                    continue;
                }

                nearFloor[x, y] =
                    !wall[x + 1, y] ||
                    !wall[x - 1, y] ||
                    !wall[x, y + 1] ||
                    !wall[x, y - 1];
            }
        }
    }

    private bool IsInner(Vector2Int p) {
        int w = wall.GetLength(0);
        int h = wall.GetLength(1);
        return p.x > 0 && p.y > 0 && p.x < w - 1 && p.y < h - 1;
    }

    // =========================================================
    // SPAWN
    // =========================================================

    private void SpawnWalls() {
        ClearChildren(wallParent);

        GetBaseBounds(out Vector3 origin, out float sizeX, out float sizeZ);

        int w = wall.GetLength(0);
        int h = wall.GetLength(1);

        float cellX = sizeX / w;
        float cellZ = sizeZ / h;
        float baseY = basePart.position.y;

        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                if (!wall[x, y]) {
                    continue;
                }

                Vector3 pos = origin + new Vector3((x + 0.5f) * cellX, baseY + 1.5f, (y + 0.5f) * cellZ);
                Instantiate(wallPrefab, pos, Quaternion.identity, wallParent);
            }
        }
    }

    private void SpawnDebugCapsules() {
        if (!spawnDebugCapsules) {
            return;
        }

        float y = basePart.position.y + capsuleLift;

        if (zombieSpawnMarker != null) {
            CreateCapsule(zombieSpawnMarker.position, y, zombieMat, "Zombie");
        }

        if (targetSpawnMarker != null) {
            CreateCapsule(targetSpawnMarker.position, y, targetMat, "Target");
        }
    }

    private void CreateCapsule(Vector3 src, float y, Material mat, string name) {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = $"Debug_{name}";
        go.transform.SetParent(debugParent);
        go.transform.position = new Vector3(src.x, y, src.z);
        go.transform.localScale = new Vector3(capsuleRadius * 2f, capsuleHeight * 0.5f, capsuleRadius * 2f);

        if (mat != null) {
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        go.GetComponent<Collider>().isTrigger = true;
    }

    private void GetBaseBounds(out Vector3 origin, out float sizeX, out float sizeZ) {
        Renderer r = basePart.GetComponentInChildren<Renderer>();
        Bounds b = r.bounds;
        sizeX = b.size.x;
        sizeZ = b.size.z;
        origin = new Vector3(b.min.x, basePart.position.y, b.min.z);
    }

    private void ClearChildren(Transform t) {
        if (t == null) {
            return;
        }

        for (int i = t.childCount - 1; i >= 0; i--) {
            //Destroy(t.GetChild(i).gameObject);
        }
    }
}
