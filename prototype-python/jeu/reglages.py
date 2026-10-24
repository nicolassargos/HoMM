"""Réglages généraux du prototype : tailles, couleurs, terrains."""

# Fenêtre
LARGEUR_ECRAN = 1280
HAUTEUR_ECRAN = 720
FPS = 60

# Carte
TAILLE_TUILE = 48
CARTE_LARGEUR = 40   # en tuiles
CARTE_HAUTEUR = 30
LARGEUR_PANNEAU = 256  # panneau d'information à droite
HAUTEUR_BARRE = 32     # barre de ressources en haut

# Héros
POINTS_MOUVEMENT_PAR_JOUR = 1500  # comme dans Heroes : 100 = une case d'herbe
RAYON_VISION = 5
VITESSE_ANIMATION = 0.09  # secondes par case pendant le déplacement

# Terrains : nom -> (coût de déplacement, franchissable, couleur de base)
TERRAINS = {
    "herbe":    {"cout": 100, "passable": True,  "couleur": (86, 150, 60)},
    "route":    {"cout": 75,  "passable": True,  "couleur": (170, 140, 95)},
    "foret":    {"cout": 150, "passable": True,  "couleur": (52, 105, 45)},
    "sable":    {"cout": 150, "passable": True,  "couleur": (214, 196, 128)},
    "marais":   {"cout": 175, "passable": True,  "couleur": (74, 98, 72)},
    "eau":      {"cout": 0,   "passable": False, "couleur": (40, 90, 170)},
    "montagne": {"cout": 0,   "passable": False, "couleur": (120, 112, 104)},
}

# Couleurs de l'interface
COULEUR_FOND_PANNEAU = (40, 32, 24)
COULEUR_BORD = (190, 160, 90)
COULEUR_TEXTE = (240, 228, 200)
COULEUR_TEXTE_DOUX = (180, 168, 140)
COULEUR_BROUILLARD = (12, 12, 16)
COULEUR_JOUEUR = (200, 40, 40)

NOMS_JOURS = ["Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche"]
