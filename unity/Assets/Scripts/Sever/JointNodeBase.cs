using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointNodeBase {
        private readonly GameObject _gameObject;
        private bool _isSevered;
        public readonly List<JointNodeBase> Children = new();
        private readonly JointNodeBase _parent;

        public JointNodeBase(GameObject gameObject, JointNodeBase parent) {
            _gameObject = gameObject;
            _parent = parent;
            _isSevered = false;
        }

        public virtual void Sever() {
            _isSevered = true;
            _gameObject.transform.localScale = Vector3.zero;

            foreach (JointNodeBase child in Children) {
                child.Sever();
            }
        }


        public bool IsSevered {
            get {
                if (_isSevered) {
                    return true;
                }

                return _parent is { IsSevered: true };
            }
        }
    }
}
