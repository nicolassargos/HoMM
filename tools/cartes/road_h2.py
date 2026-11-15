"""Les routes d'une carte de Heroes 2, comme fheroes2 : une image du fond de la série ROAD (30) parmi les bonnes, ou
certaines images des villes (35 : 13, 29… ; 38 : 13, 29)."""
import struct
ROAD_OK = {0, 2, 3, 4, 5, 6, 7, 9, 12, 13, 14, 16, 17, 18, 19, 20, 21, 26, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
           41, 42, 43, 44, 45, 46, 47, 48}
TOWN_OK = {13 + 16 * k for k in range(12)}


def is_road(icn, index):
    return icn == 30 and index in ROAD_OK or icn == 35 and index in TOWN_OK or icn == 38 and index in (13, 29)


def read(path):
    d = open(path, 'rb').read()
    w, h = struct.unpack_from('<II', d, 0x1A4)
    if len(d) < 432 + w * h * 20:
        return None
    T = [struct.unpack_from('<HBBBBBBBBHII', d, 428 + i * 20) for i in range(w * h)]
    k = 428 + w * h * 20; n = struct.unpack_from('<I', d, k)[0]; k += 4
    A = [struct.unpack_from('<HBBBBBII', d, k + i * 15) for i in range(n)]
    road = set()
    for i, t in enumerate(T):
        if is_road(t[1] >> 2, t[2]) or is_road(t[5] >> 2, t[6]):
            road.add(i)
        a = t[9]; s = 0
        while a and a < len(A) and s < 50:
            ad = A[a]
            if is_road(ad[1] >> 2, ad[2]) or is_road(ad[4] >> 2, ad[5]):
                road.add(i)
            a = ad[0]; s += 1
    water = {i for i, t in enumerate(T) if t[0] < 30}
    return w, h, T, road, water
