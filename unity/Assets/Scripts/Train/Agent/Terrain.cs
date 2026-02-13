using UnityEngine;

namespace Train.Agent {
    public class Terrain : MonoBehaviour {
        [SerializeField] private float heightOffset = 0.5f;
        [SerializeField] private int gridSize = 10;
        [SerializeField] private float spacing = 0.25f;
        [SerializeField] private LayerMask targetLayer;

        public float[,] HeightMap;

        private void Awake() => HeightMap = new float[gridSize, gridSize];

        private void Update() {
            Vector3 position = transform.position;

            float gridHalfSize = (gridSize - 1) * spacing * 0.5f;
            Vector3 gridTopLeft = new(position.x - gridHalfSize,
                position.y + heightOffset, position.z + gridHalfSize);

            for (int z = 0; z < gridSize; z++) {
                for (int x = 0; x < gridSize; x++) {
                    Vector3 rayOrigin = gridTopLeft + new Vector3(x * spacing, 0, -z * spacing);

                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            Mathf.Infinity, targetLayer)) {
                        HeightMap[x, z] = hit.point.y;
                    } else {
                        HeightMap[x, z] = Mathf.Infinity;
                    }
                }
            }
        }

        private void OnDrawGizmos() {
            if (HeightMap == null) {
                return;
            }

            Vector3 position = transform.position;

            float gridHalfSize = (gridSize - 1) * spacing * 0.5f;
            Vector3 gridTopLeft = new(position.x - gridHalfSize,
                position.y + heightOffset, position.z + gridHalfSize);

            for (int z = 0; z < gridSize; z++) {
                for (int x = 0; x < gridSize; x++) {
                    Vector3 rayOrigin = gridTopLeft + new Vector3(x * spacing, 0, -z * spacing);
                    Vector3 hitPoint = new(rayOrigin.x, HeightMap[x, z], rayOrigin.z);

                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(hitPoint, 0.01f);

                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(rayOrigin, 0.01f);
                }
            }
        }
    }
}
