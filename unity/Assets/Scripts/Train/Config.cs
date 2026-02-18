using Unity.MLAgents;
using UnityEngine;

namespace Train {
    public static class Config {
        public static class NavigationSensor {
            public static int MaxToken = 100;

            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;
                MaxToken = (int)envParams.GetWithDefault("navigation_sensor__max_token", MaxToken);
            }
        }

        public static class Terrain {
            public static int Resolution = 10;

            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;
                Resolution = (int)envParams.GetWithDefault("terrain__resolution", Resolution);
            }
        }

        public static class Reward {
            public static float StaySuccessReward = 20f;
            public static float StayingReward = 10f;
            public static float StaySuccessThreshold = 5f;

            public static float DistancePenaltyMultiplier = 0.25f;

            public static float JitterPenaltyMultiplier = 0.1f;
            public static float EnergyPenaltyMultiplier = 0.01f;
            public static float UprightRewardMultiplier = 1.0f;
            public static float DirectionRewardMultiplier = 1.0f;

            public static float ActionMultiplier = 10f;

            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                StaySuccessReward = envParams.GetWithDefault("reward__stay_success_reward", StaySuccessReward);
                StayingReward = envParams.GetWithDefault("reward__staying_reward", StayingReward);
                StaySuccessThreshold = envParams.GetWithDefault("reward__stay_success_threshold", StaySuccessThreshold);

                DistancePenaltyMultiplier =
                    envParams.GetWithDefault("reward__distance_penalty_multiplier", DistancePenaltyMultiplier);
                JitterPenaltyMultiplier =
                    envParams.GetWithDefault("reward__jitter_penalty_multiplier", JitterPenaltyMultiplier);
                EnergyPenaltyMultiplier =
                    envParams.GetWithDefault("reward__energy_penalty_multiplier", EnergyPenaltyMultiplier);
                UprightRewardMultiplier =
                    envParams.GetWithDefault("reward__upright_reward_multiplier", UprightRewardMultiplier);
                DirectionRewardMultiplier =
                    envParams.GetWithDefault("reward__direction_reward_multiplier", DirectionRewardMultiplier);

                ActionMultiplier = envParams.GetWithDefault("reward__action_multiplier", ActionMultiplier);
            }
        }

        public static class Phase {
            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                ERatio = envParams.GetWithDefault("phase__e_ratio", ERatio);
                DRatio = Mathf.Max(envParams.GetWithDefault("phase__d_ratio", DRatio), ERatio);
                CRatio = Mathf.Max(envParams.GetWithDefault("phase__c_ratio", CRatio), DRatio, ERatio);
                BRatio = Mathf.Max(envParams.GetWithDefault("phase__b_ratio", BRatio), CRatio, DRatio, ERatio);
                ARatio = Mathf.Max(envParams.GetWithDefault("phase__a_ratio", ARatio), BRatio, CRatio, DRatio, ERatio);
            }

            public static float ARatio; // Agent try to stand up and stabilize (proprioception)
            public static float BRatio; // Agent try to move towards the target (navigation)
            public static float CRatio; // Phase B with Passion
            public static float DRatio; // Agent try to overcome the terrain (terrain)
            public static float ERatio; // Phase D with Passion
        }

        public static class Normalization {
            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                ExpectedMaxSpeed = envParams.GetWithDefault("normalization__expected_max_speed", ExpectedMaxSpeed);
                ExpectedMaxHeight = envParams.GetWithDefault("normalization__expected_max_height", ExpectedMaxHeight);
                ExpectedMaxDistance =
                    envParams.GetWithDefault("normalization__expected_max_distance", ExpectedMaxDistance);
                ExpectedMaxThickness =
                    envParams.GetWithDefault("normalization__expected_max_thickness", ExpectedMaxThickness);
            }

            public static float ExpectedMaxSpeed = 20f;
            public static float ExpectedMaxDistance = 10f;
            public static float ExpectedMaxThickness = 2.5f;
            public static float ExpectedMaxHeight = 2.5f;
        }
    }
}
