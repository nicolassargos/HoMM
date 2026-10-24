"""La carte d'aventure : terrain, objets, brouillard de guerre, génération aléatoire."""

import random
from collections import deque

from reglages import TERRAINS, CARTE_LARGEUR, CARTE_HAUTEUR
from objets import RESSOURCES, TasDeRessource, Coffre, Mine


class Carte:
    def __init__(self, largeur=CARTE_LARGEUR, hauteur=CARTE_HAUTEUR, graine=None):
        self.largeur = largeur
        self.hauteur = hauteur
        self.rng = random.Random(graine)
        self.terrain = [["herbe"] * largeur for _ in range(hauteur)]
        self.objets = {}  # (x, y) -> ObjetCarte
        self.explore = [[False] * largeur for _ in range(hauteur)]
        self.depart = (4, 4)
        self._generer()

    # ------------------------------------------------------------------ accès
    def dans_carte(self, x, y):
        return 0 <= x < self.largeur and 0 <= y < self.hauteur

    def terrain_en(self, x, y):
        return self.terrain[y][x]

    def cout_terrain(self, x, y):
        return TERRAINS[self.terrain[y][x]]["cout"]

    def est_franchissable(self, x, y):
        return self.dans_carte(x, y) and TERRAINS[self.terrain[y][x]]["passable"]

    def objet_en(self, x, y):
        return self.objets.get((x, y))

    def retirer_objet(self, objet):
        self.objets.pop((objet.x, objet.y), None)

    def reveler(self, cx, cy, rayon):
        for y in range(cy - rayon, cy + rayon + 1):
            for x in range(cx - rayon, cx + rayon + 1):
                if self.dans_carte(x, y) and (x - cx) ** 2 + (y - cy) ** 2 <= rayon * rayon + 1:
                    self.explore[y][x] = True

    # ------------------------------------------------------------- génération
    def _tache(self, terrain, taille):
        """Pose une « tache » de terrain par marche aléatoire."""
        x = self.rng.randrange(self.largeur)
        y = self.rng.randrange(self.hauteur)
        for _ in range(taille):
            for dx in (0, 1):
                for dy in (0, 1):
                    if self.dans_carte(x + dx, y + dy):
                        self.terrain[y + dy][x + dx] = terrain
            x = min(max(x + self.rng.choice((-1, 0, 1)), 0), self.largeur - 1)
            y = min(max(y + self.rng.choice((-1, 0, 1)), 0), self.hauteur - 1)

    def _tracer_route(self, a, b):
        """Trace une route sinueuse de a vers b (ponts et cols compris)."""
        x, y = a
        while (x, y) != b:
            self.terrain[y][x] = "route"
            if self.rng.random() < 0.6:
                x += (b[0] > x) - (b[0] < x)
            else:
                y += (b[1] > y) - (b[1] < y)
        self.terrain[y][x] = "route"

    def _cases_accessibles(self):
        """Toutes les cases qu'on peut atteindre à pied depuis le départ."""
        vues = {self.depart}
        file = deque([self.depart])
        while file:
            x, y = file.popleft()
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                v = (x + dx, y + dy)
                if v not in vues and self.est_franchissable(*v):
                    vues.add(v)
                    file.append(v)
        return vues

    def _generer(self):
        surface = self.largeur * self.hauteur
        for terrain, nombre, taille in (
            ("foret", surface // 120, 25),
            ("sable", surface // 400, 20),
            ("marais", surface // 400, 18),
            ("eau", surface // 300, 30),
            ("montagne", surface // 200, 18),
        ):
            for _ in range(nombre):
                self._tache(terrain, taille)

        # Zone de départ dégagée
        dx0, dy0 = self.depart
        for y in range(dy0 - 2, dy0 + 3):
            for x in range(dx0 - 2, dx0 + 3):
                if self.dans_carte(x, y):
                    self.terrain[y][x] = "herbe"

        # Quelques routes qui relient le départ à des points lointains
        etapes = [self.depart,
                  (self.largeur // 2, self.hauteur // 2),
                  (self.largeur - 5, self.hauteur - 5),
                  (self.largeur - 5, 4),
                  (5, self.hauteur - 4)]
        self._tracer_route(etapes[0], etapes[1])
        for fin in etapes[2:]:
            self._tracer_route(etapes[1], fin)

        # Objets, uniquement sur des cases accessibles
        libres = [c for c in self._cases_accessibles()
                  if abs(c[0] - dx0) + abs(c[1] - dy0) > 2 and self.terrain[c[1]][c[0]] != "route"]
        self.rng.shuffle(libres)

        def placer(objet):
            self.objets[(objet.x, objet.y)] = objet

        for ressource in RESSOURCES:
            x, y = libres.pop()
            placer(Mine(x, y, ressource))
        for _ in range(6):
            x, y = libres.pop()
            placer(Coffre(x, y))
        for _ in range(28):
            x, y = libres.pop()
            placer(TasDeRessource(x, y, self.rng.choice(RESSOURCES + ["or", "bois", "minerai"])))
