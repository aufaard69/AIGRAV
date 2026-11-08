@echo off
title AIGRAV Frameless Desktop HUD
set "PATH=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Microsoft\VisualStudio\NodeJs;%PATH%"
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\AIGRAV\DesktopHUD\launch_desktop_hud.ps1"
