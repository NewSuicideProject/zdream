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

        protected override bool IsJoint(GameObject obj) => obj.GetComponent<ArticulationBody>() != null;

        protected override JointNodeBase GetJointNode(GameObject obj, JointNodeBase parent) {
            TrainJointNode node = new(obj, parent);

            GameObject[] childrenJoint = GetChildrenJoint(obj);
            foreach (GameObject childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint, node));
            }

            return node;
        }
    }
}
