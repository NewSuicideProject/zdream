using Sever;
using UnityEngine;

namespace Train.Sever {
    public struct JointLimitCache {
        public float LowerLimit;
        public float UpperLimit;
    }

    public class TrainJointNode : JointNodeBase {
        public readonly ArticulationBody Body;
        private readonly Collider _collider;
        private readonly JointLimitCache[] _jointLimitCache;
        private const float _expectedMaxSpeed = 10f;

        public override void Sever() {
            _collider.enabled = false;
            Body.enabled = false;

            base.Sever();
        }

        public TrainJointNode(GameObject obj, JointNodeBase parent) : base(obj,
            parent) {
            Body = obj.GetComponent<ArticulationBody>();
            _collider = obj.GetComponent<Collider>();
            if (_collider == null) {
                _collider = obj.GetComponentInChildren<Collider>();
            }

            if (Body.dofCount <= 0) {
                return;
            }

            _jointLimitCache = new JointLimitCache[Body.dofCount];
            for (int i = 0; i < Body.dofCount; i++) {
                ArticulationDrive drive = GetDrive(i);
                _jointLimitCache[i] = new JointLimitCache {
                    LowerLimit = drive.lowerLimit * Mathf.Deg2Rad, UpperLimit = drive.upperLimit * Mathf.Deg2Rad
                };
            }
        }

        private static float NormalizeSpeed(float speed) => Normalization.Tanh(speed, _expectedMaxSpeed);


        private ArticulationDrive GetDrive(int axisIndex) =>
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
    }
}
