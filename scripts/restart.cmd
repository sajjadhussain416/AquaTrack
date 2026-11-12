@echo off
rem AquaTrack - stop any running copy of the app, then start it fresh (run.cmd).
rem Use this when Visual Studio says a file is "locked by AquaTrack.Web" or the port is busy.
title AquaTrack restart
echo ==^> Stopping running AquaTrack copies
taskkill /F /IM AquaTrack.Web.exe >nul 2>&1
timeout /t 2 >nul
call "%~dp0run.cmd"
