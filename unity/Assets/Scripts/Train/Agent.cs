using System.Linq;
using Environment;
using Train.Joint;
using Train.Sensor;
using Unity.MLAgents.Actuators;
using UnityEngine;
using Terrain = Train.Sensor.Terrain;

namespace Train {
    [RequireComponent(typeof(Proprioception))]
    [RequireComponent(typeof(AgentJointHierarchy))]
    [RequireComponent(typeof(Passion))]
    public class Agent : Unity.MLAgents.Agent {
        private AgentJointHierarchy _hierarchy;

        private EnvironmentBase _environment;
        private Proprioception _proprioception;
        private Navigation _navigation;
        private Passion _passion;
        private Terrain _terrain;
        private float _stayTime;
        private Transform _targetTransform;

        protected override void Awake() {
            base.Awake();

            _environment = GetComponentInParent<EnvironmentBase>();

            _hierarchy = GetComponent<AgentJointHierarchy>();
            _proprioception = GetComponent<Proprioception>();
            _passion = GetComponent<Passion>();

            _navigation = GetComponentInChildren<Navigation>();
            _terrain = GetComponentInChildren<Terrain>();

            _proprioception.targetTransform = _environment.TargetTransform;
            _navigation.targetTransform = _environment.TargetTransform;
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

            AddReward(Config.Reward.StayingReward * Time.fixedDeltaTime);
            _stayTime += Time.fixedDeltaTime;
        }

        public override void OnEpisodeBegin() {
            _stayTime = 0f;

            Config.Navigation.OnEpisodeBegin();
            Config.Terrain.OnEpisodeBegin();
            Config.Passion.OnEpisodeBegin();
            Config.Joint.OnEpisodeBegin();
            Config.Reward.OnEpisodeBegin();
            Config.Normalization.OnEpisodeBegin();

            _environment.OnEpisodeBegin();

            _hierarchy.OnEpisodeBegin();

            _navigation.OnEpisodeBegin();
            _terrain.OnEpisodeBegin();

            _passion.OnEpisodeBegin();
        }

        public override void OnActionReceived(ActionBuffers actionBuffers) {
            ActionSegment<float> newTargets = actionBuffers.ContinuousActions;

            int index = 0;
            float jitterSum = 0f;
            foreach (AgentJointNode node in _hierarchy.AgentNodes) {
                for (int i = 0; i < node.DoF; i++) {
                    jitterSum += (newTargets[index] - node.RawTarget[i]) * (newTargets[index] - node.RawTarget[i]);
                    node.SetTarget(i, newTargets[index]);
                    index++;
                }
            }

            float jitterPenalty = jitterSum / _proprioception.TotalDoF * Config.Reward.JitterPenaltyMultiplier;

            float targetDirectionMatch =
                Vector3.Dot(
                    _proprioception.ProjectedLinearVelocity.normalized,
                    _proprioception.RelativeTargetPosition.normalized);
            float targetDirectionMatchReward = targetDirectionMatch * _passion.Value *
                                               Config.Reward.DirectionRewardMultiplier;

            float navigationDirectionMatchReward = 0;
            if (_navigation.Corners.Count != 0) {
                float navigationDirectionMatch =
                    Vector3.Dot(
                        _navigation.Corners.First().RelativePosition.normalized,
                        _proprioception.ProjectedLinearVelocity.normalized);
                navigationDirectionMatchReward = navigationDirectionMatch * _passion.Value *
                                                 Config.Reward.DirectionRewardMultiplier * 2;
            }

            const float targetHeight = 1f;
            const float variance = 0.1f;
            float height = transform.localPosition.y;
            float heightMatch = Mathf.Exp(-Mathf.Pow(height - targetHeight, 2) / variance);
            float heightReward = heightMatch * (1f - _passion.Value) * Config.Reward.HeightMatchRewardMultiplier;

            float energySum = 0f;
            foreach (AgentJointNode node in _hierarchy.AgentNodes) {
                for (int i = 0; i < node.DoF; i++) {
                    float normalizedForce = Normalize.Force(node.Body.jointForce[i]);
                    energySum += normalizedForce * normalizedForce;
                    // TODO - force normalization
                }
            }

            float energyPenalty = energySum * (1f - _passion.Value) * Config.Reward.EnergyPenaltyMultiplier;

            float uprightMatch = Vector3.Dot(_proprioception.Gravity, _proprioception.InitialGravity);
            float uprightReward = uprightMatch * (1f - _passion.Value) * Config.Reward.UprightRewardMultiplier;

            float distance = _proprioception.RelativeTargetPosition.magnitude;
            float distancePenalty = Normalize.Distance(distance) * _passion.Value *
                                    Config.Reward.DistancePenaltyMultiplier;

            float survivalReward = Config.Reward.SurvivalReward;

            float fullReward = targetDirectionMatchReward + navigationDirectionMatchReward - jitterPenalty -
                energyPenalty + uprightReward - distancePenalty + heightReward + survivalReward;

            AddReward(fullReward * Time.fixedDeltaTime);

            if (_stayTime > Config.Reward.StaySuccessThreshold) {
                AddReward(Config.Reward.StaySuccessReward);
                EndEpisode();
            } else if (_hierarchy.RootAgentNode.GameObject.transform.position.y < 0.5f) {
                EndEpisode();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut) { }
    }
}
