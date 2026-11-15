"""Les sortes de routes des cartes de Heroes 3 (terre, gravier, pavés) : lesquelles une carte emploie, où chacune passe
(loin ou près des villes, sur quel terrain, en surface ou sous terre), et combien de fois une route change de sorte."""
import collections, glob, math, os, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read, find_objects, TERRAIN

KINDS = {1: 'terre', 2: 'gravier', 3: 'pavés'}
BANDS = [(0, 5, 'à 5 cases d\'une ville'), (5, 15, '5 à 15'), (15, 30, '15 à 30'), (30, 1e9, 'plus de 30 (ou pas de ville)')]

folder = sys.argv[1]
maps = 0
used = collections.Counter()           # les sortes employées par une carte (ensemble)
cells = collections.Counter()          # cases de chaque sorte
by_band = collections.defaultdict(collections.Counter)
by_terrain = collections.defaultdict(collections.Counter)
by_level = collections.defaultdict(collections.Counter)
changes, pairs = 0, 0
main_share = []                        # part de la sorte la plus employée, par carte
for f in sorted(glob.glob(folder + '/*.h3m')):
    m = read(f)
    n = m.size
    roads = {k: r for k, r in enumerate(m.road) if r}
    if not roads:
        continue
    maps += 1
    kinds = collections.Counter(roads.values())
    used[tuple(sorted(KINDS[k] for k in kinds))] += 1
    main_share.append(100 * max(kinds.values()) / len(roads))
    towns = [(x, y, z) for _, _, x, y, z, _ in find_objects(m, {98, 77})]
    for k, r in roads.items():
        z, rem = divmod(k, n * n)
        y, x = divmod(rem, n)
        cells[KINDS[r]] += 1
        by_level['surface' if z == 0 else 'sous terre'][KINDS[r]] += 1
        by_terrain[TERRAIN[m.terrain[k]]][KINDS[r]] += 1
        # la ville : son objet est noté au coin en bas à droite ; l'entrée est deux cases à gauche
        d = min((math.hypot(x - (tx - 2), y - ty) for tx, ty, tz in towns if tz == z), default=1e8)
        band = next(b for lo, hi, b in BANDS if lo <= d < hi)
        by_band[band][KINDS[r]] += 1
        for dx, dy in ((1, 0), (0, 1)):
            if x + dx < n and y + dy < n:
                other = roads.get(z * n * n + (y + dy) * n + x + dx)
                if other:
                    pairs += 1
                    changes += other != r


def line(name, counter):
    total = sum(counter.values())
    parts = ', '.join(f'{k} {100 * counter[k] / total:4.1f} %' for k in KINDS.values())
    print(f'  {name:24} {total:7} cases : {parts}')


print(f'{maps} cartes avec des routes')
print('Sortes employées par carte :')
for kinds, count in used.most_common():
    print(f'  {" + ".join(kinds):28} {count}')
print(f'Part de la sorte la plus employée sur une carte : médiane {statistics.median(main_share):.0f} %')
line('toutes', cells)
print('Selon la distance à la ville la plus proche :')
for _, _, band in BANDS:
    line(band, by_band[band])
print('Selon le niveau :')
for level, counter in by_level.items():
    line(level, counter)
print('Selon le terrain :')
for terrain, counter in sorted(by_terrain.items(), key=lambda t: -sum(t[1].values())):
    line(terrain, counter)
print(f'Changements de sorte entre deux cases de route voisines : {100 * changes / max(1, pairs):.1f} % des paires')
