@echo off
setlocal
cd /d "%~dp0"
set "IMAGE=martin313/talon-api:latest"
echo Building %IMAGE%...
docker build -f API\Dockerfile -t "%IMAGE%" .
if errorlevel 1 goto :failed
echo Pushing %IMAGE% to Docker Hub...
docker push "%IMAGE%"
if errorlevel 1 goto :failed
echo.
echo API image built and pushed successfully.
goto :finish
:failed
echo.
echo ERROR: API build or push failed. Check Docker status and run docker login if needed.
set "EXIT_CODE=1"
:finish
if not defined EXIT_CODE set "EXIT_CODE=0"
echo.
pause
exit /b %EXIT_CODE%
