import logging
import os
from pathlib import Path

from dotenv import load_dotenv

logger = logging.getLogger(__name__)


load_dotenv(Path.cwd() / ".env.test")

BASE_PORT = 5004

CHECKPOINT_INTERVAL = "checkpoint_interval"
LOG_INTERVAL = "log_interval"

STEP_COUNT = "step_count"
ENV_COUNT = "env_count"

CHECKPOINT_PATH = "checkpoint_path"
UNITY_PATH = "unity_path"
UNITY_SERVER_PATH = "unity_server_path"


config = {
    CHECKPOINT_PATH: os.getenv("CHECKPOINT_PATH", None),
    UNITY_PATH: os.getenv("UNITY_PATH", None),
    UNITY_SERVER_PATH: os.getenv("UNITY_SERVER_PATH", None),
    STEP_COUNT: int(os.getenv("STEP_COUNT", "1_000_000")),
    ENV_COUNT: int(os.getenv("ENV_COUNT", "1")),
    CHECKPOINT_INTERVAL: int(os.getenv("CHECKPOINT_INTERVAL", "1_000")),
    LOG_INTERVAL: int(os.getenv("LOG_INTERVAL", "10")),
}

policy_config = {
    "net_arch": {
        "pi": [128, 128, 128],  # Actor network
        "qf": [128, 128, 128],  # Critic network
    }
}
