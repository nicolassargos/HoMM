# Carte d'aventure – Godot 4.7 + C#

Version Godot de la carte d'aventure. **Tout est construit par le code C#** : carte, dessin, interface.
L'éditeur Godot n'est pas nécessaire pour développer, seulement pour lancer le jeu si on le souhaite.

![Aperçu](apercu.png)

## Installer (Windows, une seule fois)

1. **.NET SDK 8** (ou plus récent) : https://dotnet.microsoft.com/download
2. **Godot 4.7.2, édition .NET** : https://godotengine.org/download/windows/
   (choisir « Godot Engine – .NET »). C'est un zip sans installation : décompressez-le par exemple dans `C:\Godot\`.

## Lancer le jeu

**Avec le script** : double-cliquez sur `godot\lancer.bat`.
Si Godot n'est pas dans `C:\Godot\Godot_v4.7.2-stable_mono_win64\`, modifiez la ligne `set GODOT=...` du script.

**À la main**, dans un terminal ouvert dans le dossier `godot` :

```
dotnet build
C:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe --path .
```

**Avec l'éditeur Godot** : ouvrez Godot, « Importer », choisissez `godot/project.godot`, puis appuyez sur F5.

Pour toujours obtenir la même carte, ajoutez une graine : `lancer.bat -- --seed=42`.

## Commandes

| Action | Commande |
|---|---|
| Tracer un chemin | Clic gauche sur une case |
| Partir | Second clic sur la même case, ou Espace |
| Annuler le chemin | Clic droit ou Échap |
| Faire défiler la carte | Flèches, ZQSD, souris au bord de l'écran, clic sur la mini-carte |
| Recentrer sur le héros | C |
| Fin du tour | Bouton « Fin du tour » ou Entrée |

Points verts : cases atteignables aujourd'hui. Points rouges : il faudra les jours suivants.

## Organisation du code

Le code est séparé en deux parties :

- `scripts/Core/` : **les règles du jeu en C# pur**, sans aucune dépendance à Godot.
  On peut les tester sans lancer le jeu, et les réutiliser ailleurs (générateur de cartes, serveur…).
  - `MapGenerator.cs` : génération aléatoire de la carte (graine = carte reproductible)
  - `AdventureMap.cs` : terrain, objets, brouillard de guerre
  - `Pathfinder.cs` : recherche du chemin (A*) avec coûts de terrain
  - `MapObjects.cs` : tas de ressources, coffres, mines
  - `Player.cs`, `Hero.cs` : trésor, mines, points de mouvement
  - `GameState.cs` : la partie (calendrier, fin de tour, déplacement le long du chemin, journal)
- `scripts/Game/` : **l'affichage et les commandes avec Godot**
  - `Main.cs` : point d'entrée, caméra, souris et clavier
  - `TerrainLayer.cs`, `ActorLayer.cs`, `FogLayer.cs` : les calques de la carte
  - `MapRenderer.cs` : dessin des tuiles, objets et du héros (à remplacer plus tard par des images)
  - `Hud.cs`, `Minimap.cs`, `ResourceIcon.cs` : l'interface
- `scenes/Main.tscn` : la seule scène, un nœud vide qui porte `Main.cs`.

Les tests des règles sont dans `tests/HoMM.Core.Tests` (à la racine du dépôt) : `dotnet test`.
La solution `HoMM.sln` à la racine s'ouvre dans Visual Studio ou Rider.
