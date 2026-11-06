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
- **Graphismes** : pixel art façon premier Heroes, généré avec Retro Diffusion (cartes, portraits) et PixelLab
  (personnages, créatures, interface).
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
garnison (changé à l'étape 8 : chaque joueur commence maintenant avec son château) ; les villes ont une **garnison** (comme dans Heroes) ; bâtiments minimaux ;
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
(Étape 6 : l'Ange vole et frappe sans riposte ; il a maintenant 1200 PV et 80 de dégâts, choix de l'utilisateur.)
Courbe du Château (paysans nécessaires pour vaincre une unité, héros de départ) : Paysan 1,
Archer 6, Griffon 13, Épéiste 26, Moine 50, Cavalier 99, Ange 1632 (test `UnitCurveTests`) ; avec le combat tactique
et les capacités (étape 6) : Archer 8, Griffon 13, Épéiste 26, Moine 58, Cavalier 112, Ange 1560.
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
  ✅ On peut **cocher plusieurs façons de gagner** (vaincre les monstres, prendre les villes, trouver le trésor) :
  la première accomplie l'emporte ; la limite de jours est une case à part. `VictoryCondition` est une combinaison
  (les anciennes sauvegardes se lisent toujours).
  ✅ Condition « Trouver le trésor » (demandée par l'utilisateur), comme dans Heroes 2 : le trésor est un artefact
  légendaire, le **Plastron des Rois** (`data/artifacts.json`), **enfoui** (invisible, il ne bloque rien) dans la zone
  la plus dangereuse, le plus loin possible du départ (`AdventureMap.Treasure`). **6 obélisques** dans des zones
  différentes : la première visite de chacun dévoile un morceau de la **carte au trésor** (touche M), un dessin du pays
  autour du trésor avec une croix. Pour le prendre, il faut **creuser** (touche F, ou bouton des options) sur la bonne
  case, avec une journée entière de mouvement, qui est dépensée. Déterré dans une partie « Trouver le trésor », il fait
  gagner ; dans les autres, il donne +5 attaque, +5 défense, +5 charisme. Au début, le message de l'objectif dit dans
  quelle partie de la carte il est enfoui. Images : obélisque (`art/objects/obelisk.png`) et Plastron
  (`art/artifacts/plastron-des-rois.png`), montré en grand quand on le déterre et en petit dans le panneau.
  Premier artefact du jeu : un héros porte ses artefacts (sauvegardés), leurs
  bonus s'ajoutent à ses caractéristiques.
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
  `-- --capture=fichier.png` enregistre une image de l'écran puis quitte (pour comparer).
- ✅ Interface : police pixel VT323 (licence OFL, `godot/fonts/`), tailles multipliées par `Ui.FontScale`
  (lissée en petit, nette à partir de 25 px). Cadres et boutons de bois et d'or générés (`art/ui/panel.png`,
  `art/ui/button.png`), découpés en 9 morceaux : coins intacts, bords et centre répétés sans jamais étirer,
  à l'échelle 1. Les clous dorés des bords ne sont pas répétés (seule la plus longue bande sans détail l'est).
  Les fenêtres (choix des conditions, niveau, fin de partie) ont le même cadre (`Ui.ApplyTheme`).
  Essayés puis écartés : DotGothic16 (police), les cadres de Kenney (CC0, mais traits blancs trop sobres).
- ✅ Forêts et montagnes posées sur l'herbe, comme dans le premier Heroes (`DecorLayer`) : dans chaque case
  de forêt, 4 ou 5 arbres seuls (sapins, chênes, arbres d'automne, `art/decor/`) semés au hasard ; dans chaque
  case de montagne, un petit sommet derrière et un grand devant. Ils débordent sur les cases voisines et sont dessinés de haut en bas, pour
  former des masses continues. Le hasard dépend de la case (même décor à chaque fois). Les règles ne changent pas.
  Essayés puis écartés : une image par case (groupe d'arbres, montagne), qui laissait des trous réguliers.
- ✅ Images des 7 créatures du Château (24 px, comme les monstres). L'écran de ville montre l'image de chaque
  créature sur sa carte d'habitation, dans les emplacements de la garnison et du héros, et en grand (×3) dans la
  fenêtre de recrutement. L'écran de ville se lit maintenant de haut en bas (les armées restent sous les
  bâtiments) et ses cartes gardent leur largeur. `-- --capture=… --town` montre l'écran d'une ville.
- ✅ Transitions entre terrains, calculées par un shader (`shaders/terrain.gdshader`, `SmoothGround`) : pas d'images
  de bords à préparer, et changer l'image d'un terrain change aussi ses bords. Chaque pixel d'art part du terrain de
  sa case ; près d'un autre terrain, les deux se mélangent (en paliers tramés, sur environ 7 pixels d'art) le long
  d'une ligne ondulée (grandes ondulations et petites encoches), les terrains « au-dessus » débordant un peu plus
  (rang : eau < route < sable < marais < herbe). Contre l'eau, la rive reste nette, avec contour, berge un peu sombre
  et écume. Deux images par terrain (`grass-2.png`…), mélangées par un grand bruit doux, et de grandes zones un peu
  plus claires ou plus sombres, pour que le motif ne se voie pas. Les règles ne changent pas.
  Essayés puis écartés : bords nets avec taches (trop bruité), mélange large (les cases seules s'effaçaient).
  Développement : `--terrain-test` peint un échantillon de toutes les rencontres de terrains, `--ground=réglage:valeur`
  change un réglage du shader sans recompiler.
- ✅ Curseurs de la souris, comme dans le premier Heroes (`MapCursor`, `art/cursors/`) : gantelet sur l'interface,
  botte avec le nombre de jours de marche (`GameState.DaysTo`), épées sur un monstre, main sur un objet à visiter,
  croix là où on ne peut pas aller (ou jamais exploré), casque sur un de ses héros. La case survolée n'est plus encadrée.
- ✅ Zoom de la carte (molette, touches + et -) : ×2 ou ×3 (comme le premier Heroes), pixels d'art toujours entiers.
  La partie commence à ×2 (choix de l'utilisateur, à revoir lors des réglages fins). En 1920 × 1080 (étape 7) : ×3 et ×4.
- ✅ La route est une couche posée sur le terrain (`AdventureMap.HasRoad`), plus un terrain : une case de route
  coûte 75 quel que soit le terrain dessous (`TerrainInfo.RoadCost`). Le générateur garde le sable et le marais sous
  la route ; l'eau, la montagne et la forêt y deviennent de l'herbe. Le shader dessine la route comme un chemin
  continu au-dessus du sol (virages en quart de cercle, bords un peu ondulés). Sauvegarde version 2 (ligne « roads ») ;
  une sauvegarde de la version 1 est convertie (l'ancien terrain « route » devient de l'herbe avec une route).
  Les numéros des terrains ne changent pas (le 1 n'est plus utilisé).
- ✅ Portraits des héros (64 × 64, style « retro » de Retro Diffusion, des visages marqués plutôt qu'idéalisés) :
  dans le panneau, à côté du nom, et dans la taverne (le héros qu'elle propose). Trouvés d'après le nom du héros
  (`art/heroes/portraits/renaud-le-goupil.png`). Deux nouveaux héros : Renaud le Goupil (voleur) et Brann le Rouge
  (barbare), à la place de Sire Tristan et Dame Énide. Dame Viviane et Sire Yvain n'ont pas encore de portrait.
- Plus tard, si besoin : des images plus grandes (64 ou 96 px) pour les créatures dans la ville, le portrait dans la
  fenêtre de niveau.

### 6. Combat tactique
- Écran de combat sur grille hexagonale.
- Même séparation : le combat (état, actions) dans `Core`, l'écran dans `Game`.
- Le combat automatique reste disponible en « combat rapide ».

Choix faits (avec l'utilisateur) :
- **Champ de bataille de 11 × 9 cases**, comme dans Heroes 1 et 2 ; l'attaquant à gauche, le défenseur à droite,
  2 à 5 obstacles au hasard au milieu (sans jamais couper le champ en deux).
- **Rounds** : chaque troupe joue une fois, de la plus rapide à la plus lente (à vitesse égale, l'attaquant d'abord).
  Elle peut **se déplacer** (sa vitesse en cases), **attaquer** une troupe voisine (riposte une fois par round),
  **tirer** si elle sait tirer (pas de riposte), **attendre** (elle joue à la fin du round) ou **se défendre**
  (+20 % de défense jusqu'à son prochain tour).
- **Dégâts tirés au sort** entre le minimum et le maximum (avec le hasard de la partie : même graine, même combat),
  puis la règle attaque / défense de Heroes 3. Le bonus d'attaque et de défense du héros compte, comme avant.
- **Tireurs** : un tireur ne peut pas tirer avec un ennemi à côté de lui, et se bat alors à moitié de ses dégâts.
  Au-delà de 6 cases, un tir fait moitié moins de dégâts, **sauf capacité `no-range-penalty`** (pour de futurs elfes
  qui tirent partout, ou des druides dont les sorts ne dépendent pas de la distance).
- **Fuite** : le héros s'enfuit, mais chaque troupe perd entre 50 et 90 % de ses créatures (tiré au sort), sans
  expérience. La fuite devient un choix tactique.
- **Combat rapide** : l'IA joue le combat tactique pour les deux camps, d'un coup (le combat simplifié actuel disparaîtra).
- **Capacités** (première version, à affiner) : Archer et Moine tirent ; Griffon vole et riposte deux fois ; Épéiste
  a un bouclier (moitié des dégâts des tirs) ; Cavalier charge (+5 % par case parcourue) ; Ange vole et frappe sans
  riposte ; Gobelin est rapide au premier round ; Squelette encaisse les tirs ; Loup attaque deux fois ; Orc lance des
  haches (tir court) ; Ogre ignore 40 % de la défense ; Troll se régénère ; Dragon vole et son souffle touche aussi la
  case derrière. Données : liste `abilities` de `creatures.json`.

Découpage :
1. ✅ Règles du combat (`Core/Tactics/` : `Hex`, `Battlefield`, `BattleUnit`, `TacticalBattle`), sans affichage :
   placement, obstacles, ordre de jeu, déplacement, attaque, riposte, tir (et pénalités), attente, défense, fuite,
   bilan (`BattleReport`, avec `Fled`). Capacités déjà en place : `shoots` (Archer, Moine, Orc), `no-range-penalty`.
2. ✅ L'IA des troupes (`BattleAI` : un tireur tire sur l'ennemi le plus dangereux ; une autre troupe attaque
   l'ennemi le plus dangereux qu'elle peut atteindre, sinon s'approche du plus proche). Le combat rapide
   (`Battle.Resolve`) est maintenant le combat tactique joué d'un coup par cette IA, avec le hasard de la partie
   (une estimation au survol utilise une graine fixe) ; le combat simplifié a disparu. Équilibre : la simulation
   gagne toujours vers le jour 84 (78 à 118 sur 20 cartes, comme avant). Courbe du Château (paysans pour vaincre
   une unité) : Archer 8, Griffon 12, Épéiste 26, Moine 89 (il tire maintenant), Cavalier 91, Ange 1632 : à régler
   de nouveau avec les capacités.
3. ✅ L'écran de combat (`BattleScreen`, `BattleFieldView`) : avant chaque combat, le joueur choisit « Combat
   tactique » ou « Combat rapide ». Champ de 11 × 9 cases sur le sol du terrain, centré (de la place reste de chaque
   côté pour des héros, des machines de guerre…), troupes ×3 avec leur nombre, cases où la troupe peut aller,
   curseurs (botte, épées, croix). En bas, sur une ligne comme dans Heroes V : le round, l'ordre de jeu complet
   (celles qui jouent encore, puis celles qui ont déjà joué) et les boutons ; la dernière action au-dessus du champ. Clic sur une case pour y aller, sur un
   ennemi pour l'attaquer (du côté le plus proche de la souris) ou tirer. Boutons Attendre, Se défendre, Auto, Fuir.
   L'IA joue les ennemis à un rythme lisible ; animations de marche, de coup et de dégâts. Les règles s'arrêtent au
   début d'un combat (`GameState.QuickBattles` à false, `CurrentBattle`, `FinishBattle`) puis reprennent le pas ; en
   fuyant, le héros revient sur la case d'où il venait, sans mouvement. Pas de sauvegarde pendant un combat.
4. ✅ Les capacités des créatures (tableau ci-dessus), chacune avec ses tests : `flies`, `double-retaliation`,
   `no-retaliation`, `breath`, `ranged-resistance`, `charge`, `quick-start`, `double-attack`, `short-range`, `crushing`,
   `regenerates` (en plus de `shoots` et `no-range-penalty`). L'IA d'un Cavalier charge de loin. Courbe du Château
   réglée de nouveau (environ ×2 par palier) : Moine 11-12 de dégâts (au lieu de 18-19), Cavalier 120 PV et 22 de
   dégâts (au lieu de 100 et 19), Griffon 16 PV (au lieu de 12), Ange 1200 PV et 80 de dégâts (au lieu de 2000 et 105 :
   avec ses capacités, il fallait 2300 paysans). Paysans pour vaincre une unité : Archer 8, Griffon 13,
   Épéiste 26, Moine 58, Cavalier 112, Ange 1560. La simulation gagne toujours vers le jour 84 (78 à 122 sur 20 cartes).
5. Finitions (fonds de champ de bataille selon le terrain, images d'obstacles) : reportées à l'étape 10.

Ensuite, des images plus détaillées et des animations (demandé par l'utilisateur ; les images de 24 px manquent de
détails, surtout agrandies au combat), dans cet ordre :
1. ✅ Les 14 créatures au combat en images détaillées (`art/creatures/battle/{id}.png`, affichées ×2 ; sans image, la
   petite de la carte ×3). La carte garde ses petites images. Style `rd_tile__tile_object`, une **échelle commune** : un
   humain fait environ 46 px de haut (à peu près une case), le Gobelin la moitié, l'Ogre plus grand, puis le Troll
   (orc < ogre < troll) ; l'Ange est grand comme le Troll ; le Cavalier et le Dragon, longs, couvriront 2 cases.
   Les cadres varient selon la créature (40 × 52 pour un humain, jusqu'à 96 × 80 pour le Cavalier) : Retro Diffusion
   remplit la hauteur du cadre, pas sa largeur. `tools/art/reduire.py` réduit une image sans la flouter (palette
   d'origine) et peut la retourner (`--miroir`). Des exemples d'images donnés par l'utilisateur (paysan, orc, gobelin)
   ont beaucoup aidé : décrire leur allure dans le texte de la demande.
   Essayés puis écartés : le style « classic » (trop dessin animé), le style « retro » (images carrées seulement,
   créatures de face et toutes de la même taille), redessiner une figure de King's Bounty (fond vert mêlé à la créature).
   ✅ **Le Cavalier et le Dragon sur 2 cases** (capacité `two-hex`, comme les grandes créatures de Heroes 3) : la tête
   devant, l'arrière sur la même rangée, du côté de son camp (au départ, l'arrière sur la colonne du camp). Elles ne vont
   que là où leurs deux cases tiennent, frappent et se font frapper par l'une ou l'autre ; les distances (tir, approche
   de l'IA) comptent la case la plus proche ; le souffle ne touche pas deux fois une grande cible. À l'écran, la créature
   est dessinée au milieu de ses deux cases, et un clic sur une case où irait son arrière l'y amène aussi.
   `--battle-showcase --battle-auto` : le combat de démonstration se joue seul.
2. Les animations, **à l'étape 10** (choix de l'utilisateur) : attente, marche, attaque, à partir de l'image de combat.
   Essai sur l'Épéiste : Retro Diffusion (0,14 $ l'animation, la marche glisse un peu) contre **PixelLab** (« Animate with
   text v3 », environ 0,03 $, pas mieux tenus, fond transparent) : PixelLab est retenu. L'écran de combat sait déjà jouer
   des feuilles de 8 images (`epeiste-idle.png`, `-move` (anciennement `-walk`), `-attack` : 4 colonnes × 2 rangées) ; sans feuille, l'image fixe.
   **Désormais : PixelLab pour les personnages et les créatures, Retro Diffusion pour les cartes et les portraits.**
   ✅ **Les créatures refaites une à une avec PixelLab** (choix de l'utilisateur) : d'abord l'image fixe (qui sert aussi
   d'icône), puis, une fois acceptée, ses animations. Fait : le **Squelette** (bossu, casque rouillé, haillons de
   mailles, mains griffues, d'après une image de référence ; attente, marche lente, attaque où il saisit l'adversaire).
   Il paraissait trop contrasté : c'était son contour noir (voir plus bas), maintenant adouci. L'**Ange** (grand,
   sérieux, sans barbe, longue robe blanche serrée d'une corde ; attente presque immobile, vol à grands battements d'ailes, frappe de la paume ouverte avec un arc de lumière).
   L'**Archer** (soldat d'armée en armure légère : chapeau de fer, gambison rayé bleu et blanc, col vert, grand arc, d'après
   une image de référence ; vu de trois quarts et un peu d'en haut, comme le Moine ; il bande l'arc et tire).
   Le **Paysan** (trapu, capuche et tunique brunes, fourche pointée en avant ; image faite par l'utilisateur sur
   PixelLab, réduite à ×0,4 : à ×0,5 il était aussi haut que l'Archer, mais deux fois plus massif ; il lève la fourche et la plante devant lui). Son image est élargie à gauche pour que
   le corps, pas la fourche, soit au centre de la case. Le **Dragon rouge** (dressé sur ses quatre pattes, ailes à
   demi levées ; attente, envol en planant, long jet de feu ; fait au double puis réduit à ×0,7, environ 90 × 80, pour
   couvrir ses deux cases). À cette taille, le modèle « Pro » ne rend qu'une image par 0,08 $ ; les animations se font
   aussi au double (jusqu'à 256 px), puis chaque image est réduite. Le jet de feu touchait le bord du cadre : son bout
   est effacé en trame. Le **Gobelin** (petit, peau jaune-vert, grandes oreilles, ventre rond, pagne, lance ;
   d'après l'image d'exemple ; environ 28 px de haut ; il s'accroupit puis plante sa lance). Une marche qui glissait (le
   corps dérivait de 5 px) a été recentrée image par image. L'**Orc** (trapu, peau verte, grandes défenses, sangles et pagne de cuir,
   haches ; d'après l'image d'exemple ; environ 41 px de haut, plus massif qu'un humain ; il lance une hache par-dessus
   l'épaule). L'**Ogre** (homme sauvage énorme, cheveux longs et barbe noirs, pagne de fourrure, grosse
   massue de bois sans clous ; 58 px de haut, plus grand que l'Archer ; il balaie de sa massue). Fait avec l'ancien Ogre
   comme image de style pour garder son ombrage doux, et **sans** réduire la palette (16 couleurs faisaient des bandes
   sur la peau). Une image de référence de pose recopie aussi ses détails (des clous sur la massue) : à éviter.
   Le **Colosse d'os** (futur mort-vivant le plus fort, pas encore dans les règles : `colosse-os`) : un amas chaotique
   d'os et de crânes en forme de géant, tête de crâne cornue aux yeux rouges, énorme fémur pour massue ; 70 px de haut
   pour tenir sur une case ; il balaie de son fémur à deux mains. D'après une image de l'utilisateur, mais pas recopiée
   (trop moderne, trop détaillée) : décrire l'idée en termes de jeu rétro (« classic 1990s fantasy strategy game
   sprite, simple bold shapes »). **L'angle de vue** (un peu d'en haut) vient d'une image de référence marquée « angle
   seulement » (`image.png::usage` dans `pixellab.py pro`) : le Troll a bien marché, mais seulement sans autre image
   du sujet (une image de sujet vue de côté impose son angle).
   Le **Chevalier** (remplacera l'Épéiste, pas encore dans les règles : `chevalier`) : armure de plaques d'acier,
   heaume fermé, fléau d'armes, bouclier bleu à fleur de lis d'or ; 54 px de haut, plus grand et plus massif que
   l'Archer ; d'après une image de l'utilisateur (le bouclier rouge à chevrons de la référence remplacé à sa demande).
   Le bouclier a été changé avec `retoucher` sur toute l'image (repeindre une seule zone, `repeindre`, demande un
   abonnement payant de PixelLab). Attente calme, marche lourde, attaque où il fait tournoyer le fléau au-dessus de
   sa tête avant de frapper devant lui. PixelLab dessine des éclats d'impact même quand on demande « no visual
   effects » : les effacer (et redessiner la boule du fléau qu'ils cachaient).
   Le **Fantôme** (futur mort-vivant, pas encore dans les règles : `fantome`) : le spectre d'une noyée, presque blanc
   aux reflets cyan, longs cheveux mouillés sur un visage sombre, mains griffues, robe en lambeaux qui finit en
   volutes à la place des pieds ; 52 px de haut ; d'après une image de « mariée fantôme » de l'utilisateur, puis des
   variations d'un premier candidat (F2) données en image de sujet. Attente où il se balance doucement, vol, attaque
   où il projette une gerbe d'eau de ses mains. Une animation peut dessiner un adversaire : la jeter.
   Le **Moine** et le **Loup** (images d'origine gardées, modèles de style) ont maintenant leurs animations, faites à
   partir de leur image doublée (pixels ×2) puis réduites. Le Moine prie, marche, et tend son bâton vers l'ennemi
   **sans rien lancer** : la boule de feu sera un effet du jeu (à faire ; le jeu ne dessine encore aucun projectile, ni les flèches de l'Archer). PixelLab
   dessine quand même éclairs et flammes : l'image de l'éclair est remplacée par la pose tendue, gardée au moment du
   coup. Le Loup renifle, trotte, et bondit gueule ouverte. PixelLab n'accepte que 8 animations à la fois.
   L'**Archer refait** (2026-11-06) : plus net, fait au double puis réduit (50 px), tabard bleu à fleur de lis d'or
   comme le bouclier du Chevalier. Sa **corde d'arc** (un trait d'un pixel) disparaissait à la réduction ou
   clignotait : les traits fins de la grande image sont remis après réduction, et une corde droite est tracée entre
   les deux bouts de l'arc au repos (même couleur sombre que celle de PixelLab), sur l'image fixe aussi. Il regarde
   autour de lui, rarement (pause de 10 à 20 s, 7 images par seconde). L'utilisateur trouve les animations un peu
   chargées, de peur qu'elles distraient du jeu : à revoir en jeu (pauses plus longues, mouvements plus discrets).
   **Méthode outillée** (2026-11-06, demandée par l'utilisateur) : `tools/art/creature.py` enchaîne les étapes pour
   une créature (`fixe`, `choisir … fixe`, `animer`, `retoucher`, `choisir … idle`, `page`) d'après sa **fiche**
   (`tools/art/creatures-art.json` : description, sujet, hauteur, taille de génération, modèle de style, image
   d'angle, références, miroir, corde, mouvements propres, choix faits) et un **gabarit** par sorte de créature
   (humain, volant, quadrupède, grand : fin du texte de l'image fixe, mouvements par défaut, et toujours « pas
   d'autre personnage, pas d'effet, pas de projectile » ; mouvements d'attente discrets). Les grandes images
   choisies sont gardées dans `art-candidats/references/derives/` (elles survivent à l'archivage). Chaque feuille
   est **vérifiée** (corps qui glisse, attaque qui ne revient pas à sa place, pixels en plus : autre personnage ou
   effet, éclats blancs ou de feu, image coupée au bord, traits fins qui clignotent), en comparant chaque image à la
   plus calme de la feuille ; les alertes s'affichent sur la **page de la créature** (`en-cours/<id>.html`), qui
   montre images fixes et animations ensemble, sur des cases du champ, face au défenseur retourné, avec les pauses
   du jeu. Deux essais par animation, alternant les deux modèles de PixelLab ; au plus 8 tâches à la fois (les
   autres attendent, `pixellab.py` attend aussi quand PixelLab refuse). En jeu : `--creature-preview=id,…` met ces
   créatures des deux côtés (une créature absente des règles reçoit des caractéristiques moyennes), avec un Archer
   pour l'échelle. Les fiches existent pour le Colosse, le Chevalier, le Fantôme, l'Archer, le Moine et le Loup.
   **Les effets à part** (décision de l'utilisateur, 2026-11-06) : les animations des créatures n'ont **aucun effet**
   (éclair d'impact, sang, traînée, projectile) ; c'est le jeu qui les dessinera, au moment du coup, pareils pour
   toutes les créatures et discrets : éclat du coup, flèche de l'Archer, boule de feu du Moine, gerbe d'eau du
   Fantôme… (étape 10). Les effets de PixelLab changent à chaque fois, se mêlent aux images et s'effacent mal.
   Proposé (2026-11-06) : l'animation ne montre que le geste (bander, viser, lâcher) ; la fiche de chaque tireur dit
   à quelle image et d'où part le tir (`launchFrame`, `launchPoint`, retourné pour le défenseur) ; le jeu fait voler
   le **projectile** jusqu'à sa cible, quelle que soit la distance (flèche en arc qui suit sa trajectoire, boule de
   feu droite, hache qui tourne), et il arrive au moment du coup ; un petit **impact** se joue sur la cible (choc,
   entaille, morsure, magie, eau), aussi pour les coups au corps à corps. Projectiles et impacts : de petites feuilles
   faites avec PixelLab, dans le même style. Peut-être un réglage « effets complets / discrets ». À faire après les
   images des créatures, en commençant par la flèche de l'Archer et la boule de feu du Moine.
   Le **Zombie** (futur mort-vivant de niveau 2, `zombie`) : un mort boursouflé, peau gris-violet, ventre énorme
   couvert de pustules jaunes, pointes de cristal violet sur le dos ; 44 px de haut (plus petit que l'Archer, l'Ogre
   est bien plus fort) ; d'après une image de l'utilisateur. Il tend la main en ouvrant la bouche, boite en traînant
   une jambe raide.
   La boiterie n'a pas marché avec PixelLab : à la place, une marche **lente** de 16 images (`moveFrames` : 16 dans
   animations.json, feuille de 4 × 4, `creature.py animer … --images=16`) à 8 images par seconde, et il avance
   lentement sur le champ (`stepTime` : 0,45 s par case au lieu de 0,09). Il frappe en se penchant en arrière, puis
   en poussant son énorme ventre. Les effets apparaissent moins quand le texte ne décrit que le mouvement du corps
   (sans « frappe », « coup » ni « pas d'éclair ») : `--sans=` retire la fin de texte du gabarit.
   Au combat, chaque troupe **regarde où elle va** (miroir selon le sens de la marche) et se tourne vers sa cible
   pour frapper ou tirer (la cible vers elle), puis se retourne vers le camp adverse.
   Le **Troll** (image d'origine gardée) a ses animations : il se gratte la tête (6 images par seconde), marche
   lourdement (7), et donne un « méga-coup de poing » (le bras étiré en traînée sur une image, gardée : choix de
   l'utilisateur). Textes de mouvement seuls (`--sans=no other character`) : aucun effet sur les six premiers essais.
   L'**Ogre** : nouvelle attente (il se gratte la cuisse) et nouvelle attaque (massue levée à deux mains puis abattue) ;
   sa grande image n'a que 66 px (faite avant l'outil). Le **Griffon** (image d'origine gardée) : attente, vol, attaque
   (il se cabre puis frappe des serres et du bec). L'animation « walk » s'appelle maintenant **« move »** (déplacement :
   marche ou vol ; fichiers `-move.png`, réglages `move` et `moveFrames`). La page d'une créature montre un déplacement
   qui traverse quatre cases à la vitesse du jeu, pour voir si le cycle se répète bien.
   **Vitesse et pauses des animations** (`art/creatures/battle/animations.json`, facultatif) : images par seconde par
   créature et par animation (`idle`, `move`, `attack`, 10 par défaut ; une attaque plus lente porte son coup plus
   tard), et une pause entre deux attentes (`idlePauseMin`, `idlePauseMax`, en secondes) : la créature garde sa pose de
   départ, puis joue son attente une fois. L'entrée `*` vaut pour toutes : 3 à 6 s de pause (choix de l'utilisateur, d'abord 5 à 10 s).
   Le Colosse est à 7 images par seconde.
   Ce qui marche (`tools/art/pixellab.py`) : l'image fixe avec le modèle « Pro » (`pro`, 25 candidats pour 0,08 $ à
   40 × 52), une de nos images pour le style (trait, ombrage) et une image de référence pour le sujet ; **modèles de
   style : le Moine, le Griffon, le Loup et le Troll** (choix de l'utilisateur ; plus l'Épéiste, à refaire) ; les animations
   à partir d'un cadre un peu plus grand (48 × 56, 64 × 56 pour une attaque qui va loin), avec `--retour` pour qu'une
   attaque finisse sur la pose de départ (`--minimax` a donné la meilleure prise). Chaque feuille peut avoir sa propre
   taille d'image. Écartés : « Bitforge » (`creer`) à cette taille, et une image de style trop forte (elle recopie la
   créature du modèle). PixelLab ajoute parfois des étincelles cyan à une attaque : les retirer. Il ajoute aussi un
   **contour noir** (87 % des pixels du bord presque noirs, contre 20 % pour le Moine et 12 % pour l'Ange), même avec une image de style
   sans contour : `tools/art/adoucir.py entree sortie 0.6 45 2` donne à ce bord (et à la ligne juste à l'intérieur) la
   couleur voisine assombrie, sans dépense ; à faire sur l'image fixe et sur les feuilles d'animation. Demander dans le
   texte « vu de trois quarts, un peu d'en haut » (pas de réglage de vue dans le modèle « Pro »).
   Une image générée à ~50 px est brouillonne (trop de détails pour trop peu de pixels) : mieux vaut la **générer au
   double** (~100 px de haut) puis la réduire de moitié (`reduire.py … 0.5`), comme les paysans de l'utilisateur.
   Puis `tools/art/palette.py entree sortie 16` ramène à 16 couleurs (le Moine en a 20 ; adoucir et réduire créent
   des teintes intermédiaires, jusqu'à 250) : gain léger mais net en grand.
3. Les fonds de champ de bataille et les obstacles (point 5 ci-dessus), dans le même style détaillé : étape 10.
4. Plus tard : le héros sur la carte, les bâtiments des villes, et d'autres images plus détaillées : étape 10.

### 7. Refonte de l'interface (prochaine étape)
Décision de l'utilisateur : l'interface actuelle ne convient pas ; on la repense **avant** d'en soigner l'apparence,
maintenant que l'essentiel des règles est connu (on sait ce qu'il faut montrer). On garde les cadres et couleurs
actuels : il s'agit de la structure et de la lisibilité, l'apparence viendra à l'étape 10.
Méthode : d'abord des maquettes simples (boîtes grises) de chaque écran, corrigées par l'utilisateur, puis on les
construit écran par écran.
Constats de départ (captures du 2026-11-04) :
- Carte : le panneau de droite est une colonne de texte (caractéristiques, armée en liste « 30 Paysans », héros et
  villes en un bouton ou une ligne, terrain survolé mêlé aux informations du héros) ; beaucoup d'actions ne sont que
  des touches (H, T, M, F, F5, F9) ; la boîte des messages flotte sur la carte ; la barre des ressources ne montre
  pas les revenus.
- Ville : une grille de cartes de bâtiments, pas une ville ; bâtiments construits et à construire prennent la même
  place ; construire, recruter et la taverne sont mêlés ; l'échange de troupes (un clic) n'est expliqué nulle part et
  on ne peut pas partager une troupe.
- Partout : presque tout est du texte, peu d'icônes, peu de hiérarchie, pas d'aide sur les règles.
Pistes (à valider sur maquettes), en prenant Heroes 2 et 3 comme modèles :
- Carte : à droite, la carte réduite, la liste des héros et celle des villes (portraits), l'armée du héros choisi en
  7 emplacements avec images, des boutons pour les actions (héros suivant, creuser, carte au trésor, sauvegarder…),
  une boîte d'état (date, ressources et revenus) ; les messages hors de la carte.
- Ville : construire, recruter, taverne et échange de troupes bien séparés ; partage d'une troupe.
- Nouveaux écrans : écran du héros (caractéristiques, armée, artefacts), menu principal (nouvelle partie, charger,
  options, quitter), peut-être une vue d'ensemble du royaume.
- Combat et fenêtres (niveau, recrutement, fin de partie) revus avec les mêmes règles.
- Des info-bulles qui expliquent les règles (charisme, coût du déplacement, danger d'un monstre…).
Décidé sur les maquettes (2026-11-04) :
- Carte : à droite, la carte réduite, les listes des héros et des villes, le héros choisi (caractéristiques en icônes,
  mouvement, artefacts ; un clic sur le portrait ouvre l'écran du héros), son armée en 7 emplacements, 7 boutons
  (héros suivant, avancer, centrer, royaume, creuser, carte au trésor, options : avec la touche écrite dessous) et un
  grand « Fin du tour ». Pas de bouton « Ville » (la liste des villes le remplace ; la touche T reste). L'objectif de
  la partie s'affiche au survol de la date. Les revenus restent dans la barre du haut. Sous la carte, une bande :
  ce qui est survolé, et le dernier message (avec l'historique).
- Menu principal (nouvelle partie, charger, quitter), seulement quand le jeu est lancé sans option : toute option de
  test (`--seed`, `--load`, `--town`, `--battle-…`, captures) entre directement dans la partie, et `--play` saute le menu.
- Ville : la barre des ressources tout en haut, à la même place que sur la carte. Au centre, **la ville vue de face**
  (comme Heroes 1 et 2) avec chaque bâtiment construit à sa place (`buildings.json`) ; **un nouveau bâtiment apparaît
  peu à peu** (la « magie » de Heroes, demandée par l'utilisateur). Garder des places libres pour les bâtiments à venir
  (guilde des mages, marché…). Clic sur un bâtiment pour l'utiliser ; raccourcis Construire, Recruter, Taverne ;
  construire et recruter s'ouvrent en fenêtres par-dessus la ville. En bas, toujours visibles : la garnison et le héros,
  avec **glisser-déposer et clic** pour les troupes, et le partage d'une troupe. La taverne propose **deux héros**.
  Le décor viendra à l'étape 10 (Retro Diffusion) ; d'ici là, des boîtes à la place des bâtiments.
- Écran du héros (grande fenêtre sur la carte) : portrait, nom, **classe** (place gardée pour l'étape 9), niveau,
  expérience et mouvement ; **caractéristiques en colonne**, avec ce que fait chacune ; artefacts sur une silhouette ;
  une section **Magie** à prévoir (points de sort, livre de sorts, compétences : étape 9) ; l'armée en grand, avec les
  points de commandement de chaque troupe et les troupes à risque ; fiche d'une créature au clic droit.
- Royaume (touche K) : les héros en **portraits** (mouvement restant dessous) et les villes en **images** (revenu
  dessous), le détail au survol ; une bande « mines et revenus » en bas (par ressource : mines et revenu du jour).
- Menu principal : Nouvelle partie, Continuer, Charger, Réglages, Quitter. **Plusieurs sauvegardes** (nom, date dans
  le jeu, héros, taille de la carte, heure), F5 / F9 gardent une sauvegarde rapide, et une **sauvegarde automatique**
  au début de chaque tour (une seule, remplacée à chaque fois). Nouvelle partie : **taille de la
  carte** en cinq choix (très petite à très grande), la **graine**, les façons de gagner et la limite de jours.
- Combat : le champ et la ligne du bas restent ; à gauche, le héros (portrait, bonus ; plus tard son livre de sorts) et
  la fiche de la troupe qui joue ; à droite, l'adversaire et la fiche de la troupe survolée (ses caractéristiques
  seulement : **pas de dégâts attendus**, trop d'aide) ; un journal du combat ; les touches sur les boutons.
- Fenêtres : un **bilan après chaque combat** (pertes des deux camps en images, expérience, révoltes, fuite) ; niveau
  supérieur en grands boutons (avant → après, effet) ; recrutement avec « Max » et l'avertissement de charisme ; fin de
  partie (pourquoi, quelques chiffres, menu principal ou revoir la carte).
Maquettes : `art-candidats/archives/` (sujet « maquettes-interface » : carte, ville, héros-royaume-menu, combat-fenêtres).

Découpage (une PR par point, chacune jouable et testée ; cadres et couleurs actuels, l'apparence viendra à l'étape 10) :
1. ✅ **La carte**, avec les pièces communes : barre des ressources (`ResourceBar` : revenus, d'où ils viennent au
   survol, objectif et progrès au survol de la date), emplacements d'armée (`ArmySlots` : images, nombres,
   caractéristiques au survol, bordure rouge si risque de révolte) ; la colonne de droite (carte réduite, listes des
   héros et des villes avec flèches au-delà de trois, fiche du héros choisi avec ses caractéristiques expliquées au
   survol, armée, 6 boutons avec leur touche, grand « Fin du tour ») ; la bande sous la carte (survol sur deux lignes :
   terrain, coût et jours de marche, puis l'objet et le danger estimé ; le dernier message ; le journal des 60
   derniers). La boîte des messages flottante disparaît ; les options ne gardent que sauvegarder et charger.
   Creuser et la carte au trésor ont leur bouton dans toutes les parties (le trésor est toujours enfoui).
   Le bouton Royaume viendra avec son écran (point 2). `--hover=x,y` simule la souris sur une case (captures).
2. ✅ **L'écran du héros et le royaume** (`HeroScreen`, `KingdomScreen`, sur une base commune `ScreenWindow` :
   la carte s'assombrit, Fermer ou Échap). Héros : portrait ×2, niveau, barres d'expérience et de mouvement,
   caractéristiques en colonne avec leur effet (le détail au survol), artefacts (image et effet ; la silhouette quand
   il y en aura plus), place réservée à la magie, armée en grand avec les points de commandement. S'ouvre d'un clic
   sur le portrait du panneau ou d'un double clic dans la liste. Royaume (bouton ou touche K) : héros en portraits
   (mouvement dessous), villes en images (revenu, ● si l'on peut y construire), détail au survol, bande des mines et
   revenus (clic : centrer la carte sur la mine suivante). Les boutons du panneau passent à 3 colonnes (nom court,
   touche dessous, nom complet au survol) avec Options sur toute la largeur. Réorganiser l'armée viendra avec la
   ville (point 3).
3. ✅ **La ville** (`TownScreen` refait) : la barre des ressources en haut (à la même place que sur la carte), le nom,
   le revenu et si l'on peut construire ; au centre la ville vue de face, chaque bâtiment construit à sa place
   (`view` dans `buildings.json` : x, y, largeur, hauteur dans une vue de 1280 × 390 ; `replaces` : l'hôtel de ville
   remplace la salle du village, à la même place), en boîtes en attendant les images ; des places libres pour la
   guilde des mages (en haut à gauche) et le marché (en haut à droite). **Un nouveau bâtiment apparaît peu à peu**
   (il monte du sol et devient net, avec de la poussière), sur l'événement `BuildingBuilt`. Un clic sur un bâtiment
   l'utilise ; raccourcis Construire, Recruter, Taverne. Fenêtres : Construire (tout l'écran : ce qui reste à bâtir,
   puis les bâtiments construits), Recruter (sur la vue seulement : les 7 créatures dans l'ordre, ou celle d'une
   habitation ; « Recruter… » avec le curseur, « Tout »), Taverne (deux héros : `TavernOffers`, `HireHero(ville,
   nom)`). En bas, la garnison et le héros présent (`ArmySlots`, `ArmySlot`) : glisser une troupe la déplace, la
   fusionne ou l'échange ; Maj en lâchant ou clic droit pour la partager (`Army.MoveTroop`, `GameState.MoveTroop`,
   événement `TroopMoved`) ; un clic la fait passer de l'autre côté ; « Tout donner au héros », « Tout laisser en
   garnison ». Une armée peut maintenant avoir deux troupes de la même créature (gardées par la sauvegarde :
   `Army.AddStack`). L'écran du héros permet aussi de réordonner et de partager ses troupes. Tests : `TroupesTests`.
4. ✅ **Le combat** : le champ s'élargit de chaque côté de la grille 11 × 9 (le sol continue,
   `BattleFieldView.SideMargin`) : le héros y est dessiné à gauche (son image de la carte en attendant une image de
   combat, à cheval ou non), un clic montre ses caractéristiques ; cette place servira aussi aux machines de guerre
   (catapulte, baliste…). Pas de panneaux de côté (choix de l'utilisateur) : un **clic droit maintenu** sur une troupe
   montre sa fiche (caractéristiques, PV de tête, capacités, ripostes ; pas de dégâts attendus). « Journal » au bout
   de la ligne de la dernière action ; touches W (attendre), D (se défendre), A (auto), F (fuir), écrites sur les
   boutons. Le **bilan** s'ouvre après chaque combat, tactique ou rapide : l'expérience gagnée d'abord (et une
   révolte), puis nos pertes, puis les ennemis tués, en images sans cadre.
   ✅ **Écran de 1920 × 1080** (choix de l'utilisateur, une PR à part) : la fenêtre fait 1920 × 1080 (`project.godot`).
   Les écrans restent décrits dans les unités de l'ancien écran (1280 × 720) et sont mis à l'échelle ×1,5 au même
   endroit (`Ui.Scale`, `Ui.Px`, `Ui.V`, `At`, `TextSize`, `Box`) : une autre échelle pourra devenir un réglage. Les images en
   pixel art sont toujours agrandies d'un nombre entier de fois, pour des pixels réguliers : carte ×3 et ×4 (zooms
   1,5 et 2 de `MapCamera`), créatures au combat ×3 (cases de 110 × 126, marges de 318), sol et obstacles ×3, héros
   au combat ×4, cadres et boutons ×2, portraits ×1, ×2 ou ×3, créatures de l'interface ×2 à ×4.
5. ✅ **Les fenêtres** : niveau supérieur en une seule fenêtre (portrait, gain de charisme, un grand bouton par
   caractéristique avec avant → après et son effet ; la fenêtre de résumé a disparu, le message reste dans le
   journal) ; recrutement avec l'image, les caractéristiques, ce qu'on peut payer et où vont les recrues ; fin de
   partie : victoire ou défaite, pourquoi, le jour, les chiffres de la partie (combats gagnés, villes prises, créatures
   tuées et perdues, meilleur héros : `Player.Statistics`, sauvegardés), puis « Nouvelle partie » ou « Revoir la carte ».
   Le jeu démarre en plein écran (en 1920 × 1080, la fenêtre passait sous la barre des tâches) ; F11 ou Alt+Entrée,
   ou le bouton des options, passe en fenêtre agrandie. Les options ont un bouton « Quitter le jeu » (avec confirmation).
   Échap ouvre les options sur la carte. Les touches ne sont plus écrites sur les boutons (carte, combat, ville,
   fenêtres) : elles sont dans l'info-bulle (choix de l'utilisateur).
6. ✅ **Menu principal, nouvelle partie, sauvegardes** : le menu (`MainMenu` : Nouvelle partie, Continuer, Charger,
   Réglages avec le plein écran, Quitter) s'ouvre quand le jeu est lancé sans option ; toute option (ou `--play`)
   entre directement dans une partie, `--menu` le montre quand même. On y revient depuis les options ou la fin de
   partie (« Menu principal »). Nouvelle partie (`NewGameWindow`) : **taille de la carte** en cinq choix (`MapSize` :
   36 × 27, 54 × 27, 54 × 40, 72 × 53, 90 × 66, soit 4, 6, 9, 16 et 25 régions ; le générateur s'y adaptait déjà),
   la graine (vide : au hasard), les façons de gagner et la limite de jours. La simulation d'équilibrage gagne sur les
   petites cartes (jours 54 à 106) ; sur une grande, vers le jour 146, et pas en 200 jours sur une très grande (bien
   plus de monstres : à revoir avec l'IA). **Plusieurs sauvegardes** (`SaveFiles`, `SavesWindow` ; dossier
   `user://sauvegardes/`, un fichier par nom) : nom, jour, héros et niveau, taille de la carte, heure
   (`GameState.ReadSummary`) ; supprimer, remplacer (avec confirmation). F5 / F9 : la sauvegarde rapide ; une
   **sauvegarde automatique** à chaque fin de tour. L'ancien fichier unique devient la sauvegarde rapide.
   Corrigé au passage : le générateur pouvait poser le gardien d'une mine derrière elle, dans un cul-de-sac (on ne
   pouvait jamais vaincre tous les monstres) ; les monstres qu'aucun chemin n'atteint sont retirés.
7. **Revue des écrans** (captures de tous les écrans, `art-candidats/en-cours/revue/`, 13 points relevés et corrigés) :
   une seule sorte de petite fenêtre (`GameDialog` : sans barre de titre de Godot ni croix, le cadre du jeu, le titre en
   haut à gauche comme les grandes fenêtres, les boutons en bas avec une largeur minimale, Échap annule) ; le voile
   des grandes fenêtres couvre aussi le haut de la colonne de droite ; la zone visible bien cadrée sur la carte réduite ;
   les noms des héros et des villes sur deux lignes dans les listes ; « Fin du tour » d'une hauteur ordinaire ; les
   raccourcis de la ville au-dessus de la vue ; un bouton seulement quand on peut agir, sinon le texte qui dit pourquoi
   (construire, taverne) ; la fenêtre Construire montre tous les bâtiments ensemble, dans l'ordre (les construits
   marqués « Construit »), et suit son contenu ; « Fermer » bien à l'intérieur du cadre ; les boutons du niveau supérieur alignés.
   À faire ensuite : une nouvelle revue des captures avec l'utilisateur.

### 8. Adversaires IA ✅
- Joueurs ordinateur avec leurs héros et leurs villes.
- L'IA joue avec les mêmes méthodes de `GameState` que le joueur humain, sans attendre l'affichage.
- L'affichage rejoue les déplacements des héros ennemis (événements `HeroMoved`) à un rythme lisible.

Choix faits (avec l'utilisateur, 2026-11-05) : **1 à 3 adversaires**, choisis dans la nouvelle partie (1 par défaut, selon la
taille de la carte) ; ils jouent aussi le **Château** (les autres factions viendront à l'étape 9), dans d'autres couleurs ;
**pas de triche** (l'IA ne voit que ce qu'elle a exploré, mêmes revenus) ; un seul niveau de difficulté pour commencer ;
une nouvelle façon de gagner, **« Vaincre tous les adversaires »**, cochée d'office quand il y en a.

Découpage (une PR par point, chacune jouable et testée) :
1. ✅ **Plusieurs joueurs** : un départ par joueur aux coins de la carte (`AdventureMap.Starts`, `MapGenerator.Players` :
   le joueur humain en haut à gauche, puis le coin opposé, puis les deux autres) ; le danger des zones se compte depuis
   le départ le plus proche (le milieu devient le plus dangereux) ; une ville neutre près de chaque départ, dans une zone
   voisine plus proche de ce joueur que des autres, du côté de son départ ; le trésor le plus loin possible de tous ;
   pas de monstre à moins de 4 cases d'un départ. Seul, la carte ne change pas. Au plus 1 adversaire sur les très
   petites et petites cartes, 2 sur la normale, 3 sur les grandes (`MapSizes.MaxOpponents`). Joueurs Rouge, Bleu,
   Vert, Orange (`Player.IsComputer`, sauvegardé) ; les héros des adversaires (Sire Yvain, Dame Viviane, Brann le
   Rouge) ont la même armée de départ. Couleurs des joueurs : drapeaux des mines et des villes, étendard du héros (le
   rouge de l'image prend la teinte du joueur), carte réduite (les héros adverses là où l'on a exploré). Les tours
   passent d'un joueur à l'autre ; l'adversaire joue aussitôt après le joueur (`ComputerPlayer.PlayTurn` : pour
   l'instant, il passe son tour). « Vaincre tous les adversaires » (`VictoryCondition.DefeatOpponents`), cochée d'office
   quand il y en a. Nouvelle partie : le nombre d'adversaires (0 à 3, estompé au-delà de ce que la carte accueille).
   Tests : `AdversairesTests`.
2. ✅ **L'IA explore et ramasse** (`ComputerPlayer`) : chaque héros va vers le meilleur objet qu'il peut prendre sans
   combat (le plus proche ; une mine compte double, elle rapporte chaque jour ; les obélisques qu'il n'a pas lus), et
   sinon explore (la case connue la plus proche au bord de l'inconnu). Pas de triche : il ne passe que par les cases que
   son joueur a explorées, jamais dans la zone d'un monstre ni à travers un objet. Niveaux : l'attaque, sinon la
   défense. Combats en rapide s'il en arrive un (par sécurité). Sans combat, il attend aux passages gardés (point 4).
   À l'écran : les pas des héros adverses là où le joueur a exploré sont rejoués à un rythme lisible, la vue les suit
   (« Bleu se déplace… ») ; pas de compte rendu de combat pour les combats des autres. Option `--turns=N` (joue N jours
   au lancement). Tests : `ExplorationIATests`.
3. ✅ **L'IA combat les monstres et prend les villes neutres** (avant de construire : sans ville, elle ne peut rien
   faire, et la ville neutre a une garnison). Elle attaque un monstre ou une ville neutre si la prévision (la même que
   le danger affiché à l'interface) donne une victoire en perdant au plus 30 % de la valeur de son armée
   (`ComputerPlayer.AcceptableLosses`, valeur = l'or des créatures). Un monstre est abordé par une case de sa zone que
   lui seul garde (un seul combat à la fois) ; une ville neutre compte beaucoup, une mine moins, un monstre moins
   encore. Sur trois cartes normales à deux adversaires, en quatre semaines : 2 à 4 combats gagnés chacun, la ville
   prise 5 fois sur 6 ; sans recruter, leurs armées fondent et leur or s'accumule (point 4). Le joueur peut déjà prendre
   les villes des adversaires (leur garnison se défend). Tests : `CombatIAAdversaireTests`.
4. ✅ **L'IA construit et recrute** : chaque jour, dans chaque ville, un bâtiment (dans l'ordre de la simulation
   d'équilibrage : tour des archers, taverne, hôtel de ville, tour des griffons…), un héros de taverne s'il en manque
   (un de plus que de villes, trois au plus : l'or est mis de côté pour lui), puis toutes les troupes qu'elle peut payer,
   les plus fortes d'abord. Un héros présent emporte la garnison sans dépasser son charisme (pas de révolte) ; un héros
   revient en ville quand la garnison vaut au moins la moitié de son armée. Sur trois cartes normales à deux
   adversaires, en six semaines : 5 à 9 bâtiments, 2 ou 3 héros, une deuxième ville souvent, des armées jusqu'à
   12 000 or ; un adversaire dont la ville voisine est trop bien gardée reste bloqué. Tests : `VillesIATests`.
5. ✅ **Les héros s'affrontent** : un héros attaque le héros d'un autre joueur en marchant vers lui (le chemin va jusqu'à
   lui ; le pas sur sa case déclenche le combat, comme contre un monstre). Le héros attaqué est l'adversaire du combat
   (`HeroDefender`) : son attaque et sa défense s'ajoutent à celles de ses troupes (`TacticalBattle`, `defenderHero`).
   Le vaincu disparaît ; le vainqueur gagne la valeur des troupes tuées (celui qui se défendait aussi). Un joueur sans
   héros ni ville est éliminé (`PlayerDefeated`). L'IA attaque les héros et les villes des autres joueurs (le joueur
   comme les autres adversaires) quand la prévision le permet (avec le bonus du héros adverse) ; ses combats sont
   rapides. À l'écran : survol d'un héros adverse (nom, couleur, danger), curseur d'attaque ; quand le joueur est attaqué
   (héros ou ville), le bilan s'ouvre de son point de vue, et le journal le dit, comme une ville perdue ou un joueur
   éliminé. Tests : `HerosContreHerosTests`.
   ✅ **Se défendre soi-même** : attaqué (son héros, ou une ville avec sa garnison), le joueur choisit de diriger sa
   défense ou le combat rapide (`GameState.HumansDefend` : le combat attend, même au tour d'un adversaire ; l'adversaire
   reprend son tour ensuite, `ComputerPlayer.PlayTurn` repart d'où il en était). Sur l'écran de combat, le joueur joue
   alors le camp de droite (`BattleScreen`, `playerSide`), ses troupes gardent le bleu, il ne peut pas fuir. Les deux
   héros sont dessinés, chacun à ses couleurs : l'attaquant à gauche, celui qui se défend à droite, tourné vers la
   gauche (aussi quand le joueur attaque un héros adverse). Pendant le tour des adversaires, le panneau, le brouillard
   et la carte réduite restent ceux du joueur, et il ne peut pas agir. Option `--defense-test` (et `--defense-tactical`).
6. ✅ **Niveaux de difficulté** : un **profil** de réglages par niveau (`ComputerProfile`, `GameRules.Difficulty`,
   sauvegardé ; les anciennes parties sont en Normal), choisi dans la nouvelle partie (désactivé sans adversaire) :

   | | Facile | Normal | Difficile |
   |---|---|---|---|
   | Pertes acceptées | 20 % | 30 % | 40 % |
   | Attaque le joueur à partir du jour | 21 | 0 | 0 |
   | Héros au plus | 2 | 3 | 4 |
   | Revenu d'or | 75 % | 100 % | 125 % |

   En mesurant, des défauts de l'IA sont apparus et ont été corrigés (valables à tous les niveaux) : elle évitait aussi
   les monstres qu'elle n'avait jamais vus (et restait bloquée devant un gardien caché) : elle ne craint plus que les
   monstres connus, et revoit son plan pas à pas quand un monstre apparaît ; sans ville, elle ne fait que des combats
   faciles (10 % de pertes au plus) et garde son armée ; bloquée, elle accepte des pertes doubles, mais seulement pour
   un monstre qui ouvre du pays nouveau ; chaque combat est prévu trois fois (tirages différents) et il faut le gagner
   chaque fois (une victoire de justesse à la prévision pouvait être une défaite). Et le générateur : avec des
   adversaires, le danger se compte sur la même échelle que seul, et les gardiens des sorties de chaque zone de départ
   sont réduits pour que l'armée de départ les batte (35 % de pertes au plus) ; seul, rien ne change.
   Mesuré (un adversaire, six cartes normales, six semaines) : Facile prend 1 ville, Normal 5, Difficile 5 ; armée
   moyenne 1 600, 3 500 et 5 200 en or. Encore à améliorer (par l'entraînement, point 7) : sur trois cartes sur six,
   l'adversaire reste dans son coin. Option `--difficulty=easy|normal|hard`. Tests : `DifficulteTests`.
   ✅ **Un château au départ** (proposé par l'utilisateur) : chaque joueur commence à l'entrée de son propre château
   (salle du village et corps de garde, garnison vide), posé sur son départ (`MapGenerator.PlaceStartingTown`,
   `StartingTowns`, vrai par défaut). Il peut construire et recruter dès le premier jour ; les villes neutres restent à
   prendre plus loin. Sans château, les adversaires dont la ville voisine était trop bien gardée restaient bloqués.
   14 noms de villes (les cartes d'une même graine changent). Mesuré de nouveau (un adversaire, six cartes normales,
   six semaines) : l'adversaire a environ 3 villes à chaque niveau, 2 à 4 héros, plus de 20 combats gagnés, une
   armée de 14 000 à 16 000 en or en moyenne (1 600 à 5 200 avant) ; sur une ou deux cartes, elle reste petite ; la simulation d'équilibrage (seul) gagne toujours. Les tests qui demandent une carte sans
   château passent `startingTowns: false`.
7. Plus tard (étape 10) : **entraîner les profils par des parties entre IA** jouées en arrière-plan (les règles tournent
   sans affichage : un mois de jeu se simule en une seconde environ) ; garder les réglages qui gagnent le plus souvent
   pour un « Difficile » solide, et vérifier que Facile l'est vraiment. Écarté : un réseau de neurones (lourd, difficile
   à régler, sans intérêt à cette taille).

### 9. Contenu (prochaine étape)
Après l'IA (choix de l'utilisateur), qui devra ensuite apprendre la magie et les nouvelles factions.
- Magie, autres factions, nouveaux objets sur la carte.
- **Magie** (commencée en premier, choix de l'utilisateur, 2026-11-06), proche de Heroes 2 et 3 :
  - **Chaque héros a un livre de sorts** (les classes de héros rendront plus tard certains meilleurs en magie).
  - **Points de sort** : 10 par point de savoir ; le héros commence plein, en regagne 1 par jour, et tous en entrant
    dans une ville qui a une guilde des mages (ou en y commençant la journée).
  - **Puissance** : la force des sorts (dégâts : une base plus tant par point de puissance) et leur durée
    (puissance + 1 rounds).
  - **Guilde des mages** de 4 niveaux dans la ville (à la place gardée en haut à gauche de la vue) ; chaque niveau
    offre quelques sorts de ce niveau, tirés au sort pour chaque ville (3, 3, 2 et 2) ; un héros qui entre dans la
    ville les apprend.
  - **Au combat** : le héros lance **un sort par round**, pendant le tour d'une de ses troupes, sans lui prendre son
    tour (comme dans Heroes 3). Livre de sorts (bouton et touche), puis clic sur la cible. Les monstres et les
    garnisons sans héros n'en lancent pas. Le combat rapide et les adversaires en lancent aussi (une règle simple :
    le sort qui fait le plus de mal à l'ennemi, en valeur, ou qui aide le plus sa meilleure troupe).
  - **Sorts de combat seulement** pour commencer (12, `data/spells.json`, chiffres à régler) :

    | Niveau | Sort | Points | Effet |
    |---|---|---|---|
    | 1 | Flèche magique | 5 | 10 + 10 × puissance dégâts à une troupe ennemie |
    | 1 | Bénédiction | 5 | une troupe amie fait ses dégâts maximum |
    | 1 | Malédiction | 6 | une troupe ennemie fait ses dégâts minimum |
    | 1 | Peau de pierre | 5 | +5 défense à une troupe amie |
    | 2 | Éclair | 10 | 10 + 25 × puissance dégâts à une troupe ennemie |
    | 2 | Hâte | 6 | +3 vitesse à une troupe amie |
    | 2 | Lenteur | 6 | vitesse divisée par deux pour une troupe ennemie |
    | 2 | Soins | 6 | soigne 10 + 5 × puissance PV et retire les sorts néfastes (troupe amie) |
    | 3 | Boule de feu | 15 | 10 + 10 × puissance dégâts sur une case et ses 6 voisines (amis compris) |
    | 3 | Faiblesse | 8 | −6 attaque à une troupe ennemie |
    | 4 | Résurrection | 20 | ramène 40 + 20 × puissance PV de créatures tuées dans ce combat (troupe amie) |
    | 4 | Pluie de météores | 16 | 25 + 25 × puissance dégâts sur une case et ses 6 voisines (amis compris) |

  - Plus tard : les sorts de la carte (portail de ville, porte dimensionnelle, vision…), les écoles de magie et les
    compétences (avec les classes de héros), la résistance à la magie de certaines créatures, une guilde de niveau 5.
  Découpage (une PR par point) :
  1. ✅ Les règles des sorts (`Core`) : `data/spells.json` (`SpellType`, `SpellCatalog`, vérifiés au chargement) ;
     `Hero.SpellPoints`, `MaxSpellPoints`, `Spells` (par niveau), `Learn` ; au combat (`TacticalBattleSpells`) :
     `CanCast`, `SpellTargets`, `AffectedBy`, `Cast` (un sort par round et par camp, la troupe garde son tour ; le
     sort qui dure est posé sur la troupe, `ActiveSpell`, et remplace son contraire), la vitesse change aussi l'ordre
     de jeu. Les points sont comptés à part pendant le combat et retirés au héros au bilan
     (`BattleReport.AttackerSpellPointsSpent`) : une estimation ne coûte rien. L'IA des troupes (`BattleAI.ChooseSpell`)
     lance le sort qui vaut le plus en or (points de vie ôtés ou rendus, part de la valeur de la troupe pour un sort qui
     dure ; une boule de feu qui blesse ses propres troupes compte en moins). Sauvegardés (points et sorts ; les
     anciennes parties ont un héros plein et sans sort). Personne n'apprend encore de sort (la guilde, point 2).
     Tests : `MagieTests`.
  2. ✅ La guilde des mages : 4 bâtiments (`guilde-mages-1` à `-4`, `mageGuild` dans `buildings.json`, chacun remplace
     le précédent à la même place, en haut à gauche de la vue) ; niveau 1 : 2000 or, 5 bois, 5 minerai ; niveaux 2 à 4 :
     1000 à 1500 or, du bois, du minerai et 2, 4, 6 gemmes ; le niveau 3 demande l'hôtel de ville, le 4 le grand hôtel
     de ville. Les sorts d'un niveau sont tirés au sort quand il est construit (le hasard de la partie, sauvegardés :
     `Town.GuildSpells`). Un héros apprend les sorts et retrouve tous ses points en entrant dans la ville, en y étant
     recruté, ou quand un niveau est construit pendant qu'il y est (`SpellsLearned`, dans le journal) ; une journée
     commencée dans la ville recharge aussi ses points. Les adversaires construisent la guilde (après la tour des
     griffons, puis un niveau après le monastère, le grand hôtel de ville et le portail de gloire). Écran de ville : un
     raccourci « Guilde » et un clic sur le bâtiment ouvrent la fenêtre de la guilde (les sorts par niveau, leurs points
     et leur effet calculé d'après la puissance du héros présent, « connu »). Tests : `GuildeTests`.
  3. ✅ L'écran de combat : un bouton « Sorts » (touche S), actif quand le héros du joueur peut encore lancer un sort ce
     round ; le livre de sorts (points qui restent, chaque sort avec ses points et son effet selon la puissance, grisé
     sans assez de points) ; puis la cible : les troupes visables en bleu, la zone touchée sous la souris, curseur
     d'attaque ou de visite ; clic droit ou Échap : annuler. Le sort s'allume sur ses cases (feu, vert, violet, or), puis
     les dégâts, les soins ou le nom du sort montent ; un petit losange par sort qui dure à côté du nombre de la troupe
     (vert : utile, violet : néfaste), et la fiche de la troupe (clic droit) dit les rounds qui restent. Le journal raconte
     chaque sort. Un clic sur le héros montre ses points de sort et ses sorts. Écran du héros : la section Magie (points
     de sort, chaque sort du livre avec son niveau, ses points et son effet) ; la puissance et le savoir disent ce qu'ils
     font. Options `--spells` (le héros connaît tous les sorts) et `--battle-book` (avec `--battle-test` : le livre
     s'ouvre ; `--battle-book=id` : ce sort est choisi).
  4. L'équilibrage (simulation et adversaires, avec la magie) et des icônes de sorts (Retro Diffusion ou PixelLab).
- **Factions** (demandé par l'utilisateur) : à terme **6 factions + 1 faction neutre**, chacune avec ses créatures
  (le Château et les neutres existent déjà). À concevoir ensemble : thème, 7 niveaux de créatures, rôle de chacune.
  Déjà décidé (2026-11-05) : des **morts-vivants** avec le Zombie, le Fantôme, le Vampire, la Liche et, au sommet, un
  **Colosse d'os** (un géant fait des os de tout un champ de bataille, aussi fort que l'Ange ou le Dragon ; préféré au
  Dragon d'os, au Spectre et au Chevalier noir, trop faibles pour le dernier niveau). Le Squelette pourrait les rejoindre.
  Au Château, l'Épéiste deviendra un **Chevalier**. Leurs images sont préparées d'avance (`chevalier`, `zombie`,
  `fantome`, `vampire`, `liche`, `colosse-os`), chacune d'après une image de référence de l'utilisateur.
- **Personnalisation des héros** (demandé par l'utilisateur) : à préciser ensemble. Pistes : choisir son héros
  de départ (nom, portrait), une classe ou un passé (chevalier, voleur, barbare, sorcière…) qui donne des bonus et
  des caractéristiques de départ différentes, des compétences à choisir en montant de niveau, et l'apparence du
  héros sur la carte. Les portraits marqués (étape 5) vont dans ce sens.
- **Habitations sur la carte** (demandé par l'utilisateur) : des lieux semés au hasard où un héros peut acheter des
  troupes (comme les habitations extérieures de Heroes). À concevoir plus tard : quelles créatures, réserve et
  croissance, prix, garde éventuelle.

### 10. Finitions : apparence et détails
Une fois les écrans fixés (étape 7) et le contenu en place.
- Interface dessinée avec PixelLab : cadres, boutons, icônes des ressources, cadres des portraits, bandeau de la date.
  Premier essai fait (2026-11-04, 0,24 $) : 4 cadres, 4 boutons et un cadre de l'outil « Create UI panel », bien
  découpés en 9 morceaux à l'échelle 1. Le jeu lit la taille des coins et les marges du texte dans `art/ui/ui.json`
  (facultatif). Les bords sont plus épais : il faudra agrandir les boîtes de taille fixe (barre des messages…).
  ✅ Avancé pendant l'étape 7 (2026-11-04, environ 1,30 $) : les cadres de l'essai étaient trop épais. Des cadres
  demandés plus petits (64 × 64) ont des bords fins ; choisi le cadre **B2** (argent, une fine bande de bois), affiché
  ×3, et le bouton **A1** (pierre grise bordée d'argent, 64 × 24, ×3), proches de Heroes 1 et 2. Le fond des cadres a
  un **grain de pierre** discret calculé par le jeu (pas d'image : `Ui.Grain`, motif qui se répète tous les 64 pixels
  d'art sans raccord). Réglages dans `art/ui/ui.json` : `panelScale`, `buttonScale`, `buttonPlainBand` (garder tout le
  milieu du bouton), `panelTexture` (`pierre`, `doux`, `cuir`, `toile`, `uni`), `panelTextureStrength` (4,5).
  `tools/art/cadre.py` rend transparent le centre uni d'un cadre généré. Essayés puis écartés : les cadres B7 (fil
  d'or, trop discret), les boutons de pierre de 96 × 32 (biseau trop large pour les petits boutons).
  ✅ Boîtes intérieures (2026-11-04) : une règle, comme dans Heroes 2 : **creusé pour les places, à plat pour les
  choses**. Les places (emplacements d'armée, listes du panneau, fiche du héros, sections du héros et du royaume,
  bande des mines, zones sous les cartes de Construire, Recruter, Taverne, liste des sauvegardes, ordre de jeu du
  combat) sont creusées (`Ui.Inset` : biseau d'un pixel d'art, sombre en haut à gauche, clair en bas à droite, ombre
  intérieure, grain) ; les choses (lignes des listes, vignettes du royaume, cartes de bâtiment, de créature et de
  héros) sont des plaques à plat au trait fin (`Ui.Plate`), plus claires que leur creux ; un bâtiment déjà construit
  est plus sombre. Les traits de couleur (héros choisi, bâtiment possible, troupe à risque) restent. Le fond de l'écran
  de ville a le grain (`Ui.Stone`). Grain baissé : 3 sur les cadres, 1,5 sur la pierre des boutons
  (`buttonTextureStrength`). Les coins un peu arrondis du bouton A1 sont gardés (choix de l'utilisateur).
  ✅ Revue avec ce style (2026-11-05) : traits d'argent au lieu d'or (carte réduite, portraits) ; barres au vert sourd,
  rainure creusée et remplissage à plat (`Ui.StyleBar`, taille fixée après le style : sinon Godot gardait la hauteur
  minimale du thème) ; curseur à rainure creusée et bouton de pierre, champs de texte creusés (thème) ; ligne de la
  dernière action du combat creusée, « Journal » de taille fixe dedans ; colonne de la carte sans vide (boutons un peu
  plus grands) ; fiche du héros sans débordement (artefacts à côté du charisme) ; lignes des listes à l'intérieur du
  biseau ; cases à cocher espacées de leur texte ; un bouton choisi est plus sombre (enfoncé), plus clair avant.
  Gardé tel quel pour l'instant : les sections vides (artefacts, magie, villes), qui se rempliront.
- Animations des créatures avec PixelLab (étape 6, essai sur l'Épéiste).
- Fonds de champ de bataille et obstacles ; le héros sur la carte ; les bâtiments des villes en images détaillées.
- Petits détails et équilibrage fin.

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
