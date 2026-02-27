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
            float uprightAssist = Config.Assist.UprightAssist;

            if (Config.Assist.GravityAssist > 0f) {
                rootBody.AddForce(-Physics.gravity * Config.Assist.GravityAssist, ForceMode.Acceleration);
            }

            if (!(uprightAssist > 0f)) {
                return;
            }

            Quaternion targetWorldRot = RootAgentNode.Transform.parent.rotation * _initialRootLocalRotation;
            Quaternion deltaRot = targetWorldRot * Quaternion.Inverse(rootBody.transform.rotation);
            deltaRot.ToAngleAxis(out float angle, out Vector3 axis);

            if (angle > 180f) {
                angle -= 360f;
            }

            if (Mathf.Abs(angle) > 0.01f) {
                float rotKp = 50f * uprightAssist;
                float rotKd = 2f * Mathf.Sqrt(rotKp);
                Vector3 torque = (axis.normalized * (angle * Mathf.Deg2Rad * rotKp)) -
                                 (rootBody.angularVelocity * rotKd);
                rootBody.AddTorque(torque, ForceMode.Acceleration);
            }

            Vector3 targetWorldPos = RootAgentNode.Transform.parent.TransformPoint(_initialRootLocalPosition);
            Vector3 posError = targetWorldPos - rootBody.transform.position;
            posError.y = 0f;

            float horizontalKp = 20f * uprightAssist;
            float horizontalKd = 2f * Mathf.Sqrt(horizontalKp);

            Vector3 horizontalForce = (posError * horizontalKp) - (rootBody.linearVelocity * horizontalKd);
            horizontalForce.y = 0f;

            rootBody.AddForce(horizontalForce, ForceMode.Acceleration);
        }

        public override void OnEpisodeBegin() {
            RootAgentNode.Body.TeleportRoot(
                RootAgentNode.Transform.parent.TransformPoint(_initialRootLocalPosition),
                RootAgentNode.Transform.parent.rotation * _initialRootLocalRotation);
            base.OnEpisodeBegin();
        }
    }
}
