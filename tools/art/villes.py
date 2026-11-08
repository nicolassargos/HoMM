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
    return f"{P}/{nom}@32px/{i}.png"


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


def chateau(donjon, tour, porte, mur, maisons, chapelle):
    """Le château du chevalier, serré comme dans Heroes 2 : arbres et chapelle au fond, grand donjon, maisons de chaque côté,
    deux tours, murs, porte au milieu du bas, maisons devant."""
    return [
        (f"{D}/oak.png", None, 12, 46), (f"{D}/pine.png", None, 150, 44), (f"{D}/autumn.png", None, 26, 36),
        (f"{D}/pine-2.png", None, 138, 34),
        (pc("chateau-chapelle", chapelle), 36, 124, 66, {"fondu": True}),
        (pc("chateau-maison", maisons[0]), 28, 34, 72, {"fondu": True}),
        (pc("chateau-donjon", donjon), 84, 80, 92, {"fondu": True}),
        (pc("chateau-maison", maisons[1]), 26, 52, 92, {"fondu": True}),
        (pc("chateau-maison", maisons[0]), 26, 110, 92, {"fondu": True, "miroir": True}),
        (pc("chateau-tour", tour), 46, 42, 112, {"fondu": True}),
        (pc("chateau-tour", tour), 46, 118, 112, {"fondu": True, "miroir": True}),
        (pc("chateau-mur", mur), 16, 60, 116), (pc("chateau-mur", mur), 16, 100, 116),
        (pc("chateau-porte", porte), 44, 80, 127, {"fondu": True}),
        (pc("chateau-maison", maisons[1]), 28, 16, 120, {"fondu": True}),
        (pc("chateau-maison", maisons[0]), 28, 144, 120, {"fondu": True, "miroir": True}),
        (f"{D}/oak-2.png", None, 154, 100), (f"{D}/oak-3.png", None, 6, 98),
    ]


def necropole(donjon, tour, porte, mur, crypte, cimetiere):
    """La nécropole, serrée : cimetières et arbres morts au fond, grand donjon, cryptes de chaque côté, tours, murs, porte
    au crâne, cryptes devant."""
    return [
        (pc("mv-cimetiere", cimetiere), 34, 22, 56, {"fondu": True}),
        (pc("mv-cimetiere", cimetiere), 32, 138, 52, {"fondu": True, "miroir": True}),
        (pc("mv-crypte", crypte), 28, 40, 76, {"fondu": True}),
        (pc("mv-crypte", crypte), 28, 120, 76, {"fondu": True, "miroir": True}),
        (pc("mv-donjon", donjon), 86, 80, 92, {"fondu": True}),
        (pc("mv-tour", tour), 52, 42, 112, {"fondu": True}),
        (pc("mv-tour", tour), 52, 118, 112, {"fondu": True, "miroir": True}),
        (pc("mv-mur", mur), 18, 60, 116), (pc("mv-mur", mur), 18, 100, 116, {"miroir": True}),
        (pc("mv-porte", porte), 46, 80, 127, {"fondu": True}),
        (pc("mv-crypte", crypte), 28, 16, 122, {"fondu": True}),
        (pc("mv-cimetiere", cimetiere), 28, 146, 124, {"fondu": True, "miroir": True}),
    ]


ESSAIS = {
    "town": [chateau(3, 4, 3, 2, (2, 3), 2), chateau(2, 1, 1, 3, (1, 2), 3), chateau(1, 4, 3, 4, (3, 4), 4)],
    "town-morts-vivants": [necropole(3, 2, 2, 2, 2, 2), necropole(1, 3, 2, 4, 3, 4), necropole(4, 2, 3, 2, 2, 3)],
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
