@echo off
cd /d "%~dp0"
dotnet run --project "src\XSphere\XSphere.csproj"
if errorlevel 1 pause
