# Domínio e segurança (jogo + site públicos, rodando no seu PC)

Guia para colocar o Mu Chila num domínio próprio, com HTTPS no site e o jogo acessível sem Radmin,
mantendo tudo rodando no seu computador. Escrito para o dono do servidor seguir passo a passo.

Situação de partida (27/09/2026): tudo roda no IP do Radmin `26.139.39.123`. IP público da casa: `191.9.105.14`
(faixa pública real; provavelmente **dinâmico**). Site em Apache porta 80, sem HTTPS. Firewall desligado.

## Visão geral

- **Site**: fica público e seguro com **Cloudflare Tunnel** — o PC faz uma conexão de dentro para fora até a
  Cloudflare; não precisa abrir porta no roteador, esconde o IP da casa e já vem com HTTPS.
- **Jogo**: usa TCP puro, que **não passa** no túnel grátis. Dois caminhos:
  - **Caminho A — abrir portas no roteador + DDNS**: sem custo, mas expõe o IP da casa e **não tem proteção contra DDoS**.
  - **Caminho B — VPS baratinho (~R$25/mês) como ponte**: o jogo **continua no seu PC**; o VPS só repassa, esconde o IP e ajuda contra ataque. Recomendado para abrir a desconhecidos.

## Portas do servidor (já levantadas)

| Papel | Porta | Exposição |
|---|---|---|
| ConnectServer (cliente conecta primeiro) | TCP 44405 | pública (jogo) |
| GameServers Mu Chila / Non-PvP / VIP / BattleCore | TCP 55901–55904 | pública (jogo) |
| Castle Siege | TCP 55919 | pública (jogo) |
| Contagem de jogadores na lista | UDP 55557 | pública (jogo) |
| DataServer | TCP 55960 | **NUNCA** expor |
| JoinServer | TCP 55970 | **NUNCA** expor |
| SQL Server | TCP 1433 | **NUNCA** expor |
| Área de Trabalho Remota (RDP) | TCP 3389 | **NUNCA** expor |
| Site (Apache) | TCP 80 | só local (vai pelo túnel) |

## Fase 0 — pré-requisitos (você)

1. **Conferir CGNAT** no roteador (192.168.15.1 → seção de status/WAN): o **IP WAN** bate com `191.9.105.14`?
   - Bate → sem CGNAT, o Caminho A funciona.
   - É `100.64.x`–`100.127.x` ou diferente → há CGNAT: o Caminho A **não** funciona; use o Caminho B (VPS) ou peça **IP fixo** à operadora.
2. **Registrar o domínio** (ex.: [registro.br](https://registro.br) para `.com.br`, ~R$40/ano).
3. **Criar conta grátis na [Cloudflare](https://dash.cloudflare.com/sign-up)**, adicionar o domínio (plano Free) e trocar os **nameservers** no registro.br pelos dois que a Cloudflare mostrar (leva algumas horas para propagar).

## Fase 1 — site público com HTTPS (Cloudflare Tunnel)

1. Baixar o `cloudflared` (Windows amd64) de https://github.com/cloudflare/cloudflared/releases e salvar em `C:\MuServer\cloudflared\cloudflared.exe`.
2. No PowerShell:
   ```
   cd C:\MuServer\cloudflared
   .\cloudflared.exe tunnel login            # abre o navegador; autorize o domínio
   .\cloudflared.exe tunnel create muchila   # anota o ID do túnel gerado
   ```
3. Criar `C:\MuServer\cloudflared\config.yml`:
   ```yaml
   tunnel: <ID-do-tunel>
   credentials-file: C:\Users\<voce>\.cloudflared\<ID-do-tunel>.json
   ingress:
     - hostname: seudominio.com.br
       service: http://localhost:80
     - hostname: www.seudominio.com.br
       service: http://localhost:80
     - service: http_status:404
   ```
4. Apontar o DNS do site para o túnel e instalar como serviço (liga com o Windows):
   ```
   .\cloudflared.exe tunnel route dns muchila seudominio.com.br
   .\cloudflared.exe tunnel route dns muchila www.seudominio.com.br
   .\cloudflared.exe service install
   ```
5. Na Cloudflare, deixar o SSL/TLS em **Full**. Testar `https://seudominio.com.br`.
6. Apontar o **WebEngine** para o domínio: `Site\www\includes\config\webengine.json` (URL base `https://seudominio.com.br`), cookies "secure".
7. **Mercado Pago de verdade**: com a URL pública HTTPS, configurar o webhook (`https://seudominio.com.br/api/muchila-mercadopago.php`) e trocar o provedor de `simulado` para `mercadopago` em `usercp.loja.xml` (token em `Site\config-local\mercadopago.json`). Ver [SITE.md](SITE.md).

## Fase 2 — endurecer a máquina

1. **Firewall** (feche o Radmin de administração antes NÃO; use o console local):
   ```
   cd C:\Projetos\MuServer-Season14\tools
   .\Firewall-Servidor.ps1 -Jogo            # revisa o plano
   .\Firewall-Servidor.ps1 -Aplicar -Jogo   # aplica (Caminho A). No Caminho B, sem -Jogo.
   ```
   Libera tudo pela rede do Radmin (ninguém cai), abre só as portas do jogo na internet e bloqueia SQL/RDP/DataServer/JoinServer/site.
2. **Apache/PHP em produção**: `ServerTokens Prod`, `ServerSignature Off`, sem listagem de diretório, `display_errors=Off`. (Posso preparar o patch do `httpd.conf`/`php.ini` quando quiser.)
3. **Anti-abuso** no site: limite de tentativas de login/cadastro e captcha no registro.
4. Revisar a senha do admin do site e do SQL; segredos seguem fora do repositório.

## Fase 3 — jogo público

1. Escolher **A** (roteador) ou **B** (VPS). No B, contratar o VPS e montar a ponte (o jogo segue no PC).
2. **DDNS** (IP dinâmico): criar um token de API na Cloudflare (Zone.DNS:Edit na sua zona) e o arquivo
   `Site\config-local\cloudflare-ddns.json` (ver cabeçalho de `Atualizar-DNS-Cloudflare.ps1`), e agendar:
   ```
   .\Atualizar-DNS-Cloudflare.ps1     # testa
   # depois: tarefa a cada 15 min apontando jogo.seudominio.com.br para o IP atual
   ```
3. Trocar o endereço do jogo do Radmin para o domínio:
   ```
   .\Trocar-EnderecoJogo.ps1 -Para jogo.seudominio.com.br
   .\Recarregar-Servidor.ps1 -Servidor ConnectServer -Item ServerList
   # o MapServerInfo só vale reiniciando os GameServers e o Castle Siege
   ```
   Se o cliente S14 **não aceitar** o nome, use o IP público (com DDNS) no lugar do domínio. Para voltar: `-Radmin`.
4. **Caminho A**: no roteador, encaminhar para o IP local `192.168.15.169` só as portas TCP 44405, 55901–55904, 55919 e UDP 55557. **Nunca** 55960/55970/1433/3389.
5. Atualizar o cliente/patch dos amigos com o endereço novo (mantendo o Radmin como alternativa, se quiser).

## Fase 4 — resiliência

- Backup do banco **para fora do PC**: descubra sua pasta de nuvem e rode/agende
  `.\Backup-Nuvem.ps1 -Destino "C:\Users\<voce>\OneDrive\MuChilaBackup"` depois do backup diário.

## Scripts prontos (em `tools\`)

| Script | O que faz |
|---|---|
| `Firewall-Servidor.ps1` | Liga o firewall com regras mínimas (Radmin livre, só portas do jogo na internet). Só aplica com `-Aplicar`. |
| `Trocar-EnderecoJogo.ps1` | Troca o endereço do jogo (Radmin ↔ domínio) em ServerList/MapServerInfo, com backup. |
| `Atualizar-DNS-Cloudflare.ps1` | DDNS: mantém o domínio apontando para o IP público atual. |
| `Backup-Nuvem.ps1` | Copia o backup diário do banco para uma pasta de nuvem. |

> **DDoS**: hospedando em casa, o ponto fraco é ataque de negação. O Caminho B (VPS) e o proxy da Cloudflare
> ajudam no site; para o jogo, proteção de verdade exige um intermediário (VPS/serviço pago). Comece fechado
> entre amigos e só divulgue quando o resto estiver redondo.
