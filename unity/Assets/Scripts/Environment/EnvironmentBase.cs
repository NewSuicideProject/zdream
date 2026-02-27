using UnityEngine;

namespace Environment {
    public class EnvironmentBase : MonoBehaviour {
        [SerializeField] protected GameObject agentPrefab;
        [SerializeField] protected GameObject targetPrefab;

        [SerializeField] private float timeScale = 2f;

        protected Transform AgentTransform;
        public Transform TargetTransform { get; private set; }

        protected virtual void Awake() {
            Time.timeScale = timeScale;

            TargetTransform = Instantiate(targetPrefab, transform).transform;
            AgentTransform = Instantiate(agentPrefab, transform).transform;
        }

        public virtual void OnEpisodeBegin() {
        }
    }
}
