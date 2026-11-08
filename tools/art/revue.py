"""Revue des images de la carte en 32 px : une page où l'on choisit un candidat par image et où l'on laisse un commentaire.

    python -I tools/art/revue.py [lot]                      écrit art-candidats/en-cours/revue-<lot>.html
    python -I tools/art/revue.py serveur                    écrit les réponses des pages sur le disque à chaque changement
    python -I tools/art/revue.py appliquer [lot] [fichier]  applique les réponses (par défaut : en-cours/revue-<lot>.json,
                                                            sinon le dernier revue-<lot>*.json des Téléchargements)

Un lot est une série de candidats (LOTS) : « 32px » (le premier passage, par défaut), « v2 » (les images refaites
d'après la revue du 2026-11-08 ; candidats dans les dossiers <nom>@v2).

Sur la page : un clic sur un candidat le choisit, « Garder » ou « À refaire » donne l'avis, et chaque image a sa zone de
commentaire. Les réponses restent dans le navigateur (on peut fermer la page et revenir) ; « Télécharger » les écrit dans
un fichier revue-32px.json, que « appliquer » lit : il copie les candidats choisis dans godot/art, refait les mines dont
le bâtiment a changé (tools/art/assembler.py) et affiche les images à refaire avec leurs commentaires. Rien n'est commité.
Seule la bibliothèque standard de Python est utilisée.
"""
import copy
import glob
import html
import json
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

RACINE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EN_COURS = os.path.join(RACINE, "art-candidats", "en-cours")
CANDIDATS = os.path.join(EN_COURS, "candidats")
ART = os.path.join(RACINE, "godot", "art")
VARIANTES = os.path.join(CANDIDATS, "mines-32", "variantes")
PORT = 8765

# les images revues, par groupe (nom dans godot/art, sans .png)
GROUPES_32PX = [
    ("Terrains", ["terrain/grass", "terrain/grass-2", "terrain/road", "terrain/forest", "terrain/sand", "terrain/sand-2",
                  "terrain/swamp", "terrain/swamp-2", "terrain/water", "terrain/water-2", "terrain/mountain"]),
    ("Décor", ["decor/pine", "decor/pine-2", "decor/oak", "decor/oak-2", "decor/oak-3", "decor/autumn", "decor/autumn-2",
               "decor/autumn-3", "decor/peak", "decor/peak-2", "decor/peak-small", "decor/peak-small-2"]),
    ("Objets", ["objects/chest", "objects/obelisk", "objects/pile-gold", "objects/pile-wood", "objects/pile-ore",
                "objects/pile-gems", "objects/pile-mercury", "objects/pile-crystal", "objects/pile-sulfur",
                "objects/town", "objects/town-morts-vivants"]),
    ("Mines (3 × 2 cases ; on choisit le bâtiment)", ["objects/mine-wood", "objects/mine-ore", "objects/mine-gold",
                "objects/mine-gems", "objects/mine-crystal", "objects/mine-sulfur", "objects/mine-mercury"]),
    ("Créatures et héros", [f"creatures/{c}" for c in
                            ["paysan", "archer", "griffon", "epeiste", "moine", "cavalier", "ange", "squelette", "zombie",
                             "fantome", "vampire", "liche", "araignee", "colosse-os", "gobelin", "orc", "loup", "ogre",
                             "troll", "dragon"]] + ["heroes/hero"]),
]

GROUPES_V2 = [
    ("Terrains", ["terrain/grass", "terrain/grass-2"]),
    ("Décor (montagnes comme Heroes 2 : roche grise et herbe, sans neige, plus grandes)",
     ["decor/peak", "decor/peak-2", "decor/peak-small", "decor/peak-small-2"]),
    ("Objets", ["objects/chest", "objects/pile-gems", "objects/pile-mercury", "objects/pile-crystal", "objects/pile-sulfur"]),
    ("Mines (bâtiment plus grand ; le décor autour sera replacé ensuite)",
     ["objects/mine-gems", "objects/mine-crystal", "objects/mine-sulfur", "objects/mine-mercury"]),
]

CAPTURES = [("jeu-32-revele.png", "Carte révélée, zoom ×2"), ("jeu-32-depart-zoom3.png", "Départ, zoom ×3"),
            ("jeu-32-morts-vivants.png", "Morts-vivants"), ("jeu-32-revele-adversaire.png", "Chez l'adversaire"),
            ("jeu-32-tresor.png", "Autour de l'obélisque")]

LOTS = {
    "32px": {"titre": "Carte en 32 px : tes choix", "groupes": GROUPES_32PX, "captures": CAPTURES},
    "v2": {"titre": "Images refaites (2026-11-08) : tes choix", "groupes": GROUPES_V2, "captures": CAPTURES},
}
LOT = "32px"


def regler(lot):
    global LOT, PAGE, REPONSES
    if lot not in LOTS:
        sys.exit(f"Lot inconnu : {lot} ({', '.join(LOTS)})")
    LOT = lot
    PAGE = os.path.join(EN_COURS, f"revue-{lot}.html")
    REPONSES = os.path.join(EN_COURS, f"revue-{lot}.json")


regler("32px")


def lire_octets(chemin):
    with open(chemin, "rb") as f:
        return f.read()


def dossier_candidats(nom):
    """Le dossier des candidats d'une image : « decor/pine-2 » prend ceux de decor/pine@32px s'il n'a pas les siens."""
    for essai in (nom, re.sub(r"-\d+$", "", nom)):
        d = os.path.join(CANDIDATS, *essai.split("/")) + f"@{LOT}"
        if os.path.isdir(d):
            return d
    return None


def batiment_de(mine):
    """(pièce, numéro, lot) du bâtiment d'une mine dans assembler.MINES."""
    import assembler
    chemin = assembler.MINES[mine]["batiment"][0]
    m = re.search(r"pieces/([^/@]+)@([\w-]+)/(\d+)\.png$", chemin.replace("\\", "/"))
    return m.group(1), int(m.group(3)), m.group(2)


def mine_avec(mine, numero, lot="32px"):
    """La description de la mine avec un autre candidat pour son bâtiment."""
    import assembler
    desc = copy.deepcopy(assembler.MINES[mine])
    piece, _, _ = batiment_de(mine)
    b = desc["batiment"]
    desc["batiment"] = (assembler.p(piece, numero, lot), *b[1:])
    return desc


def options(nom):
    """Les choix d'une image : [(id, chemin de l'image vu de la page, libellé, installée ?)]."""
    installee = os.path.join(ART, *nom.split("/")) + ".png"
    octets = lire_octets(installee) if os.path.exists(installee) else b""
    v = int(os.path.getmtime(installee)) if octets else 0
    rel_installee = os.path.relpath(installee, EN_COURS).replace("\\", "/") + f"?v={v}"
    res = []
    if nom.startswith("objects/mine-"):
        import assembler
        from reduire import ecrire
        mine = nom.split("/")[1]
        piece, actuel, lot_actuel = batiment_de(mine)
        os.makedirs(VARIANTES, exist_ok=True)
        res.append(("installee", rel_installee, f"★ installée ({piece} {actuel})", True))
        for n in range(1, 5):
            if (n == actuel and LOT == lot_actuel) or not os.path.exists(
                    os.path.join(CANDIDATS, "pieces", f"{piece}@{LOT}", f"{n}.png")):
                continue
            sortie = os.path.join(VARIANTES, f"{mine}-{LOT}-{n}.png")
            ecrire(sortie, assembler.assembler(mine_avec(mine, n, LOT)))
            res.append((f"mine:{LOT}:{n}", os.path.relpath(sortie, EN_COURS).replace("\\", "/"), f"{piece} {LOT} {n}", False))
        return res
    res.append(("installee", rel_installee, "★ installée", True))
    d = dossier_candidats(nom)
    if d:
        for f in sorted(glob.glob(os.path.join(d, "*.png")), key=lambda x: int(re.sub(r"\D", "", os.path.basename(x)) or 0)):
            if lire_octets(f) == octets:
                continue  # c'est l'image installée
            rel = os.path.relpath(f, CANDIDATS).replace("\\", "/")
            res.append((rel, os.path.relpath(f, EN_COURS).replace("\\", "/"), f"{os.path.basename(d)} {os.path.basename(f)[:-4]}", False))
    return res


STYLE = """
:root{--fond:#1d1f22;--carte:#2a2d31;--bord:#3b3f45;--texte:#eceae4;--doux:#a9a59c;--or:#e8b84a;--vert:#5fb36b;--rouge:#d9654f}
*{box-sizing:border-box}body{margin:0;background:var(--fond);color:var(--texte);font:15px/1.4 system-ui,sans-serif}
header{position:sticky;top:0;z-index:5;background:#16181bee;backdrop-filter:blur(4px);border-bottom:1px solid var(--bord);
 padding:10px 16px;display:flex;flex-wrap:wrap;gap:10px;align-items:center}
header h1{font-size:17px;margin:0 12px 0 0}.compte{color:var(--doux);font-variant-numeric:tabular-nums}
.barre{flex:1;min-width:120px;height:6px;background:var(--bord);border-radius:3px;overflow:hidden}.barre i{display:block;height:100%;background:var(--or)}
button{font:inherit;color:var(--texte);background:#33373d;border:1px solid var(--bord);border-radius:6px;padding:5px 10px;cursor:pointer}
button:hover{border-color:var(--doux)}button.on{background:var(--or);color:#222;border-color:var(--or)}
main{max-width:1400px;margin:0 auto;padding:12px 16px 80px}h2{margin:28px 0 10px;font-size:18px}
.aide{color:var(--doux);margin:8px 0 0}.captures{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:8px}
.captures figure{margin:0}.captures img{width:100%;border-radius:6px;cursor:zoom-in;display:block}.captures figcaption{font-size:12px;color:var(--doux)}
.grille{display:grid;grid-template-columns:repeat(auto-fill,minmax(330px,1fr));gap:10px;align-items:start}
.item{background:var(--carte);border:1px solid var(--bord);border-radius:10px;padding:8px 10px;min-width:0}
.item.garder{border-left:5px solid var(--vert)}.item.refaire{border-left:5px solid var(--rouge)}.item.change{border-left:5px solid var(--or)}
.tete{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:8px}.tete b{font-size:15px;margin-right:auto}
.etat{font-size:12px;color:var(--doux);order:9;flex-basis:100%;margin-top:-6px}
.opts{display:flex;flex-wrap:wrap;gap:8px;align-items:flex-end}
.opt{border:3px solid transparent;border-radius:6px;padding:4px;cursor:pointer;background-size:var(--case) var(--case);
 image-rendering:pixelated;display:flex;flex-direction:column;align-items:center;justify-content:flex-end}
.opt:hover{border-color:#ffffff55}.opt.pris{border-color:var(--or)}.opt img{image-rendering:pixelated;display:block}
.opt span{font-size:11px;background:#000a;padding:0 4px;border-radius:3px;margin-top:3px;white-space:nowrap}
.tuile{image-rendering:pixelated}
.opts{overflow-x:auto}
textarea{width:100%;margin-top:8px;background:#202326;color:var(--texte);border:1px solid var(--bord);border-radius:6px;padding:6px;font:inherit;min-height:30px;height:30px;resize:vertical}
.cache{display:none}#loupe{position:fixed;inset:0;background:#000d;display:none;align-items:center;justify-content:center;z-index:9;cursor:zoom-out}
#loupe img{max-width:98vw;max-height:98vh;image-rendering:pixelated}#note{position:fixed;bottom:16px;left:50%;transform:translateX(-50%);
 background:var(--or);color:#222;padding:8px 14px;border-radius:8px;display:none;z-index:10}
@media(max-width:600px){header h1{width:100%}}
"""

SCRIPT = r"""
const CLE='%PAGE%';let etat={general:'',items:{}};
try{etat=JSON.parse(localStorage.getItem(CLE))||etat}catch(e){}
const zoomSauve=(()=>{try{return +localStorage.getItem(CLE+'-zoom')||2}catch(e){return 2}})();
function sauver(){try{localStorage.setItem(CLE,JSON.stringify(etat))}catch(e){}clearTimeout(sauver.t);sauver.t=setTimeout(envoyer,400)}
// les réponses sont aussi écrites sur le disque par « revue.py serveur » s'il tourne (sinon : Télécharger)
async function envoyer(){const d=document.getElementById('disque');try{const r=await fetch('http://127.0.0.1:%PORT%/reponses',{method:'POST',
 headers:{'Content-Type':'text/plain'},body:JSON.stringify(reponses(),null,1)});if(!r.ok)throw 0;d.textContent='✓ enregistré sur le disque';d.style.color='var(--vert)'}
 catch(e){d.textContent='non enregistré sur le disque : utilise Télécharger';d.style.color='var(--rouge)'}}
function item(n){return etat.items[n]||(etat.items[n]={choix:'installee',avis:'',commentaire:''})}
function zoom(z){document.querySelectorAll('img[data-w]').forEach(i=>{i.style.width=i.dataset.w*z+'px'});
 document.querySelectorAll('.tuile').forEach(t=>{const c=32*z;t.style.width=t.style.height=3*c+'px';t.style.backgroundSize=c+'px '+c+'px'});
 document.documentElement.style.setProperty('--case',32*z+'px');
 document.querySelectorAll('[data-zoom]').forEach(b=>b.classList.toggle('on',+b.dataset.zoom===z));try{localStorage.setItem(CLE+'-zoom',z)}catch(e){}}
function majItem(div){const n=div.dataset.nom,it=etat.items[n];
 div.querySelectorAll('.opt').forEach(o=>o.classList.toggle('pris',o.dataset.id===(it?it.choix:'installee')));
 div.classList.remove('garder','refaire','change');
 if(it&&it.avis==='refaire')div.classList.add('refaire');else if(it&&it.choix!=='installee')div.classList.add('change');else if(it&&it.avis==='garder')div.classList.add('garder');
 div.querySelectorAll('[data-avis]').forEach(b=>b.classList.toggle('on',!!it&&it.avis===b.dataset.avis));
 const e=div.querySelector('.etat');e.textContent=!it||(!it.avis&&it.choix==='installee'&&!it.commentaire)?'pas encore vu':
  it.avis==='refaire'?'à refaire':it.choix!=='installee'?'changer pour '+div.querySelector('.opt.pris span').textContent:it.avis==='garder'?'gardée':'commentaire';}
function vu(it){return it&&(it.avis||it.choix!=='installee'||it.commentaire)}
function compter(){const items=[...document.querySelectorAll('.item')];const n=items.filter(d=>vu(etat.items[d.dataset.nom])).length;
 document.querySelector('.compte').textContent=n+' / '+items.length+' vues';document.querySelector('.barre i').style.width=100*n/items.length+'%';filtrer()}
let filtre='tout';function filtrer(){document.querySelectorAll('.item').forEach(d=>{const it=etat.items[d.dataset.nom];
 d.classList.toggle('cache',filtre==='reste'?!!vu(it):filtre==='repondu'?!vu(it):false)});
 document.querySelectorAll('[data-filtre]').forEach(b=>b.classList.toggle('on',b.dataset.filtre===filtre))}
document.querySelectorAll('.item').forEach(div=>{const n=div.dataset.nom;
 div.querySelectorAll('.opt').forEach(o=>o.onclick=()=>{const it=item(n);it.choix=o.dataset.id;if(it.avis==='garder'&&o.dataset.id!=='installee')it.avis='';if(it.choix!=='installee'&&it.avis==='refaire')it.avis='';sauver();majItem(div);compter()});
 div.querySelectorAll('[data-avis]').forEach(b=>b.onclick=()=>{const it=item(n);it.avis=it.avis===b.dataset.avis?'':b.dataset.avis;
  if(it.avis)it.choix='installee';sauver();majItem(div);compter()});
 const t=div.querySelector('textarea');t.value=(etat.items[n]||{}).commentaire||'';t.oninput=()=>{item(n).commentaire=t.value;sauver();majItem(div);compter()};
 majItem(div)});
const g=document.getElementById('general');g.value=etat.general||'';g.oninput=()=>{etat.general=g.value;sauver()};
document.querySelectorAll('[data-zoom]').forEach(b=>b.onclick=()=>zoom(+b.dataset.zoom));
document.querySelectorAll('[data-filtre]').forEach(b=>b.onclick=()=>{filtre=b.dataset.filtre;filtrer()});
function note(t){const n=document.getElementById('note');n.textContent=t;n.style.display='block';setTimeout(()=>n.style.display='none',2500)}
function reponses(){const items={};for(const[n,it]of Object.entries(etat.items))if(vu(it))items[n]=it;return{page:'%PAGE%',general:etat.general,items}}
function texte(){const r=reponses();let s='';for(const[n,it]of Object.entries(r.items)){
 const d=document.querySelector(`.item[data-nom="${n}"]`);const lib=d?d.querySelector(`.opt[data-id="${it.choix}"] span`).textContent:it.choix;
 s+=`${n} : ${it.avis==='refaire'?'à refaire':it.choix!=='installee'?'changer pour '+lib:it.avis==='garder'?'garder':'—'}${it.commentaire?' — '+it.commentaire:''}\n`}
 return (r.general?'Général : '+r.general+'\n':'')+s}
document.getElementById('copier').onclick=async()=>{try{await navigator.clipboard.writeText(texte()+'\n'+JSON.stringify(reponses()));note('Réponses copiées : colle-les dans la conversation')}catch(e){note('Copie impossible : utilise Télécharger')}};
document.getElementById('telecharger').onclick=()=>{const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(reponses(),null,1)],{type:'application/json'}));
 a.download='%PAGE%.json';a.click();note('Fichier %PAGE%.json enregistré dans Téléchargements')};
document.getElementById('effacer').onclick=()=>{if(confirm('Effacer toutes tes réponses de cette page ?')){etat={general:'',items:{}};sauver();location.reload()}};
const loupe=document.getElementById('loupe');document.querySelectorAll('.captures img').forEach(i=>i.onclick=()=>{loupe.querySelector('img').src=i.src;loupe.style.display='flex'});
loupe.onclick=()=>loupe.style.display='none';
zoom(zoomSauve);compter();envoyer();
"""


def largeur(chemin):
    with open(chemin.split("?")[0] if os.path.isabs(chemin) else os.path.join(EN_COURS, chemin.split("?")[0]), "rb") as f:
        d = f.read(24)
    return int.from_bytes(d[16:20], "big")


def page():
    herbe = os.path.relpath(os.path.join(ART, "terrain", "grass.png"), EN_COURS).replace("\\", "/")
    morceaux = [f"<!doctype html><html lang=fr><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'>"
                f"<title>Revue carte 32 px</title><style>{STYLE}</style><body>"
                f"<header><h1>{LOTS[LOT]['titre']}</h1><span class=compte></span><div class=barre><i></i></div>"
                "<span>Zoom</span><button data-zoom=2>×2</button><button data-zoom=3>×3</button>"
                "<span>Montrer</span><button data-filtre=tout>tout</button><button data-filtre=reste>pas vues</button>"
                "<button data-filtre=repondu>vues</button>"
                "<button id=copier>Copier mes réponses</button><button id=telecharger>Télécharger</button>"
                "<button id=effacer title='Tout effacer'>Effacer</button><span id=disque class=compte></span></header><main>"
                "<p class=aide>Pour chaque image : clique un candidat pour le choisir (cadre doré ; ★ = l'image actuelle du jeu), "
                "ou « Garder » / « À refaire », et écris un commentaire si tu veux. Tes réponses restent dans ce navigateur. "
                "Quand « ✓ enregistré sur le disque » s'affiche en haut, chaque réponse est déjà écrite pour Claude : à la fin, dis-lui simplement « c'est fait ». Sinon : <b>Télécharger</b>.</p>"
                "<textarea id=general placeholder='Commentaire général (style, couleurs, ce qui manque…)'></textarea>"]
    captures = [(f, legende) for f, legende in LOTS[LOT]["captures"] if os.path.exists(os.path.join(EN_COURS, f))]
    if captures:
        morceaux.append("<h2>Dans le jeu</h2><div class=captures>")
        for f, legende in captures:
            v = int(os.path.getmtime(os.path.join(EN_COURS, f)))
            morceaux.append(f"<figure><img src='{f}?v={v}' alt='{html.escape(legende)}'><figcaption>{html.escape(legende)}</figcaption></figure>")
        morceaux.append("</div>")
    total = 0
    for titre, noms in LOTS[LOT]["groupes"]:
        morceaux.append(f"<h2>{html.escape(titre)}</h2><div class=grille>")
        for nom in noms:
            total += 1
            terrain = nom.startswith("terrain/")
            morceaux.append(f"<div class=item data-nom='{nom}'><div class=tete><b>{nom.split('/')[1]}</b><span class=etat></span>"
                            "<button data-avis=garder>Garder</button><button data-avis=refaire>À refaire</button></div><div class=opts>")
            for ident, src, libelle, _ in options(nom):
                if terrain:
                    vue = f"<div class=tuile style=\"background-image:url('{src}')\"></div>"
                    fond = ""
                else:
                    vue = f"<img src='{src}' data-w={largeur(src)} alt=''>"
                    fond = f" style=\"background-image:url('{herbe}')\""
                morceaux.append(f"<div class=opt data-id='{ident}'{fond}>{vue}<span>{html.escape(libelle)}</span></div>")
            morceaux.append("</div><textarea placeholder='Commentaire'></textarea></div>")
        morceaux.append("</div>")
    morceaux.append(f"</main><div id=loupe><img alt=''></div><div id=note></div><script>{SCRIPT.replace('%PORT%', str(PORT)).replace('%PAGE%', 'revue-' + LOT)}</script></body></html>")
    with open(PAGE, "w", encoding="utf-8") as f:
        f.write("".join(morceaux))
    print(f"{total} images : {os.path.relpath(PAGE, RACINE)}")


def appliquer(fichier):
    if fichier is None and os.path.exists(REPONSES):
        fichier = REPONSES
    if fichier is None:
        dl = os.path.join(os.path.expanduser("~"), "Downloads")
        fichiers = sorted(glob.glob(os.path.join(dl, f"revue-{LOT}*.json")), key=os.path.getmtime)
        if not fichiers:
            sys.exit(f"Aucun revue-{LOT}*.json dans {dl}")
        fichier = fichiers[-1]
    with open(fichier, encoding="utf-8") as f:
        r = json.load(f)
    print(f"Réponses : {fichier}")
    if r.get("general"):
        print(f"\nCommentaire général : {r['general']}")
    a_refaire, changes = [], []
    for nom, it in r.get("items", {}).items():
        choix = it.get("choix", "installee")
        cible = os.path.join(ART, *nom.split("/")) + ".png"
        if choix.startswith("mine:"):
            import assembler
            from reduire import ecrire
            # « mine:3 » (lot 32px, ancienne forme) ou « mine:v2:3 »
            parts = choix.split(":")
            lot, n = (parts[1], int(parts[2])) if len(parts) == 3 else ("32px", int(parts[1]))
            mine = nom.split("/")[1]
            piece, _, _ = batiment_de(mine)
            source = os.path.join(RACINE, "tools", "art", "assembler.py")
            texte = open(source, encoding="utf-8").read()
            debut = texte.index(f'"{mine}": {{')
            ligne = texte.index('"batiment"', debut)
            m = re.compile(r'p\("' + re.escape(piece) + r'", \d+(, "[\w-]+")?\)').search(texte, ligne)
            nouveau = f'p("{piece}", {n})' if lot == "32px" else f'p("{piece}", {n}, "{lot}")'
            texte = texte[:m.start()] + nouveau + texte[m.end():]
            with open(source, "w", encoding="utf-8", newline="\n") as f:
                f.write(texte)
            assembler.MINES[mine] = mine_avec(mine, n, lot)
            ecrire(os.path.join(CANDIDATS, "mines-32", f"{mine}.png"), assembler.assembler(assembler.MINES[mine]))
            ecrire(cible, assembler.assembler(assembler.MINES[mine]))
            changes.append(f"{nom} : bâtiment {piece} {lot} {n} (assembler.py mis à jour)")
        elif choix != "installee":
            shutil.copyfile(os.path.join(CANDIDATS, *choix.split("/")), cible)
            changes.append(f"{nom} : {choix}")
        if it.get("avis") == "refaire" or it.get("commentaire"):
            a_refaire.append((nom, it.get("avis") == "refaire", it.get("commentaire", "")))
    print(f"\nInstallées ({len(changes)}) :" + "".join(f"\n  {c}" for c in changes))
    print(f"\nÀ refaire ou commentées ({len(a_refaire)}) :" + "".join(
        f"\n  {'[à refaire] ' if refaire else ''}{nom}{' — ' + c if c else ''}" for nom, refaire, c in a_refaire))


def serveur():
    """Reçoit les réponses de la page à chaque changement et les écrit dans en-cours/revue-<lot>.json."""
    from http.server import BaseHTTPRequestHandler, HTTPServer

    class Reponses(BaseHTTPRequestHandler):
        def _entetes(self, code):
            self.send_response(code)
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Access-Control-Allow-Private-Network", "true")
            self.send_header("Access-Control-Allow-Headers", "Content-Type")
            self.end_headers()

        def do_OPTIONS(self):
            self._entetes(204)

        def do_POST(self):
            corps = self.rfile.read(int(self.headers.get("Content-Length", 0)))
            try:
                r = json.loads(corps)
                assert r.get("page", "")[len("revue-"):] in LOTS
            except Exception:
                return self._entetes(400)
            fichier = os.path.join(EN_COURS, r["page"] + ".json")
            temp = fichier + ".tmp"
            with open(temp, "w", encoding="utf-8") as f:
                json.dump(r, f, ensure_ascii=False, indent=1)
            os.replace(temp, fichier)
            print(f"{r['page']} : {len(r.get('items', {}))} réponses enregistrées", flush=True)
            self._entetes(200)

        def log_message(self, *args):
            pass

    print(f"Réponses écrites dans {os.path.relpath(EN_COURS, RACINE)}/revue-<lot>.json (port {PORT}) ; Ctrl+C pour arrêter",
          flush=True)
    HTTPServer(("127.0.0.1", PORT), Reponses).serve_forever()


if __name__ == "__main__":
    args = sys.argv[1:]
    if args and args[0] == "serveur":
        serveur()
    elif args and args[0] == "appliquer":
        reste = args[1:]
        if reste and reste[0] in LOTS:
            regler(reste.pop(0))
        appliquer(reste[0] if reste else None)
    else:
        regler(args[0] if args else "32px")
        page()
