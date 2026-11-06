# Plan du jeu

Une direction, pas un contrat : les idées peuvent changer.
On avance étape par étape, sans se presser : chaque étape est jouable et testée avant la suivante.

Ce fichier garde la direction, l'étape en cours et les choix encore valables. Le détail des étapes finies (chiffres
mesurés, essais écartés, classes et fichiers) est dans `HISTORIQUE.md` : y ajouter le détail d'une étape quand elle se
termine, et ne garder ici qu'une ligne. La méthode des images est dans `tools/art/METHODE.md`.

## L'intention
- **Jeu de stratégie façon Heroes** (et King's Bounty pour le charisme) : villes, économie, recrutement chaque semaine,
  armées, combat tactique sur hexagones, adversaires joués par l'ordinateur.
- **Cartes aléatoires**, rejouables (`MapGenerator`), de 5 tailles.
- **Graphismes** : pixel art façon premier Heroes, PixelLab (personnages, créatures, interface) et Retro Diffusion
  (cartes, portraits).
- **Projet personnel**, sans échéance : on privilégie le plaisir et la simplicité.

## Les étapes

1. ✅ Armées, monstres neutres (zones de contrôle, croissance chaque semaine), combat automatique.
2. ✅ Villes : bâtiments (`buildings.json`), un par jour, revenus, recrutement, taverne, ville sur 3 × 3 cases.
3. ✅ Progression du héros : expérience, niveaux, 5 caractéristiques dont le **charisme**.
4. ✅ Parties complètes : conditions de victoire (cumulables), trésor enfoui et obélisques, générateur v2 (zones et
   passages gardés), sauvegarde exacte (le hasard compris).
5. ✅ Graphismes de la carte et de l'interface (terrains par shader, décor, curseurs, zoom, routes, portraits).
6. ✅ Combat tactique (11 × 9 cases, capacités des créatures, grandes créatures sur 2 cases, combat rapide joué par
   l'IA) ; créatures refaites une à une avec PixelLab (en cours au fil de l'eau, voir `tools/art/METHODE.md`).
7. ✅ Refonte de l'interface (carte, héros, royaume, ville vue de face, combat, fenêtres, menu, sauvegardes, 1920 × 1080).
   **Reste** : une nouvelle revue de toutes les captures avec l'utilisateur, quand les écrans qui manquent seront là
   (ville en images, artefacts du héros, livre de sorts en image).
8. ✅ Adversaires IA (1 à 3, même règles, pas de triche), héros contre héros, se défendre soi-même, 3 niveaux de
   difficulté, un château au départ pour chaque joueur. **Plus tard (étape 10)** : entraîner les profils de difficulté
   par des parties entre IA, jouées sans affichage.
9. **Contenu (en cours)** : la magie d'abord, puis les factions, les héros, les habitations sur la carte (ci-dessous).
10. Finitions : apparence et détails (ci-dessous).

## Étape 9 : contenu

### La magie (en cours)
Décidé avec l'utilisateur (2026-11-06), proche de Heroes 2 et 3 :
- Chaque héros a un **livre de sorts** (plus tard, les héros de force devront l'acheter, avec les classes).
- **Points de sort** : 10 par point de savoir ; +1 par jour ; tous en entrant dans une ville qui a une guilde des mages
  (ou en y commençant la journée). **Puissance** : force des sorts et durée (puissance + 1 rounds).
- **Guilde des mages** de 4 niveaux (le 3 demande l'hôtel de ville, le 4 le grand hôtel de ville) ; ses sorts sont tirés
  au sort par ville (niveaux 1 et 2 : 3 sur 5 ; niveau 3 : 2 sur 5 ; niveau 4 : 2 sur 3) ; un héros les apprend tous
  en entrant (pas de « Sagesse »).
- **Au combat** : un sort par round et par héros, sans prendre le tour de la troupe ; le livre se lit à tout moment
  (au combat et sur la carte). Les monstres et les garnisons seules n'en lancent pas ; le combat rapide et l'IA si
  (le sort qui vaut le plus en or).
- **18 sorts de combat** (`data/spells.json`, chiffres à régler à l'équilibrage) :

  | Niv. | Sort | Points | Effet |
  |---|---|---|---|
  | 1 | Flèche magique | 5 | 10 + 10 × puissance dégâts |
  | 1 | Bénédiction / Malédiction | 5 / 6 | +25 % / −25 % de dégâts |
  | 1 | Peau de pierre | 5 | +5 défense |
  | 1 | Soif de sang | 5 | +6 attaque |
  | 2 | Éclair | 10 | 10 + 25 × puissance dégâts |
  | 2 | Hâte / Lenteur | 6 / 6 | +3 vitesse / vitesse divisée par deux |
  | 2 | Soins | 6 | 10 + 5 × puissance PV à la créature de tête, retire les sorts néfastes |
  | 2 | Bouclier contre les tirs | 7 | moitié des dégâts des tirs |
  | 3 | Boule de feu | 15 | 10 + 10 × puissance dégâts sur 7 cases (amis compris) |
  | 3 | Faiblesse | 8 | −6 attaque |
  | 3 | Aveuglement | 10 | ne joue plus jusqu'au premier coup (sans riposte) |
  | 3 | Téléportation | 10 | une troupe amie sur n'importe quelle case libre |
  | 3 | Dissipation | 8 | retire tous les sorts d'une troupe |
  | 4 | Résurrection | 20 | 40 + 20 × puissance PV de créatures tuées dans ce combat |
  | 4 | Pluie de météores | 16 | 25 + 25 × puissance dégâts sur 7 cases (amis compris) |
  | 4 | Chaîne d'éclairs | 20 | 25 + 40 × puissance dégâts, moitié à chaque saut (4 troupes, amis compris) |

- **Plus tard** : écoles de magie et compétences (avec les classes de héros), versions « de masse » (récompense des
  classes), **créatures insensibles à la magie**, un 5e niveau avec les factions (Armageddon, élémentaires, morts
  relevés), sorts de la carte (portail de ville, porte dimensionnelle, vision…). Écartés pour l'instant : berserk,
  hypnotisme, paralysie.

Fait : les règles, la guilde, le livre au combat et sur la carte, les 18 sorts (voir `HISTORIQUE.md`). À faire, dans
cet ordre :
1. **L'équilibrage avec la magie** : la simulation (seul) et les adversaires, avec les mesures (`Mesures`).
2. **Le livre de sorts en image** : un vrai livre ouvert, une page par niveau, une animation de page qui tourne.
3. **Une image par sort** (le livre, la guilde).
4. **Une animation par sort** sur le champ (projectile, éclair, explosion…) et un **effet visible sur les troupes**
   (lueur, aura tant qu'un sort dure…). Puis le reste au fil des idées (sons…).

### Les factions
À terme **6 factions + 1 faction neutre**, chacune avec ses 7 créatures, à concevoir ensemble (thème, rôle de chacune).
Déjà décidé (2026-11-05) : des **morts-vivants** avec le Zombie, le Fantôme, le Vampire, la Liche et, au sommet, un
**Colosse d'os** (aussi fort que l'Ange ou le Dragon) ; le Squelette pourrait les rejoindre. Au Château, l'Épéiste
deviendra un **Chevalier**. Images déjà prêtes ou en cours : `chevalier`, `zombie`, `fantome`, `vampire`, `liche`,
`colosse-os`. L'IA devra apprendre les nouvelles factions.

### Les héros
À préciser ensemble : choisir son héros de départ, une **classe** ou un passé (chevalier, voleur, barbare, sorcière…)
avec des caractéristiques de départ et des bonus, des compétences à choisir en montant de niveau (dont la magie),
l'apparence sur la carte. La place de la classe est gardée sur l'écran du héros.

### Les habitations sur la carte
Des lieux semés au hasard où un héros achète des troupes (comme dans Heroes). À concevoir : créatures, réserve et
croissance, prix, garde.

## Étape 10 : finitions
- Animations des créatures avec PixelLab (au fil de l'eau) ; projectiles (flèches, boule de feu du Moine) et effets
  des sorts dessinés par le jeu.
- Fonds de champ de bataille et obstacles ; le héros sur la carte et au combat ; les bâtiments des villes en images
  (la ville vue de face est encore en boîtes) ; icônes des ressources, cadres des portraits, bandeau de la date.
- Entraîner les profils de difficulté par des parties entre IA.
- Petits détails et équilibrage fin.

## Repères (choix encore valables)
- **Héros** : 1/1/1/1 et charisme 10 au départ ; niveau n à 1000 × (n−1) × n / 2 d'expérience (= l'or des créatures
  tuées) ; chaque niveau : +1 charisme et +1 au choix. Une troupe obéit si nombre × coût de commandement ≤ charisme
  × 10, sinon elle peut se révolter au début d'un combat (≈ 50 % à deux fois la limite). Les garnisons n'ont pas de limite.
- **Château** : départ 30 paysans, 15 archers, 4 griffons ; prix 3 or par « paysan équivalent » (Archer 18, Griffon 40,
  Épéiste 80, Moine 150, Cavalier 300, Ange 4900 et 1 gemme) ; croissance 28, 18, 14, 8, 6, 4, 2 ; paysans pour vaincre
  une unité : Archer 8, Griffon 13, Épéiste 26, Moine 58, Cavalier 112, Ange 1560 (`UnitCurveTests`).
- **Économie** : villes 150 / 300 / 600 or par jour, mines d'or 250 ; héros de taverne 2500 or (8 au plus).
- **Combat** : dégâts tirés au sort puis règle attaque / défense de Heroes 3 ; tir à moitié au-delà de 6 cases ou
  au corps à corps ; défense +20 % ; fuite : 50 à 90 % de pertes ; un combat se rejoue pareil avec la même graine.
- **Carte** : zones séparées par des passages gardés ; le niveau d'une zone (passages depuis le départ) donne la force
  des monstres ; un château par joueur au départ, des villes neutres plus loin.
- **Difficulté** (`ComputerProfile`) : pertes acceptées 20 / 30 / 40 %, attaque le joueur dès le jour 21 / 0 / 0,
  héros au plus 2 / 3 / 4, revenu d'or 75 / 100 / 125 %.
- **Équilibre mesuré** (`Mesures`, 2026-11-06, six cartes) : la simulation seule (sans magie) gagne sur les petites
  cartes vers le jour 60, les normales vers le jour 101 (80 à 154), les grandes vers le jour 146. Un adversaire, après
  six semaines : 2,3 à 2,7 villes, une armée de 6 000 (Normal) à 13 000 or (Difficile) en moyenne, la plupart de ses
  héros ont des sorts. Normal paraît plus faible que Facile : à revoir à l'équilibrage (la guilde, dans l'ordre de
  construction de l'IA, prend peut-être l'or des troupes). Courbe : Archer 8, Griffon 14, Épéiste 26, Moine 63,
  Cavalier 116, Ange 1643 paysans.

## Choix à respecter
- **`GameState` gère plusieurs joueurs**, et l'IA joue avec les mêmes méthodes que le joueur humain.
- **Toutes les règles restent dans `Core/`** (sans Godot), testables ; l'affichage ne fait que lire des événements.
- **Les données de jeu** (créatures, bâtiments, sorts, artefacts) sont dans `godot/data/`, pas dans le code.
- Rester proche des règles et des chiffres de Heroes 3, sauf choix contraire noté (charisme, magie…).
