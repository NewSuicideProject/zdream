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

        public override void Join() {
            _collider.enabled = true;
            Body.enabled = true;

            base.Join();
        }

        public TrainJointNode(GameObject gameObject, JointNodeBase parent) : base(gameObject,
            parent) {
            Body = gameObject.GetComponent<ArticulationBody>();
            _collider = gameObject.GetComponent<Collider>();
            if (_collider == null) {
                _collider = gameObject.GetComponentInChildren<Collider>();
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
    }
}
