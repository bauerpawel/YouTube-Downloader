@echo off
setlocal

echo ============================================
echo   YouTube Downloader - budowanie pliku EXE
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [BLAD] Nie znaleziono polecenia "dotnet".
    echo Zainstaluj .NET 10 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

set PROJECT=YouTubeDownloader.csproj

set RID=%1
if "%RID%"=="" set RID=win-x64
if /i not "%RID%"=="win-x64" if /i not "%RID%"=="win-arm64" (
    echo [BLAD] Nieznana architektura "%RID%". Uzyj win-x64 lub win-arm64.
    echo Przyklad: build.bat win-arm64
    pause
    exit /b 1
)

set OUTPUT=publish\%RID%

if exist "%OUTPUT%" (
    echo Czyszczenie poprzedniego katalogu %OUTPUT%...
    rmdir /s /q "%OUTPUT%"
)

echo.
echo Przywracanie zaleznosci...
dotnet restore "%PROJECT%"
if errorlevel 1 (
    echo [BLAD] dotnet restore nie powiodl sie.
    pause
    exit /b 1
)

echo.
echo Budowanie pojedynczego pliku EXE (self-contained, %RID%)...
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r %RID% ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%OUTPUT%"

if errorlevel 1 (
    echo [BLAD] dotnet publish nie powiodl sie.
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Gotowe! Plik wykonywalny znajduje sie w:
echo   %CD%\%OUTPUT%\YouTubeDownloader.exe
echo ============================================
echo.
echo Wskazowka: aby zbudowac dla ARM64, uruchom: build.bat win-arm64
echo.
pause
