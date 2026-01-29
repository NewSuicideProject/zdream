using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Test
{
    public class TestAgent : Agent
    {
        private const float VelocityNormalizer = 20;

        [SerializeField] private TestManager testManager;
        [SerializeField] private InputActionAsset inputActions;

        [SerializeField] private float stayTrialReward = 5f;
        [SerializeField] private float staySuccessReward = 20f;
        [SerializeField] private float staySuccessThreshold = 5f;
        [SerializeField] private float stayFailurePenalty = 10f;

        [SerializeField] private float fallingPenalty = 30f;

        [SerializeField] private AnimationCurve distanceRewardCurve;
        [SerializeField] private float distanceRewardMultiplier = 0.01f;

        [SerializeField] private float actionMultiplier = 10f;

        private InputAction _moveAction;

        private float _positionNormalizer;

        private Rigidbody _rigidbody;
        private float _stayTime;
        private Transform _target;

        protected override void Awake()
        {
            base.Awake();

            _rigidbody = GetComponent<Rigidbody>();
            _positionNormalizer = testManager.SpawnRange * 2f;

            if (!inputActions) return;

            var playerMap = inputActions.FindActionMap("Player");
            _moveAction = playerMap?.FindAction("Move");
        }

        private void Start()
        {
            _target = testManager.targetTransform;
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

        private void OnTriggerEnter(Collider other)
        {
            if (other.transform != _target) return;

            AddReward(stayTrialReward);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform != _target) return;

            AddReward(-stayFailurePenalty);
            _stayTime = 0f;
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.transform != _target) return;

            _stayTime += Time.fixedDeltaTime;
        }

        public override void OnEpisodeBegin()
        {
            _stayTime = 0f;

            testManager.Reset();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation((_target.localPosition - transform.localPosition) / _positionNormalizer);
            sensor.AddObservation(_rigidbody.linearVelocity / VelocityNormalizer);
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            var controlSignal = Vector3.zero;
            controlSignal.x = actionBuffers.ContinuousActions[0];
            controlSignal.z = actionBuffers.ContinuousActions[1];

            _rigidbody.AddForce(controlSignal * actionMultiplier);

            var distanceToTarget = Vector3.Distance(transform.localPosition, _target.localPosition);
            AddReward(distanceRewardCurve.Evaluate(distanceToTarget / _positionNormalizer) * distanceRewardMultiplier);

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