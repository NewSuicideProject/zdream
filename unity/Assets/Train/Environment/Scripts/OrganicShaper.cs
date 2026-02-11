using System.Collections.Generic;
using UnityEngine;

namespace Train.Environment.Scripts {
    public class OrganicShaper {
        private readonly int _iterations;
        private readonly float _carveRatio;
        private readonly float _growRatio;
        private readonly int _growMaxTries;

        public OrganicShaper(int iterations, float carveRatio, float growRatio, int growMaxTries) {
            _iterations = Mathf.Max(0, iterations);
            _carveRatio = Mathf.Clamp(carveRatio, 0f, 0.25f);
            _growRatio = Mathf.Clamp(growRatio, 0f, 0.25f);
            _growMaxTries = Mathf.Max(1, growMaxTries);
        }

        public void Organic(Room room, System.Random rng) {
            if (_iterations <= 0 || room.FloorSet.Count == 0) {
                return;
            }

            for (int it = 0; it < _iterations; it++) {
                Carve(room, rng);
                Grow(room, rng);
            }
        }

        private void Carve(Room room, System.Random rng) {
            List<Vector2Int> boundary = room.CollectBoundaryCells();
            int carveCount = Mathf.Clamp(
                Mathf.RoundToInt(room.FloorSet.Count * _carveRatio),
                0,
                boundary.Count
            );

            for (int i = 0; i < carveCount && boundary.Count > 0; i++) {
                int idx = rng.Next(boundary.Count);
                Vector2Int c = boundary[idx];
                boundary.RemoveAt(idx);

                if (room.CountFloorNeighbors4(c) >= 2) {
                    room.TryRemoveFloor(c);
                }
            }
        }

        private void Grow(Room room, System.Random rng) {
            List<Vector2Int> boundary = room.CollectBoundaryCells();
            int growCount = Mathf.Clamp(
                Mathf.RoundToInt(room.FloorSet.Count * _growRatio),
                0,
                boundary.Count
            );

            for (int i = 0; i < growCount && boundary.Count > 0; i++) {
                int idx = rng.Next(boundary.Count);
                Vector2Int b = boundary[idx];
                boundary.RemoveAt(idx);

                for (int t = 0; t < _growMaxTries; t++) {
                    Vector2Int n = NeighborRandomPick(b, rng);

                    if (!room.ContainsFloor(n.x, n.y) &&
                        room.CountFloorNeighbors4(n) >= 2) {
                        if (room.TryAddFloor(n)) {
                            break;
                        }
                    }
                }
            }
        }

        private static Vector2Int NeighborRandomPick(Vector2Int c, System.Random rng) =>
            rng.Next(4) switch {
                0 => new Vector2Int(c.x + 1, c.y),
                1 => new Vector2Int(c.x - 1, c.y),
                2 => new Vector2Int(c.x, c.y + 1),
                _ => new Vector2Int(c.x, c.y - 1)
            };
    }
}
