"""Range les propositions d'images une fois la décision prise.

    python -I tools/art/archiver.py sujet

Tout ce qui est dans art-candidats/en-cours/ (captures, pages de comparaison, candidats générés) part dans
art-candidats/archives/AAAA-MM-JJ-sujet/. Le dossier en-cours/ ne garde ainsi que ce qui attend une réponse.
Si le dossier d'archive existe déjà, les fichiers s'y ajoutent (un fichier du même nom est remplacé).
Seule la bibliothèque standard de Python est utilisée.
"""

import datetime
import os
import shutil
import sys

ICI = os.path.dirname(os.path.abspath(__file__))
RACINE = os.path.normpath(os.path.join(ICI, "..", ".."))
EN_COURS = os.path.join(RACINE, "art-candidats", "en-cours")
ARCHIVES = os.path.join(RACINE, "art-candidats", "archives")


def deplacer(source, cible):
    """Déplace un fichier ou un dossier, en fusionnant les dossiers qui existent déjà."""
    if os.path.isdir(source):
        os.makedirs(cible, exist_ok=True)
        for nom in os.listdir(source):
            deplacer(os.path.join(source, nom), os.path.join(cible, nom))
        os.rmdir(source)
    else:
        os.makedirs(os.path.dirname(cible), exist_ok=True)
        if os.path.exists(cible):
            os.remove(cible)
        shutil.move(source, cible)


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    sujet = sys.argv[1].strip().replace(" ", "-")
    if not os.path.isdir(EN_COURS) or not os.listdir(EN_COURS):
        sys.exit("Rien à archiver : art-candidats/en-cours/ est vide.")
    dossier = os.path.join(ARCHIVES, f"{datetime.date.today():%Y-%m-%d}-{sujet}")
    for nom in os.listdir(EN_COURS):
        deplacer(os.path.join(EN_COURS, nom), os.path.join(dossier, nom))
    print(f"Archivé dans {os.path.relpath(dossier, RACINE)}")


if __name__ == "__main__":
    main()
