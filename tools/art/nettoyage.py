"""Outils d'image communs aux créatures (images de combat et animations), sans dépense.

Utilisé par tools/art/creature.py ; chaque fonction travaille sur une image en liste de rangées de (r, g, b, a),
lue et écrite avec reduire.lire / reduire.ecrire. Seule la bibliothèque standard de Python est utilisée.

- recadrer, doubler, miroir, axe, poser : couper la transparence, agrandir ×2, retourner, trouver l'axe du corps
  (milieu de la tête) et poser l'image en bas d'un cadre, l'axe au milieu (le jeu centre l'image sur la case).
- reduire_traits : réduire sans perdre les traits d'un pixel (corde d'arc, flèche, chaîne).
- retirer_cyan, retirer_etincelles, effacer_eclats : les effets que PixelLab ajoute.
- corde_droite : tracer la corde d'un arc au repos, entre ses deux bouts.
- verifier : les défauts habituels d'une animation (corps qui glisse, effets, autre personnage, bord du cadre).
"""
import colorsys
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import reduire
from adoucir import adoucir

VIDE = (0, 0, 0, 0)


def lum(c):
    return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]


def hls(c):
    return colorsys.rgb_to_hls(*(v / 255 for v in c[:3]))


def recadrer(p):
    ys = [y for y, r in enumerate(p) if any(c[3] > 128 for c in r)]
    xs = [x for x in range(len(p[0])) if any(p[y][x][3] > 128 for y in ys)]
    return [r[min(xs):max(xs) + 1] for r in p[min(ys):max(ys) + 1]]


def doubler(p):
    return [[c for c in r for _ in (0, 1)] for r in p for _ in (0, 1)]


def miroir(p):
    return [list(reversed(r)) for r in p]


def axe(p, part=0.2):
    """Milieu de la tête : le haut de la créature (part de sa hauteur), en pixels depuis la gauche."""
    haut = next(y for y, r in enumerate(p) if any(c[3] > 128 for c in r))
    bas = max(y for y, r in enumerate(p) if any(c[3] > 128 for c in r))
    lignes = range(haut, haut + max(3, int((bas - haut + 1) * part)))
    xs = [x for y in lignes for x, c in enumerate(p[y]) if c[3] > 128]
    return (min(xs) + max(xs)) / 2


def poser(p, largeur, hauteur, centre=None):
    """L'image en bas d'un cadre, son axe (centre) au milieu ; ce qui dépasse est coupé."""
    centre = axe(p) if centre is None else centre
    h, w = len(p), len(p[0])
    bas = max(y for y, r in enumerate(p) if any(c[3] for c in r))
    ox, oy = round(largeur / 2 - centre), hauteur - 1 - bas
    q = [[VIDE] * largeur for _ in range(hauteur)]
    for y in range(h):
        for x in range(w):
            if p[y][x][3] and 0 <= ox + x < largeur and 0 <= oy + y < hauteur:
                q[oy + y][ox + x] = p[y][x]
    return q


def traits_fins(p):
    """Les pixels d'un trait d'un pixel : opaques, avec au plus deux voisins opaques."""
    h, w = len(p), len(p[0])
    return [(x, y) for y in range(1, h - 1) for x in range(1, w - 1) if p[y][x][3]
            and sum(1 for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx or dy) and p[y + dy][x + dx][3]) <= 2]


def reduire_traits(p, f):
    """Réduit (palette d'origine), puis remet les traits fins que la réduction a perdus (corde, flèche…)."""
    q = reduire(p, f)
    h, w = len(q), len(q[0])
    for x, y in traits_fins(p):
        X, Y = int(x * f), int(y * f)
        if 0 <= X < w and 0 <= Y < h and not q[Y][X][3]:
            q[Y][X] = p[y][x]
    return q


def retirer_cyan(p):
    """Les étincelles cyan que PixelLab ajoute aux attaques."""
    return [[VIDE if (c[3] and c[2] > 140 and c[1] > 140 and c[0] < 130 and c[2] - c[0] > 60) else c for c in r] for r in p]


def morceaux(p):
    """Les groupes de pixels opaques qui se touchent (8 voisins), du plus grand au plus petit."""
    h, w = len(p), len(p[0])
    vu, groupes = set(), []
    for y0 in range(h):
        for x0 in range(w):
            if not p[y0][x0][3] or (x0, y0) in vu:
                continue
            pile, g = [(x0, y0)], []
            vu.add((x0, y0))
            while pile:
                x, y = pile.pop()
                g.append((x, y))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        a, b = x + dx, y + dy
                        if 0 <= a < w and 0 <= b < h and (a, b) not in vu and p[b][a][3]:
                            vu.add((a, b))
                            pile.append((a, b))
            groupes.append(g)
    return sorted(groupes, key=len, reverse=True)


def retirer_etincelles(p):
    """Efface les petits morceaux clairs détachés du corps (étincelles, éclats) ; garde les morceaux sombres
    (un bout de corde ou de chaîne) et le corps lui-même."""
    n = 0
    for g in morceaux(p)[1:]:
        clair = sum(lum(p[y][x]) for x, y in g) / len(g)
        if (len(g) < 400 and clair > 150) or (len(g) < 12 and clair > 120):
            for x, y in g:
                p[y][x] = VIDE
            n += 1
    return n


def effacer_eclats(p, marge=12):
    """Efface les éclats d'impact (rouge, orange, jaune vifs, blanc pur) devant la créature, à plus de <marge>
    pixels à droite de son axe. À demander pour une animation qui en a (la vérification les signale)."""
    centre = axe(p)
    n = 0
    for y, r in enumerate(p):
        for x, c in enumerate(r):
            if c[3] and x > centre + marge:
                h, l, s = hls(c)
                if (s > 0.45 and l > 0.3 and (h < 0.17 or h > 0.93)) or l > 0.9:
                    r[x] = VIDE
                    n += 1
    return n


def corde_droite(p, couleur=(61, 50, 46, 255)):
    """Trace la corde d'un arc tenu debout, à droite de la tête : une ligne entre ses deux bouts (sans toucher l'arc).
    Rend True si une corde a été tracée."""
    h, w = len(p), len(p[0])
    centre = axe(p, 0.12)

    def brun(c):
        hh, l, s = hls(c)
        return c[3] and l < 0.36 and s > 0.15 and (hh < 0.11 or hh > 0.95)

    candidats = [[VIDE] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if x > centre + 3 and brun(p[y][x]):
                candidats[y][x] = p[y][x]
    groupes = morceaux(candidats)
    if not groupes:
        return False
    arc = max(groupes, key=lambda g: max(y for _, y in g) - min(y for _, y in g))
    (x0, y0) = min(arc, key=lambda t: (t[1], -t[0]))
    (x1, y1) = max(arc, key=lambda t: (t[1], t[0]))
    if y1 - y0 < h * 0.5:
        return False
    pris = set(arc)
    for y in range(y0 + 1, y1):
        x = round(x0 + (x1 - x0) * (y - y0) / (y1 - y0)) - 1
        if (x, y) not in pris and 0 <= x < w:
            p[y][x] = couleur
    return True


def adoucir_image(p):
    return adoucir(p, len(p[0]), len(p), 0.6, 45, 2)


def decouper(feuille, colonnes=4, rangees=2):
    h, w = len(feuille) // rangees, len(feuille[0]) // colonnes
    return [[r[(k % colonnes) * w:(k % colonnes + 1) * w] for r in feuille[(k // colonnes) * h:(k // colonnes + 1) * h]]
            for k in range(colonnes * rangees)]


def assembler(images, colonnes=4):
    h, w = len(images[0]), len(images[0][0])
    rangees = (len(images) + colonnes - 1) // colonnes
    px = [[VIDE] * (colonnes * w) for _ in range(rangees * h)]
    for k, q in enumerate(images):
        for y in range(h):
            px[(k // colonnes) * h + y][(k % colonnes) * w:(k % colonnes + 1) * w] = q[y]
    return px


def eclat(q):
    """Pixels d'éclat : blanc pur, ou rouge, orange et jaune vifs (feu, impact)."""
    def vif(c):
        h, l, s = hls(c)
        return (s > 0.55 and l > 0.45 and (h < 0.17 or h > 0.93)) or l > 0.93
    return sum(1 for r in q for c in r if c[3] and vif(c))


def verifier(images, grandes=None, etat="idle", depart=None):
    """Les défauts habituels d'une animation (liste de textes, vide si rien) : <images> réduites, <grandes> avant
    réduction (pour les traits fins). Chaque image est comparée à la plus calme de la feuille, pour ne pas compter
    les reflets propres à la créature."""
    alertes = []
    try:
        axes = [axe(q) for q in images]
    except (StopIteration, ValueError):
        return ["une image est vide"]
    if etat == "attack":
        debut = axe(depart) if depart else axes[0]
        if abs(axes[-1] - debut) > 3:
            alertes.append(f"ne revient pas à sa place ({abs(axes[-1] - debut):.0f} px entre le départ et la dernière image)")
    elif max(axes) - min(axes) > 4:
        alertes.append(f"la tête se déplace de {max(axes) - min(axes):.0f} px (le corps glisse ?)")
    tailles = [sum(1 for r in q for c in r if c[3]) for q in images]
    eclats = [eclat(q) for q in images]
    base_taille, base_eclat = min(tailles), min(eclats)
    for k, q in enumerate(images, start=1):
        h, w = len(q), len(q[0])
        if tailles[k - 1] > base_taille * 1.3:
            alertes.append(f"image {k} : {100 * tailles[k - 1] // base_taille - 100} % de pixels en plus : un autre personnage ou un effet ?")
        if eclats[k - 1] > base_eclat + 25 and eclats[k - 1] > base_eclat * 1.6:
            alertes.append(f"image {k} : {eclats[k - 1] - base_eclat} pixels d'éclat (blanc pur ou feu) : un effet ?")
        groupes = morceaux(q)
        for g in groupes[1:]:
            if len(g) > max(40, len(groupes[0]) * 0.15):
                alertes.append(f"image {k} : un grand morceau détaché ({len(g)} px) : un autre personnage ?")
                break
        bord = sum(1 for x in range(w) if q[0][x][3]) + sum(1 for y in range(h) for x in (0, w - 1) if q[y][x][3])
        if bord > 2:
            alertes.append(f"image {k} : touche le bord du cadre ({bord} px), elle est coupée")
    if grandes and etat != "attack":
        fins = [len(traits_fins(g)) for g in grandes]
        if max(fins) > 30 and min(fins) < max(fins) * 0.3:
            alertes.append("des traits fins (corde, chaîne) apparaissent puis disparaissent : clignotement ?")
    return alertes
