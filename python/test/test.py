from stable_baselines3 import SAC
from stable_baselines3.common.callbacks import CheckpointCallback
from pathlib import Path
from datetime import datetime

from .test_unity_gymnasium import TestUnityGymnasium


def main():
    base_path = Path.cwd()
    timestamp = datetime.now().strftime("%Y%m%d%H%M%S")
    save_dir = base_path / "tests" / timestamp
    checkpoint_dir = save_dir / "cps"
    log_dir = save_dir / "logs"

    env = TestUnityGymnasium()

    checkpoint_callback = CheckpointCallback(
        save_freq=1000, save_path=str(checkpoint_dir), name_prefix="cp"
    )

    model = SAC("MlpPolicy", env, verbose=1, tensorboard_log=str(log_dir))
    model.learn(
        total_timesteps=10000, callback=checkpoint_callback, log_interval=10
    )

    model.save(str(save_dir / "result"))
    env.close()


if __name__ == "__main__":
    main()
