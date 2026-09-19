@echo off
setlocal
echo Sunshine+Moonlight local streaming setup
sc query Sunshine >nul 2>&1
if errorlevel 1 (
  echo Please install Sunshine from https://github.com/LizardByte/Sunshine/releases/latest
  choice /C YN /M "Press Y after install"
)
start ms-windows-store://pdp/?productid=9MN394N4MBCZ
start "stream-bridge" cmd /c "cd /d C:\better\nitrogen_bridge && python stream_bridge.py"
echo Done.
pause
endlocal
