import logging
from datetime import datetime
from functools import partial
from pathlib import Path

from stable_baselines3.common.callbacks import CheckpointCallback
from stable_baselines3.common.vec_env import SubprocVecEnv, VecMonitor
from stable_baselines3.sac import SAC
from stable_baselines3.sac.policies import MlpPolicy

from .config import config
from .unity_env import UnityEnv

logger = logging.getLogger(__name__)


def make_unity_env(file_name, base_port, worker_id):
    port = base_port + worker_id
    return UnityEnv(file_name=file_name, base_port=port)


def run():
    logger.info(f"config: {config}")

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    base_dir = Path.cwd() / "tests" / timestamp
    log_dir = base_dir / "log"
    model_path = base_dir / "result.zip"
    checkpoint_dir = base_dir / "checkpoints"

    envs = []
    envs.append(partial(make_unity_env, str(config.unity_path), 5004, 0))
    for i in range(1, config.env_count):
        envs.append(
            partial(
                make_unity_env,
                str(config.unity_server_path),
                5004,
                i,
            )
        )
    env = SubprocVecEnv(envs)
    env = VecMonitor(env)

    checkpoint_callback = CheckpointCallback(
        save_freq=config.checkpoint_interval,
        name_prefix="checkpoint",
        save_path=str(checkpoint_dir),
    )

    checkpoint_path = config.checkpoint_path
    if checkpoint_path and Path(checkpoint_path).exists():
        logger.info(f"valid checkpoint: {checkpoint_path}")
        model = SAC.load(
            path=checkpoint_path, env=env, tensorboard_log=str(log_dir)
        )
    else:
        logger.info("no valid checkpoint")
        model = SAC(
            policy=MlpPolicy,
            env=env,
            policy_kwargs=config.policy_kwargs,
            tensorboard_log=str(log_dir),
        )

    model.learn(
        total_timesteps=config.step_count,
        callback=checkpoint_callback,
        log_interval=config.log_interval,
        tb_log_name="test",
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
