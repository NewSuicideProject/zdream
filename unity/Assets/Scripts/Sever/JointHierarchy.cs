using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sever {
    public class JointHierarchy : MonoBehaviour {
        public Quaternion RootStraightQuat { get; private set; }
        public JointNode RootJointNode { get; private set; }

        public List<JointNode> Nodes;

        public int TotalDoF => Nodes.Sum(node => node.Body.dofCount);

        private void Awake() {
            ArticulationBody pelvis = GetComponent<ArticulationBody>();
            if (!pelvis) {
                pelvis = GetComponentInChildren<ArticulationBody>();
            }

            if (!pelvis) {
                Debug.LogError("No ArticulationBody found!");
                return;
            }


            RootJointNode = JointNode.GetJointNode(pelvis, null);

            RootStraightQuat = RootJointNode.Body.transform.rotation;

            Nodes = new List<JointNode>();
            GetNodes(RootJointNode);
        }

        private void GetNodes(JointNode node) {
            Nodes.Add(node);

            foreach (JointNode child in node.Children) {
                GetNodes(child);
            }
        }
    }
}
