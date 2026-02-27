using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace Train {
    public static class Config {
        private static string ToSnakeCase(string text) => string.IsNullOrEmpty(text)
            ? text
            : string.Concat(text.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())).ToLower();

        private static void GetConfig<T>(string group, string property, ref T value) {
            string key = $"{ToSnakeCase(group)}__{ToSnakeCase(property)}";
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

        public static class Navigation {
            public const int MaxMaxTokens = 10;

            private static int _maxTokens;
            public static int MaxTokens => _maxTokens;

            public static void OnEpisodeBegin() {
                GetConfig(nameof(Navigation), nameof(MaxTokens), ref _maxTokens);
                _maxTokens = Mathf.Clamp(_maxTokens, 0, MaxMaxTokens);
            }
        }

        public static class Terrain {
            public const int MaxResolution = 100;

            private static int _resolution;
            public static int Resolution => _resolution;

            public static void OnEpisodeBegin() {
                GetConfig(nameof(Terrain), nameof(Resolution), ref _resolution);
                _resolution = Mathf.Clamp(_resolution, 2, MaxResolution);
            }
        }

        public static class Passion {
            private static float _ratio;
            public static float Ratio => _ratio;

            public static void OnEpisodeBegin() => GetConfig(nameof(Passion), nameof(Ratio), ref _ratio);
        }

        public static class Assist {
            private static float _targetAssist;
            private static float _heightAssist;
            private static float _rotationAssist;
            public static float TargetAssist => _targetAssist;
            public static float HeightAssist => _heightAssist;
            public static float RotationAssist => _rotationAssist;

            public static void OnEpisodeBegin() {
                GetConfig(nameof(Assist), nameof(TargetAssist), ref _targetAssist);
                GetConfig(nameof(Assist), nameof(HeightAssist), ref _heightAssist);
                GetConfig(nameof(Assist), nameof(RotationAssist), ref _rotationAssist);
            }
        }

        public static class Reward {
            private static float _survivalReward;
            public static float SurvivalReward => _survivalReward;

            private static float _staySuccessReward;
            private static float _stayingReward;
            private static float _staySuccessThreshold;
            public static float StaySuccessReward => _staySuccessReward;
            public static float StayingReward => _stayingReward;
            public static float StaySuccessThreshold => _staySuccessThreshold;

            private static float _distancePenaltyMultiplier;
            private static float _jitterPenaltyMultiplier;
            private static float _energyPenaltyMultiplier;
            private static float _uprightRewardMultiplier;
            private static float _heightMatchRewardMultiplier;
            private static float _directionRewardMultiplier;
            public static float DistancePenaltyMultiplier => _distancePenaltyMultiplier;
            public static float JitterPenaltyMultiplier => _jitterPenaltyMultiplier;
            public static float EnergyPenaltyMultiplier => _energyPenaltyMultiplier;
            public static float UprightRewardMultiplier => _uprightRewardMultiplier;
            public static float HeightMatchRewardMultiplier => _heightMatchRewardMultiplier;
            public static float DirectionRewardMultiplier => _directionRewardMultiplier;

            public static void OnEpisodeBegin() {
                GetConfig(nameof(Reward), nameof(SurvivalReward), ref _survivalReward);

                GetConfig(nameof(Reward), nameof(StaySuccessReward), ref _staySuccessReward);
                GetConfig(nameof(Reward), nameof(StayingReward), ref _stayingReward);
                GetConfig(nameof(Reward), nameof(StaySuccessThreshold), ref _staySuccessThreshold);

                GetConfig(nameof(Reward), nameof(DistancePenaltyMultiplier), ref _distancePenaltyMultiplier);
                GetConfig(nameof(Reward), nameof(JitterPenaltyMultiplier), ref _jitterPenaltyMultiplier);
                GetConfig(nameof(Reward), nameof(EnergyPenaltyMultiplier), ref _energyPenaltyMultiplier);
                GetConfig(nameof(Reward), nameof(UprightRewardMultiplier), ref _uprightRewardMultiplier);
                GetConfig(nameof(Reward), nameof(HeightMatchRewardMultiplier), ref _heightMatchRewardMultiplier);
                GetConfig(nameof(Reward), nameof(DirectionRewardMultiplier), ref _directionRewardMultiplier);
            }
        }

        public static class Normalization {
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
                GetConfig(nameof(Normalization), nameof(ExpectedMaxSpeed), ref _expectedMaxSpeed);
                GetConfig(nameof(Normalization), nameof(ExpectedMaxHeight), ref _expectedMaxHeight);
                GetConfig(nameof(Normalization), nameof(ExpectedMaxDistance), ref _expectedMaxDistance);
                GetConfig(nameof(Normalization), nameof(ExpectedMaxThickness), ref _expectedMaxThickness);
                GetConfig(nameof(Normalization), nameof(ExpectedMaxForce), ref _expectedMaxForce);
            }
        }
    }
}
