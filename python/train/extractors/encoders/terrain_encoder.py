import torch.nn as nn


class TerrainEncoder(nn.Module):
    def __init__(
        self,
        input_dim,
        hidden_dims=None,
        activation_fn=None,
    ):
        super().__init__()

        if hidden_dims is None:
            hidden_dims = [64, 128, 256]
        if activation_fn is None:
            activation_fn = nn.ReLU

        layers = []
        last_dim = input_dim

        for hidden_dim in hidden_dims:
            layers.append(nn.Linear(last_dim, hidden_dim))
            layers.append(activation_fn())
            last_dim = hidden_dim

        self.net = nn.Sequential(*layers)
        self.output_dim = last_dim

    def forward(self, x):
        return self.net(x)
