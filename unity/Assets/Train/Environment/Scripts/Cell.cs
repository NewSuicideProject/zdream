using System;

[Serializable]
public struct Cell {
    // true = wall (logical)
    public bool IsWall;

    // true = physical/render wall (your BordorMatrix / ActiveWall 역할)
    public bool IsBordor;

    // -1 = none
    public int RoomId;

    // tile top height
    public float Height;

    public static Cell DefaultWall() => new() { IsWall = true, IsBordor = false, RoomId = -1, Height = 0f };
}
