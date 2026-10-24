# HoMM – notes pour Claude

Jeu PC fortement inspiré de Heroes of Might and Magic / King's Bounty. L'utilisateur est francophone : répondre en français, textes du jeu en français.

## Plan
La vision du jeu et les étapes prévues sont dans `PLAN.md` : le lire avant de commencer une nouvelle fonctionnalité, et le mettre à jour quand les idées changent.

## Choix techniques
- Godot 4.7.2 **édition .NET** + C# (.NET 8). Le projet est dans `godot/`.
- Utiliser l'éditeur Godot le moins possible : tout est construit par le code (scène unique `scenes/Main.tscn`).
- Les cartes doivent, à terme, être générées automatiquement (`MapGenerator`).
- Code : identifiants en anglais, commentaires et textes affichés en français.

## Organisation
- `godot/scripts/Core/` : règles en C# pur, **sans dépendance à Godot** (testables). Y mettre toute la logique de jeu.
- `godot/scripts/Game/` : affichage, interface, commandes (Godot). Une classe Godot par fichier, nom du fichier = nom de la classe.
- `tests/HoMM.Core.Tests/` : tests xUnit des règles.
- `prototype-python/` : ancien prototype, gardé pour mémoire, ne plus le faire évoluer.

## Commandes
- Tests : `dotnet test` (à la racine)
- Compiler : `dotnet build` (dans `godot/`)
- Lancer : `godot\lancer.bat` (Godot attendu dans `C:\Godot\`), option `-- --seed=42` pour une carte fixe
