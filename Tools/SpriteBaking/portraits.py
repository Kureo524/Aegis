# Head-and-shoulders portraits (HUD) of each character: front 3/4, low pitch, idle pose.
import bpy, sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common
ROOT = os.environ.get('AEGIS_BAKE', '/tmp/aegis-bake')
cfg = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'chars.json')))
FRAME = {'Ezreal': (0.77, 0.42), 'RiftHerald': (0.6, 0.6), 'MurkWolf': (0.55, 0.7), 'Krug': (0.55, 0.6), 'Gromp': (0.55, 0.65), 'Voidgrub': (0.5, 0.8)}
for name, c in cfg.items():
    if name not in ('Ezreal', 'RiftHerald'): continue
    common.reset()
    arm, meshes = common.import_glb(f'{ROOT}/lol/glb/{c["glb"]}.glb')
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and o.name.startswith('Icosphere'): bpy.data.objects.remove(o)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    idle = c['clips']['idle'][0]
    common.play(arm, idle); bpy.context.scene.frame_set(int(bpy.data.actions[idle].frame_range[0]))
    lo, hi = common.bounds(meshes)
    h = hi.z - lo.z
    center, size = FRAME.get(name, (0.65, 0.55))
    common.PITCH = 12
    cam = common.setup_scene(512, h * size, lo.z + h * center, 0)
    pivot = bpy.data.objects.new('pivot', None); bpy.context.scene.collection.objects.link(pivot)
    for o in bpy.context.scene.objects:
        if o.type in ('CAMERA', 'LIGHT'): o.parent = pivot
    pivot.rotation_euler.z = math.radians(-25)
    bpy.context.scene.eevee.taa_render_samples = 64
    bpy.context.scene.render.filepath = f'{ROOT}/sprites/Portrait_{name}.png'
    bpy.ops.render.render(write_still=True)
    print('PORTRAIT', name)
