"""Une page de choix où l'on clique, pour n'importe quel lot de candidats (choix de l'utilisateur, 2026-11-20).

    python -I tools/art/page_choix.py <sujet> <description.json>

<description.json> : {"titre": "…", "groupes": [{"id": "bouee", "titre": "Bouée", "fond": "eau"|"herbe"|"#rrggbb",
"echelle": 2, "images": ["chemin/1.png", …], "anim": {"colonnes": 4, "lignes": 2, "fps": 8}}, …]} ; chemins depuis la
racine du dépôt. Chaque image est montrée à <echelle> (2 : la taille du jeu) ; avec « anim », chaque image est une
feuille d'animation jouée en boucle (une courte pause à la fin) ; avec « "plusieurs": true », on garde autant de
candidats qu'on veut (« choix » est alors une liste). Sur la page : un clic choisit un candidat (et vaut « Garder »), « À refaire »,
un commentaire. Les réponses restent dans le navigateur et partent sur le disque si `revue.py serveur` tourne :
art-candidats/en-cours/revue-<sujet>.json ({"items": {"<id>": {"choix": "<chemin>", "avis": …, "commentaire": …}}}).
Écrit art-candidats/en-cours/<sujet>.html. Seule la bibliothèque standard de Python est utilisée.
"""
import html
import json
import os
import sys

RACINE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EN_COURS = os.path.join(RACINE, "art-candidats", "en-cours")
FONDS = {"eau": "#2d6488", "herbe": "#5a7a3e"}


def taille(chemin):
    with open(chemin, "rb") as f:
        tete = f.read(24)
    return int.from_bytes(tete[16:20], "big"), int.from_bytes(tete[20:24], "big")


def page(sujet, desc):
    sections = []
    for g in desc["groupes"]:
        fond = FONDS.get(g.get("fond", "eau"), g.get("fond", "#2d6488"))
        k = g.get("echelle", 2)
        boutons = []
        anim = g.get("anim")
        for chemin in g["images"]:
            w, h = taille(os.path.join(RACINE, chemin))
            rel = os.path.relpath(os.path.join(RACINE, chemin), EN_COURS).replace("\\", "/")
            nom = os.path.splitext(os.path.relpath(chemin, os.path.dirname(os.path.dirname(chemin))))[0].replace("\\", "/")
            if anim:
                fw, fh = w // anim["colonnes"], h // anim["lignes"]
                dessin = (f'<canvas width="{fw * k}" height="{fh * k}" data-src="{html.escape(rel)}" data-fw="{fw}" data-fh="{fh}" '
                          f'data-cols="{anim["colonnes"]}" data-n="{anim["colonnes"] * anim["lignes"]}" data-fps="{anim.get("fps", 8)}"></canvas>')
            else:
                dessin = f'<img src="{html.escape(rel)}" style="width:{w * k}px" alt="">'
            boutons.append(f'<button class="cand" data-c="{html.escape(chemin)}" style="background:{fond}">'
                           f'{dessin}<span>{html.escape(nom)}</span></button>')
        multi = ' data-multi="1"' if g.get("plusieurs") else ""
        sections.append(f"""<section data-o="{html.escape(g['id'])}"{multi}><div class="tete"><h2>{html.escape(g['titre'])}</h2>
<span class="etat"></span><button class="avis" data-a="refaire">À refaire</button></div>
<div class="rang">{''.join(boutons)}</div><textarea placeholder="Commentaire (facultatif)"></textarea></section>""")
    return f"""<!doctype html><html lang="fr"><head><meta charset="utf-8"><title>{html.escape(desc['titre'])}</title>
<meta name="viewport" content="width=device-width,initial-scale=1">
<style>
:root{{--fond:#1d1f22;--texte:#ddd;--vert:#6c6;--rouge:#e66;--choix:#ffd75e}}
body{{background:var(--fond);color:var(--texte);font-family:sans-serif;margin:16px}}
h1{{font-size:22px;margin:0 0 4px}} h2{{font-size:18px;margin:0}}
section{{margin:22px 0;padding:12px;border:1px solid #333;border-radius:6px}}
section.garder{{border-color:var(--vert)}} section.refaire{{border-color:var(--rouge)}}
.tete{{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:10px}}
.etat{{font-size:13px;opacity:.85;margin-right:auto}}
.rang{{display:flex;flex-wrap:wrap;gap:8px}}
.cand{{border:3px solid transparent;padding:8px 8px 2px;display:flex;flex-direction:column;align-items:center;
 justify-content:flex-end;min-width:72px;min-height:72px;cursor:pointer;color:#fff;font-size:11px}}
.cand:hover{{border-color:#888}} .cand.choisi{{border-color:var(--choix);box-shadow:0 0 8px var(--choix)}}
img,canvas{{image-rendering:pixelated;display:block}}
.avis{{padding:5px 12px;cursor:pointer;background:#333;color:var(--texte);border:1px solid #555;border-radius:4px}}
.avis.actif{{background:#6a2a2a;border-color:var(--rouge)}}
textarea{{width:100%;box-sizing:border-box;margin-top:8px;min-height:38px;background:#2a2c30;color:var(--texte);border:1px solid #444}}
#barre{{position:sticky;top:0;background:var(--fond);padding:8px 0;border-bottom:1px solid #333;z-index:1}}
</style></head><body>
<div id="barre"><h1>{html.escape(desc['titre'])}</h1>
Clique sur un candidat pour le choisir, ou « À refaire » ; un commentaire si tu veux. Tout s'enregistre seul.
<b id="disque"></b> <span id="compte"></span></div>
<p>{html.escape(desc.get('texte', "Chaque image à sa taille dans le jeu (un pixel d'image = 2 pixels de l'écran)."))}</p>
{''.join(sections)}
<script>
const CLE={json.dumps('choix-' + sujet + '-' + os.path.basename(RACINE))}, PAGE={json.dumps('revue-' + sujet)}, DOSSIER={json.dumps(EN_COURS)};
let etat={{}};try{{etat=JSON.parse(localStorage.getItem(CLE))||{{}}}}catch(e){{}}
function it(o){{return etat[o]||(etat[o]={{choix:null,avis:'',commentaire:''}})}}
function rendre(){{let n=0;document.querySelectorAll('section').forEach(s=>{{const o=s.dataset.o,v=it(o);
 s.querySelectorAll('.cand').forEach(b=>b.classList.toggle('choisi',v.avis==='garder'&&(Array.isArray(v.choix)?v.choix.includes(b.dataset.c):b.dataset.c===v.choix)));
 s.querySelector('.avis').classList.toggle('actif',v.avis==='refaire');
 s.classList.toggle('garder',v.avis==='garder');s.classList.toggle('refaire',v.avis==='refaire');
 s.querySelector('.etat').textContent=v.avis==='refaire'?'à refaire':v.avis==='garder'?(Array.isArray(v.choix)?v.choix.length+' gardés':'choisi : '+v.choix.split('/').slice(-2).join('/')):'rien de choisi';
 if(v.avis)n++;}});document.getElementById('compte').textContent=n+' / '+document.querySelectorAll('section').length+' répondus';}}
function sauver(){{try{{localStorage.setItem(CLE,JSON.stringify(etat))}}catch(e){{}}rendre();clearTimeout(sauver.t);sauver.t=setTimeout(envoyer,300)}}
async function envoyer(){{const d=document.getElementById('disque');try{{const r=await fetch('http://127.0.0.1:8765/reponses',{{method:'POST',
 headers:{{'Content-Type':'text/plain'}},body:JSON.stringify({{page:PAGE,dossier:DOSSIER,items:etat}},null,1)}});if(!r.ok)throw 0;
 d.textContent='✓ enregistré';d.style.color='var(--vert)'}}catch(e){{d.textContent='non enregistré (serveur arrêté ?)';d.style.color='var(--rouge)'}}}}
document.querySelectorAll('.cand').forEach(b=>b.onclick=()=>{{const s=b.closest('section'),v=it(s.dataset.o);
 if(s.dataset.multi){{const l=Array.isArray(v.choix)?v.choix:[];const i=l.indexOf(b.dataset.c);if(i>=0)l.splice(i,1);else l.push(b.dataset.c);
  v.choix=l;v.avis=l.length?'garder':''}}
 else if(v.choix===b.dataset.c&&v.avis==='garder'){{v.avis='';v.choix=null}}else{{v.choix=b.dataset.c;v.avis='garder'}}sauver()}});
document.querySelectorAll('.avis').forEach(b=>b.onclick=()=>{{const v=it(b.closest('section').dataset.o);v.avis=v.avis==='refaire'?'':'refaire';sauver()}});
document.querySelectorAll('section textarea').forEach(t=>{{t.value=it(t.closest('section').dataset.o).commentaire;
 t.oninput=()=>{{it(t.closest('section').dataset.o).commentaire=t.value;sauver()}}}});
// les feuilles d'animation, jouées en boucle avec une pause d'une demi-seconde à la fin
document.querySelectorAll('canvas[data-src]').forEach(c=>{{const im=new Image();im.src=c.dataset.src;const x=c.getContext('2d');
 x.imageSmoothingEnabled=false;const d=c.dataset,n=+d.n,fps=+d.fps,fw=+d.fw,fh=+d.fh,cols=+d.cols;
 const t0=performance.now();function f(t){{const k=Math.floor((t-t0)/1000*fps)%(n+Math.round(fps/2));const i=Math.min(k,n-1);
  x.clearRect(0,0,c.width,c.height);if(im.complete)x.drawImage(im,(i%cols)*fw,Math.floor(i/cols)*fh,fw,fh,0,0,c.width,c.height);
  requestAnimationFrame(f)}}requestAnimationFrame(f)}});
rendre();envoyer();
</script></body></html>"""


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    sujet = sys.argv[1]
    with open(sys.argv[2], encoding="utf-8") as f:
        desc = json.load(f)
    sortie = os.path.join(EN_COURS, f"{sujet}.html")
    with open(sortie, "w", encoding="utf-8") as f:
        f.write(page(sujet, desc))
    print(sortie)
