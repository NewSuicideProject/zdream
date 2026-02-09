using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1) Seed + organic random-walk blob -> floor cells
/// 2) Flood-fill floor blobs -> rooms with roomId
/// 3) roomWallCells: floor cells that touch any wall in 8-neighborhood
///
/// Outputs:
/// - wall: true=wall, false=floor
/// - roomId: -1 for wall, >=0 for rooms
/// - rooms: list of rooms (cells + wallCells + center)
/// </summary>
public sealed class OrganicRoomShaper : MonoBehaviour {
    [Header("Grid")] [SerializeField] private int gridWidth = 128;
    [SerializeField] private int gridHeight = 128;

    [Header("Random")] [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool useRandomSeed = true;

    [Header("Organic seeds")] [SerializeField] [Min(1)]
    private int seedCount = 10;

    [SerializeField] [Range(100, 50000)] private int totalWalkSteps = 15000;
    [SerializeField] [Range(0f, 1f)] private float turnChance = 0.6f;
    [SerializeField] [Range(0, 2)] private int blobRadius = 1;

    [Header("Room extraction")] [SerializeField] [Min(1)]
    private int minRoomCells = 120;

    public sealed class Room {
        public int id;
        public List<Vector2Int> floorCells = new();
        public List<Vector2Int> roomWallCells = new(); // floor cells that touch wall (8-neigh)
        public Vector2Int center; // average of floorCells
    }

    public bool[,] Wall { get; private set; } // true=wall, false=floor
    public int[,] RoomId { get; private set; } // -1 wall, >=0 room id
    public IReadOnlyList<Room> Rooms => rooms;

    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;

    private readonly List<Room> rooms = new();
    private System.Random rng;

    private static readonly Vector2Int[] Dir4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private static readonly Vector2Int[] Dir8 = {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    private void Awake() {
        if (useRandomSeed) {
            randomSeed = Environment.TickCount;
        }

        rng = new System.Random(randomSeed);
    }

    public void Generate() {
        InitAllWalls();
        CarveOrganicFloors();
        ExtractRoomsAndWalls();
    }

    private void InitAllWalls() {
        Wall = new bool[gridWidth, gridHeight];
        RoomId = new int[gridWidth, gridHeight];
        rooms.Clear();

        for (int y = 0; y < gridHeight; y++)
        for (int x = 0; x < gridWidth; x++) {
            Wall[x, y] = true;
            RoomId[x, y] = -1;
        }
    }

    private void CarveOrganicFloors() {
        List<Vector2Int> walkers = new(seedCount);

        for (int i = 0; i < seedCount; i++) {
            Vector2Int seed = new(rng.Next(2, gridWidth - 2), rng.Next(2, gridHeight - 2));
            walkers.Add(seed);
            CarveBlob(seed, blobRadius);
        }

        int stepsPer = Mathf.Max(1, totalWalkSteps / seedCount);

        for (int i = 0; i < walkers.Count; i++) {
            Vector2Int p = walkers[i];
            Vector2Int dir = RandomDir4();

            for (int s = 0; s < stepsPer; s++) {
                if ((float)rng.NextDouble() < turnChance) {
                    dir = RandomDir4();
                }

                p += dir;
                p.x = Mathf.Clamp(p.x, 1, gridWidth - 2);
                p.y = Mathf.Clamp(p.y, 1, gridHeight - 2);

                CarveBlob(p, blobRadius);
            }
        }
    }

    private void CarveBlob(Vector2Int center, int r) {
        for (int oy = -r; oy <= r; oy++)
        for (int ox = -r; ox <= r; ox++) {
            int x = center.x + ox;
            int y = center.y + oy;

            if (x <= 0 || y <= 0 || x >= gridWidth - 1 || y >= gridHeight - 1) {
                continue;
            }

            if (r > 0 && Mathf.Abs(ox) + Mathf.Abs(oy) > r + 0.5f) {
                continue;
            }

            Wall[x, y] = false;
        }
    }

    private void ExtractRoomsAndWalls() {
        bool[,] visited = new bool[gridWidth, gridHeight];

        int nextRoomId = 0;

        for (int y = 1; y < gridHeight - 1; y++)
        for (int x = 1; x < gridWidth - 1; x++) {
            if (visited[x, y]) {
                continue;
            }

            if (Wall[x, y]) {
                continue;
            }

            List<Vector2Int> floorCells = FloodFillFloor(new Vector2Int(x, y), visited);
            if (floorCells.Count < minRoomCells) {
                continue;
            }

            Room room = new() { id = nextRoomId++ };
            room.floorCells = floorCells;
            room.center = ComputeCenter(floorCells);

            foreach (Vector2Int c in floorCells) {
                RoomId[c.x, c.y] = room.id;
            }

            room.roomWallCells = ComputeRoomWallCells(floorCells);

            rooms.Add(room);
        }
    }

    private List<Vector2Int> FloodFillFloor(Vector2Int start, bool[,] visited) {
        Queue<Vector2Int> q = new();
        List<Vector2Int> outCells = new(256);

        visited[start.x, start.y] = true;
        q.Enqueue(start);

        while (q.Count > 0) {
            Vector2Int p = q.Dequeue();
            outCells.Add(p);

            for (int i = 0; i < Dir4.Length; i++) {
                Vector2Int n = p + Dir4[i];
                if (!IsInner(n)) {
                    continue;
                }

                if (visited[n.x, n.y]) {
                    continue;
                }

                if (Wall[n.x, n.y]) {
                    continue;
                }

                visited[n.x, n.y] = true;
                q.Enqueue(n);
            }
        }

        return outCells;
    }

    private List<Vector2Int> ComputeRoomWallCells(List<Vector2Int> floorCells) {
        List<Vector2Int> outWalls = new(128);

        for (int i = 0; i < floorCells.Count; i++) {
            Vector2Int c = floorCells[i];
            bool touchesWall = false;

            for (int k = 0; k < Dir8.Length; k++) {
                Vector2Int n = c + Dir8[k];
                if (!IsInside(n)) {
                    continue;
                }

                if (Wall[n.x, n.y]) {
                    touchesWall = true;
                    break;
                }
            }

            if (touchesWall) {
                outWalls.Add(c);
            }
        }

        return outWalls;
    }

    private Vector2Int ComputeCenter(List<Vector2Int> cells) {
        long sx = 0, sy = 0;
        for (int i = 0; i < cells.Count; i++) {
            sx += cells[i].x;
            sy += cells[i].y;
        }

        return new Vector2Int(
            Mathf.RoundToInt((float)sx / cells.Count),
            Mathf.RoundToInt((float)sy / cells.Count)
        );
    }

    private bool IsInside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridWidth && p.y < gridHeight;
    private bool IsInner(Vector2Int p) => p.x > 0 && p.y > 0 && p.x < gridWidth - 1 && p.y < gridHeight - 1;

    private Vector2Int RandomDir4() => Dir4[rng.Next(0, Dir4.Length)];
}
