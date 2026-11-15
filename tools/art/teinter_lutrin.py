# Baigner le lutrin d'une scène dans la lumière de la pièce (choix de l'utilisateur, 2026-11-14 : un lutrin brun et une
# lueur jaune juraient dans une crypte verte ou une caverne violette).
# Le lutrin est à la place où la disposition l'avait collé (en bas au milieu, 4 pixels au-dessus du bord) : sa forme vient
# de l'image du lutrin, élargie de quelques pixels et adoucie au bord (le lutrin généré n'est pas tout à fait le même).
# Chaque pixel du lutrin est multiplié par la couleur de la lumière, éclairé par elle, puis mêlé à lui-même selon la force.
#   python -I tools/art/teinter_lutrin.py <scène.png> <lutrin.png> <r,g,b> <force 0-1> <sortie.png>
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire

scene, lutrin, couleur, force, sortie = sys.argv[1:6]
lumiere = [int(v) for v in couleur.split(",")]
force = float(force)
W, H, px = lire(scene)
lw, lh, lec = lire(lutrin)
x0, y0 = (W - lw) // 2, H - 4 - lh

# la forme : le lutrin, élargie de MARGE pixels, avec un bord adouci sur la dernière moitié
MARGE = 2
# la part de la lumière ajoutée par-dessus (« écran »)
ECLAT = 0.8
forme = [[0.0] * W for _ in range(H)]
pleins = [(x0 + x, y0 + y) for y in range(lh) for x in range(lw) if lec[y][x][3] > 0]
for X, Y in pleins:
    for dy in range(-MARGE, MARGE + 1):
        for dx in range(-MARGE, MARGE + 1):
            d = max(abs(dx), abs(dy))
            v = 1.0 if d <= MARGE // 2 else 1 - (d - MARGE // 2) / (MARGE - MARGE // 2 + 1)
            x, y = X + dx, Y + dy
            if 0 <= x < W and 0 <= y < H and v > forme[y][x]:
                forme[y][x] = v

# la lumière ramenée à son plus fort canal à 255 : elle teinte sans assombrir
m = max(lumiere)
teinte = [v * 255 / m for v in lumiere]
for y in range(H):
    for x in range(W):
        k = forme[y][x] * force
        if k <= 0:
            continue
        r, g, b, a = px[y][x]
        # teinté (multiplié), puis éclairé (« écran ») : la lumière de la pièce tombe sur le lutrin
        t = [c * l / 255 for c, l in zip((r, g, b), teinte)]
        # les faces éclairées prennent la lumière, les ombres restent sombres
        e = ECLAT * (0.299 * r + 0.587 * g + 0.114 * b) / 255
        t = [255 - (255 - c) * (255 - l * e) / 255 for c, l in zip(t, lumiere)]
        px[y][x] = tuple(round(c * (1 - k) + n * k) for c, n in zip((r, g, b), t)) + (a,)
ecrire(sortie, px)
