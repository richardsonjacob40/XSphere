@echo off
cd /d "%~dp0"
dotnet run --project "src\ArcadeHub\ArcadeHub.csproj"
if errorlevel 1 pause
