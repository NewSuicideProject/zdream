import torch
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor

from .encoders.navigation_encoder import NavigationEncoder
from .encoders.proprioception_encoder import ProprioceptionEncoder
from .encoders.terrain_encoder import TerrainEncoder


class FeatureExtractor(BaseFeaturesExtractor):
    def __init__(
        self,
        observation_space,
        navigation_kwargs=None,
        proprioception_kwargs=None,
        terrain_kwargs=None,
    ):
        super().__init__(observation_space, features_dim=1)

        if navigation_kwargs is None:
            navigation_kwargs = {}
        if proprioception_kwargs is None:
            proprioception_kwargs = {}
        if terrain_kwargs is None:
            terrain_kwargs = {}

        self.navigation = NavigationEncoder(
            input_dim=observation_space["navigation"].shape[0],
            **navigation_kwargs,
        )

        self.terrain = TerrainEncoder(
            input_dim=observation_space["terrain"].shape[0],
            **terrain_kwargs,
        )

        self.proprioception = ProprioceptionEncoder(
            input_dim=observation_space["proprioception"].shape[0],
            **proprioception_kwargs,
        )

        self._features_dim = (
            self.navigation.output_dim
            + self.terrain.output_dim
            + self.proprioception.output_dim
        )

    def forward(self, obs):
        nav = self.navigation(obs["navigation"])
        ter = self.terrain(obs["terrain"])
        prop = self.proprioception(obs["proprioception"])

        return torch.cat([nav, ter, prop], dim=1)
