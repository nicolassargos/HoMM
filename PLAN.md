# Plan du jeu

Une direction, pas un contrat : les idées peuvent changer.

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
1. **Armées, monstres neutres, combat automatique**
   - Le héros a une armée de 7 emplacements maximum (« 12 archers »).
   - Les créatures sont décrites dans un fichier de données, pas dans le code.
   - Des monstres gardent les objets et les passages.
   - Le combat est résolu par une formule, avec des pertes des deux côtés.
   - Perdre son armée = fin de partie.
2. **Villes**
   - Un type de ville, placé sur la carte.
   - Un bâtiment par jour, payé avec les ressources.
   - Des créatures à recruter chaque semaine.
   - Recrutement d'un second héros.
3. **Progression du héros**
   - Expérience et niveaux.
   - Les 4 caractéristiques : attaque, défense, puissance, savoir.
   - Elles comptent dans le combat. Artefacts plus tard.
4. **Parties complètes**
   - Conditions de victoire et de défaite.
   - Générateur v2 : zones, gardiens de plus en plus forts loin du départ, villes neutres.
   - Sauvegarde et chargement.
5. **Graphismes** : remplacer les formes dessinées par un pack d'images (possible dès l'étape 2).
6. **Combat tactique**
   - Écran de combat sur grille hexagonale.
   - Le combat automatique reste disponible en « combat rapide ».
7. **Adversaires IA** : joueurs ordinateur avec leurs héros et leurs villes.
8. **Ensuite** : magie, autres factions, nouveaux objets sur la carte.

## Choix à respecter dès maintenant
- **`GameState` gère plusieurs joueurs dès l'étape 1** (liste de joueurs, tour par tour),
  même s'il n'y a qu'un humain. Cela évite une grosse réécriture quand l'IA arrivera.
- **Toutes les règles restent dans `Core/`** (sans Godot), pour que l'IA et le combat
  tactique réutilisent les mêmes règles et que tout reste testable.

## Questions ouvertes
- Rester proche des règles et des chiffres de Heroes 3, ou inventer les nôtres ?
- Thème de la première faction : médiéval classique (le Château) ou autre chose ?
