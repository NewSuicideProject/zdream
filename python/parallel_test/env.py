import os
import logging
from pathlib import Path
from dotenv import load_dotenv

logger = logging.getLogger(__name__)


load_dotenv(Path.cwd() / ".env.parallel_test")

config = {
    "save_freq": int(os.getenv("SAVE_FREQ", "1_000")),
    "log_interval": int(os.getenv("LOG_INTERVAL", "10")),
    "total_timesteps": int(os.getenv("TOTAL_TIMESTEPS", "1_000_000")),
    "checkpoint_path": os.getenv("CHECKPOINT_PATH", None),
    "base_port": int(os.getenv("BASE_PORT", "5004")),
}

policy_config = {
    "net_arch": {
        "pi": [128, 128, 128],  # Actor network
        "qf": [128, 128, 128],  # Critic network
    }
}

logger.info(f"parallel test config: {config}")
logger.info(f"policy config: {policy_config}")
