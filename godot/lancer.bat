@echo off
rem Compile le code C# puis lance le jeu.
rem Utilisation : lancer.bat   ou   lancer.bat -- --seed=42 (carte fixe)
cd /d "%~dp0"

rem Chemin vers Godot 4.7.2 .NET : a adapter si besoin, ou definir la variable GODOT.
if "%GODOT%"=="" set "GODOT=C:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"

if not exist "%GODOT%" (
  echo Godot est introuvable : %GODOT%
  echo Modifiez la ligne "set GODOT=..." dans lancer.bat.
  pause
  exit /b 1
)

dotnet build || (pause & exit /b 1)
"%GODOT%" --path . %*
