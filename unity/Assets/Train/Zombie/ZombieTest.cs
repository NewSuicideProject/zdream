using Train.Sever;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Train.Zombie {
    public class ZombieTest : MonoBehaviour {
        private TrainJointHierarchy _hierarchy;
        [SerializeField] private float severInterval = 2f;
        private TrainJointNode _severed;
        private float _timer;

        private void Awake() => _hierarchy = GetComponent<TrainJointHierarchy>();


        private void Update() {
            _timer -= Time.deltaTime;
            if (_timer > 0f) {
                return;
            }

            _timer = severInterval;


            _severed?.Join();
            _severed = _hierarchy.TrainNodes[Random.Range(1, _hierarchy.TrainNodes.Count)];
            _severed.Sever();
        }
    }
}
