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
- Ce que la revue a appris (les propositions corrigées) : les pieds proposés étaient trop larges (1 à 2 pixels de chaque côté)
  et un pixel trop haut (corrigé dans `proposer`) ; un sommet ne garde qu'une petite base au milieu ; le château a un
  second pied le long de la rangée de tours du fond ; une mine a un pied par objet (un arbre collé à la cabane, une souche,
  une pierre), et l'entrée coupe le pied du rocher en deux.

## Carte (Retro Diffusion)
- Images de 24 px (cases de 48 px à ×2, puis ×3 et ×4 en 1920 × 1080). `tools/art/generer.py` estime le prix
  (gratuit), génère des candidats et installe le candidat choisi ; `tools/art/assets.json` décrit les images à produire.
- Forêts et montagnes : des arbres et des sommets seuls (`art/decor/`) semés par le jeu (`DecorLayer`) ; écarté : une
  image par case (trous réguliers).
- Terrains : deux images par terrain (`grass-2.png`…), les bords sont calculés par le shader (pas d'images de bords).
  Herbe (2026-11-09) : demander « natural mid green… not lime, not too saturated » ; Retro Diffusion ignore les petites
  fleurs d'une case : les fleurs, cailloux et plages de sable sont dessinés par le shader (`terrain.gdshader`), sans
  répétition et réglables (`--ground=`).
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
- Des troncs : demander « the lower part of a very tall tree » donne de jeunes arbres à petite couronne ; posés au fond,
  leur couronne sort du cadre et l'on ne voit que les troncs.

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

## Sommets de la carte (32 px, 2026-11-08)
- Comme Heroes 2 : roche grise avec de l'herbe dans les creux, sans neige, plus grands (64 × 48). Retro Diffusion les pose
  sur un socle d'herbe en losange : en jeu, les chaînes ressemblaient à des cases empilées. `tools/art/fondu.py` fond ce
  socle dans le terrain (transparent peu à peu selon la distance à la roche, 7 pixels) : l'utilisateur voulait garder un
  peu de sol, pas le couper net. Compte comme roche un pixel gris ou brun assez clair (les contours sombres du socle
  doivent fondre aussi, sinon le bord reste net).

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
- **Icônes du panneau de la carte** (2026-11-09, même méthode, l'engrenage choisi comme image de style) : suivant 56
  (heaume et flèche), avancer 3 (botte), centrer 15 (cible), royaume 8 (couronne), creuser 48 (pelle), trésor 60 (carte
  roulée) ; Sorts et Options reprennent le livre et l'engrenage du combat. Le code commun : `IconButtons.cs`.
