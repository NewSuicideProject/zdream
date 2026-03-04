import torch
from gymnasium import spaces
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor

from .encoders import NavigationEncoder, ProprioceptionEncoder, TerrainEncoder


class FeaturesExtractor(BaseFeaturesExtractor):
    def __init__(
        self,
        observation_space: spaces.Dict,
        navigation_kwargs: dict | None = None,
        proprioception_kwargs: dict | None = None,
        terrain_kwargs: dict | None = None,
        gate_ratios: dict[str, float] | None = None,
    ) -> None:
        super().__init__(observation_space, features_dim=1)

        if navigation_kwargs is None:
            navigation_kwargs = {}
        if proprioception_kwargs is None:
            proprioception_kwargs = {}
        if terrain_kwargs is None:
            terrain_kwargs = {}
        if gate_ratios is None:
            gate_ratios = {}

        self.navigation = NavigationEncoder(
            **navigation_kwargs,
        )

        self.terrain = TerrainEncoder(
            **terrain_kwargs,
        )

        self.proprioception = ProprioceptionEncoder(
            input_dim=observation_space["proprioception"].shape[0],
            **proprioception_kwargs,
        )

        for key, value in gate_ratios.items():
            self.register_buffer(key, torch.tensor(value, dtype=torch.float32))

        self._features_dim = (
            self.navigation.output_dim
            + self.terrain.output_dim
            + self.proprioception.output_dim
            + 1  # passion
        )

    def _gate(self, key: str) -> torch.Tensor:
        if hasattr(self, key):
            buf: torch.Tensor = getattr(self, key)
            return buf
        return torch.ones(1)

    def forward(self, obs: dict[str, torch.Tensor]) -> torch.Tensor:
        navigation = self.navigation(obs["navigation"]) * self._gate("navigation_ratio")
        terrain = self.terrain(obs["terrain"]) * self._gate("terrain_ratio")
        proprioception = self.proprioception(obs["proprioception"]) * self._gate(
            "proprioception_ratio"
        )
        passion = obs["passion"] * self._gate("passion_ratio")

        return torch.cat(
            [passion, proprioception, navigation, terrain],
            dim=1,
        )
