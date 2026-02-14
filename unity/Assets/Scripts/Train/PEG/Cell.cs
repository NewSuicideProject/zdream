using System;

namespace Train.PEG {
    [Serializable]
    public struct Cell {
        public bool isWall; // logical wall
        public bool isBorder; // active wall for rendering
        public bool isRoad; // road floor marker

        public int roomId; // -1 = none
        public float height; // top height

        public Cell(bool isWall, float height = 0f, int roomId = -1) {
            this.isWall = isWall;
            isBorder = false;
            isRoad = false;
            this.roomId = roomId;
            this.height = height;
        }
    }
}
