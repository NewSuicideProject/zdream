using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Train {
    public class Agent : Unity.MLAgents.Agent {
        [SerializeField] private InputActionAsset inputActions;

        [Range(0.01f, 1f)] [SerializeField] private float passion = 0.5f;
        [SerializeField] private float expectedMaxSpeed = 20;
        [SerializeField] private float expectedMaxDistance = 20;

        [SerializeField] private float staySuccessReward = 20f;
        [SerializeField] private float stayingReward = 10f;
        [SerializeField] private float staySuccessThreshold = 5f;

        [SerializeField] private float failurePenalty = 50f;
        [SerializeField] private float distancePenaltyMultiplier = 0.25f;

        [SerializeField] private float actionMultiplier = 10f;

        [SerializeField] private float jitterPenaltyMultiplier = 0.1f;
        [SerializeField] private float energyPenaltyMultiplier = 0.01f;
        [SerializeField] private float uprightRewardMultiplier = 1.0f;
        [SerializeField] private float speedMatchRewardMultiplier = 1.0f;

        [SerializeField] private ArticulationBody[] jointBodies;


        private Test.Scripts.Environment _environment;
        private float _distanceNormalizationFactor;
        private InputAction _moveAction;
        private Rigidbody _rigidbody;
        private float _stayTime;
        private Transform _targetTransform;

        private Rigidbody[] _jointRigidbodies;
        private Vector3[] _prevAngularVelocities;
        private Vector3[] _currentAngularVelocities;
        private float[] _prevActions;


        protected override void Awake() {
            base.Awake();

            _rigidbody = GetComponent<Rigidbody>();
            _environment = GetComponentInParent<Test.Scripts.Environment>();

            _distanceNormalizationFactor = 1f / expectedMaxDistance;
            _jointRigidbodies = GetComponentsInChildren<Rigidbody>();
            _prevAngularVelocities = new Vector3[_jointRigidbodies.Length];
            _currentAngularVelocities = new Vector3[_jointRigidbodies.Length];
            _prevActions = new float[2];

            if (!inputActions) {
                return;
            }

            InputActionMap playerMap = inputActions.FindActionMap("Player");
            _moveAction = playerMap?.FindAction("Move");
        }

        private void Start() => _targetTransform = _environment.TargetTransform;

        protected override void OnEnable() {
            base.OnEnable();
            _moveAction?.Enable();
        }

        protected override void OnDisable() {
            base.OnDisable();
            _moveAction?.Disable();
        }

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

            AddReward(stayingReward * Time.fixedDeltaTime); //Staying Reward
            _stayTime += Time.fixedDeltaTime;
        }

        private float NormalizeDistance(float distance) => Normalization.Tanh(distance, _distanceNormalizationFactor);

        public override void OnEpisodeBegin() {
            _stayTime = 0f;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.linearVelocity = Vector3.zero;

            for (int i = 0; i < _prevAngularVelocities.Length; i++) {
                _prevAngularVelocities[i] = Vector3.zero;
            }

            _environment.Reset();
        }

        public override void CollectObservations(VectorSensor sensor) {
        }

        public override void OnActionReceived(ActionBuffers actionBuffers) {
            Vector3 controlSignal = Vector3.zero;
            controlSignal.x = actionBuffers.ContinuousActions[0];
            controlSignal.z = actionBuffers.ContinuousActions[1];
            _rigidbody.AddForce(controlSignal * actionMultiplier);

            Vector3 currentVelocity = _rigidbody.linearVelocity;
            Vector3 targetDir = (_targetTransform.localPosition - transform.localPosition).normalized;

            float energySum = 0f;
            for (int i = 0; i < _jointRigidbodies.Length; i++) {
                _currentAngularVelocities[i] = _jointRigidbodies[i].angularVelocity;
                energySum += _currentAngularVelocities[i].magnitude;
            }

            ActionSegment<float> continuousActions = actionBuffers.ContinuousActions;
            float actionJitterSum = 0f;

            for (int i = 0; i < continuousActions.Length; i++) {
                float diff = continuousActions[i] - _prevActions[i];
                actionJitterSum += diff * diff;
            }

            float jitterPenalty = actionJitterSum * jitterPenaltyMultiplier;

            for (int i = 0; i < continuousActions.Length; i++) {
                _prevActions[i] = continuousActions[i];
            }

            float uprightBonus = Vector3.Dot(transform.up, Vector3.up);
            float targetSpeedReward = Mathf.Exp(-Mathf.Pow(expectedMaxSpeed - currentVelocity.magnitude, 2));

            float integratedReward = CalculateFullReward(
                passion,
                currentVelocity,
                targetDir,
                jitterPenalty,
                energySum,
                uprightBonus,
                targetSpeedReward
            );

            AddReward(integratedReward * Time.fixedDeltaTime);

            float distanceToTarget = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
            AddReward(-NormalizeDistance(distanceToTarget) * distancePenaltyMultiplier); //Distance Penalty

            _currentAngularVelocities.CopyTo(_prevAngularVelocities, 0);


            if (_stayTime >= staySuccessThreshold) {
                AddReward(staySuccessReward); //Staying Success Reward
                EndEpisode();
            } else if (transform.localPosition.y < 0f) {
                EndEpisode();
            } else if (StepCount >= MaxStep - 1) {
                AddReward(-failurePenalty); // Failure Penalty
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut) {
            ActionSegment<float> continuousActionsOut = actionsOut.ContinuousActions;
            Vector2 moveInput = Vector2.zero;
            if (_moveAction != null) {
                moveInput = _moveAction.ReadValue<Vector2>();
            }

            continuousActionsOut[0] = moveInput.x;
            continuousActionsOut[1] = moveInput.y;
        }


        private float CalculateFullReward(float pw, Vector3 velocity, Vector3 targetDir, float jitter, float energy,
            float upright, float speedMatch) {
            float invPw = 1.0f / Mathf.Max(pw, 0.01f);

            float speedReward = Vector3.Dot(velocity, targetDir) * pw;
            float jitterPenalty = jitter * jitterPenaltyMultiplier;
            float energyPenalty = energy * invPw * energyPenaltyMultiplier;
            float uprightReward = upright * invPw * uprightRewardMultiplier;
            float speedMatchReward = speedMatch * pw * speedMatchRewardMultiplier;

            return speedReward - jitterPenalty - energyPenalty + uprightReward + speedMatchReward;
        }
    }
}
