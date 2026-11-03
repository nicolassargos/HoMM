"""Page de choix des images : art-candidats/apercu.html montre chaque candidat agrandi (pixels nets).

    python -I tools/art/apercu.py

Les images d'écran de jeu éventuellement posées dans art-candidats/ (jeu-*.png) sont montrées en haut.
Chaque candidat est aussi montré à la taille du jeu (×2) sur l'herbe installée, et le candidat déjà
installé dans godot/art est marqué « choisi ».
"""

import glob
import html
import json
import os

ART = None  # dossier godot/art, fixé dans main()
HERBE = "../godot/art/terrain/grass.png"


def scene(asset, chemin, taille):
    """Le candidat comme dans le jeu : cases de 48 px, image ×2, sur l'herbe installée."""
    fond = f"background-image:url({HERBE})" if os.path.exists(os.path.join(ART, "terrain", "grass.png")) else "background:#55703f"
    if asset["name"].startswith("terrain/"):
        # un carré de 3×3 cases du terrain au milieu de l'herbe
        return (f"<div class='scene' style='width:240px;height:240px;{fond}'>"
                f"<div style='left:48px;top:48px;width:144px;height:144px;background:url(\"{chemin}\");background-size:48px 48px'></div></div>")
    cote = 240 if asset["name"] == "objects/town" else 144
    t = taille * 2
    # posée en bas de la case du milieu (la ville : sur les 3×3 cases du milieu)
    bas = 192 if cote == 240 else 96
    return (f"<div class='scene' style='width:{cote}px;height:{cote}px;{fond}'>"
            f"<img src='{chemin}' width='{t}' height='{t}' style='left:{(cote - t) // 2}px;top:{bas - t}px'></div>")


def installe(asset):
    chemin = os.path.join(ART, *asset["name"].split("/")) + ".png"
    if not os.path.exists(chemin):
        return None
    with open(chemin, "rb") as f:
        return f.read()

ICI = os.path.dirname(os.path.abspath(__file__))
RACINE = os.path.normpath(os.path.join(ICI, "..", ".."))
CANDIDATS = os.path.join(RACINE, "art-candidats")

STYLE = """
body { background:#1e1a14; color:#f0e4c8; font-family:sans-serif; margin:24px; }
h2 { margin:28px 0 8px; font-size:17px; } p { color:#b4a88c; }
.row { display:flex; gap:20px; flex-wrap:wrap; align-items:flex-end; }
figure { margin:0; text-align:center; } figcaption { font-size:13px; color:#b4a88c; margin-top:4px; }
.px { image-rendering:pixelated; background:#55703f; border:1px solid #3a3020; }
.scene { position:relative; image-rendering:pixelated; background-size:48px 48px; border:1px solid #3a3020; }
.scene img, .scene div { position:absolute; image-rendering:pixelated; }
.choisi { outline:3px solid #e8c45a; outline-offset:3px; } .choisi figcaption { color:#e8c45a; font-weight:bold; }
.shot { width:900px; max-width:100%; border:2px solid #be9f5a; }
"""


def main():
    global ART
    ART = os.path.join(RACINE, "godot", "art")
    with open(os.path.join(ICI, "assets.json"), encoding="utf-8") as f:
        assets = json.load(f)["assets"]
    parties = [f"<!doctype html><html lang='fr'><head><meta charset='utf-8'><title>Choix des images</title>"
               f"<style>{STYLE}</style></head><body><h1>Choix des images</h1>"
               "<p>Chaque candidat est agrandi ×4, puis montré à la taille du jeu (×2) sur l'herbe. Le candidat déjà installé est encadré en jaune.</p>"]
    ecrans = sorted(glob.glob(os.path.join(CANDIDATS, "jeu-*.png")))
    if ecrans:
        parties.append("<h2>Dans le jeu</h2><div class='row'>")
        for ecran in ecrans:
            nom = os.path.basename(ecran)
            parties.append(f"<figure><img class='shot' src='{nom}'><figcaption>{html.escape(nom)}</figcaption></figure>")
        parties.append("</div>")
    for asset in assets:
        dossier = os.path.join(CANDIDATS, *asset["name"].split("/"))
        images = sorted(glob.glob(os.path.join(dossier, "*.png")), key=lambda p: int(os.path.splitext(os.path.basename(p))[0]))
        if not images:
            continue
        taille = asset["size"] if isinstance(asset["size"], int) else asset["size"][0]
        parties.append(f"<h2>{html.escape(asset['name'])}</h2><p>{html.escape(asset['prompt'])}</p><div class='row'>")
        actuel = installe(asset)
        for image in images:
            chemin = os.path.relpath(image, CANDIDATS).replace(os.sep, "/")
            numero = os.path.splitext(os.path.basename(image))[0]
            with open(image, "rb") as f:
                choisi = actuel is not None and f.read() == actuel
            parties.append(f"<figure{' class=choisi' if choisi else ''}><img class='px' src='{chemin}' width='{taille * 4}' height='{taille * 4}'>"
                           f"<figcaption>n°{numero}{' (choisi)' if choisi else ''}</figcaption></figure>")
        parties.append("</div><div class='row' style='margin-top:10px'>")
        for image in images:
            chemin = os.path.relpath(image, CANDIDATS).replace(os.sep, "/")
            numero = os.path.splitext(os.path.basename(image))[0]
            parties.append(f"<figure>{scene(asset, chemin, taille)}<figcaption>n°{numero} dans le jeu</figcaption></figure>")
        parties.append("</div>")
    parties.append("</body></html>")
    with open(os.path.join(CANDIDATS, "apercu.html"), "w", encoding="utf-8") as f:
        f.write("\n".join(parties))
    print(os.path.join(CANDIDATS, "apercu.html"))


if __name__ == "__main__":
    main()
