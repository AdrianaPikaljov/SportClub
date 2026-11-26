@echo off
chcp 65001 >nul
title SportClub - kaivitamine
cd /d "%~dp0"

echo ============================================
echo   SportClub: API, Worker, admin ja MAUI
echo ============================================
echo.

where dotnet >nul 2>nul || (echo [VIGA] .NET SDK puudub. Paigalda .NET 10 SDK. & pause & exit /b 1)
where npm >nul 2>nul || (echo [VIGA] Node.js puudub. Paigalda Node.js. & pause & exit /b 1)

rem ---- 1. Web API (http://localhost:5080) ----
echo [1/4] Kaivitan Web API ...
start "SportClub API" cmd /k "dotnet run --project SportClub.Api --launch-profile http"

rem Ootame, kuni API vastab (esimene kaivitus votab kauem, sest projekt kompileeritakse).
echo       Ootan, kuni API on valmis ...
set /a tries=0
:waitapi
set /a tries+=1
curl -s -o nul http://localhost:5080/swagger/index.html && goto apiready
if %tries% geq 90 (
    echo [VIGA] API ei kaivitunud 3 minutiga. Vaata akent "SportClub API".
    pause
    exit /b 1
)
timeout /t 2 /nobreak >nul
goto waitapi
:apiready
echo       API tootab.

rem ---- 2. Taustateenus (Worker) ----
echo [2/4] Kaivitan Workeri ...
start "SportClub Worker" cmd /k "dotnet run --project SportClub.Worker"

rem ---- 3. React admin (http://localhost:5173) ----
echo [3/4] Kaivitan admin-liidese ...
if not exist "SportClub.Admin\node_modules" (
    echo       Esimene kord: paigaldan npm paketid ...
    pushd SportClub.Admin
    call npm install
    popd
)
start "SportClub Admin" cmd /k "cd /d SportClub.Admin && npm run dev"

rem ---- 4. MAUI rakendus (Windows) ----
echo [4/4] Kaivitan MAUI rakenduse ...
start "SportClub MAUI" cmd /k "dotnet run --project SportClub.Maui -f net10.0-windows10.0.19041.0"

rem Brauseris: Swagger ja admin
timeout /t 5 /nobreak >nul
start "" http://localhost:5080/swagger
start "" http://localhost:5173

echo.
echo Koik on kaivitatud. Iga osa tootab oma aknas.
echo Peatamiseks kaivita stop.bat (voi sulge aknad).
echo.
timeout /t 8 >nul
