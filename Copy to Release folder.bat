@echo off
set REPLACE_IN_PATH=%APPDATA%\SpaceEngineers\Mods\ElectronicsPanel

rmdir "%REPLACE_IN_PATH%" /S /Q

robocopy.exe .\ "%REPLACE_IN_PATH%" *.* /S /xd .git bin obj .vs ignored Sources DoNotCopy_VanillaDataCompare /xf *.fbx *.xml *.hkt *.exe *.dll *.lnk *.git* *.bat *.zip *.7z *.blend* *.png *.pdn *.md *.log *.sln *.csproj *.csproj.user *.ruleset *.ps1 desktop.ini

pause