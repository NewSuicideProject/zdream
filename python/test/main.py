import numpy as np
import gymnasium as gym
from gymnasium import spaces

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from stable_baselines3 import SAC


class UnityToGymnasiumWrapper(gym.Env):
    def __init__(
        self,
        unity_env,
    ):
        self._env = unity_env
        self._env.reset()

        if not self._env.behavior_specs:
            self._env.step()

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
        if len(decision_steps) == 0:
            self._env.step()
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

        done = False
        reward = 0.0

        if len(terminal_steps) > 0:
            done = True
            reward = terminal_steps.reward[0]
            obs = terminal_steps.obs[0][0]
        else:
            reward = decision_steps.reward[0]
            obs = decision_steps.obs[0][0]

        return obs, float(reward), done, False, {}

    def close(self):
        self._env.close()


def main():
    unity_env = UnityEnvironment(file_name=None, base_port=5004)

    env = UnityToGymnasiumWrapper(unity_env)

    model = SAC("MlpPolicy", env, verbose=1)

    print("학습 시작! (터미널에 진행 상황이 표시됩니다)")
    model.learn(total_timesteps=1000)

    model.save("sac_unity_agent")
    env.close()
    print("학습 종료.")


if __name__ == "__main__":
    main()
