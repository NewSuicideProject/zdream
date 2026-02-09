using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rooms (rect/circle) -> light organic (IN-BOUNDS ONLY) -> MST corridors -> spawn walls
/// Then pick two far rooms: Zombie spawn + Target spawn.
/// Also shows debug capsules with user-assigned materials (NO Shader.Find).
///
/// Grid coords: (x,y) => world (x,z)
/// wallMatrix[y,x] == true  => wall
/// wallMatrix[y,x] == false => floor
/// </summary>
public sealed class RoomMstMapSpawner : MonoBehaviour {
    // =========================================================
    // [SECTION 0] Inspector / Scene refs
    // =========================================================
    [Header("Scene")] [SerializeField] private Transform basePart; // Optional. Used as center reference.
    [SerializeField] private Transform wallParent; // Parent for spawned walls (optional)
    [SerializeField] private Transform debugParent; // Parent for debug capsules (optional)

    [Header("Spawn markers (existing EmptyObjects)")] [SerializeField]
    private Transform zombieSpawnMarker;

    [SerializeField] private Transform targetSpawnMarker;

    [Header("Debug Capsules (NO Shader.Find)")] [SerializeField]
    private bool spawnDebugCapsules = true;

    [SerializeField] private float capsuleHeight = 2f;
    [SerializeField] private float capsuleRadius = 0.5f;
    [SerializeField] private float capsuleLift = 1.2f;
    [SerializeField] private Material zombieSpawnDebugMaterial;
    [SerializeField] private Material targetSpawnDebugMaterial;

    // =========================================================
    // [SECTION 1] Grid / Walls
    // =========================================================
    [Header("Grid")] [Min(8)] [SerializeField]
    private int gridWidth = 64;

    [Min(8)] [SerializeField] private int gridHeight = 64;
    [Min(0.1f)] [SerializeField] private float cellSize = 1f;

    [Header("Wall Prefab")] [SerializeField]
    private GameObject wallPrefab;

    [SerializeField] private float wallCenterY = 2.5f; // If wall height is 5, 2.5 sits on ground.

    [Header("Generation")] [SerializeField]
    private bool autoGenerateOnPlay = true;

    [SerializeField] private int seed = 0; // 0 => random each play
    [SerializeField] private bool keepBorderWalls = true;

    // =========================================================
    // [SECTION 2] Rooms
    // =========================================================
    [Header("Rooms")] [Min(2)] [SerializeField]
    private int roomCount = 14;

    [Min(1)] [SerializeField] private int maxRoomRerolls = 60;

    [Range(0f, 1f)] [SerializeField] private float circleRoomChance = 0.35f;

    [Header("Rect room size")] [Min(3)] [SerializeField]
    private int rectMinW = 5;

    [Min(3)] [SerializeField] private int rectMaxW = 12;
    [Min(3)] [SerializeField] private int rectMinH = 5;
    [Min(3)] [SerializeField] private int rectMaxH = 12;

    [Header("Circle room radius")] [Min(2)] [SerializeField]
    private int circleMinR = 3;

    [Min(2)] [SerializeField] private int circleMaxR = 7;

    [Header("Room spacing")] [Min(0)] [SerializeField]
    private int roomPadding = 1; // extra empty space between rooms

    // =========================================================
    // [SECTION 3] Organic (LIGHT, bounded)
    // =========================================================
    [Header("Light Organic (bounded inside room bounds)")] [Range(0, 3)] [SerializeField]
    private int organicIterations = 1; // keep small

    [Range(0f, 0.25f)] [SerializeField] private float organicCarveRatio = 0.06f; // % of room cells to carve
    [Range(0f, 0.25f)] [SerializeField] private float organicGrowRatio = 0.05f; // % of boundary-adjacent to grow
    [Min(1)] [SerializeField] private int organicGrowMaxTriesPerCell = 6;

    // =========================================================
    // [SECTION 4] Corridors
    // =========================================================
    [Header("Corridors")] [Min(1)] [SerializeField]
    private int corridorWidth = 1; // keep 1 for now

    [SerializeField] private bool randomizeLTurnOrder = true;

    // =========================================================
    // Runtime
    // =========================================================
    private bool[,] wallMatrix; // true=wall, false=floor
    private int[,] roomIdMatrix; // -1 if not room
    private readonly List<GameObject> spawnedWalls = new();
    private readonly List<GameObject> spawnedDebug = new();
    private readonly List<Room> rooms = new();
    private System.Random rng;

    private struct Cell {
        public int x, y;

        public Cell(int x, int y) {
            this.x = x;
            this.y = y;
        }
    }

    private sealed class Room {
        public int id;
        public RectInt bounds; // hard limit (organic must NOT exceed)
        public Vector2Int center;
        public List<Cell> cells = new(); // final carved cells inside bounds
    }

    // =========================================================
    // Unity
    // =========================================================
    private void Start() {
        if (autoGenerateOnPlay) {
            Generate();
        }
    }

    [ContextMenu("Generate")]
    public void Generate() {
        if (wallPrefab == null) {
            Debug.LogError("[RoomMstMapSpawner] wallPrefab is null.");
            return;
        }

        if (wallParent == null) {
            wallParent = transform;
        }

        if (debugParent == null) {
            debugParent = transform;
        }

        int actualSeed = seed == 0 ? Environment.TickCount : seed;
        rng = new System.Random(actualSeed);

        BuildEmptyMatrices();
        rooms.Clear();

        PlaceRooms();
        CarveRoomsIntoGrid();
        ConnectRoomsMst();
        ApplyBorderWallsIfNeeded();

        RebuildWalls();

        PickSpawnRoomsAndPlaceMarkers();
    }

    // =========================================================
    // [SECTION A] Matrices
    // =========================================================
    private void BuildEmptyMatrices() {
        wallMatrix = new bool[gridHeight, gridWidth];
        roomIdMatrix = new int[gridHeight, gridWidth];

        for (int y = 0; y < gridHeight; y++)
        for (int x = 0; x < gridWidth; x++) {
            wallMatrix[y, x] = true; // start full wall
            roomIdMatrix[y, x] = -1;
        }
    }

    private bool InBounds(int x, int y) => x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;

    // =========================================================
    // [SECTION B] Rooms placement
    // =========================================================
    private void PlaceRooms() {
        int tries = 0;
        int placed = 0;

        while (placed < roomCount && tries < maxRoomRerolls * roomCount) {
            tries++;

            bool makeCircle = rng.NextDouble() < circleRoomChance;

            Room room = makeCircle ? TryCreateCircleRoom(placed) : TryCreateRectRoom(placed);
            if (room == null) {
                continue;
            }

            if (!RoomFitsAndDoesntOverlap(room)) {
                continue;
            }

            // Light organic inside its bounds only
            ApplyLightOrganic(room);

            rooms.Add(room);
            placed++;
        }

        if (rooms.Count < 2) {
            Debug.LogWarning(
                $"[RoomMstMapSpawner] Only {rooms.Count} rooms placed. Increase rerolls / reduce padding / grid size.");
        }
    }

    private Room TryCreateRectRoom(int id) {
        int w = RandInt(rectMinW, rectMaxW);
        int h = RandInt(rectMinH, rectMaxH);

        // keep some margin from borders so corridors can breathe
        int minX = 1;
        int minY = 1;
        int maxX = gridWidth - w - 1;
        int maxY = gridHeight - h - 1;
        if (maxX < minX || maxY < minY) {
            return null;
        }

        int x0 = RandInt(minX, maxX);
        int y0 = RandInt(minY, maxY);

        Room r = new();
        r.id = id;
        r.bounds = new RectInt(x0, y0, w, h);
        r.center = new Vector2Int(x0 + (w / 2), y0 + (h / 2));

        // full rect cells
        for (int y = y0; y < y0 + h; y++)
        for (int x = x0; x < x0 + w; x++) {
            r.cells.Add(new Cell(x, y));
        }

        return r;
    }

    private Room TryCreateCircleRoom(int id) {
        int r = RandInt(circleMinR, circleMaxR);
        int d = (r * 2) + 1;

        int minX = 1 + r;
        int minY = 1 + r;
        int maxX = gridWidth - r - 2;
        int maxY = gridHeight - r - 2;
        if (maxX < minX || maxY < minY) {
            return null;
        }

        int cx = RandInt(minX, maxX);
        int cy = RandInt(minY, maxY);

        Room room = new();
        room.id = id;
        room.bounds = new RectInt(cx - r, cy - r, d, d);
        room.center = new Vector2Int(cx, cy);

        int rr = r * r;
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++) {
            int dx = x - cx;
            int dy = y - cy;
            if ((dx * dx) + (dy * dy) <= rr) {
                room.cells.Add(new Cell(x, y));
            }
        }

        return room;
    }

    private bool RoomFitsAndDoesntOverlap(Room room) {
        // bounds must be inside grid
        RectInt b = room.bounds;
        if (b.xMin < 0 || b.yMin < 0 || b.xMax > gridWidth || b.yMax > gridHeight) {
            return false;
        }

        // overlap test with padding: compare bounds expanded
        RectInt expanded = ExpandRect(b, roomPadding);

        for (int i = 0; i < rooms.Count; i++) {
            RectInt other = ExpandRect(rooms[i].bounds, roomPadding);
            if (expanded.Overlaps(other)) {
                return false;
            }
        }

        return true;
    }

    private RectInt ExpandRect(RectInt r, int pad) =>
        new(r.xMin - pad, r.yMin - pad, r.width + (pad * 2), r.height + (pad * 2));

    // =========================================================
    // [SECTION C] Organic (bounded)
    // =========================================================
    private void ApplyLightOrganic(Room room) {
        if (organicIterations <= 0) {
            return;
        }

        // Convert to hash set for fast membership
        HashSet<int> set = new(room.cells.Count * 2);
        foreach (Cell c in room.cells) {
            set.Add(Pack(c.x, c.y));
        }

        for (int it = 0; it < organicIterations; it++) {
            // 1) carve: remove some boundary cells
            List<Cell> boundary = CollectBoundaryCells(set, room.bounds);
            int carveCount = Mathf.Clamp(Mathf.RoundToInt(set.Count * organicCarveRatio), 0, boundary.Count);

            for (int k = 0; k < carveCount; k++) {
                if (boundary.Count == 0) {
                    break;
                }

                int idx = rng.Next(boundary.Count);
                Cell c = boundary[idx];
                boundary.RemoveAt(idx);

                // avoid carving too much (keep connectivity-ish): only carve if it has at least 2 neighbors
                int n = CountFloorNeighbors(set, c.x, c.y);
                if (n >= 2) {
                    set.Remove(Pack(c.x, c.y));
                }
            }

            // 2) grow: add a few cells adjacent to boundary, but inside bounds only
            boundary = CollectBoundaryCells(set, room.bounds);
            int growCount = Mathf.Clamp(Mathf.RoundToInt(set.Count * organicGrowRatio), 0, boundary.Count);

            for (int k = 0; k < growCount; k++) {
                if (boundary.Count == 0) {
                    break;
                }

                int idx = rng.Next(boundary.Count);
                Cell b = boundary[idx];
                boundary.RemoveAt(idx);

                // Try a few times to grow into a neighbor inside bounds
                for (int t = 0; t < organicGrowMaxTriesPerCell; t++) {
                    Cell n = PickRandom4Neighbor(b.x, b.y);
                    if (!room.bounds.Contains(new Vector2Int(n.x, n.y))) {
                        continue;
                    }

                    int p = Pack(n.x, n.y);
                    if (!set.Contains(p)) {
                        // prevent thin spikes: require that new cell touches at least 2 existing
                        if (CountFloorNeighbors(set, n.x, n.y) >= 2) {
                            set.Add(p);
                            break;
                        }
                    }
                }
            }
        }

        // Write back
        room.cells.Clear();
        foreach (int p in set) {
            Unpack(p, out int x, out int y);
            room.cells.Add(new Cell(x, y));
        }
    }

    private List<Cell> CollectBoundaryCells(HashSet<int> floorSet, RectInt bounds) {
        List<Cell> result = new();
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++) {
            int p = Pack(x, y);
            if (!floorSet.Contains(p)) {
                continue;
            }

            // boundary if at least one 4-neighbor is not floor OR outside bounds
            bool isBoundary =
                !IsFloor(floorSet, x + 1, y, bounds) ||
                !IsFloor(floorSet, x - 1, y, bounds) ||
                !IsFloor(floorSet, x, y + 1, bounds) ||
                !IsFloor(floorSet, x, y - 1, bounds);

            if (isBoundary) {
                result.Add(new Cell(x, y));
            }
        }

        return result;
    }

    private bool IsFloor(HashSet<int> set, int x, int y, RectInt bounds) {
        if (!bounds.Contains(new Vector2Int(x, y))) {
            return false;
        }

        return set.Contains(Pack(x, y));
    }

    private int CountFloorNeighbors(HashSet<int> set, int x, int y) {
        int n = 0;
        if (set.Contains(Pack(x + 1, y))) {
            n++;
        }

        if (set.Contains(Pack(x - 1, y))) {
            n++;
        }

        if (set.Contains(Pack(x, y + 1))) {
            n++;
        }

        if (set.Contains(Pack(x, y - 1))) {
            n++;
        }

        return n;
    }

    private Cell PickRandom4Neighbor(int x, int y) {
        int r = rng.Next(4);
        if (r == 0) {
            return new Cell(x + 1, y);
        }

        if (r == 1) {
            return new Cell(x - 1, y);
        }

        if (r == 2) {
            return new Cell(x, y + 1);
        }

        return new Cell(x, y - 1);
    }

    private int Pack(int x, int y) => (y << 16) ^ (x & 0xFFFF);

    private void Unpack(int p, out int x, out int y) {
        x = (short)(p & 0xFFFF);
        y = p >> 16;
    }

    // =========================================================
    // [SECTION D] Carve rooms into wallMatrix
    // =========================================================
    private void CarveRoomsIntoGrid() {
        for (int i = 0; i < rooms.Count; i++) {
            Room r = rooms[i];
            foreach (Cell c in r.cells) {
                if (!InBounds(c.x, c.y)) {
                    continue;
                }

                wallMatrix[c.y, c.x] = false;
                roomIdMatrix[c.y, c.x] = r.id;
            }
        }
    }

    // =========================================================
    // [SECTION E] MST corridors
    // =========================================================
    private struct Edge {
        public int a, b;
        public int w;
    }

    private void ConnectRoomsMst() {
        int n = rooms.Count;
        if (n <= 1) {
            return;
        }

        // Build all edges (O(n^2), fine for small n)
        List<Edge> edges = new(n * (n - 1) / 2);
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++) {
            Vector2Int ca = rooms[i].center;
            Vector2Int cb = rooms[j].center;
            int w = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y); // manhattan
            edges.Add(new Edge { a = i, b = j, w = w });
        }

        edges.Sort((e1, e2) => e1.w.CompareTo(e2.w));

        DSU dsu = new(n);
        int picked = 0;

        for (int i = 0; i < edges.Count && picked < n - 1; i++) {
            Edge e = edges[i];
            if (!dsu.Union(e.a, e.b)) {
                continue;
            }

            picked++;
            CarveCorridorBetweenRooms(rooms[e.a], rooms[e.b]);
        }
    }

    private sealed class DSU {
        private readonly int[] p;
        private readonly int[] r;

        public DSU(int n) {
            p = new int[n];
            r = new int[n];
            for (int i = 0; i < n; i++) {
                p[i] = i;
            }
        }

        private int Find(int x) {
            while (p[x] != x) {
                p[x] = p[p[x]];
                x = p[x];
            }

            return x;
        }

        public bool Union(int a, int b) {
            int ra = Find(a), rb = Find(b);
            if (ra == rb) {
                return false;
            }

            if (r[ra] < r[rb]) { p[ra] = rb; } else if (r[ra] > r[rb]) { p[rb] = ra; } else {
                p[rb] = ra;
                r[ra]++;
            }

            return true;
        }
    }

    private void CarveCorridorBetweenRooms(Room a, Room b) {
        // door candidates: pick boundary cell closest to other center
        Cell doorA = PickBestDoorCell(a, b.center);
        Cell doorB = PickBestDoorCell(b, a.center);

        // carve an L path between doors
        bool xThenY = randomizeLTurnOrder ? rng.NextDouble() < 0.5 : true;

        if (xThenY) {
            CarveLine(doorA.x, doorA.y, doorB.x, doorA.y);
            CarveLine(doorB.x, doorA.y, doorB.x, doorB.y);
        } else {
            CarveLine(doorA.x, doorA.y, doorA.x, doorB.y);
            CarveLine(doorA.x, doorB.y, doorB.x, doorB.y);
        }
    }

    private Cell PickBestDoorCell(Room room, Vector2Int toward) {
        // choose from boundary cells inside room.cells
        // (boundary = any 4-neighbor outside the room floor-set)
        HashSet<int> set = new(room.cells.Count * 2);
        foreach (Cell c in room.cells) {
            set.Add(Pack(c.x, c.y));
        }

        Cell best = room.cells[0];
        int bestScore = int.MaxValue;

        foreach (Cell c in room.cells) {
            bool isBoundary =
                !set.Contains(Pack(c.x + 1, c.y)) ||
                !set.Contains(Pack(c.x - 1, c.y)) ||
                !set.Contains(Pack(c.x, c.y + 1)) ||
                !set.Contains(Pack(c.x, c.y - 1));

            if (!isBoundary) {
                continue;
            }

            int score = Mathf.Abs(c.x - toward.x) + Mathf.Abs(c.y - toward.y);
            if (score < bestScore) {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    private void CarveLine(int x0, int y0, int x1, int y1) {
        int dx = Math.Sign(x1 - x0);
        int dy = Math.Sign(y1 - y0);

        int x = x0;
        int y = y0;

        CarveCorridorCell(x, y);

        // Move on x first if needed
        while (x != x1) {
            x += dx;
            CarveCorridorCell(x, y);
        }

        while (y != y1) {
            y += dy;
            CarveCorridorCell(x, y);
        }
    }

    private void CarveCorridorCell(int cx, int cy) {
        for (int oy = -(corridorWidth - 1) / 2; oy <= corridorWidth / 2; oy++)
        for (int ox = -(corridorWidth - 1) / 2; ox <= corridorWidth / 2; ox++) {
            int x = cx + ox;
            int y = cy + oy;
            if (!InBounds(x, y)) {
                continue;
            }

            wallMatrix[y, x] = false;
            // roomIdMatrix stays as-is (corridor can overlap rooms; not a problem)
        }
    }

    private void ApplyBorderWallsIfNeeded() {
        if (!keepBorderWalls) {
            return;
        }

        for (int x = 0; x < gridWidth; x++) {
            wallMatrix[0, x] = true;
            wallMatrix[gridHeight - 1, x] = true;
        }

        for (int y = 0; y < gridHeight; y++) {
            wallMatrix[y, 0] = true;
            wallMatrix[y, gridWidth - 1] = true;
        }
    }

    // =========================================================
    // [SECTION F] Spawn walls
    // =========================================================
    private void RebuildWalls() {
        ClearSpawnedWalls();

        Vector3 origin = GetGridOrigin();

        for (int y = 0; y < gridHeight; y++)
        for (int x = 0; x < gridWidth; x++) {
            if (!wallMatrix[y, x]) {
                continue;
            }

            Vector3 pos = CellToWorld(origin, x, y);
            GameObject w = Instantiate(wallPrefab, pos, Quaternion.identity, wallParent);
            spawnedWalls.Add(w);
        }
    }

    private void ClearSpawnedWalls() {
        for (int i = 0; i < spawnedWalls.Count; i++) {
            if (spawnedWalls[i] != null) {
                Destroy(spawnedWalls[i]);
            }
        }

        spawnedWalls.Clear();
    }

    private Vector3 GetGridOrigin() {
        Vector3 center = basePart != null ? basePart.position : transform.position;

        float totalW = gridWidth * cellSize;
        float totalH = gridHeight * cellSize;

        // bottom-left in XZ
        return new Vector3(center.x - (totalW * 0.5f), 0f, center.z - (totalH * 0.5f));
    }

    private Vector3 CellToWorld(Vector3 origin, int x, int y) {
        float wx = origin.x + ((x + 0.5f) * cellSize);
        float wz = origin.z + ((y + 0.5f) * cellSize);
        return new Vector3(wx, wallCenterY, wz);
    }

    // =========================================================
    // [SECTION G] Pick spawn rooms & show markers/capsules
    // =========================================================
    private void PickSpawnRoomsAndPlaceMarkers() {
        ClearSpawnedDebug();

        if (rooms.Count < 2) {
            Debug.LogWarning("[RoomMstMapSpawner] Not enough rooms for spawn/target.");
            return;
        }

        // Pick farthest pair by center distance (simple, stable, works well)
        int a = 0, b = 1;
        long best = -1;

        for (int i = 0; i < rooms.Count; i++)
        for (int j = i + 1; j < rooms.Count; j++) {
            Vector2Int c1 = rooms[i].center;
            Vector2Int c2 = rooms[j].center;
            long dx = c1.x - c2.x;
            long dy = c1.y - c2.y;
            long d2 = (dx * dx) + (dy * dy);
            if (d2 > best) {
                best = d2;
                a = i;
                b = j;
            }
        }

        Vector3 origin = GetGridOrigin();
        Vector3 zombiePos = CellToWorld(origin, rooms[a].center.x, rooms[a].center.y);
        Vector3 targetPos = CellToWorld(origin, rooms[b].center.x, rooms[b].center.y);

        // Markers (existing EmptyObjects)
        if (zombieSpawnMarker != null) {
            zombieSpawnMarker.position = new Vector3(zombiePos.x, 0f, zombiePos.z);
        }

        if (targetSpawnMarker != null) {
            targetSpawnMarker.position = new Vector3(targetPos.x, 0f, targetPos.z);
        }

        // Debug capsules
        if (spawnDebugCapsules) {
            SpawnDebugCapsule(zombiePos, capsuleLift, capsuleHeight, capsuleRadius, zombieSpawnDebugMaterial);
            SpawnDebugCapsule(targetPos, capsuleLift, capsuleHeight, capsuleRadius, targetSpawnDebugMaterial);
        }
    }

    private void SpawnDebugCapsule(Vector3 basePos, float lift, float height, float radius, Material mat) {
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.name = "SpawnDebugCapsule";
        cap.transform.SetParent(debugParent, true);

        // Capsule primitive default height is 2 units with radius 0.5.
        // We scale it to match desired sizes.
        float defaultHeight = 2f;
        float defaultRadius = 0.5f;

        float yScale = Mathf.Max(0.01f, height / defaultHeight);
        float xzScale = Mathf.Max(0.01f, radius / defaultRadius);

        cap.transform.localScale = new Vector3(xzScale, yScale, xzScale);
        cap.transform.position = new Vector3(basePos.x, lift, basePos.z);

        // remove collider to avoid physics interference
        Collider col = cap.GetComponent<Collider>();
        if (col != null) {
            Destroy(col);
        }

        if (mat != null) {
            Renderer r = cap.GetComponent<Renderer>();
            if (r != null) {
                r.sharedMaterial = mat;
            }
        } else {
            Debug.LogWarning("[RoomMstMapSpawner] Debug capsule material is null (assign in Inspector).");
        }

        spawnedDebug.Add(cap);
    }

    private void ClearSpawnedDebug() {
        for (int i = 0; i < spawnedDebug.Count; i++) {
            if (spawnedDebug[i] != null) {
                Destroy(spawnedDebug[i]);
            }
        }

        spawnedDebug.Clear();
    }

    // =========================================================
    // Utilities
    // =========================================================
    private int RandInt(int min, int maxInclusive) {
        if (maxInclusive < min) {
            return min;
        }

        return rng.Next(min, maxInclusive + 1);
    }
}
