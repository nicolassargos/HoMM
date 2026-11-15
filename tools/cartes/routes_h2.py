"""Les routes des cartes de Heroes 2 (.mp2) : part de la terre, bouts de route, villes reliées."""
import collections, glob, math, os, statistics, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

KIND = {0xA3: 'ville', 0xB0: 'ville', 0xB1: 'ville', 0x97: 'mine', 0x9D: 'mine', 0x81: 'mine',
        0x8D: 'habitation', 0x8E: 'habitation', 0x8F: 'habitation', 0x90: 'habitation', 0x94: 'habitation',
        0xA5: 'habitation', 0xA2: 'habitation', 0xBD: 'habitation', 0xBE: 'habitation', 0xBF: 'habitation',
        0x98: 'monstre', 0xAF: 'monstre', 0xC2: 'monstre', 0xC3: 'monstre', 0xC4: 'monstre', 0xC5: 'monstre',
        0x9B: 'tas', 0xAE: 'tas', 0x86: 'coffre', 0xA9: 'artefact', 0xAD: 'artefact', 0xB7: 'héros',
        0xAB: 'bateau', 0x80 | 0x26: 'chantier'}


from road_h2 import read as read_roads


def read(path):
    r = read_roads(path)
    return r if r else (0, 0, [], set(), set())


folder = sys.argv[1]
ends = collections.Counter()
shares, on_road, total, spacing, per_land, sizes = [], 0, 0, [], [], collections.Counter()
by_size = collections.defaultdict(list)
for f in sorted(p for p in glob.glob(folder + '/**/*', recursive=True) if p.lower().endswith('.mp2')):
    w, h, tiles, road, water = read(f)
    if not tiles:
        continue
    land = w * h - len(water)
    if road:
        shares.append(100 * len(road) / land)
        by_size[w].append(100 * len(road) / land)
    objs = [(i % w, i // w, KIND.get(t[8], f'autre:{t[8]:#x}')) for i, t in enumerate(tiles) if t[8] >= 0x80]

    def r(x, y): return 0 <= x < w and 0 <= y < h and y * w + x in road
    for i in road:
        x, y = i % w, i // w
        near = [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx or dy) and r(x + dx, y + dy)]
        end = len(near) <= 1 or len(near) == 2 and max(abs(near[0][0] - near[1][0]), abs(near[0][1] - near[1][1])) == 1
        if not end:
            continue
        kinds = [k for (ox, oy, k) in objs if abs(ox - x) <= 1 and abs(oy - y) <= 1]
        if x <= 1 or y <= 1 or x >= w - 2 or y >= h - 2:
            kinds.append('bord de carte')
        if any(0 <= x + dx < w and 0 <= y + dy < h and (y + dy) * w + x + dx in water for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            kinds.append('rivage')
        order = ['ville', 'mine', 'habitation', 'chantier', 'monstre', 'héros', 'bateau', 'rivage', 'bord de carte']
        ends[next((k for k in order if k in kinds), kinds[0] if kinds else 'rien')] += 1
    towns = [(ox, oy) for (ox, oy, k) in objs if k == 'ville']
    for (x, y) in towns:
        total += 1
        on_road += any(r(x + dx, y + dy) for dx in (-2, -1, 0, 1, 2) for dy in (0, 1, 2))
    if len(towns) >= 2:
        for a in towns:
            spacing.append(min(math.dist(a, b) for b in towns if b != a) / w)
    per_land.append(1000 * len(towns) / land)


def q(v):
    v = sorted(v)
    return f'médiane {statistics.median(v):.1f}, quart bas {v[len(v) // 4]:.1f}, quart haut {v[3 * len(v) // 4]:.1f} ({len(v)} cartes)'


print('cartes avec des routes :', len(shares))
print('part de la terre en route (%) :', q(shares))
for size in sorted(by_size):
    print(f'  {size} cases de côté : {q(by_size[size])}')
print(f'villes près d\'une route : {on_road}/{total}')
print('distance à la ville la plus proche (en part du côté) :', q(spacing))
print('villes pour 1000 cases de terre :', q(per_land))
print('bouts de route :')
for k, v in ends.most_common(25):
    print(f'  {k} : {v}')
