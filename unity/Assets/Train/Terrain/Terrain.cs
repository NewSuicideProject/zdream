using UnityEngine;

namespace Train.Terrain {
    public class Terrain : MonoBehaviour {
        [Header("Settings")] [SerializeField] private float heightOffset = 10f;
        [SerializeField] private int gridSize = 10;
        [SerializeField] private float spacing = 1f;
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private Proprioception proprioception;

        private Vector3 _centerPosition;
        public float[,] HeightMap;
        private void Awake() => HeightMap = new float[gridSize, gridSize];

        private void Update() => ScanHeightMap();

        private void ScanHeightMap() {
            _centerPosition = !proprioception ? transform.position : proprioception.transform.position;

            float halfSize = (gridSize - 1) * spacing * 0.5f;
            Vector3 startPos = new(_centerPosition.x - halfSize,
                _centerPosition.y + heightOffset, _centerPosition.z + halfSize);

            for (int z = 0; z < gridSize; z++) {
                for (int x = 0; x < gridSize; x++) {
                    Vector3 rayOrigin = startPos + new Vector3(x * spacing, 0, -z * spacing);

                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            heightOffset * 2f, targetLayer)) {
                        HeightMap[x, z] = hit.point.y;
                    } else {
                        HeightMap[x, z] = _centerPosition.y - heightOffset;
                    }
                }
            }
        }

        private void OnDrawGizmos() {
            if (HeightMap == null || HeightMap.GetLength(0) != gridSize) {
                return;
            }

            Gizmos.color = Color.red;
            float halfSize = (gridSize - 1) * spacing * 0.5f;

            Vector3 currentCenter = Application.isPlaying ? _centerPosition : transform.position;
            Vector3 startPos = new(currentCenter.x - halfSize,
                currentCenter.y + heightOffset, currentCenter.z + halfSize);

            for (int z = 0; z < gridSize; z++) {
                for (int x = 0; x < gridSize; x++) {
                    Vector3 rayOrigin = startPos + new Vector3(x * spacing, HeightMap[x, z], -z * spacing);
                    Vector3 hitPoint = new(rayOrigin.x, HeightMap[x, z], rayOrigin.z);

                    Gizmos.DrawLine(rayOrigin, hitPoint);
                    Gizmos.DrawSphere(hitPoint, 0.05f);
                    Gizmos.DrawSphere(rayOrigin, 0.05f);
                }
            }
        }
    }
}
