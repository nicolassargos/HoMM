"""Les terrains des cartes de Heroes 2 et 3 (2026-11-17) : la part de la terre de chaque terrain, et comment il se groupe
(des taches : cases du même terrain qui se touchent, 4 voisins) : combien de taches, leur taille, la part de la plus
grande ; combien de terrains une carte emploie.

    python -I terrains.py dossier_h3 [dossier_h2]
"""
import collections, glob, os, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read as read_h3, TERRAIN as T3
import road_h2

# Heroes 2 : le terrain d'une case vient de l'index de son image de fond (comme fheroes2)
H2 = [(0, 'eau'), (30, 'herbe'), (92, 'neige'), (146, 'marais'), (208, 'lave'), (262, 'sable'), (321, 'terre'),
      (361, 'rocaille'), (416, 'plage'), (432, None)]


def h2_terrain(index):
    for (lo, name), (hi, _) in zip(H2, H2[1:]):
        if lo <= index < hi:
            return name
    return None


def patches(grid, w, h):
    """Les taches de chaque terrain : {terrain: [tailles]} (4 voisins), sans l'eau."""
    seen, out = set(), collections.defaultdict(list)
    for i in range(w * h):
        t = grid[i]
        if i in seen or t in (None, 'eau', 'roche'):
            continue
        seen.add(i); stack = [i]; n = 0
        while stack:
            j = stack.pop(); n += 1
            x, y = j % w, j // w
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                k = ny * w + nx
                if 0 <= nx < w and 0 <= ny < h and k not in seen and grid[k] == t:
                    seen.add(k); stack.append(k)
        out[t].append(n)
    return out


def q(v):
    if not v:
        return '-'
    v = sorted(v)
    return f'médiane {statistics.median(v):g} (de {v[0]:g} à {v[-1]:g}, n={len(v)})'


def measure(maps, nom):
    share = collections.defaultdict(list)       # terrain → part de la terre (%) sur les cartes qui l'ont
    present = collections.Counter()             # terrain → nombre de cartes
    count = collections.defaultdict(list)       # terrain → nombre de taches (de 4 cases et plus) par carte
    size = collections.defaultdict(list)        # terrain → taille des taches de 4 cases et plus
    biggest = collections.defaultdict(list)     # terrain → part de la plus grande tache dans ce terrain (%)
    small = collections.defaultdict(list)       # terrain → part du terrain en taches de moins de 10 cases (%)
    used = []
    for grid, w, h in maps:
        land = [t for t in grid if t not in (None, 'eau', 'roche')]
        if not land:
            continue
        c = collections.Counter(land)
        used.append(sum(1 for t, n in c.items() if n * 100 >= len(land)))
        p = patches(grid, w, h)
        for t, n in c.items():
            if n * 100 < len(land):
                continue  # moins de 1 % : des miettes
            present[t] += 1
            share[t].append(round(100 * n / len(land), 1))
            big = [s for s in p[t] if s >= 4]
            count[t].append(len(big)); size[t].extend(big)
            biggest[t].append(round(100 * max(p[t]) / n))
            small[t].append(round(100 * sum(s for s in p[t] if s < 10) / n))
    print(f'== {nom} : {len(maps)} cartes ; terrains employés (1 % de la terre au moins) par carte : {q(used)}')
    for t, _ in present.most_common():
        print(f'{t:9} sur {present[t]:3} cartes | part de la terre (%) {q(share[t])}')
        print(f'{"":9} taches (4 cases et plus) par carte {q(count[t])} | taille {q(size[t])}')
        print(f'{"":9} la plus grande : {q(biggest[t])} % du terrain | en taches de moins de 10 cases : {q(small[t])} %')


maps3 = []
for f in sorted(glob.glob(sys.argv[1] + '/*.h3m')):
    m = read_h3(f)
    n = m.size
    maps3.append(([T3[t] if t < len(T3) else None for t in m.terrain[:n * n]], n, n))
measure(maps3, 'Heroes 3 (surface)')
if len(sys.argv) > 2:
    maps2 = []
    for f in sorted({os.path.normcase(f) for f in glob.glob(sys.argv[2] + '/*.mp2') + glob.glob(sys.argv[2] + '/*.MP2')}):
        r = road_h2.read(f)
        if r is None:
            continue
        w, h, T, _, _ = r
        maps2.append(([h2_terrain(t[0]) for t in T], w, h))
    measure(maps2, 'Heroes 2')
