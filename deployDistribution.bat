@echo off
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "DIST_REPO=%ROOT%..\rider-collection-usage-finder-distribution"
set "PLUGIN_NAME=Collection Usage Finder"
set "ARTIFACT_BASENAME=CollectionUsageFinder"
set "PLUGIN_ID=com.jetbrains.rider.plugins.collectionusagefinder"
set "SINCE_BUILD=243.21565"
set "PAGES_BASE_URL=https://kimyj93.github.io/rider-collection-usage-finder-distribution"

for /f "usebackq tokens=1,* delims==" %%A in ("%ROOT%gradle.properties") do (
    if "%%A"=="PluginVersion" set "PLUGIN_VERSION=%%B"
)

if not defined PLUGIN_VERSION (
    echo Failed to read PluginVersion from gradle.properties.
    exit /b 1
)

set "ZIP_NAME=%ARTIFACT_BASENAME%-%PLUGIN_VERSION%.zip"
set "ZIP_PATH=%ROOT%build\distributions\%ZIP_NAME%"
set "DOCS_DIR=%DIST_REPO%\docs"

if not exist "%DIST_REPO%\.git" (
    echo Distribution repository not found: %DIST_REPO%
    exit /b 1
)

if /i not "%~1"=="--skip-build" (
    echo Building plugin %PLUGIN_VERSION%...
    call "%ROOT%gradlew.bat" buildPlugin --no-daemon
    if errorlevel 1 exit /b 1
) else (
    echo Skipping build.
)

if not exist "%ZIP_PATH%" (
    echo Plugin zip not found: %ZIP_PATH%
    exit /b 1
)

if not exist "%DOCS_DIR%" mkdir "%DOCS_DIR%"

echo Copying %ZIP_NAME% to distribution repository...
copy /Y "%ZIP_PATH%" "%DOCS_DIR%\%ZIP_NAME%" >nul
if errorlevel 1 exit /b 1

(
echo ^<?xml version="1.0" encoding="UTF-8"?^>
echo ^<plugins^>
echo   ^<plugin id="%PLUGIN_ID%" url="%PAGES_BASE_URL%/%ZIP_NAME%" version="%PLUGIN_VERSION%"^>
echo     ^<name^>%PLUGIN_NAME%^</name^>
echo     ^<idea-version since-build="%SINCE_BUILD%" /^>
echo   ^</plugin^>
echo ^</plugins^>
) > "%DOCS_DIR%\updatePlugins.xml"

(
echo ^<!doctype html^>
echo ^<html lang="en"^>
echo ^<head^>
echo   ^<meta charset="utf-8"^>
echo   ^<title^>%PLUGIN_NAME% Internal Repository^</title^>
echo ^</head^>
echo ^<body^>
echo   ^<h1^>%PLUGIN_NAME% Internal Repository^</h1^>
echo   ^<p^>Version: %PLUGIN_VERSION%^</p^>
echo   ^<ul^>
echo     ^<li^>^<a href="updatePlugins.xml"^>updatePlugins.xml^</a^>^</li^>
echo     ^<li^>^<a href="%ZIP_NAME%"^>%ZIP_NAME%^</a^>^</li^>
echo   ^</ul^>
echo ^</body^>
echo ^</html^>
) > "%DOCS_DIR%\index.html"

git -C "%DIST_REPO%" add docs
if errorlevel 1 exit /b 1

git -C "%DIST_REPO%" diff --cached --quiet
if not errorlevel 1 (
    echo No distribution changes to commit.
    exit /b 0
)

git -C "%DIST_REPO%" commit -m "chore: update plugin distribution %PLUGIN_VERSION%"
if errorlevel 1 exit /b 1

git -C "%DIST_REPO%" push
if errorlevel 1 exit /b 1

echo Distribution updated: %PAGES_BASE_URL%/updatePlugins.xml
