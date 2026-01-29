import numpy as np
import gymnasium as gym
from gymnasium import spaces
from mlagents_envs.base_env import ActionTuple


class UnityEnvWrapper(gym.Env):
    def __init__(self, unity_env):
        self._env = unity_env
        self._env.reset()

        self.behavior_name = list(self._env.behavior_specs.keys())[0]
        self.spec = self._env.behavior_specs[self.behavior_name]

        if self.spec.action_spec.is_continuous():
            high = np.inf
            self.action_space = spaces.Box(
                -high,
                high,
                shape=(self.spec.action_spec.continuous_size,),
                dtype=np.float32,
            )
        else:
            self.action_space = spaces.Discrete(
                self.spec.action_spec.discrete_branches[0]
            )

        obs_shape = self.spec.observation_specs[0].shape
        high = np.inf
        self.observation_space = spaces.Box(
            -high, high, shape=obs_shape, dtype=np.float32
        )

    def reset(self, seed=None, options=None):
        self._env.reset()
        decision_steps, _ = self._env.get_steps(self.behavior_name)

        obs = decision_steps.obs[0][0]
        return obs, {}

    def step(self, action):
        action_tuple = ActionTuple()
        if self.spec.action_spec.is_continuous():
            action_tuple.add_continuous(np.array([action]))
        else:
            action_tuple.add_discrete(np.array([[action]]))

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
