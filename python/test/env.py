import os
from pathlib import Path
from dotenv import load_dotenv


load_dotenv(Path.cwd() / ".env.test")

config = {
    "save_freq": int(os.getenv("SAVE_FREQ", "10")),
    "log_interval": int(os.getenv("LOG_INTERVAL", "5")),
    "total_timesteps": int(os.getenv("TOTAL_TIMESTEPS", "10000")),
    "checkpoint_path": os.getenv("CHECKPOINT_PATH"),
}

print(config)
