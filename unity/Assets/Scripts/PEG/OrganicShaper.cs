using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace PEG {
    public class OrganicShaper {
        private readonly float _carveRatio;
        private readonly int _growMaxTries;
        private readonly float _growRatio;
        private readonly int _iterations;

        public OrganicShaper(int iterations, float carveRatio, float growRatio, int growMaxTries) {
            _iterations = Mathf.Max(0, iterations);
            _carveRatio = Mathf.Clamp(carveRatio, 0f, 0.25f);
            _growRatio = Mathf.Clamp(growRatio, 0f, 0.25f);
            _growMaxTries = Mathf.Max(1, growMaxTries);
        }

        public void Organic(Room room, Random rng) {
            if (_iterations <= 0 || room.Floors.Count == 0) {
                return;
            }

            for (int it = 0; it < _iterations; it++) {
                Carve(room, rng);
                Grow(room, rng);
            }
        }

        private void Carve(Room room, Random rng) {
            List<Vector2Int> boundary = room.GetBorderCells();
            int carveCount = Mathf.Clamp(Mathf.RoundToInt(room.Floors.Count * _carveRatio), 0, boundary.Count);

            for (int i = 0; i < carveCount && boundary.Count > 0; i++) {
                int idx = rng.Next(boundary.Count);
                Vector2Int c = boundary[idx];
                boundary.RemoveAt(idx);

                if (room.GetNeighborCount(c) < 2) {
                    continue;
                }

                if (room.bounds.Contains(c)) {
                    room.Floors.Add(c);
                }
            }
        }

        private void Grow(Room room, Random rng) {
            List<Vector2Int> boundary = room.GetBorderCells();
            int growCount = Mathf.Clamp(Mathf.RoundToInt(room.Floors.Count * _growRatio), 0, boundary.Count);

            for (int i = 0; i < growCount && boundary.Count > 0; i++) {
                int idx = rng.Next(boundary.Count);
                Vector2Int b = boundary[idx];
                boundary.RemoveAt(idx);

                for (int t = 0; t < _growMaxTries; t++) {
                    Vector2Int n = b + Utility.Cardinal[rng.Next(4)];

                    if (room.Floors.Contains(n) || room.GetNeighborCount(n) < 2 || !room.bounds.Contains(n)) {
                        continue;
                    }

                    if (room.Floors.Add(n)) {
                        break;
                    }
                }
            }
        }
    }
}
