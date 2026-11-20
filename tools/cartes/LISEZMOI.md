# Lire les cartes de Heroes 2 et 3

Pour mesurer des cartes faites à la main (2026-11-15, les routes) et en tirer des règles pour `MapGenerator`. Les
cartes elles-mêmes sont dans `maps references/` (non suivi par git) ; on n'en copie jamais une, on en tire des chiffres.

- `h3m.py` : une carte de Heroes 3 (`.h3m`, HotA compris) : taille, terrain, routes, rivières, modèles d'objets, objets
  (trouvés par leur motif, sans décoder tout l'en-tête). `python -I h3m.py dossier` : un résumé par carte.
- `routes_h3.py dossier` : les routes (part de la terre, bouts de route, villes reliées, sortes, rivières).
- `rivieres_h3.py dossier` : les rivières (part de la terre, nombre, longueur, où elles finissent, routes qui les croisent).
- `sortes_routes_h3.py dossier` : les sortes de routes (terre, gravier, pavés : lesquelles une carte emploie, selon la
  distance aux villes, le terrain, le niveau ; combien de fois une route change de sorte).
- `montagnes_h3.py dossier` : les montagnes (modèles et leurs cases bloquées, combien, sur quel terrain, en chaînes ou
  seules, ce qui les touche). `h3m.py` garde les masques des modèles (`m.masks` : cases bloquées, cases d'entrée).
- `terrains.py dossier_h3 dossier_h2` : les sols des deux jeux (part de la terre, taches, leur taille, part de la plus
  grande, sols par carte).
- `zones_h3.py dossier` : les zones vues du joueur (les régions où l'on marche, séparées par les obstacles et les
  monstres) : leur nombre, leur taille, la région d'une case typique, par taille de carte.
- `road_h2.py` : une carte de Heroes 2 (`.mp2`) ; une route est une image du fond de la série ROAD (30), comme fheroes2.
- `routes_h2.py dossier` : les mêmes mesures pour Heroes 2.

Mesuré (2026-11-15) : la route couvre 4,7 % de la terre dans Heroes 3 (3 à 8 %), 0,4 % dans Heroes 2 (3 à 9 % pour les
cartes qui en ont) ; 1,3 et 1,5 villes pour 1000 cases de terre ; les routes finissent surtout aux villes, aux mines, aux
habitations, aux monstres et aux tas.

Mesuré (2026-11-17, 50 cartes) : les montagnes sont des objets (classe 134), 6 modèles par terrain : deux grands
(5 × 3 cases, 12 bloquées, en biais : `.####` / `#####` / `###..`), quatre petits (3 × 2, 4 ou 5 bloquées, en biais
aussi) ; les grands sont les plus posés. Médiane : 39 montagnes pour 1000 cases de terre, 23 % de la terre bloquée
par elles (9 à 77 %), 43 % des cases bloquées par des obstacles ; 90 % des montagnes sont en chaînes (jusqu'à des
centaines) ; elles se chevauchent (25 % de leurs cases bloquées le sont déjà par une autre) ; au bord, surtout des arbres.

Mesuré (2026-11-17, 50 cartes par jeu, médianes) : 5 sols par carte dans Heroes 3, 4 dans Heroes 2. Herbe 30 et 26 % de
la terre, sable 10 et 17 %, marais 11 et 13 % (Heroes 3 : aussi terre 15 %, sol rocailleux 14 %, neige 12 %, lave 5 %).
Le sable : 5 et 2 taches par carte, la plus grande en fait 60 et 70 % ; le marais : 2 et 1 taches, la plus grande 97 et
100 %.

Mesuré (2026-11-20, 50 cartes, `zones_h3.py`) : la région d'une case typique (médiane pondérée par les cases libres)
fait environ 710 cases sur les cartes de 72 × 72, 350 sur 108 × 108, 260 sur 144 × 144 ; 7, 17 et 30 régions d'au moins
20 cases par carte (médianes) ; beaucoup de petites poches gardées autour des trésors.
