using System.Linq;
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
        [SerializeField] [ReadOnly] private Vector3 relativeLinearVelocity;
        [SerializeField] [ReadOnly] private float integrity;
        [SerializeField] [ReadOnly] private Vector3 projectedForward;
        [SerializeField] [ReadOnly] private Vector3 relativeTargetPosition;
        [SerializeField] [ReadOnly] private float[] contacts;
        [SerializeField] [ReadOnly] private float[] jointBlocks;
        [SerializeField] [ReadOnly] private float[] normalizedJointBlocks;

        private ProprioceptionSensor _proprioceptionSensor;

        public override ISensor[] CreateSensors() {
            _proprioceptionSensor = new ProprioceptionSensor(this);
            return new ISensor[] { _proprioceptionSensor };
        }

        public Vector3 InitialGravity => initialGravity;
        public Vector3 Com => com;
        public Vector3 Gravity => gravity;
        public Vector3 AngularVelocity => angularVelocity;
        public Vector3 RelativeLinearVelocity => relativeLinearVelocity;
        public float Integrity => integrity;
        public Vector3 ProjectedForward => projectedForward;
        public Vector3 RelativeTargetPosition => relativeTargetPosition;
        public float[] Contacts => contacts;
        public float[] JointBlocks => jointBlocks;
        public float[] NormalizedJointBlocks => normalizedJointBlocks;

        private AgentJointHierarchy _hierarchy;
        public Transform targetTransform;
        private int _totalDoF;

        private void Awake() => _hierarchy = GetComponent<AgentJointHierarchy>();

        private void Start() {
            _totalDoF = _hierarchy.TrainNodes.Sum(node => node.DoF);
            contacts = new float[4];
            jointBlocks = new float[(_totalDoF * 2) + _hierarchy.TrainNodes.Count];
            normalizedJointBlocks = new float[(_totalDoF * 2) + _hierarchy.TrainNodes.Count];

            FixedUpdate();
            initialGravity = gravity;
        }

        private void FixedUpdate() {
            Transform rootTransform = _hierarchy.RootAgentNode.GameObject.transform;
            ArticulationBody rootBody = _hierarchy.RootAgentNode.Body;

            gravity = rootTransform.InverseTransformDirection(Physics.gravity).normalized;

            Vector3 totalWeightedPos = Vector3.zero;
            float totalMass = 0f;
            float totalJoinedMass = 0f;
            int baseIndex = 0;

            foreach (AgentJointNode node in _hierarchy.TrainNodes) {
                jointBlocks[baseIndex] = node.IsSevered ? 1.0f : 0.0f;
                normalizedJointBlocks[baseIndex++] = node.IsSevered ? 1.0f : 0.0f;

                node.GetJointPositions(jointBlocks, baseIndex);
                node.GetJointPositions(normalizedJointBlocks, baseIndex, true);
                baseIndex += node.DoF;

                node.GetJointVelocities(jointBlocks, baseIndex);
                node.GetJointVelocities(normalizedJointBlocks, baseIndex, true);
                baseIndex += node.DoF;

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

            Vector3 forward = rootTransform.forward;
            Vector3 position = rootTransform.position;

            projectedForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (projectedForward.sqrMagnitude < 0.001f) {
                projectedForward = Vector3.forward;
            } else {
                projectedForward.Normalize();
            }

            Quaternion yawQuat = Quaternion.LookRotation(projectedForward, Vector3.up);
            Quaternion inverseYaw = Quaternion.Inverse(yawQuat);

            angularVelocity = rootTransform.InverseTransformDirection(rootBody.angularVelocity);

            relativeLinearVelocity = inverseYaw * rootBody.linearVelocity;
            relativeTargetPosition =
                targetTransform ? inverseYaw * (targetTransform.position - position) : Vector3.zero;

            integrity = totalMass > 0f ? totalJoinedMass / totalMass : 0f;
        }

        private void OnDrawGizmos() {
            if (!_hierarchy) {
                return;
            }

            Vector3 pelvisPosition = _hierarchy.RootAgentNode.Body.transform.position;
            Transform pelvisTransform = _hierarchy.RootAgentNode.Body.transform;

            Gizmos.color = Color.lightGreen;
            Gizmos.DrawRay(pelvisPosition, pelvisTransform.TransformDirection(gravity) * 0.5f);

            Gizmos.color = Color.darkGreen;
            Gizmos.DrawRay(pelvisPosition, pelvisTransform.TransformDirection(initialGravity) * 0.5f);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pelvisPosition, pelvisTransform.TransformPoint(com));

            Gizmos.color = Color.lightCoral;
            Gizmos.DrawRay(pelvisPosition, projectedForward * 0.5f);
        }
    }
}
