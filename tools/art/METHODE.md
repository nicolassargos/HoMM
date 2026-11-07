# Méthode des images

Ce qui marche (et ce qui a été écarté) pour faire les images du jeu. À lire avant tout travail d'images. Les services,
les clés et le dossier `art-candidats/` sont décrits dans `CLAUDE.md` ; l'histoire de chaque image est dans
`HISTORIQUE.md` (étapes 5, 6 et 10).

## Règles générales
- **PixelLab** pour les personnages et les créatures (images de combat, animations) et des essais d'interface ;
  **Retro Diffusion** pour les cartes (terrains, décors, objets) et les portraits des héros.
- Les images sont trouvées par convention d'après les identifiants (`res://art/creatures/paysan.png`) : jamais de chemin
  d'image dans les règles. Une image absente garde le dessin par formes (ou la petite image de la carte).
- Pixel art toujours agrandi d'un nombre entier de fois, pixels nets (écran de 1920 × 1080) : carte ×3 et ×4,
  créatures au combat ×3 (cases de 110 × 126), sol et obstacles ×3, héros au combat ×4, cadres et boutons ×3,
  portraits ×1 à ×3, créatures de l'interface ×2 à ×4.
- `tools/art/reduire.py` réduit une image sans la flouter (palette d'origine) et peut la retourner (`--miroir`) ;
  `tools/art/palette.py entree sortie 16` ramène à 16 couleurs ; `tools/art/adoucir.py` adoucit un contour noir ;
  `tools/art/detourer.py` rend transparent un fond uni ; `tools/art/cadre.py` rend transparent le centre uni d'un cadre.

## Carte (Retro Diffusion)
- Images de 24 px (cases de 48 px à ×2, puis ×3 et ×4 en 1920 × 1080). `tools/art/generer.py` estime le prix
  (gratuit), génère des candidats et installe le candidat choisi ; `tools/art/assets.json` décrit les images à produire.
- Forêts et montagnes : des arbres et des sommets seuls (`art/decor/`) semés par le jeu (`DecorLayer`) ; écarté : une
  image par case (trous réguliers).
- Terrains : deux images par terrain (`grass-2.png`…), les bords sont calculés par le shader (pas d'images de bords).
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
- **Échelle commune** : un humain fait environ 46 à 54 px de haut (à peu près une case), le Gobelin environ 28, l'Orc 41,
  le Zombie 44, l'Ogre 58, le Troll 64 (orc < ogre < troll), le Colosse 70 ; le Cavalier et le Dragon, longs, couvrent 2 cases (environ 90 × 80).
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
- Écartés : « Bitforge » (`creer`) à cette taille, le style « classic » (trop dessin animé), le style « retro » de Retro
  Diffusion pour les créatures (images carrées, toutes de face et de la même taille), repeindre une seule zone
  (`repeindre` demande un abonnement payant : `retoucher` sur toute l'image à la place).

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
- Écartés : les cadres épais du premier essai, B7 (fil d'or, trop discret), les boutons de pierre de 96 × 32, les cadres
  de Kenney, la police DotGothic16 (la police est VT323).
