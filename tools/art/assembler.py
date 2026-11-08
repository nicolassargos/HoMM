"""Assemble les grands objets de la carte (mines de 3 × 2 cases) à partir de pièces séparées (Retro Diffusion, sans
socle), aux proportions de Heroes : un petit bâtiment au fond, sa porte au-dessus de la case d'entrée (en bas au
milieu), le décor autour (tas, rochers, arbres, chariot), un chemin ou des rails. Sans dépense.

    python -I tools/art/assembler.py [sortie=art-candidats/en-cours/candidats/mines-32] [noms…]

Les pièces et leur place sont décrites dans MINES (en pixels d'art, cases de 32 px : une mine fait 96 × 64). Une pièce
est « dossier/fichier.png » (un candidat de art-candidats/en-cours/candidats/pieces, ou une image du jeu) ; « h » la
réduit à cette hauteur ; « miroir » la retourne. Seule la bibliothèque standard de Python est utilisée.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire, reduire
import nettoyage as n

W, H = 96, 64
PIECES = "art-candidats/en-cours/candidats/pieces"
DECOR = "godot/art/decor"
OBJETS = "godot/art/objects"
CHEMIN = [(176, 140, 92, 255), (158, 124, 80, 255)]
FER, TRAVERSE = (66, 66, 72, 255), (112, 80, 50, 255)


def piece(chemin, h=None, miroir=False):
    p = n.recadrer(lire(chemin)[2])
    if h and len(p) > h:
        p = reduire(p, h / len(p))
    return n.miroir(p) if miroir else p


def p(nom, i, lot="32px"):
    return f"{PIECES}/{nom}@{lot}/{i}.png"


# chaque mine : le bâtiment (centré en x, ou sa porte au-dessus du chemin ; son bas à cette hauteur), puis les autres pièces (x, y du coin haut-gauche),
# un chemin (liste de points) ou des rails (début, fin)
MINES = {
    "mine-wood": {
        # cabane de PixelLab (retouche de la cabane@v4b n° 2 : une pièce en plus, sans sol), choisie le 2026-11-08 ; sa
        # porte (à 35 % de sa largeur) au-dessus du bout du chemin
        "batiment": (f"{PIECES}/cabane@pl-retouche/1.png", None, 46, 0.35),
        "pieces": [(f"{DECOR}/oak.png", None, 2, 2), (f"{DECOR}/pine-2.png", None, 80, 4), (p("charrette-bois", 1), 20, 2, 36),
                   (f"{OBJETS}/pile-wood.png", 16, 24, 47), (p("chevalet-scie", 3), 16, 60, 48), (p("souches", 2), 14, 80, 49)],
        "chemin": [(49 + ((y // 5) % 2), y) for y in range(42, 64)],
    },
    "mine-ore": {
        "batiment": (p("entree-mine", 3), None, 40),
        "pieces": [(f"{DECOR}/peak-small.png", 20, 4, 10), (f"{DECOR}/peak-small-2.png", 18, 74, 4), (f"{DECOR}/pine.png", None, 82, 14),
                   (p("chariot-rails", 2), 22, 16, 38), (f"{OBJETS}/pile-ore.png", 16, 62, 40), (f"{OBJETS}/pile-ore.png", 14, 72, 30),
                   (f"{OBJETS}/pile-ore.png", 14, 2, 48)],
        "rails": ((44, 38), (20, 63)),
    },
    "mine-gold": {
        "batiment": (p("entree-or", 4), None, 40),
        "pieces": [(f"{DECOR}/peak-small.png", 20, 2, 8), (f"{DECOR}/oak-2.png", None, 76, 4), (p("chariot-or", 3), 22, 60, 40),
                   (f"{OBJETS}/pile-gold.png", 16, 10, 44), (f"{OBJETS}/pile-gold.png", 14, 24, 50), (f"{DECOR}/peak-small-2.png", 16, 84, 34)],
        "rails": ((52, 38), (76, 63)),
    },
    "mine-gems": {
        "batiment": (p("grotte-gemmes", 1, "v2"), None, 42),
        "pieces": [(f"{DECOR}/peak-small.png", 20, 2, 10), (f"{DECOR}/pine-2.png", None, 80, 4), (f"{OBJETS}/pile-gems.png", 16, 14, 44),
                   (f"{OBJETS}/pile-gems.png", 14, 66, 46), (f"{DECOR}/peak-small-2.png", 16, 82, 38)],
        "chemin": [(48 + ((y // 5) % 2), y) for y in range(42, 64)],
    },
    "mine-crystal": {
        "batiment": (p("caverne-cristal", 3, "v2"), None, 42),
        "pieces": [(f"{DECOR}/peak-small-2.png", 20, 2, 8), (f"{DECOR}/peak-small.png", 18, 78, 6), (f"{OBJETS}/pile-crystal.png", 16, 12, 44),
                   (f"{OBJETS}/pile-crystal.png", 16, 68, 44), (f"{OBJETS}/pile-crystal.png", 12, 84, 30)],
        "chemin": [(48 + ((y // 5) % 2), y) for y in range(42, 64)],
    },
    "mine-sulfur": {
        "batiment": (p("entree-soufre", 2, "v3"), None, 42),
        "pieces": [(f"{DECOR}/peak-small-2.png", 20, 2, 6), (f"{DECOR}/peak-small.png", 18, 80, 8), (f"{OBJETS}/pile-sulfur.png", 16, 14, 44),
                   (f"{OBJETS}/pile-sulfur.png", 16, 66, 44), (f"{OBJETS}/pile-sulfur.png", 12, 4, 30)],
        "chemin": [(48 + ((y // 5) % 2), y) for y in range(42, 64)],
    },
    "mine-mercury": {
        "batiment": (p("laboratoire", 4, "v2"), None, 44),
        "pieces": [(f"{DECOR}/oak.png", None, 8, 0), (f"{DECOR}/autumn-2.png", None, 72, 2), (p("chaudron", 1), 20, 64, 38),
                   (f"{OBJETS}/pile-mercury.png", 16, 16, 44), (f"{DECOR}/peak-small.png", 14, 84, 40)],
        "chemin": [(48 + ((y // 5) % 2), y) for y in range(44, 64)],
    },
}


def chemin(img, points):
    for x, y in points:
        for dx in range(-3, 3):
            if 0 <= x + dx < W and 0 <= y < H:
                img[y][x + dx] = CHEMIN[(x + dx + y) % 2]


def rails(img, debut, fin, ecart=4):
    (x0, y0), (x1, y1) = debut, fin
    pas = max(abs(x1 - x0), abs(y1 - y0), 1)
    for k in range(pas + 1):
        x, y = round(x0 + (x1 - x0) * k / pas), round(y0 + (y1 - y0) * k / pas)
        if k % 3 == 0:
            for dx in range(-1, ecart + 2):
                if 0 <= x + dx < W and 0 <= y < H:
                    img[y][x + dx] = TRAVERSE
        for dx in (0, ecart):
            if 0 <= x + dx < W and 0 <= y < H:
                img[y][x + dx] = FER


def assembler(desc):
    img = [[(0, 0, 0, 0)] * W for _ in range(H)]
    if "chemin" in desc:
        chemin(img, desc["chemin"])
    if "rails" in desc:
        rails(img, *desc["rails"])
    src, h, bas, *porte = desc["batiment"]
    bat = piece(src, h)
    # centré, ou sa porte (à cette part de sa largeur) au-dessus du bout du chemin (x = 49)
    x = 49 - round(porte[0] * len(bat[0])) if porte else (W - len(bat[0])) // 2
    objets = [(bat, x, bas - len(bat))]
    for src, h, x, y in desc["pieces"]:
        objets.append((piece(src, h), x, y))
    # de l'arrière vers l'avant : par le bas de chaque pièce
    for q, x, y in sorted(objets, key=lambda o: o[2] + len(o[0])):
        for j, r in enumerate(q):
            for i, c in enumerate(r):
                if c[3] > 128 and 0 <= x + i < W and 0 <= y + j < H:
                    img[y + j][x + i] = c
    return img


def main():
    sortie = sys.argv[1] if len(sys.argv) > 1 else "art-candidats/en-cours/candidats/mines-32"
    noms = sys.argv[2:] or list(MINES)
    os.makedirs(sortie, exist_ok=True)
    for nom in noms:
        ecrire(os.path.join(sortie, f"{nom}.png"), assembler(MINES[nom]))
        print(nom, "assemblée")


if __name__ == "__main__":
    main()
