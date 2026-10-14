import bpy, math, mathutils

PITCH = math.degrees(math.atan2(10, 8))  # game camera elevation (~51.3°)

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def import_glb(path):
    bpy.ops.import_scene.gltf(filepath=path)
    arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    return arm, meshes

def bounds(meshes):
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in meshes:
        e = o.evaluated_get(dg)
        m = e.to_mesh()
        pts += [o.matrix_world @ v.co for v in m.vertices]
        e.to_mesh_clear()
    lo = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi

def setup_scene(res, ortho_scale, target_z, yaw_deg):
    scn = bpy.context.scene
    scn.render.engine = 'BLENDER_EEVEE'
    scn.render.resolution_x = scn.render.resolution_y = res
    scn.render.film_transparent = True
    scn.render.image_settings.file_format = 'PNG'
    scn.render.image_settings.color_mode = 'RGBA'
    scn.view_settings.view_transform = 'Standard'
    # Camera: orthographic, looking down at the game pitch; the model is turned by yaw instead
    cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new('cam', cam_data); scn.collection.objects.link(cam); scn.camera = cam
    p = math.radians(PITCH); d = 30
    cam.location = (0, -d * math.cos(p), target_z + d * math.sin(p))
    cam.rotation_euler = (math.radians(90) - p, 0, 0)
    # Lights: warm key from the upper left-front, cool rim from behind
    def sun(name, energy, rot, color):
        l = bpy.data.lights.new(name, 'SUN'); l.energy = energy; l.color = color; l.angle = math.radians(8)
        o = bpy.data.objects.new(name, l); o.rotation_euler = [math.radians(a) for a in rot]; scn.collection.objects.link(o)
    sun('key', 3.2, (40, 0, -35), (1.0, 0.95, 0.88))
    sun('fill', 1.0, (60, 0, 140), (0.75, 0.82, 1.0))
    sun('rim', 2.5, (-60, 0, 180), (0.85, 0.9, 1.0))
    world = bpy.data.worlds.new('w'); scn.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.35, 0.37, 0.42, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = 1.35
    return cam

def root_object(arm, meshes):
    roots = {o for o in ([arm] if arm else []) + meshes}
    tops = set()
    for o in roots:
        while o.parent: o = o.parent
        tops.add(o)
    return list(tops)

def set_yaw(objs, yaw_deg):
    for o in objs:
        o.rotation_mode = 'XYZ'
        o.rotation_euler.z += math.radians(yaw_deg)

def play(arm, name):
    act = next((a for a in bpy.data.actions if a.name.split('|')[-1] == name or a.name == name), None)
    if arm is None or act is None: return None
    arm.animation_data_create()
    arm.animation_data.action = act
    if hasattr(arm.animation_data, 'action_slot') and act.slots:
        arm.animation_data.action_slot = act.slots[0]
    return act
