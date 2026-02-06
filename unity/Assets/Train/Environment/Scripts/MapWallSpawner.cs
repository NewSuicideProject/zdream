using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapWallSpawner : MonoBehaviour {
    [Header("Scene refs")] [SerializeField]
    private Transform basePart;

    [SerializeField] private Transform wallParent;

    [Header("Spawn marker refs (existing EmptyObjects)")] [SerializeField]
    private Transform zombieSpawnMarker;

    [SerializeField] private Transform targetSpawnMarker;

    [Header("Spawn debug visuals")] [SerializeField]
    private bool spawnDebugCapsules = true;

    [SerializeField] private float capsuleHeight = 2.0f;
    [SerializeField] private float capsuleRadius = 0.5f;
    [SerializeField] private float capsuleYOffset = 0.05f;

    [Header("Spawn debug materials (NO Shader.Find)")] [SerializeField]
    private Material zombieSpawnDebugMaterial;

    [SerializeField] private Material targetSpawnDebugMaterial;
    [SerializeField] private bool tintWithPropertyBlock = true;

    [Header("Grid")] [SerializeField] private int width = 256;
    [SerializeField] private int height = 256;
    [SerializeField] private float cellSize = 1f;

    [Header("Wall object")] [SerializeField]
    private Vector3 wallSize = new(1f, 5f, 1f);

    [Header("Generation - WFC stub params")] [Range(0f, 1f)] [SerializeField]
    private float initialWallFill = 0.38f;

    [Header("Generation - Connectivity params")] [SerializeField]
    private int minSpawnManhattanDistance = 80;

    [Range(0f, 1f)] [SerializeField] private float loopDoorRatio = 0.25f;

    [Header("Random")] [SerializeField] private int seed = 0;
    [SerializeField] private bool useRandomSeed = true;

    // =========================================================
    // Fence container
    // =========================================================
    [Header("Fence Container")]
    [Tooltip("If null, a child object named 'FenceContainer' will be created under wallParent/this.")]
    [SerializeField]
    private Transform fenceParent;

    [SerializeField] private string fenceContainerName = "FenceContainer";

    // =========================================================
    // Fence settings
    // =========================================================
    [Header("Fence - Door/Corridor Links")] [SerializeField]
    private bool spawnFences = true;

    [SerializeField] private bool fencesEnabled = true;

    [Tooltip("Optional prefab. If null, uses Cube primitive.")] [SerializeField]
    private GameObject fencePrefab;

    [SerializeField] private float fenceHeight = 0.8f;
    [SerializeField] private float fenceThickness = 0.12f;
    [SerializeField] private float fenceYOffset = 0.02f;

    [Tooltip("Pick 1 doorway per adjacent-room pair. (May produce 0 if map is 1 connected component.)")]
    [SerializeField]
    private bool pickOneDoorPerPair = true;

    [SerializeField] private DoorFenceLocator.DoorPickMode doorPickMode = DoorFenceLocator.DoorPickMode.ThroatHeuristic;

    [Tooltip("Spawn up to N fences (0이면 제한 없음).")] [SerializeField]
    private int maxFenceCount = 0;

    [Tooltip("If true, use deterministic random seed for selection.")] [SerializeField]
    private bool deterministicFenceSeed = true;

    [SerializeField] private int fenceSeed = 12345;

    [Header("Fence Fallback - Throat Detector")]
    [Tooltip("If DoorLinks result is empty, use throat detector to place fences in narrow corridors.")]
    [SerializeField]
    private bool enableThroatFallback = true;

    [Tooltip("Minimum spacing between fences in cells (0 = no spacing).")] [SerializeField]
    private int minFenceSpacingCells = 6;

    [Tooltip("Fence length factor relative to cell size (0.8~1.2).")] [SerializeField]
    private float fenceLengthFactor = 1.0f;

    private MapGrid _grid;
    private MaterialPropertyBlock _mpb;

    private readonly List<GameObject> _spawnedFences = new();

    private void Start() {
        if (basePart == null) {
            Debug.LogError("[MapWallSpawner] BasePart not assigned.");
            return;
        }

        _mpb ??= new MaterialPropertyBlock();

        Transform parent = wallParent != null ? wallParent : transform;

        fenceParent = EnsureFenceContainer(parent);

        ClearSpawnedChildren(parent);
        ClearFenceContainerOnly(fenceParent);

        _grid = new MapGrid(basePart, width, height, cellSize);

        System.Random rng = CreateRng();

        MapGenerationConfig genCfg = new(
            width,
            height,
            initialWallFill,
            minSpawnManhattanDistance,
            loopDoorRatio
        );

        bool[,] walls =
            MapGeneration.GenerateWallMatrix(genCfg, rng, out Vector2Int zombieCell, out Vector2Int targetCell);

        ApplySpawnMarkersAndDebug(parent, zombieCell, targetCell);
        SpawnWalls(parent, walls);

        if (spawnFences) {
            SpawnFences(parent, walls, rng);
            ApplyFencesEnabledState();
        } else {
            if (fenceParent != null) {
                fenceParent.gameObject.SetActive(false);
            }
        }
    }

    // =========================================================
    // Fence pipeline with fallback
    // =========================================================
    private void SpawnFences(Transform parent, bool[,] walls, System.Random rng) {
        _spawnedFences.Clear();

        // 1) Try "door links between rooms"
        List<DoorFenceLocator.DoorLink> links = ComputeDoorLinksOrEmpty(walls);

        if (links.Count > 0) {
            int count = links.Count;
            if (maxFenceCount > 0) {
                count = Mathf.Min(count, maxFenceCount);
            }

            for (int i = 0; i < count; i++) {
                // doorLink is adjacent cells; that's not ideal for "blocking" but keep as-is for now.
                CreateFenceSegmentBetweenCellCenters(fenceParent, links[i].cellA, links[i].cellB);
            }

            return;
        }

        // 2) Fallback: throat detector inside a single connected component map
        if (enableThroatFallback) {
            SpawnFences_ByThroatFallback(fenceParent, walls, rng);
        }
    }

    private List<DoorFenceLocator.DoorLink> ComputeDoorLinksOrEmpty(bool[,] walls) {
        int s = deterministicFenceSeed ? fenceSeed : Environment.TickCount;

        DoorFenceLocator.Result res = DoorFenceLocator.ComputeDoorLinks(
            walls,
            width,
            height,
            pickOneDoorPerPair,
            doorPickMode,
            s
        );

        return res.selectedDoorLinks ?? new List<DoorFenceLocator.DoorLink>(0);
    }

    /// <summary>
    /// Finds corridor "throats" and places fences perpendicular to travel direction.
    /// Works even when the whole map is one connected room.
    /// </summary>
    private void SpawnFences_ByThroatFallback(Transform fenceContainer, bool[,] walls, System.Random rng) {
        float topY = MapGridUtil.GetTopSurfaceY(basePart);

        // collect candidates
        List<(Vector2Int cell, bool nsCorridor)> candidates = new(2048);

        for (int y = 1; y < height - 1; y++) {
            for (int x = 1; x < width - 1; x++) {
                if (walls[y, x]) {
                    continue; // only walkable
                }

                // neighbors
                bool n = !walls[y + 1, x];
                bool s = !walls[y - 1, x];
                bool e = !walls[y, x + 1];
                bool w = !walls[y, x - 1];

                // "1-cell wide corridor throat" patterns
                bool nsCorridor = n && s && !e && !w; // open north/south, walls east/west
                bool ewCorridor = e && w && !n && !s; // open east/west, walls north/south

                if (nsCorridor) {
                    candidates.Add((new Vector2Int(x, y), true));
                } else if (ewCorridor) {
                    candidates.Add((new Vector2Int(x, y), false));
                }
            }
        }

        if (candidates.Count == 0) {
            Debug.LogWarning("[MapWallSpawner] No throat candidates found. (Map may have wide corridors.)");
            return;
        }

        // shuffle for variety
        ShuffleInPlace(candidates, rng);

        // spacing control in cell units
        int spacing = Mathf.Max(0, minFenceSpacingCells);
        List<Vector2Int> picked = new(128);

        int spawned = 0;
        int limit = maxFenceCount > 0 ? maxFenceCount : int.MaxValue;

        float halfLen = cellSize * fenceLengthFactor * 0.5f;

        for (int i = 0; i < candidates.Count && spawned < limit; i++) {
            (Vector2Int c, bool ns) = candidates[i];

            if (spacing > 0 && TooCloseToPicked(c, picked, spacing)) {
                continue;
            }

            // world center of the throat cell
            Vector3 center = _grid.CellToWorldCenter(c, topY);

            // Build endpoints perpendicular to corridor direction
            // - ns corridor => fence along east-west (Vector3.right)
            // - ew corridor => fence along north-south (Vector3.forward)
            Vector3 axis = ns ? Vector3.right : Vector3.forward;

            Vector3 a = center - (axis * halfLen);
            Vector3 b = center + (axis * halfLen);

            CreateFenceSegmentWorld(fenceContainer, a, b);

            picked.Add(c);
            spawned++;
        }
    }

    private static bool TooCloseToPicked(Vector2Int c, List<Vector2Int> picked, int spacing) {
        int ss = spacing * spacing;
        for (int i = 0; i < picked.Count; i++) {
            Vector2Int d = picked[i] - c;
            if (d.sqrMagnitude <= ss) {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // Fence creation helpers
    // =========================================================

    /// <summary>
    /// Creates fence using world endpoints (this matches your "x1,y1-x2,y2 line" intention best).
    /// </summary>
    private void CreateFenceSegmentWorld(Transform parent, Vector3 worldA, Vector3 worldB) {
        float topY = MapGridUtil.GetTopSurfaceY(basePart);

        Vector3 mid = (worldA + worldB) * 0.5f;
        Vector3 delta = worldB - worldA;
        float length = delta.magnitude;
        if (length < 0.0001f) {
            return;
        }

        Vector3 dir = delta / length;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        GameObject go;
        if (fencePrefab != null) {
            go = Instantiate(fencePrefab, mid, rot, parent);
        } else {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, true);
            go.transform.position = mid;
            go.transform.rotation = rot;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        go.name = "Fence";
        go.transform.localScale = new Vector3(fenceThickness, fenceHeight, length);

        Vector3 p = go.transform.position;
        p.y = topY + (fenceHeight * 0.5f) + fenceYOffset;
        go.transform.position = p;

        _spawnedFences.Add(go);
    }

    /// <summary>
    /// Legacy: connects two cell centers (kept for DoorLink mode).
    /// </summary>
    private void CreateFenceSegmentBetweenCellCenters(Transform parent, Vector2Int cellA, Vector2Int cellB) {
        float topY = MapGridUtil.GetTopSurfaceY(basePart);
        Vector3 a = _grid.CellToWorldCenter(cellA, topY);
        Vector3 b = _grid.CellToWorldCenter(cellB, topY);
        CreateFenceSegmentWorld(parent, a, b);
    }

    private static void ShuffleInPlace<T>(List<T> list, System.Random rng) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // =========================================================
    // Fence container helpers
    // =========================================================
    private Transform EnsureFenceContainer(Transform parent) {
        if (fenceParent != null) {
            return fenceParent;
        }

        Transform existing = parent.Find(fenceContainerName);
        if (existing != null) {
            return existing;
        }

        GameObject go = new(fenceContainerName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private void ClearFenceContainerOnly(Transform container) {
        if (container == null) {
            return;
        }

        List<GameObject> toDestroy = new(container.childCount);
        for (int i = 0; i < container.childCount; i++) {
            toDestroy.Add(container.GetChild(i).gameObject);
        }

        for (int i = 0; i < toDestroy.Count; i++) {
            Destroy(toDestroy[i]);
        }

        _spawnedFences.Clear();
    }

    // =========================================================
    // Public fence toggle
    // =========================================================
    public void SetFencesEnabled(bool enabled) {
        fencesEnabled = enabled;
        ApplyFencesEnabledState();
    }

    private void ApplyFencesEnabledState() {
        if (fenceParent != null) {
            fenceParent.gameObject.SetActive(fencesEnabled);
        }

        for (int i = 0; i < _spawnedFences.Count; i++) {
            if (_spawnedFences[i] != null) {
                _spawnedFences[i].SetActive(fencesEnabled);
            }
        }
    }

    // =========================================================
    // Spawn markers + debug
    // =========================================================
    private void ApplySpawnMarkersAndDebug(Transform parent, Vector2Int zombieCell, Vector2Int targetCell) {
        float topY = MapGridUtil.GetTopSurfaceY(basePart);

        Vector3 zombieWorld = _grid.CellToWorldCenter(zombieCell, topY + capsuleYOffset);
        Vector3 targetWorld = _grid.CellToWorldCenter(targetCell, topY + capsuleYOffset);

        if (zombieSpawnMarker != null) {
            zombieSpawnMarker.position = zombieWorld;
        }

        if (targetSpawnMarker != null) {
            targetSpawnMarker.position = targetWorld;
        }

        if (!spawnDebugCapsules) {
            return;
        }

        SpawnDebugVisuals.CreateSpawnCapsule(
            "SpawnDebug_Zombie",
            zombieWorld,
            parent,
            capsuleHeight,
            capsuleRadius,
            _mpb,
            zombieSpawnDebugMaterial,
            Color.green,
            tintWithPropertyBlock
        );

        SpawnDebugVisuals.CreateSpawnCapsule(
            "SpawnDebug_Target",
            targetWorld,
            parent,
            capsuleHeight,
            capsuleRadius,
            _mpb,
            targetSpawnDebugMaterial,
            Color.red,
            tintWithPropertyBlock
        );
    }

    // =========================================================
    // Walls
    // =========================================================
    private void SpawnWalls(Transform parent, bool[,] walls) {
        float topY = MapGridUtil.GetTopSurfaceY(basePart);
        float wallCenterY = topY + (wallSize.y * 0.5f);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (!walls[y, x]) {
                    continue;
                }

                Vector3 p = _grid.CellToWorldCenter(new Vector2Int(x, y), wallCenterY);
                CreateWall(parent, p);
            }
        }
    }

    private void CreateWall(Transform parent, Vector3 position) {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent, true);
        wall.transform.position = position;
        wall.transform.localScale = wallSize;

        Rigidbody rb = wall.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    // =========================================================
    // Cleanup
    // =========================================================
    private void ClearSpawnedChildren(Transform parent) {
        List<GameObject> toDestroy = new(parent.childCount);

        for (int i = 0; i < parent.childCount; i++) {
            Transform ch = parent.GetChild(i);

            if (ch == fenceParent || ch.name == fenceContainerName) {
                continue;
            }

            if (ch.name.StartsWith("Wall", StringComparison.Ordinal) ||
                ch.name.StartsWith("SpawnDebug", StringComparison.Ordinal)) {
                toDestroy.Add(ch.gameObject);
            }
        }

        for (int i = 0; i < toDestroy.Count; i++) {
            Destroy(toDestroy[i]);
        }
    }

    private System.Random CreateRng() {
        int s = useRandomSeed ? Environment.TickCount : seed;
        return new System.Random(s);
    }

#if UNITY_EDITOR
    private void OnValidate() {
        width = Mathf.Max(4, width);
        height = Mathf.Max(4, height);
        cellSize = Mathf.Max(0.01f, cellSize);

        minSpawnManhattanDistance = Mathf.Max(0, minSpawnManhattanDistance);

        wallSize.x = Mathf.Max(0.01f, wallSize.x);
        wallSize.y = Mathf.Max(0.01f, wallSize.y);
        wallSize.z = Mathf.Max(0.01f, wallSize.z);

        capsuleHeight = Mathf.Max(0.01f, capsuleHeight);
        capsuleRadius = Mathf.Max(0.01f, capsuleRadius);

        fenceHeight = Mathf.Max(0.01f, fenceHeight);
        fenceThickness = Mathf.Max(0.01f, fenceThickness);
        maxFenceCount = Mathf.Max(0, maxFenceCount);

        minFenceSpacingCells = Mathf.Max(0, minFenceSpacingCells);
        fenceLengthFactor = Mathf.Clamp(fenceLengthFactor, 0.5f, 2.0f);
    }
#endif
}
