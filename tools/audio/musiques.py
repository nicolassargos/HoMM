"""Les musiques des villes, prises dans les bibliothèques libres (CC0 ou CC-BY) d'après une liste
(tools/audio/musiques-villes.json), écoutées sur une page où l'on choisit une musique par faction, puis installées.

    python -I tools/audio/musiques.py telecharger        télécharge ce qui manque (art-candidats/en-cours/musiques/)
    python -I tools/audio/musiques.py page               écrit la page d'écoute art-candidats/en-cours/musiques.html
    python -I tools/audio/musiques.py serveur            sert la page (http://localhost:8767/musiques.html) ; les choix
                                                         s'écrivent aussitôt dans en-cours/musiques-reponses.json
    python -I tools/audio/musiques.py installer          copie chaque musique choisie dans godot/audio/musique/ville-<faction>
                                                         et la note dans godot/data/sounds.json (« town-<faction> »)

Chaque élément de la liste : id (« <faction>-<n> »), faction, titre, url (le fichier, ou une archive zip et « fichier »
le nom dedans), page, auteur, licence. Les licences se recopient à la main dans godot/audio/LICENCES.md.
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
import zipfile

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ICI)
import essai  # noqa: E402  (les requêtes, le dossier principal)
from sons import FACTIONS  # noqa: E402

LISTE = os.path.join(ICI, "musiques-villes.json")
EN_COURS = essai.EN_COURS
DOSSIER = os.path.join(EN_COURS, "musiques")
REPONSES = os.path.join(EN_COURS, "musiques-reponses.json")
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
                _, archives[m["url"]], _ = essai.requete(m["url"], entetes={"User-Agent": "Mozilla/5.0"})
            contenu, nom = archives[m["url"]], m["url"]
            if m.get("fichier"):
                with zipfile.ZipFile(io.BytesIO(contenu)) as z:
                    contenu = z.read(m["fichier"])
                nom = m["fichier"]
        except (RuntimeError, KeyError, zipfile.BadZipFile) as err:
            print(f"  {m['id']} : échec, {err}")
            continue
        ext = os.path.splitext(nom.split("?")[0])[1].lower()
        if ext not in essai.EXTENSIONS:
            print(f"  {m['id']} : format inconnu ({ext})")
            continue
        with open(os.path.join(DOSSIER, m["id"] + ext), "wb") as f:
            f.write(contenu)
        print(f"  {m['id']}{ext}  {len(contenu) // 1024} Ko")


def page(musiques):
    parties, finies = [], []
    deja = reponses()
    ids = {m["id"] for m in musiques}
    for faction, nom in FACTIONS.items():
        lignes = []
        for m in (m for m in musiques if m["faction"] == faction):
            # d'un tour à l'autre, ne restent que la piste choisie et celles de 4 étoiles ou plus ; une piste pas encore
            # notée reste tant que sa faction n'a pas de musique choisie
            etoiles = deja.get(f"etoiles:{m['id']}", m.get("etoiles"))
            if deja.get(faction) != m["id"] and (etoiles and etoiles < 4 or not etoiles and deja.get(faction) in ids):
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
            choisie = deja.get(faction) in ids
            (finies if choisie else parties).append(
                f"<section><h2>{nom}{' <span class=info>(dans le jeu)</span>' if choisie else ''}</h2>{''.join(lignes)}"
                f"<label><input type=radio name=\"{faction}\" value=aucune> aucune ne va : en chercher d'autres</label>"
                f"<textarea data-id=\"{faction}\" placeholder=\"ce qui plaît, ce qui manque\"></textarea></section>")
    contenu = f"""<!doctype html><html lang=fr><meta charset=utf-8><title>Musiques des villes</title>
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
<h1>Les musiques des villes</h1>
<p>Une musique par faction, jouée dans l'écran de la ville. Choisissez celle qui va le mieux, ou « aucune » avec un mot
sur ce que vous aimeriez. Chaque piste peut recevoir de 1 à 5 étoiles et un avis (cliquer de nouveau sur la même
étoile l'efface) : ils sont gardés dans <code>tools/audio/musiques-villes.json</code> à l'installation. Tout
s'enregistre tout seul.</p>
<div id=etat></div>
{''.join(parties + finies)}
<script>
const GARDES = {json.dumps({m["id"]: {k: m[k] for k in ("etoiles", "avis") if m.get(k)} for m in musiques}, ensure_ascii=False)};
let r = {{}};
const etat = document.getElementById("etat");
async function charger() {{
  try {{ r = await (await fetch("musiques-reponses.json", {{cache: "no-store"}})).json(); }} catch (e) {{ r = {{}}; }}
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
  if (r[cle] === n) delete r[cle]; else r[cle] = n;
  allumer(e); garder();
}}));
document.querySelectorAll(".avis input").forEach(t =>
  t.addEventListener("input", () => {{ r["avis:" + t.dataset.id] = t.value; garder(); }}));
let attente = null;
function garder() {{
  clearTimeout(attente);
  attente = setTimeout(async () => {{
    try {{
      await fetch("reponses", {{method: "POST", body: JSON.stringify(r)}});
      etat.textContent = "enregistré";
    }} catch (e) {{ etat.textContent = "pas enregistré : le serveur est-il lancé ?"; }}
  }}, 300);
}}
document.querySelectorAll("input[type=radio]").forEach(b =>
  b.addEventListener("change", () => {{ r[b.name] = b.value; garder(); }}));
document.querySelectorAll("textarea").forEach(t =>
  t.addEventListener("input", () => {{ r["note:" + t.dataset.id] = t.value; garder(); }}));
// une seule lecture à la fois
document.querySelectorAll("audio").forEach(a => a.addEventListener("play", () =>
  document.querySelectorAll("audio").forEach(b => {{ if (b !== a) b.pause(); }})));
charger();
</script></html>"""
    os.makedirs(EN_COURS, exist_ok=True)
    with open(os.path.join(EN_COURS, "musiques.html"), "w", encoding="utf-8") as f:
        f.write(contenu)
    print(f"http://localhost:{PORT}/musiques.html  ({os.path.join(EN_COURS, 'musiques.html')})")


class Serveur(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=EN_COURS, **kwargs)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_POST(self):
        if self.path != "/reponses":
            self.send_error(404)
            return
        donnees = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))))
        # une page de revue d'images (champ « page ») ouverte ici par erreur : ses réponses vont dans son propre fichier
        cible = (os.path.join(EN_COURS, re.sub(r"[^\w-]", "", donnees["page"]) + ".json")
                 if isinstance(donnees, dict) and "page" in donnees else REPONSES)
        with open(cible, "w", encoding="utf-8") as f:
            json.dump(donnees, f, ensure_ascii=False, indent=2)
        self.send_response(204)
        self.end_headers()

    def log_message(self, *args):
        pass


def installer(musiques):
    r = reponses()
    par_id = {m["id"]: m for m in musiques}
    catalogue_chemin = os.path.join(GODOT, "data", "sounds.json")
    with open(catalogue_chemin, encoding="utf-8") as f:
        catalogue = json.load(f)
    dossier = os.path.join(GODOT, "audio", "musique")
    os.makedirs(dossier, exist_ok=True)
    for faction in FACTIONS:
        m = par_id.get(r.get(faction, ""))
        if m is None or (source := existant(m["id"])) is None:
            continue
        nom = f"ville-{faction}{os.path.splitext(source)[1]}"
        shutil.copyfile(source, os.path.join(dossier, nom))
        catalogue["music"][f"town-{faction}"] = [f"musique/{nom}"]
        print(f"  {nom} : « {m['titre']} », {m['auteur']}, {m['licence']}, {m['page']}")
    with open(catalogue_chemin, "w", encoding="utf-8") as f:
        json.dump(catalogue, f, ensure_ascii=False, indent=2)
        f.write("\n")
    garder_avis(musiques, r)


def garder_avis(musiques, r):
    """Recopie les étoiles et les avis de la page dans tools/audio/musiques-villes.json (suivi par git)."""
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
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    commande, musiques = sys.argv[1], liste()
    if commande == "telecharger":
        telecharger(musiques)
    elif commande == "page":
        page(musiques)
    elif commande == "serveur":
        print(f"http://localhost:{PORT}/musiques.html")
        http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Serveur).serve_forever()
    elif commande == "installer":
        installer(musiques)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
