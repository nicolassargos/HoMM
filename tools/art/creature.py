"""Le travail complet sur une créature de combat avec PixelLab : images fixes, animations, vérifications, page de
choix, installation dans le jeu. Chaque créature a sa fiche dans tools/art/creatures-art.json (description, modèle
de style, image d'angle, images de référence, hauteur…), avec un gabarit par sorte de créature (humain, volant,
quadrupède, grand) qui donne la fin des textes de demande et les mouvements par défaut.

À lancer depuis la racine du dépôt. La clé PixelLab est lue dans PIXELLAB_API_KEY (jamais affichée).

    python -I tools/art/creature.py fixe <id> [lots=4] [graine]
        <lots> demandes au modèle « Pro » (4 images chacune à ~104 × 120 : 0,08 $ par lot), réduites à la hauteur de la
        fiche, contour adouci ; un lot sur deux d'après les images de référence, l'autre avec l'image d'angle seule
        (une image de sujet impose son propre angle de vue). Candidats nommés L<lot>-<n>. Coûte de l'argent.
    python -I tools/art/creature.py choisir <id> fixe L3-2
        installe l'image fixe (art/creatures/battle/<id>.png) et garde la grande image pour les animations.
    python -I tools/art/creature.py animer <id> <idle|move|attack> [essais=2] [graine] [--consigne="…"] [--sans="…"]
            [--enrichir] [--images=16] [--sans-retour] [--depart=image.png] [--largeur=3]
        <essais> animations (environ 0,02 $ chacune) depuis la grande image choisie, puis réduites en feuilles de 8
        images (traits fins gardés, étincelles retirées) et vérifiées. Les essais alternent les deux modèles de
        PixelLab. Au plus 8 tâches à la fois (les autres attendent). --sans remplace la fin du texte (« pas d'autre
        personnage, pas d'effet… » ; vide : rien) ; --enrichir laisse PixelLab détailler le mouvement ; --images=16 : une
        feuille de 16 images (4 × 4), pour un mouvement lent mais fluide ; --sans-retour : l'animation ne revient pas à la
        pose de départ (une transformation) ; --depart : une autre image de départ, à l'échelle de la grande image (la
        dernière image d'une transformation, pour animer la chauve-souris) ; --largeur : largeur du cadre d'une attaque,
        en fois la largeur de la créature (2,2 par défaut ; plus pour une cape ou des ailes déployées). État « transform » : joué avant un déplacement, et à l'envers après.
        Coûte de l'argent.
    python -I tools/art/creature.py retoucher <id> <nom> [--eclats[=partout]] [--copier=7:8,…] [--corde=7,8] [--nouveau=5] [--detaches] [--recentrer[=tete]]
        corrige une feuille (écrit <nom>-r) : efface les éclats d'impact (devant la créature, ou partout), remplace l'image 7 par la 8, trace la corde
        de l'arc sur ces images, efface dans l'image 5 les couleurs absentes des images voisines, efface les petits morceaux détachés, garde les pieds (ou la tête) à la même place d'une image à l'autre. Gratuit.
    python -I tools/art/creature.py choisir <id> <idle|move|attack> <nom> [images/s]
        installe la feuille (art/creatures/battle/<id>-idle.png, -move, -attack) et sa vitesse dans animations.json (avec son nombre
        d'images, s'il n'est pas de 8).
    python -I tools/art/creature.py retailler <id> <hauteur>
        refait l'image fixe et les feuilles choisies à une autre hauteur (en pixels), depuis les grandes images. Gratuit.
    python -I tools/art/creature.py page <id>
        refait la page de choix (art-candidats/en-cours/<id>.html) : images fixes sur l'herbe à côté des créatures de
        comparaison, animations sur des cases du champ de bataille, face à leur copie retournée (le défenseur), avec
        les pauses du jeu, et les alertes des vérifications.

Pour voir ensuite la créature dans le jeu : godot avec « -- --creature-preview=<id> » (et --battle-auto).
"""
import json
import math
import os
import shutil
import sys
import threading
from concurrent.futures import ThreadPoolExecutor

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire
import nettoyage as n
import pixellab

FICHES = "tools/art/creatures-art.json"
BATAILLE = "godot/art/creatures/battle"
VITESSES = BATAILLE + "/animations.json"
EN_COURS = "art-candidats/en-cours"
GARDEES = "art-candidats/references/derives"
ETATS = ("idle", "move", "attack", "transform")  # transform : facultatif, joué avant et après un déplacement


def charger():
    with open(FICHES, encoding="utf-8") as f:
        return json.load(f)


def enregistrer(donnees):
    with open(FICHES, "w", encoding="utf-8") as f:
        json.dump(donnees, f, ensure_ascii=False, indent=2)
        f.write("\n")


def fiche(donnees, ident):
    if ident not in donnees["creatures"]:
        sys.exit(f"Pas de fiche pour « {ident} » dans {FICHES}.")
    f = donnees["creatures"][ident]
    return f, donnees["gabarits"][f.get("gabarit", "humain")]


def dossier(ident, *parties):
    return os.path.join(EN_COURS, "candidats", ident, *parties)


def taches(travaux):
    """Lance les travaux (fonctions sans argument) à 8 au plus en même temps ; rend les erreurs."""
    erreurs = []
    verrou = threading.Lock()

    def lancer(t):
        try:
            t()
        except BaseException as e:  # pixellab.py s'arrête par sys.exit en cas d'erreur
            with verrou:
                erreurs.append(str(e))
    with ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(lancer, travaux))
    for e in erreurs:
        print("Échec :", e)
    return erreurs


def preparer_fixe(grande, f):
    """La grande image → l'image du jeu : recadrée, tournée vers la droite, réduite à la hauteur de la fiche, adoucie."""
    p = n.recadrer(grande)
    if f.get("miroir"):
        p = n.miroir(p)
    q = n.adoucir_image(n.reduire_traits(p, f["hauteur"] / len(p)))
    if f.get("corde"):
        n.corde_droite(q)
    return q


# --- images fixes ---

def cmd_fixe(ident, lots=4, graine=0):
    donnees = charger()
    f, g = fiche(donnees, ident)
    description = g["debut"] + f["description"] + g["fin"]
    style = f.get("style", BATAILLE + "/moine.png")
    angle = f"{f['angle']}::{g['usage_angle']}" if f.get("angle") else None
    refs = f.get("references", [])
    largeur, hauteur = f.get("generation", [104, 120])
    base = dossier(ident, "fixe")
    deja = [int(d[1:]) for d in os.listdir(base) if d.startswith("l") and d[1:].isdigit()] if os.path.isdir(base) else []
    premier = max(deja, default=0) + 1
    lots_faits = []

    def lot(k, modeles):
        return lambda: pixellab.pro(os.path.join(base, f"l{k}"), largeur, hauteur, description, graine + k, style, modeles)
    travaux = []
    for i in range(lots):
        k = premier + i
        if refs and angle:
            modeles = refs if i % 2 == 0 else [angle]
        else:
            modeles = refs or ([angle] if angle else [])
        f.setdefault("lots", {})[f"L{k}"] = "références" if modeles is refs and refs else ("angle seul" if modeles else "texte seul")
        travaux.append(lot(k, modeles))
        lots_faits.append(k)
    taches(travaux)
    os.makedirs(os.path.join(base, "reduit"), exist_ok=True)
    for k in lots_faits:
        d = os.path.join(base, f"l{k}")
        for i in range(1, 30):
            chemin = os.path.join(d, f"{i}.png")
            if os.path.exists(chemin):
                ecrire(os.path.join(base, "reduit", f"L{k}-{i}.png"), preparer_fixe(lire(chemin)[2], f))
    enregistrer(donnees)
    cmd_page(ident)
    pixellab.solde()


def choisir_fixe(donnees, ident, nom):
    f, _ = fiche(donnees, ident)
    lot, _, i = nom[1:].partition("-")
    source = dossier(ident, "fixe", f"l{lot}", f"{i}.png")
    if not os.path.exists(source):
        sys.exit(f"Candidat introuvable : {source}")
    os.makedirs(GARDEES, exist_ok=True)
    grande = os.path.join(GARDEES, f"{ident}-grand.png")
    shutil.copy(source, grande)
    q = preparer_fixe(lire(grande)[2], f)
    centre = n.axe(q)
    largeur = 2 * math.ceil(max(centre, len(q[0]) - centre)) + 2
    ecrire(f"{BATAILLE}/{ident}.png", n.poser(q, largeur, len(q) + 2, centre))
    f.setdefault("choix", {})["fixe"] = nom
    f["grande"] = grande.replace("\\", "/")
    print(f"{BATAILLE}/{ident}.png installée ({largeur} × {len(q) + 2}) ; grande image gardée : {grande}")


# --- animations ---

def arrondi16(v):
    return int(math.ceil(v / 16) * 16)


def departs(f, ident, large=2.2):
    """Les images de départ des animations (la grande image dans un cadre plus large), et le facteur de réduction."""
    p = n.recadrer(lire(f["grande"])[2])
    if f.get("miroir"):
        p = n.miroir(p)
    h, w = len(p), len(p[0])
    d = dossier(ident, "anim")
    os.makedirs(d, exist_ok=True)
    # assez large pour un bras tendu (sinon PixelLab le coupe au bord)
    tailles = {"idle": (min(256, arrondi16(w * 1.8)), arrondi16(h * 1.2)),
               "attack": (min(256, arrondi16(w * large)), min(256, arrondi16(h * 1.35)))}
    tailles["move"] = tailles["transform"] = tailles["idle"]
    chemins = {}
    for etat, (W, H) in tailles.items():
        chemins[etat] = os.path.join(d, f"depart-{etat}.png")
        # écrit à côté puis remplace d'un coup : une autre commande lancée en même temps ne lit jamais un fichier à moitié écrit
        provisoire = f"{chemins[etat]}.{os.getpid()}.tmp"
        ecrire(provisoire, n.poser(p, W, H))
        os.replace(provisoire, chemins[etat])
    return chemins, f["hauteur"] / h


def nombre_images(dossier_images):
    """Le nombre d'images d'une animation de PixelLab (sans la 1re, qui est l'image de départ)."""
    return len([x for x in os.listdir(dossier_images) if x[:-4].isdigit() and x.endswith(".png")]) - 1


def traiter_feuille(dossier_images, facteur, f, etat):
    """Images 2 à N+1 de PixelLab (la 1re est l'image de départ) → images réduites, nettoyées ; et les grandes."""
    grandes = [lire(os.path.join(dossier_images, f"{i}.png"))[2] for i in range(2, nombre_images(dossier_images) + 2)]
    images = []
    for g in grandes:
        q = n.adoucir_image(n.reduire_traits(n.retirer_cyan(g), facteur))
        n.retirer_etincelles(q)
        if f.get("corde") and etat != "attack":
            n.corde_droite(q)
        images.append(q)
    depart = n.adoucir_image(n.reduire_traits(lire(os.path.join(dossier_images, "1.png"))[2], facteur))
    return images, grandes, depart


def lire_verifs(ident):
    chemin = dossier(ident, "anim", "verifications.json")
    return json.load(open(chemin, encoding="utf-8")) if os.path.exists(chemin) else {}


def ecrire_verifs(ident, v):
    with open(dossier(ident, "anim", "verifications.json"), "w", encoding="utf-8") as fo:
        json.dump(v, fo, ensure_ascii=False, indent=1)


def cmd_animer(ident, etat, essais=2, graine=0, consigne=None, sans=None, enrichir=False, nombre=8, retour=True,
               depart=None, large=2.2):
    if etat not in ETATS:
        sys.exit(f"État inconnu : {etat} (idle, move ou attack)")
    donnees = charger()
    f, g = fiche(donnees, ident)
    if not f.get("grande"):
        sys.exit("Choisir d'abord l'image fixe (choisir <id> fixe L…).")
    chemins, facteur = departs(f, ident, large)
    if depart:
        # une autre image de départ (même échelle que la grande image : par exemple la dernière d'une transformation)
        chemins[etat] = depart
    mouvement = consigne or f.get("mouvements", {}).get(etat) or g["mouvements"][etat]
    # « sans » : la fin de texte du gabarit (pas d'autre personnage…) ; --sans= la remplace (vide : rien)
    fin = g["sans"] if sans is None else sans
    texte = f"{mouvement}; {fin}" if fin else mouvement
    d = dossier(ident, "anim")
    # chaque essai réserve son dossier (création atomique) : deux commandes lancées ensemble ne se mélangent pas
    noms, k = [], 1
    while len(noms) < essais:
        try:
            os.makedirs(os.path.join(d, f"{etat}-{k}"))
            noms.append(f"{etat}-{k}")
        except FileExistsError:
            pass
        k += 1
    deja = list(range(k))

    def essai(i, nom):
        return lambda: pixellab.animer(chemins[etat], os.path.join(d, nom), texte, nombre, graine + 100 * i + len(deja),
                                       retour, i % 2 == 1, f.get("sujet"), enrichir)
    taches([essai(i, nom) for i, nom in enumerate(noms)])
    verifs = lire_verifs(ident)
    for nom in noms:
        if not os.path.exists(os.path.join(d, nom, f"{nombre + 1}.png")):
            continue
        images, grandes, depart = traiter_feuille(os.path.join(d, nom), facteur, f, etat)
        ecrire(os.path.join(d, nom + ".png"), n.assembler(images))
        verifs[nom] = n.verifier(images, grandes, etat, depart)
        print(nom, ":", "; ".join(verifs[nom]) or "rien à signaler")
    ecrire_verifs(ident, verifs)
    f.setdefault("consignes", {})[etat] = mouvement
    enregistrer(donnees)
    cmd_page(ident)
    pixellab.solde()


def cmd_retoucher(ident, nom, options):
    donnees = charger()
    f, _ = fiche(donnees, ident)
    d = dossier(ident, "anim")
    base = nom.split("-r")[0]
    total = nombre_images(os.path.join(d, base)) if os.path.isdir(os.path.join(d, base)) else 8
    images = n.decouper(lire(os.path.join(d, nom + ".png"))[2], 4, (total + 3) // 4)
    # les copies d'abord, puis les effacements (sur les bonnes images)
    ordre = ("--copier", "--nouveau", "--eclats", "--detaches", "--corde", "--recentrer")
    options = sorted(options, key=lambda o: next((i for i, d in enumerate(ordre) if o.startswith(d)), 9))
    for o in options:
        if o in ("--eclats", "--eclats=partout"):
            centre = n.axe(images[0])
            marge = None if o.endswith("partout") else 12
            garder = n.teintes(images[0])
            print("éclats effacés :", sum(n.effacer_eclats(q, marge, centre, garder) for q in images), "pixels")
        elif o.startswith("--copier="):
            for paire in o.split("=", 1)[1].split(","):
                a, b = (int(v) for v in paire.split(":"))
                images[a - 1] = [list(r) for r in images[b - 1]]
        elif o.startswith("--nouveau="):
            for k in o.split("=", 1)[1].split(","):
                print(f"image {k} : {n.effacer_nouveau(images, int(k))} pixels de couleur nouvelle effacés")
        elif o == "--detaches":
            print("morceaux détachés effacés :", sum(n.retirer_detaches(q) for q in images))
        elif o.startswith("--recentrer"):
            images = n.recentrer(images, "tete" if o.endswith("=tete") else "pieds")
        elif o.startswith("--corde="):
            for k in o.split("=", 1)[1].split(","):
                print(f"corde de l'image {k} :", "tracée" if n.corde_droite(images[int(k) - 1]) else "arc introuvable")
        else:
            sys.exit(f"Option inconnue : {o}")
    for q in images:
        n.retirer_etincelles(q)
    sortie = nom + "-r"
    ecrire(os.path.join(d, sortie + ".png"), n.assembler(images))
    verifs = lire_verifs(ident)
    verifs[sortie] = n.verifier(images, None, nom.split("-")[0])
    ecrire_verifs(ident, verifs)
    print(sortie, ":", "; ".join(verifs[sortie]) or "rien à signaler")
    cmd_page(ident)


def lire_vitesses():
    with open(VITESSES, encoding="utf-8") as fo:
        return json.load(fo)


def choisir_anim(donnees, ident, etat, nom, fps=None):
    f, _ = fiche(donnees, ident)
    source = dossier(ident, "anim", nom + ".png")
    if not os.path.exists(source):
        sys.exit(f"Feuille introuvable : {source}")
    shutil.copy(source, f"{BATAILLE}/{ident}-{etat}.png")
    f.setdefault("choix", {})[etat] = nom
    dossier_images = dossier(ident, "anim", nom.split("-r")[0])
    total = nombre_images(dossier_images) if os.path.isdir(dossier_images) else 8
    vitesses = lire_vitesses()
    reglages = vitesses.setdefault(ident, {})
    if fps:
        reglages[etat] = float(fps) if "." in str(fps) else int(fps)
    # une feuille de plus de 8 images : le jeu doit le savoir
    if total != 8:
        reglages[etat + "Frames"] = total
    else:
        reglages.pop(etat + "Frames", None)
    if not reglages:
        vitesses.pop(ident)
    if True:
        with open(VITESSES, "w", encoding="utf-8") as fo:
            fo.write("{\n" + ",\n".join(f'  "{k}": ' + json.dumps(v, ensure_ascii=False) for k, v in vitesses.items()) + "\n}\n")
    print(f"{BATAILLE}/{ident}-{etat}.png installée" + (f", {fps} images/s" if fps else ""))


def cmd_retailler(ident, hauteur):
    """Refait l'image fixe et les feuilles choisies à une autre hauteur, depuis les grandes images (sans dépense)."""
    donnees = charger()
    f, _ = fiche(donnees, ident)
    f["hauteur"] = hauteur
    q = preparer_fixe(lire(f["grande"])[2], f)
    centre = n.axe(q)
    largeur = 2 * math.ceil(max(centre, len(q[0]) - centre)) + 2
    ecrire(f"{BATAILLE}/{ident}.png", n.poser(q, largeur, len(q) + 2, centre))
    print(f"{BATAILLE}/{ident}.png : {largeur} × {len(q) + 2}")
    facteur = hauteur / len(n.recadrer(lire(f["grande"])[2]))
    d = dossier(ident, "anim")
    for etat, nom in f.get("choix", {}).items():
        if etat not in ETATS:
            continue
        base = nom.split("-r")[0]
        if not os.path.isdir(os.path.join(d, base)):
            print(f"{etat} : {base} introuvable, feuille gardée telle quelle")
            continue
        images, _, _ = traiter_feuille(os.path.join(d, base), facteur, f, etat)
        ecrire(os.path.join(d, base + ".png"), n.assembler(images))
        shutil.copy(os.path.join(d, base + ".png"), f"{BATAILLE}/{ident}-{etat}.png")
        print(f"{BATAILLE}/{ident}-{etat}.png refaite" + (f" (sans les retouches de {nom} : à refaire)" if base != nom else ""))
    enregistrer(donnees)
    cmd_page(ident)


def cmd_choisir(ident, quoi, nom, fps=None):
    donnees = charger()
    if quoi == "fixe":
        choisir_fixe(donnees, ident, nom)
    elif quoi in ETATS:
        choisir_anim(donnees, ident, quoi, nom, fps)
    else:
        sys.exit("choisir <id> fixe|idle|move|attack <nom> [images/s]")
    enregistrer(donnees)
    cmd_page(ident)


# --- page de choix ---

PAGE = """<!doctype html><meta charset=utf-8><title>{titre}</title>
<style>
body{{background:#222;color:#eee;font-family:sans-serif;margin:12px}}h2{{margin:20px 4px 6px}}h3{{margin:12px 4px 4px;font-size:15px}}
.rang{{background:url({herbe});background-size:48px 48px;image-rendering:pixelated;padding:8px;display:flex;flex-wrap:wrap;align-items:flex-end}}
img{{image-rendering:pixelated;display:block}}figure{{margin:2px 6px;text-align:center}}
figcaption{{color:#fff;text-shadow:0 0 3px #000;font-size:13px}}.pris{{outline:3px solid gold}}
.scenes{{display:flex;flex-wrap:wrap;gap:10px}}.scene{{text-align:center;font-size:13px;max-width:345px}}
canvas{{image-rendering:pixelated;background:url({herbe});background-size:48px 48px}}.alerte{{color:#f99;font-size:12px}}
label{{margin-right:14px}}
</style>
<h1>{titre}</h1><p>{notes}</p>
<h2>Images fixes</h2><p>Taille du jeu (×2), sur l'herbe, à côté des créatures de comparaison. ★ : l'image installée.</p>
{fixes}
<h2>Animations</h2>
<p><label><input type=checkbox id=pauses checked> pauses entre deux attentes, comme en jeu</label>
<label>vitesse <select id=vitesse><option value=0>celle du jeu</option><option>5</option><option>6</option><option>7</option>
<option>8</option><option>10</option><option>12</option></select></label></p>
<p>Sur des cases du champ de bataille : la créature (attaquant) à gauche, une créature de comparaison derrière elle, et sa
copie retournée (le défenseur) en face ; un déplacement traverse quatre cases à la vitesse du jeu, puis recommence.
★ : l'animation installée.</p>
{anims}
<script>
const ANIMS={donnees};
const HW=73.3,HH=84,PIED=34;
function hexa(g,cx,cy){{g.beginPath();for(let i=0;i<6;i++){{const a=Math.PI/3*i+Math.PI/6;g.lineTo(cx+HH/2*Math.cos(a),cy+HH/2*Math.sin(a));}}
 g.closePath();g.fillStyle="rgba(0,0,0,0.06)";g.fill();g.strokeStyle="rgba(0,0,0,0.35)";g.stroke();}}
function bruit(c,p){{return Math.abs(Math.sin(c*12.9898+p*78.233)*43758.5453)%1;}}
function image(t,fps,attaque,pause,phase,nb){{
  const jeu=nb/fps;
  if(attaque){{const cycle=jeu+1.5;const u=t%cycle;return u<jeu?Math.floor(u*fps):0;}}
  if(!pause)return Math.floor(t*fps)%nb;
  for(let c=0;;c++){{const p=pause[0]+bruit(c,phase)*(pause[1]-pause[0]);if(t<p)return 0;t-=p;if(t<jeu)return Math.floor(t*fps);t-=jeu;}}
}}
for(const a of ANIMS){{
  const cv=document.getElementById(a.id),g=cv.getContext("2d");g.imageSmoothingEnabled=false;
  const im=new Image(),voisin=new Image();let pret=0;im.onload=voisin.onload=()=>{{pret++;}};
  im.src=a.feuille;voisin.src=a.voisin;const t0=performance.now();
  (function dessin(){{
    requestAnimationFrame(dessin);if(pret<2)return;
    const fw=im.width/4,fh=im.height/Math.ceil(a.images/4);g.clearRect(0,0,cv.width,cv.height);
    const cases=[[HW*0.6,cv.height-HH*0.6],[HW*1.6,cv.height-HH*0.6],[HW*3.9,cv.height-HH*0.6]];
    if(!a.deplacement)for(const [x,y] of cases)hexa(g,x,y);
    const choisie=+document.getElementById("vitesse").value,fps=choisie||a.fps;
    const pause=document.getElementById("pauses").checked?a.pause:null;const t=(performance.now()-t0)/1000;
    if(a.deplacement){{
      // quatre cases à la vitesse du jeu (« stepTime » par case), puis une pause sur place
      const cases4=[[HW*0.6,cv.height-HH*0.6]];for(let i=1;i<=5;i++)cases4.push([HW*(0.6+i),cv.height-HH*0.6]);
      for(const [x,y] of cases4)hexa(g,x,y);
      const trajet=4*a.pas,cycle=trajet+0.8,u=t%cycle,avance=Math.min(u,trajet)/a.pas;
      const kk=u<trajet?Math.floor(t*fps)%a.images:0;
      const x=HW*1.6+avance*HW,y=cases4[0][1];
      g.drawImage(voisin,cases4[0][0]-voisin.width,y+PIED-voisin.height*2,voisin.width*2,voisin.height*2);
      g.drawImage(im,kk%4*fw,Math.floor(kk/4)*fh,fw,fh,x-fw,y+PIED-fh*2,fw*2,fh*2);
      return;
    }}
    const k=image(t,fps,a.attaque,pause,0.3,a.images),k2=image(t+0.7,fps,a.attaque,pause,0.8,a.images);
    g.drawImage(voisin,cases[0][0]-voisin.width,cases[0][1]+PIED-voisin.height*2,voisin.width*2,voisin.height*2);
    g.drawImage(im,k%4*fw,Math.floor(k/4)*fh,fw,fh,cases[1][0]-fw,cases[1][1]+PIED-fh*2,fw*2,fh*2);
    g.save();g.translate(cases[2][0],0);g.scale(-1,1);
    g.drawImage(im,k2%4*fw,Math.floor(k2/4)*fh,fw,fh,-fw,cases[2][1]+PIED-fh*2,fw*2,fh*2);g.restore();
  }})();
}}
</script>
"""


def cmd_page(ident):
    donnees = charger()
    f, _ = fiche(donnees, ident)
    choix = f.get("choix", {})
    vers_page = lambda chemin: os.path.relpath(chemin, EN_COURS).replace("\\", "/")
    comparer = [c for c in f.get("comparer", ["archer"]) if os.path.exists(f"{BATAILLE}/{c}.png")]

    def figure(chemin, legende, pris=False):
        w, h, _ = lire(chemin)
        return (f"<figure><img src='{vers_page(chemin)}' style='width:{w * 2}px;height:{h * 2}px'"
                f"{' class=pris' if pris else ''}><figcaption>{legende}</figcaption></figure>")
    base = "".join(figure(f"{BATAILLE}/{c}.png", c) for c in comparer)
    if os.path.exists(f"{BATAILLE}/{ident}.png"):
        base += figure(f"{BATAILLE}/{ident}.png", "★ installée", True)
    fixes = ""
    reduit = dossier(ident, "fixe", "reduit")
    if os.path.isdir(reduit):
        par_lot = {}
        for nom in sorted(os.listdir(reduit), key=lambda x: [int(v) for v in x[1:-4].split("-")]):
            par_lot.setdefault(nom.split("-")[0], []).append(nom[:-4])
        for lot, noms in sorted(par_lot.items(), key=lambda kv: -int(kv[0][1:])):
            fixes += f"<h3>{lot} : {f.get('lots', {}).get(lot, '')}</h3><div class=rang>{base}" + "".join(
                figure(os.path.join(reduit, nom + ".png"), ("★ " if choix.get("fixe") == nom else "") + nom, choix.get("fixe") == nom)
                for nom in noms) + "</div>"
    vitesses = lire_vitesses()
    defaut = vitesses.get("*", {})
    propre = vitesses.get(ident, {})
    pause = [propre.get("idlePauseMin", defaut.get("idlePauseMin", 0)), propre.get("idlePauseMax", defaut.get("idlePauseMax", 0))]
    verifs = lire_verifs(ident) if os.path.isdir(dossier(ident, "anim")) else {}
    voisin = vers_page(f"{BATAILLE}/{comparer[0]}.png") if comparer else ""
    anims, donnees_js = "", []
    d = dossier(ident, "anim")
    for etat, titre in (("idle", "Attente"), ("move", "Déplacement"), ("attack", "Attaque"), ("transform", "Transformation")):
        noms = sorted((x[:-4] for x in os.listdir(d) if x.startswith(etat + "-") and x.endswith(".png")) if os.path.isdir(d) else [],
                      key=lambda x: [int(v) if v.isdigit() else 0 for v in x.split("-")[1:]], reverse=True)
        if not noms:
            continue
        anims += f"<h3>{titre}</h3><div class=scenes>"
        for nom in noms:
            fid = f"c-{nom}"
            dossier_images = os.path.join(d, nom.split("-r")[0])
            total = nombre_images(dossier_images) if os.path.isdir(dossier_images) else 8
            pris = choix.get(etat) == nom
            alertes = "".join(f"<div class=alerte>⚠ {a}</div>" for a in verifs.get(nom, []))
            largeur_scene = 560 if etat == "move" else 340
            anims += (f"<div class=scene style='max-width:{largeur_scene + 5}px'><canvas id='{fid}' width={largeur_scene} height=210></canvas>"
                      f"<div>{'★ ' if pris else ''}{nom}{f' ({total} images)' if total != 8 else ''}</div>{alertes}</div>")
            donnees_js.append({"id": fid, "feuille": vers_page(os.path.join(d, nom + ".png")), "voisin": voisin, "images": total,
                               "fps": propre.get(etat, 10), "attaque": etat in ("attack", "transform"), "deplacement": etat == "move",
                               # comme le jeu : un cycle par case pour une créature qui marche, 0,09 s pour un volant
                               "pas": propre.get("stepTime", 0.09 if f.get("vole") else total / propre.get("move", 10)),
                               "pause": pause if etat == "idle" and pause[1] > 0 else None})
        anims += "</div>"
    html = PAGE.format(titre=f.get("nom", ident), notes=f.get("notes", ""), fixes=fixes or "<p>Pas encore d'image.</p>",
                       anims=anims or "<p>Pas encore d'animation.</p>", donnees=json.dumps(donnees_js),
                       herbe=vers_page("godot/art/terrain/grass.png"))
    sortie = os.path.join(EN_COURS, f"{ident}.html")
    with open(sortie, "w", encoding="utf-8") as fo:
        fo.write(html)
    print("page :", sortie)


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    options = [x for x in sys.argv[1:] if x.startswith("--")]
    if len(a) < 2:
        sys.exit(__doc__)
    commande, ident = a[0], a[1]
    if commande == "fixe":
        cmd_fixe(ident, int(a[2]) if len(a) > 2 else 4, int(a[3]) if len(a) > 3 else 0)
    elif commande == "animer" and len(a) > 2:
        consigne = next((o.split("=", 1)[1] for o in options if o.startswith("--consigne=")), None)
        sans = next((o.split("=", 1)[1] for o in options if o.startswith("--sans=")), None)
        nombre = next((int(o.split("=", 1)[1]) for o in options if o.startswith("--images=")), 8)
        cmd_animer(ident, a[2], int(a[3]) if len(a) > 3 else 2, int(a[4]) if len(a) > 4 else 0, consigne, sans,
                   "--enrichir" in options, nombre, "--sans-retour" not in options,
                   next((o.split("=", 1)[1] for o in options if o.startswith("--depart=")), None),
                   next((float(o.split("=", 1)[1]) for o in options if o.startswith("--largeur=")), 2.2))
    elif commande == "retoucher" and len(a) > 2:
        cmd_retoucher(ident, a[2], options)
    elif commande == "choisir" and len(a) > 3:
        cmd_choisir(ident, a[2], a[3], a[4] if len(a) > 4 else None)
    elif commande == "retailler" and len(a) > 2:
        cmd_retailler(ident, int(a[2]))
    elif commande == "page":
        cmd_page(ident)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
