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
echo.
echo Hazir: dist\HalimLabs3.exe
exit /b 0
