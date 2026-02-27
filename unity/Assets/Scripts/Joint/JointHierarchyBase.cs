using System.Collections.Generic;
using UnityEngine;

namespace Joint {
    public class JointHierarchyBase : MonoBehaviour {
        protected List<JointNodeBase> Nodes;
        protected JointNodeBase RootNode { get; private set; }

        protected virtual void Awake() {
            Transform rootJoint;
            if (IsJoint(transform)) {
                rootJoint = transform;
            } else {
                Transform[] roots = GetChildrenJoint(transform);
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

        protected virtual bool IsJoint(Transform candidate) => true;

        protected Transform[] GetChildrenJoint(Transform parent) {
            List<Transform> childrenJoint = new();

            foreach (Transform child in parent) {
                CollectChildren(child, childrenJoint);
            }

            return childrenJoint.ToArray();

            void CollectChildren(Transform t, List<Transform> children) {
                if (IsJoint(t)) {
                    children.Add(t);
                    return;
                }

                foreach (Transform child in t) {
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

        protected virtual JointNodeBase GetJointNode(Transform joint, JointNodeBase parent) {
            JointNodeBase node = new(joint, parent);
            Transform[] childrenJoint = GetChildrenJoint(joint);

            foreach (Transform childJoint in childrenJoint) {
                node.Children.Add(GetJointNode(childJoint, node));
            }

            return node;
        }

        public virtual void OnEpisodeBegin() => RootNode.Reset();
    }
}
