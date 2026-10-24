# Carte d'aventure (prototype)

Premier prototype jouable de la carte d'aventure, inspiré de Heroes of Might and Magic / King's Bounty.
Écrit en **Python** avec la bibliothèque **pygame-ce** (version communautaire de pygame). Aucune image externe : tout est dessiné par le code.

## Lancer le jeu sur Windows

1. Installer Python 3.10 ou plus récent depuis https://www.python.org/downloads/
   (cocher **« Add python.exe to PATH »** pendant l'installation).
2. Double-cliquer sur `lancer.bat`. La première fois, il installe pygame puis ouvre le jeu.

Ou à la main, dans un terminal ouvert dans ce dossier :

```
py -m pip install -r requirements.txt
py jeu\principal.py
```

Sur Mac ou Linux : `python3 -m pip install -r requirements.txt` puis `python3 jeu/principal.py`.

Astuce : `py jeu\principal.py 42` génère toujours la même carte (graine 42). Sans nombre, la carte change à chaque partie.

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

## Ce que contient le prototype

- Carte de 40 × 30 cases générée au hasard : herbe, route, forêt, sable, marais, eau et montagnes.
- Coûts de déplacement par terrain (route 75, herbe 100, forêt/sable 150, marais 175 ; diagonale × 1,41).
- Héros avec 1500 points de mouvement par jour, chemin le plus court calculé automatiquement (A*).
- Brouillard de guerre qui se lève autour du héros, mini-carte.
- Tas de ressources (or, bois, minerai, gemmes) et coffres au trésor à ramasser.
- 4 mines à capturer, qui rapportent chaque jour.
- Calendrier jour / semaine / mois.

## Organisation du code (dossier `jeu/`)

- `principal.py` : boucle du jeu, commandes, affichage de l'interface.
- `carte.py` : terrain, objets, brouillard, génération de la carte.
- `heros.py` : le joueur (ressources, mines) et son héros (mouvement).
- `objets.py` : ressources, coffres, mines. C'est ici qu'on ajoutera de nouveaux objets.
- `chemin.py` : recherche du chemin.
- `dessin.py` : dessin des tuiles, objets et du héros (à remplacer plus tard par de vraies images).
- `reglages.py` : tous les chiffres à ajuster (taille de la carte, mouvement, couleurs…).
