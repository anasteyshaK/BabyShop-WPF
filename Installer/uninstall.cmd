@echo off
setlocal

set "INSTALL_ROOT=%LOCALAPPDATA%\BabyShop"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\BabyShop"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\BabyShop.lnk"

taskkill /IM BabyShop.exe /F >nul 2>nul

if exist "%DESKTOP_SHORTCUT%" del /F /Q "%DESKTOP_SHORTCUT%"
if exist "%START_MENU_DIR%" rmdir /S /Q "%START_MENU_DIR%"
if exist "%INSTALL_ROOT%" rmdir /S /Q "%INSTALL_ROOT%"

echo BabyShop has been removed from this user profile.
pause
