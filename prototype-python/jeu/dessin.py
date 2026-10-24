"""Dessin procédural des tuiles et des objets (aucune image externe nécessaire).

Plus tard, on pourra remplacer ces fonctions par de vraies images (sprites).
"""

import random
import pygame

from reglages import TAILLE_TUILE as T, TERRAINS, COULEUR_JOUEUR
from objets import TasDeRessource, Coffre, Mine


def _varier(couleur, rng, ampleur=12):
    d = rng.randint(-ampleur, ampleur)
    return tuple(max(0, min(255, c + d)) for c in couleur)


def dessiner_tuile(surface, terrain, x, y, rng):
    """Dessine une tuile de terrain à la position pixel (x, y)."""
    base = TERRAINS[terrain]["couleur"]
    pygame.draw.rect(surface, _varier(base, rng, 6), (x, y, T, T))

    if terrain in ("herbe", "marais", "sable"):
        for _ in range(10):
            px, py = x + rng.randrange(T), y + rng.randrange(T)
            pygame.draw.circle(surface, _varier(base, rng, 25), (px, py), rng.choice((1, 2)))
        if terrain == "marais":
            for _ in range(2):
                px, py = x + rng.randrange(6, T - 14), y + rng.randrange(6, T - 8)
                pygame.draw.ellipse(surface, (60, 90, 110), (px, py, 12, 5))
    elif terrain == "route":
        for _ in range(8):
            px, py = x + rng.randrange(T), y + rng.randrange(T)
            pygame.draw.circle(surface, (140, 115, 80), (px, py), 2)
    elif terrain == "foret":
        for _ in range(3):
            px, py = x + rng.randrange(8, T - 8), y + rng.randrange(14, T - 4)
            vert = _varier((30, 80, 35), rng, 15)
            pygame.draw.rect(surface, (80, 55, 30), (px - 2, py, 4, 6))
            pygame.draw.polygon(surface, vert, [(px, py - 18), (px - 10, py + 2), (px + 10, py + 2)])
    elif terrain == "eau":
        for _ in range(3):
            px, py = x + rng.randrange(4, T - 16), y + rng.randrange(6, T - 6)
            pygame.draw.arc(surface, (110, 160, 220), (px, py, 12, 6), 0, 3.14, 2)
    elif terrain == "montagne":
        px = x + T // 2 + rng.randint(-4, 4)
        pygame.draw.polygon(surface, (95, 88, 82), [(px, y + 4), (x + 2, y + T - 2), (x + T - 2, y + T - 2)])
        pygame.draw.polygon(surface, (150, 142, 134), [(px, y + 4), (px - 14, y + T - 2), (px, y + T - 2)])
        pygame.draw.polygon(surface, (240, 240, 245), [(px, y + 4), (px - 6, y + 16), (px + 6, y + 16)])


def construire_surface_terrain(carte):
    """Pré-dessine tout le terrain de la carte une seule fois."""
    surface = pygame.Surface((carte.largeur * T, carte.hauteur * T))
    rng = random.Random(1)
    for ty in range(carte.hauteur):
        for tx in range(carte.largeur):
            dessiner_tuile(surface, carte.terrain_en(tx, ty), tx * T, ty * T, rng)
    return surface


COULEURS_RESSOURCES = {
    "or": (240, 200, 40),
    "bois": (140, 90, 40),
    "minerai": (110, 110, 120),
    "gemmes": (200, 60, 200),
}


def dessiner_icone_ressource(surface, ressource, cx, cy, r=7):
    """Petite icône ronde pour une ressource (barre du haut, tas sur la carte)."""
    couleur = COULEURS_RESSOURCES[ressource]
    if ressource == "gemmes":
        pygame.draw.polygon(surface, couleur, [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)])
        pygame.draw.polygon(surface, (255, 200, 255), [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], 1)
    elif ressource == "bois":
        pygame.draw.rect(surface, couleur, (cx - r, cy - r // 2, 2 * r, r))
        pygame.draw.circle(surface, (200, 160, 100), (cx + r, cy), r // 2)
    else:
        pygame.draw.circle(surface, couleur, (cx, cy), r)
        pygame.draw.circle(surface, (40, 30, 10), (cx, cy), r, 1)


def dessiner_objet(surface, objet, x, y, temps):
    """Dessine un objet de la carte à la position pixel (x, y) de sa tuile."""
    cx, cy = x + T // 2, y + T // 2
    if isinstance(objet, TasDeRessource):
        for dx, dy in ((-6, 2), (6, 2), (0, -5)):
            dessiner_icone_ressource(surface, objet.ressource, cx + dx, cy + dy, 7)
    elif isinstance(objet, Coffre):
        pygame.draw.rect(surface, (110, 70, 30), (cx - 13, cy - 6, 26, 18))
        pygame.draw.rect(surface, (150, 100, 45), (cx - 13, cy - 12, 26, 8))
        pygame.draw.rect(surface, (230, 190, 60), (cx - 13, cy - 6, 26, 3))
        pygame.draw.rect(surface, (230, 190, 60), (cx - 3, cy - 4, 6, 7))
        # petit scintillement
        if int(temps * 2) % 2 == 0:
            pygame.draw.circle(surface, (255, 255, 200), (cx + 10, cy - 13), 2)
    elif isinstance(objet, Mine):
        pygame.draw.polygon(surface, (90, 80, 70), [(cx - 20, cy + 16), (cx, cy - 14), (cx + 20, cy + 16)])
        pygame.draw.ellipse(surface, (20, 15, 10), (cx - 7, cy + 2, 14, 14))
        dessiner_icone_ressource(surface, objet.ressource, cx + 13, cy + 12, 6)
        couleur = COULEUR_JOUEUR if objet.proprietaire else (200, 200, 200)
        pygame.draw.line(surface, (60, 40, 20), (cx - 14, cy - 2), (cx - 14, cy - 20), 2)
        pygame.draw.polygon(surface, couleur, [(cx - 13, cy - 20), (cx - 2, cy - 16), (cx - 13, cy - 12)])


def dessiner_heros(surface, cx, cy, temps):
    """Un cavalier très stylisé avec son étendard."""
    pygame.draw.ellipse(surface, (0, 0, 0), (cx - 14, cy + 10, 28, 8))
    # cheval
    pygame.draw.ellipse(surface, (240, 235, 225), (cx - 13, cy - 2, 26, 14))
    pygame.draw.rect(surface, (240, 235, 225), (cx + 7, cy - 10, 6, 12))
    for dx in (-10, -4, 4, 9):
        pygame.draw.line(surface, (220, 215, 205), (cx + dx, cy + 10), (cx + dx, cy + 14), 2)
    # cavalier
    pygame.draw.rect(surface, (60, 60, 160), (cx - 5, cy - 14, 9, 14))
    pygame.draw.circle(surface, (235, 200, 160), (cx, cy - 17), 4)
    # étendard qui flotte
    flottement = 2 if int(temps * 3) % 2 else 0
    pygame.draw.line(surface, (80, 50, 20), (cx - 9, cy + 2), (cx - 9, cy - 26), 2)
    pygame.draw.polygon(surface, COULEUR_JOUEUR,
                        [(cx - 8, cy - 26), (cx + 4, cy - 23 + flottement), (cx - 8, cy - 18)])
