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

if /i "%RID%"=="all" (
    echo.
    echo Przywracanie zaleznosci...
    dotnet restore "%PROJECT%"
    if errorlevel 1 (
        echo [BLAD] dotnet restore nie powiodl sie.
        pause
        exit /b 1
    )

    setlocal enabledelayedexpansion
    set FAILED=
    for %%R in (win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64) do (
        call :BuildOne %%R
        if errorlevel 1 set FAILED=!FAILED! %%R
    )

    echo.
    echo ============================================
    if defined FAILED (
        echo   Zakonczono z bledami. Nie udalo sie zbudowac:!FAILED!
        echo ============================================
        pause
        endlocal
        exit /b 1
    )
    echo   Wszystkie 6 wariantow zbudowane pomyslnie w publish\
    echo ============================================
    pause
    endlocal
    exit /b 0
)

if /i not "%RID%"=="win-x64" if /i not "%RID%"=="win-arm64" if /i not "%RID%"=="linux-x64" if /i not "%RID%"=="linux-arm64" if /i not "%RID%"=="osx-x64" if /i not "%RID%"=="osx-arm64" (
    echo [BLAD] Nieznana architektura "%RID%". Uzyj win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64 lub all.
    echo Przyklad: build.bat win-arm64
    pause
    exit /b 1
)

echo.
echo Przywracanie zaleznosci...
dotnet restore "%PROJECT%"
if errorlevel 1 (
    echo [BLAD] dotnet restore nie powiodl sie.
    pause
    exit /b 1
)

call :BuildOne %RID%
if errorlevel 1 (
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Gotowe! Plik wykonywalny znajduje sie w:
echo   %CD%\publish\%RID%\
echo ============================================
echo.
echo Wskazowka: aby zbudowac wszystkie warianty naraz, uruchom: build.bat all
echo.
pause
exit /b 0

:BuildOne
setlocal
set BRID=%1
set BOUTPUT=publish\%BRID%

if exist "%BOUTPUT%" (
    echo Czyszczenie poprzedniego katalogu %BOUTPUT%...
    rmdir /s /q "%BOUTPUT%"
)

echo.
echo Budowanie pojedynczego pliku EXE (self-contained, %BRID%)...
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r %BRID% ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%BOUTPUT%"

if errorlevel 1 (
    echo [BLAD] dotnet publish dla %BRID% nie powiodl sie.
    endlocal
    exit /b 1
)

endlocal
exit /b 0
