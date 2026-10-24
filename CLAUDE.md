# HoMM – notes pour Claude

Jeu PC fortement inspiré de Heroes of Might and Magic / King's Bounty. L'utilisateur est francophone : répondre en français, textes du jeu en français.

## Plan
La vision du jeu et les étapes prévues sont dans `PLAN.md`, importé ci-dessous. Le suivre pour chaque nouvelle fonctionnalité, et le mettre à jour quand les idées changent ou qu'une étape avance (cocher ✅, noter les décisions prises).

@PLAN.md

## Git
- Une branche par étape ou par sujet, créée depuis `main` à jour (`git switch main`, `git pull`, `git switch -c <nom>`). Noms en minuscules avec tirets (ex. `etape-2-villes`).
- Une PR de la branche **directement vers `main`** : pas de PR empilée sur une autre branche.
- Avant de proposer une PR : `dotnet test` réussi et `dotnet build` (dans `godot/`) sans erreur.
- Messages de commit et descriptions de PR en français.
- Après la fusion : revenir sur `main` à jour et supprimer la branche (locale et sur GitHub).
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

## Commandes
- Tests : `dotnet test` (à la racine)
- Compiler : `dotnet build` (dans `godot/`)
- Lancer : `godot\lancer.bat` (Godot attendu dans `C:\Godot\`), option `-- --seed=42` pour une carte fixe
