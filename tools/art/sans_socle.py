"""Retire le socle d'herbe (et son ombre) sous un arbre généré, pour qu'il se pose sur le sol du jeu.

    python -I tools/art/sans_socle.py entree.png sortie.png [part] [--buisson]

Retro Diffusion dessine souvent un arbre sur une petite tache d'herbe ou une ombre ovale : sur le champ de bataille,
cette tache ne se fond pas dans le sol (le jeu dessine sa propre ombre de contact, d'après les pieds de l'image :
tools/art/pieds.py). Dans la <part> du bas de l'image (0,22 par défaut, là où il n'y a que le tronc et les racines) :
- les pixels verts (le vert l'emporte nettement sur le rouge) et gris-sombres d'ombre sont retirés ;
- le contour sombre de la tache, qui ne touche plus rien, est retiré aussi ; celui des racines (il touche le brun du
  tronc) reste : les racines gardent leur trait (sans cela, elles paraissaient coupées, remarque de l'utilisateur) ;
- les rangées vides du bas sont coupées : le pied de l'image est le bas des racines (sinon l'arbre flottait au-dessus
  de son ombre).
Un buisson (--buisson) : son feuillage, vert, descend jusqu'en bas ; on ne retire alors que ce qui n'est pas vert (l'ombre
grise ou bleutée sous lui) et le contour sombre qui ne touche plus rien. Seule la bibliothèque standard est utilisée.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire  # noqa: E402

VIDE = (0, 0, 0, 0)


def vert(p):
    r, g, b, a = p
    return a > 0 and g > r + 15 and g >= b


def sombre(p):
    r, g, b, a = p
    return a > 0 and max(r, g, b) < 70


def ombre_grise(p):
    r, g, b, a = p
    return a > 0 and max(r, g, b) < 110 and max(r, g, b) - min(r, g, b) < 18 and r <= g + 5


def pas_vert(p):
    r, g, b, a = p
    return a > 0 and g <= max(r, b) + 6


def nettoyer(px, part, buisson=False):
    h, w = len(px), len(px[0])
    debut = int(h * (1 - part))
    retires = 0
    # 1. l'herbe et l'ombre grise du socle
    for y in range(debut, h):
        for x in range(w):
            p = px[y][x]
            if (pas_vert(p) and not sombre(p)) if buisson else (vert(p) or (ombre_grise(p) and not sombre(p))):
                px[y][x] = VIDE
                retires += 1
    # 2. le contour sombre qui ne touche plus aucun pixel clair (le bord de la tache, pas celui des racines)
    change = True
    while change:
        change = False
        for y in range(debut, h):
            for x in range(w):
                if not sombre(px[y][x]) or (buisson and not pas_vert(px[y][x])):
                    continue
                voisins = [px[y + dy][x + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1)
                           if (dx or dy) and 0 <= y + dy < h and 0 <= x + dx < w]
                if not any(v[3] > 0 and not sombre(v) for v in voisins):
                    px[y][x] = VIDE
                    retires += 1
                    change = True
    # 3. les petits morceaux détachés du bas (un reste de l'ombre : moins de 20 pixels, sans lien avec l'arbre)
    vus = set()
    for y in range(debut, h):
        for x in range(w):
            if px[y][x][3] == 0 or (x, y) in vus:
                continue
            morceau, pile = [], [(x, y)]
            vus.add((x, y))
            while pile:
                cx, cy = pile.pop()
                morceau.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in vus and px[ny][nx][3] > 0:
                            vus.add((nx, ny))
                            pile.append((nx, ny))
            if len(morceau) < 20:
                for cx, cy in morceau:
                    px[cy][cx] = VIDE
                    retires += 1
    # 4. les rangées vides du bas
    while px and all(p[3] == 0 for p in px[-1]):
        px.pop()
    return px, retires


def main():
    if len([a for a in sys.argv[1:] if not a.startswith("--")]) < 2:
        sys.exit(__doc__)
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    buisson = "--buisson" in sys.argv
    part = float(args[2]) if len(args) > 2 else 0.22
    w, h, px = lire(args[0])
    px, retires = nettoyer([list(r) for r in px], part, buisson)
    ecrire(args[1], px)
    print(f"{args[1]} : {retires} pixels de socle retirés, {h - len(px)} rangées vides coupées")


if __name__ == "__main__":
    main()
