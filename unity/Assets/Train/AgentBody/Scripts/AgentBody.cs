using System.Collections.Generic;
using UnityEngine;

namespace Train.AgentBody.Scripts {
    public class AgentBody : MonoBehaviour {
        public Quaternion RootStraightQuat { get; private set; }
        public JointConfig RootJointConfig { get; private set; }
        public Dictionary<string, JointConfig> JointConfigDict { get; private set; }


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

            JointConfigDict = new Dictionary<string, JointConfig>();
            RootJointConfig = BuildJointConfig(pelvis, null);

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

        private JointConfig BuildJointConfig(ArticulationBody body, JointConfig parent) {
            string bodyName = body.gameObject.name;

            if (JointConfigDict.ContainsKey(bodyName)) {
                throw new System.InvalidOperationException(
                    $"Duplicate ArticulationBody name found: '{bodyName}'. " +
                    "All ArticulationBody game objects must have unique names.");
            }

            JointConfig jointConfig = new() { ArticulationBody = body, LocalIsSevered = false, Parent = parent };

            if (body.dofCount > 0) {
                jointConfig.JointLimitCaches = new JointLimitCache[body.dofCount];
                for (int i = 0; i < body.dofCount; i++) {
                    ArticulationDrive drive = GetDriveForAxis(body, i);
                    jointConfig.JointLimitCaches[i] = new JointLimitCache {
                        LowerLimit = drive.lowerLimit * Mathf.Deg2Rad, UpperLimit = drive.upperLimit * Mathf.Deg2Rad
                    };
                }
            }

            JointConfigDict[bodyName] = jointConfig;

            foreach (Transform childTransform in body.transform) {
                ArticulationBody childBody = childTransform.GetComponent<ArticulationBody>();
                if (!childBody) {
                    continue;
                }

                JointConfig childConfig = BuildJointConfig(childBody, jointConfig);
                jointConfig.Children.Add(childConfig);
            }

            return jointConfig;
        }

        private static ArticulationDrive GetDriveForAxis(ArticulationBody body, int axisIndex) =>
            axisIndex switch {
                0 => body.xDrive,
                1 => body.yDrive,
                2 => body.zDrive,
                _ => throw new System.ArgumentOutOfRangeException(nameof(axisIndex),
                    $"Invalid axis index {axisIndex}")
            };
    }
}
