# UI textures of the aurora theme (Assets/UI/Textures): panel gradient, aurora backdrop of the big
# windows, divider, scrollbar track and thumb. Usage: python make_ui_textures.py (numpy + Pillow).
import os
import numpy as np
from PIL import Image, ImageFilter
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'UI', 'Textures')
NIGHT, DEEP = np.array([7, 9, 32]), np.array([3, 4, 16])
CYAN, VIOLET = np.array([92, 225, 255]), np.array([167, 123, 255])

def save(a, name):
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name))

# Panel: night blue at the top fading to deep blue, a faint violet tint in the middle
h = 256; t = np.linspace(0, 1, h)[:, None]
rgb = NIGHT * (1 - t) + DEEP * t + VIOLET * 0.06 * np.exp(-((t - 0.35) / 0.25) ** 2)
a = np.full((h, 1), 238.0)
save(np.repeat(np.concatenate([rgb, a], 1)[:, None, :], 16, 1), 'Panel.png')

# Aurora: curtains of cyan and violet light hanging from the top of a night sky
W, H = 1024, 640
x = np.linspace(0, 1, W)[None, :]; y = np.linspace(0, 1, H)[:, None]
rng = np.random.default_rng(7)
base = NIGHT * (1 - y[..., None]) * 1.0 + DEEP * y[..., None]
img = np.broadcast_to(base, (H, W, 3)).copy()
for k, (col, cy, amp, freq, width, gain) in enumerate([(CYAN, 0.16, 0.06, 2.3, 0.07, 0.42), (VIOLET, 0.26, 0.08, 1.6, 0.10, 0.38),
                                                        (CYAN * 0.6 + VIOLET * 0.4, 0.10, 0.04, 3.1, 0.05, 0.25)]):
    phase = rng.uniform(0, 6.28)
    centre = cy + amp * np.sin(2 * np.pi * freq * x + phase) + 0.02 * np.sin(2 * np.pi * 7 * x + phase * 2)
    band = np.exp(-((y - centre) / width) ** 2)
    folds = np.interp(x[0], np.linspace(0, 1, 90), rng.random(90))[None, :]   # irregular curtain folds
    rays = 0.72 + 0.28 * np.convolve(folds[0], np.ones(9) / 9, 'same')[None, :]
    tail = np.clip((y - centre + width) / 0.35, 0, 1) ** 0.6 * np.exp(-np.clip(y - centre, 0, None) / 0.22)   # light falling below
    glow = (band * 0.8 + tail * 0.5) * rays * gain
    img += glow[..., None] * col[None, None, :]
stars = rng.random((H, W)) > 0.9985
img[stars] += 120 * (1 - y[np.nonzero(stars)[0], 0])[:, None]
alpha = np.full((H, W, 1), 242.0)
im = Image.fromarray(np.clip(np.concatenate([img, alpha], 2), 0, 255).astype(np.uint8), 'RGBA').filter(ImageFilter.GaussianBlur(2.5))
im.save(os.path.join(OUT, 'Aurora.png'))

# Divider: a glowing line, cyan in the middle, violet at the ends, transparent tips
W = 256; u = np.linspace(-1, 1, W)
col = CYAN[None, :] * (1 - np.abs(u))[:, None] + VIOLET[None, :] * np.abs(u)[:, None]
fade = (1 - np.abs(u) ** 2)
rows = []
for r in range(12):
    d = abs(r - 5.5) / 5.5
    rows.append(np.concatenate([col, (255 * fade * np.exp(-(d / 0.25) ** 2))[:, None]], 1))
save(np.stack(rows), 'Divider.png')

# Scrollbar track (6x32, 9-sliced): a dim rail with a lighter line in its middle
tw, th = 8, 32
track = np.zeros((th, tw, 4))
track[..., :3] = [20, 22, 64]; track[..., 3] = 150
track[:, 3:5, :3] = [60, 62, 140]; track[:, 3:5, 3] = 200
track[:3, :, 3] *= np.linspace(0, 1, 3)[:, None]; track[-3:, :, 3] *= np.linspace(1, 0, 3)[:, None]
save(track, 'ScrollTrack.png')

# Scrollbar thumb (8x40, 9-sliced): a capsule going from violet to cyan with a bright core
tw, th = 8, 40
yy, xx = np.mgrid[0:th, 0:tw]
cx = (tw - 1) / 2
r = tw / 2
dy = np.clip(np.maximum(r - yy - 0.5, yy + 0.5 - (th - r)), 0, None)
dist = np.sqrt(((xx - cx) / r) ** 2 + (dy / r) ** 2)
inside = np.clip((1 - dist) * 4, 0, 1)
v = yy / (th - 1)
colr = VIOLET[None, None, :] * (1 - v[..., None]) + CYAN[None, None, :] * v[..., None]
core = np.exp(-((xx - cx) / 1.4) ** 2)[..., None] * 40
thumb = np.concatenate([colr * 0.8 + core, (inside * 215)[..., None]], 2)
save(thumb, 'ScrollThumb.png')
print('ok')
