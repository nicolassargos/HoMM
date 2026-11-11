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
6. ✅ Combat tactique (11 × 9 cases aplaties comme Heroes 3, du sol au-dessus de la grille pour la tête des grandes
   créatures, capacités des créatures, grandes créatures sur 2 cases, combat rapide joué par
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
3. **Une image par sort** (le livre, la guilde).
4. **Une animation par sort** sur le champ (projectile, éclair, explosion…) et un **effet visible sur les troupes**
   (lueur, aura tant qu'un sort dure…). Puis le reste au fil des idées (sons…).

### Les factions (en cours de conception)
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
| **Forêt** | les meilleurs tireurs, plantes et soins | Troll → Troll ancien | gemmes | cristal | **en cours** |
| **Tour de magie** | la faction de la magie | Géant → Titan | gemmes | mercure | **en cours** |
| **Enfer** | le chaos et le feu, le prix du sang | Démon → Archidémon | mercure | soufre | jouable |
| **Donjon** | les seigneurs des abîmes, la Terreur | Hydre → (à choisir) | soufre | mercure | jouable |
| **Nains** | montagne, très solides, runes et machines | Machine de guerre → Forteresse ambulante | cristal | soufre | à faire |

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
- L'**Enfer** : voir plus bas. Les **Nains** : échanger des ressources contre de l'or, ou miner davantage (à voir).
- Le **Tyran œil** : un œil géant qui tire des rayons, chacun un sort néfaste au hasard. La **Machine de guerre** :
  une machine à vapeur avec un canon (un tireur dont le coup touche une zone).
- La **faction neutre** garde les monstres de la carte seulement : le **Dragon rouge** (et d'autres au fil du temps).
- Les **héros** : les Nains n'en ont pas encore.
- L'IA devra apprendre chaque nouvelle faction. **Plus tard : les créatures améliorées** ; la **Banshee** sera le
  Fantôme amélioré.

#### L'Enfer (en cours)
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
3. Les images (session images) : les 7 créatures, l'icône du bouton « Invoquer ». ✅ La ville sur la carte (2026-11-11,
   branche `ville-enfer`, choix de l'utilisateur) : une citadelle de roche noire hérissée de pointes, des coulées de lave,
   une porte en tête de démon cornu au feu dans la gueule (`objects/town-enfer.png`, luminosité 51).

#### Le Donjon (en cours)
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
Le Dragon rouge reste neutre (peut-être le niveau 7 des Nains, à moins du Phénix).
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
3. Les images (session images) : les 7 créatures, la ville sur la carte.

#### La Tour de magie (en cours)
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

#### La Forêt (en cours)
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

#### Les Barbares (jouables)
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

#### Les morts-vivants (en cours)
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

### Les héros et leurs classes (en cours)
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

### Les compétences des héros et les artefacts (à concevoir)
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

### Les ressources rares ✅
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

### La carte ✅ (premier passage)
Remarques de l'utilisateur (2026-11-07) : la carte paraît vide, traverser une forêt est déroutant. Fait (2026-11-08,
choix de l'utilisateur) :
- **Les forêts bloquent le passage** (comme Heroes 1 à 3 : les arbres sont des obstacles qu'on contourne) ; les murs
  entre zones sont de montagne, de forêt ou d'eau (35 / 35 / 30 %) ; moins de taches de montagne et de forêt.
- **Les coffres au choix** : 1000, 1500 ou 2000 or, ou 500 de moins en expérience (une fenêtre ; l'IA prend l'or sous
  5000).
- **Des lieux** (pour 40 × 30 cases, plus sur une grande carte) : 4 **autels** (+1 attaque, défense, puissance ou savoir,
  une fois par héros), 2 **cabanes du sage** (un sort des niveaux 1 et 2), une **roue à eau** (500 or par semaine) et un
  **moulin** (2 d'une ressource rare par semaine), une **tour d'observation** (dévoile 20 cases autour) ; **42 tas** au
  lieu de 28. Sauvegarde version 5. Images : dessins par formes en attendant la session images (`objects/shrine-attack`,
  `-defense`, `-power`, `-knowledge`, `sage`, `water-wheel`, `windmill`, `tower`). Tests : `CarteObjetsTests`.
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

### Les habitations sur la carte
Des lieux semés au hasard où un héros achète des troupes (comme dans Heroes). À concevoir : créatures, réserve et
croissance, prix, garde.

## Étape 10 : finitions
- Animations des créatures avec PixelLab (au fil de l'eau). ✅ Effets du combat dessinés par le jeu (projectiles,
  impacts, poison, drain, régénération, auras ; `tools/art/METHODE.md`). Reste : les animations des sorts sur le champ
  (étape 9), avec le même système (`BattleEffects`), et peut-être une animation « touché » par créature.
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
  aux couleurs nuancées), grès, grosses souches et troncs blanchis, arbustes, buissons épineux, palmiers. Reste : le
  marais (même recette), peut-être des rochers dans les collines et quelques nuages. Détail dans `HISTORIQUE.md`.
- Le héros sur la carte et au combat ; cadres des portraits, bandeau de la date. (Icônes des ressources : faites,
  2026-11-07.)
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
  - le **héros** de la carte, plus tard.
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

## Repères (choix encore valables)
- **Héros** : les caractéristiques de départ viennent de la classe (Paladin : 2/2/1/2, charisme 14) ; niveau n à 700 × (n−1) × n / 2 d'expérience (1000 avant les prix barbares, 2026-11-09 : la simulation gagne
  de nouveau vers le niveau 20) (= l'or des créatures
  tuées) ; chaque niveau : +1 charisme et +1 au choix. Une troupe obéit si nombre × coût de commandement ≤ charisme
  × 10, sinon elle peut se révolter au début d'un combat (≈ 50 % à deux fois la limite), ou pendant
  si elle grossit (Fantômes) ; le héros attaqué aussi (ses révoltés combattent avec l'attaquant). Les garnisons n'ont
  pas de limite.
- **Château** : départ 30 paysans, 15 archers, 4 griffons ; prix 3 or par « paysan équivalent » (Archer 18, Griffon 40,
  Épéiste 80, Moine 150, Cavalier 300, Ange 4900 et 1 gemme) ; croissance 30 (28 avant le 2026-11-08), 18, 14, 8, 6, 4, 2 ; paysans pour vaincre
  une unité : Archer 8, Griffon 13, Épéiste 26, Moine 58, Cavalier 112, Ange 1560 (`UnitCurveTests`).
- **Économie** : villes 300 / 600 / 1200 or par jour, mines d'or 500 (doublés le 2026-11-11, choix de l'utilisateur : la
  simulation dépensait tout son or chaque jour ; Heroes 3 paie bien plus, par rapport au prix des créatures) ; héros de
  taverne 2500 or (8 au plus).
- **Combat** : dégâts tirés au sort puis règle attaque / défense de Heroes 3 ; tir à moitié au-delà de 6 cases ou
  au corps à corps ; défense +20 % ; fuite : 50 à 90 % de pertes ; un combat se rejoue pareil avec la même graine.
- **Carte** : zones séparées par des passages gardés ; le niveau d'une zone (passages depuis le départ) donne la force
  des monstres ; un château par joueur au départ, des villes neutres plus loin.
- **Difficulté** (`ComputerProfile`) : pertes acceptées 20 / 30 / 40 %, attaque le joueur dès le jour 21 / 0 / 0,
  héros au plus 2 / 3 / 4, revenu d'or 75 / 100 / 125 %.
- **Équilibre mesuré** (`Mesures`, 2026-11-07, six cartes, avec les ressources rares et 1,5 mine par zone) : la simulation
  seule (Château, sans magie) gagne sur les petites cartes vers le jour 73, les normales vers le jour 88, les grandes vers
  le jour 157 (plus de mines, donc plus de gardiens à vaincre). Un adversaire après six semaines : Château 19 400, morts-
  vivants 20 200 en or d'armée ; Facile 13 500, Normal 20 700, Difficile 23 200. Courbe : Archer 8, Griffon 14, Épéiste
  26, Moine 63, Cavalier 116, Ange 1643 paysans.

## Choix à respecter
- **Tout le pixel art à ×2** (un pixel d'image = 2 × 2 pixels d'écran ; choix de l'utilisateur, 2026-11-10) : la taille à
  l'écran vient du nombre de pixels de l'image, jamais d'un autre agrandissement (voir `tools/art/METHODE.md`).
- **`GameState` gère plusieurs joueurs**, et l'IA joue avec les mêmes méthodes que le joueur humain.
- **Toutes les règles restent dans `Core/`** (sans Godot), testables ; l'affichage ne fait que lire des événements.
- **Les données de jeu** (créatures, bâtiments, sorts, artefacts) sont dans `godot/data/`, pas dans le code.
- Rester proche des règles et des chiffres de Heroes 3, sauf choix contraire noté (charisme, magie…).
