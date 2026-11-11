"""Blanchit une image au soleil : du bois mort du désert à partir d'une souche couverte de mousse.

    python -I tools/art/blanchir.py entree.png sortie.png [force]

Chaque pixel garde sa luminosité (l'ombrage et le trait restent) mais perd sa couleur : un gris chaud de bois sec, mêlé
à la couleur d'origine selon <force> (0,75 par défaut ; 1 : tout gris chaud). La mousse verte devient ainsi du bois clair.
Fait pour réutiliser les souches et les troncs de l'herbe dans le désert (demandé par l'utilisateur : des troncs plus
épais et plus petits que ceux que Retro Diffusion dessinait). Seule la bibliothèque standard est utilisée.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire  # noqa: E402

BOIS = (1.0, 0.86, 0.68)  # le gris chaud du bois sec (multiplié par la luminosité)


def blanchir(p, force):
    r, g, b, a = p
    if a == 0:
        return p
    lum = 0.30 * r + 0.59 * g + 0.11 * b
    lum = min(255, lum * 1.08 + 10)  # un peu plus clair : le bois délavé
    sec = [lum * k for k in BOIS]
    return tuple(min(255, round(c * (1 - force) + s * force)) for c, s in zip((r, g, b), sec)) + (a,)


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    force = float(sys.argv[3]) if len(sys.argv) > 3 else 0.75
    w, h, px = lire(sys.argv[1])
    ecrire(sys.argv[2], [[blanchir(p, force) for p in r] for r in px])
    print(f"{sys.argv[2]} : blanchie ({force})")


if __name__ == "__main__":
    main()
