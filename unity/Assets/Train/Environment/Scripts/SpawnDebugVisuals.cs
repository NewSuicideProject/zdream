using UnityEngine;

public static class SpawnDebugVisuals {
    public static void CreateSpawnCapsule(
        string name,
        Vector3 position,
        Transform parent,
        float capsuleHeight,
        float capsuleRadius,
        MaterialPropertyBlock mpb,
        Material overrideMat,
        Color tint,
        bool tintWithPropertyBlock) {
        // Creates a non-physical debug capsule with optional material tinting via property block.
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, true);

        float radiusScale = capsuleRadius / 0.5f; // Unity capsule default radius ~0.5
        float heightScale = capsuleHeight / 2.0f; // Unity capsule default height ~2.0

        go.transform.position = position + (Vector3.up * (capsuleHeight * 0.5f));
        go.transform.localScale = new Vector3(radiusScale, heightScale, radiusScale);

        Renderer r = go.GetComponent<Renderer>();
        if (r != null) {
            if (overrideMat != null) {
                r.sharedMaterial = overrideMat;
            }

            if (tintWithPropertyBlock && mpb != null) {
                mpb.Clear();
                mpb.SetColor("_BaseColor", tint); // URP/HDRP
                mpb.SetColor("_Color", tint); // Built-in
                r.SetPropertyBlock(mpb);
            }
        }

        Collider col = go.GetComponent<Collider>();
        if (col != null) {
            col.isTrigger = true;
        }

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }
}
