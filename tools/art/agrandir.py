"""Agrandit une image générée (l'« outpainting ») : l'image au milieu d'un cadre plus grand, PixelLab peint les bords.

    python -I tools/art/agrandir.py source.png sortie.png largeur hauteur "description" [graine]

PixelLab « Pro » ne dessine pas plus de 792 de large ; son « inpaint » récent demande un abonnement (Tier 2). L'ancien
« /inpaint » marche avec notre compte, par morceaux de 200 × 200 au plus (environ 0,008 $ chacun) : on avance par
fenêtres qui se recouvrent, chacune garde ce qui est déjà peint et peint le reste. Le fond d'une ville (792 × 241 →
960 × 270, 2026-11-21) : 14 morceaux, 0,145 $, sans couture visible sur les côtés ; ajouter du ciel en haut laisse des
rectangles d'un bleu un peu différent. Ensuite, `affiner.py` (les traits de 2 px de PixelLab).
Seule la bibliothèque standard de Python est utilisée.
"""
import os
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pixellab as pl
from reduire import ecrire, lire

T = 200  # taille d'un morceau (limite de /inpaint)
VIDE = (0, 0, 0, 0)


def positions(n):
    """Les débuts des fenêtres sur une longueur n : elles se recouvrent d'au moins 50 pixels."""
    if n <= T:
        return [0]
    k = -(-(n - T) // 150) + 1
    return [round(i * (n - T) / (k - 1)) for i in range(k)]


def agrandir(source, sortie, largeur, hauteur, description, graine=53):
    w, h, px = lire(source)
    x0, y0 = (largeur - w) // 2, (hauteur - h) // 2
    img = [[VIDE] * largeur for _ in range(hauteur)]
    connu = [[False] * largeur for _ in range(hauteur)]
    for y in range(h):
        for x in range(w):
            img[y0 + y][x0 + x] = px[y][x]
            # 2 pixels du bord repeints aussi, pour que la jonction se fonde
            connu[y0 + y][x0 + x] = 2 <= x < w - 2 and 2 <= y < h - 2
    dossier = tempfile.mkdtemp()
    f_image, f_masque, f_rendu = (os.path.join(dossier, n) for n in ("fenetre.png", "masque.png", "rendu.png"))
    total, n = 0, 0
    for wy in positions(hauteur):
        for wx in positions(largeur):
            garde = [[connu[wy + j][wx + i] for i in range(T)] for j in range(T)]
            if all(all(r) for r in garde):
                continue
            ecrire(f_image, [img[wy + j][wx:wx + T] for j in range(T)])
            # masque : blanc = à peindre, noir = gardé
            ecrire(f_masque, [[(0, 0, 0, 255) if g else (255, 255, 255, 255) for g in r] for r in garde])
            r = pl.requete("POST", "/inpaint", {
                "description": description, "seed": graine + n, "no_background": False,
                "inpainting_image": pl.image_base64(f_image), "mask_image": pl.image_base64(f_masque),
                "image_size": {"width": T, "height": T}})
            with open(f_rendu, "wb") as f:
                f.write(pl.decoder(r["image"]))
            _, _, rendu = lire(f_rendu)
            for j in range(T):
                for i in range(T):
                    if not garde[j][i]:
                        img[wy + j][wx + i] = rendu[j][i]
                    connu[wy + j][wx + i] = True
            total += (r.get("usage") or {}).get("usd") or 0
            n += 1
    ecrire(sortie, img)
    print(f"{sortie} : {largeur} × {hauteur}, {n} morceaux (coût : {total:.3f} $)")


if __name__ == "__main__":
    a = sys.argv
    if len(a) < 6:
        sys.exit(__doc__)
    agrandir(a[1], a[2], int(a[3]), int(a[4]), a[5], int(a[6]) if len(a) > 6 else 53)
