import json
import zipfile
from pathlib import Path

from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.vec_env import VecEnv


class CheckpointCallback(BaseCallback):
    def __init__(
        self,
        unity_env,
        interval,
        directory,
        verbose=0,
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
                if isinstance(self.unity_env, VecEnv):
                    params = self.unity_env.env_method("get_parameters", indices=[0])[0]
                else:
                    params = self.unity_env.get_parameters()
                archive.writestr("unity_params.json", json.dumps(params))

        return True
