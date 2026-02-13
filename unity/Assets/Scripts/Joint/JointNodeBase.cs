using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointNodeBase {
        public readonly List<JointNodeBase> Children = new();
        public readonly GameObject GameObject;
        public readonly JointNodeBase Parent;

        public JointNodeBase(GameObject gameObject, JointNodeBase parent) {
            GameObject = gameObject;
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
            GameObject.transform.localScale = Vector3.zero;

            foreach (JointNodeBase child in Children) {
                child.Sever();
            }
        }

        public virtual void Join() {
            if (!IsSevered) {
                return;
            }

            IsSevered = false;
            GameObject.transform.localScale = Vector3.one;

            foreach (JointNodeBase child in Children) {
                child.Join();
            }
        }
    }
}
