import hydra
import torch
import torch.nn as nn
from omegaconf import DictConfig


class ProprioceptionEncoder(nn.Module):
    def __init__(
        self,
        input_dim: int,
        hidden_dims: list[int] | None = None,
        activation_fn: DictConfig | dict | type[nn.Module] | None = None,
    ) -> None:
        super().__init__()

        if hidden_dims is None:
            hidden_dims = [512, 512, 256]

        if activation_fn is None:
            activation_fn = nn.ReLU

        layers: list[nn.Module] = []
        last_dim = input_dim

        for hidden_dim in hidden_dims:
            layers.append(nn.Linear(last_dim, hidden_dim))
            if isinstance(activation_fn, (DictConfig, dict)):
                layers.append(hydra.utils.instantiate(activation_fn))
            else:
                layers.append(activation_fn())
            last_dim = hidden_dim

        self.net = nn.Sequential(*layers)
        self.output_dim = last_dim

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x)
