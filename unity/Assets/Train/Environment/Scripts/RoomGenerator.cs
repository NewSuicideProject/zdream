using UnityEngine;

namespace Train.Environment.Scripts {
    public class RoomGenerator {
        private readonly OrganicShaper _organicShaper;
        public RoomGenerator(OrganicShaper organicShaper) => _organicShaper = organicShaper;

        public void PlaceRooms(
            MapData map,
            System.Random rng,
            int roomCount,
            int maxRoomRerolls,
            float circleRoomChance,
            RectInt rectSizeRange, // RectInt: x=minW, y=minH, width=maxW, height=maxH
            int circleMinR,
            int circleMaxR,
            int roomPadding,
            int minRoomLevel,
            int maxRoomLevel
        ) {
            int tries = 0;
            int placed = 0;
            int maxTries = maxRoomRerolls * roomCount;

            while (placed < roomCount && tries < maxTries) {
                tries++;

                bool makeCircle = rng.NextDouble() < circleRoomChance;

                Room room = makeCircle
                    ? new Room(map, rng, placed, circleMinR, circleMaxR, minRoomLevel, maxRoomLevel)
                    : new Room(
                        map,
                        rng,
                        placed,
                        rectSizeRange.x, // minW
                        rectSizeRange.width, // maxW
                        rectSizeRange.y, // minH
                        rectSizeRange.height, // maxH
                        minRoomLevel,
                        maxRoomLevel
                    );

                if (!room.IsValid) {
                    continue;
                }

                if (!RoomFitsAndDoesntOverlap(map, room, roomPadding)) {
                    continue;
                }

                _organicShaper.ApplyLightOrganic(room, rng);

                map.Rooms.Add(room);
                placed++;
            }

            if (map.Rooms.Count < 2) {
                Debug.LogWarning(
                    $"[Environment] Only {map.Rooms.Count} rooms placed. Increase rerolls / reduce padding / increase grid size.");
            }
        }

        public void WriteRoomToGrid(MapData map, Room room, float levelStepHeight) {
            float roomHeight = Utility.LevelToHeight(room.heightLevel, levelStepHeight);

            foreach (Vector2Int c in room.FloorSet) {
                if (!map.InBounds(c.x, c.y)) {
                    continue;
                }

                ref Cell cell = ref map.Cells[c.y, c.x];
                cell.IsWall = false;
                cell.RoomId = room.id;
                cell.Height = roomHeight;
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
}
