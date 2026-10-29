"""Commercially-licensed stand-in for the RMBG-2.0 (CC BY-NC) background remover.

Uses the `rembg` package with isnet-general-use (Apache-2.0) on CPU, exposing
the same interface as trellis2.pipelines.rembg.BiRefNet.
"""
from PIL import Image
from rembg import new_session, remove


class FreeRembg:
    def __init__(self, model_name: str = "isnet-general-use", **_):
        self.session = new_session("isnet-general-use")

    def to(self, device):
        return self

    def cuda(self):
        return self

    def cpu(self):
        return self

    def __call__(self, image: Image.Image) -> Image.Image:
        return remove(image.convert("RGB"), session=self.session).convert("RGBA")
