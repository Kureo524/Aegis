# Painted ground of the arena (240 m square, 2048 px): forest-floor outside, a grassy clearing of
# radius 40 with worn dirt patches, a trampled dirt plaza at the center and a dirt rim under the
# boundary rocks. Plus a tiling grass detail texture.
import numpy as np, sys
from PIL import Image
rng = np.random.default_rng(7)
N, SIZE = 2048, 100.0
def noise(n, octaves, base, rng):
    out = np.zeros((n, n)); amp = 1.0; tot = 0
    for o in range(octaves):
        g = base * 2 ** o
        r = rng.random((g + 1, g + 1))
        img = Image.fromarray((r * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
        out += np.asarray(img, dtype=float) / 255 * amp; tot += amp; amp *= 0.5
    return out / tot
y, x = np.mgrid[0:N, 0:N]
wx = (x / N - 0.5) * SIZE; wz = (0.5 - y / N) * SIZE
r = np.sqrt(wx ** 2 + wz ** 2)
n1 = noise(N, 6, 6, rng); n2 = noise(N, 5, 14, rng); n3 = noise(N, 4, 40, rng)
grass = np.array([78, 118, 62]); grass_dark = np.array([52, 88, 50]); grass_light = np.array([104, 140, 70])
dirt = np.array([128, 104, 72]); dirt_dark = np.array([96, 78, 56]); forest = np.array([40, 62, 42])
def mix(a, b, t): return a * (1 - t[..., None]) + b * t[..., None]
col = mix(grass_dark, grass_light, np.clip((n1 - 0.3) * 1.8, 0, 1))
col = mix(col, grass, 0.35 * np.ones_like(n1))
# Worn dirt: plaza at the center, patches in the clearing, rim at the edge
plaza = np.clip(1 - (r - 7 + (n2 - 0.5) * 6) / 3, 0, 1)
patches = np.clip((n2 - 0.58) * 6, 0, 1) * np.clip((38 - r) / 6, 0, 1)
ang = np.arctan2(wz, wx)
paths = np.zeros_like(r)
for a0 in (0.35, 2.4, 4.3):   # three worn trails from the plaza to the rim
    da = np.angle(np.exp(1j * (ang - a0 - (n1 - 0.5) * 0.5)))
    width = 1.6 + n2 * 1.2
    paths = np.maximum(paths, np.clip(1 - np.abs(da * r) / width, 0, 1) * np.clip((r - 5) / 3, 0, 1) * np.clip((40 - r) / 3, 0, 1))
patches = np.maximum(patches, paths * 0.85)
rim = np.clip(1 - np.abs(r - 41.5 + (n2 - 0.5) * 5) / 3.5, 0, 1)
d = np.clip(np.maximum(np.maximum(plaza, patches), rim), 0, 1)
dcol = mix(dirt_dark, dirt, np.clip((n3 - 0.3) * 2, 0, 1))
col = mix(col, dcol, d)
# Forest floor outside the clearing
out = np.clip((r - 43 + (n1 - 0.5) * 8) / 6, 0, 1)
fcol = mix(forest, grass_dark * 0.8, np.clip(n2 * 1.4 - 0.4, 0, 1))
col = mix(col, fcol, out)
# Fine grain
col *= (0.9 + 0.2 * n3)[..., None]
Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(sys.argv[1] + '/Ground_Arena.png')
# Forest floor tile (outside the painted square), same colours as the painted edge
T = 512
f1 = noise(T, 5, 8, rng); f1 = (f1 + np.roll(f1, T // 2, 0) + np.roll(f1, T // 2, 1) + np.roll(np.roll(f1, T // 2, 0), T // 2, 1)) / 4
ft = mix(forest, grass_dark * 0.8, np.clip(f1 * 1.4 - 0.4, 0, 1)) * (0.92 + 0.16 * f1)[..., None]
Image.fromarray(np.clip(ft, 0, 255).astype(np.uint8)).save(sys.argv[1] + '/Ground_Forest.png')
# Detail: grey around 128 (URP detail albedo x2), small blade speckles, tiling
M = 512
d1 = noise(M, 4, 32, rng); d1 = (d1 + np.roll(d1, M // 2, 0) + np.roll(d1, M // 2, 1) + np.roll(np.roll(d1, M // 2, 0), M // 2, 1)) / 4
speck = rng.random((M, M)) ** 6
det = 128 + (d1 - 0.5) * 70 + speck * 40
Image.fromarray(np.clip(det, 0, 255).astype(np.uint8)).convert('RGB').save(sys.argv[1] + '/Ground_Detail.png')
print('ok')
