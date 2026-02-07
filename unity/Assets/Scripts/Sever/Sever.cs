using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sever {
    [RequireComponent(typeof(JointHierarchyBase))]
    public class Sever : MonoBehaviour {
        private JointHierarchyBase _hierarchy;
        private List<JointNodeBase> _severedNodes;

        private void Awake() {
            _hierarchy = GetComponent<JointHierarchyBase>();
            _severedNodes = new List<JointNodeBase>();
        }


        [ContextMenu("Random Sever Random Count")]
        public void RandomSeverRandomCount() => RandomSever(Random.Range(1, 4));


        public void RandomSever(int count = 1) {
            if (!_hierarchy) {
                return;
            }

            foreach (JointNodeBase node in _severedNodes) {
                node.Join();
            }

            for (int i = 0; i < count; i++) {
                int trial = 0;
                JointNodeBase node;
                do {
                    node = _hierarchy.Nodes[Random.Range(1, _hierarchy.Nodes.Count)];
                } while (node.IsSevered && trial++ < 10);

                node.Sever();
                _severedNodes.Add(node);
            }
        }
    }
}
