using UnityEngine;

namespace Train.Terrain {
    public class Terrain : MonoBehaviour {
        [SerializeField] private float heightOffset = 10f;
        [SerializeField] private int gridSize = 10;
        [SerializeField] private float spacing = 1f;
        [SerializeField] private LayerMask targetLayer;
        private Proprioception.Proprioception _proprioception;

        public float[,] HeightMap;

        private void Awake() {
            _proprioception = GetComponentInChildren<Proprioception.Proprioception>();
            HeightMap = new float[gridSize, gridSize];
        }

        private void Update() {
            Vector3 position = _proprioception.Position;

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

            Vector3 position = _proprioception.Position;

            float gridHalfSize = (gridSize - 1) * spacing * 0.5f;
            Vector3 gridTopLeft = new(position.x - gridHalfSize,
                position.y + heightOffset, position.z + gridHalfSize);

            for (int z = 0; z < gridSize; z++) {
                for (int x = 0; x < gridSize; x++) {
                    Vector3 rayOrigin = gridTopLeft + new Vector3(x * spacing, 0, -z * spacing);
                    Vector3 hitPoint = new(rayOrigin.x, HeightMap[x, z], rayOrigin.z);

                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(hitPoint, 0.025f);

                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(rayOrigin, 0.025f);
                }
            }
        }
    }
}
