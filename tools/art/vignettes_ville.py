"""Les vignettes des bâtiments d'une ville, pour la fenêtre Construire (choix de l'utilisateur, 2026-11-28 : la grille de
Heroes 3, une vignette par bâtiment) : l'image de chaque bâtiment de la scène, réduite sans flou (`reduire.py`) pour
tenir dans une boîte, posée en bas et au milieu.

    python -I tools/art/vignettes_ville.py <faction>        (ex. chateau ; « tout » : toutes les villes peintes)

Écrit godot/art/towns/<faction>/vignettes/<id>.png (72 × 52 : les cartes de la grille), vignettes/grand/<id>.png
(150 × 110 : la fenêtre d'un bâtiment) et vignettes/recrue/<id>.png (95 × 112 : Recruter, une habitation à bâtir, dans
le cadre du paysage de 103 × 120). À relancer quand une image de la scène change.
Seule la bibliothèque standard de Python est utilisée.
"""
import json
import os
import subprocess
import sys

RACINE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VILLES = os.path.join(RACINE, "godot", "art", "towns")
REDUIRE = os.path.join(RACINE, "tools", "art", "reduire.py")
BOITES = {"": (72, 52), "grand": (150, 110), "recrue": (95, 112)}


def taille(chemin):
    with open(chemin, "rb") as f:
        tete = f.read(24)
    return int.from_bytes(tete[16:20], "big"), int.from_bytes(tete[20:24], "big")


def faction(nom):
    dossier = os.path.join(VILLES, nom)
    with open(os.path.join(dossier, "scene.json"), encoding="utf-8") as f:
        ids = [b["id"] for b in json.load(f)["buildings"]]
    for sous, (bw, bh) in BOITES.items():
        sortie = os.path.join(dossier, "vignettes", sous)
        os.makedirs(sortie, exist_ok=True)
        for i in ids:
            source = os.path.join(dossier, i + ".png")
            if not os.path.exists(source):
                print(f"  sans image : {nom}/{i}")
                continue
            w, h = taille(source)
            facteur = min(1.0, bw / w, bh / h)
            subprocess.run([sys.executable, "-I", REDUIRE, source, os.path.join(sortie, i + ".png"), f"{facteur:.4f}",
                            str(bw), str(bh)], check=True, capture_output=True)
    print(f"{nom} : {len(ids)} bâtiments")


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    noms = sorted(d for d in os.listdir(VILLES) if os.path.exists(os.path.join(VILLES, d, "scene.json"))) \
        if sys.argv[1] == "tout" else [sys.argv[1]]
    for nom in noms:
        faction(nom)


if __name__ == "__main__":
    main()
