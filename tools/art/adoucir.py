# Adoucit le contour noir d'une image (comme le Moine ou le Griffon) : chaque pixel presque noir du bord
# prend la couleur de ses voisins de l'intérieur, assombrie. Aucune dépense, aucun flou.
# Utilisation : python -I tools/art/adoucir.py entree.png sortie.png [assombrir=0.55] [seuil=45] [passes=1]
#   assombrir : part de la couleur voisine gardée (plus petit = bord plus sombre)
#   seuil : luminosité sous laquelle un pixel du bord est considéré comme noir
#   passes : 2 traite aussi la ligne noire juste à l'intérieur du bord
# Marche aussi sur une feuille d'animation (chaque image est traitée de la même façon).
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire

VOISINS = ((1, 0), (-1, 0), (0, 1), (0, -1))


def lum(c):
    return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]


def adoucir(p, w, h, assombrir=0.55, seuil=45, passes=1):
    opaque = lambda x, y: 0 <= x < w and 0 <= y < h and p[y][x][3] > 128
    traites = set()
    for passe in range(passes):
        if passe == 0:
            # bord : pixel opaque qui touche la transparence
            cibles = [(x, y) for y in range(h) for x in range(w) if opaque(x, y)
                      and any(not opaque(x + dx, y + dy) for dx, dy in VOISINS)]
        else:
            # ligne suivante : pixel qui touche un pixel déjà traité
            cibles = [(x, y) for y in range(h) for x in range(w) if opaque(x, y) and (x, y) not in traites
                      and any((x + dx, y + dy) in traites for dx, dy in VOISINS)]
        cibles = [(x, y) for x, y in cibles if lum(p[y][x]) < seuil]
        nouveau = {}
        for x, y in cibles:
            # couleurs voisines de l'intérieur, pas noires (rayon 2)
            sources = [p[y + dy][x + dx] for dy in range(-2, 3) for dx in range(-2, 3)
                       if (dx or dy) and opaque(x + dx, y + dy) and (x + dx, y + dy) not in traites
                       and lum(p[y + dy][x + dx]) >= seuil]
            if not sources:
                continue
            r = sum(c[0] for c in sources) / len(sources)
            g = sum(c[1] for c in sources) / len(sources)
            b = sum(c[2] for c in sources) / len(sources)
            nouveau[(x, y)] = (int(r * assombrir), int(g * assombrir), int(b * assombrir), 255)
        for (x, y), c in nouveau.items():
            p[y][x] = c
        traites |= set(nouveau)
    return p


def main():
    if len(sys.argv) < 3:
        print(__doc__ or "python -I tools/art/adoucir.py entree.png sortie.png [assombrir] [seuil] [passes]")
        sys.exit(1)
    entree, sortie = sys.argv[1], sys.argv[2]
    assombrir = float(sys.argv[3]) if len(sys.argv) > 3 else 0.55
    seuil = float(sys.argv[4]) if len(sys.argv) > 4 else 45
    passes = int(sys.argv[5]) if len(sys.argv) > 5 else 1
    w, h, p = lire(entree)
    ecrire(sortie, adoucir(p, w, h, assombrir, seuil, passes))
    print(f"{sortie} : contour adouci")


if __name__ == "__main__":
    main()
