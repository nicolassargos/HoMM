# Plan du jeu

Une direction, pas un contrat : les idées peuvent changer.
On avance étape par étape, sans se presser : chaque étape est jouable et testée avant la suivante.

## L'intention
- **Jeu de stratégie façon Heroes** : villes, économie, recrutement chaque semaine, armées.
- **Cartes aléatoires**, rejouables (à partir du `MapGenerator` existant).
- **Une seule faction** pour commencer (environ 7 niveaux de créatures).
- **Combat** : résolution automatique d'abord, écran de combat tactique plus tard.
- **Adversaires IA** : prévus, mais plus tard. D'ici là, chaque carte a un objectif simple
  (vaincre les gardiens, prendre les villes neutres…) pour que la partie ait une fin.
- **Graphismes** : packs d'images gratuits (CC0, itch.io) une fois le jeu jouable.
- **Projet personnel**, sans échéance : on privilégie le plaisir et la simplicité.

## Étapes

### 1. Armées, monstres neutres, combat automatique ✅ (PR #3 et #4)
- Le héros a une armée de 7 troupes au maximum (« 12 Archers »).
- Les créatures sont décrites dans `godot/data/creatures.json` (7 du Château, 7 neutres).
- Des monstres gardent les mines et les coffres, d'autres errent. Ils attaquent le héros
  qui entre sur une case voisine (zone de contrôle) et grossissent de 10 % par semaine.
- Le chemin évite les zones des monstres quand c'est possible.
- Combat : simulation simplifiée sans hasard (ordre de vitesse, riposte, règle
  attaque/défense de Heroes 3), avec des pertes des deux côtés.
- Survol d'un monstre : taille approximative et danger estimé du combat.
- Perdre son dernier héros = défaite ; vaincre tous les monstres = victoire.
- À savoir : sans recrutement, l'armée de départ ne bat qu'environ 5 monstres sur 15.
  Les monstres sont limités au niveau 4 (`MapGenerator.MaxGuardTier`) en attendant les villes.

### Architecture ✅ (PR #4)
- Événements des règles vers l'affichage (`TakeEvents()`), chaque événement défini
  dans le fichier de ce qu'il concerne.
- Aucun texte affiché dans les règles : tout est dans `Game/Texts.cs`.
- Écarté pour l'instant : faire résoudre tout un chemin d'un coup par les règles
  (l'affichage devrait alors garder sa propre copie de l'état). À revoir avec l'IA (étape 7).

### 2. Villes ✅ (PR #6)
Choix faits : la ville est **neutre** au départ, à mi-distance, défendue par une petite
garnison ; les villes ont une **garnison** (comme dans Heroes) ; bâtiments minimaux ;
écran de ville en plein écran.

Découpage :
1. ✅ La ville et ses bâtiments (règles et tests) : ville neutre à prendre, bâtiments dans
   `godot/data/buildings.json` (salle du village → hôtel de ville → grand hôtel de ville,
   taverne, 7 habitations avec prérequis), un bâtiment par jour, revenus en or,
   créatures disponibles chaque semaine. La ville est dessinée sur la carte.
2. ✅ Écran de ville (plein écran) : bâtiments (coût, effet, prérequis, bouton Construire),
   garnison et héros présent. S'ouvre en entrant dans sa ville (ou en la prenant), en
   cliquant sur ses murs, depuis la liste « Villes » du panneau, ou avec T. `Main.cs` découpé : caméra dans `MapCamera.cs`,
   outils d'interface dans `Ui.cs`.
3. ✅ Recrutement : bouton « Recruter » sur les habitations construites (nombre au curseur,
   coût total) ; les recrues vont dans la garnison, ou chez le héros présent si elle est pleine.
   Un clic sur une troupe la fait passer entre la garnison et le héros (qui garde toujours
   au moins une troupe). Pas encore de partage d'une troupe en deux.
4. ✅ Héros recrutés à la taverne (2500 or, 8 héros au maximum, avec 12 créatures du premier
   niveau), qui apparaissent à l'entrée de la ville (si personne n'y est). Passage d'un héros à
   l'autre (liste du panneau, clic sur la carte, touche H), anneau doré sous le héros
   sélectionné. Chaque héros garde son chemin prévu ; les héros ne se traversent pas.
5. ✅ **Ville sur 3×3 cases avec une entrée** : la ville occupe 9 cases, l'entrée est au
   milieu de la rangée du bas et s'atteint par la case juste en dessous ; les 8 autres sont
   infranchissables. Les objets ont maintenant une « empreinte » (`MapObject.Footprint`),
   réutilisable pour d'autres grands objets. Un clic sur les murs mène à l'entrée.
6. ✅ Équilibrage, vérifié par une simulation (`BalanceTests`) qui joue une stratégie simple
   sur plusieurs cartes : monstres jusqu'au niveau 7 (`MaxGuardTier`), troupes jusqu'à 2 fois
   plus nombreuses au loin (`GuardSizeGrowth`), croissance hebdomadaire plafonnée au double,
   garnison de la ville neutre renforcée (3 à 8 pertes au siège). La simulation, optimiste,
   gagne entre le jour 34 et le jour 102 (souvent vers le jour 50).
   À noter : sur une carte 40×30, aucun monstre n'est assez loin pour être de niveau 7 ;
   il faudra de plus grandes cartes (générateur v2, étape 4).

### 3. Progression du héros ✅ (en cours de PR)
Choix faits : le niveau donne +1 charisme et +1 à une caractéristique tirée au sort (graine
de la partie) parmi attaque, défense, puissance, savoir ; la puissance et le savoir n'ont
pas encore d'effet (la magie viendra à l'étape 8). Expérience = valeur en or des monstres
tués ; niveau n atteint à 1000 × (n-1) × n / 2 points. Un héros commence avec 1/1/1/1 et
charisme 10. Le coût de commandement est de 1 à 20 (`commandCost` dans `creatures.json`).
Les troupes révoltées sont retirées de l'armée et combattent les monstres sans le bonus du
héros ; leurs pertes ne donnent pas d'expérience.
Au niveau supérieur, une fenêtre montre chaque caractéristique avant et après.
Le Paysan (1 PV, 3 or) remplace le Piquier comme unité de base du Château. L'Ange a 2000 PV, défense 30 et
105 de dégâts : il tue 200 paysans d'un coup, et il faut au moins mille paysans pour le vaincre (test `AngelTests`).
Courbe du Château (paysans nécessaires pour vaincre une unité, héros de départ) : Paysan 1,
Archer 6, Griffon 13, Épéiste 26, Moine 50, Cavalier 99, Ange 1632 (test `UnitCurveTests`).
Départ : 30 paysans, 15 archers, 4 griffons.
Prix : 3 or par paysan équivalent (Archer 18, Griffon 40, Épéiste 80, Moine 150, Cavalier 300,
Ange 4900 et 1 gemme). Croissance hebdomadaire inchangée (14, 9, 7, 4, 3, 2, 1) : une croissance
égale en valeur est impossible pour l'Ange. Revenus des villes : 150, 300, 600 or par jour.
La simulation d'équilibrage n'est pas limitée par l'or : c'est la croissance et les monstres qui la freinent.
Reste à faire : les « choix de niveau » qui donnent plus de charisme (tirage aléatoire pour l'instant).

- Expérience gagnée en combat, niveaux.
- Les 5 caractéristiques : attaque, défense, puissance, savoir, **charisme**.
- L'attaque et la défense du héros s'ajoutent à celles des créatures dans `Battle`.
- **Charisme** (comme le commandement de King's Bounty) : chaque créature a un coût de
  commandement (dans `creatures.json`, 1 pour les créatures les plus faibles, bien plus pour
  un Ange). Une troupe est obéissante si nombre × coût ≤ charisme × 10 : avec un charisme
  de 10, un héros commande au plus 100 créatures à 1 point dans une même troupe. La limite
  s'applique à chaque troupe séparément, pas à toute l'armée.
  - Dépasser la limite est permis, mais risqué : au début de chaque combat, une troupe
    trop nombreuse peut se révolter, avec un risque qui grandit avec le dépassement
    (environ 50 % à deux fois la limite). Elle combat alors dans le camp ennemi, et si elle
    survit, elle quitte l'armée. Le compte rendu du combat le signale.
  - Le risque est tiré au sort avec la graine de la partie (le combat lui-même reste sans hasard).
  - L'interface prévient avant de dépasser la limite (recrutement, échange de troupes) et
    montre les troupes à risque.
  - Les garnisons des villes n'ont pas de limite.
  - Le charisme augmente un peu à chaque niveau, et certains choix de niveau donnent plus.
- Artefacts plus tard.

### 4. Parties complètes
- Conditions de victoire et de défaite (choisies au lancement de la partie).
- Générateur v2 : zones, gardiens de plus en plus forts loin du départ, villes neutres,
  monstres qui gardent les passages.
- Sauvegarde et chargement. Il faudra donner des identifiants aux objets, héros et joueurs
  (aujourd'hui ils se référencent directement).

### 5. Graphismes
- Remplacer les formes dessinées par un pack d'images (possible dès l'étape 2).
- Seul `MapRenderer.cs` (et l'interface) change. Les images sont trouvées par convention
  à partir des identifiants, par exemple `res://art/creatures/paysan.png` :
  pas de chemins d'images dans les fichiers de règles.

### 6. Combat tactique
- Écran de combat sur grille hexagonale.
- Même séparation : le combat (état, actions) dans `Core`, l'écran dans `Game`.
- Le combat automatique reste disponible en « combat rapide ».

### 7. Adversaires IA
- Joueurs ordinateur avec leurs héros et leurs villes.
- L'IA joue avec les mêmes méthodes de `GameState` que le joueur humain, sans attendre l'affichage.
- L'affichage rejoue les déplacements des héros ennemis (événements `HeroMoved`) à un rythme lisible.

### 8. Ensuite
- Magie, autres factions, nouveaux objets sur la carte.

## Choix à respecter
- **`GameState` gère plusieurs joueurs** (liste de joueurs, tour par tour), même s'il n'y a
  qu'un humain, pour que l'IA n'oblige pas à tout réécrire.
- **Toutes les règles restent dans `Core/`** (sans Godot), pour que l'IA et le combat
  tactique réutilisent les mêmes règles et que tout reste testable.
- **Les données de jeu** (créatures, bientôt bâtiments) sont dans `godot/data/`, pas dans le code.

## Questions ouvertes
- Rester proche des règles et des chiffres de Heroes 3, ou inventer les nôtres ?
  (Pour l'instant : proche de Heroes 3.)
- Thème de la première faction : médiéval classique (le Château, utilisé pour l'instant) ou autre chose ?

## Idées à garder
- **Capacités propres à chaque unité** (à faire après l'étape 3, à prévoir avec le combat tactique) :
  premier coup, tir à distance, vol, etc. Pour que chaque unité ait un rôle distinct,
  pas seulement des chiffres différents. Demandé par l'utilisateur.
