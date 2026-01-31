import logging
from datetime import datetime
from functools import partial
from pathlib import Path

from stable_baselines3 import SAC
from stable_baselines3.common.callbacks import CheckpointCallback
from stable_baselines3.common.vec_env import SubprocVecEnv

from .env import BASE_PORT, config, policy_config
from .envs.unity_env import UnityEnv

logger = logging.getLogger(__name__)


def make_unity_env(file_name, base_port, worker_id):
    port = base_port + worker_id
    return UnityEnv(file_name=file_name, base_port=port)


def run():
    base_path = Path.cwd()

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    save_dir = base_path / "tests" / timestamp
    checkpoint_dir = save_dir / "cps"
    log_dir = save_dir / "logs"

    file_name = config.get("file_name", None)
    num_envs = config.get("num_envs", 1)

    if num_envs > 1:
        env_fns = [
            partial(make_unity_env, file_name, BASE_PORT, i)
            for i in range(num_envs)
        ]
        env = SubprocVecEnv(env_fns)
    else:
        env = UnityEnv(file_name=file_name, base_port=BASE_PORT)

    checkpoint_callback = CheckpointCallback(
        save_freq=config.get("save_freq", 1_000),
        save_path=str(checkpoint_dir),
        name_prefix="cp",
    )

    policy_kwargs = policy_config.copy()

    checkpoint_path = config.get("checkpoint_path", None)
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
        total_timesteps=config.get("total_timesteps", 1_000_000),
        callback=checkpoint_callback,
        log_interval=config.get("log_interval", 10),
    )

    model.save(str(save_dir))
    env.close()


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="[%(levelname)s] %(message)s",
        force=True,
    )
    logging.getLogger("mlagents_envs").setLevel(logging.WARNING)
    run()
