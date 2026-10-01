// carregado no <head>, antes de desenhar a página: aplica o tema salvo sem "piscar" (escuro é o padrão)
(function () {
    var t = null;
    try { t = localStorage.getItem('muadmin-tema'); } catch (e) { }
    if (t === 'claro') document.documentElement.classList.remove('dark');
    else document.documentElement.classList.add('dark');
})();
