using UnityEngine;
using Random = UnityEngine.Random;

namespace Environment {
    public class PlaneEnvironment : EnvironmentBase {
        [SerializeField] private float spawnRange = 20f;
        [SerializeField] private float minSpawnDistance = 5f;
        [SerializeField] private Vector3 agentSpawnOffest = Vector3.zero;
        [SerializeField] private Vector3 targetSpawnOffest = Vector3.zero;

        public override void Reset() {
            Vector3 agentScale = AgentTransform.localScale;
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
                targetPos = new Vector3(targetRandomX, 0, targetRandomZ);

                float agentRandomX = Random.Range(-agentSafeRange, agentSafeRange);
                float agentRandomZ = Random.Range(-agentSafeRange, agentSafeRange);
                agentPos = new Vector3(agentRandomX, 0, agentRandomZ);
            } while (Vector3.Distance(targetPos, agentPos) < minSpawnDistance);

            TargetTransform.localPosition = targetPos + targetSpawnOffest;
            AgentTransform.localPosition = agentPos + agentSpawnOffest;
        }
    }
}
