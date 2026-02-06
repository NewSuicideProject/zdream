using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public sealed class FenceSpawner : MonoBehaviour {
    // =========================================================
    // [SECTION 0] Anchor data (input)
    // =========================================================

    [Serializable]
    public struct FenceAnchor {
        [Tooltip("Fence center position in world space (usually your door/throat cell center).")]
        public Vector3 worldPos;

        [Tooltip("Fence yaw (rotation around Y axis) in degrees.")]
        public float yawDeg;

        [Tooltip("Optional id to help debugging / stable indexing.")]
        public int id;

        [Tooltip("Optional: mark as guaranteed/important (ex: target-ring).")]
        public bool isGuaranteed;
    }

    // =========================================================
    // [SECTION 1] Inspector / Config
    // =========================================================

    [Header("Fence prefab")] [SerializeField]
    private GameObject fencePrefab;

    [Tooltip("Parent transform for spawned fences (if null, this.transform).")] [SerializeField]
    private Transform fenceParent;

    [Header("Spawn control")]
    [Tooltip("Master toggle: if false, fences won't be spawned and existing fences can be hidden.")]
    [SerializeField]
    private bool fencesEnabled = true;

    [Tooltip("If true, toggling off only hides fences (SetActive(false)). If false, toggling off destroys fences.")]
    [SerializeField]
    private bool disableByHiding = true;

    [Tooltip("Auto spawn on Start() if anchors exist.")] [SerializeField]
    private bool autoSpawnOnStart = true;

    [Header("Placement offsets")] [Tooltip("Extra position offset applied to every fence (world).")] [SerializeField]
    private Vector3 globalOffset = Vector3.zero;

    [Tooltip("Lift fences by Y (useful if prefab pivot is at center).")] [SerializeField]
    private float yLift = 0.0f;

    [Header("Randomization (optional)")]
    [Tooltip("If true, spawns only a subset of anchors by probability.")]
    [SerializeField]
    private bool useProbability = false;

    [Range(0f, 1f)] [SerializeField] private float spawnProbability = 1.0f;

    [Tooltip("If true, guaranteed anchors always spawn even if probability filtering is on.")] [SerializeField]
    private bool alwaysSpawnGuaranteed = true;

    [Tooltip("If true, uses seed for deterministic random filtering.")] [SerializeField]
    private bool deterministic = true;

    [SerializeField] private int seed = 12345;

    [Header("Runtime debug")] [SerializeField]
    private bool drawGizmos = true;

    [SerializeField] private float gizmoSize = 0.4f;

    // =========================================================
    // [SECTION 2] Runtime state
    // =========================================================

    // External systems (your map generator) should set anchors here.
    [NonSerialized] public readonly List<FenceAnchor> Anchors = new();

    private readonly List<GameObject> _spawned = new();
    private bool _hasSpawnedOnce;

    // =========================================================
    // [SECTION 3] Unity lifecycle
    // =========================================================

    private void Awake() {
        if (fenceParent == null) {
            fenceParent = transform;
        }
    }

    private void Start() {
        if (autoSpawnOnStart && fencesEnabled && Anchors.Count > 0) {
            RebuildFences();
        }
    }

    // =========================================================
    // [SECTION 4] Public API
    // =========================================================

    /// <summary>
    /// Replaces current anchors with new ones and optionally rebuilds.
    /// Call this from your MapWallSpawner after room graph / door cells are computed.
    /// </summary>
    public void SetAnchors(IEnumerable<FenceAnchor> anchors, bool rebuildNow = true) {
        Anchors.Clear();
        Anchors.AddRange(anchors);

        if (rebuildNow) {
            RebuildFences();
        }
    }

    /// <summary>
    /// Builds fences from current anchors using current settings.
    /// </summary>
    public void RebuildFences() {
        // If disabled: either hide or destroy existing, and do nothing else.
        if (!fencesEnabled) {
            ApplyEnabledState(false);
            return;
        }

        // Safety
        if (fencePrefab == null) {
            Debug.LogWarning("[FenceSpawner] fencePrefab is null. Cannot spawn fences.", this);
            return;
        }

        ClearFencesInternal();

        Random rng = deterministic ? new System.Random(seed) : new System.Random();

        for (int i = 0; i < Anchors.Count; i++) {
            FenceAnchor a = Anchors[i];

            // Probability filter (optional)
            if (useProbability) {
                if (alwaysSpawnGuaranteed && a.isGuaranteed) {
                    // pass
                } else {
                    double r = rng.NextDouble();
                    if (r > spawnProbability) {
                        continue;
                    }
                }
            }

            SpawnFence(a);
        }

        _hasSpawnedOnce = true;
        ApplyEnabledState(true);
    }

    /// <summary>
    /// Master enable/disable fences. If disableByHiding=true, it toggles SetActive on spawned fences.
    /// Otherwise it destroys when disabling.
    /// </summary>
    public void SetFencesEnabled(bool enabled) {
        fencesEnabled = enabled;

        if (!enabled) {
            if (disableByHiding) {
                ApplyEnabledState(false);
            } else {
                ClearFencesInternal();
            }

            return;
        }

        // enabling
        if (_spawned.Count > 0) {
            ApplyEnabledState(true);
        } else {
            // If we haven't spawned yet or were destroyed, rebuild.
            if (Anchors.Count > 0) {
                RebuildFences();
            }
        }
    }

    public bool GetFencesEnabled() => fencesEnabled;

    /// <summary>
    /// Removes all spawned fence objects (does NOT clear anchors).
    /// </summary>
    public void ClearFences() {
        ClearFencesInternal();
        _hasSpawnedOnce = false;
    }

    // =========================================================
    // [SECTION 5] Internals
    // =========================================================

    private void SpawnFence(FenceAnchor a) {
        Vector3 pos = a.worldPos + globalOffset + new Vector3(0f, yLift, 0f);
        Quaternion rot = Quaternion.Euler(0f, a.yawDeg, 0f);

        GameObject go = Instantiate(fencePrefab, pos, rot, fenceParent);
        go.name = $"Fence_{a.id}_{(a.isGuaranteed ? "G" : "R")}";
        _spawned.Add(go);
    }

    private void ApplyEnabledState(bool enabled) {
        for (int i = 0; i < _spawned.Count; i++) {
            if (_spawned[i] == null) {
                continue;
            }

            _spawned[i].SetActive(enabled);
        }
    }

    private void ClearFencesInternal() {
        for (int i = 0; i < _spawned.Count; i++) {
            if (_spawned[i] == null) {
                continue;
            }

            Destroy(_spawned[i]);
        }

        _spawned.Clear();
    }

    // =========================================================
    // [SECTION 6] Editor helpers (ContextMenu)
    // =========================================================

    [ContextMenu("FenceSpawner/Toggle Enabled")]
    private void CM_ToggleEnabled() => SetFencesEnabled(!fencesEnabled);

    [ContextMenu("FenceSpawner/Rebuild Fences")]
    private void CM_Rebuild() => RebuildFences();

    [ContextMenu("FenceSpawner/Clear Fences")]
    private void CM_Clear() => ClearFences();

    // =========================================================
    // [SECTION 7] Gizmos
    // =========================================================

    private void OnDrawGizmosSelected() {
        if (!drawGizmos) {
            return;
        }

        Gizmos.matrix = Matrix4x4.identity;

        // Draw anchors even in editor (Anchors is runtime; but still useful if you populate it in edit mode).
        for (int i = 0; i < Anchors.Count; i++) {
            FenceAnchor a = Anchors[i];
            Gizmos.color = a.isGuaranteed ? Color.red : Color.green;

            Vector3 p = a.worldPos + globalOffset + new Vector3(0f, yLift, 0f);
            Gizmos.DrawWireCube(p, new Vector3(gizmoSize, gizmoSize, gizmoSize));

            // forward dir
            Vector3 dir = Quaternion.Euler(0f, a.yawDeg, 0f) * Vector3.forward;
            Gizmos.DrawLine(p, p + (dir * (gizmoSize * 1.5f)));
        }
    }
}
