"""Sons et musiques d'attente, synthétisés (aucun téléchargement, aucune licence) : de quoi brancher le son du jeu
avant d'avoir les vrais. Écrit des WAV mono dans godot/audio/attente/ (chemins repris par godot/data/sounds.json).

    python -I tools/audio/attente.py

Les musiques sont de simples boucles (arpèges et nappes) qui reviennent au début sans coupure ; les sons, de courts
motifs. Tout est à remplacer par les vrais sons (voir la section « Les sons et la musique » de PLAN.md).
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path

RATE = 22050
OUT = Path(__file__).resolve().parents[2] / "godot" / "audio" / "attente"


def note(name):
    """La fréquence d'une note (« A4 », « C#3 »)."""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    semis = names[name[0]]
    rest = name[1:]
    if rest.startswith("#"):
        semis += 1
        rest = rest[1:]
    elif rest.startswith("b"):
        semis -= 1
        rest = rest[1:]
    return 440.0 * 2 ** ((semis + 12 * (int(rest) - 4)) / 12)


def silence(seconds):
    return [0.0] * int(seconds * RATE)


def add(buffer, start, samples, gain=1.0):
    """Ajoute des échantillons au tampon, à partir de « start » secondes (le tampon s'allonge au besoin)."""
    i0 = int(start * RATE)
    if len(buffer) < i0 + len(samples):
        buffer.extend([0.0] * (i0 + len(samples) - len(buffer)))
    for i, s in enumerate(samples):
        buffer[i0 + i] += s * gain


def tone(freq, seconds, attack=0.005, decay=None, harmonics=(1.0, 0.3, 0.1)):
    """Une note : quelques harmoniques, une attaque, puis une décroissance (pincée) ou un plateau (nappe)."""
    n = int(seconds * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / attack) if attack > 0 else 1.0
        if decay:
            env *= math.exp(-t / decay)
        env *= min(1.0, (n - i) / (0.01 * RATE))  # pas de clic à la fin
        s = sum(h * math.sin(2 * math.pi * freq * (k + 1) * t) for k, h in enumerate(harmonics))
        out.append(env * s)
    return out


def pad(freq, seconds):
    """Une nappe douce : monte et descend lentement."""
    n = int(seconds * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        env = math.sin(math.pi * i / n) ** 2
        s = math.sin(2 * math.pi * freq * t) + 0.25 * math.sin(2 * math.pi * freq * 2.003 * t)
        out.append(env * s)
    return out


def noise(seconds, decay, rng, lowpass=0.5):
    """Un bruit bref (choc, tambour) ; « lowpass » près de 1 l'assourdit."""
    out = []
    last = 0.0
    for i in range(int(seconds * RATE)):
        t = i / RATE
        last = lowpass * last + (1 - lowpass) * rng.uniform(-1, 1)
        out.append(last * math.exp(-t / decay))
    return out


def write(name, samples, peak=0.8):
    top = max(1e-9, max(abs(s) for s in samples))
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / top * peak * 32767)) for s in samples))
    print(f"{name}.wav  {len(samples) / RATE:.1f} s")


def arpeggios(chords, beat, pattern, rng, decay=0.5, pads=True, drums=None):
    """Une boucle : un accord par mesure de 4 temps, arpégé selon « pattern » (indices des notes, par demi-temps)."""
    bar = 4 * beat
    out = silence(bar * len(chords))
    for c, chord in enumerate(chords):
        freqs = [note(n) for n in chord]
        start = c * bar
        if pads:
            for f in freqs[:3]:
                add(out, start, pad(f / 2, bar), 0.12)
        for k, index in enumerate(pattern):
            if index is not None:
                add(out, start + k * beat / 2, tone(freqs[index], beat * 1.5, decay=decay), 0.3)
        if drums:
            for k, kind in enumerate(drums):
                if kind == "x":
                    add(out, start + k * beat / 2, noise(0.25, 0.06, rng, 0.85), 0.9)
                elif kind == "o":
                    add(out, start + k * beat / 2, tone(70, 0.3, decay=0.08, harmonics=(1.0,)), 0.8)
    return out[: int(bar * len(chords) * RATE)]  # la boucle tombe juste (les queues de notes sont coupées)


def main():
    rng = random.Random(42)

    # --- les musiques (des boucles) ---
    write("musique-menu", arpeggios(
        [("A3", "C4", "E4", "A4"), ("F3", "A3", "C4", "F4"), ("C3", "E3", "G3", "C4"), ("G3", "B3", "D4", "G4")],
        beat=0.75, pattern=[0, 2, 1, 3, 2, 1, None, None], rng=rng, decay=0.8))
    write("musique-carte", arpeggios(
        [("C4", "E4", "G4", "C5"), ("A3", "C4", "E4", "A4"), ("F3", "A3", "C4", "F4"), ("G3", "B3", "D4", "G4")],
        beat=0.5, pattern=[0, 1, 2, 3, 2, 1, 2, 1], rng=rng, decay=0.4))
    write("musique-ville", arpeggios(
        [("D4", "F#4", "A4", "D5"), ("G3", "B3", "D4", "G4"), ("E3", "G3", "B3", "E4"), ("A3", "C#4", "E4", "A4")],
        beat=0.55, pattern=[0, 2, 3, 2, 1, 2, 3, None], rng=rng, decay=0.5))
    write("musique-combat", arpeggios(
        [("D3", "F3", "A3", "D4"), ("D3", "F3", "A3", "D4"), ("Bb2", "D3", "F3", "Bb3"), ("C3", "E3", "G3", "C4")],
        beat=0.36, pattern=[0, 0, 2, 0, 3, 0, 2, 1], rng=rng, decay=0.2, pads=False,
        drums=["o", None, "x", None, "o", "o", "x", None]))

    # --- les courts airs ---
    out = silence(0)
    for i, n in enumerate(["C4", "E4", "G4", "C5"]):
        add(out, i * 0.14, tone(note(n), 1.2, decay=0.4), 0.5)
    write("victoire", out)
    out = silence(0)
    for i, n in enumerate(["E4", "C4", "A3", "E3"]):
        add(out, i * 0.3, tone(note(n), 1.4, decay=0.6), 0.5)
    write("defaite", out)
    out = silence(0)
    for i, n in enumerate(["G4", "G4", "C5"]):
        add(out, i * 0.18, tone(note(n), 0.6 if i < 2 else 1.2, decay=0.35, harmonics=(1.0, 0.5, 0.3, 0.15)), 0.5)
    write("nouvelle-semaine", out)
    out = silence(0)
    for i, n in enumerate(["C5", "E5", "G5", "C6"]):
        add(out, i * 0.07, tone(note(n), 0.6, decay=0.2), 0.4)
    write("niveau", out)

    # --- les sons ---
    write("clic", tone(1800, 0.04, attack=0.001, decay=0.008, harmonics=(1.0, 0.2)), peak=0.5)
    write("ramasser", [a + b for a, b in zip(tone(note("E6"), 0.25, decay=0.06) + silence(0.06),
                                             silence(0.06) + tone(note("B6"), 0.25, decay=0.08))], peak=0.5)
    out = silence(0)
    for i in range(3):
        add(out, i * 0.22, noise(0.2, 0.03, rng, 0.6), 1.0)
        add(out, i * 0.22, tone(180, 0.15, decay=0.03, harmonics=(1.0,)), 0.6)
    write("construire", out, peak=0.6)
    out = silence(0)
    for i, n in enumerate(["C5", "E5", "G5"]):
        add(out, 0, tone(note(n), 0.7, decay=0.25), 0.3)
    add(out, 0, noise(0.15, 0.04, rng, 0.3), 0.3)
    write("coffre", out, peak=0.6)
    out = silence(0)
    for i, n in enumerate(["G3", "D4", "G4"]):
        add(out, i * 0.1, tone(note(n), 0.8, decay=0.3, harmonics=(1.0, 0.6, 0.4, 0.2)), 0.4)
    write("prendre", out, peak=0.6)


if __name__ == "__main__":
    sys.exit(main())
