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

from .test_unity_gymnasium import TestUnityGymnasium
from .env import config, policy_config

logger = logging.getLogger(__name__)


def main():
    base_path = Path.cwd()

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    save_dir = base_path / "tests" / timestamp
    checkpoint_dir = save_dir / "cps"
    log_dir = save_dir / "logs"

    env = TestUnityGymnasium()

    logger.info(f"observation space: {env.observation_space}")
    logger.info(f"action space: {env.action_space}")

    checkpoint_callback = CheckpointCallback(
        save_freq=config["save_freq"],
        save_path=str(checkpoint_dir),
        name_prefix="cp",
    )

    policy_kwargs = policy_config.copy()

    checkpoint_path = config["checkpoint_path"]
    if checkpoint_path and Path(checkpoint_path).exists():
        logger.info(f"valid checkpoint found: {checkpoint_path}")
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
        total_timesteps=config["total_timesteps"],
        callback=checkpoint_callback,
        log_interval=config["log_interval"],
    )

    model.save(str(save_dir / "result"))
    env.close()


if __name__ == "__main__":
    main()
