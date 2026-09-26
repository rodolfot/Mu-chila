<?php
/**
 * Mu Chila - página "Informações" (a do WebEngine era um modelo com "x%" e comandos de outro servidor).
 * Os números vêm dos arquivos do servidor (GameServer\DATA e Data\Lang) e da tabela da loja:
 * mudou a configuração do jogo, a página acompanha.
 */

$muchilaRaiz = dirname(__DIR__, 3);                                  // www\modules -> C:\MuServer
$muchilaDat = function(string $mcNome) use ($muchilaRaiz): array {     // "Chave = valor" de GameServerInfo - <nome>.dat
	$mcValores = [];
	foreach(@file($muchilaRaiz . '/GameServer/DATA/GameServerInfo - ' . $mcNome . '.dat', FILE_IGNORE_NEW_LINES) ?: [] as $mcL)
		if(preg_match('/^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$/', $mcL, $mcM)) $mcValores[$mcM[1]] = $mcM[2];
	return $mcValores;
};
$mcCommon = $muchilaDat('Common');
$mcCommand = $muchilaDat('Command');
$mcChaos = $muchilaDat('ChaosMix');
$mcH = fn($mcT) => htmlspecialchars((string)$mcT, ENT_QUOTES, 'UTF-8');
$mcNum = fn($n) => number_format((float)$n, 0, ',', '.');

// Nomes dos comandos no arquivo de idioma do servidor (o jogo aceita o texto que estiver lá)
$mcNomesComando = [];
$mcLang = @file_get_contents($muchilaRaiz . '/Data/Lang/Portuguese.xml') ?: '';
if(preg_match_all('#<Msg ID="(\d+)" Text="(/[a-z]+)" />#', $mcLang, $mcMm, PREG_SET_ORDER))
	foreach($mcMm as $mcM) $mcNomesComando[$mcM[2]] = true;
$mcTem = fn($mcNome) => isset($mcNomesComando[$mcNome]);

// Planos: taxas por tipo de conta (AL0..AL3) + preço e Zen dos pacotes VIP da loja
$mcPlanos = [0 => ['nome' => 'Free', 'valor' => null, 'zen' => null]];
$mcPacotes = json_decode(@file_get_contents(__ROOT_DIR__ . 'includes/config/muchila.pacotes.json') ?: '{}', true)['pacotes'] ?? [];
foreach($mcPacotes as $mcP) {
	if(($mcP['tipo'] ?? '') !== 'vip' || empty($mcP['ativo']) || isset($mcPlanos[$mcP['vip_nivel']])) continue;
	$mcPlanos[(int)$mcP['vip_nivel']] = ['nome' => trim(preg_replace('/\s*\(.*$/', '', $mcP['nome'])), 'valor' => $mcP['valor'], 'zen' => $mcP['zen'] ?? 0, 'dias' => $mcP['vip_dias']];
}
ksort($mcPlanos);

// Chaos Machine: -1 no arquivo = a chance depende do valor dos itens colocados
$mcTaxa = function(string $mcChave) use ($mcChaos) {
	if(!isset($mcChaos[$mcChave . '_AL0'])) return null;
	$mcV = (int)$mcChaos[$mcChave . '_AL0'];
	return $mcV < 0 ? 'depende dos itens' : $mcV . '%';
};
$mcCombinacoes = [
	'Item +10' => 'PlusItemLevelMixRate1', 'Item +11' => 'PlusItemLevelMixRate2', 'Item +12' => 'PlusItemLevelMixRate3',
	'Item +13' => 'PlusItemLevelMixRate4', 'Item +14' => 'PlusItemLevelMixRate5', 'Item +15' => 'PlusItemLevelMixRate6',
	'Chaos Weapon' => 'ChaosItemMixRate', 'Asa de 1º nível' => 'Wing1MixRate', 'Asa de 2º nível' => 'Wing2MixRate',
	'Condor Feather (1º passo da asa de 3º nível)' => 'FeatherOfCondorMixRate', 'Asa de 3º nível' => 'Wing3MixRate',
	'Dinorant' => 'DinorantMixRate', 'Fruta' => 'FruitMixRate', 'Pet (Dark Horse / Dark Raven)' => 'PetMixRate',
	'Fragment of Horn' => 'PieceOfHornMixRate', 'Broken Horn' => 'BrokenHornMixRate', 'Horn of Fenrir' => 'HornOfFenrirMixRate',
	'Refinar Gemstone em Jewel of Harmony (NPC Elpis)' => 'JewelOfHarmonyItemPurityMixRate',
];
$mcVipIgual = true;
foreach($mcChaos as $mcK => $mcV) if(str_ends_with($mcK, '_AL0') && ($mcChaos[substr($mcK, 0, -4) . '_AL3'] ?? $mcV) !== $mcV) $mcVipIgual = false;

echo '<div class="page-title"><span>'.lang('module_titles_txt_17').'</span></div>';
?>

<table class="table table-condensed table-hover table-striped table-bordered">
	<thead><tr><th colspan="2">Servidor</th></tr></thead>
	<tbody>
		<tr><td style="width:50%;">Versão</td><td><?php echo $mcH(config('server_info_season')); ?></td></tr>
		<tr><td>Nível máximo</td><td>400 + <?php echo $mcH($mcCommon['MasterSkillTreeMaxLevel'] ?? '?'); ?> de master</td></tr>
		<tr><td>Reset</td><td><code>/reset</code> no nível <?php echo $mcH($mcCommand['CommandResetLevel_AL0'] ?? '?'); ?>,
			<?php echo (int)($mcCommand['CommandResetMoney_AL0'] ?? 0) > 0 ? $mcNum($mcCommand['CommandResetMoney_AL0']).' de Zen' : 'grátis'; ?>,
			até <?php echo $mcNum($mcCommand['CommandResetLimit_AL0'] ?? 0); ?> resets</td></tr>
		<tr><td>Evolução de classe</td><td><code>/change</code>, <?php echo (int)($mcCommand['CommandChangeMoney_AL0'] ?? 0) > 0 ? $mcNum($mcCommand['CommandChangeMoney_AL0']).' de Zen' : 'grátis'; ?>,
			até a <?php echo $mcH($mcCommand['CommandChangeLimit_AL0'] ?? '?'); ?>ª classe</td></tr>
		<tr><td>Jogadores ao mesmo tempo</td><td>até <?php echo $mcH($mcCommon['ServerMaxUserNumber'] ?? '?'); ?></td></tr>
	</tbody>
</table>

<h2>Planos</h2>
<table class="table table-condensed table-hover table-striped table-bordered">
	<thead><tr><th>Plano</th><th class="text-center">Experiência</th><th class="text-center">Experiência master</th><th class="text-center">Drop</th><th class="text-center">Zen no baú</th><th class="text-center">Preço</th></tr></thead>
	<tbody>
	<?php foreach($mcPlanos as $mcNivel => $mcP) { ?>
		<tr>
			<td><?php echo $mcH($mcP['nome']); ?></td>
			<td class="text-center"><?php echo $mcH($mcCommon['AddExperienceRate_AL'.$mcNivel] ?? '?'); ?>x</td>
			<td class="text-center"><?php echo $mcH($mcCommon['AddMasterExperienceRate_AL'.$mcNivel] ?? '?'); ?>x</td>
			<td class="text-center"><?php echo $mcH($mcCommon['ItemDropRate_AL'.$mcNivel] ?? '?'); ?>%</td>
			<td class="text-center"><?php echo $mcP['zen'] ? $mcNum($mcP['zen']) : '—'; ?></td>
			<td class="text-center"><?php echo $mcP['valor'] === null ? 'grátis' : 'R$ '.number_format((float)$mcP['valor'], 2, ',', '.').' / '.$mcH($mcP['dias']).' dias'; ?></td>
		</tr>
	<?php } ?>
	</tbody>
</table>
<p>O VIP vale a partir do próximo login. O Zen vai para o baú da conta. Compre na <a href="<?php echo __BASE_URL__; ?>usercp/loja">Loja</a> (área do jogador).</p>

<h2>Chaos Machine</h2>
<table class="table table-condensed table-hover table-striped table-bordered">
	<thead><tr><th>Combinação</th><th class="text-center" style="width:35%;">Chance máxima</th></tr></thead>
	<tbody>
	<?php foreach($mcCombinacoes as $mcNome => $mcChave) { $mcT = $mcTaxa($mcChave); if($mcT === null) continue; ?>
		<tr><td><?php echo $mcH($mcNome); ?></td><td class="text-center"><?php echo $mcH($mcT); ?></td></tr>
	<?php } ?>
	</tbody>
</table>
<p><?php echo $mcVipIgual ? 'As chances são as mesmas para todos os planos.' : 'Contas VIP podem ter chances diferentes.'; ?>
	"Depende dos itens": a chance aumenta com o valor dos itens colocados na máquina.</p>

<h2>Comandos</h2>
<table class="table table-condensed table-hover table-striped table-bordered">
	<tbody>
	<?php
	$mcComandos = [
		['/reset', 'Reset', 'Reseta o personagem (volta ao nível 1).'],
		['/addstr /addagi /addvit /addene /addcmd [pontos]', 'AddPoint', 'Distribui pontos em força, agilidade, vitalidade, energia ou comando.'],
		['/change', 'Change', 'Evolui a classe do personagem.'],
		['/pkclear', 'PKClear', 'Limpa o status de assassino (PK).'],
		['/post [mensagem]', 'Post', 'Mensagem para o servidor inteiro (a partir do nível '.(int)($mcCommand['CommandPostLevel_AL0'] ?? 0).').'],
		['/move [mapa]', null, 'Vai para um mapa.'],
		['/ware [número]', 'Ware', 'Troca de baú (baús extras da conta).'],
		['/attack', null, 'Liga e desliga o ataque automático.'],
		['/clearinv', 'ClearInvent', 'Apaga os itens do inventário. Cuidado: não tem volta.'],
	];
	foreach($mcComandos as [$mcTexto, $mcSwitch, $mcDescricao]) {
		$mcPrimeiro = strtok($mcTexto, ' ');
		if(!$mcTem($mcPrimeiro)) continue;
		if($mcSwitch !== null && ($mcCommand['Command'.$mcSwitch.'Switch'] ?? '0') !== '1') continue;
		echo '<tr><td style="width:40%;"><code>'.$mcH($mcTexto).'</code></td><td>'.$mcH($mcDescricao).'</td></tr>';
	}
	?>
	</tbody>
</table>
<p>Horários dos eventos: quadro "Eventos" na <a href="<?php echo __BASE_URL__; ?>">página inicial</a>.</p>
