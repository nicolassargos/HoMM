"""Mesure et règle la luminosité des images, pour accorder entre elles des images faites séparément (les villes de la carte).

    python -I tools/art/luminosite.py mesure image.png [image2.png…]
        la luminosité moyenne des pixels opaques (0 à 255), la part de pixels très clairs et la saturation moyenne
    python -I tools/art/luminosite.py regler entree.png sortie.png cible [--garder-sombres]
        éclaircit ou assombrit l'image pour que sa luminosité moyenne vaille <cible> ; --garder-sombres : les tons
        sombres (luminosité sous 40) ne bougent pas, la courbe agit pleinement à partir de 110 : éclaircir ainsi
        relève le contraste (demandé par l'utilisateur pour la Forêt et les Barbares)

Le réglage passe par une courbe (gamma) sur chaque couleur : le noir et le blanc ne bougent pas, les tons moyens bougent
le plus ; le trait sombre et les reflets restent, la teinte aussi. La transparence ne change pas. Fait pour la ville de la
Tour, bien plus claire que les autres (demandé par l'utilisateur, 2026-11-11). Seule la bibliothèque standard est utilisée.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire  # noqa: E402


def lum(p):
    return 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]


def opaques(px):
    return [p for r in px for p in r if p[3] > 128]


def mesure(chemin):
    _, _, px = lire(chemin)
    ps = opaques(px)
    ls = [lum(p) for p in ps]
    sat = sum(max(p[:3]) - min(p[:3]) for p in ps) / len(ps)
    return sum(ls) / len(ls), sum(l > 200 for l in ls) / len(ls), sat


def courbe(p, g, garder_sombres=False):
    if p[3] == 0:
        return p
    nouveau = [255 * (c / 255) ** g for c in p[:3]]
    if garder_sombres:
        t = min(1.0, max(0.0, (lum(p) - 40) / 70))
        t = t * t * (3 - 2 * t)  # passage doux entre les sombres gardés et les tons réglés
        nouveau = [c + (n - c) * t for c, n in zip(p[:3], nouveau)]
    return tuple(round(c) for c in nouveau) + (p[3],)


def regler(entree, sortie, cible, garder_sombres=False):
    _, _, px = lire(entree)
    ps = opaques(px)

    def moyenne(g):
        return sum(lum(courbe(p, g, garder_sombres)) for p in ps) / len(ps)

    bas, haut = 0.2, 5.0  # gamma : < 1 éclaircit, > 1 assombrit
    for _ in range(40):
        g = (bas + haut) / 2
        if moyenne(g) > cible:
            bas = g
        else:
            haut = g
    ecrire(sortie, [[courbe(p, g, garder_sombres) for p in r] for r in px])
    print(f"{sortie} : luminosité {moyenne(g):.1f} (gamma {g:.2f})")


def main():
    if len(sys.argv) >= 3 and sys.argv[1] == "mesure":
        for chemin in sys.argv[2:]:
            m, clairs, sat = mesure(chemin)
            print(f"{chemin} : luminosité {m:.1f}, très clairs {clairs:.1%}, saturation {sat:.1f}")
    elif len(sys.argv) in (5, 6) and sys.argv[1] == "regler":
        regler(sys.argv[2], sys.argv[3], float(sys.argv[4]), "--garder-sombres" in sys.argv[5:])
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
