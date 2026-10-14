# Sets each sheet's pixels-per-unit so its idle stands at the wanted height relative to Ezreal.
import json, os
R = os.path.join(os.environ.get('AEGIS_BAKE', '/tmp/aegis-bake'), 'sprites') + '/'
REL = {'Ezreal': 1.0, 'MeleeMinion': 0.78, 'CasterMinion': 0.78, 'SuperMinion': 1.3, 'MurkWolf': 0.85,
       'Gromp': 1.05, 'Voidgrub': 0.62, 'Krug': 1.35, 'RiftHerald': 2.1, 'TargetDummy': 0.95}
EZ_UNITS = 1.7   # on-screen height of Ezreal's idle, in world units
def idle_h(d):
    c = next(c for c in d['clips'] if c['name'] == 'idle')
    return max(d['frames'][c['first'] + i]['rect'][3] for i in range(c['count']))
for n, rel in REL.items():
    d = json.load(open(R + n + '.json'))
    d['ppu'] = round(idle_h(d) / (EZ_UNITS * rel), 2)
    json.dump(d, open(R + n + '.json', 'w'), indent=1)
    print(n, 'idle px', idle_h(d), 'ppu', d['ppu'])
