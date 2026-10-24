@echo off
rem Compile le code C# puis lance le jeu.
rem Utilisation : lancer.bat   ou   lancer.bat -- --seed=42 (carte fixe)
cd /d "%~dp0"

rem Cherche Godot 4.7.2 .NET aux endroits habituels, sauf si la variable GODOT est deja definie.
if not "%GODOT%"=="" goto :verifier
for %%F in (
  "C:\Godot\Godot_v4.7.2-stable_mono_win64.exe"
  "C:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
) do if exist %%F set "GODOT=%%~F"

:verifier
if not exist "%GODOT%" (
  echo Godot 4.7.2 .NET est introuvable.
  echo Placez Godot_v4.7.2-stable_mono_win64.exe dans C:\Godot\
  echo ou definissez la variable GODOT avec le chemin complet de l exe.
  pause
  exit /b 1
)

dotnet build || (pause & exit /b 1)
"%GODOT%" --path . %*
