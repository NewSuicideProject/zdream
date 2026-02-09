import torch as th
import torch.nn as nn

class NavigationEncoder(nn.Module):
    def __init__(
        self,
        input_dim,
        hidden_dims=None,
        activation_fn=None,
    ):
        super().__init__()

        if hidden_dims is None:
            hidden_dims = [128, 128, 128]
        
        self.d_model = hidden_dims[-1]
        self.num_layers = len(hidden_dims)
        self.max_t = 10

        if self.d_model % 4 == 0:
            self.nhead = 4
        else:
            self.nhead = 2 if self.d_model % 2 == 0 else 1

        self.input_projection = nn.Linear(input_dim, self.d_model)
        self.pos_embedding = nn.Parameter(th.randn(1, self.max_t, self.d_model) * 0.02)
        
        act_str = "gelu"
        if activation_fn is not None:
            act_str = "gelu" if "GELU" in str(activation_fn) else "relu"

        encoder_layer = nn.TransformerEncoderLayer(
            d_model=self.d_model,
            nhead=self.nhead,
            dim_feedforward=self.d_model * 4,
            batch_first=True,
            activation=act_str
        )
        
        self.transformer_blocks = nn.TransformerEncoder(
            encoder_layer, 
            num_layers=self.num_layers
        )
        
        self.output_dim = self.d_model

    def forward(self, x):
        x = self.input_projection(x)
        x = x + self.pos_embedding[:, :x.size(1), :]
        x = self.transformer_blocks(x)
        return x.mean(dim=1)