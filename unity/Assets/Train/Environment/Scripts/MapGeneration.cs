using System.Collections.Generic;
using UnityEngine;

public readonly struct MapGenerationConfig {
    public readonly int Width;
    public readonly int Height;
    public readonly float InitialWallFill;
    public readonly int MinSpawnManhattanDistance;
    public readonly float LoopDoorRatio;

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
    private struct RoomInfo {
        public int id;
        public int count;
        public int minX, maxX, minY, maxY;
    }

    private struct WallCandidate {
        public Vector2Int wall;
        public Vector2Int aSide;
        public Vector2Int bSide;
    }

    private static readonly Vector2Int[] Dir4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    public static bool[,] GenerateWallMatrix(MapGenerationConfig cfg, System.Random rng, out Vector2Int zombieSpawn,
        out Vector2Int targetSpawn) {
        // Generates wall matrix and ensures zombie->target reachability (or falls back to border-only).
        zombieSpawn = default;
        targetSpawn = default;

        bool[,] walls = GenerateWalls_WFCStub(cfg, rng);
        MakeBorderWalls(cfg, walls);

        if (!TryPickSpawns(cfg, walls, rng, out zombieSpawn, out targetSpawn)) {
            Debug.LogWarning("[MapGeneration] Failed to pick spawns. Falling back to border-only.");
            walls = new bool[cfg.Height, cfg.Width];
            MakeBorderWalls(cfg, walls);
            return walls;
        }

        bool ok = TryCarveConnectivityAndLoops(cfg, walls, rng, zombieSpawn, targetSpawn, out _);

        if (!ok) {
            Debug.LogWarning(
                "[MapGeneration] No room path found. Carving minimal tunnel, then rebuilding graph for loops.");

            if (!CarveMinimalWallTunnel(cfg, walls, zombieSpawn, targetSpawn)) {
                Debug.LogWarning("[MapGeneration] Tunnel carve failed. Falling back to border-only.");
                walls = new bool[cfg.Height, cfg.Width];
                MakeBorderWalls(cfg, walls);
                return walls;
            }

            TryCarveLoopsAfterTunnel(cfg, walls, rng, zombieSpawn, targetSpawn);
        }

        if (!IsReachable(cfg, walls, zombieSpawn, targetSpawn)) {
            Debug.LogWarning("[MapGeneration] Connectivity validation failed. Falling back to border-only.");
            walls = new bool[cfg.Height, cfg.Width];
            MakeBorderWalls(cfg, walls);
        }

        return walls;
    }

    private static bool[,] GenerateWalls_WFCStub(MapGenerationConfig cfg, System.Random rng) {
        // Creates random walls and applies two smoothing passes.
        bool[,] walls = new bool[cfg.Height, cfg.Width];

        for (int y = 1; y < cfg.Height - 1; y++)
        for (int x = 1; x < cfg.Width - 1; x++) {
            walls[y, x] = rng.NextDouble() < cfg.InitialWallFill;
        }

        SmoothWallsOnce(cfg, walls);
        SmoothWallsOnce(cfg, walls);

        return walls;
    }

    private static void SmoothWallsOnce(MapGenerationConfig cfg, bool[,] walls) {
        // Applies one cellular automata smoothing step.
        bool[,] tmp = (bool[,])walls.Clone();

        for (int y = 1; y < cfg.Height - 1; y++) {
            for (int x = 1; x < cfg.Width - 1; x++) {
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

    private static void MakeBorderWalls(MapGenerationConfig cfg, bool[,] walls) {
        // Forces map borders to be walls.
        for (int x = 0; x < cfg.Width; x++) {
            walls[0, x] = true;
            walls[cfg.Height - 1, x] = true;
        }

        for (int y = 0; y < cfg.Height; y++) {
            walls[y, 0] = true;
            walls[y, cfg.Width - 1] = true;
        }
    }

    private static bool TryPickSpawns(MapGenerationConfig cfg, bool[,] walls, System.Random rng, out Vector2Int zombie,
        out Vector2Int target) {
        // Picks two empty cells with minimum Manhattan distance.
        zombie = default;
        target = default;

        const int tries = 30000;

        if (!TryPickEmptyCell(cfg, walls, rng, tries, out zombie)) {
            return false;
        }

        for (int i = 0; i < tries; i++) {
            Vector2Int p = PickRandomInnerCell(cfg, rng);
            if (walls[p.y, p.x]) {
                continue;
            }

            int dist = Mathf.Abs(p.x - zombie.x) + Mathf.Abs(p.y - zombie.y);
            if (dist >= cfg.MinSpawnManhattanDistance) {
                target = p;
                return true;
            }
        }

        return false;
    }

    private static bool TryPickEmptyCell(MapGenerationConfig cfg, bool[,] walls, System.Random rng, int tries,
        out Vector2Int cell) {
        // Finds a random empty cell within inner bounds.
        cell = default;

        for (int i = 0; i < tries; i++) {
            Vector2Int p = PickRandomInnerCell(cfg, rng);
            if (!walls[p.y, p.x]) {
                cell = p;
                return true;
            }
        }

        return false;
    }

    private static Vector2Int PickRandomInnerCell(MapGenerationConfig cfg, System.Random rng) {
        // Returns a random cell excluding border.
        int x = rng.Next(1, cfg.Width - 1);
        int y = rng.Next(1, cfg.Height - 1);
        return new Vector2Int(x, y);
    }

    private static bool TryCarveConnectivityAndLoops(
        MapGenerationConfig cfg,
        bool[,] walls,
        System.Random rng,
        Vector2Int zombieSpawn,
        Vector2Int targetSpawn,
        out int minDoorsCarved) {
        // Connects start/goal rooms and optionally adds loop doors.
        minDoorsCarved = 0;

        int[,] roomId = new int[cfg.Height, cfg.Width];
        List<RoomInfo> rooms = ExtractRooms(cfg, walls, roomId);

        int startRoom = roomId[zombieSpawn.y, zombieSpawn.x];
        int goalRoom = roomId[targetSpawn.y, targetSpawn.x];

        if (startRoom < 0 || goalRoom < 0) {
            return false;
        }

        if (startRoom == goalRoom) {
            return true;
        }

        Dictionary<(int a, int b), List<WallCandidate>> edgeCandidates = BuildAdjacencyCandidates(cfg, walls, roomId);
        if (edgeCandidates.Count == 0) {
            return false;
        }

        List<HashSet<int>> neighbors = BuildNeighbors(rooms.Count, edgeCandidates);
        List<int> roomPath = FindRoomPathBFS(neighbors, startRoom, goalRoom);
        if (roomPath == null || roomPath.Count == 0) {
            return false;
        }

        minDoorsCarved = CarveDoorsAlongRoomPath(walls, roomPath, edgeCandidates, rng);

        int loopDoors = Mathf.Max(0, Mathf.RoundToInt(minDoorsCarved * cfg.LoopDoorRatio));
        AddLoopDoors(walls, roomPath, edgeCandidates, rng, loopDoors);

        return true;
    }

    private static void TryCarveLoopsAfterTunnel(MapGenerationConfig cfg, bool[,] walls, System.Random rng,
        Vector2Int zombieSpawn, Vector2Int targetSpawn) {
        // Recomputes room graph after tunnel and adds loops.
        int[,] roomId = new int[cfg.Height, cfg.Width];
        List<RoomInfo> rooms = ExtractRooms(cfg, walls, roomId);

        Dictionary<(int a, int b), List<WallCandidate>> edgeCandidates = BuildAdjacencyCandidates(cfg, walls, roomId);
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
        int loopDoors = Mathf.Max(2, Mathf.RoundToInt(baseDoors * cfg.LoopDoorRatio));
        AddLoopDoors(walls, roomPath, edgeCandidates, rng, loopDoors);
    }

    private static List<RoomInfo> ExtractRooms(MapGenerationConfig cfg, bool[,] walls, int[,] roomId) {
        // Flood-fills connected empty regions and assigns room IDs.
        for (int y = 0; y < cfg.Height; y++)
        for (int x = 0; x < cfg.Width; x++) {
            roomId[y, x] = -1;
        }

        List<RoomInfo> rooms = new(256);
        Queue<Vector2Int> q = new(1024);

        int nextId = 0;

        for (int y = 1; y < cfg.Height - 1; y++) {
            for (int x = 1; x < cfg.Width - 1; x++) {
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

                    TryVisitRoomCell(cfg, c + Dir4[0], id, walls, roomId, q);
                    TryVisitRoomCell(cfg, c + Dir4[1], id, walls, roomId, q);
                    TryVisitRoomCell(cfg, c + Dir4[2], id, walls, roomId, q);
                    TryVisitRoomCell(cfg, c + Dir4[3], id, walls, roomId, q);
                }

                rooms.Add(info);
            }
        }

        return rooms;
    }

    private static void TryVisitRoomCell(
        MapGenerationConfig cfg,
        Vector2Int cell,
        int id,
        bool[,] walls,
        int[,] roomId,
        Queue<Vector2Int> q) {
        // Enqueues an unvisited empty cell for room flood fill.
        int nx = cell.x;
        int ny = cell.y;

        if (nx <= 0 || nx >= cfg.Width - 1 || ny <= 0 || ny >= cfg.Height - 1) {
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

    private static Dictionary<(int a, int b), List<WallCandidate>> BuildAdjacencyCandidates(MapGenerationConfig cfg,
        bool[,] walls, int[,] roomId) {
        // Collects removable wall cells that separate two different rooms.
        Dictionary<(int a, int b), List<WallCandidate>> dict = new(2048);

        for (int y = 1; y < cfg.Height - 1; y++) {
            for (int x = 1; x < cfg.Width - 1; x++) {
                if (!walls[y, x]) {
                    continue;
                }

                // Left-right
                int lx = x - 1, rx = x + 1;
                if (!walls[y, lx] && !walls[y, rx]) {
                    int ra = roomId[y, lx];
                    int rb = roomId[y, rx];
                    if (ra >= 0 && rb >= 0 && ra != rb) {
                        AddCandidate(dict, ra, rb,
                            new WallCandidate {
                                wall = new Vector2Int(x, y),
                                aSide = new Vector2Int(lx, y),
                                bSide = new Vector2Int(rx, y)
                            });
                    }
                }

                // Up-down
                int uy = y - 1, dy = y + 1;
                if (!walls[uy, x] && !walls[dy, x]) {
                    int ra = roomId[uy, x];
                    int rb = roomId[dy, x];
                    if (ra >= 0 && rb >= 0 && ra != rb) {
                        AddCandidate(dict, ra, rb,
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
    }

    private static void AddCandidate(Dictionary<(int a, int b), List<WallCandidate>> dict, int ra, int rb,
        WallCandidate c) {
        // Adds a candidate under an undirected (min,max) room-pair key.
        int a = Mathf.Min(ra, rb);
        int b = Mathf.Max(ra, rb);
        (int a, int b) key = (a, b);

        if (!dict.TryGetValue(key, out List<WallCandidate> list)) {
            list = new List<WallCandidate>(8);
            dict[key] = list;
        }

        list.Add(c);
    }

    private static List<HashSet<int>> BuildNeighbors(int roomCount,
        Dictionary<(int a, int b), List<WallCandidate>> candidates) {
        // Builds room adjacency sets from candidate edges.
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

    private static List<int> FindRoomPathBFS(List<HashSet<int>> neighbors, int start, int goal) {
        // Finds a room-to-room path using BFS over room graph.
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

    private static int CarveDoorsAlongRoomPath(
        bool[,] walls,
        List<int> roomPath,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng) {
        // Carves one door between each consecutive room pair on the path.
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

    private static WallCandidate
        PickBestCandidateByOpenness(bool[,] walls, List<WallCandidate> list, System.Random rng) {
        // Picks door candidate maximizing local openness to reduce chokepoints.
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

    private static int CountOpen4(bool[,] walls, Vector2Int p) {
        // Counts open neighbors in 4-neighborhood.
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

    private static void AddLoopDoors(
        bool[,] walls,
        List<int> roomPath,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng,
        int loopDoorsToAdd) {
        // Adds extra doors off the main path to introduce loops.
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

    private static void AddLoopDoors_NoPath(
        bool[,] walls,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng,
        int loopDoorsToAdd) {
        // Adds loop doors without relying on a known start-goal room path.
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

    private static bool CarveMinimalWallTunnel(MapGenerationConfig cfg, bool[,] walls, Vector2Int start,
        Vector2Int goal) {
        // Carves a minimal-cost path (0-1 BFS) where breaking walls costs 1.
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

        while (dq.Count > 0) {
            Vector2Int cur = dq.First.Value;
            dq.RemoveFirst();

            if (cur == goal) {
                break;
            }

            int curD = dist[cur.y, cur.x];

            for (int i = 0; i < Dir4.Length; i++) {
                int nx = cur.x + Dir4[i].x;
                int ny = cur.y + Dir4[i].y;

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

    private static bool IsReachable(MapGenerationConfig cfg, bool[,] walls, Vector2Int start, Vector2Int goal) {
        // Validates reachability using BFS on empty cells.
        if (walls[start.y, start.x] || walls[goal.y, goal.x]) {
            return false;
        }

        bool[,] visited = new bool[cfg.Height, cfg.Width];
        Queue<Vector2Int> q = new(1024);

        visited[start.y, start.x] = true;
        q.Enqueue(start);

        while (q.Count > 0) {
            Vector2Int c = q.Dequeue();
            if (c == goal) {
                return true;
            }

            for (int i = 0; i < Dir4.Length; i++) {
                int nx = c.x + Dir4[i].x;
                int ny = c.y + Dir4[i].y;

                if (nx < 0 || nx >= cfg.Width || ny < 0 || ny >= cfg.Height) {
                    continue;
                }

                if (visited[ny, nx]) {
                    continue;
                }

                if (walls[ny, nx]) {
                    continue;
                }

                visited[ny, nx] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return false;
    }
}
