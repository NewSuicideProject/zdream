import math

import torch as th
import torch.nn as nn


class NavigationEncoder(nn.Module):
    def __init__(
        self,
        d_model=128,
        activation_fn=None,
        num_layers=1,
        max_token=3,
        nhead=1,
    ):
        super().__init__()

        self.d_model = d_model
        self.num_layers = num_layers
        self.max_token = max_token
        self.nhead = nhead
        self.token_size = 6

        if isinstance(activation_fn, str):
            from stable_baselines3.common.torch_layers import get_activation_fn

            activation_fn = get_activation_fn(activation_fn)

        if activation_fn is None:
            activation_fn = nn.ReLU()

        self.input_projection = nn.Linear(self.token_size, self.d_model)

        pe = th.zeros(max_token, d_model)
        position = th.arange(0, max_token, dtype=th.float).unsqueeze(1)
        div_term = th.exp(
            th.arange(0, d_model, 2).float() * (-math.log(10000.0) / d_model)
        )
        pe[:, 0::2] = th.sin(position * div_term)
        pe[:, 1::2] = th.cos(position * div_term)
        self.register_buffer("pos_embedding", pe.unsqueeze(0))

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
        batch_size = x.shape[0]
        x = x.view(batch_size, self.max_token, self.token_size)

        valid_flags = x[:, :, -1]
        valid_mask = valid_flags.unsqueeze(-1)
        padding_mask = valid_flags == 0

        x = self.input_projection(x)
        x = x + self.pos_embedding[:, : x.size(1), :]

        if padding_mask.all():
            return th.zeros(batch_size, self.d_model, device=x.device, dtype=x.dtype)

        x = self.transformer_blocks(x, src_key_padding_mask=padding_mask)
        x = x * valid_mask
        x = x.sum(dim=1)
        x = x / valid_mask.sum(dim=1).clamp(min=1)

        return x
