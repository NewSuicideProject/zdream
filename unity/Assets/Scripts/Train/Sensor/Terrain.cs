using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensor {
    public class Terrain : SensorComponent {
        [SerializeField] private LayerMask targetLayer;
        private float _spacing;

        public float[,] HeightMap;

        private TerrainSensor _terrainSensor;

        public override ISensor[] CreateSensors() {
            _terrainSensor = new TerrainSensor(this);
            return new ISensor[] { _terrainSensor };
        }

        private void Awake() {
            HeightMap = new float[Config.Terrain.MaxResolution, Config.Terrain.MaxResolution];
            OnEpisodeBegin();
        }

        public void OnEpisodeBegin() =>
            _spacing = Config.Normalization.ExpectedMaxThickness * 2 / (Config.Terrain.Resolution - 1);

        private void FixedUpdate() {
            float gridHalfSize = (Config.Terrain.Resolution - 1) * _spacing * 0.5f;
            Vector3 gridTopLeft = transform.position + new Vector3(
                -gridHalfSize,
                Config.Normalization.ExpectedMaxHeight,
                gridHalfSize
            );

            for (int z = 0; z < Config.Terrain.Resolution; z++) {
                for (int x = 0; x < Config.Terrain.Resolution; x++) {
                    Vector3 rayOrigin = gridTopLeft + new Vector3(x * _spacing, 0, -z * _spacing);

                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity, targetLayer)) {
                        HeightMap[x, z] = hit.point.y - transform.position.y;
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

            float gridHalfSize = (Config.Terrain.Resolution - 1) * _spacing * 0.5f;
            Vector3 gridTopLeft = transform.position + new Vector3(
                -gridHalfSize,
                Config.Normalization.ExpectedMaxHeight,
                gridHalfSize
            );

            for (int z = 0; z < Config.Terrain.Resolution; z++) {
                for (int x = 0; x < Config.Terrain.Resolution; x++) {
                    Vector3 rayOrigin = gridTopLeft + new Vector3(x * _spacing, 0, -z * _spacing);
                    Vector3 hitPoint = new(rayOrigin.x, transform.position.y + HeightMap[x, z], rayOrigin.z);

                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(hitPoint, 0.01f);

                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(rayOrigin, 0.01f);
                }
            }
        }
    }
}
