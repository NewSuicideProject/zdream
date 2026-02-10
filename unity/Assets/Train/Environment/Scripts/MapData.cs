using System.Collections.Generic;
using UnityEngine;

public sealed class MapData {
    public int Width;
    public int Height;
    public float CellSize;
    public Vector3 Origin;

    public bool[,] WallMatrix; // true=wall, false=floor
    public int[,] RoomIdMatrix; // -1 if not room (road remains -1)
    public float[,] TileHeight; // per-tile world height (Y)
    public List<Room> Rooms;
}
