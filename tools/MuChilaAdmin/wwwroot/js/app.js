// Mu Chila Admin: o pouco de JavaScript do painel (o resto é Blazor no servidor).
(function () {
    // tema: escuro por padrão; a escolha fica no navegador
    function aplicarTema() {
        let t = null;
        try { t = localStorage.getItem('muadmin-tema'); } catch (e) { }
        document.documentElement.classList.toggle('dark', t !== 'claro');
    }
    aplicarTema();

    window.muAdmin = {
        alternarTema: function () {
            const escuro = !document.documentElement.classList.contains('dark');
            document.documentElement.classList.toggle('dark', escuro);
            try { localStorage.setItem('muadmin-tema', escuro ? 'escuro' : 'claro'); } catch (e) { }
            return escuro;
        },
        temaEscuro: function () { return document.documentElement.classList.contains('dark'); },

        // baixa um arquivo gerado no servidor (CSV/PDF) a partir de um DotNetStreamReference
        baixar: async function (nome, tipo, streamRef) {
            const buf = await streamRef.arrayBuffer();
            const blob = new Blob([buf], { type: tipo });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url; a.download = nome; document.body.appendChild(a); a.click(); a.remove();
            setTimeout(function () { URL.revokeObjectURL(url); }, 2000);
        },

        copiar: async function (texto) {
            try { await navigator.clipboard.writeText(texto); return true; } catch (e) { return false; }
        },

        focar: function (id) { const el = document.getElementById(id); if (el) { el.focus(); if (el.select) el.select(); } },

        // editor de HTML (notícias e páginas): põe "abre" e "fecha" em volta do trecho selecionado do textarea
        // e avisa o Blazor (evento input) para o texto novo valer
        envolver: function (id, abre, fecha) {
            const el = document.getElementById(id);
            if (!el) return;
            const ini = el.selectionStart, fim = el.selectionEnd, sel = el.value.substring(ini, fim);
            el.setRangeText(abre + sel + fecha, ini, fim, 'end');
            el.focus();
            el.setSelectionRange(ini + abre.length, ini + abre.length + sel.length);
            el.dispatchEvent(new Event('input', { bubbles: true }));
        },

        // atalho "/" para a busca global
        registrarAtalhos: function () {
            if (window.__muAtalhos) return;
            window.__muAtalhos = true;
            document.addEventListener('keydown', function (e) {
                const alvo = e.target;
                const digitando = alvo && (alvo.tagName === 'INPUT' || alvo.tagName === 'TEXTAREA' || alvo.tagName === 'SELECT' || alvo.isContentEditable);
                if (e.key === '/' && !digitando) { const b = document.getElementById('busca-global'); if (b) { e.preventDefault(); b.focus(); } }
            });
        },

        // fecha dropdowns ao clicar fora (o Blazor recebe o aviso)
        aoClicarFora: function (id, dotnet) {
            const h = function (e) {
                const el = document.getElementById(id);
                if (el && !el.contains(e.target)) dotnet.invokeMethodAsync('Fechar');
            };
            document.addEventListener('mousedown', h);
            return { dispose: function () { document.removeEventListener('mousedown', h); } };
        },

        // notificação do Windows/navegador para alertas críticos (só se o usuário permitiu)
        notificar: function (titulo, texto) {
            if (!('Notification' in window)) return;
            if (Notification.permission === 'granted') new Notification(titulo, { body: texto, icon: '/favicon.svg' });
        },
        pedirNotificacoes: async function () {
            if (!('Notification' in window)) return 'indisponivel';
            if (Notification.permission === 'default') return await Notification.requestPermission();
            return Notification.permission;
        },
        permissaoNotificacoes: function () { return ('Notification' in window) ? Notification.permission : 'indisponivel'; }
    };
})();
