import math

import torch as th
import torch.nn as nn
from omegaconf import DictConfig


class NavigationEncoder(nn.Module):
    token_size = 6

    def __init__(
        self,
        max_tokens: int = 3,
        d_model: int = 16,
        num_layers: int = 1,
        nhead: int = 2,
        activation_fn: DictConfig | nn.Module | None = None,
    ) -> None:
        super().__init__()

        self.d_model = d_model
        self.num_layers = num_layers
        self.max_tokens = max_tokens
        self.nhead = nhead

        if isinstance(activation_fn, DictConfig):
            import hydra

            activation_fn = hydra.utils.instantiate(activation_fn)

        if activation_fn is None:
            activation_fn = nn.ReLU()

        self.input_projection = nn.Linear(NavigationEncoder.token_size, self.d_model)

        pe = th.zeros(self.max_tokens, self.d_model)
        position = th.arange(0, self.max_tokens, dtype=th.float).unsqueeze(1)
        div_term = th.exp(
            th.arange(0, self.d_model, 2).float() * (-math.log(10000.0) / self.d_model)
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

    def forward(self, x: th.Tensor) -> th.Tensor:
        batch_size = x.shape[0]
        x = x.view(batch_size, self.max_tokens, NavigationEncoder.token_size)

        valid_flags = x[:, :, -1]
        valid_mask = valid_flags.unsqueeze(-1)
        padding_mask = valid_flags == 0

        x = self.input_projection(x)
        x = x + self.pos_embedding[:, : x.size(1), :]

        if padding_mask.all():
            return th.zeros(batch_size, self.d_model, device=x.device, dtype=x.dtype)

        x = self.transformer_blocks(x, src_key_padding_mask=padding_mask)
        x *= valid_mask
        x = x.sum(dim=1)
        x /= valid_mask.sum(dim=1).clamp(min=1)

        return x
