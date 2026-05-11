import os
from pathlib import Path

class Transformer:
    def __init__(self, config):
        self.config = config

    def forward(self, x):
        return self._attention(x)

    def _attention(self, x):
        return x

class Pipeline(Transformer):
    def run(self):
        result = self.forward(None)
        os.path.join("a", "b")
        return result

def helper():
    p = Pipeline({})
    p.run()
