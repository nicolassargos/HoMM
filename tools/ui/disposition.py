"""La disposition d'un écran, réglée à la souris (choix de l'utilisateur, 2026-11-21) : une page où l'on déplace, aligne,
agrandit ou cache les éléments de l'écran, qui écrit godot/data/layouts/<écran>.json (lu par Game/ScreenLayout.cs).

    python -I tools/ui/disposition.py [écran]     (« town » par défaut : l'écran de ville ; « town-xxx » : une autre
                                                  disposition du même écran, data/layouts/town-xxx.json, à comparer
                                                  en jeu avec l'option --layout=town-xxx)

Compile le jeu, le lance une fois pour relever l'écran (--layout-dump : la place de chaque élément, l'écran entier, le fond
seul, dans art-candidats/en-cours/disposition-<écran>/), puis sert la page sur http://127.0.0.1:8769/ et l'ouvre.
La page enregistre à chaque changement ; « Voir dans le jeu » relance le relevé et montre le vrai résultat.
Seuls les éléments déplacés, agrandis ou cachés vont dans le fichier : les autres gardent la place que leur donne le code.
Seule la bibliothèque standard de Python est utilisée.
"""
import json
import socket
import os
import subprocess
import sys
import threading
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

RACINE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GODOT_DIR = os.path.join(RACINE, "godot")
PORT = 8769  # pas ceux des images (8765), de sons.py (8766), de musiques.py (8767) ni de pas.py (8768)

# l'écran, les options qui l'ouvrent, et le nom de chaque élément sur la page
ECRANS = {
    "town": {
        "options": ["--town", "--seed=42"],
        "noms": {
            "resources": "Ressources", "actions": "Quitter, Construire, Recruter",
            "name-tab": "Onglet du nom et du revenu", "status": "Barre d'état", "view": "La ville vue de face",
            "garrison-label": "« Garnison »", "town-picture": "Image de la ville", "garrison": "Armée de la garnison",
            "hint": "Aide sur les troupes", "hero-label": "Nom du héros", "portrait": "Portrait du héros",
            "hero-army": "Armée du héros", "give-all": "Tout donner au héros", "leave-all": "Tout laisser en garnison",
        },
    },
}


def godot():
    if os.environ.get("GODOT"):
        return os.environ["GODOT"]
    for chemin in (r"C:\Godot\Godot_v4.7.2-stable_mono_win64.exe",
                   r"C:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"):
        if os.path.exists(chemin):
            return chemin
    sys.exit("Godot 4.7.2 .NET introuvable (C:\\Godot\\ ou la variable GODOT)")


def base(ecran):
    """L'écran d'une disposition : « town-haute » est une disposition de « town »."""
    return ecran.split("-")[0]


def releve(ecran, dossier):
    """Lance le jeu sur l'écran : elements.json, ecran.png, fond.png."""
    os.makedirs(dossier, exist_ok=True)
    r = subprocess.run([godot(), "--path", GODOT_DIR, "--", *ECRANS[base(ecran)]["options"], "--layout=" + ecran,
                        "--layout-dump=" + dossier.replace("\\", "/")], capture_output=True, text=True, timeout=300)
    if not os.path.exists(os.path.join(dossier, "elements.json")):
        raise RuntimeError("le relevé a échoué :\n" + (r.stdout + r.stderr)[-2000:])


def fichier_disposition(ecran):
    return os.path.join(GODOT_DIR, "data", "layouts", f"{ecran}.json")


def servir(ecran, dossier):
    page = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "disposition.html"), encoding="utf-8").read()
    page = page.replace("@@NOMS@@", json.dumps(ECRANS[base(ecran)]["noms"], ensure_ascii=False)).replace("@@ECRAN@@", ecran)
    verrou = threading.Lock()

    class Page(BaseHTTPRequestHandler):
        def _envoyer(self, code, corps=b"", type_="application/json"):
            self.send_response(code)
            self.send_header("Content-Type", type_)
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(corps)

        def do_GET(self):
            chemin = self.path.split("?")[0]
            if chemin == "/":
                return self._envoyer(200, page.encode("utf-8"), "text/html; charset=utf-8")
            if chemin == "/disposition.json":
                f = fichier_disposition(ecran)
                return self._envoyer(200, open(f, "rb").read() if os.path.exists(f) else b'{"elements":{}}')
            nom = chemin.lstrip("/")
            if nom in ("elements.json", "ecran.png", "fond.png"):
                return self._envoyer(200, open(os.path.join(dossier, nom), "rb").read(),
                                     "image/png" if nom.endswith(".png") else "application/json")
            self._envoyer(404)

        def do_POST(self):
            corps = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))) or b"{}")
            with verrou:
                if self.path == "/enregistrer":
                    f = fichier_disposition(ecran)
                    os.makedirs(os.path.dirname(f), exist_ok=True)
                    with open(f + ".tmp", "w", encoding="utf-8", newline="\n") as sortie:
                        json.dump({"elements": corps["elements"]}, sortie, ensure_ascii=False, indent=2)
                        sortie.write("\n")
                    os.replace(f + ".tmp", f)
                    print(f"{len(corps['elements'])} éléments placés : {os.path.relpath(f, RACINE)}", flush=True)
                    return self._envoyer(200, b"{}")
                if self.path == "/voir":
                    # le jeu relu avec la nouvelle disposition (la page enregistre avant de demander)
                    try:
                        releve(ecran, dossier)
                    except Exception as e:
                        return self._envoyer(500, json.dumps({"erreur": str(e)}).encode("utf-8"))
                    print("Relevé refait dans le jeu", flush=True)
                    return self._envoyer(200, b"{}")
            self._envoyer(404)

        def log_message(self, *args):
            pass

    class Exclusif(ThreadingHTTPServer):
        # un seul serveur sur le port : sous Windows, l'adresse réutilisable laisserait un autre serveur l'écouter aussi
        allow_reuse_address = False

        def server_bind(self):
            if hasattr(socket, "SO_EXCLUSIVEADDRUSE"):
                self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            super().server_bind()

    try:
        serveur = Exclusif(("127.0.0.1", PORT), Page)
    except OSError:
        sys.exit(f"Le port {PORT} est déjà pris : la page de disposition tourne sans doute déjà.")
    url = f"http://127.0.0.1:{PORT}/"
    print(f"Page de disposition : {url} (Ctrl+C pour arrêter)", flush=True)
    webbrowser.open(url)
    serveur.serve_forever()


def main():
    ecran = sys.argv[1] if len(sys.argv) > 1 else "town"
    if base(ecran) not in ECRANS:
        sys.exit(f"écran inconnu : {ecran} (connus : {', '.join(ECRANS)})")
    dossier = os.path.join(RACINE, "art-candidats", "en-cours", f"disposition-{ecran}")
    print("Compilation…", flush=True)
    if subprocess.run(["dotnet", "build", "-v", "q", "-nologo"], cwd=GODOT_DIR).returncode != 0:
        sys.exit("la compilation a échoué")
    print("Relevé de l'écran dans le jeu…", flush=True)
    releve(ecran, dossier)
    servir(ecran, dossier)


if __name__ == "__main__":
    main()
