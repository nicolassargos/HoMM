"""Les pas des créatures au combat : un son par façon de marcher et par sol (tools/audio/sons-pas.json), demandés à
ElevenLabs ou pris dans les bibliothèques libres, revus sur une page où chaque proposition se joue comme une marche, puis
installés dans le jeu.

    python -I tools/audio/pas.py cout [id…]              le prix de ce qui manque (gratuit)
    python -I tools/audio/pas.py generer [id…]           génère chez ElevenLabs ce qui manque ; ce qui existe reste
    python -I tools/audio/pas.py recouper [id…]          coupe de nouveau les marches reçues en pas seuls (gratuit)
    python -I tools/audio/pas.py refaire id…           garde l'ancienne proposition (ancien/) et en génère une autre
    python -I tools/audio/pas.py libres                  télécharge les pas libres (champ « libres » de la liste)
    python -I tools/audio/pas.py page                    écrit la page de revue art-candidats/en-cours/pas.html
    python -I tools/audio/pas.py serveur                 sert la page (http://localhost:8768/pas.html) ; les réponses
                                                         s'écrivent aussitôt dans en-cours/pas-reponses.json
    python -I tools/audio/pas.py installer               copie les pas choisis dans godot/audio/pas/ et les ajoute à
                                                         godot/data/sounds.json (« step-<marche>-<sol> »)

ElevenLabs rend une courte marche (quelques pas, en PCM) : elle est coupée en pas seuls (un fichier .wav chacun), que le
jeu tire au hasard à chaque pas. Les fichiers attendent dans art-candidats/en-cours/pas/<id>/<source>/ du dossier
principal du dépôt. La clé : ELEVENLABS_API_KEY (jamais affichée). Seule la bibliothèque standard de Python est utilisée.
"""
import array
import html
import http.server
import io
import json
import os
import shutil
import sys
import threading
import wave
import zipfile

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ICI)
import essai  # noqa: E402  (la clé, les requêtes, le dossier principal, le serveur des fichiers audio)

LISTE = os.path.join(ICI, "sons-pas.json")
EN_COURS = essai.EN_COURS
DOSSIER = os.path.join(EN_COURS, "pas")
REPONSES = os.path.join(EN_COURS, "pas-reponses.json")
GODOT = os.path.normpath(os.path.join(ICI, "..", "..", "godot"))
PORT = 8768  # pas ceux des images (8765), de sons.py (8766) ni de musiques.py (8767)
CREDITS_PAR_SON = 40  # relevé : le prix suit la durée demandée
TAUX = 44100          # la sortie PCM d'ElevenLabs : 16 bits, mono
TAUX_JEU = 22050      # les pas du jeu : assez pour un bruit sourd, deux fois plus légers
VERROU = threading.Lock()


def liste():
    with open(LISTE, encoding="utf-8") as f:
        d = json.load(f)
    return d["marches"], d["sols"], d["sons"]


def dossier(ident, source):
    return os.path.join(DOSSIER, ident, source)


def pas_de(ident, source):
    """Les fichiers des pas seuls d'une proposition, dans l'ordre."""
    d = dossier(ident, source)
    return sorted(f for f in os.listdir(d) if f.startswith("pas-")) if os.path.isdir(d) else []


def reponses():
    try:
        with open(REPONSES, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


# --- couper une marche en pas

def enveloppe(echantillons, fenetre):
    """Le niveau moyen (valeur absolue) de chaque fenêtre."""
    return [sum(abs(x) for x in echantillons[i:i + fenetre]) / fenetre for i in range(0, len(echantillons) - fenetre, fenetre)]


def couper(echantillons, taux=TAUX):
    """
    Les pas : les passages où le niveau (lissé sur 50 ms) dépasse 8 % du plus fort, séparés d'au moins 0,2 s de calme
    (un pas lourd et ses éboulis restent un seul pas). Rend (début, fin) en échantillons.
    """
    fenetre = taux // 100  # 10 ms
    brut = enveloppe(echantillons, fenetre)
    if not brut:
        return []
    env = [sum(brut[max(0, i - 2):i + 3]) / len(brut[max(0, i - 2):i + 3]) for i in range(len(brut))]
    seuil = max(env) * 0.08
    pas, debut, calme = [], None, 0
    for i, e in enumerate(env + [0] * 20):
        if e >= seuil:
            if debut is None:
                debut = i
            calme = 0
        elif debut is not None:
            calme += 1
            if calme >= 20:
                pas.append((debut, i - calme + 1))
                debut = None
    # remonter un peu avant la montée, pour garder l'attaque du pas ; ce qui est trop court n'est pas un pas
    return [(max(0, (d - 3) * fenetre), min(len(echantillons), (f + 5) * fenetre)) for d, f in pas if f - d >= 4]


def ecrire_wav(chemin, echantillons, taux):
    with wave.open(chemin, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(taux)
        w.writeframes(array.array("h", echantillons).tobytes())


def decouper(pcm, cible):
    """Écrit la marche entière (marche.wav) et chacun de ses pas (pas-N.wav, réduits à 22 050 Hz, fondus à la fin)."""
    ech = array.array("h")
    ech.frombytes(pcm[:len(pcm) // 2 * 2])
    ech = list(ech)
    os.makedirs(cible, exist_ok=True)
    ecrire_wav(os.path.join(cible, "marche.wav"), ech, TAUX)
    pas = couper(ech)
    tranches = len(pas) < 3
    if tranches:
        # un bruit continu (des sabots dans le sable, un corps qui rampe) : des tranches de 0,4 s de ce qui sonne
        tranche = int(TAUX * 0.4)
        pas = [(d, d + tranche) for debut, fin in pas for d in range(debut, fin - tranche // 2, tranche)]
    n = 0
    for debut, fin in pas:
        morceau = ech[debut:min(fin, debut + int(TAUX * 0.7))]
        # moyenne de deux échantillons : moitié moins, sans repli des aigus
        petit = [(morceau[j] + morceau[j + 1]) // 2 for j in range(0, len(morceau) - 1, 2)]
        fondu = min(len(petit), TAUX_JEU // 25)  # 40 ms, au début (une tranche coupée en plein son) et à la fin
        for j in range(fondu):
            petit[-1 - j] = petit[-1 - j] * j // fondu
            if tranches:
                petit[j] = petit[j] * j // fondu
        n += 1
        ecrire_wav(os.path.join(cible, f"pas-{n}.wav"), petit, TAUX_JEU)
    return n


def texte(s, marches, sols):
    if "texte" in s:
        return s["texte"]
    return marches[s["marche"]]["texte"].format(sol=sols[s["sol"]]["texte"])


def generer(sons, marches, sols, source="elevenlabs"):
    cle = essai.cle("ELEVENLABS_API_KEY")
    for s in sons:
        if pas_de(s["id"], source):
            continue
        corps = {"text": texte(s, marches, sols) + ", no music, no voice", "model_id": "eleven_text_to_sound_v3",
                 "duration_seconds": s.get("duree", 2.5), "prompt_influence": 0.6}
        try:
            _, pcm, _ = essai.requete(f"https://api.elevenlabs.io/v1/sound-generation?output_format=pcm_{TAUX}",
                                      json.dumps(corps).encode(), {"xi-api-key": cle, "Content-Type": "application/json"},
                                      "POST")
        except RuntimeError as err:
            print(f"  {s['id']} : échec, {err}")
            continue
        n = decouper(pcm, dossier(s["id"], source))
        print(f"  {s['id']} : {n} pas")


def refaire(sons, marches, sols):
    for s in sons:
        if pas_de(s["id"], "elevenlabs"):
            n = 1 + sum(1 for f in os.listdir(os.path.join(DOSSIER, s["id"])) if f.startswith("ancien-"))
            os.replace(dossier(s["id"], "elevenlabs"), dossier(s["id"], f"ancien-{n}"))
    generer(sons, marches, sols)


def libres(sons):
    """
    Les pas libres : pour chaque son, « libres » : {"source": nom, "url": zip ou fichier (ou "archive" : un zip de
    pas/_archives/), "fichiers": [noms dans le zip], "page", "auteur", "licence"}.
    """
    archives = {}
    for s in sons:
        for libre in s.get("libres", []):
            cible = dossier(s["id"], libre["source"])
            if pas_de(s["id"], libre["source"]):
                continue
            try:
                if libre.get("archive"):
                    # une archive au lien signé qui expire (Kenney) : téléchargée à la main dans pas/_archives/
                    with open(os.path.join(DOSSIER, "_archives", libre["archive"]), "rb") as f:
                        contenu = f.read()
                else:
                    if libre["url"] not in archives:
                        _, archives[libre["url"]], _ = essai.requete(libre["url"], entetes={"User-Agent": "Mozilla/5.0"})
                    contenu = archives[libre["url"]]
            except (RuntimeError, OSError) as err:
                print(f"  {s['id']} ({libre['source']}) : échec, {err}")
                continue
            os.makedirs(cible, exist_ok=True)
            if libre.get("fichiers"):
                with zipfile.ZipFile(io.BytesIO(contenu)) as z:
                    for n, nom in enumerate(libre["fichiers"], 1):
                        with open(os.path.join(cible, f"pas-{n}{os.path.splitext(nom)[1].lower()}"), "wb") as f:
                            f.write(z.read(nom))
            else:
                with open(os.path.join(cible, "pas-1" + os.path.splitext(libre["url"])[1].lower()), "wb") as f:
                    f.write(contenu)
            print(f"  {s['id']} ({libre['source']}) : {len(pas_de(s['id'], libre['source']))} pas")


def sources_de(ident):
    d = os.path.join(DOSSIER, ident)
    return sorted(f for f in os.listdir(d) if pas_de(ident, f)) if os.path.isdir(d) else []


def page(sons, marches, sols):
    deja = reponses()
    parties = []
    for marche, m in marches.items():
        lignes = []
        for s in (s for s in sons if s["marche"] == marche):
            sources = sources_de(s["id"])
            # une proposition choisie est installée : elle ne revient pas sur la page
            if not sources or deja.get(s["id"], "refaire") != "refaire":
                continue
            choix = []
            for source in sources:
                fichiers = [f"pas/{s['id']}/{source}/{f}" for f in pas_de(s["id"], source)]
                entiere = os.path.join(dossier(s["id"], source), "marche.wav")
                ecoute = (f"<audio controls preload=none src=\"pas/{s['id']}/{source}/marche.wav\"></audio>"
                          if os.path.exists(entiere) else "")
                etoiles = "".join(f"<button class=etoile data-id=\"{s['id']}:{source}\" data-n={n}>★</button>" for n in range(1, 6))
                choix.append(
                    f"<div class=source><b>{html.escape(source)}</b> ({len(fichiers)} pas) "
                    f"<button class=marche data-pas='{html.escape(json.dumps(fichiers))}' data-cadence={m['cadence']}>▶ marcher</button>"
                    f"{ecoute}<span class=etoiles>{etoiles}</span>"
                    f"<label><input type=radio name=\"{s['id']}\" value=\"{source}\"> choisir</label></div>")
            lignes.append(
                f"<tr><th>{html.escape(sols[s['sol']]['nom'] if 'sol' in s else s['nom'])}"
                f"<div class=texte>{html.escape(texte(s, marches, sols))}</div></th>"
                f"<td>{''.join(choix)}<label><input type=radio name=\"{s['id']}\" value=refaire> à refaire</label></td>"
                f"<td><textarea data-id=\"{s['id']}\" placeholder=\"ce qui ne va pas\"></textarea></td></tr>")
        if lignes:
            parties.append(f"<h2>{html.escape(m['nom'])}</h2><p class=qui>{html.escape(m['qui'])}</p><table>{''.join(lignes)}</table>")
    contenu = f"""<!doctype html><html lang=fr><meta charset=utf-8><title>Pas au combat</title>
<style>
body{{font-family:system-ui,sans-serif;background:#1d1a16;color:#e8e0d0;margin:16px}}
h1{{font-size:22px}} h2{{font-size:18px;margin-top:28px;color:#d8b870}} .qui{{color:#a89c88;font-size:13px}}
table{{border-collapse:collapse;width:100%}} td,th{{border:1px solid #4a4035;padding:8px;vertical-align:top;text-align:left}}
th{{width:28%}} th .texte{{font-weight:normal;font-size:12px;color:#a89c88}} label{{display:inline-block;margin:2px 8px}}
.source{{margin-bottom:8px}} audio{{width:200px;height:28px;vertical-align:middle}}
button.marche{{background:#3a3228;color:#e8e0d0;border:1px solid #6a5a45;padding:3px 10px;cursor:pointer}}
button.etoile{{background:none;border:none;color:#5a5045;font-size:18px;cursor:pointer;padding:0 1px}}
button.etoile.on{{color:#e8b830}}
textarea{{width:100%;height:44px;background:#2a2620;color:#e8e0d0;border:1px solid #4a4035}}
#etat{{position:fixed;top:8px;right:12px;font-size:13px;color:#a89c88}}
</style>
<h1>Les pas au combat</h1>
<p>« ▶ marcher » joue les pas seuls comme dans le jeu : un pas tiré au hasard à chaque fois, au rythme de cette façon de
marcher, la hauteur un peu changée. Le petit lecteur donne la marche entière, telle que reçue. Choisissez une proposition
par ligne (ou « à refaire », avec un mot sur ce qui ne va pas) ; les étoiles gardent votre avis. Tout s'enregistre seul.</p>
<div id=etat></div>
{''.join(parties)}
<script>
const etat = document.getElementById("etat");
let r = {{}}, modifs = {{}}, charge = false;
async function charger() {{
  try {{ r = await (await fetch("pas-reponses.json", {{cache: "no-store"}})).json(); charge = true; }}
  catch (e) {{ r = {{}}; charge = true; }}
  document.querySelectorAll("input[type=radio]").forEach(b => {{ if (r[b.name] === b.value) b.checked = true; }});
  document.querySelectorAll("textarea").forEach(t => {{ t.value = r["note:" + t.dataset.id] || ""; }});
  etoiles();
}}
function etoiles() {{
  document.querySelectorAll(".etoile").forEach(e => e.classList.toggle("on", (r["etoiles:" + e.dataset.id] || 0) >= +e.dataset.n));
}}
let attente = null;
function changer(cle, valeur) {{
  r[cle] = valeur; modifs[cle] = valeur;
  clearTimeout(attente);
  attente = setTimeout(envoyer, 300);
}}
async function envoyer() {{
  if (!Object.keys(modifs).length) return;
  const lot = modifs; modifs = {{}};
  try {{
    const rep = await fetch("reponses", {{method: "POST", body: JSON.stringify(lot)}});
    if (!rep.ok) throw new Error(rep.status);
    etat.textContent = "enregistré";
  }} catch (e) {{
    modifs = Object.assign(lot, modifs);
    etat.textContent = "pas encore enregistré : le serveur est-il lancé ? Nouvel essai dans 3 secondes";
    setTimeout(envoyer, 3000);
  }}
}}
document.querySelectorAll("input[type=radio]").forEach(b => b.addEventListener("change", () => changer(b.name, b.value)));
document.querySelectorAll("textarea").forEach(t => t.addEventListener("input", () => changer("note:" + t.dataset.id, t.value)));
document.querySelectorAll(".etoile").forEach(e => e.addEventListener("click", () => {{
  const cle = "etoiles:" + e.dataset.id, n = +e.dataset.n;
  changer(cle, r[cle] === n ? null : n); etoiles();
}}));
// la marche : des pas tirés au hasard, comme dans le jeu
const ctx = new AudioContext(), tampons = {{}};
async function tampon(url) {{
  if (!tampons[url]) tampons[url] = ctx.decodeAudioData(await (await fetch(url)).arrayBuffer());
  return tampons[url];
}}
document.querySelectorAll("button.marche").forEach(b => b.addEventListener("click", async () => {{
  document.querySelectorAll("audio").forEach(a => a.pause());
  const pas = JSON.parse(b.dataset.pas), cadence = +b.dataset.cadence;
  const sons = await Promise.all(pas.map(tampon));
  let t = ctx.currentTime + 0.05, avant = -1;
  for (let i = 0; i < 8; i++) {{
    let k; do {{ k = Math.floor(Math.random() * sons.length); }} while (sons.length > 1 && k === avant);
    avant = k;
    const s = ctx.createBufferSource();
    s.buffer = sons[k];
    s.playbackRate.value = 1 + (Math.random() - 0.5) * 0.08;
    s.connect(ctx.destination);
    s.start(t);
    t += cadence * (0.9 + Math.random() * 0.2);
  }}
}}));
document.querySelectorAll("audio").forEach(a => a.addEventListener("play", () =>
  document.querySelectorAll("audio").forEach(b => {{ if (b !== a) b.pause(); }})));
charger();
</script></html>"""
    os.makedirs(EN_COURS, exist_ok=True)
    with open(os.path.join(EN_COURS, "pas.html"), "w", encoding="utf-8") as f:
        f.write(contenu)
    print(f"http://localhost:{PORT}/pas.html  ({os.path.join(EN_COURS, 'pas.html')})")


class Serveur(essai.ServeurAudio):
    def do_POST(self):
        if self.path != "/reponses":
            self.send_error(404)
            return
        lot = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))))
        with VERROU:
            if os.path.exists(REPONSES):
                try:
                    with open(REPONSES, encoding="utf-8") as f:
                        donnees = json.load(f)
                except ValueError:
                    self.send_error(500, "pas-reponses.json illisible : rien n'est écrasé")
                    return
            else:
                donnees = {}
            for cle, valeur in lot.items():
                if valeur is None:
                    donnees.pop(cle, None)
                else:
                    donnees[cle] = valeur
            temporaire = REPONSES + ".tmp"
            with open(temporaire, "w", encoding="utf-8") as f:
                json.dump(donnees, f, ensure_ascii=False, indent=2)
            os.replace(temporaire, REPONSES)
        self.send_response(204)
        self.end_headers()


def serveur():
    print(f"http://localhost:{PORT}/pas.html")
    http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Serveur).serve_forever()


def installer(sons):
    r = reponses()
    catalogue_chemin = os.path.join(GODOT, "data", "sounds.json")
    with open(catalogue_chemin, encoding="utf-8") as f:
        catalogue = json.load(f)
    cible = os.path.join(GODOT, "audio", "pas")
    os.makedirs(cible, exist_ok=True)
    poses = 0
    for s in sons:
        source = r.get(s["id"])
        if not source or source == "refaire" or not pas_de(s["id"], source):
            continue
        fichiers = []
        for n, f in enumerate(pas_de(s["id"], source), 1):
            nom = f"{s['id']}-{n}{os.path.splitext(f)[1]}"
            shutil.copyfile(os.path.join(dossier(s["id"], source), f), os.path.join(cible, nom))
            fichiers.append(f"pas/{nom}")
        catalogue["sounds"][f"step-{s['id']}"] = fichiers
        poses += 1
        # à reporter dans godot/audio/LICENCES.md
        libre = next((l for l in s.get("libres", []) if l["source"] == source), None)
        print(f"  step-{s['id']} : " + (f"{libre['auteur']}, {libre['licence']}, {libre['page']}" if libre else "ElevenLabs"))
    with open(catalogue_chemin, "w", encoding="utf-8") as f:
        json.dump(catalogue, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"{poses} pas installés ; à refaire : " + (", ".join(s["id"] for s in sons if r.get(s["id"]) == "refaire") or "aucun"))


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    commande, ids = sys.argv[1], set(sys.argv[2:])
    marches, sols, sons = liste()
    choisis = [s for s in sons if not ids or s["id"] in ids]
    if commande == "cout":
        manque = [s for s in choisis if not pas_de(s["id"], "elevenlabs")]
        print(f"{len(manque)} marches à générer, environ {len(manque) * CREDITS_PAR_SON} crédits ElevenLabs")
    elif commande == "generer":
        generer(choisis, marches, sols)
    elif commande == "recouper":
        for s in choisis:
            for source in sources_de(s["id"]):
                entiere = os.path.join(dossier(s["id"], source), "marche.wav")
                if not os.path.exists(entiere):
                    continue
                with wave.open(entiere) as w:
                    pcm = w.readframes(w.getnframes())
                for f in pas_de(s["id"], source):
                    os.remove(os.path.join(dossier(s["id"], source), f))
                print(f"  {s['id']} ({source}) : {decouper(pcm, dossier(s['id'], source))} pas")
    elif commande == "refaire":
        refaire(choisis, marches, sols)
    elif commande == "libres":
        libres(choisis)
    elif commande == "page":
        page(sons, marches, sols)
    elif commande == "serveur":
        serveur()
    elif commande == "installer":
        installer(sons)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
