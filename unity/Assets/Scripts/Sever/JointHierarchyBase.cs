using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointHierarchyBase : MonoBehaviour {
        public List<JointNodeBase> Nodes;
        protected JointNodeBase RootNode { get; private set; }

        protected virtual void Awake() {
            GameObject rootJoint;
            if (IsJoint(gameObject)) {
                rootJoint = gameObject;
            } else {
                GameObject[] roots = GetChildrenJoint(gameObject);
                if (roots.Length == 0) {
                    Debug.LogError($"[JointHierarchyBase] No Joint found in children of {name}", this);
                    return;
                }

                rootJoint = roots[0];
                if (roots.Length > 1) {
                    Debug.LogWarning(
                        $"[JointHierarchyBase] Multiple potential roots found under {name}. Using {rootJoint.name}.",
                        this);
                }
            }

            RootNode = GetJointNode(rootJoint, null);

            Nodes = new List<JointNodeBase>();
            GetNodes(RootNode);
        }

        protected virtual bool IsJoint(GameObject candidate) => true;

        protected GameObject[] GetChildrenJoint(GameObject parent) {
            List<GameObject> childrenJoint = new();

            for (int i = 0; i < parent.transform.childCount; i++) {
                GameObject child = parent.transform.GetChild(i).gameObject;
                CollectChildren(child, childrenJoint);
            }

            return childrenJoint.ToArray();

            void CollectChildren(GameObject obj, List<GameObject> children) {
                if (IsJoint(obj)) {
                    children.Add(obj);
                    return;
                }

                for (int i = 0; i < obj.transform.childCount; i++) {
                    GameObject child = obj.transform.GetChild(i).gameObject;
                    CollectChildren(child, children);
                }
            }
        }

        private void GetNodes(JointNodeBase node) {
            Nodes.Add(node);

            foreach (JointNodeBase child in node.Children) {
                GetNodes(child);
            }
        }

        protected virtual JointNodeBase GetJointNode(GameObject joint, JointNodeBase parent) {
            JointNodeBase node = new(joint, parent);
            GameObject[] childrenJoint = GetChildrenJoint(joint);

            foreach (GameObject childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint, node));
            }

            return node;
        }

        public virtual void Reset() => RootNode.Reset();
    }
}
