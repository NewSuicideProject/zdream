from stable_baselines3.common.callbacks import BaseCallback


class CurriculumCallback(BaseCallback):
    def __init__(
        self,
        verbose: int = 0,
    ):
        super().__init__(verbose)

    def _on_step(self) -> bool:
        return True
