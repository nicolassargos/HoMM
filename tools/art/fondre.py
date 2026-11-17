"""Fond les pentes des montagnes dans un sol de la carte (2026-11-17) : les pentes vertes d'une image de montagne prennent
les couleurs d'un sol (prises dans une capture du jeu, par rang de luminosité, plus deux tons d'ombre), et leur bord, ainsi
que tout le bas de l'image, se défait en trame (le sol de la carte passe à travers).

    python -I tools/art/fondre.py capture.png x0 y0 x1 y1 mode sortie sol images…

Le cadre x0 y0 x1 y1 de la capture ne montre que ce sol. mode : « vert » (seulement les pixels verts du cadre : l'herbe) ou
« tout » (tous, sauf l'eau bleue). sol : « » pour l'herbe (mêmes noms), « sable » ou « marais » (ajouté au nom :
massif-sable-cime-1.png, peak-sable-2.png). Les montagnes de chaque sol viennent ainsi des mêmes images d'herbe : seule la
roche est commune. Seule la bibliothèque standard de Python est utilisée.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire
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
CUT = {1: 0.75, 2: 0.5, 3: 0.25}
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
    for d in (1, 2, 3):
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
        if (green(p[y][x]) or y > ih * 0.6) and BAYER[y % 4][x % 4] / 16 < CUT[d]:
            q[y][x] = (0, 0, 0, 0)
    ecrire(os.path.join(out, nom(os.path.basename(path))), q)
