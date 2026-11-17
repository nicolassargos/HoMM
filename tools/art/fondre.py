"""Fond les pentes des montagnes dans un sol de la carte (2026-11-17) : les pentes vertes d'une image de montagne prennent
les couleurs d'un sol (prises dans une capture du jeu, par rang de luminosité, plus deux tons d'ombre), et leur bord, ainsi
que tout le bas de l'image, se défait en trame (le sol de la carte passe à travers).

    python -I tools/art/fondre.py [--bord=dechire] capture.png x0 y0 x1 y1 mode sortie sol images…

Le cadre x0 y0 x1 y1 de la capture ne montre que ce sol. mode : « vert » (seulement les pixels verts du cadre : l'herbe) ou
« tout » (tous, sauf l'eau bleue). sol : « » pour l'herbe (mêmes noms), « sable » ou « marais » (ajouté au nom :
massif-sable-cime-1.png, peak-sable-2.png). Les montagnes de chaque sol viennent ainsi des mêmes images d'herbe : seule la
roche est commune. --bord : la façon dont le bord se défait, sur 4 pixels (2026-11-17, comparées avec l'utilisateur) :
« trame » (une trame régulière de Bayer, sur 3 pixels : elle se voyait comme une ligne de points), « fondu » (de plus en
plus transparent), « dechire » (des pixels ôtés par petites grappes irrégulières), « mixte » (déchiré, et les derniers
pixels à demi transparents). Seule la bibliothèque standard de Python est utilisée.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire
bord = next((a.split('=', 1)[1] for a in sys.argv if a.startswith('--bord=')), 'trame')
sys.argv = [a for a in sys.argv if not a.startswith('--bord=')]
lum = lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]
w, h, cap = lire(sys.argv[1]); x0, y0, x1, y1 = map(int, sys.argv[2:6]); mode = sys.argv[6]
keep = (lambda c: c[1] > c[0] + 15 and c[1] > c[2] + 15) if mode == 'vert' else (lambda c: c[0] >= c[2])  # pas l'eau
gs = sorted((c for y in range(y0, y1) for c in cap[y][x0:x1] if keep(c)), key=lum)
N = 6
pal = [tuple(sum(c[i] for c in gs[k * len(gs) // N:(k + 1) * len(gs) // N]) // max(1, len(gs[k * len(gs) // N:(k + 1) * len(gs) // N])) for i in range(3)) for k in range(N)]
pal = [tuple(int(v * 0.58) for v in pal[0]), tuple(int(v * 0.78) for v in pal[0])] + pal  # les pentes à l'ombre
N = len(pal)
print('couleurs du sol', pal)
green = lambda c: c[1] > c[0] + 4 and c[1] > c[2] + 4
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
CUT = {1: 0.75, 2: 0.5, 3: 0.25, 4: 0}
RAGGED = {'dechire': {1: 0.85, 2: 0.6, 3: 0.35, 4: 0.12}, 'mixte': {1: 0.6, 2: 0.35, 3: 0.12, 4: 0}}
FADE = {'fondu': {1: 0.2, 2: 0.4, 3: 0.6, 4: 0.8}, 'mixte': {1: 0.5, 2: 0.75}}


def bruit(x, y):
    """Un nombre de 0 à 1, en petites grappes de 2 × 2 pixels un peu brouillées : un bord qui s'effrite sans motif."""
    def h(a, b, s):
        n = (a * 374761393 + b * 668265263 + s * 2147483647) & 0xffffffff
        n = (n ^ (n >> 13)) * 1274126177 & 0xffffffff
        return ((n ^ (n >> 16)) & 0xffff) / 65536
    return 0.7 * h(x // 2, y // 2, 1) + 0.3 * h(x, y, 2)
out, sol = sys.argv[7], sys.argv[8]
def nom(base):
    if not sol:
        return base
    if base.startswith('massif-'):
        return 'massif-' + sol + '-' + base[len('massif-'):]
    return 'peak-' + sol + base[len('peak'):]
for path in sys.argv[9:]:
    iw, ih, p = lire(path)
    op = lambda x, y: 0 <= x < iw and 0 <= y < ih and p[y][x][3] > 128
    dist = {}
    front = [(x, y) for y in range(ih) for x in range(iw) if op(x, y) and any(not op(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for d in (1, 2, 3, 4):
        for x, y in front:
            dist.setdefault((x, y), d)
        front = list({(x + dx, y + dy) for x, y in front for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                      if op(x + dx, y + dy) and (x + dx, y + dy) not in dist})
    gr = sorted((lum(p[y][x]), x, y) for y in range(ih) for x in range(iw) if op(x, y) and green(p[y][x]))
    q = [list(r) for r in p]
    for i, (_, x, y) in enumerate(gr):
        q[y][x] = pal[min(N - 1, i * N // max(1, len(gr)))] + (255,)
    # le bord des pentes, et tout le bas de l'image (une base de roche aussi)
    for (x, y), d in dist.items():
        if not (green(p[y][x]) or y > ih * 0.6):
            continue
        if bord == 'trame':
            if BAYER[y % 4][x % 4] / 16 < CUT[d]:
                q[y][x] = (0, 0, 0, 0)
            continue
        if bruit(x, y) < RAGGED.get(bord, {}).get(d, 0):
            q[y][x] = (0, 0, 0, 0)
        elif d in FADE.get(bord, {}):
            q[y][x] = tuple(q[y][x][:3]) + (int(q[y][x][3] * FADE[bord][d]),)
    ecrire(os.path.join(out, nom(os.path.basename(path))), q)
