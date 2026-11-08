"""Les pieds des images de la carte : où chaque objet touche le sol, pour l'ombre de contact dessinée par le jeu.

    python -I tools/art/pieds.py proposer [--tout]   propose les pieds des images qui n'en ont pas encore (--tout : toutes
                                                     les images non vérifiées) et les écrit dans godot/art/pieds.json
    python -I tools/art/pieds.py page                écrit art-candidats/en-cours/revue-pieds.html (la page de revue)
    python -I tools/art/revue.py serveur             (à lancer à côté) écrit les réponses de la page sur le disque
    python -I tools/art/pieds.py appliquer [fichier] reporte les réponses (en-cours/revue-pieds.json, sinon le dernier
                                                     revue-pieds*.json des Téléchargements) dans godot/art/pieds.json

Un pied est un segment, du pixel le plus à gauche au pixel le plus à droite de la base d'un objet (en pixels de l'image).
Le jeu suit le bas du dessin entre ces deux points, ferme la forme par un arc au-dessus, puis l'entoure de bagues de plus en
plus claires (Game/ShadowLayer.cs ; la page en montre un aperçu calculé de la même façon). Une image peut avoir plusieurs
pieds (une mine : le bâtiment, les arbres, les tas), ou aucun. Son « étalement » est le nombre de bagues autour de la forme
(3 par défaut ; de 1, une ombre serrée, à 6, une ombre large).

Proposition : l'image est découpée en morceaux (pixels opaques qui se touchent) ; deux objets reliés par un passage étroit
sont séparés (on amincit de 3 pixels) ; le chemin étroit qui touche le bas de l'image (vers la case d'entrée), les morceaux
minuscules ou creux (rails) n'ont pas de pied. Pour chaque morceau, on part du pixel le plus bas du tiers du milieu et on
suit le bas du contour à gauche et à droite tant qu'il ne remonte pas de plus de 3 pixels d'un coup (le bord d'un
feuillage) ; un creux de 10 colonnes au plus (une entrée de mine, entre deux jambes) est enjambé.
Seule la bibliothèque standard de Python est utilisée.
"""
import base64
import glob
import json
import os
import struct
import sys
import zlib

RACINE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(RACINE, "godot", "art")
FICHIER = os.path.join(ART, "pieds.json")
EN_COURS = os.path.join(RACINE, "art-candidats", "en-cours")
PAGE = "revue-pieds"
PORT = 8765  # celui de « revue.py serveur »

STEP, BRIDGE = 3, 10      # remontée qui arrête le pied ; creux enjambé
NECK, SEED = 3, 20        # amincissement qui sépare deux objets ; taille d'un bout qui compte
PATH_WIDTH = 8            # largeur d'un chemin vers l'entrée
ETALEMENT = 3             # nombre de bagues autour de la forme, par défaut


def images():
    """Les images posées sur la carte (nom sans .png, relatif à godot/art)."""
    noms = []
    for dossier in ("objects", "decor", "creatures", "heroes"):
        for f in sorted(glob.glob(os.path.join(ART, dossier, "*.png"))):
            noms.append(f"{dossier}/{os.path.basename(f)[:-4]}")
    return noms


def lire(chemin):
    """(largeur, hauteur, opaque[y][x]) d'une image PNG RGBA ou RGB."""
    d = open(chemin, "rb").read(); i = 8; idat = b""
    while i < len(d):
        n, t = struct.unpack(">I4s", d[i:i + 8]); c = d[i + 8:i + 8 + n]; i += 12 + n
        if t == b"IHDR": w, h, _, ct = struct.unpack(">IIBB", c[:10])
        elif t == b"IDAT": idat += c
    bpp = {6: 4, 2: 3}[ct]; raw = zlib.decompress(idat); prev = bytearray(w * bpp); k = 0; opaque = []
    for _ in range(h):
        f = raw[k]; k += 1; line = bytearray(raw[k:k + w * bpp]); k += w * bpp
        for x in range(len(line)):
            a = line[x - bpp] if x >= bpp else 0; b = prev[x]; cc = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - cc; pa, pb, pc = abs(p - a), abs(p - b), abs(p - cc)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else cc)) & 255
        opaque.append([bpp == 3 or line[x * bpp + 3] > 127 for x in range(w)]); prev = line
    return w, h, opaque


def sans_chemin(w, h, opaque):
    """L'image sans le chemin étroit qui part du bas de l'image (vers la case d'entrée d'une mine)."""
    m = [row[:] for row in opaque]
    for x0 in range(w):
        if m[h - 1][x0] and (x0 == 0 or not m[h - 1][x0 - 1]):
            x1 = x0
            while x1 + 1 < w and m[h - 1][x1 + 1]: x1 += 1
            y, lo, hi = h - 1, x0, x1
            while y >= 0:
                row = [x for x in range(max(0, lo - 2), min(w, hi + 3)) if m[y][x]]
                if not row or max(row) - min(row) + 1 > PATH_WIDTH: break
                for x in row: m[y][x] = False
                lo, hi = min(row), max(row); y -= 1
    return m


def morceaux(w, h, m):
    """Les groupes de pixels opaques qui se touchent (par un côté ou un coin)."""
    vu = [[False] * w for _ in range(h)]; res = []
    for sy in range(h):
        for sx in range(w):
            if m[sy][sx] and not vu[sy][sx]:
                pile = [(sx, sy)]; vu[sy][sx] = True; p = []
                while pile:
                    x, y = pile.pop(); p.append((x, y))
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            nx, ny = x + dx, y + dy
                            if 0 <= nx < w and 0 <= ny < h and m[ny][nx] and not vu[ny][nx]:
                                vu[ny][nx] = True; pile.append((nx, ny))
                res.append(p)
    return res


def separer(morceau):
    """Deux objets reliés par un passage étroit : on amincit de NECK pixels ; s'il reste plusieurs gros bouts, chacun reprend
    son bord, puis ce qui est juste en dessous de lui (un tronc revient à son feuillage) ; le reste (des rails) est laissé."""
    cases = set(morceau); coeur = set(cases)
    for _ in range(NECK):
        coeur = {(x, y) for (x, y) in coeur if all((x + dx, y + dy) in coeur for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)))}
    bouts, vu = [], set()
    for c in coeur:
        if c in vu: continue
        comp, pile = [], [c]; vu.add(c)
        while pile:
            x, y = pile.pop(); comp.append((x, y))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    n = (x + dx, y + dy)
                    if n in coeur and n not in vu: vu.add(n); pile.append(n)
        if len(comp) >= SEED: bouts.append(comp)
    if len(bouts) < 2: return [morceau]
    a_qui = {}; front = []
    for i, comp in enumerate(bouts):
        for c in comp: a_qui[c] = i; front.append(c)
    for _ in range(NECK):
        suite = []
        for x, y in front:
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    n = (x + dx, y + dy)
                    if n in cases and n not in a_qui: a_qui[n] = a_qui[(x, y)]; suite.append(n)
        front = suite
    change = True
    while change:
        change = False
        for (x, y), i in list(a_qui.items()):
            n = (x, y + 1)
            if n in cases and n not in a_qui: a_qui[n] = i; change = True
    parts = [[] for _ in bouts]
    for c, i in a_qui.items(): parts[i].append(c)
    return parts


def proposer_pieds(chemin):
    """Les pieds proposés pour une image : [[[xg, yg], [xd, yd]], …]."""
    w, h, opaque = lire(chemin)
    pieds = []
    for morceau in [p for m in morceaux(w, h, sans_chemin(w, h, opaque)) for p in separer(m)]:
        xs_ = [p[0] for p in morceau]; ys_ = [p[1] for p in morceau]
        bw, bh = max(xs_) - min(xs_) + 1, max(ys_) - min(ys_) + 1
        if len(morceau) < 12 or len(morceau) < 0.3 * bw * bh: continue   # minuscule ou creux (rails)
        bas = {}
        for x, y in morceau: bas[x] = max(bas.get(x, -1), y)
        xs = sorted(bas); tiers = (xs[-1] - xs[0] + 1) // 3
        milieu = [x for x in xs if xs[0] + tiers <= x <= xs[-1] - tiers] or xs
        depart = max(milieu, key=lambda x: (bas[x], -abs(x - (xs[0] + xs[-1]) / 2)))
        garde = {depart: bas[depart]}
        for d in (1, -1):
            x = depart
            while True:
                suivant = None
                for k in range(1, BRIDGE + 2):
                    c = x + d * k
                    if c in bas and bas[c] >= bas[x] - STEP:
                        suivant = c; break
                if suivant is None: break
                garde[suivant] = bas[suivant]; x = suivant
        g, dr = min(garde), max(garde)
        # d'après la revue de l'utilisateur (2026-11-08) : un pixel plus à l'intérieur et un plus bas, sur le sol
        if dr - g >= 4:
            g, dr = g + 1, dr - 1
        pieds.append([[g, min(h - 1, garde.get(g, garde[min(garde)]) + 1)], [dr, min(h - 1, garde.get(dr, garde[max(garde)]) + 1)]])
    return sorted(pieds)


def charger():
    if os.path.exists(FICHIER):
        with open(FICHIER, encoding="utf-8") as f:
            return json.load(f)
    return {}


def ecrire(donnees):
    with open(FICHIER, "w", encoding="utf-8", newline="\n") as f:
        f.write("{\n" + ",\n".join(f'  {json.dumps(n)}: {json.dumps(donnees[n], ensure_ascii=False)}' for n in sorted(donnees))
                + "\n}\n")


def proposer(tout):
    donnees = charger()
    for nom in images():
        if nom in donnees and (donnees[nom].get("verifie") or not tout):
            continue
        donnees[nom] = {"pieds": proposer_pieds(os.path.join(ART, nom + ".png")), "verifie": False}
        print(f"{nom} : {len(donnees[nom]['pieds'])} pied(s)", flush=True)
    ecrire(donnees)
    print(f"Écrit {os.path.relpath(FICHIER, RACINE)}")


def page():
    donnees = charger()
    items = []
    for nom in images():
        with open(os.path.join(ART, nom + ".png"), "rb") as f:
            src = "data:image/png;base64," + base64.b64encode(f.read()).decode()
        d = donnees.get(nom, {"pieds": [], "verifie": False})
        items.append({"nom": nom, "src": src, "pieds": d["pieds"], "verifie": d.get("verifie", False),
                      "etalement": d.get("etalement", ETALEMENT)})
    html = PAGE_HTML.replace("%ITEMS%", json.dumps(items)).replace("%PAGE%", PAGE).replace("%PORT%", str(PORT))
    os.makedirs(EN_COURS, exist_ok=True)
    sortie = os.path.join(EN_COURS, PAGE + ".html")
    with open(sortie, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"Écrit {os.path.relpath(sortie, RACINE)} ({len(items)} images)")


def appliquer(fichier):
    if not fichier:
        fichier = os.path.join(EN_COURS, PAGE + ".json")
        if not os.path.exists(fichier):
            dl = os.path.join(os.path.expanduser("~"), "Downloads")
            trouves = sorted(glob.glob(os.path.join(dl, PAGE + "*.json")), key=os.path.getmtime)
            if not trouves: sys.exit(f"Aucune réponse : ni {fichier} ni {PAGE}*.json dans {dl}")
            fichier = trouves[-1]
    with open(fichier, encoding="utf-8") as f:
        reponses = json.load(f)
    donnees = charger(); n = 0
    for nom, it in reponses.get("items", {}).items():
        if nom not in donnees and nom not in images(): continue
        donnees[nom] = {"pieds": it["pieds"], "verifie": bool(it.get("verifie"))}
        if int(it.get("etalement", ETALEMENT)) != ETALEMENT:
            donnees[nom]["etalement"] = int(it["etalement"])
        n += 1
        if it.get("commentaire"): print(f"{nom} : {it['commentaire']}")
    ecrire(donnees)
    print(f"{n} images reportées dans {os.path.relpath(FICHIER, RACINE)} "
          f"({sum(1 for d in donnees.values() if d.get('verifie'))} vérifiées sur {len(donnees)})")


PAGE_HTML = r"""<!doctype html><html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Pieds des images</title>
<style>
:root{--fond:#1b1712;--carte:#2a231b;--texte:#eee;--doux:#a99;--vert:#7c6;--rouge:#e66;--or:#e6c47a}
body{background:var(--fond);color:var(--texte);font-family:sans-serif;margin:0}
header{position:sticky;top:0;background:#120f0b;padding:10px 16px;display:flex;gap:12px;align-items:center;flex-wrap:wrap;z-index:2;border-bottom:1px solid #444}
header h1{font-size:18px;margin:0}button{background:#3a3026;color:var(--texte);border:1px solid #665;border-radius:4px;padding:4px 10px;cursor:pointer}
button.on{background:var(--or);color:#000}main{padding:16px;display:flex;flex-wrap:wrap;gap:16px}
.item{background:var(--carte);border:2px solid #443;border-radius:6px;padding:10px;max-width:100%}.item.ok{border-color:var(--vert)}
.item h2{font-size:14px;margin:0 0 6px;display:flex;justify-content:space-between;gap:8px}canvas{image-rendering:pixelated;cursor:crosshair;display:block;max-width:100%}
.outils{display:flex;gap:6px;flex-wrap:wrap;margin-top:6px}.etat{color:var(--doux);font-weight:normal}textarea{width:100%;box-sizing:border-box;margin-top:6px;background:#1b1712;color:var(--texte);border:1px solid #554}
.aide{color:var(--doux);font-size:13px;padding:8px 16px;max-width:1100px;line-height:1.5}#disque{font-size:13px}
</style></head><body>
<header><h1>Pieds des images</h1><span class="compte"></span><button data-filtre="tout" class="on">Toutes</button><button data-filtre="reste">À voir</button>
<button id="telecharger">Télécharger</button><span id="disque"></span></header>
<p class="aide">Chaque image est posée sur l'herbe avec l'aperçu de son ombre de contact (à pleine force ; le jeu l'adoucit avec le réglage « contact »).
Un <b>pied</b> va du point <b style="color:#f4f">rose</b> (le pixel le plus à gauche de la base) au point <b style="color:#4ef">bleu</b> (le plus à droite).
<b>Glisser</b> un point pour le corriger. <b>« + pied »</b> puis deux clics (gauche, puis droite) pour en ajouter un ; <b>clic droit</b> sur un point pour retirer son pied.
<b>− / +</b> règle l'étalement (le nombre de bagues autour de la forme : 1 serrée, 6 large). <b>« Bonne »</b> quand l'image est juste. Les réponses sont gardées dans le navigateur, et écrites sur le disque si <code>python -I tools/art/revue.py serveur</code> tourne (sinon : Télécharger).</p>
<main></main>
<script>
const ITEMS=%ITEMS%;const CLE='%PAGE%';const ETALEMENT=3;
// les bagues : de 55 % de noir près du pied à presque rien, en « étalement » pas (comme ShadowLayer.ContactShades)
const shades=n=>Array.from({length:n},(_,i)=>0.55*Math.pow((n-i)/n,1.4));const ARC_K=0.12,ARC_MAX=2.5,PATH=8;
let etat={};try{etat=JSON.parse(localStorage.getItem(CLE))||{}}catch(e){}
function it(n){const d=ITEMS.find(i=>i.nom===n);const s=etat[n]||(etat[n]={pieds:JSON.parse(JSON.stringify(d.pieds)),verifie:d.verifie,commentaire:''});
 if(s.etalement==null)s.etalement=d.etalement||ETALEMENT;return s}
function sauver(){try{localStorage.setItem(CLE,JSON.stringify(etat))}catch(e){}clearTimeout(sauver.t);sauver.t=setTimeout(envoyer,400);compter()}
function reponses(){const items={};for(const i of ITEMS)items[i.nom]=it(i.nom);return{page:CLE,items}}
async function envoyer(){const d=document.getElementById('disque');try{const r=await fetch('http://127.0.0.1:%PORT%/reponses',{method:'POST',headers:{'Content-Type':'text/plain'},body:JSON.stringify(reponses())});
 if(!r.ok)throw 0;d.textContent='✓ enregistré sur le disque';d.style.color='var(--vert)'}catch(e){d.textContent='non enregistré sur le disque : utilise Télécharger';d.style.color='var(--rouge)'}}
function compter(){const n=ITEMS.filter(i=>it(i.nom).verifie).length;document.querySelector('.compte').textContent=n+' / '+ITEMS.length+' bonnes';
 document.querySelectorAll('.item').forEach(d=>{const v=it(d.dataset.nom).verifie;d.classList.toggle('ok',v);d.style.display=filtre==='reste'&&v?'none':'';
 d.querySelector('.etat').textContent=v?'bonne':'à voir';d.querySelector('[data-ok]').classList.toggle('on',v)})}
let filtre='tout';
// l'ombre de contact, calculée comme dans le jeu (ShadowLayer.ContactImage)
function opaqueSansChemin(alpha,w,h){const m=alpha.map(r=>r.slice());for(let x0=0;x0<w;x0++){if(!m[h-1][x0]||(x0>0&&m[h-1][x0-1]))continue;let x1=x0;while(x1+1<w&&m[h-1][x1+1])x1++;
 let y=h-1,lo=x0,hi=x1;while(y>=0){const row=[];for(let x=Math.max(0,lo-2);x<Math.min(w,hi+3);x++)if(m[y][x])row.push(x);if(!row.length||Math.max(...row)-Math.min(...row)+1>PATH)break;
 for(const x of row)m[y][x]=false;lo=Math.min(...row);hi=Math.max(...row);y--}}return m}
function ombre(m,w,h,pieds,mg,etalement){const W=w+2*mg,H=h+2*mg;let fill=Array.from({length:H},()=>new Uint8Array(W));
 for(const [[xl,yl],[xr,yr]] of pieds){const a=Math.min(xl,xr),b=Math.max(xl,xr),ya=xl<=xr?yl:yr,yb=xl<=xr?yr:yl,wd=b-a+1,arc=Math.min(ARC_MAX,Math.max(1,ARC_K*wd));
  for(let x=a;x<=b;x++){const t=(x+0.5-a)/wd,line=ya+(yb-ya)*(b>a?(x-a)/(b-a):0);let bas=line;
   // le bas du dessin dans cette colonne, près du segment (8 pixels au-dessus, 8 au-dessous)
   if(x>=0&&x<w)for(let y=Math.min(h-1,Math.round(line)+8);y>=Math.max(0,Math.round(line)-8);y--)if(m[y][x]){bas=y;break}
   const top=line-arc*Math.sqrt(Math.max(0,1-(2*t-1)**2));for(let y=Math.floor(Math.min(top,bas));y<=Math.round(bas);y++){const X=x+mg,Y=y+mg;if(X>=0&&Y>=0&&X<W&&Y<H)fill[Y][X]=1}}}
 const al=Array.from({length:H},()=>new Float32Array(W));let cur=fill;
 const grow=g=>{const r=Array.from({length:H},()=>new Uint8Array(W));for(let y=0;y<H;y++)for(let x=0;x<W;x++)r[y][x]=g[y][x]||(y>0&&g[y-1][x])||(y<H-1&&g[y+1][x])||(x>0&&g[y][x-1])||(x<W-1&&g[y][x+1])?1:0;return r};
 cur=grow(cur);for(const s of shades(etalement)){for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(cur[y][x]&&!al[y][x])al[y][x]=s;cur=grow(cur)}return al}
const main=document.querySelector('main');
for(const d of ITEMS){const div=document.createElement('div');div.className='item';div.dataset.nom=d.nom;
 div.innerHTML=`<h2>${d.nom}<span class="etat"></span></h2><canvas></canvas><div class="outils"><button data-ajout>+ pied</button><button data-reset>Proposition</button><button data-ok>Bonne</button><span class="lib">étalement</span><button data-moins>−</button><b data-etal></b><button data-plus>+</button></div><textarea rows="1" placeholder="commentaire"></textarea>`;
 main.appendChild(div);const img=new Image();img.src=d.src;img.onload=()=>monter(div,d,img)}
function monter(div,d,img){const w=img.width,h=img.height,mg=8,Z=Math.max(3,Math.min(10,Math.floor(420/(w+2*mg))));
 const c=document.createElement('canvas');c.width=w;c.height=h;const cx=c.getContext('2d');cx.drawImage(img,0,0);const px=cx.getImageData(0,0,w,h).data;
 const alpha=Array.from({length:h},(_,y)=>Array.from({length:w},(_,x)=>px[(y*w+x)*4+3]>127));const m=opaqueSansChemin(alpha,w,h);
 const cv=div.querySelector('canvas');cv.width=(w+2*mg)*Z;cv.height=(h+2*mg)*Z;const g=cv.getContext('2d');g.imageSmoothingEnabled=false;
 let ajout=null,prise=null;
 function dessiner(){const s=it(d.nom),al=ombre(m,w,h,s.pieds,mg,s.etalement);div.querySelector('[data-etal]').textContent=' '+s.etalement+' ';g.fillStyle='#6eaa3c';g.fillRect(0,0,cv.width,cv.height);
  for(let y=0;y<h+2*mg;y++)for(let x=0;x<w+2*mg;x++)if(al[y][x]){g.fillStyle=`rgba(0,0,0,${al[y][x]})`;g.fillRect(x*Z,y*Z,Z,Z)}
  g.globalAlpha=0.8;g.drawImage(img,mg*Z,mg*Z,w*Z,h*Z);g.globalAlpha=1;
  s.pieds.forEach(([l,r])=>{g.strokeStyle='#fff';g.lineWidth=1;g.beginPath();g.moveTo((l[0]+mg+.5)*Z,(l[1]+mg+.5)*Z);g.lineTo((r[0]+mg+.5)*Z,(r[1]+mg+.5)*Z);g.stroke();
   for(const [p,col] of [[l,'#f4f'],[r,'#4ef']]){g.fillStyle=col;g.fillRect((p[0]+mg)*Z,(p[1]+mg)*Z,Z,Z);g.strokeStyle='#000';g.strokeRect((p[0]+mg)*Z+.5,(p[1]+mg)*Z+.5,Z-1,Z-1)}});
  if(ajout&&ajout.length){g.fillStyle='#f4f';g.fillRect((ajout[0][0]+mg)*Z,(ajout[0][1]+mg)*Z,Z,Z)}}
 const pos=e=>{const r=cv.getBoundingClientRect(),k=cv.width/r.width;return[Math.floor((e.clientX-r.left)*k/Z)-mg,Math.floor((e.clientY-r.top)*k/Z)-mg]};
 const proche=p=>{const s=it(d.nom);let best=null,bd=2.5;s.pieds.forEach((f,i)=>f.forEach((q,j)=>{const dd=Math.hypot(q[0]-p[0],q[1]-p[1]);if(dd<bd){bd=dd;best=[i,j]}}));return best};
 cv.onmousedown=e=>{if(e.button!==0)return;const p=pos(e);if(ajout){ajout.push(p);if(ajout.length===2){it(d.nom).pieds.push(ajout);ajout=null;div.querySelector('[data-ajout]').classList.remove('on');sauver()}dessiner();return}
  prise=proche(p)};
 cv.onmousemove=e=>{if(!prise)return;const p=pos(e),f=it(d.nom).pieds[prise[0]];f[prise[1]]=p;dessiner()};
 window.addEventListener('mouseup',()=>{if(prise){prise=null;sauver()}});
 cv.oncontextmenu=e=>{e.preventDefault();const q=proche(pos(e));if(q){it(d.nom).pieds.splice(q[0],1);sauver();dessiner()}};
 div.querySelector('[data-ajout]').onclick=e=>{ajout=ajout?null:[];e.target.classList.toggle('on',!!ajout);dessiner()};
 div.querySelector('[data-reset]').onclick=()=>{it(d.nom).pieds=JSON.parse(JSON.stringify(d.pieds));sauver();dessiner()};
 div.querySelector('[data-moins]').onclick=()=>{const s=it(d.nom);s.etalement=Math.max(1,s.etalement-1);sauver();dessiner()};
 div.querySelector('[data-plus]').onclick=()=>{const s=it(d.nom);s.etalement=Math.min(6,s.etalement+1);sauver();dessiner()};
 div.querySelector('[data-ok]').onclick=()=>{const s=it(d.nom);s.verifie=!s.verifie;sauver()};
 const t=div.querySelector('textarea');t.value=it(d.nom).commentaire||'';t.oninput=()=>{it(d.nom).commentaire=t.value;sauver()};
 dessiner();compter()}
document.querySelectorAll('[data-filtre]').forEach(b=>b.onclick=()=>{filtre=b.dataset.filtre;document.querySelectorAll('[data-filtre]').forEach(x=>x.classList.toggle('on',x===b));compter()});
document.getElementById('telecharger').onclick=()=>{const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(reponses(),null,1)],{type:'application/json'}));a.download=CLE+'.json';a.click()};
envoyer();
</script></body></html>"""


if __name__ == "__main__":
    args = sys.argv[1:]
    if args and args[0] == "proposer":
        proposer("--tout" in args)
    elif args and args[0] == "page":
        page()
    elif args and args[0] == "appliquer":
        appliquer(args[1] if len(args) > 1 else None)
    else:
        print(__doc__)
