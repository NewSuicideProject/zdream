import importlib
import logging
import os
import re
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

        self.policy_kwargs = {}
        self.unity_kwargs = {}

        with open(self.config_path, encoding="utf-8") as file:
            for key, value in yaml.safe_load(file).items():
                setattr(self, key, value)

        if "features_extractor_class" in self.policy_kwargs:
            class_name = self.policy_kwargs["features_extractor_class"]
            if isinstance(class_name, str):
                module_name = re.sub(r"(?<!^)(?=[A-Z])", "_", class_name).lower()
                module_path = f".extractors.{module_name}"

                module = importlib.import_module(module_path, package=__package__)
                self.policy_kwargs["features_extractor_class"] = getattr(
                    module, class_name
                )

        self.env_count = int(os.getenv("ENV_COUNT", "1"))
        self.step_count = int(os.getenv("STEP_COUNT", "10_000_000"))
        self.prepare_count = int(os.getenv("PREPARE_COUNT", "10_000"))
        self.batch_size = int(os.getenv("BATCH_SIZE", "256"))
        self.train_interval = int(os.getenv("TRAIN_INTERVAL", "128"))
        self.gradient_count = int(os.getenv("GRADIENT_COUNT", "16"))

        self.checkpoint_path = self._validate_path(os.getenv("CHECKPOINT_PATH", None))
        self.checkpoint_interval = int(os.getenv("CHECKPOINT_INTERVAL", "10_000"))

        self.unity_path = self._validate_path(os.getenv("UNITY_PATH", None))
        self.unity_server_path = self._validate_path(
            os.getenv("UNITY_SERVER_PATH", None)
        )

        self.log_interval = int(os.getenv("LOG_INTERVAL", "10"))

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
            logger.info(f"path invalid: {path}")
            return None
        return path


config = Config()
