@echo off
REM One click: stock game, no mod loaded at all. Use this for playing online.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0switch-mod.ps1" vanilla -Launch
