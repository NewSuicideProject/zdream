using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Train.Environment.Scripts {
    public class Environment : MonoBehaviour {
        [SerializeField] private Transform basePart;

        [SerializeField] private Transform wallParent;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject floorPrefab;

        [SerializeField] private float wallCenterY = 2.5f;

        [SerializeField] private Transform zombieSpawnMarker;
        [SerializeField] private Transform targetSpawnMarker;

        [Min(8)] [SerializeField] private int gridWidth = 64;
        [Min(8)] [SerializeField] private int gridHeight = 64;
        [Min(0.1f)] [SerializeField] private float cellSize = 1f;

        [SerializeField] private int seed; // 0 => random each play

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

        [Min(1)] [SerializeField] private int roadWidth = 1;
        [SerializeField] private bool randomizeLTurnOrder = true;

        [Range(0, 3)] [SerializeField] private int organicIterations = 1;
        [Range(0f, 0.25f)] [SerializeField] private float organicCarveRatio = 0.06f;
        [Range(0f, 0.25f)] [SerializeField] private float organicGrowRatio = 0.05f;
        [Min(1)] [SerializeField] private int organicGrowMaxTriesPerCell = 6;

        [Header("Height")] [SerializeField] private int minRoomLevel = -2;
        [SerializeField] private int maxRoomLevel = 2;

        [Min(0.01f)] [SerializeField] private float levelStepHeight = 1.0f; // 1 level -> world Y height
        [SerializeField] private float floorThickness = 0.2f;


        private MapData _map;
        private System.Random _rng;

        private OrganicShaper _organicShaper;
        private Visualizer _visualizer;

        private void Awake() {
            if (wallParent == null) {
                wallParent = transform;
            }

            _organicShaper = new OrganicShaper();

            _visualizer = new Visualizer(
                wallParent,
                wallPrefab,
                wallCenterY,
                floorPrefab,
                floorThickness
            );
        }


        private void Start() => Generate();

        public void Generate() {
            int actualSeed = seed == 0 ? System.Environment.TickCount : seed;
            _rng = new System.Random(actualSeed);

            _map = new MapData {
                Width = gridWidth,
                Height = gridHeight,
                CellSize = cellSize,
                Origin = GetGridOrigin(gridWidth, gridHeight, cellSize),
                WallMatrix = new bool[gridHeight, gridWidth],
                RoomIdMatrix = new int[gridHeight, gridWidth],
                TileHeight = new float[gridHeight, gridWidth],
                Rooms = new List<Room>(roomCount)
            };

            InitializeMatrices();

            PlaceRooms();

            foreach (Room room in _map.Rooms) {
                float roomHeight = LevelToHeight(room.heightLevel);

                foreach (Cell cell in room.Cells.Where(cell => InBounds(cell.X, cell.Y))) {
                    _map.WallMatrix[cell.Y, cell.X] = false;
                    _map.RoomIdMatrix[cell.Y, cell.X] = room.id;
                    _map.TileHeight[cell.Y, cell.X] = roomHeight;
                }
            }

            ConnectRoomsMstAndPaintRoadHeights();

            _visualizer.Rebuild(_map);
            _visualizer.PlaceSpawnMarkers(_map, zombieSpawnMarker, targetSpawnMarker);
        }

        private void InitializeMatrices() {
            for (int y = 0; y < _map.Height; y++)
            for (int x = 0; x < _map.Width; x++) {
                _map.WallMatrix[y, x] = true;
                _map.RoomIdMatrix[y, x] = -1;
                _map.TileHeight[y, x] = 0f;
            }
        }

        private bool InBounds(int x, int y) => x >= 0 && x < _map.Width && y >= 0 && y < _map.Height;


        private float LevelToHeight(int level) => level * levelStepHeight;

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
                Room room = makeCircle ? GetCircleRoom(placed) : GetRectRoom(placed);
                if (room == null) {
                    continue;
                }

                if (!IsRoomValid(room)) {
                    continue;
                }

                room.Initialize();
                _organicShaper.ApplyLightOrganic(room, _rng, organicCfg);

                _map.Rooms.Add(room);
                placed++;
            }

            if (_map.Rooms.Count < 2) {
                Debug.LogWarning(
                    $"[Environment] Only {_map.Rooms.Count} rooms placed. Increase rerolls / reduce padding / increase grid size.");
            }
        }

        private Room GetRectRoom(int id) {
            int roomWidthCells = _rng.Next(rectMinW, rectMaxW + 1);
            int roomHeightCells = _rng.Next(rectMinH, rectMaxH + 1);

            const int border = 1;

            int maxLeftX = _map.Width - roomWidthCells - border;
            int maxBottomY = _map.Height - roomHeightCells - border;
            if (maxLeftX < border || maxBottomY < border) {
                return null;
            }

            int leftX = _rng.Next(border, maxLeftX + 1);
            int bottomY = _rng.Next(border, maxBottomY + 1);

            int centerX = leftX + (roomWidthCells / 2);
            int centerY = bottomY + (roomHeightCells / 2);

            Room room = new() {
                id = id,
                bounds = new RectInt(leftX, bottomY, roomWidthCells, roomHeightCells),
                center = new Vector2Int(centerX, centerY),
                heightLevel = _rng.Next(minRoomLevel, maxRoomLevel + 1)
            };

            int rightXExclusive = leftX + roomWidthCells;
            int topYExclusive = bottomY + roomHeightCells;

            for (int y = bottomY; y < topYExclusive; y++)
            for (int x = leftX; x < rightXExclusive; x++) {
                room.Cells.Add(new Cell(x, y));
            }

            return room;
        }

        private Room GetCircleRoom(int id) {
            int radiusCells = _rng.Next(circleMinR, circleMaxR + 1);
            int diameterCells = (radiusCells * 2) + 1;

            const int border = 1;
            int minCenterX = border + radiusCells;
            int minCenterY = border + radiusCells;

            int maxCenterX = _map.Width - border - radiusCells - 1;
            int maxCenterY = _map.Height - border - radiusCells - 1;
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
                center = new Vector2Int(centerX, centerY),
                heightLevel = _rng.Next(minRoomLevel, maxRoomLevel + 1)
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

        private bool IsRoomValid(Room room) {
            RectInt expanded = Utility.ExpandRect(room.bounds, roomPadding);

            if (expanded.xMin < 0 || expanded.yMin < 0 || expanded.xMax > _map.Width || expanded.yMax > _map.Height) {
                return false;
            }

            foreach (Room other in _map.Rooms) {
                RectInt otherExpanded = Utility.ExpandRect(other.bounds, roomPadding);
                if (expanded.Overlaps(otherExpanded)) {
                    return false;
                }
            }

            return true;
        }


        private readonly struct Edge {
            public readonly int A, B, W;

            public Edge(int a, int b, int w) {
                A = a;
                B = b;
                W = w;
            }
        }

        private void ConnectRoomsMstAndPaintRoadHeights() {
            int n = _map.Rooms.Count;
            if (n <= 1) {
                return;
            }

            List<Edge> edges = new(n * (n - 1) / 2);

            for (int i = 0; i < n; i++) {
                Vector2Int ca = _map.Rooms[i].center;
                for (int j = i + 1; j < n; j++) {
                    Vector2Int cb = _map.Rooms[j].center;
                    int manhattan = Mathf.Abs(ca.x - cb.x) + Mathf.Abs(ca.y - cb.y);
                    edges.Add(new Edge(i, j, manhattan));
                }
            }

            edges.Sort(static (e1, e2) => e1.W.CompareTo(e2.W));

            DSU dsu = new(n);
            int picked = 0;

            for (int i = 0; i < edges.Count && picked < n - 1; i++) {
                Edge e = edges[i];
                if (!dsu.Union(e.A, e.B)) {
                    continue;
                }

                picked++;

                Room a = _map.Rooms[e.A];
                Room b = _map.Rooms[e.B];

                List<Cell> roadPath = BuildRoadPathCells(a, b);

                float fromH = LevelToHeight(a.heightLevel);
                float toH = LevelToHeight(b.heightLevel);

                ApplyRoadWithConstantStepRise(roadPath, fromH, toH);
            }
        }

        private sealed class DSU {
            private readonly int[] _p;
            private readonly int[] _r;

            public DSU(int n) {
                _p = new int[n];
                _r = new int[n];
                for (int i = 0; i < n; i++) {
                    _p[i] = i;
                }
            }

            private int Find(int x) {
                while (_p[x] != x) {
                    _p[x] = _p[_p[x]];
                    x = _p[x];
                }

                return x;
            }

            public bool Union(int a, int b) {
                int ra = Find(a), rb = Find(b);
                if (ra == rb) {
                    return false;
                }

                if (_r[ra] < _r[rb]) {
                    _p[ra] = rb;
                } else if (_r[ra] > _r[rb]) {
                    _p[rb] = ra;
                } else {
                    _p[rb] = ra;
                    _r[ra]++;
                }

                return true;
            }
        }

        private List<Cell> BuildRoadPathCells(Room a, Room b) {
            Cell doorA = PickBestDoorCell(a, b.center);
            Cell doorB = PickBestDoorCell(b, a.center);

            bool xThenY = randomizeLTurnOrder ? _rng.NextDouble() < 0.5 : true;

            List<Cell> path = new(Mathf.Abs(doorA.X - doorB.X) + Mathf.Abs(doorA.Y - doorB.Y) + 2);

            if (xThenY) {
                AppendLineCells(path, doorA.X, doorA.Y, doorB.X, doorA.Y);
                AppendLineCells(path, doorB.X, doorA.Y, doorB.X, doorB.Y);
            } else {
                AppendLineCells(path, doorA.X, doorA.Y, doorA.X, doorB.Y);
                AppendLineCells(path, doorA.X, doorB.Y, doorB.X, doorB.Y);
            }

            return path;
        }

        private static Cell PickBestDoorCell(Room room, Vector2Int toward) {
            Cell best = room.Cells[0];
            int bestScore = int.MaxValue;

            for (int i = 0; i < room.Cells.Count; i++) {
                Cell c = room.Cells[i];

                bool isBoundary =
                    !room.FloorSet.Contains(Utility.Pack(c.X + 1, c.Y)) ||
                    !room.FloorSet.Contains(Utility.Pack(c.X - 1, c.Y)) ||
                    !room.FloorSet.Contains(Utility.Pack(c.X, c.Y + 1)) ||
                    !room.FloorSet.Contains(Utility.Pack(c.X, c.Y - 1));

                if (!isBoundary) {
                    continue;
                }

                int score = Mathf.Abs(c.X - toward.x) + Mathf.Abs(c.Y - toward.y);
                if (score < bestScore) {
                    bestScore = score;
                    best = c;
                }
            }

            return best;
        }

        private static void AppendLineCells(List<Cell> outCells, int x0, int y0, int x1, int y1) {
            int dx = Math.Sign(x1 - x0);
            int dy = Math.Sign(y1 - y0);

            int x = x0;
            int y = y0;

            if (outCells.Count == 0 || outCells[outCells.Count - 1].X != x || outCells[outCells.Count - 1].Y != y) {
                outCells.Add(new Cell(x, y));
            }

            while (x != x1) {
                x += dx;
                outCells.Add(new Cell(x, y));
            }

            while (y != y1) {
                y += dy;
                outCells.Add(new Cell(x, y));
            }
        }

        private void ApplyRoadWithConstantStepRise(List<Cell> roadCells, float fromHeight, float toHeight) {
            if (roadCells == null || roadCells.Count == 0) {
                return;
            }

            int segments = roadCells.Count - 1;
            if (segments <= 0) {
                Cell only = roadCells[0];
                PaintRoadCell(only.X, only.Y, fromHeight);
                return;
            }

            float stepRise = (toHeight - fromHeight) / segments;

            for (int i = 0; i < roadCells.Count; i++) {
                float h = fromHeight + (stepRise * i);
                Cell c = roadCells[i];
                PaintRoadCell(c.X, c.Y, h);
            }

            Cell last = roadCells[roadCells.Count - 1];
            PaintRoadCell(last.X, last.Y, toHeight);
        }

        private void PaintRoadCell(int cx, int cy, float height) {
            int halfA = (roadWidth - 1) / 2;
            int halfB = roadWidth / 2;

            for (int oy = -halfA; oy <= halfB; oy++)
            for (int ox = -halfA; ox <= halfB; ox++) {
                int x = cx + ox;
                int y = cy + oy;
                if (!InBounds(x, y)) {
                    continue;
                }

                _map.WallMatrix[y, x] = false;
                _map.TileHeight[y, x] = height;
            }
        }

        private Vector3 GetGridOrigin(int width, int height, float cellWorldSize) {
            Vector3 center = basePart != null ? basePart.position : transform.position;

            float totalW = width * cellWorldSize;
            float totalH = height * cellWorldSize;

            return new Vector3(center.x - (totalW * 0.5f), 0f, center.z - (totalH * 0.5f));
        }
    }

    public readonly struct Cell {
        public readonly int X, Y;

        public Cell(int x, int y) {
            X = x;
            Y = y;
        }
    }

    public sealed class MapData {
        public int Width;
        public int Height;
        public float CellSize;
        public Vector3 Origin;

        public bool[,] WallMatrix; // true=wall, false=floor
        public int[,] RoomIdMatrix; // -1 if not room (road remains -1)
        public float[,] TileHeight; // NEW: per-tile world height (Y)
        public List<Room> Rooms;
    }

    [Serializable]
    public sealed class Room {
        public int id;
        public RectInt bounds;
        public Vector2Int center;
        public int heightLevel;
        public readonly List<Cell> Cells = new();
        public readonly HashSet<int> FloorSet = new();

        public void Initialize() {
            FloorSet.Clear();
            foreach (Cell cell in Cells) {
                FloorSet.Add(Utility.Pack(cell.X, cell.Y));
            }
        }
    }
}
