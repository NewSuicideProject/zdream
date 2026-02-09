using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OrganGen: applies "light organic" carving/growing INSIDE room bounds only.
/// No wallMatrix access; purely mutates Room (cells + floorSet).
/// </summary>
public sealed class RoomMstOrganGen : MonoBehaviour {
    [Header("Light Organic (bounded inside room bounds)")] [Range(0, 3)] [SerializeField]
    private int organicIterations = 1;

    [Range(0f, 0.25f)] [SerializeField] private float organicCarveRatio = 0.06f;
    [Range(0f, 0.25f)] [SerializeField] private float organicGrowRatio = 0.05f;
    [Min(1)] [SerializeField] private int organicGrowMaxTriesPerCell = 6;

    private static bool InRect(RectInt r, int x, int y) => x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax;

    public void ApplyLightOrganic(RoomMstMainGen.Room room, System.Random rng) {
        if (room == null) {
            return;
        }

        if (organicIterations <= 0) {
            return;
        }

        for (int it = 0; it < organicIterations; it++) {
            // 1) carve boundary cells
            List<RoomMstMainGen.Cell> boundary = CollectBoundaryCells(room.floorSet, room.bounds);
            int carveCount = Mathf.Clamp(Mathf.RoundToInt(room.floorSet.Count * organicCarveRatio), 0, boundary.Count);

            for (int k = 0; k < carveCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                RoomMstMainGen.Cell c = boundary[idx];
                boundary.RemoveAt(idx);

                if (CountFloorNeighbors(room.floorSet, c.x, c.y) >= 2) {
                    room.floorSet.Remove(RoomMstMainGen.Pack(c.x, c.y));
                }
            }

            // 2) grow into neighbors (still inside bounds)
            boundary = CollectBoundaryCells(room.floorSet, room.bounds);
            int growCount = Mathf.Clamp(Mathf.RoundToInt(room.floorSet.Count * organicGrowRatio), 0, boundary.Count);

            for (int k = 0; k < growCount && boundary.Count > 0; k++) {
                int idx = rng.Next(boundary.Count);
                RoomMstMainGen.Cell b = boundary[idx];
                boundary.RemoveAt(idx);

                for (int t = 0; t < organicGrowMaxTriesPerCell; t++) {
                    RoomMstMainGen.Cell n = PickRandom4Neighbor(b.x, b.y, rng);
                    if (!InRect(room.bounds, n.x, n.y)) {
                        continue;
                    }

                    int p = RoomMstMainGen.Pack(n.x, n.y);
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
            RoomMstMainGen.Unpack(p, out int x, out int y);
            room.cells.Add(new RoomMstMainGen.Cell(x, y));
        }
    }

    private List<RoomMstMainGen.Cell> CollectBoundaryCells(HashSet<int> floorSet, RectInt bounds) {
        List<RoomMstMainGen.Cell> result = new();

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++) {
            int p = RoomMstMainGen.Pack(x, y);
            if (!floorSet.Contains(p)) {
                continue;
            }

            if (!IsFloor(floorSet, bounds, x + 1, y) ||
                !IsFloor(floorSet, bounds, x - 1, y) ||
                !IsFloor(floorSet, bounds, x, y + 1) ||
                !IsFloor(floorSet, bounds, x, y - 1)) {
                result.Add(new RoomMstMainGen.Cell(x, y));
            }
        }

        return result;
    }

    private bool IsFloor(HashSet<int> set, RectInt bounds, int x, int y) {
        if (!InRect(bounds, x, y)) {
            return false;
        }

        return set.Contains(RoomMstMainGen.Pack(x, y));
    }

    private int CountFloorNeighbors(HashSet<int> set, int x, int y) {
        int n = 0;
        if (set.Contains(RoomMstMainGen.Pack(x + 1, y))) {
            n++;
        }

        if (set.Contains(RoomMstMainGen.Pack(x - 1, y))) {
            n++;
        }

        if (set.Contains(RoomMstMainGen.Pack(x, y + 1))) {
            n++;
        }

        if (set.Contains(RoomMstMainGen.Pack(x, y - 1))) {
            n++;
        }

        return n;
    }

    private RoomMstMainGen.Cell PickRandom4Neighbor(int x, int y, System.Random rng) {
        int r = rng.Next(4);
        return r switch {
            0 => new RoomMstMainGen.Cell(x + 1, y),
            1 => new RoomMstMainGen.Cell(x - 1, y),
            2 => new RoomMstMainGen.Cell(x, y + 1),
            _ => new RoomMstMainGen.Cell(x, y - 1)
        };
    }
}
