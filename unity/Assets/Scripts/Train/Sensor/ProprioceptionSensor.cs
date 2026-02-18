using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensor {
    public class ProprioceptionSensor : ISensor {
        private readonly ObservationSpec _observationSpec;
        private readonly Proprioception _proprioception;

        private readonly int _size;

        public ProprioceptionSensor(Proprioception proprioception) {
            _proprioception = proprioception;

            _size = 3 + // gravity
                    3 + // CoM
                    3 + // angular velocity
                    3 + // relative linear velocity
                    2 + // projected forward
                    3 + // relative target position
                    1 + // integrity
                    _proprioception.Contacts.Length +
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
            writer[idx++] = Normalization.NormalizeThickness(com.x);
            writer[idx++] = Normalization.NormalizeThickness(com.y);
            writer[idx++] = Normalization.NormalizeThickness(com.z);

            Vector3 angularVelocity = _proprioception.AngularVelocity;
            writer[idx++] = Normalization.NormalizeSpeed(angularVelocity.x);
            writer[idx++] = Normalization.NormalizeSpeed(angularVelocity.y);
            writer[idx++] = Normalization.NormalizeSpeed(angularVelocity.z);

            Vector3 relativeLinearVelocity = _proprioception.RelativeLinearVelocity;
            writer[idx++] = Normalization.NormalizeSpeed(relativeLinearVelocity.x);
            writer[idx++] = Normalization.NormalizeSpeed(relativeLinearVelocity.y);
            writer[idx++] = Normalization.NormalizeSpeed(relativeLinearVelocity.z);

            Vector3 projectedForward = _proprioception.ProjectedForward;
            writer[idx++] = projectedForward.x;
            writer[idx++] = projectedForward.z;

            Vector3 relativeTargetPosition = _proprioception.RelativeTargetPosition;
            writer[idx++] = Normalization.NormalizeDistance(relativeTargetPosition.x);
            writer[idx++] = Normalization.NormalizeDistance(relativeTargetPosition.y);
            writer[idx++] = Normalization.NormalizeDistance(relativeTargetPosition.z);

            writer[idx++] = _proprioception.Integrity;

            foreach (float contact in _proprioception.Contacts) {
                writer[idx++] = contact;
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
    }
}
