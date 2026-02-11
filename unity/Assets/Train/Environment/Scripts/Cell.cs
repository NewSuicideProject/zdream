using System;

namespace Train.Environment.Scripts {
    [Serializable]
    public struct Cell {
        public bool IsWall; // logical wall
        public bool IsBordor; // active wall for rendering
        public int RoomId; // -1 = none
        public float Height; // top height

        // 기본 상태는 생성자에서 전부 정리
        public Cell(bool isWall, float height = 0f, int roomId = -1) {
            IsWall = isWall;
            IsBordor = false;
            RoomId = roomId;
            Height = height;
        }
    }
}
