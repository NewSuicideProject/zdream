using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointNodeBase {
        protected readonly GameObject GameObject;
        public readonly List<JointNodeBase> Children = new();

        public bool IsSevered { get; protected set; }

        public JointNodeBase(GameObject gameObject) => GameObject = gameObject;

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
