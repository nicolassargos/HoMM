# HoMM – notes pour Claude

Jeu PC fortement inspiré de Heroes of Might and Magic / King's Bounty. L'utilisateur est francophone mais préfère échanger en anglais (un anglais simple et clair). Les textes du jeu, les commentaires, la documentation, les commits et les PR restent en français.

## Plan
La vision du jeu et les étapes prévues sont dans `PLAN.md`, importé ci-dessous. Le suivre pour chaque nouvelle fonctionnalité, et le mettre à jour quand les idées changent ou qu'une étape avance (cocher ✅, noter les décisions prises).
`PLAN.md` reste court : la direction, l'étape en cours et les choix encore valables. Le détail d'un travail fini (chiffres mesurés, essais écartés, classes et fichiers) va dans `HISTORIQUE.md` (non importé : le lire quand on a besoin du passé d'une partie du jeu). Avant un travail d'images, lire `tools/art/METHODE.md` et y noter ce qui marche ou non.

@PLAN.md

## Git
- Une branche par étape ou par sujet, créée depuis `main` à jour (`git switch main`, `git pull`, `git switch -c <nom>`). Noms en minuscules avec tirets (ex. `etape-2-villes`).
- Une PR de la branche **directement vers `main`** : pas de PR empilée sur une autre branche.
- Avant de proposer une PR : `dotnet test` réussi et `dotnet build` (dans `godot/`) sans erreur.
- Messages de commit et descriptions de PR en français.
- Après la fusion (faite par l'utilisateur) : revenir sur `main` et récupérer les changements (`git switch main`, puis `git pull`), avant toute nouvelle branche. L'utilisateur supprime lui-même les branches fusionnées.
- Ne pas fusionner une PR ni supprimer de branche sans l'accord de l'utilisateur.

## Choix techniques
- Godot 4.7.2 **édition .NET** + C# (.NET 8). Le projet est dans `godot/`.
- Utiliser l'éditeur Godot le moins possible : tout est construit par le code (scène unique `scenes/Main.tscn`).
- Les cartes doivent, à terme, être générées automatiquement (`MapGenerator`).
- Code : identifiants en anglais, commentaires et textes affichés en français.

## Organisation
- `godot/scripts/Core/` : règles en C# pur, **sans dépendance à Godot** (testables). Y mettre toute la logique de jeu.
- Règles → affichage : `GameState` ajoute des événements (type de base dans `GameEvents.cs`, chaque événement défini dans le fichier de ce qu'il concerne) que `Main` lit avec `TakeEvents()` et montre à son rythme. L'affichage ne modifie l'état qu'en appelant les méthodes de `GameState`. Toute nouvelle action visible (animation, fenêtre, son) passe par un événement. Les règles ne produisent aucun texte affiché : tout texte va dans `Game/Texts.cs`.
- `godot/scripts/Game/` : affichage, interface, commandes (Godot). Une classe Godot par fichier, nom du fichier = nom de la classe.
- `tests/HoMM.Core.Tests/` : tests xUnit des règles.
- `prototype-python/` : ancien prototype, gardé pour mémoire, ne plus le faire évoluer.

## Services d'images (décision de l'utilisateur, 2026-11-04)
- **PixelLab** (`tools/art/pixellab.py`, clé dans la variable `PIXELLAB_API_KEY`) : les **personnages et créatures**
  (images de combat, animations), et des essais pour l'**interface**.
- **Retro Diffusion** (`tools/art/generer.py`, clé dans `RETRO_DIFFUSION_API_KEY`) : les **cartes** (terrains,
  décors, objets) et les **portraits** des héros humains.
- Ne jamais afficher ni écrire les clés. Montrer le prix avant de dépenser (Retro Diffusion : `cout`, gratuit ;
  PixelLab n'a pas d'estimation gratuite : `solde` avant et après) et attendre l'accord de l'utilisateur.

## Propositions d'images (dossier `art-candidats/`, non suivi par git)
Décision de l'utilisateur, valable pour toutes les sessions :
- `art-candidats/en-cours/` ne contient **que ce qui attend une réponse de l'utilisateur** : captures d'écran
  de comparaison (`jeu-*.png`), agrandissements (`zoom-*.png`), pages de choix, candidats générés pas encore
  choisis (`en-cours/candidats/<nom>/`, écrits par `tools/art/generer.py`).
- Une fois la décision prise, tout part dans `art-candidats/archives/AAAA-MM-JJ-sujet/` avec
  `python -I tools/art/archiver.py sujet`. Ne rien laisser traîner à la racine de `art-candidats/`.
- `art-candidats/references/` : images données par l'utilisateur (modèles, captures), à garder telles quelles.
- Outils : `tools/art/generer.py` (prix, génération, choix ; `choisir` retrouve aussi un candidat archivé),
  `tools/art/apercu.py` (page de choix dans `en-cours/`), `tools/art/revue.py` (page de revue où l'on choisit et commente chaque image, une par lot de candidats : `32px`, `v2` ; `serveur` écrit les réponses sur le disque, `appliquer` les installe), `tools/art/zoom.py` (agrandir une capture),
  `tools/art/pieds.py` (les pieds des images de la carte pour l'ombre de contact : `proposer`, `page` de revue où l'on corrige les points, `appliquer` ; écrit `godot/art/pieds.json`),
  `tools/art/sans_socle.py` (retirer la tache d'herbe et l'ombre ovale sous un arbre généré, garder le trait des racines, couper les rangées vides du bas ; `--buisson` pour un buisson, `--sable` pour la tache de sable d'un arbre du désert), `tools/art/blanchir.py` (blanchir au soleil : du bois sec, gris chaud, à partir d'une souche moussue ; garde l'ombrage), `tools/art/luminosite.py` (`mesure` : la luminosité moyenne d'images ; `regler` : éclaircir ou assombrir une image jusqu'à une luminosité, par une courbe ; `--garder-sombres` : les tons sombres ne bougent pas), `tools/art/reduire.py` (réduire ou retourner une image sans la flouter), `tools/art/cadre.py` (rendre transparent le
  centre uni d'un cadre généré), `tools/art/detourer.py` (rendre
  transparent un fond uni), `tools/art/pixellab.py` (PixelLab : solde, animations, éléments d'interface).
  **Créatures de combat** : `tools/art/creature.py` fait tout le travail d'une créature avec PixelLab (images fixes,
  animations, vérifications automatiques, une page de choix par créature dans `en-cours/<id>.html`, installation dans
  le jeu), d'après sa fiche dans `tools/art/creatures-art.json` (description, modèle de style, image d'angle,
  références, hauteur, choix faits) et un gabarit par sorte de créature ; `tools/art/nettoyage.py` contient les
  traitements d'image ; `tools/art/apercu_anims.py <id>` joue les animations candidates à la taille du jeu, entières. Voir la créature en jeu : `--creature-preview=id1,id2` (avec `--battle-auto`).
  Captures du jeu : `godot` avec `-- --capture=chemin.png` (et `--reveal`, `--zoom` (ou `--zoom=0.4` : toute la carte d'un coup), `--terrain-test`,
  `--town` (et `--town-window=build|recruit|tavern|count|guild|market`, `--town-build=id`), `--rules`, `--center=treasure`, `obelisk` ou un lieu (`windmill`, `sage`, `temple`, `tower`, `water-wheel`, `fairy-ring`, `shrine`), `--treasure-map`, `--dig-test`, `--battle-test` (et `--battle-auto`, `--battle-book` ou `--battle-book=id` : le livre de sorts, ou ce sort à viser ; `--battle-options` : les options du combat ; `--battle-aim=colonne,rangée,angle` : la souris sur cette case, à cet angle de son centre, ou `--battle-aim=x,y` : ce point du champ, pour viser le corps d'une créature ; le curseur choisi s'écrit dans la console ; `--battle-scene=open` ou `forest` : le décor du champ, au lieu du tirage), `--spells` (le héros connaît tous les sorts), `--rich` (or et ressources sans fin : remis au plein au début de chaque tour du joueur), `--skill=tactique:3` (le héros a ces compétences ; plusieurs : `--skill=a:2,b`), `--battle-showcase`, `--creature-preview=id,…`, `--effect-preview=foudre` ou `foudre-forte` (un effet de combat joué en boucle sur les troupes ; `foudre:0.2` : figé à cet instant ; `eclair` : le tir du Mage, sans cesse, de la première troupe de gauche vers la première de droite), `--hover=x,y` (case survolée), `--book-school=feu`, `--book-spread=1`, `--book-turn=0.3`, `--book-open=0.3`, `--book-hover=boule-de-feu`, `--guild-book` (avec `--town-window=guild` : le grimoire ouvert sur l'académie) (le livre de sorts : un onglet, une double page, la page qui tourne ou l'ouverture figées, la lueur d'un sort), `--open=hero`, `kingdom`, `level`, `end`, `options`, `save`, `spellbook`, `skills` ou `encounter` (écran du héros, royaume, choix de niveau, fin de partie, options, sauvegarder, livre de sorts, arbres de compétences avec 4 points, offre d'un gardien par la Diplomatie ; `--hero-tab=skills` ou `magic` : cet onglet de l'écran du héros), `--menu` (ou `--menu=new`, `--menu=load` : menu principal ; `--dropdown-open` : la liste des tailles ouverte), `--ui-preview` (propositions d'éléments d'interface), `--size=tiny|small|normal|large|huge` (taille de la carte : 36 × 27, puis celles de Heroes 3, 36, 72, 108, 144 cases de côté), `--access=open|normal|closed` (les passages entre les zones ; normaux sans l'option), `--boat` (un bateau sur l'eau la plus proche du héros), `--map-type=continent|seas|islands|archipelago` (le type de la carte : un continent sans l'option), `--opponents=N` (adversaires), `--center=opponent`, `--turns=N` (jouer N jours au lancement), `--defense-test` (un adversaire attaque ; `--defense-tactical` : défense dirigée), `--difficulty=easy|normal|hard`, `--faction=morts-vivants` (la faction du joueur), `--capture-delay=secondes`, `--shadow=force,longueur,penche,contact,flou` (les ombres de la carte, ex. `0.45,0.5,0.45,0.5,0.6`), `--shadow-tune` (curseurs pour régler les ombres en jeu), `--cursor-preview=dossier`, `--ground=réglage:valeur` (le shader du sol, ex. `beach_width:6,flower_density:0.15`) selon le besoin).

## Commandes
- Tests : `dotnet test` (à la racine)
- Mesures de l'équilibre (simulation seule, adversaires à chaque niveau, courbe du Château ; toujours les mêmes graines, pour comparer avant et après un changement) : `HOMM_MESURES=1 dotnet test --filter Category=Mesures --logger "console;verbosity=detailed"` (`tests/HoMM.Core.Tests/MesuresTests.cs` ; ignorées par un `dotnet test` ordinaire). Y ajouter une mesure plutôt qu'écrire un test jetable.
- Captures de tous les écrans (avant une PR d'interface, puis les regarder) : `python -I tools/captures.py` (ou `… ville` pour une partie), dans `art-candidats/en-cours/revue/`
- Compiler : `dotnet build` (dans `godot/`)
- Lancer : `godot\lancer.bat` (Godot attendu dans `C:\Godot\`) : le menu principal ; toute option (ou `-- --play`) entre directement dans une partie ; plein écran (F11 ou Alt+Entrée : fenêtre agrandie ; `-- --windowed` pour démarrer en fenêtre), option `-- --seed=42` pour une carte fixe, `-- --load` pour reprendre la dernière sauvegarde
