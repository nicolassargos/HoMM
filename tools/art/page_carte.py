"""Page de choix des petites images de la carte (32 px) d'une créature : les 16 premiers candidats numérotés (p1-5…), sur l'herbe
de la carte à la taille du jeu (×2) et agrandis (×4). python -I tools/art/page_carte.py <id> [nombre=16]   (racine du dépôt)
"""
import os, sys

ident = sys.argv[1]
nombre = int(sys.argv[2]) if len(sys.argv) > 2 else 16  # l'utilisateur : 16 candidats suffisent
base = f"art-candidats/en-cours/candidats/{ident}/carte"
cases = []
for lot in sorted(os.listdir(base)):
    d = os.path.join(base, lot)
    if not os.path.isdir(d) or len(cases) >= nombre:
        continue
    for k in sorted((int(f[:-4]) for f in os.listdir(d) if f[:-4].isdigit()), key=int)[:nombre - len(cases)]:
        chemin = f"candidats/{ident}/carte/{lot}/{k}.png"
        cases.append(f"<figure><div class='herbe'><img class='x2' src='{chemin}'></div><img class='x4' src='{chemin}'>"
                     f"<figcaption>{lot}-{k}</figcaption></figure>")
html = f"""<!doctype html><html lang="fr"><head><meta charset="utf-8"><title>Carte : {ident}</title>
<style>
body{{margin:0;background:#1b1a17;color:#e8e2d0;font:13px system-ui,sans-serif}}
h1{{font-size:18px;padding:12px 16px 4px;margin:0}} p{{padding:0 16px;color:#b8ae92}}
.grille{{display:flex;flex-wrap:wrap;gap:10px;padding:12px 16px}}
figure{{margin:0;background:#2a2720;padding:6px;border-radius:4px;display:flex;flex-direction:column;align-items:center;gap:4px}}
.herbe{{width:96px;height:96px;background:url(../../godot/art/terrain/grass.png);background-size:64px 64px;display:flex;align-items:center;justify-content:center}}
img{{image-rendering:pixelated}} .x2{{width:64px;height:64px}} .x4{{width:128px;height:128px;background:#55703f}}
figcaption{{font-weight:600}}
</style></head><body><h1>Petite image de la carte : {ident}</h1>
<p>En haut : à la taille du jeu (×2) sur l'herbe de la carte ; en bas : agrandie ×4. Donner le numéro (ex. p1-5).</p>
<div class="grille">{''.join(cases)}</div></body></html>"""
sortie = f"art-candidats/en-cours/{ident}-carte.html"
open(sortie, "w", encoding="utf-8").write(html)
print(sortie, len(cases), "candidats")

