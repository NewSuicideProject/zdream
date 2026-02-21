import logging

import numpy as np
from gymnasium import Env, spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.environment_parameters_channel import (
    EnvironmentParametersChannel,
)


logger = logging.getLogger(__name__)


class UnityEnv(Env):
    def __init__(
        self,
        unity_path: str = None,
        base_port: int = 5004,
        worker_id: int = 0,
        unity_kwargs: dict = None,
    ):
        super().__init__()

        self.env_params_channel = EnvironmentParametersChannel()

        if unity_kwargs:
            for key, value in unity_kwargs.items():
                self.env_params_channel.set_float_parameter(key, float(value))

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

        spec = self._env.behavior_specs[self._behavior_name]

        obs_specs = spec.observation_specs

        self.observation_space = spaces.Dict(
            {
                obs_spec.name: spaces.Box(
                    low=-1.0,
                    high=1.0,
                    shape=obs_spec.shape,
                    dtype=np.float32,
                )
                for obs_spec in obs_specs
            }
        )

        self.action_space = spaces.Box(
            low=-1.0,
            high=1.0,
            shape=(spec.action_spec.continuous_size,),
            dtype=np.float32,
        )

        logger.info(f"observation space: {self.observation_space}")
        logger.info(f"action space: {self.action_space}")

    def _get_obs(self, steps):
        spec = self._env.behavior_specs[self._behavior_name]
        obs_dict = {}
        for i, obs_spec in enumerate(spec.observation_specs):
            obs_dict[obs_spec.name] = steps.obs[i][0]
        return obs_dict

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
