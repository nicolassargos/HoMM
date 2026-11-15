"""Essai d'écoute des sources de sons : les mêmes sons (tools/audio/essai-combat.json) demandés à chaque source, puis
une page pour les écouter côte à côte et choisir à l'oreille.

    python -I tools/audio/essai.py solde                     ce qui reste chez ElevenLabs et Stability (gratuit)
    python -I tools/audio/essai.py cout                      le prix de l'essai, source par source (gratuit)
    python -I tools/audio/essai.py elevenlabs [id…]          génère chez ElevenLabs (sons et musique) ; ce qui existe reste
    python -I tools/audio/essai.py stability [id…]           génère chez Stability (Stable Audio)
    python -I tools/audio/essai.py libres                    télécharge les sons libres (liste « libres » du fichier)
    python -I tools/audio/essai.py page                      écrit la page d'écoute art-candidats/en-cours/sons-essai.html

Les clés : variables ELEVENLABS_API_KEY et STABILITY_API_KEY (jamais affichées ; lues aussi dans les variables de
l'utilisateur Windows, pour une clé créée par « setx » sans relancer la session). Les fichiers vont dans
art-candidats/en-cours/sons-essai/<source>/<id>.<ext>, du dossier principal du dépôt (celui des autres propositions,
même lancé depuis un autre dossier de travail git). Seule la bibliothèque standard de Python est utilisée.

Prix (relevés le 2026-11-15) : ElevenLabs, environ 200 crédits par son et 900 par minute de musique (10 000 crédits
gratuits par mois ; pas d'usage commercial sans abonnement) ; Stability, 8 crédits (0,08 $) par génération.
"""
import html
import io
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
import zipfile

ICI = os.path.dirname(os.path.abspath(__file__))
LISTE = os.path.join(ICI, "essai-combat.json")
SOURCES = ["libres", "elevenlabs", "stability"]
NOMS_SOURCES = {"libres": "Bibliothèques libres", "elevenlabs": "ElevenLabs", "stability": "Stable Audio"}
EXTENSIONS = (".ogg", ".wav", ".mp3")

ELEVEN_SON = 200          # crédits par son
ELEVEN_MINUTE = 900       # crédits par minute de musique
STABILITY_GENERATION = 8  # crédits (0,01 $ chacun)


def racine_principale():
    """Le dossier principal du dépôt (art-candidats/ y vit, pas dans les autres dossiers de travail git)."""
    commun = subprocess.run(["git", "rev-parse", "--path-format=absolute", "--git-common-dir"], cwd=ICI,
                            capture_output=True, text=True, check=True).stdout.strip()
    return os.path.dirname(commun)


EN_COURS = os.path.join(racine_principale(), "art-candidats", "en-cours")
DOSSIER = os.path.join(EN_COURS, "sons-essai")


def liste():
    with open(LISTE, encoding="utf-8") as f:
        donnees = json.load(f)
    elements = [dict(e, genre="son") for e in donnees["sons"]] + [dict(e, genre="musique") for e in donnees["musiques"]]
    return donnees, elements


def existant(source, ident):
    for ext in EXTENSIONS:
        chemin = os.path.join(DOSSIER, source, ident + ext)
        if os.path.exists(chemin):
            return chemin
    return None


def cle(nom):
    valeur = trouver_cle(nom)
    if not valeur:
        sys.exit(f"La variable {nom} n'est pas définie.")
    return valeur


def trouver_cle(nom):
    """La clé : variable d'environnement, fichier .env.local du dossier de travail (non suivi), ou variable Windows."""
    return os.environ.get(nom) or cle_fichier(nom) or cle_windows(nom)


def cle_fichier(nom):
    """La clé dans .env.local, à la racine du dossier de travail git (écrit par « elevenlabs onboard »), ou None."""
    haut = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=ICI, capture_output=True, text=True).stdout.strip()
    try:
        with open(os.path.join(haut, ".env.local"), encoding="utf-8") as f:
            for ligne in f:
                k, _, v = ligne.strip().partition("=")
                if k.strip() == nom and v.strip():
                    return v.strip().strip('"').strip("'")
    except OSError:
        pass
    return None


def cle_windows(nom):
    """La variable de l'utilisateur Windows (créée par « setx » après le lancement de la session), ou None."""
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as k:
            return winreg.QueryValueEx(k, nom)[0] or None
    except (ImportError, OSError):
        return None


def requete(url, donnees=None, entetes=None, methode=None):
    req = urllib.request.Request(url, data=donnees, headers=entetes or {}, method=methode)
    try:
        with urllib.request.urlopen(req, timeout=300) as rep:
            return rep.status, rep.read(), dict(rep.headers)
    except urllib.error.HTTPError as e:
        corps = e.read()[:500].decode("utf-8", "replace")
        raise RuntimeError(f"HTTP {e.code} : {corps}") from None


def ecrire(source, ident, ext, contenu):
    os.makedirs(os.path.join(DOSSIER, source), exist_ok=True)
    chemin = os.path.join(DOSSIER, source, ident + ext)
    with open(chemin, "wb") as f:
        f.write(contenu)
    print(f"  {source}/{ident}{ext}  {len(contenu) // 1024} Ko")


# --- ElevenLabs ---

def eleven_solde():
    _, corps, _ = requete("https://api.elevenlabs.io/v1/user/subscription", entetes={"xi-api-key": cle("ELEVENLABS_API_KEY")})
    abo = json.loads(corps)
    reste = abo.get("character_limit", 0) - abo.get("character_count", 0)
    return f"ElevenLabs : {reste} crédits sur {abo.get('character_limit')} (abonnement {abo.get('tier')})"


def elevenlabs(elements):
    cle_api = cle("ELEVENLABS_API_KEY")
    for e in elements:
        if existant("elevenlabs", e["id"]):
            continue
        if e["genre"] == "son":
            url = "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128"
            corps = {"text": e["texte"], "model_id": "eleven_text_to_sound_v3", "duration_seconds": max(0.5, e["duree"]), "prompt_influence": 0.5}
        else:
            url = "https://api.elevenlabs.io/v1/music?output_format=mp3_44100_128"
            corps = {"prompt": e["texte"], "music_length_ms": int(e["duree"] * 1000), "model_id": "music_v2_5",
                     "force_instrumental": True}
        try:
            _, audio, _ = requete(url, json.dumps(corps).encode(), {"xi-api-key": cle_api, "Content-Type": "application/json"}, "POST")
            ecrire("elevenlabs", e["id"], ".mp3", audio)
        except RuntimeError as err:
            print(f"  {e['id']} : échec, {err}")


# --- Stability (Stable Audio 3.0, asynchrone : on demande, puis on attend le résultat) ---

def stability_solde():
    _, corps, _ = requete("https://api.stability.ai/v1/user/balance", entetes={"Authorization": f"Bearer {cle('STABILITY_API_KEY')}"})
    credits = json.loads(corps)["credits"]
    return f"Stability : {credits:.1f} crédits ({credits / 100:.2f} $)"


def multipart(champs):
    borne = uuid.uuid4().hex
    corps = io.BytesIO()
    for nom, valeur in champs.items():
        corps.write(f"--{borne}\r\nContent-Disposition: form-data; name=\"{nom}\"\r\n\r\n{valeur}\r\n".encode())
    corps.write(f"--{borne}--\r\n".encode())
    return corps.getvalue(), f"multipart/form-data; boundary={borne}"


def stability(elements):
    cle_api = cle("STABILITY_API_KEY")
    for e in elements:
        if existant("stability", e["id"]):
            continue
        corps, type_corps = multipart({"prompt": e["texte"], "model": "stable-audio-3", "duration": max(1, e["duree"]),
                                       "output_format": "wav"})
        entetes = {"Authorization": f"Bearer {cle_api}", "Accept": "audio/*", "Content-Type": type_corps}
        try:
            statut, reponse, _ = requete("https://api.stability.ai/v2beta/audio/stable-audio/text-to-audio", corps, entetes, "POST")
            if statut == 202:
                ident = json.loads(reponse)["id"]
                for _ in range(120):
                    time.sleep(3)
                    statut, reponse, _ = requete(f"https://api.stability.ai/v2beta/audio/results/{ident}",
                                                 entetes={"Authorization": f"Bearer {cle_api}", "Accept": "audio/*"})
                    if statut == 200:
                        break
                else:
                    raise RuntimeError("pas de résultat après 6 minutes")
            ecrire("stability", e["id"], ".wav", reponse)
        except RuntimeError as err:
            print(f"  {e['id']} : échec, {err}")


# --- les bibliothèques libres ---

def libres(donnees):
    archives = {}
    for e in donnees.get("libres", []):
        if existant("libres", e["id"]):
            continue
        try:
            if e["url"] not in archives:
                _, archives[e["url"]], _ = requete(e["url"], entetes={"User-Agent": "Mozilla/5.0"})
            contenu = archives[e["url"]]
            nom = e["url"]
            if e.get("fichier"):
                with zipfile.ZipFile(io.BytesIO(contenu)) as z:
                    contenu = z.read(e["fichier"])
                nom = e["fichier"]
            ext = os.path.splitext(nom.split("?")[0])[1].lower()
            ecrire("libres", e["id"], ext if ext in EXTENSIONS else ".ogg", contenu)
        except (RuntimeError, KeyError, zipfile.BadZipFile) as err:
            print(f"  {e['id']} : échec, {err}")


# --- la page d'écoute ---

def page(donnees, elements):
    credits = {e["id"]: e for e in donnees.get("libres", [])}
    lignes = []
    for e in elements:
        cases = []
        for source in SOURCES:
            chemin = existant(source, e["id"])
            if chemin is None:
                cases.append("<td class=vide>—</td>")
                continue
            relatif = os.path.relpath(chemin, EN_COURS).replace(os.sep, "/")
            note = ""
            if source == "libres" and e["id"] in credits:
                c = credits[e["id"]]
                note = (f"<div class=note>{html.escape(c.get('licence', ''))}, {html.escape(c.get('auteur', ''))}"
                        f" · <a href=\"{html.escape(c.get('page', ''))}\" target=_blank>page</a></div>")
            cases.append(f"<td><audio controls preload=none src=\"{html.escape(relatif)}\"></audio>"
                         f"<label><input type=radio name=\"{e['id']}\" value=\"{source}\"> choisir</label>{note}</td>")
        lignes.append(f"<tr><th>{html.escape(e['nom'])}<div class=texte>{html.escape(e['texte'])}</div></th>{''.join(cases)}"
                      f"<td><textarea data-id=\"{e['id']}\" placeholder=\"commentaire\"></textarea></td></tr>")
    entetes = "".join(f"<th>{NOMS_SOURCES[s]}</th>" for s in SOURCES)
    avis = "".join(f"<label>{NOMS_SOURCES[s]} <textarea data-id=\"avis-{s}\"></textarea></label>" for s in SOURCES)
    contenu = f"""<!doctype html><html lang=fr><meta charset=utf-8><title>Essai d'écoute</title>
<style>
body{{font-family:system-ui,sans-serif;background:#1d1a16;color:#e8e0d0;margin:16px}}
h1{{font-size:22px}} table{{border-collapse:collapse;width:100%}}
td,th{{border:1px solid #4a4035;padding:8px;vertical-align:top;text-align:left}}
th .texte{{font-weight:normal;font-size:12px;color:#a89c88;max-width:280px}}
audio{{width:230px;display:block;margin-bottom:4px}} .vide{{color:#6a5f50;text-align:center}}
.note{{font-size:11px;color:#a89c88}} a{{color:#d8b870}} textarea{{width:200px;height:48px;background:#2a2620;color:#e8e0d0;border:1px solid #4a4035}}
.avis{{display:flex;gap:16px;margin:16px 0}} .avis textarea{{display:block;width:280px;height:70px}}
button{{font-size:15px;padding:6px 14px}}
</style>
<h1>Essai d'écoute : un combat du Château</h1>
<p>Écoutez chaque ligne, cochez la source préférée, ajoutez un mot si besoin. Les réponses restent dans ce navigateur ;
« Télécharger » les écrit dans un fichier <code>sons-essai.json</code>.</p>
<table><tr><th>Son</th>{entetes}<th>Commentaire</th></tr>{''.join(lignes)}</table>
<h2>Votre avis sur chaque source</h2><div class=avis>{avis}</div>
<button id=telecharger>Télécharger</button>
<script>
const CLE = "sons-essai";
let r = {{}};
try {{ r = JSON.parse(localStorage.getItem(CLE) || "{{}}"); }} catch (e) {{}}
function garder() {{ try {{ localStorage.setItem(CLE, JSON.stringify(r)); }} catch (e) {{}} }}
document.querySelectorAll("input[type=radio]").forEach(b => {{
  if (r[b.name] === b.value) b.checked = true;
  b.addEventListener("change", () => {{ r[b.name] = b.value; garder(); }});
}});
document.querySelectorAll("textarea").forEach(t => {{
  t.value = r["note:" + t.dataset.id] || "";
  t.addEventListener("input", () => {{ r["note:" + t.dataset.id] = t.value; garder(); }});
}});
// une seule lecture à la fois
document.querySelectorAll("audio").forEach(a => a.addEventListener("play", () =>
  document.querySelectorAll("audio").forEach(b => {{ if (b !== a) b.pause(); }})));
document.getElementById("telecharger").onclick = () => {{
  const l = document.createElement("a");
  l.href = URL.createObjectURL(new Blob([JSON.stringify(r, null, 2)], {{type: "application/json"}}));
  l.download = "sons-essai.json"; l.click();
}};
</script></html>"""
    os.makedirs(EN_COURS, exist_ok=True)
    chemin = os.path.join(EN_COURS, "sons-essai.html")
    with open(chemin, "w", encoding="utf-8") as f:
        f.write(contenu)
    print(chemin)


def cout(elements):
    sons = [e for e in elements if e["genre"] == "son"]
    minutes = sum(e["duree"] for e in elements if e["genre"] == "musique") / 60
    eleven = len(sons) * ELEVEN_SON + minutes * ELEVEN_MINUTE
    print(f"ElevenLabs : {len(sons)} sons × {ELEVEN_SON} + {minutes:.1f} min de musique × {ELEVEN_MINUTE} ≈ {eleven:.0f} crédits"
          " (10 000 gratuits par mois)")
    n = len(elements)
    print(f"Stability  : {n} générations × {STABILITY_GENERATION} crédits = {n * STABILITY_GENERATION} crédits"
          f" = {n * STABILITY_GENERATION / 100:.2f} $")
    print("Bibliothèques libres : gratuit")


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    commande, ids = sys.argv[1], set(sys.argv[2:])
    donnees, elements = liste()
    choisis = [e for e in elements if not ids or e["id"] in ids]
    if commande == "solde":
        for f, var in ((eleven_solde, "ELEVENLABS_API_KEY"), (stability_solde, "STABILITY_API_KEY")):
            print(f() if trouver_cle(var) else f"{var} : non définie")
    elif commande == "cout":
        cout(choisis)
    elif commande == "elevenlabs":
        elevenlabs(choisis)
    elif commande == "stability":
        stability(choisis)
    elif commande == "libres":
        libres(donnees)
    elif commande == "page":
        page(donnees, elements)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
