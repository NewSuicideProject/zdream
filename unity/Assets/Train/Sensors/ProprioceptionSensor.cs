using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensors {
    public class ProprioceptionSensor : ISensor {
        private readonly Proprioception _proprioception;
        private readonly ObservationSpec _observationSpec;

        private readonly float _expectedMaxSpeed;
        private readonly float _expectedMaxDistance;

        public string GetName() => "proprioception";

        public ProprioceptionSensor(Proprioception proprioception,
            float expectedMaxSpeed = 20f,
            float expectedMaxDistance = 20f) {
            _proprioception = proprioception;
            _expectedMaxSpeed = expectedMaxSpeed;
            _expectedMaxDistance = expectedMaxDistance;

            int size =
                3 + // gravity
                3 + // CoM diff
                3 + // angular velocity
                3 + // linear velocity
                3 + // position
                3 + // forward
                1 + // integrity
                _proprioception.Contacts.Length +
                _proprioception.Attaches.Length +
                _proprioception.NormalizedJointBlocks.Length;

            _observationSpec = ObservationSpec.Vector(size);
        }

        private float NormalizeDistance(float distance) => Normalization.Tanh(distance, _expectedMaxDistance);

        private float NormalizeSpeed(float speed) => Normalization.Tanh(speed, _expectedMaxSpeed);

        public int Write(ObservationWriter writer) {
            int idx = 0;

            Vector3 gravity = _proprioception.Gravity;
            writer[idx++] = gravity.x;
            writer[idx++] = gravity.y;
            writer[idx++] = gravity.z;

            Vector3 comDiff = _proprioception.Com - _proprioception.InitialCoM;
            writer[idx++] = NormalizeDistance(comDiff.x);
            writer[idx++] = NormalizeDistance(comDiff.y);
            writer[idx++] = NormalizeDistance(comDiff.z);

            Vector3 angularVelocity = _proprioception.AngularVelocity;
            writer[idx++] = NormalizeSpeed(angularVelocity.x);
            writer[idx++] = NormalizeSpeed(angularVelocity.y);
            writer[idx++] = NormalizeSpeed(angularVelocity.z);

            Vector3 linearVelocity = _proprioception.LinearVelocity;
            writer[idx++] = NormalizeSpeed(linearVelocity.x);
            writer[idx++] = NormalizeSpeed(linearVelocity.y);
            writer[idx++] = NormalizeSpeed(linearVelocity.z);

            Vector3 position = _proprioception.Position;
            writer[idx++] = NormalizeDistance(position.x);
            writer[idx++] = NormalizeDistance(position.y);
            writer[idx++] = NormalizeDistance(position.z);

            Vector3 forward = _proprioception.Forward;
            writer[idx++] = forward.x;
            writer[idx++] = forward.y;
            writer[idx++] = forward.z;

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

            return idx;
        }

        public byte[] GetCompressedObservation() => null;
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public ObservationSpec GetObservationSpec() => _observationSpec;

        public void Update() { }
        public void Reset() { }
    }
}
