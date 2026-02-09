using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct MapGenerationConfig {
    public readonly int Width;
    public readonly int Height;
    public readonly float InitialWallFill; // 0~1
    public readonly int MinSpawnManhattanDistance; // start-target 최소 맨해튼 거리
    public readonly float LoopDoorRatio; // 필수 문 개수 대비 추가 루프 문 비율

    public MapGenerationConfig(int width, int height, float initialWallFill, int minSpawnManhattanDistance,
        float loopDoorRatio) {
        Width = width;
        Height = height;
        InitialWallFill = initialWallFill;
        MinSpawnManhattanDistance = minSpawnManhattanDistance;
        LoopDoorRatio = loopDoorRatio;
    }
}

public static class MapGeneration {
    // ---------------------------------------------------------
    // Data
    // ---------------------------------------------------------
    private readonly struct DoorCandidate {
        public readonly Vector2Int Wall; // 뚫을 벽 셀
        public readonly Vector2Int SideA; // 벽 양쪽의 빈칸
        public readonly Vector2Int SideB;

        public DoorCandidate(Vector2Int wall, Vector2Int a, Vector2Int b) {
            Wall = wall;
            SideA = a;
            SideB = b;
        }
    }

    private static readonly Vector2Int[] Dir4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    // ---------------------------------------------------------
    // Public API
    // ---------------------------------------------------------
    public static bool[,] GenerateWallMatrix(MapGenerationConfig cfg, System.Random rng, out Vector2Int zombieSpawn,
        out Vector2Int targetSpawn) {
        zombieSpawn = default;
        targetSpawn = default;

        bool[,] wallMap = CreateRandomWallMap(cfg, rng);
        ApplyBorderWalls(cfg, wallMap);

        if (!TryPickSpawns(cfg, wallMap, rng, out zombieSpawn, out targetSpawn)) {
            Debug.LogWarning("[MapGeneration] Spawn pick failed -> border only.");
            return CreateBorderOnly(cfg);
        }

        // 1) 룸 그래프 기반 연결/루프 시도
        if (!TryConnectRoomsAndAddLoops(cfg, wallMap, rng, zombieSpawn, targetSpawn)) {
            // 2) 실패하면 0-1 BFS로 최소 벽 파괴 터널 뚫고, 그 다음 루프만 다시 시도
            Debug.LogWarning("[MapGeneration] No room path -> carve minimal tunnel then loops.");
            if (!CarveMinimalTunnel01Bfs(cfg, wallMap, zombieSpawn, targetSpawn)) {
                Debug.LogWarning("[MapGeneration] Tunnel failed -> border only.");
                return CreateBorderOnly(cfg);
            }

            // 터널 이후에는 그래프가 바뀌므로 루프만 간단히 추가(가능하면)
            TryAddLoopsAfterTunnel(cfg, wallMap, rng, zombieSpawn, targetSpawn);
        }

        // 3) 최종 검증
        if (!IsReachable(cfg, wallMap, zombieSpawn, targetSpawn)) {
            Debug.LogWarning("[MapGeneration] Reachability validation failed -> border only.");
            return CreateBorderOnly(cfg);
        }

        return wallMap;
    }

    // ---------------------------------------------------------
    // Step A: initial walls
    // ---------------------------------------------------------
    private static bool[,] CreateRandomWallMap(MapGenerationConfig cfg, System.Random rng) {
        bool[,] wallMap = new bool[cfg.Height, cfg.Width];

        for (int y = 1; y < cfg.Height - 1; y++) {
            for (int x = 1; x < cfg.Width - 1; x++) {
                wallMap[y, x] = rng.NextDouble() < cfg.InitialWallFill;
            }
        }

        // 2-pass smoothing
        SmoothOnce(cfg, wallMap);
        SmoothOnce(cfg, wallMap);
        return wallMap;
    }

    private static void SmoothOnce(MapGenerationConfig cfg, bool[,] wallMap) {
        bool[,] src = (bool[,])wallMap.Clone();

        for (int y = 1; y < cfg.Height - 1; y++) {
            for (int x = 1; x < cfg.Width - 1; x++) {
                int walls8 = 0;
                for (int dy = -1; dy <= 1; dy++) {
                    for (int dx = -1; dx <= 1; dx++) {
                        if (dx == 0 && dy == 0) {
                            continue;
                        }

                        if (src[y + dy, x + dx]) {
                            walls8++;
                        }
                    }
                }

                // 단순 CA 규칙
                if (walls8 >= 5) {
                    wallMap[y, x] = true;
                } else if (walls8 <= 2) {
                    wallMap[y, x] = false;
                }
            }
        }
    }

    private static void ApplyBorderWalls(MapGenerationConfig cfg, bool[,] wallMap) {
        int w = cfg.Width, h = cfg.Height;

        for (int x = 0; x < w; x++) {
            wallMap[0, x] = true;
            wallMap[h - 1, x] = true;
        }

        for (int y = 0; y < h; y++) {
            wallMap[y, 0] = true;
            wallMap[y, w - 1] = true;
        }
    }

    private static bool[,] CreateBorderOnly(MapGenerationConfig cfg) {
        bool[,] wallMap = new bool[cfg.Height, cfg.Width];
        ApplyBorderWalls(cfg, wallMap);
        return wallMap;
    }

    // ---------------------------------------------------------
    // Step B: spawn pick
    // ---------------------------------------------------------
    private static bool TryPickSpawns(MapGenerationConfig cfg, bool[,] wallMap, System.Random rng, out Vector2Int start,
        out Vector2Int goal) {
        start = default;
        goal = default;

        const int tries = 30000;

        if (!TryPickEmptyCell(cfg, wallMap, rng, tries, out start)) {
            return false;
        }

        for (int i = 0; i < tries; i++) {
            Vector2Int p = RandomInnerCell(cfg, rng);
            if (wallMap[p.y, p.x]) {
                continue;
            }

            int manhattan = Mathf.Abs(p.x - start.x) + Mathf.Abs(p.y - start.y);
            if (manhattan >= cfg.MinSpawnManhattanDistance) {
                goal = p;
                return true;
            }
        }

        return false;
    }

    private static bool TryPickEmptyCell(MapGenerationConfig cfg, bool[,] wallMap, System.Random rng, int tries,
        out Vector2Int cell) {
        cell = default;
        for (int i = 0; i < tries; i++) {
            Vector2Int p = RandomInnerCell(cfg, rng);
            if (!wallMap[p.y, p.x]) {
                cell = p;
                return true;
            }
        }

        return false;
    }

    private static Vector2Int RandomInnerCell(MapGenerationConfig cfg, System.Random rng) =>
        new(
            rng.Next(1, cfg.Width - 1),
            rng.Next(1, cfg.Height - 1)
        );

    // ---------------------------------------------------------
    // Step C: rooms + mandatory connection + loops (simple)
    // ---------------------------------------------------------
    private static bool TryConnectRoomsAndAddLoops(
        MapGenerationConfig cfg,
        bool[,] wallMap,
        System.Random rng,
        Vector2Int start,
        Vector2Int goal
    ) {
        // 1) 룸 라벨링
        int[,] roomLabel;
        int roomCount = LabelRooms(cfg, wallMap, out roomLabel);

        int startRoom = roomLabel[start.y, start.x];
        int goalRoom = roomLabel[goal.y, goal.x];
        if (startRoom < 0 || goalRoom < 0) {
            return false;
        }

        if (startRoom == goalRoom) {
            return true;
        }

        // 2) 룸-룸 사이의 "문 후보(벽)" 수집
        Dictionary<(int a, int b), List<DoorCandidate>> edgeToCandidates =
            CollectDoorCandidates(cfg, wallMap, roomLabel);
        if (edgeToCandidates.Count == 0) {
            return false;
        }

        // 3) 룸 그래프 BFS로 roomPath 찾기
        List<HashSet<int>> neighbors = BuildRoomNeighbors(roomCount, edgeToCandidates);
        List<int> roomPath = FindRoomPathBfs(neighbors, startRoom, goalRoom);
        if (roomPath == null || roomPath.Count < 2) {
            return false;
        }

        // 4) 필수 연결: roomPath를 따라 문 1개씩 뚫기(후보 중 openness 최대)
        int mandatoryDoors = 0;
        for (int i = 0; i < roomPath.Count - 1; i++) {
            if (TryCarveBestDoorForEdge(wallMap, edgeToCandidates, roomPath[i], roomPath[i + 1], rng)) {
                mandatoryDoors++;
            }
        }

        // 5) 루프: 메인 경로 기반 “샛길” 방식으로만 추가(단순)
        int extraLoops = Mathf.Max(0, Mathf.RoundToInt(mandatoryDoors * cfg.LoopDoorRatio));
        AddLoopsFromMainPath(edgeToCandidates, neighbors, wallMap, roomPath, extraLoops, rng);

        return true;
    }

    private static int LabelRooms(MapGenerationConfig cfg, bool[,] wallMap, out int[,] roomLabel) {
        int w = cfg.Width, h = cfg.Height;
        roomLabel = new int[h, w];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
            roomLabel[y, x] = -1;
        }

        int nextId = 0;
        Queue<Vector2Int> q = new(2048);

        for (int y = 1; y < h - 1; y++) {
            for (int x = 1; x < w - 1; x++) {
                if (wallMap[y, x]) {
                    continue;
                }

                if (roomLabel[y, x] != -1) {
                    continue;
                }

                int id = nextId++;
                roomLabel[y, x] = id;
                q.Enqueue(new Vector2Int(x, y));

                while (q.Count > 0) {
                    Vector2Int c = q.Dequeue();
                    for (int i = 0; i < 4; i++) {
                        int nx = c.x + Dir4[i].x;
                        int ny = c.y + Dir4[i].y;
                        if (nx <= 0 || nx >= w - 1 || ny <= 0 || ny >= h - 1) {
                            continue;
                        }

                        if (wallMap[ny, nx]) {
                            continue;
                        }

                        if (roomLabel[ny, nx] != -1) {
                            continue;
                        }

                        roomLabel[ny, nx] = id;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }

        return nextId;
    }

    private static Dictionary<(int a, int b), List<DoorCandidate>> CollectDoorCandidates(
        MapGenerationConfig cfg,
        bool[,] wallMap,
        int[,] roomLabel
    ) {
        int w = cfg.Width, h = cfg.Height;
        Dictionary<(int a, int b), List<DoorCandidate>> dict = new(2048);

        for (int y = 1; y < h - 1; y++) {
            for (int x = 1; x < w - 1; x++) {
                if (!wallMap[y, x]) {
                    continue; // 후보는 "벽" 셀
                }

                // 좌-우가 둘 다 빈칸이면 두 룸을 잇는 문 후보
                if (!wallMap[y, x - 1] && !wallMap[y, x + 1]) {
                    int ra = roomLabel[y, x - 1];
                    int rb = roomLabel[y, x + 1];
                    AddCandidateIfValid(dict, ra, rb,
                        new DoorCandidate(new Vector2Int(x, y), new Vector2Int(x - 1, y), new Vector2Int(x + 1, y)));
                }

                // 상-하
                if (!wallMap[y - 1, x] && !wallMap[y + 1, x]) {
                    int ra = roomLabel[y - 1, x];
                    int rb = roomLabel[y + 1, x];
                    AddCandidateIfValid(dict, ra, rb,
                        new DoorCandidate(new Vector2Int(x, y), new Vector2Int(x, y - 1), new Vector2Int(x, y + 1)));
                }
            }
        }

        return dict;
    }

    private static void AddCandidateIfValid(Dictionary<(int a, int b), List<DoorCandidate>> dict, int ra, int rb,
        DoorCandidate c) {
        if (ra < 0 || rb < 0 || ra == rb) {
            return;
        }

        int a = Mathf.Min(ra, rb);
        int b = Mathf.Max(ra, rb);
        (int a, int b) key = (a, b);

        if (!dict.TryGetValue(key, out List<DoorCandidate> list)) {
            list = new List<DoorCandidate>(8);
            dict[key] = list;
        }

        list.Add(c);
    }

    private static List<HashSet<int>> BuildRoomNeighbors(int roomCount,
        Dictionary<(int a, int b), List<DoorCandidate>> edgeToCandidates) {
        List<HashSet<int>> neighbors = new(roomCount);
        for (int i = 0; i < roomCount; i++) {
            neighbors.Add(new HashSet<int>());
        }

        foreach (KeyValuePair<(int a, int b), List<DoorCandidate>> kv in edgeToCandidates) {
            neighbors[kv.Key.a].Add(kv.Key.b);
            neighbors[kv.Key.b].Add(kv.Key.a);
        }

        return neighbors;
    }

    private static List<int> FindRoomPathBfs(List<HashSet<int>> neighbors, int startRoom, int goalRoom) {
        int n = neighbors.Count;
        int[] prev = new int[n];
        bool[] visited = new bool[n];
        for (int i = 0; i < n; i++) {
            prev[i] = -1;
        }

        Queue<int> q = new();
        visited[startRoom] = true;
        q.Enqueue(startRoom);

        while (q.Count > 0) {
            int cur = q.Dequeue();
            if (cur == goalRoom) {
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

        if (!visited[goalRoom]) {
            return null;
        }

        List<int> path = new();
        for (int t = goalRoom; t != -1; t = prev[t]) {
            path.Add(t);
            if (t == startRoom) {
                break;
            }
        }

        path.Reverse();
        return path;
    }

    private static bool TryCarveBestDoorForEdge(
        bool[,] wallMap,
        Dictionary<(int a, int b), List<DoorCandidate>> edgeToCandidates,
        int roomA,
        int roomB,
        System.Random rng
    ) {
        int a = Mathf.Min(roomA, roomB);
        int b = Mathf.Max(roomA, roomB);

        if (!edgeToCandidates.TryGetValue((a, b), out List<DoorCandidate> list) || list == null || list.Count == 0) {
            return false;
        }

        DoorCandidate best = list[0];
        int bestScore = int.MinValue;

        for (int i = 0; i < list.Count; i++) {
            DoorCandidate c = list[i];
            int score = Open4Count(wallMap, c.SideA) + Open4Count(wallMap, c.SideB);
            if (score > bestScore || (score == bestScore && rng.Next(2) == 0)) {
                bestScore = score;
                best = c;
            }
        }

        if (!wallMap[best.Wall.y, best.Wall.x]) {
            return false;
        }

        wallMap[best.Wall.y, best.Wall.x] = false;
        return true;
    }

    private static int Open4Count(bool[,] wallMap, Vector2Int p) {
        int open = 0;
        if (!wallMap[p.y, p.x + 1]) {
            open++;
        }

        if (!wallMap[p.y, p.x - 1]) {
            open++;
        }

        if (!wallMap[p.y + 1, p.x]) {
            open++;
        }

        if (!wallMap[p.y - 1, p.x]) {
            open++;
        }

        return open;
    }

    private static void AddLoopsFromMainPath(
        Dictionary<(int a, int b), List<DoorCandidate>> edgeToCandidates,
        List<HashSet<int>> neighbors,
        bool[,] wallMap,
        List<int> mainPathRooms,
        int loopCount,
        System.Random rng
    ) {
        if (loopCount <= 0 || mainPathRooms == null || mainPathRooms.Count == 0) {
            return;
        }

        HashSet<int> pathSet = new(mainPathRooms);

        // 루프를 "메인 경로에서만 가지치기"로 단순화
        int safety = (loopCount * 12) + 24;
        int added = 0;

        while (added < loopCount && safety-- > 0) {
            int baseRoom = mainPathRooms[rng.Next(mainPathRooms.Count)];

            // 경로 밖 이웃들 후보
            List<int> outs = null;
            foreach (int nxt in neighbors[baseRoom]) {
                if (pathSet.Contains(nxt)) {
                    continue;
                }

                outs ??= new List<int>(8);
                outs.Add(nxt);
            }

            if (outs == null || outs.Count == 0) {
                continue;
            }

            int other = outs[rng.Next(outs.Count)];
            if (TryCarveBestDoorForEdge(wallMap, edgeToCandidates, baseRoom, other, rng)) {
                added++;
            }
        }
    }

    private static void TryAddLoopsAfterTunnel(MapGenerationConfig cfg, bool[,] wallMap, System.Random rng,
        Vector2Int start, Vector2Int goal) {
        int[,] roomLabel;
        int roomCount = LabelRooms(cfg, wallMap, out roomLabel);
        if (roomCount <= 0) {
            return;
        }

        Dictionary<(int a, int b), List<DoorCandidate>> edgeToCandidates =
            CollectDoorCandidates(cfg, wallMap, roomLabel);
        if (edgeToCandidates.Count == 0) {
            return;
        }

        List<HashSet<int>> neighbors = BuildRoomNeighbors(roomCount, edgeToCandidates);

        int startRoom = roomLabel[start.y, start.x];
        int goalRoom = roomLabel[goal.y, goal.x];

        List<int> roomPath = startRoom >= 0 && goalRoom >= 0
            ? FindRoomPathBfs(neighbors, startRoom, goalRoom)
            : null;

        // 터널 이후엔 대충 2~8개 정도 루프 시도 (너무 공격적이지 않게)
        int loopCount = Mathf.Clamp(Mathf.RoundToInt(6 * cfg.LoopDoorRatio) + 2, 2, 8);

        if (roomPath != null && roomPath.Count >= 2) {
            AddLoopsFromMainPath(edgeToCandidates, neighbors, wallMap, roomPath, loopCount, rng);
        } else {
            // 경로를 못 찾으면: 엣지 중 랜덤 몇 개만 골라 문 뚫기
            AddLoopsNoPath(edgeToCandidates, wallMap, loopCount, rng);
        }
    }

    private static void AddLoopsNoPath(Dictionary<(int a, int b), List<DoorCandidate>> edgeToCandidates,
        bool[,] wallMap, int loopCount, System.Random rng) {
        if (loopCount <= 0) {
            return;
        }

        List<(int a, int b)> keys = new(edgeToCandidates.Count);
        foreach (KeyValuePair<(int a, int b), List<DoorCandidate>> kv in edgeToCandidates) {
            if (kv.Value == null || kv.Value.Count < 3) {
                continue;
            }

            keys.Add(kv.Key);
        }

        if (keys.Count == 0) {
            return;
        }

        // 후보 많은 엣지 우선
        keys.Sort((k1, k2) => edgeToCandidates[k2].Count.CompareTo(edgeToCandidates[k1].Count));

        int added = 0;
        int idx = 0;
        while (added < loopCount && idx < keys.Count) {
            (int a, int b) key = keys[idx++];
            List<DoorCandidate> list = edgeToCandidates[key];
            if (list == null || list.Count == 0) {
                continue;
            }

            // openness 최대 문 뚫기
            DoorCandidate best = list[0];
            int bestScore = int.MinValue;
            for (int i = 0; i < list.Count; i++) {
                DoorCandidate c = list[i];
                int score = Open4Count(wallMap, c.SideA) + Open4Count(wallMap, c.SideB);
                if (score > bestScore || (score == bestScore && rng.Next(2) == 0)) {
                    bestScore = score;
                    best = c;
                }
            }

            if (wallMap[best.Wall.y, best.Wall.x]) {
                wallMap[best.Wall.y, best.Wall.x] = false;
                added++;
            }
        }
    }

    // ---------------------------------------------------------
    // Step D: fallback connectivity (0-1 BFS tunnel)
    // ---------------------------------------------------------
    private static bool CarveMinimalTunnel01Bfs(MapGenerationConfig cfg, bool[,] wallMap, Vector2Int start,
        Vector2Int goal) {
        if (start == goal) {
            return true;
        }

        int w = cfg.Width, h = cfg.Height;
        const int INF = int.MaxValue / 4;

        int[,] dist = new int[h, w];
        Vector2Int[,] prev = new Vector2Int[h, w];
        bool[,] hasPrev = new bool[h, w];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) {
            dist[y, x] = INF;
        }

        LinkedList<Vector2Int> dq = new();
        dist[start.y, start.x] = 0;
        dq.AddFirst(start);

        while (dq.Count > 0) {
            Vector2Int cur = dq.First.Value;
            dq.RemoveFirst();
            if (cur == goal) {
                break;
            }

            int curD = dist[cur.y, cur.x];

            for (int i = 0; i < 4; i++) {
                int nx = cur.x + Dir4[i].x;
                int ny = cur.y + Dir4[i].y;

                // inner only
                if (nx < 1 || nx >= w - 1 || ny < 1 || ny >= h - 1) {
                    continue;
                }

                int cost = wallMap[ny, nx] ? 1 : 0;
                int nd = curD + cost;

                if (nd < dist[ny, nx]) {
                    dist[ny, nx] = nd;
                    prev[ny, nx] = cur;
                    hasPrev[ny, nx] = true;

                    Vector2Int np = new(nx, ny);
                    if (cost == 0) {
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

        // backtrack: 벽이면 부수고 열린 길로
        Vector2Int p = goal;
        while (p != start) {
            wallMap[p.y, p.x] = false;
            if (!hasPrev[p.y, p.x]) {
                break;
            }

            p = prev[p.y, p.x];
        }

        wallMap[start.y, start.x] = false;
        wallMap[goal.y, goal.x] = false;

        return true;
    }

    // ---------------------------------------------------------
    // Step E: final validation
    // ---------------------------------------------------------
    private static bool IsReachable(MapGenerationConfig cfg, bool[,] wallMap, Vector2Int start, Vector2Int goal) {
        if (wallMap[start.y, start.x] || wallMap[goal.y, goal.x]) {
            return false;
        }

        int w = cfg.Width, h = cfg.Height;
        bool[,] visited = new bool[h, w];
        Queue<Vector2Int> q = new(2048);

        visited[start.y, start.x] = true;
        q.Enqueue(start);

        while (q.Count > 0) {
            Vector2Int c = q.Dequeue();
            if (c == goal) {
                return true;
            }

            for (int i = 0; i < 4; i++) {
                int nx = c.x + Dir4[i].x;
                int ny = c.y + Dir4[i].y;

                if (nx < 0 || nx >= w || ny < 0 || ny >= h) {
                    continue;
                }

                if (visited[ny, nx]) {
                    continue;
                }

                if (wallMap[ny, nx]) {
                    continue;
                }

                visited[ny, nx] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return false;
    }
}
