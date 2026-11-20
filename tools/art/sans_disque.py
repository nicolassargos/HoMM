# Terre brûlée : le disque de sol peint sous un objet généré (pierre grise, cendre) prend la couleur du sol du champ et
# s'efface en s'éloignant de l'objet (65 % au pied, puis 38, 30, 12 et 8 %, comme sous les arbres de la neige) ; le feu
# orange garde sa couleur, plus opaque (la lueur que l'utilisateur aime).
#
#     python -I tools/art/sans_disque.py entree.png sortie.png [--sombre]
#
# Le disque se trouve de deux façons : une rangée du bas bien plus large que l'objet (arbres, flèches de roche ; le tronc
# descend jusqu'au milieu du disque), sinon par sa couleur, un gris sombre bleuté atteint depuis le bord (souches : les
# racines noires restent). --sombre (tas de rochers) : un anneau mince au bord, gris, brun ou violacé, et son liseré clair.
# Les cratères (le bord fait partie de l'objet) ne s'y prêtent pas. Seule la bibliothèque standard de Python est utilisée.
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire
SOL = (50, 42, 38)
RAMPE = [0.65, 0.38, 0.30, 0.12, 0.08]
SOMBRE = '--sombre' in sys.argv  # tas de rochers : l'anneau sombre aussi (pas de racines noires à garder)
w, h, px = lire(sys.argv[1])
op = [[px[y][x][3] > 40 for x in range(w)] for y in range(h)]
largeur = [sum(op[y]) for y in range(h)]
rangs = [y for y in range(h) if largeur[y] > 0]
haut, bas = rangs[0], rangs[-1]
H = bas - haut + 1
corps = sorted(largeur[y] for y in range(haut + int(H * 0.35), haut + int(H * 0.7)) if largeur[y] > 0)
w_obj = corps[len(corps) // 2] if corps else 0
# le haut du disque : en partant du milieu, la première rangée bien plus large que l'objet
bande = None
for y in range(haut + int(H * 0.5), bas + 1):
    if largeur[y] > max(1.5 * w_obj, w_obj + 6):
        bande = y; break
def lum(c): return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]
def feu(c): return c[0] > 150 and c[0] > c[1] + 40 and c[0] > c[2] + 80
if bande is None:
    # pas de disque plus large que l'objet (souches, tas de rochers) : le disque se reconnaît à sa couleur, un gris sombre
    # bleuté ou violacé, atteint depuis le bord de l'image dans le bas de l'objet
    limite = haut + int(H * 0.55)
    # distance au vide autour de l'image (8 voisins)
    vide = [[99] * w for _ in range(h)]
    fv = [(x, y) for y in range(h) for x in range(w) if not op[y][x]]
    for x, y in fv: vide[y][x] = 0
    j = 0
    while j < len(fv):
        x, y = fv[j]; j += 1
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and vide[ny][nx] > vide[y][x] + 1:
                    vide[ny][nx] = vide[y][x] + 1; fv.append((nx, ny))
    def disque(x, y):
        c = px[y][x]
        if not (y >= limite and op[y][x]) or feu(c): return False
        if SOMBRE:  # tas de rochers : un anneau mince au bord, gris, brun ou violacé
            dessus = px[y - 1][x] if y > 0 else (0, 0, 0, 0)
            # le liseré clair du disque : au bord, ou sous l'anneau sombre (les rochers clairs n'ont pas de sombre au-dessus)
            pale = lum(c) < 170 and (vide[y][x] <= 1 or vide[y][x] <= 4 and dessus[3] > 40 and lum(dessus) < 90)
            return (vide[y][x] <= 5 and lum(c) < 100 or pale) and max(c[:3]) - min(c[:3]) < 45
        return c[2] >= c[0] - 4 and 42 < lum(c) < 100
    vu = [[False] * w for _ in range(h)]
    file = []
    for y in range(h):
        for x in range(w):
            if disque(x, y) and any(not (0 <= x + dx < w and 0 <= y + dy < h) or not op[y + dy][x + dx]
                                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                vu[y][x] = True; file.append((x, y))
    i = 0
    while i < len(file):
        x, y = file[i]; i += 1
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not vu[ny][nx] and disque(nx, ny):
                vu[ny][nx] = True; file.append((nx, ny))
    if len(file) < 12:
        ecrire(sys.argv[2], px); print('sans disque', sys.argv[1]); sys.exit()
    objet = [[op[y][x] and not vu[y][x] for x in range(w)] for y in range(h)]
    bande = limite
    print('disque par la couleur :', len(file), 'pixels', sys.argv[1])
else:
    objet = None
# l'objet : tout au-dessus de la bande, et ses colonnes prolongées dans la bande
if objet is None:
  objet = [[op[y][x] and y < bande for x in range(w)] for y in range(h)]
  cols = [x for x in range(w) if op[bande - 1][x]]
# le tronc descend jusqu'au milieu du disque (sa rangée la plus large) ; dessous, tout est disque
  milieu = max(range(bande, bas + 1), key=lambda y: largeur[y])
  for y in range(bande, milieu):
    for x in cols:
        if op[y][x]: objet[y][x] = True
# distance (en pixels, 8 voisins) à l'objet
INF = 99
d = [[0 if objet[y][x] else INF for x in range(w)] for y in range(h)]
file = [(x, y) for y in range(h) for x in range(w) if objet[y][x]]
i = 0
while i < len(file):
    x, y = file[i]; i += 1
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and d[ny][nx] > d[y][x] + 1:
                d[ny][nx] = d[y][x] + 1; file.append((nx, ny))
res = [list(r) for r in px]
for y in range(bande, h):
    for x in range(w):
        r, g, b, a = px[y][x]
        if a <= 40 or objet[y][x]: continue
        k = d[y][x] - 1
        f = RAMPE[k] if 0 <= k < len(RAMPE) else 0.0
        if feu((r, g, b)):
            res[y][x] = (r, g, b, round(a * min(1.0, f * 1.4 + 0.15)))
        else:
            l = (0.3 * r + 0.59 * g + 0.11 * b) / 80
            c = tuple(round(min(255, s * (0.85 + 0.3 * min(l, 1.0)))) for s in SOL)
            res[y][x] = c + (round(a * f),)
ecrire(sys.argv[2], res)
print('disque à', bande - haut, '/', H, sys.argv[1])
