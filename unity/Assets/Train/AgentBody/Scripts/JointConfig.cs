using System.Collections.Generic;
using UnityEngine;

namespace Train.AgentBody.Scripts {
    public struct JointLimitCache {
        public float LowerLimit;
        public float UpperLimit;
    }

    public class JointConfig {
        public ArticulationBody ArticulationBody;
        public bool IsSevered;
        public JointLimitCache[] JointLimitCaches;
        public readonly List<JointConfig> Children = new();
        public JointConfig Parent;

        private ArticulationDrive GetDriveForAxis(int axisIndex) =>
            axisIndex switch {
                0 => ArticulationBody.xDrive,
                1 => ArticulationBody.yDrive,
                2 => ArticulationBody.zDrive,
                _ => throw new System.ArgumentOutOfRangeException(nameof(axisIndex),
                    $"Invalid axis index {axisIndex}")
            };

        public static JointConfig BuildJointConfig(ArticulationBody body, JointConfig parent) {
            JointConfig jointConfig = new() { ArticulationBody = body, IsSevered = false, Parent = parent };

            if (body.dofCount > 0) {
                jointConfig.JointLimitCaches = new JointLimitCache[body.dofCount];
                for (int i = 0; i < body.dofCount; i++) {
                    ArticulationDrive drive = jointConfig.GetDriveForAxis(i);
                    jointConfig.JointLimitCaches[i] = new JointLimitCache {
                        LowerLimit = drive.lowerLimit * Mathf.Deg2Rad, UpperLimit = drive.upperLimit * Mathf.Deg2Rad
                    };
                }
            }

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
    }
}
