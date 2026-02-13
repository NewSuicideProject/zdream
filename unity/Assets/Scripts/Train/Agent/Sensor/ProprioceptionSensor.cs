using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Agent.Sensors {
    public class ProprioceptionSensor : ISensor {
        private readonly float _expectedMaxDistance;

        private readonly float _expectedMaxSpeed;
        private readonly float _expectedMaxThickness;
        private readonly ObservationSpec _observationSpec;
        private readonly Proprioception _proprioception;

        private readonly int _size;

        public Transform Target;

        public ProprioceptionSensor(Proprioception proprioception, Transform target,
            float expectedMaxSpeed = 20f,
            float expectedMaxDistance = 20f,
            float expectedMaxThickness = 1f) {
            _proprioception = proprioception;
            Target = target;
            _expectedMaxSpeed = expectedMaxSpeed;
            _expectedMaxDistance = expectedMaxDistance;
            _expectedMaxThickness = expectedMaxThickness;

            _size = 3 + // gravity
                    3 + // CoM
                    3 + // angular velocity
                    3 + // linear velocity
                    2 + // projected forward
                    3 + // relative target position
                    1 + // integrity
                    _proprioception.Contacts.Length +
                    _proprioception.Attaches.Length +
                    _proprioception.NormalizedJointBlocks.Length;

            _observationSpec = ObservationSpec.Vector(_size);
        }

        public string GetName() => "proprioception";

        public int Write(ObservationWriter writer) {
            int idx = 0;

            Vector3 gravity = _proprioception.Gravity;
            writer[idx++] = gravity.x;
            writer[idx++] = gravity.y;
            writer[idx++] = gravity.z;

            Vector3 com = _proprioception.Com;
            writer[idx++] = NormalizeThickness(com.x);
            writer[idx++] = NormalizeThickness(com.y);
            writer[idx++] = NormalizeThickness(com.z);

            Vector3 angularVelocity = _proprioception.AngularVelocity;
            writer[idx++] = NormalizeSpeed(angularVelocity.x);
            writer[idx++] = NormalizeSpeed(angularVelocity.y);
            writer[idx++] = NormalizeSpeed(angularVelocity.z);

            Vector3 linearVelocity = _proprioception.LinearVelocity;
            writer[idx++] = NormalizeSpeed(linearVelocity.x);
            writer[idx++] = NormalizeSpeed(linearVelocity.y);
            writer[idx++] = NormalizeSpeed(linearVelocity.z);

            Vector3 forward = _proprioception.Forward;
            Vector3 position = _proprioception.Position;
            Vector3 projectedForward = Vector3.ProjectOnPlane(forward, Vector3.up);

            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = Vector3.forward;
            } else {
                projectedForward.Normalize();
            }

            writer[idx++] = projectedForward.x;
            writer[idx++] = projectedForward.z;

            Quaternion yawQuat = Quaternion.LookRotation(projectedForward, Vector3.up);
            Matrix4x4 inverseMatrix = Matrix4x4.TRS(position, yawQuat, Vector3.one).inverse;

            Vector3 localPosition = Vector3.zero;
            if (Target != null) {
                localPosition = inverseMatrix.MultiplyPoint3x4(Target.position);
            }

            writer[idx++] = NormalizeDistance(localPosition.x);
            writer[idx++] = NormalizeDistance(localPosition.y);
            writer[idx++] = NormalizeDistance(localPosition.z);

            writer[idx++] = _proprioception.Integrity;

            foreach (float contact in _proprioception.Contacts) {
                writer[idx++] = contact;
            }

            foreach (float attach in _proprioception.Attaches) {
                writer[idx++] = attach;
            }

            foreach (float jointBlock in _proprioception.NormalizedJointBlocks) {
                writer[idx++] = jointBlock;
            }

            return _size;
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public ObservationSpec GetObservationSpec() => _observationSpec;

        public void Update() { }
        public void Reset() { }

        private float NormalizeDistance(float distance) => Normalization.Tanh(distance, _expectedMaxDistance);

        private float NormalizeSpeed(float speed) => Normalization.Tanh(speed, _expectedMaxSpeed);

        private float NormalizeThickness(float thickness) => Normalization.Tanh(thickness, _expectedMaxThickness);
    }
}
