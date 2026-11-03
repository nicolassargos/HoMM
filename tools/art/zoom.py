"""Agrandit une partie d'une capture d'écran, pixels nets (pour regarder les détails).

    python -I tools/art/zoom.py source.png sortie.png x0 y0 x1 y1 facteur

Seule la bibliothèque standard de Python est utilisée.
"""
import struct, sys, zlib

def read(path):
    d = open(path, 'rb').read(); i = 8; idat = b''; pal = None
    while i < len(d):
        n, t = struct.unpack('>I4s', d[i:i+8]); c = d[i+8:i+8+n]; i += 12 + n
        if t == b'IHDR': w, h, bd, ct = struct.unpack('>IIBB', c[:10])
        elif t == b'IDAT': idat += c
    bpp = {6: 4, 2: 3, 0: 1, 4: 2}[ct]; raw = zlib.decompress(idat); rows = []; prev = bytearray(w * bpp); k = 0
    for y in range(h):
        f = raw[k]; k += 1; line = bytearray(raw[k:k + w * bpp]); k += w * bpp
        for x in range(len(line)):
            a = line[x - bpp] if x >= bpp else 0; b = prev[x]; cc = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - cc; pa, pb, pc = abs(p - a), abs(p - b), abs(p - cc)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else cc)) & 255
        rows.append([tuple(line[x * bpp:(x + 1) * bpp]) for x in range(w)]); prev = line
    return w, h, rows

src, out = sys.argv[1], sys.argv[2]; x0, y0, x1, y1, k = map(int, sys.argv[3:8])
w, h, rows = read(src); data = b''
for y in range(y0, y1):
    row = b''.join(bytes(rows[y][x][:3]) * k for x in range(x0, x1)); data += (b'\x00' + row) * k
W, H = (x1 - x0) * k, (y1 - y0) * k
def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d))
open(out, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(data)) + chunk(b'IEND', b''))
