using System.Collections.Generic;
using UnityEngine;

public sealed class OrganicShaper {
    public readonly struct Config {
        public readonly int Iterations;
        public readonly float CarveRatio;
        public readonly float GrowRatio;
        public readonly int GrowMaxTriesPerCell;

        public Config(int iterations, float carveRatio, float growRatio, int growMaxTriesPerCell) {
            Iterations = iterations;
            CarveRatio = carveRatio;
            GrowRatio = growRatio;
            GrowMaxTriesPerCell = growMaxTriesPerCell;
        }
    }

    private static bool InRect(RectInt r, int x, int y) => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;

    public void ApplyLightOrganic(Room room, System.Random rng, Config cfg) {
        if (room == null || rng == null || cfg.Iterations <= 0) {
            return;
        }

        for (int it = 0; it < cfg.Iterations; it++) {
            // 1) Carve boundary cells
            List<Cell> boundary = CollectBoundaryCells(room.FloorSet, room.bounds);
            int carveCount = Mathf.Clamp(Mathf.RoundToInt(room.FloorSet.Count * cfg.CarveRatio), 0, boundary.Count);

            for (int k = 0; k < carveCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                Cell c = boundary[idx];
                boundary.RemoveAt(idx);

                if (CountFloorNeighbors(room.FloorSet, c) >= 2) {
                    room.FloorSet.Remove(Utility.Pack(c.X, c.Y));
                }
            }

            // 2) Grow into neighbors (still inside bounds)
            boundary = CollectBoundaryCells(room.FloorSet, room.bounds);
            int growCount = Mathf.Clamp(Mathf.RoundToInt(room.FloorSet.Count * cfg.GrowRatio), 0, boundary.Count);

            for (int k = 0; k < growCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                Cell b = boundary[idx];
                boundary.RemoveAt(idx);

                for (int t = 0; t < cfg.GrowMaxTriesPerCell; t++) {
                    Cell n = PickRandom4Neighbor(b, rng);
                    if (!InRect(room.bounds, n.X, n.Y)) {
                        continue;
                    }

                    int p = Utility.Pack(n.X, n.Y);
                    if (!room.FloorSet.Contains(p)) {
                        if (CountFloorNeighbors(room.FloorSet, n) >= 2) {
                            room.FloorSet.Add(p);
                            break;
                        }
                    }
                }
            }
        }

        room.RebuildCellsFromFloorSet();
    }

    private static List<Cell> CollectBoundaryCells(HashSet<int> floorSet, RectInt bounds) {
        List<Cell> result = new();

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++) {
            int p = Utility.Pack(x, y);
            if (!floorSet.Contains(p)) {
                continue;
            }

            if (!IsFloor(floorSet, bounds, x + 1, y) ||
                !IsFloor(floorSet, bounds, x - 1, y) ||
                !IsFloor(floorSet, bounds, x, y + 1) ||
                !IsFloor(floorSet, bounds, x, y - 1)) {
                result.Add(new Cell(x, y));
            }
        }

        return result;
    }

    private static bool IsFloor(HashSet<int> set, RectInt bounds, int x, int y) {
        if (!InRect(bounds, x, y)) {
            return false;
        }

        return set.Contains(Utility.Pack(x, y));
    }

    private static int CountFloorNeighbors(HashSet<int> set, Cell c) {
        int n = 0;
        if (set.Contains(Utility.Pack(c.X + 1, c.Y))) {
            n++;
        }

        if (set.Contains(Utility.Pack(c.X - 1, c.Y))) {
            n++;
        }

        if (set.Contains(Utility.Pack(c.X, c.Y + 1))) {
            n++;
        }

        if (set.Contains(Utility.Pack(c.X, c.Y - 1))) {
            n++;
        }

        return n;
    }

    private static Cell PickRandom4Neighbor(Cell c, System.Random rng) {
        int r = rng.Next(4);
        return r switch {
            0 => new Cell(c.X + 1, c.Y),
            1 => new Cell(c.X - 1, c.Y),
            2 => new Cell(c.X, c.Y + 1),
            _ => new Cell(c.X, c.Y - 1)
        };
    }
}
