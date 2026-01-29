import os
from pathlib import Path
from dotenv import load_dotenv
import torch.nn as nn


load_dotenv(Path.cwd() / ".env.test")

config = {
    "save_freq": int(os.getenv("SAVE_FREQ", "10")),
    "log_interval": int(os.getenv("LOG_INTERVAL", "5")),
    "total_timesteps": int(os.getenv("TOTAL_TIMESTEPS", "10000")),
    "checkpoint_path": os.getenv("CHECKPOINT_PATH"),
}

policy_config = {
    "net_arch": {
        "pi": [32, 16, 16, 8, 8, 4],  # Actor network
        "qf": [32, 16, 16, 8, 8, 4],  # Critic network
    },
    "activation_fn": nn.ReLU,
}

print(f"test config: {config}")
print(f"policy config: {policy_config}")
