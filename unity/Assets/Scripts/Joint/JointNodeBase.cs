using System.Collections.Generic;
using UnityEngine;

namespace Joint {
    public class JointNodeBase {
        public readonly List<JointNodeBase> Children = new();
        public readonly Transform Transform;
        public readonly JointNodeBase Parent;

        public JointNodeBase(Transform transform, JointNodeBase parent) {
            Transform = transform;
            Parent = parent;
        }

        public bool IsSevered { get; protected set; }

        public virtual void Reset() {
            Join();
            foreach (JointNodeBase child in Children) {
                child.Reset();
            }
        }

        public virtual void Sever() {
            if (IsSevered) {
                return;
            }

            IsSevered = true;
            Transform.localScale = Vector3.zero;

            foreach (JointNodeBase child in Children) {
                child.Sever();
            }
        }

        public virtual void Join() {
            if (!IsSevered) {
                return;
            }

            IsSevered = false;
            Transform.localScale = Vector3.one;

            foreach (JointNodeBase child in Children) {
                child.Join();
            }
        }
    }
}
