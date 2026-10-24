@echo off
cd /d "%~dp0"
py -m pip install -r requirements.txt --quiet
py jeu\principal.py
pause
