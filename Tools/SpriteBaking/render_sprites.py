# Renders every clip of every character of chars.json into frames, from the game camera pitch,
# 3/4 view facing screen right. Output: sprites/<Char>/<clip>_<i>.png + meta.json.
# Models: CC0 glTF (Quaternius) or "@dummy" (built here). Optional: recolor {material: rgb},
# "stone" (every material turned to rock), "ember": a glowing ember in the right hand,
# "blade": {"color": rgb, "length": share of the height} a glowing blade held in the right hand.
import bpy, sys, os, json, math, mathutils
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common
ROOT = os.environ.get('AEGIS_BAKE', '/tmp/aegis-bake')
cfg = json.load(open(os.path.join(HERE, 'chars.json')))
only = os.environ.get('ONLY', '')
PPU, SS, AZIMUTH = 100, 2, -55

def find_action(name):
    return next((a for a in bpy.data.actions if a.name == name or a.name.split('|')[-1] == name), None)

def emissive(name, color, strength):
    m = bpy.data.materials.new(name); m.use_nodes = True
    n = m.node_tree.nodes; n.clear()
    out = n.new('ShaderNodeOutputMaterial'); em = n.new('ShaderNodeEmission')
    em.inputs[0].default_value = (*color, 1); em.inputs[1].default_value = strength
    m.node_tree.links.new(em.outputs[0], out.inputs[0])
    return m

def flat(name, color, rough=0.8):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF'); b.inputs[0].default_value = (*color, 1); b.inputs['Roughness'].default_value = rough
    return m

def build_dummy():
    """A straw training dummy on a post, with object-level Idle / Hit / Death actions."""
    root = bpy.data.objects.new('Dummy', None); bpy.context.scene.collection.objects.link(root)
    wood, straw, cloth, red, white = flat('Wood', (0.22, 0.12, 0.06)), flat('Straw', (0.62, 0.48, 0.2)), flat('Burlap', (0.36, 0.26, 0.14)), flat('Red', (0.6, 0.06, 0.04)), flat('White', (0.85, 0.82, 0.75))
    def add(op, mat, loc, scale, rot=(0, 0, 0), **kw):
        op(location=loc, rotation=rot, **kw); o = bpy.context.active_object
        o.scale = scale; o.data.materials.append(mat); o.parent = root
        bpy.ops.object.shade_smooth()
        return o
    add(bpy.ops.mesh.primitive_cylinder_add, wood, (0, 0, 0.55), (0.06, 0.06, 0.55), vertices=12)
    add(bpy.ops.mesh.primitive_cylinder_add, wood, (0, 0, 0.02), (0.32, 0.32, 0.03), vertices=16)
    add(bpy.ops.mesh.primitive_cylinder_add, cloth, (0, 0, 0.92), (0.24, 0.2, 0.32), vertices=16)
    add(bpy.ops.mesh.primitive_cylinder_add, wood, (0, 0, 1.08), (0.04, 0.04, 0.42), (0, math.radians(90), 0), vertices=10)
    for side in (-1, 1):
        add(bpy.ops.mesh.primitive_uv_sphere_add, straw, (side * 0.48, 0, 1.08), (0.09, 0.09, 0.09), segments=12, ring_count=8)
    add(bpy.ops.mesh.primitive_uv_sphere_add, cloth, (0, 0, 1.4), (0.17, 0.17, 0.19), segments=16, ring_count=10)
    add(bpy.ops.mesh.primitive_cylinder_add, straw, (0, 0, 1.58), (0.13, 0.13, 0.05), vertices=12)
    for i, (r, mat) in enumerate([(0.15, white), (0.11, red), (0.07, white), (0.035, red)]):
        add(bpy.ops.mesh.primitive_cylinder_add, mat, (0, -0.2 - i * 0.004, 0.95), (r, r, 0.01), (math.radians(90), 0, 0), vertices=24)
    root.animation_data_create()
    def action(name, keys):
        a = bpy.data.actions.new(name); root.animation_data.action = a
        for f, (rx, rz, sz) in keys:
            root.rotation_euler = (rx, 0, rz); root.location = (0, 0, sz)
            root.keyframe_insert('rotation_euler', frame=f); root.keyframe_insert('location', frame=f)
        return a
    d = math.radians
    action('Idle', [(1, (0, 0, 0)), (24, (d(1.5), d(2), 0)), (48, (0, 0, 0))])
    action('Hit', [(1, (0, 0, 0)), (4, (d(-16), 0, 0)), (8, (d(9), 0, 0)), (12, (d(-4), 0, 0)), (16, (0, 0, 0))])
    action('Death', [(1, (0, 0, 0)), (6, (d(-12), 0, 0)), (20, (d(84), 0, -0.05)), (24, (d(80), 0, -0.05))])
    return root, [o for o in bpy.data.objects if o.type == 'MESH']

def build_blade(color, length, width):
    """A tapered double-edged blade along +Y (hilt at the origin) with a crossguard, unit-free."""
    import bmesh
    me = bpy.data.meshes.new('Blade'); bm = bmesh.new()
    base, tip = 0.12 * length, length
    ring = lambda y, w, t: [bm.verts.new((x, y, z)) for x, z in ((w, 0), (0, t), (-w, 0), (0, -t))]
    a = ring(base, width, width * 0.28); b = ring(tip * 0.86, width * 0.8, width * 0.22); c = bm.verts.new((0, tip, 0))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((a[i], a[j], b[j], b[i])); bm.faces.new((b[i], b[j], c))
    bm.faces.new(a[::-1])
    bm.to_mesh(me); bm.free()
    blade = bpy.data.objects.new('Blade', me); bpy.context.scene.collection.objects.link(blade)
    blade.data.materials.append(emissive('BladeMat', color, 1.0))
    bpy.ops.mesh.primitive_cube_add(size=1); guard = bpy.context.active_object; guard.name = 'Guard'
    guard.data.materials.append(flat('GuardMat', (0.18, 0.14, 0.42), 0.4))
    return blade, guard

def play(arm, obj, name):
    act = find_action(name)
    target = arm if arm is not None else obj
    if act is None or target is None: return None
    target.animation_data_create(); target.animation_data.action = act
    if hasattr(target.animation_data, 'action_slot') and getattr(act, 'slots', None):
        target.animation_data.action_slot = act.slots[0]
    return act

for name, c in cfg.items():
    if only and name not in only.split(','): continue
    common.reset()
    obj = None
    if c['src'] == '@dummy':
        obj, meshes = build_dummy(); arm = None
    else:
        arm, meshes = common.import_glb(os.path.join(ROOT, c['src']))
        common.drop_helpers(); meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if c.get('recolor'): common.recolor({k: tuple(v) for k, v in c['recolor'].items()})
    if c.get('stone'):
        for i, m in enumerate(bpy.data.materials):
            if 'eye' in m.name.lower() or not m.node_tree: continue
            b = m.node_tree.nodes.get('Principled BSDF')
            if b:
                g = 0.13 + 0.05 * (i % 3)
                b.inputs[0].default_value = (g * 1.05, g, g * 0.92, 1)
                b.inputs['Roughness'].default_value = 0.95
    ember, ember_bone = None, None
    if c.get('ember') and arm is not None:
        ember_bone = next((b.name for b in arm.data.bones if any(k in b.name.lower() for k in ('fist', 'hand', 'wrist')) and b.name.lower().rstrip('.0123456789').endswith(('r', 'right'))), None)
        if ember_bone:
            bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=16, ring_count=10)
            ember = bpy.context.active_object
            ember.name = 'Ember'
            ember.data.materials.append(emissive('EmberMat', (1.0, 0.32, 0.04), 2.6))
            print('EMBER on', ember_bone)
    blade = guard = None
    if c.get('blade') and arm is not None:
        hand = next((b.name for b in arm.data.bones if any(k in b.name.lower() for k in ('fist', 'hand', 'wrist')) and b.name.lower().rstrip('.0123456789').endswith(('r', 'right'))), None)
        if hand:
            blade, guard = build_blade(tuple(c['blade'].get('color', (0.55, 0.9, 1.0))), 1.0, 1.0)
            print('BLADE on', hand)
    def place_blade():
        # along the wrist bone (+Y), from its tail; fixed world size
        if blade is None: return
        pb = arm.pose.bones[hand]
        _, rot, _ = (arm.matrix_world @ pb.matrix).decompose()
        tail = arm.matrix_world @ pb.tail
        at = mathutils.Matrix.Translation(tail) @ rot.to_matrix().to_4x4()
        L = h * c['blade'].get('length', 0.5)
        blade.matrix_world = at @ mathutils.Matrix.Diagonal((h * 0.028, L, h * 0.028, 1))
        guard.matrix_world = at @ mathutils.Matrix.Translation((0, L * 0.12, 0)) @ mathutils.Matrix.Diagonal((h * 0.11, h * 0.02, h * 0.03, 1))
    def place_ember():
        # world position of the wrist, a bit past it (the palm); fixed world size
        if ember is None: return
        pb = arm.pose.bones[ember_bone]
        head, tail = arm.matrix_world @ pb.head, arm.matrix_world @ pb.tail
        ember.location = tail + (tail - head) * 0.6
        ember.scale = (ember_size,) * 3
    idle = c['clips']['idle']
    act = play(arm, obj, idle[0]); bpy.context.scene.frame_set(int(act.frame_range[0]))
    lo, hi = common.bounds([m for m in meshes if m.name not in ('Ember', 'Blade', 'Guard')])
    h = hi.z - lo.z
    ember_size = h * 0.045
    scale = c['height'] / h
    extent = max(h, hi.x - lo.x, hi.y - lo.y) * 2.3
    target_z = h * 0.45
    res = int(extent * scale * PPU * SS)
    common.setup_scene(res, extent, target_z, 0)
    pivot = bpy.data.objects.new('pivot', None); bpy.context.scene.collection.objects.link(pivot)
    for o in bpy.context.scene.objects:
        if o.type in ('CAMERA', 'LIGHT'): o.parent = pivot
    pivot.rotation_euler.z = math.radians(AZIMUTH)
    scn = bpy.context.scene
    scn.render.film_transparent = True
    scn.eevee.taa_render_samples = 32
    out = os.path.join(ROOT, 'sprites', name); os.makedirs(out, exist_ok=True)
    meta = {'res': res, 'ss': SS, 'ppu': PPU, 'extent': extent, 'targetZ': target_z, 'scale': scale, 'pitch': common.PITCH, 'clips': {}}
    for clip, spec in c['clips'].items():
        act_name, n = spec[0], spec[1]
        portion = spec[2] if len(spec) > 2 else 1.0
        act = play(arm, obj, act_name)
        if act is None: print('MISSING', name, act_name); continue
        f0, f1 = act.frame_range
        f1 = f0 + (f1 - f0) * portion
        loop = clip in ('idle', 'run', 'dash')
        frames = []
        for i in range(n):
            t = f0 + (f1 - f0) * (i / n if loop else i / max(1, n - 1))
            scn.frame_set(int(t), subframe=t - int(t))
            place_ember()
            place_blade()
            scn.render.filepath = f'{out}/{clip}_{i:02d}.png'
            bpy.ops.render.render(write_still=True)
            frames.append(os.path.basename(scn.render.filepath))
        fps = scn.render.fps / scn.render.fps_base
        meta['clips'][clip] = {'frames': frames, 'duration': (f1 - f0) / fps, 'loop': loop}
        print('CLIP', name, clip, act_name, n, 'dur', round((f1 - f0) / fps, 2))
    json.dump(meta, open(f'{out}/meta.json', 'w'), indent=1)
    print('DONE', name, 'res', res, 'h', round(h, 2))
