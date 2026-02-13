using System.Collections.Generic;
using System.Linq;
using Sever;
using UnityEngine;

namespace Train.Agent.Sever {
    public class AgentJointHierarchy : JointHierarchyBase {
        public AgentJointNode RootAgentNode;
        public List<AgentJointNode> TrainNodes;

        protected override void Awake() {
            base.Awake();

            RootAgentNode = (AgentJointNode)RootNode;

            TrainNodes = Nodes.Cast<AgentJointNode>().ToList();
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
    }
}
