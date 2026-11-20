"""Préparer des ambiances à jouer en boucle : même force pour toutes, et une fin qui se fond dans le début.

    python boucler.py godot/audio/ambiance/*.ogg

Remplace chaque fichier (une seule fois, après `musiques.py ambiances installer` : relancé, il refondrait la fin) :
- coupe le silence ou le fondu du début et de la fin (sous la force médiane moins 12 dB) ;
- fond les 3 dernières secondes dans les 3 premières (gain constant) : la fin mène droit au début ;
- règle la force moyenne à CIBLE dB, et un limiteur garde les crêtes sous CRETE dB (craquements du feu) ;
- réécrit en .ogg Vorbis, un peu plus compressé (les ambiances sont longues).

Demande numpy et soundfile (`pip install numpy soundfile`), absents du Python ordinaire : les installer dans un
environnement à part.
"""
import os
import sys

import numpy as np
import soundfile as sf

CIBLE = -30.0     # force moyenne (RMS) en dB : sous la musique, au fond
CRETE = -3.0      # crête au plus, en dB
FONDU = 3.0       # secondes fondues de la fin dans le début
COMPRESSION = 0.6  # Vorbis : 0 = meilleure qualité, 1 = plus petit


def db(x):
    return 20 * np.log10(x + 1e-12)


def couper_bords(a, sr):
    """Retire au début et à la fin les morceaux de 100 ms bien plus faibles que la médiane (silence, fondu)."""
    w = sr // 10
    n = len(a) // w
    forces = db(np.sqrt((a[: n * w].reshape(n, w, -1) ** 2).mean(axis=(1, 2))))
    seuil = np.median(forces) - 12
    fortes = np.nonzero(forces >= seuil)[0]
    return a[fortes[0] * w: (fortes[-1] + 1) * w]


def fondre(a, sr):
    """La fin se fond dans le début : le fichier joué en boucle ne saute plus."""
    x = int(FONDU * sr)
    t = np.linspace(0, np.pi / 2, x)[:, None]
    sortie = a[x:].copy()
    sortie[-x:] = a[-x:] * np.cos(t) + a[:x] * np.sin(t)
    return sortie


def limiter(a, sr):
    """Baisse le gain là où les crêtes dépassent CRETE (par tranches de 5 ms, adouci sur 50 ms)."""
    seuil = 10 ** (CRETE / 20)
    w = sr // 200
    n = -(-len(a) // w)
    bloc = np.pad(np.abs(a), ((0, n * w - len(a)), (0, 0))).reshape(n, w, -1).max(axis=(1, 2))
    gain = np.minimum(1.0, seuil / np.maximum(bloc, 1e-9))
    # chaque tranche prend le gain le plus bas de ses voisines (le limiteur agit un peu avant la crête) …
    r = 10
    gain = np.min(np.stack([np.roll(np.pad(gain, r, mode="edge"), -k)[r: r + n] for k in range(-r, r + 1)]), axis=0)
    # … puis le gain passe en douceur d'une tranche à l'autre
    gain = np.convolve(np.pad(gain, r, mode="edge"), np.ones(2 * r + 1) / (2 * r + 1), mode="same")[r: r + n]
    par_echantillon = np.interp(np.arange(len(a)), np.arange(n) * w + w / 2, gain)
    return np.clip(a * par_echantillon[:, None], -seuil, seuil)


def regler(a, sr):
    rms = np.sqrt((a ** 2).mean())
    a = a * 10 ** ((CIBLE - db(rms)) / 20)
    return limiter(a, sr)


def main():
    for chemin in sys.argv[1:]:
        a, sr = sf.read(chemin, always_2d=True)
        avant = len(a) / sr
        a = regler(fondre(couper_bords(a, sr), sr), sr)
        # par morceaux (libsndfile plante sur un long .ogg écrit d'un coup), dans un fichier à part, remplacé à la fin
        provisoire = chemin + ".tmp"
        with sf.SoundFile(provisoire, "w", sr, a.shape[1], format="OGG", subtype="VORBIS",
                          compression_level=COMPRESSION) as f:
            for debut in range(0, len(a), sr):
                f.write(a[debut: debut + sr])
        if sf.info(provisoire).frames != len(a):
            os.remove(provisoire)
            sys.exit(f"{chemin} : écriture ratée, fichier gardé tel quel")
        os.replace(provisoire, chemin)
        print(f"{chemin} : {avant:.1f} s → {len(a) / sr:.1f} s, force {db(np.sqrt((a ** 2).mean())):.1f} dB, "
              f"crête {db(np.abs(a).max()):.1f} dB")


if __name__ == "__main__":
    main()
