# Head-and-shoulders portraits (HUD) of the hero and the boss: front 3/4, low pitch, idle pose.
import bpy, sys, os, json, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common
ROOT = os.environ.get('AEGIS_BAKE', '/tmp/aegis-bake')
cfg = json.load(open(os.path.join(HERE, 'chars.json')))
FRAME = {'Vesna': (0.8, 0.42), 'Grokk': (0.62, 0.6)}
for name, (center, size) in FRAME.items():
    c = cfg[name]
    common.reset()
    arm, meshes = common.import_glb(os.path.join(ROOT, c['src']))
    common.drop_helpers(); meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if c.get('recolor'): common.recolor({k: tuple(v) for k, v in c['recolor'].items()})
    idle = c['clips']['idle'][0]
    act = common.play(arm, idle); bpy.context.scene.frame_set(int(act.frame_range[0]) + 3)
    lo, hi = common.bounds(meshes); h = hi.z - lo.z
    common.PITCH = 12
    common.setup_scene(512, h * size, lo.z + h * center, 0)
    pivot = bpy.data.objects.new('pivot', None); bpy.context.scene.collection.objects.link(pivot)
    for o in bpy.context.scene.objects:
        if o.type in ('CAMERA', 'LIGHT'): o.parent = pivot
    pivot.rotation_euler.z = math.radians(-25)
    bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.filepath = os.path.join(ROOT, 'sprites', f'Portrait_{name}.png')
    bpy.ops.render.render(write_still=True)
    print('PORTRAIT', name)
