import logging
from datetime import datetime
from functools import partial
from pathlib import Path

from stable_baselines3 import SAC
from stable_baselines3.common.callbacks import CheckpointCallback
from stable_baselines3.common.vec_env import SubprocVecEnv, VecMonitor

from .env import (
    BASE_PORT,
    CHECKPOINT_INTERVAL,
    CHECKPOINT_PATH,
    ENV_COUNT,
    LOG_INTERVAL,
    STEP_COUNT,
    UNITY_PATH,
    UNITY_SERVER_PATH,
    config,
    policy_config,
)
from .unity_env import UnityEnv

logger = logging.getLogger(__name__)


def make_unity_env(file_name, base_port, worker_id):
    port = base_port + worker_id
    return UnityEnv(file_name=file_name, base_port=port)


def run():
    logger.info(f"config: {config}")
    logger.info(f"policy config: {policy_config}")

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    base_dir = Path.cwd() / "tests" / timestamp
    log_dir = base_dir / "log"
    model_path = base_dir / "result.zip"
    checkpoint_dir = base_dir / "checkpoints"

    unity_path = config.get(UNITY_PATH, None)
    unity_server_path = config.get(UNITY_SERVER_PATH, None)

    unity_path = Path(unity_path) if unity_path else None
    unity_server_path = Path(unity_server_path) if unity_server_path else None

    if unity_path and not unity_path.exists():
        logger.warning(f"exe not found: {unity_path}")
        unity_path = None

    if unity_server_path and not unity_server_path.exists():
        logger.warning(f"server exe not found: {unity_server_path}")
        unity_server_path = None

    num_envs = config.get(ENV_COUNT, 1)

    if unity_server_path is None and num_envs > 1:
        logger.warning("no server exe forcing env_count to 1")
        num_envs = 1

    envs = []
    envs.append(partial(make_unity_env, str(unity_path), BASE_PORT, 0))
    for i in range(1, num_envs):
        envs.append(
            partial(make_unity_env, str(unity_server_path), BASE_PORT, i)
        )
    env = SubprocVecEnv(envs)
    env = VecMonitor(env)

    checkpoint_callback = CheckpointCallback(
        save_freq=config.get(CHECKPOINT_INTERVAL, 1_000),
        name_prefix="checkpoint",
        save_path=str(checkpoint_dir),
    )

    policy_kwargs = policy_config.copy()

    checkpoint_path = config.get(CHECKPOINT_PATH, None)
    if checkpoint_path and Path(checkpoint_path).exists():
        logger.info(f"valid checkpoint: {checkpoint_path}")
        model = SAC.load(
            checkpoint_path, env=env, verbose=1, tensorboard_log=str(log_dir)
        )
    else:
        logger.info("no valid checkpoint")
        model = SAC(
            "MlpPolicy",
            env,
            policy_kwargs=policy_kwargs,
            verbose=1,
            tensorboard_log=str(log_dir),
        )

    model.learn(
        total_timesteps=config.get(STEP_COUNT, 1_000_000),
        callback=checkpoint_callback,
        log_interval=config.get(LOG_INTERVAL, 10),
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
