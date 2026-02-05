import torch
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor

from .stream_a_mlp import StreamAEncoder
from .stream_b_mlp import StreamBEncoder
from .stream_c_mlp import StreamCEncoder


class CustomFeatureExtractor(BaseFeaturesExtractor):
    def __init__(self, observation_space, **kwargs):
        super().__init__(observation_space, features_dim=1)

        activation_fn = kwargs["activation_fn"]

        self.stream_a = StreamAEncoder(
            input_dim=kwargs["stream_a_dim"],
            hidden_dims=kwargs["net_arch_a"],
            activation_fn=activation_fn,
        )

        self.stream_b = StreamBEncoder(
            input_dim=kwargs["stream_b_dim"],
            hidden_dims=kwargs["net_arch_b"],
            activation_fn=activation_fn,
        )

        self.stream_c = StreamCEncoder(
            input_dim=kwargs["stream_c_dim"],
            hidden_dims=kwargs["net_arch_c"],
            activation_fn=activation_fn,
        )

        self._features_dim = (
            self.stream_a.output_dim
            + self.stream_b.output_dim
            + self.stream_c.output_dim
        )

    def forward(self, obs):
        a = self.stream_a(obs["stream_a"])
        b = self.stream_b(obs["stream_b"])
        c = self.stream_c(obs["stream_c"])

        return torch.cat([a, b, c], dim=1)
