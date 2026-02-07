using System.Linq;
using Sever;
using Train.Sever;
using UnityEngine;

namespace Train {
    [RequireComponent(typeof(JointHierarchyBase))]
    public class Proprioception : MonoBehaviour {
        private TrainJointHierarchy _hierarchy;
        [SerializeField] [ReadOnly] private Vector3 initialGravity;
        public Vector3 InitialGravity => initialGravity;
        [SerializeField] [ReadOnly] private Vector3 initialCoM;
        public Vector3 InitialCoM => initialCoM;
        private int _totalDoF;
        [SerializeField] [ReadOnly] private Vector3 com;
        public Vector3 Com => com;
        [SerializeField] [ReadOnly] private Vector3 gravity;
        public Vector3 Gravity => gravity;
        [SerializeField] [ReadOnly] private Vector3 angularVelocity;
        public Vector3 AngularVelocity => angularVelocity;
        [SerializeField] [ReadOnly] private Vector3 linearVelocity;
        public Vector3 LinearVelocity => linearVelocity;
        [SerializeField] [ReadOnly] private Vector3 position;
        public Vector3 Position => position;
        [SerializeField] [ReadOnly] private float integrity;
        public float Integrity => integrity;
        [SerializeField] [ReadOnly] private Vector3 forward;
        public Vector3 Forward => forward;
        [SerializeField] [ReadOnly] private float[] contacts;
        public float[] Contacts => contacts;
        [SerializeField] [ReadOnly] private float[] attaches;
        public float[] Attaches => attaches;
        [SerializeField] [ReadOnly] private float[] jointBlocks;
        public float[] JointBlocks => jointBlocks;
        [SerializeField] [ReadOnly] private float[] normalizedJointBlocks;
        public float[] NormalizedJointBlocks => normalizedJointBlocks;

        private void Awake() => _hierarchy = GetComponent<TrainJointHierarchy>();

        private void Start() {
            _totalDoF = _hierarchy.TrainNodes.Sum(node => node.DoF);
            contacts = new float[4];
            attaches = new float[4];
            jointBlocks = new float[(_totalDoF * 2) + _hierarchy.TrainNodes.Count];
            normalizedJointBlocks = new float[(_totalDoF * 2) + _hierarchy.TrainNodes.Count];

            Update();
            initialCoM = com;
            initialGravity = gravity;
        }

        private void Update() {
            gravity = _hierarchy.RootTrainNode.Body.transform.InverseTransformDirection(Physics.gravity).normalized;

            Vector3 totalWeightedPos = Vector3.zero;
            float totalMass = 0f;
            float totalJoinedMass = 0f;
            int index = 0;

            foreach (TrainJointNode node in _hierarchy.TrainNodes) {
                float mass = node.Body.mass;
                totalMass += mass;

                jointBlocks[index] = node.IsSevered ? 1.0f : 0.0f;
                normalizedJointBlocks[index++] = node.IsSevered ? 1.0f : 0.0f;

                if (node.IsSevered) {
                    for (int i = 0; i < node.DoF; i++) {
                        jointBlocks[index] = 0.0f;
                        normalizedJointBlocks[index++] = 0.0f;
                    }

                    for (int i = 0; i < node.DoF; i++) {
                        jointBlocks[index] = 0.0f;
                        normalizedJointBlocks[index++] = 0.0f;
                    }
                } else {
                    totalWeightedPos += node.Body.worldCenterOfMass * mass;
                    totalJoinedMass += mass;
                    float[] jointPositions = node.GetJointPositions();
                    float[] normalizedJointPositions = node.GetJointPositions(true);
                    for (int i = 0; i < node.DoF; i++) {
                        jointBlocks[index] = jointPositions[i];
                        normalizedJointBlocks[index++] = normalizedJointPositions[i];
                    }

                    float[] jointVelocities = node.GetJointVelocities();
                    float[] normalizedJointVelocities = node.GetJointVelocities(true);
                    for (int i = 0; i < node.DoF; i++) {
                        jointBlocks[index] = jointVelocities[i];
                        normalizedJointBlocks[index++] = normalizedJointVelocities[i];
                    }
                }
            }

            com = _hierarchy.RootTrainNode.Body.transform.InverseTransformPoint(
                totalJoinedMass > 0f
                    ? totalWeightedPos / totalJoinedMass
                    : Vector3.zero);

            angularVelocity = _hierarchy.RootTrainNode.Body.angularVelocity;
            linearVelocity = _hierarchy.RootTrainNode.Body.linearVelocity;
            position = _hierarchy.RootTrainNode.Body.transform.position;
            forward = _hierarchy.RootTrainNode.Body.transform.forward;

            integrity = totalMass > 0f ? totalJoinedMass / totalMass : 0f;
        }

        private void OnDrawGizmos() {
            if (!_hierarchy) {
                return;
            }

            Vector3 pelvisPosition = _hierarchy.RootTrainNode.Body.transform.position;

            Gizmos.color = Color.green;
            Gizmos.DrawRay(pelvisPosition, gravity * 0.25f);

            Gizmos.color = Color.darkGreen;
            Gizmos.DrawRay(pelvisPosition, initialGravity * 0.25f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(pelvisPosition, com);

            Gizmos.color = Color.darkBlue;
            Gizmos.DrawRay(pelvisPosition, initialCoM);

            Gizmos.color = Color.lightBlue;
            Gizmos.DrawRay(pelvisPosition, com - initialCoM);
        }
    }
}
