"""Recadre une image sur son dessin : les colonnes et les rangées entièrement transparentes du bord sont retirées.

    python -I tools/art/recadrer.py entree.png sortie.png

Fait pour les massifs de montagnes de PixelLab (2026-11-17) : la place d'un massif sur la carte vient de la taille de
son dessin (tools/art/empreintes.py). Seule la bibliothèque standard de Python est utilisée.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire

w, h, px = lire(sys.argv[1])
ys = [y for y in range(h) if any(p[3] for p in px[y])]
xs = [x for x in range(w) if any(px[y][x][3] for y in range(h))]
px = [row[xs[0]:xs[-1] + 1] for row in px[ys[0]:ys[-1] + 1]]
ecrire(sys.argv[2], px)
print(f'{sys.argv[2]} : {len(px[0])} × {len(px)}')
