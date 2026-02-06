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

    private MapGrid _grid;
    private MaterialPropertyBlock _mpb;

    private void Start() {
        // Orchestrates full pipeline: clear -> generate matrix -> place markers/debug -> spawn walls.
        if (basePart == null) {
            Debug.LogError("[MapWallSpawner] BasePart not assigned.");
            return;
        }

        _mpb ??= new MaterialPropertyBlock();

        Transform parent = wallParent != null ? wallParent : transform;
        ClearSpawnedChildren(parent);

        _grid = new MapGrid(basePart, width, height, cellSize);

        System.Random rng = CreateRng();

        var genCfg = new MapGenerationConfig(
            width: width,
            height: height,
            initialWallFill: initialWallFill,
            minSpawnManhattanDistance: minSpawnManhattanDistance,
            loopDoorRatio: loopDoorRatio
        );

        bool[,] walls =
            MapGeneration.GenerateWallMatrix(genCfg, rng, out Vector2Int zombieCell, out Vector2Int targetCell);

        ApplySpawnMarkersAndDebug(parent, zombieCell, targetCell);

        SpawnWalls(parent, walls);
    }

#if UNITY_EDITOR
    private void OnValidate() {
        // Clamps inspector values to safe ranges.
        width = Mathf.Max(4, width);
        height = Mathf.Max(4, height);
        cellSize = Mathf.Max(0.01f, cellSize);

        minSpawnManhattanDistance = Mathf.Max(0, minSpawnManhattanDistance);

        wallSize.x = Mathf.Max(0.01f, wallSize.x);
        wallSize.y = Mathf.Max(0.01f, wallSize.y);
        wallSize.z = Mathf.Max(0.01f, wallSize.z);

        capsuleHeight = Mathf.Max(0.01f, capsuleHeight);
        capsuleRadius = Mathf.Max(0.01f, capsuleRadius);
    }
#endif

    private void ApplySpawnMarkersAndDebug(Transform parent, Vector2Int zombieCell, Vector2Int targetCell) {
        // Updates spawn markers and optionally creates debug capsules.
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
            name: "SpawnDebug_Zombie",
            position: zombieWorld,
            parent: parent,
            capsuleHeight: capsuleHeight,
            capsuleRadius: capsuleRadius,
            mpb: _mpb,
            overrideMat: zombieSpawnDebugMaterial,
            tint: Color.green,
            tintWithPropertyBlock: tintWithPropertyBlock
        );

        SpawnDebugVisuals.CreateSpawnCapsule(
            name: "SpawnDebug_Target",
            position: targetWorld,
            parent: parent,
            capsuleHeight: capsuleHeight,
            capsuleRadius: capsuleRadius,
            mpb: _mpb,
            overrideMat: targetSpawnDebugMaterial,
            tint: Color.red,
            tintWithPropertyBlock: tintWithPropertyBlock
        );
    }

    private void SpawnWalls(Transform parent, bool[,] walls) {
        // Instantiates cube walls for all true cells.
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
        // Creates a single wall cube with kinematic rigidbody.
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent, true);
        wall.transform.position = position;
        wall.transform.localScale = wallSize;

        Rigidbody rb = wall.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void ClearSpawnedChildren(Transform parent) {
        // Removes previously spawned walls/debug objects under the parent.
        List<GameObject> toDestroy = new(parent.childCount);

        for (int i = 0; i < parent.childCount; i++) {
            Transform ch = parent.GetChild(i);
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
        // Creates RNG based on seed policy.
        int s = useRandomSeed ? Environment.TickCount : seed;
        return new System.Random(s);
    }
}
