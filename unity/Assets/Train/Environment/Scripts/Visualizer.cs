using System.Collections.Generic;
using UnityEngine;

public sealed class Visualizer {
    private readonly Transform wallParent;
    private readonly GameObject wallPrefab;
    private readonly float wallCenterY;

    private readonly List<GameObject> spawnedWalls = new();

    public Visualizer(Transform wallParent, GameObject wallPrefab, float wallCenterY) {
        this.wallParent = wallParent;
        this.wallPrefab = wallPrefab;
        this.wallCenterY = wallCenterY;
    }

    public void RebuildWalls(Environment.MapData data) {
        if (wallPrefab == null) {
            Debug.LogError("[Visualizer] wallPrefab is null.");
            return;
        }

        ClearSpawnedWalls();

        bool[,] wallMatrix = data.wallMatrix;
        for (int y = 0; y < data.height; y++)
        for (int x = 0; x < data.width; x++) {
            if (!wallMatrix[y, x]) {
                continue;
            }

            Vector3 pos = CellToWorld(data, x, y);
            GameObject w = Object.Instantiate(wallPrefab, pos, Quaternion.identity, wallParent);
            spawnedWalls.Add(w);
        }
    }

    public void PlaceSpawnMarkers(Environment.MapData data, Transform zombieMarker, Transform targetMarker) {
        if (data.rooms == null || data.rooms.Count < 2) {
            Debug.LogWarning("[Visualizer] Not enough rooms for spawn/target.");
            return;
        }

        // farthest pair by center distance
        int a = 0, b = 1;
        long best = -1;

        for (int i = 0; i < data.rooms.Count; i++)
        for (int j = i + 1; j < data.rooms.Count; j++) {
            Vector2Int c1 = data.rooms[i].center;
            Vector2Int c2 = data.rooms[j].center;

            long dx = c1.x - c2.x;
            long dy = c1.y - c2.y;
            long d2 = (dx * dx) + (dy * dy);

            if (d2 > best) {
                best = d2;
                a = i;
                b = j;
            }
        }

        Vector3 zombiePos = CellToWorld(data, data.rooms[a].center.x, data.rooms[a].center.y);
        Vector3 targetPos = CellToWorld(data, data.rooms[b].center.x, data.rooms[b].center.y);

        if (zombieMarker != null) {
            zombieMarker.position = new Vector3(zombiePos.x, 0f, zombiePos.z);
        }

        if (targetMarker != null) {
            targetMarker.position = new Vector3(targetPos.x, 0f, targetPos.z);
        }
    }

    private Vector3 CellToWorld(Environment.MapData data, int x, int y) {
        float wx = data.origin.x + ((x + 0.5f) * data.cellSize);
        float wz = data.origin.z + ((y + 0.5f) * data.cellSize);
        return new Vector3(wx, wallCenterY, wz);
    }

    private void ClearSpawnedWalls() {
        for (int i = 0; i < spawnedWalls.Count; i++) {
            if (spawnedWalls[i] != null) {
                Object.Destroy(spawnedWalls[i]);
            }
        }

        spawnedWalls.Clear();
    }
}
