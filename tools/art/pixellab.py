"""Anime une image existante avec l'API de PixelLab (« Animate with text v3 »).

La clé d'API est lue dans la variable d'environnement PIXELLAB_API_KEY (jamais écrite dans un fichier du projet).

    python -I tools/art/pixellab.py solde
        le crédit restant (gratuit)
    python -I tools/art/pixellab.py animer image.png dossier "action" [images] [graine]
        anime image.png (« walking forward », « attacking with a sword »…) : 4 à 16 images (8 par défaut),
        fond transparent ; écrit dossier/1.png, 2.png… et dossier/feuille.png (4 colonnes) ; coûte de l'argent
    python -I tools/art/pixellab.py ui sortie.png largeur hauteur "description" [graine] [modele.png]
        un élément d'interface (bouton, cadre, case…) de 16 px à 512 px, fond transparent ; <modele.png> guide
        le dessin (« concept image ») ; coûte de l'argent
    python -I tools/art/pixellab.py panneau sortie.png largeur hauteur "description" [graine] [style.png]
        un cadre de fenêtre (outil « Create UI panel », de 192 à 688 px) ; <style.png> donne le style ; coûte de l'argent

La première image rendue est l'image d'entrée elle-même. Seule la bibliothèque standard de Python est utilisée.
"""

import base64
import json
import os
import struct
import sys
import time
import urllib.error
import urllib.request
import zlib

API = "https://api.pixellab.ai/v2"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


def requete(methode, chemin, corps=None):
    cle = os.environ.get("PIXELLAB_API_KEY")
    if not cle:
        sys.exit("Variable d'environnement PIXELLAB_API_KEY absente.")
    entetes = {"Authorization": f"Bearer {cle}", "Content-Type": "application/json"}
    donnees = json.dumps(corps).encode("utf-8") if corps is not None else None
    req = urllib.request.Request(API + chemin, data=donnees, headers=entetes, method=methode)
    try:
        with urllib.request.urlopen(req, timeout=120) as reponse:
            return json.loads(reponse.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        sys.exit(f"Erreur {e.code} de l'API : {e.read().decode('utf-8', 'replace')[:1000]}")


def solde():
    r = requete("GET", "/balance")
    abonnement = r.get("subscription", {})
    print(f"Crédit : {r.get('credits', {}).get('usd')} $ ; abonnement : {abonnement.get('status')} "
          f"({abonnement.get('plan')}), générations restantes : {abonnement.get('generations')}")


def decoder(image):
    """Une image de la réponse : base64 brut ou « data:image/png;base64,… »."""
    texte = image.get("base64", "") if isinstance(image, dict) else image
    if texte.startswith("data:"):
        texte = texte.split(",", 1)[1]
    return base64.b64decode(texte)


def feuille(chemins, sortie, colonnes=4):
    """Assemble les images (même taille) en une feuille : <colonnes> par rangée, de gauche à droite."""
    from reduire import ecrire, lire
    images = [lire(c)[2] for c in chemins]
    h, w = len(images[0]), len(images[0][0])
    rangees = (len(images) + colonnes - 1) // colonnes
    px = [[(0, 0, 0, 0)] * (w * colonnes) for _ in range(h * rangees)]
    for i, img in enumerate(images):
        ox, oy = i % colonnes * w, i // colonnes * h
        for y in range(h):
            px[oy + y][ox:ox + w] = img[y]
    ecrire(sortie, px)


def animer(image, dossier, action, images=8, graine=0):
    corps = {
        "first_frame": {"type": "base64", "base64": base64.b64encode(open(image, "rb").read()).decode("ascii"), "format": "png"},
        "action": action,
        "frame_count": images,
        "no_background": True,
        "seed": graine,
    }
    r = requete("POST", "/animate-with-text-v3", corps)
    tache = r["background_job_id"]
    while True:
        etat = requete("GET", f"/background-jobs/{tache}")
        if etat["status"] == "completed":
            break
        if etat["status"] == "failed":
            sys.exit(f"Échec de l'animation : {json.dumps(etat.get('last_response'))[:500]}")
        time.sleep(4)
    os.makedirs(dossier, exist_ok=True)
    chemins = []
    for i, img in enumerate(etat["last_response"]["images"], start=1):
        chemin = os.path.join(dossier, f"{i}.png")
        with open(chemin, "wb") as f:
            f.write(decoder(img))
        chemins.append(chemin)
    feuille(chemins, os.path.join(dossier, "feuille.png"))
    cout = etat.get("usage") or r.get("usage") or {}
    print(f"{action} : {len(chemins)} images dans {dossier} (coût : {cout.get('usd')} $ / {cout.get('generations')} générations)")


def image_base64(chemin):
    return {"type": "base64", "base64": base64.b64encode(open(chemin, "rb").read()).decode("ascii"), "format": "png"}


def taille_png(chemin):
    d = open(chemin, "rb").read(24)
    return struct.unpack(">II", d[16:24])


def attendre(tache):
    while True:
        etat = requete("GET", f"/background-jobs/{tache}")
        if etat["status"] == "completed":
            return etat
        if etat["status"] == "failed":
            sys.exit(f"Échec : {json.dumps(etat.get('last_response'))[:500]}")
        time.sleep(5)


def ui(sortie, largeur, hauteur, description, graine=None, modele=None):
    corps = {"description": description, "image_size": {"width": largeur, "height": hauteur}, "no_background": True}
    if graine is not None:
        corps["seed"] = graine
    if modele:
        w, h = taille_png(modele)
        corps["concept_image"] = {"image": image_base64(modele), "size": {"width": w, "height": h}}
    r = requete("POST", "/generate-ui-v2", corps)
    etat = attendre(r["background_job_id"])
    images = etat["last_response"]["images"]
    os.makedirs(os.path.dirname(os.path.abspath(sortie)), exist_ok=True)
    base, ext = os.path.splitext(sortie)
    for i, img in enumerate(images, start=1):
        with open(sortie if len(images) == 1 else f"{base}-{i}{ext}", "wb") as f:
            f.write(decoder(img))
    cout = etat.get("usage") or r.get("usage") or {}
    print(f"{sortie} : {len(images)} image(s) (coût : {cout.get('usd')} $)")


def telecharger(url, sortie):
    """Télécharge une image du serveur de PixelLab (il refuse les demandes sans nom de navigateur)."""
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (HoMM, outils d'images)"})
    with urllib.request.urlopen(req, timeout=120) as reponse, open(sortie, "wb") as f:
        f.write(reponse.read())


def panneau(sortie, largeur, hauteur, description, graine=None, style=None):
    corps = {"description": description, "image_size": {"width": largeur, "height": hauteur}, "no_background": True}
    if graine is not None:
        corps["seed"] = graine
    if style:
        corps["style_image"] = image_base64(style)
    r = requete("POST", "/create-ui-asset", corps)
    while True:
        etat = requete("GET", f"/ui-assets/{r['ui_asset_id']}")
        if etat.get("status") == "completed" and etat.get("image_url"):
            break
        if etat.get("status") == "failed":
            sys.exit(f"Échec du cadre : {json.dumps(etat)[:500]}")
        time.sleep(5)
    os.makedirs(os.path.dirname(os.path.abspath(sortie)), exist_ok=True)
    telecharger(etat["image_url"], sortie)
    cout = r.get("usage") or {}
    print(f"{sortie} : cadre {largeur}×{hauteur} (coût : {cout.get('usd')} $)")


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ("solde", "animer", "ui", "panneau"):
        sys.exit(__doc__)
    if sys.argv[1] == "solde":
        solde()
    elif sys.argv[1] in ("ui", "panneau"):
        if len(sys.argv) < 6:
            sys.exit(__doc__)
        a = sys.argv
        (ui if a[1] == "ui" else panneau)(a[2], int(a[3]), int(a[4]), a[5],
                                          int(a[6]) if len(a) > 6 else None, a[7] if len(a) > 7 else None)
    else:
        if len(sys.argv) < 5:
            sys.exit(__doc__)
        animer(sys.argv[2], sys.argv[3], sys.argv[4],
               int(sys.argv[5]) if len(sys.argv) > 5 else 8, int(sys.argv[6]) if len(sys.argv) > 6 else 0)


if __name__ == "__main__":
    main()
