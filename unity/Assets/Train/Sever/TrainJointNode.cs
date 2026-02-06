using Sever;
using UnityEngine;

namespace Train.Sever {
    public struct JointLimitCache {
        public float LowerLimit;
        public float UpperLimit;
    }

    public class TrainJointNode : JointNodeBase {
        public ArticulationBody Body;
        public JointLimitCache[] JointLimitCache;
        private const float _expectedMaxSpeed = 10f;
        private static float NormalizeSpeed(float speed) => Normalization.Tanh(speed, _expectedMaxSpeed);


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
                        JointLimitCache[i].LowerLimit,
                        JointLimitCache[i].UpperLimit
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
    }
}
