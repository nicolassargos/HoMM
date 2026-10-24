# Plan du jeu

Une direction, pas un contrat : les idées peuvent changer.
On avance étape par étape, sans se presser : chaque étape est jouable et testée avant la suivante.

## L'intention
- **Jeu de stratégie façon Heroes** : villes, économie, recrutement chaque semaine, armées.
- **Cartes aléatoires**, rejouables (à partir du `MapGenerator` existant).
- **Une seule faction** pour commencer (environ 7 niveaux de créatures).
- **Combat** : résolution automatique d'abord, écran de combat tactique plus tard.
- **Adversaires IA** : prévus, mais plus tard. D'ici là, chaque carte a un objectif simple
  (vaincre les gardiens, prendre les villes neutres…) pour que la partie ait une fin.
- **Graphismes** : packs d'images gratuits (CC0, itch.io) une fois le jeu jouable.
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

### 2. Villes
- Un type de ville (Château), placé sur la carte.
- Un bâtiment par jour, payé avec les ressources (les bâtiments décrits dans un fichier de données).
- Des créatures à recruter, renouvelées chaque semaine (`CreatureType.Growth`, `Cost`).
- Recrutement d'un second héros.
- Écran de ville (côté affichage). C'est le bon moment pour découper `Main.cs`
  (caméra, commandes) qui grossira avec ce nouvel écran.
- Une fois le recrutement possible : rééquilibrer et relever `MaxGuardTier`, pour que
  la victoire soit atteignable.

### 3. Progression du héros
- Expérience gagnée en combat, niveaux.
- Les 4 caractéristiques : attaque, défense, puissance, savoir.
- L'attaque et la défense du héros s'ajoutent à celles des créatures dans `Battle`.
- Artefacts plus tard.

### 4. Parties complètes
- Conditions de victoire et de défaite (choisies au lancement de la partie).
- Générateur v2 : zones, gardiens de plus en plus forts loin du départ, villes neutres,
  monstres qui gardent les passages.
- Sauvegarde et chargement. Il faudra donner des identifiants aux objets, héros et joueurs
  (aujourd'hui ils se référencent directement).

### 5. Graphismes
- Remplacer les formes dessinées par un pack d'images (possible dès l'étape 2).
- Seul `MapRenderer.cs` (et l'interface) change. Les images sont trouvées par convention
  à partir des identifiants, par exemple `res://art/creatures/piquier.png` :
  pas de chemins d'images dans les fichiers de règles.

### 6. Combat tactique
- Écran de combat sur grille hexagonale.
- Même séparation : le combat (état, actions) dans `Core`, l'écran dans `Game`.
- Le combat automatique reste disponible en « combat rapide ».

### 7. Adversaires IA
- Joueurs ordinateur avec leurs héros et leurs villes.
- L'IA joue avec les mêmes méthodes de `GameState` que le joueur humain, sans attendre l'affichage.
- L'affichage rejoue les déplacements des héros ennemis (événements `HeroMoved`) à un rythme lisible.

### 8. Ensuite
- Magie, autres factions, nouveaux objets sur la carte.

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
