/*
 * Mu Chila - loja de itens (modules/usercp/lojaitens.php), 30/09/2026.
 * Vitrine: categorias, busca, filtro por classe e ordem, tudo na própria página.
 * Montagem: nível, adicional, sorte, skill e excelentes com preço ao vivo. A conta é a mesma do servidor
 * (MuChilaLojaItens::precoDetalhado); o servidor recalcula na compra e recusa se o preço mostrado não bater.
 */
(function () {
	'use strict';
	var fmt = function (n) { return Number(n).toLocaleString('pt-BR'); };
	var semAcento = function (s) { return String(s).normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase(); };
	var dadosEl = document.getElementById('mcLojaDados');
	if (dadosEl) montagem(JSON.parse(dadosEl.textContent));
	if (document.getElementById('mcItens')) vitrine();

	// ================================================================ montagem do item
	function montagem(d) {
		var P = d.precos, it = d.item;
		var form = document.getElementById('mcForm');
		var nivel = document.getElementById('mcNivel');
		var excs = Array.prototype.slice.call(form.querySelectorAll('input[name="exc[]"]'));
		var raridades = [['comum', 'Comum'], ['excelente', 'Excelente'], ['excelente', 'Excelente'], ['raro', 'Raro'], ['raro', 'Raro'], ['lendario', 'Lendário'], ['mitico', 'Mítico']];
		var totalAnterior = null;

		function estado() {
			var add = form.querySelector('input[name="adicional"]:checked');
			var s = {
				nivel: parseInt(nivel.value, 10) || 0,
				adicional: add ? parseInt(add.value, 10) : 0,
				sorte: !!(form.sorte && form.sorte.checked),
				skill: !!(form.skill && form.skill.checked),
				exc: excs.filter(function (e) { return e.checked; }).length
			};
			return s;
		}
		function linhas(s) {
			var l = [['Item', it.preco]];
			if (s.nivel > 0) l.push(['Nível +' + s.nivel, P.nivel[s.nivel]]);
			if (s.adicional > 0) l.push(['Adicional +' + s.adicional * it.passo + it.sufixo, P.adicional[s.adicional]]);
			if (s.sorte) l.push(['Sorte', P.sorte]);
			if (s.skill) l.push(['Skill', P.skill]);
			if (s.exc > 0) l.push(['Excelente (' + s.exc + ')', P.excelente[s.exc]]);
			return l;
		}
		function nomeCompleto(s) {
			return it.nome + (s.nivel > 0 ? ' +' + s.nivel : '') + (s.adicional > 0 ? ' +' + s.adicional * it.passo + it.sufixo : '');
		}
		function opcoesTexto(s) {
			var o = [];
			if (s.skill) o.push('Skill');
			if (s.sorte) o.push('Sorte');
			if (s.exc) o.push(s.exc + ' excelente' + (s.exc > 1 ? 's' : ''));
			return o.length ? o.join(' · ') : 'sem opções extras';
		}

		function render() {
			var s = estado();
			// nível
			document.getElementById('mcNivelTxt').textContent = '+' + s.nivel;
			nivel.style.setProperty('--p', (s.nivel / Math.max(1, it.nivel_max) * 100) + '%');
			form.querySelector('[data-passo="-1"]').disabled = s.nivel <= 0;
			form.querySelector('[data-passo="1"]').disabled = s.nivel >= it.nivel_max;
			// excelentes: trava as desmarcadas no limite
			excs.forEach(function (e) {
				var lab = e.closest('.mc-exc');
				lab.classList.toggle('is-on', e.checked);
				var trava = !e.checked && s.exc >= it.exc_max;
				e.disabled = trava;
				lab.classList.toggle('is-locked', trava);
			});
			var conta = document.getElementById('mcExcConta');
			if (conta) conta.textContent = s.exc + '/' + it.exc_max;
			// preço
			var l = linhas(s), total = l.reduce(function (a, x) { return a + x[1]; }, 0);
			document.getElementById('mcLinhas').innerHTML = l.map(function (x) {
				return '<li><span>' + x[0] + '</span><span>' + (x[1] > 0 ? fmt(x[1]) : 'grátis') + '</span></li>';
			}).join('');
			var tot = document.getElementById('mcTotal');
			tot.innerHTML = fmt(total) + ' <small>Cash</small>';
			if (totalAnterior !== null && totalAnterior !== total) {
				tot.classList.remove('is-bump'); void tot.offsetWidth; tot.classList.add('is-bump');
				setTimeout(function () { tot.classList.remove('is-bump'); }, 180);
			}
			totalAnterior = total;
			document.getElementById('mcPreco').value = total;
			// saldo
			var depois = d.saldo - total, dep = document.getElementById('mcDepois');
			dep.classList.toggle('is-short', depois < 0);
			dep.textContent = depois < 0 ? 'Faltam ' + fmt(-depois) + ' Cash (você tem ' + fmt(d.saldo) + ').' : 'Seu saldo depois da compra: ' + fmt(depois) + ' Cash.';
			document.getElementById('mcComprar').hidden = depois < 0;
			document.getElementById('mcRecarregar').hidden = depois >= 0;
			// nome e raridade
			document.getElementById('mcNome').textContent = nomeCompleto(s);
			var r = raridades[Math.min(6, s.exc)];
			document.getElementById('mcShow').setAttribute('data-raridade', r[0]);
			document.getElementById('mcRaridade').textContent = r[1] + (s.nivel >= 13 ? ' · refinado' : '');
			// confirmação
			document.getElementById('mcConfNome').textContent = nomeCompleto(s);
			document.getElementById('mcConfOpcoes').textContent = opcoesTexto(s);
			document.getElementById('mcConfPreco').textContent = fmt(total) + ' Cash';
			document.getElementById('mcConfSaldo').textContent = fmt(Math.max(0, depois)) + ' Cash';
		}

		form.addEventListener('input', render);
		form.addEventListener('change', render);
		form.querySelectorAll('[data-passo]').forEach(function (b) {
			b.addEventListener('click', function () {
				nivel.value = Math.max(0, Math.min(it.nivel_max, (parseInt(nivel.value, 10) || 0) + parseInt(b.getAttribute('data-passo'), 10)));
				render();
			});
		});
		form.querySelectorAll('[data-exc]').forEach(function (b) {
			b.addEventListener('click', function () {
				var todas = b.getAttribute('data-exc') === 'todas', n = 0;
				excs.forEach(function (e) { e.disabled = false; e.checked = todas && n++ < it.exc_max; });
				render();
			});
		});

		// comprar: abre a confirmação; só o botão "Confirmar e pagar" envia
		var confirmar = document.getElementById('mcConfirmar'), confirmado = false;
		confirmar.addEventListener('click', function () { confirmado = true; });   // navegador sem e.submitter
		form.addEventListener('submit', function (e) {
			if (e.submitter === confirmar || confirmado) {
				confirmar.disabled = true;
				confirmar.innerHTML = 'Processando...';
				return;
			}
			e.preventDefault();
			render();
			window.mcJanela.abrir(document.getElementById('mcConfirma'));
		});

		// vitrine 3D: inclina com o mouse, clique amplia
		var show = document.getElementById('mcShow'), stage = document.getElementById('mcStage');
		var movimento = !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
		if (movimento) {
			show.addEventListener('pointermove', function (e) {
				var r = show.getBoundingClientRect();
				var x = (e.clientX - r.left) / r.width - .5, y = (e.clientY - r.top) / r.height - .5;
				stage.style.transform = 'rotateY(' + (x * 22) + 'deg) rotateX(' + (-y * 18) + 'deg)';
			});
			show.addEventListener('pointerleave', function () { stage.style.transform = ''; });
		}
		stage.addEventListener('click', function () { stage.classList.toggle('is-zoom'); });
		render();
	}

	// ================================================================ vitrine
	function vitrine() {
		var grade = document.getElementById('mcItens');
		var itens = Array.prototype.slice.call(grade.querySelectorAll('.mc-item'));
		var cats = Array.prototype.slice.call(document.querySelectorAll('.mc-cat'));
		var busca = document.getElementById('mcBusca'), classe = document.getElementById('mcClasse'), ordem = document.getElementById('mcOrdem');
		var cat = decodeURIComponent(location.hash.replace('#', ''));
		if (!cats.some(function (c) { return c.getAttribute('data-cat') === cat; })) cat = '';
		var espera;

		function aplicar() {
			var q = semAcento(busca.value.trim()), cl = classe.value, n = 0;
			itens.forEach(function (el) {
				var ok = (!cat || el.getAttribute('data-cat') === cat)
					&& (!q || semAcento(el.getAttribute('data-nome')).indexOf(q) >= 0)
					&& (!cl || el.getAttribute('data-classes').split('|').indexOf(cl) >= 0);
				el.hidden = !ok;
				if (ok) n++;
			});
			cats.forEach(function (c) { c.classList.toggle('is-active', c.getAttribute('data-cat') === cat); });
			document.getElementById('mcContagem').textContent = n + (n === 1 ? ' item' : ' itens');
			document.getElementById('mcVazio').hidden = n > 0;
		}
		function ordenar() {
			var o = ordem.value, campo = o.replace('-', ''), inv = o.charAt(0) === '-' ? -1 : 1;
			itens.slice().sort(function (a, b) {
				if (!o) return (b.getAttribute('data-destaque') - a.getAttribute('data-destaque')) || (a.getAttribute('data-ordem') - b.getAttribute('data-ordem'));
				if (campo === 'nome') return a.getAttribute('data-nome').localeCompare(b.getAttribute('data-nome'));
				return inv * (a.getAttribute('data-' + campo) - b.getAttribute('data-' + campo)) || a.getAttribute('data-ordem') - b.getAttribute('data-ordem');
			}).forEach(function (el) { grade.appendChild(el); });
		}

		cats.forEach(function (c) {
			c.addEventListener('click', function () {
				cat = c.getAttribute('data-cat');
				history.replaceState(null, '', cat ? '#' + cat : location.pathname + location.search);
				aplicar();
				if (window.innerWidth < 1080) grade.scrollIntoView({ behavior: 'smooth', block: 'start' });
			});
		});
		busca.addEventListener('input', function () { clearTimeout(espera); espera = setTimeout(aplicar, 120); });
		classe.addEventListener('change', aplicar);
		ordem.addEventListener('change', ordenar);
		document.getElementById('mcLimpar').addEventListener('click', function () { busca.value = ''; classe.value = ''; cat = ''; aplicar(); });
		ordenar();
		aplicar();
	}
})();
