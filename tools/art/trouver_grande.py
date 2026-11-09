"""Pour une créature sans grande image : retrouve, parmi les images brutes de ses archives, celle d'où vient son image de
combat installée (la plus ressemblante une fois réduite, droite ou retournée), la copie comme grande image et lui fait une
fiche minimale (nom, hauteur actuelle, grande, miroir), pour que reconstruire.py puisse la refaire.
python -I tools/art/trouver_grande.py <archives> id1 id2 ...   (depuis la racine du dépôt)
"""
import os, shutil, sys
sys.path.insert(0, os.path.join(os.getcwd(), "tools", "art"))
import creature as c
import nettoyage as n
from reduire import lire

archives = sys.argv[1]
donnees = c.charger()


def ecart(a, b):
    h, w = min(len(a), len(b)), min(len(a[0]), len(b[0]))
    diff = tot = 0
    for y in range(h):
        for x in range(w):
            pa, pb = a[y][x], b[y][x]
            oa, ob = pa[3] > 128, pb[3] > 128
            if oa or ob:
                tot += 1
                if oa != ob or abs(pa[0] - pb[0]) + abs(pa[1] - pb[1]) + abs(pa[2] - pb[2]) > 90:
                    diff += 1
    return diff / max(1, tot) + abs(len(a) - len(b)) * 0.02 + abs(len(a[0]) - len(b[0])) * 0.02


for ident in sys.argv[2:]:
    installee = n.recadrer(lire(f"{c.BATAILLE}/{ident}.png")[2])
    hauteur = len(installee)
    candidats = []
    for base, dirs, files in os.walk(archives):
        if ident not in base.replace("\\", "/").split("/")[-1] and f"/{ident}" not in base.replace("\\", "/"):
            continue
        for nom in files:
            if nom.endswith(".png") and (nom[:-4].isdigit() or nom.startswith(("pro", "grand", "statique"))):
                candidats.append(os.path.join(base, nom))
    meilleur = None
    for chemin in candidats:
        try:
            p = n.recadrer(lire(chemin)[2])
        except Exception:
            continue
        if len(p) < hauteur * 1.4:  # trop petite : pas une grande image
            continue
        for miroir in (False, True):
            q = n.adoucir_image(n.reduire_traits(n.miroir(p) if miroir else p, hauteur / len(p)))
            e = ecart(installee, n.recadrer(q))
            if meilleur is None or e < meilleur[0]:
                meilleur = (e, chemin, miroir, len(p))
    if meilleur is None:
        print(ident, ": aucune image brute assez grande")
        continue
    e, chemin, miroir, h = meilleur
    os.makedirs(c.GARDEES, exist_ok=True)
    grande = f"{c.GARDEES}/{ident}-grand.png"
    shutil.copy(chemin, grande)
    f = donnees["creatures"].setdefault(ident, {"nom": ident})
    f.update({"hauteur": hauteur, "grande": grande})
    if miroir:
        f["miroir"] = True
    f.setdefault("notes", "Fiche minimale (2026-11-09) : grande image retrouvée dans les archives pour le passage du combat à ×2.")
    print(f"{ident}: hauteur {hauteur}, grande {h} px, écart {e:.3f}{' (retournée)' if miroir else ''} <- {chemin}")
c.enregistrer(donnees)
