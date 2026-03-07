using System.Collections.Generic;
using System.Linq;
using Joint;
using UnityEngine;

namespace Train.Joint {
    public class AgentJointHierarchy : JointHierarchyBase {
        public AgentJointNode RootAgentNode;
        public List<AgentJointNode> AgentNodes;

        public int TotalDoF {
            get;
            private set;
        }

        public float TotalMass {
            get;
            private set;
        }

        private Vector3 _initialRootLocalPosition;
        private Quaternion _initialRootLocalRotation;

        protected override void Awake() {
            base.Awake();

            RootAgentNode = (AgentJointNode)RootNode;
            _initialRootLocalPosition = RootAgentNode.Transform.localPosition;
            _initialRootLocalRotation = RootAgentNode.Transform.localRotation;

            AgentNodes = Nodes.Cast<AgentJointNode>().ToList();
            TotalDoF = AgentNodes.Sum(node => node.DoF);
            TotalMass = AgentNodes.Sum(node => node.Body.mass);
        }

        protected override bool IsJoint(Transform candidate) => candidate.GetComponent<ArticulationBody>() != null;

        protected override JointNodeBase GetJointNode(Transform joint, JointNodeBase parent) {
            AgentJointNode node = new(joint, (AgentJointNode)parent);

            Transform[] childrenJoint = GetChildrenJoint(joint);
            foreach (Transform childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint, node));
            }

            return node;
        }

        private void FixedUpdate() {
            ArticulationBody rootBody = RootAgentNode.Body;
            float heightAssist = Config.Assist.HeightAssist;
            float rotationAssist = Config.Assist.RotationAssist;

            if (heightAssist > 0f) {
                Vector3 targetWorldPos = RootAgentNode.Transform.parent.TransformPoint(_initialRootLocalPosition);
                float heightError = targetWorldPos.y - rootBody.transform.position.y;

                float kp = 50f * heightAssist;
                float kd = 5f * Mathf.Sqrt(kp);

                float verticalAccel = (heightError * kp) - (rootBody.linearVelocity.y * kd);
                rootBody.AddForce(Vector3.up * verticalAccel, ForceMode.Acceleration);
            }

            if (rotationAssist > 0f) {
                Quaternion targetWorldRot = RootAgentNode.Transform.parent.rotation * _initialRootLocalRotation;

                Quaternion rotationError = targetWorldRot * Quaternion.Inverse(rootBody.transform.rotation);
                rotationError.ToAngleAxis(out float angle, out Vector3 axis);

                if (angle > 180f) {
                    angle -= 360f;
                }

                if (Mathf.Abs(angle) > 0.01f) {
                    float kp = 500f * rotationAssist;
                    float kd = 50f * Mathf.Sqrt(kp);

                    Vector3 torqueAccel = (axis.normalized * (angle * Mathf.Deg2Rad * kp)) -
                                          (rootBody.angularVelocity * kd);
                    rootBody.AddTorque(torqueAccel, ForceMode.Acceleration);
                }
            }
        }

        public override void OnEpisodeBegin() {
            RootAgentNode.Body.TeleportRoot(
                RootAgentNode.Transform.parent.TransformPoint(_initialRootLocalPosition),
                RootAgentNode.Transform.parent.rotation * _initialRootLocalRotation
            );
            base.OnEpisodeBegin();
        }
    }
}
