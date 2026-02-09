using System;
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

    public void ApplyLightOrganic(Environment.Room room, System.Random rng, Config cfg) {
        if (room == null) {
            return;
        }

        if (rng == null) {
            return;
        }

        if (cfg.Iterations <= 0) {
            return;
        }

        for (int it = 0; it < cfg.Iterations; it++) {
            // 1) Carve boundary cells
            List<Environment.Cell> boundary = CollectBoundaryCells(room.FloorSet, room.bounds);
            int carveCount = Mathf.Clamp(Mathf.RoundToInt(room.FloorSet.Count * cfg.CarveRatio), 0, boundary.Count);

            for (int k = 0; k < carveCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                Environment.Cell c = boundary[idx];
                boundary.RemoveAt(idx);

                if (CountFloorNeighbors(room.FloorSet, c.X, c.Y) >= 2) {
                    room.FloorSet.Remove(Utility.Pack(c.X, c.Y));
                }
            }

            // 2) Grow into neighbors (still inside bounds)
            boundary = CollectBoundaryCells(room.FloorSet, room.bounds);
            int growCount = Mathf.Clamp(Mathf.RoundToInt(room.FloorSet.Count * cfg.GrowRatio), 0, boundary.Count);

            for (int k = 0; k < growCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                Environment.Cell b = boundary[idx];
                boundary.RemoveAt(idx);

                for (int t = 0; t < cfg.GrowMaxTriesPerCell; t++) {
                    Environment.Cell n = PickRandom4Neighbor(b.X, b.Y, rng);
                    if (!InRect(room.bounds, n.X, n.Y)) {
                        continue;
                    }

                    int p = Utility.Pack(n.X, n.Y);
                    if (!room.FloorSet.Contains(p)) {
                        if (CountFloorNeighbors(room.FloorSet, n.X, n.Y) >= 2) {
                            room.FloorSet.Add(p);
                            break;
                        }
                    }
                }
            }
        }

        // Write back to cells list
        room.Cells.Clear();
        foreach (int p in room.FloorSet) {
            Utility.Unpack(p, out int x, out int y);
            room.Cells.Add(new Environment.Cell(x, y));
        }
    }

    private List<Environment.Cell> CollectBoundaryCells(HashSet<int> floorSet, RectInt bounds) {
        List<Environment.Cell> result = new();

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
                result.Add(new Environment.Cell(x, y));
            }
        }

        return result;
    }

    private bool IsFloor(HashSet<int> set, RectInt bounds, int x, int y) {
        if (!InRect(bounds, x, y)) {
            return false;
        }

        return set.Contains(Utility.Pack(x, y));
    }

    private int CountFloorNeighbors(HashSet<int> set, int x, int y) {
        int n = 0;
        if (set.Contains(Utility.Pack(x + 1, y))) {
            n++;
        }

        if (set.Contains(Utility.Pack(x - 1, y))) {
            n++;
        }

        if (set.Contains(Utility.Pack(x, y + 1))) {
            n++;
        }

        if (set.Contains(Utility.Pack(x, y - 1))) {
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
