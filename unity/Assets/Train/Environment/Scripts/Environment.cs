using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Environment : MonoBehaviour {
    [SerializeField] private Transform basePart;

    [SerializeField] private Transform wallParent;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private float wallCenterY = 2.5f;

    [SerializeField] private Transform zombieSpawnMarker;
    [SerializeField] private Transform targetSpawnMarker;

    [Min(8)] [SerializeField] private int gridWidth = 64;
    [Min(8)] [SerializeField] private int gridHeight = 64;
    [Min(0.1f)] [SerializeField] private float cellSize = 1f;

    [SerializeField] private bool autoGenerateOnPlay = true;
    [SerializeField] private int seed = 0; // 0 => random each play
    [SerializeField] private bool keepBorderWalls = true;

    [Min(2)] [SerializeField] private int roomCount = 14;
    [Min(1)] [SerializeField] private int maxRoomRerolls = 60;
    [Range(0f, 1f)] [SerializeField] private float circleRoomChance = 0.35f;

    [Min(3)] [SerializeField] private int rectMinW = 5;
    [Min(3)] [SerializeField] private int rectMaxW = 12;
    [Min(3)] [SerializeField] private int rectMinH = 5;
    [Min(3)] [SerializeField] private int rectMaxH = 12;

    [Min(2)] [SerializeField] private int circleMinR = 3;
    [Min(2)] [SerializeField] private int circleMaxR = 7;

    [Min(0)] [SerializeField] private int roomPadding = 1;

    [Min(1)] [SerializeField] private int corridorWidth = 1;
    [SerializeField] private bool randomizeLTurnOrder = true;

    [Range(0, 3)] [SerializeField] private int organicIterations = 1;
    [Range(0f, 0.25f)] [SerializeField] private float organicCarveRatio = 0.06f;
    [Range(0f, 0.25f)] [SerializeField] private float organicGrowRatio = 0.05f;
    [Min(1)] [SerializeField] private int organicGrowMaxTriesPerCell = 6;

    private MapData _map;
    private System.Random _rng;

    private OrganicShaper _organicShaper;
    private Visualizer _visualizer;

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
        public RectInt bounds; // organic must stay inside
        public Vector2Int center; // seed center (can be recomputed later if you want)
        public readonly List<Cell> Cells = new();
        public readonly HashSet<int> FloorSet = new(); // source of truth for organic boundary tests
    }

    public sealed class MapData {
        public int width;
        public int height;
        public float cellSize;
        public Vector3 origin; // bottom-left world origin (XZ)

        public bool[,] wallMatrix; // true=wall, false=floor
        public int[,] roomIdMatrix; // -1 if not room (corridors remain -1)
        public List<Room> rooms;
    }

    private void Awake() {
        if (wallParent == null) {
            wallParent = transform;
        }

        _organicShaper = new OrganicShaper();
        _visualizer = new Visualizer(wallParent, wallPrefab, wallCenterY);
    }

    private void Start() {
        if (autoGenerateOnPlay) {
            Generate();
        }
    }

    public void Generate() {
        int actualSeed = seed == 0 ? System.Environment.TickCount : seed;
        _rng = new System.Random(actualSeed);

        _map = new MapData {
            width = gridWidth,
            height = gridHeight,
            cellSize = cellSize,
            origin = GetGridOrigin(gridWidth, gridHeight, cellSize),
            wallMatrix = new bool[gridHeight, gridWidth],
            roomIdMatrix = new int[gridHeight, gridWidth],
            rooms = new List<Room>(roomCount)
        };

        InitializeMatrices();

        PlaceRooms();
        WriteRoomsToGrid();

        ConnectRoomsMst();
        ApplyBorderWallsIfNeeded();

        _visualizer.RebuildWalls(_map);
        _visualizer.PlaceSpawnMarkers(_map, zombieSpawnMarker, targetSpawnMarker);
    }

    private void InitializeMatrices() {
        for (int y = 0; y < _map.height; y++)
        for (int x = 0; x < _map.width; x++) {
            _map.wallMatrix[y, x] = true;
            _map.roomIdMatrix[y, x] = -1;
        }
    }

    private bool InBounds(int x, int y) => x >= 0 && x < _map.width && y >= 0 && y < _map.height;

    private static RectInt ExpandRect(RectInt r, int pad) =>
        new(r.xMin - pad, r.yMin - pad, r.width + (pad * 2), r.height + (pad * 2));

    private void PlaceRooms() {
        int tries = 0;
        int placed = 0;
        int maxTries = maxRoomRerolls * roomCount;

        OrganicShaper.Config organicCfg = new(
            organicIterations,
            organicCarveRatio,
            organicGrowRatio,
            organicGrowMaxTriesPerCell
        );

        while (placed < roomCount && tries < maxTries) {
            tries++;

            bool makeCircle = _rng.NextDouble() < circleRoomChance;
            Room room = makeCircle ? CreateCircleRoom(placed) : CreateRectRoom(placed);
            if (room == null) {
                continue;
            }

            if (!RoomFitsAndDoesntOverlap(room)) {
                continue;
            }

            BuildRoomFloorSet(room);
            _organicShaper.ApplyLightOrganic(room, _rng, organicCfg);

            _map.rooms.Add(room);
            placed++;
        }

        if (_map.rooms.Count < 2) {
            Debug.LogWarning(
                $"[Environment] Only {_map.rooms.Count} rooms placed. Increase rerolls / reduce padding / increase grid size.");
        }
    }

    private Room CreateRectRoom(int id) {
        int roomWidthCells = _rng.Next(rectMinW, rectMaxW + 1);
        int roomHeightCells = _rng.Next(rectMinH, rectMaxH + 1);

        const int border = 1; // keep at least 1-cell margin for border walls
        int minLeftX = border;
        int minBottomY = border;

        int maxLeftX = _map.width - roomWidthCells - border;
        int maxBottomY = _map.height - roomHeightCells - border;
        if (maxLeftX < minLeftX || maxBottomY < minBottomY) {
            return null;
        }

        int leftX = _rng.Next(minLeftX, maxLeftX + 1);
        int bottomY = _rng.Next(minBottomY, maxBottomY + 1);

        int centerX = leftX + (roomWidthCells / 2);
        int centerY = bottomY + (roomHeightCells / 2);

        Room room = new() {
            id = id,
            bounds = new RectInt(leftX, bottomY, roomWidthCells, roomHeightCells),
            center = new Vector2Int(centerX, centerY)
        };

        int rightXExclusive = leftX + roomWidthCells;
        int topYExclusive = bottomY + roomHeightCells;

        for (int y = bottomY; y < topYExclusive; y++)
        for (int x = leftX; x < rightXExclusive; x++) {
            room.Cells.Add(new Cell(x, y));
        }

        return room;
    }

    private Room CreateCircleRoom(int id) {
        int radiusCells = _rng.Next(circleMinR, circleMaxR + 1);
        int diameterCells = (radiusCells * 2) + 1;

        const int border = 1;
        int minCenterX = border + radiusCells;
        int minCenterY = border + radiusCells;

        int maxCenterX = _map.width - border - radiusCells - 1;
        int maxCenterY = _map.height - border - radiusCells - 1;
        if (maxCenterX < minCenterX || maxCenterY < minCenterY) {
            return null;
        }

        int centerX = _rng.Next(minCenterX, maxCenterX + 1);
        int centerY = _rng.Next(minCenterY, maxCenterY + 1);

        int boundsLeftX = centerX - radiusCells;
        int boundsBottomY = centerY - radiusCells;

        Room room = new() {
            id = id,
            bounds = new RectInt(boundsLeftX, boundsBottomY, diameterCells, diameterCells),
            center = new Vector2Int(centerX, centerY)
        };

        int radiusSq = radiusCells * radiusCells;

        for (int y = centerY - radiusCells; y <= centerY + radiusCells; y++)
        for (int x = centerX - radiusCells; x <= centerX + radiusCells; x++) {
            int offsetX = x - centerX;
            int offsetY = y - centerY;

            if ((offsetX * offsetX) + (offsetY * offsetY) <= radiusSq) {
                room.Cells.Add(new Cell(x, y));
            }
        }

        return room;
    }

    private bool RoomFitsAndDoesntOverlap(Room room) {
        RectInt expanded = ExpandRect(room.bounds, roomPadding);

        if (expanded.xMin < 0 || expanded.yMin < 0 || expanded.xMax > _map.width || expanded.yMax > _map.height) {
            return false;
        }

        for (int i = 0; i < _map.rooms.Count; i++) {
            RectInt otherExpanded = ExpandRect(_map.rooms[i].bounds, roomPadding);
            if (expanded.Overlaps(otherExpanded)) {
                return false;
            }
        }

        return true;
    }

    private void BuildRoomFloorSet(Room room) {
        room.FloorSet.Clear();
        for (int i = 0; i < room.Cells.Count; i++) {
            Cell c = room.Cells[i];
            room.FloorSet.Add(Utility.Pack(c.x, c.y));
        }
    }

    private void WriteRoomsToGrid() {
        for (int i = 0; i < _map.rooms.Count; i++) {
            Room r = _map.rooms[i];
            for (int k = 0; k < r.Cells.Count; k++) {
                Cell c = r.Cells[k];
                if (!InBounds(c.x, c.y)) {
                    continue;
                }

                _map.wallMatrix[c.y, c.x] = false;
                _map.roomIdMatrix[c.y, c.x] = r.id;
            }
        }
    }

    private readonly struct Edge {
        public readonly int a, b, w; // indices in map.rooms

        public Edge(int a, int b, int w) {
            this.a = a;
            this.b = b;
            this.w = w;
        }
    }

    private void ConnectRoomsMst() {
        int n = _map.rooms.Count;
        if (n <= 1) {
            return;
        }

        List<Edge> edges = new(n * (n - 1) / 2);

        for (int i = 0; i < n; i++) {
            Vector2Int ca = _map.rooms[i].center;
            for (int j = i + 1; j < n; j++) {
                Vector2Int cb = _map.rooms[j].center;
                int manhattan = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y);
                edges.Add(new Edge(i, j, manhattan));
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
            CarveCorridorBetweenRooms(_map.rooms[e.a], _map.rooms[e.b]);
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

        bool xThenY = randomizeLTurnOrder ? _rng.NextDouble() < 0.5 : true;

        if (xThenY) {
            CarveLine(doorA.x, doorA.y, doorB.x, doorA.y);
            CarveLine(doorB.x, doorA.y, doorB.x, doorB.y);
        } else {
            CarveLine(doorA.x, doorA.y, doorA.x, doorB.y);
            CarveLine(doorA.x, doorB.y, doorB.x, doorB.y);
        }
    }

    private static Cell PickBestDoorCell(Room room, Vector2Int toward) {
        Cell best = room.Cells[0];
        int bestScore = int.MaxValue;

        for (int i = 0; i < room.Cells.Count; i++) {
            Cell c = room.Cells[i];

            bool isBoundary =
                !room.FloorSet.Contains(Utility.Pack(c.x + 1, c.y)) ||
                !room.FloorSet.Contains(Utility.Pack(c.x - 1, c.y)) ||
                !room.FloorSet.Contains(Utility.Pack(c.x, c.y + 1)) ||
                !room.FloorSet.Contains(Utility.Pack(c.x, c.y - 1));

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

            _map.wallMatrix[y, x] = false;
        }
    }

    private void ApplyBorderWallsIfNeeded() {
        if (!keepBorderWalls) {
            return;
        }

        for (int x = 0; x < _map.width; x++) {
            _map.wallMatrix[0, x] = true;
            _map.wallMatrix[_map.height - 1, x] = true;
        }

        for (int y = 0; y < _map.height; y++) {
            _map.wallMatrix[y, 0] = true;
            _map.wallMatrix[y, _map.width - 1] = true;
        }
    }

    private Vector3 GetGridOrigin(int width, int height, float cellWorldSize) {
        Vector3 center = basePart != null ? basePart.position : transform.position;

        float totalW = width * cellWorldSize;
        float totalH = height * cellWorldSize;

        return new Vector3(center.x - (totalW * 0.5f), 0f, center.z - (totalH * 0.5f));
    }
}
