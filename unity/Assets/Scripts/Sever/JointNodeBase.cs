using System.Collections.Generic;
using UnityEngine;

namespace Sever {
    public class JointNodeBase {
        public GameObject GameObject;
        public bool LocalIsSevered;
        public readonly List<JointNodeBase> Children = new();
        public JointNodeBase Parent;


        public bool IsSevered {
            get {
                if (LocalIsSevered) {
                    return true;
                }

                return Parent is { IsSevered: true };
            }
        }
    }
}
