using UnityEngine;
using Random = UnityEngine.Random;

namespace Test.Scripts {
    public class TestEnvironment : MonoBehaviour {
        [SerializeField] private float spawnRange = 20f;

        [SerializeField] private float minSpawnDistance = 5f;

        [SerializeField] private GameObject testAgentPrefab;
        [SerializeField] public GameObject testTargetPrefab;

        private Rigidbody _agentRigidbody;
        private Transform _agentTransform;

        public float SpawnRange => spawnRange;
        public Transform TargetTransform { get; private set; }

        private void Awake() {
            GameObject agentInstance = Instantiate(testAgentPrefab, Vector3.zero, Quaternion.identity, transform);
            _agentTransform = agentInstance.transform;
            _agentRigidbody = agentInstance.GetComponent<Rigidbody>();

            GameObject targetInstance = Instantiate(testTargetPrefab, Vector3.zero, Quaternion.identity, transform);
            TargetTransform = targetInstance.transform;
        }

        public void Reset() {
            Vector3 agentScale = _agentTransform.localScale;
            Vector3 targetScale = TargetTransform.localScale;

            float agentRadius = Mathf.Max(agentScale.x, agentScale.z) * 0.5f;
            float targetRadius = Mathf.Max(targetScale.x, targetScale.z) * 0.5f;

            float agentSafeRange = spawnRange - agentRadius;
            float targetSafeRange = spawnRange - targetRadius;

            if (agentSafeRange < 0 || targetSafeRange < 0) {
                Debug.LogError($"Spawn range {spawnRange} is too small for object sizes.");
                return;
            }

            float maxPossibleDist2D = Mathf.Sqrt(2) * (agentSafeRange + targetSafeRange);
            float yDiff = Mathf.Abs((agentScale.y * 0.5f) - (targetScale.y * 0.5f));
            float maxPossibleDist3D = Mathf.Sqrt((maxPossibleDist2D * maxPossibleDist2D) + (yDiff * yDiff));

            if (minSpawnDistance > maxPossibleDist3D) {
                Debug.LogError($"minSpawnDistance {minSpawnDistance} is too large. Max possible: {maxPossibleDist3D}");
                return;
            }

            Vector3 targetPos;
            Vector3 agentPos;

            do {
                float targetRandomX = Random.Range(-targetSafeRange, targetSafeRange);
                float targetRandomZ = Random.Range(-targetSafeRange, targetSafeRange);
                targetPos = new Vector3(targetRandomX, targetScale.y / 2f, targetRandomZ);

                float agentRandomX = Random.Range(-agentSafeRange, agentSafeRange);
                float agentRandomZ = Random.Range(-agentSafeRange, agentSafeRange);
                agentPos = new Vector3(agentRandomX, _agentTransform.localScale.y / 2f, agentRandomZ);
            } while (Vector3.Distance(targetPos, agentPos) < minSpawnDistance);

            TargetTransform.localPosition = targetPos;
            _agentTransform.localPosition = agentPos;

            _agentRigidbody.angularVelocity = Vector3.zero;
            _agentRigidbody.linearVelocity = Vector3.zero;
        }
    }
}
