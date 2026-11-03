"""Réduit une image de pixel art sans la rendre floue : moyenne des pixels, puis chaque couleur
ramenée à la plus proche de l'image d'origine (la palette ne change pas).

    python -I tools/art/reduire.py source.png sortie.png facteur [largeur hauteur] [--miroir]

Avec largeur et hauteur, l'image réduite est posée en bas et au centre d'un cadre de cette taille.
Avec --miroir, l'image est retournée de gauche à droite (une créature tournée vers la gauche regarde à droite).
Seule la bibliothèque standard de Python est utilisée.
"""
import os, struct, sys, zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


def lire(chemin):
    d = open(chemin, "rb").read(); i = 8; idat = b""
    while i < len(d):
        n, t = struct.unpack(">I4s", d[i:i + 8]); c = d[i + 8:i + 8 + n]; i += 12 + n
        if t == b"IHDR": w, h, bd, ct = struct.unpack(">IIBB", c[:10])
        elif t == b"IDAT": idat += c
    if bd != 8 or ct not in (2, 6):
        sys.exit("RGB ou RGBA 8 bits attendu")
    bpp = 4 if ct == 6 else 3; raw = zlib.decompress(idat); px = []; prec = bytearray(w * bpp); k = 0
    for _ in range(h):
        f = raw[k]; k += 1; l = bytearray(raw[k:k + w * bpp]); k += w * bpp
        for x in range(len(l)):
            a = l[x - bpp] if x >= bpp else 0; b = prec[x]; cc = prec[x - bpp] if x >= bpp else 0
            if f == 1: l[x] = (l[x] + a) & 255
            elif f == 2: l[x] = (l[x] + b) & 255
            elif f == 3: l[x] = (l[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - cc; pa, pb, pc = abs(p - a), abs(p - b), abs(p - cc)
                l[x] = (l[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else cc)) & 255
        px.append([tuple(l[x * bpp:x * bpp + 3]) + ((l[x * bpp + 3],) if bpp == 4 else (255,)) for x in range(w)])
        prec = l
    return w, h, px


def ecrire(chemin, px):
    h, w = len(px), len(px[0])
    raw = b"".join(b"\x00" + bytes(c for p in r for c in p) for r in px)
    def bloc(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
    open(chemin, "wb").write(b"\x89PNG\r\n\x1a\n" + bloc(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                             + bloc(b"IDAT", zlib.compress(raw)) + bloc(b"IEND", b""))


def reduire(px, facteur):
    h, w = len(px), len(px[0])
    nw, nh = max(1, round(w * facteur)), max(1, round(h * facteur))
    palette = sorted({p[:3] for r in px for p in r if p[3] > 0})
    out = []
    for y in range(nh):
        ligne = []
        for x in range(nw):
            x0, x1 = x / facteur, (x + 1) / facteur; y0, y1 = y / facteur, (y + 1) / facteur
            s = [0.0] * 3; opaque = total = 0.0
            for sy in range(int(y0), min(h, int(y1 + 0.999))):
                for sx in range(int(x0), min(w, int(x1 + 0.999))):
                    # part du pixel source couverte par le pixel réduit
                    a = (min(x1, sx + 1) - max(x0, sx)) * (min(y1, sy + 1) - max(y0, sy))
                    if a <= 0: continue
                    total += a; p = px[sy][sx]
                    if p[3] > 0:
                        opaque += a
                        for i in range(3): s[i] += p[i] * a
            if opaque < total * 0.5:
                ligne.append((0, 0, 0, 0)); continue
            moy = [v / opaque for v in s]
            c = min(palette, key=lambda q: sum((q[i] - moy[i]) ** 2 for i in range(3)))
            ligne.append(c + (255,))
        out.append(ligne)
    return out


if __name__ == "__main__":
    miroir = "--miroir" in sys.argv
    args = [a for a in sys.argv if a != "--miroir"]
    sys.argv = args
    src, dst, f = sys.argv[1], sys.argv[2], float(sys.argv[3])
    _, _, px = lire(src)
    if miroir:
        px = [list(reversed(r)) for r in px]
    out = reduire(px, f)
    if len(sys.argv) > 5:
        fw, fh = int(sys.argv[4]), int(sys.argv[5])
        # le bas de la créature (dernière rangée non vide) reste à la même distance du bas
        bas = max(y for y, r in enumerate(px) for p in r if p[3] > 0)
        marge = round((len(px) - 1 - bas) * f)
        cadre = [[(0, 0, 0, 0)] * fw for _ in range(fh)]
        ox = (fw - len(out[0])) // 2; oy = fh - marge - 1 - max(y for y, r in enumerate(out) for p in r if p[3] > 0)
        for y, r in enumerate(out):
            for x, p in enumerate(r):
                if 0 <= oy + y < fh and 0 <= ox + x < fw and p[3]:
                    cadre[oy + y][ox + x] = p
        out = cadre
    ecrire(dst, out)
