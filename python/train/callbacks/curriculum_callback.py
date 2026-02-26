import logging

import torch
from omegaconf import DictConfig, OmegaConf
from python.train.unity_env import UnityEnv
from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.vec_env import VecEnv


logger = logging.getLogger(__name__)


class CurriculumCallback(BaseCallback):
    def __init__(
        self,
        unity_env: UnityEnv,
        config: DictConfig,
        steps_count: int,
        interval: int = 1,
        verbose: int = 0,
    ):
        super().__init__(verbose)

        self.unity_env = unity_env
        self.steps_count = steps_count
        self.interval = interval

        self.gate_ratio_targets: dict[str, float] = (
            OmegaConf.to_container(config.gate_ratios, resolve=True)
            if "gate_ratios" in config
            else {}
        )
        self.unity_param_targets: dict[str, dict[str, float]] = (
            OmegaConf.to_container(config.unity_params, resolve=True)
            if "unity_params" in config
            else {}
        )

        self.gate_ratio_starts: dict[str, float] = {}
        self.unity_param_starts: dict[str, dict[str, float]] = {}

    def _on_training_start(self) -> None:
        features_extractor = self.model.policy.features_extractor
        self.gate_ratio_starts = {
            gate_ratio_target: getattr(features_extractor, gate_ratio_target).item()
            for gate_ratio_target in self.gate_ratio_targets
            if hasattr(features_extractor, gate_ratio_target)
        }

        if isinstance(self.unity_env, VecEnv):
            unity_params = self.unity_env.env_method("get_parameters", indices=[0])[0]
        else:
            unity_params = self.unity_env.get_parameters()

        self.unity_param_starts = {
            group: unity_params[group]
            for group in self.unity_param_targets
            if group in unity_params
        }

        logger.info("start gate  : %s", self.gate_ratio_starts)
        logger.info("target gate : %s", self.gate_ratio_targets)
        logger.info("start env   : %s", self.unity_param_starts)
        logger.info("target env  : %s", self.unity_param_targets)

    def _on_step(self) -> bool:
        if self.n_calls % self.interval != 0:
            return True

        t = min(self.num_timesteps / self.steps_count, 1.0)

        self._update_gate(t)
        self._update_env(t)

        return True

    @staticmethod
    def _lerp(start: float, end: float, t: float) -> float:
        return start + (end - start) * t

    def _update_gate(self, t: float) -> None:
        features_extractor = self.model.policy.features_extractor
        for key in self.gate_ratio_targets:
            if key not in self.gate_ratio_starts:
                continue
            value = self._lerp(
                self.gate_ratio_starts[key], self.gate_ratio_targets[key], t
            )
            buffer: torch.Tensor = getattr(features_extractor, key)
            buffer.fill_(value)
            if self.verbose >= 2:
                logger.debug("gate.%s = %.4f (t=%.4f)", key, value, t)

    def _update_env(self, t: float) -> None:
        for group in self.unity_param_targets:
            if (
                group not in self.unity_param_targets
                or group not in self.unity_param_starts
            ):
                continue
            for key, target_val in self.unity_param_targets[group].items():
                start_val = self.unity_param_starts[group].get(key, target_val)
                value = self._lerp(start_val, target_val, t)

                if isinstance(self.unity_env, VecEnv):
                    self.unity_env.env_method("set_parameter", group, key, value)
                else:
                    self.unity_env.set_parameter(group, key, value)

                if self.verbose >= 2:
                    logger.debug("%s.%s = %.4f (t=%.4f)", group, key, value, t)
