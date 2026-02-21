using System.Linq;
using Train.Joint;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensor {
    public class ProprioceptionSensor : ISensor {
        private readonly ObservationSpec _observationSpec;
        private readonly Proprioception _proprioception;

        private readonly int _size;

        public ProprioceptionSensor(Proprioception proprioception) {
            _proprioception = proprioception;

            const int totalDoF = ((3 + 1 + 3) * 4) + 3;
            const int nodeCount = 13;

            _size = 3 + // gravity
                    3 + // CoM
                    3 + // angular velocity
                    3 + // projected linear velocity
                    2 + // projected forward
                    3 + // relative target position
                    1 + // integrity
                    nodeCount + (totalDoF * 2) + (3 * nodeCount) + // severed, (positions, velocities), forces
                    totalDoF; // targets
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

            Vector3 projectedLinearVelocity = _proprioception.ProjectedLinearVelocity;
            writer[idx++] = Normalization.NormalizeSpeed(projectedLinearVelocity.x);
            writer[idx++] = Normalization.NormalizeSpeed(projectedLinearVelocity.y);
            writer[idx++] = Normalization.NormalizeSpeed(projectedLinearVelocity.z);

            Vector3 projectedForward = _proprioception.ProjectedForward;
            writer[idx++] = projectedForward.x;
            writer[idx++] = projectedForward.z;

            Vector3 relativeTargetPosition = _proprioception.RelativeTargetPosition;
            writer[idx++] = Normalization.NormalizeDistance(relativeTargetPosition.x);
            writer[idx++] = Normalization.NormalizeDistance(relativeTargetPosition.y);
            writer[idx++] = Normalization.NormalizeDistance(relativeTargetPosition.z);

            writer[idx++] = _proprioception.Integrity;

            foreach (AgentJointNode node in _proprioception.TrainNodes.Skip(1)) {
                if (node.IsSevered) {
                    writer[idx++] = 1f; // severed
                    for (int i = 0; i < (node.DoF * 2) + 3; i++) {
                        writer[idx++] = 0f;
                    }

                    continue;
                }

                writer[idx++] = 0f; // not severed

                ArticulationReducedSpace positions = node.Body.jointPosition;
                ArticulationReducedSpace velocities = node.Body.jointVelocity;
                Vector3 force = node.Force.Value;

                for (int i = 0; i < node.DoF; i++) {
                    ArticulationDrive drive = node.GetDrive(i);
                    writer[idx++] = Normalization.NormalizeJointPosition(positions[i],
                        drive.lowerLimit * Mathf.Deg2Rad, drive.upperLimit * Mathf.Deg2Rad);
                }

                for (int i = 0; i < node.DoF; i++) {
                    writer[idx++] = Normalization.NormalizeSpeed(velocities[i]);
                }

                writer[idx++] = Normalization.NormalizeForce(force.x);
                writer[idx++] = Normalization.NormalizeForce(force.y);
                writer[idx++] = Normalization.NormalizeForce(force.z);
            }

            foreach (float target in _proprioception.Targets) {
                writer[idx++] = target;
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
