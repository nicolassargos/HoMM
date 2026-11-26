"""Un cadre mince pour les barres, tiré du cadre des fenêtres : seulement ses filets du bord extérieur et intérieur.

    python -I tools/art/cadre_mince.py [cadre.png] [sortie.png] [extérieur] [intérieur]

Par défaut, godot/art/ui/panel.png donne godot/art/ui/panel-mince.png. Le bord du cadre est lu au milieu de chaque
côté, du dehors vers le centre transparent ; on garde ses <extérieur> premières rangées (4 par défaut) et ses
<intérieur> dernières (3), sans le bois du milieu. Les coins sont des onglets nets (rangées emboîtées), les volutes ne
tenant pas dans si peu de place. Affiche l'épaisseur obtenue, pour « thinCorner » dans godot/art/ui/ui.json.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import ecrire, lire  # noqa: E402

RACINE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else os.path.join(RACINE, "godot", "art", "ui", "panel.png")
    sortie = sys.argv[2] if len(sys.argv) > 2 else os.path.join(RACINE, "godot", "art", "ui", "panel-mince.png")
    dehors = int(sys.argv[3]) if len(sys.argv) > 3 else 4
    dedans = int(sys.argv[4]) if len(sys.argv) > 4 else 3
    w, h, px = lire(source)

    def bord(pixels):
        """Les pixels du bord, du dehors jusqu'au premier pixel transparent : filets gardés seulement."""
        n = next(i for i, p in enumerate(pixels) if p[3] == 0)
        return pixels[:dehors] + pixels[n - dedans:n]

    haut = bord([px[y][w // 2] for y in range(h)])
    bas = bord([px[h - 1 - y][w // 2] for y in range(h)])
    gauche = bord([px[h // 2][x] for x in range(w)])
    droite = bord([px[h // 2][w - 1 - x] for x in range(w)])
    e = len(haut)
    taille = 4 * e
    image = [[(0, 0, 0, 0)] * taille for _ in range(taille)]
    for y in range(taille):
        for x in range(taille):
            # profondeur depuis chaque côté ; le côté le plus proche donne la couleur (onglet à 45°)
            cotes = [(y, haut), (taille - 1 - y, bas), (x, gauche), (taille - 1 - x, droite)]
            d, profil = min(cotes, key=lambda c: c[0])
            if d < e:
                image[y][x] = profil[d]
    ecrire(sortie, image)
    print(f"{sortie} : {taille} × {taille}, bord de {e} px (« thinCorner » : {e})")


if __name__ == "__main__":
    main()
