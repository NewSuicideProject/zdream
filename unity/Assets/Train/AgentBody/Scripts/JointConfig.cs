using System.Collections.Generic;
using UnityEngine;

namespace Train.AgentBody.Scripts {
    public class JointConfig {
        private struct JointLimitCache {
            public float LowerLimit;
            public float UpperLimit;
        }

        public ArticulationBody Body;
        public bool LocalIsSevered;
        private JointLimitCache[] _jointLimitCache;
        public readonly List<JointConfig> Children = new();
        public JointConfig Parent;

        private const float _expectedMaxSpeed = 10f;

        private static float NormalizeSpeed(float speed) => Normalization.Tanh(speed, _expectedMaxSpeed);

        public bool IsSevered {
            get {
                if (LocalIsSevered) {
                    return true;
                }

                return Parent is { IsSevered: true };
            }
        }

        public ArticulationDrive GetDrive(int axisIndex) =>
            axisIndex switch {
                0 => Body.xDrive,
                1 => Body.yDrive,
                2 => Body.zDrive,
                _ => throw new System.ArgumentOutOfRangeException(nameof(axisIndex),
                    $"Invalid axis index {axisIndex}")
            };


        public float[] GetJointPositions(bool normalize = false) {
            float[] angles = new float[Body.dofCount];

            for (int i = 0; i < Body.dofCount; i++) {
                float value = Body.jointPosition[i];
                if (normalize) {
                    value = Normalization.LinearMinMax(
                        value,
                        _jointLimitCache[i].LowerLimit,
                        _jointLimitCache[i].UpperLimit
                    );
                }

                angles[i] = value;
            }

            return angles;
        }


        public float[] GetJointVelocities(bool normalize = false) {
            float[] velocities = new float[Body.dofCount];

            for (int i = 0; i < Body.dofCount; i++) {
                float value = Body.jointVelocity[i];
                if (normalize) {
                    value = NormalizeSpeed(value);
                }

                velocities[i] = value;
            }

            return velocities;
        }

        public static JointConfig GetJointConfig(ArticulationBody body, JointConfig parent) {
            JointConfig config = new() { Body = body, LocalIsSevered = false, Parent = parent };

            if (body.dofCount > 0) {
                config._jointLimitCache = new JointLimitCache[body.dofCount];
                for (int i = 0; i < body.dofCount; i++) {
                    ArticulationDrive drive = config.GetDrive(i);
                    config._jointLimitCache[i] = new JointLimitCache {
                        LowerLimit = drive.lowerLimit * Mathf.Deg2Rad, UpperLimit = drive.upperLimit * Mathf.Deg2Rad
                    };
                }
            }

            foreach (Transform childTransform in body.transform) {
                ArticulationBody childBody = childTransform.GetComponent<ArticulationBody>();
                if (!childBody) {
                    continue;
                }

                JointConfig childConfig = GetJointConfig(childBody, config);
                config.Children.Add(childConfig);
            }

            return config;
        }
    }
}
