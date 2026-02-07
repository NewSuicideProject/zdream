using System;
using System.Linq;
using Sever;
using Train.Sever;
using UnityEngine;

namespace Train {
    [RequireComponent(typeof(JointHierarchyBase))]
    public class Proprioception : MonoBehaviour {
        private TrainJointHierarchy _hierarchy;
        private Quaternion _rootInitialQuat;
        private Vector3 _rootInitialCoM;
        [NonSerialized] private int _totalDoF;

        private void Awake() => _hierarchy = GetComponent<TrainJointHierarchy>();

        private void Start() {
            _rootInitialQuat = _hierarchy.RootTrainNode.Body.transform.rotation;
            _rootInitialCoM = _hierarchy.RootTrainNode.Body.centerOfMass;
            _totalDoF = _hierarchy.TrainNodes.Sum(node => node.Body.dofCount);
        }

        public float[] GetJointBlocks(bool normalize = false) {
            float[] jointBlocks = new float[(_totalDoF * 2) + _hierarchy.Nodes.Count];
            int index = 0;

            foreach (TrainJointNode node in _hierarchy.TrainNodes) {
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
            _hierarchy.RootTrainNode.Body.centerOfMass - _rootInitialCoM;

        public Vector3 GetGravity() =>
            _hierarchy.RootTrainNode.Body.transform.InverseTransformDirection(Physics.gravity)
                .normalized;

        public Vector3 GetInitialGravity() =>
            Quaternion.Inverse(_rootInitialQuat) * Physics.gravity.normalized;

        public Vector3 GetAngularVelocity() =>
            _hierarchy.RootTrainNode.Body.angularVelocity;

        public Vector3 GetLinearVelocity() =>
            _hierarchy.RootTrainNode.Body.linearVelocity;

        public Vector3 GetPosition() =>
            _hierarchy.RootTrainNode.Body.transform.position;

        public float GetIntegrity() {
            float totalMass = 0f;
            float intactMass = 0f;

            foreach (TrainJointNode node in _hierarchy.TrainNodes) {
                totalMass += node.Body.mass;
                if (!node.IsSevered) {
                    intactMass += node.Body.mass;
                }
            }

            return totalMass > 0f ? intactMass / totalMass : 0f;
        }

        public Vector3 GetForward() =>
            _hierarchy.RootTrainNode.Body.transform.forward;

        public float[] GetContacts() =>
            new float[4];

        public float[] GetAttaches() =>
            new float[4];


        private void OnDrawGizmos() {
            if (!_hierarchy) {
                return;
            }

            Vector3 pelvisPosition = _hierarchy.RootTrainNode.Body.transform.position;

            Vector3 gravity = GetGravity();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(pelvisPosition, gravity);

            Vector3 initialGravity = GetInitialGravity();
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pelvisPosition, initialGravity);

            Vector3 comDiff = GetCoMDiff();
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(pelvisPosition, comDiff);
        }
    }
}
