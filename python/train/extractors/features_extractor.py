import torch
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor

from .encoders import NavigationEncoder, ProprioceptionEncoder, TerrainEncoder


class FeaturesExtractor(BaseFeaturesExtractor):
    def __init__(
        self,
        observation_space,
        navigation_kwargs=None,
        proprioception_kwargs=None,
        terrain_kwargs=None,
        navigation_ratio=1.0,
        terrain_ratio=1.0,
        proprioception_ratio=1.0,
        passion_ratio=1.0,
    ):
        super().__init__(observation_space, features_dim=1)

        if navigation_kwargs is None:
            navigation_kwargs = {}
        if proprioception_kwargs is None:
            proprioception_kwargs = {}
        if terrain_kwargs is None:
            terrain_kwargs = {}

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

        self.register_buffer(
            "navigation_ratio", torch.tensor(navigation_ratio, dtype=torch.float32)
        )
        self.register_buffer(
            "terrain_ratio", torch.tensor(terrain_ratio, dtype=torch.float32)
        )
        self.register_buffer(
            "proprioception_ratio",
            torch.tensor(proprioception_ratio, dtype=torch.float32),
        )
        self.register_buffer(
            "passion_ratio", torch.tensor(passion_ratio, dtype=torch.float32)
        )

        self.proprioception_ratio: torch.Tensor
        self.terrain_ratio: torch.Tensor
        self.navigation_ratio: torch.Tensor
        self.passion_ratio: torch.Tensor

        self._features_dim = (
            self.navigation.output_dim
            + self.terrain.output_dim
            + self.proprioception.output_dim
            + 1  # passion
        )

    def forward(self, obs):
        navigation = self.navigation(obs["navigation"]) * self.navigation_ratio
        terrain = self.terrain(obs["terrain"]) * self.terrain_ratio
        proprioception = (
            self.proprioception(obs["proprioception"]) * self.proprioception_ratio
        )
        passion = obs["passion"] * self.passion_ratio

        return torch.cat(
            [passion, proprioception, navigation, terrain],
            dim=1,
        )
