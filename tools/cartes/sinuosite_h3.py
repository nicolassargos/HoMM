"""Les détours des routes de Heroes 3 (2026-11-20) : entre deux cases de route distantes de 10 à 20 cases (à vol d'oiseau),
le chemin le plus court par la route (en diagonale aussi, √2), divisé par la distance à vol d'oiseau ; 1 : tout droit.
Des paires tirées au hasard (toujours les mêmes) sur la surface de chaque carte. À comparer avec la mesure
Detours_des_routes de nos cartes.

    python -I sinuosite_h3.py dossier [près loin]   (10 et 20 par défaut ; 5 et 8 : la forme d'une route de près)
"""
import glob, heapq, math, os, random, statistics, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from h3m import read

PAIRS = 150


def detours(n, road, near=10, far=20):
    cells = [c for c in road]
    rng = random.Random(7)
    ratios = []
    tries = 0
    while len(ratios) < PAIRS and tries < PAIRS * 30 and len(cells) > 20:
        tries += 1
        a, b = rng.choice(cells), rng.choice(cells)
        d = math.dist(a, b)
        if not near <= d <= far:
            continue
        # Dijkstra par les cases de route, jusqu'à 3 fois la distance
        best = {a: 0.0}
        heap = [(0.0, a)]
        found = None
        while heap:
            g, c = heapq.heappop(heap)
            if c == b:
                found = g
                break
            if g > best.get(c, 1e9) or g > 3 * d:
                continue
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    if dx == dy == 0:
                        continue
                    nb = (c[0] + dx, c[1] + dy)
                    if nb in road:
                        ng = g + (1.4142 if dx and dy else 1)
                        if ng < best.get(nb, 1e9):
                            best[nb] = ng
                            heapq.heappush(heap, (ng, nb))
        if found is not None:
            ratios.append(found / d)
    return ratios


if __name__ == '__main__':
    near, far = (int(sys.argv[2]), int(sys.argv[3])) if len(sys.argv) > 3 else (10, 20)
    per_map, everything = [], []
    for f in sorted(glob.glob(sys.argv[1] + '/*.h3m')):
        try:
            m = read(f)
        except Exception as e:
            print('ERREUR', f, e)
            continue
        n = m.size
        road = {(k % n, k // n) for k in range(n * n) if m.road[k]}
        r = detours(n, road, near, far)
        if len(r) < 20:
            continue
        per_map.append(statistics.median(r))
        everything += r
        print(f'{m.name[:30]:30} {n:3} routes {len(road):5} paires {len(r):3} détour médian {statistics.median(r):.3f}')
    q = sorted(everything)
    print()
    print(f'détour médian par carte : min {min(per_map):.3f} médiane {statistics.median(per_map):.3f} max {max(per_map):.3f} (n={len(per_map)})')
    print(f'toutes les paires : quart {q[len(q)//4]:.3f} médiane {q[len(q)//2]:.3f} trois quarts {q[3*len(q)//4]:.3f} 90 % {q[9*len(q)//10]:.3f}')
