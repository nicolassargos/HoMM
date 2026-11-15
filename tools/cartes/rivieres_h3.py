"""Les rivières des cartes de Heroes 3 : part de la terre, nombre, longueur, où elles finissent, ponts (routes qui les croisent)."""
import collections, glob, os, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read, find_objects

WATER = 8
SORTS = {1: 'claire', 2: 'glacée', 3: 'boueuse', 4: 'lave'}


def q(v):
    if not v:
        return '-'
    v = sorted(v)
    return f'min {v[0]:.1f}  médiane {statistics.median(v):.1f}  max {v[-1]:.1f}  (n={len(v)})'


folder = sys.argv[1]
with_rivers, without = 0, 0
shares, counts, lengths, per_1000, crossings, crossing_len, sorts = [], [], [], [], [], [], collections.Counter()
ends = collections.Counter()
town_dist = []
for f in sorted(glob.glob(folder + '/*.h3m')):
    m = read(f)
    n = m.size
    land = sum(1 for t in m.terrain if t not in (8, 9))
    rivers = {k for k, r in enumerate(m.river) if r}
    if not rivers:
        without += 1
        continue
    with_rivers += 1
    shares.append(100 * len(rivers) / land)
    for k in rivers:
        sorts[SORTS[m.river[k]]] += 1

    def xyz(k):
        z, rem = divmod(k, n * n); y, x = divmod(rem, n); return x, y, z

    def idx(x, y, z): return z * n * n + y * n + x

    # rivières : composantes reliées par les 4 côtés (une rivière de Heroes 3 ne va pas en diagonale)
    seen, comps = set(), []
    for k in rivers:
        if k in seen:
            continue
        stack, comp = [k], []
        seen.add(k)
        while stack:
            c = stack.pop(); comp.append(c)
            x, y, z = xyz(c)
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                a, b = x + dx, y + dy
                if 0 <= a < n and 0 <= b < n and idx(a, b, z) in rivers and idx(a, b, z) not in seen:
                    seen.add(idx(a, b, z)); stack.append(idx(a, b, z))
        comps.append(comp)
    big = [c for c in comps if len(c) >= 3]
    counts.append(len(big))
    per_1000.append(1000 * len(big) / land)
    for c in big:
        lengths.append(len(c))
        cs = set(c)
        for k in c:
            x, y, z = xyz(k)
            nb = [(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            inside = [p for p in nb if 0 <= p[0] < n and 0 <= p[1] < n and idx(p[0], p[1], z) in cs]
            if len(inside) != 1:
                continue  # pas un bout
            if any(not (0 <= a < n and 0 <= b < n) for a, b in nb):
                ends['bord de la carte'] += 1
            elif any(m.terrain[idx(a, b, z)] == WATER for a, b in nb if 0 <= a < n and 0 <= b < n):
                ends['eau (mer, lac)'] += 1
            else:
                ends['terre (source, lac décor…)'] += 1
    # ponts : une case de route sur une case de rivière ; on compte les traversées (groupes de cases)
    cross = [k for k in rivers if m.road[k]]
    crossings.append(len(cross))
    if big:
        crossing_len.append(len(cross) / (sum(len(c) for c in big) / 100))
    # distance des villes à la rivière la plus proche (cases, Tchebychev)
    towns = find_objects(m, {98, 77})
    rpos = [xyz(k) for k in rivers]
    for _, _, x, y, z, _ in towns:
        x -= 2  # la porte : l'objet est donné par son coin bas droit
        d = min((max(abs(a - x), abs(b - y)) for a, b, c in rpos if c == z), default=None)
        if d is not None:
            town_dist.append(d)

print(f'cartes avec rivières : {with_rivers}, sans : {without}')
print('part de la terre (%) :', q(shares))
print('rivières (3 cases ou plus) par carte :', q(counts))
print('rivières pour 1000 cases de terre :', q(per_1000))
print('longueur d\'une rivière (cases) :', q(lengths))
print('bouts :', dict(ends))
print('cases route sur rivière par carte :', q(crossings))
print('cases route sur rivière pour 100 cases de rivière :', q(crossing_len))
print('distance ville → rivière (cases) :', q(town_dist))
print('sortes :', dict(sorts))
