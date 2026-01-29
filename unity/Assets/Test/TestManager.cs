using UnityEngine;
using Random = UnityEngine.Random;

namespace Test
{
    public class TestManager : MonoBehaviour
    {
        [SerializeField] private Transform agentTransform;
        [SerializeField] private Rigidbody agentRigidbody;
        public Transform targetTransform;

        [SerializeField] private float spawnRange = 20f;

        [SerializeField] private float minSpawnDistance = 5f;

        [SerializeField] private float timeScale = 2f;

        public float SpawnRange => spawnRange;

        private void Awake()
        {
            Time.timeScale = timeScale;
        }

        public void Reset()
        {
            var agentScale = agentTransform.localScale;
            var targetScale = targetTransform.localScale;

            var agentRadius = Mathf.Max(agentScale.x, agentScale.z) * 0.5f;
            var targetRadius = Mathf.Max(targetScale.x, targetScale.z) * 0.5f;

            var agentSafeRange = spawnRange - agentRadius;
            var targetSafeRange = spawnRange - targetRadius;

            if (agentSafeRange < 0 || targetSafeRange < 0)
            {
                Debug.LogError($"Spawn range {spawnRange} is too small for object sizes.");
                return;
            }

            var maxPossibleDist2D = Mathf.Sqrt(2) * (agentSafeRange + targetSafeRange);
            var yDiff = Mathf.Abs(agentScale.y * 0.5f - targetScale.y * 0.5f);
            var maxPossibleDist3D = Mathf.Sqrt(maxPossibleDist2D * maxPossibleDist2D + yDiff * yDiff);

            if (minSpawnDistance > maxPossibleDist3D)
            {
                Debug.LogError($"minSpawnDistance {minSpawnDistance} is too large. Max possible: {maxPossibleDist3D}");
                return;
            }

            Vector3 targetPos;
            Vector3 agentPos;

            do
            {
                var targetRandomX = Random.Range(-targetSafeRange, targetSafeRange);
                var targetRandomZ = Random.Range(-targetSafeRange, targetSafeRange);
                targetPos = new Vector3(targetRandomX, targetScale.y / 2f, targetRandomZ);

                var agentRandomX = Random.Range(-agentSafeRange, agentSafeRange);
                var agentRandomZ = Random.Range(-agentSafeRange, agentSafeRange);
                agentPos = new Vector3(agentRandomX, agentTransform.localScale.y / 2f, agentRandomZ);
            } while (Vector3.Distance(targetPos, agentPos) < minSpawnDistance);

            targetTransform.localPosition = targetPos;
            agentTransform.localPosition = agentPos;

            agentRigidbody.angularVelocity = Vector3.zero;
            agentRigidbody.linearVelocity = Vector3.zero;
        }
    }
}