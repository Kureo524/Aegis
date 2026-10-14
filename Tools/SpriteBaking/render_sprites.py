# Renders every clip of every character of chars.json into frames, from the game camera pitch,
# 3/4 view facing screen right. Output: sprites/<Char>/<clip>_<i>.png + meta.json (fps, size, scale).
import bpy, sys, os, json, math, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common
ROOT = os.environ.get('AEGIS_BAKE', '/tmp/aegis-bake')
cfg = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'chars.json')))
only = os.environ.get('ONLY', '')
PPU, SS, AZIMUTH = 100, 2, -55   # pixels per game unit, supersampling, camera azimuth (model faces screen right)

def model_height(meshes):
    lo, hi = common.bounds(meshes)
    return lo, hi

for name, c in cfg.items():
    if only and name not in only.split(','): continue
    common.reset()
    arm, meshes = common.import_glb(f'{ROOT}/lol/glb/{c["glb"]}.glb')
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and o.name.startswith('Icosphere'): bpy.data.objects.remove(o)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    idle = c['clips'].get('idle')
    common.play(arm, idle[0]); bpy.context.scene.frame_set(int(bpy.data.actions[idle[0]].frame_range[0]))
    lo, hi = model_height(meshes)
    h = hi.z - lo.z
    scale = c['height'] / h                       # game units per Blender unit
    extent = max(h, hi.x - lo.x, hi.y - lo.y) * 2.3   # generous frame: clips may reach further, cropped later
    target_z = h * 0.45
    res = int(extent * scale * PPU * SS)
    cam = common.setup_scene(res, extent, target_z, 0)
    # Orbit the camera (and lights) around the vertical axis instead of turning the model
    pivot = bpy.data.objects.new('pivot', None); bpy.context.scene.collection.objects.link(pivot)
    pivot.location = (0, 0, 0)
    for o in bpy.context.scene.objects:
        if o.type in ('CAMERA', 'LIGHT'): o.parent = pivot
    pivot.rotation_euler.z = math.radians(AZIMUTH)
    scn = bpy.context.scene
    scn.render.film_transparent = True
    scn.eevee.taa_render_samples = 32
    out = f'{ROOT}/sprites/{name}'; os.makedirs(out, exist_ok=True)
    meta = {'res': res, 'ss': SS, 'ppu': PPU, 'extent': extent, 'targetZ': target_z, 'scale': scale,
            'pitch': common.PITCH, 'clips': {}}
    for clip, spec in c['clips'].items():
        if not spec: continue
        act_name, n = spec[0], spec[1]
        portion = spec[2] if len(spec) > 2 else (0.45 if clip == 'death' else 1.0)   # LoL deaths sink into the ground
        act = common.play(arm, act_name)
        if act is None: print('MISSING', name, act_name); continue
        f0, f1 = act.frame_range
        f1 = f0 + (f1 - f0) * portion
        loop = clip in ('idle', 'run')
        frames = []
        for i in range(n):
            t = f0 + (f1 - f0) * (i / n if loop else i / max(1, n - 1))
            scn.frame_set(int(t), subframe=t - int(t))
            scn.render.filepath = f'{out}/{clip}_{i:02d}.png'
            bpy.ops.render.render(write_still=True)
            frames.append(os.path.basename(scn.render.filepath))
        fps_src = scn.render.fps / scn.render.fps_base
        meta['clips'][clip] = {'frames': frames, 'duration': (f1 - f0) / fps_src, 'loop': loop}
        print('CLIP', name, clip, act_name, n, 'dur', round((f1 - f0) / fps_src, 2))
    json.dump(meta, open(f'{out}/meta.json', 'w'), indent=1)
    print('DONE', name, 'res', res, 'h', round(h, 2))
