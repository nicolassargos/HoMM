# Licences des sons et des musiques

Chaque fichier de `godot/audio/` (hors `attente/`, synthétisé par `tools/audio/attente.py`) et sa source.
Choisis à l'oreille par l'utilisateur (essai d'écoute du 2026-11-16, `tools/audio/essai.py`).

## Générés par ElevenLabs (effets sonores, modèle `eleven_text_to_sound_v3`, abonnement gratuit)
L'abonnement gratuit demande de citer ElevenLabs et ne permet pas un usage commercial : prendre un abonnement payant
avant toute diffusion commerciale.
- `combat/archer-tir.mp3`, `combat/fleche-impact.mp3`, `combat/griffon-attaque.mp3`, `combat/griffon-mort.mp3`,
  `combat/cavalier-mort.mp3`, `combat/ange-gifle.mp3`, `combat/ange-mort-1.mp3`, `combat/ange-mort-2.mp3`,
  `combat/coup-epee.mp3`, `combat/coup-sourd.mp3`, `combat/mort-soldat.mp3`, `combat/sort-boule-de-feu.mp3`,
  `combat/sort-eclair.mp3`, `combat/sort-resurrection.mp3`
- Les sons des créatures des autres factions : `combat/<créature>-<action>.mp3` (décrits dans
  `tools/audio/sons-creatures.json`, faits par `tools/audio/sons.py`).

## Bibliothèques libres (OpenGameArt)
- `combat/cavalier-galop.ogg` : « Horse Gallop Loop », AntumDeluge, **CC-BY 3.0** (citer l'auteur),
  https://opengameart.org/content/horse-gallop-loop
- `combat/sort-fleche-magique.ogg` : « 80 CC0 RPG SFX » (`spell_01.ogg`), rubberduck, CC0,
  https://opengameart.org/content/80-cc0-rpg-sfx
- `musique/combat-poursuite.wav` : « Determined Pursuit (Epic Orchestra Loop) », Emma_MA, CC0,
  https://opengameart.org/content/determined-pursuit-epic-orchestra-loop (passée en mono à 22 050 Hz pour la taille ;
  musique d'attente, à remplacer par Stable Audio plus tard)
- `musique/combat-heroic-age.mp3` : « Heroic Age », Kevin MacLeod (incompetech.com), Licensed under Creative Commons:
  By Attribution 4.0, https://incompetech.com/music/royalty-free/index.html?keywords=Heroic%20Age
- `musique/combat-heated-lands.mp3` : « Heated Lands », Jonathan Shaw (InspectorJ), CC-BY 3.0,
  https://opengameart.org/content/heated-lands-rpg-orchestral-essentials-dungeon-music
- Les musiques des villes (choisies le 2026-11-17, `tools/audio/musiques.py`, liste dans
  `tools/audio/musiques-villes.json` ; **CC-BY** : citer l'auteur) :
  - `musique/ville-chateau.mp3` : « RPG Town Theme 1 », LarsG, CC-BY 4.0, https://opengameart.org/content/rpg-town-theme-1
  - `musique/ville-barbares.mp3` : « Drums in the Deepwood », Elyvilon, CC-BY 4.0,
    https://opengameart.org/content/dark-forest-tribal-troll-folk-shamanic-music-drums-in-the-deepwood
  - `musique/ville-tour.ogg` : « City in the Clouds », peastman, CC-BY 3.0, https://opengameart.org/content/city-in-the-clouds
  - `musique/ville-donjon.ogg` : « RPG - Foreboding Ambiance », Hitctrl, CC-BY 3.0,
    https://opengameart.org/content/rpg-foreboding-ambiance
  - `musique/ville-enfer.ogg` : « Demonium », Alexandr Zhelanov, CC-BY 4.0, https://opengameart.org/content/demonium
    (la version ogg de l'aperçu du site, au lieu du wav de 67 Mo)
  - `musique/ville-morts-vivants.ogg` : « RPG - The Graveyard », HitCtrl, CC-BY 3.0,
    https://opengameart.org/content/rpg-the-graveyard
  - `musique/ville-nains.mp3` : « Five Armies », Kevin MacLeod (incompetech.com), Licensed under Creative Commons:
    By Attribution 4.0, https://incompetech.com/music/royalty-free/index.html?keywords=Five%20Armies
  - `musique/ville-foret.mp3` : « Mist Forest (Orchestra) », migfus20, CC-BY 4.0,
    https://opengameart.org/content/mist-forest-orchestra
- Les musiques de la carte (choisies le 2026-11-18, `tools/audio/musiques.py carte`, liste dans
  `tools/audio/musiques-carte.json` ; **CC-BY** : citer l'auteur) :
  - `musique/carte-grass.mp3` : « World Map », Scrabbit, CC-BY 4.0, https://opengameart.org/content/world-map-1
  - `musique/carte-water.ogg` : « Exploring the Ocean », Leonardo Paz, CC-BY 4.0,
    https://opengameart.org/content/ocean-music-pack (la version ogg de l'aperçu du site)
  - `musique/carte-water-2.ogg` : « Seaside Village », Leonardo Paz, CC-BY 4.0, même page, même version
  - `musique/carte-water-3.ogg` : « Siren's Call », Leonardo Paz, CC-BY 4.0, même page, même version
  - `musique/carte-swamp.mp3` : « Crystal Meadow », joeBaxterWebb, CC-BY 4.0, https://opengameart.org/content/crystal-meadow
  - `musique/carte-sand.mp3` : « Myst », Kevin MacLeod (incompetech.com), Licensed under Creative Commons:
    By Attribution 4.0, https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100448
  - `musique/carte-rock.mp3` : « Crusade », Kevin MacLeod (incompetech.com), Licensed under Creative Commons:
    By Attribution 4.0, https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100884
  - `musique/carte-snow.mp3` : « Mevalian Winter », Tsorthan Grove, CC0, https://opengameart.org/content/mevalian-winter
- `musique/musique-menu.ogg` : la musique du menu principal (choisie le 2026-11-19, `tools/audio/musiques.py menu`),
  « Alandriel's Theme », HitCtrl, CC-BY 3.0, https://opengameart.org/node/108463
