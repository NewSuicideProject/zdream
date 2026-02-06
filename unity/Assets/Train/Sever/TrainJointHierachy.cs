using System.Collections.Generic;
using System.Linq;
using Sever;
using UnityEngine;

namespace Train.Sever {
    public class TrainJointHierachy : JointHierarchyBase {
        public Quaternion RootStraightQuat { get; private set; }

        public int TotalDoF => TrainJointNodes.Sum(node => node.Body.dofCount);

        public TrainJointNode RootTrainJointNode => (TrainJointNode)RootJointNode;
        public List<TrainJointNode> TrainJointNodes => Nodes.Cast<TrainJointNode>().ToList();

        protected override void Awake() {
            base.Awake();

            RootStraightQuat = RootTrainJointNode.Body.transform.rotation;
        }

        protected override bool IsJoint(GameObject obj) => obj.GetComponent<ArticulationBody>() != null;

        protected override JointNodeBase GetJointNode(GameObject obj, JointNodeBase parent) {
            ArticulationBody body = obj.GetComponent<ArticulationBody>();
            TrainJointNode node = new() { Body = body, LocalIsSevered = false, Parent = parent };

            if (body.dofCount > 0) {
                node.JointLimitCache = new JointLimitCache[body.dofCount];
                for (int i = 0; i < body.dofCount; i++) {
                    ArticulationDrive drive = node.GetDrive(i);
                    node.JointLimitCache[i] = new JointLimitCache {
                        LowerLimit = drive.lowerLimit * Mathf.Deg2Rad, UpperLimit = drive.upperLimit * Mathf.Deg2Rad
                    };
                }
            }

            GameObject[] childrenJoint = GetChildrenJoint(obj);
            foreach (GameObject childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint, node));
            }

            return node;
        }
    }
}
