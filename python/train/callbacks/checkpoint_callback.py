import json
import zipfile
from pathlib import Path

from python.train.unity_env import UnityEnv
from stable_baselines3.common.callbacks import BaseCallback


class CheckpointCallback(BaseCallback):
    def __init__(
        self,
        interval: int,
        directory: str,
        unity_env: UnityEnv,
        verbose: int = 0,
    ):
        super().__init__(verbose)
        self.interval = interval
        self.directory = Path(directory)
        self.directory.mkdir(parents=True, exist_ok=True)
        self.unity_env = unity_env

    def _on_step(self) -> bool:
        if self.n_calls % self.interval == 0:
            step = self.num_timesteps
            checkpoint_path = self.directory / f"{step}.zip"

            self.model.save(checkpoint_path)

            with zipfile.ZipFile(checkpoint_path, mode="a") as archive:
                unity_params = json.dumps(self.unity_env.parameters, indent=4)
                archive.writestr("unity_params.json", unity_params)

        return True
