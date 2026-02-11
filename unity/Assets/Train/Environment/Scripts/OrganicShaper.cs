using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
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

        private static bool InRect(RectInt r, int x, int y)
            => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;

        public void ApplyLightOrganic(Room room, System.Random rng, Config cfg) {
            if (room == null || rng == null || cfg.Iterations <= 0) {
                return;
            }

            if (room.FloorSet.Count == 0) {
                return;
            }

            for (int it = 0; it < cfg.Iterations; it++) {
                // 1) Carve boundary cells
                List<Vector2Int> boundary = CollectBoundaryCells(room.FloorSet, room.bounds);
                int carveCount = Mathf.Clamp(Mathf.RoundToInt(room.FloorSet.Count * cfg.CarveRatio), 0, boundary.Count);

                for (int k = 0; k < carveCount && boundary.Count > 0; k++) {
                    int idx = rng.Next(boundary.Count);
                    Vector2Int c = boundary[idx];
                    boundary.RemoveAt(idx);

                    if (CountFloorNeighbors(room.FloorSet, c) >= 2) {
                        room.FloorSet.Remove(c);
                    }
                }

                // 2) Grow into neighbors (still inside bounds)
                boundary = CollectBoundaryCells(room.FloorSet, room.bounds);
                int growCount = Mathf.Clamp(Mathf.RoundToInt(room.FloorSet.Count * cfg.GrowRatio), 0, boundary.Count);

                for (int k = 0; k < growCount && boundary.Count > 0; k++) {
                    int idx = rng.Next(boundary.Count);
                    Vector2Int b = boundary[idx];
                    boundary.RemoveAt(idx);

                    for (int t = 0; t < cfg.GrowMaxTriesPerCell; t++) {
                        Vector2Int n = PickRandom4Neighbor(b, rng);
                        if (!InRect(room.bounds, n.x, n.y)) {
                            continue;
                        }

                        if (!room.FloorSet.Contains(n)) {
                            if (CountFloorNeighbors(room.FloorSet, n) >= 2) {
                                room.FloorSet.Add(n);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static List<Vector2Int> CollectBoundaryCells(HashSet<Vector2Int> floorSet, RectInt bounds) {
            List<Vector2Int> result = new();

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++) {
                Vector2Int p = new(x, y);
                if (!floorSet.Contains(p)) {
                    continue;
                }

                if (!IsFloor(floorSet, bounds, x + 1, y) ||
                    !IsFloor(floorSet, bounds, x - 1, y) ||
                    !IsFloor(floorSet, bounds, x, y + 1) ||
                    !IsFloor(floorSet, bounds, x, y - 1)) {
                    result.Add(p);
                }
            }

            return result;
        }

        private static bool IsFloor(HashSet<Vector2Int> set, RectInt bounds, int x, int y) {
            if (!InRect(bounds, x, y)) {
                return false;
            }

            return set.Contains(new Vector2Int(x, y));
        }

        private static int CountFloorNeighbors(HashSet<Vector2Int> set, Vector2Int c) {
            int n = 0;
            if (set.Contains(new Vector2Int(c.x + 1, c.y))) {
                n++;
            }

            if (set.Contains(new Vector2Int(c.x - 1, c.y))) {
                n++;
            }

            if (set.Contains(new Vector2Int(c.x, c.y + 1))) {
                n++;
            }

            if (set.Contains(new Vector2Int(c.x, c.y - 1))) {
                n++;
            }

            return n;
        }

        private static Vector2Int PickRandom4Neighbor(Vector2Int c, System.Random rng) {
            int r = rng.Next(4);
            return r switch {
                0 => new Vector2Int(c.x + 1, c.y),
                1 => new Vector2Int(c.x - 1, c.y),
                2 => new Vector2Int(c.x, c.y + 1),
                _ => new Vector2Int(c.x, c.y - 1)
            };
        }
    }
}
