using Sever;
using Train.Sever;
using UnityEngine;

namespace Train {
    [RequireComponent(typeof(JointHierarchyBase))]
    public class Proprioception : MonoBehaviour {
        private TrainJointHierachy _jointHierarchy;

        private void Awake() => _jointHierarchy = GetComponent<TrainJointHierachy>();

        public float[] GetJointBlocks(bool normalize = false) {
            float[] jointBlocks = new float[(_jointHierarchy.TotalDoF * 2) + _jointHierarchy.Nodes.Count];
            int index = 0;

            foreach (TrainJointNode node in _jointHierarchy.TrainNodes) {
                if (node.IsSevered) {
                    for (int i = 0; i < node.Body.dofCount; i++) {
                        jointBlocks[index++] = 0.0f;
                    }

                    for (int i = 0; i < node.Body.dofCount; i++) {
                        jointBlocks[index++] = 0.0f;
                    }

                    jointBlocks[index++] = 1.0f;
                } else {
                    float[] jointPositions = node.GetJointPositions(normalize);
                    foreach (float position in jointPositions) {
                        jointBlocks[index++] = position;
                    }

                    float[] jointVelocities = node.GetJointVelocities(normalize);
                    foreach (float velocity in jointVelocities) {
                        jointBlocks[index++] = velocity;
                    }

                    jointBlocks[index++] = 0.0f;
                }
            }

            return jointBlocks;
        }

        public Vector3 GetCoMDiff() =>
            // TODO
            Vector3.zero;

        public Vector3 GetGravity() =>
            _jointHierarchy.RootTrainNode.Body.transform.InverseTransformDirection(Physics.gravity)
                .normalized;

        public Vector3 GetStraightGravity() =>
            Quaternion.Inverse(_jointHierarchy.RootStraightQuat) * Physics.gravity.normalized;

        public Vector3 GetAngularVelocity() =>
            _jointHierarchy.RootTrainNode.Body.angularVelocity;

        public Vector3 GetLinearVelocity() =>
            _jointHierarchy.RootTrainNode.Body.linearVelocity;

        public Vector3 GetPosition() =>
            _jointHierarchy.RootTrainNode.Body.transform.position;

        public float GetIntegrity() =>
            // TODO
            1.0f;

        public Vector3 GetForward() =>
            _jointHierarchy.RootTrainNode.Body.transform.forward;

        public float[] GetContacts() =>
            new float[4];

        public float[] GetAttaches() =>
            new float[4];


        private void OnDrawGizmos() {
            if (!_jointHierarchy) {
                return;
            }

            Vector3 pelvisPosition = _jointHierarchy.RootTrainNode.Body.transform.position;

            Vector3 gravityVector = GetGravity();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(pelvisPosition, gravityVector);

            Vector3 straightGravityVector = GetStraightGravity();
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pelvisPosition, straightGravityVector);
        }
    }
}
