"""Génère les images du jeu avec l'API de Retro Diffusion (pixel art).

Les images à produire sont décrites dans assets.json (à côté de ce script). La clé d'API est lue dans
la variable d'environnement RETRO_DIFFUSION_API_KEY (jamais écrite dans un fichier du projet).

    python -I tools/art/generer.py cout [noms...]       prix estimé, gratuit (rien n'est généré)
    python -I tools/art/generer.py generer noms...      génère les candidats dans art-candidats/<nom>/
    python -I tools/art/generer.py choisir nom numero    copie le candidat choisi dans godot/art/<nom>.png

Sans nom, « cout » estime toutes les images. Un nom peut être un préfixe (« terrain/ »).
Un nom avec « @ » (« terrain/grass@48 ») est un essai : « choisir » le copie à la place de terrain/grass.
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
CANDIDATS = os.path.join(RACINE, "art-candidats")
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
    source = os.path.join(CANDIDATS, *nom.split("/"), f"{numero}.png")
    if not os.path.exists(source):
        sys.exit(f"Candidat introuvable : {source}")
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
