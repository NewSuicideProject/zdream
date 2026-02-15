import logging

import gymnasium as gym
import numpy as np
from gymnasium import spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.environment_parameters_channel import (
    EnvironmentParametersChannel,
)


logger = logging.getLogger(__name__)


class UnityEnv(gym.Env):
    def __init__(self, file_name=None, base_port=5004, env_params: dict = None):
        logger.info("waiting unity")

        # Set up side channels
        self.engine_channel = EngineConfigurationChannel()
        self.env_params_channel = EnvironmentParametersChannel()

        # Set environment parameters if provided
        if env_params:
            for key, value in env_params.items():
                self.env_params_channel.set_float_parameter(key, float(value))

        self._env = UnityEnvironment(
            file_name=file_name,
            base_port=base_port,
            side_channels=[self.engine_channel, self.env_params_channel],
        )
        logger.info("unity connected")

        self._env.reset()

        self.behavior_name = list(self._env.behavior_specs.keys())[0]
        self.spec = self._env.behavior_specs[self.behavior_name]

        self.action_space = spaces.Box(
            -1, 1, shape=(self.spec.action_spec.continuous_size,)
        )

        obs_shape = self.spec.observation_specs[0].shape
        self.observation_space = spaces.Box(-1, 1, shape=obs_shape)

        logger.info(f"observation space: {self.observation_space}")
        logger.info(f"action space: {self.action_space}")

    def reset(self, seed=None, options=None):
        self._env.reset()
        decision_steps, _ = self._env.get_steps(self.behavior_name)

        obs = decision_steps.obs[0][0]
        return obs, {}

    def step(self, action):
        action_tuple = ActionTuple()
        action_tuple.add_continuous(np.array([action]))

        self._env.set_actions(self.behavior_name, action_tuple)
        self._env.step()

        decision_steps, terminal_steps = self._env.get_steps(self.behavior_name)

        terminated = False
        truncated = False

        if len(terminal_steps) > 0:
            obs = terminal_steps.obs[0][0]
            reward = terminal_steps.reward[0]

            if terminal_steps.interrupted[0]:
                truncated = True
                terminated = False
            else:
                truncated = False
                terminated = True

        else:
            obs = decision_steps.obs[0][0]
            reward = decision_steps.reward[0]

        return obs, float(reward), terminated, truncated, {}

    def close(self):
        self._env.close()
