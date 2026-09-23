@echo off
setlocal

rem ===========================================================================
rem  Roda a versao PUBLICADA (gerada por publicar-producao.bat) da v1 - o
rem  fluxo real de producao: le os arquivos ja compilados em .\publish, sem
rem  recompilar a cada start. Mesmo banco/config do executar-producao.bat;
rem  so muda a forma de rodar (dotnet publish em vez de dotnet run).
rem
rem  Se voce mudar o codigo, precisa rodar publicar-producao.bat de novo
rem  antes de usar este script, senao continua servindo a versao antiga.
rem ===========================================================================

set DB_USER=prduser
set DB_PASSWORD=senha.123
set DB_HOST=localhost
set DB_PORT=5432
set DB_NAME=PRD_Questoes

set ASPNETCORE_ENVIRONMENT=Production
set ConnectionStrings__DefaultConnection=Host=%DB_HOST%;Port=%DB_PORT%;Database=%DB_NAME%;Username=%DB_USER%;Password=%DB_PASSWORD%

cd /d "%~dp0"

if not exist "publish\bancodequestoes.dll" (
    echo.
    echo [AVISO] Build publicada nao encontrada em .\publish
    echo Rode publicar-producao.bat primeiro.
    echo.
    pause
    exit /b 1
)

echo.
echo  BancoQuestoes - v1 (publicada)
echo  Banco:    %DB_NAME% em %DB_HOST%:%DB_PORT% (usuario: %DB_USER%)
echo  Ambiente: %ASPNETCORE_ENVIRONMENT%
echo.

dotnet publish\bancodequestoes.dll --urls "https://0.0.0.0:7017;http://0.0.0.0:5183"

endlocal
