using System.Collections.Generic;
using UnityEngine;

public sealed class OrganicShaper {
    public readonly struct Config {
        public readonly int iterations;
        public readonly float carveRatio;
        public readonly float growRatio;
        public readonly int growMaxTriesPerCell;

        public Config(int iterations, float carveRatio, float growRatio, int growMaxTriesPerCell) {
            this.iterations = iterations;
            this.carveRatio = carveRatio;
            this.growRatio = growRatio;
            this.growMaxTriesPerCell = growMaxTriesPerCell;
        }
    }

    private static bool InRect(RectInt r, int x, int y) => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;

    public void ApplyLightOrganic(Environment.Room room, System.Random rng, Config cfg) {
        if (room == null) {
            return;
        }

        if (cfg.iterations <= 0) {
            return;
        }

        for (int it = 0; it < cfg.iterations; it++) {
            // 1) carve boundary cells
            List<Environment.Cell> boundary = CollectBoundaryCells(room.floorSet, room.bounds);
            int carveCount = Mathf.Clamp(Mathf.RoundToInt(room.floorSet.Count * cfg.carveRatio), 0, boundary.Count);

            for (int k = 0; k < carveCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                Environment.Cell c = boundary[idx];
                boundary.RemoveAt(idx);

                if (CountFloorNeighbors(room.floorSet, c.x, c.y) >= 2) {
                    room.floorSet.Remove(Environment.Pack(c.x, c.y));
                }
            }

            // 2) grow into neighbors (still inside bounds)
            boundary = CollectBoundaryCells(room.floorSet, room.bounds);
            int growCount = Mathf.Clamp(Mathf.RoundToInt(room.floorSet.Count * cfg.growRatio), 0, boundary.Count);

            for (int k = 0; k < growCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                Environment.Cell b = boundary[idx];
                boundary.RemoveAt(idx);

                for (int t = 0; t < cfg.growMaxTriesPerCell; t++) {
                    Environment.Cell n = PickRandom4Neighbor(b.x, b.y, rng);
                    if (!InRect(room.bounds, n.x, n.y)) {
                        continue;
                    }

                    int p = Environment.Pack(n.x, n.y);
                    if (!room.floorSet.Contains(p)) {
                        if (CountFloorNeighbors(room.floorSet, n.x, n.y) >= 2) {
                            room.floorSet.Add(p);
                            break;
                        }
                    }
                }
            }
        }

        // write back to cells list
        room.cells.Clear();
        foreach (int p in room.floorSet) {
            Environment.Unpack(p, out int x, out int y);
            room.cells.Add(new Environment.Cell(x, y));
        }
    }

    private List<Environment.Cell> CollectBoundaryCells(HashSet<int> floorSet, RectInt bounds) {
        List<Environment.Cell> result = new();

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++) {
            int p = Environment.Pack(x, y);
            if (!floorSet.Contains(p)) {
                continue;
            }

            if (!IsFloor(floorSet, bounds, x + 1, y) ||
                !IsFloor(floorSet, bounds, x - 1, y) ||
                !IsFloor(floorSet, bounds, x, y + 1) ||
                !IsFloor(floorSet, bounds, x, y - 1)) {
                result.Add(new Environment.Cell(x, y));
            }
        }

        return result;
    }

    private bool IsFloor(HashSet<int> set, RectInt bounds, int x, int y) {
        if (!InRect(bounds, x, y)) {
            return false;
        }

        return set.Contains(Environment.Pack(x, y));
    }

    private int CountFloorNeighbors(HashSet<int> set, int x, int y) {
        int n = 0;
        if (set.Contains(Environment.Pack(x + 1, y))) {
            n++;
        }

        if (set.Contains(Environment.Pack(x - 1, y))) {
            n++;
        }

        if (set.Contains(Environment.Pack(x, y + 1))) {
            n++;
        }

        if (set.Contains(Environment.Pack(x, y - 1))) {
            n++;
        }

        return n;
    }

    private Environment.Cell PickRandom4Neighbor(int x, int y, System.Random rng) {
        int r = rng.Next(4);
        return r switch {
            0 => new Environment.Cell(x + 1, y),
            1 => new Environment.Cell(x - 1, y),
            2 => new Environment.Cell(x, y + 1),
            _ => new Environment.Cell(x, y - 1)
        };
    }
}
