@echo off
setlocal
cd /d "%~dp0"
dotnet publish src\HalimLabs\HalimLabs.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
if errorlevel 1 (
  echo.
  echo Derleme basarisiz. Windows icin .NET 8 SDK gerekir:
  echo https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)
if exist "dist\HalimLabs3.zip" del /f /q "dist\HalimLabs3.zip"
tar -a -c -f "dist\HalimLabs3.zip" -C dist HalimLabs3.exe appsettings.json
if errorlevel 1 (
  echo ZIP olusturulamadi.
  exit /b 1
)
echo.
echo Hazir: dist\HalimLabs3.exe
echo Zip:   dist\HalimLabs3.zip
exit /b 0
