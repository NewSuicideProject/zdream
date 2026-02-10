using System;
using UnityEngine;

public sealed class RoomGenerator {
    private readonly OrganicShaper _organicShaper;

    public RoomGenerator(OrganicShaper organicShaper) => _organicShaper = organicShaper;

    public void PlaceRooms(
        Environment.MapData map,
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
            Environment.Room room = makeCircle
                ? CreateCircleRoom(map, rng, placed, circleMinR, circleMaxR, minRoomLevel, maxRoomLevel)
                : CreateRectRoom(map, rng, placed, rectMinW, rectMaxW, rectMinH, rectMaxH, minRoomLevel, maxRoomLevel);

            if (room == null) {
                continue;
            }

            if (!RoomFitsAndDoesntOverlap(map, room, roomPadding)) {
                continue;
            }

            BuildRoomFloorSet(room);
            _organicShaper.ApplyLightOrganic(room, rng, organicCfg);

            map.Rooms.Add(room);
            placed++;
        }

        if (map.Rooms.Count < 2) {
            Debug.LogWarning(
                $"[Environment] Only {map.Rooms.Count} rooms placed. Increase rerolls / reduce padding / increase grid size.");
        }
    }

    public void WriteRoomsToGrid(Environment.MapData map, float levelStepHeight) {
        for (int i = 0; i < map.Rooms.Count; i++) {
            Environment.Room r = map.Rooms[i];
            float roomHeight = LevelToHeight(r.heightLevel, levelStepHeight);

            for (int k = 0; k < r.Cells.Count; k++) {
                Environment.Cell c = r.Cells[k];
                if (!InBounds(map, c)) {
                    continue;
                }

                map.WallMatrix[c.Y, c.X] = false;
                map.RoomIdMatrix[c.Y, c.X] = r.id;
                map.TileHeight[c.Y, c.X] = roomHeight;
            }
        }
    }

    private static bool InBounds(Environment.MapData map, Environment.Cell c)
        => c.X >= 0 && c.X < map.Width && c.Y >= 0 && c.Y < map.Height;

    private static RectInt ExpandRect(RectInt r, int pad) =>
        new(r.xMin - pad, r.yMin - pad, r.width + (pad * 2), r.height + (pad * 2));

    private static float LevelToHeight(int level, float levelStepHeight) => level * levelStepHeight;

    private static Environment.Room CreateRectRoom(
        Environment.MapData map,
        System.Random rng,
        int id,
        int rectMinW,
        int rectMaxW,
        int rectMinH,
        int rectMaxH,
        int minRoomLevel,
        int maxRoomLevel
    ) {
        int roomWidthCells = rng.Next(rectMinW, rectMaxW + 1);
        int roomHeightCells = rng.Next(rectMinH, rectMaxH + 1);

        const int border = 1;
        int minLeftX = border;
        int minBottomY = border;

        int maxLeftX = map.Width - roomWidthCells - border;
        int maxBottomY = map.Height - roomHeightCells - border;
        if (maxLeftX < minLeftX || maxBottomY < minBottomY) {
            return null;
        }

        int leftX = rng.Next(minLeftX, maxLeftX + 1);
        int bottomY = rng.Next(minBottomY, maxBottomY + 1);

        int centerX = leftX + (roomWidthCells / 2);
        int centerY = bottomY + (roomHeightCells / 2);

        Environment.Room room = new() {
            id = id,
            bounds = new RectInt(leftX, bottomY, roomWidthCells, roomHeightCells),
            center = new Vector2Int(centerX, centerY),
            heightLevel = rng.Next(minRoomLevel, maxRoomLevel + 1)
        };

        int rightXExclusive = leftX + roomWidthCells;
        int topYExclusive = bottomY + roomHeightCells;

        for (int y = bottomY; y < topYExclusive; y++)
        for (int x = leftX; x < rightXExclusive; x++) {
            room.Cells.Add(new Environment.Cell(x, y));
        }

        return room;
    }

    private static Environment.Room CreateCircleRoom(
        Environment.MapData map,
        System.Random rng,
        int id,
        int circleMinR,
        int circleMaxR,
        int minRoomLevel,
        int maxRoomLevel
    ) {
        int radiusCells = rng.Next(circleMinR, circleMaxR + 1);
        int diameterCells = (radiusCells * 2) + 1;

        const int border = 1;
        int minCenterX = border + radiusCells;
        int minCenterY = border + radiusCells;

        int maxCenterX = map.Width - border - radiusCells - 1;
        int maxCenterY = map.Height - border - radiusCells - 1;
        if (maxCenterX < minCenterX || maxCenterY < minCenterY) {
            return null;
        }

        int centerX = rng.Next(minCenterX, maxCenterX + 1);
        int centerY = rng.Next(minCenterY, maxCenterY + 1);

        int boundsLeftX = centerX - radiusCells;
        int boundsBottomY = centerY - radiusCells;

        Environment.Room room = new() {
            id = id,
            bounds = new RectInt(boundsLeftX, boundsBottomY, diameterCells, diameterCells),
            center = new Vector2Int(centerX, centerY),
            heightLevel = rng.Next(minRoomLevel, maxRoomLevel + 1)
        };

        int radiusSq = radiusCells * radiusCells;

        for (int y = centerY - radiusCells; y <= centerY + radiusCells; y++)
        for (int x = centerX - radiusCells; x <= centerX + radiusCells; x++) {
            int offsetX = x - centerX;
            int offsetY = y - centerY;

            if ((offsetX * offsetX) + (offsetY * offsetY) <= radiusSq) {
                room.Cells.Add(new Environment.Cell(x, y));
            }
        }

        return room;
    }

    private static bool RoomFitsAndDoesntOverlap(Environment.MapData map, Environment.Room room, int roomPadding) {
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

    private static void BuildRoomFloorSet(Environment.Room room) {
        room.FloorSet.Clear();
        for (int i = 0; i < room.Cells.Count; i++) {
            Environment.Cell c = room.Cells[i];
            room.FloorSet.Add(Utility.Pack(c.X, c.Y));
        }
    }
}
