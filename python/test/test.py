from stable_baselines3 import SAC
from stable_baselines3.common.callbacks import CheckpointCallback
from pathlib import Path
from datetime import datetime

from .test_unity_gymnasium import TestUnityGymnasium
from .env import config


def main():
    base_path = Path.cwd()

    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    save_dir = base_path / "tests" / timestamp
    checkpoint_dir = save_dir / "cps"
    log_dir = save_dir / "logs"

    env = TestUnityGymnasium()

    checkpoint_callback = CheckpointCallback(
        save_freq=config["save_freq"],
        save_path=str(checkpoint_dir),
        name_prefix="cp",
    )

    checkpoint_path = config["checkpoint_path"]
    if checkpoint_path and Path(checkpoint_path).exists():
        model = SAC.load(
            checkpoint_path, env=env, verbose=1, tensorboard_log=str(log_dir)
        )
    else:
        model = SAC("MlpPolicy", env, verbose=1, tensorboard_log=str(log_dir))

    model.learn(
        total_timesteps=config["total_timesteps"],
        callback=checkpoint_callback,
        log_interval=config["log_interval"],
    )

    model.save(str(save_dir / "result"))
    env.close()


if __name__ == "__main__":
    main()
