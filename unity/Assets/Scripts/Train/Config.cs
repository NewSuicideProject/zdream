using Unity.MLAgents;

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

            public static float FailurePenalty = 50f;
            public static float DistancePenaltyMultiplier = 0.25f;

            public static float ActionMultiplier = 10f;

            public static float JitterPenaltyMultiplier = 0.1f;
            public static float EnergyPenaltyMultiplier = 0.01f;
            public static float UprightRewardMultiplier = 1.0f;
            public static float SpeedRewardMultiplier = 1.0f;

            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                StaySuccessReward = envParams.GetWithDefault("reward__stay_success_reward", StaySuccessReward);
                StayingReward = envParams.GetWithDefault("reward__staying_reward", StayingReward);
                StaySuccessThreshold = envParams.GetWithDefault("reward__stay_success_threshold", StaySuccessThreshold);

                FailurePenalty = envParams.GetWithDefault("reward__failure_penalty", FailurePenalty);
                DistancePenaltyMultiplier =
                    envParams.GetWithDefault("reward__distance_penalty_multiplier", DistancePenaltyMultiplier);
                ActionMultiplier = envParams.GetWithDefault("reward__action_multiplier", ActionMultiplier);

                JitterPenaltyMultiplier =
                    envParams.GetWithDefault("reward__jitter_penalty_multiplier", JitterPenaltyMultiplier);
                EnergyPenaltyMultiplier =
                    envParams.GetWithDefault("reward__energy_penalty_multiplier", EnergyPenaltyMultiplier);
                UprightRewardMultiplier =
                    envParams.GetWithDefault("reward__upright_reward_multiplier", UprightRewardMultiplier);
                SpeedRewardMultiplier =
                    envParams.GetWithDefault("reward__speed_reward_multiplier", SpeedRewardMultiplier);
            }
        }

        public static class Phase {
            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                ARatio = envParams.GetWithDefault("phase__a_ratio", ARatio);
                BRatio = envParams.GetWithDefault("phase__b_ratio", BRatio);
                CRatio = envParams.GetWithDefault("phase__c_ratio", CRatio);
                DRatio = envParams.GetWithDefault("phase__d_ratio", DRatio);
                ERatio = envParams.GetWithDefault("phase__e_ratio", ERatio);
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
