import json, os, sys
GI = os.environ.get('GAME_ICONS', '/tmp/game-icons/icons-master')
names = set()
for root, _, files in os.walk(GI):
    for f in files:
        if f.endswith('.svg'): names.add(f[:-4])
S = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'GraphicAssets', 'Sprites')
P = {
 'red': [(190, 60, 45), (55, 12, 10), (235, 125, 95)], 'orange': [(215, 120, 40), (70, 30, 8), (255, 185, 95)],
 'yellow': [(200, 170, 50), (65, 52, 10), (250, 222, 120)], 'blue': [(70, 95, 205), (14, 16, 58), (145, 165, 255)],
 'purple': [(140, 70, 205), (34, 12, 60), (205, 155, 255)], 'crimson': [(175, 50, 72), (45, 10, 22), (240, 125, 145)],
 'green': [(60, 150, 92), (10, 40, 22), (135, 222, 152)], 'teal': [(40, 150, 162), (8, 40, 48), (112, 222, 232)],
 'brown': [(150, 105, 60), (45, 28, 12), (222, 182, 122)], 'prism': [(175, 82, 225), (42, 10, 72), (242, 172, 255)],
 'silver': [(130, 146, 168), (30, 36, 48), (222, 230, 242)], 'gold': [(205, 160, 60), (62, 40, 10), (255, 222, 122)],
 'fire': [(232, 112, 32), (72, 15, 5), (255, 192, 112)], 'grey': [(110, 110, 120), (28, 28, 34), (200, 200, 210)],
}
def pick(*cands):
    for c in cands:
        if c in names: return c
    for c in cands:
        hit = sorted(n for n in names if c in n)
        if hit: return hit[0]
    raise SystemExit('no icon for ' + str(cands))
spec = []
def T(path, pal, *cands): spec.append([f'{S}/{path}.png', pick(*cands), 'tile', P[pal]])
def B(path, color, *cands): spec.append([f'{S}/{path}.png', pick(*cands), 'bare', [color]])
# ---- hero
T('Spells/Vesna_Q', 'fire', 'flamer', 'fire-breath', 'fire-ray')
T('Spells/Vesna_W', 'fire', 'fire-bomb', 'sparky-bomb', 'unlit-bomb')
T('Spells/Vesna_E', 'fire', 'dodge', 'rolling-energy', 'sprint')
T('Spells/Vesna_R', 'fire', 'meteor-impact', 'burning-meteor', 'fire-rain')
T('Spells/Vesna_Passive', 'fire', 'burning-embers', 'ember-shot', 'fire')
T('Spells/Spell_Flash', 'teal', 'teleport', 'lightning-trio', 'sprint')
T('Augments/Spell_Ghost', 'teal', 'wind-slap', 'run', 'sprint')
T('Augments/Spell_Ignite', 'fire', 'small-fire', 'fire')
T('Augments/Loot_SummonerSpell', 'gold', 'scroll-unfurled', 'tied-scroll')
# ---- items
I = {
 'SunsteelBlade': ('red', 'winged-sword', 'broadsword'), 'TideBreaker': ('red', 'trident', 'harpoon'), 'GaleBow': ('yellow', 'high-shot', 'pocket-bow', 'bow'),
 'StormNeedle': ('yellow', 'thunder-blade', 'lightning-bolt'), 'MistStep': ('yellow', 'ghost', 'misty'), 'CrimsonChalice': ('red', 'jeweled-chalice', 'pouring-chalice'),
 'GiantsBane': ('red', 'giant', 'stone-axe', 'battle-axe'), 'QuicksilverEdge': ('yellow', 'crescent-blade', 'curvy-knife'), 'RuneSickle': ('orange', 'sickle', 'scythe'),
 'GraveFang': ('crimson', 'fangs', 'dripping-blade'), 'HexEdge': ('purple', 'relic-blade', 'cursed-star'), 'WrathBlade': ('orange', 'flaming-sword', 'fire-sword', 'sword-spin'),
 'RepeatingCrossbow': ('yellow', 'crossbow', 'arrow-cluster'), 'TrophyCrown': ('gold', 'laurel-crown', 'crown'), 'WraithKnife': ('purple', 'switchblade', 'knife'),
 'FrostbiteAxe': ('blue', 'ice-axe', 'frozen-orb', 'battle-axe'), 'ArchmageHood': ('blue', 'warlock-hood', 'wizard-face', 'hood'), 'EmberChoir': ('fire', 'fire-spell-cast', 'fireflake', 'fire'),
 'WitchfireMask': ('purple', 'totem-mask', 'tribal-mask'), 'SplitterRod': ('blue', 'crescent-staff', 'wizard-staff'), 'BoneLantern': ('blue', 'old-lantern', 'lantern'),
 'RimeScepter': ('blue', 'ice-spear', 'ice-bolt', 'frozen-orb'), 'SerpentFang': ('green', 'snake', 'snake-bite', 'fangs'), 'SageStaff': ('blue', 'wizard-staff', 'staff'),
 'GloomTorch': ('purple', 'torch', 'primitive-torch'), 'CometHeart': ('blue', 'comet-spark', 'burning-meteor'), 'CinderLantern': ('fire', 'lantern-flame', 'paper-lantern'),
 'AbyssalLens': ('purple', 'crystal-ball', 'eye-target', 'prism'), 'TriadSigil': ('gold', 'gem-chain', 'trefoil-lily'), 'IronCleaver': ('crimson', 'meat-cleaver', 'cleaver'),
 'DawnHammer': ('gold', 'thor-hammer', 'warhammer'), 'ThunderAxe': ('crimson', 'thunder-struck', 'battle-axe', 'axe'), 'HungryAxe': ('crimson', 'wood-axe', 'axe-sword', 'sharp-axe'),
 'OathGauntlet': ('crimson', 'gauntlet', 'mailed-fist'), 'WarDrum': ('crimson', 'drum', 'war-pick'), 'WardingHelm': ('teal', 'visored-helm', 'helmet'),
 'MoonblightBlade': ('purple', 'moon-claws', 'crescent-blade', 'moon'), 'AscendantPike': ('orange', 'sun-spear', 'spears'), 'EmberPlate': ('fire', 'breastplate', 'chest-armor'),
 'BrambleMail': ('green', 'thorny-vine', 'spiked-armor', 'thorn-helix'), 'SealedBulwark': ('green', 'shield-reflect', 'round-shield', 'shield'), 'TrollhideVest': ('green', 'leather-vest', 'troll'),
 'TitanHeart': ('green', 'heart-plus', 'heart'), 'GlacierPlate': ('teal', 'ice-cube', 'frozen-body', 'ice-shield'), 'BarkCloak': ('green', 'tree-branch', 'oak-leaf', 'cloak'),
 'SoulLocket': ('teal', 'heart-necklace', 'gem-pendant', 'locket'), 'ShiftingScale': ('teal', 'scale-mail', 'dorsal-scales'), 'MourningShroud': ('purple', 'hooded-figure', 'cloak'),
 'JuggernautPlate': ('grey', 'shoulder-armor', 'metal-plate', 'breastplate'), 'DuelistGreaves': ('brown', 'leg-armor', 'boots'), 'MageSlippers': ('brown', 'slippers', 'boots'),
 'IronBoots': ('brown', 'steeltoe-boots', 'boots'), 'NomadBoots': ('brown', 'walking-boot', 'leather-boot'), 'SageSandals': ('brown', 'sandal', 'boots'),
 'CourierBoots': ('brown', 'winged-boot', 'boot-prints'), 'EmberCrown': ('prism', 'crowned-explosion', 'sharp-crown'), 'ColossusBlade': ('prism', 'zeus-sword', 'broadsword'),
 'EchoPrism': ('prism', 'prism', 'crystal-cluster'), 'HourglassAmulet': ('prism', 'hourglass', 'empty-hourglass'), 'GluttonFang': ('prism', 'fangs', 'sharp-teeth'),
 'StormSpire': ('prism', 'lightning-tower', 'lightning-spanner', 'lightning-bolt'), 'BloomBlade': ('prism', 'flower-twirl', 'lotus-flower', 'rose'), 'MirrorMask': ('prism', 'domino-mask', 'mask'),
 'BindingChain': ('prism', 'chained-arrow-heads', 'wavy-chains', 'chain'), 'MagmaShield': ('prism', 'lava', 'shield'),
}
for k, (pal, *c) in I.items(): T(f'Items/Item_{k}', pal, *c)
T('Items/Elixir_Iron', 'green', 'potion-ball', 'round-bottom-flask')
T('Items/Elixir_Sorcery', 'blue', 'magic-potion', 'potion-ball')
T('Items/Elixir_Strength', 'red', 'drink-me', 'potion-ball')
T('Items/Anvil_Stats', 'silver', 'anvil')
T('Items/Anvil_Legendary', 'gold', 'anvil-impact', 'anvil')
T('Items/Anvil_Prismatic', 'prism', 'anvil-impact', 'anvil')
# ---- augments (palette by rarity)
A = {
 'CoupDeGrace': ('silver', 'guillotine', 'executioner-hood'), 'SparkVolley': ('silver', 'firework-rocket', 'sparkles'), 'Heavyweight': ('silver', 'weight-lifting-up', 'weight'),
 'WillOWisps': ('silver', 'will-o-the-wisp', 'wisp', 'fluffy-flame'), 'FrostGrip': ('silver', 'frozen-orb', 'snowflake-1'), 'UnstableCore': ('silver', 'unstable-orb', 'time-bomb'),
 'FleetStep': ('silver', 'running-ninja', 'run'), 'SurvivalInstinct': ('silver', 'heart-plus', 'life-buoy'), 'Corrosion': ('silver', 'acid', 'acid-blob'),
 'SoulCollector': ('silver', 'soul-vessel', 'skull-in-jar', 'skull'), 'SpellEdge': ('gold', 'spell-book', 'magic-swirl'), 'BurningFist': ('gold', 'fire-punch', 'punch-blast', 'fist'),
 'ArcaneMissiles': ('gold', 'missile-swarm', 'missile-pod', 'rocket'), 'DeadlyCadence': ('gold', 'metronome', 'crosshair'), 'SecondWind': ('gold', 'wind-hole', 'whirlwind'),
 'Clamor': ('gold', 'shouting', 'megaphone', 'trumpet'), 'IronWill': ('gold', 'iron-mask', 'brain-freeze', 'mighty-force'), 'HuntersThrill': ('gold', 'wolf-howl', 'hunting-horn'),
 'ReturningBlade': ('gold', 'boomerang', 'returning-blade', 'spinning-blades'), 'WellPracticed': ('gold', 'upgrade', 'progression'), 'SecondNature': ('gold', 'reticule', 'two-feathers', 'clover'),
 'ThirdEye': ('gold', 'third-eye', 'eye'), 'RunawaySorcery': ('prism', 'magic-swirl', 'whirlpool-shuriken', 'vortex'), 'LavaChannel': ('prism', 'lava', 'volcano'),
 'Orbit': ('prism', 'orbital', 'planet-core', 'circle'), 'ArcLightning': ('prism', 'chain-lightning', 'lightning-arc'), 'Ambidextrous': ('prism', 'crossed-swords', 'sword-clash'),
 'QuakeStep': ('prism', 'earth-crack', 'ground-sprout', 'quake-stomp'), 'RuneTrail': ('prism', 'rune-stone', 'rune-sword'), 'AllOrNothing': ('prism', 'glass-heart', 'broken-heart'),
 'Apotheosis': ('prism', 'ascension', 'angel-outfit', 'aura'), 'EternalMoment': ('prism', 'stopwatch', 'hourglass'), 'RunicFist': ('prism', 'magic-palm', 'fist'),
 'Simmer': ('prism', 'cauldron', 'boiling-bubbles'), 'GoldsmithGlove': ('prism', 'gem-pendant', 'ring'), 'Fury': ('gold', 'enrage', 'angry-eyes'),
 'Gambler': ('silver', 'rolling-dices', 'perspective-dice-six'), 'HotBlood': ('silver', 'bleeding-heart', 'blood'), 'SealCaller': ('prism', 'scroll-unfurled', 'magic-portal'),
 'StaticShock': ('gold', 'electric', 'lightning-bolt'), 'ArcaneBarrier': ('gold', 'magic-shield', 'shield'), 'Phoenix': ('prism', 'fire-bird', 'eagle-emblem', 'bird-twitter', 'fire'),
}
for k, (pal, *c) in A.items(): T(f'Augments/Augment_{k}', pal, *c)
# ---- stats (glyph only)
ST = {
 'PhysicalAttack': ((255, 150, 110), 'broadsword', 'sword'), 'MagicAttack': ((150, 170, 255), 'magic-swirl', 'spell-book'), 'Armor': ((255, 200, 120), 'shield', 'round-shield'),
 'MagicResist': ((170, 140, 255), 'magic-shield', 'shield'), 'AttackSpeed': ((255, 225, 120), 'sword-spin', 'speedometer'), 'AbilityHaste': ((150, 230, 255), 'hourglass', 'stopwatch'),
 'CritChance': ((255, 160, 80), 'crosshair', 'bullseye'), 'MoveSpeed': ((230, 230, 230), 'walking-boot', 'boots'), 'Lifesteal': ((255, 90, 90), 'drop', 'blood'),
 'ArmorPenPercent': ((255, 175, 140), 'broken-shield', 'shield-disabled'), 'MagicPenPercent': ((200, 160, 255), 'shattered-glass', 'crystal-shine'), 'Tenacity': ((210, 210, 160), 'anchor', 'strong'),
 'MaxHealth': ((120, 230, 140), 'heart-plus', 'heart'), 'MaxMana': ((120, 170, 255), 'crystal-ball', 'water-drop'),
}
for k, (col, *c) in ST.items(): B(f'UI/Stats/Stat_{k}', col, *c)
# ---- statuses
SS = {'Stun': ((255, 230, 100), 'knocked-out-stars', 'star-swirl'), 'Root': ((140, 230, 120), 'root-tip', 'vine-whip'), 'Silence': ((200, 150, 255), 'silenced', 'mute'),
      'Slow': ((140, 200, 255), 'snail', 'turtle'), 'Burn': ((255, 140, 60), 'flame', 'fire'), 'Bleed': ((255, 90, 90), 'bleeding-wound', 'drop'), 'Shred': ((220, 200, 170), 'cracked-shield', 'broken-shield')}
for k, (col, *c) in SS.items(): B(f'Status/Status_{k}', col, *c)
json.dump(spec, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'spec.json'), 'w'), indent=0)
print(len(spec), 'icons')
for out, n, *_ in spec: print(os.path.basename(out), '<-', n)
