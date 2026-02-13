using System.Linq;
using Train.Joint;
using Train.Sensor;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train {
    [RequireComponent(typeof(Proprioception))]
    public class Agent : Unity.MLAgents.Agent {
        [Range(0.01f, 1f)] [SerializeField] private float passion = 0.5f;

        [SerializeField] private float staySuccessReward = 20f;
        [SerializeField] private float stayingReward = 10f;
        [SerializeField] private float staySuccessThreshold = 5f;

        [SerializeField] private float failurePenalty = 50f;
        [SerializeField] private float distancePenaltyMultiplier = 0.25f;

        [SerializeField] private float actionMultiplier = 10f;
        [SerializeField] private float minPassion = 0.01f;

        [SerializeField] private float jitterPenaltyMultiplier = 0.1f;
        [SerializeField] private float energyPenaltyMultiplier = 0.01f;
        [SerializeField] private float uprightRewardMultiplier = 1.0f;
        [SerializeField] private float speedMatchRewardMultiplier = 1.0f;

        private AgentJointHierarchy _jointHierarchy;
        private float[] _prevActions;

        private Environment.Environment _environment;
        private Proprioception _proprioception;
        private Navigation _navigation;
        private float _stayTime;
        private Transform _targetTransform;


        protected override void Awake() {
            base.Awake();

            _environment = GetComponentInParent<Environment.Environment>();

            _jointHierarchy = GetComponent<AgentJointHierarchy>();
            _proprioception = GetComponent<Proprioception>();

            _navigation = GetComponentInChildren<Navigation>();

            _proprioception.targetTransform = _environment.TargetTransform;
            _navigation.targetTransform = _environment.TargetTransform;
        }

        private void Start() {
            _targetTransform = _environment.TargetTransform;

            int totalDoF = _jointHierarchy.TrainNodes.Sum(n => n.DoF);
            _prevActions = new float[totalDoF];
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

            AddReward(stayingReward * Time.fixedDeltaTime);
            _stayTime += Time.fixedDeltaTime;
        }

        public override void OnEpisodeBegin() {
            _stayTime = 0f;
            _environment.Reset();
            _jointHierarchy.Reset();
            _navigation.Reset();
        }

        public override void CollectObservations(VectorSensor sensor) {
        }

        public override void OnActionReceived(ActionBuffers actionBuffers) {
            ActionSegment<float> continuousActions = actionBuffers.ContinuousActions;
            int actionIndex = 0;

            foreach (AgentJointNode node in _jointHierarchy.TrainNodes) {
                for (int i = 0; i < node.DoF; i++) {
                    float targetValue = continuousActions[actionIndex++];

                    ArticulationDrive drive = node.GetDrive(i);

                    drive.target = targetValue * actionMultiplier;

                    switch (i) {
                        case 0: node.Body.xDrive = drive; break;
                        case 1: node.Body.yDrive = drive; break;
                        case 2: node.Body.zDrive = drive; break;
                    }
                }
            }

            Vector3 currentVelocity = _proprioception.LinearVelocity;
            Vector3 targetDir = (_targetTransform.localPosition - transform.localPosition).normalized;

            float energySum = 0f;

            foreach (AgentJointNode node in _jointHierarchy.TrainNodes) {
                if (node.Body != null) {
                    energySum += node.Body.angularVelocity.magnitude;
                }
            }

            float actionJitterSum = 0f;

            for (int i = 0; i < continuousActions.Length; i++) {
                float diff = continuousActions[i] - _prevActions[i];
                actionJitterSum += diff * diff;
                _prevActions[i] = continuousActions[i];
            }


            float uprightBonus = Vector3.Dot(_proprioception.Gravity, _proprioception.InitialGravity);
            float targetSpeedReward =
                Mathf.Exp(-Mathf.Pow(Normalization.ExpectedMaxSpeed - currentVelocity.magnitude, 2));

            float integratedReward = CalculateFullReward(
                passion,
                currentVelocity,
                targetDir,
                actionJitterSum,
                energySum,
                uprightBonus,
                targetSpeedReward,
                minPassion
            );

            AddReward(integratedReward * Time.fixedDeltaTime);

            float distanceToTarget = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
            AddReward(-Normalization.NormalizeDistance(distanceToTarget) * distancePenaltyMultiplier *
                      Time.fixedDeltaTime);


            if (_stayTime >= staySuccessThreshold) {
                AddReward(staySuccessReward);
                EndEpisode();
            } else if (transform.localPosition.y < 0f) {
                EndEpisode();
            } else if (StepCount >= MaxStep - 1) {
                AddReward(-failurePenalty);
            }
        }

        private float CalculateFullReward(float pw, Vector3 velocity, Vector3 targetDir, float jitter, float energy,
            float upright, float speedMatch, float minPassionEpsilon) {
            float invPw = 1.0f / Mathf.Max(pw, minPassionEpsilon);

            float speedReward = Vector3.Dot(velocity, targetDir) * pw;
            float jitterPenalty = jitter * jitterPenaltyMultiplier;
            float energyPenalty = energy * invPw * energyPenaltyMultiplier;
            float uprightReward = upright * invPw * uprightRewardMultiplier;
            float speedMatchReward = speedMatch * pw * speedMatchRewardMultiplier;

            return speedReward - jitterPenalty - energyPenalty + uprightReward + speedMatchReward;
        }
    }
}
