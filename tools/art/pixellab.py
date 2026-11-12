"""Anime une image existante avec l'API de PixelLab (« Animate with text v3 »).

La clé d'API est lue dans la variable d'environnement PIXELLAB_API_KEY (jamais écrite dans un fichier du projet).

    python -I tools/art/pixellab.py solde
        le crédit restant (gratuit)
    python -I tools/art/pixellab.py animer image.png dossier "action" [images] [graine]
        anime image.png (« walking forward », « attacking with a sword »…) : 4 à 16 images (8 par défaut),
        fond transparent ; écrit dossier/1.png, 2.png… et dossier/feuille.png (4 colonnes) ; coûte de l'argent
        Options (après les autres) : --retour (l'animation finit sur la pose de départ, pour une attaque),
        --minimax (modèle « PixMiniMax », vue de côté vers la droite), --sujet="…" (ce que montre l'image, pour
        PixMiniMax), --enrichir (PixelLab détaille lui-même le mouvement décrit)
    python -I tools/art/pixellab.py ui sortie.png largeur hauteur "description" [graine] [modele.png]
        un élément d'interface (bouton, cadre, case…) de 16 px à 512 px, fond transparent ; <modele.png> guide
        le dessin (« concept image ») ; coûte de l'argent
    python -I tools/art/pixellab.py panneau sortie.png largeur hauteur "description" [graine] [style.png]
        un cadre de fenêtre (outil « Create UI panel », de 192 à 688 px) ; <style.png> donne le style ; coûte de l'argent
    python -I tools/art/pixellab.py creer sortie.png largeur hauteur "description" [graine] [style.png] [force]
        une image de créature (outil « Bitforge », 16 à 200 px) vue de côté, tournée vers la droite, fond transparent ;
        <style.png> (une de nos images) donne le style, avec la force <force> de 0 à 100 (50 par défaut) ;
        coûte de l'argent (environ 0,01 $)
    python -I tools/art/pixellab.py pro dossier largeur hauteur "description" [graine] [style.png] [modele.jpg…]
        plusieurs images (modèle « Pro ») d'un coup : 25 pour 42 à 50 px, 16 pour 51 à 64 px, 4 pour 86 à 170 px…
        écrites dans dossier/1.png, 2.png… et dossier/planche.png ; <style.png> donne le trait, l'ombrage et le détail
        (pas les couleurs ; « - » pour aucun) ; les <modele> (4 au plus, PNG ou JPEG) montrent le sujet, pas le style ;
        coûte de l'argent ; option --fond (après les autres) : garder le fond (une petite peinture qui remplit l'image)
    python -I tools/art/pixellab.py retoucher image.png dossier "changement" [graine]
        modifie une image d'après un texte (« Edit images », Pro), en gardant sa forme et sa pose ; coûte de l'argent ;
        option --fond : garder le fond (sinon ce que PixelLab prend pour le fond devient transparent)
    python -I tools/art/pixellab.py repeindre image.png sortie.png x y largeur hauteur "description" [graine]
        repeint seulement le rectangle donné (en pixels de l'image), le reste ne change pas (« Inpaint », Pro) ;
        coûte de l'argent

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
    # PixelLab n'accepte que 8 tâches à la fois : au-delà (erreur 429), on attend qu'une place se libère
    for essai in range(60):
        req = urllib.request.Request(API + chemin, data=donnees, headers=entetes, method=methode)
        try:
            with urllib.request.urlopen(req, timeout=120) as reponse:
                return json.loads(reponse.read().decode("utf-8"))
        except urllib.error.HTTPError as e:
            texte = e.read().decode('utf-8', 'replace')
            if e.code == 429 and "concurrent" in texte:
                if essai == 0:
                    print("PixelLab : 8 tâches en cours, celle-ci attend son tour…", flush=True)
                time.sleep(15)
                continue
            sys.exit(f"Erreur {e.code} de l'API : {texte[:1000]}")
    sys.exit("PixelLab : toujours 8 tâches en cours après 15 minutes d'attente.")


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


def animer(image, dossier, action, images=8, graine=0, retour=False, minimax=False, sujet=None, enrichir=False):
    depart = {"type": "base64", "base64": base64.b64encode(open(image, "rb").read()).decode("ascii"), "format": "png"}
    corps = {"first_frame": depart, "frame_count": images, "no_background": True, "seed": graine}
    if retour:
        corps["last_frame"] = depart
    if enrichir:
        corps["enhance_prompt"] = True
    if minimax:
        corps.update({"description": action, "view": "side", "direction": "east"})
        if sujet:
            corps["subject_description"] = sujet
        r = requete("POST", "/animate-pixminimax", corps)
    else:
        corps["action"] = action
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


def taille_image(chemin):
    """Largeur et hauteur d'une image PNG ou JPEG."""
    d = open(chemin, "rb").read()
    if d[1:4] == b"PNG":
        return struct.unpack(">II", d[16:24])
    i = 2
    while i < len(d):
        marque, longueur = d[i + 1], struct.unpack(">H", d[i + 2:i + 4])[0]
        if 0xC0 <= marque <= 0xCF and marque not in (0xC4, 0xC8, 0xCC):
            h, w = struct.unpack(">HH", d[i + 5:i + 9])
            return w, h
        i += 2 + longueur
    sys.exit(f"Taille de {chemin} introuvable")


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


def creer(sortie, largeur, hauteur, description, graine=None, style=None, force=50):
    corps = {
        "description": description,
        "negative_description": "background, ground, shadow, text, frame, blurry",
        "image_size": {"width": largeur, "height": hauteur},
        "view": "side",
        "direction": "east",
        "outline": "single color black outline",
        "shading": "detailed shading",
        "detail": "highly detailed",
        "no_background": True,
    }
    if graine is not None:
        corps["seed"] = graine
    if style:
        corps["style_image"] = image_base64(style)
        corps["style_strength"] = force
    r = requete("POST", "/create-image-bitforge", corps)
    if "background_job_id" in r:
        r = attendre(r["background_job_id"]).get("last_response", r)
    os.makedirs(os.path.dirname(os.path.abspath(sortie)), exist_ok=True)
    with open(sortie, "wb") as f:
        f.write(decoder(r["image"]))
    cout = r.get("usage") or {}
    print(f"{sortie} : {largeur}×{hauteur} (coût : {cout.get('usd')} $)")


def pro(dossier, largeur, hauteur, description, graine=None, style=None, modeles=(), fond=False):
    # fond : garder le fond de l'image (une icône de sort peinte jusqu'aux bords) ; sinon, fond transparent
    corps = {"description": description, "image_size": {"width": largeur, "height": hauteur}, "no_background": not fond}
    if graine is not None:
        corps["seed"] = graine
    if modeles:
        corps["reference_images"] = []
        for m in modeles:
            # « chemin::usage » : dit à PixelLab à quoi sert l'image (par exemple l'angle de vue seulement)
            m, _, usage = m.partition("::")
            w, h = taille_image(m)
            image = image_base64(m)
            image["format"] = "jpeg" if m.lower().endswith((".jpg", ".jpeg")) else "png"
            corps["reference_images"].append({"image": image, "size": {"width": w, "height": h},
                                              "usage_description": usage or "subject reference only (anatomy, costume), not the style"})
    if style and style != "-":
        w, h = taille_png(style)
        corps["style_image"] = {"image": image_base64(style), "size": {"width": w, "height": h}}
        corps["style_options"] = {"color_palette": False, "outline": True, "detail": True, "shading": True}
    r = requete("POST", "/generate-image-v2", corps)
    etat = attendre(r["background_job_id"])
    os.makedirs(dossier, exist_ok=True)
    chemins = []
    for i, img in enumerate(etat["last_response"]["images"], start=1):
        chemin = os.path.join(dossier, f"{i}.png")
        with open(chemin, "wb") as f:
            f.write(decoder(img))
        chemins.append(chemin)
    feuille(chemins, os.path.join(dossier, "planche.png"), 4)
    cout = etat.get("usage") or r.get("usage") or {}
    print(f"{dossier} : {len(chemins)} images (coût : {cout.get('usd')} $)")


def ecrire_images(dossier, images):
    os.makedirs(dossier, exist_ok=True)
    chemins = []
    for i, img in enumerate(images, start=1):
        chemin = os.path.join(dossier, f"{i}.png")
        with open(chemin, "wb") as f:
            f.write(decoder(img))
        chemins.append(chemin)
    return chemins


def retoucher(image, dossier, changement, graine=None, fond=False):
    # fond : garder le fond (sinon PixelLab rend transparent ce qu'il prend pour le fond, ainsi les pages d'un livre)
    w, h = taille_png(image)
    corps = {"method": "edit_with_text", "description": changement, "no_background": not fond,
             "edit_images": [{"image": image_base64(image), "width": w, "height": h}],
             "image_size": {"width": max(w, 32), "height": max(h, 32)}}
    if graine is not None:
        corps["seed"] = graine
    r = requete("POST", "/edit-images-v2", corps)
    etat = attendre(r["background_job_id"])
    chemins = ecrire_images(dossier, etat["last_response"]["images"])
    cout = etat.get("usage") or r.get("usage") or {}
    print(f"{dossier} : {len(chemins)} image(s) (coût : {cout.get('usd')} $)")


def repeindre(image, sortie, x, y, largeur, hauteur, description, graine=None):
    """Le masque est blanc dans le rectangle (à repeindre), noir ailleurs (gardé)."""
    from reduire import ecrire
    w, h = taille_png(image)
    masque = [[(255, 255, 255, 255) if x <= i < x + largeur and y <= j < y + hauteur else (0, 0, 0, 255)
               for i in range(w)] for j in range(h)]
    chemin_masque = os.path.splitext(sortie)[0] + "-masque.png"
    os.makedirs(os.path.dirname(os.path.abspath(sortie)), exist_ok=True)
    ecrire(chemin_masque, masque)
    taille = {"width": w, "height": h}
    corps = {"description": description, "no_background": True,
             "inpainting_image": {"image": image_base64(image), "size": taille},
             "mask_image": {"image": image_base64(chemin_masque), "size": taille}}
    if graine is not None:
        corps["seed"] = graine
    r = requete("POST", "/inpaint-v3", corps)
    etat = attendre(r["background_job_id"])
    reponse = etat["last_response"]
    images = reponse.get("images") or [reponse.get("image")]
    base, ext = os.path.splitext(sortie)
    for i, img in enumerate(images, start=1):
        with open(sortie if len(images) == 1 else f"{base}-{i}{ext}", "wb") as f:
            f.write(decoder(img))
    cout = etat.get("usage") or r.get("usage") or {}
    print(f"{sortie} : {len(images)} image(s) (coût : {cout.get('usd')} $)")


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ("solde", "animer", "ui", "panneau", "creer", "pro", "retoucher", "repeindre"):
        sys.exit(__doc__)
    a = sys.argv
    if a[1] == "solde":
        solde()
    elif a[1] == "retoucher":
        fond = "--fond" in a
        a = [x for x in a if x != "--fond"]
        if len(a) < 5:
            sys.exit(__doc__)
        retoucher(a[2], a[3], a[4], int(a[5]) if len(a) > 5 else None, fond)
    elif a[1] == "repeindre":
        if len(a) < 9:
            sys.exit(__doc__)
        repeindre(a[2], a[3], int(a[4]), int(a[5]), int(a[6]), int(a[7]), a[8], int(a[9]) if len(a) > 9 else None)
    elif sys.argv[1] == "creer":
        a = sys.argv
        if len(a) < 6:
            sys.exit(__doc__)
        creer(a[2], int(a[3]), int(a[4]), a[5], int(a[6]) if len(a) > 6 else None,
              a[7] if len(a) > 7 else None, int(a[8]) if len(a) > 8 else 50)
    elif sys.argv[1] == "pro":
        fond = "--fond" in sys.argv
        a = [x for x in sys.argv if x != "--fond"]
        if len(a) < 6:
            sys.exit(__doc__)
        pro(a[2], int(a[3]), int(a[4]), a[5], int(a[6]) if len(a) > 6 else None, a[7] if len(a) > 7 else None, a[8:], fond)
    elif sys.argv[1] in ("ui", "panneau"):
        if len(sys.argv) < 6:
            sys.exit(__doc__)
        a = sys.argv
        (ui if a[1] == "ui" else panneau)(a[2], int(a[3]), int(a[4]), a[5],
                                          int(a[6]) if len(a) > 6 else None, a[7] if len(a) > 7 else None)
    else:
        options = [x for x in sys.argv[2:] if x.startswith("--")]
        a = [x for x in sys.argv[2:] if not x.startswith("--")]
        if len(a) < 3:
            sys.exit(__doc__)
        sujet = next((x.split("=", 1)[1] for x in options if x.startswith("--sujet=")), None)
        animer(a[0], a[1], a[2], int(a[3]) if len(a) > 3 else 8, int(a[4]) if len(a) > 4 else 0,
               "--retour" in options, "--minimax" in options, sujet, "--enrichir" in options)


if __name__ == "__main__":
    main()
