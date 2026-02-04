# python/train/unity_env.py
import numpy as np
from gymnasium import Env, spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment

HEIGHT_MAP_KEY = 'height_map'


class UnityEnv(Env):
    def __init__(self, unity_path: str):
        super().__init__()

        self.env = UnityEnvironment(
            file_name=unity_path,
            no_graphics=True
        )
        self.env.reset()

        self.behavior = list(self.env.behavior_specs.keys())[0]
        spec = self.env.behavior_specs[self.behavior]

        obs_shape = spec.observation_specs[0].shape
        self.observation_space = spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=obs_shape,
            dtype=np.float32
        )

        act_spec = spec.action_spec
        if act_spec.is_continuous():
            self.action_space = spaces.Box(
                low=-1.0, high=1.0,
                shape=(act_spec.continuous_size,),
                dtype=np.float32
            )
        else:
            self.action_space = spaces.Discrete(act_spec.discrete_branches[0])

    def reset(self, **kwargs):
        self.env.reset()
        dec, term = self.env.get_steps(self.behavior)
        obs = dec.obs[0][0]
        return obs, {}

    def step(self, action):
        if isinstance(self.action_space, spaces.Discrete):
            action = [action]

        action = np.array([action], dtype=np.float32)
        self.env.set_actions(self.behavior, ActionTuple(continuous=action))

        self.env.step()
        dec, term = self.env.get_steps(self.behavior)

        if len(term) > 0:
            obs = term.obs[0][0]
            reward = term.reward[0]
            terminated = True
        else:
            obs = dec.obs[0][0]
            reward = dec.reward[0]
            terminated = False

        return obs, reward, terminated, False, {}

    def close(self):
        self.env.close()
