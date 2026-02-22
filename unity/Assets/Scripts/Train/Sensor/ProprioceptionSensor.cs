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
                    3 + // root force
                    nodeCount + (totalDoF * 3) + (3 * nodeCount); // severed, (positions, targets, velocities), forces
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
            writer[idx++] = Normalize.Thickness(com.x);
            writer[idx++] = Normalize.Thickness(com.y);
            writer[idx++] = Normalize.Thickness(com.z);

            Vector3 angularVelocity = _proprioception.AngularVelocity;
            writer[idx++] = Normalize.Speed(angularVelocity.x);
            writer[idx++] = Normalize.Speed(angularVelocity.y);
            writer[idx++] = Normalize.Speed(angularVelocity.z);

            Vector3 projectedLinearVelocity = _proprioception.ProjectedLinearVelocity;
            writer[idx++] = Normalize.Speed(projectedLinearVelocity.x);
            writer[idx++] = Normalize.Speed(projectedLinearVelocity.y);
            writer[idx++] = Normalize.Speed(projectedLinearVelocity.z);

            Vector3 projectedForward = _proprioception.ProjectedForward;
            writer[idx++] = projectedForward.x;
            writer[idx++] = projectedForward.z;

            Vector3 relativeTargetPosition = _proprioception.RelativeTargetPosition;
            writer[idx++] = Normalize.Distance(relativeTargetPosition.x);
            writer[idx++] = Normalize.Distance(relativeTargetPosition.y);
            writer[idx++] = Normalize.Distance(relativeTargetPosition.z);

            writer[idx++] = _proprioception.Integrity;

            Vector3 rootForce = _proprioception.Hierarchy.RootAgentNode.Force.Value;
            writer[idx++] = Normalize.Force(rootForce.x);
            writer[idx++] = Normalize.Force(rootForce.y);
            writer[idx++] = Normalize.Force(rootForce.z);

            foreach (AgentJointNode node in _proprioception.Hierarchy.AgentNodes.Skip(1)) {
                if (node.IsSevered) {
                    writer[idx++] = 1f; // severed
                    for (int i = 0; i < (node.DoF * 3) + 3; i++) {
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
                    float position = positions[i] * Mathf.Rad2Deg;
                    writer[idx++] = Normalize.JointPosition(drive.target, drive.lowerLimit, drive.upperLimit);
                    writer[idx++] = Normalize.JointPosition(position, drive.lowerLimit, drive.upperLimit);
                    writer[idx++] = Normalize.Speed(velocities[i]);
                }

                writer[idx++] = Normalize.Force(force.x);
                writer[idx++] = Normalize.Force(force.y);
                writer[idx++] = Normalize.Force(force.z);
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
