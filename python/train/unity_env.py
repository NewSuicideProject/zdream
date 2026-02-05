import numpy as np
from gymnasium import Env, spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment


class UnityGymEnv(Env):
    def __init__(self, unity_path: str):
        super().__init__()

        self.env = UnityEnvironment(
            file_name=unity_path,
            no_graphics=True,
        )
        self.env.reset()

        self.behavior = list(self.env.behavior_specs.keys())[0]
        spec = self.env.behavior_specs[self.behavior]

        self._obs_names = list(spec.observation_specs.keys())

        self.observation_space = spaces.Dict(
            {
                name: spaces.Box(
                    low=-np.inf,
                    high=np.inf,
                    shape=spec.observation_specs[name].shape,
                    dtype=np.float32,
                )
                for name in self._obs_names
            }
        )

        act_spec = spec.action_spec
        if act_spec.is_continuous():
            self.action_space = spaces.Box(
                low=-1.0,
                high=1.0,
                shape=(act_spec.continuous_size,),
                dtype=np.float32,
            )
        else:
            self.action_space = spaces.Discrete(act_spec.discrete_branches[0])

    def _build_obs(self, decision_steps):
        return {name: decision_steps.obs[name][0] for name in self._obs_names}

    def reset(self, **kwargs):
        self.env.reset()
        decision_steps, _ = self.env.get_steps(self.behavior)
        return self._build_obs(decision_steps), {}

    def step(self, action):
        if isinstance(self.action_space, spaces.Discrete):
            action = [action]

        action = np.array([action], dtype=np.float32)
        self.env.set_actions(
            self.behavior,
            ActionTuple(continuous=action),
        )

        self.env.step()
        decision_steps, terminal_steps = self.env.get_steps(self.behavior)

        if len(terminal_steps) > 0:
            obs = self._build_obs(terminal_steps)
            reward = terminal_steps.reward[0]
            terminated = True
        else:
            obs = self._build_obs(decision_steps)
            reward = decision_steps.reward[0]
            terminated = False

        return obs, reward, terminated, False, {}
