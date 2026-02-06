using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public static class DoorFenceLocator {
    public readonly struct DoorLink : IEquatable<DoorLink> {
        public readonly int roomA;
        public readonly int roomB;
        public readonly Vector2Int cellA;
        public readonly Vector2Int cellB;

        public DoorLink(int roomA, int roomB, Vector2Int cellA, Vector2Int cellB) {
            if (roomA <= roomB) {
                this.roomA = roomA;
                this.roomB = roomB;
                this.cellA = cellA;
                this.cellB = cellB;
            } else {
                this.roomA = roomB;
                this.roomB = roomA;
                this.cellA = cellB;
                this.cellB = cellA;
            }
        }

        public bool Equals(DoorLink other) =>
            roomA == other.roomA
            && roomB == other.roomB
            && cellA == other.cellA
            && cellB == other.cellB;

        public override bool Equals(object obj) => obj is DoorLink other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                int h = 17;
                h = (h * 31) + roomA;
                h = (h * 31) + roomB;
                h = (h * 31) + cellA.GetHashCode();
                h = (h * 31) + cellB.GetHashCode();
                return h;
            }
        }

        public override string ToString() => $"DoorLink Rooms({roomA},{roomB}) {cellA}->{cellB}";
    }

    public sealed class Result {
        public int[,] roomId; // roomId[y,x], -1 for walls
        public int roomCount;
        public List<DoorLink> doorLinks;
        public List<DoorLink> selectedDoorLinks;
    }

    public enum DoorPickMode {
        MidpointOfContacts,
        RandomPerPair,
        ThroatHeuristic
    }

    public static Result ComputeDoorLinks(
        bool[,] walls,
        int width,
        int height,
        bool pickOnePerPair = true,
        DoorPickMode pickMode = DoorPickMode.MidpointOfContacts,
        int randomSeed = 12345) {
        if (walls == null) {
            throw new ArgumentNullException(nameof(walls));
        }

        if (walls.GetLength(0) != height || walls.GetLength(1) != width) {
            throw new ArgumentException("walls[,] shape must be [height,width] with walls[y,x].");
        }

        Result res = new() {
            roomId = new int[height, width],
            doorLinks = new List<DoorLink>(2048),
            selectedDoorLinks = new List<DoorLink>(512)
        };

        LabelRooms(walls, width, height, res.roomId, out res.roomCount);
        FindAdjacencyContacts(res.roomId, walls, width, height, res.doorLinks);

        if (pickOnePerPair) {
            Random rng = new(randomSeed);
            PickOnePerPair(res.roomId, width, height, res.doorLinks, res.selectedDoorLinks, pickMode, rng);
        } else {
            res.selectedDoorLinks.AddRange(res.doorLinks);
        }

        return res;
    }

    // =========================================================
    // Room labeling (flood fill)
    // =========================================================

    private static void LabelRooms(bool[,] walls, int width, int height, int[,] roomId, out int roomCount) {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) {
            roomId[y, x] = -1;
        }

        roomCount = 0;
        Queue<Vector2Int> q = new(1024);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) {
            if (walls[y, x]) {
                continue;
            }

            if (roomId[y, x] != -1) {
                continue;
            }

            int id = roomCount++;
            roomId[y, x] = id;
            q.Enqueue(new Vector2Int(x, y));

            while (q.Count > 0) {
                Vector2Int c = q.Dequeue();
                TryPush(c.x + 1, c.y);
                TryPush(c.x - 1, c.y);
                TryPush(c.x, c.y + 1);
                TryPush(c.x, c.y - 1);
            }

            void TryPush(int nx, int ny) {
                if ((uint)nx >= (uint)width || (uint)ny >= (uint)height) {
                    return;
                }

                if (walls[ny, nx]) {
                    return;
                }

                if (roomId[ny, nx] != -1) {
                    return;
                }

                roomId[ny, nx] = id;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    // =========================================================
    // Adjacency contacts (door candidates)
    // =========================================================

    private static void FindAdjacencyContacts(int[,] roomId, bool[,] walls, int width, int height,
        List<DoorLink> outLinks) {
        outLinks.Clear();

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) {
            if (walls[y, x]) {
                continue;
            }

            int ra = roomId[y, x];
            if (ra < 0) {
                continue;
            }

            if (x + 1 < width && !walls[y, x + 1]) {
                int rb = roomId[y, x + 1];
                if (rb >= 0 && rb != ra) {
                    outLinks.Add(new DoorLink(ra, rb, new Vector2Int(x, y), new Vector2Int(x + 1, y)));
                }
            }

            if (y + 1 < height && !walls[y + 1, x]) {
                int rb = roomId[y + 1, x];
                if (rb >= 0 && rb != ra) {
                    outLinks.Add(new DoorLink(ra, rb, new Vector2Int(x, y), new Vector2Int(x, y + 1)));
                }
            }
        }
    }

    // =========================================================
    // Thinning: pick one doorway per (roomA, roomB)
    // =========================================================

    private static void PickOnePerPair(
        int[,] roomId,
        int width,
        int height,
        List<DoorLink> all,
        List<DoorLink> selected,
        DoorPickMode mode,
        System.Random rng) {
        selected.Clear();

        Dictionary<(int a, int b), List<DoorLink>> groups = new(512);

        for (int i = 0; i < all.Count; i++) {
            DoorLink d = all[i];
            (int roomA, int roomB) key = (d.roomA, d.roomB);
            if (!groups.TryGetValue(key, out List<DoorLink> list)) {
                list = new List<DoorLink>(16);
                groups.Add(key, list);
            }

            list.Add(d);
        }

        foreach (KeyValuePair<(int a, int b), List<DoorLink>> kv in groups) {
            List<DoorLink> list = kv.Value;
            if (list.Count == 0) {
                continue;
            }

            DoorLink pick = list[0];

            switch (mode) {
                case DoorPickMode.RandomPerPair:
                    pick = list[rng.Next(list.Count)];
                    break;

                case DoorPickMode.MidpointOfContacts:
                    pick = PickByContactCentroid(list);
                    break;

                case DoorPickMode.ThroatHeuristic:
                    pick = PickByThroatHeuristic(roomId, width, height, list);
                    break;
            }

            selected.Add(pick);
        }
    }

    // ✅ FIXED: Vector2Int * float -> cast to Vector2
    private static DoorLink PickByContactCentroid(List<DoorLink> list) {
        Vector2 sum = Vector2.zero;

        for (int i = 0; i < list.Count; i++) {
            Vector2 a = (Vector2)list[i].cellA;
            Vector2 b = (Vector2)list[i].cellB;
            Vector2 m = (a + b) * 0.5f;
            sum += m;
        }

        Vector2 centroid = sum / Mathf.Max(1, list.Count);

        float best = float.MaxValue;
        DoorLink pick = list[0];

        for (int i = 0; i < list.Count; i++) {
            Vector2 a = (Vector2)list[i].cellA;
            Vector2 b = (Vector2)list[i].cellB;
            Vector2 m = (a + b) * 0.5f;

            float d = (m - centroid).sqrMagnitude;
            if (d < best) {
                best = d;
                pick = list[i];
            }
        }

        return pick;
    }

    private static DoorLink PickByThroatHeuristic(int[,] roomId, int width, int height, List<DoorLink> list) {
        int bestScore = int.MinValue;
        DoorLink pick = list[0];

        for (int i = 0; i < list.Count; i++) {
            DoorLink d = list[i];
            int s = SurroundingWallScore(roomId, width, height, d.cellA)
                    + SurroundingWallScore(roomId, width, height, d.cellB);

            if (s > bestScore) {
                bestScore = s;
                pick = d;
            }
        }

        return pick;
    }

    private static int SurroundingWallScore(int[,] roomId, int width, int height, Vector2Int c) {
        int score = 0;
        score += IsWallOrOut(roomId, width, height, c.x + 1, c.y) ? 1 : 0;
        score += IsWallOrOut(roomId, width, height, c.x - 1, c.y) ? 1 : 0;
        score += IsWallOrOut(roomId, width, height, c.x, c.y + 1) ? 1 : 0;
        score += IsWallOrOut(roomId, width, height, c.x, c.y - 1) ? 1 : 0;
        return score;
    }

    private static bool IsWallOrOut(int[,] roomId, int width, int height, int x, int y) {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) {
            return true;
        }

        return roomId[y, x] < 0;
    }
}
