import logging
from datetime import datetime
from functools import partial
from pathlib import Path

from stable_baselines3.common.callbacks import CheckpointCallback
from stable_baselines3.common.monitor import Monitor
from stable_baselines3.common.vec_env import SubprocVecEnv, VecMonitor
from stable_baselines3.sac import SAC
from stable_baselines3.sac.policies import MultiInputPolicy

from .config import config
from .unity_env import UnityEnv


logger = logging.getLogger(__name__)


def make_unity_env(file_name: str, worker_id: int, env_params: dict):
    return UnityEnv(unity_path=file_name, worker_id=worker_id, unity_kwargs=env_params)


def run():
    logger.info(f"config: {config}")

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    base_dir = Path.cwd() / timestamp
    log_dir = base_dir / "log"
    model_path = base_dir / "result.zip"
    checkpoint_dir = base_dir / "checkpoints"

    unity_server_path = config.unity_server_path
    unity_path = config.unity_path if config.unity_path else unity_server_path

    if config.env_count > 1 and unity_server_path:
        envs = [partial(make_unity_env, str(unity_path), 0, config.unity_kwargs)]
        for i in range(1, config.env_count):
            envs.append(
                partial(make_unity_env, str(unity_server_path), i, config.unity_kwargs)
            )
        env = SubprocVecEnv(envs)
        env = VecMonitor(env)
    else:
        env = UnityEnv(
            unity_path=str(unity_path) if unity_path else None,
            unity_kwargs=config.unity_kwargs,
        )
        env = Monitor(env)

    checkpoint_callback = CheckpointCallback(
        save_freq=config.checkpoint_interval,
        name_prefix="checkpoint",
        save_path=str(checkpoint_dir),
    )

    if config.checkpoint_path:
        model = SAC.load(
            path=config.checkpoint_path,
            env=env,
            tensorboard_log=str(log_dir),
            custom_objects={
                "learning_starts": config.prepare_count,
                "gradient_steps": config.gradient_count,
                "train_freq": config.train_interval,
                "batch_size": config.batch_size,
            },
        )
    else:
        model = SAC(
            policy=MultiInputPolicy,
            learning_starts=config.prepare_count,
            gradient_steps=config.gradient_count,
            train_freq=config.train_interval,
            batch_size=config.batch_size,
            env=env,
            policy_kwargs=config.policy_kwargs,
            tensorboard_log=str(log_dir),
        )

    model.learn(
        total_timesteps=config.step_count,
        callback=checkpoint_callback,
        log_interval=config.log_interval,
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
