import logging

if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="[%(levelname)s] %(message)s",
        force=True,
    )
    logging.getLogger("mlagents_envs").setLevel(logging.WARNING)

from stable_baselines3 import SAC
from stable_baselines3.common.callbacks import CheckpointCallback
from pathlib import Path
from datetime import datetime

from .envs.unity_env import UnityEnv
from .envs.unity_parallel_env import UnityParallelEnv
from .env import config, policy_config

logger = logging.getLogger(__name__)


def run():
    base_path = Path.cwd()

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    save_dir = base_path / "tests" / timestamp
    checkpoint_dir = save_dir / "cps"
    log_dir = save_dir / "logs"

    base_port = config.get("base_port", 5004)

    if config.get("parallel", False):
        env = UnityParallelEnv(base_port=base_port)
    else:
        env = UnityEnv(base_port=base_port)

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

    model.save(str(save_dir / "result"))
    env.close()


if __name__ == "__main__":
    run()
