@echo off
setlocal

rem ===========================================================================
rem  Roda a v1 do BancoQuestoes em modo "producao" (ASPNETCORE_ENVIRONMENT=
rem  Production) contra um banco Postgres proprio, separado do banco de
rem  desenvolvimento. Da pra clicar duas vezes neste arquivo sempre que for
rem  usar o sistema de verdade.
rem
rem  ANTES DE USAR PELA PRIMEIRA VEZ:
rem    1) Preencha DB_USER e DB_PASSWORD abaixo com as credenciais do seu
rem       Postgres local.
rem    2) Crie o banco (uma vez so), por exemplo via psql:
rem         createdb -U SEU_USUARIO bancodequestoes_producao
rem       O app cria as tabelas sozinho (migrations automaticas) na primeira
rem       vez que subir - so o banco em si (vazio) precisa existir antes.
rem
rem  ACESSO DE OUTRO PC NA MESMA REDE:
rem    O app escuta em 0.0.0.0 (todas as interfaces), entao outro
rem    computador/celular no mesmo Wi-Fi/roteador consegue acessar. Nesta
rem    mesma maquina, rode "ipconfig" e pegue o "Endereco IPv4" (ex.:
rem    192.168.1.23). No outro PC, abra:
rem      https://192.168.1.23:7017
rem    O certificado e valido so para "localhost", entao o navegador do
rem    outro PC vai avisar "conexao nao segura" na primeira vez - isso e
rem    esperado aqui (rede local confiavel); clique em avancado/continuar.
rem    Tambem e preciso liberar a porta no Firewall do Windows NESTA maquina,
rem    uma vez so, com o Prompt de Comando/PowerShell como administrador:
rem      netsh advfirewall firewall add rule name="BancoQuestoes v1" dir=in action=allow protocol=TCP localport=7017
rem ===========================================================================

set DB_USER=prduser
set DB_PASSWORD=senha.123
set DB_HOST=localhost
set DB_PORT=5432
set DB_NAME=PRD_Questoes

set ASPNETCORE_ENVIRONMENT=Production
set ConnectionStrings__DefaultConnection=Host=%DB_HOST%;Port=%DB_PORT%;Database=%DB_NAME%;Username=%DB_USER%;Password=%DB_PASSWORD%

echo.
echo  BancoQuestoes - v1 (producao)
echo  Banco:    %DB_NAME% em %DB_HOST%:%DB_PORT% (usuario: %DB_USER%)
echo  Ambiente: %ASPNETCORE_ENVIRONMENT%
echo.

if "%DB_USER%"=="SEU_USUARIO_POSTGRES" (
    echo  [AVISO] Voce ainda nao preencheu DB_USER/DB_PASSWORD neste arquivo.
    echo  Edite executar-producao.bat antes de continuar.
    echo.
    pause
    exit /b 1
)

cd /d "%~dp0"
dotnet run -c Release --no-launch-profile --urls "https://0.0.0.0:7017;http://0.0.0.0:5183"

endlocal
