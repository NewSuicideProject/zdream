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
    logger.info(f"config: {config}")
    logger.info(f"policy config: {policy_config}")

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    base_dir = Path.cwd() / "tests" / timestamp
    log_dir = base_dir / "log"
    model_dir = base_dir
    checkpoint_dir = base_dir / "checkpoints"

    file_name = config.get("file_name", None)
    server_file_name = config.get("server_file_name", None)

    file_path = Path(file_name) if file_name else None
    server_file_path = Path(server_file_name) if server_file_name else None

    if file_path and not file_path.exists():
        logger.warning(f"exe not found: {file_path}")
        file_path = None

    if server_file_path and not server_file_path.exists():
        logger.warning(f"server exe not found: {server_file_path}")
        server_file_path = None

    num_envs = config.get("num_envs", 1)

    if server_file_path is None and num_envs > 1:
        logger.warning("no server exe provided. forcing num_envs to 1.")
        num_envs = 1

    if num_envs > 1:
        env_fns = []
        env_fns.append(
            partial(
                make_unity_env,
                str(file_path if file_path else server_file_path),
                BASE_PORT,
                0,
            )
        )
        for i in range(1, num_envs):
            env_fns.append(
                partial(make_unity_env, str(server_file_path), BASE_PORT, i)
            )
        env = SubprocVecEnv(env_fns)
    else:
        exe_to_use = file_path if file_path else server_file_path
        env = UnityEnv(
            file_name=str(exe_to_use) if exe_to_use else None,
            base_port=BASE_PORT,
        )

    checkpoint_callback = CheckpointCallback(
        save_freq=config.get("save_freq", 1_000),
        name_prefix="checkpoint",
        save_path=str(checkpoint_dir),
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
        tb_log_name="test",
    )

    model.save(str(model_dir))
    env.close()


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="[%(levelname)s] %(message)s",
        force=True,
    )
    logging.getLogger("mlagents_envs").setLevel(logging.WARNING)
    run()
