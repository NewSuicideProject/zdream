using System.Collections.Generic;
using System.Linq;
using Joint;
using UnityEngine;

namespace Train.Joint {
    public class AgentJointHierarchy : JointHierarchyBase {
        public AgentJointNode RootAgentNode;
        public List<AgentJointNode> AgentNodes;

        public int TotalDoF { get; private set; }
        public float TotalMass { get; private set; }

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
            float mass = TotalMass;

            if (heightAssist > 0f) {
                Vector3 targetWorldPos = RootAgentNode.Transform.parent.TransformPoint(_initialRootLocalPosition);
                float heightError = targetWorldPos.y - rootBody.transform.position.y;

                float kp = 150f * heightAssist;
                float kd = 2f * Mathf.Sqrt(kp);

                float verticalForce = mass * ((heightError * kp) - (rootBody.linearVelocity.y * kd));
                rootBody.AddForce(Vector3.up * verticalForce, ForceMode.Force);
            }

            if (rotationAssist > 0f) {
                Quaternion targetWorldRot = RootAgentNode.Transform.parent.rotation * _initialRootLocalRotation;
                Quaternion deltaRot = targetWorldRot * Quaternion.Inverse(rootBody.transform.rotation);
                deltaRot.ToAngleAxis(out float angle, out Vector3 axis);

                if (angle > 180f) {
                    angle -= 360f;
                }

                if (!(Mathf.Abs(angle) > 0.01f)) {
                    return;
                }

                float kp = 50f * rotationAssist;
                float kd = 2f * Mathf.Sqrt(kp);

                Vector3 torque = mass * ((axis.normalized * (angle * Mathf.Deg2Rad * kp)) -
                                         (rootBody.angularVelocity * kd));
                rootBody.AddTorque(torque, ForceMode.Force);
            }
        }

        public override void OnEpisodeBegin() {
            RootAgentNode.Body.TeleportRoot(
                RootAgentNode.Transform.parent.TransformPoint(_initialRootLocalPosition),
                RootAgentNode.Transform.parent.rotation * _initialRootLocalRotation);
            base.OnEpisodeBegin();
        }
    }
}
