import copy
import logging

import numpy as np
from gymnasium import Env, spaces
from mlagents_envs.base_env import ActionTuple, DecisionSteps, TerminalSteps
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.environment_parameters_channel import (
    EnvironmentParametersChannel,
)

from .extractors.encoders import NavigationEncoder


logger = logging.getLogger(__name__)


class UnityEnv(Env):
    def __init__(
        self,
        parameters: dict,
        unity_path: str | None = None,
        base_port: int = 5004,
        worker_id: int = 0,
    ) -> None:
        super().__init__()

        self.env_params_channel = EnvironmentParametersChannel()

        self._parameters: dict = {}
        for group, group_value in parameters.items():
            for key, value in group_value.items():
                self.set_parameter(group, key, value)

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

        resolution = int(self._parameters["terrain"]["resolution"])
        max_token = int(self._parameters["navigation"]["max_token"])

        self.observation_space = spaces.Dict(
            {
                "passion": spaces.Box(low=-1.0, high=1.0, shape=shapes["passion"]),
                "proprioception": spaces.Box(
                    low=-1.0, high=1.0, shape=shapes["proprioception"]
                ),
                "terrain": spaces.Box(
                    low=-1.0, high=1.0, shape=(resolution * resolution,)
                ),
                "navigation": spaces.Box(
                    low=-1.0,
                    high=1.0,
                    shape=(max_token * NavigationEncoder.token_size,),
                ),
            }
        )

        self.action_space = spaces.Box(
            low=-1.0, high=1.0, shape=(self._behavior_spec.action_spec.continuous_size,)
        )

        logger.info(f"observation space: {self.observation_space}")
        logger.info(f"action space: {self.action_space}")

    @staticmethod
    def _get_parameter_key(group: str, key: str) -> str:
        return f"{group}__{key}"

    def set_parameter(self, group: str, key: str, value: float) -> None:
        self._parameters.setdefault(group, {})[key] = value
        self.env_params_channel.set_float_parameter(
            self._get_parameter_key(group, key), float(value)
        )

    def get_parameters(self) -> dict:
        return copy.deepcopy(self._parameters)

    def _get_obs(self, steps: DecisionSteps | TerminalSteps) -> dict[str, np.ndarray]:
        raw_obs = {
            observation_spec.name: steps.obs[i][0]
            for i, observation_spec in enumerate(self._behavior_spec.observation_specs)
        }

        obs: dict[str, np.ndarray] = {}
        for key, space in self.observation_space.items():
            size = np.prod(space.shape)
            obs[key] = raw_obs[key][:size]

        return obs

    def reset(self, **kwargs) -> tuple[dict[str, np.ndarray], dict]:
        self._env.reset()
        decision_steps, _ = self._env.get_steps(self._behavior_name)
        obs = self._get_obs(decision_steps)
        return obs, {}

    def step(
        self, action: np.ndarray
    ) -> tuple[dict[str, np.ndarray], float, bool, bool, dict]:
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

    def close(self) -> None:
        self._env.close()
