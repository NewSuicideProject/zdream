using Train.Joint;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train.Sensor {
    [RequireComponent(typeof(AgentJointHierarchy))]
    public class Proprioception : SensorComponent {
        [SerializeField] [ReadOnly] private Vector3 initialGravity;
        [SerializeField] [ReadOnly] private Vector3 com;
        [SerializeField] [ReadOnly] private Vector3 gravity;
        [SerializeField] [ReadOnly] private Vector3 angularVelocity;
        [SerializeField] [ReadOnly] private Vector3 projectedLinearVelocity;
        [SerializeField] [ReadOnly] private float integrity;
        [SerializeField] [ReadOnly] private Vector3 projectedForward;
        [SerializeField] [ReadOnly] private Vector3 relativeTargetPosition;

        private ProprioceptionSensor _proprioceptionSensor;

        public override ISensor[] CreateSensors() {
            _proprioceptionSensor = new ProprioceptionSensor(this);
            return new ISensor[] { _proprioceptionSensor };
        }

        public Vector3 InitialGravity => initialGravity;
        public Vector3 Com => com;
        public Vector3 Gravity => gravity;
        public Vector3 AngularVelocity => angularVelocity;
        public Vector3 ProjectedLinearVelocity => projectedLinearVelocity;
        public float Integrity => integrity;
        public Vector3 ProjectedForward => projectedForward;
        public Vector3 RelativeTargetPosition => relativeTargetPosition;
        public AgentJointHierarchy Hierarchy { get; private set; }

        public Transform targetTransform;

        private void Awake() => Hierarchy = GetComponent<AgentJointHierarchy>();

        private void Start() {
            FixedUpdate();
            initialGravity = gravity;
        }

        private void FixedUpdate() {
            Transform rootTransform = Hierarchy.RootAgentNode.GameObject.transform;
            ArticulationBody rootBody = Hierarchy.RootAgentNode.Body;

            gravity = rootTransform.InverseTransformDirection(Physics.gravity).normalized;

            Vector3 totalWeightedPos = Vector3.zero;
            float totalMass = 0f;
            float totalJoinedMass = 0f;

            foreach (AgentJointNode node in Hierarchy.AgentNodes) {
                float mass = node.Body.mass;
                totalMass += mass;

                if (node.IsSevered) {
                    continue;
                }

                totalWeightedPos += node.Body.worldCenterOfMass * mass;
                totalJoinedMass += mass;
            }

            com = rootTransform.InverseTransformPoint(totalJoinedMass > 0f
                ? totalWeightedPos / totalJoinedMass
                : Vector3.zero);

            projectedForward = Vector3.ProjectOnPlane(rootTransform.forward, Vector3.up);
            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = Vector3.forward;
            } else {
                projectedForward.Normalize();
            }

            Quaternion inverseYaw = Quaternion.Inverse(Quaternion.LookRotation(projectedForward, Vector3.up));

            angularVelocity = rootTransform.InverseTransformDirection(rootBody.angularVelocity);

            projectedLinearVelocity = inverseYaw * rootBody.linearVelocity;
            relativeTargetPosition =
                targetTransform ? inverseYaw * (targetTransform.position - rootTransform.position) : Vector3.zero;

            integrity = totalMass > 0f ? totalJoinedMass / totalMass : 0f;
        }

        private void OnDrawGizmos() {
            if (!Hierarchy) {
                return;
            }

            Vector3 pelvisPosition = Hierarchy.RootAgentNode.Body.transform.position;
            Transform pelvisTransform = Hierarchy.RootAgentNode.Body.transform;

            Gizmos.color = Color.lightGreen;
            Gizmos.DrawRay(pelvisPosition, pelvisTransform.TransformDirection(gravity) * 0.5f);

            Gizmos.color = Color.darkGreen;
            Gizmos.DrawRay(pelvisPosition, pelvisTransform.TransformDirection(initialGravity) * 0.5f);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pelvisPosition, pelvisTransform.TransformPoint(com));

            Gizmos.color = Color.red;
            Gizmos.DrawRay(pelvisPosition, projectedForward * 0.5f);
        }
    }
}
