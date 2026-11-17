"""Lecture partielle des cartes de Heroes 3 (HotA) : taille, terrain, routes, rivières, modèles d'objets, objets.

Sans décoder tout l'en-tête : le bloc du terrain finit juste avant la liste des modèles d'objets (des noms en .def).
"""
import glob, gzip, re, struct, sys

TERRAIN = ['terre', 'sable', 'herbe', 'neige', 'marais', 'rocaille', 'souterrain', 'lave', 'eau', 'roche']


class Map:
    pass


def read(path):
    d = gzip.open(path).read()
    m = Map()
    m.name = path.split('/')[-1].split('\\')[-1]
    # la taille et les deux niveaux : "aa size(4) two" juste avant la longueur du nom
    for i in range(4, 80):
        size = struct.unpack_from('<I', d, i + 1)[0]
        n = struct.unpack_from('<I', d, i + 6)[0]
        if d[i] in (0, 1) and size in (36, 72, 108, 144, 180, 216, 252) and d[i + 5] in (0, 1) and 0 < n < 60 \
                and all(32 <= b < 127 or b >= 128 for b in d[i + 10:i + 10 + n]):
            m.size, m.levels = size, 1 + d[i + 5]
            break
    else:
        raise ValueError('taille introuvable')
    first = re.search(rb'[\x20-\x7e]{1,12}\.def', d, re.I)
    name_start = first.start()
    # la longueur du nom juste avant, et le nombre de modèles avant elle
    while struct.unpack_from('<I', d, name_start - 4)[0] != first.end() - name_start:
        name_start += 1
    count_at = name_start - 8
    tiles = m.size * m.size * m.levels
    start = count_at - tiles * 7
    t = d[start:count_at]
    m.terrain = [t[k * 7] for k in range(tiles)]
    m.river = [t[k * 7 + 2] for k in range(tiles)]
    m.road = [t[k * 7 + 4] for k in range(tiles)]
    assert max(m.terrain) <= 9 and max(m.road) <= 3 and max(m.river) <= 4, (m.name, max(m.terrain), max(m.road), max(m.river))
    # les modèles d'objets
    k = count_at
    ntpl = struct.unpack_from('<I', d, k)[0]; k += 4
    m.templates = []
    m.masks = []  # pour chaque modèle : (cases bloquées, cases d'entrée), en (colonne, rangée) d'un cadre de 8 × 6
    for _ in range(ntpl):
        ln = struct.unpack_from('<I', d, k)[0]; k += 4
        name = d[k:k + ln].decode('latin-1'); k += ln
        passage, action = d[k:k + 6], d[k + 6:k + 12]; k += 12
        # rangée 0 en haut ; le bit j d'une rangée est la colonne 7 - j (l'objet est posé par sa case en bas à droite)
        m.masks.append(({(7 - j, i) for i in range(6) for j in range(8) if not passage[i] >> j & 1},
                        {(7 - j, i) for i in range(6) for j in range(8) if action[i] >> j & 1}))
        k += 2 + 2  # terrains
        cls, sub = struct.unpack_from('<II', d, k); k += 8
        k += 1 + 1 + 16
        m.templates.append((name, cls, sub))
    m.objects_at = k
    m.data = d
    return m


def find_objects(m, classes):
    """Les objets de ces classes : on cherche « x y z index(4) 00×5 » pour chaque modèle de ces classes."""
    d = m.data
    found = []
    for idx, (name, cls, sub) in enumerate(m.templates):
        if cls not in classes:
            continue
        pat = struct.pack('<I', idx) + b'\0' * 5
        for hit in re.finditer(re.escape(pat), d[m.objects_at:]):
            p = m.objects_at + hit.start() - 3
            x, y, z = d[p], d[p + 1], d[p + 2]
            if x < m.size + 8 and y < m.size + 8 and z < m.levels:
                found.append((cls, sub, x, y, z, name))
    return found


if __name__ == '__main__':
    for f in sorted(glob.glob(sys.argv[1] + '/*.h3m')):
        try:
            m = read(f)
            land = sum(1 for t in m.terrain if t not in (8, 9))
            roads = sum(1 for r in m.road if r)
            towns = find_objects(m, {98, 77})
            print(f'{m.name[:34]:34} {m.size:3}x{m.levels} terre {land:6} routes {roads:5} ({100 * roads / max(1, land):4.1f} %) '
                  f'rivières {sum(1 for r in m.river if r):5} modèles {len(m.templates):3} villes {len(towns)}')
        except Exception as e:
            print('ERREUR', f, e)
