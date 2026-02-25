import json
import zipfile
from pathlib import Path

from stable_baselines3.common.callbacks import BaseCallback


class CheckpointCallback(BaseCallback):
    def __init__(
        self,
        interval: int,
        directory: str,
        verbose: int = 0,
    ):
        super().__init__(verbose)
        self.interval = interval
        self.path = Path(directory)
        self.path.mkdir(parents=True, exist_ok=True)

    def _on_step(self) -> bool:
        if self.n_calls % self.interval == 0:
            step = self.num_timesteps
            file_name = f"{step}"
            ckpt_path = self.path / f"{file_name}.zip"

            self.model.save(ckpt_path)

            metadata = {}

            with zipfile.ZipFile(ckpt_path, mode="a") as archive:
                meta_json = json.dumps(metadata, indent=4)
                archive.writestr("metadata.json", meta_json)

        return True
