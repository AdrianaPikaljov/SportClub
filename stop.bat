@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Peatan SportClubi ...

rem 1) Aknad, mille start.bat avas (pealkirja jargi)
for %%T in ("SportClub API" "SportClub Worker" "SportClub Admin" "SportClub MAUI") do (
    taskkill /FI "WINDOWTITLE eq %%~T*" /T /F >nul 2>nul
)

rem 2) Pordi jargi: 5173 (admin, Vite) ja 5080 (API).
rem    npm muudab akna pealkirja, seega admin-akent ei leia pealkirja jargi.
rem    Leiame pordil kuulava protsessi ja sulgeme ka selle cmd-akna (node -> npm -> cmd).
powershell -NoProfile -Command "foreach ($port in 5173, 5080) { Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | ForEach-Object { $top = $_.OwningProcess; while ($true) { $p = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $top); if (-not $p) { break }; $parent = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $p.ParentProcessId); if ($parent -and @('cmd.exe','node.exe','dotnet.exe') -contains $parent.Name) { $top = $parent.ProcessId } else { break } }; taskkill /PID $top /T /F | Out-Null } }"

rem 3) Kui midagi jai alles
taskkill /IM SportClub.Api.exe /F >nul 2>nul
taskkill /IM SportClub.Worker.exe /F >nul 2>nul
taskkill /IM SportClub.Maui.exe /F >nul 2>nul

echo Valmis.
timeout /t 3 >nul
