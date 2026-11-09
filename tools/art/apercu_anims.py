"""Page d'aperçu des animations candidates d'une créature, à la taille du jeu (×2), sans rien couper : chaque feuille
(art-candidats/en-cours/candidats/<id>/anim/*.png) jouée en boucle dans un cadre assez grand, sur de l'herbe, avec la
créature retournée (le défenseur) à côté. Écrit art-candidats/en-cours/<id>-anims.html. Gratuit.

    python -I tools/art/apercu_anims.py <id> [images/s=8]
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from reduire import lire

ident = sys.argv[1]
fps = float(sys.argv[2]) if len(sys.argv) > 2 else 8
base = f"art-candidats/en-cours/candidats/{ident}/anim"
feuilles = []
for nom in sorted(os.listdir(base)):
    if not nom.endswith(".png") or nom.startswith("depart"):
        continue
    w, h, _ = lire(os.path.join(base, nom))
    dossier = os.path.join(base, nom[:-4].split("-r")[0])
    brutes = len([x for x in os.listdir(dossier) if x[:-4].isdigit()]) - 1 if os.path.isdir(dossier) else 8
    images = brutes if brutes in (8, 16) else 8
    feuilles.append({"nom": nom, "images": images, "chemin": f"candidats/{ident}/anim/{nom}"})

html = """<!doctype html><html lang="fr"><head><meta charset="utf-8"><title>Animations : """ + ident + """</title>
<style>
body{margin:0;background:#1b1a17;color:#e8e2d0;font:14px/1.4 system-ui,sans-serif}
h1{font-size:18px;padding:12px 16px 4px;margin:0} p{padding:0 16px;margin:4px 0;color:#b8ae92}
.liste{display:flex;flex-wrap:wrap;gap:16px;padding:12px 16px}
figure{margin:0;background:#4f7a35;border-radius:4px;padding:8px}
figcaption{font-weight:600;color:#fff;text-shadow:0 1px 2px #000;margin-bottom:4px}
.paire{display:flex;gap:120px;align-items:flex-end}
canvas{image-rendering:pixelated;display:block;outline:1px dashed rgba(255,255,255,.25)}
</style></head><body>
<h1>Animations candidates : """ + ident + """</h1>
<p>À la taille du jeu (×2), sans rien couper (le pointillé : une marge large autour du cadre de l'animation) ; à droite, la même retournée (le
défenseur), écartée pour ne pas la gêner. Vitesse : <span id="v"></span> images/s
(touches + et −).</p>
<div class="liste" id="liste"></div>
<script>
const FEUILLES = """ + json.dumps(feuilles, ensure_ascii=False) + """;
let fps = """ + str(fps) + """;
const v = document.getElementById("v"); v.textContent = fps;
document.addEventListener("keydown", e => { if (e.key === "+") fps = Math.min(30, fps + 1); if (e.key === "-") fps = Math.max(1, fps - 1); v.textContent = fps; });
const toiles = [];
for (const f of FEUILLES) {
  const fig = document.createElement("figure");
  fig.innerHTML = `<figcaption>${f.nom.replace(".png", "")}</figcaption>`;
  const paire = document.createElement("div"); paire.className = "paire";
  const a = document.createElement("canvas"), b = document.createElement("canvas");
  paire.append(a, b); fig.appendChild(paire); document.getElementById("liste").appendChild(fig);
  const img = new Image();
  img.onload = () => {
    const rangs = Math.ceil(f.images / 4), cw = img.width / 4, ch = img.height / rangs;
    // une marge vide autour de l'animation (la moitié de son cadre de chaque côté) : rien ne touche le bord
    const mx = Math.round(cw / 2), my = Math.round(ch / 4);
    for (const c of [a, b]) { c.width = (cw + 2 * mx) * 2; c.height = (ch + 2 * my) * 2; }
    toiles.push({img, n: f.images, cw, ch, mx, my, a, b});
  };
  img.src = f.chemin;
}
let k = 0, dernier = 0;
function boucle(t) {
  if (t - dernier >= 1000 / fps) {
    dernier = t; k++;
    for (const s of toiles) {
      const i = k % s.n, sx = (i % 4) * s.cw, sy = Math.floor(i / 4) * s.ch;
      for (const [c, retourne] of [[s.a, false], [s.b, true]]) {
        const x = c.getContext("2d"); x.imageSmoothingEnabled = false; x.clearRect(0, 0, c.width, c.height);
        x.save(); if (retourne) { x.translate(c.width, 0); x.scale(-1, 1); }
        x.drawImage(s.img, sx, sy, s.cw, s.ch, s.mx * 2, s.my * 2, s.cw * 2, s.ch * 2); x.restore();
      }
    }
  }
  requestAnimationFrame(boucle);
}
requestAnimationFrame(boucle);
</script></body></html>"""
sortie = f"art-candidats/en-cours/{ident}-anims.html"
open(sortie, "w", encoding="utf-8").write(html)
print(sortie)
