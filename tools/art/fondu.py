"""Fond le socle d'herbe d'un sommet dans le terrain : l'herbe autour de la roche devient peu à peu transparente.

    python -I tools/art/fondu.py source.png sortie.png [largeur=7]

Chaque pixel du socle (herbe, contours sombres) perd de l'opacité selon sa distance à la roche (gris ou brun, assez clair) :
opaque contre la roche, transparent à « largeur » pixels. La mousse sur la roche, tout près d'elle, reste. Sans dépense ;
seule la bibliothèque standard de Python est utilisée.
"""
import colorsys
import os
import sys
from collections import deque

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire


def roche(c):
    """Un pixel de roche : gris ou brun, pas trop sombre (l'herbe, les contours sombres et le bord du socle n'en sont pas)."""
    h, l, s = colorsys.rgb_to_hls(c[0] / 255, c[1] / 255, c[2] / 255)
    verte = 0.16 <= h <= 0.45 and s >= 0.25
    return not verte and l >= 0.22


def fondre(px, largeur=7):
    h, w = len(px), len(px[0])
    herbe = [[px[y][x][3] > 0 and not roche(px[y][x]) for x in range(w)] for y in range(h)]
    # distance (en pas de 8 voisins) de chaque pixel à la roche la plus proche
    dist = [[None] * w for _ in range(h)]
    file = deque()
    for y in range(h):
        for x in range(w):
            if px[y][x][3] > 0 and not herbe[y][x]:
                dist[y][x] = 0
                file.append((x, y))
    while file:
        x, y = file.popleft()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and dist[ny][nx] is None:
                    dist[ny][nx] = dist[y][x] + 1
                    file.append((nx, ny))
    sortie = []
    for y in range(h):
        ligne = []
        for x in range(w):
            r, g, b, a = px[y][x]
            if herbe[y][x]:
                d = dist[y][x] if dist[y][x] is not None else largeur
                a = round(a * max(0.0, min(1.0, (largeur - d) / (largeur - 1))))
            ligne.append((r, g, b, a) if a else (0, 0, 0, 0))
        sortie.append(ligne)
    return sortie


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    largeur = int(sys.argv[3]) if len(sys.argv) > 3 else 7
    ecrire(sys.argv[2], fondre(lire(sys.argv[1])[2], largeur))
    print(f"{sys.argv[2]} : socle fondu sur {largeur} pixels")
