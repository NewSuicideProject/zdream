import torch.nn as nn


class TerrainEncoder(nn.Module):
    def __init__(
        self,
        terrain_input_dim,
        hidden_dims,
        activation_fn,
    ):
        super().__init__()

        layers = []
        last_dim = terrain_input_dim

        for h in hidden_dims:
            layers.append(nn.Linear(last_dim, h))
            layers.append(activation_fn())
            last_dim = h

        self.net = nn.Sequential(*layers)
        self.output_dim = last_dim

    def forward(self, x):
        return self.net(x)
