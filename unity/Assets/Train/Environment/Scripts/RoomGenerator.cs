using UnityEngine;

public sealed class RoomGenerator {
    private readonly OrganicShaper _organicShaper;
    public RoomGenerator(OrganicShaper organicShaper) => _organicShaper = organicShaper;

    public void PlaceRooms(
        MapData map,
        System.Random rng,
        int roomCount,
        int maxRoomRerolls,
        float circleRoomChance,
        int rectMinW,
        int rectMaxW,
        int rectMinH,
        int rectMaxH,
        int circleMinR,
        int circleMaxR,
        int roomPadding,
        int minRoomLevel,
        int maxRoomLevel,
        OrganicShaper.Config organicCfg
    ) {
        int tries = 0;
        int placed = 0;
        int maxTries = maxRoomRerolls * roomCount;

        while (placed < roomCount && tries < maxTries) {
            tries++;

            bool makeCircle = rng.NextDouble() < circleRoomChance;

            Room room = makeCircle
                ? new Room(map, rng, placed, circleMinR, circleMaxR, minRoomLevel, maxRoomLevel)
                : new Room(map, rng, placed, rectMinW, rectMaxW, rectMinH, rectMaxH, minRoomLevel, maxRoomLevel);

            if (!room.IsValid) {
                continue;
            }

            if (!RoomFitsAndDoesntOverlap(map, room, roomPadding)) {
                continue;
            }

            room.RebuildFloorSetFromCells();
            _organicShaper.ApplyLightOrganic(room, rng, organicCfg);

            map.Rooms.Add(room);
            placed++;
        }

        if (map.Rooms.Count < 2) {
            Debug.LogWarning(
                $"[Environment] Only {map.Rooms.Count} rooms placed. Increase rerolls / reduce padding / increase grid size.");
        }
    }

    public void WriteRoomsToGrid(MapData map, float levelStepHeight) {
        for (int i = 0; i < map.Rooms.Count; i++) {
            Room r = map.Rooms[i];
            float roomHeight = Utility.LevelToHeight(r.heightLevel, levelStepHeight);

            for (int k = 0; k < r.Cells.Count; k++) {
                Vector2Int c = r.Cells[k];
                if (!Utility.InBounds(map, c.x, c.y)) {
                    continue;
                }

                ref Cell cell = ref map.Cells[c.y, c.x];
                cell.IsWall = false;
                cell.RoomId = r.id;
                cell.Height = roomHeight;
            }
        }
    }

    private static RectInt ExpandRect(RectInt r, int pad)
        => new(r.xMin - pad, r.yMin - pad, r.width + (pad * 2), r.height + (pad * 2));

    private static bool RoomFitsAndDoesntOverlap(MapData map, Room room, int roomPadding) {
        RectInt expanded = ExpandRect(room.bounds, roomPadding);

        if (expanded.xMin < 0 || expanded.yMin < 0 || expanded.xMax > map.Width || expanded.yMax > map.Height) {
            return false;
        }

        for (int i = 0; i < map.Rooms.Count; i++) {
            RectInt otherExpanded = ExpandRect(map.Rooms[i].bounds, roomPadding);
            if (expanded.Overlaps(otherExpanded)) {
                return false;
            }
        }

        return true;
    }
}
