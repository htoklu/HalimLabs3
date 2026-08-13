@echo off
setlocal
cd /d "%~dp0"

if not exist "dist\HalimLabs3.exe" (
  if exist "dist\HalimLabs3.zip" (
    echo ZIP aciliyor...
    tar -xf "dist\HalimLabs3.zip" -C "dist"
  )
)

if exist "dist\HalimLabs3.exe" (
  start "" "dist\HalimLabs3.exe"
  exit /b 0
)

echo Calistirilacak dosya yok.
echo dist\HalimLabs3.zip veya dist\HalimLabs3.exe bekleniyordu.
echo .NET 8 SDK varsa:  build.bat
pause
exit /b 1
