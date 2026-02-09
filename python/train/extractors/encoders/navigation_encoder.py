import torch as th
import torch.nn as nn


class NavigationEncoder(nn.Module):
    def __init__(
        self,
        input_dim,
        d_model=128,
        activation_fn=None,
        num_layers=3,
        max_token=10,
    ):
        super().__init__()

        self.d_model = d_model
        self.num_layers = num_layers
        self.max_token = max_token

        if self.d_model % 4 == 0:
            self.nhead = 4
        else:
            self.nhead = 2 if self.d_model % 2 == 0 else 1

        self.input_projection = nn.Linear(input_dim, self.d_model)
        self.pos_embedding = nn.Parameter(
            th.randn(1, self.max_token, self.d_model) * 0.02
        )

        if activation_fn is None:
            activation_fn = nn.ReLU()

        encoder_layer = nn.TransformerEncoderLayer(
            d_model=self.d_model,
            nhead=self.nhead,
            dim_feedforward=self.d_model * 4,
            batch_first=True,
            activation=activation_fn,
        )

        self.transformer_blocks = nn.TransformerEncoder(
            encoder_layer, num_layers=self.num_layers
        )

        self.output_dim = self.d_model

    def forward(self, x):
        x = self.input_projection(x)
        x = x + self.pos_embedding[:, : x.size(1), :]
        x = self.transformer_blocks(x)
        return x.mean(dim=1)
