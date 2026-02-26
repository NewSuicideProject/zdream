import logging

import numpy as np
from gymnasium import Env, spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.environment_parameters_channel import (
    EnvironmentParametersChannel,
)
from python.train.extractors.encoders.navigation_encoder import NavigationEncoder


logger = logging.getLogger(__name__)


class UnityEnv(Env):
    def __init__(
        self,
        parameters: dict,
        unity_path: str = None,
        base_port: int = 5004,
        worker_id: int = 0,
    ):
        super().__init__()

        self.env_params_channel = EnvironmentParametersChannel()

        self.parameters: dict

        self.set_parameters(parameters)

        logger.info("waiting unity")
        self._env = UnityEnvironment(
            file_name=unity_path,
            base_port=base_port,
            worker_id=worker_id,
            side_channels=[self.env_params_channel],
        )
        logger.info("unity connected")

        self._env.reset()

        self._behavior_name = list(self._env.behavior_specs.keys())[0]
        self._behavior_spec = self._env.behavior_specs[self._behavior_name]

        shapes = {
            observation_spec.name: observation_spec.shape
            for observation_spec in self._behavior_spec.observation_specs
        }

        resolution = int(parameters.terrain.resolution)
        max_token = int(parameters.navigation.max_token)

        self.observation_space = spaces.Dict(
            {
                "passion": spaces.Box(
                    low=-1.0, high=1.0, shape=shapes["passion"], dtype=np.float32
                ),
                "proprioception": spaces.Box(
                    low=-1.0, high=1.0, shape=shapes["proprioception"], dtype=np.float32
                ),
                "terrain": spaces.Box(
                    low=-1.0,
                    high=1.0,
                    shape=(resolution * resolution,),
                    dtype=np.float32,
                ),
                "navigation": spaces.Box(
                    low=-1.0,
                    high=1.0,
                    shape=(max_token * NavigationEncoder.token_size,),
                    dtype=np.float32,
                ),
            }
        )

        self.action_space = spaces.Box(
            low=-1.0,
            high=1.0,
            shape=(self._behavior_spec.action_spec.continuous_size,),
            dtype=np.float32,
        )

        logger.info(f"observation space: {self.observation_space}")
        logger.info(f"action space: {self.action_space}")

    def set_parameters(self, parameters: dict):
        self.parameters = parameters
        for group, group_value in parameters.items():
            for key, value in group_value.items():
                self.env_params_channel.set_float_parameter(
                    f"{group}__{key}", float(value)
                )

    def _get_obs(self, steps):
        raw = {
            observation_spec.name: steps.obs[i][0]
            for i, observation_spec in enumerate(self._behavior_spec.observation_specs)
        }

        return {
            "passion": raw["passion"],
            "proprioception": raw["proprioception"],
            "terrain": raw["terrain"][: self.observation_space["terrain"].shape[0]],
            "navigation": raw["navigation"][
                : self.observation_space["navigation"].shape[0]
            ],
        }

    def reset(self, **kwargs):
        self._env.reset()
        decision_steps, _ = self._env.get_steps(self._behavior_name)
        obs = self._get_obs(decision_steps)
        return obs, {}

    def step(self, action):
        action = np.array([action], dtype=np.float32)
        self._env.set_actions(
            self._behavior_name,
            ActionTuple(continuous=action),
        )

        self._env.step()
        decision_steps, terminal_steps = self._env.get_steps(self._behavior_name)

        if len(terminal_steps) > 0:
            obs = self._get_obs(terminal_steps)
            reward = terminal_steps.reward[0]
            terminated = True
        else:
            obs = self._get_obs(decision_steps)
            reward = decision_steps.reward[0]
            terminated = False

        return obs, reward, terminated, False, {}

    def close(self):
        self._env.close()
