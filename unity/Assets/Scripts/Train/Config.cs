using Unity.MLAgents;

namespace Train {
    public static class Config {
        public static class NavigationSensor {
            public static int MaxToken = 100;

            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;
                MaxToken = (int)envParams.GetWithDefault("navigation_sensor_max_token", MaxToken);
            }
        }

        public static class Terrain {
            public static int Resolution = 10;

            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;
                Resolution = (int)envParams.GetWithDefault("terrain_resolution", Resolution);
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
            public static float SpeedMatchRewardMultiplier = 1.0f;

            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                StaySuccessReward = envParams.GetWithDefault("stay_success_reward", StaySuccessReward);
                StayingReward = envParams.GetWithDefault("staying_reward", StayingReward);
                StaySuccessThreshold = envParams.GetWithDefault("stay_success_threshold", StaySuccessThreshold);

                FailurePenalty = envParams.GetWithDefault("failure_penalty", FailurePenalty);
                DistancePenaltyMultiplier =
                    envParams.GetWithDefault("distance_penalty_multiplier", DistancePenaltyMultiplier);
                ActionMultiplier = envParams.GetWithDefault("action_multiplier", ActionMultiplier);

                JitterPenaltyMultiplier =
                    envParams.GetWithDefault("jitter_penalty_multiplier", JitterPenaltyMultiplier);
                EnergyPenaltyMultiplier =
                    envParams.GetWithDefault("energy_penalty_multiplier", EnergyPenaltyMultiplier);
                UprightRewardMultiplier =
                    envParams.GetWithDefault("upright_reward_multiplier", UprightRewardMultiplier);
                SpeedMatchRewardMultiplier =
                    envParams.GetWithDefault("speed_match_reward_multiplier", SpeedMatchRewardMultiplier);
            }
        }

        public static class Phase {
            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                PhaseAMultiplier = envParams.GetWithDefault("phase_a_multiplier", PhaseAMultiplier);
                PhaseBMultiplier = envParams.GetWithDefault("phase_b_multiplier", PhaseBMultiplier);
                PhaseCMultiplier = envParams.GetWithDefault("phase_c_multiplier", PhaseCMultiplier);
            }

            public static float PhaseAMultiplier; // Agent try to stand up and stabilize (proprioception)
            public static float PhaseBMultiplier; // Agent try to move towards the target (navigation)
            public static float PhaseCMultiplier; // Agent try to overcome the terrain (terrain)
        }

        public static class Normalization {
            public static void Reset() {
                EnvironmentParameters envParams = Academy.Instance.EnvironmentParameters;

                ExpectedMaxSpeed = envParams.GetWithDefault("expected_max_speed", ExpectedMaxSpeed);
                ExpectedMaxDistance = envParams.GetWithDefault("expected_max_distance", ExpectedMaxDistance);
                ExpectedMaxThickness = envParams.GetWithDefault("expected_max_thickness", ExpectedMaxThickness);
                ExpectedMaxHeight = envParams.GetWithDefault("expected_max_height", ExpectedMaxHeight);
            }

            public static float ExpectedMaxSpeed = 20f;
            public static float ExpectedMaxDistance = 10f;
            public static float ExpectedMaxThickness = 2.5f;
            public static float ExpectedMaxHeight = 2.5f;
        }
    }
}
