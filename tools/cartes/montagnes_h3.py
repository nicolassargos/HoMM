"""Les montagnes des cartes de Heroes 3 (2026-11-17) : leurs modèles (cases bloquées), combien une carte en pose, sur
quel terrain, comment elles se groupent en chaînes, et ce qui les entoure (autres obstacles).

    python -I montagnes_h3.py dossier
"""
import collections, glob, os, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read, find_objects, TERRAIN

MOUNTAIN = 134
# les obstacles du décor (arbres, lacs, rochers, cratères…), pour comparer
OBSTACLES = set(range(116, 162)) | {177, 199, 206, 207, 208, 209, 210}


def q(v):
    if not v:
        return '-'
    v = sorted(v)
    return f'min {v[0]}  médiane {statistics.median(v)}  max {v[-1]}  (n={len(v)})'


def shape(cells):
    xs = [c[0] for c in cells]; ys = [c[1] for c in cells]
    return max(xs) - min(xs) + 1, max(ys) - min(ys) + 1


def drawing(cells):
    """Le masque en lignes de texte : # bloqué, . libre (dans le rectangle des cases bloquées)."""
    x0 = min(c[0] for c in cells); y0 = min(c[1] for c in cells)
    w, h = shape(cells)
    return ['  ' + ''.join('#' if (x0 + i, y0 + j) in cells else '.' for i in range(w)) for j in range(h)]


folder = sys.argv[1]
models = {}                       # nom → cases bloquées
uses = collections.Counter()      # nom → nombre posé
under = collections.Counter()     # terrain sous les cases bloquées par une montagne
per_map, share_land, share_obst, chains, per_chain, alone, near = [], [], [], [], [], [], collections.Counter()
for f in sorted(glob.glob(folder + '/*.h3m')):
    m = read(f)
    n = m.size
    land = sum(1 for k, t in enumerate(m.terrain[:n * n]) if t not in (8, 9))
    blocked_by = {}               # case → classe de l'obstacle (surface seulement)
    mountains = []                # (nom, cases)
    for idx, (name, cls, sub) in enumerate(m.templates):
        if cls == MOUNTAIN:
            models.setdefault(name.lower()[:-4], m.masks[idx][0])
    for cls, sub, x, y, z, name in find_objects(m, OBSTACLES | {MOUNTAIN}):
        if z:
            continue
        idx = next(i for i, t in enumerate(m.templates) if t[0] == name and t[1] == cls)
        cells = {(x - 7 + c, y - 5 + r) for c, r in m.masks[idx][0]}
        cells = {c for c in cells if 0 <= c[0] < n and 0 <= c[1] < n}
        for c in cells:
            blocked_by.setdefault(c, cls)
        if cls == MOUNTAIN:
            mountains.append((name.lower()[:-4], cells))
            uses[name.lower()[:-4]] += 1
            for cx, cy in cells:
                under[TERRAIN[m.terrain[cy * n + cx]]] += 1
    if not mountains:
        continue
    rock = set().union(*(c for _, c in mountains))
    per_map.append(round(1000 * len(mountains) / max(1, land), 1))
    share_land.append(round(100 * len(rock) / max(1, land), 1))
    share_obst.append(round(100 * len(rock) / max(1, len(blocked_by)), 1))
    # les chaînes : montagnes dont les cases bloquées se touchent (8 voisins)
    owner = {}
    for i, (_, cells) in enumerate(mountains):
        for c in cells:
            owner.setdefault(c, i)
    parent = list(range(len(mountains)))

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]; i = parent[i]
        return i
    for i, (_, cells) in enumerate(mountains):
        for cx, cy in cells:
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    j = owner.get((cx + dx, cy + dy))
                    if j is not None:
                        parent[root(i)] = root(j)
    groups = collections.Counter(root(i) for i in range(len(mountains)))
    chains.extend(groups.values())
    alone.append(round(100 * sum(1 for v in groups.values() if v == 1) / len(mountains), 1))
    # ce qui touche une montagne (cases voisines bloquées par un autre obstacle)
    for cx, cy in rock:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                c = (cx + dx, cy + dy)
                if c not in rock and c in blocked_by:
                    near[blocked_by[c]] += 1

print('Les modèles de montagne (cases bloquées : # ; largeur × hauteur, nombre de cases ; fois posé) :')
for name in sorted(models, key=lambda k: (k[-1], k)):
    cells = models[name]
    if not cells:
        continue
    w, h = shape(cells)
    print(f'{name:10} {w} × {h}, {len(cells):2} cases, posé {uses[name]:4} fois')
    if name.startswith('avlmtgr'):
        print('\n'.join(drawing(cells)))
print()
print('Montagnes pour 1000 cases de terre :', q(per_map))
print('Part de la terre bloquée par des montagnes (%) :', q(share_land))
print('Part des cases bloquées par des obstacles qui sont des montagnes (%) :', q(share_obst))
print('Montagnes par chaîne (se touchant) :', q(chains))
print('Chaînes par taille :', sorted(collections.Counter(chains).items()))
print('Part des montagnes seules (%) :', q(alone))
print('Terrain sous les montagnes :', under.most_common())
print('Obstacles qui touchent une montagne (classe : cases) :', near.most_common(10))
