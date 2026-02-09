using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoomMstMainGen : MonoBehaviour {
    [Header("Scene refs")] [SerializeField]
    private Transform basePart; // Optional. Used as center reference (grid origin).

    [Header("Sub modules")] [SerializeField]
    private RoomMstOrganGen organGen;

    [SerializeField] private RoomMstVisualGen visualGen;

    [Header("Grid")] [Min(8)] [SerializeField]
    private int gridWidth = 64;

    [Min(8)] [SerializeField] private int gridHeight = 64;
    [Min(0.1f)] [SerializeField] private float cellSize = 1f;

    [Header("Generation")] [SerializeField]
    private bool autoGenerateOnPlay = true;

    [SerializeField] private int seed = 0; // 0 => random each play
    [SerializeField] private bool keepBorderWalls = true;

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
    private int roomPadding = 1;

    [Header("Corridors")] [Min(1)] [SerializeField]
    private int corridorWidth = 1;

    [SerializeField] private bool randomizeLTurnOrder = true;

    private bool[,] wallMatrix; // true=wall, false=floor
    private int[,] roomIdMatrix; // -1 if not room
    private readonly List<Room> rooms = new();
    private System.Random rng;

    public readonly struct Cell {
        public readonly int x, y;

        public Cell(int x, int y) {
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public sealed class Room {
        public int id;
        public RectInt bounds; // organic must NOT exceed
        public Vector2Int center;
        public readonly List<Cell> cells = new();
        public readonly HashSet<int> floorSet = new(); // cached membership
    }

    public readonly struct MapData {
        public readonly int width;
        public readonly int height;
        public readonly float cellSize;
        public readonly Vector3 origin; // bottom-left world origin (XZ)
        public readonly bool[,] wallMatrix;
        public readonly List<Room> rooms;

        public MapData(int width, int height, float cellSize, Vector3 origin, bool[,] wallMatrix, List<Room> rooms) {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;
            this.origin = origin;
            this.wallMatrix = wallMatrix;
            this.rooms = rooms;
        }
    }

    private void Start() {
        if (autoGenerateOnPlay) {
            Generate();
        }
    }

    public void Generate() {
        int actualSeed = seed == 0 ? Environment.TickCount : seed;
        rng = new System.Random(actualSeed);

        rooms.Clear();
        BuildEmptyMatrices();

        PlaceRooms();
        CarveRoomsIntoGrid();
        ConnectRoomsMst();
        ApplyBorderWallsIfNeeded();

        Vector3 origin = GetGridOrigin();
        MapData data = new(gridWidth, gridHeight, cellSize, origin, wallMatrix, rooms);

        visualGen.RebuildWalls(data);
        visualGen.PickSpawnRoomsAndPlaceMarkers(data);
    }

    private void BuildEmptyMatrices() {
        wallMatrix = new bool[gridHeight, gridWidth];
        roomIdMatrix = new int[gridHeight, gridWidth];

        for (int y = 0; y < gridHeight; y++)
        for (int x = 0; x < gridWidth; x++) {
            wallMatrix[y, x] = true;
            roomIdMatrix[y, x] = -1;
        }
    }

    private bool InBounds(int x, int y) => x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    private static bool InRect(RectInt r, int x, int y) => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;

    private static RectInt ExpandRect(RectInt r, int pad) =>
        new(r.xMin - pad, r.yMin - pad, r.width + (pad * 2), r.height + (pad * 2));

    private void PlaceRooms() {
        int tries = 0;
        int placed = 0;
        int maxTries = maxRoomRerolls * roomCount;

        while (placed < roomCount && tries < maxTries) {
            tries++;

            bool makeCircle = rng.NextDouble() < circleRoomChance;
            Room room = makeCircle ? CreateCircleRoom(placed) : CreateRectRoom(placed);
            if (room == null) {
                continue;
            }

            if (!RoomFitsAndDoesntOverlap(room)) {
                continue;
            }

            BuildRoomFloorSet(room);

            // organic inside bounds only (delegated)
            organGen.ApplyLightOrganic(room, rng);

            rooms.Add(room);
            placed++;
        }

        if (rooms.Count < 2) {
            Debug.LogWarning(
                $"[MainGen] Only {rooms.Count} rooms placed. Increase rerolls / reduce padding / increase grid size.");
        }
    }

    private Room CreateRectRoom(int id) {
        int w = RandInt(rectMinW, rectMaxW);
        int h = RandInt(rectMinH, rectMaxH);

        int minX = 1;
        int minY = 1;
        int maxX = gridWidth - w - 1;
        int maxY = gridHeight - h - 1;
        if (maxX < minX || maxY < minY) {
            return null;
        }

        int x0 = RandInt(minX, maxX);
        int y0 = RandInt(minY, maxY);

        Room r = new() {
            id = id, bounds = new RectInt(x0, y0, w, h), center = new Vector2Int(x0 + (w / 2), y0 + (h / 2))
        };

        for (int y = y0; y < y0 + h; y++)
        for (int x = x0; x < x0 + w; x++) {
            r.cells.Add(new Cell(x, y));
        }

        return r;
    }

    private Room CreateCircleRoom(int id) {
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

        Room room = new() { id = id, bounds = new RectInt(cx - r, cy - r, d, d), center = new Vector2Int(cx, cy) };

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
        RectInt b = room.bounds;
        if (b.xMin < 0 || b.yMin < 0 || b.xMax > gridWidth || b.yMax > gridHeight) {
            return false;
        }

        RectInt expanded = ExpandRect(b, roomPadding);
        for (int i = 0; i < rooms.Count; i++) {
            RectInt other = ExpandRect(rooms[i].bounds, roomPadding);
            if (expanded.Overlaps(other)) {
                return false;
            }
        }

        return true;
    }

    private void BuildRoomFloorSet(Room room) {
        room.floorSet.Clear();
        for (int i = 0; i < room.cells.Count; i++) {
            Cell c = room.cells[i];
            room.floorSet.Add(Pack(c.x, c.y));
        }
    }

    private void CarveRoomsIntoGrid() {
        for (int i = 0; i < rooms.Count; i++) {
            Room r = rooms[i];
            for (int k = 0; k < r.cells.Count; k++) {
                Cell c = r.cells[k];
                if (!InBounds(c.x, c.y)) {
                    continue;
                }

                wallMatrix[c.y, c.x] = false;
                roomIdMatrix[c.y, c.x] = r.id;
            }
        }
    }

    private readonly struct Edge {
        public readonly int a, b, w;

        public Edge(int a, int b, int w) {
            this.a = a; //fir room index
            this.b = b; //sec room index
            this.w = w; // weight
        }
    }

    private void ConnectRoomsMst() {
        int n = rooms.Count;
        if (n <= 1) {
            return;
        }

        List<Edge> edges = new(n * (n - 1) / 2);
        for (int i = 0; i < n; i++) {
            Vector2Int ca = rooms[i].center;
            for (int j = i + 1; j < n; j++) {
                Vector2Int cb = rooms[j].center;
                int w = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y);
                edges.Add(new Edge(i, j, w));
            }
        }

        edges.Sort(static (e1, e2) => e1.w.CompareTo(e2.w));

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

            if (r[ra] < r[rb]) {
                p[ra] = rb;
            } else if (r[ra] > r[rb]) {
                p[rb] = ra;
            } else {
                p[rb] = ra;
                r[ra]++;
            }

            return true;
        }
    }

    private void CarveCorridorBetweenRooms(Room a, Room b) {
        Cell doorA = PickBestDoorCell(a, b.center);
        Cell doorB = PickBestDoorCell(b, a.center);

        bool xThenY = randomizeLTurnOrder ? rng.NextDouble() < 0.5 : true;

        if (xThenY) {
            CarveLine(doorA.x, doorA.y, doorB.x, doorA.y);
            CarveLine(doorB.x, doorA.y, doorB.x, doorB.y);
        } else {
            CarveLine(doorA.x, doorA.y, doorA.x, doorB.y);
            CarveLine(doorA.x, doorB.y, doorB.x, doorB.y);
        }
    }

    private static Cell PickBestDoorCell(Room room, Vector2Int toward) {
        Cell best = room.cells[0];
        int bestScore = int.MaxValue;

        for (int i = 0; i < room.cells.Count; i++) {
            Cell c = room.cells[i];

            bool isBoundary =
                !room.floorSet.Contains(Pack(c.x + 1, c.y)) ||
                !room.floorSet.Contains(Pack(c.x - 1, c.y)) ||
                !room.floorSet.Contains(Pack(c.x, c.y + 1)) ||
                !room.floorSet.Contains(Pack(c.x, c.y - 1));

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
        int halfA = (corridorWidth - 1) / 2;
        int halfB = corridorWidth / 2;

        for (int oy = -halfA; oy <= halfB; oy++)
        for (int ox = -halfA; ox <= halfB; ox++) {
            int x = cx + ox;
            int y = cy + oy;
            if (!InBounds(x, y)) {
                continue;
            }

            wallMatrix[y, x] = false;
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

    private Vector3 GetGridOrigin() {
        Vector3 center = basePart != null ? basePart.position : transform.position;

        float totalW = gridWidth * cellSize;
        float totalH = gridHeight * cellSize;

        // bottom-left in XZ
        return new Vector3(center.x - (totalW * 0.5f), 0f, center.z - (totalH * 0.5f));
    }

    public static int Pack(int x, int y) => (y << 16) ^ (x & 0xFFFF);

    public static void Unpack(int p, out int x, out int y) {
        x = (short)(p & 0xFFFF);
        y = p >> 16;
    }

    private int RandInt(int min, int maxInclusive) {
        if (maxInclusive < min) {
            return min;
        }

        return rng.Next(min, maxInclusive + 1);
    }
}
