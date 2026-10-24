"""Objets posés sur la carte : ressources, coffres, mines."""

import random

# Ressources du royaume
RESSOURCES = ["or", "bois", "minerai", "gemmes"]


class ObjetCarte:
    """Classe de base d'un objet visitable sur la carte."""

    disparait = True  # l'objet est-il retiré après la visite ?

    def __init__(self, x, y):
        self.x = x
        self.y = y

    def visiter(self, joueur):
        """Applique l'effet de l'objet et renvoie un message à afficher."""
        raise NotImplementedError


class TasDeRessource(ObjetCarte):
    QUANTITES = {"or": (500, 1000), "bois": (5, 10), "minerai": (5, 10), "gemmes": (3, 6)}

    def __init__(self, x, y, ressource):
        super().__init__(x, y)
        self.ressource = ressource
        mini, maxi = self.QUANTITES[ressource]
        self.quantite = random.randint(mini, maxi)
        if ressource == "or":
            self.quantite = self.quantite // 100 * 100

    def visiter(self, joueur):
        joueur.ressources[self.ressource] += self.quantite
        return f"Vous trouvez {self.quantite} {self.ressource}."


class Coffre(ObjetCarte):
    def __init__(self, x, y):
        super().__init__(x, y)
        self.or_contenu = random.choice([1000, 1500, 2000])

    def visiter(self, joueur):
        joueur.ressources["or"] += self.or_contenu
        return f"Un coffre au trésor ! +{self.or_contenu} or."


class Mine(ObjetCarte):
    """Une mine reste sur la carte : une fois capturée, elle rapporte chaque jour."""

    disparait = False
    PRODUCTION = {"or": 1000, "bois": 2, "minerai": 2, "gemmes": 1}
    NOMS = {"or": "Mine d'or", "bois": "Scierie", "minerai": "Mine de minerai", "gemmes": "Mine de gemmes"}

    def __init__(self, x, y, ressource):
        super().__init__(x, y)
        self.ressource = ressource
        self.proprietaire = None

    @property
    def nom(self):
        return self.NOMS[self.ressource]

    def visiter(self, joueur):
        if self.proprietaire is joueur:
            return f"{self.nom} vous appartient déjà."
        self.proprietaire = joueur
        joueur.mines.append(self)
        return f"{self.nom} capturée ! +{self.PRODUCTION[self.ressource]} {self.ressource} par jour."
