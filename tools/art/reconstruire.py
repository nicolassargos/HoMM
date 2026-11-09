"""Refait les images de combat d'une créature <facteur> fois plus grandes (passage du combat de ×3 à ×2), sans dépense.

Image fixe : depuis sa grande image (fiche « grande »). Feuilles d'animation : chaque image de la feuille installée est
retrouvée parmi les images brutes de PixelLab gardées dans les archives (la plus ressemblante, à l'ancienne taille, droite
ou retournée), puis refaite depuis cette image brute à la nouvelle taille, avec le même nettoyage. Les feuilles coupées,
réordonnées ou dont une image a été recopiée sont ainsi refaites pareil. Un rapport donne la ressemblance de chaque image
(0 : identique ; plus haut : retouchée à la main, à vérifier).

python -I tools/art/reconstruire.py <archives> <rapport.json> <facteur> id1 id2 ...   (depuis la racine du dépôt)
"""
import json, math, os, sys
sys.path.insert(0, os.path.join(os.getcwd(), "tools", "art"))
import creature as c
import nettoyage as n
from reduire import lire, ecrire

archives, rapport_chemin, facteur = sys.argv[1], sys.argv[2], float(sys.argv[3])
vitesses = json.load(open(c.VITESSES, encoding="utf-8"))
donnees = c.charger()

# tous les dossiers d'images brutes d'animation : (créature, nom) -> [dossiers]
bruts = {}
for base, dirs, files in os.walk(archives):
    if "1.png" not in files or "2.png" not in files:
        continue
    parts = base.replace("\\", "/").split("/")
    nom = parts[-1]
    if parts[-2] == "anim":
        ident = parts[-3]
    elif parts[-2].endswith("-anim"):
        ident = parts[-2][:-5]
    elif parts[-2] == "pixellab" and "-" in nom:
        # la toute première façon : pixellab/<id>-<état>/
        ident, _, nom = nom.rpartition("-")
        nom = nom + "-1"
    else:
        continue
    bruts.setdefault(ident, {}).setdefault(nom, []).append(base)
ALIAS = {"colosse": "colosse-os"}
for ancien, nouveau in ALIAS.items():
    for nom, l in bruts.pop(ancien, {}).items():
        bruts.setdefault(nouveau, {}).setdefault(nom, []).extend(l)


def vide(q):
    return not any(p[3] for r in q for p in r)


def ecart(a, b):
    """Différence entre deux images de même taille (à 1 pixel près) : part des pixels dont la présence ou la couleur diffère."""
    h, w = min(len(a), len(b)), min(len(a[0]), len(b[0]))
    diff = tot = 0
    for y in range(h):
        ra, rb = a[y], b[y]
        for x in range(w):
            pa, pb = ra[x], rb[x]
            oa, ob = pa[3] > 128, pb[3] > 128
            if oa or ob:
                tot += 1
                if oa != ob or abs(pa[0] - pb[0]) + abs(pa[1] - pb[1]) + abs(pa[2] - pb[2]) > 90:
                    diff += 1
    return diff / max(1, tot)


def preparer(brut, f_reduc, f, etat):
    q = n.adoucir_image(n.reduire_traits(n.retirer_cyan(brut), f_reduc), f.get("contour"))
    n.retirer_etincelles(q)
    if f.get("corde") and etat != "attack":
        n.corde_droite(q)
    return n.bord_translucide(q, f.get("bord"))


rapport = {}
for ident in sys.argv[4:]:
    f, _ = c.fiche(donnees, ident)
    ancienne = f["hauteur"]
    nouvelle = round(ancienne * facteur)
    r = rapport[ident] = {"hauteur": [ancienne, nouvelle]}
    # l'image fixe
    if f.get("grande") and os.path.exists(f["grande"]):
        g = dict(f, hauteur=nouvelle)
        q = c.preparer_fixe(lire(f["grande"])[2], g)
        centre = n.axe(q)
        largeur = 2 * math.ceil(max(centre, len(q[0]) - centre)) + 2
        ecrire(f"{c.BATAILLE}/{ident}.png", n.poser(q, largeur, len(q) + 2, centre))
        r["fixe"] = "refaite"
    else:
        r["fixe"] = "PAS DE GRANDE IMAGE"
    for etat in ("idle", "move", "attack", "transform"):
        chemin = f"{c.BATAILLE}/{ident}-{etat}.png"
        if not os.path.exists(chemin):
            continue
        reglages = vitesses.get(ident, {})
        nombre = reglages.get(f"{etat}Frames", 8)
        rangees = math.ceil(nombre / 4)
        feuille = lire(chemin)[2]
        installees = [q for q in n.decouper(feuille, 4, rangees)][:nombre]
        ch = len(feuille) // rangees
        # les dossiers à essayer : ceux du choix de la fiche d'abord, sinon tous ceux de cet état
        choix = f.get("choix", {}).get(etat, "").split(",")[0].split(" ")[0]
        base = choix.split("-r")[0].replace("-corde", "").replace("-propre", "").replace("-main", "")
        noms = [base, base.replace("move", "walk")] if base else []
        dossiers = [d for nm in noms for d in bruts.get(ident, {}).get(nm, [])]
        if not dossiers:
            prefixes = (etat, "walk") if etat == "move" else (etat,)
            dossiers = [d for nm, l in bruts.get(ident, {}).items() if nm.startswith(prefixes) for d in l]
        meilleur = None
        for d in dossiers:
            k = 1
            images = []
            while os.path.exists(os.path.join(d, f"{k}.png")):
                images.append(lire(os.path.join(d, f"{k}.png"))[2])
                k += 1
            f_ancien = ch / len(images[0])
            reduites = []
            for im in images:
                q = preparer(im, f_ancien, f, etat)
                reduites.append((q, False))
                reduites.append((n.miroir(q), True))
            choisis, total = [], 0
            for q in installees:
                if vide(q):
                    choisis.append(None)
                    continue
                e, i = min((ecart(q, rq), i) for i, (rq, _) in enumerate(reduites))
                choisis.append((i // 2, reduites[i][1], round(e, 3)))
                total += e
            if meilleur is None or total < meilleur[0]:
                meilleur = (total, d, images, choisis, f_ancien)
        if meilleur is None:
            r[etat] = "PAS D'IMAGES BRUTES"
            continue
        total, d, images, choisis, f_ancien = meilleur
        f_nouveau = f_ancien * facteur
        nouvelles = []
        for ch_ in choisis:
            if ch_ is None:
                nouvelles.append(None)
                continue
            i, retourne, _ = ch_
            q = preparer(images[i], f_nouveau, f, etat)
            nouvelles.append(n.miroir(q) if retourne else q)
        ref = next(q for q in nouvelles if q is not None)
        h, w = len(ref), len(ref[0])
        nouvelles = [q if q is not None else [[n.VIDE] * w for _ in range(h)] for q in nouvelles]
        while len(nouvelles) % 4:
            nouvelles.append([[n.VIDE] * w for _ in range(h)])
        ecrire(chemin, n.assembler(nouvelles))
        r[etat] = {"dossier": d.replace("\\", "/").split("art-candidats/")[-1],
                   "images": [None if x is None else (x[0] + 1, "retournée" if x[1] else "", x[2]) for x in choisis],
                   "ecart_max": max((x[2] for x in choisis if x), default=0)}
    # le point de départ des tirs grandit aussi
    if "launchPoint" in vitesses.get(ident, {}):
        vitesses[ident]["launchPoint"] = [round(v * facteur) for v in vitesses[ident]["launchPoint"]]
        r["launchPoint"] = vitesses[ident]["launchPoint"]
    f["hauteur"] = nouvelle
    print(ident, json.dumps({k: (v if not isinstance(v, dict) else v["ecart_max"]) for k, v in r.items()}, ensure_ascii=False))

c.enregistrer(donnees)
with open(c.VITESSES, "w", encoding="utf-8") as fo:
    fo.write("{\n" + ",\n".join(f"  {json.dumps(k, ensure_ascii=False)}: {json.dumps(v, ensure_ascii=False)}" for k, v in vitesses.items()) + "\n}\n")
json.dump(rapport, open(rapport_chemin, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
