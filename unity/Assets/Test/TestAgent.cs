using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Test
{
    public class TestAgent : Agent
    {
        [SerializeField]
        private TestManager testManager;
        [SerializeField]
        private InputActionAsset inputActions;
        private Transform _target;

        private Rigidbody _rigidbody;
        private InputAction _moveAction;
        private float _stayTime;
        
        private float _normalizationFactor;
        
        [SerializeField]
        private float stayTimeThreshold = 5f;
        
        [SerializeField]
        private float stayTrialReward = 5f;
        [SerializeField]
        private float stayFailurePenalty = 10f;
        [SerializeField]
        private float staySuccessReward = 20f;
        [SerializeField]
        private float fallingPenalty = 30f;
        [SerializeField]
        private float distancePenaltyMultiplier = 0.001f;
        
        [SerializeField]
        private float actionMultiplier = 10f;

        protected override void Awake()
        {
            base.Awake();
            
            _rigidbody = GetComponent<Rigidbody>();
            _normalizationFactor = testManager.SpawnRange * 2f;

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

        private void OnTriggerStay(Collider other)
        {
            if (other.transform != _target) return;
            
            _stayTime += Time.fixedDeltaTime;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform != _target) return;
            
            AddReward(-stayFailurePenalty); 
            _stayTime = 0f;
        }

        public override void OnEpisodeBegin()
        {
            _stayTime = 0f;

            testManager.Reset();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var relativePosition = _target.localPosition - transform.localPosition;
            
            sensor.AddObservation(relativePosition / _normalizationFactor);
            sensor.AddObservation(relativePosition.magnitude / _normalizationFactor);
            sensor.AddObservation(_rigidbody.linearVelocity);
            sensor.AddObservation(_rigidbody.angularVelocity);
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            var controlSignal = Vector3.zero;
            controlSignal.x = actionBuffers.ContinuousActions[0];
            controlSignal.z = actionBuffers.ContinuousActions[1];

            _rigidbody.AddForce(controlSignal * actionMultiplier);

            var distanceToTarget = Vector3.Distance(transform.localPosition, _target.localPosition);
            AddReward(-distancePenaltyMultiplier * distanceToTarget / _normalizationFactor);
        
            if (_stayTime >= stayTimeThreshold)
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

            if (_moveAction != null)
            {
                moveInput = _moveAction.ReadValue<Vector2>();
            }

            continuousActionsOut[0] = moveInput.x;
            continuousActionsOut[1] = moveInput.y;
        }
    }
}