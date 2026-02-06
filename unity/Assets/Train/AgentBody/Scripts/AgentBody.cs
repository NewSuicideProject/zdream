using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Train.AgentBody.Scripts {
    public class AgentBody : MonoBehaviour {
        public Quaternion RootStraightQuat { get; private set; }
        public JointConfig RootJointConfig { get; private set; }

        public List<JointConfig> Configs;

        public int DoFCount => Configs.Sum(config => config.Body.dofCount);

        private void Awake() {
            ArticulationBody pelvis = GetComponent<ArticulationBody>();
            if (!pelvis) {
                pelvis = GetComponentInChildren<ArticulationBody>();
            }

            if (!pelvis) {
                Debug.LogError("No ArticulationBody found!");
                return;
            }


            RootJointConfig = JointConfig.GetJointConfig(pelvis, null);

            RootStraightQuat = RootJointConfig.Body.transform.rotation;

            Configs = new List<JointConfig>();
            GetConfigs(RootJointConfig);
        }

        private void GetConfigs(JointConfig config) {
            Configs.Add(config);

            foreach (JointConfig child in config.Children) {
                GetConfigs(child);
            }
        }
    }
}
