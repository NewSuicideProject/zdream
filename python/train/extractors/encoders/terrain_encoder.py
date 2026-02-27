import torch
import torch.nn as nn


class TerrainEncoder(nn.Module):
    def __init__(
        self,
        resolution: int = 8,
        hidden_dims: list[int] | None = None,
        activation_fn: type[nn.Module] | str | None = None,
    ) -> None:
        super().__init__()

        if hidden_dims is None:
            hidden_dims = [256, 128, 64]

        if isinstance(activation_fn, str):
            from stable_baselines3.common.torch_layers import get_activation_fn

            activation_fn = get_activation_fn(activation_fn)

        if activation_fn is None:
            activation_fn = nn.ReLU

        layers: list[nn.Module] = []
        last_dim = resolution

        for hidden_dim in hidden_dims:
            layers.append(nn.Linear(last_dim, hidden_dim))
            layers.append(activation_fn())
            last_dim = hidden_dim

        self.net = nn.Sequential(*layers)
        self.output_dim = last_dim

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x)
