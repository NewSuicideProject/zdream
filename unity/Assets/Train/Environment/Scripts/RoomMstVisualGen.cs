using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VisualGen: spawns walls from wallMatrix and visualizes spawn points (markers + debug capsules).
/// No generation logic here.
/// </summary>
public sealed class RoomMstVisualGen : MonoBehaviour {
    // =========================================================
    // Scene
    // =========================================================
    [Header("Parents")] [SerializeField] private Transform wallParent;
    [SerializeField] private Transform debugParent;

    [Header("Wall Prefab")] [SerializeField]
    private GameObject wallPrefab;

    [SerializeField] private float wallCenterY = 2.5f;

    [Header("Spawn markers (existing EmptyObjects)")] [SerializeField]
    private Transform zombieSpawnMarker;

    [SerializeField] private Transform targetSpawnMarker;

    [Header("Debug Capsules (NO Shader.Find)")] [SerializeField]
    private bool spawnDebugCapsules = true;

    [SerializeField] private float capsuleHeight = 2f;
    [SerializeField] private float capsuleRadius = 0.5f;
    [SerializeField] private float capsuleLift = 1.2f;
    [SerializeField] private Material zombieSpawnDebugMaterial;
    [SerializeField] private Material targetSpawnDebugMaterial;

    private readonly List<GameObject> spawnedWalls = new();
    private readonly List<GameObject> spawnedDebug = new();

    private void Awake() {
        if (wallParent == null) {
            wallParent = transform;
        }

        if (debugParent == null) {
            debugParent = transform;
        }
    }

    public void RebuildWalls(RoomMstMainGen.MapData data) {
        if (wallPrefab == null) {
            Debug.LogError("[VisualGen] wallPrefab is null.");
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
            GameObject w = Instantiate(wallPrefab, pos, Quaternion.identity, wallParent);
            spawnedWalls.Add(w);
        }
    }

    public void PickSpawnRoomsAndPlaceMarkers(RoomMstMainGen.MapData data) {
        ClearSpawnedDebug();

        if (data.rooms == null || data.rooms.Count < 2) {
            Debug.LogWarning("[VisualGen] Not enough rooms for spawn/target.");
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

        if (zombieSpawnMarker != null) {
            zombieSpawnMarker.position = new Vector3(zombiePos.x, 0f, zombiePos.z);
        }

        if (targetSpawnMarker != null) {
            targetSpawnMarker.position = new Vector3(targetPos.x, 0f, targetPos.z);
        }

        if (spawnDebugCapsules) {
            SpawnDebugCapsule(zombiePos, capsuleLift, capsuleHeight, capsuleRadius, zombieSpawnDebugMaterial);
            SpawnDebugCapsule(targetPos, capsuleLift, capsuleHeight, capsuleRadius, targetSpawnDebugMaterial);
        }
    }

    // =========================================================
    // Helpers
    // =========================================================
    private Vector3 CellToWorld(RoomMstMainGen.MapData data, int x, int y) {
        float wx = data.origin.x + ((x + 0.5f) * data.cellSize);
        float wz = data.origin.z + ((y + 0.5f) * data.cellSize);
        return new Vector3(wx, wallCenterY, wz);
    }

    private void SpawnDebugCapsule(Vector3 basePos, float lift, float height, float radius, Material mat) {
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cap.name = "SpawnDebugCapsule";
        cap.transform.SetParent(debugParent, true);

        const float defaultHeight = 2f;
        const float defaultRadius = 0.5f;

        float yScale = Mathf.Max(0.01f, height / defaultHeight);
        float xzScale = Mathf.Max(0.01f, radius / defaultRadius);

        cap.transform.localScale = new Vector3(xzScale, yScale, xzScale);
        cap.transform.position = new Vector3(basePos.x, lift, basePos.z);

        Collider col = cap.GetComponent<Collider>();
        if (col != null) {
            Destroy(col);
        }

        if (mat != null) {
            Renderer r = cap.GetComponent<Renderer>();
            if (r != null) {
                r.sharedMaterial = mat;
            }
        } else {
            Debug.LogWarning("[VisualGen] Debug capsule material is null (assign in Inspector).");
        }

        spawnedDebug.Add(cap);
    }

    private void ClearSpawnedWalls() {
        for (int i = 0; i < spawnedWalls.Count; i++) {
            if (spawnedWalls[i] != null) {
                Destroy(spawnedWalls[i]);
            }
        }

        spawnedWalls.Clear();
    }

    private void ClearSpawnedDebug() {
        for (int i = 0; i < spawnedDebug.Count; i++) {
            if (spawnedDebug[i] != null) {
                Destroy(spawnedDebug[i]);
            }
        }

        spawnedDebug.Clear();
    }
}
