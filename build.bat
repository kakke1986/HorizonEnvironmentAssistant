@echo off
setlocal

cd /d "%~dp0"

where go >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Go is not installed or not in PATH.
    exit /b 1
)

if not exist "OfflinePackages" mkdir "OfflinePackages"
if not exist "dist" mkdir "dist"
if not exist "dist\OfflinePackages" mkdir "dist\OfflinePackages"

echo [1/4] Tidying Go modules...
go mod tidy
if errorlevel 1 exit /b 1

echo [2/4] Generating Windows manifest resource...
set "RSRC=rsrc"
where rsrc >nul 2>nul
if errorlevel 1 (
    for /f "delims=" %%G in ('go env GOPATH') do set "GOPATH_DIR=%%G"
    if exist "%GOPATH_DIR%\bin\rsrc.exe" (
        set "RSRC=%GOPATH_DIR%\bin\rsrc.exe"
    ) else (
        go install github.com/akavel/rsrc@latest
        if errorlevel 1 exit /b 1
        set "RSRC=%GOPATH_DIR%\bin\rsrc.exe"
    )
)
"%RSRC%" -manifest "app.manifest" -o "rsrc.syso"
if errorlevel 1 exit /b 1

echo [3/4] Building HorizonEnvironmentAssistant.exe...
go build -trimpath -ldflags="-H windowsgui -s -w" -o "dist\HorizonEnvironmentAssistant.exe" .
if errorlevel 1 exit /b 1

echo [4/4] Preparing runtime folders...
if exist "OfflinePackages\.gitkeep" copy /Y "OfflinePackages\.gitkeep" "dist\OfflinePackages\.gitkeep" >nul

echo.
echo Build completed: dist\HorizonEnvironmentAssistant.exe
endlocal
