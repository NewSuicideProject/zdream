using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointHierarchyBase : MonoBehaviour {
        protected JointNodeBase RootJointNode { get; private set; }

        public List<JointNodeBase> Nodes;

        protected virtual bool IsJoint(GameObject obj) => true;

        protected GameObject[] GetChildrenJoint(GameObject obj) {
            List<GameObject> children = new();
            for (int i = 0; i < obj.transform.childCount; i++) {
                GameObject child = obj.transform.GetChild(i).gameObject;
                if (IsJoint(child)) {
                    children.Add(child);
                }
            }

            return children.ToArray();
        }

        protected virtual void Awake() {
            GameObject rootObj = IsJoint(gameObject)
                ? gameObject
                : GetChildrenJoint(gameObject)[0];

            RootJointNode = GetJointNode(rootObj, null);

            Nodes = new List<JointNodeBase>();
            GetNodes(RootJointNode);
        }

        private void GetNodes(JointNodeBase node) {
            Nodes.Add(node);

            foreach (JointNodeBase child in node.Children) {
                GetNodes(child);
            }
        }


        protected virtual JointNodeBase GetJointNode(GameObject obj, JointNodeBase parent) {
            JointNodeBase node = new() { GameObject = obj, LocalIsSevered = false, Parent = parent };
            GameObject[] childrenJoint = GetChildrenJoint(obj);

            foreach (GameObject childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint, node));
            }

            return node;
        }
    }
}
