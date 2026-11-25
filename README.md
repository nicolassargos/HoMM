# HoMM

Jeu PC fortement inspiré de Heroes of Might and Magic et King's Bounty.

## Jouer sous Windows

Téléchargez le dernier fichier `HoMM-windows-x64-*.zip` dans les
[versions publiées](https://github.com/nicolassargos/HoMM/releases), décompressez-le
puis lancez `HoMM.exe`. Godot et le SDK .NET ne sont nécessaires que pour développer.

## Contenu du dépôt

- `godot/` : **le jeu**, en Godot 4.7 + C#. Tout est construit par le code, cartes générées automatiquement.
  Voir `godot/LISEZMOI.md` pour l'installer et le lancer.
- `tests/HoMM.Core.Tests/` : tests des règles du jeu (`dotnet test` à la racine).
- `PLAN.md` : la vision du jeu et les prochaines étapes.
- `HoMM.sln` : solution à ouvrir dans Visual Studio ou Rider.
- `prototype-python/` : le tout premier prototype de la carte d'aventure (Python + pygame-ce), gardé pour mémoire.
