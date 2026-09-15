// Interop mínimo do widget de chat — só rola o corpo da conversa até o
// fim depois de cada mensagem nova (não dá pra fazer isso só com CSS
// porque a lista cresce dinamicamente via Blazor Server).
window.chatIaRolarParaFim = (elemento) => {
    if (elemento) {
        elemento.scrollTop = elemento.scrollHeight;
    }
};
