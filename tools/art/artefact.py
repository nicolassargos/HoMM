"""Les images des artefacts (recette dans METHODE.md, « Artefacts », refaits en 48).

    python -I tools/art/artefact.py apercus <dossier>
        chaque candidat <dossier>/<id>/<n>.png posé sur la tuile de son rang, comme le jeu (ArtifactLook) :
        écrit <dossier>-tuile/<id>/<n>.png ; les candidats trop grands pour la tuile sont laissés de côté
    python -I tools/art/artefact.py installer <id> <candidat.png>
        coupe les bords vides et écrit godot/art/artifacts/<id>.png, puis l'image de la carte, réduite de moitié
        (godot/art/artifacts/map/<id>.png)

Seule la bibliothèque standard de Python est utilisée.
"""
import glob
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire, reduire  # noqa: E402

RACINE = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ARTEFACTS = os.path.join(RACINE, "godot", "art", "artifacts")

# comme ArtifactLook : la tuile de 58, un bord sombre, un pixel libre, un dégradé de 0,55 à 0,25 fois la couleur du rang
TUILE = 58
COULEURS = {"Minor": (200, 210, 220), "Major": (80, 150, 240), "Relic": (190, 110, 240)}


def rangs():
    with open(os.path.join(RACINE, "godot", "data", "artifacts.json"), encoding="utf-8") as f:
        d = json.load(f)
    liste = d if isinstance(d, list) else next(v for v in d.values() if isinstance(v, list))
    return {a["id"]: a["rank"] for a in liste}


def rogner(px):
    """L'image sans ses bords vides."""
    pts = [(x, y) for y, r in enumerate(px) for x, p in enumerate(r) if p[3] > 0]
    x0, y0 = min(x for x, _ in pts), min(y for _, y in pts)
    x1, y1 = max(x for x, _ in pts), max(y for _, y in pts)
    return [r[x0:x1 + 1] for r in px[y0:y1 + 1]]


def tuile(objet, couleur):
    n = TUILE

    def teinte(k):
        return tuple(max(0, min(255, round(v * k))) for v in couleur) + (255,)

    t = [[teinte(0.35 if x in (0, n - 1) or y in (0, n - 1) else 0.55 - 0.3 * (x + y) / (2 * (n - 1)))
          for x in range(n)] for y in range(n)]
    h, w = len(objet), len(objet[0])
    ax, ay = (n - w) // 2, (n - h) // 2
    for passe in (0, 1):  # l'ombre (un pixel à droite, deux plus bas), puis l'objet
        for y in range(h):
            for x in range(w):
                c = objet[y][x]
                if c[3] < 26:
                    continue
                X, Y = ax + x + (1 - passe), ay + y + 2 * (1 - passe)
                if not (1 < X < n - 2 and 1 < Y < n - 2):
                    continue
                q = t[Y][X]
                a = c[3] / 255
                t[Y][X] = (tuple(v * 6 // 10 for v in q[:3]) if passe == 0
                           else tuple(round(c[i] * a + q[i] * (1 - a)) for i in range(3))) + (255,)
    return t


def apercus(dossier):
    r = rangs()
    for f in sorted(glob.glob(os.path.join(dossier, "*", "[0-9]*.png"))):
        ident = os.path.basename(os.path.dirname(f))
        objet = rogner(lire(f)[2])
        if max(len(objet), len(objet[0])) > TUILE - 4:
            continue  # trop grand pour la tuile
        sortie = os.path.join(dossier.rstrip("/\\") + "-tuile", ident)
        os.makedirs(sortie, exist_ok=True)
        ecrire(os.path.join(sortie, os.path.basename(f)), tuile(objet, COULEURS[r[ident]]))


def installer(ident, candidat):
    objet = rogner(lire(candidat)[2])
    ecrire(os.path.join(ARTEFACTS, f"{ident}.png"), objet)
    os.makedirs(os.path.join(ARTEFACTS, "map"), exist_ok=True)
    ecrire(os.path.join(ARTEFACTS, "map", f"{ident}.png"), reduire(objet, 0.5))
    print(ident, f"{len(objet[0])} × {len(objet)}")


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "apercus":
        apercus(sys.argv[2])
    elif len(sys.argv) == 4 and sys.argv[1] == "installer":
        installer(sys.argv[2], sys.argv[3])
    else:
        sys.exit(__doc__)
