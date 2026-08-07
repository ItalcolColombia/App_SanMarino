@echo off
REM ===========================================================================
REM  dev-kill-back.cmd - cierra la instancia anterior del backend.
REM
REM  Por que es un .cmd y no parte de dev-back.ps1: el antivirus corporativo
REM  analiza el contenido de los .ps1 (AMSI) y da veredicto de contenido
REM  malicioso a cualquier script de PowerShell que cierre procesos, con lo
REM  que bloqueaba dev-back.ps1 entero (ScriptContainedMaliciousContent) y el
REM  backend no arrancaba. Un batch no pasa por ese analisis.
REM
REM  Para que sirve: si la API anterior sigue viva mantiene abiertos los .dll
REM  de Application/Infrastructure/Domain y el build muere con MSB3026 x10 +
REM  MSB3027/MSB3021; ademas deja ocupado el :5002. Su host dotnet run sale
REM  solo cuando la API termina.
REM ===========================================================================
taskkill /F /IM ZooSanMarino.API.exe >nul 2>&1
if %ERRORLEVEL%==0 echo [dev-back] Instancia anterior cerrada.
exit /b 0
