@echo off
setlocal

set "INSTALL_ROOT=%LOCALAPPDATA%\BabyShop"
set "APP_ROOT=%INSTALL_ROOT%\app"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\BabyShop"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\BabyShop.lnk"
set "START_MENU_SHORTCUT=%START_MENU_DIR%\BabyShop.lnk"

if not exist "%INSTALL_ROOT%" mkdir "%INSTALL_ROOT%"
if exist "%APP_ROOT%" rmdir /S /Q "%APP_ROOT%"
mkdir "%APP_ROOT%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath '%~dp0BabyShopPackage.zip' -DestinationPath '%APP_ROOT%' -Force"
if errorlevel 1 (
    echo Failed to extract BabyShop package.
    pause
    exit /b 1
)

copy /Y "%~dp0README.txt" "%INSTALL_ROOT%\README.txt" >nul
copy /Y "%~dp0uninstall.cmd" "%INSTALL_ROOT%\uninstall.cmd" >nul

if not exist "%START_MENU_DIR%" mkdir "%START_MENU_DIR%"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $shortcut = $ws.CreateShortcut('%DESKTOP_SHORTCUT%'); $shortcut.TargetPath = '%APP_ROOT%\BabyShop.exe'; $shortcut.WorkingDirectory = '%APP_ROOT%'; $shortcut.IconLocation = '%APP_ROOT%\BabyShop.exe,0'; $shortcut.Save(); $shortcut2 = $ws.CreateShortcut('%START_MENU_SHORTCUT%'); $shortcut2.TargetPath = '%APP_ROOT%\BabyShop.exe'; $shortcut2.WorkingDirectory = '%APP_ROOT%'; $shortcut2.IconLocation = '%APP_ROOT%\BabyShop.exe,0'; $shortcut2.Save();"

start "" "%APP_ROOT%\BabyShop.exe"
exit /b 0
