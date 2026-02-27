using System.Collections.Generic;
using System.Linq;
using Joint;
using UnityEngine;

namespace Train.Joint {
    public class AgentJointHierarchy : JointHierarchyBase {
        public AgentJointNode RootAgentNode;
        public List<AgentJointNode> AgentNodes;

        private Vector3 _initialRootRelativePosition;
        private Quaternion _initialRootRelativeRotation;

        protected override void Awake() {
            base.Awake();

            RootAgentNode = (AgentJointNode)RootNode;
            _initialRootRelativePosition = RootAgentNode.GameObject.transform.localPosition;
            _initialRootRelativeRotation = RootAgentNode.GameObject.transform.localRotation;

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

        public override void OnEpisodeBegin() {
            RootAgentNode.Body.TeleportRoot(transform.TransformPoint(_initialRootRelativePosition),
                transform.rotation * _initialRootRelativeRotation);
            base.OnEpisodeBegin();
        }
    }
}
