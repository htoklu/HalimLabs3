@echo off
setlocal
cd /d "%~dp0"
if exist "dist\HalimLabs3.exe" (
  start "" "dist\HalimLabs3.exe"
  exit /b 0
)
echo dist\HalimLabs3.exe bulunamadi.
echo .NET 8 SDK varsa:  build.bat
echo veya:  dotnet run --project src\HalimLabs\HalimLabs.csproj
pause
exit /b 1
