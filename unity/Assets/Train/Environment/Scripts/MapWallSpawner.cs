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
    // Road width post-process (segment-based)
    // =========================================================
    [Header("Road Width (1/2/3 cells) - Segment Based")]
    [Tooltip("If true, widen ONLY corridor segments (room-to-room links). Width does NOT change per-cell.")]
    [SerializeField]
    private bool enableVariableRoadWidth = true;

    [Tooltip("Chance that a corridor segment becomes width=2 (if width=3 not chosen).")]
    [Range(0f, 1f)]
    [SerializeField]
    private float roadWidth2Chance = 0.22f;

    [Tooltip("Chance that a corridor segment becomes width=3.")] [Range(0f, 1f)] [SerializeField]
    private float roadWidth3Chance = 0.10f;

    [Tooltip("Clamp road width to 1..3.")] [SerializeField]
    private int maxRoadWidth = 3;

    [Tooltip("If no path exists, carve a simple L tunnel first (only opens extra).")] [SerializeField]
    private bool carvePathIfDisconnected = true;

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
    private readonly List<Vector2Int> _mainPath = new(4096);

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

        // =========================================================
        // Segment-based corridor widening (room-to-room links only)
        // =========================================================
        if (enableVariableRoadWidth) {
            PostProcess_VariableWidthMainRoute_SegmentBased(walls, zombieCell, targetCell, rng);
        }

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
    // Road width post-process (SEGMENT BASED)
    // =========================================================
    private void PostProcess_VariableWidthMainRoute_SegmentBased(bool[,] walls, Vector2Int start, Vector2Int goal,
        System.Random rng) {
        _mainPath.Clear();

        bool hasPath = TryFindPathBFS(walls, start, goal, _mainPath);

        if (!hasPath && carvePathIfDisconnected) {
            CarveLTunnel(walls, start, goal, rng);
            _mainPath.Clear();
            hasPath = TryFindPathBFS(walls, start, goal, _mainPath);
        }

        if (!hasPath || _mainPath.Count < 2) {
            return;
        }

        int wClamp = Mathf.Clamp(maxRoadWidth, 1, 3);

        // Build "room core" mask once (heuristic)
        bool[,] isRoomCore = BuildRoomCoreMask(walls);

        // Walk the main path:
        // - inside room core: DO NOT change width per-cell (we keep whatever map already has)
        // - outside room core: corridor segment => choose width ONCE for the whole segment
        int i = 1;
        while (i < _mainPath.Count) {
            Vector2Int cur = _mainPath[i];

            if (isRoomCore[cur.y, cur.x]) {
                // Keep room area as-is (no per-cell width variation logic).
                SetWalkable(walls, cur);
                i++;
                continue;
            }

            // corridor segment = until we enter room core again (or path ends)
            int segStart = i;
            int segEnd = i;

            while (segEnd < _mainPath.Count && !isRoomCore[_mainPath[segEnd].y, _mainPath[segEnd].x]) {
                segEnd++;
            }

            int corridorWidth = SampleRoadWidthForSegment(rng, wClamp);

            for (int k = segStart; k < segEnd; k++) {
                Vector2Int p = _mainPath[k];
                Vector2Int prev = _mainPath[k - 1];
                Vector2Int step = p - prev;

                SetWalkable(walls, p);

                // widen perpendicular to travel direction, using FIXED corridorWidth
                if (step.x != 0) {
                    // moving E/W => widen along Y
                    CarvePerpBandY_Fixed(walls, p, corridorWidth);
                } else if (step.y != 0) {
                    // moving N/S => widen along X
                    CarvePerpBandX_Fixed(walls, p, corridorWidth);
                } else {
                    CarvePerpBandX_Fixed(walls, p, corridorWidth);
                }
            }

            i = segEnd;
        }
    }

    private int SampleRoadWidthForSegment(System.Random rng, int clampMax) {
        if (clampMax <= 1) {
            return 1;
        }

        double r = rng.NextDouble();
        if (clampMax >= 3 && r < roadWidth3Chance) {
            return 3;
        }

        r = rng.NextDouble();
        if (clampMax >= 2 && r < roadWidth2Chance) {
            return 2;
        }

        return 1;
    }

    // Fixed band carving (no per-cell randomness)
    private void CarvePerpBandX_Fixed(bool[,] walls, Vector2Int center, int widthCells) {
        SetWalkable(walls, center);

        if (widthCells <= 1) {
            return;
        }

        if (widthCells == 2) {
            // deterministic preference: open both if possible, else whichever exists.
            // But to keep "fixed", we choose +1 as default and fallback to -1 if OOB.
            Vector2Int c1 = new(center.x + 1, center.y);
            Vector2Int c2 = new(center.x - 1, center.y);

            if (InInnerBounds(c1)) {
                SetWalkable(walls, c1);
            } else if (InInnerBounds(c2)) {
                SetWalkable(walls, c2);
            }

            return;
        }

        // 3
        SetWalkable(walls, new Vector2Int(center.x - 1, center.y));
        SetWalkable(walls, new Vector2Int(center.x + 1, center.y));
    }

    private void CarvePerpBandY_Fixed(bool[,] walls, Vector2Int center, int widthCells) {
        SetWalkable(walls, center);

        if (widthCells <= 1) {
            return;
        }

        if (widthCells == 2) {
            Vector2Int c1 = new(center.x, center.y + 1);
            Vector2Int c2 = new(center.x, center.y - 1);

            if (InInnerBounds(c1)) {
                SetWalkable(walls, c1);
            } else if (InInnerBounds(c2)) {
                SetWalkable(walls, c2);
            }

            return;
        }

        // 3
        SetWalkable(walls, new Vector2Int(center.x, center.y - 1));
        SetWalkable(walls, new Vector2Int(center.x, center.y + 1));
    }

    private bool[,] BuildRoomCoreMask(bool[,] walls) {
        bool[,] core = new bool[height, width];

        for (int y = 1; y < height - 1; y++) {
            for (int x = 1; x < width - 1; x++) {
                if (walls[y, x]) {
                    continue;
                }

                int open4 = 0;
                if (!walls[y + 1, x]) {
                    open4++;
                }

                if (!walls[y - 1, x]) {
                    open4++;
                }

                if (!walls[y, x + 1]) {
                    open4++;
                }

                if (!walls[y, x - 1]) {
                    open4++;
                }

                // Heuristic A: junction / space
                bool isSpace = open4 >= 3;

                // Heuristic B: part of 2x2 open block (room-like)
                bool in2x2 =
                    (!walls[y, x] && !walls[y, x + 1] && !walls[y + 1, x] && !walls[y + 1, x + 1]) ||
                    (!walls[y, x] && !walls[y, x - 1] && !walls[y + 1, x] && !walls[y + 1, x - 1]) ||
                    (!walls[y, x] && !walls[y, x + 1] && !walls[y - 1, x] && !walls[y - 1, x + 1]) ||
                    (!walls[y, x] && !walls[y, x - 1] && !walls[y - 1, x] && !walls[y - 1, x - 1]);

                core[y, x] = isSpace || in2x2;
            }
        }

        return core;
    }

    private void SetWalkable(bool[,] walls, Vector2Int c) {
        if (!InInnerBounds(c)) {
            return;
        }

        walls[c.y, c.x] = false;
    }

    private bool InInnerBounds(Vector2Int c) =>
        // keep a 1-cell margin so we don't carve outside the base
        c.x > 0 && c.x < width - 1 && c.y > 0 && c.y < height - 1;

    private void CarveLTunnel(bool[,] walls, Vector2Int a, Vector2Int b, System.Random rng) {
        bool hv = rng.Next(2) == 0;

        if (hv) {
            CarveLineX(walls, a, new Vector2Int(b.x, a.y));
            CarveLineY(walls, new Vector2Int(b.x, a.y), b);
        } else {
            CarveLineY(walls, a, new Vector2Int(a.x, b.y));
            CarveLineX(walls, new Vector2Int(a.x, b.y), b);
        }
    }

    private void CarveLineX(bool[,] walls, Vector2Int from, Vector2Int to) {
        int y = from.y;
        int x0 = Mathf.Min(from.x, to.x);
        int x1 = Mathf.Max(from.x, to.x);
        for (int x = x0; x <= x1; x++) {
            SetWalkable(walls, new Vector2Int(x, y));
        }
    }

    private void CarveLineY(bool[,] walls, Vector2Int from, Vector2Int to) {
        int x = from.x;
        int y0 = Mathf.Min(from.y, to.y);
        int y1 = Mathf.Max(from.y, to.y);
        for (int y = y0; y <= y1; y++) {
            SetWalkable(walls, new Vector2Int(x, y));
        }
    }

    private bool TryFindPathBFS(bool[,] walls, Vector2Int start, Vector2Int goal, List<Vector2Int> outPath) {
        outPath.Clear();

        if (!InBounds(start) || !InBounds(goal)) {
            return false;
        }

        if (walls[start.y, start.x]) {
            return false;
        }

        if (walls[goal.y, goal.x]) {
            return false;
        }

        int W = width;
        int H = height;

        Queue<Vector2Int> q = new(8192);
        Vector2Int[,] prev = new Vector2Int[H, W];
        bool[,] visited = new bool[H, W];

        for (int y = 0; y < H; y++) {
            for (int x = 0; x < W; x++) {
                prev[y, x] = new Vector2Int(int.MinValue, int.MinValue);
            }
        }

        visited[start.y, start.x] = true;
        q.Enqueue(start);

        Vector2Int[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        bool found = false;

        while (q.Count > 0) {
            Vector2Int cur = q.Dequeue();
            if (cur == goal) {
                found = true;
                break;
            }

            for (int i = 0; i < 4; i++) {
                Vector2Int nx = cur + dirs[i];
                if (!InBounds(nx)) {
                    continue;
                }

                if (visited[nx.y, nx.x]) {
                    continue;
                }

                if (walls[nx.y, nx.x]) {
                    continue;
                }

                visited[nx.y, nx.x] = true;
                prev[nx.y, nx.x] = cur;
                q.Enqueue(nx);
            }
        }

        if (!found) {
            return false;
        }

        Vector2Int p = goal;
        outPath.Add(p);
        while (p != start) {
            Vector2Int pr = prev[p.y, p.x];
            if (pr.x == int.MinValue) {
                return false;
            }

            p = pr;
            outPath.Add(p);
        }

        outPath.Reverse();
        return true;
    }

    private bool InBounds(Vector2Int c) => c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

    // =========================================================
    // Fence pipeline with fallback
    // =========================================================
    private void SpawnFences(Transform parent, bool[,] walls, System.Random rng) {
        _spawnedFences.Clear();

        List<DoorFenceLocator.DoorLink> links = ComputeDoorLinksOrEmpty(walls);

        if (links.Count > 0) {
            int count = links.Count;
            if (maxFenceCount > 0) {
                count = Mathf.Min(count, maxFenceCount);
            }

            for (int i = 0; i < count; i++) {
                // DoorLink fences now span 1/2/3 based on corridor width.
                CreateFenceForDoorLink(fenceParent, walls, links[i].cellA, links[i].cellB);
            }

            return;
        }

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
    /// Finds corridor-ish places and places fences. Excludes room cores.
    /// Fence spans 1/2/3 by measuring perpendicular openness.
    /// </summary>
    private void SpawnFences_ByThroatFallback(Transform fenceContainer, bool[,] walls, System.Random rng) {
        float topY = MapGridUtil.GetTopSurfaceY(basePart);

        bool[,] isRoomCore = BuildRoomCoreMask(walls);

        List<(Vector2Int cell, bool nsCorridor)> candidates = new(2048);

        for (int y = 1; y < height - 1; y++) {
            for (int x = 1; x < width - 1; x++) {
                if (walls[y, x]) {
                    continue;
                }

                if (isRoomCore[y, x]) {
                    continue; // avoid inside rooms
                }

                bool n = !walls[y + 1, x];
                bool s = !walls[y - 1, x];
                bool e = !walls[y, x + 1];
                bool w = !walls[y, x - 1];

                bool nsCorridor = n && s;
                bool ewCorridor = e && w;

                // prefer tighter places but keep permissive
                if (nsCorridor && (!e || !w)) {
                    candidates.Add((new Vector2Int(x, y), true));
                } else if (ewCorridor && (!n || !s)) {
                    candidates.Add((new Vector2Int(x, y), false));
                }
            }
        }

        if (candidates.Count == 0) {
            Debug.LogWarning("[MapWallSpawner] No fallback fence candidates found.");
            return;
        }

        ShuffleInPlace(candidates, rng);

        int spacing = Mathf.Max(0, minFenceSpacingCells);
        List<Vector2Int> picked = new(128);

        int spawned = 0;
        int limit = maxFenceCount > 0 ? maxFenceCount : int.MaxValue;

        for (int i = 0; i < candidates.Count && spawned < limit; i++) {
            (Vector2Int c, bool ns) = candidates[i];

            if (spacing > 0 && TooCloseToPicked(c, picked, spacing)) {
                continue;
            }

            int span = ns
                ? MeasurePerpOpenWidth_X(walls, c, 3)
                : MeasurePerpOpenWidth_Y(walls, c, 3);

            span = Mathf.Clamp(span, 1, 3);

            Vector3 center = _grid.CellToWorldCenter(c, topY);
            Vector3 axis = ns ? Vector3.right : Vector3.forward;

            float halfLen = cellSize * fenceLengthFactor * 0.5f * span;

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
    // DoorLink fence that blocks width 1/2/3
    // =========================================================
    private void CreateFenceForDoorLink(Transform parent, bool[,] walls, Vector2Int cellA, Vector2Int cellB) {
        Vector2Int d = cellB - cellA;

        float topY = MapGridUtil.GetTopSurfaceY(basePart);

        Vector3 aW = _grid.CellToWorldCenter(cellA, topY);
        Vector3 bW = _grid.CellToWorldCenter(cellB, topY);
        Vector3 mid = (aW + bW) * 0.5f;

        bool travelNS = d.y != 0;

        // Measure span from both sides and take max (more stable for widened corridors)
        int spanA = travelNS ? MeasurePerpOpenWidth_X(walls, cellA, 3) : MeasurePerpOpenWidth_Y(walls, cellA, 3);
        int spanB = travelNS ? MeasurePerpOpenWidth_X(walls, cellB, 3) : MeasurePerpOpenWidth_Y(walls, cellB, 3);
        int span = Mathf.Clamp(Mathf.Max(spanA, spanB), 1, 3);

        Vector3 axis = travelNS ? Vector3.right : Vector3.forward;
        float halfLen = cellSize * fenceLengthFactor * 0.5f * span;

        Vector3 p0 = mid - (axis * halfLen);
        Vector3 p1 = mid + (axis * halfLen);

        CreateFenceSegmentWorld(parent, p0, p1);
    }

    private int MeasurePerpOpenWidth_X(bool[,] walls, Vector2Int center, int max) {
        if (max <= 1) {
            return 1;
        }

        bool left = IsWalkable(walls, new Vector2Int(center.x - 1, center.y));
        bool right = IsWalkable(walls, new Vector2Int(center.x + 1, center.y));

        if (max >= 3 && left && right) {
            return 3;
        }

        if (left || right) {
            return 2;
        }

        return 1;
    }

    private int MeasurePerpOpenWidth_Y(bool[,] walls, Vector2Int center, int max) {
        if (max <= 1) {
            return 1;
        }

        bool down = IsWalkable(walls, new Vector2Int(center.x, center.y - 1));
        bool up = IsWalkable(walls, new Vector2Int(center.x, center.y + 1));

        if (max >= 3 && down && up) {
            return 3;
        }

        if (down || up) {
            return 2;
        }

        return 1;
    }

    private bool IsWalkable(bool[,] walls, Vector2Int c) {
        if (!InInnerBounds(c)) {
            return false;
        }

        return !walls[c.y, c.x];
    }

    // =========================================================
    // Fence creation helpers
    // =========================================================
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

        maxRoadWidth = Mathf.Clamp(maxRoadWidth, 1, 3);
        roadWidth2Chance = Mathf.Clamp01(roadWidth2Chance);
        roadWidth3Chance = Mathf.Clamp01(roadWidth3Chance);
    }
#endif
}
