import torch
import torch.nn as nn
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor
from unity_env import HEIGHT_MAP_KEY


def build_mlp(input_dim, net_arch, activation_fn):
    """
    MLP를 자동으로 만들어주는 함수

    input_dim: 입력 차원 (int)
    net_arch: 은닉층 구조 예: [128, 256]
    activation_fn: nn.ReLU, nn.Tanh 같은 클래스
    """
    layers = []
    last_dim = input_dim

    for hidden_dim in net_arch:
        layers.append(nn.Linear(last_dim, hidden_dim))
        layers.append(activation_fn())
        last_dim = hidden_dim

    return nn.Sequential(*layers), last_dim


class FeatureExtractor(BaseFeaturesExtractor):
    def __init__(
        self,
        observation_space,
        stream_b_dim: int,
        stream_c_dim: int,
        net_arch_b=(128, 128),
        net_arch_c=(256, 256),
        activation_fn=nn.ReLU,
    ):
        """
        observation_space: SB3에서 자동으로 전달 (사용만 안 함)
        stream_b_dim: Stream B 입력 차원 (N*N)
        stream_c_dim: Stream C 입력 차원 (D_prop)
        net_arch_b: Stream B MLP 구조
        net_arch_c: Stream C MLP 구조
        activation_fn: 활성화 함수
        """

        super().__init__(observation_space, features_dim=1)

        # Stream B (Terrain Vision)
        self.stream_b, b_out_dim = HeightExtractor()

        # Stream C (Proprioception)
        self.stream_c, c_out_dim = build_mlp(
            input_dim=stream_c_dim,
            net_arch=net_arch_c,
            activation_fn=activation_fn,
        )

        # Concatenate 이후 최종 feature 차원
        self._features_dim = b_out_dim + c_out_dim

    def forward(self, observations):
        """
        observations는 Dict 형태여야 함
        {
            "stream_b": Tensor (Batch, stream_b_dim),
            "stream_c": Tensor (Batch, stream_c_dim)
        }
        """

        obs_b = observations[HEIGHT_MAP_KEY]
        obs_c = observations["stream_c"]

        out_b = self.stream_b(obs_b)
        out_c = self.stream_c(obs_c)

        # 여기서 Concatenate
        features = torch.cat([out_b, out_c], dim=1)

        return features
