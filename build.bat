@echo off
setlocal EnableExtensions
chcp 65001 >nul
title ChestCounter - Compilar
cd /d "%~dp0"

rem Colores (Windows 10/11). Si la consola no los soporta, solo se ve el texto.
for /f %%a in ('echo prompt $E^| cmd') do set "ESC=%%a"
set "OK=%ESC%[92mOK%ESC%[0m"
set "FALTA=%ESC%[91mFALTA%ESC%[0m"
set "ERR=%ESC%[91mERROR%ESC%[0m"
set "LOG=%~dp0build.log"

cls
echo.
echo ==============================================================
echo    ChestCounter - Compilar desde el código fuente
echo ==============================================================
echo.
echo Este asistente genera ChestCounter.exe a partir del código.
echo Primero comprobará que tu PC tiene todo lo necesario.
echo No se instalará nada sin preguntarte antes.
echo.

rem ===============================================================
rem  FASE 1: requisitos
rem ===============================================================
:checks
echo Comprobando requisitos
echo ----------------------
set "MISSING="

rem [1/3] Windows de 64 bits
<nul set /p "=[1/3] Windows de 64 bits ..................... "
set "ARCH=%PROCESSOR_ARCHITECTURE%"
if defined PROCESSOR_ARCHITEW6432 set "ARCH=%PROCESSOR_ARCHITEW6432%"
if /i "%ARCH%"=="AMD64" goto arch_ok
if /i "%ARCH%"=="ARM64" goto arch_ok
echo %FALTA%
echo.
echo ChestCounter solo funciona en Windows de 64 bits. No se puede continuar.
goto fail_end
:arch_ok
echo %OK%

rem [2/3] Programa dotnet
<nul set /p "=[2/3] Programa .NET (dotnet) ................. "
set "DOTNET="
where dotnet >nul 2>nul && set "DOTNET=dotnet"
if not defined DOTNET if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if defined DOTNET (echo %OK%) else (echo %FALTA%& set "MISSING=1")

rem [3/3] SDK de .NET 10 (el runtime solo NO basta para compilar)
<nul set /p "=[3/3] Kit de desarrollo .NET 10 (SDK) ........ "
set "SDKVER="
if defined DOTNET "%DOTNET%" --list-sdks > "%TEMP%\cc_sdks.txt" 2>nul
if defined DOTNET for /f "tokens=1" %%v in ('findstr /b "10." "%TEMP%\cc_sdks.txt"') do set "SDKVER=%%v"
if defined SDKVER (echo %OK%  versión %SDKVER%) else (echo %FALTA%& set "MISSING=1")
echo.

if not defined MISSING goto build

rem ===============================================================
rem  Falta el SDK: explicar y ofrecer instalarlo
rem ===============================================================
if defined TRIED_INSTALL goto manual
echo Falta el kit de desarrollo de .NET 10 (SDK).
echo Es la herramienta gratuita de Microsoft que convierte el código en el .exe.
if defined DOTNET echo Tienes .NET instalado, pero solo la parte para EJECUTAR programas
if defined DOTNET echo (el "runtime"), no la necesaria para COMPILARLOS (el "SDK").
echo.
where winget >nul 2>nul || goto manual
echo Puedo instalarlo ahora con winget, el instalador oficial de Windows:
echo   - Se descargan unos 200 MB desde Microsoft.
echo   - Windows te pedirá permiso de administrador.
echo   - Tarda unos minutos, según tu conexión.
echo.
choice /c SN /n /m "¿Instalar el SDK de .NET 10 ahora? [S/N] "
if errorlevel 2 goto manual
set "TRIED_INSTALL=1"
echo.
echo Instalando el SDK de .NET 10. No cierres esta ventana...
echo --------------------------------------------------------------
winget install --id Microsoft.DotNet.SDK.10 -e --source winget --accept-source-agreements --accept-package-agreements
echo --------------------------------------------------------------
echo.
echo Instalación terminada. Volvemos a comprobar los requisitos.
echo.
goto checks

:manual
if defined TRIED_INSTALL echo La instalación automática no ha funcionado.
echo Puedes instalarlo a mano en 3 pasos:
echo   1. Abre https://dotnet.microsoft.com/download/dotnet/10.0
echo   2. En la columna "SDK", descarga el instalador "Windows x64".
echo      Ojo: el "Runtime" NO sirve, tiene que ser el SDK.
echo   3. Instálalo y vuelve a abrir build.bat.
echo.
choice /c SN /n /m "¿Abrir la página de descarga ahora? [S/N] "
if not errorlevel 2 start "" "https://dotnet.microsoft.com/download/dotnet/10.0"
goto fail_end

rem ===============================================================
rem  FASE 2: compilación (el detalle va a build.log)
rem ===============================================================
:build
echo Todo listo. Empezamos la compilación.
echo.
echo Compilando
echo ----------
if exist "dist\ChestCounter.exe" del /q "dist\ChestCounter.exe"
echo Registro de compilación - %date% %time% > "%LOG%"

<nul set /p "=[1/2] Descargando librerías necesarias ....... "
"%DOTNET%" restore src\ChestCounter\ChestCounter.csproj -r win-x64 --nologo >> "%LOG%" 2>&1
rem errorlevel puede ser negativo, por eso se compara con 0 y no con "errorlevel 1".
if not "%errorlevel%"=="0" (
  echo %ERR%
  echo.
  echo No se pudieron descargar las librerías desde nuget.org.
  echo Comprueba tu conexión a internet, o si un antivirus/proxy bloquea api.nuget.org.
  goto build_failed
)
echo %OK%

<nul set /p "=[2/2] Compilando ChestCounter.exe (1-2 min) .. "
"%DOTNET%" publish src\ChestCounter\ChestCounter.csproj -c Release -r win-x64 --self-contained true --no-restore --nologo ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist >> "%LOG%" 2>&1
if not "%errorlevel%"=="0" (
  echo %ERR%
  goto build_failed
)
if not exist "dist\ChestCounter.exe" (
  echo %ERR%
  echo.
  echo La compilación terminó pero no se generó ChestCounter.exe.
  goto build_failed
)
echo %OK%

echo.
echo ==============================================================
echo    ¡Listo! ChestCounter.exe se ha generado correctamente.
echo ==============================================================
echo.
echo Lo tienes en: %~dp0dist\ChestCounter.exe
echo Se abrirá la carpeta para que lo veas.
explorer dist
echo.
pause
exit /b 0

:build_failed
echo.
echo Algo ha fallado. Estos son los errores encontrados:
echo --------------------------------------------------------------
findstr /i /r /c:": error " /c:"error [A-Z][A-Z]*[0-9]" "%LOG%"
echo --------------------------------------------------------------
echo El registro completo está en: %LOG%

:fail_end
echo.
pause
exit /b 1
