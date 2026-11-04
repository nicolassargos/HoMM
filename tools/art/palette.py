"""Ramène une image à une petite palette (comme le Moine, 20 couleurs), pour des formes plus nettes.
Les couleurs sont regroupées (découpage médian, puis quelques passes de moyennes) ; la transparence ne change pas.

    python -I tools/art/palette.py entree.png sortie.png [couleurs=24]

À faire après tools/art/adoucir.py et la réduction, qui créent des teintes intermédiaires.
Marche aussi sur une feuille d'animation (une seule palette pour toutes les images, qui restent cohérentes).
Seule la bibliothèque standard de Python est utilisée.
"""
import os
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire


def distance(a, b):
    # écart perçu approché (le vert compte plus)
    return 2 * (a[0] - b[0]) ** 2 + 4 * (a[1] - b[1]) ** 2 + 3 * (a[2] - b[2]) ** 2


def moyenne(groupe):
    n = sum(k for _, k in groupe)
    return tuple(round(sum(c[i] * k for c, k in groupe) / n) for i in range(3))


def palette(couleurs, n):
    """couleurs : Counter {(r, g, b): nombre}. Rend au plus n couleurs."""
    boites = [list(couleurs.items())]
    while len(boites) < n:
        # coupe la boîte la plus étendue (pondérée par le nombre de pixels)
        def etendue(b):
            if len(b) < 2:
                return -1
            return max(max(c[i] for c, _ in b) - min(c[i] for c, _ in b) for i in range(3)) * sum(k for _, k in b) ** 0.5
        b = max(boites, key=etendue)
        if etendue(b) <= 0:
            break
        i = max(range(3), key=lambda i: max(c[i] for c, _ in b) - min(c[i] for c, _ in b))
        b.sort(key=lambda ck: ck[0][i])
        total, cumul, coupe = sum(k for _, k in b), 0, 1
        for j, (_, k) in enumerate(b):
            cumul += k
            if cumul >= total / 2:
                coupe = min(max(j + 1, 1), len(b) - 1)
                break
        boites.remove(b)
        boites += [b[:coupe], b[coupe:]]
    centres = [moyenne(b) for b in boites]
    for _ in range(6):
        groupes = [[] for _ in centres]
        for c, k in couleurs.items():
            groupes[min(range(len(centres)), key=lambda j: distance(c, centres[j]))].append((c, k))
        centres = [moyenne(g) if g else centres[j] for j, g in enumerate(groupes)]
    return centres


def reduire_palette(p, n):
    couleurs = Counter(c[:3] for r in p for c in r if c[3] > 128)
    centres = palette(couleurs, n)
    proche = {c: min(centres, key=lambda m: distance(c, m)) for c in couleurs}
    return [[(proche[c[:3]] + (255,)) if c[3] > 128 else (0, 0, 0, 0) for c in r] for r in p]


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    n = int(sys.argv[3]) if len(sys.argv) > 3 else 24
    w, h, p = lire(sys.argv[1])
    ecrire(sys.argv[2], reduire_palette(p, n))
    print(f"{sys.argv[2]} : {n} couleurs au plus")


if __name__ == "__main__":
    main()
