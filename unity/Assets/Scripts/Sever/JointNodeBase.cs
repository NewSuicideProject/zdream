using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointNodeBase {
        protected readonly GameObject GameObject;
        public readonly List<JointNodeBase> Children = new();
        private readonly JointNodeBase _parent;

        public bool IsSevered { get; protected set; }

        public JointNodeBase(GameObject gameObject, JointNodeBase parent) {
            GameObject = gameObject;
            _parent = parent;
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
