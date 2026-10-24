"""Le joueur (son royaume) et son héros."""

from reglages import POINTS_MOUVEMENT_PAR_JOUR, RAYON_VISION
from objets import Mine
from chemin import cout_pas


class Joueur:
    def __init__(self, nom):
        self.nom = nom
        self.ressources = {"or": 2500, "bois": 10, "minerai": 10, "gemmes": 2}
        self.mines = []

    def revenus_du_jour(self):
        gains = {}
        for mine in self.mines:
            gains[mine.ressource] = gains.get(mine.ressource, 0) + Mine.PRODUCTION[mine.ressource]
        return gains


class Heros:
    def __init__(self, nom, joueur, x, y):
        self.nom = nom
        self.joueur = joueur
        self.x = x
        self.y = y
        self.mouvement_max = POINTS_MOUVEMENT_PAR_JOUR
        self.mouvement = self.mouvement_max
        self.vision = RAYON_VISION

    @property
    def position(self):
        return (self.x, self.y)

    def nouveau_jour(self):
        self.mouvement = self.mouvement_max

    def peut_faire_pas(self, carte, case):
        return cout_pas(carte, self.position, case) <= self.mouvement

    def faire_pas(self, carte, case):
        self.mouvement -= cout_pas(carte, self.position, case)
        self.x, self.y = case
        carte.reveler(self.x, self.y, self.vision)
