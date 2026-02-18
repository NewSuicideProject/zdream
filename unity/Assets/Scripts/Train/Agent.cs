using System.Linq;
using Train.Joint;
using Train.Sensor;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Train {
    [RequireComponent(typeof(Proprioception))]
    public class Agent : Unity.MLAgents.Agent {
        [Range(0f, 1f)] [SerializeField] private float passion = 0.5f;

        private AgentJointHierarchy _jointHierarchy;
        private float[] _prevActions;

        private Environment _environment;
        private Proprioception _proprioception;
        private Navigation _navigation;
        private float _stayTime;
        private Transform _targetTransform;


        protected override void Awake() {
            base.Awake();

            _environment = GetComponentInParent<Environment>();

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

            AddReward(Config.Reward.StayingReward * Time.fixedDeltaTime);
            _stayTime += Time.fixedDeltaTime;
        }

        public override void OnEpisodeBegin() {
            _stayTime = 0f;

            Config.NavigationSensor.Reset();
            Config.Normalization.Reset();
            Config.Terrain.Reset();
            Config.Reward.Reset();
            Config.Phase.Reset();

            _environment.Reset();
            _jointHierarchy.Reset();
            _navigation.Reset();
        }

        public override void CollectObservations(VectorSensor sensor) => sensor.AddObservation(passion);

        public override void OnActionReceived(ActionBuffers actionBuffers) {
            ActionSegment<float> continuousActions = actionBuffers.ContinuousActions;
            int index = 0;

            foreach (AgentJointNode node in _jointHierarchy.TrainNodes) {
                for (int i = 0; i < node.DoF; i++) {
                    float targetValue = continuousActions[index++];

                    ArticulationDrive drive = node.GetDrive(i);
                    drive.target = targetValue * Config.Reward.ActionMultiplier;
                    node.SetDrive(i, drive);
                }
            }

            float jitterSum = 0f;
            for (int i = 0; i < continuousActions.Length; i++) {
                float diff = continuousActions[i] - _prevActions[i];
                jitterSum += diff * diff;
                _prevActions[i] = continuousActions[i];
            }

            float jitterPenalty = jitterSum * Config.Reward.JitterPenaltyMultiplier;

            float passionInverseMultiplier = 1f / (passion + 1f);
            float passionMultiplier = passion;

            float directionMatch =
                Vector3.Dot(
                    _proprioception.RelativeLinearVelocity.normalized,
                    _proprioception.RelativeTargetPosition.normalized);
            float directionReward = directionMatch * Config.Reward.DirectionRewardMultiplier;

            float energySum = _jointHierarchy.TrainNodes.Sum(node => node.Body.angularVelocity.magnitude);
            float energyPenalty = energySum * passionInverseMultiplier * Config.Reward.EnergyPenaltyMultiplier;

            float uprightMatch = Vector3.Dot(_proprioception.Gravity, _proprioception.InitialGravity);
            float uprightReward = uprightMatch * passionInverseMultiplier * Config.Reward.UprightRewardMultiplier;

            float distance = Vector3.Distance(transform.localPosition, _targetTransform.localPosition);
            float distancePenalty = Normalization.NormalizeDistance(distance) * passionMultiplier *
                                    Config.Reward.DistancePenaltyMultiplier;

            float fullReward = directionReward - jitterPenalty - energyPenalty + uprightReward - distancePenalty;

            AddReward(fullReward * Time.fixedDeltaTime);

            if (_stayTime > Config.Reward.StaySuccessThreshold) {
                AddReward(Config.Reward.StaySuccessReward);
                EndEpisode();
            } else if (transform.localPosition.y < 0f) {
                EndEpisode();
            }
        }
    }
}
