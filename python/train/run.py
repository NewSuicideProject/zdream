import logging
import os
from functools import partial
from pathlib import Path

import hydra
from dotenv import load_dotenv
from hydra.utils import get_class, get_original_cwd
from omegaconf import DictConfig, OmegaConf
from stable_baselines3.common.logger import configure
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import SubprocVecEnv, VecMonitor
from stable_baselines3.sac import SAC
from stable_baselines3.sac.policies import MultiInputPolicy

from .callbacks import CheckpointCallback, CurriculumCallback
from .unity_env import UnityEnv


logger = logging.getLogger(__name__)
load_dotenv(Path(__file__).parent / ".env")


def make_unity_env(file_name: str, worker_id: int, parameters: dict):
    return UnityEnv(unity_path=file_name, worker_id=worker_id, parameters=parameters)


def get_path(path_str):
    if not path_str:
        return None
    path = Path(path_str)
    if not path.is_absolute():
        path = Path(get_original_cwd()) / path
    if not path.exists():
        logger.info(f"path invalid: {path}")
        return None
    return path


@hydra.main(version_base=None, config_path="./configs", config_name="config")
def run(config: DictConfig):
    base_dir = Path.cwd()
    log_dir = base_dir / "log"
    model_path = base_dir / "model.zip"
    checkpoint_dir = base_dir / "checkpoints"

    unity_server_path = get_path(os.getenv("UNITY_SERVER_PATH", None))
    unity_path = get_path(os.getenv("UNITY_PATH", None))
    if not unity_path:
        unity_path = unity_server_path

    checkpoint_path = get_path(os.getenv("CHECKPOINT_PATH", None))

    if checkpoint_path:
        import json
        from zipfile import ZipFile

        with ZipFile(checkpoint_path, "r").open("unity_params.json") as file:
            unity_params = json.load(file)
    else:
        unity_params = OmegaConf.to_container(config.model.unity_params, resolve=True)

    logger.info(f"unity_path: {unity_path}")
    logger.info(f"unity_server_path: {unity_server_path}")
    logger.info(f"checkpoint_path: {checkpoint_path}")
    logger.info(f"config:\n{OmegaConf.to_yaml(config)}")
    logger.info(f"unity_params: {unity_params}")

    if config.train.env_count > 1 and unity_server_path:
        unity_envs = [partial(make_unity_env, str(unity_path), 0, unity_params)]
        for i in range(1, config.train.env_count):
            unity_envs.append(
                partial(make_unity_env, str(unity_server_path), i, unity_params)
            )
        unity_env = VecMonitor(SubprocVecEnv(unity_envs))
    else:
        unity_env = Monitor(
            UnityEnv(
                unity_path=str(unity_path) if unity_path else None,
                parameters=unity_params,
            )
        )

    if checkpoint_path:
        model = SAC.load(
            path=checkpoint_path,
            env=unity_env,
            custom_objects={
                "learning_starts": config.train.prepare_count,
                "gradient_steps": config.train.gradient_count,
                "train_freq": config.train.train_interval,
                "batch_size": config.train.batch_size,
            },
        )
    else:
        policy_kwargs = OmegaConf.to_container(config.model.policy_kwargs, resolve=True)

        if "features_extractor_class" in policy_kwargs:
            policy_kwargs["features_extractor_class"] = get_class(
                policy_kwargs["features_extractor_class"]
            )

        model = SAC(
            policy=MultiInputPolicy,
            learning_starts=config.train.prepare_count,
            gradient_steps=config.train.gradient_count,
            train_freq=config.train.train_interval,
            batch_size=config.train.batch_size,
            env=unity_env,
            policy_kwargs=policy_kwargs,
        )

    model.set_logger(configure(str(log_dir), ["tensorboard"]))
    model.learn(
        total_timesteps=config.train.step_count,
        callback=[
            CheckpointCallback(
                interval=config.train.checkpoint_interval,
                directory=str(checkpoint_dir),
                unity_env=unity_env,
            ),
            CurriculumCallback(
                unity_env=unity_env,
                config=config,
                steps_count=config.train.step_count,
            ),
        ],
    )

    model.save(str(model_path))
    unity_env.close()


if __name__ == "__main__":
    logging.getLogger("mlagents_envs").setLevel(logging.WARNING)
    run()
