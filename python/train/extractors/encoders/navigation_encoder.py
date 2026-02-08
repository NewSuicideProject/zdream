import torch as th
import torch.nn as nn

class NavigationEncoder(nn.Module):
    def __init__(
        self, 
        input_dim=5, 
        d_model=128, 
        nhead=2, 
        num_layers=2, 
        max_t=10
    ):
        super().__init__()
        
        assert d_model % nhead == 0
        
        self.input_projection = nn.Linear(input_dim, d_model)
        self.pos_embedding = nn.Parameter(th.zeros(1, max_t, d_model))
        
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=d_model,
            nhead=nhead,
            dim_feedforward=d_model * 4,
            batch_first=True,
            activation="gelu"
        )
        
        self.transformer_blocks = nn.TransformerEncoder(
            encoder_layer, 
            num_layers=num_layers
        )
        
        self.output_dim = d_model

    def forward(self, x):
        x = self.input_projection(x)
        
        x = x + self.pos_embedding[:, :x.size(1), :]
        
        x = self.transformer_blocks(x)
        
        return x.mean(dim=1)