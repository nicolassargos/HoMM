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
11. Multijoueur : d'abord à tour de rôle sur un même PC, plus tard en réseau (ci-dessous).

## Étape 9 : contenu

### La magie
✅ Fait : les règles (proche de Heroes 2 et 3 : points de sort, puissance, guilde de 4 niveaux, un sort par round), les
18 sorts de combat (`data/spells.json`), le livre de sorts en image (le lutrin dans l'étude du héros et dans
l'académie de chaque faction, le livre ouvert, la page qui tourne), une image et une animation par sort.
✅ **Quatre sorts de la carte** (idées de l'utilisateur, 2026-11-24, branche `sorts-carte`) : invocation de bateau,
vent porteur, porte dimensionnelle (10 cases au plus : choix de l'utilisateur, pour pouvoir dézoomer), retour en ville ;
lancés d'un clic dans le livre, sur la carte, avec leurs icônes (détail dans `HISTORIQUE.md`). Leurs effets sur la carte et leurs sons ; l'IA les
lance aussi (branche `ia-sorts-carte`).
**Reste** :
- **L'équilibre force / magie**, reporté aux classes (choix de l'utilisateur) : d'abord apprendre à la simulation à
  **ramasser les tas de ressources** (sans eux, la guilde est bâtie trop tard), puis comparer les classes
  (`Simulation_force_ou_magie`).
- **Plus tard** : versions « de masse », un 5e niveau (Armageddon, élémentaires, morts relevés), d'autres sorts de la
  carte (vision…). Écartés pour l'instant : berserk, hypnotisme, paralysie.
- **Idées de l'utilisateur (2026-11-23)**, à concevoir avec la session règles :
  - au combat : **mur de feu** (feu : des cases qui brûlent qui les traverse, sans arrêter les tirs), **douves** (eau :
    une créature qui y entre perd la fin de son tour), **séisme** (terre : abîme tous les murs, aux sièges ; déjà
    prévu avec les murs) ;
  - sur la carte : ✅ faits (2026-11-24, plus haut).

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
| **Nains** | l'obstination, les runes et le cristal | Thane (Obstination) | Roi de cristal → Roi des ancêtres | cristal | soufre |

✅ Toutes jouables : créatures et capacités, ville, classe et héros, ville et héros sur la carte ; images des créatures
(les Nains : 2026-11-16, branche `creatures-nains-images`, des nains caricaturés, chacun le sien, loin des clichés : choix
de l'utilisateur ; détail dans `HISTORIQUE.md`). La faction neutre garde les monstres de la carte (le Dragon rouge, les
pirates).
**Reste** :
- **Nains, deux créatures remplacées** (choix de l'utilisateur, 2026-11-16) : le **Sapeur** (niveau 5, à la place du
  Lance-flammes : un mineur qui creuse sous le champ, ressort à côté d'un ennemi et frappe le premier, capacité
  `burrows` du Ver des profondeurs) et le **Roi de cristal → Roi des ancêtres** (niveau 7, à la place de la Machine de
  guerre : un très vieux roi nain scellé dans le cristal, réveillé pour la guerre ; vivant, pas « façonné » ; le Roi des
  ancêtres portera une couronne de cristal qui luit). ✅ Leurs données (PR #167 : `sapeur`, `roi-de-cristal`, habitations
  `galeries-sapeurs` et `caveau-rois` ; le déplacement sous terre du Sapeur marche), leurs images et les deux capacités
  du Roi (`crystal-mirror`, `ancestral-hold`). **À faire (session règles)** : le **Gyrocoptère**, devenu
  un nain aux ailes de cuir, de bois et de fer (idée de l'utilisateur), n'est plus une machine : sa capacité « façonné »
  et peut-être son nom.
- **Château, la refonte** (choix de l'utilisateur, 2026-11-17) : le **Cavalier descend au niveau 4** (un cavalier plus
  léger, plus petit ; charge), le **Chevalier à pied** prend le niveau 6 (armure de plaques, grand bouclier : résiste
  aux tirs et protège les tireurs à côté de lui), l'Épéiste disparaît ; le Griffon garde ses règles. ✅ Les données et
  les images (branche `chateau-refonte`) : l'Archer en braconnier osseux, un Griffon de guerre maigre et plus petit, le
  Cavalier en jeune sergent à cheval, la taille des Nomades ; le Chevalier garde son image de combat. ✅ Le **Moine**
  redessiné en frère jovial, lanceur de sorts (une boule de lumière part de son bâton), avec le **Loup** et le **Troll**
  (branche `creatures-loup-moine-troll`, 2026-11-19 ; détail dans `HISTORIQUE.md`).
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
  l'Archer perd contre tous les niveaux 2, le Griffon bat largement les niveaux 3, le Vampire écrase le Moine ; le Squelette peut-être trop fort face aux paysans.
- ✅ **Animations du Ver des profondeurs** (2026-11-20, branche `animations-dragon-hydre-ver`) : il glisse vers sa
  cible, le sol se fend devant lui, il avale en plongeant d'en haut.
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

### Les artefacts (en cours)
Décidé avec l'utilisateur (2026-11-21) : trois rangs (mineur, majeur, relique), des pouvoirs uniques, trouvés partout,
les ensembles plus tard.
✅ **La liste et leurs effets** (2026-11-21, branche `artefacts`) : 46 artefacts dans `data/artifacts.json`, tous leurs
effets en jeu sauf les pouvoirs uniques (détail dans `HISTORIQUE.md`).
✅ **Sur la carte** (2026-11-21, branche `artefacts-carte`) : 8 posés par carte de 40 × 30 (plus sur une grande), les
mineurs seuls, les majeurs et les reliques gardés ; un coffre sur cinq cache un mineur. Image d'attente : une pierre de
la couleur du rang sur une lueur.
**Reste**, dans l'ordre proposé :
1. Les trouver ailleurs : dans les cavernes et le Donjon (quand ils existeront) ; peut-être la Chance sur les coffres.
2. ✅ **Les images** des 46 artefacts (2026-11-22 et 23, huit lots, branches `artefacts-lot1` à `artefacts-lot8`) :
   environ 48 px sur une tuile de la couleur du rang, une image réduite de moitié sur la carte, ×2 (détail dans
   `HISTORIQUE.md`, méthode dans `tools/art/METHODE.md`, outil `tools/art/artefact.py`).
3. **Les déplacer à la main** sur l'écran du héros (porter, ranger au coffre, échanger entre deux héros).
4. **Les pouvoirs uniques**, un par un (la lame qui boit, le bouclier du marais, le masque de l'épouvante, le collier
   aux cent clochettes, la cape d'ombre, les ailes, les bottes de la marée, l'anneau du semeur, le sablier).
5. **Le marchand d'artefacts** (un bâtiment de ville : acheter et vendre).
6. L'IA : ramasser, porter les meilleurs, payer le marchand.
7. ✅ **Sept nouveaux artefacts** (idées de l'utilisateur, 2026-11-23, branche `idees-artefacts-sorts`) : la longue-vue
   des guetteurs, l'œil du recenseur, la lanterne des âmes et quatre talismans à double tranchant (détail dans
   `HISTORIQUE.md`) ; leurs images (2026-11-24, branche `artefacts-idees`). **Reste** : le **chariot** (charger des troupes d'une ville choisie et les amener au
   héros : plutôt un bâtiment ou une caravane qu'un artefact, à creuser).
✅ **L'écran du héros** est prêt pour eux (2026-11-09, images d'attente) : à gauche le portrait et les
caractéristiques ; trois onglets Équipement, Compétences, Magie. Équipement : le héros en pied, **11 emplacements**
reliés par un trait à l'endroit où ils se portent (tête, cou, cape, torse, arme, bouclier, deux anneaux, ceinture,
pieds, divers), deux par deux de chaque côté du héros, un **coffre de 16 places** en deux pages. Une image d'environ
48 par artefact, ×2, sur une tuile de la couleur de son rang (l'écran du héros, la fenêtre « Un artefact ! ») ; sur la
carte, une image réduite de moitié avec une lueur dessous ; le panneau du héros ne montre que leur nombre (« Art 3 », la
liste au survol).
**Reste** : déplacer les artefacts à la main ; les images (41 héros en pied, une icône par compétence : session
images) ; les onglets en marque-pages (à l'essai) ; la taille de l'écran, à régler avec la maquette
`art-candidats/en-cours/maquettes/heros.html`.

### Les bâtiments des villes (à faire, avant les villes en images)
Décidé avec l'utilisateur (2026-11-20) : la liste complète d'abord, puis les scènes des villes. Aujourd'hui, 17
bâtiments par ville (10 communs, 7 habitations, `data/buildings.json`) ; demain **environ 32**, comme Heroes 3 (une
amélioration garde la place de son bâtiment, avec sa propre image : 18 places, dans les 25 prévues par scène).
- ✅ **Le fort, la citadelle, le château fort, le capitole et le silo** (2026-11-20, branche `villes-batiments-communs` ;
  l'IA les bâtit quand sa ville n'a plus rien à recruter : l'or manque plus que les créatures ; détail dans
  `HISTORIQUE.md`).
- **Communs (9 places)** : salle du village → hôtel de ville → grand hôtel de ville → **capitole** (un par joueur,
  2400 or par jour) ; **fort → citadelle → château** (les murs des sièges ; +50 % puis +100 % de croissance) ; guilde
  des mages 1 à 4 ; taverne ; marché ; **silo** (+1 par jour de la ressource principale de la faction) ; **forge**
  (machines de guerre : baliste, tente de soins, chariot de munitions ; **à décider** : maintenant, ou seulement sa
  place dans la scène) ; chantier naval ; **bâtiment du Graal** (bâti avec le trésor enfoui des obélisques, un grand
  bonus propre à la faction).
- **Par faction (9 places)** : les 7 habitations, chacune avec son **amélioration** (d'où viennent les créatures
  améliorées) ; un **bâtiment de croissance** d'un petit niveau (les « hordes » de Heroes 3) ; un **bâtiment spécial**.
- **Les bâtiments spéciaux** (choix de l'utilisateur, 2026-11-20, après trois tours d'idées : des effets qui changent la
  façon de jouer ; forts pour les factions faibles des `Mesures`, utiles mais doux pour les fortes ; à régler en
  refaisant les `Mesures`) :

| Faction | Bâtiment | Effet |
|---|---|---|
| Morts-vivants | **Cortège funèbre** | après chaque combat gagné par un héros du joueur, où qu'il soit, les morts des deux camps reviennent à la ville : des Squelettes à recruter la semaine suivante |
| Barbares | **La Marmite** | on y jette de petites créatures, il en sort de plus fortes (4 Gobelins → 1 Orc, 3 Loups → 1 Ogre…) |
| Tour de magie | **La Tour qui pousse** | un étage de plus par semaine, **5 au plus** (visible dans l'image de la ville) ; +1 puissance par étage aux héros qui la visitent |
| Château | **Les Hérauts** | plus un héros a gagné de combats, plus les monstres neutres proposent de le rejoindre ; héros de taverne moins chers |
| Nains | **Le Grand Comptoir** ✅ | compte pour 2 marchés de plus (meilleurs taux d'échange) |
| Donjon | **Tourbillon de mana** ✅ | une fois par semaine, les points de sort d'un héros qui le visite sont doublés |
| Forêt | **Sentiers des rôdeurs** ✅ | les héros qui quittent la ville ont plus de mouvement ce jour-là (+400, un peu plus d'un quart) |
| Enfer | **Portail infernal** | aller d'une ville de l'Enfer du joueur à une autre |

- **Les murs et les sièges** (avec l'utilisateur, 2026-11-20) :
  - **Dans la scène de la ville** (choix de l'utilisateur, 2026-11-21) : le fort, la citadelle et le château sont **le
    château de la ville**, un bâtiment comme les autres, une image par étape à la même place (au fond pour le Château,
    comme Heroes 3) ; **avant le fort, une simple demeure du héros** à cette place, qui devient le château. Le fond de
    la scène n'a pas de château. **Des murs simples** (choix de l'utilisateur) : bois, briques ou pierre, selon la
    faction, sans formes trop particulières.
  - **Au combat (les sièges)** : contre une ville qui a un fort, un mur coupe le champ, avec une porte que seuls les
    défenseurs passent et des tireurs sur les murs ; la citadelle ajoute un fossé et une tour qui tire, le château
    deux tours de plus. L'attaquant doit ouvrir une brèche : les troupes barbares frappent les murs (déjà prévu), des
    sorts comme le Séisme. Sans fort, la ville se défend en terrain ouvert, comme aujourd'hui.
  - **Les engins de siège** (la catapulte, avec les machines de guerre de la forge) : **plus tard** (choix de
    l'utilisateur). En attendant, à décider avec la session règles : comment ouvrir une brèche sans eux (par exemple,
    toutes les troupes au corps à corps frappent la porte).
  - **Ensuite** : l'IA apprend à attaquer et à défendre les murs ; refaire les `Mesures` (une ville sans fort devient
    plus facile à prendre) ; peut-être des bâtiments spéciaux qui touchent les murs.
- **Règles à faire (session règles)** : les sièges et les murs, les machines de guerre, les créatures améliorées, le
  bonus du Graal de chaque faction, les bâtiments spéciaux et de croissance ; leurs noms (français) ; puis les `Mesures`.
  Les images des villes peuvent commencer dès que la liste est fixée.

### L'économie ✅
7 ressources (or, bois, minerai ; mercure, cristal, soufre, gemmes) ; le marché ; mines sur 3 × 2 cases et villes sur
5 × 4 cases (`HISTORIQUE.md`). **Reste** : les taux du marché, à revoir en jouant (l'utilisateur n'en est pas sûr).
**À faire (IA)** : sa prévision d'un combat (jouée trois fois) se trompe dans les combats qui tournent vite (Fantômes
qui grossissent, Loups qui frappent deux fois) : elle perd parfois son héros dans un combat jugé gagné.

### La carte
✅ Fait (détail dans `HISTORIQUE.md`) : forêts qui bloquent, coffres au choix, lieux (autels, sage, roue à eau, moulin,
tour d'observation, temple, cercle des fées), gardiens justes, les tailles de Heroes 3, les passages entre zones selon
une option, une ville au plus par zone, une scierie et une mine de minerai gardées près de chaque ville, la plupart des sortes de mines dans chaque zone et les objets répartis zone par zone (2026-11-20), victoire par
défaut : prendre toutes les villes (les parties longues sont acceptées) ; rivières, ruisseaux et ponts en images ; les
rives par tronçons, avec des roseaux et de l'herbe sur les plages ; les ruisseaux (profondeur, mares, rives, pierres,
roseaux, un delta à bras) ; des rives arrondies ; l'herbe (taches sèches et drues, touffes, bouquets de fleurs) ; les routes (un arbre entre villes, mines et ports ; une sorte par zone selon la faction ; en courbes douces, 2026-11-20, dessinées en courbes
arrondies comme dans Heroes 3, 2026-11-21 ; leur dessin) ; les montagnes en massifs posés comme dans Heroes 3, qui se fondent dans l'herbe, le sable ou le marais, et les mines creusées à leur flanc (2026-11-17) ; un sol par zone (de grandes régions de sable et de marais), le départ et les villes neutres au sol de leur faction, un héros sans ralentissement sur le sien ; le sol rocheux des Nains et la terre brûlée de l'Enfer, aux fissures rougeoyantes animées (2026-11-18) ; la neige de la Tour, les forêts de chaque sol (2026-11-19) ; des zones 2,5 fois plus grandes, comme les régions de Heroes 3 (2026-11-20) ; plus aucune terre sans chemin : ouverte à travers la forêt, remplie d'arbres, ou atteinte par un **portail** gardé qui cache une grande récompense (2026-11-21) ; le marais refait, avec ses mares, ses roseaux et ses boues (2026-11-24).
**Reste** :
- **Les sols** : **six sols suffisent** (herbe, sable, marais, roche, neige, terre brûlée ; choix de l'utilisateur,
  2026-11-20) : pas un par faction, des factions les partagent ; plus tard, des **variantes** d'un même sol pour les
  factions qui le partagent (teinte, arbres, détails ; le même champ de bataille, une autre palette). Une faction peut
  aussi avoir **plus d'un sol** : les Nains la roche ou la neige, la Tour la neige ou le sable, l'Enfer la terre brûlée
  ou le sable, les morts-vivants le marais ou la roche (✅ 2026-11-20). Peut-être plus tard un sol souterrain pour le Donjon,
  s'il y a un souterrain. Des thèmes de cartes (la répartition se réglera alors) ; le sol
  des factions encore sur l'herbe, à décider ; les arbres verts des mines creusées (elles iront plutôt au flanc des montagnes) ;
  l'équilibre : les factions sans sol propre perdent le plus (voir `HISTORIQUE.md`).
- **Retouches de la carte** (demandées par l'utilisateur, 2026-11-24, dans cet ordre) : ✅ le marais ; un peu la roche
  (remise à plus tard : l'utilisateur y réfléchit ; essais dans `art-candidats/archives/2026-11-24-roche`) ;
  ✅ des rives moins nettes ; ✅ les ruisseaux le long des pentes (une carte des hauteurs, des méandres, des affluents,
  nés au pied des montagnes, d'un marais ou d'une source ; voir `HISTORIQUE.md`) ; ✅ moins de mares dans le marais
  (un tiers de moins) ; puis de petites animations, comme la terre brûlée (de douces vagues au bord de la
  mer, des feuilles dans le vent…) ; l'ordre du dessin (des objets dessinés devant ce qui devrait les cacher : des
  exemples à donner par l'utilisateur).
- **Les rives** : l'utilisateur les travaillera encore ; **revoir les routes ensuite**, et quand les zones seront plus
  variées (une route dans les zones sans ville, la quantité de routes).
- **L'herbe** : embellir les fleurs (l'utilisateur, plus tard) ; le bord des routes suivra.
- **Les portails** : leur gardien et leurs récompenses, à revoir en jouant.
- **Plus tard** : un style de sol par zone de faction (herbe fleurie du Château, sèche des Barbares, morte des
  morts-vivants…) ; une route enneigée ; un coût de marche par sorte de route (pavés 50 %, gravier 65 %, terre 75 %) ;
  une entrée pavée devant chaque ville ; des ornières ou des flaques ; redessiner le petit pont de bois d'ouest en est
  s'il gêne en jouant ; le moulin et la roue à eau animés.

### La mer
✅ Fait (2026-11-14 et 15) : bateaux, chantiers navals, embarquer et débarquer, les types de cartes (continent, mers,
îles, archipel), les objets de la mer, la Navigation, les navires abandonnés, les pirates, les gardiens de la mer.
✅ Les images des objets de la mer, du phare, du chantier de la côte et du navire abandonné (2026-11-20, branche
`images-mer`). ✅ Le bateau des héros, le navire des pirates et le Pirate (2026-11-20, branche `bateau-pirate`). ✅ Le pont d'un navire, champ des combats en mer (2026-11-20, branche `champ-navire`). **Reste** : des pas sur le bois
du pont (ceux de l'herbe en attendant) ; peut-être une faction
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
l'attaque, les champs de l'herbe, du désert et du marais (2026-11-16), les obstacles plutôt au milieu du champ, des
nuages et des rochers dans les paysages ouverts, les flaques loin des obstacles (2026-11-17), le champ de la neige
(2026-11-19), celui de la roche, une terre désolée, et celui de la terre brûlée, aux feux qui respirent (2026-11-20).
✅ Une **animation de mort** par créature (2026-11-17), revue avec l'utilisateur et **variée** (2026-11-19, branche
`morts-variees` : en arrière, sur le côté, une mort propre au Démon, à la Liche, au Roi de cristal, au Golem…).
✅ La mort du **Moloch** qui se défait vraiment, sa tête visible au sol (2026-11-20, branche `moloch-jambes` ; « good
enough », selon l'utilisateur : les jambes s'affaissent dans le tas sans se défaire).
✅ Un **coup reçu** par créature (2026-11-17), revu et choisi avec l'utilisateur (le Dragon, l'Hydre et le Démon par le
second modèle de PixelLab) ; ceux du **Dragon** et de l'**Hydre** finis (2026-11-20, branche `coups-dragon-hydre` : ils
se cabrent en arrière ; préférés par l'utilisateur à ceux de `animations-dragon-hydre-ver`, faits le même jour).
✅ La mort et le coup reçu de l'Archer, du Griffon et du Cavalier (2026-11-19, branche `morts-chateau`) ; ceux du
**Loup**, du **Moine** et du **Troll** refaits d'après leurs nouvelles images (2026-11-19).

### Les héros en images
✅ Le héros de chaque faction sur la carte (8 directions, une marche), leurs pieds revus.
✅ **Une image de combat par héros** (2026-11-19, branche `heros-combat`) : sur le côté du champ, à cheval sur la
monture de sa faction, d'après son portrait (`tools/art/heros_combat.py`) ; faite pour un héros par faction (Sire
Roland, Mortimer, Tarka, Sylvain, Maître Aldric, Zarkhan, Malachar, Brokk), choisie par l'utilisateur au premier tour.
**Pas pour les 39 autres héros** (choix de l'utilisateur, 2026-11-19) : ils gardent leur image de la carte au combat.
✅ Le sud-est du **Seigneur des abîmes** tourné vers le sud, son grand cheval gardé (2026-11-20, branche
`heros-sud-est`) ; celui du **chef barbare** reste l'ancien (l'utilisateur le trouve lisible, aucun essai ne l'était).
**Reste** : plus tard, peut-être des animations pour ces huit ; cadres des portraits,
bandeau de la date.

### L'interface
✅ Fait : les tailles communes (`Ui.cs`) et la police Pixel Operator, des icônes sur les boutons du combat et du
panneau de la carte, les écrans revus pour la nouvelle police.
✅ Dans la ville, « Tout donner au héros » et « Tout laisser en garnison » en boutons à icône (2026-11-20, branche
`ville-boutons-garnison` ; l'interface de la ville avancera par petits pas, choix de l'utilisateur).
✅ **L'écran de ville façon Heroes 2** (2026-11-21, branche `disposition-ville`) : la vue sur toute la largeur, les
ressources en grille, Quitter seul visible (Construire et Recruter glissent au survol), le nom dans un onglet, une barre
d'état ; la disposition se règle à la souris (`tools/ui/disposition.py`) ; un clic sur les ressources ouvre le marché,
des flèches autour du nom passent d'une ville à l'autre (branche `ville-marche-fleches`). **Reste** : peut-être un fond
derrière chaque créature, comme Heroes 2.
**Reste** : des icônes sur les autres écrans, au fil des besoins ; des icônes pour les récompenses (coffre, niveau) ;
l'écran de recrutement, à revoir pour gagner de la place ; la poignée des barres de défilement comme taillée dans la
pierre. ✅ Les pieds des mines de mercure, de minerai (ses six sols) et de soufre, et la force de leur ombre de
contact, revus par l'utilisateur (2026-11-20, avec ceux du Seigneur des abîmes) ; environ 110 images ont encore des
pieds non revus (champ de la roche, massifs, arbres des sols, ponts).

### La ville vue de face par faction (rien n'est encore fait)
La liste de ses bâtiments : « Les bâtiments des villes », plus haut.
Conçue avec l'utilisateur (2026-11-07). Chaque faction a sa **scène** (`art/towns/<faction>/` : un fond de **960 × 270,
affiché ×2** sur toute la largeur de l'écran comme tout le pixel art (choix de l'utilisateur, 2026-11-21, avec la
disposition de l'écran ; d'abord 640 × 195 à ×3, puis 960 × 292), et `scene.json` : pour chaque bâtiment, son pied, sa profondeur et son image `<id>.png`) ; chaque
amélioration a sa propre image au même pied. Bâtiments dessinés de l'arrière vers l'avant ; clic et survol sur les
pixels de l'image, léger contour au survol. Chaque faction place aussi les bâtiments communs ; un test vérifie que la
scène couvre tous ses bâtiments. Choix de l'utilisateur : une place vide ne montre **rien** ; **un seul fond** par
faction ; place pour **au moins 25 bâtiments** ; petits détails animés plus tard. Images : un concept de la ville
entière, puis le fond, puis chaque bâtiment avec le fond comme référence ; une page HTML pour placer les bâtiments à
la souris et écrire `scene.json`.
**Choix de l'utilisateur (2026-11-20)**, après des esquisses (`art-candidats/en-cours/villes-esquisses/index.html`, une
par faction, placements dans `placements.json`) :
- **Une mise en scène libre par faction**, chacune son point de vue (pas de plan commun) ; chaque ville **inspirée de
  son image sur la carte**.
- **Pas d'eau dans les fonds** : le chantier naval apporte la sienne (un quai, un peu d'eau) et n'existe que dans une
  ville au bord de l'eau (il n'est même pas proposé ailleurs).
- Le **capitole** est la 4e image de l'hôtel de ville ; **La Tour qui pousse** a 5 étages au plus (une image par
  étage) ; le **Donjon** s'ouvre sur une caverne ; la forteresse des **Nains** fait partie de la montagne : un fond
  surtout de roche.

**Choix de l'utilisateur (2026-11-21)** :
- **Le ×2 est gardé** pour les villes : le fond et les bâtiments sont dessinés à leur vraie taille (fond de 960 × 270),
  comme tout le pixel art (sinon des pixels plus gros que ceux des créatures et des boutons posés dessus). À 1920 de
  large, cela fait le détail d'une ville de Heroes 3 (800 de large, ×2,4), pas celui de Heroes 2 (640, ×3). Les
  **concepts** de la ville entière peuvent avoir n'importe quelle taille : ils ne servent qu'à la mise en scène et à
  l'ambiance. PixelLab ne dessine pas plus de 792 de large : chaque bâtiment est une petite image à part ; le fond se
  dessine à 792 × 241, puis PixelLab l'élargit à 960 × 270 (`tools/art/agrandir.py`, puis `affiner.py`).
- **Chaque ville a son angle** (certaines vues plus d'en haut, d'autres plus de face) et ses bâtiments le suivent.
- Une place pour la **forge** dans chaque scène.
- Le **Château d'abord**. Son concept (2026-11-21, six tours) : la **disposition de E1** (vue de face, un peu d'en
  haut : chaque bâtiment entier, côte à côte) avec **l'allure de C2** (bâtiments serrés et vivants, grès, toits rouges
  et bleus) ; le rempart et le donjon au fond, le Portail de gloire sur un rocher à gauche, la statue à droite. La
  scène se construit pièce par pièce (le fond, puis chaque bâtiment seul), pas d'une seule image générée.
- **Le fond « B »** (paysage seul, montagnes, collines, places vides ; `art-candidats/en-cours/ville-chateau-fond/`) :
  son **style graphique** (ombrage doux, peint, couleurs claires) est celui que l'utilisateur préfère. **Un même style
  partout** : les bâtiments et les scènes des autres villes le suivent.
- Comme **Heroes 2** (les villes préférées de l'utilisateur), un bâtiment peut être **coupé par le bord** de l'image
  ou posé tout au bord, pourvu qu'on en voie la plus grande partie.

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
  options, des sons d'attente de `tools/audio/attente.py`) ; 2. ✅ un essai (2026-11-16, branche `sons-combat`,
  `tools/audio/essai.py`) : les sons d'un combat du Château et une musique de combat, trois sources côte à côte sur une
  page d'écoute. **Choix de l'utilisateur** : les effets chez **ElevenLabs** (choisi 13 fois sur 15 ; sa musique
  demande un abonnement payant), les bibliothèques libres là où elles sonnent mieux (le galop, la flèche magique) ; la
  musique d'abord des **bibliothèques libres**, **Stable Audio** plus tard (« plus années 90 »). Dans le jeu, chaque
  créature a ses sons (geste, tir, coup qui porte, tir qui touche, mort ; sinon ceux de tout le combat) et chaque sort
  le sien (`Audio.PlayCreature`, `spell-<id>`) ; l'Ange gifle (comme son animation), sa première mort essayée sert à la
  Résurrection. Licences : `godot/audio/LICENCES.md` ; 3. le reste partie par partie, comme les images : ✅ les sons
  de combat de **toutes les créatures** (2026-11-17, branche `sons-factions`, 113 sons ElevenLabs revus un à un sur la
  page de `tools/audio/sons.py` ; le Paysan garde les sons communs) ; ✅ les **musiques des villes** (2026-11-17,
  branche `musiques-villes`, bibliothèques libres, `tools/audio/musiques.py` : page d'écoute, étoiles et avis gardés
  dans `tools/audio/musiques-villes.json`) : les huit choisies (Nains : Five Armies ; Forêt : Mist Forest, peut-être
  à revoir avec l'image de sa ville, The Pilgrimage aussi candidate) ; ✅ la **musique de la carte** (2026-11-19,
  branche `musiques-carte`) : une par terrain autour du héros (`AdventureMap.MusicTerrain` ; herbe World Map, désert
  Myst, marais Crystal Meadow, roche Crusade, neige Mevalian Winter, sol brûlé Forging Flames, mer trois pistes qui
  alternent), celle du **menu** (Alandriel's Theme) et
  trois musiques de combat qui alternent (Determined Pursuit, Heroic Age, Heated Lands). Idées de l'utilisateur :
  « Death Is Just Another Path » pour l'écran de défaite ; « King's Feast » lui plaît (taverne ?) ; un **second
  désert** au thème arabe (Ibn Al-Noor, Desert City gardées) ; Forging Flames aussi pour les combats sur le sol
  brûlé ; ✅ les **pas au combat** (2026-11-19, branche `sons-pas`, ElevenLabs, `tools/audio/pas.py` : une courte
  marche coupée en pas seuls, revue en « marchant » sur la page) : cinq façons de marcher (à pied, lourd, sabots,
  pattes, rampant ; le Limon de magma à part) sur six sols, au rythme de l'animation de marche ; rien pour les volants,
  le Cerf qui saute et les fouisseurs (les ailes plus tard ?) ; ✅ les **ambiances** (2026-11-20, branche
  `sons-ambiances`, bibliothèques libres CC0, `musiques.py ambiances`) : une boucle par sol sous la musique, sur la
  carte (le sol autour du héros) comme au combat, en fondu ; rien en ville ni au menu ; herbe Evening Birds, désert
  Wind puis des cigales, marais Swamp Environment, roche Whistling of the Wind, terre brûlée un grand feu, mer la
  plage de Dellec ; **pas encore la neige** (aucune ne plaisait) ; « Lava loop » gardé pour de futurs lacs de lave.
  **Reste** : les autres sorts, la carte, l'interface, de courts airs, puis la musique chez Stable Audio.

## Étape 11 : multijoueur (à concevoir)
Idée de l'utilisateur (2026-11-17). Les règles s'y prêtent déjà : plusieurs joueurs dans `GameState`, un brouillard
par joueur, un humain peut diriger sa défense, et une partie se rejoue pareil avec la même graine.
- **À tour de rôle sur un même PC** (après le contenu de l'étape 9) : humain ou ordinateur pour chaque joueur dans la
  nouvelle partie ; un écran « Au tour de… » qui cache la carte entre deux joueurs ; l'affichage suit le joueur humain
  du tour (son brouillard, ses villes, ses héros) ; un combat entre deux humains, chacun dirige son camp.
- **En réseau** (plus tard, après l'étape 10) : n'envoyer que les ordres des joueurs, chaque PC calcule la même
  partie (tests qui le vérifient) ; rejoindre une partie, reprendre après une coupure ; un combat où l'un attend
  l'autre.

## Repères (choix encore valables)
- **Héros** : les caractéristiques de départ viennent de la classe (Paladin : 2/2/1/2, charisme 14) ; niveau n à
  700 × (n−1) × n / 2 d'expérience (= l'or des créatures tuées) ; chaque niveau : +1 charisme, +1 au choix et un point
  de compétence. Une troupe obéit si nombre × coût de commandement ≤ charisme × 10, sinon elle peut se révolter au
  début d'un combat (≈ 50 % à deux fois la limite), ou pendant si elle grossit (Fantômes). Les garnisons n'ont pas de
  limite.
- **Château** : départ 30 paysans, 15 archers, 4 griffons ; prix 3 or par « paysan équivalent » (Archer 18, Griffon
  40, Cavalier 80, Moine 150, Chevalier 300, Ange 4900 et 1 cristal) ; croissance 30, 18, 14, 8, 6, 4, 2 ; paysans
  pour vaincre une unité : Archer 8, Griffon 14, Cavalier 25, Moine 61, Chevalier 93, Ange 1597 (`UnitCurveTests`).
- **Économie** : villes 300 / 600 / 1200 or par jour, mines d'or 1000 (comme Heroes 3, deux fois moins nombreuses : 2026-11-20), autres mines 2 bois ou
  minerai, 1 d'une ressource rare ; héros de taverne 2500 or (8 au plus).
- **Combat** : dégâts tirés au sort puis règle attaque / défense de Heroes 3 ; tir à moitié au-delà de 6 cases ou
  au corps à corps ; défense +20 % ; fuite : 50 à 90 % de pertes ; un combat se rejoue pareil avec la même graine.
- **Carte** : zones séparées par des passages gardés ; le niveau d'une zone (passages depuis le départ) donne la force
  des monstres ; un château par joueur au départ, des villes neutres plus loin.
- **Difficulté** (`ComputerProfile`) : pertes acceptées 20 / 30 / 40 %, attaque le joueur dès le jour 21 / 0 / 0,
  héros au plus 2 / 3 / 4, revenu d'or 75 / 100 / 125 %.
- **Équilibre mesuré** (`Mesures`, 2026-11-11, six cartes) : la simulation seule gagne les petites cartes vers le jour
  68, les normales vers le jour 86, les grandes vers le jour 147. Un adversaire après six semaines (Normal, or
  d'armée ; `Adversaires_par_faction`, 2026-11-20, après les zones plus grandes) : Château 59 400, morts-vivants
  64 100, Barbares 77 300, Forêt 84 600, Tour 70 900, Donjon 68 800, Enfer 72 700, Nains 66 500.

## Choix à respecter
- **Tout le pixel art à ×2** (un pixel d'image = 2 × 2 pixels d'écran ; choix de l'utilisateur, 2026-11-10) : la taille à
  l'écran vient du nombre de pixels de l'image, jamais d'un autre agrandissement (voir `tools/art/METHODE.md`).
- **Les tailles communes de l'interface** (`Ui.cs`, 2026-11-11) : un écran prend ses tailles de bouton, de texte et de
  marge parmi celles de `Ui` (`ButtonHeight`, `NormalText`…), et ses longueurs tombent sur la grille (`Px`, `Snap`).
- **`GameState` gère plusieurs joueurs**, et l'IA joue avec les mêmes méthodes que le joueur humain. Ne pas supposer
  un seul humain (en vue du multijoueur) : l'affichage et les règles passent par le joueur du tour, pas par « le
  joueur ».
- **Toutes les règles restent dans `Core/`** (sans Godot), testables ; l'affichage ne fait que lire des événements.
- **Les données de jeu** (créatures, bâtiments, sorts, artefacts) sont dans `godot/data/`, pas dans le code.
- Rester proche des règles et des chiffres de Heroes 3, sauf choix contraire noté (charisme, magie…).
