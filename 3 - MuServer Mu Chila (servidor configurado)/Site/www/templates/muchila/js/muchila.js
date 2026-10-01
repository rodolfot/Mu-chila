/*
 * Mu Chila - comportamento do tema (30/09/2026). Sem dependências além do jQuery que o WebEngine já carrega.
 * Mantém o que os módulos do WebEngine esperam da template padrão: relógio do servidor (api/servertime.php), contagem do
 * Castle Siege (api/castlesiege.php), quadro de eventos (api/events.php) e o filtro de classes dos rankings.
 * Extras: menu do celular, menu da conta, efeito de clique, avisos flutuantes (mcToast) e janelas (data-mc-open).
 */
(function () {
	'use strict';

	// ---------------- menu do celular e menu da conta
	var nav = document.getElementById('mcNav');
	var toggle = document.querySelector('[data-mc-toggle]');
	if (nav && toggle) {
		toggle.addEventListener('click', function () {
			var aberto = nav.classList.toggle('is-open');
			toggle.setAttribute('aria-expanded', aberto ? 'true' : 'false');
			document.body.style.overflow = aberto ? 'hidden' : '';
		});
	}
	document.querySelectorAll('[data-mc-menu]').forEach(function (m) {
		var btn = m.querySelector('button');
		btn.addEventListener('click', function (e) {
			e.stopPropagation();
			var aberto = m.classList.toggle('is-open');
			btn.setAttribute('aria-expanded', aberto ? 'true' : 'false');
		});
	});
	document.addEventListener('click', function () {
		document.querySelectorAll('[data-mc-menu].is-open').forEach(function (m) { m.classList.remove('is-open'); });
	});
	document.addEventListener('keydown', function (e) {
		if (e.key !== 'Escape') return;
		document.querySelectorAll('[data-mc-menu].is-open').forEach(function (m) { m.classList.remove('is-open'); });
		document.querySelectorAll('.mc-modal.is-open').forEach(fecharJanela);
	});

	// ---------------- efeito de clique nos botões
	document.addEventListener('pointerdown', function (e) {
		var b = e.target.closest && e.target.closest('.mc-btn, .btn');
		if (!b || b.disabled) return;
		var r = b.getBoundingClientRect(), s = Math.max(r.width, r.height);
		var o = document.createElement('span');
		o.className = 'mc-ripple';
		o.style.width = o.style.height = s + 'px';
		o.style.left = (e.clientX - r.left - s / 2) + 'px';
		o.style.top = (e.clientY - r.top - s / 2) + 'px';
		if (getComputedStyle(b).position === 'static') b.style.position = 'relative';
		b.style.overflow = 'hidden';
		b.appendChild(o);
		setTimeout(function () { o.remove(); }, 650);
	});

	// ---------------- avisos flutuantes: mcToast('texto', 'check')
	window.mcToast = function (texto, icone) {
		var c = document.getElementById('mcToasts');
		if (!c) return;
		var t = document.createElement('div');
		t.className = 'mc-toast';
		t.innerHTML = '<svg class="mc-ico"><use href="' + baseUrl + 'templates/muchila/img/icones.svg#' + (icone || 'check') + '"></use></svg><span></span>';
		t.querySelector('span').textContent = texto;
		c.appendChild(t);
		setTimeout(function () { t.classList.add('is-out'); setTimeout(function () { t.remove(); }, 320); }, 4200);
	};

	// ---------------- janelas: <button data-mc-open="id">, dentro da janela [data-mc-close]
	function abrirJanela(m) {
		m.classList.add('is-open');
		document.body.style.overflow = 'hidden';
		var f = m.querySelector('[data-mc-focus], button, a');
		if (f) setTimeout(function () { f.focus(); }, 60);
	}
	function fecharJanela(m) { m.classList.remove('is-open'); document.body.style.overflow = ''; }
	window.mcJanela = { abrir: abrirJanela, fechar: fecharJanela };
	document.addEventListener('click', function (e) {
		var a = e.target.closest && e.target.closest('[data-mc-open]');
		if (a) { var m = document.getElementById(a.getAttribute('data-mc-open')); if (m) { e.preventDefault(); abrirJanela(m); } return; }
		var c = e.target.closest && e.target.closest('[data-mc-close]');
		if (c) { var j = c.closest('.mc-modal'); if (j) fecharJanela(j); }
	});

	// ---------------- copiar (PIX): <button data-mc-copy="#id">
	document.addEventListener('click', function (e) {
		var b = e.target.closest && e.target.closest('[data-mc-copy]');
		if (!b) return;
		var alvo = document.querySelector(b.getAttribute('data-mc-copy'));
		if (!alvo) return;
		var txt = alvo.value || alvo.textContent;
		(navigator.clipboard ? navigator.clipboard.writeText(txt) : Promise.reject()).then(function () {
			window.mcToast('Código copiado!', 'copy');
		}, function () { alvo.select && alvo.select(); document.execCommand('copy'); window.mcToast('Código copiado!', 'copy'); });
	});
})();

// ---------------- relógio do servidor (mesmos ids da template padrão)
var serverTime = {
	weekDays: ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'],
	monthNames: ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'],
	init: function (e, c, s, l) {
		var f = this;
		if (!document.getElementById(e)) return;
		f.ids = [e, c, s, l];
		$.getJSON(baseUrl + 'api/servertime.php', function (a) {
			f.offset = new Date(a.ServerTime) - new Date();
			f.update();
			setInterval(function () { f.update(); }, 1000);
		});
	},
	update: function () {
		var agora = new Date(), srv = new Date(agora.getTime() + this.offset);
		this.set(0, this.time(srv)); this.set(1, this.time(agora));
		this.set(2, this.date(srv)); this.set(3, this.date(agora));
	},
	set: function (i, v) { var el = document.getElementById(this.ids[i]); if (el) el.textContent = v; },
	time: function (d) { return [d.getHours(), d.getMinutes(), d.getSeconds()].map(function (n) { return ('0' + n).slice(-2); }).join(':'); },
	date: function (d) { return this.weekDays[d.getDay()] + ', ' + d.getDate() + ' ' + this.monthNames[d.getMonth()]; }
};

// ---------------- contagem do Castle Siege
var csTime = {
	init: function () {
		var a = this;
		if (!document.getElementById('cscountdown') && !document.getElementById('siegeTimer')) return;
		$.getJSON(baseUrl + 'api/castlesiege.php', function (c) {
			a.left = c.TimeLeft;
			setInterval(function () { a.update(); }, 1000);
		});
	},
	update: function () {
		this.left--;
		var t = this.left, txt;
		if (t < 1) txt = 'Batalha!';
		else {
			var d = Math.floor(t / 86400), h = Math.floor(t % 86400 / 3600), m = Math.floor(t % 3600 / 60), s = t % 60;
			txt = (t > 86400 ? d + 'd ' : '') + (t > 3600 ? h + 'h ' : '') + (t > 60 ? m + 'm ' : '') + s + 's';
		}
		['cscountdown', 'siegeTimer'].forEach(function (id) { var el = document.getElementById(id); if (el) el.textContent = txt; });
	}
};

// ---------------- quadro de eventos (ids <evento>, <evento>_name, <evento>_next)
function loadEventSchedule() {
	$.getJSON(baseUrl + 'api/events.php', function (data) {
		$.each(data, function (key, val) {
			if (!document.getElementById(key)) return;
			var bloco = document.getElementById(key + '_box');
			if (bloco) bloco.hidden = false;
			eventSchedule(key, val.opentime, val.duration, val.offset, val.timeleft);
			document.getElementById(key + '_name').textContent = val.event;
			document.getElementById(key + '_next').textContent = val.nextF;
		});
	});
}
function eventSchedule(id, openTime, duration, offset, timeLeft) {
	var el = document.getElementById(id);
	function recarregar() {
		$.getJSON(baseUrl + 'api/events.php?event=' + id, function (d) {
			openTime = d.opentime; duration = d.duration; offset = d.offset; timeLeft = d.timeleft;
			document.getElementById(id + '_name').textContent = d.event;
			document.getElementById(id + '_next').textContent = d.nextF;
		});
	}
	setInterval(function () {
		if (timeLeft < 1) { recarregar(); timeLeft = 1; return; }
		if (openTime > 0 && offset - timeLeft < openTime) { el.innerHTML = '<span class="event-schedule-open">Entrada aberta</span>'; timeLeft--; return; }
		if (openTime <= 0 && duration > 0 && offset - timeLeft < duration) { el.innerHTML = '<span class="event-schedule-inprogress">Acontecendo</span>'; timeLeft--; return; }
		var d = Math.floor(timeLeft / 86400), h = Math.floor(timeLeft % 86400 / 3600), m = Math.floor(timeLeft % 3600 / 60), s = timeLeft % 60;
		el.textContent = (d > 0 ? d + 'd ' : '') + (d > 0 || h > 0 ? h + 'h ' : '') + ('0' + m).slice(-2) + 'm ' + ('0' + s).slice(-2) + 's';
		timeLeft--;
	}, 1000);
}

// ---------------- filtro de classes dos rankings (chamado pelo módulo do WebEngine)
function rankingsFilterByClass() {
	var classes = Array.prototype.slice.call(arguments);
	$('.rankings-table tr').each(function () {
		var c = $(this).attr('data-class-id');
		if (c == null) return true;
		$(this).toggle(classes.indexOf(parseInt(c, 10)) >= 0);
	});
}
function rankingsFilterRemove() { $('.rankings-table tr').show(); }

$(function () {
	serverTime.init('tServerTime', 'tLocalTime', 'tServerDate', 'tLocalDate');
	csTime.init();
	if (document.getElementById('mcEvents')) loadEventSchedule();
	$('[data-toggle="tooltip"]').tooltip();
	$('a.rankings-class-filter-selection').click(function () {
		$('a.rankings-class-filter-selection').addClass('rankings-class-filter-grayscale');
		$(this).removeClass('rankings-class-filter-grayscale');
	});
});
