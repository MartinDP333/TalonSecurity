@echo off
setlocal
cd /d "%~dp0"

echo Restoring TalonSecurity settings and updating Kubernetes services from Docker Hub...
echo Publish fresh :latest images before running this script.
set "EXIT_CODE=0"

for /f "delims=" %%C in ('kubectl config current-context 2^>nul') do set "KUBE_CONTEXT=%%C"
if not "%KUBE_CONTEXT%"=="docker-desktop" (
  echo ERROR: Expected Kubernetes context docker-desktop, found "%KUBE_CONTEXT%".
  echo Switch context with: kubectl config use-context docker-desktop
  goto :failed
)

kubectl get secret talon-secrets >nul 2>&1
if errorlevel 1 (
  echo ERROR: Kubernetes secret talon-secrets is missing.
  goto :failed
)

rem Restore the full Deployment specs first, then only override image tags below.
kubectl apply -f "k8s\api.yaml"
if errorlevel 1 goto :failed
kubectl apply -f "k8s\ui.yaml"
if errorlevel 1 goto :failed
kubectl apply -f "k8s\worker.yaml"
if errorlevel 1 goto :failed
kubectl apply -f "k8s\waf.yaml"
if errorlevel 1 goto :failed
kubectl set env deployment/talon-api ASPNETCORE_ENVIRONMENT=Development TenantResolution__AllowHeaderForTesting=true TenantResolution__UseProtectedSites=true
if errorlevel 1 goto :failed

kubectl set image deployment/talon-api api=martin313/talon-api:latest
if errorlevel 1 goto :failed
kubectl set image deployment/talon-ui ui=martin313/talon-ui:latest
if errorlevel 1 goto :failed
kubectl set image deployment/talon-worker worker=martin313/talon-worker:latest
if errorlevel 1 goto :failed
kubectl set image deployment/talon-waf waf-adapter=martin313/talon-waf-adapter:latest
if errorlevel 1 goto :failed
for %%D in (talon-api talon-ui talon-worker talon-waf) do (
  echo Restarting %%D...
  kubectl rollout restart deployment/%%D
  if errorlevel 1 goto :failed
)

for %%D in (talon-api talon-ui talon-worker talon-waf) do (
  echo Waiting for %%D rollout...
  kubectl rollout status deployment/%%D --timeout=180s
  if errorlevel 1 goto :failed
)

echo.
echo TalonSecurity services now use the latest Docker Hub images.
goto :finish

:failed
echo.
echo ERROR: Update failed. Check Docker Hub image availability and Kubernetes events.
set "EXIT_CODE=1"

:finish
echo.
pause
exit /b %EXIT_CODE%
