import logging
import os
from pathlib import Path

from dotenv import load_dotenv

logger = logging.getLogger(__name__)


load_dotenv(Path.cwd() / ".env.test")

BASE_PORT = 5004

config = {
    "checkpoint_path": os.getenv("CHECKPOINT_PATH", None),
    "file_name": os.getenv("UNITY_EXE_PATH", None),
    "total_timesteps": int(os.getenv("STEP_COUNT", "1_000_000")),
    "num_envs": int(os.getenv("ENV_COUNT", "1")),
    "save_freq": int(os.getenv("CHECKPOINT_INTERVAL", "1_000")),
    "log_interval": int(os.getenv("LOG_INTERVAL", "10")),
}

policy_config = {
    "net_arch": {
        "pi": [128, 128, 128],  # Actor network
        "qf": [128, 128, 128],  # Critic network
    }
}

logger.info(f"test config: {config}")
logger.info(f"policy config: {policy_config}")
