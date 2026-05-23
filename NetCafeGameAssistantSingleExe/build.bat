@echo off
setlocal

cd /d "%~dp0"

echo [1/3] Building GoRepairCore.exe...
if not exist "WinFormsClient\Tools" mkdir "WinFormsClient\Tools"
set GOOS=windows
set GOARCH=amd64
pushd "GoRepairCore"
go build -ldflags="-s -w" -o "..\WinFormsClient\Tools\GoRepairCore.exe" .
popd
if errorlevel 1 (
    echo GoRepairCore build failed.
    exit /b 1
)

echo [2/3] Publishing WinForms single EXE...
if exist "publish" rmdir /s /q "publish"
dotnet publish ".\WinFormsClient\WinFormsClient.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishTrimmed=false ^
  -o ".\publish"
if errorlevel 1 (
    echo WinForms publish failed.
    exit /b 1
)

echo [3/3] Done.
echo Output folder: %cd%\publish
endlocal
