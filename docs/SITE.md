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

- **Pacotes e preços**: `www\includes\config\muchila.pacotes.json` (VIP R$ 35/40/50 por 30 dias; cash 200/500/1.000 por R$ 37/45/60, definidos pelo dono). VIP 1/2/3 por N dias; cash vai para o `WCoinC` (saldo da Cash Shop do jogo).
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

| Plano | Tipo de conta | Experiência | Experiência master | Drop | Zen no baú | Preço (30 dias) |
|---|---|---|---|---|---|---|
| Free | `_AL0` | 100x | 7x | 50% | — | — |
| Vipzinho | `_AL1` | 130x | 20x | 80% | — (era 200.000.000) | R$ 35 |
| Vip | `_AL2` | 150x | 57x | 110% | — (era 500.000.000) | R$ 40 |
| Vipzão | `_AL3` | 180x | 133x | 150% | — (era 2.000.000.000) | R$ 50 |

**27/09/2026:** o VIP **não dá mais Zen** (pedido do Mario, aprovado pelo dono): `zen = 0` nos pacotes. O mecanismo abaixo continua no código, caso volte.

**EXP master** (26/09/2026, pedido do dono: "extremamente difícil"): a taxa normal dividida por 15. Um Vipzão jogando ~18 h por dia (como o Mario) leva ~3 semanas do master 0 ao 600, antes eram ~2 dias; um Free leva ~1 ano nesse ritmo. O `/reset` zera só o nível 1–400: os níveis de master continuam, que é o padrão do MU (issue #14).

**Zen do VIP**: vai para o baú da conta (`warehouse.Money`, limite de 2 bilhões; se a conta nunca abriu o baú, ele é criado vazio). Só entra com a conta **fora do jogo** há 30 s: com o baú aberto, o servidor regravaria o valor antigo ao fechar. Comprado durante o jogo, o Zen fica "aguardando sair do jogo" e é entregue pela tarefa "Mu Chila - Zen do VIP" do agendador do site (a cada minuto) ou quando a pessoa abre a loja. O painel mostra a situação na coluna Entrega; o registro fica em `Site\logs\loja.log`.

Taxas em `AddExperienceRate_ALn`, `AddMasterExperienceRate_ALn` e `ItemDropRate_ALn` do `GameServerInfo - Common.dat` (três GameServers), aplicadas com Reload Common. Antes todas as contas tinham 2000x/100%, e o VIP ainda perdia o ataque automático (`CustomAttackEnable_AL1..3 = 0`, agora 1) e tinha o `CommandPostSellLevel_AL1` digitado errado (corrigido).

**Não aplicado (decisão do dono, 25/09/2026)**: a redução de experiência e drop pela metade depois de N resets (Free 10, Vipzinho/Vip 150, Vipzão 1000). O emulador só tem redução de experiência por reset igual para todos os tipos de conta (`Data\Util\ExperienceTable.txt`: MinReset/MaxReset → ExperienceRate) e nenhuma redução de drop por reset; o servidor não guarda taxa por jogador (lê a do tipo de conta na hora), então uma regra diferente por plano exigiria alterar o executável.

## Mercado entre jogadores (issue #22, 27/09/2026)

Página **Painel do jogador → Mercado entre jogadores** (`usercp/mercado`): jogadores vendem **itens do baú** e **personagens** por **dinheiro real** (PIX do Mercado Pago). A loja fica com **10%** (`<taxa>` em `usercp.mercado.xml`). Núcleo: `www\includes\muchila\MuChilaMercado.php` (+ `MuChilaItens.php`); tabelas `MUCHILA_MERCADO_ANUNCIOS`, `_PEDIDOS`, `_VENDEDORES` (`muchila\sql\MUCHILA_MERCADO.sql`).

- **Pagamento com split**: cada vendedor liga a própria conta do Mercado Pago (OAuth). O PIX é gerado **com o token do vendedor** e a taxa da loja em `application_fee`: o dinheiro cai direto na conta dele e os 10% na conta da loja. O site nunca guarda nem repassa dinheiro. Tokens dos vendedores ficam **criptografados** (AES-256-GCM).
- **Custódia (contra duplicação)**: ao anunciar, o item **sai do baú** e fica só no anúncio; o personagem sai das vagas e vai para a conta de custódia `MUCHILAMKT` (ninguém entra nela). Vendido → baú/vaga do comprador (o personagem leva junto os presentes da Gremory, registros de caça e de restauração). Cancelado → volta para o vendedor.
- **Reserva**: comprar reserva o anúncio enquanto o PIX está aberto (30 min); só um comprador consegue. PIX vencido ou recusado → o anúncio volta à venda. Pago depois de vencer, com o anúncio já vendido a outro → compra "com problema" para **reembolsar** no Mercado Pago (aparece no admin).
- **Sempre fora do jogo** para anunciar, cancelar e receber. Pago com o comprador no jogo, sem espaço no baú ou sem vaga de personagem (máx. 8) → fica "pago" e a tarefa **Mu Chila - Mercado** (a cada minuto) entrega depois.
- **Não vendáveis**: pentagramas e errtels (os dados ficam presos ao personagem), itens com prazo da loja de cash (`CashShopPeriodItem`), personagens com `CtlCode` (GM/bloqueado). Muuns não entram (ficam em outro inventário).
- **Admin**: AdminCP → Mu Chila → **Mercado entre jogadores** (compras, taxa do mês, entregar de novo, cancelar).
- **Testes**: `muchila\testes\teste-mercado.php` (núcleo, 26 casos: custódia, disputa, recusa, vencimento, pagamento atrasado, personagem, comprador online) e `teste-mercado-site.ps1` (pelo site como jogador, 15 casos). Os dois criam e apagam contas de teste.

### Mercado: modo de teste (atual)

`<provedor>simulado</provedor>`: o vendedor "liga" a conta sem Mercado Pago de verdade e a compra mostra um PIX falso com os botões de simular pagamento.

### Ligar o split real do Mercado Pago — depende do domínio com HTTPS

1. No Mercado Pago (Suas integrações), criar a aplicação da loja com **OAuth** e pegar `client_id` e `client_secret`; cadastrar a **URL de redirecionamento** `https://SEUDOMINIO/api/muchila-mercado-oauth.php` e o webhook de **Pagamentos** `https://SEUDOMINIO/api/muchila-mercado-mp.php` (pegar o segredo da assinatura).
2. Gerar a chave de criptografia dos tokens: `php -r "echo base64_encode(random_bytes(32));"`.
3. Em `Site\config-local\mercadopago.json` (fora do repositório) acrescentar: `"client_id": "...", "client_secret": "...", "mercado_chave": "<a chave do passo 2>"` (o `webhook_secret` já é usado pela loja). **Não perder a chave**: sem ela os tokens guardados não abrem e os vendedores precisam ligar de novo.
4. Em `usercp.mercado.xml`: `<mp_redirect_uri>` e `<mp_notification_url>` com as URLs do passo 1, `<email_padrao>` e `<provedor>mercadopago</provedor>`. O `curl.cainfo` do `php.ini` precisa estar configurado (igual à loja).
5. Testar com contas de teste do próprio Mercado Pago (um vendedor e um comprador) antes de abrir para os jogadores; conferir `Site\logs\mercado.log`.

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
Configurações feitas pelo painel/arquivos: nome, título, idioma `pt`, taxas ("100x dinâmico (VIP até +80%)", antes "100x (VIP até 2000x)", drop "50% (VIP até 150%)"), máximo 100 online, limites de conta/senha (`webengine.json`); ranking de resets ligado e padrão, ranking de tempo online ligado, bandeiras desligadas (`rankings.xml`).

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
