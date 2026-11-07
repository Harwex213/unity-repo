"""Turns the generated art in art/source into the game assets in src/assets."""
import numpy as np
from PIL import Image, ImageFilter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "art" / "source"
OUT = ROOT / "src" / "assets"
OUT.mkdir(exist_ok=True)


def seamless(image, band=0.06):
    """Crossfades the two sides, so the map closes around the sphere."""
    a = np.asarray(image, dtype=np.float32)
    width = a.shape[1]
    size = int(width * band)
    weight = np.linspace(0, 1, size)[None, :, None]
    out = a[:, : width - size].copy()
    out[:, :size] = a[:, width - size:] * (1 - weight) + a[:, :size] * weight
    return Image.fromarray(out.astype(np.uint8))


def trim(image, margin=8):
    box = image.getchannel("A").point(lambda v: 255 if v > 10 else 0).getbbox()
    left, top, right, bottom = box
    return image.crop((max(left - margin, 0), max(top - margin, 0), right + margin, bottom + margin))


def blur(field, radius):
    image = Image.fromarray((np.clip(field, 0, 1) * 255).astype(np.uint8))
    return np.asarray(image.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255


albedo = seamless(Image.open(SRC / "planet-albedo.png").convert("RGB"))
albedo.resize((4096, 2048), Image.LANCZOS).filter(ImageFilter.UnsharpMask(3, 60, 2)).save(OUT / "planet-albedo.webp", quality=90, method=6)

rgb = np.asarray(albedo.resize((2048, 1024), Image.LANCZOS), dtype=np.float32) / 255
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
ocean = np.clip((b - np.maximum(r, g * 0.92) + 0.02) * 9, 0, 1)
ice = np.clip((np.minimum(np.minimum(r, g), b) - 0.55) * 4, 0, 1)
ocean = blur(ocean * (1 - ice), 1.2)
height = (0.3 * r + 0.6 * g + 0.1 * b) * (1 - ocean)
height = np.clip(height + (height - blur(height, 7)) * 2.4, 0, 1)
coast = np.clip(blur(ocean, 24) * 2.4, 0, 1) * (1 - ocean)
cities = np.clip(coast * (1 - ice), 0, 1)
mask = np.stack([ocean, height, cities], axis=-1)
Image.fromarray((mask * 255).astype(np.uint8)).save(OUT / "planet-mask.webp", quality=88, method=6)

clouds = seamless(Image.open(SRC / "planet-clouds.png").convert("RGB"))
clouds.resize((2880, 1440), Image.LANCZOS).convert("L").convert("RGB").save(OUT / "planet-clouds.webp", quality=84, method=6)

nebula = Image.open(SRC / "nebula.png").convert("RGB")
nebula.resize((2560, 1707), Image.BICUBIC).save(OUT / "nebula.webp", quality=88, method=6)

island = Image.open(SRC / "island.png").convert("RGBA")
ia = np.asarray(island, dtype=np.float32).copy()
ia[..., 3] = np.clip((ia[..., 3] - 14) / 241, 0, 1) * 255

# The mist under the waterfall ends in a hard blunt tip. Here it dissolves in the void.
height, width = ia.shape[:2]
ys, xs = np.mgrid[0:height, 0:width]
pale = np.clip((ia[..., :3].min(axis=-1) - 60) / 50, 0, 1)
strip = ((xs > 972) & (xs < 1105)).astype(np.float32)
grain = np.random.default_rng(5).random((height // 6 + 1, width // 6 + 1)).astype(np.float32)
grain = np.asarray(Image.fromarray((grain * 255).astype(np.uint8)).resize((width, height), Image.BICUBIC), dtype=np.float32) / 255
fall = np.clip((ys + (grain - 0.5) * 34 - 668) / 62, 0, 1)
fall = fall * fall * (3 - 2 * fall)
ia[..., 3] *= 1 - blur(pale * strip, 3) * fall
island = trim(Image.fromarray(ia.astype(np.uint8)))
island.save(OUT / "island.webp", quality=93, method=6, exact=False)
print("island", island.size)

logo = np.asarray(Image.open(SRC / "logo-a.png").convert("RGB"), dtype=np.float32) / 255
alpha = np.clip((logo.max(axis=-1) - 0.09) / 0.46, 0, 1) ** 0.8
colour = np.clip(logo / np.maximum(logo.max(axis=-1, keepdims=True), 0.04), 0, 1)
colour = colour * np.clip(logo.max(axis=-1, keepdims=True) * 1.25, 0, 1) ** 0.75
out = np.dstack([colour, alpha])
logo_image = trim(Image.fromarray((out * 255).astype(np.uint8)), 18)
logo_image.save(OUT / "logo.webp", quality=95, method=6)
print("logo", logo_image.size)

sigil = trim(Image.open(SRC / "sigil.png").convert("RGBA"))
sigil.resize((420, round(420 * sigil.height / sigil.width)), Image.LANCZOS).save(OUT / "sigil.webp", quality=95, method=6)

for name in ("rock", "lava"):
    Image.open(SRC / f"{name}.png").convert("RGB").resize((1024, 1024), Image.LANCZOS).save(OUT / f"{name}.webp", quality=86, method=6)


def spectral(size, exponent, low, seed):
    """A tiling fBm: white noise shaped in the frequency domain."""
    random = np.random.default_rng(seed)
    white = random.standard_normal((size, size, size), dtype=np.float32)
    f = np.fft.fftfreq(size) * size
    fx, fy, fz = np.meshgrid(f, f, f, indexing="ij", sparse=True)
    radius = np.sqrt(fx * fx + fy * fy + fz * fz)
    shape = np.where(radius >= low, 1 / np.maximum(radius, 1) ** exponent, 0)
    field = np.fft.ifftn(np.fft.fftn(white) * shape).real.astype(np.float32)
    return field


def normalise(field):
    low, high = np.percentile(field, [0.5, 99.5])
    return np.clip((field - low) / (high - low), 0, 1)


SIZE = 128
soft = normalise(spectral(SIZE, 1.75, 2, 11))
billow = spectral(SIZE, 1.55, 3, 12)
billow = normalise(1 - np.abs(billow) / np.percentile(np.abs(billow), 99.5))
volume = np.stack([soft, billow], axis=-1)
(OUT / "noise3d.bin").write_bytes((volume * 255).astype(np.uint8).tobytes())
print("noise", soft.mean(), billow.mean())
