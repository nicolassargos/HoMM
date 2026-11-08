"""Assemble les villes de la carte (5 × 4 cases, 160 × 128 pixels d'art, comme Heroes 2) à partir de pièces séparées
(Retro Diffusion, groupe pieces/chateau-… et pieces/mv-… de assets.json) : le donjon au fond, des tours, des murs, la
porte au milieu de la rangée du bas (la case d'entrée), des maisons et des arbres autour. Fond transparent. Sans dépense.

    python -I tools/art/villes.py      écrit les essais dans art-candidats/en-cours/candidats/objects/<ville>@villes/<n>.png

Chaque pièce est (fichier, hauteur ou None, x du milieu de son pied, y de son pied, options) ; « miroir » la retourne,
« fondu » fond son socle d'herbe dans le terrain (tools/art/fondu.py). Les pièces sont dessinées de l'arrière vers l'avant
(par leur pied) et mélangées selon leur transparence. Seule la bibliothèque standard de Python est utilisée.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire, reduire
import nettoyage as n
from fondu import fondre

RACINE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
W, H = 160, 128
P = "art-candidats/en-cours/candidats/pieces"
D = "godot/art/decor"
SORTIE = "art-candidats/en-cours/candidats/objects"


def pc(nom, i):
    """Un candidat de pièce : un numéro (lot 32px) ou (lot, numéro), par exemple ("v2", 3) : vus de face, plus bas."""
    lot, i = i if isinstance(i, tuple) else ("32px", i)
    return f"{P}/{nom}@{lot}/{i}.png"


def charger(chemin, h=None, miroir=False, fondu=False):
    p = lire(os.path.join(RACINE, chemin))[2]
    if fondu:
        p = fondre(p, 6)
    p = n.recadrer(p)
    if h and len(p) != h:
        p = reduire(p, h / len(p))
    return n.miroir(p) if miroir else p


def composer(pieces):
    img = [[(0, 0, 0, 0)] * W for _ in range(H)]
    objets = []
    for chemin, h, x, y, *opts in pieces:
        o = opts[0] if opts else {}
        q = charger(chemin, h, o.get("miroir", False), o.get("fondu", False))
        objets.append((q, x - len(q[0]) // 2, y - len(q)))
    for q, x0, y0 in sorted(objets, key=lambda o: o[2] + len(o[0])):
        for j, r in enumerate(q):
            for i, c in enumerate(r):
                x, y = x0 + i, y0 + j
                if c[3] == 0 or not (0 <= x < W and 0 <= y < H):
                    continue
                a = c[3] / 255
                f = img[y][x]
                fa = f[3] / 255
                oa = a + fa * (1 - a)
                rgb = tuple(round((c[k] * a + f[k] * fa * (1 - a)) / oa) for k in range(3))
                img[y][x] = rgb + (round(oa * 255),)
    return img


def enceinte(prefixe, tour, mur, h_tour, h_mur, miroir_mur=False):
    """Les murs qui prennent toute la largeur : un mur du fond, deux tours au fond, des murs de côté et de face, deux
    grosses tours devant, aux coins."""
    pieces = []
    for x in (36, 64, 96, 124):
        pieces.append((pc(f"{prefixe}-mur", mur), h_mur - 2, x, 52))
    pieces += [(pc(f"{prefixe}-tour", tour), h_tour - 8, 14, 60, {"fondu": True}),
               (pc(f"{prefixe}-tour", tour), h_tour - 8, 146, 60, {"fondu": True, "miroir": True})]
    for x in (26, 54, 106, 134):
        pieces.append((pc(f"{prefixe}-mur", mur), h_mur, x, 118, {"miroir": miroir_mur and x > 80}))
    pieces += [(pc(f"{prefixe}-tour", tour), h_tour, 12, 126, {"fondu": True}),
               (pc(f"{prefixe}-tour", tour), h_tour, 148, 126, {"fondu": True, "miroir": True})]
    return pieces


def chateau(donjon, tour, porte, mur, maisons, chapelle):
    """Le château du chevalier, qui remplit ses 5 × 4 cases : enceinte, grand donjon au milieu, chapelle et maisons à
    l'intérieur, porte au milieu du bas. Rien dehors."""
    return enceinte("chateau", tour, mur, 56, 22) + [
        (pc("chateau-chapelle", chapelle), 40, 128, 84, {"fondu": True}),
        (pc("chateau-maison", maisons[0]), 30, 32, 84, {"fondu": True}),
        (pc("chateau-donjon", donjon), 100, 80, 100, {"fondu": True}),
        (pc("chateau-maison", maisons[1]), 30, 40, 108, {"fondu": True}),
        (pc("chateau-maison", maisons[0]), 30, 120, 108, {"fondu": True, "miroir": True}),
        (pc("chateau-porte", porte), 50, 80, 127, {"fondu": True}),
    ]


def necropole(donjon, tour, porte, mur, crypte, cimetiere):
    """La nécropole, qui remplit ses 5 × 4 cases : enceinte, grand donjon, cimetière et cryptes à l'intérieur, porte au
    crâne au milieu du bas. Rien dehors."""
    return enceinte("mv", tour, mur, 60, 22, miroir_mur=True) + [
        (pc("mv-cimetiere", cimetiere), 30, 34, 84, {"fondu": True}),
        (pc("mv-crypte", crypte), 32, 128, 84, {"miroir": True}),
        (pc("mv-donjon", donjon), 100, 80, 100, {"fondu": True}),
        (pc("mv-crypte", crypte), 30, 40, 108),
        (pc("mv-cimetiere", cimetiere), 28, 120, 108, {"fondu": True, "miroir": True}),
        (pc("mv-porte", porte), 50, 80, 127, {"fondu": True}),
    ]


V = lambda i: ("v2", i)
ESSAIS = {
    "town": [chateau(V(2), V(4), 3, 2, (V(4), V(2)), V(2)), chateau(V(4), V(1), 1, 3, (V(2), V(4)), V(4)),
             chateau(V(2), V(1), 3, 4, (V(4), V(1)), V(3))],
    "town-morts-vivants": [necropole(V(4), V(4), 2, 2, V(2), V(1)), necropole(V(3), V(1), 2, 4, V(4), V(3)),
                           necropole(V(4), V(1), 3, 2, V(2), V(3))],
}


def main():
    for ville, essais in ESSAIS.items():
        dossier = os.path.join(RACINE, SORTIE, f"{ville}@villes")
        os.makedirs(dossier, exist_ok=True)
        for k, pieces in enumerate(essais, start=1):
            ecrire(os.path.join(dossier, f"{k}.png"), composer(pieces))
        print(f"{ville} : {len(essais)} essais dans {os.path.relpath(dossier, RACINE)}")


if __name__ == "__main__":
    main()
