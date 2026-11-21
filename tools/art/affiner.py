"""Amincit les traits sombres de 2 pixels à 1 : PixelLab en dessine parfois deux côte à côte (un noir, un brun
sombre), sur le bord d'un rocher, le haut d'un muret (fond de la ville du Château, 2026-11-21).

    python -I tools/art/affiner.py source.png sortie.png [seuil=70]

Dans une paire de pixels sombres (sous le seuil de luminosité), en ligne ou en colonne, entre deux pixels clairs : le
plus sombre reste, l'autre prend la couleur de son voisin clair. Un trait de 3 pixels ou plus ne bouge pas. Aucune
dépense, aucun flou. Regarder l'image à ×2 exactement : une page qui la réduit double aussi des traits.
Seule la bibliothèque standard de Python est utilisée.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import ecrire, lire


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def affiner(source, sortie, seuil=70):
    w, h, px = lire(source)
    lu = [[lum(c) for c in r] for r in px]

    def sombre(x, y):
        return 0 <= x < w and 0 <= y < h and lu[y][x] < seuil

    res = [list(r) for r in px]
    n = 0
    for y in range(h):
        for x in range(w):
            for dx, dy in ((1, 0), (0, 1)):
                x2, y2 = x + dx, y + dy
                xa, ya, xb, yb = x - dx, y - dy, x2 + dx, y2 + dy
                if not (sombre(x, y) and sombre(x2, y2)):
                    continue
                if not (0 <= xa and 0 <= ya and xb < w and yb < h) or sombre(xa, ya) or sombre(xb, yb):
                    continue
                if lu[y][x] <= lu[y2][x2]:
                    res[y2][x2] = px[yb][xb]
                else:
                    res[y][x] = px[ya][xa]
                n += 1
    ecrire(sortie, res)
    print(f"{sortie} : {n} pixels amincis")


if __name__ == "__main__":
    a = sys.argv
    if len(a) < 3:
        sys.exit(__doc__)
    affiner(a[1], a[2], float(a[3]) if len(a) > 3 else 70)
