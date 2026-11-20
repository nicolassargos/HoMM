# Méthode des images

Ce qui marche (et ce qui a été écarté) pour faire les images du jeu. À lire avant tout travail d'images. Les services,
les clés et le dossier `art-candidats/` sont décrits dans `CLAUDE.md` ; l'histoire de chaque image est dans
`HISTORIQUE.md` (étapes 5, 6 et 10).

## Règles générales
- **PixelLab** pour les personnages et les créatures (images de combat, animations) et des essais d'interface ;
  **Retro Diffusion** pour les cartes (terrains, décors, objets) et les portraits des héros.
- Les images sont trouvées par convention d'après les identifiants (`res://art/creatures/paysan.png`) : jamais de chemin
  d'image dans les règles. Une image absente garde le dessin par formes (ou la petite image de la carte).
- **LA RÈGLE (choix de l'utilisateur, 2026-11-10) : tout le pixel art est montré à ×2** (un pixel d'image = 2 × 2 pixels
  d'écran en 1920 × 1080) : la carte, le combat, les créatures, les cadres et boutons, les icônes, les artefacts, les
  portraits, les curseurs. **La taille d'une chose à l'écran vient du nombre de pixels de son image**, jamais d'un autre
  agrandissement : une image qui doit paraître plus grande est dessinée plus grande. Mélanger ×1, ×2, ×3 et ×4 cassait
  le style et obligeait à refaire des images. Exceptions qui restent à corriger : le héros sur le champ de bataille (×4,
  en attendant son image de combat), les petites images des créatures dans les emplacements étroits (×1). Le texte est
  calé sur la grille depuis 2026-11-11 (Pixel Operator HB 8 à 16 : un pixel de police = un pixel d'art, `Ui.NormalText`).
- (Avant la règle, pour mémoire : carte ×3 et ×4, combat ×3, héros au combat ×4, cadres et boutons ×3, portraits ×1 à ×3,
  créatures de l'interface ×2 à ×4, artefacts ×3, curseurs ×2,4 et ×3. Toujours un nombre entier de fois, pixels
  nets : c'est le mélange des facteurs qui gênait.)
- **Curseurs** (2026-11-10, choisis par l'utilisateur) : 32 × 32, montrés ×2 (`MapCursor.CursorScale`), PixelLab « pro »
  avec `objects/town.png` comme style (sinon un contour noir trop fort pour notre style) ; 64 candidats par curseur.
  Ce qui plaît : **vu de trois quarts** (pas de dessus) ; le gantelet montre de l'index (sa pointe = le pixel le plus
  haut, c'est là que le clic tombe) ; la botte **en marche** ; « visiter » : un gantelet qui tient une lueur. La botte
  choisie regardait à droite : retournée, pour aller dans le même sens que le gantelet.
- **Épées d'attaque au combat** (2026-11-10) : six directions faites d'une seule épée (C15 du lot des curseurs) : les quatre
  diagonales par simple retournement (pixels intacts) ; la droite et la gauche tournées de 45° (agrandir ×8, tourner, reprendre
  le pixel le plus présent de chaque bloc), puis **la lame redessinée à la main** : tournée, son contour du haut était en
  pointillés et sa pointe sortait de l'image (une épée en diagonale est plus longue qu'à plat sur 32 pixels). À plat, elle paraissait courte : allongée de 25 % (lame +6, poignée +2) dans une image de 40 × 32 (un curseur n'a pas à être carré). Le pommeau rond, perdu en tournant, redessiné à la main (44 × 32).
- **Flèche de tir** (2026-11-10) : PixelLab « pro » 32 × 32, l'épée C15 comme style (64 candidats, 0,08 $), la 15 choisie ; la flèche brisée (tir trop lointain) en est faite sans dépense : la moitié avec la pointe décalée de 3 pixels à droite et 1 en haut, deux rangées de hampe retirées.
- `tools/art/reduire.py` réduit une image sans la flouter (palette d'origine) et peut la retourner (`--miroir`) ;
  `tools/art/palette.py entree sortie 16` ramène à 16 couleurs ; `tools/art/adoucir.py` adoucit un contour noir ;
  `tools/art/detourer.py` rend transparent un fond uni ; `tools/art/cadre.py` rend transparent le centre uni d'un cadre.

## Références de Heroes 1 et 2 (règles)
- Les images de Heroes 1 et 2 (copie de l'utilisateur, `art-candidats/references/heroes1-2/`, hors de git ; extraites de
  `HEROES2.AGG` par `art-candidats/outils/agg.py`, qui ne fait que lire l'archive) servent **seulement à calibrer** :
  taille, proportions, composition, lumière ; on peut les montrer à côté de nos candidats sur une page de choix.
- **Jamais** comme image de départ d'un redessin (« input »), **jamais** comme image de style de PixelLab, **jamais**
  recopiées morceau par morceau, **jamais** dans `godot/art/` ni dans un commit.
- Ce qu'elles apprennent : chaque objet de la carte projette une **ombre** douce et semi-transparente (elle l'ancre au
  sol) ; un objet est un **petit bâtiment entouré de décor** (arbres, rochers, chariots), qui déborde un peu de son
  emprise ; contours sombres teintés de la couleur de l'objet, jamais noirs ; une ville vue de face fait 640 × 256,
  bâtiments très imbriqués, un arrière-plan fort (murs, montagnes) et un premier plan chargé. Les cases font 32 px
  (les nôtres 24 : leurs objets ×0,75 donnent notre échelle).

## Ombres de la carte (dessinées par le jeu, 2026-11-08)
- Ombre portée : `Game/ShadowLayer.cs`, réglages `--shadow=force,longueur,penche,contact,flou` et `--shadow-tune` (curseurs).
  Chaque morceau de l'image tourne autour de son pied ; un grand morceau (remparts) pose chaque colonne sur son sol ; les
  chemins vers l'entrée et les rails ne font pas d'ombre.
- Ombre de contact : d'après les **pieds** de chaque image (`godot/art/pieds.json`), proposés puis revus par l'utilisateur
  avec `tools/art/pieds.py` (page `revue-pieds.html` : glisser les points, ajouter ou retirer un pied, régler l'étalement).
  Méthode de l'utilisateur : le bas du contour entre le pixel le plus à gauche et le plus à droite, fermé par un arc au-dessus,
  puis des bagues de plus en plus claires.
- Les héros de la carte (2026-11-15) : seules les images fixes ont des pieds (la marche et l'ouest en miroir les
  reprennent) ; `pieds.py page heroes/` ne montre qu'eux. Les 48 propositions étaient presque toutes justes ; le
  nord-est demande souvent un second pied (le sabot avant, plus haut).
- Ce que la revue a appris (les propositions corrigées) : les pieds proposés étaient trop larges (1 à 2 pixels de chaque côté)
  et un pixel trop haut (corrigé dans `proposer`) ; un sommet ne garde qu'une petite base au milieu ; le château a un
  second pied le long de la rangée de tours du fond ; une mine a un pied par objet (un arbre collé à la cabane, une souche,
  une pierre), et l'entrée coupe le pied du rocher en deux.

## Carte (Retro Diffusion)
- Images de 24 px (cases de 48 px à ×2, puis ×3 et ×4 en 1920 × 1080). `tools/art/generer.py` estime le prix
  (gratuit), génère des candidats et installe le candidat choisi ; `tools/art/assets.json` décrit les images à produire.
- Forêts : des arbres seuls (`art/decor/`) semés par le jeu (`DecorLayer`) ; écarté : une image par case (trous
  réguliers). Montagnes : des massifs (voir « Montagnes de la carte »).
- Terrains : deux images par terrain (`grass-2.png`…), les bords sont calculés par le shader (pas d'images de bords).
- Routes (2026-11-15) : une case de 32 px par sorte (`terrain/road.png` terre, `road-gravel.png`, `road-cobbles.png`),
  Retro Diffusion `rd_tile__single_tile`, 4 candidats chacune (0,24 $) ; **les juger en jeu** (l'utilisateur ne choisit
  pas sur la case seule : copier chaque jeu dans `godot/art/terrain/`, capturer, remettre). PixelLab : « tiles pro »
  demande un abonnement Tier 1 ; ses jeux de transition (`pixellab.py tuiles`, environ 0,1 $) donnent une case qui se
  répète sans couture, mais plate et terne pour la terre et le gravier (Retro Diffusion bien meilleur) ; leurs bords
  d'herbe plaisaient à l'utilisateur (le liseré clair, l'ombre sur la route).
  Herbe (2026-11-09) : demander « natural mid green… not lime, not too saturated » ; Retro Diffusion ignore les petites
  fleurs d'une case : les fleurs, cailloux et plages de sable sont dessinés par le shader (`terrain.gdshader`), sans
  répétition et réglables (`--ground=`).
  Herbe (2026-11-16) : le vide d'une grande prairie se comble au shader, à l'échelle moyenne (taches sèches et drues,
  touffes, bouquets de fleurs) ; trop de touffes ou de grandes taches foncées font des plaques.
  Roche (2026-11-18) : la case de Retro Diffusion seule ressemble à de la terre gravillonnée ; les dalles et les fissures
  viennent du shader. Des fissures franches font de la boue séchée, des dalles rondes du cuir : des fissures discrètes,
  des dalles anguleuses et allongées, une pente légère et inégale d'une dalle à l'autre. Les pierres dessinées par-dessus
  n'ont pas plu ; la couleur brune d'origine non plus n'est pas à changer.
  Terre brûlée (2026-11-18) : les cases de la roche recolorées d'après leur luminosité suffisent, sans rien générer. Une
  fissure qui se lit comme telle n'est pas un bord de plaques (trop lisse : des brindilles) : c'est un trait en zigzag,
  avec des branches, noir, large au milieu et fin aux bouts, le feu seulement au fond de sa partie large ; des tailles
  très inégales, regroupées par régions. Une animation lente et décalée d'une fissure à l'autre reste discrète.
  Neige (2026-11-19) : les cases du sable recolorées en blanc bleuté, de faible contraste. Les congères étirées en
  longues bandes font des nuages : des congères rondes, l'ombre bleutée d'un seul côté.
  Arbres des sols (2026-11-19, 24 × 32) : finir la description par « single tree, … isolated object, no ground ».
  Arbres brûlés : fins et sombres, on ne voit que des pixels ; demander un tronc épais, des reflets de cendre grise sur
  les bords, des branches épaisses et peu nombreuses, des fentes orange, « clear readable silhouette, strong light and
  dark contrast ». Les palmiers sortent vert vif : les ternir vers l'olive (moins saturés, un peu plus sombres). Sous les
  arbres de la neige, retirer la tache de neige (pixels clairs et bleutés du bas, pas le tronc blanc des bouleaux).
- Portraits : 64 × 64, style « retro », des visages marqués plutôt qu'idéalisés, trouvés d'après le nom du héros.
  28 portraits de plus (2026-11-07, 5,58 $ : 3 candidats chacun, 3 refaits) : barbares, chevaliers, sorciers, mages,
  gens des bois, morts-vivants, démons. Ce qui marche : décrire un détail qui se voit (cicatrice, cache-œil,
  corbeau, lunettes) et **l'âge** (« about fifty ») ; sans âge, une femme mage sort jeune et lisse, et un
  vampire tourne au Dracula de dessin animé (préciser « old, gaunt, not cartoonish »).
  **Un fond par héros** (choix de l'utilisateur, 2026-11-07) : tous les portraits (35) refaits avec une scène
  (« background: a snowy pine forest at dusk ») à la place de « dark plain background » (`slug@fond` dans
  `assets.json`, 6,48 $). La lumière de la scène tombe sur le personnage. Écartés : détourer le noir et poser un
  fond dessiné ou généré (le personnage a l'air collé, et les habits noirs se trouent au détourage).
  **Garder un visage et changer le fond** (Sire Roland) : son fond est du noir pur, et rien de noir sur lui ; détouré
  (le noir relié aux bords, seuil 6) puis posé sur un fond généré seul (« empty background scene, no people »),
  assombri à 85 % : propre, sans contour. Le refaire passer par Retro Diffusion pour fondre la lumière (`input`,
  `strength` 0,2 ou 0,35) change l'expression du visage : écarté.
  **Des visages nuancés** (demandé par l'utilisateur, 2026-11-07 : assez des jeunes beautés, des vieillards et des
  laiderons) : « characterful imperfect features, not idealized, gritty » poussait vers la laideur, et sans âge les
  femmes sortent jeunes et belles. Ce qui marche (`slug@nuance`) : un **âge en chiffres** (35 à 60), **une expression
  nette** (« a cocky smirk on one side of the mouth », « deadpan unimpressed stare », « a sly knowing grin »), et
  « a real lived-in face, ordinary proportions, neither pretty nor ugly » à la fin. Pour une femme, l'âge ne suffit
  pas : ajouter « visible wrinkles around the eyes and mouth, a few grey hairs, weathered skin, a slightly crooked
  nose, no makeup » (`@nuance2`). Des « gueules » de films d'action des années 80-90 : décrire les traits (yeux
  lourds et lèvre retroussée, crâne rasé et barbe de trois jours…), **sans nom d'acteur** (pas de ressemblance avec
  une vraie personne).
  **Le choix de l'utilisateur** (2026-11-07) : une page de choix (artifact claude.ai, un clic par héros, choix lus
  ensuite) avec tous les candidats de chaque héros ; il a choisi les 41 lui-même. Pour les prochains portraits :
  **pas seulement des humains** (squelette, orc, nain, elfe, gnome, barbare vraiment sauvage), **l'allure des jeux
  des années 80-90** avec des **fonds simples et dramatiques** (un ciel de feu, une lune, une lueur verte), pas
  seulement de belles images. Ses préférés : Comte Morvan, Mortimer, Tarka, Ulrik.
  **Les héros nains, à la Heroes 2** (2026-11-12, choix de l'utilisateur, 5,4 $ en quatre essais) : regarder d'abord la
  planche des portraits de Heroes 2 (`HoMM-images/art-candidats/references/heroes1-2/heroes 2/`). Écartés : des visages
  réalistes ; puis la « panoplie du nain » (lunettes, casque à lanterne, barbe fourchue, cache-œil, graisse : trop
  cliché) ; puis des visages de face sans traits de nain (on ne voyait plus des nains) ; **pas de femmes à barbe**.
  Ce qui marche (`@h2n`, `@h2f` dans `assets.json`) : la fin « 1996 hand-painted VGA fantasy strategy game hero
  portrait, head and shoulders filling the whole frame, face straight towards the viewer… detailed bright painted
  landscape background » ; pour un nain, « very wide square face, large broad nose, deep-set eyes under heavy brows,
  ruddy cheeks, thick neck, massive broad shoulders » et une longue barbe ; pour une naine, sans barbe, c'est la
  carrure et l'armure qui la disent naine : « stout heavyset dwarf woman warrior, short and very broad, round heavy
  face… huge armoured shoulders, smooth bare chin », un casque, des tresses épaisses à anneaux de métal.
- **Petites images des créatures (24 × 24, la carte)** : ne jamais réduire une image de combat (bouillie), même en la
  donnant à Retro Diffusion comme image de départ. Pour qu'elle ressemble à l'image de combat : PixelLab `pro` à
  24 × 24, l'image de combat comme sujet, la petite image du Gobelin (`godot/art/creatures/gobelin.png`) comme style
  (64 candidats pour 0,08 à 0,18 $).
  **Attention au modèle de style** (2026-11-07) : la petite image du Gobelin a une grosse tête ; elle donne à l'Ange,
  au Vampire et au Dragon une allure de dessin animé (refusée par l'utilisateur). Pour une créature aux proportions
  normales, prendre une petite image aux proportions normales (l'Archer) et ajouter « realistic adult proportions,
  small head, not chibi, not cartoonish » ; pour le Dragon, son ancienne petite image comme style. D'après une description seule : Retro Diffusion `rd_tile__tile_object`, 24 px,
  « front view, simple, clear silhouette, few details ».

## Créatures de combat (PixelLab)
- **Leçons des Barbares (2026-11-09)** :
  - Un **visage** se perd une fois réduit (dans l'ombre d'un chapeau) : `pixellab.py retoucher` la grande image
    (« make the face bigger and clearly visible… keep everything else ») puis la réduire de nouveau.
  - Un **éclat** dessiné par-dessus une main : l'effacer laisse des trous (rien dessous). Combler les petits trous de la
    couleur des voisins (seulement autour de la main), ou remplacer l'image par sa voisine (`retoucher --copier`).
    `--eclats` ne touche pas un blanc ou un orange que la créature porte déjà (l'Oiseau-tonnerre) : remplacer l'image.
  - Un **volant** au repos debout : son vol doit partir d'une **pose en vol** (retouche de la grande image : corps à
    l'horizontale, serres repliées, ailes levées) avec `animer … --depart=` ; l'envol est un état `transform` (joué avant
    le déplacement et à l'envers après). Couper les images où la pose est tenue (sinon il « lévite »).
  - Une **boucle** (galop) : retirer les images du départ et du retour à l'arrêt ; « moveFrames », « move » (images/s) et
    « stepTime » dans `animations.json` règlent la vitesse (une marche fait un cycle par case par défaut).
  - Un **éclair** (ou tout effet qui tombe sur la cible) : le jeu le joue sur la case de la cible
    (`BattleEffects.OnHex`) ; l'attaque ne montre que le geste (Oiseau-tonnerre : « raises its wings high… lifts its head
    toward the sky with its beak wide open in a fierce cry », sans les mots foudre ni orage). Le second modèle déploie
    plus les ailes mais ajoute parfois une étincelle : `retoucher --copier=2:3 --detaches`.

- **Échelle commune** (combat à ×2 depuis le 2026-11-09 : un pixel d'art = 2 pixels de l'écran, comme sur la carte) :
  un humain fait environ 70 à 81 px de haut (à peu près une case), le Gobelin environ 42, l'Orc 62, le Zombie 66, l'Ogre
  87, le Troll 96 (orc < ogre < troll), le Colosse 105, l'Ange 105, le Béhémoth 108 ; le Dragon, long, couvre 2 cases
  (113 de haut). Les hauteurs sont dans les fiches (`creatures-art.json`). Générer assez grand : la grande image doit
  dépasser la hauteur voulue (sinon pas de détail en plus).
- **Contour et bord** (fiche, facultatifs) : « contour » (assombrir, seuil, passes ; 0,6, 45, 2 par défaut) adoucit un
  contour sombre ; « bord » (0,6 : le Farfadet) rend le bord semi-transparent (traits d'un pixel gardés pleins), pour
  qu'une créature se fonde dans le décor. Juger en combat, à la vraie taille (une planche agrandie trompe).
- **Leçons de la Forêt (2026-11-09)** :
  - Choisir l'image fixe **en combat** : poser des candidats dans le jeu (`art/creatures/battle/essai-<k>.png`, effacés
    ensuite) et les voir avec `--creature-preview=essai-1,…` ; régler la taille là (le Sanglier ×0,85).
  - Une créature large (le Tréant) : agrandir le cadre de génération (« generation » : 144 × 160), sinon PixelLab la
    coupe ; vérifier que la grande image ne touche pas son bord.
  - « enemy » dans une consigne d'attaque fait dessiner une autre créature (le Farfadet) : décrire seulement le geste.
  - Une référence d'image doit exister dans le dossier de travail (sinon le lot échoue sans bruit) ; une image de 740 px
    est réduite à 256 ; une AVIF se convertit avec Windows (`System.Windows.Media.Imaging.BitmapDecoder`).
  - Vérifier le sens de l'image choisie avant de poser « miroir » (le Druide L10-3 regardait déjà à droite) ; une feuille
    faite dans le mauvais sens se retourne image par image, sans dépense.
  - Trous laissés par un éclat effacé : `tools/art/combler.py feuille.png 8 3,4` ; un éclat blanc sur une créature qui
    porte du blanc (yeux, barbe, ventre) s'efface à la main dans la zone de l'éclat seulement.
  - **Jamais deux commandes qui écrivent `creatures-art.json` en même temps** (animer, choisir) : la dernière efface les
    choix de l'autre.
- **Voir les animations candidates** : `tools/art/apercu_anims.py <id>` (page `en-cours/<id>-anims.html`) les joue à la
  taille du jeu, entières, avec une large marge et la copie retournée bien à l'écart (la page de `creature.py page`
  coupe les grandes attaques et les grandes ailes). Remarques de l'utilisateur (2026-11-09) : un **volant** doit voler
  assez lentement pour battre des ailes (`stepTime` dans `animations.json` : 0,4 s par case pour l'Ange, un battement
  toutes les deux cases) ; un cycle de vol où le corps se plie puis se redresse est bizarre sur un long vol (plusieurs
  cycles) ; une attaque « gifle » : le geste à hauteur de poitrine, et « no light, no glow, no motion lines, no trail »
  dans `--sans` ; un battement d'ailes coupé au bord : `--largeur` élargit aussi le cadre d'un déplacement.
- **Refaire une créature à une autre taille sans dépense** : la grande image refait l'image fixe ; chaque image d'une
  feuille se retrouve parmi les images brutes de PixelLab gardées dans les archives (la plus ressemblante à l'ancienne
  taille), puis se refait à la nouvelle (`tools/art/reconstruire.py <archives> <rapport.json> <facteur> id…` ; sans
  grande image, `tools/art/trouver_grande.py` la retrouve dans les archives). Les retouches à la main reviennent (éclats, étincelles) : les refaire
  (`retoucher --eclats --detaches`, ou effacer un éclat blanc à la main quand la créature porte du blanc).
- **Une créature à la fois** : d'abord l'image fixe (qui sert aussi d'icône), puis, une fois acceptée, ses animations
  (attente, déplacement, attaque : feuilles de 8 images, 4 colonnes × 2 rangées, `{id}-idle.png`, `-move` (marche ou
  vol ; anciennement `-walk`), `-attack`). Une feuille peut avoir 16 images (4 × 4 : `moveFrames` dans
  `animations.json`, `creature.py animer … --images=16`), pour un mouvement lent mais fluide.
- **Outil** : `tools/art/creature.py` enchaîne les étapes (`fixe`, `choisir … fixe`, `animer`, `retoucher`,
  `choisir … idle`, `retailler`, `page`) d'après la **fiche** de la créature (`tools/art/creatures-art.json` : description, sujet,
  hauteur, taille de génération, modèle de style, image d'angle, références, miroir, corde, mouvements, choix faits) et
  un **gabarit** par sorte de créature (humain, volant, quadrupède, grand). Les grandes images choisies sont gardées
  dans `art-candidats/references/derives/`. Chaque feuille est vérifiée (corps qui glisse, attaque qui ne revient pas à
  sa place, autre personnage ou effet, éclats, image coupée au bord, traits fins qui clignotent) ; la **page de la
  créature** (`en-cours/<id>.html`) montre images fixes et animations sur des cases du champ, face au défenseur.
  Un déplacement y traverse quatre cases à la vitesse du jeu. `retailler <id> <hauteur>` refait l'image fixe et les
  feuilles choisies à une autre taille, sans dépense. En jeu : `--creature-preview=id,…` (avec `--battle-auto`).
- **Image fixe** : modèle « Pro » (`pixellab.py pro`, 25 candidats pour 0,08 $ à 40 × 52), une de nos images pour le
  style et une image de référence pour le sujet. **Modèles de style** : le Moine, le Griffon, le Loup et le Troll
  (choix de l'utilisateur). Une image de style trop forte recopie la créature du modèle.
- **Générer au double** (~100 px de haut) puis réduire de moitié (`reduire.py … 0.5`) : une image générée à ~50 px est
  brouillonne. Puis 16 couleurs (`palette.py`), sauf si cela fait des bandes (l'Ogre est gardé sans réduction de palette).
- **Angle de vue** : demander « vu de trois quarts, un peu d'en haut » ; une image de référence marquée « angle
  seulement » (`image.png::usage`) marche, mais seulement sans autre image du sujet vue de côté.
- Décrire l'idée en termes de jeu rétro (« classic 1990s fantasy strategy game sprite, simple bold shapes ») plutôt que
  recopier une image moderne ; une image de référence de pose recopie aussi ses détails (à éviter).
- **Contour noir** ajouté par PixelLab : `tools/art/adoucir.py entree sortie 0.6 45 2`, sur l'image fixe et les feuilles.
- **Animations** : à partir d'un cadre un peu plus grand (48 × 56, 64 × 56 pour une attaque qui va loin), `--retour`
  pour qu'une attaque finisse sur la pose de départ ; deux essais par animation en alternant les deux modèles ; au plus
  8 tâches à la fois. Les grandes créatures s'animent au double (jusqu'à 256 px), puis chaque image est réduite.
- **Pas d'effet dans les animations** (décision de l'utilisateur) : éclats, sang, traînées, projectiles seront dessinés
  par le jeu (le projectile vole jusqu'à sa cible, un petit impact se joue sur elle). Décrire **seulement le mouvement
  du corps** : les mots « frappe », « coup », et même « pas d'éclair », attirent les effets ; `--sans=no other
  character` remplace la fin de texte du gabarit. Une attaque de tireur ne montre que le geste (bander, lâcher).
  Une attente ou un déplacement : sur place (le jeu déplace la créature) ; pour un volant, un battement d'ailes sur place.
- **À retirer après coup** (`creature.py retoucher`) : étincelles cyan, éclats d'impact (`--eclats`, ou
  `--eclats=partout` : rouge vif et blanc pur aussi sur le corps), couleurs absentes des images voisines (`--nouveau=5`),
  une image ratée remplacée par une autre (`--copier=7:8`), un petit morceau détaché (`--detaches`), une marche qui glisse (`--recentrer`), la corde d'un arc
  (`--corde=7,8`) ; un adversaire dessiné dans l'animation : jeter l'essai.
- **Transformation** (facultative) : `{id}-transform.png`, jouée une fois avant un déplacement et à l'envers à l'arrivée
  (`animer … transform --sans-retour`) ; puis animer le déplacement depuis sa dernière image (`--depart=…/9.png`).
  `--largeur=3` élargit le cadre d'une attaque (cape ou ailes déployées).
- **Vitesse et pauses** : `art/creatures/battle/animations.json` (images par seconde par animation, pause de 3 à 6 s
  entre deux attentes, `stepTime` : temps pour traverser une case ; par défaut, une créature qui marche fait **un cycle de déplacement
  par case** (8 images à 10 par seconde : 0,8 s), un volant 0,09 s ; le Zombie 0,45). Au combat,
  une troupe regarde où elle va (miroir) et se tourne vers sa cible. L'utilisateur trouve les animations un peu chargées : préférer des mouvements discrets.
- **Leçons de la Tour** (2026-11-11) :
  - un pied coupé en marchant (les pieds au bas du cadre de génération) : `"marge_bas": 0.08` dans la fiche laisse de la
    place sous les pieds, retirée ensuite des feuilles (la créature ne flotte pas) ;
  - un éclat qui couvre le corps ne s'efface pas (il laisse un trou) : remplacer l'image par sa voisine (`--copier`), ou
    remplacer chaque pixel d'effet par la moyenne de ses voisins de pierre (une créature grise) ; **ne pas redessiner
    les images couvertes avec `pixellab.py retoucher`** : la créature change (le Golem sans tête y gagnait une tête) ;
  - « ram », « slam », « strike » font dessiner une explosion presque à coup sûr : décrire le corps (« throws its torso
    forward, chest first ») ; une créature qui avance vraiment : « charges two steps forward », `--largeur=3.2` ;
  - au-delà de 224 px de large, un des deux modèles refuse 16 images : 8 images pour une attaque large ;
  - une partie de l'image fixe (une main levée) reste figée dans toutes les animations : choisir l'image fixe en pensant
    aux animations ;
  - une marche ratée où la créature ne bouge pas les pieds fait une bonne attente (le Géant) ; une marche en deux temps :
    la transformation pose l'objet (la colonne sur l'épaule), la marche part de sa dernière image ;
  - un muscle trop gros, « stéréotypé » : « natural proportions, lean, NOT muscular », l'ancien candidat en référence ;
  - petite image de la carte : des armes blanches font des taches blanches à 32 px (« un fond blanc ») : demander
    « dark grey steel blades » ;
  - plusieurs points de départ d'un tir : `launchPoint2` (la seconde main) dans `animations.json`, mesurés sur l'image
    où le tir part ; `--effect-preview=eclair` montre l'éclair sans cesse pour une capture.
- **Leçons du Donjon** (2026-11-14) :
  - **Animer directement** (`pixellab.py animer depart.png dossier "action" 8 graine --retour`, sans le « sujet » ni la
    fin de texte du gabarit) : bien plus propre que `creature.py animer` (qui faisait venir éclats, fumée, autres
    personnages), environ 0,02 $ l'essai. Les images de départ : `creature.departs` ; les feuilles : `traiter_feuille`,
    puis retirer les morceaux détachés du corps (sauf à 2 pixels : un bout de tentacule coupé par la réduction).
  - **Un quadrupède qui marche, des cous qui bougent (l'Hydre)** : le premier modèle ne bouge presque pas les pattes,
    même avec « walk cycle… lifts its legs high » ; le second (`--minimax`, avec le « sujet » de la fiche, vue de côté) fait
    un vrai pas et des cous qui s'étirent, sans éclat (« … ; no effects »), environ 0,015 $. Le premier ajoute des éclairs
    quand on décrit des têtes qui frappent « in all directions ». Le solde se met à jour en retard : le relire plus tard.
  - Une marche qui semble courir : la ralentir (`"move": 6` dans `animations.json`, un cycle par case). Des cous qui
    vont trop loin : « the heads stay close to the body, the necks bend and lunge a short distance » ; une tête qui
    mord vers le haut devient un cou de girafe.
  - **Une créature qui flotte** (le Tyran œil) : le jeu ne soulève pas une créature qui vole ; « flotte » dans la fiche
    (pixels vides sous elle, image fixe et feuilles) ; pas de pied pour l'ombre de la carte.
  - **Une créature tournée vers la gauche** (le Géant, qui marchait à reculons) : retourner l'image fixe et chaque
    feuille, image par image ; vérifier le sens de toute créature avant ses animations.
  - Un rayon, un éclair : dessinés par le jeu (`projectile` : `rayon`, violet, droit, plus épais au milieu, une lueur sur
    l'œil) ; `launchPoint` mesuré sur l'œil dans l'image du lâcher.
  - Un **fouisseur** (le Ver) : la transformation le montre s'enfoncer derrière son tas de terre, puis le tas s'effondre ;
    le jeu dessine le tas et le sillon de mottes (une carte de hauteurs vue un peu d'en haut, pas de côté ni d'en haut)
    qui s'affaisse en s'effaçant ; les images de la transformation et de l'attaque calées pour que la terre touche le bas
    de la case.
  - **La vue d'ensemble** (2026-11-15, demandée par l'utilisateur à chaque créature commencée ou finie) :
    `python -I tools/art/apercu_faction.py <id>` écrit `art-candidats/en-cours/ensemble-<id>.html`, sa faction et les
    créatures de son niveau, animations jouées : on juge le style de toute la faction, pas une image seule.
  - **Des fonds de papier blanc** restent sur bien des candidats (« plenty of white pixels ») : remplir depuis le bord
    et effacer aussi les taches blanches enfermées (par exemple entre un bras et le corps), en gardant l'original.
  - **Une image nette** : une image fixe gardée à sa taille de génération (le Tourmenteur, le Démon) est bien plus nette
    qu'une image réduite (la Succube, un peu floue) ; générer à la taille du jeu quand c'est possible.
  - **Un fluide** (le Limon de magma) : son attente tourne sans pause (`idlePauseMax` 0), et un mouvement lent se fait en
    **32 images** (`--images=32`, à 20 images/s, `moveFrames` 32) : en 16 images, il glissait ou on voyait les images
    manquer.
  - **Un objet lancé et ramené** (la chaîne du Tourmenteur) : l'animation `throw` (un état à part de l'attaque) ; le jeu
    dessine la chaîne avec **les pixels de l'image** (un maillon et le crochet découpés, posés en `-chain.png` et
    `-hook.png`) ; un point de départ par image (`throwPoints`), pris à l'intérieur de la chaîne dessinée, pour qu'il n'y
    ait pas de trou ; nombre de maillons arrondi vers le haut. Une chaîne dessinée autrement que celle de l'image se
    remarque tout de suite.
  - **Le feu ou les traînées de PixelLab** (Démon, Tourmenteur, Cultistes) : les retirer des feuilles (les pixels très
    rouges, r ≥ 235, puis les restes isolés) et laisser le jeu dessiner l'effet. Un jet de feu au corps à corps :
    `meleeProjectile` (« flammes ») dans `animations.json` ; il reprend les outils du feu des sorts (`SpellEffects.FireJet` :
    paliers de couleur de la Boule de feu, boule qui bouillonne, fumée, braises) : un jet fait de couches unies ne
    ressemblait pas à du feu.
  - Une créature qui vole trop vite (le Diablotin) : `stepTime` (0,3 s par case pour un petit volant).
- **Leçons du Château** (2026-11-17) :
  - **Un cheval qui galope** : PixelLab l'anime souvent comme un chien ou un cerf (il bondit). Ce qui marche : un essai
    de **16 images** (`--images=16`) avec le texte des Nomades (« the horse gallops forward to the right, its four legs
    in a clear fast gallop, the rider leaning forward »), puis garder **le cycle exact** : comparer la forme des jambes
    d'une image à l'autre (le bas de chaque image, pixels opaques) ; l'image qui ressemble presque à l'identique à une
    image plus haut (écart d'environ 40, contre plus de 1000 ailleurs) ferme le cycle (le Cavalier : images 4 à 12, soit
    9). Une image en trop au bouclage se voit (deux poses pareilles de suite). **8 images/s** pour 0,4 s par case : à 16,
    les jambes allaient trop vite pour la vitesse du déplacement.
  - **Une image nette** : une créature réduite depuis un grand cadre est floue ; la garder à sa taille de génération
    (la « hauteur » de la fiche égale à la hauteur recadrée de la grande image). Pour la faire plus grande, redessiner
    le candidat choisi dans un plus grand cadre (lui-même en référence), jamais l'agrandir.
  - **Petites images de la carte** : avec le Moine ou le Cerf comme modèle de style, le Chevalier sortait trop
    contrasté, illisible (« crunchy ») ; avec la petite image de l'Épéiste (gardée :
    `art-candidats/references/style-carte-epeiste.png`), un dessin plus doux et lisible.
  - Pour rendre une créature moins impressionnante (le niveau 4) : un équipement modeste, un cheval ordinaire, un
    visage jeune, sans moustaches.
- **Leçons du Loup, du Moine et du Troll** (2026-11-19) :
  - **Vérifier le sens** : une image fixe peut regarder vers la gauche (le Moine de trois quarts) ; l'utilisateur l'a vu
    au combat (« il ne fait pas face à son ennemi »). `miroir` dans la fiche retourne la grande image (image fixe et
    départs des animations) ; l'attente et la marche se retournent sans dépense, mais une attaque tendue vers l'avant
    est à refaire depuis l'image retournée.
  - **Un lanceur de sorts** : l'attaque montre seulement le geste (lever le bâton, le pointer vers la droite et le
    tenir) ; « no light, no glow » et le jeu dessine le sort (`projectile`). PixelLab ajoute quand même des éclairs :
    remplacer ces images par la pose tendue (`--copier`), qui devient la pose tenue pendant que le sort part.
  - **Une gueule fermée** (un loup moins agressif) : une image de départ gueule fermée (retouchée par `pixellab.py
    retoucher`) et « its mouth stays fully closed the whole time » ; même ainsi, garder le cycle exact de la marche,
    les dernières images rouvraient la gueule.
  - **Un saut sur l'ennemi** : « crouches low, then springs forward in one long leap through the air, all four paws off
    the ground… lands far ahead, then walks backward to its starting place », dans un cadre large.
  - **Une grande créature de face qui marche** (le Troll) : sans consigne, ses pieds s'écartent ; « the feet always stay
    the same distance apart and never spread wide », pieds posés « under its hip ».
  - **Les taches que `retoucher` ne voit pas** (un vert vif proche de la peau, un anneau gris) : à la main, dans un
    rectangle autour, effacer ce qui sort de la silhouette d'une image propre voisine (élargie d'un pixel) et les
    couleurs vives ; ne toucher que les images tachées et seulement au-dessus des épaules, sinon un bras qui dépasse
    est coupé (l'utilisateur l'a vu).
  - **Une mort sort du cadre** quand la créature tombe plus bas que ses pieds (face contre terre) : la refaire avec de
    la place sous les pieds (`marge_bas` 0,12 à 0,15 dans la fiche, le temps de la génération). Vérifier la dernière
    rangée des images brutes (des pixels opaques sur toute une largeur : coupé).
  - **Une mort lente et lourde** (le Troll) : 16 images (`--images=16`, environ trois fois le prix de 8) et des étapes
    qui s'enchaînent : « sways, takes one heavy stumbling step backward, its knees bend and it sits down hard, then its
    upper body slowly falls back… each stage flows into the next ». Basculer d'un bloc en arrière n'est « pas
    naturel » (l'utilisateur) ; s'effondrer en avant « like a falling rock » non plus.
  - **Un recul bien visible pour une grande créature** (Dragon, Hydre, 2026-11-20) : le second modèle, 8 images,
    « recoils violently from a blow: it rears back onto its hind legs… its wings snap wide open, its tail lashes, then
    it drops back down to exactly its starting pose » ; pour l'Hydre, « all the heads are thrown back on their long
    necks like whips, then they swing forward again one after another ». **Prix** : une grande créature coûte bien plus
    par essai (8 coups reçus du Dragon et de l'Hydre : environ 1,05 $). Le même jour, l'utilisateur leur a préféré
    ceux du modèle v3 (voir « Les coups reçus » plus bas) : les ailes de ce Dragon s'ouvrent jusqu'à le tourner vers nous.
  - **Une créature qui avale** (le Ver, état `swallow`) : « rises very tall out of its hole, its body arching high up
    and over to the right, then its head comes straight down from above, mouth-first… a big round lump slides down
    inside its pale body ». Sans « from above », il mord de côté et crache parfois une boule ou un œuf. Les alertes
    « pixels en plus » et « éclat » sont fausses ici (corps pâle, plus haut que d'habitude). 4 essais : environ 0,06 $.
  - **Filmer le jeu** pour vérifier un geste : `--record=dossier --record-delay=s --record-time=s
    --record-rect=x,y,l,h` (images WebP), avec `--effect-preview=coup`, `mort` ou `avaler`.
  - **Le second modèle refuse 6 images** (coups reçus) : multiples de 4 seulement ; un coup reçu en 8 images marche
    aussi (le Loup, le Moine).
- **Leçons des Nains** (2026-11-16) :
  - **Pas de clichés** (choix de l'utilisateur) : chaque créature a sa silhouette, son âge, son visage, des traits
    exagérés (« a grotesque caricature in the style of a fantasy board game miniature, exaggerated features ») ; une
    créature de la faction sert de modèle **de rendu seulement** (« completely different face, body shape, hair and
    gear ») : avec le même croquis pour toutes, les sept nains avaient la même barbe. Oser plus fou.
  - **Une image fixe immobile** : une monture décrite « charging » galope sur toutes les images ; écrire « stands still
    on all four hooves, not leaping » et donner l'image choisie comme modèle d'allure.
  - **Un envol** (le nain ailé) : transformation `--sans-retour` qui finit en l'air, puis le vol animé depuis sa dernière
    image (`--depart=…/9.png`) : l'envol, le vol et l'atterrissage (l'envol à l'envers) se suivent.
  - **Le nettoyage du cyan** efface aussi ce qui est cyan pour de vrai (les yeux de la tête de pierre, le cristal du Roi
    de cristal : 300 à 400 pixels par image) : `"garder_cyan": true` dans la fiche, ou remettre les pixels après coup.
  - **Les trous** d'une feuille (pixels vides enfermés) : ne boucher que les petits (une douzaine de pixels au plus) ;
    les grands sont de vrais jours (entre une corne et le cou, un bras et le corps).
  - **Une traînée ou un éclair collé au corps** : si l'image d'après a la même pose sans l'effet, la copier par-dessus
    (le marteau du Roi de cristal) ; sinon effacer l'effet et reboucher la forme rangée par rangée (la tête de pierre du
    Porte-étendard).
  - **Un objet lancé au corps à corps** (la bombe) : effacer l'objet qui part dans la feuille et le faire dessiner par le
    jeu (`meleeProjectile`, `launchFrame`, `launchPoint` : là où l'objet quitte la main) ; une explosion de zone joue une
    fois, les autres troupes touchées n'ont pas de nouveau geste (`UnitStruck.Splash`).
  - **Une image de la carte au contour trop fort** (la chèvre) : un autre modèle de style plus doux (`cerf.png`) et « thin
    dark brown outline only » ; l'utilisateur a pourtant gardé un candidat du premier lot.
- **Les morts** (2026-11-17) : animées directement (modèle v3, sans le « sujet », `--sans-retour`, 8 images), depuis la
  grande image dans un cadre de la largeur de l'attaque (la créature couchée est plus large) ; environ 0,017 $ l'essai.
  La consigne : « is mortally wounded: staggers backward, drops to its knees and collapses face down, lying motionless
  on the ground at the end; no effects, no blood, no other character » (le quadrupède « its legs give way… onto its
  side », le volant « stops flying and falls down… »). Un essai suffit pour presque toutes. Ce qui rate : « crumbles into
  rubble » (le Golem reste debout, ou une fente blanche s'ouvre) : le faire **basculer** (« topples forward and falls
  face down ») ; des taches blanches (poussière) sur la Gargouille : une autre graine. Les os qui se défont (Squelette,
  Colosse d'os), la lave qui refroidit (Limon), l'arbre qui bascule (Tréant) marchent bien.
  **Des morts variées** (2026-11-19, revues avec l'utilisateur) : la même consigne fait tomber tout le monde en avant ;
  l'utilisateur veut de la variété (en arrière, sur le côté, un dernier geste) et une mort propre aux créatures qui s'y
  prêtent. Ce qui marche :
  - **Aucune interdiction dans la consigne** : « no fire », « no flash », « no light » font **naître** le feu, l'éclair
    ou la lueur. Décrire seulement ce qu'on veut voir, et finir par « no other character ».
  - **En arrière** : « falls backward to the left, away from the direction it faces » (sans « to the left », PixelLab
    le fait souvent tomber en avant). Un geste avant la chute aide : s'asseoir lourdement, rugir les bras levés, tourner
    sur soi, se serrer la gorge.
  - **Ce qui se défait** (os, pierres, bronze, robe vide, marteau qui tombe) : de la place sous les pieds pendant
    l'essai (`marge_bas` 0,14 provisoire), puis remonter ce qui descend sous les pieds de départ ; sinon un morceau qui
    tombe est coupé en bas. Nommer chaque morceau et dire qu'il reste visible au sol (« the horned bull head drops in
    front, fully visible »). Encore difficile : le Moloch défait jusqu'aux jambes, le Golem vraiment pierre à pierre ;
    « crumble », « dissolve » restent peu sûrs.
  - **Une partie qui disparaît ou devient une masse** (la tête du Moloch, 2026-11-20) : la découper dans une image où
    elle est nette et la poser par le code, image par image, sur le chemin que PixelLab lui donnait (elle recouvre la
    masse). Une créature découpée en morceaux qui tombent par le code « éclate » au lieu de « se démonter » : refusé.
    **Prix** : le second modèle coûte environ 0,65 $ l'essai pour une grande créature (le Moloch) ; vérifier avant.
  - **Une mort presque bonne se finit à la main**, sans nouvel essai : une fin recolorée (le Démon : un corps calciné
    écrasé vers le sol, puis le tas recoloré en cendre grise) ; une robe restée debout écrasée vers le sol image après
    image ; ou **une suite animée depuis la dernière grande image** (`--depart=<dossier de l'essai>/9.png`, même
    cadre), dont on colle deux images à la fin en retirant deux images presque pareilles du début (le Béhémoth, dont
    les bras restaient levés). Un objet coupé au bas du cadre ne se répare pas à la main (le marteau du Roi de
    cristal : le manche relevé se voit) : refaire l'essai avec de la place sous les pieds.
  - **Dans un worktree, vérifier d'abord la grande image** (`grande` de la fiche) : `art-candidats/` n'est pas suivi
    par git, chaque worktree a sa copie, parfois d'avant une refonte (l'Archer et le Griffon du Château, 2026-11-19 :
    tout un lot animé depuis les anciennes créatures). La comparer à celle du dossier principal, ou à l'image fixe en
    jeu, avant de dépenser.
  - Revue : une page par tour, l'essai actuel et deux nouveaux par créature, les essais commentés du tour d'avant à
    côté ; environ 20 % refaits à chaque tour.
- **Les coups reçus** (2026-11-17) : animés directement (modèle v3), **avec retour** à l'image de départ, **6 images**
  (joués à 12 images/s : une demi-seconde), même cadre que la mort ; environ 0,016 $ l'essai. **Ne jamais parler du coup
  ni de la douleur** (« is struck by a blow », « recoils in pain ») : PixelLab dessine alors une étincelle ou un éclair
  d'impact sur la créature, et « no sparks, no flash » en ajoute encore. Décrire seulement le geste, **avec le sens** :
  « flinches away to the left: its head and shoulders jerk backward and it leans back to the left, arms pulled in, then
  recovers back to exactly its starting pose; stays on the same spot, nothing else in the picture ». Sans « to the
  left », le Fantôme tend le bras vers l'avant (l'utilisateur : on dirait qu'il attaque). Une créature de face (le
  Troll) bouge à peine. Une étincelle reste parfois sur une image (Djinn, Arbalétrier) quelle que soit la graine.
  Les grandes créatures (Dragon, Hydre, Démon) ont résisté à cinq revues : elles bougent, mais pas comme sous un coup
  (le Dragon penche même en avant quoi qu'on écrive). **Une image clé** guide le geste (l'idée de l'utilisateur : « que
  la créature bascule »), donnée en dernière image (`last_frame`). Tournée d'un bloc autour des pieds, elle donne un
  corps raide qui pivote, les pieds en l'air (refusé : « pas naturel »). **Ce qui marche : la pose de départ pliée en
  arrière**, les pieds fixes, chaque rangée décalée d'autant plus qu'elle est haute (en courbe, 14° en haut) ; PixelLab
  anime l'aller (départ → clé, 4 images : « is hit hard and bends backward from the blow, the feet stay planted on the
  ground, the upper body and head are thrown back… ») **et le retour à part** (clé → départ : « recovers from a blow:
  straightens up… »), pour que le corps vive aussi en revenant ; la feuille : l'aller, le retour, la pose de départ
  (8 images). « is hit hard » fait encore dessiner un éclat à l'image 3 : la remplacer par la 4e. Des morceaux détachés
  (une tête de trop pour l'Hydre) : les retirer ; un éclair blanc : l'effacer. L'utilisateur a pourtant refusé ces
  images clés (on voit l'image écrasée). **Le mieux pour ces trois : le second modèle (`minimax`, avec le « sujet »,
  retour à la pose de départ, 8 images)**, sans image clé : « takes a heavy blow to the chest and recoils: the head and
  neck jerk back, the wings flinch, the body sways back on its legs, then it recovers; no effects » (le Dragon se tasse,
  le Démon se plie un peu) ; il dessine un effet sur les 1 ou 2 premières images : les remplacer par une image suivante.
  **Mieux encore (2026-11-20, choisi par l'utilisateur) : le modèle v3 avec un geste qui se cabre en arrière**, 8
  images avec retour : « rears back to the left: it throws its head and long neck back to the left, jaws open, rises
  onto its hind legs with its front claws lifted, the wings snapping open, then recovers back to exactly its starting
  pose… » ; le corps entier recule, la tête en arrière. Le second modèle, avec la même consigne, tourne le Dragon vers
  nous et lance les têtes de l'Hydre en avant. Faire 3 ou 4 graines : environ une sur trois dessine un éclat (le
  retirer avec `retoucher … --eclats=partout`, et `--copier` pour une image trop tachée).
  Le jeu ajoute un recul de 9 pixels de l'écran (`Recoil`) : la page de revue le montre aussi. Retirer des effets à la
  main : par rectangles, n'effacer que les pixels clairs (ou jaune vif sur des cheveux blonds) ; sur le corps, leur donner
  la couleur du corps voisine plutôt que de faire un trou ; ou reprendre une image voisine.
- Écartés : « Bitforge » (`creer`) à cette taille, le style « classic » (trop dessin animé), le style « retro » de Retro
  Diffusion pour les créatures (images carrées, toutes de face et de la même taille), repeindre une seule zone
  (`repeindre` demande un abonnement payant : `retoucher` sur toute l'image à la place).

## Champ de bataille (décor, 2026-11-11)
- Regarder les vrais : captures de Heroes 1 à 3 dans `art-candidats/references/screenshots/`. Un fond **doux** (brume vers
  une couleur, contours sombres adoucis) : sinon il rivalise avec les créatures. Pas de grand horizon peint vu de côté (raté :
  arbres à la taille des créatures, style lisse) ; la lisière (troncs coupés par le haut du cadre) ou de **petites**
  collines lointaines.
- La profondeur d'un paysage ouvert : des arbres de trois tailles (16 × 24 au pied des collines, 32 × 48 entre elles et
  le champ, 64 × 96 sur le champ), une brume claire vers l'horizon, des couleurs proches du sol au raccord (trop pâles,
  le raccord se voit). Les petits arbres de la carte, vus de dessus, juraient : il faut des arbres vus de côté.
- Retro Diffusion `rd_tile__tile_object` refuse 48 × 112 ; 64 × 96 et 48 × 96 passent. Ses arbres et buissons ont une
  tache d'herbe ou une ombre ovale sous eux : `tools/art/sans_socle.py` (`--buisson` pour un buisson), puis les pieds
  (`pieds.py proposer`, revus par l'utilisateur) pour l'ombre de contact du jeu. Garder le trait des racines (sinon
  elles paraissent coupées) et couper les rangées vides du bas (sinon l'arbre flotte au-dessus de son ombre).
- Pas de tramage en points pour une ombre ou une brume (granuleux) : des bandes d'opacité aux bords ondulés.
- **Des paliers de transparence** (25, 50, 75 %), en pixels entiers, plutôt que tout ou rien (remarque de l'utilisateur,
  2026-11-11) : le bord d'une tache de pierres ou du pré au pied du paysage se fond mieux qu'avec un tramage ou des grains.
  Garder peu de paliers (3 ou 4) pour rester du pixel art.
- Sol du désert (`BuildStones`) : essayé et écarté, un pavage de dalles (même en relief) puis un gravier serré (trop de
  cailloux). Retenu, d'après les références de l'utilisateur (`art-candidats/references/sol-desert/`, dont le « Desert
  Ground Pack » d'Alexet) : **des marques légères sur le sable**, pas une texture pleine : des rides de vent (un trait
  sombre, un clair au-dessus, qui suivent un même vent par combat), des traînées de points, des fissures ouvertes d'un
  pixel dans quelques taches, quelques cailloux pâles ; en paliers de transparence, de plus en plus rares vers le bord des
  taches. Le sable reste la couleur du sol.
- Dunes du fond : une à une (largeur, hauteur, biais tirés au sort), face raide à l'ombre dont le bord part de la
  crête en biais et s'incurve ; des trains de dunes réguliers faisaient des pyramides, et une limite verticale entre
  ombre et soleil paraissait fausse.
- Désert : Retro Diffusion dessinait des troncs morts trop fins ; demander « huge very wide ... stump, much wider than
  tall » (64 × 48) donne de grosses souches, à réduire ensuite (`reduire.py`, 80 %) pour tenir dans une case. Le bois
  d'une souche moussue réutilisé dans le désert : `blanchir.py`. Les arbres du désert reposent sur une tache de sable :
  `sans_socle.py --sable` (le sable clair seul est retiré dans les colonnes du tronc, sinon un tronc brun clair disparaît).
- Marais (2026-11-16) : `edge-swamp` (cyprès, arbres morts, saules), `trunk-swamp`, `bush-swamp`, `mid-swamp` (bouleaux,
  petits saules), `far-swamp`, `side-swamp` (roseaux, fougères), `obstacle-swamp`, `detail-swamp` ; descriptions
  « damp swamp, muted dark greens and browns » et « no water under it ». Un saule : « long thin drooping branches
  hanging down like a curtain almost to the ground » ; demandé gros (« very thick short trunk, broad full crown »), il
  devient une boule : garder les candidats où les branches pendent encore. Les arbres obstacles à 64 × 80 (comme le
  chêne de l'herbe) : à 48 de large, ils paraissaient grêles. Mousse et bois gris : `sans_socle.py` prend un tronc gris
  pour une ombre (il efface les racines) et les mousses vertes pour de l'herbe : `--buisson` pour les souches, rochers et
  troncs moussus ; un tronc gris : ne retirer que le vert. L'eau sous un arbre (anneau bleu-vert) n'est pas retirée.
- Des troncs : demander « the lower part of a very tall tree » donne de jeunes arbres à petite couronne ; posés au fond,
  leur couronne sort du cadre et l'on ne voit que les troncs.
- Neige (2026-11-19) : les arbres générés reposent sur une tache de neige, avec une ombre peinte ou des reflets chauds.
  Ni la retirer (l'arbre paraît posé), ni la garder (une tache) : tout ce qui, sous l'arbre, n'est ni aiguilles (vert plus
  que bleu) ni écorce prend la couleur de la neige du champ, à 65 % au pied puis 38, 30, 12 et 8 % (les retouches de
  l'utilisateur) ; l'ombre de contact au bas du tronc, bleue et légère. La neige bleutée a plus de bleu que de vert : ce
  qui la distingue des aiguilles vert-bleu.
- Roche (2026-11-20) : la roche de la carte vue de près fait du cuir (petites dalles, fissures fortes) ou un pavage
  (grandes dalles) ; les affleurements du shader non plus. Les roches plates générées (« flat rock slab ») sortent
  rondes ou carrées, comme des objets posés : ne garder que les éboulis irréguliers, les fondre dans le sol (moins de
  couleur, tirés vers la teinte du sol, bord à 55 puis 90 %). Ce qui marche : une terre sèche dessinée par le shader,
  avec de grandes plaques en paliers, des ravins de gravier pâle à très basse fréquence (plus fins, ils faisaient des
  courbes de niveau), des fissures sèches (`vein_at` de la terre brûlée) et des cailloux tirés vers la couleur du sol
  (pâles, ils faisaient des points blancs). Les touffes dessinées par le shader faisaient des croix : prendre des
  images de l'herbe et les sécher (gris-jaune `l × 1,07, l, l × 0,78`, sans bleu ni orange). Décaler le dessin du
  sol d'un combat à l'autre (`rock_shift`), sinon les mêmes ravins reviennent.
- Le pont d'un navire (2026-11-20) : le sol, la rambarde, la mer et le ciel dessinés par le jeu (`BuildDeck`). Les
  **obstacles à la taille d'une créature** (remarque de l'utilisateur : « un tonneau doit être aussi grand qu'une
  unité ») : à 56 × 48, un groupe de trois tonneaux donne de tout petits tonneaux ; demander **un seul** objet « as tall
  as a man, filling the whole picture » à 48 × 64 ou 64 × 80 (le Pirate fait 74 pixels de haut ; les comparer à côté de
  lui sur la page). Retro Diffusion ne sait pas dessiner un gros mât brisé (de minces poteaux, ou des arbres morts) ;
  « rope and folded sail » donne de petits bateaux. Un poisson de 16 px n'est qu'une tache : 32 × 16, « a clear fish
  shape with a forked tail fin, a dorsal fin and a round eye ».
- Terre brûlée (2026-11-20) : sur un sol sombre, des objets noirs ne se lisent pas ; plutôt que changer le sol, demander
  « pale ash grey » (pierre, bois blanchi) et « strongly lit from below by a bright orange fire glow, glowing orange rim
  light on its lower edges and sides, must stand out on a very dark ground ». Retro Diffusion pose presque chaque objet
  sur un disque de sol (pierre grise, cendre) : `tools/art/sans_disque.py` le fond dans le sol (`--sombre` pour les tas
  de rochers) ; à vérifier image par image (les dessous sombres des rochers, les racines, le bord des cratères).

## Effets du combat (dessinés par le jeu)
- **Pas d'effet dans les animations des créatures** : projectiles, impacts, auras sont dessinés par `Game/BattleEffects.cs`,
  en **pixels d'art de 3 pixels de l'écran** (la finesse des créatures ×3), sans image : flèche (en arc, tournée selon sa
  course), boule de feu (Moine), boule de poison (Liche), hache qui tourne (Orc) ; éclair (gardé pour plus tard, jaune) ;
  impacts (choc, piqûre, entaille, morsure, feu, poison) ; nuage de mort ; bulles, soins, âmes ; flux de sang (Vampire) ;
  auras (poison, sort utile ou néfaste) ; la troupe touchée recule, une troupe empoisonnée verdit, une troupe qui se
  régénère s'éclaire de vert.
- Réglages par créature dans `art/creatures/battle/animations.json` : `projectile`, `launchFrame` (l'image du lâcher),
  `launchPoint` ([x, y] dans l'image d'attaque), `impact`. Le coup d'un tir porte à l'arrivée du projectile.
- Écarté : le style `rd_animation__vfx` de Retro Diffusion (2026-11-07) : trois essais rendus vides (0,28 $).

## Animations des sorts (dessinées par le jeu, 2026-11-14)
- `Game/SpellEffects.cs`. Les revoir sur une page où chaque sort est joué en boucle, à la vitesse du jeu : enregistrer le
  jeu (`--spell-preview=id --record=dossier --fixed-fps 25`, images WebP, qualité 0,7 : environ 7 Mo par sort) ; une
  bande de quelques images recadrées sur la cible suffit pour vérifier soi-même avant de montrer.
- Ce qui plaît à l'utilisateur : **des formes franches et pleines**, pas des points ni des anneaux d'un pixel (« formes
  trop simples ») ; 3 à 4 tons par effet, du plus chaud au plus sombre, par paliers ; des flammes larges à plusieurs
  pointes ; une boule de feu qui est une seule boule en feu (une sphère suivie d'une traîne à part paraît bizarre) ; des
  flammes qui durent.
- **La profondeur** : ce qui tourne autour d'une troupe passe derrière son corps (sinon l'effet paraît collé devant).
- **La troupe elle-même change** (teinte, opacité, images fantômes) : c'est ce qui rend un sort lisible.
- Écarté : des flèches vers le bas pour la faiblesse (l'utilisateur ne les aime pas) ; les chevrons ressemblaient à des
  oiseaux.

## Sommets de la carte (32 px, 2026-11-08)
- Comme Heroes 2 : roche grise avec de l'herbe dans les creux, sans neige, plus grands (64 × 48). Retro Diffusion les pose
  sur un socle d'herbe en losange : en jeu, les chaînes ressemblaient à des cases empilées. `tools/art/fondu.py` fond ce
  socle dans le terrain (transparent peu à peu selon la distance à la roche, 7 pixels) : l'utilisateur voulait garder un
  peu de sol, pas le couper net. Compte comme roche un pixel gris ou brun assez clair (les contours sombres du socle
  doivent fondre aussi, sinon le bord reste net).

## Montagnes de la carte (massifs, PixelLab, 2026-11-17)
- Des **massifs** posés comme les montagnes de Heroes 3 (voir `HISTORIQUE.md`). **PixelLab « Pro »** :
  `pixellab.py pro <dossier> 160 112 "<description>" <graine> <modèle>` donne 4 images (0,12 $), `112 96` 4, `80 56`
  9, `72 88` 9, `44 44` 25.
- **Le modèle** : une image composée de nos images (`ref-montagne-arbres.png` : un massif et des arbres de la carte),
  puis une de nos montagnes faites ainsi. Jamais une capture d'un autre jeu.
- **La description** qui marche : « Rocky ochre and grey peaks and jagged ridges at the top; the lower slopes are smooth
  green grass with a few grey rocks, absolutely no trees, no bushes, no shrubs (…), at the bottom the grassy slopes fade
  into green grass with an irregular soft edge, no hard base line. No outline at all, no dark contour. One light from the
  upper right ». **Pas d'arbres dessinés** : de tout petits arbres à côté de ceux de la carte (l'utilisateur). Formes :
  « a section of a long mountain range running diagonally (…) both ends going down low as if the range continues »
  (`chaine`), « a compact mountain with two or three summits » (`cime`), « a narrow mountain, a bit taller than wide »
  (`etroit`), « a small low mountain spur » (`petit`), « a single small mountain peak » (`peak`). Les autres (morceaux
  vus de face, rangées vers le fond) ont donné des piles de boules ou des images coupées par le bord. Retourner une image
  (`reduire.py … 1 --miroir`) donne la chaîne qui descend.
- **Écartés** (l'utilisateur) : un contour sombre (`adoucir.py … 0.85 90 1 0.4` l'adoucit), des gradins, le style des
  mines (« des morceaux de rocher côte à côte, pas une chaîne »).
- **Fondre dans le sol** : `fondre.py` donne aux pentes les couleurs d'un sol pris dans une capture du jeu (l'herbe : un
  pré de `--center=mountains` ; le sable et le marais : `--terrain-test`) et défait leur bord. **`--bord=fondu`** (choix
  de l'utilisateur, 2026-11-17) : de plus en plus transparent sur 4 pixels ; une trame régulière se voyait comme une ligne
  de points, un bord effrité restait granuleux. Avant, `adoucir.py … 0.85 90 1 0.4` sur les images brutes (un trait
  sombre restait sur certains pics).
- **Un seul jeu d'images, celui de l'herbe** (2026-11-17) : en jeu, le shader `mountain_slopes.gdshader` donne à chaque
  pixel de pente le sol qui est dessous (l'herbe, le sable, le marais…). Il reconnaît une pente à son vert franc (le vert
  de plus de 0,06 au-dessus du rouge et du bleu) : les pentes des images doivent rester vertes (`fondre.py`, sol « »),
  la roche ne doit pas l'être. Les jeux `massif-sable-*` et `massif-marais-*` ne servent plus. Les jeux du sable
  et du marais sont les images d'herbe ainsi recolorées (`massif-sable-*`, `massif-marais-*`, `peak-sable*`…). Les
  images brutes et les captures sont dans `art-candidats/archives/2026-11-17-montagnes/sources/`.
- Installer : `recadrer.py` (l'image coupée à son dessin), `fondre.py`, puis `python -I tools/art/empreintes.py` écrit les
  cases bloquées, celles du dessin, la sorte (`tier`) et le sol de chaque image dans `godot/data/mountains.json`. Les
  montagnes n'ont ni ombre portée ni ombre de contact (leur image porte sa lumière). Voir en jeu : `--center=mountains`.

## Villes de la carte (5 × 4 cases, 160 × 128, 2026-11-08)
- Assemblées de pièces (`tools/art/villes.py`) comme les mines : enceinte sur **toute la largeur** (tours aux coins, mur du
  fond, mur de face), grand donjon au milieu, maisons, chapelle, cryptes **à l'intérieur des murs**, porte au milieu du
  bas. Refusé par l'utilisateur : des pièces semées autour des murs (la ville paraît petite) et la vue trop haute.
- Couleurs vives (grès, toits rouges et bleus ; nécropole violette aux fenêtres vertes) : le gris a été refusé.
- Vue plus basse : ajouter « seen from the front at a low angle, almost a straight side view, not seen from above » ;
  Retro Diffusion penche encore un peu vers le dessus, garder les candidats les plus de face.
- Ne pas fondre (`fondu.py`) une pièce verdâtre (cryptes) : elle passe pour de l'herbe et devient floue.
- **PixelLab « Pro » d'un seul coup** (160 × 128, 4 candidats pour 0,115 $) : bien meilleur que l'assemblage, une
  ville cohérente, vue de face, serrée, porte au milieu du bas (comme Heroes 2, où chaque ville est une seule image
  découpée en cases). Description « seen from the front at a low angle, almost a straight side view », la ville
  assemblée comme modèle (disposition), `objects/mine-gold.png` comme style. Un candidat sur quatre recopie le modèle.

- **Ville de la Forêt (2026-11-09)** : une ville faite d'arbres tend à remplir tout le rectangle (une scène, arbres
  coupés aux bords) ; une idée centrée sur un seul grand arbre dans une palissade ronde reste un objet détouré. Montrer
  plusieurs idées d'un coup (un lot de quatre chacune, 0,115 $) pour que l'utilisateur compare.
- **Contour trop marqué d'une image déjà faite (2026-11-10)** : les retouches ne suffisent pas (moyenne des voisins :
  image « mousseuse » ; amincir les traits épais : trop discret), car le poids vient aussi des couleurs sombres. Regénérer
  avec la ville du Château comme style et l'image actuelle comme modèle : « drawn in the same style as the reference
  castle: thin light outlines, no thick black outlines, bright warm soft colours ». Comparer les villes côte à côte sur
  l'herbe de la carte, à la taille du jeu.
- **Ville de la Tour (2026-11-11)** : trois idées (une tour de mages, une cité de flèches, une citadelle de géants) ;
  choisie : la série « tour de mages », refaite **plus lisible** en demandant l'ombrage du Château (« stone walls and
  towers shaded with several light and dark tones like the reference castle, lit from the upper left… a thin consistent
  outline of the same width everywhere ») avec le candidat aimé comme modèle et le Château comme style. Le toit du donjon
  changé en dôme d'or par `pixellab.py retoucher` (le reste ne bouge pas). **Coûts réels** : un lot « Pro » avec des
  modèles a coûté 0,46 $ (annoncé 0,115 $) et la retouche 0,54 $ (annoncée 0,08 $) : regarder le solde avant et après.
- **L'ombrage du Château par retouche (2026-11-11)** : le Château a des volumes ombrés en dégradés (tours rondes comme des
  cylindres, éclairées de gauche), une texture douce faite de petits écarts de ton et presque pas de traits à l'intérieur
  (3 % de pixels de trait, contre 14 % pour la première ville de la Tour). Un candidat sur quatre qui recopie le modèle
  (le Château) garde ce dessin. Pour le donner à une image qu'on aime : `pixellab.py retoucher` en deux temps,
  « Redraw the shading in a softer painterly pixel style: large stone surfaces shaded with smooth gradients, lit from the
  upper left… round towers shaded like cylinders, gentle stone texture made of small tone changes, almost no dark outlines
  inside the image. Keep the same shapes… », puis « Add small details to the stone walls and towers so they are less flat:
  visible stone blocks and masonry joints…, a few cracks, small arrow slits, some moss and dark stains near the base, worn
  edges. Subtle details, no thick outlines… Keep everything else exactly the same ». Les formes ne bougent pas (les pieds
  restent bons). Redessiner l'image avec une image de style (`pro`) ne marche pas : le style d'avant revient. La
  retouche change la luminosité : la régler ensuite (`luminosite.py`). Fait pour le Donjon, la Tour et les Barbares.
- **Ne pas lancer `godot --headless --import`** pour prendre en compte une nouvelle image : il écrit plus de 400 fichiers `.import` et `.uid` non suivis à côté des images et des scripts. Une capture ordinaire (`-- --capture=…`) voit déjà la nouvelle image.
- **Luminosité des villes (2026-11-11)** : mesurée par `luminosite.py mesure` (Château 125, Forêt 77, Barbares 74,
  morts-vivants 45) ; la Tour (156, trop claire) ramenée à 125 par une courbe. Éclaircir la Forêt et les Barbares (même
  en gardant les sombres) a été refusé par l'utilisateur.
- **Ville des Nains (2026-11-12)** : les mots de la Tour dans la description du premier lot (« shaded with several light
  and dark tones like the reference castle, lit from the upper left, a thin consistent outline… ») donnent d'emblée des
  villes lisibles, sans retouche d'ombrage. Pour comparer des retouches, les poser une à une en
  `objects/town-<faction>.png` et capturer le départ (`--seed=42 --faction=<faction>`) : la ville de départ est à
  l'écran ; retirer l'image d'essai ensuite. Une retouche « plus de couleurs » (bannières, cuivre, runes d'or) marche
  (0,08 $) mais assombrit l'image (98 → 64).
- **Ville barbare (2026-11-09)** : une image trop détaillée (« croustillante ») s'adoucit en ajoutant « simple clean
  shapes, smooth soft shading, few small details, not noisy, not grainy » et en prenant `objects/town.png` comme style ;
  le contour noir épais des villes de PixelLab s'adoucit avec `adoucir.py` (0,7, 60, 2 passes : le bord prend une teinte
  sombre de sa couleur), sans dépense. Le candidat aimé, donné comme modèle, garde la disposition d'un lot à l'autre.

## Ponts de la carte (Retro Diffusion, 2026-11-15)
- Les juger **en jeu, sur la rivière**, à ×2 : à leur taille, on ne voit rien. Les captures : `--center=bridge`,
  `bridge:9` (avec une route ; seed 42), `ford` et `ford:2` (une route sur un ruisseau, dans les deux sens).
- **Accorder les couleurs par rang de luminosité** : chaque pixel prend la couleur du modèle au même rang (pas la plus
  proche, qui fond les planches en un seul ton) ; les joints et les bords restent lisibles. Gratuit, et chaque fois réussi.
- L'**image de départ fixe la forme** (largeur, longueur, angle) : la ramener d'abord à la bonne longueur (`reduire.py`),
  la recentrer sur ses pixels. Ce qui a raté : **tourner de 90° une vue de côté** (le pont de pierre d'ouest en est,
  mince, devient une bande étroite de pierres) ; **partir d'une image de bois pour la pierre** (les rambardes de bois
  restent). Ce qui a marché : partir de la forme juste repeinte aux couleurs voulues (le bois du nord au sud aux couleurs
  de la pierre), force 0,8 ; les petits ponts à partir des grands réduits, force 0,5.
- Retro Diffusion dessine la pierre presque de côté et grise froide : la réchauffer vers la pierre du temple (rouge × 1,2).

## Grands objets de la carte (mines…)
- **Méthode** : des **pièces** séparées faites par Retro Diffusion (`assets.json`, groupe `pieces/` : cabane, chariot,
  chevalet, souches, entrée de mine, chariot sur rails…), **sans socle**, puis **assemblées** aux proportions de Heroes
  (petit bâtiment au fond, décor autour, chemin), à la main dans le **composeur** (`tools/art/composeur.py`) ou par un
  petit script ; l'image assemblée sert telle quelle (même style partout).
- Écarté : redessiner la maquette par Retro Diffusion (`input`) : il perd des pièces (rails, chariots, arbres) et le
  détourage confond un fond vert avec les feuilles (un fond magenta laisse des traces) ; demander tout le décor dans un
  seul texte (Retro Diffusion n'en garde qu'une partie et pose l'objet sur un socle ou une île) ; PixelLab pour la carte
  (bien plus de détails, mais des contours plus marqués : l'utilisateur préfère Retro Diffusion) ; 144 × 96 chez Retro
  Diffusion (refusé à la génération).
- **Scierie refaite (2026-11-08)** : une cabane vue de **trois quarts**, porte au bout du chemin, avec une **pièce en plus**,
  **sol transparent**. Retro Diffusion donnait des cabanes trop simples ou posées sur un carré de sable ou d'herbe, et à
  partir d'une image (« input », force 0,55 puis 0,8) il ne la changeait presque pas. **PixelLab** a réussi : « retoucher »
  la cabane choisie (« add a second, lower room… ; remove the sand and ground… », 0,08 $) ; « pro » à 48 × 48 (25 cabanes
  pour 0,08 $, avec la cabane comme style et un modèle) en donnait aussi de bonnes. La cabane (42 × 42) est à l'échelle des
  autres bâtiments de mine (38 × 32 à 52 × 51) ; sa porte n'est pas au milieu : l'assemblage la pose au-dessus du chemin.
- **Lieux de la carte (2026-11-12)** : PixelLab « pro » à 64 × 64 (16 candidats pour 0,08 $ par lieu), la mine d'or comme
  style, « one small object alone on a transparent background, seen from the front and slightly from above… no grass
  patch, no ground base, no shadow » : des lieux nets, sans socle, au style des mines. **Montrer les candidats à leur
  taille de jeu** (un pixel d'image = 2 pixels de l'écran) : à ×1, ils paraissaient plus fins que le reste et
  l'utilisateur les a crus d'un autre style. Un lieu d'une case : l'image réduite de moitié (32 × 32) ; un lieu haut
  (2 cases) : l'image de 64 × 64 telle quelle. Recadrer chaque image sur son dessin avant de l'installer (le jeu pose le
  bas de l'image sur le bas de la case : des rangées vides dessous le feraient flotter). `--center=windmill` (sage,
  temple…) centre la capture sur un lieu.
  Un lieu au contour trop dur (« crunchy », le moulin, 2026-11-18) : `adoucir.py entree sortie 0.7 60 2` (plus doux : `0.85 90 2`, la tour ; le 4e nombre, `0.4`, adoucit aussi les traits de l'intérieur), puis recadrer et
  corriger ses pieds dans `godot/art/pieds.json`.
- **Le bateau des héros (2026-11-20)** : « pro » à 48, vu de côté, l'est du chevalier comme style, « no red anywhere
  except the sail » (la voile prend la couleur du joueur) ; « a small sturdy medieval wooden sailing ship, a cog… a plain
  wooden prow with no carving » (sans cela, des drakkars à tête de dragon). Puis `tourner` « Pro » (vue « high
  top-down », 0,08 $) : des vues justes et la même taille ; la v3 (0,02 $) déforme la coque. Les noms des fichiers de
  `tourner` : sud, sud-est, est, nord-est et nord sont justes, « ouest » est un second est (inutile : l'ouest en miroir).
  Un navire de pirates : « two tattered black sails, a black flag with a white skull and crossbones ».
- **Les coups reçus et les morts d'un humain** (le Pirate) : sans consigne, des éclairs, du sang, et une mort qui ne
  tombe pas. « takes a blow and recoils backward… no effects, no blood, no sparks » dessine encore des étoiles collées au
  corps (le modèle v3) ; celles qui sont devant lui partent avec `retoucher --eclats --detaches --copier=1:8`. Pour la
  mort : « drops the cleaver, clutches his chest, sinks down to his knees and falls face down… no effects, no blood ».
- **Les pages de choix** : `tools/art/page_choix.py <sujet> description.json` fait la page où l'on clique pour tout lot
  (les feuilles d'animation y jouent en boucle, `"anim"`) ; réponses dans `en-cours/revue-<sujet>.json` (`revue.py
  serveur`).
- **Objets de la mer (2026-11-20)** : la même recette que les lieux (« pro » à 64, mine d'or comme style), avec pour ce
  qui flotte « it floats on the sea: draw only the object, its bottom cut flat at the waterline, the water itself is not
  drawn » : propre du premier coup, sans eau autour. Le tourbillon : « seen from above at an angle… its edges fading out
  to transparent » donne un disque net, qui passe bien sur l'eau. Les petits objets réduits à 32 px, le navire, le phare
  et le chantier gardés à 64. La page de choix où l'on clique (choix, « à refaire », commentaire, enregistrés par
  `revue.py serveur`) plaît à l'utilisateur : en faire une pour chaque lot.
- Choisis (2026-11-07) : scierie « A4 », mine de minerai « A » (assemblée, rails de la porte au chariot) ; installées
  avec les règles des grandes mines (3 × 2 cases, entrée en bas au milieu : l'autre session).

## Héros de la carte (48 × 48, 2026-11-14)
- **Générer à la taille du jeu** (« pro » à 48, 25 candidats pour 0,08 $) avec une image choisie comme style : plus
  lisible que réduire une image de 96 (les petits détails deviennent du bruit). Ce qui rend lisible : « bold simple
  silhouette, large flat areas of colour, few colours, strong contrast, dark outline all around, very few small
  details ». De profil (« side view, four visible legs »), les chevaliers étaient lisibles mais l'utilisateur préférait
  le cheval caparaçonné de trois quarts.
- **La marche** : `pixellab.py animer` (8 images, la 1re est l'image fixe) ; `--enrichir` a donné la meilleure.
  Vérifier que le dessin touche le sol à chaque image (sinon il glisse) ; un rebond d'un pixel du haut du dessin (les
  jambes encaissent) quand un sabot se pose. Le mouvement sur la carte n'est pas régulier : un élan par case
  (page à curseurs `en-cours/heros-elan.html` pour régler vitesse, élan et moment avec l'utilisateur).
- Montrer une marche **jouée**, à la taille et à la vitesse du jeu, sur place et en avançant sur l'herbe : la planche
  d'images ne permet pas de juger les jambes.
- **Les 8 directions** (2026-11-15) : `pixellab.py tourner image.png dossier --vue="high top-down" --description="…"`
  (« Generate 8 rotations » Pro, 0,06 $ le lot ; l'ordre réel des images à l'écran est sud, sud-est, est, nord-est,
  nord, nord-ouest, ouest, sud-ouest : la documentation le donne en miroir) ; la v3 garde mieux le dessin d'origine
  mais rate des directions. Puis une marche par direction avec `animer --enrichir` sur l'image tournée (environ 0,02 $
  l'essai : deux essais par direction suffisent) ; 5 directions suffisent, l'ouest en miroir. Les vues de face et de dos
  bougent peu les jambes : c'est normal.
- La recette d'une faction (le nécromancien, 2026-11-15, environ 0,50 $) : l'image fixe par `pro` à 48 avec le sud-est
  du chevalier (`hero-chateau-se.png`) comme style, deux idées de 25 essais (garder un **étendard rouge** : le jeu le
  peint de la couleur du joueur) ; puis `tourner` (deux lots Pro et un v3, à choisir), puis les marches. « Pro » rend
  parfois un fond blanc opaque : `tourner` le retire.
- **Ce que veut l'utilisateur** (le rôdeur, 2026-11-15) : **l'est vu de côté** et **le sud-est tourné vers le sud**. Les
  rotations « Pro » donnent un est et un sud-est presque pareils (trois quarts) ; la caméra « side » n'y change rien.
  L'est : `pro` à 48, « seen from the side, facing right, the horse in full profile with all four legs visible »,
  l'est du chevalier (`hero-chateau-e.png`) comme style, l'image choisie comme modèle. Le sud-est : `pro` **sans style**
  (« - », sinon les essais gardent l'angle du style), « front three-quarter view from above: the horse comes toward the
  viewer, turned only a little to the right… », la vue de face, le profil et l'image choisie comme modèles ; un essai
  tourné vers la gauche sert aussi, retourné. Vérifier ensuite que chaque vue est posée au même sol (rangée 45).
  Si le sud-est reste de trois quarts (le nécromancien), prendre **le sud-est du rôdeur (`hero-foret-se.png`) comme
  style** : c'est le style qui fixe l'angle. La vue de face en modèle tire vers le plein sud (et vers un fond blanc).
  Mesurer la hauteur des essais (45 comme les autres vues ; l'utilisateur voit tout de suite un dessin plus petit) et
  retirer les plus petits de la page. Écrire la couleur de la cape dans la description (sinon elle sort sombre).
  Le sud-est du chef barbare n'a jamais été juste (six lots) : il garde l'ancien.
- **Le magicien de la Tour** (2026-11-15) : nos vues de l'est (chevalier, rôdeur) ne sont **pas de vrais profils** ; une
  image fixe de trois quarts en modèle tire aussi les essais vers le trois quarts. Pour un vrai est : **seulement les
  cavaliers des images de combat** (`creatures/battle/cavalier.png`, `nomades.png`) comme modèles, sans style, « pure
  side view like a side-scrolling game… we do not see its chest ». Le dessin change un peu : le corriger par le code
  (chaque pixel à la couleur la plus proche de la palette de l'image fixe ; le drapeau et la hampe de l'image fixe à la
  place des siens). Le sud-est vient à 40-42 pixels quoi qu'on fasse : l'agrandir au plus proche voisin (46). Vérifier
  que le drapeau flotte vers l'arrière dans chaque vue (le nord-est le portait vers l'avant : retourné autour de la
  hampe). Une marche trop lente : « walks briskly with long quick strides ». Regarder chaque lot soi-même et mesurer
  les hauteurs **avant** de faire une page.
- **Le démoniste de l'Enfer** (2026-11-15) : **partir de l'est** (vrai profil d'après les cavaliers des images de combat)
  et faire tourner l'est (`tourner`, « Pro ») pour le sud, le nord-est et le nord : le cheval garde ses proportions (les
  vues tirées d'une image fixe de trois quarts donnaient un poney à côté du grand cheval de l'est). Comparer **la taille
  du corps** (sans drapeau ni hampe) d'une vue à l'autre, pas seulement la hauteur totale. **Pas de rouge hors du
  drapeau** (le jeu le peint de la couleur du joueur) : un feu passe à l'orange par le code ; écrire « no red anywhere
  except the banner » dans la description.
- **Le Seigneur des abîmes du Donjon** (2026-11-15) : proposer les idées **directement de côté** (les cavaliers des images
  de combat en modèles, sans style), puis tourner l'est choisi : une seule recette, le même cheval partout. Le sud-est
  tourné vers le sud reste le point faible : le cheval y prend souvent un cou arqué tête basse (« un chameau ») ;
  regarder le cou à ×6 avant de montrer une page. Une retouche (« Edit images ») d'un sud-est déjà réussi garde la pose
  mais raccourcit les jambes. **Refait le 2026-11-20** : l'est du héros en modèle ramène le cheval de côté ; le style du
  sud-est du rôdeur, le sud du héros et deux bons sud-est (Nains, Enfer) en modèles donnent l'angle, mais **un poney** ;
  ajouter **l'ancien sud-est du héros** en modèle et « a big tall heavy war horse with long legs… much bigger than the
  rider » rend le grand cheval (un peu moins tourné vers nous). Une couleur à corriger (le casque trop gris) : ne pas
  éclaircir d'un bloc (trop blanc), mais donner à chaque pixel la teinte de même rang de luminosité dans l'image
  installée (les gris peu saturés du haut, classés par luminosité des deux côtés).
- **Le thane des Nains** (2026-11-15) : la même recette (idées de côté, rotation de l'est choisi, sud-est au style du
  rôdeur) a marché du premier coup sur un bélier. Passer les rouges hors du drapeau au brun **avant** de tourner l'image
  (sinon toutes les vues les gardent). Regarder la hampe du drapeau : PixelLab la coupe souvent à la main du cavalier ;
  la prolonger par le code avant les marches.

## Héros au combat (environ 125 px, 2026-11-19)
- `tools/art/heros_combat.py essayer <id>` : « Pro » à 160 × 144 (4 images, 0,12 $), le Cavalier du combat comme style
  et comme modèle de la vue de côté, le **portrait** comme modèle du visage (« face, hair, beard and clothes colours
  only; not the framing, not the background »), le héros de la faction sur la carte comme modèle de la tenue et de la
  monture. La description reprend ce que montre le portrait (couleur des cheveux, coiffe, peau) : les quatre essais
  lui ressemblent du premier coup, la monture suit celle de la carte. Gardés à la taille de génération (pas réduits).
- **Les rouges** : le jeu peint de la couleur du joueur les rouges vifs (saturation > 0,45, teinte < 0,05 ou > 0,93),
  et un cheval brun, du cuir, des cheveux roux, du feu y tombent souvent. « no red anywhere except the banner » ne
  suffit pas : `choisir` passe au brun (teinte 0,06) toutes les taches rouges sauf l'étendard, la tache qui commence le
  plus haut (au bout de la hampe). Vérifier avec un aperçu peint en bleu. Éviter un héros à la peau rouge (Méphis).

## Interface
- **Icônes des ressources** (`art/icons/{gold,wood,ore,gems,mercury,crystal,sulfur}.png`, 16 × 16, affichées ×2 dans
  la barre des ressources et le royaume ; `-grand.png` en 32 × 32 pour les fenêtres à venir) : PixelLab « Pro » à 32 × 32
  (64 candidats pour 0,08 $), le tas de la carte comme image de style, puis recadrées et réduites. Le soufre doit se
  distinguer de l'or (cristaux jaune-vert sur roche sombre), les gemmes sont des pierres taillées (rubis, saphir,
  émeraude, diamant), pas des cristaux. « crystal » seul est refusé par le filtre de PixelLab (décrire du quartz).
- Cadre **B2** (argent, une fine bande de bois) affiché ×3, bouton **A1** (pierre grise bordée d'argent, coins un peu
  arrondis, gardés) ×3 ; réglages dans `art/ui/ui.json` (coins, marges, échelles, grain de pierre `panelTexture`).
- **Creusé pour les places, à plat pour les choses** (comme Heroes 2) : `Ui.Inset` pour les emplacements, listes,
  sections ; `Ui.Plate` pour les cartes, lignes et vignettes ; un bouton choisi est plus sombre (enfoncé).
- **Liste déroulante** (proposition A, 2026-11-06) : le bouton de pierre A1 et une flèche d'argent (`Ui.Arrow`) ; la
  liste dans un creux, la ligne survolée en plaque bordée d'argent, le choix coché (`Ui.CheckMark`) ; `Ui.MakeDropdown`
  (un `OptionButton` habillé par le thème). **Sélecteur à flèches** (proposition D) : deux petits boutons de pierre
  ◄ ► autour d'un creux, en boucle (`ArrowSelector`) : pour une liste qui peut grandir (les héros) ou peu de choix.
  Écartés : un creux avec un petit bouton (B), le bois et l'or (C, seul élément doré dans une interface d'argent).
  `--ui-preview` montre les quatre propositions.
- **Onglets en marque-pages** (2026-11-09, choix de l'utilisateur, à l'essai) : un ruban de soie crème
  (`art/ui/ribbon.png`, 96 × 24), **teint par le jeu** de la couleur de chaque onglet (`TabStrip`), affiché ×2, qui sort
  du haut de sa page. Une seule image pour toutes les couleurs. PixelLab « Pro » : d'abord 36 rubans à 64 × 24 sans image
  de style (0,08 $ ; le n° 6 choisi), puis 4 à 96 × 24 avec le n° 6 comme image de style (0,08 $ ; « large 2 » choisi,
  bords roulés) : à 64 px, le ruban était plus étroit que « Compétences » et le jeu l'étirait. Prompt : « a single wide
  horizontal silk ribbon bookmark tab seen from the front, pale cream white fabric, the top edge cut with a small
  swallowtail notch in the middle, soft vertical folds… transparent background ». Écartés : la pierre des boutons en
  onglets (ressemblait à une rangée de boutons), les intercalaires, les plaques, le cuir cousu dessiné, les rubans
  dessinés (sans relief). Une image plus juste sera faite si l'essai est validé. Candidats :
  `art-candidats/en-cours/onglets/`. Une page HTML qui teint des images par masque CSS ne montre pas les couleurs
  ouverte en fichier local : passer par un petit serveur, ou montrer une capture du jeu.
- Écartés : les cadres épais du premier essai, B7 (fil d'or, trop discret), les boutons de pierre de 96 × 32, les cadres
  de Kenney, la police DotGothic16. La police VT323 (2026-11-11 : nette seulement à 25, trop petite, ou à 50, trop
  grande) ; Pixel Operator fin, en gras ou mêlé, et Jersey 10 pour les titres, essayés à côté de Pixel Operator HB 8.
- **Le solde PixelLab est partagé entre les sessions** (2026-11-09) : pour connaître le prix d'un lot, lire le `solde`
  juste avant et juste après (une autre session peut générer en même temps). `pixellab.py pro` à 32 × 32 (64 icônes) :
  0,08 $ le lot, vérifié.
- **Icônes des boutons du combat** (2026-11-09, `art/ui/icons/*.png`) : argent gravé sur la pierre des boutons, PixelLab
  « Pro » 32 × 32, 64 candidats par icône sans image de style (« single icon, light silver engraved metal, monochrome
  pale silver and grey shades with a dark outline, simple bold silhouette… »), montrées ×2 réduites à leur partie
  visible (réduites à 16 × 16, elles perdaient leurs détails). Choisies par l'utilisateur sur une page
  (`art-candidats/en-cours/icones-combat.html`) : livre 33, sablier 34, bouclier 26, rester 1, auto 5, options 2,
  commencer 62 ; fuir refait (« de profil, vers la gauche, tête basse de honte » : n° 4 ; le premier lot, vu de dos,
  ressemblait à des cosmonautes). Le jeu les **incruste** dans la pierre du bouton (`BattleScreen.Engrave` : un creux
  autour de l'argent, l'ombre en haut à gauche, la lumière en bas à droite, contrastes doux).
  La même recette pour la ville (2026-11-20) : « a thick bold arrow pointing straight down onto a knight's helmet » et
  « … up into a small castle tower » (le héros est sous la garnison), un lot de 64 chacune, choisies au premier tour
  (`donner-heros` 56, `laisser-garnison` 9).
- **Icônes des sorts** (2026-11-11, `art/spells/<id>.png`, 32 × 32, ×2 dans le livre de sorts et le grimoire de la
  guilde) : **de petites peintures en couleur qui remplissent leur carré**, comme Heroes 2 et 3 (choix de l'utilisateur ;
  pas l'argent gravé des boutons). PixelLab « pro » 32 × 32 **avec l'option `--fond`** (le fond est gardé : sans elle,
  le sujet flotte sur du transparent), sans image de style, 64 candidats par sort, 0,08 $ le lot (vérifié au solde :
  0,27 $ pour trois). Début de la description, le même pour tous les sorts : « tiny square spell icon for a 1990s
  fantasy strategy game, a small painted scene filling the whole square edge to edge, rich saturated colours, soft
  painterly shading, crisp clean pixel art, readable at small size, no text, no border, no frame: » puis le sujet et le
  fond (« a blazing orange and yellow fireball bursting with flames and sparks, against a deep red and black
  background »). Page de choix : chaque candidat ×2 sur le parchemin du livre, avec le nom et le coût du sort.
  Choisis : Boule de feu 50 (une comète de feu), Bénédiction 45 (une épée dans des rayons d'or, ciel bleu), Éclair 8
  (un éclair fourchu sous les nuages, nuit bleue).
  **Les 15 autres** (2026-11-14) : même recette, 15 lots lancés ensemble (5 à la fois), 0,08 $ chacun. Choix sur une
  **page où l'on ne fait que cliquer** (demandé par l'utilisateur : ne rien recopier à la main) : une rangée de 64
  icônes ×2 par sort, le choisi montré dans une case du livre, « Aucun ne va » et un commentaire ; chaque clic est
  envoyé à `tools/art/revue.py serveur` (toute page nommée `revue-<nom>.html` qui envoie `{page, dossier, items}`), qui
  écrit `revue-<nom>.json` dans le dossier `en-cours` donné par la page, lu ensuite par Claude. **Sessions en parallèle**
  (2026-11-17) : trois serveurs écoutaient le même port (Windows le permet avec l'adresse réutilisable) et deux pages de
  pieds avaient le même nom, des réponses se sont perdues. Depuis, le serveur refuse de démarrer si le port est pris (un
  seul serveur sert toutes les sessions, il écrit dans le dossier de chaque page) et le nom des pages porte celui du
  dossier de travail (`revue-pieds-marais.html` dans `HoMM-marais`, rien de plus dans `HoMM`). Choisis : Flèche magique 14 (flèche violette), Malédiction 54
  (crâne dans une fumée verte), Soif de sang 37 (lame sanglante), Hâte 20 (bottes ailées), Lenteur 5 (escargot),
  Soins 21 (mains et croix de lumière), Bouclier contre les tirs 13, Faiblesse 6 (épée brisée), Aveuglement 39 (yeux
  bandés), Téléportation 13 (portail bleu), Dissipation 3 (éclat blanc), Résurrection 22 (ankh d'or à l'aube), Pluie
  de météores 24, Chaîne d'éclairs 45. Peau de pierre refaite à la demande de l'utilisateur : « a warrior seen from
  behind, his back and shoulders slowly turning into grey cracked stone » (choisi le 31 ; le poing qui devient pierre
  écarté).
- **Le lutrin et son livre fermé** (2026-11-12, `art/ui/lutrin.png`, 160 × 168, ×2, posé en bas au milieu de
  `LecternView`) : PixelLab « pro », 4 candidats, l'image de la ville (`town.png`) comme style, vus de trois quarts d'en
  haut, un livre rouge foncé aux ferrures d'or ; 0,17 $ le lot (0,115 $ annoncé). Choisi : le 3, un socle rond et massif
  de bois sculpté. Écartés par l'utilisateur : un lutrin vu de face, et un zoom du livre du lutrin vers le livre ouvert
  (essayé) : le clic ouvre le livre au milieu de l'écran, sans transition.
- **Le livre ouvert** (2026-11-12, `art/ui/livre-ouvert.png`, 480 × 288, ×2, sous les sorts que le jeu écrit) : trop
  grand pour « pro » (170 px au plus). `ui` avec un dessin de la disposition comme modèle : le livre sort petit (165 px)
  au milieu d'une image vide, inutilisable. Ce qui marche : **`retoucher` sur un dessin de la disposition** (la
  couverture de 8 px, les deux pages, le dos, la tranche ; fait en Python), qui garde la taille et les proportions,
  **avec `--fond`** (sans elle, les pages blanches deviennent transparentes) et en demandant une couverture fine et
  un dos étroit (sinon 24 et 36 px). 0,15 $ annoncé, environ 0,15 à 0,23 $ au solde. Quelques pixels blancs restés
  du modèle sur les bords, rendus transparents.
- **L'étude** (2026-11-12, `art/ui/scenes/etude.png`, 504 × 296, ×2, le fond de l'onglet Magie) : même recette
  (`retoucher --fond` sur un dessin de la disposition : mur, sol aux 72 % de la hauteur, fenêtre à gauche, étagère à
  droite, bougie sur une table, le milieu vide pour le lutrin) ; « cozy medieval wizard study at night… the middle of
  the room left empty and uncluttered, keep the exact same layout and proportions ». Réussie du premier coup, 0,15 $.
  **Puis refaite avec le lutrin dedans** (choix de l'utilisateur) : le lutrin généré à part n'avait ni le point de vue
  ni la lumière de la pièce. Recette : coller le lutrin choisi (`art/ui/lutrin.png`) dans l'image de la scène, à sa place
  dans le jeu (en bas au milieu, 6 pixels au-dessus du bord), puis `retoucher --fond` : « one single consistent point
  of view and one single warm candle lighting for the whole scene, the lectern lit and shaded like the rest of the room
  and standing firmly on the floor with a soft shadow; fill the room with more details: a rug, a desk with scrolls,
  potions and a quill, a globe, a small cauldron, hanging dried herbs, piles of books, a few more candles; keep … the
  lectern in the middle at the same place and size ». Deux essais (0,15 $ chacun) ; choisi le A (moins de bougies). Le
  jeu ne dessine plus le lutrin sur une scène qui a son image (`LecternView.ShowImage`) : la lueur, les étincelles et le
  clic seulement. **À refaire ainsi pour chaque académie.**
- **L'académie du Château** (2026-11-13, `art/ui/scenes/academie-chateau.png`, 480 × 232) : même recette. Écartées par
  l'utilisateur : deux grandes salles vues de face (« la perspective ne marche pas » : un mur plat de face ne va pas
  avec le lutrin vu de trois quarts et d'un peu au-dessus). Réussi : **une petite pièce fermée**, vue d'un peu au-dessus
  comme le lutrin ; le dessin de départ a un mur rond (son pied descend vers les côtés), un sol de planches en rayons,
  un tapis rond, deux fenêtres étroites, une bannière derrière le lutrin, des étagères sur les côtés ; « small cozy
  round room at the top of a stone castle tower… seen from slightly above… one single consistent point of view matching
  the lectern… ». Deux essais (graines 11 et 29, 0,15 $ chacun) ; choisi le 29 (une pièce sous un toit en pente, plus
  riche). Pour les autres académies : de petites pièces de ce genre.
- **Les académies des 7 autres factions** (2026-11-14, `art/ui/scenes/academie-<faction>.png`) : même dessin de départ,
  aux couleurs de chaque faction (mur, sol, lumière, bannière, tapis), et une description par faction (crypte, hutte du
  chaman, pièce dans un arbre, tour de marbre, roche volcanique, caverne aux cristaux, salle des runes). Deux essais par
  faction (graines 11 et 29, 2,19 $ les 14) ; choisis : morts-vivants B, Barbares B, Forêt B, Enfer B, Donjon B,
  Nains A. La Tour refaite : « très blanche et froide » ; plus chaude (marbre couleur de miel, ciel du soir, bougies,
  « no white or cold tones »), choisie la graine 47. **Le lutrin dans la lumière de la pièce** (demandé par l'utilisateur :
  un lutrin brun à la lueur jaune jurait dans la caverne violette) : `tools/art/teinter_lutrin.py <scène> <lutrin.png>
  <r,g,b> 0.6 <sortie>` multiplie le lutrin par la couleur de la lumière puis l'éclaire (plus sur les faces claires : les
  ombres restent sombres), sur la forme du lutrin collé, élargie de 2 pixels ; et le jeu donne à la lueur et aux
  étincelles la lumière de la scène (`SceneBackdrop.Light`, `LecternView.GlowColor`). Un lutrin trop « baigné » (écran
  sur tous les pixels, bord de 4 pixels) paraissait voilé, comme dans la brume.
- **Icônes du panneau de la carte** (2026-11-09, même méthode, l'engrenage choisi comme image de style) : suivant 56
  (heaume et flèche), avancer 3 (botte), centrer 15 (cible), royaume 8 (couronne), creuser 48 (pelle), trésor 60 (carte
  roulée) ; Sorts et Options reprennent le livre et l'engrenage du combat. Le code commun : `IconButtons.cs`.
