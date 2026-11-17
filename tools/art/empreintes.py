"""Les cases qu'un massif de montagnes bloque sur la carte, d'après son image (2026-11-17) ; écrit godot/data/mountains.json.

    python -I tools/art/empreintes.py [seuil]

Comme dans Heroes 3, une montagne est un objet : une image posée sur un rectangle de cases, dont seules certaines sont
bloquées. L'image est posée par le milieu de son bas sur le milieu du bas du rectangle (largeur : la largeur de l'image
arrondie à des cases de 32 pixels) ; une case est bloquée si le dessin couvre au moins <seuil> de sa surface (0,45 par
défaut). Les sommets qui dépassent en haut ne bloquent pas : on passe derrière.
Chaque image godot/art/decor/massif-*.png donne un massif, chaque sommet peak*.png un massif d'une case ; les cases y
sont écrites en lignes (« # » bloquée, « . » libre), à retoucher à la main si besoin (le jeu relit le fichier tel quel ;
relancer l'outil l'écrase).
"""
import glob, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire

CASE = 32
RACINE = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'godot')


def empreinte(chemin, seuil):
    w, h, px = lire(chemin)
    cols = max(1, round(w / CASE)); rows = math.ceil(h / CASE)
    ox = (cols * CASE - w) // 2; oy = rows * CASE - h  # l'image dans le rectangle de cases
    lignes = []
    for r in range(rows):
        ligne = ''
        for c in range(cols):
            plein = sum(1 for y in range(r * CASE, (r + 1) * CASE) for x in range(c * CASE, (c + 1) * CASE)
                        if 0 <= y - oy < h and 0 <= x - ox < w and px[y - oy][x - ox][3] > 0)
            ligne += '#' if plein >= seuil * CASE * CASE else '.'
        lignes.append(ligne)
    while lignes and '#' not in lignes[0]:
        lignes.pop(0)  # les rangées du haut sans case bloquée (seulement des pointes)
    return lignes


if __name__ == '__main__':
    seuil = float(sys.argv[1]) if len(sys.argv) > 1 else 0.45
    massifs = []
    for chemin in sorted(glob.glob(os.path.join(RACINE, 'art', 'decor', 'massif-*.png'))):
        nom = os.path.basename(chemin)[:-4]
        lignes = empreinte(chemin, seuil)
        massifs.append({'id': nom[len('massif-'):], 'image': 'decor/' + nom, 'cells': lignes})
        print(nom, len(lignes[0]), '×', len(lignes)); print('\n'.join('  ' + l for l in lignes))
    # les sommets seuls, d'une case : ils comblent ce que les massifs ne couvrent pas
    for chemin in sorted(glob.glob(os.path.join(RACINE, 'art', 'decor', 'peak.png')) + glob.glob(os.path.join(RACINE, 'art', 'decor', 'peak-[0-9].png'))):
        nom = os.path.basename(chemin)[:-4]
        massifs.append({'id': 'sommet' + nom[len('peak'):], 'image': 'decor/' + nom, 'cells': ['#']})
    with open(os.path.join(RACINE, 'data', 'mountains.json'), 'w', encoding='utf-8') as f:
        f.write('{\n  "massifs": [\n' + ',\n'.join('    ' + json.dumps(m, ensure_ascii=False) for m in massifs) + '\n  ]\n}\n')
