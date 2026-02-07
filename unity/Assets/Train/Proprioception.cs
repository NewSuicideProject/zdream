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

        private float[] _jointBlocks;

        private void Awake() => _hierarchy = GetComponent<TrainJointHierarchy>();

        private void Start() {
            _totalDoF = _hierarchy.TrainNodes.Sum(node => node.Body.dofCount);
            contacts = new float[4];
            attaches = new float[4];
            _jointBlocks = new float[(_totalDoF * 2) + _hierarchy.Nodes.Count];

            Update();
            initialCoM = com;
            initialGravity = gravity;
        }

        private void Update() {
            gravity = _hierarchy.RootTrainNode.Body.transform.InverseTransformDirection(Physics.gravity).normalized;

            Vector3 totalWeightedPos = Vector3.zero;
            float totalMass = 0f;
            float totalJoinedMass = 0f;

            foreach (TrainJointNode node in _hierarchy.TrainNodes) {
                float mass = node.Body.mass;
                totalMass += mass;

                if (node.IsSevered) {
                    continue;
                }

                totalWeightedPos += node.Body.worldCenterOfMass * mass;
                totalJoinedMass += mass;
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

        public float[] GetJointBlocks(bool normalize = false) {
            int index = 0;

            foreach (TrainJointNode node in _hierarchy.TrainNodes) {
                if (node.IsSevered) {
                    for (int i = 0; i < node.Body.dofCount; i++) {
                        _jointBlocks[index++] = 0.0f;
                    }

                    for (int i = 0; i < node.Body.dofCount; i++) {
                        _jointBlocks[index++] = 0.0f;
                    }

                    _jointBlocks[index++] = 1.0f;
                } else {
                    float[] jointPositions = node.GetJointPositions(normalize);
                    foreach (float pos in jointPositions) {
                        _jointBlocks[index++] = pos;
                    }

                    float[] jointVelocities = node.GetJointVelocities(normalize);
                    foreach (float vel in jointVelocities) {
                        _jointBlocks[index++] = vel;
                    }

                    _jointBlocks[index++] = 0.0f;
                }
            }

            return _jointBlocks;
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
