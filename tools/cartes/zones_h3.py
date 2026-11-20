"""Les zones des cartes de Heroes 3 telles que le joueur les voit (2026-11-20) : les régions où l'on marche, séparées par
les obstacles et par les monstres qui gardent un passage (la case du monstre et ses 8 voisines comptent comme un mur).
Pour chaque carte (la surface) : combien de régions d'au moins 20 cases, leur taille, la plus grande ; à comparer avec
les zones de MapGenerator (mesure Zones_vues_du_joueur).

    python -I zones_h3.py dossier
"""
import collections, glob, os, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read, find_objects

MONSTERS = {54, 71, 72, 73, 74, 75, 162, 163, 164}
MIN_REGION = 20


def q(v):
    if not v:
        return '-'
    v = sorted(v)
    return f'min {v[0]}  médiane {statistics.median(v):.0f}  max {v[-1]}  (n={len(v)})'


folder = sys.argv[1]
all_sizes, per_map, per_1000, biggest_share, rows = [], [], [], [], []
weighted, big_per_map, big_sizes, big_per_1000 = [], [], [], []
by_size = collections.defaultdict(lambda: ([], []))  # taille → (région d'une case typique, régions par carte)
BIG = 150
for f in sorted(glob.glob(folder + '/*.h3m')):
    try:
        m = read(f)
    except Exception as e:
        print('ERREUR', f, e)
        continue
    n = m.size
    wall = [[m.terrain[y * n + x] in (8, 9) for x in range(n)] for y in range(n)]
    classes = {cls for _, cls, _ in m.templates}
    monsters = 0
    for cls, sub, x, y, z, name in find_objects(m, classes):
        if z:
            continue
        if cls in MONSTERS:
            monsters += 1
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if 0 <= x + dx < n and 0 <= y + dy < n:
                        wall[y + dy][x + dx] = True
            continue
        idx = next(i for i, t in enumerate(m.templates) if t[0] == name and t[1] == cls)
        for c, r in m.masks[idx][0]:
            cx, cy = x - 7 + c, y - 5 + r
            if 0 <= cx < n and 0 <= cy < n:
                wall[cy][cx] = True
    land = sum(1 for k in range(n * n) if m.terrain[k] not in (8, 9))
    seen = [[False] * n for _ in range(n)]
    sizes = []
    for y0 in range(n):
        for x0 in range(n):
            if wall[y0][x0] or seen[y0][x0]:
                continue
            seen[y0][x0] = True
            stack, size = [(x0, y0)], 0
            while stack:
                x, y = stack.pop(); size += 1
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < n and 0 <= ny < n and not wall[ny][nx] and not seen[ny][nx]:
                            seen[ny][nx] = True; stack.append((nx, ny))
            if size >= MIN_REGION:
                sizes.append(size)
    if not sizes or monsters < 5:
        continue
    free = sum(sizes)
    all_sizes += sizes
    # la région d'une case typique : la médiane pondérée par les cases
    acc = 0
    for v in sorted(sizes):
        acc += v
        if acc * 2 >= free:
            weighted.append(v); by_size[n][0].append(v); break
    big = [v for v in sizes if v >= BIG]
    big_per_map.append(len(big)); big_sizes += big
    big_per_1000.append(round(1000 * len(big) / max(1, land), 2))
    per_map.append(len(sizes)); by_size[n][1].append(len(sizes))
    per_1000.append(round(1000 * len(sizes) / max(1, land), 1))
    biggest_share.append(round(100 * max(sizes) / free))
    rows.append(f'{m.name[:30]:30} {n:3} terre {land:6} monstres {monsters:4} régions {len(sizes):3} '
                f'médiane {statistics.median(sizes):5.0f} plus grande {max(sizes):5} ({100 * max(sizes) // free} %)')

print('\n'.join(rows))
print()
print('régions par carte    ', q(per_map))
print('régions / 1000 terre ', q(per_1000))
print('taille d\'une région  ', q(all_sizes))
print('part de la plus grande', q(biggest_share))
print()
print("la région d'une case typique (médiane pondérée par les cases)", q(weighted))
print(f"zones d'au moins {BIG} cases : par carte", q(big_per_map))
print(f"zones d'au moins {BIG} cases : taille", q(big_sizes))
print(f"zones d'au moins {BIG} cases / 1000 terre", q(big_per_1000))
print()
for size in sorted(by_size):
    w, c = by_size[size]
    print(f"{size} × {size} : la région d'une case typique", q(w), '; régions par carte', q(c))
