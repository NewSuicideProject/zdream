import logging

import torch
from omegaconf import OmegaConf
from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.vec_env import VecEnv


logger = logging.getLogger(__name__)


class CurriculumCallback(BaseCallback):
    def __init__(self, unity_env, config, steps_count, interval=1, verbose=0):
        super().__init__(verbose)

        self.unity_env = unity_env
        self.steps_count = steps_count
        self.interval = interval

        self.gate_ratio_targets: dict[str, float] = OmegaConf.to_container(
            config.gate_ratios, resolve=True
        )
        self.unity_param_targets: dict[str, dict[str, float]] = OmegaConf.to_container(
            config.unity_params, resolve=True
        )

        self.gate_ratio_starts: dict[str, float] = {}
        self.unity_param_starts: dict[str, dict[str, float]] = {}

    def _on_training_start(self):
        features_extractor = self.model.policy.features_extractor
        self.gate_ratio_starts = {
            key: getattr(features_extractor, key).item()
            for key in self.gate_ratio_targets
            if hasattr(features_extractor, key)
            and getattr(features_extractor, key).item() != self.gate_ratio_targets[key]
        }

        if isinstance(self.unity_env, VecEnv):
            unity_params = self.unity_env.env_method("get_parameters", indices=[0])[0]
        else:
            unity_params = self.unity_env.get_parameters()

        self.unity_param_starts = {
            group: {
                key: unity_params[group][key]
                for key in self.unity_param_targets[group]
                if key in unity_params.get(group, {})
                and unity_params[group][key] != self.unity_param_targets[group][key]
            }
            for group in self.unity_param_targets
            if group in unity_params
        }

        logger.info("start gate  : %s", self.gate_ratio_starts)
        logger.info("target gate : %s", self.gate_ratio_targets)
        logger.info("start env   : %s", self.unity_param_starts)
        logger.info("target env  : %s", self.unity_param_targets)

    def _on_step(self):
        if self.n_calls % self.interval != 0:
            return True

        t = min(self.num_timesteps / self.steps_count, 1.0)

        def lerp(start, end, t):
            return start + (end - start) * t

        features_extractor = self.model.policy.features_extractor
        for key, start in self.gate_ratio_starts.items():
            value = lerp(start, self.gate_ratio_targets[key], t)
            buffer: torch.Tensor = getattr(features_extractor, key)
            buffer.fill_(value)

        for group, starts in self.unity_param_starts.items():
            for key, start_val in starts.items():
                value = lerp(start_val, self.unity_param_targets[group][key], t)

                if isinstance(self.unity_env, VecEnv):
                    self.unity_env.env_method("set_parameter", group, key, value)
                else:
                    self.unity_env.set_parameter(group, key, value)

        return True
