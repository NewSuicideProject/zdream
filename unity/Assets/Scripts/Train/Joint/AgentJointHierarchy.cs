using System.Collections.Generic;
using System.Linq;
using Joint;
using UnityEngine;

namespace Train.Joint {
    public class AgentJointHierarchy : JointHierarchyBase {
        public AgentJointNode RootAgentNode;
        public List<AgentJointNode> AgentNodes;

        private Vector3 _initialRootLocalPosition;
        private Quaternion _initialRootLocalRotation;

        protected override void Awake() {
            base.Awake();

            RootAgentNode = (AgentJointNode)RootNode;
            _initialRootLocalPosition = RootAgentNode.GameObject.transform.localPosition;
            _initialRootLocalRotation = RootAgentNode.GameObject.transform.localRotation;

            AgentNodes = Nodes.Cast<AgentJointNode>().ToList();
        }

        protected override bool IsJoint(GameObject candidate) => candidate.GetComponent<ArticulationBody>() != null;

        protected override JointNodeBase GetJointNode(GameObject joint, JointNodeBase parent) {
            AgentJointNode node = new(joint, (AgentJointNode)parent);

            GameObject[] childrenJoint = GetChildrenJoint(joint);
            foreach (GameObject childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint, node));
            }

            return node;
        }

        private void FixedUpdate() {
            if (Config.Assist.GravityAssist > 0f) {
                RootAgentNode.Body.AddForce(Vector3.up * Config.Assist.GravityAssist, ForceMode.Acceleration);
            }

            if (Config.Assist.UprightAssist > 0f) {
            }
        }

        public override void OnEpisodeBegin() {
            RootAgentNode.Body.TeleportRoot(
                RootAgentNode.GameObject.transform.parent.TransformPoint(_initialRootLocalPosition),
                RootAgentNode.GameObject.transform.parent.rotation * _initialRootLocalRotation);
            base.OnEpisodeBegin();
        }
    }
}
