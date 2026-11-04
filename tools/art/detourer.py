"""Rend transparent le fond uni d'une image (par exemple le magenta des feuilles d'animation).

    python -I tools/art/detourer.py source.png sortie.png [r g b] [tolérance]

Par défaut, le fond est magenta (255, 0, 255) avec une tolérance de 90 : les pixels proches de cette couleur
(et ceux nettement teintés de magenta, au bord de la créature) deviennent transparents.
Seule la bibliothèque standard de Python est utilisée.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import ecrire, lire  # noqa: E402


def detourer(px, fond, tolerance):
    n = 0
    for ligne in px:
        for x, (r, g, b, a) in enumerate(ligne):
            proche = ((r - fond[0]) ** 2 + (g - fond[1]) ** 2 + (b - fond[2]) ** 2) ** 0.5 <= tolerance
            # un pixel de bord mêlé au magenta : rouge et bleu forts, vert faible
            teinte = fond == (255, 0, 255) and r > 170 and b > 170 and g < 110
            if a and (proche or teinte):
                ligne[x] = (0, 0, 0, 0)
                n += 1
    return n


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    src, dst = sys.argv[1], sys.argv[2]
    fond = tuple(int(v) for v in sys.argv[3:6]) if len(sys.argv) >= 6 else (255, 0, 255)
    tolerance = float(sys.argv[6]) if len(sys.argv) >= 7 else 90
    _, _, px = lire(src)
    n = detourer(px, fond, tolerance)
    ecrire(dst, px)
    print(f"{n} pixels rendus transparents")
