"""Les héros sur le côté du champ de bataille (godot/art/heroes/battle/<id>.png) : à cheval sur la monture de l'image de
la carte de leur faction, tournés vers la droite, d'après leur portrait. Chaque héros a sa description dans
tools/art/heros-combat.json (et son choix une fois installé).

À lancer depuis la racine du dépôt. La clé PixelLab est lue dans PIXELLAB_API_KEY (jamais affichée).

    python -I tools/art/heros_combat.py essayer <id>… [lot=l1]
        un lot « Pro » de 4 images (160 × 144, environ 0,12 $) par héros, dans
        art-candidats/en-cours/candidats/heros-combat/<id>/<lot>/ ; modèles : le portrait (le visage), le héros de sa
        faction sur la carte (tenue, monture), le Cavalier du combat (la vue de côté) ; style : le Cavalier.
        Coûte de l'argent.
    python -I tools/art/heros_combat.py page <lot> <id>…
        la page de choix art-candidats/en-cours/heros-combat-<lot>.html (le portrait, puis les essais à ×2 sur l'herbe) ;
        réponses dans revue-heros-combat-<lot>.json.
    python -I tools/art/heros_combat.py choisir <revue.json | id=l1-3…>
        installe les images choisies : recadrées au dessin, et les rouges hors de l'étendard passés au brun (le jeu peint
        les rouges vifs de la couleur du joueur : MapRenderer.PaintPlayerColor).

Voir en jeu : godot avec « -- --battle-test --faction=<faction> » (le héros de départ de la faction).
"""
import colorsys
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire, ecrire
import nettoyage as n
import pixellab
from creature import taches

FICHES = "tools/art/heros-combat.json"
SORTIE = "godot/art/heroes/battle"
EN_COURS = "art-candidats/en-cours"
BASE = EN_COURS + "/candidats/heros-combat"
STYLE = "godot/art/creatures/battle/cavalier.png"
DEBUT = "classic 1990s fantasy strategy game sprite of a hero on horseback for the side of the battlefield: "
# « no red anywhere except the banner » : le jeu peint les rouges de la couleur du joueur
FIN = (", holding a long pole with a red banner flying backward; no red anywhere except the banner. Pure side view, "
       "facing right, the mount in full profile with all four legs standing on the ground, slightly from above, full body, "
       "simple bold shapes, clear readable silhouette, a caricature in the style of a fantasy board game miniature, "
       "full of character; no other character, no visual effects")


def charger():
    return json.load(open(FICHES, encoding="utf-8"))


def enregistrer(fiches):
    with open(FICHES, "w", encoding="utf-8", newline="\n") as f:
        f.write("{\n" + ",\n".join(f" {json.dumps(k)}: " + json.dumps(v, ensure_ascii=False) for k, v in fiches.items()) + "\n}\n")


def heros():
    h = json.load(open("godot/data/heroes.json", encoding="utf-8"))
    return {x["id"]: x for x in (h["heroes"] if isinstance(h, dict) else h)}


def cmd_essayer(ids, lot="l1"):
    fiches, tous = charger(), heros()
    manque = [i for i in ids if i not in fiches or i not in tous]
    if manque:
        sys.exit(f"Sans description dans {FICHES} (ou inconnus) : {manque}")

    def essai(i):
        modeles = [f"godot/art/heroes/portraits/{i}.png::this hero's face, hair, beard and clothes colours only; not the framing, not the background",
                   f"godot/art/heroes/hero-{tous[i]['faction']}-e.png::the outfit and the mount of this faction's hero on the map; not the size or the style",
                   STYLE + "::camera angle and side view of a rider on horseback only: do not copy this rider, his horse or colours"]
        return lambda: pixellab.pro(os.path.join(BASE, i, lot), 160, 144, DEBUT + fiches[i]["description"] + FIN, 1, STYLE, modeles)
    pixellab.solde()
    erreurs = taches([essai(i) for i in ids])
    for e in erreurs:
        print("ERREUR", e)
    pixellab.solde()


def rouge(c):
    """Les pixels que le jeu peint de la couleur du joueur (comme MapRenderer.PaintPlayerColor)."""
    if c[3] <= 127:
        return False
    h, s, _ = colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
    return s > 0.45 and (h < 0.05 or h > 0.93)


def brunir(c):
    _, s, v = colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
    r, g, b = colorsys.hsv_to_rgb(0.06, s, v)
    return (round(r * 255), round(g * 255), round(b * 255), c[3])


def garder_etendard(q):
    """Les taches rouges hors de l'étendard passent au brun. L'étendard : la tache qui commence le plus haut (au bout de
    la hampe) et celles qui commencent presque aussi haut (coupé par la hampe) ; cheveux roux, cuir, feu sont plus bas."""
    h, w = len(q), len(q[0])
    vu, taches_rouges = set(), []
    for y0 in range(h):
        for x0 in range(w):
            if (x0, y0) in vu or not rouge(q[y0][x0]):
                continue
            tache, pile = [], [(x0, y0)]
            vu.add((x0, y0))
            while pile:
                x, y = pile.pop()
                tache.append((x, y))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        p = (x + dx, y + dy)
                        if 0 <= p[0] < w and 0 <= p[1] < h and p not in vu and rouge(q[p[1]][p[0]]):
                            vu.add(p)
                            pile.append(p)
            taches_rouges.append(tache)
    haut = min((min(y for _, y in t) for t in taches_rouges), default=0)
    brunis = 0
    for tache in taches_rouges:
        if min(y for _, y in tache) > haut + 6:
            for x, y in tache:
                q[y][x] = brunir(q[y][x])
            brunis += len(tache)
    return brunis


def cmd_choisir(args):
    if len(args) == 1 and args[0].endswith(".json"):
        revue = json.load(open(os.path.expanduser(args[0]), encoding="utf-8"))
        choix = {i: v["choix"] for i, v in revue.items() if v.get("choix", "").startswith("l")}
    else:
        choix = dict(a.split("=", 1) for a in args)
    fiches = charger()
    os.makedirs(SORTIE, exist_ok=True)
    for i, nom in choix.items():
        lot, _, k = nom.partition("-")
        q = n.recadrer(lire(os.path.join(BASE, i, lot, f"{k}.png"))[2])
        brunis = garder_etendard(q)
        ecrire(os.path.join(SORTIE, f"{i}.png"), q)
        fiches.setdefault(i, {})["choix"] = nom
        print(f"{SORTIE}/{i}.png installée ({len(q[0])} × {len(q)}, {brunis} rouges hors de l'étendard passés au brun)")
    enregistrer(fiches)


PAGE = """<!doctype html><meta charset=utf-8><title>Héros au combat</title>
<style>
body{background:#222;color:#eee;font-family:sans-serif;margin:12px}
.c{background:#333;padding:8px;border-radius:4px;margin:0 0 12px}.t{font-weight:bold;margin:2px 0 6px}.t small{font-weight:normal;color:#bbb}
.r{display:flex;flex-wrap:wrap;gap:8px;align-items:flex-end}.p{image-rendering:pixelated;width:128px;height:128px;margin-right:10px}
.e{cursor:pointer;outline:3px solid transparent}.e.pris{outline-color:#6c6}
.e img{image-rendering:pixelated;width:320px;height:288px;display:block;background:url(../../godot/art/terrain/grass.png);background-size:48px 48px}
.l{font-size:14px;padding:4px 2px}input[type=text]{width:min(620px,100%);margin-top:6px}button{font-size:16px;padding:6px 14px;margin:10px 0}
.ref{margin-left:14px;font-size:14px}.c.refaire{box-shadow:inset 0 0 0 2px #e66}#n{margin-left:10px;color:#bbb}
</style>
<h1>Héros au combat</h1>
<p>Chaque héros à cheval sur la monture de son image de la carte, d'après son portrait, à la taille du jeu (×2). L'étendard
rouge prend la couleur du joueur. Cliquez sur l'essai à garder, ou cochez <b>refaire</b> et laissez un mot. Puis
<b>Télécharger les réponses</b> (__REVUE__).</p>
<input type=text id=general placeholder="remarque générale (facultatif)" style="width:min(900px,100%)">
<br><button onclick=telecharger()>Télécharger les réponses</button><span id=n></span>
<div id=g></div>
<button onclick=telecharger()>Télécharger les réponses</button>
<script>
const ITEMS = __ITEMS__, g = document.getElementById('g'), rep = {};
function compte() { document.getElementById('n').textContent = Object.keys(rep).filter(k => rep[k].choix).length + ' / ' + ITEMS.length; }
for (const it of ITEMS) {
  const c = document.createElement('div'); c.className = 'c';
  c.innerHTML = `<div class=t>${it.titre} <small>(${it.faction})</small></div><div class=r><img class=p src="${it.portrait}"></div>
    <label class=ref><input type=checkbox> refaire</label><br><input type=text placeholder="un mot (facultatif)">`;
  g.appendChild(c);
  const r = c.querySelector('.r'), box = c.querySelector('input[type=checkbox]'), txt = c.querySelector('input[type=text]'), a = rep[it.id] = {};
  const maj = () => { c.classList.toggle('refaire', a.choix === 'refaire'); c.querySelectorAll('.e').forEach(e => e.classList.toggle('pris', e.dataset.nom === a.choix)); box.checked = a.choix === 'refaire'; compte(); };
  for (const e of it.essais) {
    const d = document.createElement('div'); d.className = 'e'; d.dataset.nom = e.nom;
    d.innerHTML = `<img src="${e.src}"><div class=l>${e.titre}</div>`;
    d.onclick = () => { a.choix = e.nom; maj(); };
    r.appendChild(d);
  }
  box.onchange = () => { a.choix = box.checked ? 'refaire' : undefined; if (!box.checked) delete a.choix; maj(); };
  txt.oninput = () => { a.mot = txt.value; };
}
compte();
function telecharger() {
  const general = document.getElementById('general').value;
  const sortie = general ? { ...rep, '_general': { mot: general } } : rep;
  const l = document.createElement('a');
  l.href = URL.createObjectURL(new Blob([JSON.stringify(sortie, null, 1)], { type: 'application/json' }));
  l.download = '__REVUE__'; l.click();
}
</script>
"""


def cmd_page(lot, ids):
    tous = heros()
    items = []
    for i in ids:
        d = os.path.join(BASE, i, lot)
        essais = [{"nom": f"{lot}-{k}", "titre": f"Essai {k}", "src": f"candidats/heros-combat/{i}/{lot}/{k}.png"}
                  for k in range(1, 9) if os.path.exists(os.path.join(d, f"{k}.png"))]
        items.append({"id": i, "titre": tous[i]["name"], "faction": tous[i]["faction"],
                      "portrait": f"../../godot/art/heroes/portraits/{i}.png", "essais": essais})
    revue = f"revue-heros-combat-{lot}.json"
    chemin = f"{EN_COURS}/heros-combat-{lot}.html"
    with open(chemin, "w", encoding="utf-8", newline="\n") as f:
        f.write(PAGE.replace("__ITEMS__", json.dumps(items, ensure_ascii=False)).replace("__REVUE__", revue))
    print(chemin, ":", len(items), "héros,", sum(len(i["essais"]) for i in items), "essais")


def main():
    a = sys.argv[1:]
    if not a:
        sys.exit(__doc__)
    if a[0] == "essayer":
        lot = next((x for x in a[1:] if x.startswith("lot=")), "lot=l1")[4:]
        cmd_essayer([x for x in a[1:] if not x.startswith("lot=")], lot)
    elif a[0] == "page" and len(a) > 2:
        cmd_page(a[1], a[2:])
    elif a[0] == "choisir" and len(a) > 1:
        cmd_choisir(a[1:])
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
