// Baixa uma string de texto como arquivo, direto no navegador — usado pela
// exportação de questões em GIFT/Aiken, que gera o conteúdo no servidor
// (Blazor Server, sem endpoint HTTP próprio) e precisa entregá-lo como
// download sem trocar de página.
window.bqDownloadText = (filename, content) => {
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};

// Calcula o novo texto de um textarea depois de aplicar um botão da toolbar
// de formatação (negrito, itálico, lista, código, tabela, fórmula) sobre a
// seleção atual — envolve o texto selecionado com a marcação Markdown certa,
// ou insere um texto de exemplo se nada estiver selecionado. NÃO toca no
// value do elemento nem dispara eventos: só calcula e devolve a string nova,
// pra quem chamou (o componente Blazor) decidir o que fazer com ela — assim
// não corre risco de o Blazor "brigar" com uma mudança feita direto no DOM.
window.bqToolbarAction = (textareaId, action) => {
    const el = document.getElementById(textareaId);
    if (!el) {
        return null;
    }

    const start = el.selectionStart ?? el.value.length;
    const end = el.selectionEnd ?? el.value.length;
    const selecionado = el.value.substring(start, end);

    let antes = '', depois = '', exemplo = '';
    switch (action) {
        case 'negrito': antes = '**'; depois = '**'; exemplo = 'texto em negrito'; break;
        case 'italico': antes = '_'; depois = '_'; exemplo = 'texto em itálico'; break;
        case 'codigo-linha': antes = '`'; depois = '`'; exemplo = 'código'; break;
        case 'codigo-bloco': antes = '\n```\n'; depois = '\n```\n'; exemplo = 'seu código aqui'; break;
        case 'lista': antes = '\n- '; depois = ''; exemplo = 'item da lista'; break;
        case 'formula': antes = '$'; depois = '$'; exemplo = 'x^2 + y^2 = z^2'; break;
        case 'tabela': antes = '\n'; depois = ''; exemplo = '| Coluna 1 | Coluna 2 |\n| --- | --- |\n| valor 1 | valor 2 |\n'; break;
        default: return null;
    }

    const texto = selecionado.length > 0 ? selecionado : exemplo;
    return el.value.substring(0, start) + antes + texto + depois + el.value.substring(end);
};

// Manda o MathJax (re)processar as fórmulas dentro de um elemento — usado na
// pré-visualização ao vivo do enunciado da questão. MathJax carrega de forma
// assíncrona (via CDN), então espera a promise de inicialização antes de
// tentar usar typesetPromise.
window.bqTypesetMath = async (elementId) => {
    const el = document.getElementById(elementId);
    if (!el || !window.MathJax) {
        return;
    }
    try {
        if (window.MathJax.startup && window.MathJax.startup.promise) {
            await window.MathJax.startup.promise;
        }
        if (window.MathJax.typesetClear) {
            window.MathJax.typesetClear([el]);
        }
        if (window.MathJax.typesetPromise) {
            await window.MathJax.typesetPromise([el]);
        }
    } catch (e) {
        console.error('Falha ao renderizar fórmula:', e);
    }
};
