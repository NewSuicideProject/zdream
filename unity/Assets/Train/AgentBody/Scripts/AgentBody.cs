using UnityEngine;

namespace Train.AgentBody.Scripts {
    public class AgentBody : MonoBehaviour {
        public Quaternion RootStraightQuat { get; private set; }
        public JointConfig RootJointConfig { get; private set; }


        private void Awake() {
            ArticulationBody pelvis = GetComponent<ArticulationBody>();
            if (!pelvis) {
                pelvis = GetComponentInChildren<ArticulationBody>();
            }

            if (!pelvis) {
                Debug.LogError("No ArticulationBody found!");
                return;
            }

            RootStraightQuat = pelvis.transform.rotation;

            RootJointConfig = JointConfig.BuildJointConfig(pelvis, null);

            string log = "Joint Structure:\n";
            LogJointConfig(RootJointConfig, 0, ref log);
            Debug.Log(log);
        }

        private static void LogJointConfig(JointConfig config, int depth, ref string log) {
            string indent = new(' ', depth * 2);
            log += $"{indent}{config.ArticulationBody.name} {config.ArticulationBody.dofCount}DoF\n";

            foreach (JointConfig child in config.Children) {
                LogJointConfig(child, depth + 1, ref log);
            }
        }
    }
}
