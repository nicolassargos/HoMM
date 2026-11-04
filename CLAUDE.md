# HoMM – notes pour Claude

Jeu PC fortement inspiré de Heroes of Might and Magic / King's Bounty. L'utilisateur est francophone mais préfère échanger en anglais (un anglais simple et clair). Les textes du jeu, les commentaires, la documentation, les commits et les PR restent en français.

## Plan
La vision du jeu et les étapes prévues sont dans `PLAN.md`, importé ci-dessous. Le suivre pour chaque nouvelle fonctionnalité, et le mettre à jour quand les idées changent ou qu'une étape avance (cocher ✅, noter les décisions prises).

@PLAN.md

## Git
- Une branche par étape ou par sujet, créée depuis `main` à jour (`git switch main`, `git pull`, `git switch -c <nom>`). Noms en minuscules avec tirets (ex. `etape-2-villes`).
- Une PR de la branche **directement vers `main`** : pas de PR empilée sur une autre branche.
- Avant de proposer une PR : `dotnet test` réussi et `dotnet build` (dans `godot/`) sans erreur.
- Messages de commit et descriptions de PR en français.
- Après la fusion (faite par l'utilisateur) : revenir sur `main` et récupérer les changements (`git switch main`, puis `git pull`), avant toute nouvelle branche. L'utilisateur supprime lui-même les branches fusionnées.
- Ne pas fusionner une PR ni supprimer de branche sans l'accord de l'utilisateur.

## Choix techniques
- Godot 4.7.2 **édition .NET** + C# (.NET 8). Le projet est dans `godot/`.
- Utiliser l'éditeur Godot le moins possible : tout est construit par le code (scène unique `scenes/Main.tscn`).
- Les cartes doivent, à terme, être générées automatiquement (`MapGenerator`).
- Code : identifiants en anglais, commentaires et textes affichés en français.

## Organisation
- `godot/scripts/Core/` : règles en C# pur, **sans dépendance à Godot** (testables). Y mettre toute la logique de jeu.
- Règles → affichage : `GameState` ajoute des événements (type de base dans `GameEvents.cs`, chaque événement défini dans le fichier de ce qu'il concerne) que `Main` lit avec `TakeEvents()` et montre à son rythme. L'affichage ne modifie l'état qu'en appelant les méthodes de `GameState`. Toute nouvelle action visible (animation, fenêtre, son) passe par un événement. Les règles ne produisent aucun texte affiché : tout texte va dans `Game/Texts.cs`.
- `godot/scripts/Game/` : affichage, interface, commandes (Godot). Une classe Godot par fichier, nom du fichier = nom de la classe.
- `tests/HoMM.Core.Tests/` : tests xUnit des règles.
- `prototype-python/` : ancien prototype, gardé pour mémoire, ne plus le faire évoluer.

## Services d'images (décision de l'utilisateur, 2026-11-04)
- **PixelLab** (`tools/art/pixellab.py`, clé dans la variable `PIXELLAB_API_KEY`) : les **personnages et créatures**
  (images de combat, animations), et des essais pour l'**interface**.
- **Retro Diffusion** (`tools/art/generer.py`, clé dans `RETRO_DIFFUSION_API_KEY`) : les **cartes** (terrains,
  décors, objets) et les **portraits** des héros humains.
- Ne jamais afficher ni écrire les clés. Montrer le prix avant de dépenser (Retro Diffusion : `cout`, gratuit ;
  PixelLab n'a pas d'estimation gratuite : `solde` avant et après) et attendre l'accord de l'utilisateur.

## Propositions d'images (dossier `art-candidats/`, non suivi par git)
Décision de l'utilisateur, valable pour toutes les sessions :
- `art-candidats/en-cours/` ne contient **que ce qui attend une réponse de l'utilisateur** : captures d'écran
  de comparaison (`jeu-*.png`), agrandissements (`zoom-*.png`), pages de choix, candidats générés pas encore
  choisis (`en-cours/candidats/<nom>/`, écrits par `tools/art/generer.py`).
- Une fois la décision prise, tout part dans `art-candidats/archives/AAAA-MM-JJ-sujet/` avec
  `python -I tools/art/archiver.py sujet`. Ne rien laisser traîner à la racine de `art-candidats/`.
- `art-candidats/references/` : images données par l'utilisateur (modèles, captures), à garder telles quelles.
- Outils : `tools/art/generer.py` (prix, génération, choix ; `choisir` retrouve aussi un candidat archivé),
  `tools/art/apercu.py` (page de choix dans `en-cours/`), `tools/art/zoom.py` (agrandir une capture),
  `tools/art/reduire.py` (réduire ou retourner une image sans la flouter), `tools/art/detourer.py` (rendre
  transparent un fond uni), `tools/art/pixellab.py` (PixelLab : solde, animations, éléments d'interface).
  Captures du jeu : `godot` avec `-- --capture=chemin.png` (et `--reveal`, `--zoom`, `--terrain-test`,
  `--town`, `--rules`, `--center=treasure` ou `obelisk`, `--treasure-map`, `--dig-test`, `--battle-test` (et `--battle-auto`), `--battle-showcase`, `--capture-delay=secondes`, `--cursor-preview=dossier`, `--ground=réglage:valeur` selon le besoin).

## Commandes
- Tests : `dotnet test` (à la racine)
- Compiler : `dotnet build` (dans `godot/`)
- Lancer : `godot\lancer.bat` (Godot attendu dans `C:\Godot\`), option `-- --seed=42` pour une carte fixe, `-- --load` pour reprendre la partie sauvegardée
