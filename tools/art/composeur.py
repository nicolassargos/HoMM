"""Le composeur : une page pour composer une image à la main avec les images du jeu et des candidats (arbres, cabanes,
chariots…), par exemple la maquette d'une grande mine que Retro Diffusion redessine ensuite d'un seul style
(« input » dans assets.json). Sans dépense.

    python -I tools/art/composeur.py [nom=maquette] [largeur=72] [hauteur=48] [dossiers…]

Écrit art-candidats/en-cours/composeur-<nom>.html. La bibliothèque montre les images PNG des dossiers donnés (par
défaut : godot/art/decor, godot/art/objects, et les candidats en cours de art-candidats/en-cours/candidats/pieces et
objects) ; on peut aussi y ajouter ses propres images. Sur la page : glisser une image de la bibliothèque sur le cadre,
la déplacer à la souris ou aux flèches (pixel par pixel, Maj : 5), F la retourne, Suppr l'enlève, D la double,
Page préc./suiv. la passe devant ou derrière. « Enregistrer l'image » télécharge la maquette en taille réelle, sur un
fond uni (pour Retro Diffusion) ; « Enregistrer la disposition » garde la place de chaque image (JSON), qu'on peut
recharger. Seule la bibliothèque standard de Python est utilisée.
"""
import base64
import json
import os
import sys

EN_COURS = "art-candidats/en-cours"
DEFAUT = ["godot/art/decor", "godot/art/objects", "art-candidats/en-cours/candidats/pieces",
          "art-candidats/en-cours/candidats/objects"]

PAGE = r"""<!doctype html><meta charset=utf-8><title>Composeur : __NOM__</title>
<style>
body{background:#1e1e1e;color:#eee;font-family:sans-serif;margin:0;display:flex;height:100vh}
#biblio{width:360px;overflow-y:auto;background:#2a2a2a;padding:8px;box-sizing:border-box}
#biblio h3{font-size:12px;margin:10px 0 4px;color:#bbb;word-break:break-all}
.vignettes{display:flex;flex-wrap:wrap;gap:4px}
.vignettes img{image-rendering:pixelated;background:#3a5a2a;border:1px solid #444;cursor:grab;max-width:96px;max-height:96px;min-width:32px}
#centre{flex:1;display:flex;flex-direction:column;align-items:center;overflow:auto;padding:10px}
#outils{margin:6px 0;display:flex;gap:8px;flex-wrap:wrap;align-items:center;font-size:13px}
canvas{image-rendering:pixelated;border:1px solid #666;cursor:crosshair}
button{background:#444;color:#eee;border:1px solid #666;padding:4px 8px;cursor:pointer}
#aide{font-size:12px;color:#aaa;max-width:900px}
</style>
<div id=biblio><b>Bibliothèque</b> (glisser sur le cadre)<br>
<label style="font-size:12px">Ajouter mes images : <input type=file id=fichiers accept="image/png" multiple></label>
<div id=listes></div></div>
<div id=centre>
<div id=outils>
 <b>__NOM__</b>
 <label>Taille <input id=W type=number value=__W__ style="width:50px"> × <input id=H type=number value=__H__ style="width:50px"></label>
 <label>Zoom <input id=zoom type=range min=2 max=12 value=8></label>
 <label>Fond <select id=fond><option value=herbe>herbe du jeu</option><option value=uni>uni</option></select></label>
 <input id=couleur type=color value="#5c8438">
 <button id=retourner>Retourner (F)</button><button id=doubler>Doubler (D)</button><button id=devant>Devant</button><button id=derriere>Derrière</button><button id=enlever>Enlever (Suppr)</button>
</div>
<canvas id=cadre></canvas>
<div id=outils>
 <button id=image>Enregistrer l'image (fond uni)</button>
 <button id=dispo>Enregistrer la disposition</button>
 <label>Charger une disposition <input type=file id=charger accept=".json"></label>
</div>
<div id=calques style="font-size:13px;max-width:900px;margin:4px 0"></div>
<div id=aide>Glisser une image de la bibliothèque sur le cadre. Clic : choisir une image (elle s'encadre), la déplacer à la
souris ou aux flèches (Maj : 5 pixels). F : retourner, D : doubler, Suppr : enlever, Page précédente / suivante : devant /
derrière. Le cadre est en pixels du jeu (__W__ × __H__ pour une grande mine de 3 × 2 cases). L'image enregistrée est en
taille réelle, sur un fond uni, pour Retro Diffusion. Les fichiers vont dans Téléchargements : me dire leur nom.</div>
</div>
<script>
const BIBLIO=__BIBLIO__;
const NOMS={};for(const l of Object.values(BIBLIO))for(const [src,nom] of l)NOMS[src]=nom;
const herbe=new Image();herbe.src="__HERBE__";
let W=__W__,H=__H__,Z=8,items=[],choisi=null,glisse=null;
const cv=document.getElementById("cadre"),g=cv.getContext("2d");
function taille(){W=+document.getElementById("W").value;H=+document.getElementById("H").value;Z=+document.getElementById("zoom").value;
 cv.width=W*Z;cv.height=H*Z;dessin();}
function charge(src,cb){const im=new Image();im.onload=()=>cb(im);im.src=src;}
function calques(){const d=document.getElementById("calques");d.innerHTML="<b>Calques</b> (du fond vers l'avant, clic pour choisir) : ";
 items.forEach((it,i)=>{const b=document.createElement("button");b.textContent=(i+1)+". "+(it.nom||"image").split("/").slice(-2).join("/");
  if(it===choisi)b.style.background="#8a6d1a";b.onclick=()=>{choisi=it;dessin();};b.style.margin="2px";d.append(b);});}
function dessin(enregistrer){
 const c=enregistrer?enregistrer.getContext("2d"):g, z=enregistrer?1:Z;
 c.imageSmoothingEnabled=false;
 if(!enregistrer&&document.getElementById("fond").value=="herbe"&&herbe.complete){
   for(let y=0;y<H;y+=herbe.height)for(let x=0;x<W;x+=herbe.width)c.drawImage(herbe,x*z,y*z,herbe.width*z,herbe.height*z);
 }else{c.fillStyle=document.getElementById("couleur").value;c.fillRect(0,0,W*z,H*z);}
 for(const it of items){c.save();
   if(it.flip){c.translate((it.x+it.img.width)*z,it.y*z);c.scale(-1,1);c.drawImage(it.img,0,0,it.img.width*z,it.img.height*z);}
   else c.drawImage(it.img,it.x*z,it.y*z,it.img.width*z,it.img.height*z);
   c.restore();}
 if(!enregistrer&&choisi){g.strokeStyle="#ffd84a";g.lineWidth=2;g.strokeRect(choisi.x*Z,choisi.y*Z,choisi.img.width*Z,choisi.img.height*Z);}
 if(!enregistrer){g.strokeStyle="rgba(255,255,255,0.25)";g.lineWidth=1;
   for(let x=24;x<W;x+=24){g.beginPath();g.moveTo(x*Z,0);g.lineTo(x*Z,H*Z);g.stroke();}
   for(let y=24;y<H;y+=24){g.beginPath();g.moveTo(0,y*Z);g.lineTo(W*Z,y*Z);g.stroke();}
   calques();}
}
// le point est-il dans le cadre de l'image ? (une page ouverte depuis le disque ne peut pas lire les pixels de ses images :
// on choisit donc au rectangle, la plus en avant d'abord ; la liste des calques permet de choisir celles qui se cachent)
function dedans(it,px,py){return px>=it.x&&py>=it.y&&px<it.x+it.img.width&&py<it.y+it.img.height;}
// le pixel visé est-il opaque ? (pour choisir la pièce sous la souris, pas son cadre vide)
const sonde=document.createElement("canvas").getContext("2d",{willReadFrequently:true});
function opaque(it,px,py){if(!dedans(it,px,py))return false;const x=Math.floor(px-it.x),y=Math.floor(py-it.y);
 try{sonde.canvas.width=it.img.width;sonde.canvas.height=it.img.height;sonde.drawImage(it.img,0,0);
  return sonde.getImageData(it.flip?it.img.width-1-x:x,y,1,1).data[3]>40;}catch(e){return true;}}
function ajoute(src,x,y,nom){charge(src,im=>{const it={src,nom:nom||NOMS[src],img:im,x:Math.round(x-im.width/2),y:Math.round(y-im.height/2),flip:false};items.push(it);choisi=it;dessin();});}
cv.addEventListener("dragover",e=>e.preventDefault());
cv.addEventListener("drop",e=>{e.preventDefault();const src=e.dataTransfer.getData("text/plain");const r=cv.getBoundingClientRect();
 if(src)ajoute(src,(e.clientX-r.left)/Z,(e.clientY-r.top)/Z);});
cv.addEventListener("mousedown",e=>{const r=cv.getBoundingClientRect(),px=(e.clientX-r.left)/Z,py=(e.clientY-r.top)/Z;
 choisi=null;for(let i=items.length-1;i>=0;i--)if(opaque(items[i],px,py)){choisi=items[i];break;}
 if(!choisi)for(let i=items.length-1;i>=0;i--)if(dedans(items[i],px,py)){choisi=items[i];break;}
 if(choisi)glisse={dx:px-choisi.x,dy:py-choisi.y};dessin();});
window.addEventListener("mousemove",e=>{if(!glisse||!choisi)return;const r=cv.getBoundingClientRect();
 choisi.x=Math.round((e.clientX-r.left)/Z-glisse.dx);choisi.y=Math.round((e.clientY-r.top)/Z-glisse.dy);dessin();});
window.addEventListener("mouseup",()=>glisse=null);
function action(k){if(!choisi)return;const i=items.indexOf(choisi);
 if(k=="f")choisi.flip=!choisi.flip;
 if(k=="d"){const c={...choisi,x:choisi.x+4,y:choisi.y+2};items.push(c);choisi=c;}
 if(k=="suppr"){items.splice(i,1);choisi=null;}
 if(k=="devant"&&i<items.length-1){items.splice(i,1);items.splice(i+1,0,choisi);}
 if(k=="derriere"&&i>0){items.splice(i,1);items.splice(i-1,0,choisi);}
 dessin();}
window.addEventListener("keydown",e=>{if(e.target.tagName=="INPUT")return;const p=e.shiftKey?5:1;
 if(!choisi)return;
 if(e.key=="ArrowLeft")choisi.x-=p;else if(e.key=="ArrowRight")choisi.x+=p;else if(e.key=="ArrowUp")choisi.y-=p;else if(e.key=="ArrowDown")choisi.y+=p;
 else if(e.key=="f"||e.key=="F")return action("f");else if(e.key=="d"||e.key=="D")return action("d");
 else if(e.key=="Delete"||e.key=="Backspace")return action("suppr");else if(e.key=="PageUp")return action("devant");else if(e.key=="PageDown")return action("derriere");
 else return;e.preventDefault();dessin();});
for(const [id,k] of [["retourner","f"],["doubler","d"],["enlever","suppr"],["devant","devant"],["derriere","derriere"]])
 document.getElementById(id).onclick=()=>action(k);
for(const id of ["W","H","zoom","fond","couleur"])document.getElementById(id).oninput=taille;
function telecharge(nom,url){const a=document.createElement("a");a.href=url;a.download=nom;a.click();}
document.getElementById("image").onclick=()=>{const c=document.createElement("canvas");c.width=W;c.height=H;dessin(c);
 telecharge("maquette-__NOM__.png",c.toDataURL("image/png"));};
document.getElementById("dispo").onclick=()=>{const d={W,H,fond:document.getElementById("couleur").value,
 items:items.map(it=>({src:it.src,nom:it.nom,x:it.x,y:it.y,flip:it.flip}))};
 telecharge("disposition-__NOM__.json","data:application/json,"+encodeURIComponent(JSON.stringify(d,null,1)));};
document.getElementById("charger").onchange=e=>{const f=e.target.files[0];if(!f)return;f.text().then(t=>{const d=JSON.parse(t);
 document.getElementById("W").value=d.W;document.getElementById("H").value=d.H;items=[];choisi=null;taille();
 d.items.forEach(o=>charge(o.src,im=>{items.push({src:o.src,nom:o.nom,img:im,x:o.x,y:o.y,flip:o.flip});dessin();}));});};
document.getElementById("fichiers").onchange=e=>{for(const f of e.target.files){const r=new FileReader();r.onload=()=>{NOMS[r.result]="mes images/"+f.name;ajoutVignette("Mes images",r.result,f.name);};r.readAsDataURL(f);}};
function ajoutVignette(groupe,src,nom){let d=document.getElementById("g-"+groupe);
 if(!d){const h=document.createElement("h3");h.textContent=groupe;d=document.createElement("div");d.className="vignettes";d.id="g-"+groupe;
  document.getElementById("listes").append(h,d);}
 const im=document.createElement("img");im.src=src;im.title=nom;im.draggable=true;
 im.onload=()=>{im.style.width=(im.naturalWidth*2)+"px";};
 im.addEventListener("dragstart",e=>e.dataTransfer.setData("text/plain",src));
 im.addEventListener("dblclick",()=>ajoute(src,W/2,H/2));d.append(im);}
for(const [groupe,liste] of Object.entries(BIBLIO))for(const [src,nom] of liste)ajoutVignette(groupe,src,nom);
herbe.onload=dessin;taille();
</script>
"""


def en_ligne(chemin):
    with open(chemin, "rb") as fo:
        return "data:image/png;base64," + base64.b64encode(fo.read()).decode("ascii")


def main():
    a = sys.argv[1:]
    nom = a[0] if a else "maquette"
    largeur = int(a[1]) if len(a) > 1 else 72
    hauteur = int(a[2]) if len(a) > 2 else 48
    dossiers = a[3:] or DEFAUT
    biblio = {}
    for racine in dossiers:
        for dossier, sous, fichiers in os.walk(racine):
            sous.sort()
            # pas les grandes images ni les dossiers de travail (réduites, au double…)
            if any(p in dossier.replace("\\", "/") for p in ("/16", "/24", "/reduit")):
                continue
            pngs = sorted(f for f in fichiers if f.lower().endswith(".png") and not f.startswith("planche"))
            if not pngs:
                continue
            groupe = os.path.relpath(dossier).replace("\\", "/")
            # les images sont mises dans la page même : une page ouverte depuis le disque ne pourrait pas, sinon,
            # enregistrer l'image composée (le navigateur l'interdit pour des images lues sur le disque)
            biblio[groupe] = [(en_ligne(os.path.join(dossier, f)), f"{groupe}/{f}") for f in pngs]
    herbe = en_ligne("godot/art/terrain/grass.png")
    page = (PAGE.replace("__NOM__", nom).replace("__W__", str(largeur)).replace("__H__", str(hauteur))
            .replace("__BIBLIO__", json.dumps(biblio, ensure_ascii=False)).replace("__HERBE__", herbe))
    sortie = os.path.join(EN_COURS, f"composeur-{nom}.html")
    with open(sortie, "w", encoding="utf-8") as fo:
        fo.write(page)
    print("page :", sortie, "—", sum(len(v) for v in biblio.values()), "images dans la bibliothèque")


if __name__ == "__main__":
    main()
