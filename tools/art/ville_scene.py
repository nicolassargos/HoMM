# La scène d'une ville vue de face, mise dans le jeu : godot/art/towns/<faction>/ (fond.png, scene.json, et par bâtiment son
# image, son ombre et le décor qui passe devant lui), d'après le dossier des bâtiments peints (art-candidats/…/batiments-v5/ pour le Château).
#
# Le dossier source (voir son A-INTEGRER.md) : places.json (coin haut-gauche, taille et profondeur de chaque bâtiment
# dans un rendu de 960 × 540, dont la scène du jeu est la bande des rangées 135 à 404), peints/<id>.png, seul/<id>.png et
# sur-sol/<id>.png (pour couper ce que cache la colline), ombres-contact/<id>.png, ajouts/<id>.png (peint en plus sur le sol
# visible, s'il y en a), lueurs/<id>.json et lueurs/runes/ (des runes
# animées par le jeu, autour de la guilde des mages : runes_guilde.py), decor-devant/<id>.png (le décor que seul
# ce bâtiment et le sol cachent : rendre_devant.py ; sinon visible/decor.png), peints/decor.png, demeure.png (la
# demeure du héros, avant le fort, peinte dans la scène vide), et composer_scene.py qui fait le fond (montagnes, sol, ombres du décor, décor).
#
# Dans le jeu, de l'arrière vers l'avant : fond.png, l'ombre de chaque bâtiment bâti (déjà coupée sous le décor, et réglée
# là où elle couvre l'ombre du décor : le même résultat que l'union des ombres de composer_scene.py), les bâtiments du plus
# loin au plus proche, chacun suivi du décor qui passe devant lui (<id>-devant.png ; les bâtiments plus proches le couvrent).
#
# Usage : python -I tools/art/ville_scene.py <dossier source> [faction]   (faction : chateau sans l'option)
import os, sys, json, subprocess, glob, shutil
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


# les étapes d'un bâtiment peintes à la même place, sans maquette à elles (peints/<id>.png, à la taille de la première) :
# la place, les masques, l'ombre et le décor de devant sont ceux de la première
MEME_PLACE = {"hotel-ville": "salle-village", "grand-hotel-ville": "salle-village", "capitole": "salle-village",
              "guilde-mages-2": "guilde-mages-1", "guilde-mages-3": "guilde-mages-1", "guilde-mages-4": "guilde-mages-1"}

# les bâtiments dont le pied coupé par la colline finit en touffes (sur d'autres, elles mangeaient des détails peints :
# les bouteilles sous l'étal du marché)
TOUFFES = {"tour-griffons"}


def touffes(src, k, toile, fond):
    """Le pied d'un bâtiment coupé par la colline, en touffes d'herbe plutôt qu'en ligne droite : la colline de la
    maquette est lisse, l'herbe n'est que peinte. Au-dessus de la coupe, l'herbe du fond se montre sur 0 à 5 pixels, en
    brins inégaux, là où le sol sous la coupe est vert (pas sur la route). `toile` et `fond` : le rendu de 960 × 540."""
    import random
    cache = ImageChops.subtract(masque(os.path.join(src, "seul", k + ".png")),
                                masque(os.path.join(src, "sur-sol", k + ".png"))).filter(ImageFilter.MaxFilter(5))
    a = toile.getchannel("A"); ap, cp, fp = a.load(), cache.load(), fond.load()
    hasard = random.Random(k)
    for x in range(toile.width):
        brin = hasard.choice([0, 1, 1, 2, 2, 3, 3, 4, 5])
        for y in range(toile.height - 1):
            if not ap[x, y] or not cp[x, y + 1]:
                continue
            r, g, b, _ = fp[x, y + 1]
            if g > r and g > b:
                for h in range(brin):
                    ap[x, y - h] = 0
    toile.putalpha(a)


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
    for k, premier in MEME_PLACE.items():
        if premier in places:
            places[k] = places[premier]
    ids = [k for k in places if os.path.exists(os.path.join(src, "peints", k + ".png"))]

    # le fond : composer_scene.py sans aucun bâtiment (montagnes, sol, ombres du décor, décor), ramené à sa vraie taille
    # (sa planche finale échoue faute de bâtiment, après avoir écrit la scène : refaire-scenes.bat l'ignore aussi)
    vide = os.path.join(src, "scene-peinte-x2-jeu-vide.png")
    subprocess.run([sys.executable, "-I", os.path.join(src, "composer_scene.py"), "-jeu-vide"] + ids, stderr=subprocess.DEVNULL)
    if not os.path.exists(vide):
        sys.exit("composer_scene.py n'a pas fait le fond")
    fond = Image.open(vide).convert("RGBA").resize((960, HAUTEUR), Image.NEAREST)
    fond.save(os.path.join(dest, "fond.png"))
    fond_haut = Image.new("RGBA", (960, 540)); fond_haut.paste(fond, (0, HAUT))
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
        vu = masque(f if os.path.exists(f) else os.path.join(src, "visible", "decor.png"))
        # bouché : la maquette d'un bâtiment perce parfois une maison du décor (la Tour des griffons dans son toit)
        vu = vu.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
        return ImageChops.multiply(vu, decor.getchannel("A"))

    for f in glob.glob(os.path.join(src, "lueurs", "runes", "*.png")):
        shutil.copy(f, os.path.join(dest, os.path.basename(f)))
    batiments = []
    for k in sorted(ids, key=lambda k: -places[k]["profondeur"]):
        p, m = places[k], MEME_PLACE.get(k, k)  # m : le nom de ses masques
        b = Image.open(os.path.join(src, "peints", k + ".png")).convert("RGBA").crop((0, 0, p["largeur"], p["hauteur"]))
        toile = Image.new("RGBA", decor.size); toile.alpha_composite(sans_miettes(sous_le_sol(src, m, p, b)), (p["x"], p["y"]))
        if k in TOUFFES:
            touffes(src, k, toile, fond_haut)
        # ce qui a été peint en plus dans la scène, sur le sol visible (ajouts/<id>.png, 960 × 540) : la barrière et le
        # petit champ devant la ferme, cachés par la colline dans la maquette ; pas coupé, montré avec le bâtiment
        ajout = os.path.join(src, "ajouts", k + ".png")
        if os.path.exists(ajout):
            toile.alpha_composite(Image.open(ajout).convert("RGBA"))
        im, x, y = rogner(bande(toile), marge=1)
        im.save(os.path.join(dest, k + ".png"))
        entree = {"id": k, "x": x, "y": y, "depth": p["profondeur"]}
        # l'ombre : rien sous le décor (il la couvre) ; sur l'ombre du décor, seulement de quoi passer au ton fort
        f = os.path.join(src, "ombres-contact", m + ".png")
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
        # plus, à 3 pixels au plus, là où le bâtiment peint déborde de sa maquette (le bord de la Tour des griffons sur le
        # toit de la maison) : seulement sur ce débord, pour ne pas montrer le décor caché derrière un autre bâtiment
        seul = masque(os.path.join(src, "seul", m + ".png"))
        vu = ImageChops.multiply(seul, decor_vu(m))
        deborde = ImageChops.subtract(toile.getchannel("A").point(lambda v: 255 if v else 0), seul)
        vu = ImageChops.lighter(vu, ImageChops.multiply(vu.filter(ImageFilter.MaxFilter(7)), deborde))
        devant.paste(decor, (0, 0), ImageChops.multiply(vu, decor.getchannel("A")))
        devant = bande(devant)
        if devant.getbbox():
            im, fx, fy = rogner(devant)
            im.save(os.path.join(dest, k + "-devant.png"))
            entree.update({"frontX": fx, "frontY": fy})
        # des runes animées par le jeu (lueurs/<id>.json : [x, y, n] dans le rendu de 960 × 540, l'image rune-<n>.png)
        lueur = os.path.join(src, "lueurs", k + ".json")
        if os.path.exists(lueur):
            entree["runes"] = [[x, y - HAUT, n] for x, y, n in json.load(open(lueur))]
        batiments.append(entree)

    # la demeure du héros, avant le fort (demeure.png, 960 × 540 : peinte dans la scène vide), à la profondeur du fort
    demeure = None
    if os.path.exists(os.path.join(src, "demeure.png")):
        im, x, y = rogner(bande(Image.open(os.path.join(src, "demeure.png")).convert("RGBA")), marge=1)
        im.save(os.path.join(dest, "demeure.png"))
        demeure = {"id": "demeure", "x": x, "y": y, "depth": places["fort"]["profondeur"]}
    with open(os.path.join(dest, "scene.json"), "w", encoding="utf-8") as f:
        f.write("{\n  \"width\": 960, \"height\": %d,\n" % HAUTEUR)
        if demeure:
            f.write("  \"home\": %s,\n" % json.dumps(demeure))
        f.write("  \"buildings\": [\n")
        f.write(",\n".join("    " + json.dumps(b, ensure_ascii=False) for b in batiments))
        f.write("\n  ]\n}\n")
    print(f"{len(batiments)} bâtiments dans {dest}")


if __name__ == "__main__":
    main()
