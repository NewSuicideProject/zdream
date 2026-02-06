using System.Collections.Generic;
using UnityEngine;

namespace Train.AgentBody.Scripts {
    public struct JointLimitCache {
        public float LowerLimit;
        public float UpperLimit;
    }

    public class JointConfig {
        public ArticulationBody ArticulationBody;
        public bool LocalIsSevered;
        public JointLimitCache[] JointLimitCaches;
        public readonly List<JointConfig> Children = new();
        public JointConfig Parent;

        public bool IsSevered {
            get {
                if (LocalIsSevered) {
                    return true;
                }

                if (Parent != null) {
                    return Parent.IsSevered;
                }

                return false;
            }
        }
    }
}
