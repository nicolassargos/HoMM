"""Prépare un cadre de fenêtre pour le jeu : son centre uni devient transparent (le jeu le remplit lui-même).

    python -I tools/art/cadre.py source.png sortie.png [tolérance]

Le centre est trouvé à partir du pixel du milieu : tous les pixels voisins de couleur proche (écart total des
composantes rouge, vert, bleu inférieur à <tolérance>, 40 par défaut) deviennent transparents. L'image est ensuite
réduite à sa partie non transparente. Affiche l'épaisseur du bord (milieu de chaque côté), pour régler
« panelContentX » et « panelContentY » dans godot/art/ui/ui.json.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import ecrire, lire  # noqa: E402


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    source, sortie = sys.argv[1], sys.argv[2]
    tolerance = int(sys.argv[3]) if len(sys.argv) > 3 else 40
    w, h, px = lire(source)
    px = [list(ligne) for ligne in px]
    cx, cy = w // 2, h // 2
    ref = px[cy][cx]

    def proche(p):
        return p[3] > 128 and sum(abs(p[i] - ref[i]) for i in range(3)) < tolerance

    vus, pile = set(), [(cx, cy)]
    while pile:
        x, y = pile.pop()
        if (x, y) in vus or not (0 <= x < w and 0 <= y < h) or not proche(px[y][x]):
            continue
        vus.add((x, y))
        pile += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    for x, y in vus:
        px[y][x] = (0, 0, 0, 0)

    # réduite à sa partie non transparente
    xs = [x for y in range(h) for x in range(w) if px[y][x][3] > 0]
    ys = [y for y in range(h) for x in range(w) if px[y][x][3] > 0]
    x0, x1, y0, y1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1
    px = [ligne[x0:x1] for ligne in px[y0:y1]]
    w, h = x1 - x0, y1 - y0
    ecrire(sortie, px)

    milieu_x, milieu_y = w // 2, h // 2
    gauche = next(x for x in range(w) if px[milieu_y][x][3] == 0)
    haut = next(y for y in range(h) if px[y][milieu_x][3] == 0)
    print(f"{sortie} : {w} × {h}, centre transparent ({len(vus)} pixels), bord gauche {gauche} px, bord haut {haut} px")


if __name__ == "__main__":
    main()
