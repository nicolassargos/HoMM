"""Prototype de carte d'aventure inspiré de Heroes of Might and Magic.

Commandes :
  Clic gauche sur une case : affiche le chemin. Second clic : le héros part.
  Espace          : continuer le chemin tracé
  Clic droit/Échap: annuler le chemin
  Flèches / ZQSD  : faire défiler la carte (ou souris au bord de l'écran)
  Entrée / bouton : fin du tour
  C               : recentrer sur le héros
"""

import sys
import pygame

from reglages import (LARGEUR_ECRAN, HAUTEUR_ECRAN, FPS, TAILLE_TUILE as T,
                      LARGEUR_PANNEAU, HAUTEUR_BARRE, TERRAINS, VITESSE_ANIMATION,
                      COULEUR_FOND_PANNEAU, COULEUR_BORD, COULEUR_TEXTE, COULEUR_TEXTE_DOUX,
                      COULEUR_BROUILLARD, COULEUR_JOUEUR, NOMS_JOURS)
from carte import Carte
from heros import Joueur, Heros
from chemin import trouver_chemin, cout_pas
from objets import RESSOURCES, TasDeRessource, Coffre, Mine
import dessin

LARGEUR_VUE = LARGEUR_ECRAN - LARGEUR_PANNEAU
HAUTEUR_VUE = HAUTEUR_ECRAN - HAUTEUR_BARRE
NOMS_TERRAINS = {"herbe": "Herbe", "route": "Route", "foret": "Forêt", "sable": "Sable",
                 "marais": "Marais", "eau": "Eau", "montagne": "Montagne"}


class Jeu:
    def __init__(self, graine=None):
        pygame.display.init()
        pygame.font.init()
        pygame.display.set_caption("Carte d'aventure - prototype")
        self.ecran = pygame.display.set_mode((LARGEUR_ECRAN, HAUTEUR_ECRAN))
        self.horloge = pygame.time.Clock()
        self.police = pygame.font.Font(None, 22)
        self.police_titre = pygame.font.Font(None, 30)
        self.police_petite = pygame.font.Font(None, 18)

        self.carte = Carte(graine=graine)
        self.joueur = Joueur("Rouge")
        self.heros = Heros("Sire Roland", self.joueur, *self.carte.depart)
        self.carte.reveler(self.heros.x, self.heros.y, self.heros.vision)
        self.surface_terrain = dessin.construire_surface_terrain(self.carte)

        self.jour = 0  # 0 = jour 1, semaine 1, mois 1
        self.chemin = None
        self.en_marche = False
        self.minuteur_pas = 0.0
        self.messages = ["Bienvenue, Sire Roland ! Explorez la carte."]
        self.camera = [0.0, 0.0]
        self.centrer_camera()
        self.temps = 0.0

        self.bouton_fin = pygame.Rect(LARGEUR_VUE + 24, HAUTEUR_ECRAN - 64, LARGEUR_PANNEAU - 48, 40)
        self.echelle_mini = min((LARGEUR_PANNEAU - 16) // self.carte.largeur, 6)
        self.rect_mini = pygame.Rect(LARGEUR_VUE + (LARGEUR_PANNEAU - self.carte.largeur * self.echelle_mini) // 2,
                                     12, self.carte.largeur * self.echelle_mini,
                                     self.carte.hauteur * self.echelle_mini)

    # ---------------------------------------------------------------- caméra
    def centrer_camera(self):
        self.camera[0] = self.heros.x * T + T / 2 - LARGEUR_VUE / 2
        self.camera[1] = self.heros.y * T + T / 2 - HAUTEUR_VUE / 2
        self.borner_camera()

    def borner_camera(self):
        self.camera[0] = max(0, min(self.camera[0], self.carte.largeur * T - LARGEUR_VUE))
        self.camera[1] = max(0, min(self.camera[1], self.carte.hauteur * T - HAUTEUR_VUE))

    def case_sous_souris(self, pos):
        mx, my = pos
        if mx >= LARGEUR_VUE or my < HAUTEUR_BARRE:
            return None
        x = int((mx + self.camera[0]) // T)
        y = int((my - HAUTEUR_BARRE + self.camera[1]) // T)
        return (x, y) if self.carte.dans_carte(x, y) else None

    # ---------------------------------------------------------------- règles
    def message(self, texte):
        self.messages.append(texte)
        self.messages = self.messages[-6:]

    def date_texte(self):
        mois = self.jour // 28 + 1
        semaine = (self.jour // 7) % 4 + 1
        return f"Mois {mois}, semaine {semaine}, {NOMS_JOURS[self.jour % 7]}"

    def fin_du_tour(self):
        if self.en_marche:
            return
        self.jour += 1
        self.heros.nouveau_jour()
        gains = self.joueur.revenus_du_jour()
        for ressource, quantite in gains.items():
            self.joueur.ressources[ressource] += quantite
        texte = "Nouveau jour : " + self.date_texte() + "."
        if gains:
            texte += " Revenus : " + ", ".join(f"+{q} {r}" for r, q in gains.items())
        if self.jour % 7 == 0:
            texte = "Une nouvelle semaine commence ! " + texte
        self.message(texte)

    def cliquer_case(self, case):
        if self.en_marche or case is None:
            return
        if not self.carte.explore[case[1]][case[0]]:
            return
        if self.chemin and self.chemin[-1] == case:
            self.lancer_marche()
            return
        self.chemin = trouver_chemin(self.carte, self.heros.position, case)

    def lancer_marche(self):
        if self.chemin and self.heros.peut_faire_pas(self.carte, self.chemin[0]):
            self.en_marche = True
            self.minuteur_pas = VITESSE_ANIMATION
        elif self.chemin:
            self.message("Plus assez de points de mouvement aujourd'hui.")

    def avancer_d_un_pas(self):
        case = self.chemin[0]
        if not self.heros.peut_faire_pas(self.carte, case):
            self.en_marche = False
            return
        self.heros.faire_pas(self.carte, case)
        self.chemin.pop(0)
        objet = self.carte.objet_en(*case)
        if objet:
            self.message(objet.visiter(self.joueur))
            if objet.disparait:
                self.carte.retirer_objet(objet)
        if not self.chemin:
            self.chemin = None
            self.en_marche = False

    def portee_du_chemin(self):
        """Pour chaque case du chemin : est-elle atteignable aujourd'hui ?"""
        resultats = []
        mouvement = self.heros.mouvement
        precedente = self.heros.position
        for case in self.chemin:
            mouvement -= cout_pas(self.carte, precedente, case)
            resultats.append(mouvement >= 0)
            precedente = case
        return resultats

    # ---------------------------------------------------------------- boucle
    def executer(self):
        while True:
            dt = self.horloge.tick(FPS) / 1000
            self.temps += dt
            for evenement in pygame.event.get():
                self.gerer_evenement(evenement)
            self.mettre_a_jour(dt)
            self.dessiner()
            pygame.display.flip()

    def gerer_evenement(self, ev):
        if ev.type == pygame.QUIT:
            pygame.quit()
            sys.exit()
        elif ev.type == pygame.MOUSEBUTTONDOWN:
            if ev.button == 1:
                if self.bouton_fin.collidepoint(ev.pos):
                    self.fin_du_tour()
                elif self.rect_mini.collidepoint(ev.pos):
                    self.camera[0] = (ev.pos[0] - self.rect_mini.x) / self.echelle_mini * T - LARGEUR_VUE / 2
                    self.camera[1] = (ev.pos[1] - self.rect_mini.y) / self.echelle_mini * T - HAUTEUR_VUE / 2
                    self.borner_camera()
                else:
                    self.cliquer_case(self.case_sous_souris(ev.pos))
            elif ev.button == 3 and not self.en_marche:
                self.chemin = None
        elif ev.type == pygame.KEYDOWN:
            if ev.key in (pygame.K_RETURN, pygame.K_KP_ENTER):
                self.fin_du_tour()
            elif ev.key == pygame.K_SPACE:
                self.lancer_marche()
            elif ev.key == pygame.K_ESCAPE and not self.en_marche:
                self.chemin = None
            elif ev.key == pygame.K_c:
                self.centrer_camera()

    def mettre_a_jour(self, dt):
        # Défilement clavier et bord d'écran
        vitesse = 700 * dt
        touches = pygame.key.get_pressed()
        mx, my = pygame.mouse.get_pos()
        focus = pygame.mouse.get_focused()
        if touches[pygame.K_LEFT] or touches[pygame.K_q] or touches[pygame.K_a] or (focus and mx < 4):
            self.camera[0] -= vitesse
        if touches[pygame.K_RIGHT] or touches[pygame.K_d] or (focus and LARGEUR_VUE - 4 < mx < LARGEUR_VUE):
            self.camera[0] += vitesse
        if touches[pygame.K_UP] or touches[pygame.K_z] or touches[pygame.K_w] or (focus and my < 4):
            self.camera[1] -= vitesse
        if touches[pygame.K_DOWN] or touches[pygame.K_s] or (focus and my > HAUTEUR_ECRAN - 4 and mx < LARGEUR_VUE):
            self.camera[1] += vitesse
        self.borner_camera()

        if self.en_marche:
            self.minuteur_pas -= dt
            if self.minuteur_pas <= 0:
                self.minuteur_pas = VITESSE_ANIMATION
                self.avancer_d_un_pas()
                self.suivre_heros()

    def suivre_heros(self):
        hx = self.heros.x * T - self.camera[0]
        hy = self.heros.y * T - self.camera[1]
        marge = 3 * T
        if hx < marge or hx > LARGEUR_VUE - marge or hy < marge or hy > HAUTEUR_VUE - marge:
            self.centrer_camera()

    # ---------------------------------------------------------------- dessin
    def dessiner(self):
        self.ecran.fill((0, 0, 0))
        self.dessiner_carte()
        self.dessiner_barre_ressources()
        self.dessiner_panneau()

    def dessiner_carte(self):
        vue = pygame.Rect(0, HAUTEUR_BARRE, LARGEUR_VUE, HAUTEUR_VUE)
        self.ecran.set_clip(vue)
        cx, cy = int(self.camera[0]), int(self.camera[1])
        self.ecran.blit(self.surface_terrain, (0, HAUTEUR_BARRE),
                        pygame.Rect(cx, cy, LARGEUR_VUE, HAUTEUR_VUE))

        def ecran_xy(tx, ty):
            return tx * T - cx, ty * T - cy + HAUTEUR_BARRE

        x0, y0 = cx // T, cy // T
        x1, y1 = (cx + LARGEUR_VUE) // T + 1, (cy + HAUTEUR_VUE) // T + 1

        # Objets
        for (tx, ty), objet in self.carte.objets.items():
            if x0 <= tx <= x1 and y0 <= ty <= y1:
                dessin.dessiner_objet(self.ecran, objet, *ecran_xy(tx, ty), self.temps)

        # Chemin : vert = atteignable aujourd'hui, rouge = jours suivants
        if self.chemin:
            portee = self.portee_du_chemin()
            for i, (case, ok) in enumerate(zip(self.chemin, portee)):
                px, py = ecran_xy(*case)
                couleur = (60, 220, 60) if ok else (220, 60, 60)
                centre = (px + T // 2, py + T // 2)
                if i == len(self.chemin) - 1:
                    pygame.draw.line(self.ecran, couleur, (centre[0] - 9, centre[1] - 9), (centre[0] + 9, centre[1] + 9), 4)
                    pygame.draw.line(self.ecran, couleur, (centre[0] - 9, centre[1] + 9), (centre[0] + 9, centre[1] - 9), 4)
                else:
                    pygame.draw.circle(self.ecran, (0, 0, 0), centre, 6)
                    pygame.draw.circle(self.ecran, couleur, centre, 5)

        # Héros
        hx, hy = ecran_xy(self.heros.x, self.heros.y)
        dessin.dessiner_heros(self.ecran, hx + T // 2, hy + T // 2 + 4, self.temps)

        # Brouillard de guerre
        for ty in range(max(0, y0), min(self.carte.hauteur, y1 + 1)):
            for tx in range(max(0, x0), min(self.carte.largeur, x1 + 1)):
                if not self.carte.explore[ty][tx]:
                    self.ecran.fill(COULEUR_BROUILLARD, (*ecran_xy(tx, ty), T, T))

        # Case survolée
        case = self.case_sous_souris(pygame.mouse.get_pos())
        if case and self.carte.explore[case[1]][case[0]]:
            pygame.draw.rect(self.ecran, (255, 255, 255), (*ecran_xy(*case), T, T), 1)
        self.ecran.set_clip(None)

    def dessiner_barre_ressources(self):
        pygame.draw.rect(self.ecran, COULEUR_FOND_PANNEAU, (0, 0, LARGEUR_VUE, HAUTEUR_BARRE))
        pygame.draw.line(self.ecran, COULEUR_BORD, (0, HAUTEUR_BARRE - 1), (LARGEUR_VUE, HAUTEUR_BARRE - 1), 2)
        x = 16
        for ressource in RESSOURCES:
            dessin.dessiner_icone_ressource(self.ecran, ressource, x + 8, HAUTEUR_BARRE // 2)
            texte = self.police.render(f"{ressource.capitalize()} : {self.joueur.ressources[ressource]}", True, COULEUR_TEXTE)
            self.ecran.blit(texte, (x + 22, HAUTEUR_BARRE // 2 - texte.get_height() // 2))
            x += 44 + texte.get_width()
        date = self.police.render(self.date_texte(), True, COULEUR_TEXTE)
        self.ecran.blit(date, (LARGEUR_VUE - date.get_width() - 16, HAUTEUR_BARRE // 2 - date.get_height() // 2))

    def dessiner_panneau(self):
        panneau = pygame.Rect(LARGEUR_VUE, 0, LARGEUR_PANNEAU, HAUTEUR_ECRAN)
        pygame.draw.rect(self.ecran, COULEUR_FOND_PANNEAU, panneau)
        pygame.draw.line(self.ecran, COULEUR_BORD, (LARGEUR_VUE, 0), (LARGEUR_VUE, HAUTEUR_ECRAN), 3)

        # Mini-carte
        e = self.echelle_mini
        for ty in range(self.carte.hauteur):
            for tx in range(self.carte.largeur):
                if self.carte.explore[ty][tx]:
                    couleur = TERRAINS[self.carte.terrain_en(tx, ty)]["couleur"]
                    objet = self.carte.objet_en(tx, ty)
                    if isinstance(objet, Mine):
                        couleur = COULEUR_JOUEUR if objet.proprietaire else (230, 230, 230)
                else:
                    couleur = COULEUR_BROUILLARD
                self.ecran.fill(couleur, (self.rect_mini.x + tx * e, self.rect_mini.y + ty * e, e, e))
        pygame.draw.rect(self.ecran, (255, 255, 0),
                         (self.rect_mini.x + self.heros.x * e - 1, self.rect_mini.y + self.heros.y * e - 1, e + 2, e + 2))
        vue = pygame.Rect(self.rect_mini.x + self.camera[0] / T * e, self.rect_mini.y + self.camera[1] / T * e,
                          LARGEUR_VUE / T * e, HAUTEUR_VUE / T * e)
        pygame.draw.rect(self.ecran, (255, 255, 255), vue, 1)
        pygame.draw.rect(self.ecran, COULEUR_BORD, self.rect_mini.inflate(4, 4), 2)

        y = self.rect_mini.bottom + 16
        x = LARGEUR_VUE + 16
        self.ecran.blit(self.police_titre.render(self.heros.nom, True, COULEUR_TEXTE), (x, y))
        y += 30

        # Points de mouvement
        self.ecran.blit(self.police.render("Mouvement", True, COULEUR_TEXTE_DOUX), (x, y))
        y += 20
        largeur_barre = LARGEUR_PANNEAU - 32
        pygame.draw.rect(self.ecran, (20, 16, 12), (x, y, largeur_barre, 14))
        ratio = self.heros.mouvement / self.heros.mouvement_max
        pygame.draw.rect(self.ecran, (60, 180, 60), (x, y, int(largeur_barre * ratio), 14))
        pygame.draw.rect(self.ecran, COULEUR_BORD, (x, y, largeur_barre, 14), 1)
        texte = self.police_petite.render(f"{self.heros.mouvement} / {self.heros.mouvement_max}", True, COULEUR_TEXTE)
        self.ecran.blit(texte, (x + largeur_barre // 2 - texte.get_width() // 2, y + 1))
        y += 26

        # Mines possédées
        self.ecran.blit(self.police.render(f"Mines : {len(self.joueur.mines)}", True, COULEUR_TEXTE_DOUX), (x, y))
        y += 20
        for mine in self.joueur.mines:
            self.ecran.blit(self.police_petite.render(f"- {mine.nom}", True, COULEUR_TEXTE), (x + 6, y))
            y += 16
        y += 8

        # Case survolée
        case = self.case_sous_souris(pygame.mouse.get_pos())
        if case and self.carte.explore[case[1]][case[0]]:
            terrain = self.carte.terrain_en(*case)
            infos = NOMS_TERRAINS[terrain]
            if TERRAINS[terrain]["passable"]:
                infos += f" (coût {TERRAINS[terrain]['cout']})"
            else:
                infos += " (infranchissable)"
            self.ecran.blit(self.police_petite.render(infos, True, COULEUR_TEXTE_DOUX), (x, y))
            y += 16
            objet = self.carte.objet_en(*case)
            if objet:
                if isinstance(objet, TasDeRessource):
                    nom = f"Tas de {objet.ressource}"
                elif isinstance(objet, Coffre):
                    nom = "Coffre au trésor"
                else:
                    nom = objet.nom + (" (à vous)" if objet.proprietaire else " (libre)")
                self.ecran.blit(self.police_petite.render(nom, True, COULEUR_TEXTE), (x, y))
                y += 16

        # Journal des messages
        y_journal = self.bouton_fin.top - 12
        for texte in reversed(self.messages[-5:]):
            lignes = self.couper_texte(texte, LARGEUR_PANNEAU - 32)
            for ligne in reversed(lignes):
                rendu = self.police_petite.render(ligne, True, COULEUR_TEXTE)
                y_journal -= 16
                if y_journal < y + 8:
                    break
                self.ecran.blit(rendu, (x, y_journal))
            y_journal -= 6

        # Bouton fin du tour
        survol = self.bouton_fin.collidepoint(pygame.mouse.get_pos())
        pygame.draw.rect(self.ecran, (120, 90, 40) if survol else (90, 66, 30), self.bouton_fin, border_radius=6)
        pygame.draw.rect(self.ecran, COULEUR_BORD, self.bouton_fin, 2, border_radius=6)
        texte = self.police_titre.render("Fin du tour", True, COULEUR_TEXTE)
        self.ecran.blit(texte, texte.get_rect(center=self.bouton_fin.center))

    def couper_texte(self, texte, largeur):
        mots, lignes, ligne = texte.split(), [], ""
        for mot in mots:
            essai = (ligne + " " + mot).strip()
            if self.police_petite.size(essai)[0] <= largeur:
                ligne = essai
            else:
                lignes.append(ligne)
                ligne = mot
        if ligne:
            lignes.append(ligne)
        return lignes


if __name__ == "__main__":
    graine = int(sys.argv[1]) if len(sys.argv) > 1 else None
    Jeu(graine).executer()
