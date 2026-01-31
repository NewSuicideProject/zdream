import logging

import numpy as np
from gymnasium import spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from stable_baselines3.common.vec_env import VecEnv

logger = logging.getLogger(__name__)


class UnityParallelEnv(VecEnv):
    def __init__(self, base_port=5004):
        logger.info("waiting unity")
        self._env = UnityEnvironment(file_name=None, base_port=base_port)
        logger.info("unity connected")

        self._env.reset()

        self.behavior_name = list(self._env.behavior_specs.keys())[0]
        self.spec = self._env.behavior_specs[self.behavior_name]

        decision_steps, _ = self._env.get_steps(self.behavior_name)
        available_agents = len(decision_steps)

        self.num_envs = available_agents

        action_space = spaces.Box(
            -1,
            1,
            shape=(self.spec.action_spec.continuous_size,),
            dtype=np.float32,
        )

        obs_shape = self.spec.observation_specs[0].shape
        observation_space = spaces.Box(-1, 1, shape=obs_shape, dtype=np.float32)

        super().__init__(
            num_envs=self.num_envs,
            observation_space=observation_space,
            action_space=action_space,
        )

        self.agent_ids = []
        self._update_agent_ids()

        logger.info(f"observation space: {self.observation_space}")
        logger.info(f"action space: {self.action_space}")
        logger.info(f"num_envs: {self.num_envs}")

    def _update_agent_ids(self):
        decision_steps, terminal_steps = self._env.get_steps(self.behavior_name)
        self.agent_ids = list(decision_steps.agent_id)[: self.num_envs]

        terminal_ids = list(terminal_steps.agent_id)[
            : max(0, self.num_envs - len(self.agent_ids))
        ]
        self.agent_ids.extend(terminal_ids)

    def reset_async(self, seed=None, options=None):
        self._env.reset()
        self._update_agent_ids()

    def reset_wait(self, seed=None, options=None):
        decision_steps, _ = self._env.get_steps(self.behavior_name)

        observations = []

        for i in range(self.num_envs):
            if i < len(decision_steps):
                obs = decision_steps.obs[0][i]
                observations.append(obs)
            else:
                observations.append(
                    np.zeros(self.observation_space.shape, dtype=np.float32)
                )

        return np.array(observations, dtype=np.float32)

    def reset(self, seed=None, options=None):
        self.reset_async(seed, options)
        return self.reset_wait(seed, options)

    def step_async(self, actions):
        decision_steps, _ = self._env.get_steps(self.behavior_name)

        if len(decision_steps) > 0:
            agent_actions = []
            for i, agent_id in enumerate(self.agent_ids):
                if i < len(actions) and agent_id in decision_steps:
                    agent_actions.append(actions[i])

            if len(agent_actions) > 0:
                action_tuple = ActionTuple()
                action_tuple.add_continuous(
                    np.array(agent_actions, dtype=np.float32)
                )
                self._env.set_actions(self.behavior_name, action_tuple)

        self._env.step()

    def step_wait(self):
        decision_steps, terminal_steps = self._env.get_steps(self.behavior_name)

        observations = []
        rewards = []
        terminateds = []
        truncateds = []
        infos = []

        for i in range(self.num_envs):
            agent_id = self.agent_ids[i] if i < len(self.agent_ids) else -1

            if agent_id in terminal_steps:
                idx = list(terminal_steps.agent_id).index(agent_id)
                obs = terminal_steps.obs[0][idx]
                reward = terminal_steps.reward[idx]

                if terminal_steps.interrupted[idx]:
                    truncated = True
                    terminated = False
                else:
                    truncated = False
                    terminated = True

            elif agent_id in decision_steps:
                idx = list(decision_steps.agent_id).index(agent_id)
                obs = decision_steps.obs[0][idx]
                reward = decision_steps.reward[idx]
                terminated = False
                truncated = False

            else:
                if len(decision_steps) > i:
                    obs = decision_steps.obs[0][i]
                    reward = decision_steps.reward[i]
                else:
                    obs = np.zeros(
                        self.observation_space.shape, dtype=np.float32
                    )
                    reward = 0.0
                terminated = False
                truncated = False

            observations.append(obs)
            rewards.append(float(reward))
            terminateds.append(terminated)
            truncateds.append(truncated)
            infos.append({})

        self._update_agent_ids()

        dones = np.logical_or(terminateds, truncateds)

        return (
            np.array(observations, dtype=np.float32),
            np.array(rewards, dtype=np.float32),
            np.array(dones, dtype=bool),
            infos,
        )

    def step(self, actions):
        self.step_async(actions)
        return self.step_wait()

    def close(self):
        self._env.close()
        super().close()

    def close_extras(self, **kwargs):
        pass

    def seed(self, seed=None):
        return [None for _ in range(self.num_envs)]

    def render(self, mode="human"):
        return None

    def env_is_wrapped(self, wrapper_class, indices=None):
        if indices is None:
            indices = range(self.num_envs)
        return [False for _ in indices]

    def get_attr(self, attr_name, indices=None):
        if indices is None:
            indices = range(self.num_envs)
        return [None for _ in indices]

    def set_attr(self, attr_name, value, indices=None):
        pass

    def env_method(
        self, method_name, *method_args, indices=None, **method_kwargs
    ):
        if indices is None:
            indices = range(self.num_envs)
        return [None for _ in indices]
