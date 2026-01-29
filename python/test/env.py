import os
import logging
from pathlib import Path
from dotenv import load_dotenv
import torch.nn as nn

logger = logging.getLogger(__name__)


load_dotenv(Path.cwd() / ".env.test")

config = {
    "save_freq": int(os.getenv("SAVE_FREQ", "10")),
    "log_interval": int(os.getenv("LOG_INTERVAL", "5")),
    "total_timesteps": int(os.getenv("TOTAL_TIMESTEPS", "10000")),
    "checkpoint_path": os.getenv("CHECKPOINT_PATH"),
}

policy_config = {
    "net_arch": {
        "pi": [128, 64, 64],  # Actor network
        "qf": [128, 64, 64],  # Critic network
    }
}

logger.info(f"test config: {config}")
logger.info(f"policy config: {policy_config}")
