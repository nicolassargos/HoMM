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

## Créatures de combat (PixelLab)
- **Échelle commune** : un humain fait environ 46 à 54 px de haut (à peu près une case), le Gobelin environ 28, l'Orc 41,
  l'Ogre 58, le Colosse 70 ; le Cavalier et le Dragon, longs, couvrent 2 cases (environ 90 × 80).
- **Une créature à la fois** : d'abord l'image fixe (qui sert aussi d'icône), puis, une fois acceptée, ses animations
  (attente, marche, attaque : feuilles de 8 images, 4 colonnes × 2 rangées, `{id}-idle.png`, `-walk`, `-attack`).
- **Outil** : `tools/art/creature.py` enchaîne les étapes (`fixe`, `choisir … fixe`, `animer`, `retoucher`,
  `choisir … idle`, `page`) d'après la **fiche** de la créature (`tools/art/creatures-art.json` : description, sujet,
  hauteur, taille de génération, modèle de style, image d'angle, références, miroir, corde, mouvements, choix faits) et
  un **gabarit** par sorte de créature (humain, volant, quadrupède, grand). Les grandes images choisies sont gardées
  dans `art-candidats/references/derives/`. Chaque feuille est vérifiée (corps qui glisse, attaque qui ne revient pas à
  sa place, autre personnage ou effet, éclats, image coupée au bord, traits fins qui clignotent) ; la **page de la
  créature** (`en-cours/<id>.html`) montre images fixes et animations sur des cases du champ, face au défenseur.
  En jeu : `--creature-preview=id,…`.
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
- **À retirer après coup** : étincelles cyan, éclats d'impact, éclairs ou flammes d'une attaque (les projectiles et
  les sorts seront des effets du jeu), un adversaire dessiné dans l'animation ; recentrer une marche qui glisse ;
  remettre les traits fins (corde d'arc) perdus à la réduction.
- **Vitesse et pauses** : `art/creatures/battle/animations.json` (images par seconde par animation, pause de 5 à 10 s
  entre deux attentes). L'utilisateur trouve les animations un peu chargées : préférer des mouvements discrets.
- Écartés : « Bitforge » (`creer`) à cette taille, le style « classic » (trop dessin animé), le style « retro » de Retro
  Diffusion pour les créatures (images carrées, toutes de face et de la même taille), repeindre une seule zone
  (`repeindre` demande un abonnement payant : `retoucher` sur toute l'image à la place).

## Interface
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
