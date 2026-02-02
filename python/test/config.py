import logging
import os
from pathlib import Path

import yaml
from dotenv import load_dotenv


logger = logging.getLogger(__name__)


load_dotenv(Path.cwd() / ".env")


class Config:
    def __init__(self):
        self.config_path = self._validate_path(os.getenv("CONFIG_PATH", None))
        if self.config_path is None:
            self.config_path = Path(__file__).parent / "examples" / "config.yml.example"

        with open(self.config_path, encoding="utf-8") as file:
            for key, value in yaml.safe_load(file).items():
                setattr(self, key, value)

        self.policy_kwargs: dict

        self.checkpoint_path = os.getenv("CHECKPOINT_PATH", None)
        self.unity_path = self._validate_path(os.getenv("UNITY_PATH", None))
        self.unity_server_path = self._validate_path(
            os.getenv("UNITY_SERVER_PATH", None)
        )
        self.step_count = int(os.getenv("STEP_COUNT", "1_000_000"))
        self.env_count = int(os.getenv("ENV_COUNT", "1"))
        self.checkpoint_interval = int(os.getenv("CHECKPOINT_INTERVAL", "1_000"))
        self.log_interval = int(os.getenv("LOG_INTERVAL", "10"))

        if self.unity_server_path is None and self.env_count > 1:
            logger.warning("no server exe, forcing env_count to 1")
            self.env_count = 1

    def __str__(self):
        items = []
        for key, value in self.__dict__.items():
            if isinstance(value, dict):
                dict_str = ", ".join(f"{k}: {v}" for k, v in value.items())
                items.append(f"{key}: {{{dict_str}}}")
            else:
                items.append(f"{key}: {value}")
        return "\n".join(items)

    @staticmethod
    def _validate_path(path_str):
        if not path_str:
            return None
        path = Path(path_str)
        if not path.exists():
            logger.warning(f"path invalid: {path}")
            return None
        return path


config = Config()
