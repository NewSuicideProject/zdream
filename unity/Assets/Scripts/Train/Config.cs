using Unity.MLAgents;
using UnityEngine;

namespace Train {
    public static class Config {
        private static void GetConfig<T>(string key, ref T value) {
            EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;
            T newValue;

            if (typeof(T) == typeof(float)) {
                float current = (float)(object)value;
                float updated = envParams.GetWithDefault(key, current);
                newValue = (T)(object)updated;
                if (!Mathf.Approximately(current, updated)) {
                    Debug.Log($"{key}={updated}");
                }

                value = newValue;
                return;
            }

            if (typeof(T) == typeof(int)) {
                int current = (int)(object)value;
                int updated = Mathf.RoundToInt(envParams.GetWithDefault(key, current));
                newValue = (T)(object)updated;
                if (current != updated) {
                    Debug.Log($"{key}={updated}");
                }

                value = newValue;
                return;
            }

            if (typeof(T) == typeof(bool)) {
                bool current = (bool)(object)value;
                bool updated = envParams.GetWithDefault(key, current ? 1f : 0f) >= 0.5f;
                newValue = (T)(object)updated;
                if (current != updated) {
                    Debug.Log($"{key}={updated}");
                }

                value = newValue;
                return;
            }

            throw new System.NotSupportedException($"Unsupported config type: {typeof(T).Name}");
        }

        public static class NavigationSensor {
            public const int MaxMaxToken = 10;

            private static int _maxToken;
            public static int MaxToken => _maxToken;

            public static void OnEpisodeBegin() {
                GetConfig("navigation_sensor__max_token", ref _maxToken);
                _maxToken = Mathf.Clamp(_maxToken, 0, MaxMaxToken);
            }
        }

        public static class Terrain {
            public const int MaxResolution = 100;

            private static int _resolution;
            public static int Resolution => _resolution;

            public static void OnEpisodeBegin() {
                GetConfig("terrain__resolution", ref _resolution);
                _resolution = Mathf.Clamp(_resolution, 2, MaxResolution);
            }
        }

        public static class Passion {
            private static float _ratio;
            public static float Ratio => _ratio;

            public static void OnEpisodeBegin() => GetConfig("passion__ratio", ref _ratio);
        }

        public static class Joint {
            private static float _targetSmoothing;
            public static float TargetSmoothing => _targetSmoothing;

            public static void OnEpisodeBegin() => GetConfig("joint__target_smoothing", ref _targetSmoothing);
        }

        public static class Reward {
            private static float _survivalReward;
            private static float _staySuccessReward;
            private static float _stayingReward;
            private static float _staySuccessThreshold;

            private static float _distancePenaltyMultiplier;
            private static float _jitterPenaltyMultiplier;
            private static float _energyPenaltyMultiplier;
            private static float _uprightRewardMultiplier;
            private static float _heightMatchRewardMultiplier;
            private static float _directionRewardMultiplier;

            public static float SurvivalReward => _survivalReward;
            public static float StaySuccessReward => _staySuccessReward;
            public static float StayingReward => _stayingReward;
            public static float StaySuccessThreshold => _staySuccessThreshold;

            public static float DistancePenaltyMultiplier => _distancePenaltyMultiplier;
            public static float JitterPenaltyMultiplier => _jitterPenaltyMultiplier;
            public static float EnergyPenaltyMultiplier => _energyPenaltyMultiplier;
            public static float UprightRewardMultiplier => _uprightRewardMultiplier;
            public static float HeightMatchRewardMultiplier => _heightMatchRewardMultiplier;
            public static float DirectionRewardMultiplier => _directionRewardMultiplier;

            public static void OnEpisodeBegin() {
                GetConfig("reward__survival_reward", ref _survivalReward);

                GetConfig("reward__stay_success_reward", ref _staySuccessReward);
                GetConfig("reward__staying_reward", ref _stayingReward);
                GetConfig("reward__stay_success_threshold", ref _staySuccessThreshold);

                GetConfig("reward__distance_penalty_multiplier", ref _distancePenaltyMultiplier);
                GetConfig("reward__jitter_penalty_multiplier", ref _jitterPenaltyMultiplier);
                GetConfig("reward__energy_penalty_multiplier", ref _energyPenaltyMultiplier);
                GetConfig("reward__upright_reward_multiplier", ref _uprightRewardMultiplier);
                GetConfig("reward__height_match_reward_multiplier", ref _heightMatchRewardMultiplier);
                GetConfig("reward__direction_reward_multiplier", ref _directionRewardMultiplier);
            }
        }

        public static class Normalize {
            private static float _expectedMaxSpeed;
            private static float _expectedMaxHeight;
            private static float _expectedMaxDistance;
            private static float _expectedMaxThickness;
            private static float _expectedMaxForce;

            public static float ExpectedMaxSpeed => _expectedMaxSpeed;
            public static float ExpectedMaxHeight => _expectedMaxHeight;
            public static float ExpectedMaxDistance => _expectedMaxDistance;
            public static float ExpectedMaxThickness => _expectedMaxThickness;
            public static float ExpectedMaxForce => _expectedMaxForce;

            public static void OnEpisodeBegin() {
                GetConfig("normalize__expected_max_speed", ref _expectedMaxSpeed);
                GetConfig("normalize__expected_max_height", ref _expectedMaxHeight);
                GetConfig("normalize__expected_max_distance", ref _expectedMaxDistance);
                GetConfig("normalize__expected_max_thickness", ref _expectedMaxThickness);
                GetConfig("normalize__expected_max_force", ref _expectedMaxForce);
            }
        }
    }
}
