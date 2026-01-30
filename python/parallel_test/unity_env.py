import logging
import numpy as np

np.bool = bool  # Fix for numpy compatibility issue

from gymnasium import spaces
from gymnasium.vector import VectorEnv
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment

logger = logging.getLogger(__name__)


class UnityEnv(VectorEnv):
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
        logger.info(f"num_envs: {self.num_envs}")

        single_action_space = spaces.Box(
            -1,
            1,
            shape=(self.spec.action_spec.continuous_size,),
            dtype=np.float32,
        )

        obs_shape = self.spec.observation_specs[0].shape
        single_observation_space = spaces.Box(
            -1, 1, shape=obs_shape, dtype=np.float32
        )

        super().__init__(
            num_envs=self.num_envs,
            observation_space=single_observation_space,
            action_space=single_action_space,
        )

        self.agent_ids = []
        self._update_agent_ids()

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
        infos = [{} for _ in range(self.num_envs)]

        for i in range(self.num_envs):
            if i < len(decision_steps):
                obs = decision_steps.obs[0][i]
                observations.append(obs)
            else:
                observations.append(
                    np.zeros(
                        self.single_observation_space.shape, dtype=np.float32
                    )
                )

        return np.array(observations, dtype=np.float32), infos

    def reset(self, seed=None, options=None):
        self.reset_async(seed, options)
        return self.reset_wait(seed, options)

    def step_async(self, actions):
        action_tuple = ActionTuple()

        action_tuple.add_continuous(np.array(actions, dtype=np.float32))

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

            # 종료된 에이전트 확인
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

            # 진행 중인 에이전트
            elif agent_id in decision_steps:
                idx = list(decision_steps.agent_id).index(agent_id)
                obs = decision_steps.obs[0][idx]
                reward = decision_steps.reward[idx]
                terminated = False
                truncated = False

            # 에이전트를 찾을 수 없는 경우 (새로운 에이전트로 대체)
            else:
                if len(decision_steps) > i:
                    obs = decision_steps.obs[0][i]
                    reward = decision_steps.reward[i]
                else:
                    obs = np.zeros(
                        self.single_observation_space.shape, dtype=np.float32
                    )
                    reward = 0.0
                terminated = False
                truncated = False

            observations.append(obs)
            rewards.append(float(reward))
            terminateds.append(terminated)
            truncateds.append(truncated)
            infos.append({})

        # 에이전트 ID 업데이트
        self._update_agent_ids()

        return (
            np.array(observations, dtype=np.float32),
            np.array(rewards, dtype=np.float32),
            np.array(terminateds, dtype=bool),
            np.array(truncateds, dtype=bool),
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
