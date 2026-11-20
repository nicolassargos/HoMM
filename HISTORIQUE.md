# Historique du jeu

Le détail des étapes, tel qu'il était noté dans `PLAN.md` jusqu'au 2026-11-06 : choix faits, chiffres mesurés, essais
écartés, noms des classes et des fichiers. `PLAN.md` ne garde plus que la direction, l'étape en cours et les choix encore
valables ; le détail d'une étape finie vient ici. La méthode des images est dans `tools/art/METHODE.md`.

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
   des feuilles de 8 images (`epeiste-idle.png`, `-walk`, `-attack` : 4 colonnes × 2 rangées) ; sans feuille, l'image fixe.
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
   autour de lui, rarement (pause de 15 à 30 s, 7 images par seconde). L'utilisateur trouve les animations un peu
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
   pour l'échelle. Les fiches existent pour le Colosse, le Chevalier, le Fantôme, l'Archer, le Moine, le Loup, puis le Zombie, le
   Troll, l'Ogre et le Griffon.
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
   L'**Orc** : animations refaites (2026-11-06) avec l'outil, depuis sa grande image d'origine (82 px) : attente calme,
   marche, lancer de hache où la hache ne vole pas (elle sera dessinée par le jeu). Une créature qui marche fait
   maintenant un cycle de déplacement par case (demandé par l'utilisateur).
   Le **Vampire** (futur mort-vivant, `vampire`) : comte élégant et pâle, cape noire au haut col rouge, d'après une
   image de l'utilisateur, sans le verre de vin ; 52 px. Pour se déplacer, il **se change en chauve-souris** :
   nouvelle animation facultative « transform » jouée avant le déplacement et à l'envers à l'arrivée ; la
   chauve-souris vole sur place (`--depart` : animée depuis la dernière image de la transformation), 0,25 s par case.
   Attaque : il balaie sa cape devant lui et se penche derrière, on devine la morsure (idée de l'utilisateur ; les
   attaques « brutales » de PixelLab ont échoué : sans victime dessinée, le geste restait vague).
   La **Liche** (futur mort-vivant, `liche`) : roi squelette voûté, fine couronne d'or terni, robes en lambeaux bleu nuit
   presque noir, long bâton ; d'après une image de l'utilisateur, en plus sombre, puis un lot au crâne plus grand (le
   crâne doit être la partie la plus claire pour se lire). Attente, marche voûtée appuyée sur le bâton, attaque où elle
   pointe son bâton vers l'ennemi (l'éclair sera dessiné par le jeu).
   L'**Araignée géante** (mort-vivant de niveau 4, `araignee`), sans image de référence : grande araignée noire aux reflets
   rouge sombre (un crâne sur le dos était « trop » : remplacé par du rouge ; le violet du candidat choisi a été tourné
   vers le rouge par l'outil, sans dépense), pattes aux os apparents, yeux et venin verts ; 36 px, plus large que haute.
   Attente, déplacement, attaque où elle se dresse puis mord (le poison sera un effet du jeu).
   **Vitesse et pauses des animations** (`art/creatures/battle/animations.json`, facultatif) : images par seconde par
   créature et par animation (`idle`, `walk`, `attack`, 10 par défaut ; une attaque plus lente porte son coup plus
   tard), et une pause entre deux attentes (`idlePauseMin`, `idlePauseMax`, en secondes) : la créature garde sa pose de
   départ, puis joue son attente une fois. L'entrée `*` vaut pour toutes : 5 à 10 s de pause (choix de l'utilisateur), puis 3 à 6 s (2026-11-06).
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
  4. L'équilibrage (simulation et adversaires, avec la magie).
  Ensuite (demandé par l'utilisateur, 2026-11-06 : « il y aura bien plus à faire avec la magie ») :
  - ✅ **Choisir les sorts** (comparaison avec Heroes 1, 2, 3, 5 et 7, décidé avec l'utilisateur le 2026-11-06) :
    Bénédiction et Malédiction deviennent **+25 % / −25 % de dégâts** (le maximum ou le minimum ne changeait rien aux
    créatures à dégâts fixes : Paysan, Archer, Épéiste, Cavalier, Ange) ; **6 sorts de plus** : Soif de sang (niveau 1,
    +attaque à une troupe amie), Bouclier contre les tirs (niveau 2, moitié des dégâts des tirs), Aveuglement (niveau 3,
    la troupe ne joue plus jusqu'à ce qu'on la frappe), Téléportation (niveau 3, une troupe amie va sur n'importe
    quelle case libre), Dissipation (niveau 3, retire tous les sorts d'une troupe), Chaîne d'éclairs (niveau 4, saute
    sur les troupes voisines) : 18 sorts. Un niveau 5 plus tard, avec les factions (Armageddon, élémentaires, morts
    relevés). Écartés pour l'instant : berserk, hypnotisme, paralysie (forts, difficiles à jouer pour l'IA), les
    versions « de masse » (plutôt une récompense des classes et des compétences). Les **écoles de magie attendront les
    classes de héros**.
    Ce qui distingue notre magie de la série : le héros lance sans prendre le tour d'une troupe (comme Heroes 2 et 3,
    pas comme 5 et 7) ; pas d'écoles ni de compétences de magie (comme Heroes 1 et 2) ; 4 niveaux au lieu de 5 ; un livre
    pour chaque héros, gratuit ; tous les sorts de la guilde appris sans condition (pas de « Sagesse ») ; une durée de
    puissance + 1 rounds ; pas encore de résistance à la magie ni de créatures insensibles ; le combat rapide lance
    aussi des sorts. Prévu (choix de l'utilisateur) : les héros de **force** (avec les classes) devront **acheter leur
    livre de sorts** ; il y aura des **créatures insensibles à la magie**.
    ✅ Construit : `data/spells.json` (18 sorts ; aux niveaux 1 et 2, la guilde en tire 3 sur 5, au niveau 3, 2 sur 5, au
    niveau 4, 2 sur 3) ; nouvelles cibles `Any` (dissipation) et `Chain` (`TacticalBattle.SpellHits` : chaque troupe
    touchée et sa force) ; une troupe aveuglée passe ses tours (`UnitSkipped`), le premier coup ou sort de dégâts la
    réveille, sans riposte ; la téléportation prend une destination (`Cast(sort, cible, destination)`,
    `TeleportDestinations`, deux clics à l'écran) ; l'IA téléporte une troupe de corps à corps qui n'atteint personne à
    côté de l'ennemi le plus dangereux, dissipe les bons sorts de l'ennemi, aveugle les grosses troupes. Le livre de
    sorts passe à deux colonnes (en attendant le vrai livre). Tests : `MagieTests`.
    ✅ Le livre de sorts se lit à tout moment (`SpellBookWindow`, demandé par l'utilisateur) : au combat, même quand on
    ne peut pas lancer (tour de l'adversaire, sort déjà lancé ce round : il le dit, les sorts sont grisés) ; sur la
    carte, un bouton « Sorts » (touche S) à côté d'Options. La fenêtre Construire : un bâtiment qui s'améliore (salle du
    village → hôtel de ville → grand hôtel de ville, guilde 1 → 4) n'a qu'une carte, le prochain niveau ou le dernier
    construit, comme dans Heroes 3 (10 cartes au lieu de 15) ; des barres de défilement de pierre bien visibles partout.
  - **Le livre de sorts en image** : un vrai livre ouvert, une page par niveau, avec une **animation de page qui
    tourne** pour passer d'un niveau à l'autre.
  - **Une image par sort** (icône dans le livre et la guilde).
  - ✅ **Une animation par sort** sur le champ de bataille, et un **effet visible sur les troupes** (2026-11-14,
    branche `sorts-animations`). Tout est dessiné par le jeu (`Game/SpellEffects.cs`, avec les outils de
    `BattleEffects` : pixels d'art de 2 pixels, unité `U`), sans dépense. Revu avec l'utilisateur en quatre passages sur
    une page (`revue-sorts.html` : chaque sort joué en boucle, « Bien » ou « À revoir » et un commentaire) ; ses
    remarques : « des formes trop simples » (des points, des anneaux), les effets passaient devant le corps, la boule
    de feu pas assez impressionnante ; il a donné deux planches d'effets en pixel art comme modèle (formes franches, 3 à
    4 tons chauds, flammes larges à plusieurs pointes, boules à bandes en spirale). Ce qui est fait :
    - **La profondeur** : ce qui tourne autour d'une troupe (volutes, pierres, vent, fumée, âmes…) a une profondeur `z` ;
      la part de derrière se dessine juste avant la troupe (`SpellEffects.DrawBehind`, une entrée de plus dans la liste
      triée des troupes de `BattleFieldView`), son corps la cache ; de même pour les auras (`DrawLasting(…, behind)`).
    - **La troupe change** pendant le sort (`SpellEffects.Look` : une teinte multipliée, au-delà de 1 elle éclaire ;
      une opacité ; des images fantômes) : violette (malédiction), terne (faiblesse), couleur de pierre (peau de
      pierre), rouge à deux battements (soif de sang), verte (soins), dorée (résurrection), sable (lenteur), un éclair
      blanc (aveuglement, dissipation), deux images fantômes bleutées en arrière (hâte), effacée puis rendue
      (téléportation).
    - **Les formes** : lueurs en dégradé par paliers (`Glow`), anneaux, rubans qui s'amincissent (`Ribbon`), étincelles
      (`Star`), traînées, bouffées de fumée bosselées (`Puff`), boules de feu qui bouillonnent (`Blaze`), sphère à bandes
      en spirale (`SwirlBall`), traîne de comète en bandes (`CometTail`), langues de flamme et flammes larges à trois
      pointes (`Tongue`, `FlameBush`), pierres ombrées et fissurées de lave (`Rock`), sablier ouvragé (`Hourglass`),
      bouclier en treillis d'hexagones (`Shield`, `HexEdge`).
    - **Chaque sort** : flèche magique (un fer et une hampe de lumière violette, un sillage, elle se brise en éclats) ;
      éclair (la foudre de l'Oiseau-tonnerre, `foudre` : « foudre-forte » était trop épaisse) ; chaîne d'éclairs (13
      pixels d'art de large pour le premier arc, 9 pour les sauts suivants, des fourches) ; boule de feu (une boule en
      feu qui file droit, une explosion avec onde au sol, de grandes flammes qui brûlent 1,45 s, braises, fumée) ; pluie
      de météores (deux par case, roches fissurées dans une traîne de feu, éclats, poussière, cratères qui refroidissent,
      trois grandes flammes par impact) ; bénédiction (gardée de la première version) ; malédiction ; peau de pierre (des
      pierres arrivent en spirale et se fondent dans la peau) ; soif de sang (flammes rouges de rage, gouttes) ; hâte ;
      lenteur (un sablier au-dessus de la tête, un cadran au sol) ; faiblesse (une lueur chaude qui s'éteint, des
      coulées pâles qui descendent en tournant ; des flèches vers le bas ont été écartées) ; aveuglement ; bouclier
      contre les tirs ; soins ; résurrection ; téléportation ; dissipation (un cercle de runes, des éclats de couleur).
    - **Règle** : la chaîne d'éclairs ne saute qu'à `SpellType.ChainReach` (3) cases au plus de la troupe précédente
      (choix de l'utilisateur : elle sautait d'un bout à l'autre du champ). Test : `MagieTests`.
    - Outils de développement : `--spell-preview=id` (avec `--spells` : le sort lancé sans cesse par le héros de gauche,
      sur la troupe ennemie ou amie la plus proche, sans changer le combat ; la chaîne suit la règle du jeu, à essayer
      avec `--creature-preview=orc,gobelin,loup` pour des troupes côte à côte), `--record=dossier` (25 images WebP par
      seconde, avec `--fixed-fps 25` ; `--record-delay`, `--record-time`, `--record-rect`) ; un enregistrement ne montre
      pas la fenêtre des conditions de la partie.
  - Le reste au fil des idées (sons, sorts de la carte…).
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
- ✅ **Le moral et la chance** (2026-11-09, branche `moral-chance`). Choix de l'utilisateur : 5 % par point (un peu
  plus que Heroes 3 : 1/24), mauvais moral 10 % par point, la malchance existe (−3 à +3) ; le moral vient du charisme
  (« aura » de l'armée à 20 / 30 / 40, et −1 / −2 / −3 pour une troupe au-delà de 1 / 1,5 / 2 fois la limite, la révolte
  restant possible), du mélange des factions, du temple ; pas de règle à part pour les morts-vivants ; les touches des
  factions plus tard. Code : `Core/Morale.cs` (`MoraleParts`, `LuckParts`, `UnitCheered`, `UnitFroze`),
  `TacticalBattle` (`SetUpMorale` au début du combat d'après les troupes restées fidèles ; mauvais moral tiré dans
  `NextUnit` après le poison ; `EndTurn(acted)` pour le bon moral après un déplacement, une attaque, un tir, ou après
  « Rester » des Nomades ; `BeginTurn(fresh)` : le tour donné par le moral ne régénère ni ne soigne, et ne permet pas
  d'attendre, `CanWait` ; `Strike(splash)` et `RollLuck` : la chance n'est pas tirée pour le souffle, le balayage et le
  nuage de mort), `BattleUnit.Morale/Luck/HasCheered` (remis à 0 par la révolte), `UnitStruck.Luck`,
  `Hero.SiteMorale/SiteLuck` (effacés par `FinishBattle`), `BlessingSite` (`MapSites.cs`, posés en dernier par
  `PlaceSites` pour ne pas changer le reste des cartes), sauvegarde version 6. Le dé du hasard n'est tiré que pour une
  troupe qui a du moral ou de la chance : les combats sans héros (duels, courbe, simulation d'origine) ne changent pas.
  L'IA prend Moral après Commandement et Chance après Inspiration, et visite les lieux (poids 1,5). Affichage : la
  ligne « Moral · Chance » de l'écran du héros (le détail au survol), le moral de chaque troupe dans son info-bulle et
  sur la fiche au combat, « Moral ! », « Hésite… », « Chance ! », « Malchance » en texte flottant, le journal.
- ✅ **L'écran du héros refait** (2026-11-09, branche `ecran-heros`, proposition B de
  `art-candidats/en-cours/disposition-heros.html`). `HeroScreen` : la colonne de gauche reste (portrait, niveau,
  barres, caractéristiques, moral et chance) ; trois onglets (`Équipement`, `Compétences`, `Magie`) sur la ligne du
  titre, entre le nom et « Fermer », pour laisser 414 unités de haut aux pages (six emplacements de 64 unités, l'artefact
  ×3, ne tenaient pas sous une rangée d'onglets) ; l'armée descend (emplacements de 114 de haut au lieu de 128).
  `HeroFigure` dessine la silhouette (le repère de la maquette, ×1,55), les traits et les points d'or, et place les
  11 emplacements (six à gauche tous les 68, cinq à droite tous les 85) ; `Picture` attend l'image en pied de chaque
  héros. Compétences : des sous-onglets par arbre, une rangée par ligne, une icône ronde par compétence (or : apprise ;
  pâle : rangée fermée), le nom et l'effet du rang suivant à côté (dans un `VBoxContainer` : un `Label` hors conteneur
  ne revenait pas à la ligne). Magie : les sorts sur deux colonnes, et le bouton du livre. `SkillsWindow` retirée ;
  `OpenSkillTrees` ouvre l'onglet (`--open=skills` inchangé) ; `--hero-tab=skills|magic` pour les captures.
  Core : `ArtifactType.Slot`, `ArtifactSlot`, `Equipment` (`Slots`, `ChestSize`, `Arrange`).
  Les onglets ensuite, en marque-pages (`TabStrip`) : essayés à l'écran (`--ui-preview`) la pierre des boutons, des
  intercalaires, des plaques, la pierre ouverte en bas (seule ou avec la page bordée du même métal, idée tirée de
  PixelLab), du cuir cousu et des rubans dessinés ; choisi : un ruban de soie en image, teint (voir
  `tools/art/METHODE.md`). Le ruban sort de `PageTop` au-dessus de sa page, descend de `Overlap` sur son bord ; la page
  est ajoutée après la rangée (elle cache le bas des rubans de derrière) et le choisi a `ZIndex` 1. Les arbres : une
  seule rangée de rubans au-dessus d'un cadre creusé, le contenu refait au choix (`Changed`).
- ✅ **La Diplomatie et la fuite des gardiens** (2026-11-09, branche `diplomatie`). Choix de l'utilisateur : les seuils
  proposés (3 / 2 / 1,5 fois, prix entier / moitié / gratuit) **et** le charisme (il abaisse le seuil, un degré par
  palier comme le moral ; le gardien doit tenir dans le commandement), +1 degré pour un gardien de la faction du héros
  (la faction de sa classe), tout ou rien, la fuite pour tout héros avec le choix de poursuivre. Code :
  `Diplomacy.Encounter` (null : il n'y a qu'à combattre), `GuardEncounter`, `JoinOffer`, `JoinRefusal`, `EncounterChoice`,
  `GuardJoined`, `GuardFled` ; `GameState.NextFight` s'arrête sur `PendingEncounter` (marche, fin de tour et
  sauvegarde bloquées, comme pendant un combat) ; `ResolveEncounter` combat (`StartFight`, tiré de `NextFight`),
  prend le gardien (`Player.Pay`, `Army.Add`, retiré de la carte) ou le laisse fuir, puis reprend le pas ; l'IA choisit
  aussitôt (`ComputerPlayer.ChooseEncounter`). Les prix arrondis au-dessus, ressources rares comprises. Fenêtre
  `Hud.ShowEncounter` (l'image de combat de la créature, l'offre ou la fuite, ce qui empêche d'accepter ; fermer, c'est
  combattre), `--open=encounter`. Les textes donnent le nombre exact (on les paie). Cinq tests d'avant entraient à côté
  de 1 à 3 Gobelins, qui fuient maintenant : ils les poursuivent, ou le gardien est de 10 Gobelins.

- ✅ **La Tactique** (2026-11-09, branche `tactique`). Choix de l'utilisateur : 2 / 3 / 4 colonnes, le plus haut rang
  seul place, selon son avance (comme Heroes 3), rien d'autre que le placement. `TacticalBattle` : `SetUpTactics` après
  les obstacles ; `IsPlacing` retarde le premier round (`Active` nul, `Round` 0) jusqu'à `EndPlacement` ; `CanPlace`,
  `PlacementHexes`, `Place` (consigné en `UnitMoved` d'une case : l'écran fait marcher la troupe jusqu'à sa place, sans
  toucher `BattleFieldView`). Les troupes révoltées ne se placent pas. `BattleAI.PlaceTroops` (aussi au début de`PlayTurn`,
  donc combat rapide et « Auto ») ; `GameState.StartFight` place aussitôt pour un héros de l'ordinateur. `BattleScreen` :
  le mode placement (`PlayerPlaces`, `_placing`, `RefreshPlacement`, le bouton « Commencer »). `--skill=id:rang`.

- ✅ **Les boutons du combat en icônes** (2026-11-09, branche `boutons-combat`). Recherche : Heroes 1 n'avait que AUTO et SKIP ;
  Heroes 3 met à gauche les boutons du combat (options, se rendre, fuir, auto), au milieu les messages, à droite ceux de
  la troupe (sorts, attendre, défendre). Choix de l'utilisateur : la barre partagée comme Heroes 3, des boutons carrés de
  pierre, des icônes d'argent gravé de PixelLab. `BattleScreen` : `AddButton(icône, nom, touche)`, `SetIcon` (une
  icône à la place du texte, deux lettres sans image ; « Se défendre » devient « Rester »), `DimIcons` (l'icône d'un
  bouton grisé pâlit), `ButtonSize` 44 unités.

- ✅ **Le panneau de la carte en icônes** (2026-11-09, branche `icones-carte`) : les boutons carrés à icône incrustée sortis
  de `BattleScreen` dans `IconButtons` (`Icon`, `Inlay`, `Make`, `SetIcon`, `Dim`), utilisés par la barre du combat et
  le panneau (`Hud` : deux rangées de quatre, côté calculé sur la largeur du panneau). « Fin du tour » garde son texte.
  L'info-bulle d'Options donne sa vraie touche (Échap).
- ✅ **Les talents des classes** (2026-11-09, branche `talents-classes`) : `Talents.Crusader`, `Warlord`, `Hunter` (`classes.json`),
  `Talents.DamageBonus` (dans `RollDamage`, après les sorts), `Talents.RageMax` (à chaque coup reçu) ; une troupe révoltée
  n'a plus le talent de son héros. `Texts.TalentText` (détails de la classe, info-bulle de la classe sur l'écran du héros).
- ✅ **La Tour de magie, ses créatures** (2026-11-09, branche `tour-de-magie`). Conception avec l'utilisateur : philosophie du
  savoir (mages, créatures façonnées, géants), la marque « bibliothèque des arcanes » (préférée à l'écho des sorts et à
  « fait pour durer ») ; les créatures après un rappel des Heroes 2, 3, 5 et 7 : le Gnome (idée de l'utilisateur, qui
  répare les créatures façonnées) au lieu d'un Apprenti, la Naga et le Djinn de la tradition, le Mage au niveau 6.
  Code : `Ability.Made` (`BattleUnit.IsMade` ; `IsValidTarget` refuse les sorts néfastes, `Apply` divise les dégâts
  par deux et n'enchante pas d'un sort néfaste), `Repairs` et `Blessing` dans `BeginTurn` (un tour ordinaire),
  `NoMeleePenalty` dans `RollDamage` ; `CreatureType.Blessings` reliés aux sorts par `GameData` (`BlessingSpells`),
  `SpellCatalog.Find`. Réglage en trois passes de `Duels_a_prix_egal` (la Tour ajoutée aux factions comparées).
- ✅ **La Tour de magie jouable** (2026-11-10, branche `tour-ville`) : une classe de faction `tour` suffit à la rendre jouable
  partout (`PlayableFactions` : nouvelle partie, adversaires, villes neutres). `Town.ArcaneLibraryFaction`,
  `GuildSpellsAt` (aussi dans le texte de la guilde) ; `Talents.Scholar` ajouté au bonus de sort du héros (avec les
  écoles). `Texts.FactionName` : « Tour de magie ».
- ✅ **Le Donjon, ses créatures** (2026-11-11, branche `donjon`). Conception avec l'utilisateur, d'après ses images
  (`art-candidats/references/donjon/`, renommées par créature) : les seigneurs des abîmes, la Terreur comme marque (le
  Pacte sombre gardé pour l'Enfer, l'échange de ressources pour les Nains), des élites fortes sur une base faible. Écartés :
  la Méduse (trop proche de la Naga), le Gnoll, l'Augure vautour, la Manticore, la Chimère, la Wyverne, le Centaure orc.
  Code : `Ability.Terror`, `Horror`, `Brave`, `EndlessRetaliation`, `Burrows`, `Swallow`, `Rays` ; `BattleUnit.FearsNothing`
  (morts-vivants, façonnés, niveau 7, braves), `RetaliationsPerRound` ; dans `Strike`, l'avalement (`SwallowChance`) puis
  la Terreur ; `Ray` après le premier tir (`RayRounds`) ; `CreatureType.Rays` reliés par `GameData` (`RaySpells`) ;
  événements `UnitTerrified` et `UnitSwallowed` (textes « Terreur » et « Avalé ! » au-dessus des troupes, journal).
  Réglage en cinq passes de `Duels_a_prix_egal` (le Donjon ajouté) : la Terreur fait basculer un duel d'un coup
  (Aberration à 54 PV : presque toutes ses défaites ; à 56 : presque toutes ses victoires ; 55 retenu).
- ✅ **L'Enfer, ses créatures** (2026-11-11, branche `enfer`). Conception avec l'utilisateur : le chaos et le feu, la
  magie du feu la plus forte, le prix du sang (préféré au pacte qui sacrifie des créatures pour lancer un sort). Créatures
  voulues **moins attendues que celles de Heroes 3** (l'utilisateur a écarté une première liste trop proche : Cerbère,
  Gog, Efrit, Seigneur des abîmes, Cauchemar) ; le Cerbère gardé un temps au niveau 3, remplacé par le Limon de magma
  qui se divise (idée de l'utilisateur). Code : les capacités dans `TacticalBattle` (partie « l'Enfer » : `BloodPrice`,
  `Split`, `FreeHexNextTo`, `AddUnit`, `CanHook`/`Hook`, `CanSummon`/`Summon`), `MeleeAndRetaliation` (le sacrifice, la
  riposte refusée à une troupe séduite), le prix du sang appelé partout où des créatures meurent (coups, foudre,
  avalement, poison, sorts, sacrifice) ; l'insensibilité au feu dans `SpellHits` (`Burns`, aussi pour la valeur des
  sorts de l'IA) ; `Talents.BloodPriceCap` et le talent `Pact` (prêt pour la classe). À l'écran, une troupe qui apparaît
  en cours de combat est ajoutée cachée quand l'événement arrive, puis montrée par son animation
  (`BattleFieldView.AddShown`, `Appear`, `Announce`). Réglage en trois passes de `Duels_a_prix_egal`.
- ✅ **Les Nains jouables** (2026-11-12, branche `nains-ville`) : une classe de faction `nains` suffit
  (`PlayableFactions`). `Talents.Obstinacy` et `StubbornShare` (0,2 + 0,01 × niveau, 0,5 au plus), appliqué dans
  `TacticalBattle.RollDamage` à la place de la part fixe ; `Texts.TalentText`. `Art.Slug` écrit « œ » et « æ » en deux
  lettres (le portrait de Kalla Œil-de-Braise : `kalla-oeil-de-braise.png`). Portraits : quatre essais avant le bon
  (voir `tools/art/METHODE.md`).
- ✅ **L'Enfer jouable** (2026-11-11, branche `enfer`, faite pendant une coupure de GitHub, à la suite de l'étape 1) :
  comme le Donjon, une classe de faction `enfer` suffit (`PlayableFactions`). `Texts.TalentText` pour le Pacte ; les
  quatre héros « sombres » qui restaient passent à l'Enfer, `DataValidation.PlannedFactions` est vide.
  `HerosContreHerosTests.FaceToFace` fixe l'adversaire au Château (avec une faction de plus, le tirage donnait un
  Magicien, dont les Flèches magiques battaient 80 paysans avec 3).
- ✅ **Le Donjon jouable** (2026-11-11, branche `donjon-ville`) : comme la Tour, une classe de faction `donjon` suffit
  (`PlayableFactions`). `Talents.Dread` et `DreadMorale` (1 + niveau / 8), appliqué par `TacticalBattle.Dread` juste
  après le moral de chaque camp et avant le brave (ceux que rien n'effraie gardent leur moral) ; `Texts.TalentText`.
  Cinq héros de la faction « sombre » passent au Donjon ; « tour » retiré des factions prévues (`DataValidation`).
- ✅ **Les gardiens justes et les revenus doublés** (2026-11-11, branche `gardiens`). Diagnostic : les deux petites cartes
  lentes (graines 3 et 77, jours 164 et 136) avaient 16 et 11 gardiens d'une seule créature du niveau 7 (Hydres, Démons,
  Colosses) ; la plus rapide (60), deux Trolls. La taille d'un gardien est arrondie à une créature au moins, et une
  seule créature du niveau 7 vaut plusieurs fois la force voulue. Mesure `Gardiens_par_creature` (l'armée du Château la
  plus petite qui bat un gardien moyen en perdant au plus 30 %) : avant, Hydre ×1,45, Loups ×1,30, et les tireurs
  bien trop faciles (Hommes-grenouilles ×0,37, Druides ×0,39, Tyran œil ×0,29) ; après, 0,76 à 1,12. Code :
  `CreatureStrength.GoldToBeat` (armée de mesure : paysans, archers, griffons, épéistes, nombreux, par petits pas ;
  troupe mesurée d'environ 3000 or), `CreatureStrength.Prepare` (toutes les créatures en parallèle avant une carte :
  2 s en série), `MapGenerator.SizeGuard`, `ReferenceStrength`, `GuardReferenceCreature`, `SmallestGuard` (0,75). Puis
  l'économie : la simulation finissait chaque semaine avec 450 à 600 or (tout dépensé) ; comparée à Heroes 3 (archer
  100 or, hôtel de ville 1000 par jour), elle était trois fois plus serrée. Villes 300 / 600 / 1200, mines d'or 500.
  Les cartes normales ne vont pas plus vite (la simulation y livre un combat tous les deux jours, c'est sa limite).
- **Les factions : conception et chiffres** (déplacés de `PLAN.md` le 2026-11-15, tels quels : chaque faction telle
  qu'elle a été conçue, ses créatures, ses chiffres réglés et ses mesures).

#### Le Château, la refonte des niveaux 4 et 6 (2026-11-17, branche `chateau-refonte`)
Choix de l'utilisateur : le Griffon, l'Épéiste, le Cavalier et l'Archer à revoir ; le Moine reste. Le **Cavalier
descend au niveau 4** à la place de l'Épéiste (un cavalier plus léger : attaque 10, défense 9, 4 à 6 de dégâts, 36 PV,
vitesse 7, charge, deux cases ; 80 or, croissance 8) ; le **Chevalier à pied** prend le niveau 6 (attaque 11,
défense 17, 12 à 16 de dégâts, 150 PV, vitesse 4 ; résiste aux tirs et fait **mur de boucliers** pour les tireurs
amis à côté de lui, comme le Porte-bouclier nain ; 300 or, croissance 4). Habitations : **Écuries** (niveau 4, à la
place de la Caserne) et **Commanderie** (niveau 6, à la place du Terrain d'entraînement), mêmes prix et mêmes places.
Les anciennes sauvegardes retrouvent `epeiste` → `cavalier`, `caserne` → `ecuries`, `terrain-entrainement` →
`commanderie`. L'armée qui mesure les gardiens (`CreatureStrength`) prend les Cavaliers à la place des Épéistes.
Réglage par `Duels_a_prix_egal` : avec les chiffres de l'Épéiste puis de l'ancien Cavalier, ils gagnaient 99 et 97 %
de leurs duels ; très sensible (Cavalier : 41 % à 35 PV, 56 % à 36, 71 % à 37) ; gardés : Cavalier 56 %,
Chevalier 62 % (les meilleurs des autres : Tréants 64 %, Molochs 57 %, Cerfs 56 %). Courbe des paysans : Archer 8,
Griffon 14, Cavalier 25, Moine 61, Chevalier 93, Ange 1597.
**Les images** (PixelLab, environ 3 $ ; choix de l'utilisateur à chaque étape) :
- **Archer** : un ancien braconnier long et osseux, nez crochu, chapeau mou à plume, tabard bleu délavé (L4-2) ;
  attente idle-2, marche move-2, tir attack-2 (une flèche détachée effacée à la main) ; le départ de la flèche suit le
  nouvel arc (`launchPoint` [83, 42]).
- **Griffon** : une monture de guerre maigre et ébouriffée, tête d'aigle sombre et cabossée, harnais bleu (L2-1), plus
  petit (66 px, ailes comprises, au lieu de 76) ; idle-1, move-2, attack-2 (éclats effacés). L'ancienne image, modèle de
  style, est gardée dans `art-candidats/references/griffon-ancien.png`.
- **Cavalier** : après un fanfaron du XVIe siècle (trop impressionnant pour le niveau 4, des moustaches refusées) et un
  chevalier en surcot fleurdelisé (trop riche), un **jeune sergent à cheval** : casque à nasal, mailles, surcot bleu à
  une fleur de lys, lance à fanion, cheval alezan au chanfrein et aux lames de cou d'acier, couverture bleue (L20-3,
  redessiné plus grand en L22-3) ; gardé à sa taille de génération (126 px lance comprise ; réduit, il était flou),
  la taille des Nomades. Attente idle-3 (le cheval lève un sabot) ; attaque attack-2 (la lance abaissée) ; le galop :
  voir `METHODE.md`.
- **Chevalier** : l'image de combat des débuts gardée ; image de la carte p1-13.
- **Images de la carte** (32 px) : Archer p1-15, Griffon p1-1, Cavalier p1-1, Chevalier p1-13 ; pieds revus par
  l'utilisateur. Les images de l'Épéiste sont retirées.
- Le galop des **Nomades** ralenti à 8 images/s (au lieu de 16) et une image en double retirée (11 images) : les
  jambes allaient trop vite pour la vitesse du déplacement (remarque de l'utilisateur).

**Le Loup, le Moine et le Troll redessinés** (2026-11-17 au 19, branche `creatures-loup-moine-troll`, PixelLab, environ
6 $ ; choix de l'utilisateur à chaque étape). Les anciennes images servent de modèles de style ou d'angle, gardées dans
`tools/art/modeles/` (le modèle de style par défaut de `creature.py`).
- **Loup** : le même loup, redessiné (L1-4). Attente idle-4 (il regarde autour de lui, se lèche le museau) ; marche
  move-4 gueule fermée (« trop agressif » gueule ouverte), coupée à son cycle exact (images 2 à 8 : 7 images, 8 images/s,
  0,4 s par case ; la fin de l'essai avait un trait lumineux) ; attaque attack-4 : il s'accroupit et **bondit sur
  l'ennemi**. Mort death-4 (il se cabre et retombe sur le flanc ; sang effacé), coup reçu hit-4 (il se cabre en
  arrière).
- **Moine** : un **frère gros et jovial aux joues rouges**, plus sérieux mais aimable (L13-1, d'après L10-4, sans le
  bâton qui luisait) ; c'est un **lanceur de sorts** : le jeu dessine une **boule de lumière** blanche et dorée qui part
  du haut de son bâton (`boule-lumiere`, impact `lumiere` dans `BattleEffects` ; `launchFrame` 2, `launchPoint`
  [142, 42]). L'image générée regardait vers la gauche (remarque de l'utilisateur : il ne fait pas face à l'ennemi) :
  `miroir` dans sa fiche, l'attente et la marche retournées, l'attaque refaite depuis l'image retournée. Attente idle-4,
  marche move-4 (les pieds se voient), attaque attack-7 (il pointe le bâton ; deux éclairs remplacés par la pose
  tendue) ; mort death-8 (il tombe à genoux puis face contre terre ; refaite avec de la place sous les pieds : la
  première sortait du cadre), coup reçu hit-4.
- **Troll** : longue recherche (40 lots) d'après la référence de l'utilisateur (`Troll_2.png`) : calme, pas menaçant,
  barbe courte et sombre, visage neutre, **pas humain, pas de défenses (pas un orc), hors des clichés**. Refusés :
  les visages humains, le nez tombant des contes ; essayés : visage dans la poitrine, museau d'élan, paresseux géant,
  tête en galet. Gardé : L36-4, **le visage dans l'ombre d'une capuche de mousse pendante**, un dos de pierres
  moussues, épaules plus hautes que la tête. Attente idle-5, marche move-7 (pieds sous les hanches, jamais écartés),
  attaque attack-5 (le bras levé puis abattu ; la tache verte et l'anneau autour du poing effacés à la main) ; coup
  reçu hit-1 (traits jaunes effacés au-dessus des épaules) ; mort death-13, **lente** (16 images) : il vacille,
  s'assoit lourdement, puis bascule sur le dos.
- **Images de la carte** (32 px) : Loup p1-4, Moine p1-2, Troll p1-1 ; pieds revus par l'utilisateur.

#### Les Nains
Commencé avec l'utilisateur (2026-11-11). Des créatures qui ne copient ni Heroes 3 ni Heroes 5 (choix de l'utilisateur).
- **Philosophie : les halls de la montagne.** Des artisans têtus qui gravent des runes et bâtissent des machines.
- **Forces** (proposées) : les troupes les plus solides (défense, points de vie), les machines de guerre, une économie
  forte (leurs mines produisent davantage, à voir).
- **Faiblesses** (proposées) : lents (vitesse 3 à 5), aucune créature vivante qui vole, peu de magie, chers au début.
- **Leur marque : l'obstination** (choix de l'utilisateur) : une troupe naine qui ne s'est pas déplacée de tout le round
  prend 20 % de dégâts en moins (chiffre à régler en mesurant).
- Ressources : cristal (principale : l'habitation et la créature du niveau 7), soufre (secondaire : niveaux 5 et 6).
- **Les machines** (Gyrocoptère, Machine de guerre) sont **façonnées**, comme les golems de la Tour : elles résistent
  à la magie (choix de l'utilisateur).
- **La classe Thane** (choix de l'utilisateur, 2026-11-12) : attaque 1, défense 3, puissance 1, savoir 2, charisme 11 ;
  Peau de pierre ; École de la terre au départ ; talent **Obstination** : 1 % de dégâts en moins de plus par niveau
  (la moitié au plus).
- **Les héros** (portraits à la Heroes 2, 2026-11-12, choix de l'utilisateur ; femmes sans barbe, robustes et en
  armure) : Brokk Marteau-Noir, Hulda Pierre-Lune, Gunnar Fend-la-Roche, Ingrid Vapeur-d'Airain, Bardur le Têtu,
  Kalla Œil-de-Braise.

| Niveau | Créature | Rôle | Capacité (proposée, chiffres à régler) |
|---|---|---|---|
| 1 | Porte-bouclier | le mur | les tireurs amis voisins ne prennent que moitié des dégâts des tirs |
| 2 | Arbalétrier | le tireur | ses carreaux percent 30 % de la défense de la cible |
| 3 | Chevaucheur de chèvre | la cavalerie de montagne, la seule troupe rapide | pied sûr : frappe le premier quand on le charge, rien ne le repousse |
| 4 | Porte-étendard des ancêtres | la bannière | aura : +2 défense aux troupes amies voisines |
| 5 | Lance-flammes | le feu de près | sa flamme touche la cible et la case derrière elle |
| 6 | Gyrocoptère | la machine volante | vole ; lâche une bombe sur la cible et les cases voisines |
| 7 | Machine de guerre (→ Forteresse ambulante) | le canon à vapeur | tire ; son obus touche une zone |

Écartés pour l'instant (idées à garder pour des monstres neutres ou des améliorations) : le Mineur, le Porteur de
lanterne, le Lanceur de poudre, le Brasseur, le Brise-fer, le Forgeron de guerre, le Tueur de géants, le Prêtre des
ancêtres, le Forgeron des runes, le Gardien de pierre, l'Ours des cavernes, le Dragon rouge et le Phénix.
Découpage comme les autres : 1. les créatures et leurs capacités ; 2. la ville, la classe et ses héros ; 3. les images.
1. ✅ Les créatures et leurs capacités (2026-11-11, branche `nains`) : `stubborn` (20 % de dégâts en moins tant que la
   troupe ne s'est pas déplacée d'elle-même dans le round, `BattleUnit.HasMoved`), `shield-wall`, `piercing` (30 %),
   `sure-footed` (la riposte avant le coup ; le crochet ne le tire pas), `banner` (+2 défense, `TacticalBattle.BannerBonus`),
   `blast` (les ennemis autour de la cible, au corps à corps comme au tir) ; le Lance-flammes prend le souffle du Dragon ;
   les Nains gardent aussi des passages de la carte (`MonsterFactions`). Chiffres réglés par `Duels_a_prix_egal` (chaque
   créature au milieu de son niveau ; les auras du Porte-bouclier et du Porte-étendard ne comptent pas en duel, une seule
   troupe par camp) :
   | | Att. | Déf. | Dégâts | PV | Vit. | Croiss. | Prix |
   |---|---|---|---|---|---|---|---|
   | Porte-bouclier | 3 | 5 | 1-3 | 8 | 4 | 14 | 15 |
   | Arbalétrier | 8 | 6 | 3-6 | 16 | 4 | 10 | 35 |
   | Chevaucheur de chèvre | 9 | 7 | 4-6 | 25 | 7 | 8 | 65 |
   | Porte-étendard des ancêtres | 11 | 13 | 6-9 | 58 | 4 | 5 | 140 |
   | Lance-flammes | 14 | 12 | 10-14 | 70 | 4 | 4 | 230 |
   | Gyrocoptère | 18 | 13 | 18-24 | 95 | 9 | 3 | 370 |
   | Machine de guerre | 27 | 24 | 100-120 | 1900 | 3 | 2 | 4900 et 1 cristal |
   Victoires en duel dans leur niveau : 54, 53, 55, 60, 55, 52, 57 %. Mesuré : la simulation gagne comme avant (jours 69,
   86, 148) ; les gardiens nains demandent de 0,92 à 1,17 fois la même armée ; adversaires (six semaines, Normal, or
   d'armée, les cartes changent un peu) : Château 54 400, morts-vivants 52 600, Barbares 51 400, Forêt 62 800, Tour
   46 400, Donjon 56 800, Enfer 60 200. Tests : `NainsTests`.
2. ✅ La ville et le jeu (2026-11-12, branche `nains-ville`) : 7 habitations (`salle-boucliers` Salle des boucliers,
   `atelier-arbaletriers` Atelier des arbalétriers, `enclos-chevres` Enclos des chèvres, `salle-ancetres` Salle des
   ancêtres, `forge-flammes` Forge des flammes, `hangar-gyrocopteres` Hangar des gyrocoptères, `grande-fonderie` Grande
   fonderie : prix et places de celles de l'Enfer ; soufre aux niveaux 5 et 6, cristal au niveau 7 ; le grand hôtel de
   ville accepte aussi la Salle des ancêtres), la classe Thane (armée de départ au prix des autres : 14 Porte-boucliers,
   5 Arbalétriers, 2 Chevaucheurs de chèvre) et son talent Obstination (`Talents.StubbornShare`), les 6 héros nains
   jouables ; `--faction=nains`. Mesuré (un adversaire, Normal, six semaines, or d'armée) : **Nains 59 700**, Château
   56 500, morts-vivants 53 900, Barbares 54 200, Forêt 64 700, Tour 49 400, Donjon 62 200, Enfer 62 000 ; la simulation
   seule comme avant (jours 72, 86, 150). Tests : `VilleNainsTests`.
3. Les images (session images) : les 7 créatures. ✅ La ville sur la carte (2026-11-12, branche `creatures-nains`, choix
   de l'utilisateur) : une forteresse taillée dans une montagne grise, une porte gravée de runes d'or entre deux statues
   de rois nains, une cheminée de forge (`objects/town-nains.png`, luminosité 98, gardée telle quelle).
4. ✅ **Le Sapeur et le Roi de cristal** remplacent le Lance-flammes et la Machine de guerre (2026-11-16, branche
   `nains-sapeur-roi`, choix de l'utilisateur, qui n'aimait ni la grosse machine ni le lance-flammes). Le **Sapeur**
   (niveau 5, soufre) : un mineur à la pioche et à la lanterne, qui **se déplace sous terre** (`burrows`, comme le Ver
   des profondeurs), mêmes chiffres que le Lance-flammes ; habitation `galeries-sapeurs` (Galeries des sapeurs). Le **Roi
   de cristal** (→ Roi des ancêtres ; niveau 7, cristal) : un ancien roi scellé dans le cristal, vivant (plus façonné),
   au corps à corps ; habitation `caveau-rois` (Caveau des rois). Chiffres réglés par `Duels_a_prix_egal` (la Machine
   de guerre, sans son canon, gagnait tous ses duels) : attaque 24, défense 25, dégâts 76-96, 1400 PV, vitesse 4 ;
   victoires dans son niveau 56 % (Sapeur 56 %). Les anciens identifiants restent lus (sauvegardes d'avant :
   `CreatureCatalog`, `BuildingCatalog`). ✅ Ses deux capacités (2026-11-17, branche `roi-de-cristal-capacites`) : le
   **miroir de cristal** (`crystal-mirror`) : un sort néfaste (malédiction, lenteur, faiblesse, aveuglement) lancé sur
   lui frappe la troupe active du lanceur à sa place (`SpellHits` le redirige, événement `SpellReflected` ; sans effet
   sur une troupe qui n'y est pas sensible ; l'IA compte un sort néfaste sur sa propre troupe comme une perte, donc ne
   le tente pas) ; la **garde des ancêtres** (`ancestral-hold`) : tant que le Roi ne s'est pas déplacé dans le round,
   les troupes obstinées de son camp gardent leur obstination même après avoir bougé (`HeldByAncestors`). Les sorts
   de dégâts le touchent normalement. Pas encore d'effet visible du renvoi (seule la ligne du journal). Peut-être
   plus tard : un Sapeur qui surgit n'importe où (aujourd'hui, sa vitesse limite où il ressort).

#### L'Enfer
Décidé avec l'utilisateur (2026-11-11). Des créatures moins attendues que celles de Heroes 3 (choix de l'utilisateur).
- **Philosophie : le chaos et le feu.** Des démons venus d'un monde qui brûle : imprévisibles, destructeurs, ils brûlent
  tout, leurs propres pertes comprises.
- **Forces** : la magie du feu la plus forte ; **toutes les créatures de l'Enfer sont insensibles au feu** (Boule de feu,
  l'aura du Moloch, plus tard le feu de l'Archidémon) : le héros lance le feu dans la mêlée sans blesser ses troupes.
- **Faiblesses** : plus faibles au corps à corps pur (peu de défense et de PV) ; elles comptent sur la magie de leur héros
  (plus faibles sans héros, ou quand ses points de sort sont épuisés).
- **Sa marque : le prix du sang** (choix de l'utilisateur, plutôt que sacrifier des créatures pour lancer un sort) :
  chaque créature de l'Enfer tuée au combat rend au héros des points de sort (proposé : son niveau, au plus deux fois
  le savoir du héros par combat ; à régler en mesurant).
- **La classe Démoniste** : attaque 1, défense 1, puissance 3, savoir 2, charisme 9 ; École du feu au départ ; deux sorts
  du feu de niveau 1 (Soif de sang, Malédiction) ; talent **Pacte** : le prix du sang rend plus avec le niveau du héros
  (proposé : le plafond monte de 1 par niveau).
- Ressources : mercure (principale : l'habitation et la créature du niveau 7), soufre (secondaire : niveaux 5 et 6).
- **Les héros** : les quatre de la « faction sombre » qui restent (Méphis le Cornu, Dame Cendre, Zarkhan, Sorcha la
  Damnée).

| Niveau | Créature | Rôle | Capacité (chiffres proposés, à régler) |
|---|---|---|---|
| 1 | Diablotin | le voleur | vole ; **vole des points de sort** : chacun de ses coups retire au héros ennemi des points de sort (1 par 5 diablotins) et les donne au sien |
| 2 | Cultistes (des humains qui ont vendu leur âme) | le sang pour la force | **sacrifice** : avant chaque attaque, un cultiste sur dix meurt, et la troupe frappe 50 % plus fort (ces morts nourrissent le prix du sang) |
| 3 | Limon de magma | l'essaim qui se divise | **se divise** : frappée sans mourir, un tiers de la troupe se détache en une nouvelle troupe sur une case libre voisine (3 créatures au moins) ; elles se rejoignent après le combat |
| 4 | Succube | la tireuse qui séduit | tire (un baiser de feu) ; **séduit** : un tir sur quatre fige la cible, qui passe son prochain tour et ne riposte plus (sauf ceux que rien n'effraie : morts-vivants, façonnés, niveau 7) |
| 5 | Tourmenteur | le crochet | **chaîne** : au lieu d'attaquer, lance son crochet sur un ennemi à 3 cases au plus, le tire contre lui et le frappe (pas les créatures de 2 cases ni du niveau 7) : il arrache les tireurs à leur abri |
| 6 | Moloch (une idole de bronze, une fournaise dedans) | la fournaise | lent ; **aura de chaleur** : au début de son tour, brûle chaque ennemi voisin ; **grandit** : +1 attaque chaque fois que des créatures de l'Enfer meurent (+6 au plus) |
| 7 | Démon | le géant | **invoque des diablotins** : une fois par combat, à la place de son action, ouvre un portail : une troupe de diablotins (10 par démon) apparaît sur une case libre voisine, et disparaît après le combat |

Écartés : les créatures de Heroes 3 (Cerbère, Gog, Efrit, Seigneur des abîmes…), les Furies, les Scarabées de soufre, les
Feux follets, l'Oni, le Rejeton du chaos, la Forge vivante (idées à garder pour des monstres neutres ou des améliorations).
Découpage comme les autres : 1. les créatures et leurs capacités ; 2. la ville, la classe et ses héros ; 3. les images.
1. ✅ Les créatures et leurs capacités (2026-11-11, branche `enfer`) : `infernal` (insensible aux sorts de dégâts du feu
   et à l'aura du Moloch ; le prix du sang, événement `BloodPaid`), `mana-thief` (`SpellPointsStolen`), `sacrifice`
   (`UnitSacrificed`), `splits` (une nouvelle troupe en cours de combat, `UnitSplit`, une fois par round ; elle rejoint
   l'armée après), `seduce` (`UnitSeduced`, puis `UnitEntranced` au tour passé), `hook` (`TacticalBattle.Hook`,
   `UnitHooked`), `heat-aura` (`UnitBurned`), `feeds`, `summons` (« summon » dans `creatures.json` ;
   `TacticalBattle.Summon`, `UnitSummoned` ; les invoqués ne comptent ni dans les pertes ni dans l'expérience). Les
   points de sort gagnés ou volés reviennent au héros après le combat (`BattleReport.AttackerSpellPointsGained`, sans
   dépasser son maximum). À l'écran : le crochet au clic (curseur de tir), le bouton « Invoquer » (touche I, icône à
   faire), les troupes qui apparaissent, des textes. L'IA invoque aussitôt, et lance d'abord son crochet sur un tireur.
   Chiffres réglés par `Duels_a_prix_egal` (sans héros : l'Enfer y perd sa magie ; la Succube un peu en dessous, comme
   le Mage : les duels en terrain ouvert avantagent le corps à corps) :
   | | Att. | Déf. | Dégâts | PV | Vit. | Croiss. | Prix |
   |---|---|---|---|---|---|---|---|
   | Diablotin | 4 | 3 | 1-3 | 6 | 7 | 16 | 11 |
   | Cultiste | 6 | 4 | 3-5 | 16 | 5 | 12 | 28 |
   | Limon de magma | 9 | 7 | 4-6 | 34 | 4 | 9 | 50 |
   | Succube | 14 | 9 | 7-10 | 45 | 6 | 6 | 130 |
   | Tourmenteur | 15 | 12 | 10-14 | 60 | 6 | 4 | 210 |
   | Moloch | 16 | 16 | 18-24 | 120 | 4 | 3 | 360 |
   | Démon | 26 | 22 | 80-100 | 1500 | 7 | 2 | 4900 et 1 mercure |
   Mesuré : la simulation gagne toujours, normales jour 89, grandes 153, petites 101 en moyenne dont une au jour 164 (les
   gardiens du Donjon et de l'Enfer, à surveiller) ; adversaires (six semaines, Normal, or d'armée) : Château 25 200,
   morts-vivants 25 800, Barbares 31 500, Forêt 33 000, Tour 23 700, Donjon 31 400. Courbe des paysans : Diablotin 4,
   Cultiste 14, Limon 22, Succube 75, Tourmenteur 52, Moloch 121, Démon 1170. Tests : `EnferTests`.
2. ✅ La ville et le jeu (2026-11-11, branche `enfer`) : 7 habitations (`puits-diablotins` Puits des diablotins,
   `temple-culte` Temple du culte, `coulee-magma` Coulée de magma, `palais-succubes` Palais des succubes,
   `salle-tourments` Salle des tourments, `fournaise-moloch` Fournaise du Moloch, `portail-demons` Portail des démons :
   prix et places de celles de la Forêt ; soufre aux niveaux 5 et 6, mercure au niveau 7 ; le grand hôtel de ville
   accepte aussi le Palais des succubes ; noms à revoir si l'utilisateur le souhaite), la classe Démoniste (armée de
   départ au prix des autres : 15 Diablotins, 7 Cultistes, 3 Limons de magma) et son talent Pacte (le plafond du prix
   du sang monte de 1 par niveau), les 4 héros de l'Enfer jouables (plus aucun héros de la « faction sombre ») ;
   `--faction=enfer`. Un test des héros face à face fixe la faction de l'adversaire (un Magicien, tiré au sort avec la
   nouvelle faction, gagnait par ses sorts). Mesuré (un adversaire, Normal, six semaines, or d'armée) : **Enfer 30 800**, Château
   24 700, morts-vivants 25 900, Barbares 30 200, Forêt 32 500, Tour 24 800, Donjon 30 800 ; la simulation seule comme
   avant (jours 101, 86, 153). Tests : `VilleEnferTests`.
3. Les images (session images) : ✅ les 7 créatures et l'icône du bouton « Invoquer » (2026-11-15, branche
   `creatures-enfer` : voir « Créatures de l'Enfer » plus bas). ✅ La ville sur la carte (2026-11-11,
   branche `ville-enfer`, choix de l'utilisateur) : une citadelle de roche noire hérissée de pointes, des coulées de lave,
   une porte en tête de démon cornu au feu dans la gueule (`objects/town-enfer.png`, luminosité 51).

#### Le Donjon
Décidé avec l'utilisateur (2026-11-11), d'après ses images (`art-candidats/references/donjon/`).
- **Philosophie : les seigneurs des abîmes.** Des sorciers qui règnent sur les monstres des profondeurs par la peur :
  la ruse, la cruauté, la domination.
- **Forces** : des créatures d'élite puissantes (niveaux 5 à 7), une magie de destruction forte, forte en fin de partie.
- **Faiblesses** : une base faible (les premiers niveaux médiocres), des troupes chères ; faible au début.
- **Sa marque : la Terreur** : chaque coup d'une troupe du Donjon fait perdre 1 de moral à sa cible jusqu'à la fin du
  combat (cumulable) : l'armée ennemie se brise peu à peu. Insensibles : les morts-vivants, les créatures façonnées et
  celles du niveau 7.
- **La classe Seigneur des abîmes** : attaque 1, défense 1, puissance 3, savoir 2, charisme 9 ; Flèche magique et
  Malédiction ; École de l'air au départ (le feu ira à l'Enfer, choix de l'utilisateur) ; talent **Effroi** (l'armée ennemie commence le combat à −1 de moral, −1 de plus
  tous les 8 niveaux).
- Ressources : soufre (principale : l'habitation et la créature du niveau 7), mercure (secondaire : niveaux 5 et 6).
- **Les héros** : à choisir parmi ceux de la « faction sombre » (proposé : Corvin le Maudit, Ysolde la Noire, Malachar,
  Dame Sombreval, Dame Morgane ; les quatre autres, plus démoniaques, pour l'Enfer).

| Niveau | Créature | Rôle | Capacité |
|---|---|---|---|
| 1 | Homme-lézard | la masse bon marché | (la Terreur seulement) |
| 2 | Homme-grenouille | le frondeur | tire, à courte portée |
| 3 | Minotaure (le mineur des profondeurs) | le brave | son moral ne descend jamais sous +1 ; insensible à la Terreur |
| 4 | Aberration (six bras, des gueules dans les mains) | l'horreur | riposte à chaque attaque (pas une fois par round) ; ses coups font perdre 2 de moral |
| 5 | Ver des profondeurs | le fouisseur | se déplace sous terre (passe sous les troupes et les obstacles) ; sa morsure peut **avaler** une créature entière de la cible, en plus des dégâts |
| 6 | Tyran œil | le tireur d'élite | tire ; chaque tir lance aussi un sort néfaste au hasard (Malédiction, Lenteur, Faiblesse, Aveuglement) |
| 7 | Hydre | le géant | mord toutes les troupes ennemies voisines, et aucune ne riposte |

Écartés de la faction, pour les monstres neutres de la carte ou les améliorations : la Méduse (trop proche de la Naga de
la Tour), le Gnoll, l'Augure vautour, la Manticore, la Chimère, la Wyverne, le Centaure orc (trop proche des Nomades).
Le Dragon rouge reste neutre (les Nains gardent la Machine de guerre au niveau 7 : choix de l'utilisateur, 2026-11-11).
Découpage comme les autres : 1. les créatures et leurs capacités ; 2. la ville, la classe et ses héros ; 3. les images
(les créatures : session images ; ✅ la ville sur la carte, 2026-11-11, branche `villes-carte` : une forteresse sombre
creusée dans la roche autour d'un gouffre qui luit de rose et de violet, `objects/town-donjon.png`, luminosité 60).
1. ✅ Les créatures et leurs capacités (2026-11-11, branche `donjon`) : `terror` (−1 de moral par coup, jusqu'à −3 ;
   événement `UnitTerrified`), `horror` (−2), `brave` (moral au moins +1, insensible), `endless-retaliation`, `burrows`
   (se déplace comme en volant, sous terre), `swallow` (une chance sur cinq par morsure, pas en riposte, une cible d'un
   niveau au plus égal ; événement `UnitSwallowed`), `rays` (les sorts dans `creatures.json` : `rays`, pour 2 rounds, pas
   sur une cible qui y résiste) ; le Donjon garde aussi des passages de la carte (`MonsterFactions`). Chiffres réglés par
   `Duels_a_prix_egal` (chaque créature au milieu de son niveau ; la riposte sans fin de l'Aberration compte peu en duel) :
   | | Att. | Déf. | Dégâts | PV | Vit. | Croiss. | Prix |
   |---|---|---|---|---|---|---|---|
   | Homme-lézard | 3 | 2 | 1-3 | 6 | 5 | 16 | 12 |
   | Homme-grenouille | 8 | 6 | 3-5 | 17 | 5 | 12 | 28 |
   | Minotaure | 9 | 7 | 4-7 | 30 | 5 | 8 | 64 |
   | Aberration | 13 | 10 | 6-9 | 55 | 5 | 6 | 130 |
   | Ver des profondeurs | 14 | 11 | 10-14 | 60 | 7 | 4 | 215 |
   | Tyran œil | 17 | 13 | 16-22 | 80 | 5 | 3 | 340 |
   | Hydre | 23 | 21 | 60-75 | 1400 | 5 | 2 | 4900 et 1 soufre |
   La Terreur fait boule de neige (le camp qui prend l'avantage fige l'autre) : un petit changement de chiffres fait
   passer une créature de toutes ses défaites à toutes ses victoires. Mesuré : la simulation gagne toujours, les cartes
   normales (jour 87) et grandes (153) comme avant ; les petites en moyenne au jour 98, dont une au jour 158 (des gardiens
   du Donjon : leur Terreur fige l'armée, **à surveiller**). Adversaires (six semaines, Normal, or d'armée) : Château
   26 500, morts-vivants 26 100, Barbares 27 000, Forêt 33 800, Tour 23 900. Courbe des paysans : le Tyran œil en
   demande 448 (les autres niveaux 6 : 106 à 174), la Terreur brise les grosses troupes de paysans.
   Tests : `DonjonTests`.
2. ✅ La ville et le jeu (2026-11-11, branche `donjon-ville`) : 7 habitations (`bourbier-lezards` Bourbier des
   hommes-lézards, `mare-grenouilles` Mare des hommes-grenouilles, `labyrinthe` Labyrinthe des minotaures,
   `fosse-aberrations` Fosse des aberrations, `galeries-vers` Galeries des vers, `chambre-tyrans` Chambre des tyrans,
   `gouffre-hydre` Gouffre de l'hydre : prix et places de celles de la Forêt ; mercure aux niveaux 5 et 6, soufre au
   niveau 7 ; le grand hôtel de ville accepte aussi la Fosse des aberrations ; noms à revoir si l'utilisateur le
   souhaite), la classe Seigneur des abîmes (armée de départ au prix des autres : 15 Hommes-lézards, 7 Hommes-grenouilles,
   2 Minotaures) et son talent Effroi (`Talents.DreadMorale`), les 5 héros du Donjon jouables (les 4 autres de la
   faction sombre attendent l'Enfer) ; `--faction=donjon`. Mesuré (un adversaire, Normal, six semaines, or d'armée) :
   **Donjon 29 100**, Château 27 300, morts-vivants 24 700, Barbares 29 200, Forêt 34 000, Tour 25 900 ; la simulation
   seule comme avant (jours 95, 88, 153). Tests : `VilleDonjonTests`.
3. Les images (session images) : ✅ les 7 créatures (2026-11-15, branche `creatures-donjon`, choix de l'utilisateur) :
   Homme-lézard à la lance, Homme-grenouille frondeur, Minotaure mineur, Aberration à la chair pâle, Ver des profondeurs
   qui creuse une galerie dessinée par le jeu, Tyran œil qui flotte et tire un rayon violet, Hydre verte à cinq têtes qui
   mordent tout autour : images de combat, animations, petites images de la carte. Détail dans `HISTORIQUE.md`.
   ✅ La ville sur la carte. **Plus tard** (choix de l'utilisateur,
   2026-11-14) : revoir les animations du Ver des profondeurs (il devrait glisser vers sa cible en attaquant ; les
   fissures devant lui quand il creuse sont à améliorer ; peut-être une animation où il avale une créature).
   **À revoir** (idée de l'utilisateur, 2026-11-14) : l'attaque du Géant (Tour) qui frappe le sol avec sa colonne
   pourrait toucher une zone (les cases autour de sa cible) : une règle à concevoir avec la session règles.

#### La Tour de magie
Décidée avec l'utilisateur (2026-11-09).
- **Philosophie : le savoir.** Une cité de savants qui lient les esprits et animent la pierre et le métal : des mages,
  des créatures **façonnées** (gargouilles, golems) et des géants gardiens de la Tour.
- **Forces** : la magie la plus forte (la classe Magicien, la ville qui apprend plus de sorts) ; les créatures façonnées
  **résistent à la magie** ; forte en fin de partie (le Géant, puis le Titan ; les mages qui tirent de loin).
- **Faiblesses** : lente et chère au début (peu de troupes, des golems lents, une croissance faible) ; un charisme bas
  (les grosses troupes se révoltent plus vite) ; fragile au corps à corps contre les troupes rapides qui atteignent ses
  mages.
- **Sa marque : la bibliothèque des arcanes** : la guilde des mages de la Tour propose un sort de plus par niveau
  (4 sur 5 aux niveaux 1 et 2, 3 sur 5 au niveau 3, 3 sur 3 au niveau 4).
- **Résiste à la magie** (les créatures façonnées) : les sorts de dégâts font moitié moins, les sorts néfastes
  (Lenteur, Faiblesse, Malédiction, Aveuglement) ne les touchent pas ; les sorts amis marchent.
- **La classe Magicien** : attaque 0, défense 1, puissance 3, savoir 3, charisme 8 ; Flèche magique et Bénédiction ;
  Mysticisme au départ ; talent **Érudit** (+2 % de force des sorts par niveau).
- Ressources : gemmes (principale), mercure (secondaire).

| Niveau | Créature (→ amélioration plus tard) | Rôle | Capacité | Habitation |
|---|---|---|---|---|
| 1 | Gnome | petit et vif | **répare les créatures façonnées** : au début de son tour, soigne la créature de tête de chaque troupe façonnée voisine | Atelier des gnomes |
| 2 | Gargouille | le volant | vole ; façonnée : résiste à la magie | Perchoir des gargouilles |
| 3 | Golem de pierre (→ Golem d'airain) | le mur lent | façonné : résiste à la magie | Carrière des golems |
| 4 | Naga | la lame rapide | pas de riposte contre elle | Bassin des nagas |
| 5 | Djinn | le soutien volant | vole ; au début de son tour, une petite bénédiction au hasard sur une troupe amie (Bénédiction, Peau de pierre ou Hâte) | Lampe des djinns |
| 6 | Mage (→ Archimage) | le grand tireur | tire ; pas de pénalité au corps à corps | Académie des mages |
| 7 | Géant (→ Titan) | le gardien | fort au corps à corps (le Titan lancera la foudre de loin) | Sommet des géants |

Découpage comme la Forêt : 1. les créatures et leurs capacités ; 2. la ville, la bibliothèque des arcanes et la classe ;
3. les images (session images).
1. ✅ Les créatures et leurs capacités (2026-11-09, branche `tour-de-magie`) : `made` (résiste à la magie), `repairs`
   (1 PV par Gnome), `blessing` (les sorts du Djinn dans `creatures.json` : `blessings`, pour ce round), `no-melee-penalty` ;
   la Tour garde aussi des passages de la carte (`MonsterFactions`). Chiffres réglés par `Duels_a_prix_egal` (chaque
   créature au milieu de son niveau ; le Mage un peu en dessous, à revoir quand la Tour sera jouable : les duels en
   terrain ouvert avantagent le corps à corps) :
   | | Att. | Déf. | Dégâts | PV | Vit. | Croiss. | Prix |
   |---|---|---|---|---|---|---|---|
   | Gnome | 3 | 3 | 1-3 | 6 | 6 | 16 | 12 |
   | Gargouille | 9 | 9 | 3-5 | 22 | 8 | 12 | 35 |
   | Golem de pierre | 8 | 10 | 4-6 | 30 | 3 | 8 | 60 |
   | Naga | 10 | 10 | 6-8 | 42 | 6 | 6 | 130 |
   | Djinn | 13 | 11 | 9-12 | 50 | 9 | 4 | 200 |
   | Mage | 18 | 14 | 18-24 | 75 | 6 | 3 | 330 |
   | Géant | 26 | 22 | 85-105 | 1600 | 6 | 2 | 4900 et 1 gemme |
   Mesuré : la simulation gagne comme avant (jours 81, 86, 153) ; les gardiens de la Tour changent les cartes, donc les
   adversaires (six semaines, Normal, or d'armée) : Château 23 500, morts-vivants 25 800, Barbares 24 900, Forêt 32 000 ;
   Facile 27 200 et Normal 25 200 (Facile au-dessus de Normal sur ces cartes : à surveiller à l'équilibrage), Difficile
   34 700. Tests : `TourTests`.
2. ✅ La ville et le jeu (2026-11-10, branche `tour-ville`) : 7 habitations (`atelier-gnomes`, `perchoir-gargouilles`,
   `carriere-golems`, `bassin-nagas`, `lampe-djinns`, `academie-mages`, `sommet-geants` : prix et places de celles de la
   Forêt ; mercure aux niveaux 5 et 6, gemmes au niveau 7 ; le grand hôtel de ville accepte aussi le Bassin des nagas),
   la bibliothèque des arcanes (`Town.GuildSpellsAt`), la classe Magicien (armée de départ au prix des autres : 15 Gnomes,
   6 Gargouilles, 2 Golems de pierre) et son talent Érudit (`Hero.SpellBonus`), les 6 héros de la Tour jouables ;
   `--faction=tour`. Un test de l'IA fixe la faction de son adversaire (le Rôdeur, qui voit plus loin, préférait attaquer
   le héros du joueur). Mesuré (un adversaire, Normal, six semaines, or d'armée) : **Tour 27 000**, Château 25 600,
   morts-vivants 24 000, Barbares 25 600, Forêt 29 200 (les cartes changent un peu : des villes neutres de la Tour).
   Tests : `VilleTourTests`.
3. ✅ Les images des créatures (2026-11-11, branche `creatures-tour`, choix de l'utilisateur, d'après ses références) :
   Gnome mage chauve, Gargouille de pierre, Golem sans tête au visage dans la poitrine, Naga bleue à six bras armés,
   Djinn à la queue de fumée d'or, Mage brun sans chapeau qui lance un **éclair bleu des deux mains**, Géant bleu mince en
   tunique grecque qui pose sa colonne sur l'épaule pour marcher : images de combat, animations, petites images de la
   carte. Détail dans `HISTORIQUE.md`. ✅ La ville sur la carte (2026-11-11, branche `ville-tour`, choix de
   l'utilisateur) : une tour de mages au dôme d'or en bulbe (un air oriental, comme les créatures : Naga, Djinn), petits
   dômes bleus, enceinte ronde de pierre claire (`objects/town-tour.png`), ramenée à la luminosité du Château. Puis
   (branche `villes-carte`) la Tour et les Barbares redessinés avec l'ombrage doux du Château et des détails sur les murs.

#### La Forêt
Décidé avec l'utilisateur (2026-11-09).
- **Philosophie : les esprits du bois.** Magie et créatures en équilibre : les plantes, les bêtes et les elfes de la
  forêt, qui frappent de loin et se soignent.
- **Forces** : les meilleurs tireurs (l'Archer elfe tire deux fois, le Druide tire sans pénalité de distance) ; des
  troupes qui se remettent de leurs blessures.
- **Faiblesses** : peu nombreux, ils grandissent lentement (moins de croissance, des prix plus hauts, chaque créature
  plus forte : à prix égal, un combat reste égal) ; faibles au corps à corps quand l'ennemi atteint leurs tireurs.
- **Leur marque : la régénération de la nature** : au début de son tour, la créature de tête de chaque troupe de la
  Forêt retrouve un quart de ses points de vie (le Troll, tous).
- **La classe Rôdeur** (un chasseur des bois, peu tourné vers la magie ; le Druide est une créature) : attaque 3,
  défense 1, puissance 1, savoir 2, charisme 9, Soins au départ.
- Ressources : gemmes (principale : l'habitation et la créature du niveau 7), cristal (secondaire : niveaux 5 et 6).

| Niveau | Créature | Rôle | Capacité | Habitation |
|---|---|---|---|---|
| 1 | Farfadet | petit et vif | vole | Bosquet des farfadets |
| 2 | Sanglier | la charge | charge (+5 % de dégâts par case parcourue) | Bauge des sangliers |
| 3 | Archer elfe | le meilleur tireur | **tire deux fois** | Camp des archers |
| 4 | Cerf | le rapide | **bondit** : passe par-dessus troupes et obstacles, comme en volant, mais au sol | Clairière des cerfs |
| 5 | Druide | tireur et soigneur | tire sans pénalité de distance ; **aura de soin** : au début de son tour, soigne la créature de tête de chaque troupe amie voisine | Cercle des druides |
| 6 | Tréant | le mur lent | résiste aux tirs (écorce) ; ses coups **enracinent** : la cible ne peut pas se déplacer à son prochain tour | Bois des tréants |
| 7 | Troll (des neutres) | le géant | la régénération de la nature, comme toute la Forêt (sa régénération entière revient au Troll ancien) | Antre du troll |

1. ✅ Les créatures et leurs capacités (2026-11-09) : `nature-regeneration`, `double-shot`, `leaps`, `healing-aura`
   (5 PV par Druide), `roots` (`BattleUnit.IsRooted`). Le Troll n'est plus neutre (les neutres : le Dragon rouge seul).
   **La régénération entière ne va pas à une créature de 1000 PV** : rien ne la tue sous 1000 dégâts par tour, tout la
   tue au-delà (à prix égal : 100 % de victoires contre les Anges, 100 % de défaites contre les Béhémoths) ; le Troll
   prend donc la régénération de la nature. Chiffres réglés par `Duels_a_prix_egal` (chaque créature au milieu de son
   niveau ; au niveau 7, les écarts restent grands : les Trolls battent les Anges, perdent contre les Béhémoths) :
   | | Att. | Déf. | Dégâts | PV | Vit. | Croiss. | Prix |
   |---|---|---|---|---|---|---|---|
   | Farfadet | 4 | 3 | 1-3 | 7 | 7 | 18 | 12 |
   | Sanglier | 9 | 7 | 4-6 | 26 | 7 | 12 | 44 |
   | Archer elfe | 9 | 6 | 3-5 | 18 | 6 | 9 | 56 |
   | Cerf | 12 | 10 | 6-9 | 48 | 9 | 6 | 120 |
   | Druide | 13 | 10 | 10-14 | 55 | 6 | 4 | 210 |
   | Tréant | 16 | 20 | 15-20 | 150 | 4 | 3 | 450 |
   | Troll | 22 | 20 | 65-80 | 1200 | 6 | 2 | 4900 et 1 gemme |
2. ✅ La ville et le jeu (2026-11-09) : 7 habitations (`bosquet-farfadets`, `bauge-sangliers`, `camp-archers`,
   `clairiere-cerfs`, `cercle-druides`, `bois-treants`, `antre-troll`), la classe Rôdeur (armée de départ au prix des
   autres : 15 Farfadets, 5 Sangliers, 2 Archers elfes), les 6 héros de la Forêt jouables ; `--faction=foret`. Tests :
   `ForetTests`. Mesuré (un adversaire, Normal, six semaines, or d'armée) : Château 25 000, morts-vivants 20 900,
   Barbares 23 100, **Forêt 30 600** : la plus forte (sa régénération lui fait perdre moins à chaque combat), à revoir
   à l'équilibrage. La simulation seule gagne toujours (petites cartes jour 79, normales 86, grandes 153).
3. ✅ Les images des créatures (2026-11-09, branche `creatures-foret`, choix de l'utilisateur) : Farfadet aux ailes de
   feuilles (bord semi-transparent), Sanglier sauvage, Archer elfe encapuchonné, grand Cerf, Druide trapu à la chouette
   (d'après deux références de l'utilisateur ; il lance un tourbillon de feuilles dessiné par le jeu), Tréant en fleurs
   (un arbre qui marche, pas un homme de bois) : images de combat, animations, petites images de la carte. Détail dans
   `HISTORIQUE.md`. ✅ La ville de la Forêt sur la carte (2026-11-09, branche `ville-foret`) : un grand chêne aux maisons
   perchées dans une palissade ronde (`objects/town-foret.png`).

#### Les Barbares
Décidé avec l'utilisateur (2026-11-08).
- **Philosophie : la horde.** La force brute, le nombre et la vitesse : ils frappent les premiers et fort, et méprisent
  la magie.
- **Forces** : des troupes bon marché qui grandissent vite (la faction la plus forte des premières semaines) ; beaucoup
  d'attaque et de dégâts, des créatures rapides ; le héros qui a le plus d'attaque. **La horde** (choix de
  l'utilisateur) : aux niveaux 1 à 4, des créatures moins chères, plus nombreuses chaque semaine et plus faibles une à
  une ; à prix égal, un combat reste égal : leur avantage est d'en avoir plus.
- **Aux sièges** (quand les combats en ville auront des murs ; idée de l'utilisateur, 2026-11-08) : les troupes barbares
  peuvent frapper les murs directement, contrairement aux autres factions ; ils prennent ainsi une ville sans attendre
  leurs Oiseaux-tonnerre, leurs Orcs tirant mal.
- **Faiblesses** : peu de défense (ils frappent fort mais meurent vite) ; peu de tireurs, à courte portée ; peu de
  magie, par le héros seulement (la guilde des mages est complète) ; moins disciplinés que le Château (charisme 10
  contre 14).
- **Leur marque : la Rage** (comme la nécromancie des morts-vivants) : une troupe barbare frappée gagne de l'attaque
  jusqu'à la fin du combat (chiffres à régler en mesurant).
- **La classe Barbare** : attaque 3, défense 2, puissance 0, savoir 1, charisme 10, aucun sort au départ ; armée de
  départ : 25 Gobelins, 8 Loups, 3 Orcs.
- Le Gobelin, le Loup, l'Orc et l'Ogre deviennent barbares et restent des monstres de la carte (comme les morts-vivants).
  Mis à l'échelle de prix du Château, ils rapportent moins d'expérience : ✅ l'expérience reste le prix des créatures
  tuées (juste aussi pour les créatures magiques, puisque les prix se règlent à prix égal), et chaque niveau en demande
  moins (2026-11-09, choix de l'utilisateur) : 700 × (n−1) × n / 2 au lieu de 1000. Mesuré : la simulation gagne au
  niveau 21 environ (20 avant les Barbares ; 300 la menait au niveau 31).

| Niveau | Créature | Rôle | Capacité | Habitation |
|---|---|---|---|---|
| 1 | Gobelin (des neutres) | la masse | rapide au premier round (déjà) | Hutte des gobelins |
| 2 | Loup (des neutres) | le harceleur | frappe deux fois (déjà) | Tanière des loups |
| 3 | Orc (des neutres) | le seul tireur | tir court (déjà) | Camp des orcs |
| 4 | Ogre (des neutres) | la brute | coup écrasant : ignore 40 % de la défense (déjà) | Fort des ogres |
| 5 | **Nomades** (choix de l'utilisateur) | cavaliers rapides, sur 2 cases | **frappe et repart** : après l'attaque, ils se déplacent avec ce qui leur reste de mouvement (l'ennemi riposte comme d'habitude) ; à mesurer contre les armées lentes | Campement nomade |
| 6 | **Oiseau-tonnerre** | le volant, sur une case (ailes repliées) | vole ; ses coups appellent parfois la foudre (des dégâts en plus) | Nid du tonnerre |
| 7 | Béhémoth | le géant, debout sur une case | ses griffes ignorent 60 % de la défense | Repaire du béhémoth |

Habitations : mêmes prix et places que celles du Château, cristal (secondaire) aux niveaux 5 et 6, soufre (principale)
au niveau 7 et pour le Béhémoth. Découpage comme les morts-vivants :
1. ✅ Les créatures et leurs capacités (2026-11-08) : `rage` (+2 attaque par coup reçu, +6 au plus), `hit-and-run`
   (`TacticalBattle.RetreatSteps` ; le bouton « Se défendre » devient « Rester » ; l'IA repart vers la case que le moins
   d'ennemis au corps à corps peuvent atteindre, mais reste à côté d'un tireur pour l'empêcher de tirer), `lightning`
   (une chance sur cinq, 15 dégâts par oiseau, sans défense), `rending` (60 %). Les gardiens de la carte se mesurent à
   une force de référence par niveau (`MapGenerator.GuardReferenceStrength`, celle des anciens neutres).
   Chiffres réglés avec l'utilisateur, par une nouvelle mesure, **`Duels_a_prix_egal`** (chaque créature contre celles
   du même niveau pour le même prix en or : plus juste que la courbe des paysans, qui ne voit pas la taille des troupes)
   : chaque créature barbare est au milieu de son niveau (elle bat l'une, perd contre l'autre, ou de peu).
   | | Att. | Déf. | Dégâts | PV | Vit. | Croiss. | Prix |
   |---|---|---|---|---|---|---|---|
   | Gobelin | 2 | 1 | 1-2 | 3 | 5 | 30 | 6 |
   | Loup | 6 | 4 | 2-4 | 9 | 6 | 18 | 21 |
   | Orc | 8 | 3 | 2-5 | 15 | 4 | 14 | 32 |
   | Ogre | 10 | 5 | 4-6 | 30 | 4 | 9 | 76 |
   | Nomade | 13 | 8 | 10-14 | 45 | 8 | 6 | 165 |
   | Oiseau-tonnerre | 16 | 10 | 18-24 | 90 | 11 | 4 | 320 |
   | Béhémoth | 22 | 20 | 80-90 | 1400 | 7 | 2 | 4900 et 1 soufre |
   **Frappe et repart** (mesure `Nomades_frappe_et_repart`) aide un peu contre les troupes lentes (contre des Ogres ou
   des Vampires du même prix : 2 victoires sur 20 avec, aucune sans) sans rendre les Nomades plus forts que les autres
   créatures du niveau 5. **Tailles** (choix de l'utilisateur, avec les images) : les Nomades sur 2 cases (un vrai
   cheval), le Béhémoth et l'Oiseau-tonnerre sur une seule (debout ; l'oiseau, ailes repliées) ; à prix égal, les Nomades battent les Moines (il leur
   reste 71 %) et perdent contre les Vampires (93 % restent aux Vampires) ; le Béhémoth ne change pas.
   Mesuré (avec 30 paysans par semaine) : la simulation gagne les petites cartes vers le jour 89, les normales 86, les
   grandes 152 ; un adversaire : Facile 11 700, Normal 15 400, Difficile 20 600 en or d'armée ; Château 21 900,
   morts-vivants 15 600. Les anciens neutres rapportent moins d'expérience (prix plus bas) : **l'expérience sera
   redéfinie**. Tests : `BarbaresTests`.
   Vu au passage (`Duels_a_prix_egal`, à revoir avec l'utilisateur) : l'Archer perd contre tous les niveaux 2, le
   Griffon bat largement les niveaux 3, le Vampire écrase le Moine, le Cavalier écrase les niveaux 6, l'Ange écrase le
   Colosse d'os.
2. ✅ La ville et le jeu (2026-11-08) : les 7 habitations (`hutte-gobelins`, `taniere-loups`, `camp-orcs`, `fort-ogres`,
   `campement-nomade`, `nid-tonnerre`, `repaire-behemoth` : prix et places de celles du Château ; le grand hôtel de
   ville demande aussi bien le Fort des ogres), la classe Barbare (armée de départ ramenée au prix des autres : 30
   Gobelins, 10 Loups, 4 Orcs), les 5 héros barbares jouables ; rien à changer dans l'IA ni la nouvelle partie (tout
   vient des données). Option `--faction=barbares`. Tests : `VilleBarbaresTests`. Mesuré (un adversaire, Normal, six
   semaines, en or d'armée, ce qui compare bien puisqu'à prix égal un combat est égal) : Château 22 500, morts-vivants
   16 100, **Barbares 17 400** ; la simulation seule comme avant (jours 87, 87, 152). À voir : la horde devrait être
   la plus forte des premières semaines.
3. Les images (session images : Nomades, Oiseau-tonnerre, Béhémoth, la ville ; en attendant, des ronds de couleur).

#### Les morts-vivants
| Niveau | Créature | Rôle | Capacité (proposition) |
|---|---|---|---|
| 1 | Squelette (des neutres) | l'infanterie de base, nombreuse | encaisse les tirs (déjà) |
| 2 | Zombie | un mur lent et solide | lent, beaucoup de PV |
| 3 | Fantôme | grossit en tuant (choix de l'utilisateur) | vole ; **chaque créature ennemie tuée ajoute un fantôme à sa troupe**, pour de bon : une troupe qui grossit peut dépasser le charisme du héros et se révolter **aussitôt, pendant le combat** (elle change de camp ; ses survivants quittent l'armée ; 2026-11-08) |
| 4 | Araignée géante | (choix de l'utilisateur) | morsure **empoisonnée** : des dégâts au début de chaque tour de la cible |
| 5 | Vampire | le tueur au corps à corps | vole, **draine la vie** : ses dégâts soignent sa créature de tête, puis ramènent des vampires tués dans ce combat (jamais plus qu'au début) |
| 6 | Liche | le tireur | **nuage de mort** : son tir touche aussi les cases voisines, sauf les morts-vivants |
| 7 | Colosse d'os | le géant du sommet (fort comme l'Ange) | **balayage** : frappe tous les ennemis voisins |
Écartés pour le niveau 4 : la Banshee (trop proche du Fantôme : elle sera sa version améliorée), la Goule, le
Chevalier noir. Images de combat : toutes faites (Squelette, Zombie, Fantôme, Araignée, Vampire, Liche, Colosse d'os).
Décidé : **tous les morts-vivants sont insensibles aux sorts de l'esprit** (aveuglement, malédiction, faiblesse), et
**soins et résurrection n'ont pas d'effet sur eux** (les premières créatures qui résistent à la magie).
Chiffres décidés (2026-11-06), chaque créature à peu près aussi forte que celle du Château du même niveau (paysans pour
en vaincre une : Squelette 3, Zombie 10, Fantôme 13, Araignée 24, Vampire 50, Liche 106, Colosse d'os 964 ; Château :
Archer 8, Griffon 14, Épéiste 26, Moine 63, Cavalier 116, Ange 1643). Squelette 3/2, 1-2, 4 PV, 9 or (l'utilisateur
craint qu'il soit trop fort face aux paysans : à surveiller en jeu) ; Zombie 5/6, 2-3, 20 PV, vitesse 3 ; Fantôme 7/5,
3-4, 12 PV, vitesse 7 ; Araignée 10/9, 5-7, 30 PV (poison : 2 par araignée, 3 tours) ; Vampire 13/10, 9-12, 40 PV ;
Liche 15/13, 14-18, 70 PV ; Colosse d'os 22/24, 60-80, 1400 PV, vitesse 6. Le Colosse est plus faible que l'Ange :
à revoir.
Découpage : 1. ✅ les créatures et leurs capacités au combat (`undead`, `ghost-growth`, `poison`, `life-drain`,
`death-cloud`, `sweep`), les morts-vivants déjà rencontrés comme monstres sur la carte (`MapGenerator.MonsterFactions`) ;
un gardien d'une autre faction que les neutres est dimensionné pour avoir la force du gardien neutre du même niveau
(`CreatureStrength`, mesurée au combat), sinon les Liches (niveau 6, les neutres n'en ont pas) et les Colosses (4 fois
un Dragon) rendaient les cartes bien plus dures. La simulation gagne comme avant. Tests : `MortsVivantsTests`.
2. ✅ La ville et le jeu : les bâtiments communs à toutes les factions (salle du village et hôtels de ville, taverne,
guilde : faction « * ») et 7 habitations par faction (Ossuaire, Cimetière, Étang des noyées, Antre des araignées, Crypte,
Tour de la liche, Charnier : mêmes prix et places que celles du Château) ; un prérequis « a|b » (le grand hôtel de ville
demande la Caserne ou l'Antre). Nouvelle partie : la faction (le Château ou les morts-vivants), sa classe et ce qu'elle
donne ; les adversaires et les villes neutres ont une faction au hasard (un hasard à part, fixé par la graine : la carte
ne change pas) ; une garnison neutre de morts-vivants vaut celle du Château. Le Nécromancien (armée : 20 Squelettes,
10 Zombies, 2 Fantômes) et la **nécromancie** (`Talents`, événement `DeadRaised`, dans le journal). L'IA construit dans
l'ordre des niveaux pour toutes les factions. La simulation (Château) prend dans une ville prise les 7 meilleures
troupes (avec deux factions, il y a plus de créatures que de places). Option `--faction=morts-vivants`. Tests :
`VilleMortsVivantsTests`. Mesuré (un adversaire, Normal, six semaines) : Château 16 100 or d'armée, morts-vivants
12 700 : un peu plus faibles, à revoir (avec le Colosse). L'amplificateur de nécromancie : plus tard, une fois la
nécromancie essayée.
3. ✅ Les images : les images de combat ; la ville des morts-vivants sur la carte (`art/objects/town-morts-vivants.png`,
`Art.Town` : chaque ville prend l'image de sa faction, sinon le château ; la carte, les listes, le royaume, l'écran de
ville). Les petites images de la carte (24 × 24) de **toutes les créatures**, refaites d'après leur image de combat
(PixelLab `pro` : l'image de combat comme sujet, une petite image comme style ; 64 candidats d'un coup), choisies par
l'utilisateur sur une page de choix ; Zombie et Fantôme par Retro Diffusion. Reste : la ville vue de face en images
(étape 10, pour toutes les factions).
✅ **Les créatures dans l'interface, comme Heroes 1 et 2** (choix de l'utilisateur, 2026-11-07, après la liste de toutes
les places : on y mêlait l'image de combat et la petite image, à des tailles de pixels différentes) : les places
ordinaires (garnison, héros en ville, écran du héros, recrutement, construction, bilan) montrent **l'image de combat,
toujours ×2** (`Ui.CreaturePicture`) : les pixels ont la même taille partout, et une grande créature qui ne tient pas
est coupée en gardant sa tête et son avant ; les places étroites (panneau de la carte, ordre de jeu du combat) montrent
la petite image (`Ui.SmallCreatureArt`, ×2 dans le panneau). Plus tard peut-être : les créatures animées sur la carte.
Décidé aussi : la classe **Nécromancien** (attaque 1, défense 1, puissance 2, savoir 2, charisme 8, Malédiction au
départ, talent **Nécromancie** : après une victoire, 10 % des ennemis tués + 2 % par niveau se relèvent en Squelettes,
sans dépasser son charisme). À choisir avec la ville : les noms des habitations (proposés : Ossuaire, Cimetière, Étang
des noyées, Antre des araignées, Crypte, Tour de la liche, Charnier).

### 10. Finitions : apparence et détails
- ✅ Les lieux de la carte en images (2026-11-12, branche `lieux-carte`, PixelLab « pro », 0,80 $) : 16 candidats par lieu
  à 64 × 64, la mine d'or comme style. Choisis : autels attaque 10, défense 8, puissance 14, savoir 9 (réduits à 32),
  sage 8, roue à eau 13, moulin 12 (remplacé le 2026-11-18 par le 8, tout brun et illisible : le 8 au contour adouci), tour 16 (contour adouci le 2026-11-19, `adoucir.py … 0.85 90 2` ; la version qui adoucit aussi les traits du haut, `… 0.85 90 2 0.4`, est gardée dans `art-candidats/archives/2026-11-19-tour/tower-c.png` si l'utilisateur change d'avis), temple 8, cercle des fées 12. L'utilisateur trouvait les lieux petits : comme
  Heroes 2 et 3, les lieux hauts prennent 2 cases (`TallSite.FootprintAt` : la porte, `Position`, et la case au-dessus,
  qui bloque ; `Large`, faux pour les sauvegardes d'avant la version 7). Une empreinte de 2 × 2 aurait mis la porte à
  côté du héros (largeur paire) : une colonne de 2 cases et une image qui déborde d'une demi-case de chaque côté, comme
  Heroes. Générateur : `PlaceSites` vérifie la case du toit (terrain où l'on marche, sans route ni objet) et les cases
  sous l'image (`_overhang`, que `CanPlaceGuard` évite aussi : des monstres errants s'y posaient). Pieds revus par
  l'utilisateur. Tests : `CarteObjetsTests` (lieux hauts, générateur sur quatre cartes, sauvegarde).
- ✅ La mer, les règles (2026-11-14, branche `mer`, conçue avec l'utilisateur comme Heroes 3) : un bateau (`Boat`, objet
  de la carte sur l'eau) ; **un héros sur l'eau est dans son bateau** (rien de plus dans la sauvegarde des héros). Le
  `Pathfinder` sait les pas : à terre vers l'eau seulement sur un bateau (on y monte, le chemin continue en mer), en mer
  sur l'eau (pas dans un autre bateau), débarquer sur une case de terre libre ; monter à bord ou débarquer prend tout le
  mouvement qui reste (`Hero.StepTo`, `PathReachability`, `DaysTo`) et coûte une journée pour choisir un chemin
  (`BoardingPenalty`) ; l'eau coûte comme l'herbe. Monter à bord retire le bateau de la carte, débarquer le laisse sur la
  case d'eau quittée (`BoatBoarded`, `BoatLeft`). Un monstre de la terre n'attaque pas un bateau (`GuardsAround`). Le
  **chantier naval** : un bâtiment commun (2000 or, 20 bois), seulement dans une ville dont les murs touchent l'eau
  (`Town.BoatSpot`, calculé d'après la carte, `BuildResult.NotCoastal`) ; un bateau coûte 1000 or et 10 bois
  (`GameState.BuildBoat`), apparaît sur cette case si elle est libre ; un **chantier de la côte** (`Shipyard`, lieu de la
  carte) vend de même (`BuyBoat`, une fenêtre à la visite) ; le générateur les posera avec les types de cartes. L'IA
  sait monter dans un bateau amarré (`SafeCosts` suit les règles du `Pathfinder`). En attendant les images : un bateau
  dessiné (coque, mât, voile aux couleurs du héros qui le mène) et une cabane au ponton. Développement : `--boat`.
  Tests : `MerTests`.
- ✅ Les routes, premières règles (2026-11-15, branche `routes-sans-issue`, en quatre tours avec l'utilisateur sur une page
  de six cartes entières, `art-candidats/archives/2026-11-15-routes/`) :
  1. **Plus de route qui ne mène nulle part** : une route était tracée du centre de chaque zone à ses passages ; dans une
     zone sans ville, elle finissait au milieu de rien (126 bouts sans rien sur 16 cartes). `RemoveDeadEndRoads` retire
     les bouts qui n'arrivent à aucune destination ; le terrain reste. **Les routes serpentent** (`WindingCost`, un bruit
     doux). **Des petites routes** vers les mines des villes et les ports (`ConnectToRoads`, `RoadToNetwork`).
  2. **Les villes bien réparties** (remarque de l'utilisateur : six villes en haut de la carte, aucune en bas) : les zones
     étaient déjà une grille régulière, mais les villes neutres prenaient des zones au hasard ; chacune va maintenant dans
     l'une des zones les plus éloignées des villes déjà posées.
  3. **Plus de passages** : chaque zone en a au moins deux (plus de cul-de-sac), d'autres au hasard jusqu'à trois
     (`ExtraPassageChance` 0,4, `MaxPassages`) ; des sentiers vers les lieux. Deux obélisques ne se posent plus côte à
     côte de part et d'autre d'un passage ; le test des gardiens demande deux tiers des mines et coffres gardés (trois
     quarts avant : les passages gardent plus de cases libres).
  4. **D'après 160 cartes de Heroes 2 et 3** (`tools/cartes/` ; la route y couvre 3 à 9 % de la terre, la nôtre 13 %,
     faite de boucles entre les passages ; 1,3 à 1,5 villes pour 1000 cases de terre ; les routes finissent surtout aux
     villes, aux mines, aux habitations, aux monstres et aux tas) : le réseau principal est un arbre (`KeepMainNetwork`)
     entre les villes, les mines des villes et les ports ; de courts sentiers vers les autres mines (5 cases) et les lieux
     (3 cases) ; au plus 1,5 villes pour 1000 cases de terre (`TownsPerLand` ; chaque joueur garde sa ville voisine).
  Mesuré : routes 7,5 % de la terre (normale), 8,4 % (grande), 5,2 % (îles), 4,2 % (archipel), 11,6 % (petite : quatre
  villes sur peu de terre) ; villes pour 1000 cases de terre 1,7 (continent), 1,8 (îles, 3 avant). Adversaires (six
  semaines, Normal, or d'armée) : continent 55 000, des mers 52 100, îles 32 200, archipel 31 700. Tests :
  `Les_routes_ne_menent_pas_nulle_part`, `Les_villes_sont_reparties_sur_toute_la_carte`.
- ✅ L'herbe qui déborde sur les routes (2026-11-15, branche `routes-herbe`, choisie par l'utilisateur sur une page qui
  compare cinq forces, à la taille du jeu et ×2, `art-candidats/archives/2026-11-15-herbe-routes/`). Dessinée par le
  shader (`terrain.gdshader`), seulement là où le sol est de l'herbe : par plaques le long du bord (`road_grass_share`
  0,45 du bord, plaques de `road_grass_scale` 10 pixels d'art), l'herbe avance sur la route de `road_grass` 2,2 pixels
  d'art au plus, en brins de longueurs inégales (chaque colonne de pixels, par bouts de trois), le bout des brins un peu
  plus clair ; de petites touffes de trois brins sur la route (pied sombre, pointe claire ; `road_tufts` 0,3 des carrés
  de 7 pixels près du bord, `road_tufts_middle` 0,04 au milieu) ; une petite ombre de l'herbe vers le haut et la gauche
  (`road_grass_shadow`). **Selon le tronçon** (idée de l'utilisateur) : un bruit doux de `road_grass_stretch` 70 pixels
  d'art donne des tronçons sans herbe (sous 0,32) et d'autres envahis (au-dessus de 0,68 : 1,6 fois plus). Écarté :
  3 pixels partout (les routes de 7 à 9 pixels paraissaient étouffées). Avec `--ground=road_grass:0,road_tufts:0,
  road_tufts_middle:0`, rien. Attention : `--zoom` montre la carte à ×1,5, pas à la taille du jeu.
- ✅ Le bord des routes sur le sable (2026-11-15, branche `routes-sable`, choisi par l'utilisateur parmi huit versions
  sur une page de comparaison, `art-candidats/archives/2026-11-15-routes-sable/`). Avant, le bord sur le sable était
  celui de l'herbe (net, cranté) : la route semblait posée sur le sable, la terre s'y perdait. Dans le shader, là où le
  sol est du sable : la route et le sable se mêlent sur plus de pixels (`road_sand_scatter` 4 au lieu de 2,5) ; **le
  sable porté par le vent recouvre le bord** en dunes douces (`road_sand_drift` 3,5 pixels d'art au plus,
  `road_sand_drift_share` 0,6 du bord, dunes de `road_sand_drift_scale` 14 pixels, selon le tronçon comme l'herbe),
  tramé à son bout ; **un bourrelet de sable repoussé**, plus clair (`road_sand_berm` +8 % sur 3 pixels), le long du
  bord **qui reste visible** : le sable posé sur la route et celui d'à côté ont la même lumière (remarque de
  l'utilisateur : ils ne s'accordaient pas), et rien là où le sable couvre toute la largeur ; le liseré au soleil de
  l'herbe (`road_rim`) n'est pas dessiné sur le sable. Écartés : 4,5 et 6 pixels (une route trop enterrée). Vu au
  passage : les grandes zones claires et sombres du sol (`shade`) font parfois une bande le long d'une route, sans
  rapport avec elle. Développement : `--center=road-sand` (une route sur le sable).
- ✅ Les rives (2026-11-15, branche `rives`, choisies par l'utilisateur sur une page de comparaison en deux tours,
  `art-candidats/archives/2026-11-15-rives/`). Toutes les rives : lacs, mer et rivières (les ruisseaux ne changent
  pas). Dans le shader du sol :
  - **Les rives par tronçons** (`bank_kind` : un bruit doux par tronçons de `bank_stretch` 80 pixels d'art) : sous
    `bank_grass` 0,3, l'herbe descend jusqu'à l'eau, bordée d'une ligne de terre mouillée (`mud_width` 2, `mud_color`) ;
    au-dessus, une plage plus ou moins large ; au-dessus de `bank_pebbles` 0,72, une plage de galets (`pebbles` 0,35 des
    carrés de 4 pixels). **Des plages plus larges** (deuxième tour, l'utilisateur voulait plus de sable) : `beach_width`
    11 au lieu de 8 ; écartés : moins de rives herbeuses (`bank_grass` 0,2 ou 0,15, avec 14 pixels de sable).
  - **La profondeur** (`water_depth`) : une eau claire et un peu turquoise près du bord (`shallow` 0,25 sur
    `shallow_width` 5 pixels, tramée), qui s'assombrit vers le large (`deep` 0,35, de `deep_start` 10 à `deep_end` 76
    pixels, en 5 paliers tramés au bord bruité de ±6 pixels). Au-delà d'une demi-case, la distance à la terre vient d'une
    nouvelle couche de `water_field` (le bleu : `RiverField.Depth`, une transformée de distance en deux passes, jusqu'à
    `RiverField.Deep` 96 pixels ; image `Rgbh`). Vu : entre des îles proches, la mer reste claire (un détroit peu
    profond).
  - **Des roseaux** (`reed_at`) en touffes sur les rives herbeuses (`reeds` 0,6, `reed_share` 0,45 des tronçons, tous
    les `reed_scale` 24 pixels), quelques-uns à massette brune ; **des rochers** (`rock_at`) dans l'eau peu profonde,
    entourés d'écume (`rocks` 0,08).
  Écartés au premier tour : une version plus légère et une plus forte (plus de profondeur, de roseaux et de rochers).
  **Troisième tour** (demandé par l'utilisateur : des roseaux sur le sable, de l'herbe sur les plages, même page,
  `art-candidats/archives/2026-11-15-rives-herbe/`) : une part `reed_sand` 0,3 des touffes de roseaux pousse aussi sur
  les plages de sable (pas sur les galets) ; **de petites plaques d'herbe au milieu du sable**, ni contre l'eau ni contre
  l'herbe (`beach_patches` 0,3, un bruit de `beach_patch_scale` 5 pixels) ; **des touffes d'herbe doubles** semées sur
  le sable, plus côté terre (`beach_tufts` 0,25 des carrés de 6 pixels). Écartés : une herbe qui gagne sur le sable
  depuis la terre (la plage s'amincissait, alors que l'utilisateur voulait plus de sable), une version plus légère et
  une plus forte.
- ✅ Les ruisseaux (2026-11-16, branche `ruisseaux`, choisis par l'utilisateur sur une page de comparaison en deux
  tours, `art-candidats/archives/2026-11-16-ruisseaux/`). Ils n'avaient pas changé avec les rives. Dans le shader du sol
  (`stream_depth` : la profondeur du ruisseau, avec ses mares) :
  - **des mares** où il s'élargit (`stream_pools` 2,5 pixels d'art de plus, un bruit de 34 pixels) ;
  - **l'eau claire contre le bord** (`stream_shallow` 0,3) **et plus sombre au milieu** (`stream_deep` 0,25) ;
  - **une rive de terre mouillée** (`stream_mud` 0,5 sur `mud_width` pixels) ; **des bancs de sable** par endroits, qui
    mangent le bord de l'eau (`stream_bars` 0,3) ; **des pierres** dans l'eau avec de l'écume (`stream_stones` 0,12 des
    carrés de 8 pixels, `stream_stone_at`) ; **des roseaux**, plus rares que sur les lacs (`stream_reeds` 0,25 ;
    `reed_at` sert aux deux) ;
  - **l'embouchure** (deuxième tour, l'utilisateur voulait un delta plutôt qu'une coupure nette) : un éventail de sable
    sur la rive où il se jette (`stream_mouth` 9 pixels), le ruisseau qui s'élargit (`stream_delta` 4) autour de petits
    îlots de sable (`stream_islets` 0,5), des rives de sable mouillé sans contour, une eau claire en éventail dans le
    lac, la mer ou la rivière. Écartés : des îlots de sable avancés dans l'eau (des taches), un delta plus fort.
    **À améliorer plus tard** (l'utilisateur) : de vrais bras qui se partagent, ce qui demande le sens du ruisseau à son
    embouchure (`RiverField`).
  Au passage : le commit des roseaux sur le sable et de l'herbe des plages, poussé après la fusion de `rives`, est repris
  ici.
- ✅ Le delta des ruisseaux et des rives arrondies (2026-11-16, branche `delta`, choisis par l'utilisateur sur une page
  de comparaison en trois tours, `art-candidats/archives/2026-11-16-delta/`) :
  - **de vrais bras** (`RiverField.Delta`) : à l'embouchure, le sens du ruisseau (sa dernière case vers la case d'eau)
    donne un ou deux bras plus étroits (`Arm` 1,5 pixel de moins) qui quittent le ruisseau une case avant la rive et
    s'ouvrent en éventail vers l'eau ; à gauche, à droite ou les deux selon un tirage par embouchure ; pas de bras du
    côté d'où vient le ruisseau (il le recouvrirait), ni s'il ne finit pas dans l'eau. La terre entre les bras devient
    des îlots de sable. Le ruisseau s'élargit moins (`stream_delta` 4 → 2).
  - **des rives arrondies** (l'utilisateur trouvait trop d'angles durs) : la rive des lacs et de la mer suivait les
    cases (droite le long d'une rangée, à 45° le long d'un escalier) ; `water_part` la mêle à une courbe douce sur les
    neuf cases autour (B-spline quadratique, `shore_round` 0,8). Écarté : toute en courbes et plus ondulée
    (`shore_round` 1, `shore_wave` 1,4), qui pince certaines rives. Les cases où l'on marche ne changent pas.
  - **l'embouchure fondue** : l'écume de la rive barrait l'entrée du ruisseau (l'eau du ruisseau compte maintenant
    pour l'écume, `water_at`), et le ruisseau prend peu à peu la teinte claire de l'eau où il se jette.
- ✅ L'herbe (2026-11-16, branche `herbe`, choisie par l'utilisateur sur une page de comparaison en deux tours,
  `art-candidats/archives/2026-11-16-herbe/`). Comparée à Heroes 2 (peinte, des touffes et des creux, des fleurs en
  bouquets) et Heroes 3 (vert olive plus foncé, grandes taches claires et sombres, beaucoup d'objets de décor), la
  nôtre n'avait qu'un grain très fin et un ombrage très large : une surface plate. Dans le shader du sol :
  - **un vert un peu plus profond** (`grass_depth` 0,92) ;
  - **des taches d'herbe sèche (plus jaune) et drue (plus verte)**, d'environ 40 pixels d'art, à deux niveaux tramés
    (`grass_tint` 0,6, `grass_dry`, `grass_lush`) ;
  - **des touffes** dans 2 carrés de 7 pixels sur 10 (`grass_clumps` 0,2, `grass_clump_at`) : un brin seul, une touffe
    ou une touffe doublée, sombres au pied, claires au bout, une ombre à gauche ;
  - **des bouquets de fleurs** (`flower_patches` 0,1 des carrés de 22 pixels : 3 à 7 fleurs d'une couleur), moins de
    fleurs seules (`flower_density` 0,1 → 0,05). À embellir plus tard (l'utilisateur).
  Écartés : façon Heroes 2 (touffes partout), façon Heroes 3 (grandes taches, plus foncé : des plaques), des touffes
  rassemblées par endroits (`grass_clump_gather`, gardé à 0) ; au premier tour, trop de touffes (0,35).
- ✅ Les rivières et les ponts (2026-11-15, branche `rivieres-ponts`, en deux tours avec l'utilisateur sur une page de
  six cartes entières, `art-candidats/archives/2026-11-15-rivieres/`). D'après 50 cartes de Heroes 3
  (`tools/cartes/rivieres_h3.py`) : 40 en ont, 2,3 % de la terre en médiane, 0,6 rivière pour 1000 cases de terre, longue
  de 20 cases, un décor qu'on traverse. Décidé avec l'utilisateur : **deux sortes**, une couche sur le terrain comme la
  route (`RiverKind` : `River`, `Stream`, `Bridge` ; `AdventureMap.GetRiver`, sauvegarde version 8).
  1. **Les rivières** remplacent les murs d'eau entre les zones et bloquent (`AdventureMap.IsPassable`). Premier tour :
     30 % des frontières au hasard, de petits bouts isolés ; puis (remarque de l'utilisateur) **d'un bord de la carte à
     un autre** (`ChooseRivers` : un plus court chemin par les cases des murs, d'un bout du bord à un autre bord, ou à la
     mer, un poids au hasard par frontière ; une rivière pour 48 cases de côté) ; là où elle passe d'une frontière à
     l'autre, les cases d'un troisième mur deviennent la rivière aussi (sinon elle s'arrêtait). Le passage d'une
     rivière est un **pont** d'une case de large, choisi là où la bande va tout droit ; son gardien se tient à côté de son
     bout, sur une case qui garde tout atteignable (`Passage.OtherGates`, essayées dans `PlaceGuards`).
  2. **Les ruisseaux** (`PlaceStreams`, `StreamsPerThousand` 0,6) : une marche au hasard guidée d'une source vers une
     rivière, un lac ou la mer (8 à 30 cases), qui croise une route tout droit (un petit pont) ; rien n'y est posé.
  3. **Le dessin** (shader) : premier tour, des bords par cases (des escaliers) et une rive fine à part, qui jurait avec
     la plage des lacs ; puis `RiverField` (une image de 8 points par case, calculée une fois) : la bande des rivières
     floutée (son bord suit des courbes douces) et la distance aux ruisseaux, dont la ligne passe par les centres des
     cases, arrondie (Chaikin) ; les rivières ont **la plage des lacs et de la mer** ; un ruisseau ne se dessine plus sur
     l'eau où il se jette. Les ponts en planches dessinés par le jeu, en attendant des images (l'utilisateur : pas
     convaincants). La mini-carte, la carte au trésor et le texte du survol (« Rivière », « Pont », « …, ruisseau »).
     Développement : `--center=bridge`, `stream`, `ford`.
  Mesuré (`Rivieres_par_type_de_carte`, six cartes normales) : rivières et ruisseaux 4,6 % de la terre d'un continent,
  6 ponts et 3 ruisseaux par carte ; des mers 3,3 % ; îles 2,4 % (pas de rivière : îles trop petites). Adversaires (six
  semaines, Normal, or d'armée ; avant → après) : continent 54 900 → 63 400, des mers 52 100 → 36 700, îles 32 200 →
  30 900, archipel 31 700 → 22 500 (sans rivière : l'archipel montre combien ces chiffres changent avec les cartes ; les
  mers sont à surveiller). Tests : `RivieresTests`.
- ✅ Les ponts en images (2026-11-15, branche `ponts`, choisis avec l'utilisateur, puis par la session de coordination à
  qui il a laissé les choix) : huit images Retro Diffusion (`rd_tile__tile_object`), **2,64 $ en tout, 11 lots de 4**.
  `BridgeLayer` les pose sur le sol, sous les ombres et les objets, centrées sur les deux cases du pont ; le shader ne
  dessine plus de planches là où un pont a son image (`bridge_art`). **Pierre là où la route du pont ou de ses abords est
  pavée** (Château, Tour, Nains : comme les sortes de routes), bois ailleurs ; sans image d'une sorte, l'autre la remplace.
  Ponts de rivière (96 × 48 et 48 × 96) : bois d'ouest en est, le 3 du premier lot (rambardes des deux côtés, d'une berge
  à l'autre) ; pierre d'ouest en est, au troisième lot (le 1 en ruine, le 2 trop dessin animé, le 3 avec des buissons ;
  partir du bois donnait des rambardes de bois ; parti du 4, raccourci à la longueur du bois, puis réchauffé vers la
  pierre du temple) ; du nord au sud, partis du pont d'ouest en est tourné : bois réussi (le 2, aux couleurs du bois
  d'ouest en est), pierre ratée (une bande étroite : le pont d'ouest en est est vu de côté), puis réussie en partant du
  bois du nord au sud repeint aux couleurs de la pierre (le 3). Petits ponts des ruisseaux (48 × 24 et 24 × 48), partis
  des grands réduits : bois 4 et 4, pierre 1 (d'ouest en est) et 3. Chaque image prend la palette du pont d'ouest en est
  de sa sorte, par rang de luminosité. Développement : `--center=bridge:9` (un pont et une route), `ford`, `ford:2`.
- ✅ La mer, les objets de la mer, seconde partie (2026-11-15, branche `mer-pirates`, choix de l'utilisateur : les pirates,
  une seule créature neutre ; une faction « pirates et monstres de la mer » peut-être plus tard). Le **Pirate** (neutre,
  niveau 3, `"sea": true` dans `creatures.json`, `CreatureType.Sea` : il ne garde que la mer, jamais la terre) : attaque 9,
  défense 6, dégâts 3-5, 22 PV, vitesse 6, 55 or, frappe deux fois ; `Duels_a_prix_egal` (qui compte maintenant les
  neutres) : 77 victoires sur 160 dans son niveau (il bat Fantômes, Orcs, Chevaucheurs de chèvre ; égal contre les
  Archers elfes ; perd contre Griffons, Golems, Minotaures, Limons). Les **gardiens de la mer** : une bande de pirates à
  côté d'un coffre de la mer sur deux, et 1 bande errante pour 1000 cases de haute mer, à 10 cases au moins d'un départ ;
  leur force, celle d'un gardien de la terre au même danger (`SeaDanger` : la distance au départ le plus proche, de 0,2 à
  1 ; les zones de mer n'ont pas de niveau). Ils attaquent un bateau qui passe à côté (`GuardsAround` ne prenait déjà que
  les gardiens de l'eau en mer). Le **navire abandonné** (`AbandonedShip`, 0,7 pour 1000 cases de haute mer) : un
  équipage de morts-vivants (une troupe du niveau du danger, une du niveau en dessous ; une fois et demie un gardien),
  combattu en montant à bord, comme une garnison ; vaincu, de l'or (1500 à 4500) et 2 à 6 d'une ressource rare
  (`AbandonedShipLooted`), puis le navire disparaît. L'IA y va si l'équipage se bat sans trop de pertes. Mesuré
  (adversaires, six semaines, Normal, or d'armée) : continent 46 000, des mers 48 300, îles 31 600, archipel 31 600
  (comme avant). Dessinés par le jeu en attendant leurs images ; `--center=abandoned-ship`, `pirates`. Tests :
  `ObjetsMerTests`.
- ✅ La mer, les objets de la mer, première partie (2026-11-14, branche `objets-mer`, choix de l'utilisateur) : sur l'eau,
  l'**épave flottante** (`Flotsam`, comme Heroes 3 : rien, 5 bois, 5 bois et 200 or, ou 10 bois et 500 or), le **coffre
  de la mer** (`SeaChest` : vide une fois sur cinq, sinon 1000 ou 1500 or ; un artefact plus tard), la **bouée** (+1 moral)
  et les **sirènes** (+1 chance), jusqu'au prochain combat (`BlessingSite` avec `Sea`), le **tourbillon** (`Whirlpool`, par
  paires : le bateau ressort par l'autre, la troupe la plus faible perd un dixième de ses créatures, au moins une, jamais
  la dernière de l'armée ; `WhirlpoolCrossed`) ; sur la côte, le **phare** (`Lighthouse`, un lieu haut qu'on prend en y
  passant : +500 de mouvement par phare à chaque journée commencée en mer). La compétence **Navigation** (Aventure,
  rangée 2) : +50 / 100 / 150 % de mouvement en mer, bateaux 20 / 40 / 60 % moins chers (la baisse du meilleur marin du
  joueur, `GameState.BoatCost`). Le mouvement d'une journée commencée en mer : `Hero.SeaMovement`,
  `GameState.DayMovement` ; les barres de mouvement montrent celui du matin (`Hero.DayMovement`). Le générateur, sur les
  cartes où l'on navigue seulement, pour 1000 cases de haute mer : 4 épaves, 2 coffres, 1 bouée, 1 sirènes, une demi-paire
  de tourbillons (à 20 cases au moins l'un de l'autre), 0,7 phare (hors des zones de départ, à 20 cases l'un de l'autre) ;
  loin de la côte (leurs 8 voisines sont de l'eau) et à 3 cases de tout autre objet. Carte normale : 5 à 7 épaves avec des
  mers, 11 ou 12 sur les îles et l'archipel. L'IA ramasse épaves et coffres, prend les phares, apprend la Navigation en
  dernier. Dessinés par le jeu en attendant leurs images. Développement : `--center=flotsam`, `sea-chest`, `buoy`,
  `mermaids`, `whirlpool`, `lighthouse`. Tests : `ObjetsMerTests`.
- ✅ La mer, les types de cartes (2026-11-14, branche `types-cartes`, choix de l'utilisateur) : une option de la nouvelle
  partie (`MapType`, liste « Carte » à côté de la graine, continent par défaut ; `--map-type=`). Le générateur part des
  zones : avec **des mers**, 20 % des zones (jamais un départ) sont toutes de mer, la terre restant d'un seul tenant ;
  avec les **îles**, 15 % de zones de mer, puis l'île de chaque joueur (sa zone et deux voisines, qui grandissent à tour
  de rôle) et des îles neutres de trois zones ; l'**archipel**, 15 % de zones de mer, une île de deux zones par joueur et
  une zone par île neutre. La mer : les zones de mer, et une **côte** de 1 à 3 cases (1 à 2 dans l'archipel, un bruit
  doux) le long de chaque rivage entre deux îles ou contre une zone de mer, et le long des bords de la carte (îles et
  archipel ; pas près des départs) ; chaque zone ne garde que son plus grand morceau de terre (celui du départ s'il y en
  a un). La côte coupe parfois toutes les places d'un passage : avec des mers, la dernière zone de mer redevient de la
  terre ; sinon le morceau devient une île à part (`IslandsWhole`, on recommence). D'une île à l'autre, une **traversée**
  compte comme un passage pour le niveau des zones (`Crossings`). Une zone sans passage (une petite île) a une route de
  son centre au rivage. Une ville sur deux préfère le bord de la mer (sa case tout autour peut être de l'eau : un port),
  si sa porte mène par la terre au centre de la zone. **Les ports** (`PlaceHarbours`) : sur l'île de chaque joueur, un
  chantier naval de la côte et un bateau amarré à côté, hors de sa zone de départ si l'île en a une autre (on prend la
  mer après la sortie de sa zone, comme on entre dans la zone voisine), que le joueur doit atteindre à pied
  (`_harbourChecks`, vérifié à chaque objet posé) ; un chantier sur une île neutre sur deux (une sur trois dans
  l'archipel) ; avec des mers, un chantier et un bateau au bord de chaque mer, hors des zones de départ. Le générateur
  compte l'eau comme un chemin sur les îles et l'archipel (chaque joueur a son bateau), pas avec des mers (tout s'atteint
  à pied). Le nombre d'objets suit la surface de terre ; les mines et les villes, le nombre de zones de terre. Mesuré
  (cartes normales) : 12 % d'eau sur un continent, 31 à 38 % avec des mers, 55 à 57 % sur les îles et l'archipel ;
  vérifié sur 138 cartes à quatre joueurs (30 graines par type, et 8 grandes cartes). **L'IA** prend les bateaux amarrés ;
  quand elle n'a plus rien à faire à pied et pas de bateau à portée, elle en construit un dans une ville qui a un
  chantier naval, sinon va en acheter un au chantier de la côte le plus proche (`ComputerPlayer.GetBoat`, en gardant
  1000 or pour recruter) ; elle ne bâtit pas encore le chantier naval de ses villes. Mesuré (un adversaire, Normal, six
  semaines, `Adversaires_par_type_de_carte`) : continent 48 200 or d'armée, 2,7 villes ; des mers 44 100, 3,0 villes ;
  îles 29 800, 2,5 villes, 10 embarquements par partie ; archipel 28 900, 3,2 villes, 12 embarquements (moins de terre,
  donc moins de gardiens à battre et d'armée). Les cartes du continent ne changent pas (aucun tirage de plus). Tests :
  `TypesCartesTests`, `MerTests`. Puis, remarques de l'utilisateur : sur les îles et l'archipel, **une ville au plus par
  île** (l'île d'un joueur n'a que son château ; sa ville voisine est sur une île proche) ; les **tours d'observation** à
  15 cases des bords au moins (`TowerEdgeMargin`, le quart de la carte au plus) et à 30 cases l'une de l'autre
  (`TowerSpacing`, une fois et demie ce qu'elles dévoilent) ; une tour sans place n'empêche plus les autres lieux. Les
  cartes du continent changent un peu (les tours, puis les lieux posés après elles).
- ✅ Plus de villes sur les grandes cartes (2026-11-14, branche `plus-de-villes`, demandé par l'utilisateur : une grande
  carte doit pouvoir contenir au moins 8 villes) : le générateur vise une ville pour trois zones, mais s'arrêtait aux 14
  noms de `TownNames`. 26 noms de plus (`MoreTownNames`), mêlés par un hasard à part (d'après la graine) et ajoutés après
  les premiers : les cartes qui avaient assez de noms ne changent pas. Mesuré (deux graines ; seul, puis à quatre) :
  normale 7 et 10 villes (comme avant), grande 17 et 20 (14 avant), très grande 30 et 33 (14 avant). Générer une très
  grande carte prend 7 à 8 s (4 avant : les mines et leurs gardiens près de chaque ville). Test : `TaillesCarteTests`.
- ✅ Les zones de la carte (2026-11-14, branche `carte-zones`, demandé par l'utilisateur) :
  - **Les passages entre les zones**, une option de la nouvelle partie (`ZoneAccess`, liste « Passages » à côté de la
    taille, normaux par défaut ; `--access=`) : ouverts (comme avant), normaux (le gardien de la sortie de la zone de
    départ prend la plus grande taille que **deux fois** l'armée de départ bat en perdant au plus 35 %, par recherche
    dichotomique sur le combat rapide), fermés (quatre fois, et les autres passages deux fois plus gardés). Mesuré (54 ×
    40) : sortie de 132 or (ouverts), 504 or (normaux), 1056 or (fermés) pour une armée de départ de 520 or. Les
    adversaires s'en sortent (passages normaux, six semaines : 48 900 or d'armée, 2,8 villes). Le code garde « ouverts »
    par défaut (tests et mesures inchangés). Test : `La_sortie_de_la_zone_de_depart_demande_l_armee_voulue`
    (`MapGenerator.StartExitGates`).
  - **Une scierie et une mine de minerai près de chaque ville neutre**, chacune avec une petite armée (moitié d'un
    gardien de sa zone, `TownMineGuardFactor`) **posée aussitôt** (posé à la fin avec les autres gardiens, il bouchait
    souvent un passage étroit et était refusé), sur un abord dégagé de préférence (les deux places du gardien et la case
    sous l'entrée restent libres, `_keptFree`) ; à 11 pas de la porte de préférence, 18 au plus. Les villes se posent de
    préférence à 12 cases d'une autre (deux villes serrées au bord de la carte n'avaient plus la place). 160 mines sur
    160 sur 20 cartes. La ville de départ garde ses mines sans gardien. Une ville par zone : c'était déjà la règle.
  - **Les tours d'observation à 10 cases des bords** au moins (`TowerEdgeMargin`, le quart de la carte au plus) ; les
    cases trop près restent aux autres lieux.
  - **Les tailles de Heroes 3** : 36, 72, 108, 144 cases de côté (et 36 × 27 pour les tests) ; jusqu'à 3 adversaires dès
    la normale. Le générateur, 20 s pour 144 × 144, en prend 3,8 (la distance aux villes calculée une fois par ville
    posée, `TownDistances` ; le parcours depuis le départ dans des tableaux, `WalkableGrid`). La carte réduite ne dépasse
    plus 240 pixels de haut (les cartes carrées poussaient « Fin du tour » hors de l'écran). Les tests qui ne parlent pas
    de taille gardent leurs cartes (`GameState(seed, data, 54, 40)`, et 72 × 53 pour l'ancienne grande) ; deux tests
    vérifient l'accès par un seul parcours au lieu d'un chemin par objet (la suite était passée de 39 s à 4 min 44,
    elle fait 36 s). Mesuré : la simulation seule gagne la petite carte vers le jour 62, la normale (72 × 72) vers le
    jour 198 (5 fois sur 6), jamais la grande en 200 jours ; un adversaire après six semaines (Normal, or d'armée) :
    Château 50 000, morts-vivants 47 400, Barbares 51 700, Forêt 57 000, Tour 47 500, Donjon 52 700, Enfer 50 100,
    Nains 49 700 ; Facile 29 100, Normal 48 200, Difficile 73 400.
  - **Les routes se rejoignent** : suivre une route coûte moins, la longer à côté coûte plus (plus d'« échelles »).
  - **La victoire** (choix de l'utilisateur) : « vaincre tous les monstres » retiré (sur la carte normale de 72 × 72, la
    simulation ne gagnait plus qu'au jour 198) ; par défaut, prendre toutes les villes ; les parties longues sont
    acceptées, comme Heroes 3. Une ancienne sauvegarde qui l'avait prend « prendre toutes les villes »
    (`GameRules.Updated`). La simulation gagne en prenant toutes les villes, en 400 jours au plus (200 avant) : petite
    carte vers le jour 15 (elle n'a que deux villes), normale vers le jour 210, grande pas en 400 jours (ses villes
    lointaines sont trop gardées pour un seul héros qui ne voyage pas).
- ✅ Les diagonales sur la carte (2026-11-14, branche `diagonales`) : tout était fait de rectangles (zones posées en
  grille, routes en angles droits, bords des terrains le long des cases). **Le dessin** (`terrain.gdshader`) : la part
  d'un terrain autour d'un pixel mêle les quatre centres de cases voisins (« marching squares », `part_of`) ; la ligne
  passe à 0,5 : un bord droit reste au milieu, un escalier devient une ligne à 45°, un coin s'arrondit ; les ondulations,
  les rangs, les rives, les plages et l'écume suivent (`shore_distance` en découle). **Les routes**
  (`MapGenerator.Road`) : le plus court chemin dans les 8 directions (diagonale 1,41, et un petit coût d'écart à la ligne
  droite) ; un pas en diagonale ne passe pas entre deux coins interdits, et ses deux coins restent de l'herbe sans objet
  (`_roadCorners` ; un monstre peut s'y tenir, comme sur la route) ; une ville posée sur un coin fait de l'autre une route
  (`KeepRoadsAround`) ; les bouts de route et la route coupée par une ville comptent les 8 voisines. **Les zones** : chaque
  case est déplacée par un bruit doux (`SmoothNoise`, 3 cases au plus, méandres de 7) avant de chercher le centre le plus
  proche, sauf près des départs (rien à moins de 9 cases, tout à partir de 15 : avec 6, une zone de départ trop serrée
  éloignait sa mine) ; un morceau de zone détaché rejoint la zone qui l'entoure (`JoinZoneIslands`). Avec 4 cases, les
  zones étroites serraient les objets et leurs gardiens (3 cartes sur 14 sous les trois quarts de mines et coffres
  gardés) ; les gardiens se posent maintenant d'abord autour de l'objet le plus à l'étroit. Corrigé au passage : un
  monstre pouvait en enfermer un autre (`AllObjectsReachable` ne vérifiait pas les monstres) ; deux tests de l'IA
  laissaient les lieux de la carte (un autel proche attirait l'adversaire). Mesuré (22 cartes) : 81 % des mines et des
  coffres gardés (82 % avant). `Mesures` : la simulation seule gagne aux jours 63, 89, 147 (68, 86, 147 avant) ; un
  adversaire après six semaines (Normal, or d'armée, les cartes ont changé) : Château 48 100, morts-vivants 46 000,
  Barbares 49 800, Forêt 68 100, Tour 47 800, Donjon 56 700, Enfer 49 000, Nains 48 000 (environ 15 % de moins, à
  surveiller) ; Facile 32 400, Normal 47 000, Difficile 66 000. Développement : `--zoom=0.4`, toute la carte d'un coup.
- ✅ Le héros du Château sur la carte (2026-11-14, branche `heros-carte`, PixelLab, environ 1,30 $) : **une image par
  faction** (`heroes/hero-{faction}.png`, sinon `heroes/hero.png`), **48 × 48** (choisie en jeu après 48, 56 et 64 ;
  l'ancienne faisait 32), tournée vers la gauche ou la droite (miroir, `Art.MirrorSuffix` ; ses pieds retournés pour
  l'ombre), aux couleurs du joueur (les rouges de l'étendard et de la selle). Le chevalier : d'abord générés à 96 et
  réduits (b-1 : cheval caparaçonné, choisi pour l'allure mais peu lisible), puis de profil (d à g : écartés), puis
  **générés à 48 directement avec b-1 comme style** (« pro », 25 candidats) : choisi **i-21** (« bold simple
  silhouette, few colours, strong contrast, dark outline… »). **La marche** (`hero-chateau-move.png`, 8 images de
  48 × 48, 4 × 2) : 8 essais d'`animer` (a à h ; écartés : PixMiniMax, le trot, 16 images…) ; choisie **d**
  (`--enrichir`), puis **posée au sol** (le cheval flottait de 1 à 2 pixels : on aurait dit qu'il glissait) et un
  **rebond** (le haut du dessin descend d'un pixel pendant deux images, deux fois par cycle : « d3 »). **Le pas** : le
  héros glisse de case en case (il sautait d'une case à l'autre toutes les 0,09 s), **3 cases par seconde**
  (`MapRenderer.HeroStepTime`), avec **un élan par case** (vitesse 1 − 0,1 × cos(2π (p + 0,8)), `HeroStepShare`),
  réglés par l'utilisateur sur une page à curseurs ; un cycle d'images en deux cases. Le pas du héros du joueur est
  montré avant que les règles ne le fassent (`ActorLayer.BeginStep`, `Main.NextStep` : une rencontre arrive quand il est
  sur la case) ; les adversaires rejoués glissent de même. Au combat, l'image de la carte ×2 en attendant la sienne.
  Pieds revus par l'utilisateur.
- ✅ La marche du chevalier dans les 8 directions (2026-11-15, branche `heros-carte-factions`, PixelLab, 0,44 $) : l'image
  du chevalier tournée par PixelLab (`pixellab.py tourner`) en trois lots : « Pro » vue basse, « Pro » vue plus haute,
  v3 (qui garde notre dessin pour sa direction, mais d'autres floues ou mal tournées) ; choisi **la vue plus haute**
  (« high top-down », un chevalier un peu plus mince que le nôtre). Puis deux marches par direction (`animer
  --enrichir`, 8 images), pour le sud, le sud-est, l'est, le nord-est et le nord : choisies sud 1, les autres 2 ; posées
  au sol et le même rebond que la première marche (images 2, 3, 6 et 7). Images `heroes/hero-chateau-{s,se,e,ne,n}.png`
  et `-move.png` ; l'ouest, le nord-ouest et le sud-ouest sont le miroir de l'est, du nord-est et du sud-est. Le héros
  regarde vers son dernier pas (`MapRenderer.HeroFacing`, la direction et le côté gauche ou droit pour une faction qui
  n'a pas encore ses directions) ; avant tout pas, vers le sud-est (comme l'ancienne image). Au combat, l'image du
  sud-est. Pieds des 5 images mesurés (pas encore revus par l'utilisateur ; les images de marche prennent ceux de leur
  image fixe). Voir la marche en jeu : `--hero-walk` (le héros choisi fait sans cesse le tour d'un octogone, montré
  seulement).
- ✅ Le héros des morts-vivants sur la carte (2026-11-15, branche `heros-carte-morts-vivants`, PixelLab, environ 0,50 $) :
  deux idées, 25 essais chacune (« pro » à 48, le sud-est du chevalier comme style) : a, un chevalier noir sur un cheval
  squelette ; b, un nécromancien encapuchonné de violet sur un cheval mort-vivant pâle, au bâton à crâne et à l'étendard
  rouge (la couleur du joueur) ; choisi **b-1** (`heroes/hero-morts-vivants.png`). Tourné en trois lots (deux « Pro »
  vue plus haute, une v3) : les « Pro » sont revenus sur un **fond blanc opaque** (retiré, et désormais par `tourner`
  lui-même) ; choisi le premier (son nord-ouest regardait du mauvais côté, sans importance : le miroir du nord-est le
  remplace ; son est ressemble presque à son sud-est). Marches : sud 1, les autres 2, posées au sol avec le rebond
  (`hero-morts-vivants-{s,se,e,ne,n}.png` et `-move.png`). Pieds mesurés, pas encore revus. Vu en jeu avec `--hero-walk`.
- ✅ Le héros des Barbares sur la carte (2026-11-15, branche `heros-carte-barbares`, PixelLab, environ 0,55 $) : même
  recette ; deux idées (a : un chef au casque à cornes et à la cape de fourrure sur un solide cheval brun, à la hache et
  à l'étendard rouge ; b : le même sur un grand loup de guerre) ; choisi **a-10**. Tourné en trois lots (deux « Pro »
  vue plus haute, sans fond blanc cette fois, et une v3) : choisi le second « Pro » ; marches toutes l'essai 2.
  `hero-barbares.png`, `hero-barbares-{s,se,e,ne,n}.png` et `-move.png`. Pieds mesurés, pas encore revus. Vu en jeu
  avec `--hero-walk`.
- ✅ Le héros de la Forêt sur la carte (2026-11-15, branche `heros-carte-foret`, PixelLab, environ 0,90 $) : deux idées
  (a : un rôdeur encapuchonné à la cape verte et à l'arc long sur un cheval alezan ; b : le même sur un grand cerf) ;
  choisi **a-10**. Deux lots « Pro » tournés (sans v3) : choisi le second. **L'utilisateur veut l'est vu de côté** (dans
  chaque lot, l'est ressemblait au sud-est) : une rotation avec la caméra « side » ne change rien ; 25 essais `pro` vus
  de côté, avec l'est du chevalier comme style et a-10 comme modèle, donnent de vrais profils : choisi le 25. **Et le
  sud-est plus tourné vers le sud** : avec le sud-est du chevalier comme style, les essais restent à son angle ; sans
  style, avec la vue de face, le profil et a-10 comme modèles, ils viennent vers nous : choisi le 2 (un peu plus petit
  que les autres vues ; posé 2 pixels plus haut, ramené au même sol). Une v3 tournée depuis la vue de face : dessin plus
  grand, d'un autre style, écartée. Marches : sud 1, sud-est 1, est 2, nord-est 1, nord 1. `hero-foret.png`,
  `hero-foret-{s,se,e,ne,n}.png` et `-move.png`. Pieds mesurés, pas encore revus. Vu en jeu avec `--hero-walk`.
- ✅ L'est de côté et le sud-est vers le sud pour le nécromancien et le chef barbare (2026-11-15, branche
  `heros-carte-profils`, PixelLab, environ 1,65 $ : dix-huit lots `pro` de 25, une retouche, six marches). **Nécromancien** :
  l'est vu de côté (style l'est du chevalier, choisi le 4) ; le sud-est n'a marché qu'avec **le sud-est du rôdeur comme
  style** (choisi le 8 du lot 6) : sans style, les essais restaient de trois quarts et souvent trop petits (28 à 41 pixels
  au lieu de 45 : l'utilisateur l'a vu) ; avec la vue de face en modèle et « almost a front view », ils regardaient droit
  vers le sud (et revenaient sur un fond blanc). **Chef barbare** : l'est avec le style et le modèle des profils du
  rôdeur et du chevalier (le cheval enfin de profil ; choisi le 1), sa cape trop sombre éclaircie par une retouche
  (« make the fur cloak pale cream white ») ; préciser « a pale cream white fur cloak » dans la description pour les
  essais suivants. Son sud-est : six lots, jamais juste (trois quarts avec le style du rôdeur, plein sud avec la vue de
  face en modèle, entre les deux rien de mieux) ; **l'utilisateur garde l'ancien** pour l'instant. Les essais trop petits
  sont retirés de la page (hauteur mesurée), les fonds blancs nettoyés (bords, puis taches enfermées de blanc pur).
  Marches : nécromancien est 2 et sud-est 2, chef barbare est 2, posées au sol commun (rangée 45) avec le rebond. Pieds
  mesurés, pas encore revus. Vu en jeu avec `--hero-walk`.
- ✅ Le héros de la Tour sur la carte (2026-11-15, branche `heros-carte-tour`, PixelLab, environ 2 $) : deux idées (a : un
  magicien à longue barbe blanche, robe bleue et or au capuchon pointu, bâton au cristal bleu ; b : un mage enturbanné),
  sur un cheval blanc caparaçonné de bleu et d'or ; choisi **a-6**. Sud, nord-est et nord : le lot « Pro » tourné A.
  **L'est** : les essais faits d'après nos propres vues de l'est (chevalier, rôdeur) et d'après a-6 restaient un peu
  tournés vers le sud (l'utilisateur : « always slightly oriented south ») ; seuls les cavaliers des images de combat
  (`creatures/battle/cavalier.png`, `nomades.png`) comme modèles, sans style ni a-6, donnent un vrai profil (choisi le 2
  du lot 4). Ses couleurs plus vives et son drapeau d'une autre forme corrigés par le code : chaque pixel ramené à la
  couleur la plus proche de la palette de a-6, le drapeau et le haut de la hampe de a-6 à la place des siens.
  **Le sud-est** : le style du rôdeur donne l'angle, mais PixelLab dessine le cheval qui vient vers nous à 40-42 pixels
  quels que soient les modèles (six lots ; une toile de 54 n'agrandit pas le dessin) : le 6 du lot 8, agrandi au plus
  proche voisin à 46 pixels (des rangées et colonnes répétées, sans flou), une ligne blanche entre les jambes avant
  retirée. **Le nord-est** : le drapeau flottait vers l'avant ; retourné autour de la hampe (derrière le bâton).
  Marches : sud 1, nord 1, puis (après des remarques : foulées trop lentes à l'est) sud-est 3, est 4 (« walks briskly with
  long quick strides »), nord-est 3 ; posées au sol commun (rangée 45) avec le rebond. Pieds mesurés à la main, pas encore
  revus. Vu en jeu avec `--hero-walk` (l'herbe y paraît bleue sur `main` depuis les rivières, pour toutes les factions :
  signalé à l'utilisateur).
- ✅ Le héros de l'Enfer sur la carte (2026-11-15, branche `heros-carte-enfer`, PixelLab, environ 1,70 $) : deux idées
  (a : un démoniste aux petites cornes, robe noire à capuchon aux runes orange, bâton de feu ; b : au masque de crâne de
  bouc), sur un cheval noir à la crinière de feu ; choisi **a-12**. **Le rouge** : le jeu peint les rouges saturés de la
  couleur du joueur ; le bout des flammes y tombait : tout rouge hors du drapeau passé à l'orange par le code (le drapeau :
  une grande tache rouge en haut de l'image ; une première règle qui demandait aussi un rouge sombre a pris le drapeau plus
  clair d'une marche pour une flamme), et sous la toile du drapeau, dans chaque image des marches, plus aucun rouge (la
  hampe). **L'est** vraiment de côté du premier coup, d'après les cavaliers des images de combat (choisi le 19) ;
  l'utilisateur l'a adoré (« the horse looks like a horse ») et trouvait les autres vues, tirées de a-12, à poney : **toutes
  les vues refaites d'après l'est** (une rotation « Pro » de l'est 19 donne sud, nord-est et nord au grand cheval ; une
  seconde et une v3 ratées). Le sud-est : sans a-12 comme modèle, le nouveau sud et les sud-est déjà choisis en modèles, le
  sud-est du rôdeur en style (choisi le 18 du lot 6), mis à la taille du corps de la vue de face. L'ensemble fait 42 à 43
  pixels de haut ; agrandir tout d'un même facteur (45 ou 46) a été proposé, l'utilisateur le garde tel quel pour l'instant.
  Marches (« walks briskly with long strides ») : sud 1, sud-est 2, est 1, nord-est 1, nord 2 ; posées au sol commun
  (rangée 45) avec le rebond. L'image fixe `hero-enfer.png` est le sud-est (a-12 était l'ancien cheval). Pieds mesurés à la
  main, pas encore revus. Vu en jeu avec `--hero-walk`.
- ✅ Le héros du Donjon sur la carte (2026-11-15, branche `heros-carte-donjon`, PixelLab, environ 1,30 $) : les idées
  dessinées **tout de suite de côté** (l'est), d'après les cavaliers des images de combat : a, un seigneur sorcier en
  armure noire hérissée, cape violette, heaume d'argent à cornes, bâton à l'orbe violet, sur un grand cheval gris sombre ;
  b, le même sur un lézard des cavernes ; choisi **a-16**. Sa rotation « Pro » donne le sud, le nord-est, le nord (et un
  sud-est de trois quarts) au même cheval ; le premier lot avait perdu l'étendard au sud, le second gardé. **Le sud-est
  tourné vers le sud** : deux lots, puis deux autres d'après le sud-est du démoniste : le cheval y a un cou arqué, la tête
  basse (l'utilisateur : « looks like a camel ») ; trois retouches du sud-est du démoniste (on ne change que le cavalier et
  les couleurs) gardent la pose mais raccourcissent les jambes : refusées. **L'utilisateur garde le sud-est de la rotation**
  (trois quarts, le même cheval que les autres vues) pour l'instant, comme le chef barbare. Marches : sud 1, sud-est 1,
  est 2, nord-est 2, nord 1 ; posées au sol commun (rangée 45) avec le rebond. L'image fixe est le sud-est. Pieds mesurés
  à la main, pas encore revus. Vu en jeu avec `--hero-walk`.
- ✅ Le héros des Nains sur la carte (2026-11-15, branche `heros-carte-nains`, PixelLab, environ 0,75 $) : les idées
  dessinées de côté d'emblée : a, un thane nain en armure d'acier et d'or, heaume ailé, longue barbe tressée, marteau aux
  runes bleues, sur un grand bélier des montagnes aux cornes enroulées (comme les chevaucheurs de chèvre) ; b, le même sur
  un ours de guerre caparaçonné ; choisi **a-1** (sa selle rouge passée au brun d'abord, pour que la rotation ne la garde
  pas). Sud, nord-est et nord : la première rotation « Pro » de a-1. Sud-est : le sud-est du rôdeur en style, le nouveau sud
  en modèle, du premier coup tourné vers nous (choisi le 4), agrandi de 44 à 46 pixels (la taille de la vue de face). **La
  hampe** s'arrêtait à l'épaule du nain dans toutes les vues (remarque de l'utilisateur) : prolongée de 9 pixels sous la
  main, par le code, avant les marches (qui la gardent). Marches : sud 2, sud-est 2, est 2, nord-est 2, nord 1 ; posées
  au sol commun (rangée 45) avec le rebond ; les rouges hors du drapeau (barbe, selle) passés au brun dans chaque image.
  L'image fixe est le sud-est. Pieds mesurés à la main, pas encore revus. Vu en jeu avec `--hero-walk`.
- ✅ Ville des Nains sur la carte (2026-11-12, branche `creatures-nains`, PixelLab, 0,64 $) : trois idées en lots de quatre
  (a : hall de la montagne, b : forteresse au pied d'une colline aux toits de cuivre, c : cité des forges, engrenages et
  tuyaux de bronze), le Château comme style et modèle (a : le Donjon comme second modèle, pour la forteresse creusée dans
  la roche) ; choisie **a-1**. Trois retouches essayées (montagne plus sombre, forteresse plus sombre, les deux ; bannières
  rouges et bleues, cuivre, runes d'or) et vues en jeu : l'utilisateur garde a-1 telle quelle (luminosité 98). Pieds
  revus par l'utilisateur (deux segments, opacité 0,8).
- ✅ Ville de l'Enfer sur la carte (2026-11-11, branche `ville-enfer`, PixelLab, 1,31 $) : trois idées (a : citadelle noire,
  b : cité du portail, c : temple de Moloch) ; choisie a-2, trop sombre (luminosité 32, contours durs) : les deux
  retouches de la méthode (ombrage doux, puis détails) l'ont rendue lisible (f-1, luminosité 51, gardée telle quelle).
  Pieds revus par l'utilisateur.
- ✅ Villes du Donjon, de la Tour et des Barbares (2026-11-11, branche `villes-carte`, PixelLab, 1,93 $) : le Donjon en trois
  idées (a : porte de l'abîme, b : citadelle du gouffre, c : tour de l'œil) ; choisie a-2 (forteresse creusée dans la
  falaise), retouchée pour la lumière rose et violette du gouffre (d-1), puis pour l'ombrage doux du Château (h-1 : a-4,
  qui recopiait le Château, montrait ce dessin), puis pour des détails sur les murs (j-1), assombrie de 71 à 60. Les
  mêmes deux retouches (ombrage, détails) sur la Tour (puis luminosité 125) et les Barbares (70). Redessiner d-1 avec a-4
  comme image de style (série i) n'a rien donné. Pieds du Donjon revus par l'utilisateur.
- ✅ Ville de la Tour sur la carte (2026-11-11, branche `ville-tour`, PixelLab « Pro » 160 × 128 et une retouche, 2,38 $) :
  trois idées en lots de quatre (a : tour de mages, b : cité de flèches, c : citadelle de géants), puis c-2 aux murs
  ronds (d, e), puis la série a plus lisible (f, g : ombrage et trait du Château demandés) ; choix **g-2**, son toit bleu
  changé en dôme d'or en bulbe (`retoucher`, h-1 ; l'utilisateur aime l'air oriental, accordé aux Naga et Djinn).
  Luminosité ramenée de 156 à 125, celle du Château (`tools/art/luminosite.py`, nouvel outil) ; pieds revus par
  l'utilisateur (trois segments, opacité 0,9).
- ✅ Champ de bataille du marais (2026-11-16, branche `champ-marais`). Choix de l'utilisateur : **les deux à la fois**, un
  bayou à la lisière (cyprès à mousse espagnole, arbres morts, saules pleureurs, grands troncs, buissons) et une tourbière
  au paysage ouvert (bouleaux, petits arbres lointains) ; **des flaques où l'on marche** (pas des obstacles). Palette
  `SwampPalette` (ciel couvert gris-vert, collines basses bleu-gris, pré de la couleur du sol mesurée en jeu,
  0,40 / 0,32 / 0,20). Flaques (`BuildPuddles`, drapeau `Puddles`) : une dizaine, de 1 à 3 ronds aplatis au bord déformé
  par un bruit doux, une rive de boue mouillée en paliers de 25 et 50 %, quelques taches de boue sans eau ; l'eau olive
  (bleutée, elle jurait avec la boue brune), d'après la distance à la rive du haut : la rive reflétée (sombre), une
  **bande de ciel étroite** qui se défait en traits (large et pâle, elle faisait des rochers gris), puis l'eau trouble ;
  des rides, des nénuphars, des joncs sur les bords et sur la rive du haut avec leur reflet renversé. Pas de terre battue.
  **Moins de taches vertes** sur le sol (`SmoothGround.FewerSpecks`, au combat seulement) : l'image du marais a des
  touffes vertes qui se répétaient trop sur un grand champ ; les petites (4 pixels au plus) restent, les grandes
  (6 à 28 pixels) prennent la boue d'à côté (essayé : en enlever deux sur trois, puis garder une grande sur trois : trop).
  Paysage ouvert : bouleaux fondus dans une brume plus sombre (leurs feuilles presque blanches restaient claires avec
  la brume ordinaire), des mares lointaines dans le pré, à moitié de la couleur des collines (plus pâles, trop voyantes).
  Images Retro Diffusion (5,04 $ : 16 sujets puis les saules, puis des arbres obstacles plus gros à 64 × 80, comme le
  chêne de l'herbe, demandés « plus gros ou plus épais » par l'utilisateur) ; deux retouches à la main : un arbre au
  tronc gris (pris pour une ombre par `sans_socle.py` : seule l'herbe retirée) et un saule posé sur un anneau d'eau
  bleu-vert. **Le raccord du pré et du sol** des paysages ouverts (herbe, désert, marais) : une ligne nette coupait la
  scène en deux (remarque de l'utilisateur) ; le pré se fond maintenant sur 8 rangées, en paliers mêlés d'un pixel à
  l'autre, depuis un bord qui ondule, et la brume commence au pied des collines et culmine sur le raccord (elle
  commençait juste au raccord et doublait la ligne). **Les obstacles plutôt au milieu** (règle, tous terrains) :
  colonnes 3 à 7 seulement (jamais la première devant chaque camp), la colonne du milieu le plus souvent (1, 2, 3, 2, 1
  sur 9), les rangées du milieu un peu plus souvent ; test `Les_obstacles_sont_plutot_au_milieu_du_champ`. Les tirages
  du combat changent : deux tests revus (le prix du sang du Démoniste compté avec le vol du Diablotin ; l'adversaire
  sans ville n'attaque qu'avec au plus 10 % de pertes prévues : 200 paysans contre 40 au lieu de 60, qui était à la
  limite).
- ✅ Champ de bataille de la neige (2026-11-19, branche `champs-sols`). Choix de l'utilisateur : **une neige épaisse**.
  `SnowPalette` (ciel pâle et froid, montagnes blanc bleuté, collines enneigées, pré de la couleur du sol de neige, brume
  bleutée) ; pas de terre battue ; peu de détails sur le sol (16). Images Retro Diffusion (3,84 $, 16 sujets) : sapins et
  sapin pectiné ployant sous la neige, arbres morts enneigés, grands troncs, buissons ensevelis, jeunes sapins et bouleaux,
  petits sapins lointains, congères et rochers presque ensevelis sur les bords ; obstacles : sapins ensevelis, rochers
  coiffés de neige, blocs de glace bleue, troncs couchés sous la neige ; détails : mottes de neige, touffes gelées. Les
  rochers, la glace et les troncs gardent leur congère (la neige épaisse). **La neige sous les arbres** : retirée, ils
  paraissaient posés ; gardée, elle faisait une tache et une ombre peinte. Retenu, d'après sept arbres retouchés par
  l'utilisateur : tout ce qui, sous l'arbre, n'est ni aiguilles ni écorce prend la couleur de la neige du champ, à 65 %
  au pied puis 38, 30, 12 et 8 % en s'éloignant ; leur ombre de contact est bleue et plus légère (`pieds.json`), posée au
  bas du tronc.
- ✅ Champ de bataille de la roche (2026-11-20, branche `champ-roche`). Choix de l'utilisateur : **une terre désolée,
  entre le désert et la terre brûlée**. Essayés et écartés : la roche de la carte vue de près (des dalles et des
  fissures : « du cuir », puis « un pavage »), des affleurements dessinés par le shader, des roches plates générées
  (« pas des formes naturelles » : 7 gardées sur 15, des éboulis pris dans le sol, fondus dans le sol par un
  script). Retenu : `SmoothGround.CreateBattle` met presque tout en terre (`rock_outcrops` 0,02) ; dans
  `terrain.gdshader`, une terre gris-brun (`rock_soil`) avec de grandes plaques en trois paliers (`rock_patches`),
  des ravins secs de gravier pâle (`rock_washes`), du gravier et des pierres plus nombreux dans les zones
  caillouteuses et les ravins, des fissures sèches fines et grandes (le générateur des fissures de la terre brûlée,
  `vein_at`, sans feu) ; `rock_shift`, tiré du combat, décale ravins et fissures d'un combat à l'autre. Ciel plus pâle
  et voilé, pré plus gris (`RockPalette`). Images Retro Diffusion (arbres, pins, buissons, rochers, obstacles) ; les
  plantes vertes séchées par un script (gris-jaune, l'ombrage gardé), les touffes et brindilles copiées de l'herbe et
  séchées ; les pins restent verts. Les combats sur la roche n'empruntent plus le champ du désert (la terre brûlée,
  encore).
- ✅ Finitions des champs (2026-11-17, branche `champ-finitions`). **Nuages** dans le ciel des paysages ouverts : 1 à 3,
  plats dessous, des bosses bombées dessus (gardées dans le nuage : une bosse coupée au bout faisait un mur) ; gris sous
  le ciel couvert du marais (drapeau `Overcast` : le haut blanc y brillait trop, remarque de l'utilisateur). **Rochers**
  sur les collines proches (pas sur les dunes) : de petits dômes gris éclairés en haut à droite. Nuages et rochers ont
  leur propre tirage : les collines d'un combat restent les mêmes. **Pas de flaque sous un obstacle** du marais : la
  place est tirée à nouveau (12 essais), sinon pas de flaque. « De vrais obstacles de combat » : déjà faits avec les
  trois champs (herbe 7 images, désert 8, marais 11 ; la forêt et la montagne prennent ceux de l'herbe).
- ✅ Champ de bataille du désert (2026-11-11, branche `champ-sable`). Une palette par terrain pour le paysage
  (`ScenePalette` : ciel, chaînes, pré, brumes, ombre ; `FlatTops` pour des mesas, `Dunes` pour des dunes une à une, face
  raide à l'ombre au bord en biais et incurvé), `--battle-terrain=sand`. Écartés : les cactus (un désert américain), les
  petits cactus verts, une tuile de sol caillouteux (Retro Diffusion : du bruit), un pavage de dalles même en relief, un
  gravier serré. Retenu (`BuildStones`, d'après `art-candidats/references/sol-desert/`, dont le « Desert Ground Pack »
  d'Alexet) : des marques légères sur le sable, en 8 à 13 taches irrégulières (2 à 4 ronds qui se chevauchent, bord
  déformé par un bruit doux), deux teintes par tache mêlées par taches douces, rides de vent, traînées de points, fissures
  d'un pixel, touffes d'herbe sèche, cailloux de couleurs mêlées, en paliers de transparence. Dunes en relief sur le champ
  (`BuildDunes`). Sans terre battue ; ombres du sol à 0,05. Images (Retro Diffusion, 4,08 $) : grès, grosses souches et
  troncs (64 px, blanchis à moitié par `tools/art/blanchir.py`, réduits à 80 %), arbustes et buissons épineux, palmiers,
  acacias, petits palmiers lointains, herbe sèche, cailloux, un crâne ; socles retirés par `sans_socle.py --sable` ou
  `--buisson` ; pieds revus par l'utilisateur.
- ✅ Champ de bataille de l'herbe (2026-11-11, branche `champ-de-bataille`). Écarté d'abord : un horizon peint vu de côté
  (Retro Diffusion `rd_plus__environment` 384 × 96, prolongé par symétrie) : la perspective et le style juraient (arbres à
  la taille des créatures, collines lisses). Les captures de Heroes 1 à 3 (`art-candidats/references/screenshots/`) ont
  montré deux familles : la lisière d'une forêt (troncs, feuillage coupé par le cadre) et le paysage ouvert (une bande de
  petites collines lointaines et de ciel) ; un fond doux et peu contrasté pour que les créatures ressortent ; des bords
  de décor à gauche et à droite. Fait, dans `BattleFieldView` : `SmoothGround.CreateBattle` (le shader de la carte, taches
  de terre battue : le terrain libre 1 avec l'image de la route ; `shade` 0,13, plus de fleurs et de cailloux),
  `BuildEdge` (trois rangées tirées au hasard par combat, de plus en plus brumeuses vers le fond : `Hazy` ; grands troncs
  `trunk-grass` dont le haut sort du champ, buissons `bush-grass` devant, bords `side-grass`), `BuildLandscape` (ciel en
  bandes tramées, deux chaînes de collines de sinus irréguliers, ombrées sous la crête et mouchetées, petits arbres vus de
  côté `far-grass`, pré tramé vers le sol), arbres moyens `mid-grass`, `Bands` (ombre de la forêt, brume vers l'horizon :
  bandes aux bords ondulés ; le tramage en points rendait l'image granuleuse), détails `detail-grass` (44 par champ),
  obstacles `obstacle-grass` (variante choisie par la case). `--battle-scene=open|forest`. Images : Retro Diffusion
  `rd_tile__tile_object` (4,92 $ en tout), socle retiré par `tools/art/sans_socle.py` ; ombres de contact de `ShadowLayer`
  (pieds dans `pieds.json`, revus par l'utilisateur ; opacité et couleur par image ajoutées à `pieds.py` et à
  `ShadowLayer`, opacité 0,8 au combat).
- ✅ Effets du combat (2026-11-07), dessinés par le jeu (`BattleEffects`, sans image, en pixels d'art) : projectiles qui
  volent jusqu'à leur cible (le coup porte à l'arrivée), un impact à chaque coup, recul de la troupe touchée, nuage de
  mort et boule de poison de la Liche, hache de l'Orc, flux de sang du Vampire, poison (bulles, troupe qui verdit),
  régénération du Troll, âmes du Fantôme, auras des sorts. Réglage de la vitesse des projectiles : `ProjectileSpeed`
  (le bouton de l'écran de combat est fait par l'autre session).
- ✅ Herbe et rives (2026-11-09, branche `sol-herbe-rivages`) : nouvelles images d'herbe (Retro Diffusion, 0,48 $ ;
  `terrain/grass@v3` n° 4 et `grass-fleurs@v3` n° 1), un vert plus profond et naturel au lieu du vert citron. Les fleurs
  demandées à Retro Diffusion n'apparaissaient pas, et une case fleurie répétait ses fleurs : elles sont semées par le
  shader (`flower_at` : l'herbe découpée en carrés de 9 pixels, une fleur blanche, jaune ou violette ou un caillou dans
  un carré sur dix). Plages : une bande de sable côté terre le long de l'eau (`shore_distance` : la même rive ondulée que
  le mélange des terrains ; sable mouillé plus sombre sur 2 pixels), 8 pixels d'art (choix de l'utilisateur, « wide »).
  Réglages : `beach_width`, `beach_wave`, `wet`, `wet_width`, `flower_density`, `flower_cell`, `pebble_share`.
- ✅ Ville barbare sur la carte (2026-11-09, branche `ville-barbare`, PixelLab « Pro » 160 × 128, 0,46 $) : choix de
  l'utilisateur, une **forteresse de pierre brute** (murs de rochers, tours rondes, donjon au toit de chaume, huttes, porte
  au milieu du bas, crânes, bannières rouges) avec une **grande tente de peau** au milieu des huttes. Le premier lot était
  trop « croustillant » : le second demande des formes simples et un ombrage doux, avec la ville du Château comme image
  de style (au lieu de la mine d'or) et le candidat aimé comme modèle. Le contour noir trop épais est adouci
  (`tools/art/adoucir.py`, assombrir 0,7, seuil 60, 2 passes), et la ville des morts-vivants de même.
- ✅ Ville de la Forêt sur la carte (2026-11-09, branche `ville-foret`, PixelLab « Pro » 160 × 128, 0,35 $) : trois idées
  montrées à l'utilisateur, un lot de quatre chacune (la ville barbare comme style, la ville du Château comme modèle de
  disposition) : un grand arbre aux maisons perchées, un cercle de druides, une citadelle elfe. Choix : **a-2**, un grand
  chêne aux maisons et ponts de corde dans ses branches, au milieu d'une palissade ronde, la porte en bas au milieu.
  Contour adouci (`adoucir.py` 0,7, 60, 2 passes) ; pieds proposés (trois, à vérifier). Le cercle et la citadelle
  remplissaient tout le rectangle (arbres coupés aux bords) : une scène, pas un objet détouré.
  Revue (2026-11-10) : vues côte à côte sur l'herbe, la ville barbare et l'arbre paraissaient bien plus cernés de noir
  que le Château. Adoucir les traits après coup (moyenne des voisins : « mousseux », moins de contraste ; amincir les
  traits épais : trop discret) ne suffit pas : le poids vient aussi des couleurs sombres du dessin. Regénérés avec le
  Château comme style (« thin light outlines, no thick black outlines, bright warm soft colours ») et la ville actuelle
  comme modèle (0,23 $) : l'utilisateur garde la ville barbare actuelle et prend la nouvelle Forêt (candidat 1, plus
  claire, traits plus fins).
- ✅ Créatures de la Forêt (2026-11-09, branche `creatures-foret`, PixelLab, environ 6 $ avec les essais), une à la fois,
  choisies en combat (`essai-<k>.png` posés dans le jeu avec `--creature-preview`) et sur la page d'aperçu des
  animations (à la taille du jeu, un adversaire devant les attaques) :
  - **Farfadet** (45 px, L1-1) : ailes de feuilles d'automne ; « contour » 0,7/70/2 et « bord » à 60 % d'opacité (traits
    d'un pixel pleins), choisis en combat ; idle-2, vol move-2 (0,25 s par case), attaque attack-4-r (plongée et griffes ;
    le mot « enemy » faisait dessiner une autre créature : consigne sans lui). Carte p2-24.
  - **Sanglier** (46 px après ×0,85, L2-1) : idle-2-r, trot move-2, charge attack-2-r (trous de l'éclat comblés :
    `tools/art/combler.py`). Carte p2-24.
  - **Archer elfe** (74 px, L2-4) : corde de l'arc non tracée (elle lui barrait le visage) ; attente idle-5-sc (prend une
    flèche, la regarde, la remet), marche move-3-sc, tir attack-2-r (flèche lancée à l'image 6, depuis [80, 51]). Carte p1-5.
  - **Cerf** (84 px, L4-1) : attente idle-2 ralentie (4 images/s), marche move-4 (« pas de saut » : les premiers essais
    galopaient), coup de bois de bas en haut attack-4-r (arcs orange et blancs effacés). Carte p1-15.
  - **Druide** (72 px, L10-3) : trapu (remarque de l'utilisateur), d'après ses références `druid1.png` (Heroes 2 : capuche
    pointue, robe brune, ceinture rouge) et `druid2.avif` (chouette sur le bâton ; convertie en PNG par Windows, réduite à
    256 px). L10-3 regardait déjà à droite : le « miroir » posé par erreur a été retiré, les feuilles d'animation
    retournées (gratuit). idle-2-r (se caresse la barbe), move-2, attack-2-r (la main tendue). Nouveau projectile du jeu,
    le **tourbillon de feuilles** (`feuilles` : sept feuilles qui tournent, une traînée qui tombe, un bord sombre pour se
    voir sur l'herbe ; lancé à l'image 5 depuis la main [108, 46]). Carte p1-3.
  - **Tréant** (100 px, L10-1) : d'abord trop humain (L1–L4), refait « un arbre qui marche, pas un homme de bois » (L5–L8) ;
    L5-1 avait la couronne coupée en haut (cadre de génération trop petit, vu par l'utilisateur) : regénéré à 144 × 160
    avec L5-1 en référence. idle-6, marche move-6, attaque attack-4-r (bras-branche levé puis abattu). Carte p1-6.
  - Leçons : ne jamais lancer deux commandes qui écrivent `creatures-art.json` en même temps (l'une efface les choix de
    l'autre) ; vérifier que la grande image ne touche pas son cadre ; pour la carte, 16 candidats suffisent.
- ✅ Créatures de la Tour (2026-11-10 et 11, branche `creatures-tour`, PixelLab, environ 10 $ avec les essais), une à la
  fois, d'après les références de l'utilisateur (sauf le Géant), deux ou trois styles essayés par créature :
  - **Gnome** (40 px, L12-1) : mage chauve sans bonnet, robe bleue ; idle-1, move-2, attack-2-r (le bâton manquant d'une
    image recopié de la voisine). Carte p1-5.
  - **Gargouille** (50 px, L8-1, volante) : pierre éveillée ; pas d'attente (elle reste immobile au sol) ; envol
    transform-1 d'après une pose en vol retouchée par PixelLab, vol move-4-gris (lueurs bleues rendues grises), attaque
    attack-13-gris (le bond et le coup des deux pieds : **à retravailler**). Carte p1-14.
  - **Golem de pierre** (56 px, L8-1 ; 84 px le faisait trop fort pour un niveau 3) : sans tête, le visage taillé dans la
    poitrine ; attente idle-9 (ramasse un caillou et le mange), marche move-9, attaque attack-8-gris (coup de poing en
    avant). La charge du torse (attack-23-gris) est gardée pour plus tard : « ram » fait toujours dessiner une explosion.
    Carte p1-11.
  - **Naga** (66 px, L2-2) : bleue, six bras armés de cimeterres, queue de serpent ; idle-2, move-5 (la queue ondule ; avec
    `--enrichir`), attaque attack-7-sb (8 images : à cette largeur, un des modèles refuse 16 images ; l'arc de lame bleu
    vif effacé par sa couleur). Carte p2-5 : la première série avait des lames blanches qui faisaient un fond blanc à
    32 px, refaite avec « dark grey steel blades ».
  - **Djinn** (72 px, L2-1, volant) : peau bleu-violet, turban vert, barbe rousse, queue de fumée d'or ; idle-2, vol
    move-2 (0,3 s par case), attaque attack-2-r (fond en avant, coup de poing). Carte p1-4.
  - **Mage** (76 px, L3-1) : brun, sans chapeau ni bâton, grandes manches ; idle-2, marche move-5 (14 images/s ; ses bras
    ne bougent pas : sa main levée sur l'image fixe reste figée dans tous les essais), sort attack-5-r (les deux mains au
    ciel puis en avant). Son tir : l'**éclair** du jeu (`eclair`, inutilisé jusque-là), bleu, brisé, avec de petites
    branches, et un éclair plus fin de chaque main (`launchPoint2`) qui rejoint le grand devant lui. Carte p1-6.
  - **Géant** (105 px, L11-4) : le style grec (peau bleue, barbe, tunique, colonne) choisi, mais d'abord trop musclé
    (« stéréotypé ») : refait mince et digne avec le candidat L9-2 en référence. Attente move-1 (une marche ratée où il ne
    bouge pas les pieds), transform-1 (pose la colonne sur l'épaule ; jouée à l'envers à l'arrivée), marche move-5 (depuis
    la colonne sur l'épaule), attaque attack-5-r (frappe le sol). Carte p1-12.
  - Outils : « marge_bas » dans la fiche (un pied qui descend en marchant n'est plus coupé), `--effect-preview=eclair`.
- ✅ Créatures du Donjon (2026-11-11 à 15, branche `creatures-donjon`, PixelLab), une à la fois, d'après les références
  à l'encre de l'utilisateur (couleurs proposées par Claude, deux styles par créature) :
  - **Homme-lézard** (46 px, L6-1, lézard des cavernes à la lance) : idle-3, move-5-r, attack-2-r. Carte p1-7.
  - **Homme-grenouille** (48 px, L4-3) : idle-5, move-1, attack-5 ; sa pierre de fronde dessinée par le jeu. Carte p1-1.
  - **Minotaure** (84 px, L5-4, mineur noir) : idle-2, move-1, attack-2-r. Carte p1-4.
  - **Aberration** (80 px, L3-2, chair pâle, retournée vers la droite) : idle-5, move-5, attack-5 nettoyée. Carte p1-6.
  - **Ver des profondeurs** (90 px, L6-1) : idle-2, attaque attack-12-c ; il **creuse** : la transformation
    (`transform-terre`, refaite à la main) le montre s'enfoncer derrière ses gravats qui s'émiettent ; le jeu dessine le
    tas de terre au départ et à l'arrivée et la **galerie** (`BattleEffects.DrawTrail` : une carte de hauteurs faite de
    mottes, vue un peu d'en haut, dessus éclairé selon la pente, flanc à l'ombre, pierres, racines, herbe arrachée, miettes ;
    elle s'affaisse en s'effaçant). Essayés et écartés : des buttes soulevées en chemin, un bourrelet continu vu d'en haut
    (« liquide »), la galerie vue de côté. Images d'attaque et de transformation descendues pour que la terre touche le bas
    de la case. Carte p1-3. **Plus tard** : glisser vers sa cible en attaquant, de meilleures fissures, peut-être avaler.
  - **Tyran œil** (64 px, L2-1, peau marbrée violet-brun) : il **flotte** (« flotte » : 14 pixels vides sous lui) ;
    attente calme-2, vol vent-4 (ses tentacules ondulent ; 6 images/s, 0,18 s par case ; attente 7 images/s, pause de 1 à
    3 s), attaque regard-3 (son œil s'ouvre tout grand). Son tir : le **rayon** du jeu (`rayon`, violet, droit, plus épais,
    une lueur sur l'œil ; `launchFrame` 4, `launchPoint` [86, 61]). Animé directement par `pixellab.py animer` (le
    « sujet » et les consignes de `creature.py animer` faisaient venir éclats et autres personnages). Carte p1-13, sans
    pied (il flotte).
  - **Hydre** (100 px, L3-4, écailles vert sombre, ventre jaune pâle, cinq têtes de dragon) : attente mm-calme-3, marche
    mm-marche-1 (ralentie à 6 images/s : à 10, elle courait), attaque mm-proche-6 (les têtes mordent tout autour d'elle sans
    s'éloigner). Le premier modèle ne bougeait pas ses pattes et couvrait les attaques d'éclairs ; le second (`--minimax`)
    fait un vrai pas. Carte p1-7.
  - Au passage, le **Géant** (Tour) retourné vers la droite (image fixe et feuilles, image par image) : il marchait à
    reculons. Pieds des sept petites images revus par l'utilisateur (le Ver plus léger, aucun sous le Tyran œil).
- ✅ Créatures de l'Enfer (2026-11-15, branche `creatures-enfer`, PixelLab), une à la fois ; à chaque créature, une **vue
  d'ensemble** de sa faction et de son niveau, animations jouées (`tools/art/apercu_faction.py`, demandée par
  l'utilisateur pour juger le style) :
  - **Succube** (84 px, L7-1, ailes noires, robe rouge vif) : attente queue-2, marche mm-pas-3, attaque mm-baiser-1 ; son
    tir, le **baiser** de feu dessiné par le jeu (un cœur rouge, une lueur qui palpite, des braises). Carte p1-6.
  - **Démon** (132 px, L10-1, gras et rouge, barbe noire, ailes orange, trident ; fond blanc retiré, gardé à sa taille
    d'origine : plus net) : attente mm-rot-3 (il se frotte le ventre et rote), transformation ailes-1-envol (il
    s'enveloppe de ses ailes, s'élance et redescend : la **téléportation**, déplacement invisible, `stepTime` 0,03 ; la
    règle viendra de la session règles), attaque crache-2-r : il **crache le feu**, le feu de PixelLab retiré, le jeu
    dessine le jet (`meleeProjectile` « flammes », `SpellEffects.FireJet` : les tons et la boule de feu de la Boule de feu ;
    un premier jet fait de trois couches unies ne ressemblait pas à du feu). L'estoc au trident (mm-estoc-1) est remplacé.
    Carte p1-5.
  - **Diablotin** (44 px, L6-1, peau noire de suie) : attente lecher-8 (il se lèche la main comme un chien, ailes et
    queue qui bougent), marche sauts-1 (par bonds), attaque bond-2 (bondit sur l'ennemi et revient) ; il vole à 0,3 s par
    case (le vol par défaut, 0,09 s, était trop rapide). Carte p1-1.
  - **Cultistes** (68 px, L1-3, robe rouge à capuche, dague rituelle) : priere-1, marche-3, coup-1 sans sa traînée.
    Carte p1-2.
  - **Limon de magma** (50 px, L1-1, masse de lave) : il bouillonne **sans pause** (bouillon-1, `idlePauseMax` 0 : un
    fluide ne s'arrête jamais), rampe en **32 images** à 20 images/s (rampe32-2 : en 16 images, il glissait ou sautait),
    s'écrase sur l'ennemi (ecrase-1). Carte p1-2.
  - **Tourmenteur** (100 px, L2-1, wendigo au crâne de cerf) : attente guette-1, marche marche-1, attaque fauche-2-r
    (griffe-3 écartée : le crochet remontait d'un coup), et le **crochet lancé** (état `throw`, lancer-1) : le jeu dessine
    la chaîne avec **ses propres pixels** (un maillon et le crochet découpés de l'image, `-chain.png` et `-hook.png`,
    `BattleEffects.DrawChain`), depuis la main à chaque image (`throwPoints`), sans trou entre les maillons ; le crochet
    penché de 45°. Carte p1-1.
  - **Moloch** (112 px, L8-1, idole de bronze à tête de taureau, fournaise dans le ventre : une statue animée, pas un être
    vivant) : attente fournaise-2, marche pas-2, attaque poings-3 (ses poings rougissent et s'abattent). Carte p1-6.
  - **Icône du bouton « Invoquer »** (`ui/icons/invoquer.png`, n° 2 : un diablotin qui sort d'un portail).
- ✅ Créatures des Nains (2026-11-16, branche `creatures-nains-images`, PixelLab). Choix de l'utilisateur : **des nains
  caricaturés, chacun le sien, loin des clichés** (« ils se ressemblent tous, tellement stéréotypés ! ») ; un premier lot
  d'après le Porte-bouclier, le héros nain et un croquis de l'utilisateur donnait sept fois la même barbe et le même cuir ;
  le second, des caricatures de figurines (« grotesque caricature… exaggerated features »), le seul Porte-bouclier comme
  modèle de rendu, a plu (« va encore plus loin »).
  - **Porte-bouclier** (L7-4, barbe noire, cuir, bouclier rond de bois ; moins d'armure : un niveau 1) : garde-1,
    mmarche-1, mcoup-2 (les trous du bouclier bouchés). Carte p1-3.
  - **Arbalétrier** (L6-3, chauve, ventru, gros nez rouge, monocle, manteau vert) : garde-1, mmarche-1, mtir-1 (le
    carreau dessiné par PixelLab effacé ; le jeu tire la flèche de l'Archer depuis le bout de l'arbalète, image 5). Carte p1-2.
  - **Chevaucheur de chèvre** (L10-2, nain sauvage aux cheveux blancs et peinture bleue, chèvre pie hirsute ; les
    premières images galopaient : refaites **immobiles**, d'après L8-1) : mgarde-2, mmarche-1, mcoup-1 ; un trot vif
    (0,45 s par case). Carte p1-2.
  - **Porte-étendard** (L8-3, vieux nain voûté encapuchonné, tête d'ancêtre de pierre au bout de la hampe) : mgarde-2,
    mmarche-1 (les yeux bleus de la tête, effacés par le nettoyage du cyan, remis), attaque attack-2 refaite **à deux
    mains** (l'éclair de l'impact effacé, la tête de pierre réparée). Carte p1-2.
  - **Gyrocoptère** : un **nain aux ailes de cuir, de bois et de fer** (idée de l'utilisateur, après des tonneaux et une
    baignoire volante), L13-4, bandé de partout, casque-casserole, barbe orange. Attente garde-1, **envol** (transformation
    menvol-1) puis vol move-4 depuis sa dernière image, lancer de bombe attack-2 (la bombe qui part effacée). Le jeu
    lance la **bombe** (`meleeProjectile` « bombe », `SpellEffects.DrawBomb` : fonte noire, mèche qui crépite, en cloche)
    qui **explose** sur la cible et les cases autour (l'explosion de la Boule de feu) ; les troupes voisines touchées par
    l'explosion le sont sans nouveau geste (`UnitStruck.Splash`). Carte p1-2.
  - **Sapeur** (remplace le Lance-flammes, choix de l'utilisateur ; L3-2, mineur voûté au gros nez pâle, bougie au
    casque) : garde-2, mmarche-2, mcoup-1. Carte p1-6.
  - **Roi de cristal** (remplace la Machine de guerre ; L5-1, un **esprit pâle** demandé par l'utilisateur, cristal et
    brume, 136 px) : mgarde-1, mmarche-2, mcoup-2 (la traînée du marteau retirée). Le nettoyage effaçait son cristal
    cyan : `garder_cyan` dans la fiche. Carte p1-1.
  - Les pieds des 7 images de la carte revus par l'utilisateur.
- ✅ Combat à ×2 (2026-11-09, branche `combat-x2`). Question de l'utilisateur : un pixel d'art a-t-il la même taille au
  combat et sur la carte ? Non : 3 pixels de l'écran au combat, 2 sur la carte (zoom ×1 ; 3 au zoom ×1,5). Essai montré
  (page `echelle.html` : même combat à ×3 et à ×2, créatures 1,5 fois plus hautes) ; choix de l'utilisateur : ×2 partout
  (« much much better »). Fait :
  - `BattleSpriteScale` et `GroundScale` à 2 ; sol et obstacles du combat = images de la carte à ×2 (les obstacles,
    sommets et sapins de 32 px, sont plus petits qu'avant : de vrais obstacles de combat viendront plus tard).
  - Créatures refaites 1,5 fois plus hautes, sans dépense : image fixe depuis la grande image (fiche « grande ») ;
    chaque image des feuilles installées retrouvée parmi les images brutes de PixelLab des archives (la plus
    ressemblante, réduite à l'ancienne taille, droite ou retournée ; anciennes dispositions des dossiers comprises :
    `<id>/anim/<nom>/`, `<id>-anim/<nom>/`, `pixellab/<id>-<état>/`), puis refaite à la nouvelle taille avec le même
    nettoyage. 20 créatures : les 17 à fiche, plus le Gobelin, le Paysan (grande image retrouvée, ligne de sol noire
    effacée) et le Dragon (113 px au lieu de 118 : sa grande image n'est pas plus haute). Les retouches à la main
    revenues ont été refaites : éclats du Chevalier (dont un éclat blanc, effacé à la main : l'armure porte du blanc),
    des Nomades et du Béhémoth ; points détachés de l'Oiseau-tonnerre. Points de tir (`launchPoint`) ×1,5.
  - Regénérés (PixelLab, environ 1,7 $ avec les essais) : le **Squelette** (même allure, l'ancienne image en référence :
    fixe L1-1, attente idle-3, marche move-5 (consigne : chaque pied se lève et avance à son tour), attaque attack-6-r :
    les deux bras levés puis abattus sur l'ennemi, éclat effacé) et l'**Ange**, rendu **androgyne** à la demande de
    l'utilisateur (on ne sait pas si c'est un homme ou une femme, sans poitrine) : fixe L3-4, attente idle-1, vol move-4
    (grands battements d'ailes ; les 16 images de move-2 plient le corps, bizarre sur un long vol), attaque attack-8-r
    (une gifle à hauteur de poitrine ; consigne sans lumière ni traînée, puis éclat effacé). L'Ange vole lentement
    (`stepTime` 0,4 s par case, un battement toutes les deux cases) : à 0,09 s (les volants par défaut), il traversait le
    champ sans avoir le temps de battre des ailes (remarque de l'utilisateur). Les icônes d'argent **incrustées** dans la pierre (`Engrave` : l'argent posé dans un creux, l'ombre en haut
  à gauche et la lumière en bas à droite, contrastes doux), choisies par l'utilisateur parmi l'argent seul, gravé (un
  creux de pierre plus sombre) et incrusté ; un premier essai trop brut a été adouci. L'Épéiste et le Cavalier restent (plus
    petits au combat), à refaire plus tard (choix de l'utilisateur).
  - Effets (`BattleEffects`) : points de 2 pixels (`Pixel`), tailles et distances dans l'unité `U` (3 pixels : l'ancien
    pixel d'art) pour garder leur taille à l'écran ; les formes pleines (bulles, boules, largeur de l'éclair, fer de la
    hache, croix) comptées en pixels d'art pour ne pas laisser de trous.
  - Ailleurs, les images de combat ×2 (cases d'armée, fenêtre de recrutement) sont plus grandes et plus fines ; les
    créatures larges y sont un peu coupées (tête et avant gardés), comme avant les grandes.
  - Outils : « contour » et « bord » (semi-transparent) dans les fiches (`nettoyage.adoucir_image`,
    `nettoyage.bord_translucide`), essayés sur le Farfadet (Forêt, en cours).
- ✅ Foudre de l'Oiseau-tonnerre sur la case de la cible (2026-11-09, branche `foudre-cible`) : un **effet joué sur
  une case** (`BattleEffects.OnHex("foudre", sol)`), dessiné par le jeu en pixels d'art, et non plus dans l'animation de
  l'attaquant (choix de l'utilisateur : dessiné par le jeu plutôt qu'une feuille d'images). Déroulé : un traceur pâle
  descend du haut du champ (0,12 s, `LightningHitTime`), puis l'éclair éclate (cœur blanc de 3 pixels d'art, halo jaune
  pâle, trois branches fines, zigzag : des pas de 14 pixels d'art qui changent de côté), s'éteint 0,04 s et éclate de
  nouveau plus faible ; étincelles au point d'impact ; au sol, sous les troupes (`DrawGround`), une lueur blanche puis
  une marque brûlée qui s'efface (1,1 s en tout). Les dégâts s'affichent quand l'éclair touche le sol ; l'ancien éclat
  jaune des cases est retiré. Aperçu : `--effect-preview=foudre` (une troupe après l'autre) ou `foudre:0.2` (figé, pour
  les captures). Revu par l'utilisateur : **la foudre tombe à chaque coup** de l'Oiseau-tonnerre (son impact,
  `"impact": "foudre"` dans `animations.json` : l'éclair part un peu avant le coup et touche le sol quand il porte), et la
  foudre appelée (une attaque sur cinq, règles inchangées) est un second éclair plus fort (`foudre-forte` : plus large,
  plus de branches, tout le champ s'éclaire un instant), ses dégâts en bleu au-dessus de ceux du coup ; éclair **plus
  grand et bleu**, puis affiné (cœur blanc bleuté de 3 pixels d'art, halo bleu vif et lueur bleu sombre d'un pixel de
  chaque côté : 7 en tout ; la forte 5 + 2 + 2 : 13 en tout ; quatre ou six branches). Attaque refaite (PixelLab, 4 essais, 0,06 $) : ailes déployées en totem, cri vers le ciel, sans
  éclair ; choisie `attack-2-r` (l'image 2 avait une étincelle : remplacée par la 3, morceaux détachés effacés).
- ✅ Créatures barbares (2026-11-09, branche `creatures-barbares`, PixelLab, environ 3 $) : fiches dans
  `tools/art/creatures-art.json`, faites avec `tools/art/creature.py`. Choix de l'utilisateur : **Nomades**, archers à cheval des
  steppes au repos (sabre baissé), sur **2 cases** ; **Oiseau-tonnerre**, oiseau-totem (rouge, noir et blanc), ailes repliées,
  **1 case** ; **Béhémoth**, pas un ours mais une bête mythique **debout sur deux jambes**, visage de singe, crocs, crinière
  rousse, mousse, **1 case** (les tailles en cases ont changé en route : la session règles a suivi).
  - Nomades : visage retouché (réduit, il était dans l'ombre du chapeau) ; galop en boucle (images 3 à 14 d'un essai de 16,
    sans le départ ni le retour au pas), 16 images/s, 0,4 s par case.
  - Oiseau-tonnerre : attente (se lisse l'aile), attaque (la foudre frappe le sol devant lui), vol à l'horizontale serres
    repliées (animé depuis une pose en vol faite par retouche), un battement par case (0,57 s par case), envol en état
    « transform » (images 1 à 5, 12 images/s ; à l'envers à l'atterrissage).
  - Béhémoth : attente (frappe le sol), marche (bras l'un après l'autre), attaque (trous de la main comblés après
    l'effacement de l'éclat).
  - Images de la carte : PixelLab « Pro » à 32 px (Nomades 14, Oiseau-tonnerre 2, Béhémoth 37), pieds proposés.
- ✅ Ombres de la carte (2026-11-08, branche `ombres-carte`), dessinées par le jeu (`Game/ShadowLayer.cs`, un calque sous
  le décor et les objets ; pas d'ombre peinte dans les images). Lumière d'en haut à droite, comme Heroes 1 et 2.
  - **Ombre portée** : l'image noire, couchée sur le sol vers la gauche. L'image est découpée en morceaux (pixels qui se
    touchent) ; un morceau étroit (arbre, créature, tas) tourne autour de son pied (pixel le plus bas du milieu) ; un
    morceau large (remparts) pose chaque colonne sur son propre sol (sinon l'ombre des tours du fond passe sous le
    château), sans compter ses parties fines ; les morceaux minuscules ou creux (rails) et le chemin vers l'entrée d'une
    mine n'en font pas. Bord flou (gaussien). Réglages par défaut : force 0,45, longueur 0,5, penche 0,45, flou 0,6.
    Essais écartés : une seule ligne de pied par image (ombres sous les objets), une colonne par pixel pour tous (les
    arbres perdaient leur ombre quand l'angle changeait), un contour lissé (les flèches et drapeaux disparaissaient).
  - **Ombre de contact** (méthode de l'utilisateur) : pour chaque pied (du pixel le plus à gauche au plus à droite de la
    base), le bas du dessin entre les deux, fermé par un arc au-dessus, puis des bagues de plus en plus claires (55 %,
    31 %, 12 % ; « étalement » de 1 à 6 bagues par image), multipliées par la force « contact » (0,5). Les pieds sont
    proposés par `tools/art/pieds.py` (morceaux séparés à leurs passages étroits, suivi du bas du contour) puis revus sur
    une page (`revue-pieds.html` : glisser les points, ajouter, retirer, étalement) : 47 images sur 51 vérifiées. Écarté :
    une tache ronde sous l'objet, et toute règle automatique seule (château, mines, objets collés).
  - Réglage en jeu : `--shadow=force,longueur,penche,contact,flou` et `--shadow-tune` (curseurs).
  - **Scierie refaite** : cabane de trois quarts avec une pièce en plus, porte au bout du chemin, sol transparent
    (retouche PixelLab d'un candidat de Retro Diffusion, 0,08 $), décor replacé sans que rien ne se touche.
- ✅ Icônes des ressources (2026-11-07) : or, bois, minerai, gemmes, et d'avance mercure, cristal, soufre (pas encore dans
  les règles) ; 16 × 16 dans la barre et le royaume, 32 × 32 pour plus tard.
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
- **Harmoniser l'interface : les tailles communes** (2026-11-11, branche `interface-harmonie`). Constaté après le passage
  à ×2 : deux échelles mêlées (les écrans écrits dans les unités de l'ancien écran 1280 × 720, ×1,5, les images ×2),
  donc des cadres, écarts et boutons hors de la grille des pixels d'art ; 14 tailles de texte (7 à 44) et 6 pour les
  boutons, toujours lissées (la police nette n'était jamais choisie) ; des boutons de 30 à 70 de haut.
  - **La police.** VT323 est dessinée sur une grille de 80 unités pour 1000 (mesuré dans le fichier) : 12,5 pixels par
    taille, nette seulement à 25 (trop petite, a jugé l'utilisateur) ou 50 (trop grande). Six essais comparés sur les
    mêmes écrans (page archivée dans `art-candidats/archives/2026-11-11-polices/`) : VT323 à 25 ; Pixel Operator fin à
    32 ; en trait épais (HB) à 32 (aimé, mais trop grand) ; fin et épais mêlés ; fin avec des titres en Jersey 10 ;
    **Pixel Operator HB 8 à 16, choisi** (licence CC0 ; 8 pixels par taille : un pixel de police = un pixel d'art ;
    capitales de 14 pixels, en gras). Les titres en Pixel Operator HB à 32, le nom du jeu à 48. `Ui.TextSize` ramène
    les anciennes tailles à trois : moins de 18 → 16, 18 à 39 → 32, 40 et plus → 48. La police laissait deux pixels
    entre les lettres et presque rien entre les lignes : `FontVariation` retire un pixel d'art entre les lettres (un
    huitième moins large) et en ajoute un au-dessus et au-dessous de chaque ligne.
  - Les textes qui ne tiennent plus finissent par « … » dans leur case, le texte entier au survol (carte de
    construction, case des mines du royaume, sauvegardes, nom d'une ville dans la colonne), ou passent à la ligne sans
    couper un mot ; le dernier message s'arrête avant « Journal ».
  - `Ui.Px` arrondit à la grille (2 pixels) ; `Ui.Snap`, `SnapDown`, `ArtPx` (pixels d'art) ; les bords des boîtes en
    pixels d'art entiers. Tailles communes : `SmallButtonHeight` 32, `ButtonHeight` 44, `LargeButtonHeight` 60,
    `IconButtonSide` 80 (l'icône de 32 ×2 et 4 pixels d'art de pierre), `CloseButtonWidth` 128, `WindowMargin` 24,
    `ButtonGap` 8 ; `Ui.Sized` les donne. `MakeButton` a au moins la hauteur commune ; les boutons des petites
    fenêtres la prennent par le thème (`buttons_min_width`, `buttons_min_height` : Godot écrase sinon leur taille).
  - Les icônes des boutons centrées sur la grille ; le groupe de droite du combat sortait de l'écran (sa largeur se
    calcule maintenant d'après les tailles communes, `BattleScreen.GroupWidth`).
  - « Fermer » : même taille, en haut à droite, avec le titre sur la même ligne (grandes fenêtres et fenêtres de la
    ville) ; les petites fenêtres gardent leurs boutons en bas.
  - Les petites fenêtres (`GameDialog`) assombrissent l'écran derrière elles, comme les grandes.
  - Ajustés au passage : les noms des places d'équipement en info-bulle (écrits dedans, ils ne tenaient qu'en taille 7),
    les nombres des caractéristiques au bout de la ligne de leur nom, les cartes de recrutement (trois lignes de
    caractéristiques), la description d'un sort de la guilde sur deux lignes au plus (le reste au survol), la taverne
    et le champ du nom d'une sauvegarde, « Max » dans `Texts`.
  - **Écran par écran** (branche `interface-ecrans`) : les listes des héros et des villes de la colonne de la carte comme
    Heroes 2, des portraits sans nom, deux par rangée (six visibles au lieu de trois ; les flèches font défiler une
    rangée), le détail au survol (points de sort ajoutés au héros, `Texts.TownDetails` pour la ville) ; à côté d'un
    héros, deux barres qui se vident vers le bas (mouvement vert, points de sort bleus) ; à côté d'une ville, une marque
    verte si l'on peut encore y construire. La fiche du héros plus aérée. Les cartes de recrutement : les textes dans
    une colonne (`VBoxContainer`), les boutons calés en bas. Le choix de niveau et les barres du combat n'avaient plus
    besoin de changer. Les images des villes de la liste restent un morceau de 64 pixels ×1 (×2, on n'en verrait que la
    porte).
  - Gardé : les petites images de l'armée dans la colonne de la carte restent ×1 (choix de l'utilisateur du
    2026-11-08, entières plutôt que ×2 coupées), à revoir avec la colonne.
- Animations des créatures avec PixelLab (étape 6, essai sur l'Épéiste).
- Fonds de champ de bataille et obstacles ; le héros sur la carte ; les bâtiments des villes en images détaillées.
- Petits détails et équilibrage fin.

### Le plan des étapes 9 et 10 au 2026-11-15

Déplacé de `PLAN.md` le 2026-11-15 (second ménage du plan), tel quel ; les titres ont un niveau de plus. Le plan
n'en garde que ce qui reste à faire et les choix encore valables.

### Étape 9 : contenu

#### La magie (en cours)
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
  | 4 | Chaîne d'éclairs | 20 | 25 + 40 × puissance dégâts, moitié à chaque saut (4 troupes, amis compris, chaque saut à 3 cases au plus) |

- **Plus tard** : écoles de magie et compétences (avec les classes de héros), versions « de masse » (récompense des
  classes), **créatures insensibles à la magie**, un 5e niveau avec les factions (Armageddon, élémentaires, morts
  relevés), sorts de la carte (portail de ville, porte dimensionnelle, vision…). Écartés pour l'instant : berserk,
  hypnotisme, paralysie.

Fait : les règles, la guilde, le livre au combat et sur la carte, les 18 sorts (voir `HISTORIQUE.md`). À faire, dans
cet ordre :
1. **L'équilibrage avec la magie** : la simulation (seul) et les adversaires, avec les mesures (`Mesures`).
   En cours (2026-11-06). Corrigé : l'IA bâtissait la guilde avec le bois et le minerai que ses habitations
   attendaient (en Normal, son armée était tombée de 14 000 à 6 000 or) ; elle ne bâtit plus un bâtiment plus loin
   dans sa liste que s'il ne retarde pas le premier (`ComputerPlayer.BuildNext`) : Facile 12 000, Normal 16 000,
   Difficile 18 500 or après six semaines. Mesuré : la simulation avec un héros qui monte de niveau gagne vers le jour
   87 en force (attaque et défense). En magie, le chiffre ne vaut rien : la simulation ne ramasse pas les tas de
   ressources, et la guilde (5 bois, 5 minerai) n'était bâtie que très tard ou jamais (le héros n'avait aucun sort) ;
   bâtie en premier, elle prend le bois et le minerai des habitations, et l'armée reste trop petite pour gagner.
   **L'équilibre force / magie est reporté aux classes de héros** (choix de l'utilisateur) : d'abord apprendre à la
   simulation à ramasser les tas, puis comparer les classes (`Simulation_force_ou_magie`).
2. **Le livre de sorts en image** : un vrai livre ouvert, une page par niveau, une animation de page qui tourne.
   Décidé avec l'utilisateur (2026-11-11, « ne pas dépenser à l'aveugle ») : **une page par niveau** (niveaux 1-2, puis
   3-4) et des **onglets par école** sur le bord (Eau, Feu, Terre, Air, Tous) pour n'en montrer qu'une ; chaque sort :
   **son icône, son nom, son coût** (l'effet au survol), six par page ; au combat, un clic lance, un sort impossible est
   grisé ; sur la carte, le livre se lit. **D'abord une maquette dans le jeu sans rien générer** (parchemin et onglets
   dessinés par le code, icônes d'attente), pour régler les tailles sur des captures ; **ensuite seulement** l'image du
   livre ouvert, à la taille trouvée. La page qui tourne : dessinée par le code.
   Puis (2026-11-11, choix de l'utilisateur) : **en perspective et magique**. La perspective vient des images, jamais
   d'une déformation par le jeu (les pixels resteraient réguliers et les sorts lisibles) : dans l'onglet Magie de
   l'écran du héros, **le livre fermé posé sur un lutrin**, en perspective, qui luit ; un clic l'**ouvre** (la
   couverture tourne, une gerbe de lumière et d'étincelles) ; le livre ouvert **vu un peu d'en haut**, comme posé sur une
   table, les sorts dessinés à plat sur les pages. Effets dessinés par le jeu : la gerbe à l'ouverture, des étincelles
   derrière la page qui tourne, une lueur de la couleur de l'école autour du sort survolé (des sons plus tard). **La
   guilde des mages montre son propre grimoire** : le même livre, ses sorts par niveau, ceux que le héros connaît déjà
   marqués, les niveaux pas encore construits dits. **Des scènes** (choix de l'utilisateur) : l'onglet Magie montre le
   lutrin dans **une étude** (la même pour tous les héros : une bougie, une étagère, une fenêtre) ; la guilde ouvre
   d'abord **l'académie de la faction**, une scène par faction (le Château d'abord, les autres avec les images de leur
   ville), le grimoire sur son lutrin au milieu ; un clic ouvre le livre. Chaque scène remplit son onglet ou sa fenêtre ;
   le fond est une image sans le lutrin (`art/ui/scenes/etude.png`, `academie-<faction>.png`), le lutrin et son livre une
   image à part posée dessus (`art/ui/lutrin.png` : il luit, on clique dessus). Sans image, le jeu dessine une scène
   d'attente. ✅ Le lutrin (le 3, un socle rond sculpté) le livre ouvert et l'étude (2026-11-12 ; le lutrin fait partie de l'image de chaque scène, pour le même point de vue et la même lumière : choix de l'utilisateur). ✅ L'académie du Château
   (2026-11-13 : une petite pièce sous le toit d'une tour, choix de l'utilisateur). ✅ Les 7 autres académies (2026-11-14, de petites pièces ; le lutrin et sa lueur prennent la lumière de chaque pièce, choix de l'utilisateur).
3. ✅ **Une image par sort** (le livre, la guilde) : **de petites peintures en couleur**, comme Heroes 2 et 3 (choix de
   l'utilisateur), 32 × 32 ×2 ; les 18 faites (2026-11-14).
4. ✅ **Une animation par sort** sur le champ et un **effet visible sur les troupes** (2026-11-14, branche
   `sorts-animations`, dessinés par le jeu, revus sort par sort avec l'utilisateur en quatre passages) : ce qui tourne
   autour d'une troupe passe derrière son corps, la troupe elle-même change (grise sous la peau de pierre, rougit de
   rage, s'efface dans la téléportation…), une aura tant qu'un sort dure ; la chaîne d'éclairs ne saute qu'à 3 cases au
   plus (choix de l'utilisateur). Détail dans `HISTORIQUE.md`. Puis le reste au fil des idées (sons…).

#### Les factions (en cours de conception)
Décidé avec l'utilisateur (2026-11-06, puis 2026-11-08) : **8 factions jouables + 1 faction neutre**. Chacune a sa
créature du niveau 7 et, plus tard, sa **version améliorée** (comme Heroes 3 : un bâtiment améliore une créature) ; ses
deux ressources rares (la **principale** pour l'habitation et la créature du niveau 7, la **secondaire** pour les
niveaux 5 et 6 : chaque ressource est principale pour deux factions et secondaire pour deux autres).
**Méthode pour chaque faction** (choix de l'utilisateur, 2026-11-08) : définir ensemble d'abord sa **philosophie**, ses
**forces** et ses **faiblesses** (et ce qui la distingue des autres), puis ses créatures niveau par niveau.

| Faction | Style | Niveau 7 → amélioré | Principale | Secondaire | État |
|---|---|---|---|---|---|
| **Château** (humains) | équilibré, le plus charismatique ; classe Paladin | Ange → Archange | cristal | gemmes | jouable |
| **Morts-vivants** | magie | Colosse d'os → Colosse funèbre | mercure | gemmes | jouable |
| **Barbares** | force, la horde | Béhémoth → Béhémoth ancien | soufre | cristal | jouable |
| **Forêt** | les meilleurs tireurs, plantes et soins | Troll → Troll ancien | gemmes | cristal | jouable |
| **Tour de magie** | la faction de la magie | Géant → Titan | gemmes | mercure | jouable |
| **Enfer** | le chaos et le feu, le prix du sang | Démon → Archidémon | mercure | soufre | jouable |
| **Donjon** | les seigneurs des abîmes, la Terreur | Hydre → (à choisir) | soufre | mercure | jouable |
| **Nains** | montagne, très solides, runes et machines | Machine de guerre → Forteresse ambulante | cristal | soufre | jouable |

- **L'Ange ressuscite** (idée de l'utilisateur, 2026-11-08, **plus tard**, sur une branche à part) : à la place de son
  action, il ramène des créatures amies tuées dans ce combat (pas les morts-vivants), **à chacun de ses tours** s'il le
  veut : puissant, mais il y passe tout son tour. Proposé : autant de points de vie par Ange que ses dégâts (80), pour
  que soigner ou frapper soit un vrai choix (100, comme Heroes 3, en vaut un peu plus). Puis refaire la mesure
  `Duels_a_prix_egal`.
- Le **Colosse d'os** reste au niveau 7 des morts-vivants, rendu aussi fort que l'Ange (il est plus faible : à revoir) ;
  le Colosse funèbre a plus de vie et son balayage effraie (les ennemis touchés ne ripostent plus).
- Les **Barbares** : voir plus bas.
- La **Forêt** : voir plus bas ; le Troll ancien se régénérera plus, et ses coups repousseront l'ennemi (il ne riposte
  plus).
- L'**Enfer** et les **Nains** : voir plus bas.
- Le **Tyran œil** : un œil géant qui tire des rayons, chacun un sort néfaste au hasard. La **Machine de guerre** :
  une machine à vapeur avec un canon (un tireur dont le coup touche une zone).
- La **faction neutre** garde les monstres de la carte seulement : le **Dragon rouge** (et d'autres au fil du temps).
- L'IA devra apprendre chaque nouvelle faction. **Plus tard : les créatures améliorées** ; la **Banshee** sera le
  Fantôme amélioré.

Le détail de chaque faction (philosophie, créatures, capacités, chiffres réglés par `Duels_a_prix_egal`, mesures) est
dans `HISTORIQUE.md` (étape 9, « Les factions : conception et chiffres ») ; les chiffres en vigueur sont dans
`godot/data/` (`creatures.json`, `buildings.json`, `classes.json`). Ici, ce qui reste valable et ce qui reste à faire.

##### Les Nains (jouables)
Les halls de la montagne : des artisans têtus, runes et machines (créatures qui ne copient ni Heroes 3 ni Heroes 5).
Marque : **l'obstination** (une troupe qui ne s'est pas déplacée du round prend 20 % de dégâts en moins) ; les machines
sont façonnées (résistent à la magie). Classe **Thane** (talent Obstination). Créatures : Porte-bouclier, Arbalétrier,
Chevaucheur de chèvre, Porte-étendard des ancêtres, Lance-flammes, Gyrocoptère, Machine de guerre (→ Forteresse
ambulante). ✅ Créatures et capacités, ✅ ville, classe et héros, ✅ ville et héros sur la carte.
**Reste** : les images des 7 créatures (session images).

##### L'Enfer (jouable)
Le chaos et le feu : toutes ses créatures sont insensibles au feu. Marque : **le prix du sang** (chaque créature de
l'Enfer tuée rend des points de sort au héros). Classe **Démoniste** (talent Pacte). Créatures : Diablotin, Cultistes,
Limon de magma, Succube, Tourmenteur, Moloch, Démon (→ Archidémon). ✅ Créatures et capacités, ✅ ville, classe et héros,
✅ ville et héros sur la carte, ✅ images des créatures et icône du bouton « Invoquer » (2026-11-15, branche
`creatures-enfer`).
**Reste** : les noms des habitations, à revoir si l'utilisateur le souhaite. **À faire (session règles, choix de
l'utilisateur, 2026-11-15)** : le Démon **se téléporte** (capacité `teleports` : toute case libre à sa portée, comme un
volant, sans voler ; son animation est prête) ; il **crache le feu** (le jet est fait) et sa cible **brûle** quelques
tours (comme le poison de l'Araignée), à comparer d'abord avec l'aura de chaleur du Moloch.

##### Le Donjon (jouable)
Les seigneurs des abîmes : des élites fortes sur une base faible. Marque : **la Terreur** (chaque coup fait perdre 1 de
moral à la cible ; insensibles : morts-vivants, façonnés, niveau 7). Classe **Seigneur des abîmes** (talent Effroi).
Créatures : Homme-lézard, Homme-grenouille, Minotaure, Aberration, Ver des profondeurs, Tyran œil, Hydre. ✅ Créatures et
capacités, ✅ ville, classe et héros, ✅ images des créatures, ✅ ville et héros sur la carte.
**Reste** : le nom de l'Hydre améliorée ; **plus tard** (choix de l'utilisateur, 2026-11-14), revoir les animations du Ver
des profondeurs (glisser vers sa cible, les fissures, peut-être avaler) ; **à surveiller** : ses gardiens (la Terreur)
ralentissent certaines petites cartes. **À revoir** (idée de l'utilisateur, 2026-11-14) : l'attaque du Géant (Tour) qui
frappe le sol pourrait toucher une zone : une règle à concevoir avec la session règles.

##### La Tour de magie (jouable)
Le savoir : des mages, des créatures **façonnées** (résistent à la magie : moitié des dégâts des sorts, aucun sort
néfaste) et des géants. Marque : **la bibliothèque des arcanes** (un sort de plus par niveau de guilde). Classe
**Magicien** (talent Érudit). Créatures : Gnome, Gargouille, Golem de pierre (→ Golem d'airain), Naga, Djinn, Mage
(→ Archimage), Géant (→ Titan). ✅ Créatures et capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la
carte.
**Reste** : le Mage un peu en dessous de son niveau en duel (les duels en terrain ouvert avantagent le corps à corps), à
revoir à l'équilibrage.

##### La Forêt (jouable)
Les esprits du bois : les meilleurs tireurs, des troupes qui se soignent, peu nombreuses et chères. Marque : **la
régénération de la nature** (la créature de tête de chaque troupe retrouve un quart de ses PV à son tour). Classe
**Rôdeur** (talent Chasseur). Créatures : Farfadet, Sanglier, Archer elfe, Cerf, Druide, Tréant, Troll (→ Troll ancien).
✅ Créatures et capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la carte.
**Reste** : souvent la faction la plus forte des adversaires mesurés (sa régénération), à revoir à l'équilibrage.

##### Les Barbares (jouables)
La horde : la force, le nombre et la vitesse, le mépris de la magie. Aux niveaux 1 à 4, des créatures moins chères, plus
nombreuses et plus faibles une à une. Marque : **la Rage** (une troupe frappée gagne de l'attaque). Classe **Barbare**
(talent Chef de guerre). Créatures : Gobelin, Loup, Orc, Ogre, Nomades (2 cases, frappe et repart), Oiseau-tonnerre,
Béhémoth. ✅ Créatures et capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la carte.
**Reste** : **aux sièges** (quand les combats en ville auront des murs ; idée de l'utilisateur), les troupes barbares
frappent les murs ; **à voir** : la horde devrait être la plus forte des premières semaines (avec le moral, ses grosses
troupes dépassent souvent la limite de commandement) ; **à revoir avec l'utilisateur** (vu par `Duels_a_prix_egal`) :
l'Archer perd contre tous les niveaux 2, le Griffon bat largement les niveaux 3, le Vampire écrase le Moine, le Cavalier
écrase les niveaux 6, l'Ange écrase le Colosse d'os.

##### Les morts-vivants (jouables)
La magie et la mort : insensibles aux sorts de l'esprit, ni soins ni résurrection sur eux. Marque : **la nécromancie**
(du Nécromancien : des ennemis tués se relèvent en Squelettes). Créatures : Squelette, Zombie, Fantôme (grossit en
tuant), Araignée géante (poison), Vampire (draine la vie), Liche (nuage de mort), Colosse d'os (balayage). ✅ Créatures et
capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la carte.
**Reste** : le **Colosse d'os plus faible que l'Ange** (à rendre aussi fort) ; le Squelette peut-être trop fort face aux
paysans (à surveiller en jeu) ; **plus tard** : l'amplificateur de nécromancie, la Banshee (le Fantôme amélioré).

#### Les héros et leurs classes (en cours)
Décidé avec l'utilisateur (2026-11-06, après une revue des classes de Heroes 1 à 7 et de King's Bounty) :
- **Un héros n'est qu'un nom et un portrait** (`data/heroes.json`) ; **sa classe donne tout le reste**
  (`data/classes.json` : caractéristiques et sorts de départ ; plus tard un talent, et le livre à acheter pour les
  classes de force).
- **Une classe par faction** pour commencer : un héros prend la classe de la faction qui le recrute (la ville de départ,
  la taverne). La force et la magie se départageront entre factions (comme Heroes 5).
- **Le Château : le Paladin**, équilibré (il se bat bien et lance un peu de magie sainte) et le **plus charismatique**
  de toutes les classes : attaque 2, défense 2, puissance 1, savoir 2, **charisme 14**, le sort Soins au départ.
  Plus tard, un talent (par exemple plus fort contre les morts-vivants, comme le Paladin de King's Bounty).
- **Au niveau supérieur, le choix reste libre** entre les quatre caractéristiques (la classe ne fait que le départ).
- **Nouvelle partie** : on choisit sa faction (le Château seulement pour l'instant) et son héros dans une grille de
  portraits ; les adversaires prennent les derniers héros de la liste.
✅ Fait : les données, la classe donnée au départ et à la taverne (sauvegardée), la classe sur l'écran du héros et
dans la taverne. La fenêtre Nouvelle partie refaite (choisie par l'utilisateur parmi trois arrangements, 2026-11-06) :
deux colonnes ; à gauche « La partie » (**listes déroulantes** pour la taille de la carte, les adversaires et la
difficulté, la graine, puis les façons de gagner) ; à droite « Votre héros » (la faction, le héros choisi en grand,
portrait ×4, son nom, sa classe et ce qu'elle donne, et en bas une **bande de portraits qui défile** ◄ ►, le choisi
encadré d'argent : la liste des héros peut grandir). Tests : `ClassesTests`.
✅ **Chaque héros a une faction** (2026-11-07, `faction` dans `data/heroes.json`) : 41 héros, chacun avec son portrait et
un fond qui lui est propre (Retro Diffusion) ; Château 9, morts-vivants 6, et déjà ceux des factions à venir (Barbares 5,
Forêt 6, Tour 6, faction sombre 9 : sorciers et démons). La nouvelle partie ne montre que les héros de la faction choisie ;
une taverne propose d'abord ceux de la faction de sa ville, puis les autres quand il n'y en a plus ; chaque adversaire
prend le dernier héros de sa faction. Tests : `ClassesTests`.
À faire avec les factions : leurs classes, puis l'équilibre force / magie (d'abord apprendre à la simulation à ramasser
les tas de ressources : `Simulation_force_ou_magie`).

#### Les compétences des héros et les artefacts (à concevoir)
Rappelé par l'utilisateur (2026-11-09) : deux grands sujets à concevoir ensemble, avec la même méthode que les factions
(l'idée d'abord, puis le détail).
- **Les compétences** (en cours de conception, 2026-11-09, décidé avec l'utilisateur) :
  - **Trois arbres**, un par famille : **Aventure** (déplacement, vision, économie, diplomatie), **Commandement**
    (autour du charisme) et **Magie** (écoles de magie). Pas de famille du combat (le combat passe par l'attaque et la
    défense).
  - **À chaque niveau** : +1 charisme, +1 à la caractéristique choisie (comme aujourd'hui) **et un point de
    compétence** : une compétence nouvelle au rang de base, ou un rang de plus (base → avancé → expert).
  - **Les rangées d'un arbre s'ouvrent selon les points dépensés dans cet arbre** (la deuxième après 3 points, la
    troisième après 6) : se spécialiser paie.
  - **Six compétences au plus** par héros, trois rangs chacune : un héros devient un spécialiste.
  - **La classe** donne une compétence de départ et **son propre talent**, qui grandit avec le niveau (comme la
    nécromancie du Nécromancien).
  - Les compétences (rangée : 1 dès le départ, 2 après 3 points dans l'arbre, 3 après 6 ; base / avancé / expert ;
    chiffres à régler en jouant) :

    | Arbre | Rangée | Compétence | Effet |
    |---|---|---|---|
    | Aventure | 1 | Logistique | +10 / 20 / 30 % de mouvement sur la carte |
    | Aventure | 1 | Éclaireur | voit 2 / 4 / 6 cases plus loin |
    | Aventure | 2 | Orientation | le surcoût des terrains difficiles baisse de 25 / 50 / 75 % |
    | Aventure | 2 | Intendance | +100 / 150 / 200 or par jour (à équilibrer après des essais) |
    | Aventure | 2 | Chance | +1 / 2 / 3 de chance aux troupes ; compte aussi pour les artefacts trouvés et les coffres |
    | Aventure | 3 | Diplomatie | un gardien bien plus faible propose de rejoindre l'armée (voir plus bas) |
    | Commandement | 1 | Commandement | +10 / 20 / 30 % de points de commandement par charisme |
    | Commandement | 1 | Discipline | révoltes 25 / 50 / 75 % moins probables |
    | Commandement | 1 | Moral | +1 / 2 / 3 de moral aux troupes |
    | Commandement | 2 | Recruteur | +10 / 20 / 30 % de croissance dans la ville où le héros commence la semaine |
    | Commandement | 2 | Inspiration | les troupes plus rapides au début du combat (+1 / +1 / +2 au premier round, puis +0 / +1 / +1) |
    | Commandement | 3 | Tactique | placer ses troupes avant le combat, dans les 2 / 3 / 4 premières colonnes (le plus haut rang, selon son avance) |
    | Magie | 1 | Mysticisme | +1 / 2 / 3 points de sort par jour |
    | Magie | 1 | École de l'eau | ses sorts coûtent 20 / 30 / 40 % de moins, +15 / 30 % de force ; expert : un sort spécial |
    | Magie | 2 | École du feu | de même |
    | Magie | 2 | École de la terre | de même |
    | Magie | 3 | École de l'air | de même |

  - Les écoles : **eau** (Bénédiction, Soins, Faiblesse, Téléportation, Dissipation), **feu** (Soif de sang,
    Malédiction, Aveuglement, Boule de feu), **terre** (Peau de pierre, Lenteur, Pluie de météores, Résurrection),
    **air** (Flèche magique, Éclair, Hâte, Bouclier contre les tirs, Chaîne d'éclairs).
  - Les classes : Paladin (Commandement ; talent **Croisé** : +2 % de dégâts par niveau contre les morts-vivants),
    Nécromancien (École de la terre ; **Nécromancie**), Barbare (Logistique ; **Chef de guerre** : +1 au plus fort de
    la Rage tous les 3 niveaux), Rôdeur (Éclaireur ; **Chasseur** : +1 % de dégâts par niveau pour ses tireurs).
  - **À concevoir à part** : les **quatre sorts spéciaux** des écoles (idées : Prière, Armageddon, Séisme, une invocation).
  - ✅ **La Diplomatie et la fuite des gardiens** (décidées avec l'utilisateur, 2026-11-09, branche `diplomatie`,
    `Core/Diplomacy.cs`) : avec la Diplomatie, un gardien de la carte bien plus faible **propose de rejoindre** le héros
    avant le combat (une fenêtre : accepter, combattre). Chaque **degré** le rend plus facile : le rang (1 à 3), +1 pour un
    gardien de la faction du héros, +1 par palier de charisme (20, 30, 40). Degré 1 : l'armée vaut au moins 3 fois la
    sienne, à son prix ; 2 : 2 fois, moitié prix ; 3 : 1,5 fois, gratuit ; 4 : 1,25 fois ; 5 et plus : 1 fois. Il doit
    **tenir dans le commandement** du héros (avec la troupe de la même créature), sinon « il ne lui obéirait pas » ; tout
    ou rien (de quoi payer, une place). Ni combat ni expérience. Pour **tout héros**, un gardien devant une armée qui vaut
    **10 fois** la sienne **s'enfuit** : le laisser partir (ni combat ni expérience) ou le poursuivre. Les forces se
    comparent en or. L'IA prend le gardien si elle garde 1000 or pour recruter, sinon combat (elle poursuit toujours).
    Mesuré : les adversaires comme avant (leurs six compétences sont prises avant la Diplomatie ; ils poursuivent les
    gardiens qui fuient, comme ils les combattaient). À voir : l'IA qui choisit la Diplomatie (une classe, plus tard).
    Tests : `DiplomatieTests`.
  - ✅ **Le moral et la chance** (décidés avec l'utilisateur, 2026-11-09, branche `moral-chance`, `Core/Morale.cs`) :
    de −3 à +3 par troupe. **Bon moral** : après avoir agi (pas attendu ni défendu), la troupe rejoue aussitôt, une fois
    par round, 5 % par point (sans pouvoir attendre) ; **mauvais moral** : elle se fige et passe son tour, 10 % par
    point. **Chance** : un coup sur 20 par point fait deux fois plus de dégâts ; **malchance** : moitié moins (pas sur
    les troupes touchées en plus de la cible). Le moral vient du **charisme** (choix de l'utilisateur : +1, +2, +3 à
    toute l'armée à 20, 30, 40 de charisme, et −1, −2, −3 à une troupe au-delà de 1, 1,5, 2 fois la limite de
    commandement), du **mélange des factions** (une +1, trois −1, quatre ou plus −2), de la compétence **Moral** et du
    **temple** ; la chance de la compétence **Chance** et du **cercle des fées** (+1 jusqu'au prochain combat, deux
    lieux par carte de 40 × 30). Sans héros (monstres, garnisons seules, révoltés) : 0. Les morts-vivants suivent les
    mêmes règles (pas d'exception, choix de l'utilisateur). Plus tard : les touches des factions (à l'équilibrage),
    les artefacts, un bâtiment de ville, l'effet de la Chance sur les coffres et les artefacts trouvés.
    Mesuré (un adversaire, Normal, six semaines, or d'armée ; avant → après) : Château 25 300 → 26 100, morts-vivants
    25 700 → 27 600, **Barbares 27 700 → 23 000** (charisme 10 et grosses troupes : souvent au-delà de la limite, donc
    un mauvais moral ; à revoir à l'équilibrage), Forêt 29 200 → 33 700 ; Facile 24 500, Normal 27 000, Difficile
    34 600. La simulation seule (sans héros au combat) ne change pas. Tests : `MoralChanceTests`.
  - ✅ **Fait (2026-11-09)** : les règles (`data/skills.json`, `SkillCatalog`, `Hero.Skills`, `Hero.SkillPoints`,
    `GameState.LearnSkill`, sauvegarde), la compétence de départ de chaque classe, les écoles des 18 sorts
    (`spells.json`), et les effets de Logistique, Éclaireur, Orientation, Intendance, Commandement, Discipline,
    Recruteur, Inspiration, Mysticisme et des quatre écoles (sans leurs sorts spéciaux). Écran du héros : un cadre
    « Compétences » (les points, les compétences du héros, le bouton des arbres) au-dessus des artefacts ; la fenêtre
    des arbres (`SkillsWindow`, `--open=skills`), en attendant le nouvel écran du héros (ci-dessous). L'IA dépense ses
    points (la compétence de sa classe d'abord, jusqu'à expert, puis Logistique, Intendance, Commandement…). La
    simulation seule n'en prend pas (ses chiffres restent comparables). Mesuré (un adversaire, Normal, six semaines,
    or d'armée) : Château 25 300, morts-vivants 25 700, Barbares 27 700, Forêt 29 200 (sans compétences : 25 000,
    20 900, 23 100, 30 600 : les factions se rapprochent). Tests : `CompetencesTests`.
  - ✅ **La Tactique** (décidée avec l'utilisateur, 2026-11-09, branche `tactique`) : le héros qui a le plus haut rang en
    Tactique place ses troupes avant le premier round, dans 2 / 3 / 4 colonnes selon son avance (rangs égaux : personne) ;
    placement seulement. À l'écran : « Placement », cliquer une troupe puis sa case, « Commencer » (« Auto » place pour
    le joueur). L'IA avance ses troupes de corps à corps au plus loin, garde ses tireurs derrière. Tests : `TactiqueTests`.
  - ✅ **Les talents des classes** (2026-11-09, branche `talents-classes`) : Croisé (Paladin, +2 % de dégâts par niveau
    contre les morts-vivants), Chef de guerre (Barbare, la Rage au plus +1 tous les 3 niveaux), Chasseur (Rôdeur, +1 % de
    dégâts par niveau pour ses tireurs), avec la Nécromancie ; au survol de la classe sur l'écran du héros. Mesuré (un
    adversaire, Normal, six semaines, or d'armée) : Château 26 100 → 26 800, Forêt 33 700 → 32 800, morts-vivants et
    Barbares inchangés (des bonus qui grandissent lentement). Tests : `TalentsTests`.
  - **Reste** : les sorts spéciaux (plus tard, quand le jeu sera plus avancé : choix de l'utilisateur) ; la simulation qui
    prend des compétences.
  - Plus tard : les cavernes de la carte, où l'on apprend une compétence.
- **Les artefacts** : les objets que porte un héros (aujourd'hui un seul, `data/artifacts.json`) : leurs places sur le
  héros, leurs effets, où on les trouve (gardiens, coffres, cavernes), peut-être des ensembles.
- **L'écran du héros, refait** (choix de l'utilisateur, 2026-11-09, parmi trois maquettes :
  `art-candidats/en-cours/disposition-heros.html`) : à gauche, toujours visibles, le portrait, le niveau, l'expérience,
  le mouvement et les caractéristiques ; à droite, **trois grands onglets** : **Équipement** (le héros **en pied**, une
  image par héros d'après son portrait, bien visible ; **11 emplacements en deux colonnes**, à sa gauche et à sa droite,
  **chacun relié par un trait** à l'endroit où il se porte : tête, cou, épaules (cape), torse, main de
  l'arme, main du bouclier, deux anneaux, ceinture, pieds, divers ; et un **coffre de 16 places de la même taille**),
  **Compétences** (trois sous-onglets Aventure, Commandement, Magie ; des icônes, le texte à côté ou en info-bulle),
  **Magie** ; l'armée en bas. **Une image de 32 × 32 par artefact** sert partout (emplacements et coffre ×3, et sur la
  carte ×2, posé au sol comme un objet à ramasser, une ombre ou une lueur dessinée par le jeu dessous) ; une seconde
  image pour la carte seulement si celle-ci s'y lit mal. Images à faire (session images) : 41 héros en pied, une icône
  par compétence.
  ✅ **Construit avec des images d'attente** (2026-11-09, branche `ecran-heros`) : les onglets sur la ligne du titre
  (le choisi en or), une silhouette dessinée à la place du héros en pied (`HeroFigure`), les emplacements de 64 unités
  (l'artefact ×3), le coffre 4 × 4, des ronds aux deux premières lettres pour les icônes des compétences (un clic
  apprend) ; la fenêtre des arbres à part est retirée. Chaque artefact a sa place (`slot` dans `artifacts.json`) et va
  à la première libre de son genre, sinon au coffre (`Equipment.Arrange`) ; **on ne les déplace pas encore à la main**
  (avec la conception des artefacts). Tests : `EquipementTests`.
  **Les onglets en marque-pages** (choix de l'utilisateur, à l'essai) : un ruban de soie généré (PixelLab), teint par
  le jeu de la couleur de chaque onglet, qui sort du haut de sa page (`TabStrip`, réutilisable) ; une image plus juste
  si l'essai est validé. À revoir avec **la taille de toute l'interface** (la carte est passée de 24 à 32 px, le combat
  à ×2) : une maquette HTML vivante de l'écran du héros, faite des vraies images, essaie les tailles sans recompiler
  (`art-candidats/en-cours/maquettes/heros.html`, par un petit serveur ; captures de trois variantes à côté).

#### Les ressources rares ✅
Décidé avec l'utilisateur (2026-11-07), comme Heroes 2 et 3 : **7 ressources**, l'or, le bois et le minerai (communs),
le **mercure, le cristal, le soufre et les gemmes** (rares). Mines : 250 or, 2 bois ou minerai, 1 d'une ressource rare
par jour ; tas rares de 3 à 6 ; au départ 2 de chaque ressource rare.
- **Chaque faction a deux ressources rares** (8 factions pour 4 ressources : chacune sa paire, `classes.json`) : la
  **principale** pour son habitation et sa créature du niveau 7, la **secondaire** pour ses habitations des niveaux 5 et
  6. Château : cristal et gemmes (Portail de gloire 5 cristal, Ange 1 cristal) ; morts-vivants : mercure et gemmes
  (Charnier 5 mercure, Colosse 1 mercure) ; plus tard l'Enfer : mercure et soufre. Le Dragon rouge (neutre) : 1 soufre.
- **La guilde des mages** demande un peu de chaque ressource rare : 1, 2, 3 de chacune pour les niveaux 2, 3, 4.
- **La carte** : **1,5 mine par zone** (au lieu d'une : la carte paraissait vide), plus une de chaque ressource rare ; au-delà
  d'une de chaque ressource, tour à tour or, rare, bois, or, rare, minerai (`MapGenerator.MinesPerZone`) ; **dans la zone de
  départ de chaque joueur, une mine de bois, une de minerai et une de sa ressource principale, sans gardien** (comme les
  cartes aléatoires de Heroes 3 : un joueur n'est jamais bloqué par la carte, sans combat risqué les premiers jours ;
  gardées, elles faisaient perdre son héros à l'IA des morts-vivants) ; **une mine gardée de sa ressource secondaire dans
  une zone voisine** ; deux tas sur huit d'une ressource rare. Mesuré (6 cartes) : une très grande carte a 13 mines d'or,
  7 de bois, 6 de minerai, 3 ou 4 de chaque rare (une seule gemme avant) ; la ressource secondaire est à 11 pas du départ.
- Corrigé au passage : l'IA sans héros bâtissait avec l'or du héros à engager, chaque jour, et ne jouait plus ; elle
  engage maintenant un héros d'abord.
- Interface : la barre du haut montre les 7 ressources ; le royaume, une case par ressource (le nom dans l'info-bulle) ;
  un coût de la même quantité des quatre rares s'écrit « 3 de chaque rare ». Les images des nouvelles mines et des tas
  viendront de la session images (en attendant, des formes dessinées).
- Tests : `RessourcesTests`.
- ✅ **Le marché** (bâtiment commun, 500 or et 5 bois ; `Market`, `GameState.Trade`) : chaque ressource vaut 1 (or), 100
  (bois, minerai) ou 250 (rare) ; on vend à 20 % de la valeur avec un marché, 10 % de plus par marché, 60 % au plus, et on
  achète à la valeur divisée par la même part (un marché : le bois se vend 20 or et s'achète 500). Taux à revoir en
  jouant (l'utilisateur n'en est pas sûr). Fenêtre : donner, recevoir (avec le taux), combien, « Max ». L'IA le bâtit
  après l'habitation du niveau 3 et s'en sert quand un bâtiment attend une ressource, un bâtiment par jour au plus, en
  gardant 1000 or pour recruter (plus le prix d'un héros s'il en manque) : sans ces garde-fous, elle bâtissait chaque jour
  et son armée ne grandissait plus. Mesuré (six semaines, un adversaire) : Château 24 500, morts-vivants 24 800 ; Facile
  17 500, Normal 24 500, Difficile 30 100 en or d'armée. Tests : `MarcheTests`.
- ✅ **Les mines sur 3 × 2 cases**, comme Heroes 3 (choix de l'utilisateur, 2026-11-07) : l'entrée au milieu en bas, on y
  entre par la case juste en dessous, les 5 autres cases bloquent ; un clic sur la mine mène à l'entrée. Le générateur leur
  trouve la place (6 cases libres et la case devant l'entrée, qui peut être une route) ; les mines de départ, à 4 à 11 pas
  de préférence, plus loin si la zone est trop étroite. Sauvegarde version 3 ; les mines des sauvegardes d'avant gardent
  une case (`Mine.Large`). L'image couvre la surface, quelle que soit la taille des cases (images de 3 × 2 cases : session
  images ; en attendant, l'ancienne image carrée sur l'entrée, ou une colline dessinée).
- ✅ **Les villes sur 5 × 4 cases**, comme Heroes 2 (choix de l'utilisateur, 2026-11-08 ; 3 × 3 avant) : l'entrée au milieu
  de la rangée du bas, image de 160 × 128 pixels d'art (session images ; en attendant, l'ancienne image en bas au milieu).
  Le générateur cherche la ville, sa case d'approche et une case tout autour dans la zone ; la ville proche de chaque
  joueur est d'abord cherchée plus près de lui que des autres départs, dans chacune des zones voisines (sinon une
  ville trop grande pour sa zone lui manquait). Sauvegarde version 4 : les villes d'avant gardent 3 × 3 cases
  (`Town.Large`), et une sauvegarde le note pour chaque mine ou ville petite (sinon, sauvegardée de nouveau, elle
  grandissait). Mesuré : la simulation et l'adversaire comme avant (sur 18 autres cartes, Normal : 15 400 or d'armée
  contre 16 000 ; les 6 cartes des `Mesures` ont changé, leurs chiffres aussi : Normal 16 700 contre 23 700).
  **Les routes mènent à la porte** (demandé par l'utilisateur, 2026-11-08) : la route d'un départ part de la case devant
  la porte du château et contourne ses murs (avant, elle partait de côté et s'arrêtait contre le flanc) ; celle d'une
  ville neutre rejoint la route la plus proche ; un bout de route qui finit contre les murs d'une ville est retiré.
  Vérifié sur 420 cartes (`RoutesTests`).
- Corrigé au passage : le joueur sans héros qui perdait sa dernière ville n'avait pas perdu (la partie continuait sans
  lui, à l'infini) ; fin « Eliminated ».
- **À faire (IA)** : sa prévision d'un combat (jouée trois fois) se trompe dans les combats qui tournent vite (Fantômes qui
  grossissent, Loups qui frappent deux fois) : l'IA perd parfois son héros dans un combat jugé gagné.

#### La carte ✅ (premier passage)
Remarques de l'utilisateur (2026-11-07) : la carte paraît vide, traverser une forêt est déroutant. Fait (2026-11-08,
choix de l'utilisateur) :
- **Les forêts bloquent le passage** (comme Heroes 1 à 3 : les arbres sont des obstacles qu'on contourne) ; les murs
  entre zones sont de montagne, de forêt ou d'eau (35 / 35 / 30 %) ; moins de taches de montagne et de forêt.
- **Les coffres au choix** : 1000, 1500 ou 2000 or, ou 500 de moins en expérience (une fenêtre ; l'IA prend l'or sous
  5000).
- **Des lieux** (pour 40 × 30 cases, plus sur une grande carte) : 4 **autels** (+1 attaque, défense, puissance ou savoir,
  une fois par héros), 2 **cabanes du sage** (un sort des niveaux 1 et 2), une **roue à eau** (500 or par semaine) et un
  **moulin** (2 d'une ressource rare par semaine), une **tour d'observation** (dévoile 20 cases autour) ; **42 tas** au
  lieu de 28. Sauvegarde version 5. Tests : `CarteObjetsTests`.
  ✅ **Leurs images, et les lieux hauts sur 2 cases** (2026-11-12, branche `lieux-carte`, choix de l'utilisateur, comme
  Heroes 2 et 3 où les lieux sont plus grands qu'une case) : la cabane du sage, le moulin, la roue à eau, la tour
  d'observation et le temple occupent leur porte (où le héros entre) et la case au-dessus, qui bloque (`TallSite`) ; leur
  image de 64 pixels d'art de large déborde d'une demi-case de chaque côté, où le générateur ne pose rien. Les autels et
  le cercle des fées restent sur une case (32 pixels). Sauvegarde version 7 (les lieux d'avant gardent une case).
  Images PixelLab (`objects/shrine-*`, `sage`, `water-wheel`, `windmill`, `tower`, `temple`, `fairy-ring`) ; le moulin et
  la roue ne tournent plus (une animation, plus tard peut-être).
- Mesuré (`Mesures`) : la simulation gagne toujours (petites cartes jour 88, normales 86, grandes 154) ; un adversaire
  après six semaines : Facile 17 200, Normal 16 800, Difficile 22 900 en or d'armée.
- **Plus tard** : des **cavernes** où l'on apprend un talent (avec les classes) ou trouve un artefact.
- ✅ **Les gardiens justes** (2026-11-11, branche `gardiens`, demandé par l'utilisateur : les petites cartes ralentissaient
  à chaque faction) : la force d'une créature se mesure contre une vraie armée du Château, à la taille d'un gardien
  (`CreatureStrength.GoldToBeat`), et non plus par les paysans qu'il faut pour en vaincre une seule (un tireur seul
  paraissait bien trop fort : ses gardiens étaient minuscules) ; la force de référence de chaque niveau reste celle
  d'avant. Une seule créature du niveau 7 d'une faction était déjà bien plus forte que le gardien voulu (une Hydre,
  2,5 fois trois Dragons rouges) : le gardien prend alors la créature du niveau 6 de sa faction, à la même force. Mesure
  `Gardiens_par_creature` : dans chaque niveau, les gardiens demandent de 0,76 à 1,12 fois la même armée (0,3 à 2,5
  avant). Puis **les revenus doublés** (voir Repères). Mesuré (six cartes) : petites cartes jour 101 → 84 (gardiens)
  → **68** (revenus), la plus lente 164 → 90 ; normales 86 ; grandes 153 → 147 ; un adversaire après six semaines
  (Normal, or d'armée) : Château 57 100, morts-vivants 47 700, Barbares 54 700, Forêt 61 700, Tour 56 200, Donjon
  60 000, Enfer 65 200 (environ 25 000 avant : les adversaires sont deux fois plus riches).
- ✅ **Les montagnes en massifs, comme Heroes 3** (2026-11-17, branche `montagnes-pied`, demandé par l'utilisateur : des
  montagnes sans motif répété ni trous). Mesuré d'abord sur 50 cartes de Heroes 3 (`tools/cartes/montagnes_h3.py`) : une
  montagne y est un objet, 6 modèles par terrain (2 grands de 5 × 3 cases dont 12 bloquées, 4 petits de 3 × 2), aux
  cases bloquées en biais ; médiane de 39 montagnes pour 1000 cases de terre, 23 % de la terre bloquée, 90 % en chaînes,
  un quart des cases bloquées chevauchent une autre montagne. Chez nous : le générateur recouvre chaque tache de
  montagne de **massifs** qui se chevauchent (`MapGenerator.ShapeMountains`, `data/mountains.json`, `Core/Mountains.cs`),
  le plus couvrant d'abord, d'en haut à gauche ; un massif peut déborder d'une ou deux cases dans un creux du bord (jamais
  sur un passage, une route, l'eau, près d'un départ) ; le reste prend un sommet seul. Les cases de montagne sont
  exactement les cases bloquées des massifs (test `MontagnesTests`) ; ils sont sauvegardés (version 10 ; une sauvegarde
  d'avant dessine encore ses montagnes case par case). Les cases bloquées de chaque image viennent de l'image
  (`tools/art/empreintes.py`), avec les cases que le dessin couvre (pas de massif dont l'image couvrirait de l'eau). Puis,
  avec l'utilisateur, une longue suite d'essais d'images et de placement (même jour) :
  - **Pour qu'une chaîne se lise comme une chaîne** : d'abord les grands sommets (`cime`) au cœur des montagnes larges,
    puis des morceaux de chaîne (`chaine`, `etroit`), des contreforts (`petit`) au bord ; une forme d'une seule rangée
    seulement en dernier recours ; à couverture égale, pas la même image qu'un voisin ; un peu de chevauchement préféré.
    Les petites taches de montagne (moins de 6 cases) deviennent de la forêt (elle bloque aussi).
  - **Pour qu'elle se fonde dans le pays** (d'après une capture de Heroes 3 HD donnée par l'utilisateur : la montagne y
    est une surface de pentes, pas des objets posés) : des images sans contour, des pics de roche et des **pentes
    d'herbe sans arbres dessinés** (de tout petits arbres à côté de ceux de la carte : l'échelle n'allait pas) ; leurs
    verts sont ceux de l'herbe de la carte et leur bord se défait en trame (`tools/art/fondre.py`) ; pas d'ombre portée
    pour les montagnes ; des arbres de la carte à leur pied ; au cœur, le sol de la montagne assombri (`foothill_rock`) ;
    le pied du shader presque sans terre ni cailloux (`foothill` 0,35).
  - **Un jeu d'images par sol**, comme Heroes 3 : l'herbe, le sable, le marais (`ground` dans `mountains.json`,
    `MountainGrounds`) : les mêmes images, leurs pentes aux couleurs de chaque sol ; le sol le plus courant à deux cases
    décide, et le shader dessine ce sol sous la montagne.
  Essayés puis écartés : des sommets semés case par case (motif répété), des massifs de Retro Diffusion (des îlots), des
  pics pointus (« trop pointus »), larges en gradins (« trop carrés »), recolorés au style des mines (des morceaux de
  rocher côte à côte, « pas une vraie chaîne »), de grands sommets en blocs de rocher (« pas la bonne direction »).
  Puis le bord des pentes (branche `montagnes-bords`) : une trame régulière se voyait comme une ligne de points ; comparés
  avec l'utilisateur, un bord de plus en plus transparent (choisi), effrité par grappes, ou les deux ; et le trait sombre
  qui restait sur certains pics, adouci. Puis **les mines au flanc des montagnes** (idée de l'utilisateur, comme Heroes 3 ; branche `mines-montagnes`) :
  une mine creusée (toutes sauf la scierie) préfère une place où deux des trois cases juste derrière elle sont de la
  montagne (`MapGenerator.MountainBack`), dans les limites de chaque sorte de mine (à 4 à 11 pas du départ, près d'une
  ville neutre, ailleurs) ; une montagne seulement à côté ne suffit pas (essayé : la mine paraissait posée à côté).
  Mesuré sur les cartes 5, 12 et 42 : 23 mines creusées sur 45 adossées (test `MontagnesTests`), une sur dix avant.
  Une même graine donne une carte un peu différente d'avant.
- ✅ **Les montagnes au bord de deux sols** (2026-11-17, branche `montagnes-deux-sols`) : choisir le jeu d'images d'après
  le sol devant le pied de chaque massif ne suffisait pas (une image n'a qu'un sol : fausse d'un côté de la frontière). À
  la place, un shader (`shaders/mountain_slopes.gdshader`, sur `DecorLayer`) : chaque pixel de pente (vert franc dans
  l'image) prend la couleur du sol déjà dessiné à cet endroit, avec la lumière du pixel d'origine (`slope_relief` 0,6) ;
  les pentes se fondent ainsi dans l'herbe, le sable, le marais, une frontière, et la texture du sol continue sur elles.
  Les jeux du sable et du marais sont retirés (un seul jeu, celui de l'herbe) ; le sol sous les montagnes est celui qui
  les entoure ; plus d'assombrissement au cœur (`foothill_rock` 0). Vu en chemin : les bords du marais sont carrés (le
  shader du sol, sans lien avec les montagnes).
- ✅ **Un sol par zone** (2026-11-18, branche `terrains`) : mesuré avec `tools/cartes/terrains.py`, un sol forme une ou
  deux grandes régions dans Heroes 2 et 3, chez nous douze petites taches. Une zone de terre sur trois environ
  (`ZoneGroundShare` 0,35) devient du sable ou du marais ; il reste une tache sur quatre pour le décor (le hasard des
  autres est tiré quand même : la disposition de la carte ne change pas) ; six passes rendent les frontières irrégulières
  (le shader suit les rangées de cases). Le sol de chaque faction est dans `classes.json` (`ground`) : sable pour les
  Barbares et l'Enfer, marais pour les morts-vivants et le Donjon, herbe pour les autres (les Nains et quelques autres,
  à décider). Une ville neutre prend une faction de son sol, la zone d'un départ le sol de la faction du joueur, et un
  héros ne paie aucun surcoût sur le sol de sa classe (`Hero.OwnGround`). Mesuré (`Sols_des_cartes`, cartes normales,
  médianes) : herbe 91 → 74 %, sable 5 → 18 %, marais 5 → 12 % de la terre ; la plus grande tache de sable, 17 → 30 % du
  sable (les bords irréguliers laissent beaucoup de miettes : la taille médiane d'une tache ne dit plus rien).
  Armée d'un adversaire après six semaines (`Adversaires_par_faction`, moyenne des 8 factions) : 62 300 avant, 45 800
  avec les sols, 52 200 avec le sol de la faction sans surcoût (57 700 si sable et marais ne ralentissaient pas). Par
  faction : Donjon et Enfer −4 %, Nains −10, Barbares −11, Tour −17, morts-vivants −21, Château −29, Forêt −32 ;
  l'équilibre est remis à plus tard, quand d'autres sols viendront (choix de l'utilisateur).
- ✅ **Le sol rocheux** (2026-11-18, branche `sols-nouveaux`) : `TerrainType.Rock`, le sol des Nains, coût 125 (le sol
  accidenté de Heroes 3) ; une zone qui change de sol prend le sable, le marais ou la roche à parts égales. Deux cases de
  Retro Diffusion (`terrain/rock`), puis le shader : des dalles anguleuses allongées comme des strates, des fissures
  discrètes, un peu de pente sur chaque dalle, moins de grain fin (`slab_*`, `rock_grain`, `rock_cool`). Essayé et écarté
  par l'utilisateur : des fissures franches (de la boue séchée), des pierres (petites ou en gros blocs), une pente trop
  marquée (un pavage), des dalles arrondies (du cuir), une roche grise, bleutée ou à lichen (pour s'éloigner du futur sol
  brûlé de l'Enfer : c'est ce sol-là qui devra se distinguer, sombre, cendres et braises). Les combats sur la roche prennent le champ du désert en
  attendant le leur. Mesuré (cartes normales, médianes) : herbe 73 %, sable 11 %, marais 9 %, roche 10 % de la terre.
  Armée d'un adversaire après six semaines : moyenne 51 500 (52 200 avant) ; Nains 50 600 → 61 700, les premiers ; Tour
  47 000 → 40 400, les derniers.
- ✅ **La terre brûlée** (2026-11-18, branche `sol-brule`) : `TerrainType.Burnt`, le sol de l'Enfer, coût 100 comme la
  lave de Heroes 3 ; une zone qui change de sol en tire un parmi le sable, le marais, la roche et la terre brûlée. Les
  deux cases sont celles de la roche, recolorées d'après leur luminosité (de 0,085 / 0,065 / 0,06 à 0,33 / 0,27 / 0,245) :
  rien à payer. Le shader ajoute des nappes de cendre, des braises isolées et des fissures (`burnt_*`) : par carrés de
  44 pixels d'art, plus nombreuses par régions, une fente noire en zigzag à branches, large au milieu et fine aux bouts ;
  beaucoup de petites sans feu, quelques grandes où le feu passe du rouge sombre à l'orange, la lèvre du bas éclairée.
  Le feu et les braises **respirent** (cycle de 5 s, chacun à son rythme, par paliers tramés ; `burnt_glow_at` fige une
  capture). Essayé et écarté par l'utilisateur : des fissures entre plaques (Voronoï : des brindilles lumineuses, pas des
  fissures). Aucun écart d'images par seconde mesuré avec ou sans fissures (48 sur le PC de l'utilisateur, 45 environ
  avec toute la carte à l'écran). Les combats y prennent le champ du désert en attendant le leur. Mesuré (cartes normales,
  médianes) : herbe 74 %, marais 10 %, sable 9 %, terre brûlée 7 %, roche 4 % de la terre. Armée d'un adversaire après
  six semaines : moyenne 53 700 (51 500 avant) ; Nains 64 100, Barbares 62 200, Enfer 58 600, Forêt 55 400, Donjon
  55 100, Château 47 200, Tour 45 600, morts-vivants 41 500.
- ✅ **La neige** (2026-11-19, branche `sol-neige`) : `TerrainType.Snow`, le sol de la Tour (comme dans Heroes 3), coût 150
  comme dans Heroes 3 ; une zone qui change de sol en tire un parmi cinq. Mesuré dans les cartes de référence : la neige
  sur 35 cartes de Heroes 3 sur 50 (12 % de la terre, médiane) et 19 de Heroes 2 (19 %), en peu de grandes taches. Les
  deux cases sont celles du sable, recolorées en blanc bleuté de faible contraste (rien à payer). Le shader ajoute des
  congères rondes (`snow_*`) : une ombre bleutée du côté abrité, une crête un peu plus claire, quelques pixels qui
  scintillent ; trois fois moins de cailloux semés (`snow_stones` : sombres, ils se voyaient trop). Écartés par l'utilisateur : de longues congères en bandes (des nuages), une neige presque lisse. Les
  combats y prennent le champ du désert en attendant le leur. Mesuré (cartes normales, médianes) : herbe 74 %, roche 11 %, terre brûlée 8 %, neige 7 %, sable
  6 %, marais 6 % de la terre.
  Armée d'un adversaire après six semaines : moyenne 54 600 (53 700 avant) ; Tour 45 600 → 48 500 ; les
  autres bougent beaucoup (Forêt 70 000, Enfer 48 100, Château 42 000) : un sol de plus change le tirage des zones de
  chaque carte, donc les cartes mesurées.
- ✅ **Les forêts de chaque sol** (2026-11-19, branche `vegetation`) : une forêt pousse sur le sol qui l'entoure
  (`ForestGrounds`, comme les montagnes ; avant, une tache d'herbe sous chaque forêt) et prend les arbres de ce sol
  (`decor/tree-<sol>`, `DecorLayer.TreesOn`) : palmiers et acacias au vert olive dans le désert, en palmeraies plus
  clairsemées (trois arbres par case) ; cyprès, arbres morts et saules dans le marais ; arbres calcinés et souches fendues
  qui rougeoient sur la terre brûlée ; sapins enneigés et bouleaux nus dans la neige ; les sapins seuls sur la roche.
  Retro Diffusion, 48 candidats (2,88 $) ; écartés par l'utilisateur : des arbres brûlés fins et sombres (« des pixels,
  sans détail »), des palmiers vert vif trop serrés. Les ombres portées prennent la teinte de leur sol : bleutées sur la
  neige (`ShadowLayer.SnowShade`), noires ailleurs ; l'ombre de contact des arbres de la neige est bleue (`pieds.json`).
  Reste : les arbres seuls posés près des bâtiments et des mines, encore verts partout.
- ✅ **La scierie et la mine de minerai de chaque sol** (2026-11-19, branche `mines-sols`) : une image par sol
  (`objects/mine-wood-sand`…, `MapRenderer.MineImage` d'après le sol de l'entrée), leurs arbres verts remplacés par ceux
  du sol (un script : rien à payer ; les pieds repris de l'image de l'herbe). Les autres mines gardent leurs arbres :
  creusées, elles iront plutôt au flanc des montagnes (choix de l'utilisateur).

#### Les habitations sur la carte
Des lieux semés au hasard où un héros achète des troupes (comme dans Heroes). À concevoir : créatures, réserve et
croissance, prix, garde.

#### Les donjons à explorer (idée, plus tard)
Idée de l'utilisateur (2026-11-11) : un **donjon** marqué sur la carte, comme un vrai donjon de D&D ; le héros y entre,
le parcourt et en ressort avec une **grande récompense**. Façon préférée par l'utilisateur : **une petite carte à part**,
comme le souterrain de Heroes 3, que le héros parcourt case par case (couloirs, salles, gardiens, coffres, pièges, une
sortie). Écartées pour l'instant : une suite de salles dans une fenêtre (plus simple), un plan de donjon où l'on clique
la salle suivante. À concevoir avec la session règles : sa génération (`MapGenerator`), sa sauvegarde et son
brouillard, sortir et revenir ou non, les pertes d'une salle à l'autre, les récompenses (artefact, or, expérience,
créature rare), le nombre par carte et la difficulté selon la zone ; les créatures du Donjon en gardiens.

### Étape 10 : finitions
- Animations des créatures avec PixelLab (au fil de l'eau). ✅ Effets du combat dessinés par le jeu (projectiles,
  impacts, poison, drain, régénération, auras ; `tools/art/METHODE.md`). ✅ Les animations des sorts sur le champ
  (étape 9, `SpellEffects`). Reste : peut-être une animation « touché » par créature.
  ✅ **Une animation de mort par créature** (2026-11-17, branche `animations-mort`, demandée par l'utilisateur : un seul
  essai chacune, pour commencer à peu de frais ; 1,0 $ les 59 essais). Dans le jeu (`BattleFieldView.Kill`) : une troupe
  sans créatures joue sa feuille `<id>-death.png` une fois, reste couchée 0,7 s, puis s'efface en 0,6 s, sans retenir le
  combat (le nombre et les sorts ne s'affichent plus) ; une troupe ramenée pendant sa mort se relève ; sans feuille,
  elle disparaît comme avant. `--effect-preview=mort` (ou `mort:0.5`, figé) : toutes les troupes meurent en boucle.
  Images : l'état `death` de `creature.py` (cadre de la largeur de l'attaque, hauteur de l'attente), une consigne par
  gabarit et quelques-unes à part (os, pierre, magma, cristal, arbre ; `tools/art/METHODE.md`). Refaits : le Golem de
  pierre (ne s'effondrait pas) et la Gargouille (taches blanches).
  ✅ **Des morts variées** (2026-11-19, branche `morts-variees`, revues avec l'utilisateur en six pages de choix) : presque
  toutes tombaient en avant de la même façon ; il voulait tomber en arrière, sur le côté, un petit geste, et une mort
  propre à certaines créatures (ses idées). 40 des 51 refaites, 11 gardées (Paysan, Homme-lézard, Homme-grenouille,
  Aberration, Ver des profondeurs, Tyran œil, Hydre, Farfadet, Sanglier, Araignée, Chevaucheur de chèvre). Les morts
  propres : le Démon brûle puis tombe en cendre grise, la Liche disparaît et laisse la robe, la couronne et le bâton, le
  Roi de cristal s'évapore et laisse son marteau, le Moloch se défait en morceaux de bronze (tête à cornes devant ; les
  jambes ne se défont pas, accepté), le Golem tombe pierre à pierre, le Squelette et le Colosse d'os en tas d'os, le
  Fantôme se dissipe, le Vampire s'effondre dans sa cape, le Béhémoth rugit et tombe sur le dos. 137 essais,
  2,59 $. Retouches à la main (gratuites) : le bâton du Druide sans sa lueur, les gouttes du Sapeur, la fin du Démon (un
  corps calciné qui s'affaisse puis la cendre grise, au lieu d'un démon de nouveau entier), la robe de la Liche qui
  s'affaisse, la tête du marteau du Roi de cristal (essai écarté : le relever recourbait le manche ; refait avec de la
  place sous les pieds), la fin du Béhémoth (une suite animée depuis sa dernière image, collée). Méthode :
  `tools/art/METHODE.md`.
  ✅ **La mort et le coup reçu de l'Archer, du Griffon et du Cavalier** (2026-11-19, branche `morts-chateau`, leurs
  nouvelles images du Château) : trois morts différentes et deux coups reçus chacun, une page de choix, tout choisi au
  premier tour. L'Archer s'effondre face contre terre (les trois essais tombaient en avant, même celui demandé en
  arrière), le Griffon se cabre et s'affaisse, le cavalier du Cavalier tombe de selle puis le cheval s'écroule à côté.
  Une étincelle cyan effacée sur le coup reçu du Cavalier. 25 essais, 0,42 $ dont 0,17 $ perdus : les premiers essais
  de l'Archer et du Griffon partaient des anciennes grandes images, restées dans le dossier `art-candidats` du
  worktree (non suivi par git).
  ✅ **Les héros au combat** (2026-11-19, branche `heros-combat`) : le combat montrait l'image de la carte du héros
  (48 px, ×2). Désormais `MapRenderer.HeroBattleImage` prend `heroes/battle/<nom>.png` s'il existe (sinon l'image de la
  carte), aux couleurs du joueur comme sur la carte (`PaintPlayerColor`, commun aux deux) ; celui qui se défend, à
  droite, est retourné. Images : `tools/art/heros_combat.py`, un lot « Pro » de 4 (160 × 144, 0,12 $) par héros, à
  cheval sur la monture de l'image de la carte de sa faction (le bélier des Nains, le cheval aux flammes de l'Enfer),
  tourné vers la droite, à la taille du Cavalier (environ 125 px, l'étendard compris). Un héros par faction d'abord :
  tous choisis au premier tour par l'utilisateur, 0,93 $. Méphis écarté pour l'Enfer (sa peau rouge prendrait la
  couleur du joueur).
  ✅ **Un coup reçu par créature** (2026-11-17, même branche, demandé par l'utilisateur avant de juger les morts) : une
  troupe touchée qui survit (coup, tir, sort, poison, brûlure, foudre, nuage) et qui attendait joue sa feuille
  `<id>-hit.png` une fois (6 images à 12 images/s), puis revient à l'attente (`BattleFieldView.Hurt`) ; l'éclair blanc
  et l'effet du coup s'y ajoutent. `--effect-preview=coup` : toutes les troupes le jouent toutes les 1,5 s. Un essai
  d'abord sur 8 créatures, revu par l'utilisateur (gardés : le Paysan, le Roi de cristal, puis le Fantôme, le Troll et le
  Limon refaits « vers la gauche » ; l'Archer, le Loup, le Troll et le Griffon seront refaits entièrement), puis les 48
  autres (l'Ogre, l'Oiseau-tonnerre, le Djinn et l'Arbalétrier refaits pour des étincelles) ; environ 1,4 $ en tout.
  Méthode dans `tools/art/METHODE.md`. Revus avec l'utilisateur en six tours (2026-11-17) : gardés ou refaits avec ses
  remarques (plus de mouvement, en arrière, sans effets ; effets retirés à la main sur le Zombie, l'Oiseau-tonnerre,
  l'Archer elfe, le Tourmenteur, le Tyran) ; le Dragon, l'Hydre et le Démon, après des images clés refusées (corps
  raide, puis écrasé), par le second modèle de PixelLab (`minimax`) : le Démon surpris, puis content du coup.
  À la fusion de la refonte du Château, la mort et le coup reçu de l'Archer et du Griffon (faits d'après leurs anciennes
  images) ont été retirés : 54 créatures ont les deux ; l'Archer, le Griffon et le Cavalier restent à faire.
  ✅ Les options du combat (bouton Options, touche O) : la vitesse des projectiles (lente, normale, rapide), gardée
  d'une partie à l'autre dans `user://reglages.cfg` (`Settings`).
- ✅ **Des cases de 32 pixels d'art** (au lieu de 24 ; décision de l'utilisateur, 2026-11-08) : cases de 64 pixels à
  l'écran (×2), zooms ×2 et ×3 ; chaque image de la carte à ×2 exactement, posée en bas et centrée (`MapRenderer.DrawArt`) ;
  les images de 32 pixels viennent de la session images (`graphismes-suite-4`).
- **Des icônes plutôt que du texte sur la plupart des boutons**, pour gagner de la place (demandé par l'utilisateur,
  2026-11-08 ; le nom dans l'info-bulle).
  ✅ **Au combat** (2026-11-09, branche `boutons-combat`) : la barre du bas comme Heroes 3, à gauche Options, Fuir, Auto,
  au milieu le round et l'ordre de jeu (plus long), à droite Sorts, Attendre, Se défendre (Rester, Commencer) ; des boutons
  carrés de pierre, une icône d'argent incrustée dans la pierre (PixelLab, 32 × 32, ×2, choisies par l'utilisateur), le nom et la touche en
  info-bulle. ✅ **Le panneau de la carte** aussi (2026-11-09, branche `icones-carte`) : deux rangées de quatre (Suivant,
  Avancer, Centrer, Royaume / Creuser, Trésor, Sorts, Options), « Fin du tour » garde son texte (choix de l'utilisateur).
  Reste : les autres écrans, au fil des besoins.
- ✅ **Choisir le côté de l'attaque**, comme Heroes 3 (demandé par l'utilisateur, 2026-11-10, branche `attaque-direction`) :
  la partie de la case ennemie sous la souris donne le côté d'où la troupe frappe (un côté impossible passe au plus
  proche) ; l'épée du curseur pointe vers la cible depuis ce côté (six images, l'épée C15 choisie par l'utilisateur,
  retournée, redessinée à plat pour la droite et la gauche) ; la case d'arrivée est mise en valeur. On vise une créature aussi par son corps, qui dépasse de sa case (sa tête : le haut, ses pieds : le bas). Un tir montre une flèche (la 15, choisie par l'utilisateur ; PixelLab), brisée quand il est trop lointain et ne fait que moitié des dégâts (`TacticalBattle.IsLongShot`), comme Heroes 3.
- **Les barres de défilement** : la poignée comme taillée dans la pierre (demandé par l'utilisateur, 2026-11-08).
- **Harmoniser l'interface**, comme Heroes 1 et 2 dessinés sur une seule grille (demandé par l'utilisateur, 2026-11-11,
  après le passage à ×2 ; branche `interface-harmonie`). ✅ 1. Les tailles communes (`Ui.cs`) : toutes les longueurs
  sur la grille des pixels d'art (2 pixels de l'écran) ; une nouvelle police, **Pixel Operator HB 8** (choix de
  l'utilisateur parmi six essais : VT323 nette était trop petite), nette à 16 (un pixel de police = un pixel d'art),
  les titres en Pixel Operator HB à 32 ; trois hauteurs de bouton (petit 32,
  ordinaire 44, grand 60), boutons à icône de 80, « Fermer » de la même taille en haut à droite des grandes fenêtres ;
  l'écran s'assombrit aussi derrière les petites fenêtres. ✅ 2. Écran par écran pour la nouvelle police (branche
  `interface-ecrans`) : les listes de la colonne de la carte en portraits comme Heroes 2 (deux par rangée, barres de
  mouvement et de points de sort, le détail au survol), la fiche du héros, les cartes de recrutement (l'écran de
  recrutement sera encore revu pour gagner de la place, choix de l'utilisateur). Reste : 3. des icônes pour les
  récompenses (coffre, niveau), le livre de sorts en image, la ville.
- ✅ **Le champ de bataille de l'herbe** (2026-11-11, branche `champ-de-bataille`, d'après des captures de Heroes 1 à 3
  données par l'utilisateur) : le sol du shader de la carte (taches de terre battue, ombres et éclaircies), deux décors tirés
  à chaque combat, la **lisière d'une forêt** (rangées d'arbres, grands troncs au fond, buissons devant, ombre sous les
  arbres) ou un **paysage ouvert** (ciel, collines lointaines, petits arbres vus de côté, arbres moyens, brume vers
  l'horizon), des bords de buissons et d'arbres, des détails semés (cailloux, fleurs, touffes, racines), de nouveaux
  obstacles (chênes, rochers, souches, troncs couchés), les ombres de contact de la carte (pieds revus par
  l'utilisateur, opacité et couleur par image). ✅ **Le désert** (2026-11-11, branche `champ-sable`, un Sahara, pas un
  désert américain : choix de l'utilisateur) : toujours le paysage ouvert (ciel pâle, mesas, dunes une à une dont la face
  à l'ombre a un bord en biais), des dunes en relief sur le champ, un sol de marques légères d'après des références de
  l'utilisateur (rides de vent, traînées de points, fissures, touffes, cailloux de couleurs mêlées, en taches irrégulières
  aux couleurs nuancées), grès, grosses souches et troncs blanchis, arbustes, buissons épineux, palmiers. ✅ **Le
  marais** (2026-11-16, branche `champ-marais`) : un bayou à la lisière, une tourbière au loin, des flaques où l'on
  marche. Reste : peut-être des rochers dans les collines et quelques nuages.
- Le héros sur la carte et au combat ; cadres des portraits, bandeau de la date. ✅ **Le héros du Château sur la
  carte** (2026-11-14, branche `heros-carte`, choix de l'utilisateur) : une image par faction, 48 × 48, gauche et droite,
  une marche de 8 images avec un rebond, un pas de 3 cases par seconde avec un élan par case. ✅ **La marche dans les
  8 directions** pour le chevalier (2026-11-15, branche `heros-carte-factions`, choix de l'utilisateur) : 5 directions
  dessinées (sud, sud-est, est, nord-est, nord), l'ouest en miroir ; le héros regarde vers son dernier pas. ✅ **Le
  nécromancien des morts-vivants** (2026-11-15, branche `heros-carte-morts-vivants`, choix de l'utilisateur : un
  nécromancien encapuchonné de violet sur un cheval mort-vivant pâle). ✅ **Le chef des Barbares** (2026-11-15, branche
  `heros-carte-barbares` : casque à cornes, cape de fourrure, sur un cheval brun). ✅ **Le rôdeur de la Forêt**
  (2026-11-15, branche `heros-carte-foret` : cape verte, arc long, cheval alezan) ; l'utilisateur veut **l'est et l'ouest
  vus de côté** et **le sud-est et le sud-ouest plus tournés vers le sud** (pour les factions suivantes aussi). ✅ Refaits
  ainsi (2026-11-15, branche `heros-carte-profils`) : l'est et le sud-est du nécromancien, l'est du chef barbare (cape
  claire) ; **le sud-est du chef barbare reste tel quel** pour l'instant (aucun essai juste : trop de face ou pas assez,
  choix de l'utilisateur). ✅ **Le magicien de la Tour** (2026-11-15, branche `heros-carte-tour` : longue barbe blanche,
  robe bleue et or au capuchon pointu, bâton au cristal bleu, cheval blanc). ✅ **Le démoniste de l'Enfer** (2026-11-15,
  branche `heros-carte-enfer` : petites cornes, robe noire aux runes orange, bâton de feu, grand cheval noir à la crinière
  de feu ; le feu orange, seul l'étendard rouge). ✅ **Le Seigneur des abîmes du Donjon** (2026-11-15, branche
  `heros-carte-donjon` : armure noire hérissée, cape violette, heaume d'argent à cornes, bâton à l'orbe violet, grand cheval
  gris sombre ; son sud-est reste de trois quarts pour l'instant, choix de l'utilisateur). ✅ **Le thane des Nains**
  (2026-11-15, branche `heros-carte-nains` : armure d'acier et d'or, heaume ailé, barbe tressée, marteau aux runes bleues,
  sur un grand bélier des montagnes). Les 8 factions ont leur héros. Reste : refaire le sud-est du chef barbare et du
  Seigneur des abîmes (tournés vers le sud), une image de combat par héros. ✅ Les pieds des 49 images des héros revus
  par l'utilisateur, pour l'ombre de contact (2026-11-15, branche `heros-pieds`).
  ✅ **Les diagonales partout sur la carte** (2026-11-14, branche `diagonales`, très important pour l'utilisateur) : les
  bords des terrains suivent les centres des cases (un escalier devient une ligne à 45°, un coin s'arrondit), les routes
  vont en diagonale, les murs entre zones serpentent. (Icônes des ressources : faites, 2026-11-07.)
- **La carte, à revoir** (demandé par l'utilisateur, 2026-11-14, à concevoir ensemble) : des graphismes, la répartition
  des objets ; les **tours d'observation loin des bords** ; des **zones fermées** qu'on n'ouvre qu'en battant une grosse
  armée (pour ne pas conquérir une autre zone trop tôt ; peut-être une option) ; **une ville au plus par zone**, chaque
  ville dans sa zone ; **une scierie et une mine de minerai près de chaque ville, toujours, gardées par une petite
  armée** ; des **cartes plus grandes**, d'après Heroes 1, 2 et 3. Vu aussi : deux routes côte à côte forment parfois
  une « échelle ».
  ✅ **Fait (2026-11-14, branche `carte-zones`, détail dans `HISTORIQUE.md`)**, décidé avec l'utilisateur : les passages entre zones selon une **option de la nouvelle partie**
  (ouvertes : l'armée de départ sort, comme avant ; normales : environ une semaine d'armée ; fermées : deux à trois
  semaines ; normales par défaut, la force exacte à voir en jouant) ; une ville au plus par zone ; une scierie et une mine
  de minerai près de chaque ville, gardées par une petite armée, **sauf celles de la ville de départ** (sans gardien,
  comme avant) ; les tours d'observation loin des bords ; **les tailles de Heroes 3** (36, 72, 108, 144 cases de côté,
  carrées, plus une toute petite pour les tests) ; plus d'« échelles » de routes. **La victoire** (choix de
  l'utilisateur, 2026-11-14) : « vaincre tous les monstres » est retiré (il n'a plus de sens sur ces grandes cartes) ; par
  défaut, **prendre toutes les villes**, comme Heroes 3 ; **les parties longues sont acceptées** (la simulation seule
  prend toutes les villes de la normale vers le jour 210).
  Puis (2026-11-14, remarques de l'utilisateur) : ✅ les tours d'observation à 15 cases des bords et à 30 cases l'une de
  l'autre. **À revoir ensemble** : un problème du moulin (à en parler).
  ✅ **Les rivières et les ponts (2026-11-15, branche `rivieres-ponts`, détail dans `HISTORIQUE.md`)**, en deux tours
  avec l'utilisateur : **deux sortes**, une couche posée sur le terrain comme la route (`RiverKind`). **Les rivières**
  bloquent : chacune va d'un bord de la carte à un autre (ou jusqu'à la mer) en suivant les frontières des zones ; on ne
  les passe que sur un **pont** gardé. **Les ruisseaux**, dans une zone, se traversent (un décor). Dessinés par le
  shader en courbes douces, avec la plage des lacs. ✅ **Les ponts en images** (2026-11-15, branche `ponts`, détail dans
  `HISTORIQUE.md`) : de bois ou de pierre selon les routes de la zone (pierre là où elles sont pavées), dans les deux
  sens, et un petit pont où une route croise un ruisseau. ✅ **Les rives** (2026-11-15, branche `rives`, choix de
  l'utilisateur, détail dans `HISTORIQUE.md`) : toutes les rives (lacs, mer, rivières) changent par tronçons (herbe
  jusqu'à l'eau bordée de terre mouillée, plages de sable plus larges, plages de galets), l'eau claire près du bord et
  plus sombre au large, des roseaux sur les rives herbeuses, quelques rochers dans l'eau peu profonde. **À continuer**
  (l'utilisateur) : les rives seront encore travaillées. **Revoir les routes ensuite**. **Plus tard** : redessiner le petit pont de bois d'ouest en est (des marques sur sa face avant) s'il gêne en
  jouant.
  ✅ **Les routes, premières règles (2026-11-15, branche `routes-sans-issue`, détail dans `HISTORIQUE.md`)** : leurs règles se définissent avec l'utilisateur, en
  plusieurs tours (une page de cartes entières, ses remarques, des retouches). Décidé : plus de route qui ne mène nulle
  part (chaque route arrive à une ville, une mine, un port) ; une petite route vers les mines proches des villes et vers
  les ports ; les routes serpentent ; les villes neutres bien réparties (chacune dans la zone la plus éloignée des
  villes déjà posées, un peu au hasard parmi les meilleures). Plus de passages entre les zones (chaque zone au moins deux,
  d'autres au hasard jusqu'à trois). D'après 160 cartes de Heroes 2 et 3 (`tools/cartes/`, mesurées le 2026-11-15 : la
  route y couvre 3 à 9 % de la terre, 13 % chez nous ; 1,3 à 1,5 villes pour 1000 cases de terre) : le réseau principal
  est un arbre entre les villes, les mines des villes et les ports, de courts sentiers mènent aux autres mines (5 cases)
  et aux lieux (3 cases) ; au plus 1,5 villes pour 1000 cases de terre (chaque joueur garde sa ville voisine). Mesuré :
  routes 7,5 % de la terre (normale), 8,4 % (grande), 5 % (îles), 11,6 % (petite : 4 villes sur peu de terre). Une route dans les zones sans ville et la quantité de routes : à revoir avec des zones plus variées.
  **Les sortes et le dessin des routes (en cours, 2026-11-15, branche `routes-suite`)**, avec l'utilisateur. D'après 43
  cartes de Heroes 3 (`tools/cartes/sortes_routes_h3.py` : terre 37 %, gravier 31 %, pavés 33 % ; une carte emploie
  surtout une sorte, en longs morceaux ; la sorte dépend peu de ce que la route relie) : **une sorte par zone**
  (`RoadKind`, sauvegarde version 9), celle de la faction de sa ville (pavés : Château, Tour, Nains ; gravier :
  morts-vivants, Donjon, Enfer ; terre : Barbares, Forêt), sinon terre ou gravier au hasard. Images Retro Diffusion
  choisies en jeu (terre 3, gravier 2, pavés 1 ; Retro Diffusion bien meilleur que PixelLab pour ces cases). Le dessin :
  des routes plus fines (de 7 à 9 pixels d'art, une largeur qui change vite), un bord éparpillé (la route et l'herbe se
  mêlent pixel par pixel), une ombre douce du côté du soleil et un liseré de soleil sur l'herbe d'en face (légers). De
  petits cailloux de tailles variées le long des bords (peu), quelques-uns sur la route, et d'autres semés sur le sol,
  loin des routes ; leur ombre suit la lumière de la carte (comme les petits cailloux de l'herbe, corrigés).
  Écartés : le cadre de terre sombre de Heroes 3 (« pas naturel »), la terre battue posée ou multipliée (des taches,
  pas des formes ; le code reste, éteint). ✅ L'herbe qui déborde sur la
  route (2026-11-15, branche `routes-herbe`, choix de l'utilisateur) : par plaques et en touffes, sans herbe sur
  certains tronçons, envahie sur d'autres. ✅ Le bord sur le sable (2026-11-15, branche `routes-sable`, choix de l'utilisateur) : le sable
  recouvre un peu le bord, un bourrelet clair à côté. ✅ Une revue de six cartes (2026-11-15,
  `art-candidats/archives/2026-11-15-routes-revue/`) : les routes vont bien ainsi (choix de l'utilisateur) ; elles seront
  revues quand les zones seront plus variées. **Plus tard** (idées) : une route
  enneigée (avec un sol de neige) ; un coût de marche par sorte comme Heroes 3 (pavés 50 %, gravier 65 %, terre 75 %) ;
  une entrée pavée devant chaque ville (Heroes 2) ; des ornières ou des flaques dessinées sur la terre ; **le shader de
  l'herbe sera revu par l'utilisateur** (le bord des routes prend la couleur du sol, il suivra).
  ✅ **Plus de villes sur les grandes cartes** (2026-11-14, demandé par l'utilisateur) : 14 noms de villes les limitaient à
  14 ; avec 40 noms, une ville pour trois zones : grande carte 17 à 20 villes, très grande 30 à 33 (avec les châteaux).
- **La mer** (conçue avec l'utilisateur, 2026-11-14, comme Heroes 3) :
  - **Les bateaux** : un **chantier naval** (bâtiment des villes au bord de l'eau, et lieu sur les côtes de la carte) en
    construit un (or et bois), qui apparaît sur l'eau à côté ; et **quelques bateaux déjà amarrés** sur les côtes.
  - **Monter à bord ou débarquer prend tout le mouvement qui reste** au héros ; en mer, il va à sa vitesse ordinaire.
  - **Les types de cartes** (option de la nouvelle partie) : **continent** (aujourd'hui), **quelques mers** (tout
    s'atteint aussi à pied), **îles** (chaque joueur sur son île, des îles neutres), **archipel** (beaucoup de petites
    îles, tout en bateau).
  - **Sur l'eau** : épaves flottantes et coffres de la mer ; bouées (+1 moral) et sirènes (+1 chance) ; tourbillons
    (téléportent le bateau, coûtent une part de la troupe la plus faible) ; phare (les bateaux de son propriétaire vont
    plus loin) ; **des navires abandonnés gardés par des morts-vivants**, un trésor à bord ; peut-être **des pirates**,
    les monstres de la mer (idées de l'utilisateur).
  - Dans l'ordre : 1. ✅ les règles (2026-11-14, branche `mer` : bateau, embarquer, naviguer, chantier naval des villes
    et de la côte, l'IA qui prend un bateau amarré ; le bateau et le chantier dessinés par le jeu en attendant leurs
    images) ; 2. ✅ les types de cartes (2026-11-14, branche `types-cartes` : liste « Carte » de la nouvelle partie,
    continent par défaut ; des mers, îles, archipel ; un chantier naval et un bateau amarré sur l'île de chaque joueur,
    après la sortie de sa zone de départ ; des villes au bord de la mer ; l'IA achète un bateau quand elle n'a plus rien
    à faire à pied ; sur les îles et l'archipel, une ville au plus par île, choix de l'utilisateur) ; 3. les objets de la mer, en deux parties (choix de l'utilisateur, 2026-11-14 : un combat en mer se joue sur le champ
    ordinaire en attendant un pont de navire avec les images ; un tourbillon coûte quelques créatures ; la Navigation donne
    du mouvement en mer et des bateaux moins chers ; les pirates sont des créatures neutres) : 3a. ✅ épaves flottantes,
    coffres de la mer, bouées, sirènes, tourbillons, phares, la compétence Navigation (2026-11-14, branche `objets-mer`) ;
    3b. ✅ les navires abandonnés (un équipage de morts-vivants), les pirates (une seule créature neutre pour l'instant ;
    une faction « pirates et monstres de la mer » peut-être plus tard : choix de l'utilisateur, 2026-11-15), les gardiens
    de la mer (2026-11-15, branche `mer-pirates`) ; 4. les images
    (session images). À voir en chemin : la compétence
    **Navigation** et les **combats en mer** (un pont de navire comme champ de bataille).
- **La carte passe en cases de 32 px** (décidé avec l'utilisateur, 2026-11-07, après un essai 24 / 32 px) : zooms ×2 et
  ×3, mines sur 3 × 2 cases (96 × 64). Images refaites et revues avec l'utilisateur en deux passages (pages de choix
  `art-candidats/en-cours/revue-<lot>.html`, faites par `tools/art/revue.py`, 2026-11-08) ; le code (taille des cases,
  shader, décor) vient de l'autre session (branche `tuiles-32`), les images fusionnent juste après. Le sol des forêts et
  des montagnes est de l'herbe (`terrain/forest.png` et `mountain.png` retirées). Reste :
  - ✅ **l'herbe et les rives** (2026-11-09) : un vert plus profond et naturel, des fleurs et des cailloux semés par le
    shader (sans répétition), des plages de sable le long de l'eau (8 pixels d'art, sable mouillé contre l'eau). Réglables
    avec `--ground=`. Plus tard : **un style de sol par zone de faction** (comme Heroes 3 : herbe fleurie pour le Château,
    herbe sèche et rocailleuse pour les Barbares, herbe morte pour les morts-vivants…) ; le générateur sait déjà la zone
    et la faction de chaque ville (session règles).
  - le **héros** de la carte : voir plus haut.
  Fait (2026-11-08) : ✅ les **villes sur 5 × 4 cases** (160 × 128, PixelLab « Pro » d'un seul coup, vues de face : bien
  meilleur que l'assemblage de pièces ; l'empreinte et le drapeau viennent de l'autre session, PR #76 et #77), ✅ la
  mine de soufre (entrée plus grande), ✅ le squelette et le colosse d'os (PixelLab « Pro », l'image de combat comme
  sujet), ✅ sommets fondus dans le terrain (`tools/art/fondu.py`).
  ✅ **Ombres de la carte** (2026-11-08, dessinées par le jeu) : ombre portée vers la gauche et ombre de contact au pied
  de chaque objet, d'après des pieds revus par l'utilisateur (`godot/art/pieds.json`, `tools/art/pieds.py`) ; scierie
  refaite (cabane de trois quarts, PixelLab). Détail dans `HISTORIQUE.md`. Reste : vérifier les pieds des mines de
  mercure, minerai et soufre, et la force de l'ombre de contact en jeu.
  ✅ **Ville barbare sur la carte** (2026-11-09) : forteresse de pierre brute avec une tente de peau ; contour noir adouci
  (aussi sur la ville des morts-vivants). La ville vue de face par faction reste à faire (voir plus bas).
  ✅ **Créatures barbares** (2026-11-09, branche `creatures-barbares`) : Nomades (2 cases), Oiseau-tonnerre (1 case, vol
  avec envol et atterrissage), Béhémoth (1 case, debout) : images de combat, animations et images de la carte. Détail
  dans `HISTORIQUE.md`.
  ✅ **Foudre sur la case de la cible** (2026-11-09, branche `foudre-cible`) : à chaque coup de l'Oiseau-tonnerre, un
  éclair bleu tombe du ciel sur la troupe touchée (un second, plus fort, quand il appelle la foudre), dessiné par le jeu (`BattleEffects.OnHex` : un effet joué sur une case, à réutiliser pour les sorts) ;
  l'Oiseau-tonnerre attaque ailes déployées, sans éclair dessiné. Détail dans `HISTORIQUE.md`.
  ✅ **Combat à ×2** (2026-11-09, branche `combat-x2`, choix de l'utilisateur) : un pixel d'art fait 2 pixels de l'écran
  au combat comme sur la carte (×3 avant). Les créatures sont refaites 1,5 fois plus hautes (plus de détails, même
  taille à l'écran), sans dépense depuis leurs grandes images et les images brutes gardées dans les archives ; l'Ange
  (androgyne) et le Squelette regénérés ; les effets gardent leur taille (unité `BattleEffects.U`). Reste : l'Épéiste et
  le Cavalier (plus petits, à refaire plus tard), de vrais obstacles de combat (aujourd'hui les sommets et sapins de la
  carte). Détail dans `HISTORIQUE.md`.
- **La ville vue de face par faction** (conçue avec l'utilisateur, 2026-11-07, rien n'est encore fait) : les règles
  (`buildings.json`) ne gardent plus la place des bâtiments ; chaque faction a sa **scène** (`art/towns/<faction>/` :
  un fond de 640 × 195 affiché ×3, et `scene.json` : pour chaque bâtiment, son pied (bas, milieu), sa profondeur et son
  image `<id>.png`). Chaque amélioration a sa propre image au même pied. Bâtiments dessinés de l'arrière vers l'avant
  selon leur pied ; clic et survol sur les pixels de l'image (formes irrégulières), léger contour au survol. Chaque
  faction dessine et place aussi les bâtiments communs (taverne, hôtel de ville, guildes) ; un test vérifie que la
  scène de chaque faction couvre tous ses bâtiments. Choix de l'utilisateur : une place vide ne montre **rien** (le
  terrain et les chemins) ; **un seul fond** par faction (pas selon le terrain) ; place pour **au moins 25 bâtiments**
  par faction ; petits détails animés (fumée, drapeaux…) plus tard. Images : un concept de la ville entière d'abord
  (référence de disposition et de style), puis le fond, puis chaque bâtiment séparément avec le fond comme référence ;
  une page HTML pour placer les bâtiments à la souris et écrire `scene.json`. La ville sur la carte change déjà selon
  la faction (fait dans une autre session).
- **Une image de combat par héros** (demandé par l'utilisateur, 2026-11-07) : le héros dessiné sur le côté du champ
  (aujourd'hui son image de la carte ×4), qui ressemble à son portrait (PixelLab, le portrait comme référence) ; à
  cheval ou non selon le héros ; plus tard ses animations (lancer un sort, être touché).
- Entraîner les profils de difficulté par des parties entre IA.
- Petits détails et équilibrage fin.

#### Les sons et la musique (à concevoir)
Commencé avec l'utilisateur (2026-11-15) : le jeu n'a encore ni son ni musique. Une première liste, à revoir ensemble.
- **Les musiques** (longues, 1 à 3 minutes, qui bouclent ; 20 à 30 en tout) : le menu ; la carte, une par terrain (4 à
  8) ; **une par ville, donc par faction** (8 : comme Heroes, chaque ville a son humeur) ; le combat (2 ou 3) ; de
  courts airs (5 à 10 secondes) pour la victoire, la défaite, un niveau gagné, une nouvelle semaine.
- **Les sons** (courts, moins de 2 secondes ; 150 à 250) : l'interface (clic, survol, fenêtres, page qui tourne,
  construire, recruter, l'or) ; la carte (pas du héros, ramasser, prendre une mine, monter en bateau) ; **chaque créature
  au combat** (attaque, coup reçu, mort, et pour certaines marche ou tir : le plus gros du travail) ; chaque sort (18).
- **Les ambiances** (longues boucles discrètes) : le vent, la mer, les oiseaux de la forêt, la lave de l'Enfer…
- **D'où les prendre** (à essayer et comparer avant de produire en nombre) : des bibliothèques libres (Freesound CC0,
  Kenney, OpenGameArt, les lots Sonniss de la GDC) pour l'interface, les pas et les ambiances ; des sons générés (IA)
  pour les créatures et les sorts ; de la musique générée (IA) si elle plaît, ou des lots payants. **Noter la licence de
  chaque fichier.**
- **Dans le jeu** : les sons suivent les **événements** des règles (`TakeEvents()`), comme les animations ; une classe
  `Audio` dans `Game/` (bus Musique, Effets, Ambiance ; volumes dans les Options, gardés dans `Settings`) ; un fichier de
  données (`godot/data/sounds.json` : un événement, ou une créature et son action, vers un ou plusieurs fichiers, tirés au
  hasard pour ne pas répéter) ; la musique passe en fondu entre carte, ville et combat. Les outils dans `tools/audio/`
  (comme `tools/art/` : générer, une page d'écoute pour choisir, mettre au même volume).
- **Dans l'ordre** (proposé) : 1. ✅ la base technique (2026-11-15, branche `sons`) : `Game/Audio.cs` (musique en fondu
  et en boucle, qui survit au changement de scène ; un clic pour chaque bouton ; les sons des événements de la carte :
  ramasser, coffre, prendre une mine ou une ville, construire, niveau, nouvelle semaine, victoire, défaite), les volumes
  dans les trois fenêtres d'options, `data/sounds.json`, des sons et musiques d'attente synthétisés par
  `tools/audio/attente.py` (`godot/audio/attente/`, à remplacer ; test `SonsTests`) ; 2. un essai :
  les sons d'un combat (les créatures d'une faction, quelques sorts) et une musique de combat, de 2 ou 3 sources côte à
  côte sur une page d'écoute, l'utilisateur choisit à l'oreille ; 3. le reste partie par partie, comme les images.

#### Repères : l'équilibre mesuré du 2026-11-07

- **Équilibre mesuré** (`Mesures`, 2026-11-07, six cartes, avec les ressources rares et 1,5 mine par zone) : la simulation
  seule (Château, sans magie) gagne sur les petites cartes vers le jour 73, les normales vers le jour 88, les grandes vers
  le jour 157 (plus de mines, donc plus de gardiens à vaincre). Un adversaire après six semaines : Château 19 400, morts-
  vivants 20 200 en or d'armée ; Facile 13 500, Normal 20 700, Difficile 23 200. Courbe : Archer 8, Griffon 14, Épéiste
  26, Moine 63, Cavalier 116, Ange 1643 paysans.

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
