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
10. Finitions : apparence et détails, **les sons et la musique** (ci-dessous).

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

#### Les Nains (jouables)
Les halls de la montagne : des artisans têtus, runes et machines (créatures qui ne copient ni Heroes 3 ni Heroes 5).
Marque : **l'obstination** (une troupe qui ne s'est pas déplacée du round prend 20 % de dégâts en moins) ; les machines
sont façonnées (résistent à la magie). Classe **Thane** (talent Obstination). Créatures : Porte-bouclier, Arbalétrier,
Chevaucheur de chèvre, Porte-étendard des ancêtres, Lance-flammes, Gyrocoptère, Machine de guerre (→ Forteresse
ambulante). ✅ Créatures et capacités, ✅ ville, classe et héros, ✅ ville et héros sur la carte.
**Reste** : les images des 7 créatures (session images).

#### L'Enfer (jouable)
Le chaos et le feu : toutes ses créatures sont insensibles au feu. Marque : **le prix du sang** (chaque créature de
l'Enfer tuée rend des points de sort au héros). Classe **Démoniste** (talent Pacte). Créatures : Diablotin, Cultistes,
Limon de magma, Succube, Tourmenteur, Moloch, Démon (→ Archidémon). ✅ Créatures et capacités, ✅ ville, classe et héros,
✅ ville et héros sur la carte.
**Reste** : les images des 7 créatures et l'icône du bouton « Invoquer » (en cours, branche `creatures-enfer`) ; les noms
des habitations, à revoir si l'utilisateur le souhaite.

#### Le Donjon (jouable)
Les seigneurs des abîmes : des élites fortes sur une base faible. Marque : **la Terreur** (chaque coup fait perdre 1 de
moral à la cible ; insensibles : morts-vivants, façonnés, niveau 7). Classe **Seigneur des abîmes** (talent Effroi).
Créatures : Homme-lézard, Homme-grenouille, Minotaure, Aberration, Ver des profondeurs, Tyran œil, Hydre. ✅ Créatures et
capacités, ✅ ville, classe et héros, ✅ images des créatures, ✅ ville et héros sur la carte.
**Reste** : le nom de l'Hydre améliorée ; **plus tard** (choix de l'utilisateur, 2026-11-14), revoir les animations du Ver
des profondeurs (glisser vers sa cible, les fissures, peut-être avaler) ; **à surveiller** : ses gardiens (la Terreur)
ralentissent certaines petites cartes. **À revoir** (idée de l'utilisateur, 2026-11-14) : l'attaque du Géant (Tour) qui
frappe le sol pourrait toucher une zone : une règle à concevoir avec la session règles.

#### La Tour de magie (jouable)
Le savoir : des mages, des créatures **façonnées** (résistent à la magie : moitié des dégâts des sorts, aucun sort
néfaste) et des géants. Marque : **la bibliothèque des arcanes** (un sort de plus par niveau de guilde). Classe
**Magicien** (talent Érudit). Créatures : Gnome, Gargouille, Golem de pierre (→ Golem d'airain), Naga, Djinn, Mage
(→ Archimage), Géant (→ Titan). ✅ Créatures et capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la
carte.
**Reste** : le Mage un peu en dessous de son niveau en duel (les duels en terrain ouvert avantagent le corps à corps), à
revoir à l'équilibrage.

#### La Forêt (jouable)
Les esprits du bois : les meilleurs tireurs, des troupes qui se soignent, peu nombreuses et chères. Marque : **la
régénération de la nature** (la créature de tête de chaque troupe retrouve un quart de ses PV à son tour). Classe
**Rôdeur** (talent Chasseur). Créatures : Farfadet, Sanglier, Archer elfe, Cerf, Druide, Tréant, Troll (→ Troll ancien).
✅ Créatures et capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la carte.
**Reste** : souvent la faction la plus forte des adversaires mesurés (sa régénération), à revoir à l'équilibrage.

#### Les Barbares (jouables)
La horde : la force, le nombre et la vitesse, le mépris de la magie. Aux niveaux 1 à 4, des créatures moins chères, plus
nombreuses et plus faibles une à une. Marque : **la Rage** (une troupe frappée gagne de l'attaque). Classe **Barbare**
(talent Chef de guerre). Créatures : Gobelin, Loup, Orc, Ogre, Nomades (2 cases, frappe et repart), Oiseau-tonnerre,
Béhémoth. ✅ Créatures et capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la carte.
**Reste** : **aux sièges** (quand les combats en ville auront des murs ; idée de l'utilisateur), les troupes barbares
frappent les murs ; **à voir** : la horde devrait être la plus forte des premières semaines (avec le moral, ses grosses
troupes dépassent souvent la limite de commandement) ; **à revoir avec l'utilisateur** (vu par `Duels_a_prix_egal`) :
l'Archer perd contre tous les niveaux 2, le Griffon bat largement les niveaux 3, le Vampire écrase le Moine, le Cavalier
écrase les niveaux 6, l'Ange écrase le Colosse d'os.

#### Les morts-vivants (jouables)
La magie et la mort : insensibles aux sorts de l'esprit, ni soins ni résurrection sur eux. Marque : **la nécromancie**
(du Nécromancien : des ennemis tués se relèvent en Squelettes). Créatures : Squelette, Zombie, Fantôme (grossit en
tuant), Araignée géante (poison), Vampire (draine la vie), Liche (nuage de mort), Colosse d'os (balayage). ✅ Créatures et
capacités, ✅ ville, classe et héros, ✅ images, ✅ ville et héros sur la carte.
**Reste** : le **Colosse d'os plus faible que l'Ange** (à rendre aussi fort) ; le Squelette peut-être trop fort face aux
paysans (à surveiller en jeu) ; **plus tard** : l'amplificateur de nécromancie, la Banshee (le Fantôme amélioré).

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

### Les habitations sur la carte
Des lieux semés au hasard où un héros achète des troupes (comme dans Heroes). À concevoir : créatures, réserve et
croissance, prix, garde.

### Les donjons à explorer (idée, plus tard)
Idée de l'utilisateur (2026-11-11) : un **donjon** marqué sur la carte, comme un vrai donjon de D&D ; le héros y entre,
le parcourt et en ressort avec une **grande récompense**. Façon préférée par l'utilisateur : **une petite carte à part**,
comme le souterrain de Heroes 3, que le héros parcourt case par case (couloirs, salles, gardiens, coffres, pièges, une
sortie). Écartées pour l'instant : une suite de salles dans une fenêtre (plus simple), un plan de donjon où l'on clique
la salle suivante. À concevoir avec la session règles : sa génération (`MapGenerator`), sa sauvegarde et son
brouillard, sortir et revenir ou non, les pertes d'une salle à l'autre, les récompenses (artefact, or, expérience,
créature rare), le nombre par carte et la difficulté selon la zone ; les créatures du Donjon en gardiens.

## Étape 10 : finitions
- Animations des créatures avec PixelLab (au fil de l'eau). ✅ Effets du combat dessinés par le jeu (projectiles,
  impacts, poison, drain, régénération, auras ; `tools/art/METHODE.md`). ✅ Les animations des sorts sur le champ
  (étape 9, `SpellEffects`). Reste : peut-être une animation « touché » par créature.
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
  aux couleurs nuancées), grès, grosses souches et troncs blanchis, arbustes, buissons épineux, palmiers. Reste : le
  marais (même recette), peut-être des rochers dans les collines et quelques nuages. Détail dans `HISTORIQUE.md`.
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

### Les sons et la musique (à concevoir)
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
- **Les tailles communes de l'interface** (`Ui.cs`, 2026-11-11) : un écran prend ses tailles de bouton, de texte et de
  marge parmi celles de `Ui` (`ButtonHeight`, `NormalText`…), et ses longueurs tombent sur la grille (`Px`, `Snap`).
- **`GameState` gère plusieurs joueurs**, et l'IA joue avec les mêmes méthodes que le joueur humain.
- **Toutes les règles restent dans `Core/`** (sans Godot), testables ; l'affichage ne fait que lire des événements.
- **Les données de jeu** (créatures, bâtiments, sorts, artefacts) sont dans `godot/data/`, pas dans le code.
- Rester proche des règles et des chiffres de Heroes 3, sauf choix contraire noté (charisme, magie…).
