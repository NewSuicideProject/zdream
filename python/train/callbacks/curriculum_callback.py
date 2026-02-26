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

        self.target_gate: dict[str, float] = OmegaConf.to_container(
            config.curriculum.gate_ratios, resolve=True
        )
        self.target_env: dict[str, dict[str, float]] = {
            group: OmegaConf.to_container(params, resolve=True)
            for group, params in config.curriculum.unity_params.items()
        }

        self._start_gate: dict[str, float] = {}
        self._start_env: dict[str, dict[str, float]] = {}

    def _on_training_start(self) -> None:
        extractor = self.model.policy.features_extractor
        self._start_gate = {
            key: getattr(extractor, key).item()
            for key in self.target_gate
            if hasattr(extractor, key)
        }

        if isinstance(self.unity_env, VecEnv):
            params = self.unity_env.env_method("get_parameters", indices=[0])[0]
        else:
            params = self.unity_env.get_parameters()

        self._start_env = {
            group: params[group] for group in self.target_env if group in params
        }

        logger.info("start gate  : %s", self._start_gate)
        logger.info("target gate : %s", self.target_gate)
        logger.info("start env   : %s", self._start_env)
        logger.info("target env  : %s", self.target_env)

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
        extractor = self.model.policy.features_extractor
        for key in self.target_gate:
            if key not in self._start_gate:
                continue
            value = self._lerp(self._start_gate[key], self.target_gate[key], t)
            buffer: torch.Tensor = getattr(extractor, key)
            buffer.fill_(value)
            if self.verbose >= 2:
                logger.debug("gate.%s = %.4f (t=%.4f)", key, value, t)

    def _update_env(self, t: float) -> None:
        for group in self.target_env:
            if group not in self.target_env or group not in self._start_env:
                continue
            for key, target_val in self.target_env[group].items():
                start_val = self._start_env[group].get(key, target_val)
                value = self._lerp(start_val, target_val, t)

                if isinstance(self.unity_env, VecEnv):
                    self.unity_env.env_method("set_parameter", group, key, value)
                else:
                    self.unity_env.set_parameter(group, key, value)

                if self.verbose >= 2:
                    logger.debug("%s.%s = %.4f (t=%.4f)", group, key, value, t)
