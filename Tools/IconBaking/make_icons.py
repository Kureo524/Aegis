# Icons from game-icons.net (CC BY 3.0, see ICONS.md): a white glyph on a radial gradient tile
# coloured by category / rarity, with a soft drop shadow and a light rim. Stats and statuses:
# the glyph alone, tinted. Usage: make_icons.py <spec.json> <icons-master dir>
import json, os, re, subprocess, sys, io
from PIL import Image, ImageDraw, ImageFilter, ImageChops
spec = json.load(open(sys.argv[1])); GI = sys.argv[2]
index = {}
for root, _, files in os.walk(GI):
    for f in files:
        if f.endswith('.svg'): index.setdefault(f[:-4], os.path.join(root, f))
used = {}

def glyph(name, size, color=(255, 255, 255)):
    path = index[name]
    svg = open(path).read()
    svg = svg.replace('<path d="M0 0h512v512H0z"/>', '')
    svg = re.sub(r'fill="#fff"', 'fill="#%02x%02x%02x"' % color, svg)
    png = subprocess.run(['rsvg-convert', '-w', str(size), '-h', str(size)], input=svg.encode(), capture_output=True, check=True).stdout
    used[name] = os.path.relpath(path, GI)
    return Image.open(io.BytesIO(png)).convert('RGBA')

def tile(name, c1, c2, rim, size=128):
    S = size * 2
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    # radial gradient background
    bg = Image.new('RGBA', (S, S))
    px = bg.load()
    for y in range(S):
        for x in range(S):
            d = min(1.0, (((x - S * 0.5) ** 2 + (y - S * 0.38) ** 2) ** 0.5) / (S * 0.72))
            px[x, y] = tuple(int(c1[i] + (c2[i] - c1[i]) * d) for i in range(3)) + (255,)
    mask = Image.new('L', (S, S), 0); ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=S // 9, fill=255)
    im.paste(bg, (0, 0), mask)
    g = glyph(name, int(S * 0.74))
    off = ((S - g.size[0]) // 2, (S - g.size[1]) // 2)
    shadow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    sh = Image.new('RGBA', g.size, (0, 0, 0, 200)); sh.putalpha(g.getchannel('A').point(lambda a: int(a * 0.8)))
    shadow.paste(sh, (off[0] + 5, off[1] + 7), sh); shadow = shadow.filter(ImageFilter.GaussianBlur(6))
    im = Image.alpha_composite(im, ImageChops.multiply(shadow, Image.new('RGBA', (S, S), (255, 255, 255, 255))) if False else shadow)
    # glyph: cream with a light top
    grad = Image.new('RGBA', g.size)
    gp = grad.load()
    for y in range(g.size[1]):
        t = y / g.size[1]
        col = (255, int(250 - 25 * t), int(235 - 55 * t))
        for x in range(g.size[0]): gp[x, y] = col + (255,)
    grad.putalpha(g.getchannel('A'))
    layer = Image.new('RGBA', (S, S), (0, 0, 0, 0)); layer.paste(grad, off, grad)
    im = Image.alpha_composite(im, layer)
    rimimg = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(rimimg)
    d.rounded_rectangle((3, 3, S - 4, S - 4), radius=S // 9 - 2, outline=tuple(rim) + (230,), width=6)
    d.rounded_rectangle((10, 10, S - 11, S - 11), radius=S // 9 - 6, outline=(255, 255, 255, 40), width=2)
    im = Image.alpha_composite(im, rimimg)
    im.putalpha(ImageChops.multiply(im.getchannel('A'), mask))
    return im.resize((size, size), Image.LANCZOS)

def bare(name, color, size=64):
    g = glyph(name, size * 2, tuple(color))
    out = Image.new('RGBA', g.size, (0, 0, 0, 0))
    sh = Image.new('RGBA', g.size, (0, 0, 0, 220)); sh.putalpha(g.getchannel('A').filter(ImageFilter.MaxFilter(7)))
    out = Image.alpha_composite(out, sh); out = Image.alpha_composite(out, g)
    return out.resize((size, size), Image.LANCZOS)

missing = []
for out, name, kind, pal in spec:
    if name not in index: missing.append((out, name)); continue
    img = tile(name, *pal) if kind == 'tile' else bare(name, pal[0])
    os.makedirs(os.path.dirname(out), exist_ok=True); img.save(out)
json.dump(used, open(os.path.join(os.path.dirname(sys.argv[1]), 'used.json'), 'w'), indent=1)
print('made', len(spec) - len(missing), 'missing', missing)
