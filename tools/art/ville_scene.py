# La scène d'une ville vue de face, mise dans le jeu : godot/art/towns/<faction>/ (fond.png, scene.json, et par bâtiment son
# image, son ombre et le décor qui passe devant lui), d'après le dossier des bâtiments peints (art-candidats/…/batiments-v5/ pour le Château).
#
# Le dossier source (voir son A-INTEGRER.md) : places.json (coin haut-gauche, taille et profondeur de chaque bâtiment
# dans un rendu de 960 × 540, dont la scène du jeu est la bande des rangées 135 à 404), peints/<id>.png, seul/<id>.png et
# sur-sol/<id>.png (pour couper ce que cache la colline), ombres-contact/<id>.png, decor-devant/<id>.png (le décor que seul
# ce bâtiment et le sol cachent : rendre_devant.py ; sinon visible/decor.png), peints/decor.png, et
# composer_scene.py qui fait le fond (montagnes, sol, ombres du décor, décor).
#
# Dans le jeu, de l'arrière vers l'avant : fond.png, l'ombre de chaque bâtiment bâti (déjà coupée sous le décor, et réglée
# là où elle couvre l'ombre du décor : le même résultat que l'union des ombres de composer_scene.py), les bâtiments du plus
# loin au plus proche, chacun suivi du décor qui passe devant lui (<id>-devant.png ; les bâtiments plus proches le couvrent).
#
# Usage : python -I tools/art/ville_scene.py <dossier source> [faction]   (faction : chateau sans l'option)
import os, sys, json, subprocess
from PIL import Image, ImageChops, ImageFilter

HAUT, HAUTEUR = 135, 270          # la bande du jeu dans le rendu de 960 × 540
OMBRE = (34, 46, 70)              # la couleur des ombres (composer_scene.py)
FORT, FAIBLE = 110, 60            # leurs deux tons


def masque(chemin):
    return Image.open(chemin).getchannel("A").point(lambda v: 255 if v > 128 else 0)


def tons(chemin):
    """L'ombre de contact en deux tons, comme ombres() de composer_scene.py."""
    return Image.open(chemin).getchannel("A").point(lambda v: FORT if v > 120 else (FAIBLE if v > 45 else 0))


def sous_le_sol(src, k, p, b):
    """Efface ce que la colline cache (sous_le_sol() de composer_scene.py)."""
    boite = (p["x"], p["y"], p["x"] + p["largeur"], p["y"] + p["hauteur"])
    seul = masque(os.path.join(src, "seul", k + ".png")).crop(boite)
    vu = masque(os.path.join(src, "sur-sol", k + ".png")).crop(boite)
    cache = ImageChops.subtract(seul, vu).filter(ImageFilter.MaxFilter(5))
    b = b.copy(); b.putalpha(ImageChops.subtract(b.getchannel("A"), cache))
    return b


def bande(im):
    return im.crop((0, HAUT, im.width, HAUT + HAUTEUR))


def sans_miettes(im, part=0.2):
    """Retire les morceaux détachés du bâtiment (8-connexes) plus petits que `part` du plus grand : ce que la coupe sous la
    colline laisse isolé (la porte de la Tour des archers, sous la colline) et les pixels perdus."""
    a = im.getchannel("A"); w, h = im.size; p = a.load(); vu = set(); morceaux = []
    for y in range(h):
        for x in range(w):
            if p[x, y] and (x, y) not in vu:
                pile, morceau = [(x, y)], []; vu.add((x, y))
                while pile:
                    i, j = pile.pop(); morceau.append((i, j))
                    for u in (i - 1, i, i + 1):
                        for v in (j - 1, j, j + 1):
                            if 0 <= u < w and 0 <= v < h and p[u, v] and (u, v) not in vu:
                                vu.add((u, v)); pile.append((u, v))
                morceaux.append(morceau)
    if not morceaux:
        return im
    plus_grand = max(len(m) for m in morceaux)
    for m in morceaux:
        if len(m) < part * plus_grand:
            for xy in m: p[xy] = 0
    im = im.copy(); im.putalpha(a)
    return im


def rogner(im, marge=0):
    """L'image coupée à son dessin (une marge transparente autour, pour le contour du survol) et son coin dans la bande."""
    x0, y0, x1, y1 = im.getbbox()
    x0, y0 = max(0, x0 - marge), max(0, y0 - marge)
    x1, y1 = min(im.width, x1 + marge), min(im.height, y1 + marge)
    return im.crop((x0, y0, x1, y1)), x0, y0


def main():
    src = os.path.abspath(sys.argv[1]); faction = sys.argv[2] if len(sys.argv) > 2 else "chateau"
    racine = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    dest = os.path.join(racine, "godot", "art", "towns", faction)
    os.makedirs(dest, exist_ok=True)
    places = {k: v for k, v in json.load(open(os.path.join(src, "places.json"))).items() if k not in ("sol", "decor")}
    ids = [k for k in places if os.path.exists(os.path.join(src, "peints", k + ".png"))]

    # le fond : composer_scene.py sans aucun bâtiment (montagnes, sol, ombres du décor, décor), ramené à sa vraie taille
    # (sa planche finale échoue faute de bâtiment, après avoir écrit la scène : refaire-scenes.bat l'ignore aussi)
    vide = os.path.join(src, "scene-peinte-x2-jeu-vide.png")
    subprocess.run([sys.executable, "-I", os.path.join(src, "composer_scene.py"), "-jeu-vide"] + ids, stderr=subprocess.DEVNULL)
    if not os.path.exists(vide):
        sys.exit("composer_scene.py n'a pas fait le fond")
    Image.open(vide).resize((960, HAUTEUR), Image.NEAREST).save(os.path.join(dest, "fond.png"))
    os.remove(vide)
    for f in ("scene-peinte-haute-jeu-vide.png", "planche-peints-jeu-vide.png"):
        if os.path.exists(os.path.join(src, f)): os.remove(os.path.join(src, f))

    decor = Image.open(os.path.join(src, "peints", "decor.png")).convert("RGBA")
    sans_decor = decor.getchannel("A").point(lambda v: 255 - v)
    ombre_decor = tons(os.path.join(src, "ombres-contact", "decor.png"))

    def decor_vu(k):
        """Le décor que rien ne cache devant le bâtiment k, les autres bâtiments retirés (rendre_devant.py) : sans eux,
        visible/decor.png manquait ce qu'un autre bâtiment cache dans la scène entière (devant la Tour des griffons)."""
        f = os.path.join(src, "decor-devant", k + ".png")
        return ImageChops.multiply(masque(f if os.path.exists(f) else os.path.join(src, "visible", "decor.png")), decor.getchannel("A"))

    batiments = []
    for k in sorted(ids, key=lambda k: -places[k]["profondeur"]):
        p = places[k]
        b = Image.open(os.path.join(src, "peints", k + ".png")).convert("RGBA").crop((0, 0, p["largeur"], p["hauteur"]))
        toile = Image.new("RGBA", decor.size); toile.alpha_composite(sans_miettes(sous_le_sol(src, k, p, b)), (p["x"], p["y"]))
        im, x, y = rogner(bande(toile), marge=1)
        im.save(os.path.join(dest, k + ".png"))
        entree = {"id": k, "x": x, "y": y, "depth": p["profondeur"]}
        # l'ombre : rien sous le décor (il la couvre) ; sur l'ombre du décor, seulement de quoi passer au ton fort
        f = os.path.join(src, "ombres-contact", k + ".png")
        if os.path.exists(f):
            t, td = tons(f).load(), ombre_decor.load()
            a = Image.new("L", decor.size, 0); ap = a.load()
            for yy in range(decor.height):
                for xx in range(decor.width):
                    v, d = t[xx, yy], td[xx, yy]
                    if v > d:
                        ap[xx, yy] = v if d == 0 else round(255 * (1 - (255 - v) / (255 - d)))
            a = ImageChops.multiply(a, sans_decor)
            ombre = Image.new("RGBA", decor.size, OMBRE + (0,)); ombre.putalpha(a)
            ombre = bande(ombre)
            if ombre.getbbox():
                im, sx, sy = rogner(ombre)
                im.save(os.path.join(dest, k + "-ombre.png"))
                entree.update({"shadowX": sx, "shadowY": sy})
        # le décor qui passe devant ce bâtiment (en 3D), redessiné juste après lui quand il est bâti
        devant = Image.new("RGBA", decor.size)
        devant.paste(decor, (0, 0), ImageChops.multiply(masque(os.path.join(src, "seul", k + ".png")), decor_vu(k)))
        devant = bande(devant)
        if devant.getbbox():
            im, fx, fy = rogner(devant)
            im.save(os.path.join(dest, k + "-devant.png"))
            entree.update({"frontX": fx, "frontY": fy})
        batiments.append(entree)

    scene = {"width": 960, "height": HAUTEUR, "buildings": batiments}
    with open(os.path.join(dest, "scene.json"), "w", encoding="utf-8") as f:
        f.write("{\n  \"width\": 960, \"height\": %d,\n  \"buildings\": [\n" % HAUTEUR)
        f.write(",\n".join("    " + json.dumps(b, ensure_ascii=False) for b in batiments))
        f.write("\n  ]\n}\n")
    print(f"{len(batiments)} bâtiments dans {dest}")


if __name__ == "__main__":
    main()
