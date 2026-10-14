# Sets each sheet's pixels-per-unit so its idle stands at the wanted height relative to the hero.
import json, os
R = os.path.join(os.environ.get('AEGIS_BAKE', '/tmp/aegis-bake'), 'sprites') + '/'
REL = {'Vesna': 1.0, 'Goblin': 0.78, 'SkeletonArcher': 0.92, 'Brute': 1.35, 'Wolf': 0.8, 'FrostSpecter': 1.0,
       'MaskedShaman': 0.82, 'StoneGolem': 1.4, 'Grokk': 2.1, 'TrainingDummy': 0.95}
HERO_UNITS = 1.7   # on-screen height of the hero's idle, in world units
def idle_h(d):
    c = next(c for c in d['clips'] if c['name'] == 'idle')
    return max(d['frames'][c['first'] + i]['rect'][3] for i in range(c['count']))
for n, rel in REL.items():
    d = json.load(open(R + n + '.json'))
    d['ppu'] = round(idle_h(d) / (HERO_UNITS * rel), 2)
    json.dump(d, open(R + n + '.json', 'w'), indent=1)
    print(n, 'idle px', idle_h(d), 'ppu', d['ppu'])
