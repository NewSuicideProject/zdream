import logging

import torch
from omegaconf import DictConfig, OmegaConf
from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.vec_env import VecEnv

from ..unity_env import UnityEnv


logger = logging.getLogger(__name__)


class CurriculumCallback(BaseCallback):
    def __init__(
        self,
        unity_env: VecEnv | UnityEnv,
        config: DictConfig,
        steps_count: int,
        interval: int = 1,
        verbose: int = 0,
    ) -> None:
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

    def _on_training_start(self) -> None:
        features_extractor = self.model.policy.features_extractor
        self.gate_ratio_starts = {
            key: getattr(features_extractor, key).item()
            for key in self.gate_ratio_targets
            if hasattr(features_extractor, key)
            and getattr(features_extractor, key).item() != self.gate_ratio_targets[key]
        }
        self.gate_ratio_targets = {
            k: v
            for k, v in self.gate_ratio_targets.items()
            if k in self.gate_ratio_starts
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
        self.unity_param_targets = {
            group: {
                k: v
                for k, v in keys.items()
                if k in self.unity_param_starts.get(group, {})
            }
            for group, keys in self.unity_param_targets.items()
            if group in self.unity_param_starts
        }

        gate_log = "\n".join(
            f"\t{key}: {start:.4f} -> {self.gate_ratio_targets[key]:.4f}"
            for key, start in self.gate_ratio_starts.items()
        )
        unity_log = "\n".join(
            f"\t{group}:\n"
            + "\n".join(
                f"\t\t{key}: {start_val:.4f} -> {self.unity_param_targets[group][key]:.4f}"
                for key, start_val in starts.items()
            )
            for group, starts in self.unity_param_starts.items()
        )
        logger.info("gate ratios:\n%s", gate_log)
        logger.info("unity params:\n%s", unity_log)

    def _on_step(self) -> bool:
        prev_timesteps = self.num_timesteps - self.training_env.num_envs
        if self.num_timesteps // self.interval == prev_timesteps // self.interval:
            return True

        t = min(self.num_timesteps / self.steps_count, 1.0)

        def lerp(start: float, end: float, t: float) -> float:
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
