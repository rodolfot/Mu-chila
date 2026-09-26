# Site do Mu Chila

Site com cadastro de conta, rankings, área do jogador (desbugar personagem, limpar PK...), loja de VIP e cash e painel admin.
Base: **WebEngine CMS 1.2.7** (código aberto, MIT), com o código do Mu Chila por cima. Instalado em 25/09/2026.

## Uso no dia a dia

| O quê | Como |
|---|---|
| Ligar / desligar | `C:\MuServer\Site\Ligar Site.bat` / `Desligar Site.bat` (só mexem no Apache do site; o Apache do EDB PEM, porta 8080, é outro) |
| Endereço | `http://26.139.39.123` pelo Radmin, ou `http://localhost` neste PC |
| Quem acessa | Só este PC e a rede do Radmin (26.x.x.x): `Require ip` no `httpd.conf` e regra do firewall "Mu Chila - Site (porta 80, so Radmin)" |
| Painel admin | Entrar no site com a conta **yolaxd** e abrir `http://localhost/admincp/`. Outras contas admin: `"admins"` no `www\includes\config\webengine.json` |
| Pedidos da loja | Painel admin > **Mu Chila > Pedidos da loja** |
| Rankings | Recalculados a cada minuto pela tarefa agendada do Windows **"Mu Chila\Site - rankings"** (`php-win.exe ... includes\cron\cron.php`) |
| Quadro de eventos | Lido dos arquivos do servidor (`C:\MuServer\Data\Event`): mudou a agenda do jogo, o site acompanha. Castle Siege pelo ciclo do `MuCastleData.dat` e o início gravado em `MuCastle_DATA` |
| Logs | `C:\MuServer\Site\logs` (Apache, erros do PHP, avisos do Mercado Pago) |

A conta criada pelo site é igual à do `Criar Conta.bat` (senha em texto no `MEMB_INFO`, que é o que o jogo lê).
Conta e senha: até **10 caracteres** (limite do jogo). Não há verificação de e-mail nem captcha (o PC não tem servidor de e-mail; o site só é acessível pelo Radmin).

## Páginas (26/09/2026)

- **Informações** (`muchila\www\modules\info.php`): lê os valores direto dos arquivos do servidor e da tabela da loja. Mostra:
  - nível máximo, reset e evolução de classe;
  - planos com taxas, preço e Zen;
  - chances da Chaos Machine;
  - comandos ligados.
  A página original do WebEngine era um modelo com "x%" e comandos de outro servidor.
- **Downloads:**
  - Oferece o cliente completo, o patch e o LEIA-ME da pasta `C:\MuServer\Cliente para amigos`.
  - O Apache serve essa pasta em `/arquivos/` (`Alias` no `httpd.conf`, mesmo bloqueio do site, sem listar a pasta). Ela fica fora do `www` para o cliente de 1,3 GB não entrar no sincronismo do repositório.
  - Trocou um arquivo da pasta: rode `Instalar-Modulos.ps1`, que atualiza o tamanho e o cache da página.
- **Desligados:**
  - Reset pelo site: o jogo tem `/reset` e reset automático com outras regras (`Command.dat` + `ResetTable.txt`).
  - Comprar zen e votar: dependem do sistema de créditos do WebEngine, que não está configurado (a moeda do Mu Chila é o cash).
  - Esqueci a senha: manda e-mail. No login aparece "Esqueceu a senha? Peça ao administrador do servidor."
- **Continuam ligados:**
  - Área do jogador: distribuir pontos, zerar pontos, limpar PK, zerar árvore master, desbugar personagem, trocar senha e e-mail.
  - Rankings, perfis, notícias e Castle Siege.

## Pendências

1. ~~Preços do cash~~: definidos pelo dono em 26/09/2026, 200 cash por R$ 37, 500 por R$ 45 e 1.000 por R$ 60 (`cash-200`, `cash-500`, `cash-1000` no `muchila.pacotes.json`).
2. **Mercado Pago (PIX real):** pronto, mas nunca testado contra a API. Passos na seção abaixo; o dono pediu para continuar no modo de teste por enquanto.
3. **Teste pelo navegador (`teste-site.ps1`):** precisa da senha da conta admin do site e só roda com ela. O teste do núcleo da loja (`teste-loja.php`) passou 19 de 19 em 26/09.
4. **Notícias:** não há nenhuma publicada. A página inicial mostra só o quadro de eventos e os rankings.
5. ~~Segundo servidor~~: o "Users Online" do site conta as contas conectadas no banco (`MEMB_STAT.ConnectStat = 1`), então já inclui o Mu Chila Non-PvP.

## Loja de VIP e cash

Área do jogador > **Loja: VIP e Cash** (ou "Loja" no menu do topo).

- **Pacotes e preços**: `www\includes\config\muchila.pacotes.json` (os preços atuais são **de exemplo**). VIP 1/2/3 por N dias; cash vai para o `WCoinC` (saldo da Cash Shop do jogo).
- **Regras**: VIP do mesmo nível soma aos dias restantes; outro nível só depois que o atual acabar. Cada pagamento é entregue uma vez só (a troca de situação e a entrega ficam na mesma transação). Até 3 pedidos aguardando pagamento por conta. O PIX expira em 30 min (`usercp.loja.xml`), mas pagamento que chegar depois ainda é entregue.
- **VIP e cash no jogo**: o VIP vale a partir do próximo login; o cash aparece ao abrir a Cash Shop (se não, relogar).
- **Situações**: aguardando pagamento → pago e entregue / cancelado / expirado / com problema. "Com problema" = pago, mas a entrega falhou (o motivo fica em Observações); o painel tem **Entregar** (reentrega) e **Cancelar**.
- **Entrega manual**: se alguém pagar por fora (PIX direto), use **Entregar** no pedido dele no painel.

### Modo de teste (atual)

`<provedor>simulado</provedor>` em `www\includes\config\modules\usercp.loja.xml`. Nenhum dinheiro envolvido: o pedido mostra um "PIX-DE-TESTE-NAO-PAGUE" e os botões **Simular pagamento aprovado/recusado**. A simulação passa pelo **mesmo caminho** do aviso real do Mercado Pago (consulta o "pagamento", confere valor e pedido, entrega).
Pedidos de teste ficam fora do "Recebido neste mês" do painel.

### Ligar o Mercado Pago (PIX real) — ainda não feito

O código está pronto (`MuChilaProvedorMercadoPago` em `www\includes\muchila\MuChilaLoja.php` e `www\api\muchila-mercadopago.php`), mas **nunca foi testado contra a API**. Para ligar:

1. No Mercado Pago (Suas integrações): criar a aplicação, pegar o **Access Token** e o **segredo da assinatura** dos webhooks. Começar com as credenciais **de teste**.
2. Criar `C:\MuServer\Site\config-local\mercadopago.json` (fica fora do repositório):
   `{ "access_token": "APP_USR-...", "webhook_secret": "..." }`
3. O Mercado Pago precisa chamar o site pela internet (HTTPS). O IP do Radmin não serve: usar, por exemplo, um túnel da Cloudflare apontando só para `/api/muchila-mercadopago.php`. Colocar esse endereço em `<mp_notification_url>` e também no painel do Mercado Pago (evento "Pagamentos").
4. PHP para Windows não traz certificados: baixar o `cacert.pem` (curl.se) e configurar `curl.cainfo` no `php.ini`.
5. Contas sem e-mail: preencher `<email_padrao>` (o Mercado Pago exige e-mail do pagador).
6. Trocar `<provedor>` para `mercadopago`, fazer um pagamento de teste e conferir `logs\mercadopago.log` e o painel.

O aviso do Mercado Pago não é confiável por si: o endpoint confere a assinatura `x-signature` e a loja **consulta o pagamento na API** antes de entregar.

### Planos (definidos pelo dono em 25/09/2026)

| Plano | Tipo de conta | Experiência (e master) | Drop | Zen no baú | Preço (30 dias) |
|---|---|---|---|---|---|
| Free | `_AL0` | 100x | 50% | — | — |
| Vipzinho | `_AL1` | 300x | 80% | 200.000.000 | R$ 35 |
| Vip | `_AL2` | 850x | 110% | 500.000.000 | R$ 40 |
| Vipzão | `_AL3` | 2000x | 150% | 2.000.000.000 | R$ 50 |

**Zen do VIP**: vai para o baú da conta (`warehouse.Money`, limite de 2 bilhões; se a conta nunca abriu o baú, ele é criado vazio). Só entra com a conta **fora do jogo** há 30 s: com o baú aberto, o servidor regravaria o valor antigo ao fechar. Comprado durante o jogo, o Zen fica "aguardando sair do jogo" e é entregue pela tarefa "Mu Chila - Zen do VIP" do agendador do site (a cada minuto) ou quando a pessoa abre a loja. O painel mostra a situação na coluna Entrega; o registro fica em `Site\logs\loja.log`.

Taxas em `AddExperienceRate_ALn`, `AddMasterExperienceRate_ALn` e `ItemDropRate_ALn` do `GameServerInfo - Common.dat` (três GameServers), aplicadas com Reload Common. Antes todas as contas tinham 2000x/100%, e o VIP ainda perdia o ataque automático (`CustomAttackEnable_AL1..3 = 0`, agora 1) e tinha o `CommandPostSellLevel_AL1` digitado errado (corrigido).

**Não aplicado (decisão do dono, 25/09/2026)**: a redução de experiência e drop pela metade depois de N resets (Free 10, Vipzinho/Vip 150, Vipzão 1000). O emulador só tem redução de experiência por reset igual para todos os tipos de conta (`Data\Util\ExperienceTable.txt`: MinReset/MaxReset → ExperienceRate) e nenhuma redução de drop por reset; o servidor não guarda taxa por jogador (lê a do tipo de conta na hora), então uma regra diferente por plano exigiria alterar o executável.

## Segurança

- Usuário próprio do banco para o site: `muchila_site` (lê e grava dados do `MuOnlineS14` e cria tabelas; não é `sa`). Senha em `Site\config-local\banco.txt` e no `webengine.json`, **nunca no repositório** (o sincronismo e o `.gitignore` excluem `config-local`, `webengine.json`, `tmp`, cache e logs).
- O instalador do WebEngine (`install/`) foi tirado de dentro do site (`Site\install-webengine-1.2.7`): se voltar para `www`, qualquer um na rede poderia reinstalar e trocar a configuração.
- Erros do PHP vão para o log, não para a tela. O endpoint de cron do WebEngine por endereço web está desligado (`cron_api: false`); os rankings rodam pela tarefa agendada.
- O WebEngine instalado foi conferido arquivo por arquivo contra a tag 1.2.7 do GitHub (647 idênticos) e revisado: sem execução de comandos, SQL sempre com parâmetros. Chamadas externas dele: consulta de versão (painel admin), plugins e `ip-api.com` (bandeira do país, desligada nos rankings).

## Arquivos do Mu Chila e ajustes no WebEngine

O código do Mu Chila fica separado em `C:\MuServer\Site\muchila` e é copiado para o site por **`muchila\Instalar-Modulos.ps1`** (pode rodar de novo; cada ajuste confere se já foi feito):

| Arquivo | O quê |
|---|---|
| `www\includes\muchila\MuChilaLoja.php` | Núcleo da loja: pacotes, pedidos, entrega, provedores (simulado e Mercado Pago) |
| `www\modules\usercp\loja.php` | Página da loja (jogador) |
| `www\admincp\modules\muchila_pedidos.php` | Pedidos da loja (painel admin) |
| `www\api\muchila-mercadopago.php` | Aviso de pagamento do Mercado Pago (inativo no modo de teste) |
| `www\includes\muchila\eventos.php` | Agenda real dos eventos para o quadro da página inicial |
| `www\includes\config\muchila.pacotes.json`, `modules\usercp.loja.xml` | Pacotes/preços e configuração da loja |
| `sql\MUCHILA_PEDIDOS.sql` | Tabela dos pedidos (já criada) |
| `conf\httpd.conf`, `conf\php.ini` | Cópia da configuração do Apache e do PHP |
| `www\includes\cron\muchila_zen.php` | Tarefa do agendador: Zen pendente do VIP (registrada pelo `Instalar-Modulos.ps1` com o MD5 do arquivo) |
| `testes\teste-loja.php` | Teste do núcleo (19 cenários, conta descartável `lojateste`) |
| `testes\teste-site.ps1` | Teste pelo navegador (10 cenários, conta descartável `sitetest1`): `.\teste-site.ps1 -Admin yolaxd -AdminSenha <senha>` |
| `www\modules\info.php` | Página Informações com os valores reais do servidor |

Ajustes que o script faz no WebEngine: conexão com a instância `.\MUONLINE` sem porta (`class.database.php`, `webengine.php`); fuso de São Paulo (`timezone.php`, `api/events.php`); agenda real no quadro de eventos (`api/events.php`); "Doação" (PayPal) → "Loja" no menu do topo e item "Loja: VIP e Cash" no menu do jogador; textos em `languages\pt|en`; grupo "Mu Chila" no menu do painel admin; módulos sem suporte desligados e link de senha do login; downloads da pasta "Cliente para amigos" e cache da página.
Configurações feitas pelo painel/arquivos: nome, título, idioma `pt`, taxas ("100x (VIP até 2000x)", drop "50% (VIP até 150%)"), máximo 100 online, limites de conta/senha (`webengine.json`); ranking de resets ligado e padrão, ranking de tempo online ligado, bandeiras desligadas (`rankings.xml`).

**Atualizar o WebEngine**: baixar a versão nova, conferir, copiar por cima de `www` (sem `install`), rodar `Instalar-Modulos.ps1` e os dois testes.

## Recriar do zero

Apache e PHP não vão para o repositório (140 MB, baixáveis):

| Pacote | Origem | SHA-256 |
|---|---|---|
| Apache 2.4.68 Win64 VS18 (`httpd-2.4.68-260920-Win64-VS18.zip`) | apachelounge.com | `F6DCF17D08AA32721AE418CD818C157E4C521C9E889B758646FB64287F1D56E3` |
| PHP 8.5.11 Thread Safe x64 (`php-8.5.11-Win32-vs17-x64.zip`) | windows.php.net | conferido com o `sha256sum.txt` oficial |
| Microsoft Drivers for PHP for SQL Server 5.13.3 (`php_pdo_sqlsrv_85_ts_x64.dll`) | github.com/microsoft/msphpsql | — |
| WebEngine CMS 1.2.7 | github.com/lautaroangelico/WebEngine (tag 1.2.7) | 647 arquivos iguais à tag |

Passos: extrair Apache em `Site\apache` e PHP em `Site\php`; copiar a DLL do driver para `php\ext`; copiar `muchila\conf\httpd.conf` e `php.ini`; criar o login `muchila_site` e o `config-local\banco.txt` (`servidor=.\MUONLINE`, `porta=` vazia); WebEngine em `Site\www`; rodar `Instalar-Modulos.ps1`; ligar o site e rodar o instalador em `/install` (driver 2 = sqlsrv, servidor `.\MUONLINE` sem porta, senha sem criptografia, perfil X-Team); tirar `install` de `www`; rodar `sql\MUCHILA_PEDIDOS.sql`; criar a tarefa agendada dos rankings e a regra do firewall.

Por que `.\MUONLINE` sem porta: há duas instâncias de SQL Server no PC e a porta TCP 61764 é da outra; a `MUONLINE` atende pela conexão local. Pelo TCP, o driver falhava na negociação antes do login.
