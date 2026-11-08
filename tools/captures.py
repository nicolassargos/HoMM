"""Capture tous les écrans du jeu dans art-candidats/en-cours/revue/, pour une revue avant une PR d'interface.

    python -I tools/captures.py            (compile, puis toutes les captures)
    python -I tools/captures.py ville      (seulement celles dont le nom contient « ville »)

Chaque capture lance Godot avec une graine fixe (42) et les options de développement, enregistre l'image puis quitte.
Une capture ratée est signalée (l'ancienne image est effacée avant, pour ne pas la confondre avec une nouvelle).
"""
import os
import subprocess
import sys
import time

RACINE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GODOT = r"C:\Godot\Godot_v4.7.2-stable_mono_win64_console.exe"
DOSSIER = os.path.join(RACINE, "art-candidats", "en-cours", "revue")

# nom de l'image, options du jeu (après « -- »)
ECRANS = [
    ("01-menu", ["--menu"]),
    ("02-nouvelle-partie", ["--menu=new"]),
    ("02b-nouvelle-partie-liste", ["--menu=new", "--dropdown-open"]),
    ("03-charger", ["--menu=load"]),
    ("04-carte", ["--play"]),
    ("05-carte-adversaire", ["--opponents=1", "--turns=10"]),
    ("06-heros", ["--spells", "--open=hero"]),
    ("07-royaume", ["--open=kingdom"]),
    ("08-livre-de-sorts", ["--spells", "--open=spellbook"]),
    ("09-niveau", ["--open=level"]),
    ("09b-coffre", ["--open=chest"]),
    ("04b-carte-lieux", ["--reveal", "--center=sites"]),
    ("10-options", ["--open=options"]),
    ("11-sauvegarder", ["--open=save"]),
    ("12-fin-de-partie", ["--open=end"]),
    ("13-carte-au-tresor", ["--treasure-map"]),
    ("14-ville", ["--town"]),
    ("15-ville-construire", ["--town", "--town-window=build"]),
    ("16-ville-recruter", ["--town", "--town-window=recruit"]),
    ("17-ville-nombre", ["--town", "--town-window=count"]),
    ("18-ville-taverne", ["--town", "--town-window=tavern"]),
    ("19-ville-guilde", ["--town", "--town-window=guild"]),
    ("20-combat", ["--battle-test", "--capture-delay=2"]),
    ("21-combat-livre", ["--spells", "--battle-test", "--battle-book", "--capture-delay=2"]),
    ("22-combat-cible", ["--spells", "--battle-test", "--battle-book=boule-de-feu", "--capture-delay=2"]),
    ("23-combat-vitrine", ["--battle-showcase", "--capture-delay=2"]),
    ("24-defense", ["--opponents=1", "--defense-test", "--capture-delay=3"]),
    ("25-defense-combat", ["--opponents=1", "--defense-test", "--defense-tactical", "--capture-delay=3"]),
]


def main():
    filtre = sys.argv[1] if len(sys.argv) > 1 else ""
    ecrans = [(nom, options) for nom, options in ECRANS if filtre in nom]
    if not ecrans:
        sys.exit(f"Aucun écran ne contient « {filtre} ».")
    if not os.path.exists(GODOT):
        sys.exit(f"Godot introuvable : {GODOT}")
    print("Compilation…")
    compile_ = subprocess.run(["dotnet", "build"], cwd=os.path.join(RACINE, "godot"), capture_output=True, text=True)
    if compile_.returncode != 0:
        sys.exit("La compilation a échoué :\n" + compile_.stdout[-2000:])
    os.makedirs(DOSSIER, exist_ok=True)
    rates = []
    debut = time.time()
    for nom, options in ecrans:
        image = os.path.join(DOSSIER, nom + ".png")
        if os.path.exists(image):
            os.remove(image)
        commande = [GODOT, "--path", ".", "--", "--seed=42", *options, f"--capture={image}"]
        resultat = subprocess.run(commande, cwd=os.path.join(RACINE, "godot"), capture_output=True, text=True,
                                  encoding="utf-8", errors="replace", timeout=120)
        ok = os.path.exists(image) and "Image enregistrée" in resultat.stdout
        print(("  ok    " if ok else "  RATÉE ") + nom)
        if not ok:
            rates.append(nom)
    print(f"{len(ecrans) - len(rates)} captures sur {len(ecrans)} en {time.time() - debut:.0f} s, dans {DOSSIER}")
    if rates:
        sys.exit("Ratées : " + ", ".join(rates))


if __name__ == "__main__":
    main()
