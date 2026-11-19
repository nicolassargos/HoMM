"""Les cases qu'un massif de montagnes bloque sur la carte, d'après son image (2026-11-17) ; écrit godot/data/mountains.json.

    python -I tools/art/empreintes.py [seuil]

Comme dans Heroes 3, une montagne est un objet : une image posée sur un rectangle de cases, dont seules certaines sont
bloquées. L'image est posée par le milieu de son bas sur le milieu du bas du rectangle (largeur : la largeur de l'image
arrondie à des cases de 32 pixels) ; une case est bloquée si le dessin couvre au moins <seuil> de sa surface (0,45 par
défaut). Les sommets qui dépassent en haut ne bloquent pas : on passe derrière. « drawn » : toutes les cases que le dessin couvre un
peu (au moins 3 %), alignées en bas sur « cells » : le générateur ne pose pas un massif dont le dessin couvrirait de
l'eau (2026-11-17). « tier », d'après le nom de l'image : massif-cime-* 3 (un grand sommet, au cœur des montagnes
larges), massif-petit-* et massif-contrefort-* 1 (au bord), les autres massifs 2 (des morceaux de chaîne), les sommets seuls 0.
« ground », le sol où les pentes se fondent : massif-sable-* et peak-sable-* le sable, massif-marais-* et peak-marais-*
le marais, les autres l'herbe.
Chaque image godot/art/decor/massif-*.png donne un massif, chaque sommet peak*.png un massif d'une case ; les cases y
sont écrites en lignes (« # » bloquée, « . » libre), à retoucher à la main si besoin (le jeu relit le fichier tel quel ;
relancer l'outil l'écrase).
"""
import glob, json, math, os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire

CASE = 32
SOLS = {'sable': 'Sand', 'marais': 'Swamp'}
RACINE = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'godot')


def empreinte(chemin, seuil, peu=0.03):
    """Les cases bloquées (« # ») et toutes les cases que le dessin couvre un peu (« drawn »), en lignes."""
    w, h, px = lire(chemin)
    cols = max(1, round(w / CASE)); rows = math.ceil(h / CASE)
    ox = (cols * CASE - w) // 2; oy = rows * CASE - h  # l'image dans le rectangle de cases
    lignes, dessin = [], []
    for r in range(rows):
        ligne, vu = '', ''
        for c in range(cols):
            plein = sum(1 for y in range(r * CASE, (r + 1) * CASE) for x in range(c * CASE, (c + 1) * CASE)
                        if 0 <= y - oy < h and 0 <= x - ox < w and px[y - oy][x - ox][3] > 0)
            ligne += '#' if plein >= seuil * CASE * CASE else '.'
            vu += '#' if plein >= peu * CASE * CASE else '.'
        lignes.append(ligne)
        dessin.append(vu)
    while lignes and '#' not in lignes[0]:
        lignes.pop(0)  # les rangées du haut sans case bloquée (seulement des pointes)
    while dessin and '#' not in dessin[0]:
        dessin.pop(0)
    return lignes, dessin


if __name__ == '__main__':
    seuil = float(sys.argv[1]) if len(sys.argv) > 1 else 0.45
    massifs = []
    for chemin in sorted(glob.glob(os.path.join(RACINE, 'art', 'decor', 'massif-*.png'))):
        nom = os.path.basename(chemin)[:-4]
        lignes, dessin = empreinte(chemin, seuil)
        mots = nom[len('massif-'):].split('-')
        sol = SOLS.get(mots[0], 'Grass')
        sorte = mots[1] if mots[0] in SOLS else mots[0]
        tier = 3 if sorte == 'cime' else 1 if sorte in ('petit', 'contrefort') else 2
        massifs.append({'id': nom[len('massif-'):], 'image': 'decor/' + nom, 'cells': lignes, 'drawn': dessin, 'tier': tier,
                        'ground': sol})
        print(nom, len(lignes[0]), '×', len(lignes)); print('\n'.join('  ' + l for l in lignes))
    # les sommets seuls, d'une case : ils comblent ce que les massifs ne couvrent pas
    sommets = [f for f in glob.glob(os.path.join(RACINE, 'art', 'decor', 'peak*.png'))
               if re.fullmatch(r'peak(-(sable|marais))?(-[0-9]+)?', os.path.basename(f)[:-4])]
    for chemin in sorted(sommets):
        nom = os.path.basename(chemin)[:-4]
        sol = next((SOLS[m] for m in SOLS if '-' + m in nom), 'Grass')
        massifs.append({'id': 'sommet' + nom[len('peak'):], 'image': 'decor/' + nom, 'cells': ['#'], 'drawn': ['#'], 'tier': 0,
                        'ground': sol})
    with open(os.path.join(RACINE, 'data', 'mountains.json'), 'w', encoding='utf-8') as f:
        f.write('{\n  "massifs": [\n' + ',\n'.join('    ' + json.dumps(m, ensure_ascii=False) for m in massifs) + '\n  ]\n}\n')
