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
            public static int MaxToken = 3;

            public static void Reset() => GetConfig("navigation_sensor__max_token", ref MaxToken);
        }

        public static class Terrain {
            public static int Resolution = 8;

            public static void Reset() => GetConfig("terrain__resolution", ref Resolution);
        }

        public static class Joint {
            public static float TargetSmoothing = 0.2f;

            public static void Reset() => GetConfig("joint__target_smoothing", ref TargetSmoothing);
        }

        public static class Reward {
            public static float StaySuccessReward = 5f;
            public static float StayingReward = 0.75f;
            public static float StaySuccessThreshold = 5f;

            public static float DistancePenaltyMultiplier = 0.05f;
            public static float JitterPenaltyMultiplier = 0.01f;
            public static float EnergyPenaltyMultiplier = 0.005f;
            public static float UprightRewardMultiplier = 0.5f;
            public static float HeightMatchRewardMultiplier = 0.25f;
            public static float DirectionRewardMultiplier = 0.5f;

            public static void Reset() {
                GetConfig("reward__stay_success_reward", ref StaySuccessReward);
                GetConfig("reward__staying_reward", ref StayingReward);
                GetConfig("reward__stay_success_threshold", ref StaySuccessThreshold);

                GetConfig("reward__distance_penalty_multiplier", ref DistancePenaltyMultiplier);
                GetConfig("reward__jitter_penalty_multiplier", ref JitterPenaltyMultiplier);
                GetConfig("reward__energy_penalty_multiplier", ref EnergyPenaltyMultiplier);
                GetConfig("reward__upright_reward_multiplier", ref UprightRewardMultiplier);
                GetConfig("reward__height_match_reward_multiplier", ref HeightMatchRewardMultiplier);
                GetConfig("reward__direction_reward_multiplier", ref DirectionRewardMultiplier);
            }
        }

        public static class Phase {
            public static void Reset() {
                GetConfig("phase__e_ratio", ref ERatio);
                GetConfig("phase__d_ratio", ref DRatio);
                GetConfig("phase__c_ratio", ref CRatio);
                GetConfig("phase__b_ratio", ref BRatio);
                GetConfig("phase__a_ratio", ref ARatio);

                DRatio = Mathf.Max(DRatio, ERatio);
                CRatio = Mathf.Max(CRatio, DRatio, ERatio);
                BRatio = Mathf.Max(BRatio, CRatio, DRatio, ERatio);
                ARatio = Mathf.Max(ARatio, BRatio, CRatio, DRatio, ERatio);
            }

            public static float ARatio = 1.0f; // Agent try to stand up and stabilize (proprioception)
            public static float BRatio; // Agent try to move towards the target (navigation)
            public static float CRatio; // Phase B with Passion
            public static float DRatio; // Agent try to overcome the terrain (terrain)
            public static float ERatio; // Phase D with Passion
        }

        public static class Normalization {
            public static void Reset() {
                GetConfig("normalization__expected_max_speed", ref ExpectedMaxSpeed);
                GetConfig("normalization__expected_max_height", ref ExpectedMaxHeight);
                GetConfig("normalization__expected_max_distance", ref ExpectedMaxDistance);
                GetConfig("normalization__expected_max_thickness", ref ExpectedMaxThickness);
                GetConfig("normalization__expected_max_force", ref ExpectedMaxForce);
            }

            public static float ExpectedMaxSpeed = 5f;
            public static float ExpectedMaxDistance = 10f;
            public static float ExpectedMaxThickness = 1f;
            public static float ExpectedMaxHeight = 2f;
            public static float ExpectedMaxForce = 300f;
        }
    }
}
