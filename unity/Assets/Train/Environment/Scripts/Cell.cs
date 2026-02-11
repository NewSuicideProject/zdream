using System;

namespace Train.Environment.Scripts {
    [Serializable]
    public struct Cell {
        public bool IsWall; // logical wall
        public bool IsBordor; // active wall for rendering
        public bool IsRoad; // road floor marker

        public int RoomId; // -1 = none
        public float Height; // top height

        public Cell(bool isWall, float height = 0f, int roomId = -1) {
            IsWall = isWall;
            IsBordor = false;
            IsRoad = false;
            RoomId = roomId;
            Height = height;
        }
    }
}
