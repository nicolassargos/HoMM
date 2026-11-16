# Plan du jeu

Une direction, pas un contrat : les idées peuvent changer.
On avance étape par étape, sans se presser : chaque étape est jouable et testée avant la suivante.

Ce fichier garde la direction, ce qui reste à faire et les choix encore valables. Le détail du travail fini (règles
décidées, chiffres mesurés, essais écartés, classes et fichiers) est dans `HISTORIQUE.md` : quand un sujet se termine,
y mettre son détail et ne garder ici qu'une ligne ✅. La méthode des images est dans `tools/art/METHODE.md` ; les
chiffres en vigueur sont dans `godot/data/`.

## L'intention
- **Jeu de stratégie façon Heroes** (et King's Bounty pour le charisme) : villes, économie, recrutement chaque semaine,
  armées, combat tactique sur hexagones, adversaires joués par l'ordinateur.
- **Cartes aléatoires**, rejouables (`MapGenerator`), de 5 tailles.
- **Graphismes** : pixel art façon premier Heroes, PixelLab (personnages, créatures, interface) et Retro Diffusion
  (cartes, portraits).
- **Projet personnel**, sans échéance : on privilégie le plaisir et la simplicité.

## Les étapes

1. ✅ Armées, monstres neutres (zones de contrôle, croissance chaque semaine), combat automatique.
2. ✅ Villes : bâtiments (`buildings.json`), un par jour, revenus, recrutement, taverne.
3. ✅ Progression du héros : expérience, niveaux, 5 caractéristiques dont le **charisme**.
4. ✅ Parties complètes : conditions de victoire (cumulables), trésor enfoui et obélisques, générateur v2 (zones et
   passages gardés), sauvegarde exacte (le hasard compris).
5. ✅ Graphismes de la carte et de l'interface (terrains par shader, décor, curseurs, zoom, routes, portraits).
6. ✅ Combat tactique (11 × 9 cases aplaties comme Heroes 3, grandes créatures sur 2 cases, capacités, combat rapide
   joué par l'IA) ; créatures refaites une à une avec PixelLab.
7. ✅ Refonte de l'interface (1920 × 1080). **Reste** : une nouvelle revue de toutes les captures avec l'utilisateur,
   quand les écrans qui manquent seront là (ville en images, artefacts du héros).
8. ✅ Adversaires IA (1 à 3, mêmes règles, pas de triche), héros contre héros, se défendre soi-même, 3 niveaux de
   difficulté.
9. **Contenu (en cours)** : magie, factions, héros, compétences, artefacts, carte, mer (ci-dessous).
10. Finitions : apparence et détails, **les sons et la musique** (ci-dessous).

## Étape 9 : contenu

### La magie
✅ Fait : les règles (proche de Heroes 2 et 3 : points de sort, puissance, guilde de 4 niveaux, un sort par round), les
18 sorts de combat (`data/spells.json`), le livre de sorts en image (le lutrin dans l'étude du héros et dans
l'académie de chaque faction, le livre ouvert, la page qui tourne), une image et une animation par sort.
**Reste** :
- **L'équilibre force / magie**, reporté aux classes (choix de l'utilisateur) : d'abord apprendre à la simulation à
  **ramasser les tas de ressources** (sans eux, la guilde est bâtie trop tard), puis comparer les classes
  (`Simulation_force_ou_magie`).
- **Plus tard** : versions « de masse », un 5e niveau (Armageddon, élémentaires, morts relevés), sorts de la carte
  (portail de ville, porte dimensionnelle, vision…). Écartés pour l'instant : berserk, hypnotisme, paralysie.

### Les factions
**8 factions jouables + 1 neutre** (décidé le 2026-11-08). Chacune a deux ressources rares : la **principale** pour
l'habitation et la créature du niveau 7, la **secondaire** pour les niveaux 5 et 6. Méthode (choix de l'utilisateur) :
la philosophie, les forces et les faiblesses d'abord, puis les créatures niveau par niveau. Le détail de chacune est
dans `HISTORIQUE.md` (« Les factions : conception et chiffres »).

| Faction | Marque | Classe (talent) | Niveau 7 → amélioré | Principale | Secondaire |
|---|---|---|---|---|---|
| **Château** | équilibré, le plus charismatique | Paladin (Croisé) | Ange → Archange | cristal | gemmes |
| **Morts-vivants** | la nécromancie | Nécromancien (Nécromancie) | Colosse d'os → Colosse funèbre | mercure | gemmes |
| **Barbares** | la horde, la Rage | Barbare (Chef de guerre) | Béhémoth → Béhémoth ancien | soufre | cristal |
| **Forêt** | tireurs, régénération | Rôdeur (Chasseur) | Troll → Troll ancien | gemmes | cristal |
| **Tour de magie** | façonnés, bibliothèque des arcanes | Magicien (Érudit) | Géant → Titan | gemmes | mercure |
| **Enfer** | le feu, le prix du sang | Démoniste (Pacte) | Démon → Archidémon | mercure | soufre |
| **Donjon** | la Terreur | Seigneur des abîmes (Effroi) | Hydre → (à choisir) | soufre | mercure |
| **Nains** | l'obstination, les machines | Thane (Obstination) | Machine de guerre → Forteresse ambulante | cristal | soufre |

✅ Toutes jouables : créatures et capacités, ville, classe et héros, ville et héros sur la carte ; images des créatures
(sauf les Nains). La faction neutre garde les monstres de la carte (le Dragon rouge, les pirates).
**Reste** :
- **Images** : les 7 créatures des Nains (session images).
- **Noms** : l'Hydre améliorée ; les habitations de l'Enfer, si l'utilisateur le souhaite.
- **Règles (session règles)** : le **Démon se téléporte** (toute case libre à sa portée, sans voler ; l'animation est
  prête) et sa cible **brûle** quelques tours (à comparer d'abord avec l'aura du Moloch) ; l'**attaque du Géant**
  pourrait toucher une zone ; **aux sièges** (quand les villes auront des murs), les Barbares frappent les murs.
- **L'Ange ressuscite** (plus tard, sur une branche à part) : à la place de son action, à chacun de ses tours, des
  créatures amies tuées dans ce combat (pas les morts-vivants), autant de PV par Ange que ses dégâts (80) ; puis
  refaire `Duels_a_prix_egal`.
- **Les créatures améliorées** (plus tard, comme Heroes 3) : la Banshee (le Fantôme amélioré) ; le Colosse funèbre
  (plus de vie, son balayage effraie) ; le Troll ancien (régénère plus, repousse l'ennemi) ; l'amplificateur de
  nécromancie.
- **Équilibrage** : le **Colosse d'os plus faible que l'Ange** ; la Forêt souvent la plus forte (régénération) ; le
  Mage un peu faible en duel ; la horde barbare devrait dominer les premières semaines (son moral la freine) ; les
  gardiens du Donjon ralentissent certaines petites cartes ; vus par `Duels_a_prix_egal`, à revoir avec l'utilisateur :
  l'Archer perd contre tous les niveaux 2, le Griffon bat largement les niveaux 3, le Vampire écrase le Moine, le
  Cavalier écrase les niveaux 6 ; le Squelette peut-être trop fort face aux paysans.
- **Animations** (plus tard) : le Ver des profondeurs (glisser vers sa cible, les fissures, peut-être avaler).
- L'IA apprend chaque nouvelle faction.

### Les héros, leurs classes et leurs compétences
✅ Fait : **un héros n'est qu'un nom, un portrait et une faction** (`data/heroes.json`, 47 héros) ; **sa classe donne
le reste** (`data/classes.json` : caractéristiques, compétence de départ, talent qui grandit avec le niveau, sorts) ;
au niveau supérieur, le choix reste libre. La nouvelle partie (faction, héros dans une bande de portraits). Les
**compétences** (`data/skills.json`) : trois arbres (Aventure, Commandement, Magie), un point par niveau, rangées
ouvertes à 3 et 6 points dans l'arbre, six compétences au plus, trois rangs ; la Diplomatie et la fuite des gardiens ;
le moral et la chance ; la Tactique ; la Navigation ; les talents des classes.
**Reste** :
- Les **quatre sorts spéciaux** des écoles au rang expert (idées : Prière, Armageddon, Séisme, une invocation ; plus
  tard, quand le jeu sera plus avancé : choix de l'utilisateur).
- La simulation qui prend des compétences ; l'IA qui choisit la Diplomatie.
- Le moral et la chance : les touches des factions, les artefacts, un bâtiment de ville, la Chance sur les coffres et
  les artefacts trouvés.
- Plus tard : les héros de force achètent leur livre de sorts ; les **cavernes** de la carte, où l'on apprend une
  compétence ou trouve un artefact.

### Les artefacts (à concevoir)
Les objets que porte un héros (aujourd'hui un seul, `data/artifacts.json`) : leurs effets, où on les trouve (gardiens,
coffres, cavernes), peut-être des ensembles. À concevoir avec l'utilisateur, l'idée d'abord, puis le détail.
✅ **L'écran du héros** est prêt pour eux (2026-11-09, images d'attente) : à gauche le portrait et les
caractéristiques ; trois onglets Équipement, Compétences, Magie. Équipement : le héros en pied, **11 emplacements**
reliés par un trait à l'endroit où ils se portent (tête, cou, cape, torse, arme, bouclier, deux anneaux, ceinture,
pieds, divers), un **coffre de 16 places**. **Une image de 32 × 32 par artefact** sert partout (×3 à l'écran du héros,
×2 posé sur la carte, une ombre ou une lueur dessinée dessous).
**Reste** : déplacer les artefacts à la main ; les images (41 héros en pied, une icône par compétence : session
images) ; les onglets en marque-pages (à l'essai) ; la taille de l'écran, à régler avec la maquette
`art-candidats/en-cours/maquettes/heros.html`.

### L'économie ✅
7 ressources (or, bois, minerai ; mercure, cristal, soufre, gemmes) ; le marché ; mines sur 3 × 2 cases et villes sur
5 × 4 cases (`HISTORIQUE.md`). **Reste** : les taux du marché, à revoir en jouant (l'utilisateur n'en est pas sûr).
**À faire (IA)** : sa prévision d'un combat (jouée trois fois) se trompe dans les combats qui tournent vite (Fantômes
qui grossissent, Loups qui frappent deux fois) : elle perd parfois son héros dans un combat jugé gagné.

### La carte
✅ Fait (détail dans `HISTORIQUE.md`) : forêts qui bloquent, coffres au choix, lieux (autels, sage, roue à eau, moulin,
tour d'observation, temple, cercle des fées), gardiens justes, les tailles de Heroes 3, les passages entre zones selon
une option, une ville au plus par zone, une scierie et une mine de minerai gardées près de chaque ville, victoire par
défaut : prendre toutes les villes (les parties longues sont acceptées) ; rivières, ruisseaux et ponts en images ; les
rives par tronçons, avec des roseaux et de l'herbe sur les plages ; les ruisseaux (profondeur, mares, rives, pierres,
roseaux, un delta à bras) ; des rives arrondies ; l'herbe (taches sèches et drues, touffes, bouquets de fleurs) ; les routes (un arbre entre villes, mines et ports ; une sorte par zone selon la faction ; leur
dessin).
**Reste** :
- **À revoir ensemble** : un problème du moulin (à en parler).
- **Les rives** : l'utilisateur les travaillera encore ; **revoir les routes ensuite**, et quand les zones seront plus
  variées (une route dans les zones sans ville, la quantité de routes).
- **L'herbe** : embellir les fleurs (l'utilisateur, plus tard) ; le bord des routes suivra.
- **Plus tard** : un style de sol par zone de faction (herbe fleurie du Château, sèche des Barbares, morte des
  morts-vivants…) ; une route enneigée ; un coût de marche par sorte de route (pavés 50 %, gravier 65 %, terre 75 %) ;
  une entrée pavée devant chaque ville ; des ornières ou des flaques ; redessiner le petit pont de bois d'ouest en est
  s'il gêne en jouant ; le moulin et la roue à eau animés.

### La mer
✅ Fait (2026-11-14 et 15) : bateaux, chantiers navals, embarquer et débarquer, les types de cartes (continent, mers,
îles, archipel), les objets de la mer, la Navigation, les navires abandonnés, les pirates, les gardiens de la mer.
**Reste** : les images (session images) ; un pont de navire comme champ des combats en mer ; peut-être une faction
« pirates et monstres de la mer ».

### Les habitations sur la carte (à concevoir)
Des lieux semés au hasard où un héros achète des troupes (comme dans Heroes). À concevoir : créatures, réserve et
croissance, prix, garde.

### Les donjons à explorer (idée, plus tard)
Idée de l'utilisateur (2026-11-11) : un **donjon** marqué sur la carte, comme un vrai donjon de D&D ; le héros y entre,
le parcourt et en ressort avec une **grande récompense**. Façon préférée : **une petite carte à part**, comme le
souterrain de Heroes 3, parcourue case par case (couloirs, salles, gardiens, coffres, pièges, une sortie). À concevoir
avec la session règles : sa génération, sa sauvegarde et son brouillard, sortir et revenir ou non, les pertes d'une
salle à l'autre, les récompenses (artefact, or, expérience, créature rare), le nombre par carte et la difficulté selon
la zone ; les créatures du Donjon en gardiens.

## Étape 10 : finitions

### Le combat
✅ Fait : combat à ×2, effets dessinés par le jeu, animations des sorts, les options du combat, choisir le côté de
l'attaque, les champs de l'herbe et du désert.
**Reste** : le champ du **marais** (même recette) ; peut-être des rochers dans les collines et des nuages ; de vrais
obstacles de combat ; l'**Épéiste et le Cavalier** à refaire (plus petits) ; peut-être une animation « touché » par
créature.

### Les héros en images
✅ Le héros de chaque faction sur la carte (8 directions, une marche), leurs pieds revus.
**Reste** : refaire le sud-est du chef barbare et du Seigneur des abîmes (tournés vers le sud) ; **une image de combat
par héros**, sur le côté du champ, qui ressemble à son portrait (PixelLab, le portrait comme référence), à cheval ou
non, plus tard ses animations ; cadres des portraits, bandeau de la date.

### L'interface
✅ Fait : les tailles communes (`Ui.cs`) et la police Pixel Operator, des icônes sur les boutons du combat et du
panneau de la carte, les écrans revus pour la nouvelle police.
**Reste** : des icônes sur les autres écrans, au fil des besoins ; des icônes pour les récompenses (coffre, niveau) ;
l'écran de recrutement, à revoir pour gagner de la place ; la poignée des barres de défilement comme taillée dans la
pierre ; vérifier les pieds des mines de mercure, minerai et soufre, et la force de l'ombre de contact en jeu.

### La ville vue de face par faction (rien n'est encore fait)
Conçue avec l'utilisateur (2026-11-07). Chaque faction a sa **scène** (`art/towns/<faction>/` : un fond de 640 × 195
affiché ×3, et `scene.json` : pour chaque bâtiment, son pied, sa profondeur et son image `<id>.png`) ; chaque
amélioration a sa propre image au même pied. Bâtiments dessinés de l'arrière vers l'avant ; clic et survol sur les
pixels de l'image, léger contour au survol. Chaque faction place aussi les bâtiments communs ; un test vérifie que la
scène couvre tous ses bâtiments. Choix de l'utilisateur : une place vide ne montre **rien** ; **un seul fond** par
faction ; place pour **au moins 25 bâtiments** ; petits détails animés plus tard. Images : un concept de la ville
entière, puis le fond, puis chaque bâtiment avec le fond comme référence ; une page HTML pour placer les bâtiments à
la souris et écrire `scene.json`.

### L'IA et l'équilibre
Entraîner les profils de difficulté par des parties entre IA, jouées sans affichage ; petits détails et équilibrage
fin.

### Les sons et la musique (en cours)
Commencé avec l'utilisateur (2026-11-15), à revoir ensemble :
- **Musiques** (1 à 3 minutes, qui bouclent ; 20 à 30) : le menu, la carte (une par terrain), **une par ville**
  (8), le combat (2 ou 3), de courts airs (victoire, défaite, niveau, nouvelle semaine).
- **Sons** (moins de 2 secondes ; 150 à 250) : l'interface, la carte, **chaque créature au combat** (attaque, coup
  reçu, mort, parfois marche ou tir : le plus gros du travail), chaque sort. **Ambiances** : vent, mer, forêt, lave…
- **Sources**, à comparer avant de produire en nombre : bibliothèques libres (Freesound CC0, Kenney, OpenGameArt,
  Sonniss) pour l'interface, les pas et les ambiances ; sons générés (IA) pour les créatures et les sorts ; musique
  générée si elle plaît, ou des lots payants. **Noter la licence de chaque fichier.**
- **Dans l'ordre** : 1. ✅ la base technique (2026-11-15 : `Game/Audio.cs`, `data/sounds.json`, les volumes dans les
  options, des sons d'attente de `tools/audio/attente.py`) ; 2. un essai : les sons d'un combat (une faction, quelques
  sorts) et une musique de combat, 2 ou 3 sources côte à côte sur une page d'écoute, l'utilisateur choisit à
  l'oreille ; 3. le reste partie par partie, comme les images.

## Repères (choix encore valables)
- **Héros** : les caractéristiques de départ viennent de la classe (Paladin : 2/2/1/2, charisme 14) ; niveau n à
  700 × (n−1) × n / 2 d'expérience (= l'or des créatures tuées) ; chaque niveau : +1 charisme, +1 au choix et un point
  de compétence. Une troupe obéit si nombre × coût de commandement ≤ charisme × 10, sinon elle peut se révolter au
  début d'un combat (≈ 50 % à deux fois la limite), ou pendant si elle grossit (Fantômes). Les garnisons n'ont pas de
  limite.
- **Château** : départ 30 paysans, 15 archers, 4 griffons ; prix 3 or par « paysan équivalent » (Archer 18, Griffon
  40, Épéiste 80, Moine 150, Cavalier 300, Ange 4900 et 1 cristal) ; croissance 30, 18, 14, 8, 6, 4, 2 ; paysans pour
  vaincre une unité : Archer 8, Griffon 13, Épéiste 26, Moine 58, Cavalier 112, Ange 1560 (`UnitCurveTests`).
- **Économie** : villes 300 / 600 / 1200 or par jour, mines d'or 500 (doublés le 2026-11-11), autres mines 2 bois ou
  minerai, 1 d'une ressource rare ; héros de taverne 2500 or (8 au plus).
- **Combat** : dégâts tirés au sort puis règle attaque / défense de Heroes 3 ; tir à moitié au-delà de 6 cases ou
  au corps à corps ; défense +20 % ; fuite : 50 à 90 % de pertes ; un combat se rejoue pareil avec la même graine.
- **Carte** : zones séparées par des passages gardés ; le niveau d'une zone (passages depuis le départ) donne la force
  des monstres ; un château par joueur au départ, des villes neutres plus loin.
- **Difficulté** (`ComputerProfile`) : pertes acceptées 20 / 30 / 40 %, attaque le joueur dès le jour 21 / 0 / 0,
  héros au plus 2 / 3 / 4, revenu d'or 75 / 100 / 125 %.
- **Équilibre mesuré** (`Mesures`, 2026-11-11, six cartes) : la simulation seule gagne les petites cartes vers le jour
  68, les normales vers le jour 86, les grandes vers le jour 147 ; un adversaire après six semaines (Normal, or
  d'armée) : Château 57 100, morts-vivants 47 700, Barbares 54 700, Forêt 61 700, Tour 56 200, Donjon 60 000, Enfer
  65 200.

## Choix à respecter
- **Tout le pixel art à ×2** (un pixel d'image = 2 × 2 pixels d'écran ; choix de l'utilisateur, 2026-11-10) : la taille à
  l'écran vient du nombre de pixels de l'image, jamais d'un autre agrandissement (voir `tools/art/METHODE.md`).
- **Les tailles communes de l'interface** (`Ui.cs`, 2026-11-11) : un écran prend ses tailles de bouton, de texte et de
  marge parmi celles de `Ui` (`ButtonHeight`, `NormalText`…), et ses longueurs tombent sur la grille (`Px`, `Snap`).
- **`GameState` gère plusieurs joueurs**, et l'IA joue avec les mêmes méthodes que le joueur humain.
- **Toutes les règles restent dans `Core/`** (sans Godot), testables ; l'affichage ne fait que lire des événements.
- **Les données de jeu** (créatures, bâtiments, sorts, artefacts) sont dans `godot/data/`, pas dans le code.
- Rester proche des règles et des chiffres de Heroes 3, sauf choix contraire noté (charisme, magie…).
