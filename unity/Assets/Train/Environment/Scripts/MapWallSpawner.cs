using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapWallSpawner : MonoBehaviour {
    // =========================================================
    // [SECTION 0] Inspector / Config
    // =========================================================
    [Header("Scene refs")] [SerializeField]
    private Transform basePart; // Plane (256x256). 월드 원점/크기 기준점

    [SerializeField] private Transform wallParent; // 생성된 벽/디버그 오브젝트를 넣을 부모(없으면 this.transform)

    [Header("Spawn marker refs (existing EmptyObjects)")] [SerializeField]
    private Transform zombieSpawnMarker; // 씬에 이미 있는 Empty(좀비 스폰 위치 표시)

    [SerializeField] private Transform targetSpawnMarker; // 씬에 이미 있는 Empty(타겟 위치 표시)

    [Header("Spawn debug visuals")] [SerializeField]
    private bool spawnDebugCapsules = true; // 스폰 위치를 캡슐로 시각화할지

    [SerializeField] private float capsuleHeight = 2.0f;
    [SerializeField] private float capsuleRadius = 0.5f;
    [SerializeField] private float capsuleYOffset = 0.05f;

    [Header("Spawn debug materials (NO Shader.Find)")] [SerializeField]
    private Material zombieSpawnDebugMaterial; // URP/HDRP/Built-in에 맞는 머티리얼을 인스펙터로 지정

    [SerializeField] private Material targetSpawnDebugMaterial; // ↑ 같은 방식
    [SerializeField] private bool tintWithPropertyBlock = true; // 머티리얼 자체는 그대로 두고 색만 덮어쓸지

    [Header("Grid")] [SerializeField] private int width = 256; // X축 셀 개수
    [SerializeField] private int height = 256; // Y축 셀 개수 (월드에서는 Z축)
    [SerializeField] private float cellSize = 1f; // 1셀당 월드 크기

    [Header("Wall object")] [SerializeField]
    private Vector3 wallSize = new(1f, 5f, 1f); // 벽 큐브 크기

    [Header("Generation - WFC stub params")] [Range(0f, 1f)] [SerializeField]
    private float initialWallFill = 0.38f; // 초기 벽 확률(대충 0.33~0.40 추천)

    [Header("Generation - Connectivity params")] [SerializeField]
    private int minSpawnManhattanDistance = 80; // 좀비-타겟 맨해튼 최소 거리

    [Range(0f, 1f)] [SerializeField] private float loopDoorRatio = 0.25f; // “최소 연결 문 개수”에 비례해서 루프 문을 추가하는 비율

    [Header("Random")] [SerializeField] private int seed = 0;
    [SerializeField] private bool useRandomSeed = true;

    // =========================================================
    // [SECTION 1] Data structs
    // =========================================================
    private struct RoomInfo {
        public int id;
        public int count;
        public int minX, maxX, minY, maxY;
    }

    private struct WallCandidate {
        public Vector2Int wall; // 실제로 제거할 벽 셀 좌표
        public Vector2Int aSide; // 벽의 한쪽(룸 A) 바닥 셀
        public Vector2Int bSide; // 벽의 반대쪽(룸 B) 바닥 셀
    }

    private Vector2Int _zombieCell;
    private Vector2Int _targetCell;

    // PropertyBlock은 재사용 (GC/할당 줄이기)
    private MaterialPropertyBlock _mpb;

    // =========================================================
    // [MAIN FLOW] Start()
    // =========================================================
    private void Start() {
        if (basePart == null) {
            Debug.LogError("[MapWallSpawner] BasePart not assigned.");
            return;
        }

        _mpb ??= new MaterialPropertyBlock();

        ClearSpawnedChildren();

        System.Random rng = CreateRng();

        // =====================================================
        // [SECTION A] MAP DATA 생성 (wallMatrix + spawnCells)
        // =====================================================
        bool[,] wallMatrix = GenerateWallMatrix(rng, out _zombieCell, out _targetCell);

        // =====================================================
        // [SECTION B] SPAWN 표시 (marker 이동 + 디버그 캡슐)
        // =====================================================
        ApplySpawnMarkersAndDebug(_zombieCell, _targetCell);

        // =====================================================
        // [SECTION C] VISUAL BUILD (wallMatrix -> 실제 벽 생성)
        // =====================================================
        SpawnWalls(wallMatrix);
    }

    // =========================================================
    // [SECTION A] MAP DATA 생성 파트
    // =========================================================
    private bool[,] GenerateWallMatrix(System.Random rng, out Vector2Int zombieSpawn, out Vector2Int targetSpawn) {
        zombieSpawn = default;
        targetSpawn = default;

        bool[,] walls = GenerateWalls_WFCStub(rng);
        MakeBorderWalls(walls);

        if (!TryPickSpawns(walls, rng, out zombieSpawn, out targetSpawn)) {
            Debug.LogWarning("[MapWallSpawner] Failed to pick spawns. Falling back to border-only.");
            walls = new bool[height, width];
            MakeBorderWalls(walls);
            return walls;
        }

        bool ok = TryCarveConnectivityAndLoops(walls, rng, zombieSpawn, targetSpawn, out _);

        if (!ok) {
            Debug.LogWarning(
                "[MapWallSpawner] No room path found. Carving minimal tunnel, then rebuilding graph for loops.");

            bool tunnelOk = CarveMinimalWallTunnel(walls, zombieSpawn, targetSpawn);
            if (!tunnelOk) {
                Debug.LogWarning("[MapWallSpawner] Tunnel carve failed. Falling back to border-only.");
                walls = new bool[height, width];
                MakeBorderWalls(walls);
                return walls;
            }

            TryCarveLoopsAfterTunnel(walls, rng, zombieSpawn, targetSpawn);
        }

        if (!IsReachable(walls, zombieSpawn, targetSpawn)) {
            Debug.LogWarning("[MapWallSpawner] Connectivity validation failed. Falling back to border-only.");
            walls = new bool[height, width];
            MakeBorderWalls(walls);
        }

        return walls;
    }

    private bool[,] GenerateWalls_WFCStub(System.Random rng) {
        bool[,] walls = new bool[height, width];

        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++) {
            walls[y, x] = rng.NextDouble() < initialWallFill;
        }

        SmoothWallsOnce(walls);
        SmoothWallsOnce(walls);

        return walls;
    }

    private void SmoothWallsOnce(bool[,] walls) {
        bool[,] tmp = (bool[,])walls.Clone();

        for (int y = 1; y < height - 1; y++) {
            for (int x = 1; x < width - 1; x++) {
                int wallCount = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++) {
                    if (dx == 0 && dy == 0) {
                        continue;
                    }

                    if (tmp[y + dy, x + dx]) {
                        wallCount++;
                    }
                }

                if (wallCount >= 5) {
                    walls[y, x] = true;
                } else if (wallCount <= 2) {
                    walls[y, x] = false;
                }
            }
        }
    }

    private void MakeBorderWalls(bool[,] walls) {
        for (int x = 0; x < width; x++) {
            walls[0, x] = true;
            walls[height - 1, x] = true;
        }

        for (int y = 0; y < height; y++) {
            walls[y, 0] = true;
            walls[y, width - 1] = true;
        }
    }

    private bool TryPickSpawns(bool[,] walls, System.Random rng, out Vector2Int zombie, out Vector2Int target) {
        zombie = default;
        target = default;

        const int tries = 30000;

        Vector2Int PickOne() {
            int x = rng.Next(1, width - 1);
            int y = rng.Next(1, height - 1);
            return new Vector2Int(x, y);
        }

        bool foundZombie = false;
        for (int i = 0; i < tries; i++) {
            Vector2Int p = PickOne();
            if (!walls[p.y, p.x]) {
                zombie = p;
                foundZombie = true;
                break;
            }
        }

        if (!foundZombie) {
            return false;
        }

        for (int i = 0; i < tries; i++) {
            Vector2Int p = PickOne();
            if (walls[p.y, p.x]) {
                continue;
            }

            int dist = Mathf.Abs(p.x - zombie.x) + Mathf.Abs(p.y - zombie.y);
            if (dist >= minSpawnManhattanDistance) {
                target = p;
                return true;
            }
        }

        return false;
    }

    private bool TryCarveConnectivityAndLoops(
        bool[,] walls,
        System.Random rng,
        Vector2Int zombieSpawn,
        Vector2Int targetSpawn,
        out int minDoorsCarved) {
        minDoorsCarved = 0;

        int[,] roomId = new int[height, width];
        List<RoomInfo> rooms = ExtractRooms(walls, roomId);

        int startRoom = roomId[zombieSpawn.y, zombieSpawn.x];
        int goalRoom = roomId[targetSpawn.y, targetSpawn.x];

        if (startRoom < 0 || goalRoom < 0) {
            return false;
        }

        if (startRoom == goalRoom) {
            return true;
        }

        Dictionary<(int a, int b), List<WallCandidate>> edgeCandidates = BuildAdjacencyCandidates(walls, roomId);
        if (edgeCandidates.Count == 0) {
            return false;
        }

        List<HashSet<int>> neighbors = BuildNeighbors(rooms.Count, edgeCandidates);
        List<int> roomPath = FindRoomPathBFS(neighbors, startRoom, goalRoom);
        if (roomPath == null || roomPath.Count == 0) {
            return false;
        }

        minDoorsCarved = CarveDoorsAlongRoomPath(walls, roomPath, edgeCandidates, rng);

        int loopDoors = Mathf.Max(0, Mathf.RoundToInt(minDoorsCarved * loopDoorRatio));
        AddLoopDoors(walls, roomPath, edgeCandidates, rng, loopDoors);

        return true;
    }

    private void TryCarveLoopsAfterTunnel(bool[,] walls, System.Random rng, Vector2Int zombieSpawn,
        Vector2Int targetSpawn) {
        int[,] roomId = new int[height, width];
        List<RoomInfo> rooms = ExtractRooms(walls, roomId);

        Dictionary<(int a, int b), List<WallCandidate>> edgeCandidates = BuildAdjacencyCandidates(walls, roomId);
        if (edgeCandidates.Count == 0) {
            return;
        }

        int startRoom = roomId[zombieSpawn.y, zombieSpawn.x];
        int goalRoom = roomId[targetSpawn.y, targetSpawn.x];

        List<HashSet<int>> neighbors = BuildNeighbors(rooms.Count, edgeCandidates);
        List<int> roomPath = startRoom >= 0 && goalRoom >= 0
            ? FindRoomPathBFS(neighbors, startRoom, goalRoom)
            : null;

        if (roomPath == null || roomPath.Count <= 1) {
            AddLoopDoors_NoPath(walls, edgeCandidates, rng, 8);
            return;
        }

        int baseDoors = Mathf.Max(4, roomPath.Count - 1);
        int loopDoors = Mathf.Max(2, Mathf.RoundToInt(baseDoors * loopDoorRatio));
        AddLoopDoors(walls, roomPath, edgeCandidates, rng, loopDoors);
    }

    private List<RoomInfo> ExtractRooms(bool[,] walls, int[,] roomId) {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) {
            roomId[y, x] = -1;
        }

        List<RoomInfo> rooms = new(256);
        Queue<Vector2Int> q = new(1024);

        int nextId = 0;

        for (int y = 1; y < height - 1; y++) {
            for (int x = 1; x < width - 1; x++) {
                if (walls[y, x]) {
                    continue;
                }

                if (roomId[y, x] != -1) {
                    continue;
                }

                int id = nextId++;
                RoomInfo info = new() {
                    id = id,
                    count = 0,
                    minX = x,
                    maxX = x,
                    minY = y,
                    maxY = y
                };

                roomId[y, x] = id;
                q.Enqueue(new Vector2Int(x, y));

                while (q.Count > 0) {
                    Vector2Int c = q.Dequeue();
                    info.count++;

                    if (c.x < info.minX) {
                        info.minX = c.x;
                    }

                    if (c.x > info.maxX) {
                        info.maxX = c.x;
                    }

                    if (c.y < info.minY) {
                        info.minY = c.y;
                    }

                    if (c.y > info.maxY) {
                        info.maxY = c.y;
                    }

                    TryVisit(c.x + 1, c.y);
                    TryVisit(c.x - 1, c.y);
                    TryVisit(c.x, c.y + 1);
                    TryVisit(c.x, c.y - 1);

                    void TryVisit(int nx, int ny) {
                        if (nx <= 0 || nx >= width - 1 || ny <= 0 || ny >= height - 1) {
                            return;
                        }

                        if (walls[ny, nx]) {
                            return;
                        }

                        if (roomId[ny, nx] != -1) {
                            return;
                        }

                        roomId[ny, nx] = id;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                rooms.Add(info);
            }
        }

        return rooms;
    }

    private Dictionary<(int a, int b), List<WallCandidate>> BuildAdjacencyCandidates(bool[,] walls, int[,] roomId) {
        Dictionary<(int, int), List<WallCandidate>> dict = new(2048);

        for (int y = 1; y < height - 1; y++) {
            for (int x = 1; x < width - 1; x++) {
                if (!walls[y, x]) {
                    continue;
                }

                // left-right
                int lx = x - 1, rx = x + 1;
                if (!walls[y, lx] && !walls[y, rx]) {
                    int ra = roomId[y, lx];
                    int rb = roomId[y, rx];
                    if (ra >= 0 && rb >= 0 && ra != rb) {
                        AddCandidate(ra, rb,
                            new WallCandidate {
                                wall = new Vector2Int(x, y),
                                aSide = new Vector2Int(lx, y),
                                bSide = new Vector2Int(rx, y)
                            });
                    }
                }

                // up-down
                int uy = y - 1, dy = y + 1;
                if (!walls[uy, x] && !walls[dy, x]) {
                    int ra = roomId[uy, x];
                    int rb = roomId[dy, x];
                    if (ra >= 0 && rb >= 0 && ra != rb) {
                        AddCandidate(ra, rb,
                            new WallCandidate {
                                wall = new Vector2Int(x, y),
                                aSide = new Vector2Int(x, uy),
                                bSide = new Vector2Int(x, dy)
                            });
                    }
                }
            }
        }

        return dict;

        void AddCandidate(int ra, int rb, WallCandidate c) {
            int a = Mathf.Min(ra, rb);
            int b = Mathf.Max(ra, rb);
            (int a, int b) key = (a, b);

            if (!dict.TryGetValue(key, out List<WallCandidate> list)) {
                list = new List<WallCandidate>(8);
                dict[key] = list;
            }

            list.Add(c);
        }
    }

    private List<HashSet<int>>
        BuildNeighbors(int roomCount, Dictionary<(int a, int b), List<WallCandidate>> candidates) {
        List<HashSet<int>> neighbors = new(roomCount);
        for (int i = 0; i < roomCount; i++) {
            neighbors.Add(new HashSet<int>());
        }

        foreach (KeyValuePair<(int a, int b), List<WallCandidate>> kv in candidates) {
            int a = kv.Key.a;
            int b = kv.Key.b;
            if (a >= 0 && a < roomCount && b >= 0 && b < roomCount) {
                neighbors[a].Add(b);
                neighbors[b].Add(a);
            }
        }

        return neighbors;
    }

    private List<int> FindRoomPathBFS(List<HashSet<int>> neighbors, int start, int goal) {
        int n = neighbors.Count;
        int[] prev = new int[n];
        bool[] visited = new bool[n];
        for (int i = 0; i < n; i++) {
            prev[i] = -1;
        }

        Queue<int> q = new();
        visited[start] = true;
        q.Enqueue(start);

        while (q.Count > 0) {
            int cur = q.Dequeue();
            if (cur == goal) {
                break;
            }

            foreach (int nxt in neighbors[cur]) {
                if (visited[nxt]) {
                    continue;
                }

                visited[nxt] = true;
                prev[nxt] = cur;
                q.Enqueue(nxt);
            }
        }

        if (!visited[goal]) {
            return null;
        }

        List<int> path = new();
        int t = goal;
        while (t != -1) {
            path.Add(t);
            if (t == start) {
                break;
            }

            t = prev[t];
        }

        path.Reverse();
        return path;
    }

    private int CarveDoorsAlongRoomPath(
        bool[,] walls,
        List<int> roomPath,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng) {
        int carved = 0;

        for (int i = 0; i < roomPath.Count - 1; i++) {
            int ra = roomPath[i];
            int rb = roomPath[i + 1];

            int a = Mathf.Min(ra, rb);
            int b = Mathf.Max(ra, rb);

            if (!candidates.TryGetValue((a, b), out List<WallCandidate> list) || list.Count == 0) {
                continue;
            }

            WallCandidate chosen = PickBestCandidateByOpenness(walls, list, rng);

            if (walls[chosen.wall.y, chosen.wall.x]) {
                walls[chosen.wall.y, chosen.wall.x] = false;
                carved++;
            }
        }

        return carved;
    }

    private WallCandidate PickBestCandidateByOpenness(bool[,] walls, List<WallCandidate> list, System.Random rng) {
        int bestScore = int.MinValue;
        WallCandidate best = list[0];

        for (int i = 0; i < list.Count; i++) {
            WallCandidate c = list[i];
            int score = CountOpen4(walls, c.aSide) + CountOpen4(walls, c.bSide);

            if (score > bestScore || (score == bestScore && rng.Next(2) == 0)) {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    private int CountOpen4(bool[,] walls, Vector2Int p) {
        int open = 0;
        if (!walls[p.y, p.x + 1]) {
            open++;
        }

        if (!walls[p.y, p.x - 1]) {
            open++;
        }

        if (!walls[p.y + 1, p.x]) {
            open++;
        }

        if (!walls[p.y - 1, p.x]) {
            open++;
        }

        return open;
    }

    private void AddLoopDoors(
        bool[,] walls,
        List<int> roomPath,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng,
        int loopDoorsToAdd) {
        if (loopDoorsToAdd <= 0) {
            return;
        }

        HashSet<(int a, int b)> pathEdges = new();
        for (int i = 0; i < roomPath.Count - 1; i++) {
            int a = Mathf.Min(roomPath[i], roomPath[i + 1]);
            int b = Mathf.Max(roomPath[i], roomPath[i + 1]);
            pathEdges.Add((a, b));
        }

        List<(int a, int b)> edgeKeys = new(candidates.Count);
        foreach (KeyValuePair<(int a, int b), List<WallCandidate>> kv in candidates) {
            if (pathEdges.Contains(kv.Key)) {
                continue;
            }

            if (kv.Value == null || kv.Value.Count < 2) {
                continue;
            }

            edgeKeys.Add(kv.Key);
        }

        if (edgeKeys.Count == 0) {
            return;
        }

        edgeKeys.Sort((e1, e2) => candidates[e2].Count.CompareTo(candidates[e1].Count));

        int added = 0;
        int idx = 0;
        while (added < loopDoorsToAdd && idx < edgeKeys.Count) {
            (int a, int b) key = edgeKeys[idx++];
            List<WallCandidate> list = candidates[key];
            WallCandidate chosen = PickBestCandidateByOpenness(walls, list, rng);

            if (!walls[chosen.wall.y, chosen.wall.x]) {
                continue;
            }

            walls[chosen.wall.y, chosen.wall.x] = false;
            added++;
        }
    }

    private void AddLoopDoors_NoPath(
        bool[,] walls,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng,
        int loopDoorsToAdd) {
        if (loopDoorsToAdd <= 0) {
            return;
        }

        List<(int a, int b)> edgeKeys = new(candidates.Count);
        foreach (KeyValuePair<(int a, int b), List<WallCandidate>> kv in candidates) {
            if (kv.Value == null || kv.Value.Count < 3) {
                continue;
            }

            edgeKeys.Add(kv.Key);
        }

        if (edgeKeys.Count == 0) {
            return;
        }

        edgeKeys.Sort((e1, e2) => candidates[e2].Count.CompareTo(candidates[e1].Count));

        int added = 0;
        int idx = 0;
        while (added < loopDoorsToAdd && idx < edgeKeys.Count) {
            (int a, int b) key = edgeKeys[idx++];
            List<WallCandidate> list = candidates[key];
            WallCandidate chosen = PickBestCandidateByOpenness(walls, list, rng);

            if (!walls[chosen.wall.y, chosen.wall.x]) {
                continue;
            }

            walls[chosen.wall.y, chosen.wall.x] = false;
            added++;
        }
    }

    private bool CarveMinimalWallTunnel(bool[,] walls, Vector2Int start, Vector2Int goal) {
        if (start == goal) {
            return true;
        }

        int H = walls.GetLength(0);
        int W = walls.GetLength(1);

        int[,] dist = new int[H, W];
        Vector2Int[,] prev = new Vector2Int[H, W];
        bool[,] hasPrev = new bool[H, W];

        const int INF = int.MaxValue / 4;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) {
            dist[y, x] = INF;
        }

        LinkedList<Vector2Int> dq = new();
        dist[start.y, start.x] = 0;
        dq.AddFirst(start);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (dq.Count > 0) {
            Vector2Int cur = dq.First.Value;
            dq.RemoveFirst();

            if (cur == goal) {
                break;
            }

            int curD = dist[cur.y, cur.x];

            for (int i = 0; i < 4; i++) {
                int nx = cur.x + dx[i];
                int ny = cur.y + dy[i];

                if (nx < 1 || nx >= W - 1 || ny < 1 || ny >= H - 1) {
                    continue;
                }

                int wCost = walls[ny, nx] ? 1 : 0;
                int nd = curD + wCost;

                if (nd < dist[ny, nx]) {
                    dist[ny, nx] = nd;
                    prev[ny, nx] = cur;
                    hasPrev[ny, nx] = true;

                    Vector2Int np = new(nx, ny);
                    if (wCost == 0) {
                        dq.AddFirst(np);
                    } else {
                        dq.AddLast(np);
                    }
                }
            }
        }

        if (dist[goal.y, goal.x] == INF) {
            return false;
        }

        Vector2Int p = goal;
        while (p != start) {
            if (walls[p.y, p.x]) {
                walls[p.y, p.x] = false;
            }

            if (!hasPrev[p.y, p.x]) {
                break;
            }

            p = prev[p.y, p.x];
        }

        walls[start.y, start.x] = false;
        walls[goal.y, goal.x] = false;

        return true;
    }

    private bool IsReachable(bool[,] walls, Vector2Int start, Vector2Int goal) {
        if (walls[start.y, start.x] || walls[goal.y, goal.x]) {
            return false;
        }

        bool[,] visited = new bool[height, width];
        Queue<Vector2Int> q = new(1024);

        visited[start.y, start.x] = true;
        q.Enqueue(start);

        while (q.Count > 0) {
            Vector2Int c = q.Dequeue();
            if (c == goal) {
                return true;
            }

            TryPush(c.x + 1, c.y);
            TryPush(c.x - 1, c.y);
            TryPush(c.x, c.y + 1);
            TryPush(c.x, c.y - 1);

            void TryPush(int nx, int ny) {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) {
                    return;
                }

                if (visited[ny, nx]) {
                    return;
                }

                if (walls[ny, nx]) {
                    return;
                }

                visited[ny, nx] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return false;
    }

    // =========================================================
    // [SECTION B] SPAWN 표시 파트 (Shader.Find 제거 버전)
    // =========================================================
    private void ApplySpawnMarkersAndDebug(Vector2Int zombieCell, Vector2Int targetCell) {
        float topY = GetTopSurfaceY(basePart);

        Vector3 zombieWorld = CellToWorldCenter(zombieCell.x, zombieCell.y, topY + capsuleYOffset);
        Vector3 targetWorld = CellToWorldCenter(targetCell.x, targetCell.y, topY + capsuleYOffset);

        if (zombieSpawnMarker != null) {
            zombieSpawnMarker.position = zombieWorld;
        }

        if (targetSpawnMarker != null) {
            targetSpawnMarker.position = targetWorld;
        }

        if (!spawnDebugCapsules) {
            return;
        }

        Transform parent = wallParent != null ? wallParent : transform;

        // ✅ 인스펙터에 할당된 머티리얼을 그대로 쓰고, 필요하면 property block으로 색만 덮어씀.
        CreateSpawnCapsule("SpawnDebug_Zombie", zombieWorld, parent, zombieSpawnDebugMaterial, Color.green);
        CreateSpawnCapsule("SpawnDebug_Target", targetWorld, parent, targetSpawnDebugMaterial, Color.red);
    }

    private void CreateSpawnCapsule(string name, Vector3 position, Transform parent, Material overrideMat, Color tint) {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, true);

        float radiusScale = capsuleRadius / 0.5f;
        float heightScale = capsuleHeight / 2.0f;

        go.transform.position = position + (Vector3.up * (capsuleHeight * 0.5f));
        go.transform.localScale = new Vector3(radiusScale, heightScale, radiusScale);

        Renderer r = go.GetComponent<Renderer>();
        if (r != null) {
            // 1) 머티리얼은 “주어진 것을 그대로” 사용 (셰이더 관련 코드 없음)
            if (overrideMat != null) {
                r.sharedMaterial = overrideMat; // shared: 인스턴스 머티리얼 무한 생성 방지
            }

            // 2) 색만 다르게 하고 싶으면 MaterialPropertyBlock 사용(머티리얼 자체는 유지)
            if (tintWithPropertyBlock) {
                _mpb.Clear();

                // URP/HDRP 계열: _BaseColor, Built-in Standard: _Color
                _mpb.SetColor("_BaseColor", tint);
                _mpb.SetColor("_Color", tint);

                r.SetPropertyBlock(_mpb);
            }
        }

        Collider col = go.GetComponent<Collider>();
        if (col != null) {
            col.isTrigger = true;
        }

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private Vector3 CellToWorldCenter(int x, int y, float yWorld) {
        float originX = basePart.position.x - (width * cellSize * 0.5f) + (cellSize * 0.5f);
        float originZ = basePart.position.z - (height * cellSize * 0.5f) + (cellSize * 0.5f);

        float wx = originX + (x * cellSize);
        float wz = originZ + (y * cellSize);

        return new Vector3(wx, yWorld, wz);
    }

    // =========================================================
    // [SECTION C] VISUAL BUILD 파트 (벽 생성)
    // =========================================================
    private void SpawnWalls(bool[,] walls) {
        Transform parent = wallParent != null ? wallParent : transform;

        float topY = GetTopSurfaceY(basePart);
        float wallCenterY = topY + (wallSize.y * 0.5f);

        float originX = basePart.position.x - (width * cellSize * 0.5f) + (cellSize * 0.5f);
        float originZ = basePart.position.z - (height * cellSize * 0.5f) + (cellSize * 0.5f);

        for (int y = 0; y < height; y++) {
            float z = originZ + (y * cellSize);

            for (int x = 0; x < width; x++) {
                if (!walls[y, x]) {
                    continue;
                }

                float xx = originX + (x * cellSize);
                Vector3 pos = new(xx, wallCenterY, z);
                CreateWall(pos, parent);
            }
        }
    }

    private void CreateWall(Vector3 position, Transform parent) {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent, true);
        wall.transform.position = position;
        wall.transform.localScale = wallSize;

        Rigidbody rb = wall.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    // =========================================================
    // [UTIL]
    // =========================================================
    private void ClearSpawnedChildren() {
        Transform parent = wallParent != null ? wallParent : transform;

        List<GameObject> toDestroy = new(parent.childCount);
        for (int i = 0; i < parent.childCount; i++) {
            Transform ch = parent.GetChild(i);
            if (ch.name.StartsWith("Wall", StringComparison.Ordinal) ||
                ch.name.StartsWith("SpawnDebug", StringComparison.Ordinal)) {
                toDestroy.Add(ch.gameObject);
            }
        }

        for (int i = 0; i < toDestroy.Count; i++) {
            Destroy(toDestroy[i]);
        }
    }

    private static float GetTopSurfaceY(Transform t) {
        Collider c = t.GetComponentInChildren<Collider>();
        if (c != null) {
            return c.bounds.max.y;
        }

        Renderer r = t.GetComponentInChildren<Renderer>();
        if (r != null) {
            return r.bounds.max.y;
        }

        return t.position.y;
    }

    private System.Random CreateRng() {
        int s = useRandomSeed ? Environment.TickCount : seed;
        return new System.Random(s);
    }
}
