<?php
/**
 * Mu Chila - agenda do quadro "Eventos" do site, lida dos arquivos de evento do servidor (C:\MuServer\Data\Event).
 * Usada pelo api/events.php (o WebEngine vinha com horários fixos de exemplo). Mudou a agenda do jogo, o site acompanha.
 * Retorna o formato do WebEngine: id => [name, opentime (s de entrada antes do início), duration (s), schedule ['HH:MM', ...]]
 * ou, para evento semanal, [name, opentime, duration, day, time].
 */

require_once(__DIR__ . '/MuChilaLoja.php');   // conexão com o banco (início do ciclo do Castle Siege)
$muchilaEventos = dirname(__DIR__, 4) . '/Data/Event/';

/** Linhas de dados do bloco N (o número sozinho na linha até "end"), já separadas em colunas. */
$muchilaBloco = function(string $arquivo, int $bloco) use ($muchilaEventos): array {
	$linhas = @file($muchilaEventos . $arquivo, FILE_IGNORE_NEW_LINES) ?: [];
	$dentro = false; $dados = [];
	foreach($linhas as $l) {
		$l = trim(preg_replace('#//.*$#', '', $l));
		if($l === '') continue;
		if(!$dentro) { if($l === (string)$bloco) $dentro = true; continue; }
		if(strtolower($l) === 'end') break;
		$dados[] = preg_split('/\s+/', $l);
	}
	return $dados;
};

/** Horários diários ('HH:MM') das linhas Ano Mês Dia DiaDaSemana Hora Minuto; hora "*" = toda hora. Só entra o que vale todo dia. */
$muchilaHorarios = function(array $linhas, int $pular = 0): array {
	$horas = [];
	foreach($linhas as $c) {
		$c = array_slice($c, $pular);
		if(count($c) < 6 || $c[0] !== '*' || $c[1] !== '*' || $c[2] !== '*' || $c[3] !== '*' || !ctype_digit($c[5])) continue;
		foreach($c[4] === '*' ? range(0, 23) : (ctype_digit($c[4]) ? [(int)$c[4]] : []) as $h)
			$horas[] = sprintf('%02d:%02d', $h, (int)$c[5]);
	}
	$horas = array_values(array_unique($horas));
	sort($horas);
	return $horas;
};

$eventTimes = [];
foreach(['bloodcastle' => ['Blood Castle', 'BloodCastle.dat'], 'devilsquare' => ['Devil Square', 'DevilSquare.dat'], 'chaoscastle' => ['Chaos Castle', 'ChaosCastle.dat']] as $id => [$nome, $arquivo]) {
	$aviso = $muchilaBloco($arquivo, 0)[0][0] ?? 5;   // WarningTime: minutos de entrada antes do início
	$eventTimes[$id] = ['name' => $nome, 'opentime' => (int)$aviso * 60, 'duration' => 0, 'schedule' => $muchilaHorarios($muchilaBloco($arquivo, 1))];
}

// invasões: bloco 0 = Índice + horário; bloco 1 = Índice ... InvasionTime (duração em segundos)
// (ids que a página inicial do WebEngine já tem na tabela: dragoninvasion e goldeninvasion)
$invasoes = ['dragoninvasion' => [1, 'Invasão do Red Dragon'], 'goldeninvasion' => [2, 'Invasão Dourada']];
$agendaInvasoes = $muchilaBloco('InvasionManager.dat', 0);
$duracoes = [];
foreach($muchilaBloco('InvasionManager.dat', 1) as $c) if(isset($c[5]) && ctype_digit($c[0])) $duracoes[(int)$c[0]] = (int)$c[5];
foreach($invasoes as $id => [$indice, $nome]) {
	$linhas = array_filter($agendaInvasoes, function($c) use ($indice) { return ($c[0] ?? '') === (string)$indice; });
	$eventTimes[$id] = ['name' => $nome, 'opentime' => 0, 'duration' => $duracoes[$indice] ?? 600, 'schedule' => $muchilaHorarios($linhas, 1)];
}

// Castle Siege: ciclo do MuCastleData.dat (estado 7 = início do cerco, 8 = fim) contado a partir do início do ciclo no banco
try {
	$estados = [];
	foreach($muchilaBloco('MuCastleData.dat', 1) as $c) if(count($c) >= 4) $estados[(int)$c[0]] = [(int)$c[1], (int)$c[2], (int)$c[3]];
	if(isset($estados[7], $estados[8]) && class_exists('MuChilaLoja')) {
		$inicioCiclo = MuChilaLoja::conectar()->query("SELECT TOP 1 SIEGE_START_DATE FROM MuCastle_DATA")->fetchColumn();
		if($inicioCiclo) {
			[$dia, $hora, $minuto] = $estados[7];
			$cerco = strtotime(date('Y-m-d', strtotime($inicioCiclo)) . " +$dia days $hora:$minuto");
			$duracao = ($estados[8][0] - $dia) * 86400 + ($estados[8][1] - $hora) * 3600 + ($estados[8][2] - $minuto) * 60;
			$eventTimes['castlesiege'] = ['name' => 'Castle Siege', 'opentime' => 0, 'duration' => max(0, $duracao),
				'day' => date('l', $cerco), 'time' => date('H:i', $cerco)];
		}
	}
} catch(Exception $e) { /* sem o banco, o quadro mostra os outros eventos */ }

return array_filter($eventTimes, function($e) { return !empty($e['schedule']) || isset($e['day']); });
