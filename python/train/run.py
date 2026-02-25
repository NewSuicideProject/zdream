import logging
import os
from functools import partial
from pathlib import Path

import hydra
from dotenv import load_dotenv
from hydra.utils import get_class, get_original_cwd
from omegaconf import DictConfig, OmegaConf
from stable_baselines3.common.callbacks import CheckpointCallback
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import SubprocVecEnv, VecMonitor
from stable_baselines3.sac import SAC
from stable_baselines3.sac.policies import MultiInputPolicy

from .unity_env import UnityEnv


logger = logging.getLogger(__name__)
load_dotenv(Path(__file__).parent / ".env")


def make_unity_env(file_name: str, worker_id: int, env_params: dict):
    return UnityEnv(unity_path=file_name, worker_id=worker_id, unity_kwargs=env_params)


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


def get_dict_config(cfg: DictConfig, path: str) -> DictConfig:
    node = OmegaConf.select(cfg, path)
    return node if node is not None else OmegaConf.create({})


@hydra.main(version_base=None, config_path="conf", config_name="config")
def run(config: DictConfig):
    logger.info(f"config: \n{OmegaConf.to_yaml(config)}")

    base_dir = Path.cwd()
    log_dir = base_dir / "log"
    model_path = base_dir / "model.zip"
    checkpoint_dir = base_dir / "checkpoints"

    unity_server_path = get_path(os.getenv("UNITY_SERVER_PATH", None))
    unity_path = get_path(os.getenv("UNITY_PATH", None))
    if not unity_path:
        unity_path = unity_server_path

    checkpoint_path = get_path(os.getenv("CHECKPOINT_PATH", None))

    policy_kwargs: dict = OmegaConf.to_container(
        OmegaConf.merge(
            get_dict_config(config, "model.policy_kwargs"),
            get_dict_config(config, "curriculum.policy_kwargs"),
        ),
        resolve=True,
    )

    unity_kwargs: dict = OmegaConf.to_container(
        OmegaConf.merge(
            get_dict_config(config, "model.unity_kwargs"),
            get_dict_config(config, "normalization.unity_kwargs"),
            get_dict_config(config, "curriculum.unity_kwargs"),
        ),
        resolve=True,
    )

    if "features_extractor_class" in policy_kwargs:
        policy_kwargs["features_extractor_class"] = get_class(
            policy_kwargs["features_extractor_class"]
        )

    train_cfg = config.train

    if train_cfg.env_count > 1 and unity_server_path:
        envs = [partial(make_unity_env, str(unity_path), 0, unity_kwargs)]
        for i in range(1, train_cfg.env_count):
            envs.append(
                partial(make_unity_env, str(unity_server_path), i, unity_kwargs)
            )
        env = SubprocVecEnv(envs)
        env = VecMonitor(env)
    else:
        env = UnityEnv(
            unity_path=str(unity_path) if unity_path else None,
            unity_kwargs=unity_kwargs,
        )
        env = Monitor(env)

    checkpoint_callback = CheckpointCallback(
        save_freq=train_cfg.checkpoint_interval,
        name_prefix="checkpoint",
        save_path=str(checkpoint_dir),
    )

    if checkpoint_path:
        model = SAC.load(
            path=checkpoint_path,
            env=env,
            tensorboard_log=str(log_dir),
            custom_objects={
                "learning_starts": train_cfg.prepare_count,
                "gradient_steps": train_cfg.gradient_count,
                "train_freq": train_cfg.train_interval,
                "batch_size": train_cfg.batch_size,
            },
        )
    else:
        model = SAC(
            policy=MultiInputPolicy,
            learning_starts=train_cfg.prepare_count,
            gradient_steps=train_cfg.gradient_count,
            train_freq=train_cfg.train_interval,
            batch_size=train_cfg.batch_size,
            env=env,
            policy_kwargs=policy_kwargs,
            tensorboard_log=str(log_dir),
        )

    model.learn(
        total_timesteps=train_cfg.step_count,
        callback=checkpoint_callback,
        log_interval=train_cfg.log_interval,
        tb_log_name="train",
    )

    model.save(str(model_path))
    env.close()


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="[%(levelname)s] %(message)s",
        force=True,
    )
    logging.getLogger("mlagents_envs").setLevel(logging.WARNING)
    run()
