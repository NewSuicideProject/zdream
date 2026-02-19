using Environment;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Test {
    public class Agent : Unity.MLAgents.Agent {
        [SerializeField] private float expectedMaxSpeed = 20;
        [SerializeField] private float expectedMaxDistance = 20;

        [SerializeField] private float staySuccessReward = 20f;
        [SerializeField] private float stayingReward = 10f;
        [SerializeField] private float staySuccessThreshold = 5f;
        private float _stayTime;

        [SerializeField] private float fallingPenalty = 50f;
        [SerializeField] private float distancePenaltyMultiplier = 0.25f;

        [SerializeField] private float actionMultiplier = 10f;

        private float _distanceNormalizationFactor;
        private float _speedNormalizationFactor;

        private EnvironmentBase _environment;
        private Rigidbody _rigidbody;

        private Transform _targetTransform;

        protected override void Awake() {
            base.Awake();

            _rigidbody = GetComponent<Rigidbody>();
            _environment = GetComponentInParent<EnvironmentBase>();

            _distanceNormalizationFactor = 1f / expectedMaxDistance;
            _speedNormalizationFactor = 1f / expectedMaxSpeed;
        }

        private void Start() => _targetTransform = _environment.TargetTransform;

        private void OnTriggerExit(Collider other) {
            if (other.transform != _targetTransform) {
                return;
            }

            _stayTime = 0f;
        }

        private void OnTriggerStay(Collider other) {
            if (other.transform != _targetTransform) {
                return;
            }

            AddReward(stayingReward * Time.fixedDeltaTime);
            _stayTime += Time.fixedDeltaTime;
        }

        private float NormalizeDistance(float distance) => Normalization.Tanh(distance, _distanceNormalizationFactor);
        private float NormalizeSpeed(float speed) => Normalization.Tanh(speed, _speedNormalizationFactor);

        private Vector3 NormalizeCoordinate(Vector3 coordinate) =>
            new(
                NormalizeDistance(coordinate.x),
                NormalizeDistance(coordinate.y),
                NormalizeDistance(coordinate.z));

        private Vector3 NormalizeVelocity(Vector3 velocity) =>
            new(
                NormalizeSpeed(velocity.x),
                NormalizeSpeed(velocity.y),
                NormalizeSpeed(velocity.z));

        public override void OnEpisodeBegin() {
            _stayTime = 0f;

            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.linearVelocity = Vector3.zero;

            _environment.Reset();
        }

        public override void CollectObservations(VectorSensor sensor) {
            sensor.AddObservation(NormalizeCoordinate(_targetTransform.localPosition - transform.localPosition));
            sensor.AddObservation(NormalizeVelocity(_rigidbody.linearVelocity));
        }

        public override void OnActionReceived(ActionBuffers actionBuffers) {
            Vector3 controlSignal = Vector3.zero;
            controlSignal.x = actionBuffers.ContinuousActions[0];
            controlSignal.z = actionBuffers.ContinuousActions[1];

            _rigidbody.AddForce(controlSignal * actionMultiplier);

            float distanceToTarget = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
            AddReward(-NormalizeDistance(distanceToTarget) * distancePenaltyMultiplier);

            if (_stayTime >= staySuccessThreshold) {
                AddReward(staySuccessReward);
                EndEpisode();
            } else if (transform.localPosition.y < 0f) {
                AddReward(-fallingPenalty);
                EndEpisode();
            }
        }
    }
}
