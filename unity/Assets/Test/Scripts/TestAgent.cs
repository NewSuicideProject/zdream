using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Test.Scripts
{
    public class TestAgent : Agent
    {
        [SerializeField] private InputActionAsset inputActions;
        private InputAction _moveAction;

        [SerializeField] private float expectedMaxSpeed = 20;
        [SerializeField] private float expectedMaxDistance = 20;
        private float _distanceNormalizationFactor;
        private float _speedNormalizationFactor;

        [SerializeField] private float staySuccessReward = 20f;
        [SerializeField] private float stayingReward = 10f;
        [SerializeField] private float staySuccessThreshold = 5f;
        private float _stayTime;

        [SerializeField] private float fallingPenalty = 50f;

        [SerializeField] private AnimationCurve distanceRewardCurve;
        [SerializeField] private float distanceRewardMultiplier = 0.025f;

        [SerializeField] private float actionMultiplier = 10f;

        private TestEnvironment _testEnvironment;
        private Transform _targetTransform;
        private Rigidbody _rigidbody;

        protected override void Awake()
        {
            base.Awake();

            _rigidbody = GetComponent<Rigidbody>();
            _testEnvironment = GetComponentInParent<TestEnvironment>();

            _distanceNormalizationFactor = 1f / expectedMaxDistance;
            _speedNormalizationFactor = 1f / expectedMaxSpeed;

            if (!inputActions) return;

            var playerMap = inputActions.FindActionMap("Player");
            _moveAction = playerMap?.FindAction("Move");
        }

        private void Start()
        {
            _targetTransform = _testEnvironment.TargetTransform;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _moveAction?.Enable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _moveAction?.Disable();
        }


        private void OnTriggerExit(Collider other)
        {
            if (other.transform != _targetTransform) return;

            _stayTime = 0f;
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.transform != _targetTransform) return;

            AddReward(stayingReward * Time.fixedDeltaTime);
            _stayTime += Time.fixedDeltaTime;
        }

        private float NormalizeDistance(float distance)
        {
            return (float)Math.Tanh(distance * _distanceNormalizationFactor);
        }

        private float NormalizeSpeed(float speed)
        {
            return (float)Math.Tanh(speed * _speedNormalizationFactor);
        }

        private Vector3 NormalizeCoordinate(Vector3 coordinate)
        {
            return new Vector3(
                NormalizeDistance(coordinate.x),
                NormalizeDistance(coordinate.y),
                NormalizeDistance(coordinate.z));
        }

        private Vector3 NormalizeVelocity(Vector3 velocity)
        {
            return new Vector3(
                NormalizeSpeed(velocity.x),
                NormalizeSpeed(velocity.y),
                NormalizeSpeed(velocity.z));
        }

        public override void OnEpisodeBegin()
        {
            _stayTime = 0f;
            _testEnvironment.Reset();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(NormalizeCoordinate(_targetTransform.localPosition - transform.localPosition));
            sensor.AddObservation(NormalizeVelocity(_rigidbody.linearVelocity));
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            var controlSignal = Vector3.zero;
            controlSignal.x = actionBuffers.ContinuousActions[0];
            controlSignal.z = actionBuffers.ContinuousActions[1];

            _rigidbody.AddForce(controlSignal * actionMultiplier);

            var distanceToTarget = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
            AddReward(distanceRewardCurve.Evaluate(NormalizeDistance(distanceToTarget)) * distanceRewardMultiplier);

            if (_stayTime >= staySuccessThreshold)
            {
                SetReward(staySuccessReward);
                EndEpisode();
            }
            else if (transform.localPosition.y < 0f)
            {
                SetReward(-fallingPenalty);
                EndEpisode();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActionsOut = actionsOut.ContinuousActions;
            var moveInput = Vector2.zero;

            if (_moveAction != null) moveInput = _moveAction.ReadValue<Vector2>();

            continuousActionsOut[0] = moveInput.x;
            continuousActionsOut[1] = moveInput.y;
        }
    }
}