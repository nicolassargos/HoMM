@echo off
rem Met ce dossier a jour (la branche main, depuis GitHub), puis lance la demo.
rem Utilisation : demo-a-jour.bat (les options passent a demo.bat).
rem Ne fait rien si le dossier est sur une autre branche ou a des fichiers suivis modifies :
rem une autre session y travaille peut-etre.
cd /d "%~dp0.."

for /f %%B in ('git rev-parse --abbrev-ref HEAD') do set "BRANCHE=%%B"
if not "%BRANCHE%"=="main" (
  echo Ce dossier est sur la branche %BRANCHE%, pas sur main : rien n est mis a jour.
  pause
  exit /b 1
)
git diff --quiet HEAD --
if errorlevel 1 (
  echo Des fichiers suivis sont modifies dans ce dossier : rien n est mis a jour.
  git status --short --untracked-files=no
  pause
  exit /b 1
)
git pull --ff-only || (pause & exit /b 1)

call "%~dp0demo.bat" %*