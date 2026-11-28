"""Les objets des cartes de Heroes 3 (2026-11-28) : combien de chaque sorte pour 1000 cases de terre (monstres, artefacts,
tas de ressources, petits trésors, mines, habitations, lieux), lesquels sont gardés (un monstre à 2 cases au plus de leur
case d'entrée), s'ils vont en groupes (un autre trésor à 3 cases au plus), à quelle distance des villes ; à comparer avec
la mesure Objets_par_zone de nos cartes.

    python -I objets_h3.py dossier
"""
import collections, glob, os, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read, find_objects

MONSTERS = {54, 71, 72, 73, 74, 75, 162, 163, 164}
GROUPS = {
    'artefacts': {5, 65, 66, 67, 68, 69},
    'ressources': {76, 79},
    'petits trésors': {12, 101, 93, 22, 6},  # feu de camp, coffre, parchemin, cadavre, boîte de Pandore
    'mines': {53, 220},
    'habitations': {17, 18, 19, 20, 216, 217, 218},
    'villes': {98, 77},
}
TREASURES = GROUPS['artefacts'] | GROUPS['ressources'] | GROUPS['petits trésors']
SKIP = {26, 8, 29, 82, 86, 94, 33, 34, 70, 214}  # événements, bateaux et objets de la mer, héros, prisons, signes…


def q(v, f='{:.1f}'):
    if not v:
        return '-'
    v = sorted(v)
    return f'{f.format(v[len(v) // 4])} / {f.format(statistics.median(v))} / {f.format(v[3 * len(v) // 4])}'


folder = sys.argv[1]
per1000 = collections.defaultdict(list)
guarded = collections.defaultdict(list)
grouped, town_dist, free_monsters, monster_share = [], [], [], []
sites_names = collections.Counter()
count = 0
for f in sorted(glob.glob(folder + '/*.h3m')):
    try:
        m = read(f)
    except Exception as e:
        continue
    n = m.size
    land = sum(1 for k in range(n * n) if m.terrain[k] not in (8, 9))
    if land < 500:
        continue
    count += 1
    all_classes = {cls for _, cls, _ in m.templates}
    found = [o for o in find_objects(m, all_classes) if o[4] == 0 and o[2] < n and o[3] < n]
    # la case d'entrée : celle du masque d'action, sinon la case de pose
    tpl = {name: i for i, (name, _, _) in enumerate(m.templates)}

    def entry(o):
        cls, sub, x, y, z, name = o
        _, action = m.masks[tpl[name]]
        if action:
            c, r = max(action, key=lambda a: (a[1], a[0]))
            return x - (7 - c), y - (5 - r)
        return x, y

    def kind(o):
        cls = o[0]
        if cls in MONSTERS:
            return 'monstres'
        for g, s in GROUPS.items():
            if cls in s:
                return g
        if cls in SKIP:
            return None
        _, action = m.masks[tpl[o[5]]]
        return 'lieux' if action else None

    kinds = collections.Counter()
    items = []
    for o in found:
        k = kind(o)
        if k:
            kinds[k] += 1
            items.append((k, o, entry(o)))
            if k == 'lieux':
                sites_names[o[0]] += 1
    for k in list(GROUPS) + ['monstres', 'lieux']:
        per1000[k].append(1000 * kinds[k] / land)
    monsters = [e for k, o, e in items if k == 'monstres']
    towns = [e for k, o, e in items if k == 'villes']
    near_monster = lambda p, d=2: any(max(abs(p[0] - a), abs(p[1] - b)) <= d for a, b in monsters)
    for g in ['artefacts', 'ressources', 'petits trésors', 'mines', 'habitations', 'lieux']:
        es = [e for k, o, e in items if k == g]
        if es:
            guarded[g].append(100 * sum(near_monster(e) for e in es) / len(es))
    tr = [e for k, o, e in items if o[0] in TREASURES]
    if len(tr) > 1:
        grouped.append(100 * sum(any(p != t and max(abs(p[0] - t[0]), abs(p[1] - t[1])) <= 3 for t in tr) for p in tr) / len(tr))
    if towns:
        for p in tr:
            town_dist.append(min(max(abs(p[0] - a), abs(p[1] - b)) for a, b in towns))
    others = [e for k, o, e in items if k not in ('monstres', 'villes')]
    if monsters:
        free = sum(not any(max(abs(p[0] - a), abs(p[1] - b)) <= 2 for a, b in others) for p in monsters)
        free_monsters.append(100 * free / len(monsters))

print(f'{count} cartes ; quartiles : bas / médiane / haut')
print('Pour 1000 cases de terre :')
for k, v in per1000.items():
    print(f'  {k:15} {q(v)}')
print('Part gardée (un monstre à 2 cases au plus), en % :')
for k, v in guarded.items():
    print(f'  {k:15} {q(v, "{:.0f}")}')
print(f'Trésors (artefacts, ressources, petits) avec un autre à 3 cases au plus : {q(grouped, "{:.0f}")} %')
print(f'Monstres loin de tout objet (passages, errants) : {q(free_monsters, "{:.0f}")} %')
td = sorted(town_dist)
print(f'Distance d\'un trésor à la ville la plus proche : {q(td, "{:.0f}")} cases ; à moins de 10 : '
      f'{100 * sum(d < 10 for d in td) / len(td):.0f} %, 10-20 : {100 * sum(10 <= d < 20 for d in td) / len(td):.0f} %')
print('Lieux les plus posés (classe : nombre) :', sites_names.most_common(25))
