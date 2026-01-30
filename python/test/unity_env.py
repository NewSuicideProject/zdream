import logging
import numpy as np

np.bool = bool  # Fix for numpy compatibility issue

import gymnasium as gym
from gymnasium import spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment

logger = logging.getLogger(__name__)


class UnityEnv(gym.Env):
    def __init__(self, base_port=5004):
        logger.info("waiting unity")
        self._env = UnityEnvironment(file_name=None, base_port=base_port)
        logger.info("unity connected")

        self._env.reset()

        self.behavior_name = list(self._env.behavior_specs.keys())[0]
        self.spec = self._env.behavior_specs[self.behavior_name]

        self.action_space = spaces.Box(
            -1,
            1,
            shape=(self.spec.action_spec.continuous_size,),
            dtype=np.float32,
        )

        obs_shape = self.spec.observation_specs[0].shape
        self.observation_space = spaces.Box(
            -1, 1, shape=obs_shape, dtype=np.float32
        )

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
