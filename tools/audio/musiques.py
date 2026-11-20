"""Les musiques des villes et de la carte, prises dans les bibliothèques libres (CC0 ou CC-BY) d'après une liste
(tools/audio/musiques-villes.json, musiques-carte.json), écoutées sur une page où l'on choisit une musique par faction
ou par terrain et note chaque piste, puis installées.

    python -I tools/audio/musiques.py [carte] telecharger    télécharge ce qui manque (art-candidats/en-cours/musiques/,
                                                             musiques-carte/ pour la carte)
    python -I tools/audio/musiques.py [carte] page [tout]    écrit la page d'écoute art-candidats/en-cours/musiques.html
                                                             (musiques-carte.html pour la carte) ; « tout » : garde
                                                             toutes les pistes, pendant qu'un tour de notes est en cours
    python -I tools/audio/musiques.py serveur                sert les pages (http://localhost:8767/musiques.html) ; les
                                                             choix s'écrivent aussitôt dans en-cours/<page>-reponses.json
    python -I tools/audio/musiques.py [carte] installer      copie chaque musique choisie dans godot/audio/musique/
                                                             (ville-<faction>, carte-<terrain>) et la note dans
                                                             godot/data/sounds.json (« town-<faction> », « map-<terrain> »)

« ambiances » : les fonds sonores par sol (godot/audio/ambiance/, section « ambience » de sounds.json).
Sans « carte » ni « menu » : les villes (« menu » : la musique du menu principal, parmi celles qui ne servent pas). Chaque
élément de la liste : id (« <groupe>-<n> »), faction (le groupe : la faction ou le terrain), « nouveau » (proposée en plus
de la musique choisie), « aussi » (jouée en plus de la musique choisie), titre, url (le fichier, ou une archive zip et « fichier » le nom dedans), page, auteur, licence. Les licences
se recopient à la main dans godot/audio/LICENCES.md.
Seule la bibliothèque standard de Python est utilisée.
"""
import html
import http.server
import io
import json
import os
import re
import shutil
import sys
import threading
import time
import zipfile

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ICI)
import essai  # noqa: E402  (les requêtes, le dossier principal)
from sons import FACTIONS  # noqa: E402

EN_COURS = essai.EN_COURS
# chaque sujet : sa liste, ses groupes (une musique par groupe), son nom dans sounds.json et dans godot/audio/musique/
SUJETS = {
    "villes": {"liste": "musiques-villes.json", "page": "musiques", "groupes": FACTIONS, "cle": "town", "fichier": "ville",
               "titre": "Les musiques des villes", "quoi": "Une musique par faction, jouée dans l'écran de la ville."},
    "carte": {"liste": "musiques-carte.json", "page": "musiques-carte", "cle": "map", "fichier": "carte",
              "groupes": {"grass": "Herbe", "sand": "Désert", "swamp": "Marais", "rock": "Roche", "burnt": "Sol brûlé",
                         "snow": "Neige", "water": "Mer"},
              "titre": "Les musiques de la carte",
              "quoi": "Une musique par terrain, jouée sur la carte selon le terrain autour du héros (la mer : en bateau)."},
    "menu": {"liste": "musiques-menu.json", "page": "musiques-menu", "cle": "", "fichier": "musique",
             "groupes": {"menu": "Menu principal"}, "titre": "La musique du menu",
             "quoi": "Toutes les musiques notées 3 étoiles ou plus qui ne servent encore nulle part, les mieux notées d'abord."},
    "ambiances": {"liste": "ambiances.json", "page": "ambiances", "cle": "", "fichier": "ambiance",
                  "section": "ambience", "dossier": "ambiance",
                  "groupes": {"grass": "Herbe", "sand": "Désert", "swamp": "Marais", "rock": "Roche", "snow": "Neige",
                              "burnt": "Sol brûlé", "water": "Mer"},
                  "titre": "Les ambiances",
                  "quoi": "Un fond sonore par sol, sans musique, discret sous la musique : sur la carte (le sol autour du "
                          "héros, la mer en bateau) et au combat (le sol du champ)."},
}
SUJET = SUJETS["villes"]
LISTE = DOSSIER = REPONSES = ""


def choisir(nom):
    global SUJET, LISTE, DOSSIER, REPONSES
    SUJET = SUJETS[nom]
    LISTE = os.path.join(ICI, SUJET["liste"])
    DOSSIER = os.path.join(EN_COURS, SUJET["page"])
    REPONSES = os.path.join(EN_COURS, SUJET["page"] + "-reponses.json")


choisir("villes")
GODOT = os.path.normpath(os.path.join(ICI, "..", "..", "godot"))
PORT = 8767  # ni celui des revues d'images (8765), ni celui des sons (8766)


def liste():
    with open(LISTE, encoding="utf-8") as f:
        return json.load(f)["musiques"]


def existant(ident):
    for ext in essai.EXTENSIONS:
        chemin = os.path.join(DOSSIER, ident + ext)
        if os.path.exists(chemin):
            return chemin
    return None


def reponses():
    try:
        with open(REPONSES, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def telecharger(musiques):
    os.makedirs(DOSSIER, exist_ok=True)
    archives = {}
    for m in musiques:
        if existant(m["id"]):
            continue
        try:
            if m["url"] not in archives:
                if "bigsoundbank.com" in m["url"]:
                    time.sleep(4)  # BigSoundBank répond 403 à trop de demandes rapprochées
                # Wikimedia refuse un nom de navigateur vague (429) : il demande un nom d'outil
                agent = "HoMM-outils-audio/1.0 (jeu personnel)" if "wikimedia.org" in m["url"] else "Mozilla/5.0"
                _, archives[m["url"]], _ = essai.requete(m["url"], entetes={"User-Agent": agent})
            contenu, nom = archives[m["url"]], m["url"]
            if m.get("fichier"):
                with zipfile.ZipFile(io.BytesIO(contenu)) as z:
                    contenu = z.read(m["fichier"])
                nom = m["fichier"]
        except (RuntimeError, KeyError, zipfile.BadZipFile) as err:
            print(f"  {m['id']} : échec, {err}")
            continue
        # « format » : quand l'adresse ne dit pas le format (Free Music Archive sert ses mp3 en .bin)
        ext = m.get("format") or os.path.splitext(nom.split("?")[0])[1].lower()
        if ext not in essai.EXTENSIONS:
            print(f"  {m['id']} : format inconnu ({ext})")
            continue
        with open(os.path.join(DOSSIER, m["id"] + ext), "wb") as f:
            f.write(contenu)
        print(f"  {m['id']}{ext}  {len(contenu) // 1024} Ko")


def page(musiques, tout=False):
    parties, finies = [], []
    deja = reponses()
    ids = {m["id"] for m in musiques}
    for faction, nom in SUJET["groupes"].items():
        lignes = []
        for m in (m for m in musiques if m["faction"] == faction):
            # d'un tour à l'autre, ne restent que la piste choisie et celles de 4 étoiles ou plus ; une piste pas encore
            # notée reste tant que sa faction n'a pas de musique choisie, ou si elle est marquée « nouveau » (proposée
            # en plus de la musique choisie)
            etoiles = deja.get(f"etoiles:{m['id']}", m.get("etoiles"))
            if not tout and deja.get(faction) != m["id"] and (etoiles and etoiles < 4 or not etoiles and not m.get("nouveau") and deja.get(faction) in ids):
                continue
            chemin = existant(m["id"])
            if chemin is None:
                continue
            relatif = os.path.relpath(chemin, EN_COURS).replace(os.sep, "/")
            lignes.append(
                f"<div class=piste><b>{html.escape(m['titre'])}</b> <span class=info>{html.escape(m['auteur'])}, "
                f"{html.escape(m['licence'])}{', ' + html.escape(m['duree']) if m.get('duree') else ''} · "
                f"<a href=\"{html.escape(m['page'])}\" target=_blank>page</a></span>"
                f"<audio controls preload=none src=\"{relatif}?v={int(os.path.getmtime(chemin))}\"></audio>"
                f"<div class=avis><span class=etoiles data-id=\"{m['id']}\">"
                + "".join(f"<button data-n={n} title=\"{n} sur 5\">★</button>" for n in range(1, 6))
                + f"</span><input type=text data-id=\"{m['id']}\" placeholder=\"ce que j'en pense\"></div>"
                f"<label><input type=radio name=\"{faction}\" value=\"{m['id']}\"> choisir celle-ci</label></div>")
        if lignes:
            # une faction dont la musique est choisie et installée passe en bas, pour noter encore ses pistes
            choisie = not tout and deja.get(faction) in ids
            (finies if choisie else parties).append(
                f"<section><h2>{nom}{' <span class=info>(dans le jeu)</span>' if choisie else ''}</h2>{''.join(lignes)}"
                f"<label><input type=radio name=\"{faction}\" value=aucune> aucune ne va : en chercher d'autres</label>"
                f"<textarea data-id=\"{faction}\" placeholder=\"ce qui plaît, ce qui manque\"></textarea></section>")
    contenu = f"""<!doctype html><html lang=fr><meta charset=utf-8><title>{SUJET["titre"]}</title>
<style>
body{{font-family:system-ui,sans-serif;background:#1d1a16;color:#e8e0d0;margin:16px;max-width:900px}}
h1{{font-size:22px}} h2{{font-size:18px;margin:0 0 8px;color:#d8b870}}
section{{border:1px solid #4a4035;padding:12px;margin:16px 0}} .piste{{margin:10px 0}}
.info{{font-size:12px;color:#a89c88}} a{{color:#d8b870}} audio{{width:100%;display:block;margin:4px 0}}
label{{display:block;margin:2px 0}} .avis{{display:flex;gap:8px;align-items:center}}
.etoiles button{{background:none;border:0;font-size:22px;color:#4a4035;cursor:pointer;padding:0 1px}}
.etoiles button.allume{{color:#e8b830}} .avis input{{flex:1;background:#2a2620;color:#e8e0d0;border:1px solid #4a4035;padding:4px}}
textarea{{width:100%;height:44px;background:#2a2620;color:#e8e0d0;border:1px solid #4a4035;margin-top:6px}}
#etat{{position:fixed;top:8px;right:12px;font-size:13px;color:#a89c88}}
</style>
<h1>{SUJET["titre"]}</h1>
<p>{SUJET["quoi"]} Choisissez celle qui va le mieux, ou « aucune » avec un mot
sur ce que vous aimeriez. Chaque piste peut recevoir de 1 à 5 étoiles et un avis (cliquer de nouveau sur la même
étoile l'efface) : ils sont gardés dans <code>tools/audio/{SUJET["liste"]}</code> à l'installation. Tout
s'enregistre tout seul.</p>
<div id=etat></div>
{''.join(parties + finies)}
<script>
const GARDES = {json.dumps({m["id"]: {k: m[k] for k in ("etoiles", "avis") if m.get(k)} for m in musiques}, ensure_ascii=False)};
let r = {{}};
const etat = document.getElementById("etat");
async function charger() {{
  try {{
    const rep = await fetch("{SUJET["page"]}-reponses.json", {{cache: "no-store"}});
    if (rep.ok) r = await rep.json();
  }} catch (e) {{ etat.textContent = "notes pas chargées : le serveur est-il lancé ? Rechargez la page quand il repart"; }}
  document.querySelectorAll("input[type=radio]").forEach(b => {{ if (r[b.name] === b.value) b.checked = true; }});
  document.querySelectorAll("textarea").forEach(t => {{ t.value = r["note:" + t.dataset.id] || ""; }});
  document.querySelectorAll(".avis input").forEach(t => {{ t.value = r["avis:" + t.dataset.id] ?? (GARDES[t.dataset.id]?.avis || ""); }});
  document.querySelectorAll(".etoiles").forEach(e => {{
    if (r["etoiles:" + e.dataset.id] === undefined && GARDES[e.dataset.id]?.etoiles) r["etoiles:" + e.dataset.id] = GARDES[e.dataset.id].etoiles;
    allumer(e);
  }});
}}
function allumer(e) {{
  const n = r["etoiles:" + e.dataset.id] || 0;
  e.querySelectorAll("button").forEach(b => b.classList.toggle("allume", +b.dataset.n <= n));
}}
document.querySelectorAll(".etoiles button").forEach(b => b.addEventListener("click", () => {{
  const e = b.parentElement, cle = "etoiles:" + e.dataset.id, n = +b.dataset.n;
  changer(cle, r[cle] === n ? undefined : n);
  allumer(e);
}}));
document.querySelectorAll(".avis input").forEach(t =>
  t.addEventListener("input", () => changer("avis:" + t.dataset.id, t.value)));
// seuls les changements partent vers le serveur, qui les ajoute aux réponses déjà écrites : une page ouverte depuis
// longtemps ou mal chargée n'efface aucune note ; si le serveur ne répond pas, on réessaie jusqu'à ce qu'il revienne
let attente = null, modifs = {{}};
function changer(cle, valeur) {{
  if (valeur === undefined) delete r[cle]; else r[cle] = valeur;
  modifs[cle] = valeur ?? null;
  garder();
}}
function garder() {{
  clearTimeout(attente);
  attente = setTimeout(async () => {{
    const envoi = modifs;
    modifs = {{}};
    try {{
      const rep = await fetch("reponses?{SUJET["page"]}", {{method: "POST", body: JSON.stringify(envoi)}});
      if (!rep.ok) throw new Error();
      etat.textContent = "enregistré";
    }} catch (e) {{
      modifs = {{...envoi, ...modifs}};
      etat.textContent = "pas encore enregistré : le serveur est-il lancé ? Nouvel essai dans 3 secondes";
      attente = setTimeout(garder, 3000);
    }}
  }}, 300);
}}
document.querySelectorAll("input[type=radio]").forEach(b =>
  b.addEventListener("change", () => changer(b.name, b.value)));
document.querySelectorAll("textarea").forEach(t =>
  t.addEventListener("input", () => changer("note:" + t.dataset.id, t.value)));
// une seule lecture à la fois
document.querySelectorAll("audio").forEach(a => a.addEventListener("play", () =>
  document.querySelectorAll("audio").forEach(b => {{ if (b !== a) b.pause(); }})));
charger();
</script></html>"""
    os.makedirs(EN_COURS, exist_ok=True)
    nom = SUJET["page"] + ".html"
    with open(os.path.join(EN_COURS, nom), "w", encoding="utf-8") as f:
        f.write(contenu)
    print(f"http://localhost:{PORT}/{nom}  ({os.path.join(EN_COURS, nom)})")


VERROU = threading.Lock()


def ecrire_json(cible, donnees):
    """Écrit d'un coup : un arrêt du serveur en pleine écriture laisse l'ancien fichier entier."""
    provisoire = cible + ".tmp"
    with open(provisoire, "w", encoding="utf-8") as f:
        json.dump(donnees, f, ensure_ascii=False, indent=2)
    os.replace(provisoire, cible)


class Serveur(essai.ServeurAudio):
    def do_POST(self):
        chemin, _, page = self.path.partition("?")
        if chemin != "/reponses":
            self.send_error(404)
            return
        donnees = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))))
        # une page de revue d'images (champ « page ») ouverte ici par erreur : ses réponses vont dans son propre fichier
        if isinstance(donnees, dict) and "page" in donnees:
            ecrire_json(os.path.join(EN_COURS, re.sub(r"[^\w-]", "", donnees["page"]) + ".json"), donnees)
        else:
            cible = os.path.join(EN_COURS, (re.sub(r"[^\w-]", "", page) or "musiques") + "-reponses.json")
            # les changements s'ajoutent aux réponses déjà écrites (null : retirer) ; un fichier illisible n'est pas écrasé
            with VERROU:
                try:
                    with open(cible, encoding="utf-8") as f:
                        actuel = json.load(f)
                except FileNotFoundError:
                    actuel = {}
                for cle, valeur in donnees.items():
                    if valeur is None:
                        actuel.pop(cle, None)
                    else:
                        actuel[cle] = valeur
                ecrire_json(cible, actuel)
        self.send_response(204)
        self.end_headers()


def installer(musiques):
    r = reponses()
    par_id = {m["id"]: m for m in musiques}
    catalogue_chemin = os.path.join(GODOT, "data", "sounds.json")
    with open(catalogue_chemin, encoding="utf-8") as f:
        catalogue = json.load(f)
    # « section » de sounds.json et dossier de godot/audio/ : la musique, sauf pour les ambiances
    section, sous_dossier = SUJET.get("section", "music"), SUJET.get("dossier", "musique")
    dossier = os.path.join(GODOT, "audio", sous_dossier)
    os.makedirs(dossier, exist_ok=True)
    for faction in SUJET["groupes"]:
        m = par_id.get(r.get(faction, ""))
        if m is None or (source := existant(m["id"])) is None:
            continue
        nom = f"{SUJET['fichier']}-{faction}{os.path.splitext(source)[1]}"
        shutil.copyfile(source, os.path.join(dossier, nom))
        fichiers = [f"{sous_dossier}/{nom}"]
        print(f"  {nom} : « {m['titre']} », {m['auteur']}, {m['licence']}, {m['page']}")
        # les pistes marquées « aussi » s'ajoutent à la choisie : le jeu passe de l'une à l'autre
        autres = [a for a in musiques if a["faction"] == faction and a.get("aussi") and a is not m]
        for n, autre in enumerate(autres, 2):
            source = existant(autre["id"])
            nom = f"{SUJET['fichier']}-{faction}-{n}{os.path.splitext(source)[1]}"
            shutil.copyfile(source, os.path.join(dossier, nom))
            fichiers.append(f"{sous_dossier}/{nom}")
            print(f"  {nom} : « {autre['titre']} », {autre['auteur']}, {autre['licence']}, {autre['page']}")
        catalogue.setdefault(section, {})[f"{SUJET['cle']}-{faction}" if SUJET["cle"] else faction] = fichiers
    with open(catalogue_chemin, "w", encoding="utf-8") as f:
        json.dump(catalogue, f, ensure_ascii=False, indent=2)
        f.write("\n")
    garder_avis(musiques, r)


def garder_avis(musiques, r):
    """Recopie les étoiles et les avis de la page dans la liste (tools/audio/musiques-*.json, suivie par git)."""
    for m in musiques:
        for cle in ("etoiles", "avis"):
            valeur = r.get(f"{cle}:{m['id']}", m.get(cle))
            if valeur:
                m[cle] = valeur
            else:
                m.pop(cle, None)
    with open(LISTE, encoding="utf-8") as f:
        donnees = json.load(f)
    donnees["musiques"] = musiques
    with open(LISTE, "w", encoding="utf-8", newline="\n") as f:
        json.dump(donnees, f, ensure_ascii=False, indent=2)
        f.write("\n")


def main():
    args = sys.argv[1:]
    if args and args[0] in SUJETS:
        choisir(args.pop(0))
    if not args:
        sys.exit(__doc__)
    commande = args[0]
    if commande == "serveur":
        print(f"http://localhost:{PORT}/musiques.html, /musiques-carte.html, /ambiances.html")
        http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Serveur).serve_forever()
    musiques = liste()
    if commande == "telecharger":
        telecharger(musiques)
    elif commande == "page":
        page(musiques, "tout" in args[1:])
    elif commande == "installer":
        installer(musiques)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
