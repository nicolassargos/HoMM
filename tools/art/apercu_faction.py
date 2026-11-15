"""Page de vue d'ensemble, au début ou à la fin d'une créature : toutes les créatures de sa faction, puis toutes celles
de son niveau (toutes factions), chacune avec son image fixe et ses animations installées (repos, marche, attaque,
transformation) jouées en boucle, à la taille du jeu (×2), les pieds sur la même ligne, sur de l'herbe : pour juger le
style d'ensemble et voir ce qui manque. Les vitesses et nombres d'images viennent de animations.json, comme dans le jeu.
Écrit art-candidats/en-cours/ensemble-<id>.html. Gratuit.

    python -I tools/art/apercu_faction.py <id>
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire

BATAILLE = "godot/art/creatures/battle"
ETATS = ["idle", "move", "attack", "transform"]
NOMS = {"fixe": "fixe", "idle": "repos", "move": "marche", "attack": "attaque", "transform": "transformation"}
FPS, IMAGES = 10, 8  # comme BattleFieldView (AnimationFps, AnimationFrames)

ident = sys.argv[1]
creatures = json.load(open("godot/data/creatures.json", encoding="utf-8"))["creatures"]
reglages = json.load(open(os.path.join(BATAILLE, "animations.json"), encoding="utf-8"))
moi = next((c for c in creatures if c["id"] == ident), None)
if moi is None:
    sys.exit(f"Créature inconnue : {ident}")


def fiche(c):
    r = reglages.get(c["id"], {})
    vues = []
    fixe = os.path.join(BATAILLE, c["id"] + ".png")
    if os.path.exists(fixe):
        vues.append({"etat": "fixe", "chemin": f"../../{BATAILLE}/{c['id']}.png", "images": 1, "fps": 1})
    for e in ETATS:
        chemin = os.path.join(BATAILLE, f"{c['id']}-{e}.png")
        if os.path.exists(chemin):
            vues.append({"etat": e, "chemin": f"../../{BATAILLE}/{c['id']}-{e}.png",
                         "images": int(r.get(e + "Frames", IMAGES)), "fps": float(r.get(e, FPS))})
    manque = [NOMS[e] for e in ["idle", "move", "attack"] if not any(v["etat"] == e for v in vues)]
    if not os.path.exists(fixe):
        manque.insert(0, "image fixe")
    return {"id": c["id"], "nom": c["name"], "faction": c["faction"], "niveau": c["tier"], "vues": vues, "manque": manque}


faction = [fiche(c) for c in sorted((c for c in creatures if c["faction"] == moi["faction"]), key=lambda c: c["tier"])]
niveau = [fiche(c) for c in creatures if c["tier"] == moi["tier"]]
sections = [{"titre": f"Faction : {moi['faction']}", "liste": faction},
            {"titre": f"Niveau {moi['tier']} : toutes les factions", "liste": niveau}]

html = """<!doctype html><html lang="fr"><head><meta charset="utf-8"><title>Ensemble : """ + moi["name"] + """</title>
<style>
body{margin:0;background:#1b1a17;color:#e8e2d0;font:14px/1.4 system-ui,sans-serif}
h1{font-size:18px;padding:12px 16px 4px;margin:0} h2{font-size:16px;padding:16px 16px 0;margin:0;color:#f0d68a}
p{padding:0 16px;margin:4px 0;color:#b8ae92}
.ligne{display:flex;align-items:flex-end;gap:6px;margin:8px 16px;padding:8px;background:#4f7a35;border-radius:4px;
  overflow-x:auto}
.ligne.moi{outline:3px solid #f0d68a}
.titre{width:150px;flex:none;align-self:center;color:#fff;text-shadow:0 1px 2px #000}
.titre b{display:block} .titre small{color:#e0e8c8} .manque{color:#ffb0a0;font-size:12px}
figure{margin:0;flex:none} figcaption{font-size:11px;color:#e8f0d8;text-align:center;text-shadow:0 1px 2px #000}
canvas{image-rendering:pixelated;display:block}
</style></head><body>
<h1>Vue d'ensemble : """ + moi["name"] + """</h1>
<p>À la taille du jeu (×2), les pieds sur la même ligne, aux vitesses du jeu. Encadrée : la créature en cours. En rouge :
ce qui manque encore.</p>
<div id="tout"></div>
<script>
const SECTIONS = """ + json.dumps(sections, ensure_ascii=False) + """;
const MOI = """ + json.dumps(ident) + """;
const jeux = [];
for (const s of SECTIONS) {
  const h = document.createElement("h2"); h.textContent = s.titre; document.getElementById("tout").appendChild(h);
  for (const c of s.liste) {
    const l = document.createElement("div"); l.className = "ligne" + (c.id === MOI ? " moi" : "");
    l.innerHTML = `<div class="titre"><b>${c.nom}</b><small>${c.faction}, niveau ${c.niveau}</small>` +
      (c.manque.length ? `<div class="manque">manque : ${c.manque.join(", ")}</div>` : "") + "</div>";
    document.getElementById("tout").appendChild(l);
    for (const v of c.vues) {
      const f = document.createElement("figure"), t = document.createElement("canvas");
      f.appendChild(t); f.insertAdjacentHTML("beforeend", `<figcaption>${v.etat}</figcaption>`); l.appendChild(f);
      const img = new Image();
      img.onload = () => {
        const cols = v.images > 1 ? 4 : 1, rangs = Math.ceil(v.images / cols);
        const cw = img.width / cols, ch = img.height / rangs;
        t.width = cw * 2; t.height = ch * 2;
        jeux.push({img, t, cw, ch, cols, n: v.images, fps: v.fps, i: 0, dernier: 0});
        dessiner(jeux[jeux.length - 1]);
      };
      img.src = v.chemin;
    }
  }
}
function dessiner(j) {
  const x = j.t.getContext("2d"); x.imageSmoothingEnabled = false; x.clearRect(0, 0, j.t.width, j.t.height);
  x.drawImage(j.img, (j.i % j.cols) * j.cw, Math.floor(j.i / j.cols) * j.ch, j.cw, j.ch, 0, 0, j.cw * 2, j.ch * 2);
}
function boucle(t) {
  for (const j of jeux) {
    if (j.n > 1 && t - j.dernier >= 1000 / j.fps) { j.dernier = t; j.i = (j.i + 1) % j.n; dessiner(j); }
  }
  requestAnimationFrame(boucle);
}
requestAnimationFrame(boucle);
</script></body></html>"""
sortie = f"art-candidats/en-cours/ensemble-{ident}.html"
open(sortie, "w", encoding="utf-8").write(html)
print(sortie)
