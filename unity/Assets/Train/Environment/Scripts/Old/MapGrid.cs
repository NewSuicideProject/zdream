using UnityEngine;

public sealed class MapGrid {
    private readonly Transform _basePart;
    private readonly int _width;
    private readonly int _height;
    private readonly float _cellSize;

    private readonly float _originX;
    private readonly float _originZ;

    public int Width => _width;
    public int Height => _height;
    public float CellSize => _cellSize;

    public MapGrid(Transform basePart, int width, int height, float cellSize) {
        _basePart = basePart;
        _width = width;
        _height = height;
        _cellSize = cellSize;

        (_originX, _originZ) = ComputeOrigin(basePart, width, height, cellSize);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell, float yWorld) {
        // Converts a grid cell to world-space center position.
        float wx = _originX + (cell.x * _cellSize);
        float wz = _originZ + (cell.y * _cellSize);
        return new Vector3(wx, yWorld, wz);
    }

    private static (float originX, float originZ) ComputeOrigin(Transform basePart, int width, int height,
        float cellSize) {
        // Computes grid origin based on basePart center and grid dimensions.
        float originX = basePart.position.x - (width * cellSize * 0.5f) + (cellSize * 0.5f);
        float originZ = basePart.position.z - (height * cellSize * 0.5f) + (cellSize * 0.5f);
        return (originX, originZ);
    }
}

public static class MapGridUtil {
    public static float GetTopSurfaceY(Transform t) {
        // Returns top surface Y using collider/renderer bounds fallback.
        Collider c = t.GetComponentInChildren<Collider>();
        if (c != null) {
            return c.bounds.max.y;
        }

        Renderer r = t.GetComponentInChildren<Renderer>();
        if (r != null) {
            return r.bounds.max.y;
        }

        return t.position.y;
    }
}
