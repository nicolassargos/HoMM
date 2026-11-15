# Lire les cartes de Heroes 2 et 3

Pour mesurer des cartes faites à la main (2026-11-15, les routes) et en tirer des règles pour `MapGenerator`. Les
cartes elles-mêmes sont dans `maps references/` (non suivi par git) ; on n'en copie jamais une, on en tire des chiffres.

- `h3m.py` : une carte de Heroes 3 (`.h3m`, HotA compris) : taille, terrain, routes, rivières, modèles d'objets, objets
  (trouvés par leur motif, sans décoder tout l'en-tête). `python -I h3m.py dossier` : un résumé par carte.
- `routes_h3.py dossier` : les routes (part de la terre, bouts de route, villes reliées, sortes, rivières).
- `rivieres_h3.py dossier` : les rivières (part de la terre, nombre, longueur, où elles finissent, routes qui les croisent).
- `sortes_routes_h3.py dossier` : les sortes de routes (terre, gravier, pavés : lesquelles une carte emploie, selon la
  distance aux villes, le terrain, le niveau ; combien de fois une route change de sorte).
- `road_h2.py` : une carte de Heroes 2 (`.mp2`) ; une route est une image du fond de la série ROAD (30), comme fheroes2.
- `routes_h2.py dossier` : les mêmes mesures pour Heroes 2.

Mesuré (2026-11-15) : la route couvre 4,7 % de la terre dans Heroes 3 (3 à 8 %), 0,4 % dans Heroes 2 (3 à 9 % pour les
cartes qui en ont) ; 1,3 et 1,5 villes pour 1000 cases de terre ; les routes finissent surtout aux villes, aux mines, aux
habitations, aux monstres et aux tas.
