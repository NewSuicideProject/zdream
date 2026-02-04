using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapWallSpawner : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private Transform basePart;   // Plane (256x256)
    [SerializeField] private Transform wallParent; // Optional parent for walls

    [Header("Spawn marker refs (existing EmptyObjects)")]
    [SerializeField] private Transform zombieSpawnMarker; // existing Empty in scene
    [SerializeField] private Transform targetSpawnMarker; // existing Empty in scene

    [Header("Spawn debug visuals")]
    [SerializeField] private bool spawnDebugCapsules = true;
    [SerializeField] private float capsuleHeight = 2.0f;
    [SerializeField] private float capsuleRadius = 0.5f;
    [SerializeField] private float capsuleYOffset = 0.05f;

    [Header("Grid")]
    [SerializeField] private int width = 256;
    [SerializeField] private int height = 256;
    [SerializeField] private float cellSize = 1f;

    [Header("Wall object")]
    [SerializeField] private Vector3 wallSize = new Vector3(1f, 5f, 1f);

    [Header("Generation - WFC stub params")]
    [Range(0f, 1f)]
    [SerializeField] private float initialWallFill = 0.38f; // 0.33~0.40 추천

    [Header("Generation - Connectivity params")]
    [SerializeField] private int minSpawnManhattanDistance = 80;
    [Range(0f, 1f)]
    [SerializeField] private float loopDoorRatio = 0.25f;

    [Header("Inevitable fence (must-pass)")]
    [SerializeField] private bool drawInevitableFenceGizmos = true;
    [SerializeField] private int maxCutOptions = 24; // SubFenceLink 후보가 너무 많을 때 상한

    [Header("Random")]
    [SerializeField] private int seed = 0;
    [SerializeField] private bool useRandomSeed = true;

    // ----------------------------
    // Internal structs
    // ----------------------------
    private struct RoomInfo
    {
        public int id;
        public int count;
        public int minX, maxX, minY, maxY;
    }

    private struct WallCandidate
    {
        public Vector2Int wall;   // 제거할 벽 셀
        public Vector2Int aSide;  // room A 쪽 인접 땅
        public Vector2Int bSide;  // room B 쪽 인접 땅
    }

    [Serializable]
    public sealed class InevitableFenceLink
    {
        public Vector2Int aCell;     // 바닥 셀 A
        public Vector2Int bCell;     // 바닥 셀 B (상하좌우 인접)
        public Vector3 worldCenter;  // 두 셀 사이 월드 중앙점 (펜스 설치 추천 위치)
    }

    // "펜스 하나"가 아니라, "펜스 옵션들의 묶음(SubFenceLink)"을 하나의 노드로 취급
    // options.Count == 1  -> 진짜 단일 필수 링크(bridge)
    // options.Count >= 2  -> 둘 중 하나는 반드시 지나게 되는 cut-set (SubFenceLink)
    [Serializable]
    public sealed class InevitableFenceNode
    {
        public List<InevitableFenceLink> options = new List<InevitableFenceLink>(8);
    }

    [SerializeField] private List<InevitableFenceNode> inevitableFenceNodes = new List<InevitableFenceNode>(4);
    public IReadOnlyList<InevitableFenceNode> InevitableFenceNodes => inevitableFenceNodes;

    private Vector2Int _zombieCell;
    private Vector2Int _targetCell;

    // =========================================================
    // Unity entry
    // =========================================================
    private void Start()
    {
        if (basePart == null)
        {
            Debug.LogError("[MapWallSpawner] BasePart not assigned.");
            return;
        }

        ClearSpawnedChildren();

        var rng = CreateRng();

        bool[,] wallMatrix = GenerateWallMatrix(rng, out _zombieCell, out _targetCell);

        ApplySpawnMarkersAndDebug(_zombieCell, _targetCell);

        // 항상 최소 1개 노드 생성 목표:
        // 1) s-t bridge가 있으면 단일 링크 노드 1개
        // 2) 없으면 layer cut-set(SubFenceLink) 노드 1개
        BuildInevitableFenceNodes(wallMatrix, _zombieCell, _targetCell, rng);

        SpawnWalls(wallMatrix);
    }

    private void ClearSpawnedChildren()
    {
        Transform parent = wallParent != null ? wallParent : transform;

        var toDestroy = new List<GameObject>(parent.childCount);
        for (int i = 0; i < parent.childCount; i++)
        {
            var ch = parent.GetChild(i);
            if (ch.name.StartsWith("Wall", StringComparison.Ordinal) ||
                ch.name.StartsWith("SpawnDebug", StringComparison.Ordinal))
            {
                toDestroy.Add(ch.gameObject);
            }
        }

        for (int i = 0; i < toDestroy.Count; i++)
            Destroy(toDestroy[i]);
    }

    // =========================================================
    // Generate pipeline
    // =========================================================
    private bool[,] GenerateWallMatrix(System.Random rng, out Vector2Int zombieSpawn, out Vector2Int targetSpawn)
    {
        zombieSpawn = default;
        targetSpawn = default;

        bool[,] walls = GenerateWalls_WFCStub(rng);
        MakeBorderWalls(walls);

        if (!TryPickSpawns(walls, rng, out zombieSpawn, out targetSpawn))
        {
            Debug.LogWarning("[MapWallSpawner] Failed to pick spawns. Falling back to border-only.");
            walls = new bool[height, width];
            MakeBorderWalls(walls);
            return walls;
        }

        bool ok = TryCarveConnectivityAndLoops(walls, rng, zombieSpawn, targetSpawn, out _);

        if (!ok)
        {
            Debug.LogWarning("[MapWallSpawner] No room path found. Carving minimal tunnel, then rebuilding graph for loops.");

            bool tunnelOk = CarveMinimalWallTunnel(walls, zombieSpawn, targetSpawn);
            if (!tunnelOk)
            {
                Debug.LogWarning("[MapWallSpawner] Tunnel carve failed. Falling back to border-only.");
                walls = new bool[height, width];
                MakeBorderWalls(walls);
                return walls;
            }

            TryCarveLoopsAfterTunnel(walls, rng, zombieSpawn, targetSpawn);
        }

        if (!IsReachable(walls, zombieSpawn, targetSpawn))
        {
            Debug.LogWarning("[MapWallSpawner] Connectivity validation failed. Falling back to border-only.");
            walls = new bool[height, width];
            MakeBorderWalls(walls);
        }

        return walls;
    }

    // =========================================================
    // Spawn marker placement + debug capsules
    // =========================================================
    private void ApplySpawnMarkersAndDebug(Vector2Int zombieCell, Vector2Int targetCell)
    {
        float topY = GetTopSurfaceY(basePart);
        Vector3 zombieWorld = CellToWorldCenter(zombieCell.x, zombieCell.y, topY + capsuleYOffset);
        Vector3 targetWorld = CellToWorldCenter(targetCell.x, targetCell.y, topY + capsuleYOffset);

        if (zombieSpawnMarker != null) zombieSpawnMarker.position = zombieWorld;
        if (targetSpawnMarker != null) targetSpawnMarker.position = targetWorld;

        if (!spawnDebugCapsules) return;

        Transform parent = wallParent != null ? wallParent : transform;
        CreateSpawnCapsule("SpawnDebug_Zombie", zombieWorld, parent, Color.green);
        CreateSpawnCapsule("SpawnDebug_Target", targetWorld, parent, Color.red);
    }

    // ✅ URP/HDRP/Built-in 모두에서 핑크 안 뜨게 처리
    private void CreateSpawnCapsule(string name, Vector3 position, Transform parent, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, true);

        float radiusScale = capsuleRadius / 0.5f;
        float heightScale = capsuleHeight / 2.0f;

        go.transform.position = position + Vector3.up * (capsuleHeight * 0.5f);
        go.transform.localScale = new Vector3(radiusScale, heightScale, radiusScale);

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            Shader sh =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("HDRP/Lit") ??
                Shader.Find("Standard");

            if (sh != null)
            {
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                r.material = mat;
            }
            else
            {
                Debug.LogWarning("[MapWallSpawner] No compatible shader found for spawn capsules.");
            }
        }

        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private Vector3 CellToWorldCenter(int x, int y, float yWorld)
    {
        float originX = basePart.position.x - (width * cellSize * 0.5f) + (cellSize * 0.5f);
        float originZ = basePart.position.z - (height * cellSize * 0.5f) + (cellSize * 0.5f);
        float wx = originX + x * cellSize;
        float wz = originZ + y * cellSize;
        return new Vector3(wx, yWorld, wz);
    }

    // =========================================================
    // Inevitable fence nodes (always at least 1 if reachable)
    //
    // 1) 먼저: s-t bridge(단일 필수 통로)가 있으면 options=1로 저장
    // 2) 없으면: BFS 거리 레이어를 이용한 cut-set(여러 후보 중 하나 반드시 통과)로 options>=2 생성
    // =========================================================
    private void BuildInevitableFenceNodes(bool[,] walls, Vector2Int start, Vector2Int goal, System.Random rng)
    {
        inevitableFenceNodes.Clear();

        if (!IsReachable(walls, start, goal))
            return;

        // 1) 단일 bridge가 있으면 그걸 1옵션 노드로
        if (TryFindSTBridgeAsSingleLink(walls, start, goal, out InevitableFenceLink bridgeLink))
        {
            var node = new InevitableFenceNode();
            node.options.Add(bridgeLink);
            inevitableFenceNodes.Add(node);
            return;
        }

        // 2) bridge가 없으면: 레이어 컷셋(SubFenceLink) 강제 생성 -> 항상 최소 1개 노드 보장
        if (TryBuildLayerCutSetNode(walls, start, goal, rng, out InevitableFenceNode cutNode))
        {
            inevitableFenceNodes.Add(cutNode);
            return;
        }

        // 3) 정말 드물게 여기까지 오면: 최단경로 중간 엣지 하나를 넣음(엄밀히 inevitable은 아닐 수 있음)
        if (TryPickAnyPathEdgeAsFallback(walls, start, goal, out InevitableFenceLink fallback))
        {
            var node = new InevitableFenceNode();
            node.options.Add(fallback);
            inevitableFenceNodes.Add(node);
        }
    }

    // (A) 엄밀 bridge(Tarjan) 대신, 기존에 쓰던 "bridge 집합 + bridge-tree 경로" 방식으로
    // s->t 경로에 포함되는 bridge가 있는지 찾고, 하나만 뽑아준다.
    private bool TryFindSTBridgeAsSingleLink(bool[,] walls, Vector2Int start, Vector2Int goal, out InevitableFenceLink link)
    {
        link = null;

        // BFS로 reachable만 마킹
        int N = width * height;
        bool[] reachable = new bool[N];
        int startIdx = start.y * width + start.x;
        int goalIdx = goal.y * width + goal.x;

        {
            var q = new Queue<int>(4096);
            q.Enqueue(startIdx);
            reachable[startIdx] = true;

            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % width;
                int cy = cur / width;

                TryPush(cx + 1, cy);
                TryPush(cx - 1, cy);
                TryPush(cx, cy + 1);
                TryPush(cx, cy - 1);

                void TryPush(int nx, int ny)
                {
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
                    if (walls[ny, nx]) return;

                    int ni = ny * width + nx;
                    if (reachable[ni]) return;
                    reachable[ni] = true;
                    q.Enqueue(ni);
                }
            }
        }

        if (!reachable[goalIdx]) return false;

        // Tarjan bridges (iterative)
        int[] disc = new int[N];
        int[] low = new int[N];
        int[] parent = new int[N];
        for (int i = 0; i < N; i++)
        {
            disc[i] = -1;
            low[i] = 0;
            parent[i] = -1;
        }

        var bridges = new HashSet<long>(256);

        int time = 0;
        var stack = new Stack<DFSFrame>(4096);
        stack.Push(new DFSFrame(startIdx, -1, 0, false));

        while (stack.Count > 0)
        {
            var f = stack.Pop();
            int u = f.u;

            if (!reachable[u]) continue;

            if (!f.entered)
            {
                if (disc[u] != -1) continue;

                disc[u] = low[u] = time++;
                parent[u] = f.parentIdx;

                f.entered = true;
                stack.Push(f);
                continue;
            }

            int ux = u % width;
            int uy = u / width;

            bool advanced = false;
            for (int dir = f.nextDir; dir < 4; dir++)
            {
                int vx = ux, vy = uy;
                switch (dir)
                {
                    case 0: vx = ux + 1; break;
                    case 1: vx = ux - 1; break;
                    case 2: vy = uy + 1; break;
                    case 3: vy = uy - 1; break;
                }

                f.nextDir = dir + 1;

                if (vx < 0 || vx >= width || vy < 0 || vy >= height) continue;
                if (walls[vy, vx]) continue;

                int v = vy * width + vx;
                if (!reachable[v]) continue;

                if (disc[v] == -1)
                {
                    stack.Push(f);
                    stack.Push(new DFSFrame(v, u, 0, false));
                    advanced = true;
                    break;
                }
                else if (v != parent[u])
                {
                    low[u] = Math.Min(low[u], disc[v]);
                }
            }

            if (advanced) continue;

            int p = parent[u];
            if (p != -1)
            {
                low[p] = Math.Min(low[p], low[u]);

                if (low[u] > disc[p])
                {
                    bridges.Add(EdgeKey(p, u));
                }
            }
        }

        if (bridges.Count == 0) return false;

        // 2-edge-connected components by BFS ignoring bridge edges
        int[] comp = new int[N];
        for (int i = 0; i < N; i++) comp[i] = -1;

        int compCount = 0;
        var cq = new Queue<int>(4096);

        for (int i = 0; i < N; i++)
        {
            if (!reachable[i]) continue;
            if (comp[i] != -1) continue;

            int cid = compCount++;
            comp[i] = cid;
            cq.Enqueue(i);

            while (cq.Count > 0)
            {
                int cur = cq.Dequeue();
                int cx = cur % width;
                int cy = cur / width;

                Visit(cx + 1, cy, cur);
                Visit(cx - 1, cy, cur);
                Visit(cx, cy + 1, cur);
                Visit(cx, cy - 1, cur);

                void Visit(int nx, int ny, int from)
                {
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
                    if (walls[ny, nx]) return;

                    int ni = ny * width + nx;
                    if (!reachable[ni]) return;
                    if (comp[ni] != -1) return;

                    if (bridges.Contains(EdgeKey(from, ni))) return;

                    comp[ni] = cid;
                    cq.Enqueue(ni);
                }
            }
        }

        int sComp = comp[startIdx];
        int tComp = comp[goalIdx];
        if (sComp < 0 || tComp < 0) return false;
        if (sComp == tComp) return false; // s-t bridge 없음

        // bridge-tree build with representative bridge edge per comp-edge
        var tree = new List<List<int>>(compCount);
        for (int i = 0; i < compCount; i++) tree.Add(new List<int>(4));

        var compEdgeToBridge = new Dictionary<long, (int u, int v)>(bridges.Count);

        foreach (long e in bridges)
        {
            int u = (int)(e >> 32);
            int v = (int)(e & 0xffffffff);

            int cu = comp[u];
            int cv = comp[v];
            if (cu < 0 || cv < 0 || cu == cv) continue;

            tree[cu].Add(cv);
            tree[cv].Add(cu);

            long ceKey = CompEdgeKey(cu, cv);
            if (!compEdgeToBridge.ContainsKey(ceKey))
                compEdgeToBridge[ceKey] = (u, v);
        }

        // BFS on tree
        int[] prevComp = new int[compCount];
        for (int i = 0; i < compCount; i++) prevComp[i] = -1;

        var q2 = new Queue<int>(256);
        q2.Enqueue(sComp);
        prevComp[sComp] = sComp;

        while (q2.Count > 0 && prevComp[tComp] == -1)
        {
            int cur = q2.Dequeue();
            var neigh = tree[cur];
            for (int i = 0; i < neigh.Count; i++)
            {
                int nxt = neigh[i];
                if (prevComp[nxt] != -1) continue;
                prevComp[nxt] = cur;
                q2.Enqueue(nxt);
            }
        }

        if (prevComp[tComp] == -1) return false;

        // reconstruct comp path
        var compPath = new List<int>(64);
        int ccur = tComp;
        while (ccur != sComp)
        {
            compPath.Add(ccur);
            ccur = prevComp[ccur];
            if (ccur == -1) break;
        }
        compPath.Add(sComp);
        compPath.Reverse();

        if (compPath.Count < 2) return false;

        // pick one mandatory bridge edge from the unique comp path (middle)
        int mid = (compPath.Count - 2) / 2;
        int ca = compPath[mid];
        int cb = compPath[mid + 1];

        long ce = CompEdgeKey(ca, cb);
        if (!compEdgeToBridge.TryGetValue(ce, out var uv))
            return false;

        link = MakeFenceLinkFromCellEdge(new Vector2Int(uv.u % width, uv.u / width),
                                         new Vector2Int(uv.v % width, uv.v / width));
        return link != null;

        static long EdgeKey(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }

        static long CompEdgeKey(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }
    }

    private struct DFSFrame
    {
        public int u;
        public int parentIdx;
        public int nextDir;
        public bool entered;

        public DFSFrame(int u, int parentIdx, int nextDir, bool entered)
        {
            this.u = u;
            this.parentIdx = parentIdx;
            this.nextDir = nextDir;
            this.entered = entered;
        }
    }

    // (B) 레이어 컷셋(SubFenceLink) 생성:
    // BFS 최단거리 distFromStart를 만들고, dist = d 레이어와 dist = d+1 레이어 사이의 모든 간선을 options로 넣는다.
    // goal이 dist> d인 한, 어떤 경로로든 dist를 0->...->goal까지 올려야 해서,
    // 최소 한 번은 d->d+1을 넘어야 하므로 options 중 하나는 반드시 지나게 된다.
    private bool TryBuildLayerCutSetNode(bool[,] walls, Vector2Int start, Vector2Int goal, System.Random rng, out InevitableFenceNode node)
    {
        node = null;

        int[,] dist = new int[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                dist[y, x] = -1;

        var q = new Queue<Vector2Int>(4096);
        dist[start.y, start.x] = 0;
        q.Enqueue(start);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c == goal) break;

            int cd = dist[c.y, c.x];

            for (int i = 0; i < 4; i++)
            {
                int nx = c.x + dx[i];
                int ny = c.y + dy[i];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                if (walls[ny, nx]) continue;
                if (dist[ny, nx] != -1) continue;

                dist[ny, nx] = cd + 1;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        int goalDist = dist[goal.y, goal.x];
        if (goalDist <= 1) return false; // 너무 가깝거나 dist 실패

        // 후보 레이어 d를 잡되, edge가 비면 주변으로 탐색
        int startD = Mathf.Clamp(goalDist / 2, 1, goalDist - 1);

        List<(Vector2Int a, Vector2Int b)> cutEdges = null;

        // d를 바꿔가며 (d, d+1) 경계 간선이 존재하는 레이어를 찾는다
        for (int step = 0; step < goalDist; step++)
        {
            int d = startD + ((step % 2 == 0) ? (step / 2) : -(step / 2 + 1));
            if (d < 1 || d >= goalDist) continue;

            var edges = CollectLayerBoundaryEdges(dist, d);
            if (edges.Count >= 2)
            {
                cutEdges = edges;
                break;
            }
        }

        if (cutEdges == null || cutEdges.Count == 0)
            return false;

        // 너무 많으면 랜덤 샘플/셔플 후 상한 적용
        ShuffleList(cutEdges, rng);

        int take = Mathf.Clamp(maxCutOptions, 2, cutEdges.Count);
        node = new InevitableFenceNode();

        var used = new HashSet<long>(take * 2);
        for (int i = 0; i < cutEdges.Count && node.options.Count < take; i++)
        {
            var e = cutEdges[i];
            long k = EdgeKeyCells(e.a, e.b);
            if (used.Contains(k)) continue;
            used.Add(k);

            var link = MakeFenceLinkFromCellEdge(e.a, e.b);
            if (link != null) node.options.Add(link);
        }

        return node.options.Count >= 2;

        List<(Vector2Int a, Vector2Int b)> CollectLayerBoundaryEdges(int[,] dists, int d)
        {
            var list = new List<(Vector2Int a, Vector2Int b)>(256);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (dists[y, x] != d) continue;

                    // check neighbors with dist == d+1
                    if (x + 1 < width && dists[y, x + 1] == d + 1) list.Add((new Vector2Int(x, y), new Vector2Int(x + 1, y)));
                    if (x - 1 >= 0    && dists[y, x - 1] == d + 1) list.Add((new Vector2Int(x, y), new Vector2Int(x - 1, y)));
                    if (y + 1 < height && dists[y + 1, x] == d + 1) list.Add((new Vector2Int(x, y), new Vector2Int(x, y + 1)));
                    if (y - 1 >= 0     && dists[y - 1, x] == d + 1) list.Add((new Vector2Int(x, y), new Vector2Int(x, y - 1)));
                }
            }

            return list;
        }

        static void ShuffleList<T>(List<T> list, System.Random r)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static long EdgeKeyCells(Vector2Int a, Vector2Int b)
        {
            // pack two cells into a stable edge key
            int a1 = (a.y << 16) ^ a.x;
            int b1 = (b.y << 16) ^ b.x;
            int lo = a1 < b1 ? a1 : b1;
            int hi = a1 < b1 ? b1 : a1;
            return ((long)(uint)lo << 32) | (uint)hi;
        }
    }

    // (C) 최후 폴백: 아무 최단경로 하나의 중앙 엣지
    private bool TryPickAnyPathEdgeAsFallback(bool[,] walls, Vector2Int start, Vector2Int goal, out InevitableFenceLink link)
    {
        link = null;

        var prev = new Vector2Int[height, width];
        var visited = new bool[height, width];
        var q = new Queue<Vector2Int>(4096);

        visited[start.y, start.x] = true;
        prev[start.y, start.x] = new Vector2Int(-1, -1);
        q.Enqueue(start);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        bool found = false;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c == goal) { found = true; break; }

            for (int i = 0; i < 4; i++)
            {
                int nx = c.x + dx[i];
                int ny = c.y + dy[i];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                if (walls[ny, nx]) continue;
                if (visited[ny, nx]) continue;

                visited[ny, nx] = true;
                prev[ny, nx] = c;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        if (!found) return false;

        // reconstruct path cells
        var path = new List<Vector2Int>(256);
        var p = goal;
        while (p.x != -1)
        {
            path.Add(p);
            p = prev[p.y, p.x];
        }
        path.Reverse();

        if (path.Count < 2) return false;

        int mid = (path.Count - 2) / 2;
        link = MakeFenceLinkFromCellEdge(path[mid], path[mid + 1]);
        return link != null;
    }

    private InevitableFenceLink MakeFenceLinkFromCellEdge(Vector2Int aCell, Vector2Int bCell)
    {
        // ensure adjacency
        int man = Mathf.Abs(aCell.x - bCell.x) + Mathf.Abs(aCell.y - bCell.y);
        if (man != 1) return null;

        float topY = GetTopSurfaceY(basePart) + capsuleYOffset;
        Vector3 aw = CellToWorldCenter(aCell.x, aCell.y, topY);
        Vector3 bw = CellToWorldCenter(bCell.x, bCell.y, topY);
        Vector3 center = (aw + bw) * 0.5f;

        return new InevitableFenceLink
        {
            aCell = aCell,
            bCell = bCell,
            worldCenter = center
        };
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawInevitableFenceGizmos) return;
        if (inevitableFenceNodes == null || inevitableFenceNodes.Count == 0) return;

        // Cyan spheres for all options. One node may have multiple options(SubFenceLink).
        Gizmos.color = Color.cyan;

        for (int n = 0; n < inevitableFenceNodes.Count; n++)
        {
            var node = inevitableFenceNodes[n];
            if (node == null || node.options == null) continue;

            for (int i = 0; i < node.options.Count; i++)
            {
                var opt = node.options[i];
                Gizmos.DrawSphere(opt.worldCenter + Vector3.up * 0.2f, 0.22f);
                Gizmos.DrawLine(opt.worldCenter + Vector3.up * 0.2f, opt.worldCenter + Vector3.up * 1.1f);
            }
        }
    }

    // =========================================================
    // Stage helper: try rooms/graph/path/carve doors + loops
    // =========================================================
    private bool TryCarveConnectivityAndLoops(
        bool[,] walls,
        System.Random rng,
        Vector2Int zombieSpawn,
        Vector2Int targetSpawn,
        out int minDoorsCarved)
    {
        minDoorsCarved = 0;

        int[,] roomId = new int[height, width];
        var rooms = ExtractRooms(walls, roomId);

        int startRoom = roomId[zombieSpawn.y, zombieSpawn.x];
        int goalRoom  = roomId[targetSpawn.y, targetSpawn.x];

        if (startRoom < 0 || goalRoom < 0) return false;
        if (startRoom == goalRoom) return true;

        var edgeCandidates = BuildAdjacencyCandidates(walls, roomId);
        if (edgeCandidates.Count == 0) return false;

        var neighbors = BuildNeighbors(rooms.Count, edgeCandidates);
        List<int> roomPath = FindRoomPathBFS(neighbors, startRoom, goalRoom);

        if (roomPath == null || roomPath.Count == 0) return false;

        minDoorsCarved = CarveDoorsAlongRoomPath(walls, roomPath, edgeCandidates, rng);

        int loopDoors = Mathf.Max(0, Mathf.RoundToInt(minDoorsCarved * loopDoorRatio));
        AddLoopDoors(walls, roomPath, edgeCandidates, rng, loopDoors);

        return true;
    }

    private void TryCarveLoopsAfterTunnel(bool[,] walls, System.Random rng, Vector2Int zombieSpawn, Vector2Int targetSpawn)
    {
        int[,] roomId = new int[height, width];
        var rooms = ExtractRooms(walls, roomId);

        var edgeCandidates = BuildAdjacencyCandidates(walls, roomId);
        if (edgeCandidates.Count == 0) return;

        int startRoom = roomId[zombieSpawn.y, zombieSpawn.x];
        int goalRoom  = roomId[targetSpawn.y, targetSpawn.x];

        var neighbors = BuildNeighbors(rooms.Count, edgeCandidates);
        List<int> roomPath = (startRoom >= 0 && goalRoom >= 0)
            ? FindRoomPathBFS(neighbors, startRoom, goalRoom)
            : null;

        if (roomPath == null || roomPath.Count <= 1)
        {
            AddLoopDoors_NoPath(walls, edgeCandidates, rng, loopDoorsToAdd: 8);
            return;
        }

        int baseDoors = Mathf.Max(4, roomPath.Count - 1);
        int loopDoors = Mathf.Max(2, Mathf.RoundToInt(baseDoors * loopDoorRatio));
        AddLoopDoors(walls, roomPath, edgeCandidates, rng, loopDoors);
    }

    // =========================================================
    // WFC 자리: Stub
    // =========================================================
    private bool[,] GenerateWalls_WFCStub(System.Random rng)
    {
        bool[,] walls = new bool[height, width];

        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
            walls[y, x] = rng.NextDouble() < initialWallFill;

        SmoothWallsOnce(walls);
        SmoothWallsOnce(walls);

        return walls;
    }

    private void SmoothWallsOnce(bool[,] walls)
    {
        bool[,] tmp = (bool[,])walls.Clone();

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int wallCount = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (tmp[y + dy, x + dx]) wallCount++;
                }

                if (wallCount >= 5) walls[y, x] = true;
                else if (wallCount <= 2) walls[y, x] = false;
            }
        }
    }

    private void MakeBorderWalls(bool[,] walls)
    {
        for (int x = 0; x < width; x++)
        {
            walls[0, x] = true;
            walls[height - 1, x] = true;
        }
        for (int y = 0; y < height; y++)
        {
            walls[y, 0] = true;
            walls[y, width - 1] = true;
        }
    }

    // =========================================================
    // Spawn picking
    // =========================================================
    private bool TryPickSpawns(bool[,] walls, System.Random rng, out Vector2Int zombie, out Vector2Int target)
    {
        zombie = default;
        target = default;

        const int tries = 30000;

        Vector2Int PickOne()
        {
            int x = rng.Next(1, width - 1);
            int y = rng.Next(1, height - 1);
            return new Vector2Int(x, y);
        }

        bool foundZombie = false;
        for (int i = 0; i < tries; i++)
        {
            var p = PickOne();
            if (!walls[p.y, p.x])
            {
                zombie = p;
                foundZombie = true;
                break;
            }
        }
        if (!foundZombie) return false;

        for (int i = 0; i < tries; i++)
        {
            var p = PickOne();
            if (walls[p.y, p.x]) continue;

            int dist = Mathf.Abs(p.x - zombie.x) + Mathf.Abs(p.y - zombie.y);
            if (dist >= minSpawnManhattanDistance)
            {
                target = p;
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // Room extraction
    // =========================================================
    private List<RoomInfo> ExtractRooms(bool[,] walls, int[,] roomId)
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                roomId[y, x] = -1;

        var rooms = new List<RoomInfo>(256);
        var q = new Queue<Vector2Int>(1024);

        int nextId = 0;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (walls[y, x]) continue;
                if (roomId[y, x] != -1) continue;

                int id = nextId++;
                var info = new RoomInfo
                {
                    id = id,
                    count = 0,
                    minX = x, maxX = x,
                    minY = y, maxY = y
                };

                roomId[y, x] = id;
                q.Enqueue(new Vector2Int(x, y));

                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    info.count++;

                    if (c.x < info.minX) info.minX = c.x;
                    if (c.x > info.maxX) info.maxX = c.x;
                    if (c.y < info.minY) info.minY = c.y;
                    if (c.y > info.maxY) info.maxY = c.y;

                    TryVisit(c.x + 1, c.y);
                    TryVisit(c.x - 1, c.y);
                    TryVisit(c.x, c.y + 1);
                    TryVisit(c.x, c.y - 1);

                    void TryVisit(int nx, int ny)
                    {
                        if (nx <= 0 || nx >= width - 1 || ny <= 0 || ny >= height - 1) return;
                        if (walls[ny, nx]) return;
                        if (roomId[ny, nx] != -1) return;

                        roomId[ny, nx] = id;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                rooms.Add(info);
            }
        }

        return rooms;
    }

    // =========================================================
    // Adjacency candidates
    // =========================================================
    private Dictionary<(int a, int b), List<WallCandidate>> BuildAdjacencyCandidates(bool[,] walls, int[,] roomId)
    {
        var dict = new Dictionary<(int, int), List<WallCandidate>>(2048);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (!walls[y, x]) continue;

                // left-right
                int lx = x - 1, rx = x + 1;
                if (!walls[y, lx] && !walls[y, rx])
                {
                    int ra = roomId[y, lx];
                    int rb = roomId[y, rx];
                    if (ra >= 0 && rb >= 0 && ra != rb)
                    {
                        AddCandidate(ra, rb, new WallCandidate
                        {
                            wall = new Vector2Int(x, y),
                            aSide = new Vector2Int(lx, y),
                            bSide = new Vector2Int(rx, y),
                        });
                    }
                }

                // up-down
                int uy = y - 1, dy = y + 1;
                if (!walls[uy, x] && !walls[dy, x])
                {
                    int ra = roomId[uy, x];
                    int rb = roomId[dy, x];
                    if (ra >= 0 && rb >= 0 && ra != rb)
                    {
                        AddCandidate(ra, rb, new WallCandidate
                        {
                            wall = new Vector2Int(x, y),
                            aSide = new Vector2Int(x, uy),
                            bSide = new Vector2Int(x, dy),
                        });
                    }
                }
            }
        }

        return dict;

        void AddCandidate(int ra, int rb, WallCandidate c)
        {
            int a = Mathf.Min(ra, rb);
            int b = Mathf.Max(ra, rb);
            var key = (a, b);

            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<WallCandidate>(8);
                dict[key] = list;
            }
            list.Add(c);
        }
    }

    private List<HashSet<int>> BuildNeighbors(int roomCount, Dictionary<(int a, int b), List<WallCandidate>> candidates)
    {
        var neighbors = new List<HashSet<int>>(roomCount);
        for (int i = 0; i < roomCount; i++) neighbors.Add(new HashSet<int>());

        foreach (var kv in candidates)
        {
            int a = kv.Key.a;
            int b = kv.Key.b;
            if (a >= 0 && a < roomCount && b >= 0 && b < roomCount)
            {
                neighbors[a].Add(b);
                neighbors[b].Add(a);
            }
        }

        return neighbors;
    }

    // =========================================================
    // Room path BFS
    // =========================================================
    private List<int> FindRoomPathBFS(List<HashSet<int>> neighbors, int start, int goal)
    {
        int n = neighbors.Count;
        var prev = new int[n];
        var visited = new bool[n];
        for (int i = 0; i < n; i++) prev[i] = -1;

        var q = new Queue<int>();
        visited[start] = true;
        q.Enqueue(start);

        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            if (cur == goal) break;

            foreach (int nxt in neighbors[cur])
            {
                if (visited[nxt]) continue;
                visited[nxt] = true;
                prev[nxt] = cur;
                q.Enqueue(nxt);
            }
        }

        if (!visited[goal]) return null;

        var path = new List<int>();
        int t = goal;
        while (t != -1)
        {
            path.Add(t);
            if (t == start) break;
            t = prev[t];
        }
        path.Reverse();
        return path;
    }

    // =========================================================
    // Door carving
    // =========================================================
    private int CarveDoorsAlongRoomPath(
        bool[,] walls,
        List<int> roomPath,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng)
    {
        int carved = 0;

        for (int i = 0; i < roomPath.Count - 1; i++)
        {
            int ra = roomPath[i];
            int rb = roomPath[i + 1];

            int a = Mathf.Min(ra, rb);
            int b = Mathf.Max(ra, rb);

            if (!candidates.TryGetValue((a, b), out var list) || list.Count == 0)
                continue;

            WallCandidate chosen = PickBestCandidateByOpenness(walls, list, rng);

            if (walls[chosen.wall.y, chosen.wall.x])
            {
                walls[chosen.wall.y, chosen.wall.x] = false;
                carved++;
            }
        }

        return carved;
    }

    private WallCandidate PickBestCandidateByOpenness(bool[,] walls, List<WallCandidate> list, System.Random rng)
    {
        int bestScore = int.MinValue;
        var best = list[0];

        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            int score = CountOpen4(walls, c.aSide) + CountOpen4(walls, c.bSide);

            if (score > bestScore || (score == bestScore && rng.Next(2) == 0))
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    private int CountOpen4(bool[,] walls, Vector2Int p)
    {
        int open = 0;
        if (!walls[p.y, p.x + 1]) open++;
        if (!walls[p.y, p.x - 1]) open++;
        if (!walls[p.y + 1, p.x]) open++;
        if (!walls[p.y - 1, p.x]) open++;
        return open;
    }

    // =========================================================
    // Loop doors
    // =========================================================
    private void AddLoopDoors(
        bool[,] walls,
        List<int> roomPath,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng,
        int loopDoorsToAdd)
    {
        if (loopDoorsToAdd <= 0) return;

        var pathEdges = new HashSet<(int a, int b)>();
        for (int i = 0; i < roomPath.Count - 1; i++)
        {
            int a = Mathf.Min(roomPath[i], roomPath[i + 1]);
            int b = Mathf.Max(roomPath[i], roomPath[i + 1]);
            pathEdges.Add((a, b));
        }

        var edgeKeys = new List<(int a, int b)>(candidates.Count);
        foreach (var kv in candidates)
        {
            if (pathEdges.Contains(kv.Key)) continue;
            if (kv.Value == null || kv.Value.Count < 2) continue;
            edgeKeys.Add(kv.Key);
        }

        if (edgeKeys.Count == 0) return;

        edgeKeys.Sort((e1, e2) => candidates[e2].Count.CompareTo(candidates[e1].Count));

        int added = 0;
        int idx = 0;

        while (added < loopDoorsToAdd && idx < edgeKeys.Count)
        {
            var key = edgeKeys[idx++];
            var list = candidates[key];
            var chosen = PickBestCandidateByOpenness(walls, list, rng);

            if (!walls[chosen.wall.y, chosen.wall.x]) continue;

            walls[chosen.wall.y, chosen.wall.x] = false;
            added++;
        }
    }

    private void AddLoopDoors_NoPath(
        bool[,] walls,
        Dictionary<(int a, int b), List<WallCandidate>> candidates,
        System.Random rng,
        int loopDoorsToAdd)
    {
        if (loopDoorsToAdd <= 0) return;

        var edgeKeys = new List<(int a, int b)>(candidates.Count);
        foreach (var kv in candidates)
        {
            if (kv.Value == null || kv.Value.Count < 3) continue;
            edgeKeys.Add(kv.Key);
        }

        if (edgeKeys.Count == 0) return;

        edgeKeys.Sort((e1, e2) => candidates[e2].Count.CompareTo(candidates[e1].Count));

        int added = 0;
        int idx = 0;

        while (added < loopDoorsToAdd && idx < edgeKeys.Count)
        {
            var key = edgeKeys[idx++];
            var list = candidates[key];
            var chosen = PickBestCandidateByOpenness(walls, list, rng);

            if (!walls[chosen.wall.y, chosen.wall.x]) continue;

            walls[chosen.wall.y, chosen.wall.x] = false;
            added++;
        }
    }

    // =========================================================
    // Minimal tunnel carving (0-1 BFS)
    // =========================================================
    private bool CarveMinimalWallTunnel(bool[,] walls, Vector2Int start, Vector2Int goal)
    {
        if (start == goal) return true;

        int H = walls.GetLength(0);
        int W = walls.GetLength(1);

        int[,] dist = new int[H, W];
        Vector2Int[,] prev = new Vector2Int[H, W];
        bool[,] hasPrev = new bool[H, W];

        const int INF = int.MaxValue / 4;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                dist[y, x] = INF;

        var dq = new LinkedList<Vector2Int>();

        dist[start.y, start.x] = 0;
        dq.AddFirst(start);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (dq.Count > 0)
        {
            var cur = dq.First.Value;
            dq.RemoveFirst();

            if (cur == goal) break;

            int curD = dist[cur.y, cur.x];

            for (int i = 0; i < 4; i++)
            {
                int nx = cur.x + dx[i];
                int ny = cur.y + dy[i];

                if (nx < 1 || nx >= W - 1 || ny < 1 || ny >= H - 1) continue;

                int wCost = walls[ny, nx] ? 1 : 0;
                int nd = curD + wCost;

                if (nd < dist[ny, nx])
                {
                    dist[ny, nx] = nd;
                    prev[ny, nx] = cur;
                    hasPrev[ny, nx] = true;

                    var np = new Vector2Int(nx, ny);
                    if (wCost == 0) dq.AddFirst(np);
                    else dq.AddLast(np);
                }
            }
        }

        if (dist[goal.y, goal.x] == INF) return false;

        Vector2Int p = goal;
        while (p != start)
        {
            if (walls[p.y, p.x])
                walls[p.y, p.x] = false;

            if (!hasPrev[p.y, p.x]) break;
            p = prev[p.y, p.x];
        }

        if (walls[start.y, start.x]) walls[start.y, start.x] = false;
        if (walls[goal.y, goal.x]) walls[goal.y, goal.x] = false;

        return true;
    }

    // =========================================================
    // Final connectivity validation (cell BFS)
    // =========================================================
    private bool IsReachable(bool[,] walls, Vector2Int start, Vector2Int goal)
    {
        if (walls[start.y, start.x] || walls[goal.y, goal.x]) return false;

        var visited = new bool[height, width];
        var q = new Queue<Vector2Int>(1024);

        visited[start.y, start.x] = true;
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c == goal) return true;

            TryPush(c.x + 1, c.y);
            TryPush(c.x - 1, c.y);
            TryPush(c.x, c.y + 1);
            TryPush(c.x, c.y - 1);

            void TryPush(int nx, int ny)
            {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
                if (visited[ny, nx]) return;
                if (walls[ny, nx]) return;

                visited[ny, nx] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return false;
    }

    // =========================================================
    // Wall spawning
    // =========================================================
    private void SpawnWalls(bool[,] walls)
    {
        Transform parent = wallParent != null ? wallParent : transform;

        float topY = GetTopSurfaceY(basePart);
        float wallCenterY = topY + wallSize.y * 0.5f;

        float originX = basePart.position.x - (width * cellSize * 0.5f) + (cellSize * 0.5f);
        float originZ = basePart.position.z - (height * cellSize * 0.5f) + (cellSize * 0.5f);

        for (int y = 0; y < height; y++)
        {
            float z = originZ + y * cellSize;

            for (int x = 0; x < width; x++)
            {
                if (!walls[y, x]) continue;

                float xx = originX + x * cellSize;
                Vector3 pos = new Vector3(xx, wallCenterY, z);
                CreateWall(pos, parent);
            }
        }
    }

    private void CreateWall(Vector3 position, Transform parent)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent, true);
        wall.transform.position = position;
        wall.transform.localScale = wallSize;

        Rigidbody rb = wall.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private static float GetTopSurfaceY(Transform t)
    {
        Collider c = t.GetComponentInChildren<Collider>();
        if (c != null) return c.bounds.max.y;

        Renderer r = t.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds.max.y;

        return t.position.y;
    }

    private System.Random CreateRng()
    {
        int s = useRandomSeed ? Environment.TickCount : seed;
        return new System.Random(s);
    }
}
