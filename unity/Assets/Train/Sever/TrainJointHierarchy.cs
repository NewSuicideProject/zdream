using System.Collections.Generic;
using System.Linq;
using Sever;
using UnityEngine;

namespace Train.Sever {
    public class TrainJointHierarchy : JointHierarchyBase {
        public TrainJointNode RootTrainNode;
        public List<TrainJointNode> TrainNodes;

        protected override void Awake() {
            base.Awake();

            RootTrainNode = (TrainJointNode)RootNode;

            TrainNodes = Nodes.Cast<TrainJointNode>().ToList();
        }

        protected override bool IsJoint(GameObject candidate) => candidate.GetComponent<ArticulationBody>() != null;

        protected override JointNodeBase GetJointNode(GameObject joint) {
            TrainJointNode node = new(joint);

            GameObject[] childrenJoint = GetChildrenJoint(joint);
            foreach (GameObject childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint));
            }

            return node;
        }
    }
}
