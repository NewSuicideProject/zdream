using System;

namespace PEG {
    [Serializable]
    public struct Cell {
        public bool isWall;
        public bool isBorder;
        public bool isRoad;

        public int roomId;
        public float height;

        public Cell(bool isWall, float height = 0f, int roomId = -1) {
            this.isWall = isWall;
            isBorder = false;
            isRoad = false;
            this.roomId = roomId;
            this.height = height;
        }
    }
}
