import logging

import numpy as np
from gymnasium import Env, spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.environment_parameters_channel import (
    EnvironmentParametersChannel,
)


logger = logging.getLogger(__name__)


class UnityEnv(Env):
    def __init__(
        self, unity_path: str, base_port: int = None, unity_kwargs: dict = None
    ):
        super().__init__()

        self.engine_channel = EngineConfigurationChannel()
        self.env_params_channel = EnvironmentParametersChannel()

        if unity_kwargs:
            for key, value in unity_kwargs.items():
                self.env_params_channel.set_float_parameter(key, float(value))

        logger.info("waiting unity")
        self.env = UnityEnvironment(
            file_name=unity_path,
            base_port=base_port,
            no_graphics=True,
            side_channels=[self.engine_channel, self.env_params_channel],
        )
        logger.info("unity connected")

        self.env.reset()

        self.behavior = list(self.env.behavior_specs.keys())[0]
        spec = self.env.behavior_specs[self.behavior]

        self._obs_names = list(spec.observation_specs.keys())

        self.observation_space = spaces.Dict(
            {
                name: spaces.Box(
                    low=-1.0,
                    high=1.0,
                    shape=spec.observation_specs[name].shape,
                    dtype=np.float32,
                )
                for name in self._obs_names
            }
        )

        self.action_space = spaces.Box(
            low=-1.0,
            high=1.0,
            shape=(spec.action_spec.continuous_size,),
            dtype=np.float32,
        )

    def _get_obs(self, steps):
        return {name: steps.obs[name][0] for name in self._obs_names}

    def reset(self, **kwargs):
        self.env.reset()
        decision_steps, _ = self.env.get_steps(self.behavior)
        obs = self._get_obs(decision_steps)
        return obs, {}

    def step(self, action):
        action = np.array([action], dtype=np.float32)
        self.env.set_actions(
            self.behavior,
            ActionTuple(continuous=action),
        )

        self.env.step()
        decision_steps, terminal_steps = self.env.get_steps(self.behavior)

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
        self.env.close()
