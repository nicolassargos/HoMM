"""Les routes des cartes de Heroes 3 : part de la terre, bouts de route, villes reliées, boucles, rivières."""
import collections, glob, math, os, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read, find_objects

KIND = {98: 'ville', 77: 'ville', 53: 'mine', 220: 'mine', 54: 'monstre', 71: 'monstre', 72: 'monstre', 73: 'monstre',
        74: 'monstre', 75: 'monstre', 162: 'monstre', 163: 'monstre', 164: 'monstre', 17: 'habitation', 20: 'habitation',
        216: 'habitation', 217: 'habitation', 218: 'habitation', 87: 'chantier', 43: 'portail', 44: 'portail', 45: 'portail',
        103: 'porte souterraine', 33: 'garnison', 219: 'garnison', 34: 'héros', 70: 'héros', 62: 'héros',
        79: 'tas', 76: 'tas', 101: 'coffre', 5: 'artefact', 65: 'artefact', 66: 'artefact', 67: 'artefact', 68: 'artefact',
        69: 'artefact', 212: 'portail de bordure', 9: 'tente de bordure', 10: 'garde de bordure', 8: 'bateau', 92: 'sirène'}
SKIP = set(range(116, 212)) - {162, 163, 164}  # décors (arbres, montagnes, lacs…) : pas des destinations

folder = sys.argv[1]
ends = collections.Counter()
shares, towns_on_road, towns_total, loops, cobble, gravel, dirt = [], 0, 0, [], 0, 0, 0
river_roads, spacing, per_land, junctions, diag_share = [], [], [], [], []
for f in sorted(glob.glob(folder + '/*.h3m')):
    m = read(f)
    n = m.size
    land = sum(1 for t in m.terrain if t not in (8, 9))
    roadset = {k for k, r in enumerate(m.road) if r}
    if not roadset:
        continue
    shares.append(100 * len(roadset) / land)
    for k in roadset:
        cobble += m.road[k] == 3; gravel += m.road[k] == 2; dirt += m.road[k] == 1
    river_roads.append(sum(1 for k in roadset if m.river[k]))
    objs = [o for o in find_objects(m, set(range(0, 256)) - SKIP)]
    by_level = collections.defaultdict(list)
    for o in objs:
        by_level[o[4]].append(o)

    def idx(x, y, z): return z * n * n + y * n + x

    def road(x, y, z): return 0 <= x < n and 0 <= y < n and idx(x, y, z) in roadset
    edges = 0
    diag = 0
    for k in roadset:
        z, rem = divmod(k, n * n); y, x = divmod(rem, n)
        near = [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx or dy) and road(x + dx, y + dy, z)]
        ortho = [p for p in near if p[0] == x or p[1] == y]
        if len(near) >= 3 and not any(abs(a[0] - b[0]) + abs(a[1] - b[1]) == 1 for a in near for b in near if a < b) or len(ortho) >= 3:
            pass
        end = len(near) <= 1 or len(near) == 2 and max(abs(near[0][0] - near[1][0]), abs(near[0][1] - near[1][1])) == 1
        if end:
            kinds = []
            for (cls, sub, ox, oy, oz, name) in by_level[z]:
                # x, y : le coin en bas à droite de l'objet ; la case d'action est à gauche, sur sa rangée du bas
                if ox - 4 <= x <= ox + 1 and oy - 2 <= y <= oy + 1:
                    kinds.append(KIND.get(cls, f'autre:{cls}'))
            if x <= 1 or y <= 1 or x >= n - 2 or y >= n - 2:
                kinds.append('bord de carte')
            if any(0 <= x + dx < n and 0 <= y + dy < n and m.terrain[idx(x + dx, y + dy, z)] == 8 for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                kinds.append('rivage')
            order = ['ville', 'mine', 'habitation', 'chantier', 'portail', 'porte souterraine', 'garnison', 'portail de bordure',
                     'garde de bordure', 'tente de bordure', 'monstre', 'héros', 'bateau', 'rivage', 'bord de carte']
            best = next((k for k in order if k in kinds), kinds[0] if kinds else 'rien')
            ends[best] += 1
    # villes reliées : une route à 2 cases au plus de l'entrée (x-2, y)
    towns = [o for o in objs if o[0] in (98, 77)]
    for (cls, sub, ox, oy, oz, name) in towns:
        towns_total += 1
        ex, ey = ox - 2, oy
        if any(road(ex + dx, ey + dy, oz) for dx in range(-2, 3) for dy in range(-1, 3)):
            towns_on_road += 1
    surface = [o for o in towns if o[4] == 0]
    if len(surface) >= 2:
        for a in surface:
            d = min(math.dist(a[2:4], b[2:4]) for b in surface if b is not a)
            spacing.append(d / n)
    per_land.append(1000 * len(towns) / land)

def q(v):
    v = sorted(v)
    return f'médiane {statistics.median(v):.1f}, quart bas {v[len(v) // 4]:.1f}, quart haut {v[3 * len(v) // 4]:.1f}'

print('cartes avec des routes :', len(shares))
print('part de la terre en route (%) :', q(shares))
print(f'villes près d\'une route : {towns_on_road}/{towns_total}')
print(f'sortes de route : terre {dirt}, gravier {gravel}, pavés {cobble}')
print('cases route + rivière par carte :', q(river_roads))
print('distance à la ville la plus proche (en part du côté) :', q(spacing))
print('villes pour 1000 cases de terre :', q(per_land))
print('bouts de route :')
for k, v in ends.most_common():
    print(f'  {k} : {v}')
