"""Génère les images du jeu avec l'API de Retro Diffusion (pixel art).

Les images à produire sont décrites dans assets.json (à côté de ce script). La clé d'API est lue dans
la variable d'environnement RETRO_DIFFUSION_API_KEY (jamais écrite dans un fichier du projet).

    python -I tools/art/generer.py cout [noms...]       prix estimé, gratuit (rien n'est généré)
    python -I tools/art/generer.py generer noms...      génère les candidats dans art-candidats/en-cours/candidats/<nom>/
    python -I tools/art/generer.py choisir nom numero    copie le candidat choisi dans godot/art/<nom>.png

Sans nom, « cout » estime toutes les images. Un nom peut être un préfixe (« terrain/ »).
Un nom avec « @ » (« terrain/grass@48 ») est un essai : « choisir » le copie à la place de terrain/grass.
Une image avec « input » part d'une image existante (style rd_tile__tile_variation : variantes d'une case).
« choisir » cherche le candidat dans art-candidats/en-cours/, puis dans les archives (le plus récent d'abord).
Seule la bibliothèque standard de Python est utilisée.
"""

import base64
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.request
import zlib

API = "https://api.retrodiffusion.ai/v2"
ICI = os.path.dirname(os.path.abspath(__file__))
RACINE = os.path.normpath(os.path.join(ICI, "..", ".."))
CANDIDATS = os.path.join(RACINE, "art-candidats", "en-cours", "candidats")
ARCHIVES = os.path.join(RACINE, "art-candidats", "archives")
ART = os.path.join(RACINE, "godot", "art")


def charger_assets():
    with open(os.path.join(ICI, "assets.json"), encoding="utf-8") as f:
        return json.load(f)["assets"]


def choisir_assets(assets, noms):
    if not noms:
        return assets
    choisis = [a for a in assets if any(a["name"] == n or (n.endswith("/") and a["name"].startswith(n)) for n in noms)]
    inconnus = [n for n in noms if not any(a["name"] == n or (n.endswith("/") and a["name"].startswith(n)) for a in assets)]
    if inconnus:
        sys.exit(f"Inconnu dans assets.json : {', '.join(inconnus)}")
    return choisis


def cle():
    valeur = os.environ.get("RETRO_DIFFUSION_API_KEY")
    if not valeur:
        sys.exit("La variable d'environnement RETRO_DIFFUSION_API_KEY n'est pas définie.")
    return valeur


def requete(methode, chemin, corps=None):
    entetes = {"X-RD-Token": cle(), "Content-Type": "application/json"}
    donnees = json.dumps(corps).encode("utf-8") if corps is not None else None
    req = urllib.request.Request(API + chemin, data=donnees, headers=entetes, method=methode)
    try:
        with urllib.request.urlopen(req, timeout=120) as reponse:
            return json.loads(reponse.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        sys.exit(f"Erreur {e.code} de l'API : {e.read().decode('utf-8', 'replace')}")


def image_rgb_base64(chemin):
    """Une image PNG en base64, sans transparence (l'API veut du RGB) : on relit le fichier et on retire le canal alpha."""
    import struct
    d = open(chemin, "rb").read()
    i = 8
    idat = b""
    while i < len(d):
        n, t = struct.unpack(">I4s", d[i:i + 8])
        c = d[i + 8:i + 8 + n]
        i += 12 + n
        if t == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", c[:10])
        elif t == b"IDAT":
            idat += c
    if bd != 8 or ct not in (2, 6):
        sys.exit(f"Image non prise en charge (RGB ou RGBA 8 bits attendu) : {chemin}")
    bpp = 4 if ct == 6 else 3
    raw = zlib.decompress(idat)
    lignes = []
    prec = bytearray(w * bpp)
    k = 0
    for _ in range(h):
        f = raw[k]
        k += 1
        ligne = bytearray(raw[k:k + w * bpp])
        k += w * bpp
        for x in range(len(ligne)):
            a = ligne[x - bpp] if x >= bpp else 0
            b = prec[x]
            cc = prec[x - bpp] if x >= bpp else 0
            if f == 1: ligne[x] = (ligne[x] + a) & 255
            elif f == 2: ligne[x] = (ligne[x] + b) & 255
            elif f == 3: ligne[x] = (ligne[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - cc
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - cc)
                ligne[x] = (ligne[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else cc)) & 255
        lignes.append(b"\x00" + b"".join(bytes(ligne[x * bpp:x * bpp + 3]) for x in range(w)))
        prec = ligne
    def bloc(t, data):
        return struct.pack(">I", len(data)) + t + data + struct.pack(">I", zlib.crc32(t + data))
    png = (b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
           + bloc(b"IDAT", zlib.compress(b"".join(lignes))) + bloc(b"IEND", b""))
    return base64.b64encode(png).decode("ascii")


def graine(nom):
    """Une graine fixe par image : relancer donne un résultat proche."""
    return zlib.crc32(nom.encode("utf-8")) % 1_000_000


def demande(asset, cout_seulement=False):
    corps = {
        "prompt": asset["prompt"],
        "prompt_style": asset["style"],
        "width": asset["size"][0] if isinstance(asset["size"], list) else asset["size"],
        "height": asset["size"][1] if isinstance(asset["size"], list) else asset["size"],
        "num_images": asset.get("variants", 1),
        "seed": asset.get("seed", graine(asset["name"])),
    }
    if asset.get("remove_bg"):
        corps["remove_bg"] = True
    if asset.get("tile"):
        corps["tile_x"] = corps["tile_y"] = True
    if asset.get("input"):
        corps["input_image"] = image_rgb_base64(os.path.join(RACINE, asset["input"]))
    if cout_seulement:
        corps["check_cost"] = True
    return corps


def resultat(reponse):
    """La réponse finale : directe (estimation du prix) ou après avoir attendu la tâche."""
    if "task_id" not in reponse:
        return reponse
    tache = reponse["task_id"]
    while True:
        etat = requete("GET", f"/inferences/tasks/{tache}")
        if etat["status"] == "succeeded":
            return etat["result"]
        if etat["status"] == "failed":
            sys.exit(f"Échec de la génération : {etat.get('error')}")
        time.sleep(2)


def cout(assets):
    total = 0.0
    solde = None
    for asset in assets:
        r = resultat(requete("POST", "/inferences", demande(asset, cout_seulement=True)))
        total += r["balance_cost"]
        solde = r.get("remaining_balance", solde)
        print(f"{asset['name']:32} {asset.get('variants', 1)} image(s)  {r['balance_cost']:.3f} $")
    print(f"Total : {total:.3f} $ (solde : {solde} $)")


def generer(assets):
    for asset in assets:
        dossier = os.path.join(CANDIDATS, *asset["name"].split("/"))
        os.makedirs(dossier, exist_ok=True)
        r = resultat(requete("POST", "/inferences", demande(asset)))
        for i, image in enumerate(r.get("base64_images", []), start=1):
            with open(os.path.join(dossier, f"{i}.png"), "wb") as f:
                f.write(base64.b64decode(image))
        print(f"{asset['name']:32} {len(r.get('base64_images', []))} candidat(s)  {r['balance_cost']:.3f} $  (solde : {r.get('remaining_balance')} $)")


def choisir(nom, numero):
    chemin = os.path.join(*nom.split("/"), f"{numero}.png")
    lieux = [CANDIDATS] + [os.path.join(ARCHIVES, d, "candidats")
                           for d in sorted(os.listdir(ARCHIVES), reverse=True)] if os.path.isdir(ARCHIVES) else [CANDIDATS]
    source = next((os.path.join(l, chemin) for l in lieux if os.path.exists(os.path.join(l, chemin))), None)
    if source is None:
        sys.exit(f"Candidat introuvable : {chemin} (ni en cours, ni dans les archives)")
    # « terrain/grass@48 » est un essai d'une autre taille de terrain/grass : il remplace la même image
    cible = os.path.join(ART, *nom.split("@")[0].split("/")) + ".png"
    os.makedirs(os.path.dirname(cible), exist_ok=True)
    shutil.copyfile(source, cible)
    print(f"{nom} : candidat {numero} copié dans {os.path.relpath(cible, RACINE)}")


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ("cout", "generer", "choisir"):
        sys.exit(__doc__)
    commande, noms = sys.argv[1], sys.argv[2:]
    if commande == "choisir":
        if len(noms) != 2:
            sys.exit("Usage : choisir nom numero")
        choisir(noms[0], noms[1])
    elif commande == "cout":
        cout(choisir_assets(charger_assets(), noms))
    else:
        if not noms:
            sys.exit("Indiquer les images à générer (chaque image coûte de l'argent).")
        generer(choisir_assets(charger_assets(), noms))


if __name__ == "__main__":
    main()
