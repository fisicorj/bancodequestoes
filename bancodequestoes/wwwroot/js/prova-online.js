// Monitoramento de anti-cola da prova online: entra em tela cheia e observa perda de foco
// e saída da tela cheia, avisando o componente Blazor pra encerrar a tentativa na hora.
// Limitação conhecida (documentada pro usuário): isso DETECTA, não IMPEDE — um aluno
// tecnicamente capaz consegue burlar (outro dispositivo, desabilitar JS etc.). Também não dá
// pra bloquear atalhos de troca de app do sistema operacional (ex.: Alt+Tab), só reagir depois
// que o foco já saiu.
window.provaOnline = {
    _dotNetRef: null,
    _handlerVisibilidade: null,
    _handlerBlur: null,
    _handlerTelaCheia: null,

    iniciar: function (dotNetRef) {
        this._dotNetRef = dotNetRef;

        // Sem gesto do usuário garantido (o clique já disparou uma chamada assíncrona ao
        // servidor Blazor antes de chegar aqui), então o pedido de tela cheia pode ser
        // recusado por alguns navegadores — nesse caso a prova segue sem tela cheia, e o
        // monitoramento de foco/visibilidade continua valendo normalmente.
        var elemento = document.documentElement;
        if (elemento.requestFullscreen) {
            elemento.requestFullscreen().catch(function () { /* recusado — segue sem tela cheia */ });
        }

        this._handlerVisibilidade = function () {
            if (document.hidden) {
                dotNetRef.invokeMethodAsync('OnPerdaFoco');
            }
        };
        this._handlerBlur = function () {
            dotNetRef.invokeMethodAsync('OnPerdaFoco');
        };
        this._handlerTelaCheia = function () {
            if (!document.fullscreenElement) {
                dotNetRef.invokeMethodAsync('OnSaiuTelaCheia');
            }
        };

        document.addEventListener('visibilitychange', this._handlerVisibilidade);
        window.addEventListener('blur', this._handlerBlur);
        document.addEventListener('fullscreenchange', this._handlerTelaCheia);
    },

    // Chamado quando a tentativa termina (enviada ou encerrada) — sai da tela cheia e para
    // de escutar os eventos, senão o envio normal do aluno dispararia OnSaiuTelaCheia à toa.
    parar: function () {
        if (this._handlerVisibilidade) {
            document.removeEventListener('visibilitychange', this._handlerVisibilidade);
        }
        if (this._handlerBlur) {
            window.removeEventListener('blur', this._handlerBlur);
        }
        if (this._handlerTelaCheia) {
            document.removeEventListener('fullscreenchange', this._handlerTelaCheia);
        }
        this._handlerVisibilidade = this._handlerBlur = this._handlerTelaCheia = null;

        if (document.fullscreenElement && document.exitFullscreen) {
            document.exitFullscreen().catch(function () { });
        }
    },

    // Cronômetro de UX pro TempoLimiteMinutos: só atualiza o texto na tela e avisa o
    // componente Blazor quando zera, pra ele chamar o envio automático. A fonte de verdade do
    // prazo é sempre o servidor (RespostaProvaOnlineService.TempoEsgotado) — esse timer não
    // "aplica" limite nenhum sozinho, então desligar o JS não dá mais tempo de verdade.
    cronometro: {
        _intervalo: null,

        iniciar: function (dotNetRef, prazoUtcIso, elementId) {
            this.parar();
            var prazo = new Date(prazoUtcIso).getTime();
            var elemento = document.getElementById(elementId);

            var atualizar = function () {
                var restanteMs = prazo - Date.now();
                if (restanteMs <= 0) {
                    if (elemento) {
                        elemento.textContent = "00:00";
                    }
                    window.provaOnline.cronometro.parar();
                    dotNetRef.invokeMethodAsync('OnTempoEsgotado');
                    return;
                }

                var totalSegundos = Math.floor(restanteMs / 1000);
                var minutos = Math.floor(totalSegundos / 60);
                var segundos = totalSegundos % 60;
                if (elemento) {
                    elemento.textContent =
                        (minutos < 10 ? "0" : "") + minutos + ":" + (segundos < 10 ? "0" : "") + segundos;
                }
            };

            atualizar();
            this._intervalo = setInterval(atualizar, 1000);
        },

        parar: function () {
            if (this._intervalo) {
                clearInterval(this._intervalo);
                this._intervalo = null;
            }
        }
    }
};
