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
        public readonly int DoF;

        public override void Sever() {
            if (IsSevered) {
                return;
            }

            IsSevered = true;
            _collider.enabled = false;
            Body.enabled = false;
            GameObject.transform.localScale = Vector3.zero;


            foreach (JointNodeBase child in Children) {
                child.Sever();
            }
        }

        public override void Join() {
            if (!IsSevered) {
                return;
            }

            IsSevered = false;
            GameObject.transform.localScale = Vector3.one;
            _collider.enabled = true;
            Body.enabled = true;

            foreach (JointNodeBase child in Children) {
                child.Join();
            }
        }

        public TrainJointNode(GameObject gameObject, JointNodeBase parent) : base(gameObject,
            parent) {
            Body = gameObject.GetComponent<ArticulationBody>();
            _collider = gameObject.GetComponent<Collider>();
            if (_collider == null) {
                _collider = gameObject.GetComponentInChildren<Collider>();
            }

            DoF = Body.dofCount;

            if (DoF <= 0) {
                return;
            }

            _jointLimitCache = new JointLimitCache[DoF];
            for (int i = 0; i < DoF; i++) {
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
            if (IsSevered) {
                return new float[DoF];
            }

            float[] angles = new float[DoF];

            for (int i = 0; i < DoF; i++) {
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
            if (IsSevered) {
                return new float[DoF];
            }

            float[] velocities = new float[DoF];

            for (int i = 0; i < DoF; i++) {
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
