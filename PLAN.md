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
- **Graphismes** : pixel art façon premier Heroes, généré avec Retro Diffusion (étape 5).
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
   À noter : sur une carte 40×30, aucun monstre n'est assez loin pour être de niveau 7
   (réglé par le générateur v2, étape 4).

### 3. Progression du héros ✅
Choix faits : le niveau donne +1 charisme et +1 à une caractéristique choisie parmi les quatre
(attaque, défense, puissance, savoir) ; la puissance et le savoir n'ont
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
Ange 4900 et 1 gemme). Croissance hebdomadaire doublée (28, 18, 14, 8, 6, 4, 2) : une croissance
égale en valeur est impossible pour l'Ange. Revenus des villes : 150, 300, 600 or par jour. Mines d'or : 250 or par jour (au lieu de 1000).
La simulation d'équilibrage n'est pas limitée par l'or : c'est la croissance et les monstres qui la freinent.

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

### 4. Parties complètes ✅
- ✅ Identifiants stables : joueurs, héros et objets de carte ont un `Id`, retrouvable avec
  `PlayerById`, `HeroById`, `ObjectById`. Les données (`DataValidation`) sont vérifiées au chargement.
- ✅ Conditions de victoire et de défaite (choisies au lancement de la partie) : vaincre tous les
  monstres, ou prendre toutes les villes, avec ou sans limite de jours (150 au plus). Perdre tous ses
  héros reste une défaite dans tous les cas (`GameRules`, `ChooseRules`).
  Plus tard : condition « Trouver le trésor » (demandée par l'utilisateur).
- ✅ Générateur v2 : carte de 54 × 40 découpée en zones (3 × 3) séparées par des montagnes ou des
  rivières. Des passages (3 cases de large) relient toutes les zones, plus quelques-uns au hasard ;
  chacun est gardé par un monstre qu'on ne peut pas contourner. Le niveau d'une zone est le nombre de
  passages à franchir depuis le départ : il donne la force des monstres (niveau 1 près du départ,
  Dragons dans la zone la plus lointaine). Une ville neutre pour trois zones : la première juste après
  la zone de départ, les autres plus loin avec des garnisons plus fortes. Une mine par zone (au moins
  une de chaque ressource) ; coffres, tas et monstres errants en proportion de la taille de la carte.
  Les monstres sont maintenant seulement des créatures neutres (un Ange gardien était imbattable).
  Aucun objet ni monstre ne coupe le chemin vers un autre objet. La sortie de la zone de départ reste
  à la portée de l'armée de départ (il n'y a pas de ville avant). La simulation gagne vers le jour 82
  (de 76 à 120 environ ; sur 60 graines, une seule n'est pas gagnée en 200 jours).
- ✅ Sauvegarde et chargement (F5 / F9, ou boutons du panneau ; `-- --load` au lancement) : toute la
  partie en JSON dans `user://sauvegarde.json` (`GameState.Save` / `GameState.Load`), terrain compris,
  pour ne pas dépendre du générateur. Le hasard de la partie (`GameRandom`) est enregistré : une partie
  chargée continue exactement comme l'originale. Les choix de niveau en attente sont reproposés.
  Une sauvegarde abîmée ou d'une autre version (`SaveVersion`) est refusée sans perdre la partie en cours.
  Une seule sauvegarde pour l'instant.

### 5. Graphismes
- Remplacer les formes dessinées par un pack d'images (possible dès l'étape 2).
- Seul `MapRenderer.cs` (et l'interface) change. Les images sont trouvées par convention
  à partir des identifiants, par exemple `res://art/creatures/paysan.png` :
  pas de chemins d'images dans les fichiers de règles.
- ✅ Images de la carte : pixel art façon premier Heroes, généré avec Retro Diffusion (API payante, choisie
  par l'utilisateur à la place d'un pack gratuit). Images de 24 px affichées ×2 (cases de 48 px), pixels nets.
  `tools/art/generer.py` estime le prix (gratuit), génère des candidats dans `art-candidats/` (non suivi)
  et installe le candidat choisi ; `tools/art/apercu.py` fait une page de choix. Les images à produire et
  leurs descriptions sont dans `tools/art/assets.json`. Une image absente garde le dessin par formes.
  Les forêts ont des sapins dessinés par-dessus leur image, en attendant mieux.
  `-- --capture=fichier.png` enregistre une image de l'écran puis quitte (pour comparer).
- ✅ Interface : police pixel VT323 (licence OFL, `godot/fonts/`), tailles multipliées par `Ui.FontScale`
  (lissée en petit, nette à partir de 25 px). Cadres et boutons de bois et d'or générés (`art/ui/panel.png`,
  `art/ui/button.png`), découpés en 9 morceaux : coins intacts, bords et centre répétés sans jamais étirer,
  à l'échelle 1. Les clous dorés des bords ne sont pas répétés (seule la plus longue bande sans détail l'est).
  Les fenêtres (choix des conditions, niveau, fin de partie) ont le même cadre (`Ui.ApplyTheme`).
  Essayés puis écartés : DotGothic16 (police), les cadres de Kenney (CC0, mais traits blancs trop sobres).
- À faire : portraits des créatures dans la ville, transitions entre terrains, forêts et montagnes
  dessinées comme des objets posés sur le sol (comme le premier Heroes).

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
