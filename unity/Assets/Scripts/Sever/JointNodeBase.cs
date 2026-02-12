using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointNodeBase {
        public readonly List<JointNodeBase> Children = new();
        protected readonly GameObject GameObject;

        public JointNodeBase(GameObject gameObject) => GameObject = gameObject;

        public bool IsSevered { get; protected set; }

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
