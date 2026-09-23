@echo off
setlocal

rem ===========================================================================
rem  Gera uma build publicada (dotnet publish, Release) da v1, pra testar o
rem  fluxo real de producao: sem recompilar a cada start, com os arquivos
rem  estaticos ja resolvidos em wwwroot (sem depender do UseStaticWebAssets()
rem  usado pelo executar-producao.bat, que roda direto do codigo-fonte).
rem
rem  Rode este script de novo sempre que o codigo mudar e voce quiser testar
rem  a versao publicada com as atualizacoes.
rem ===========================================================================

cd /d "%~dp0"

rem Limpa artefatos intermediarios antes de publicar. Sem isso, o cache de
rem arquivos estaticos comprimidos (gzip/brotli) do build incremental pode
rem ficar com uma versao vazia/corrompida de algum .js/.css, e o navegador
rem recusa o arquivo (erro de "integrity"/SHA-256 nao bate) mesmo o codigo
rem estando correto.
echo Limpando build anterior...
if exist "publish" rmdir /s /q "publish"
if exist "bin\Release" rmdir /s /q "bin\Release"
if exist "obj\Release" rmdir /s /q "obj\Release"

echo Publicando (Release) em .\publish ...
dotnet publish -c Release -o "publish"

if errorlevel 1 (
    echo.
    echo [ERRO] "dotnet publish" falhou - veja a mensagem acima.
    pause
    exit /b 1
)

echo.
echo Build publicada em: %~dp0publish
echo Use executar-publicado.bat para rodar essa versao.
pause

endlocal
