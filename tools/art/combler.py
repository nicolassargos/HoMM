"""Comble les trous laissés dans une créature par un éclat effacé : un pixel vide entouré de la créature (des pixels pleins
sur au moins 3 de ses 4 côtés, ou sur ses deux côtés opposés) prend la couleur moyenne de ses voisins pleins ; répété
jusqu'à ce qu'il n'y ait plus rien à combler. Gratuit.

    python -I tools/art/combler.py <feuille.png> [images=8] [numéros des images : 2,3 ; toutes par défaut]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nettoyage as n
from reduire import lire, ecrire


def combler(q, passes=6):
    h, w = len(q), len(q[0])
    total = 0
    for _ in range(passes):
        plein = lambda x, y: 0 <= x < w and 0 <= y < h and q[y][x][3] > 128
        a_remplir = []
        for y in range(h):
            for x in range(w):
                if plein(x, y):
                    continue
                g, d, hh, b = plein(x - 1, y), plein(x + 1, y), plein(x, y - 1), plein(x, y + 1)
                if g + d + hh + b >= 3 or (g and d) or (hh and b):
                    voisins = [q[y + dy][x + dx] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if plein(x + dx, y + dy)]
                    a_remplir.append((x, y, tuple(sum(c[k] for c in voisins) // len(voisins) for k in range(3)) + (255,)))
        if not a_remplir:
            break
        for x, y, c in a_remplir:
            q[y][x] = c
        total += len(a_remplir)
    return total


if __name__ == "__main__":
    chemin = sys.argv[1]
    total = int(sys.argv[2]) if len(sys.argv) > 2 else 8
    images = n.decouper(lire(chemin)[2], 4, (total + 3) // 4)
    choisies = [int(k) for k in sys.argv[3].split(",")] if len(sys.argv) > 3 else range(1, total + 1)
    print("pixels comblés :", sum(combler(images[k - 1]) for k in choisies))
    ecrire(chemin, n.assembler(images))
