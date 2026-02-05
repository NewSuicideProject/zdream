import torch
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor

from .navigation_mlp import NavigationEncoder
from .proprioception_mlp import ProprioceptionEncoder
from .terrain_mlp import TerrainEncoder


class FeatureExtractor(BaseFeaturesExtractor):
    def __init__(self, observation_space, **kwargs):
        super().__init__(observation_space, features_dim=1)

        activation_fn = kwargs["activation_fn"]

        self.navigation = NavigationEncoder(
            input_dim=observation_space["navigation"].shape[0],
            hidden_dims=kwargs["net_arch_navigation"],
            activation_fn=activation_fn,
        )

        self.terrain = TerrainEncoder(
            input_dim=observation_space["terrain"].shape[0],
            hidden_dims=kwargs["net_arch_terrain"],
            activation_fn=activation_fn,
        )

        self.proprioception = ProprioceptionEncoder(
            input_dim=observation_space["proprioception"].shape[0],
            hidden_dims=kwargs["net_arch_proprioception"],
            activation_fn=activation_fn,
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
