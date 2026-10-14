# Post-process rendered frames: per-frame tight crop, downscale, soft dark outline, shelf-packed
# sheet + sheet.json (rect and pivot of each frame, in pixels from the sheet's bottom-left; clips).
import json, math, os, sys
from PIL import Image, ImageFilter
ROOT = os.path.join(os.environ.get('AEGIS_BAKE', '/tmp/aegis-bake'), 'sprites')
OUTLINE = (20, 14, 26)
MAXW = 2048

def process(name):
    d = f'{ROOT}/{name}'
    meta = json.load(open(f'{d}/meta.json'))
    res, ss = meta['res'], meta['ss']
    feet_x = res / 2
    feet_y = res / 2 + meta['targetZ'] * math.cos(math.radians(meta['pitch'])) / meta['extent'] * res
    frames = []
    for clip, c in meta['clips'].items():
        for f in c['frames']:
            im = Image.open(f'{d}/{f}').convert('RGBA')
            b = im.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox() or (int(feet_x) - 2, int(feet_y) - 2, int(feet_x) + 2, int(feet_y) + 2)
            pad = 3 * ss
            l = (b[0] - pad) // ss * ss; t = (b[1] - pad) // ss * ss
            r = -(-(b[2] + pad) // ss) * ss; bt = -(-(b[3] + pad) // ss) * ss
            c2 = im.crop((l, t, r, bt)).resize(((r - l) // ss, (bt - t) // ss), Image.LANCZOS)
            a = c2.getchannel('A')
            ol = Image.new('RGBA', c2.size, OUTLINE + (0,)); ol.putalpha(a.filter(ImageFilter.MaxFilter(3)).point(lambda v: int(v * 0.8)))
            c2 = Image.alpha_composite(ol, c2)
            # pivot (feet) in pixels from the frame's bottom-left
            px = (feet_x - l) / ss; py = (bt - feet_y) / ss
            frames.append({'clip': clip, 'img': c2, 'pivot': [px, py]})
    # Shelf packing, tallest first
    order = sorted(range(len(frames)), key=lambda i: -frames[i]['img'].size[1])
    x = y = shelf = 0; pos = {}
    width = min(MAXW, max(256, int(math.sqrt(sum(f['img'].size[0] * f['img'].size[1] for f in frames)) * 1.15)))
    for i in order:
        w, h = frames[i]['img'].size
        if x + w > width: x, y, shelf = 0, y + shelf + 2, 0
        pos[i] = (x, y); x += w + 2; shelf = max(shelf, h)
    height = y + shelf
    sheet = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    for i, f in enumerate(frames): sheet.paste(f['img'], pos[i])
    sheet.save(f'{ROOT}/{name}.png', optimize=True)
    out = []
    for i, f in enumerate(frames):
        w, h = f['img'].size; px, py = pos[i]
        out.append({'clip': f['clip'], 'rect': [px, height - py - h, w, h], 'pivot': [round(f['pivot'][0] / w, 4), round(f['pivot'][1] / h, 4)]})
    clips, k = [], 0
    for clip, c in meta['clips'].items():
        clips.append({'name': clip, 'first': k, 'count': len(c['frames']), 'duration': round(c['duration'], 3), 'loop': c['loop']})
        k += len(c['frames'])
    json.dump({'ppu': meta['ppu'], 'frames': out, 'clips': clips}, open(f'{ROOT}/{name}.json', 'w'), indent=1)
    print(name, 'sheet', sheet.size, 'frames', len(frames))

for n in (sys.argv[1:] or sorted(x for x in os.listdir(ROOT) if os.path.isdir(f'{ROOT}/{x}'))): process(n)
