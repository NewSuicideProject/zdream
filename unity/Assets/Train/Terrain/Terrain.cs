using UnityEngine;

namespace Train.Terrain {
    public class Terrain : MonoBehaviour {
        [SerializeField] private float heightOffset = 10f;
        [SerializeField] private int gridSize = 10;
        [SerializeField] private float spacing = 1f;
        [SerializeField] private LayerMask targetLayer;
        private Proprioception _proprioception;

        private Vector3 _position;

        public float[,] HeightMap;

        private void Awake() {
            _proprioception = GetComponentInChildren<Proprioception>();
            HeightMap = new float[gridSize, gridSize];
        }

        private void Update() {
            _position = _proprioception?.Position ?? transform.position;

            float gridHalfSize = (gridSize - 1) * spacing * 0.5f;
            Vector3 gridTopLeft = new(_position.x - gridHalfSize,
                _position.y + heightOffset, _position.z + gridHalfSize);

            for (int z = 0; z < gridSize; z++) {
                for (int x = 0; x < gridSize; x++) {
                    Vector3 rayOrigin = gridTopLeft + new Vector3(x * spacing, 0, -z * spacing);

                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            heightOffset * 2f, targetLayer)) {
                        HeightMap[x, z] = hit.point.y;
                    } else {
                        HeightMap[x, z] = 0f;
                    }
                }
            }
        }

        private void OnDrawGizmos() {
            if (HeightMap == null) {
                return;
            }

            float gridHalfSize = (gridSize - 1) * spacing * 0.5f;
            Vector3 gridTopLeft = new(_position.x - gridHalfSize,
                _position.y + heightOffset, _position.z + gridHalfSize);

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
