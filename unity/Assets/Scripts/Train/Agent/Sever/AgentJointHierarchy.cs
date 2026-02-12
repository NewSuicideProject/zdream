using System.Collections.Generic;
using System.Linq;
using Sever;
using Train.Agent.Scripts.Sever;
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

        protected override JointNodeBase GetJointNode(GameObject joint) {
            AgentJointNode node = new(joint);

            GameObject[] childrenJoint = GetChildrenJoint(joint);
            foreach (GameObject childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint));
            }

            return node;
        }
    }
}
