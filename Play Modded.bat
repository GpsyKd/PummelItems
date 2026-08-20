@echo off
REM One click: turn our items on and start the game. For local play on this machine.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0switch-mod.ps1" modded -Launch
