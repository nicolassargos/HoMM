"""Recherche de chemin (A*) sur la carte d'aventure, avec coûts de terrain."""

import heapq
import math

DIRECTIONS = [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)]


def cout_pas(carte, depart, arrivee):
    """Coût pour passer de la case `depart` à la case voisine `arrivee`.

    Comme dans Heroes, c'est le terrain de départ qui compte, et un pas
    en diagonale coûte environ 1,41 fois plus.
    """
    base = carte.cout_terrain(*depart)
    if depart[0] != arrivee[0] and depart[1] != arrivee[1]:
        return int(base * math.sqrt(2))
    return base


def trouver_chemin(carte, depart, arrivee):
    """Renvoie la liste des cases de `depart` (exclu) à `arrivee` (inclus), ou None."""
    if depart == arrivee or not carte.est_franchissable(*arrivee):
        return None

    def estimation(case):
        dx = abs(case[0] - arrivee[0])
        dy = abs(case[1] - arrivee[1])
        # Distance « octile » avec le terrain le moins cher (la route)
        return 75 * (max(dx, dy) + (math.sqrt(2) - 1) * min(dx, dy))

    file = [(estimation(depart), 0, depart)]
    meilleur_cout = {depart: 0}
    venant_de = {}

    while file:
        _, cout, case = heapq.heappop(file)
        if case == arrivee:
            chemin = []
            while case != depart:
                chemin.append(case)
                case = venant_de[case]
            chemin.reverse()
            return chemin
        if cout > meilleur_cout.get(case, float("inf")):
            continue
        # On ne traverse pas un objet : on peut seulement s'arrêter dessus
        if case != depart and carte.objet_en(*case) is not None:
            continue
        for dx, dy in DIRECTIONS:
            voisin = (case[0] + dx, case[1] + dy)
            if not carte.est_franchissable(*voisin):
                continue
            # Pas de diagonale qui « coupe » un coin infranchissable
            if dx and dy and not (carte.est_franchissable(case[0] + dx, case[1])
                                  and carte.est_franchissable(case[0], case[1] + dy)):
                continue
            nouveau = cout + cout_pas(carte, case, voisin)
            if nouveau < meilleur_cout.get(voisin, float("inf")):
                meilleur_cout[voisin] = nouveau
                venant_de[voisin] = case
                heapq.heappush(file, (nouveau + estimation(voisin), nouveau, voisin))
    return None
