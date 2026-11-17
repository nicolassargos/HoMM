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
