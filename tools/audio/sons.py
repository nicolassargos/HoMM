"""Les sons du jeu, générés chez ElevenLabs d'après une liste (tools/audio/sons-creatures.json), revus à l'oreille sur une
page où l'on marque chaque son « bon » ou « à refaire », puis installés dans le jeu.

    python -I tools/audio/sons.py solde                  ce qui reste chez ElevenLabs (gratuit)
    python -I tools/audio/sons.py cout [id…]             le prix de ce qui manque (gratuit)
    python -I tools/audio/sons.py generer [id…]          génère ce qui manque ; ce qui existe reste
    python -I tools/audio/sons.py refaire id…            garde l'ancienne version (ancien/<id>-vN.mp3) et en génère une autre
    python -I tools/audio/sons.py page                   écrit la page de revue art-candidats/en-cours/sons.html
    python -I tools/audio/sons.py serveur                sert la page (http://localhost:8766/sons.html) ; les réponses
                                                         s'écrivent aussitôt dans en-cours/sons-reponses.json
    python -I tools/audio/sons.py installer              copie les sons « bons » dans godot/audio/combat/ et les ajoute à
                                                         godot/data/sounds.json

L'id d'un son est son nom dans le jeu (« liche-shoot », « hydre-death »…). Les fichiers attendent dans
art-candidats/en-cours/sons/ du dossier principal du dépôt. La clé : ELEVENLABS_API_KEY (variable, ou .env.local du
dossier de travail ; jamais affichée). Prix relevé le 2026-11-16 : environ 30 crédits par son (10 000 gratuits par mois).
Seule la bibliothèque standard de Python est utilisée.
"""
import html
import http.server
import json
import os
import re
import shutil
import sys

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ICI)
import essai  # noqa: E402  (la clé, les requêtes, le dossier principal)

LISTE = os.path.join(ICI, "sons-creatures.json")
EN_COURS = essai.EN_COURS
DOSSIER = os.path.join(EN_COURS, "sons")
ANCIEN = os.path.join(DOSSIER, "ancien")
REPONSES = os.path.join(EN_COURS, "sons-reponses.json")
GODOT = os.path.normpath(os.path.join(ICI, "..", "..", "godot"))
PORT = 8766  # pas celui de tools/art/revue.py et pieds.py (8765), qui écrivent aussi « /reponses »
CREDITS_PAR_SON = 30

FACTIONS = {"chateau": "Château", "morts-vivants": "Morts-vivants", "barbares": "Barbares", "foret": "Forêt",
            "tour": "Tour de magie", "donjon": "Donjon", "enfer": "Enfer", "nains": "Nains", "neutre": "Neutres"}


def liste():
    with open(LISTE, encoding="utf-8") as f:
        return json.load(f)["sons"]


def chemin(ident):
    return os.path.join(DOSSIER, ident + ".mp3")


def anciens(ident):
    if not os.path.isdir(ANCIEN):
        return []
    motif = re.compile(re.escape(ident) + r"-v(\d+)\.mp3$")
    return sorted((f for f in os.listdir(ANCIEN) if motif.match(f)), key=lambda f: int(motif.match(f).group(1)))


def reponses():
    try:
        with open(REPONSES, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def generer(sons):
    cle = essai.cle("ELEVENLABS_API_KEY")
    os.makedirs(DOSSIER, exist_ok=True)
    for s in sons:
        if os.path.exists(chemin(s["id"])):
            continue
        corps = {"text": s["texte"] + ", no music", "model_id": "eleven_text_to_sound_v3",
                 "duration_seconds": max(0.5, s["duree"]), "prompt_influence": 0.5}
        try:
            _, audio, _ = essai.requete("https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128",
                                        json.dumps(corps).encode(), {"xi-api-key": cle, "Content-Type": "application/json"},
                                        "POST")
        except RuntimeError as err:
            print(f"  {s['id']} : échec, {err}")
            continue
        with open(chemin(s["id"]), "wb") as f:
            f.write(audio)
        print(f"  {s['id']}.mp3  {len(audio) // 1024} Ko")


def refaire(sons):
    os.makedirs(ANCIEN, exist_ok=True)
    for s in sons:
        if os.path.exists(chemin(s["id"])):
            os.replace(chemin(s["id"]), os.path.join(ANCIEN, f"{s['id']}-v{len(anciens(s['id'])) + 1}.mp3"))
    generer(sons)


def page(sons):
    parties = []
    deja = reponses()
    for faction, nom in FACTIONS.items():
        lignes = []
        for s in (s for s in sons if s["faction"] == faction):
            # un son marqué « bon » (ou dont on garde une version d'avant) est installé : il ne revient pas sur la page
            if not os.path.exists(chemin(s["id"])) or deja.get(s["id"], "refaire") != "refaire":
                continue
            versions = "".join(f"<div class=ancien>{html.escape(a)} <audio controls preload=none src=\"sons/ancien/{a}\"></audio>"
                               f"<label><input type=radio name=\"{s['id']}\" value=\"{a}\"> garder celle-ci</label></div>"
                               for a in anciens(s["id"]))
            lignes.append(
                f"<tr><th>{html.escape(s['nom'])}<div class=texte>{html.escape(s['texte'])}</div></th>"
                f"<td><audio controls preload=none src=\"sons/{s['id']}.mp3?v={int(os.path.getmtime(chemin(s['id'])))}\"></audio>{versions}</td>"
                f"<td><label><input type=radio name=\"{s['id']}\" value=bon> bon</label>"
                f"<label><input type=radio name=\"{s['id']}\" value=refaire> à refaire</label></td>"
                f"<td><textarea data-id=\"{s['id']}\" placeholder=\"ce qui ne va pas\"></textarea></td></tr>")
        if lignes:
            parties.append(f"<h2>{nom}</h2><table>{''.join(lignes)}</table>")
    contenu = f"""<!doctype html><html lang=fr><meta charset=utf-8><title>Sons des créatures</title>
<style>
body{{font-family:system-ui,sans-serif;background:#1d1a16;color:#e8e0d0;margin:16px}}
h1{{font-size:22px}} h2{{font-size:18px;margin-top:28px;color:#d8b870}} table{{border-collapse:collapse;width:100%}}
td,th{{border:1px solid #4a4035;padding:8px;vertical-align:top;text-align:left}} th{{width:30%}}
th .texte{{font-weight:normal;font-size:12px;color:#a89c88}} label{{display:block;margin:2px 0}}
audio{{width:240px;display:block;margin-bottom:4px}} .ancien{{font-size:11px;color:#a89c88;margin-top:6px}}
textarea{{width:100%;height:44px;background:#2a2620;color:#e8e0d0;border:1px solid #4a4035}}
#etat{{position:fixed;top:8px;right:12px;font-size:13px;color:#a89c88}}
</style>
<h1>Les sons des créatures au combat</h1>
<p>Écoutez chaque son, marquez-le « bon » ou « à refaire » (avec un mot sur ce qui ne va pas). Les réponses s'enregistrent
toutes seules : il n'y a rien à télécharger.</p>
<div id=etat></div>
{''.join(parties)}
<script>
let r = {{}};
const etat = document.getElementById("etat");
async function charger() {{
  try {{ r = await (await fetch("sons-reponses.json", {{cache: "no-store"}})).json(); }} catch (e) {{ r = {{}}; }}
  document.querySelectorAll("input[type=radio]").forEach(b => {{ if (r[b.name] === b.value) b.checked = true; }});
  document.querySelectorAll("textarea").forEach(t => {{ t.value = r["note:" + t.dataset.id] || ""; }});
}}
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
    with open(os.path.join(EN_COURS, "sons.html"), "w", encoding="utf-8") as f:
        f.write(contenu)
    print(f"http://localhost:{PORT}/sons.html  ({os.path.join(EN_COURS, 'sons.html')})")


class Serveur(essai.ServeurAudio):
    def do_POST(self):
        if self.path != "/reponses":
            self.send_error(404)
            return
        corps = self.rfile.read(int(self.headers.get("Content-Length", 0)))
        donnees = json.loads(corps)
        # une page de revue d'images (champ « page ») ouverte ici par erreur : ses réponses vont dans son propre fichier
        cible = (os.path.join(EN_COURS, re.sub(r"[^\w-]", "", donnees["page"]) + ".json")
                 if isinstance(donnees, dict) and "page" in donnees else REPONSES)
        with open(cible, "w", encoding="utf-8") as f:
            json.dump(donnees, f, ensure_ascii=False, indent=2)
        self.send_response(204)
        self.end_headers()


def serveur():
    print(f"http://localhost:{PORT}/sons.html")
    http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Serveur).serve_forever()


def installer(sons):
    r = reponses()
    catalogue_chemin = os.path.join(GODOT, "data", "sounds.json")
    with open(catalogue_chemin, encoding="utf-8") as f:
        catalogue = json.load(f)
    dossier = os.path.join(GODOT, "audio", "combat")
    os.makedirs(dossier, exist_ok=True)
    poses = 0
    for s in sons:
        choix = r.get(s["id"])
        if choix == "bon":
            source = chemin(s["id"])
        elif choix and choix.endswith(".mp3"):
            source = os.path.join(ANCIEN, choix)  # une version d'avant, gardée
        else:
            continue
        shutil.copyfile(source, os.path.join(dossier, s["id"] + ".mp3"))
        catalogue["sounds"][s["id"]] = [f"combat/{s['id']}.mp3"]
        poses += 1
    with open(catalogue_chemin, "w", encoding="utf-8") as f:
        json.dump(catalogue, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"{poses} sons installés ; à refaire : "
          + (", ".join(s["id"] for s in sons if r.get(s["id"]) == "refaire") or "aucun"))


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    commande, ids = sys.argv[1], set(sys.argv[2:])
    sons = liste()
    inconnus = ids - {s["id"] for s in sons}
    if inconnus:
        sys.exit("Inconnus : " + ", ".join(sorted(inconnus)))
    choisis = [s for s in sons if not ids or s["id"] in ids]
    if commande == "solde":
        print(essai.eleven_solde())
    elif commande == "cout":
        manquants = [s for s in choisis if not os.path.exists(chemin(s["id"]))]
        print(f"{len(manquants)} sons à générer ≈ {len(manquants) * CREDITS_PAR_SON} crédits ElevenLabs"
              " (10 000 gratuits par mois)")
    elif commande == "generer":
        generer(choisis)
    elif commande == "refaire":
        if not ids:
            sys.exit("Nommer les sons à refaire.")
        refaire(choisis)
    elif commande == "page":
        page(sons)
    elif commande == "serveur":
        serveur()
    elif commande == "installer":
        installer(sons)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
