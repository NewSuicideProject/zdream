using UnityEngine;

namespace Environment {
    public class EnvironmentBase : MonoBehaviour {
        [SerializeField] protected GameObject agentPrefab;
        [SerializeField] protected GameObject targetPrefab;
        [SerializeField] protected Vector3 agentSpawnOffest = Vector3.zero;
        [SerializeField] protected Vector3 targetSpawnOffest = Vector3.zero;

        [SerializeField] private float timeScale = 2f;

        protected Transform AgentTransform;

        public Transform TargetTransform {
            get;
            private set;
        }

        protected virtual void Awake() {
            Time.timeScale = timeScale;

            TargetTransform = Instantiate(targetPrefab, targetSpawnOffest, Quaternion.identity, transform).transform;
            AgentTransform = Instantiate(agentPrefab, agentSpawnOffest, Quaternion.identity, transform).transform;
        }

        public virtual void OnEpisodeBegin() {
        }
    }
}
