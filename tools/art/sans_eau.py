"""L'eau peinte au bas d'une image générée (le ruisseau sous une roue à eau), retirée : le ruisseau de la carte passe dessous.

    python -I tools/art/sans_eau.py <dossier des candidats> <dossier de sortie>

Pour chaque PNG (sauf planche.png) : les pixels d'eau (bleus, ou d'écume blanche) reliés au bas de l'image sont effacés
(pas l'eau de la goulotte, en haut), puis le bord sombre de ce ruisseau (des traits d'un pixel, vides dessus et dessous)
et les petits morceaux détachés ; l'image est coupée à son dessin. Avec Pillow.
"""
import sys, os
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
os.makedirs(dst, exist_ok=True)
for name in sorted(os.listdir(src)):
    if not name.endswith('.png') or name == 'planche.png':
        continue
    im = Image.open(os.path.join(src, name)).convert('RGBA'); w, h = im.size; px = im.load()
    def water(p):
        r, g, b, a = p
        return a > 0 and ((b > r + 40 and b > g + 10) or (r > 200 and g > 215 and b > 225))
    # l'eau touche le bas : on la suit depuis les pixels d'eau des rangées du bas (pas l'eau de la goulotte, en haut)
    seen = set(); todo = [(x, y) for y in range(int(h * 0.55), h) for x in range(w) if water(px[x, y])]
    todo = [(x, y) for (x, y) in todo if y > h * 0.8 or any(water(px[x, yy]) for yy in range(y, h))]
    while todo:
        x, y = todo.pop()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h) or y < h * 0.55 or not water(px[x, y]):
            continue
        seen.add((x, y)); todo += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    for x, y in seen:
        px[x, y] = (0, 0, 0, 0)
    # le bord sombre du ruisseau peint : des traits d'un pixel d'épaisseur, vides dessus et dessous
    clear = lambda x, y: not (0 <= x < w and 0 <= y < h) or px[x, y][3] == 0
    for _ in range(3):
        thin = [(x, y) for y in range(int(h * 0.5), h) for x in range(w)
                if not clear(x, y) and clear(x, y - 1) and clear(x, y + 1)]
        for x, y in thin:
            px[x, y] = (0, 0, 0, 0)
    # puis les petits morceaux détachés
    seen2 = set()
    for y0 in range(h):
        for x0 in range(w):
            if clear(x0, y0) or (x0, y0) in seen2:
                continue
            part, todo = [], [(x0, y0)]
            while todo:
                x, y = todo.pop()
                if (x, y) in seen2 or clear(x, y):
                    continue
                seen2.add((x, y)); part.append((x, y))
                todo += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
            if len(part) < 40:
                for x, y in part:
                    px[x, y] = (0, 0, 0, 0)
    box = im.getbbox()
    im.crop(box).save(os.path.join(dst, name.replace('.png', '') + '.png'))
    print(name, im.crop(box).size)
