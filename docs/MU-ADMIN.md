# Mu Chila Admin: o painel web

O Mu Chila Admin é o painel de administração do servidor. Desde 01/10/2026 ele é um **painel web**: roda no PC do servidor,
abre no navegador (http://localhost:5170) e pede **usuário e senha**. Substitui o programa de janelas antigo (as abas
viraram páginas) e acrescenta usuários com papéis, auditoria, alertas, dashboard e relatórios em CSV e PDF.

- Código: `tools\MuChilaAdmin.Core` (regras, banco e arquivos do servidor) e `tools\MuChilaAdmin` (painel web e linha de comando).
- Testes: `tools\MuChilaAdmin.Testes`.
- Instalado em `C:\MuServer\MuChilaAdmin`. Liga com o Windows e fica no ícone da bandeja.
- Precisa do **runtime do ASP.NET Core 10** (o `Publicar-Painel.ps1` confere).

## Abrir o painel

| Onde | Como |
|---|---|
| No PC do servidor | Atalho **Mu Chila Admin** na área de trabalho ou http://localhost:5170. O ícone da bandeja (perto do relógio) tem "Abrir o painel" e "Desligar o painel". |
| De outro PC da rede do Radmin | http://IP-DO-RADMIN-DO-SERVIDOR:5170, depois de liberar (ver [Abrir para a rede do Radmin](#abrir-para-a-rede-do-radmin)). |

O painel liga sozinho com o Windows (atalho **Mu Chila - Painel** na pasta Inicializar) e liga o vigia se ele estiver parado.

### Primeiro acesso

Na primeira vez, o painel ainda não tem usuários: a tela de login leva ao **Primeiro acesso**, que cria o administrador.
Essa tela **só abre no próprio PC do servidor** (http://localhost:5170), para ninguém da rede criar o administrador antes do dono.

### Usuários e papéis

Página **Painel → Usuários do painel** (só administrador). Cada pessoa tem o seu usuário; o usuário do painel **não é conta do jogo**.

| Papel | Pode |
|---|---|
| **Administrador** | Tudo, inclusive usuários do painel, economia (lojas, drops, taxas), servidor (ligar, parar, eventos, bônus) e auditoria |
| **Moderador** | Contas (VIP, ban, senha, pontos, cash), itens e baú, Passe dos Mapas, vendas, avisos aos jogadores, notícias e mensagens do site |
| **Somente leitura** | Ver o dashboard, contas, servidor, comandos e relatórios, sem mudar nada |

- **Usuário novo** ou **"Redefinir senha"**: o painel mostra uma **senha temporária** uma vez; no primeiro login a pessoa é obrigada a trocá-la.
- **Desativar** um usuário derruba as sessões dele na hora. Sempre sobra pelo menos um administrador ativo.
- Esqueceu a senha do único administrador? No PC do servidor, num PowerShell:
  `C:\MuServer\MuChilaAdmin\MuChilaAdmin.exe --redefinir-senha <usuário> C:\temp\senha.txt` (a senha temporária fica no arquivo).

## Segurança

- **Senhas** guardadas só como hash PBKDF2-SHA256 com 600.000 iterações e sal (tabela `MUCHILA_ADMIN_USUARIOS`). Mínimo de 8 caracteres, com letra e número.
- **Bloqueio**: 5 senhas erradas seguidas bloqueiam o usuário por 15 minutos (abre um alerta; um administrador pode desbloquear antes). 20 erros em 10 minutos vindos do mesmo computador bloqueiam aquele IP por 10 minutos.
- **Sessão**: cookie HttpOnly e SameSite=Strict, expira após 8 horas sem uso (`MinutosSessao`). Trocar a senha, o papel ou desativar o usuário derruba as sessões abertas dele.
- **Todas as páginas exigem login**. Os formulários têm proteção contra CSRF, e "Sair" só funciona pelo botão do painel.
- **Cabeçalhos**: Content-Security-Policy (só conteúdo do próprio painel), sem iframe de outros sites, sem MIME sniffing e sem Referer.
- **Auditoria**: cada ação que muda algo (e cada login, certo ou errado) fica em `MUCHILA_ADMIN_AUDITORIA` com usuário, IP, hora, alvo, detalhes e resultado. Página **Painel → Auditoria** (filtros e exportação).
- **Rede**: por padrão o painel só atende o próprio PC. Ao abrir para o Radmin, a regra do firewall libera a porta só para a rede 26.x.
- **Uma ação por vez**: duas pessoas não gravam os arquivos do servidor ao mesmo tempo. Se alguém salvou a mesma lista depois que você abriu a página, o painel avisa antes de sobrescrever.

## Páginas

| Grupo | Página | O que faz |
|---|---|---|
| Visão geral | **Dashboard** | Jogadores online (agora e gráfico de 24 h), contas e contas novas por dia, VIPs, personagens por classe, os mais fortes, vendas e Cash gasto no site, mensagens novas, compras com problema, situação do servidor e do vigia, alertas abertos e últimas ações no painel |
| | **Alertas** | O que precisa de atenção: servidor ou processo parado, vigia parado, mensagens novas no Contate-nos, compras pagas com problema, tentativas de login erradas, disco quase cheio, backup do banco atrasado, usuário bloqueado. O sino no topo mostra os não lidos; com a permissão do navegador, os críticos viram notificação do Windows |
| Servidor | **Processos e jogadores** | Os processos do servidor, jogadores online (desconectar, levar à seleção), iniciar e parar, recarregar sem reiniciar, abrir o MuEditor e o launcher, publicar a atualização do cliente |
| | **Eventos** | Disparar Blood Castle, Devil Square, invasões e os outros eventos daqui a N minutos |
| | **Bônus de EXP e drop** | EXP, EXP master ou drop multiplicados por um tempo, agora ou agendado; o vigia liga e desliga |
| | **Avisos aos jogadores** | Mensagem na tela de todos agora, e a lista dos avisos automáticos (ordem e intervalo) |
| | **Comandos do jogo** | O que cada comando faz |
| Jogadores | **Contas** | Busca e filtros (online agora, VIP ativo, banidas, criadas em 7 dias). A página da conta tem VIP, ban com motivo e prazo, senha do jogo, Cash, pontos de atributo ou master, nível e reset, zerar a árvore master, personagens e histórico |
| | **Bans** | Banidas agora (motivo, prazo, quem baniu) e o histórico. Ban temporário vence sozinho |
| | **Itens e baú** | Inventário e baú com nível e opções, colocar item novo, remover (com backup) e os presentes da Gremory Case |
| | **Passe dos Mapas** | Dar e tirar passe, quem tem, histórico, cobrança ligada ou desligada |
| | **Vendas** | VIP/Cash/Passe (PIX), loja de itens, Zen e mercado, com "só com problema" e exportação |
| Economia | **Lojas de NPC** | O que cada NPC vende, com a prévia da janela 8×15 e o aviso do que não cabe |
| | **Loja de Cash** | Preços (inclusive por opção), pacotes na loja ou fora, adicionar item e apagar o que o painel criou. Publicar para o launcher para os jogadores verem |
| | **Drops** | Por monstro ou por item (chance em % e "1 em N", para quem a regra vale, quantos monstros alcança), as regras do `ItemDrop.txt` e o drop comum e o zen de cada monstro |
| | **Taxas e opções** | EXP, drop, zen, joias, Chaos Machine e todas as opções dos 7 `GameServerInfo - X.dat`, por plano; grava nos 5 GameServers e confere na memória se valeu na hora |
| | **EXP dinâmica** | % da EXP por faixa de nível, com o gráfico da curva e a EXP efetiva de cada plano |
| | **Resets** | Créditos do Master e do Supreme Reset e o atributo máximo |
| Conteúdo | **Monstros (respawn)** | Onde e quantos monstros nascem, desenhados sobre o mapa do jogo (tecla Tab) |
| | **Atributos dos monstros** | Vida, dano, defesa, velocidade, visão, tempo para renascer e resistências de cada monstro (`Monster.txt`), com Reload Monster; os poderes especiais só para consulta |
| | **Itens novos** | Item novo com o visual de outro |
| Site | **Loja de itens** | Catálogo, preços e vendas da loja paga com Cash |
| | **Notícias** | Publicar, editar e apagar, com editor de HTML e prévia |
| | **Páginas** | Termos, Privacidade, Reembolso e Contate-nos (texto e canais) |
| | **Comprar Zen** | Pacotes e últimas compras |
| | **Mensagens** | Contate-nos: ler, responder, arquivar |
| Painel | **Relatórios** | CSV (abre no Excel em português) e PDF: contas, personagens, bans, pedidos, loja de itens, Zen, mercado, jogadores online, mensagens e auditoria |
| | **Auditoria** | Quem fez o quê, quando e de onde |
| | **Usuários do painel** | Criar, mudar papel, desativar, desbloquear, redefinir senha |
| | **Minha conta** | Trocar a própria senha |

A busca no topo (tecla `/`) acha contas, personagens e páginas. O botão de lua/sol troca o tema claro/escuro.

### O que continua igual

As regras de cada tela são as mesmas do programa antigo. Exemplos:

- mexer em itens, pontos ou árvore master de uma conta online desloga a conta antes;
- tudo que grava arquivo faz backup `.bak-*` ao lado e recarrega no servidor sem reiniciar;
- monstros só recarregam fora das invasões (se houver uma no ar, o painel agenda para quando ela acabar).

Detalhes de cada assunto continuam em [OPERACAO.md](OPERACAO.md) e [SITE.md](SITE.md).

## Instalar ou atualizar no PC do servidor

1. Instale o **.NET SDK 10 (x64)** (já traz o runtime do ASP.NET Core): https://dotnet.microsoft.com/download/dotnet/10.0
2. Num PowerShell **como administrador**, na pasta do repositório: `.\tools\Publicar-Painel.ps1`
   - fecha o painel e o vigia, publica em `C:\MuServer\MuChilaAdmin` e cria as chaves do vigia;
   - cria os atalhos **Mu Chila - Painel** e **Mu Chila - Vigia** na pasta Inicializar e **Mu Chila Admin** na área de trabalho;
   - se a configuração abre o painel para o Radmin, cria a regra do firewall;
   - liga tudo e confere (http://localhost:5170/api/saude e a sonda do vigia).
3. Na primeira vez, abra http://localhost:5170 **no próprio PC do servidor** e crie o administrador.

As tabelas do painel (`MUCHILA_ADMIN_USUARIOS`, `_AUDITORIA`, `_ALERTAS`, `_BANS`, `_METRICAS`) são criadas sozinhas no primeiro uso.

### Configuração

Arquivo `%ProgramData%\MuChilaAdmin\muchila-admin.json`. Sem o arquivo, valem os padrões do PC do servidor. Modelo comentado:
[tools/MuChilaAdmin/muchila-admin.exemplo.json](../tools/MuChilaAdmin/muchila-admin.exemplo.json).

| Chave | Padrão | Para quê |
|---|---|---|
| `Banco` | `Server=.\MUONLINE;Database=MuOnlineS14;Integrated Security=true;...` | Conexão com o banco do jogo |
| `PastaServidor` | `C:\MuServer` | Pasta do servidor |
| `PastaCliente` | `C:\Projetos\MuServer-Season14\2 - Cliente Season 14 Full` | Cliente do repositório (loja de cash e itens novos) |
| `PastaRepositorio` | `C:\Projetos\MuServer-Season14` | Para o "Publicar para o launcher" |
| `Ambiente` | `producao` | `desenvolvimento` mostra a faixa roxa e não liga o vigia |
| `Web.Porta` | `5170` | Porta do painel |
| `Web.Enderecos` | `["localhost"]` | Onde o painel atende |
| `Web.MinutosSessao` | `480` | Tempo sem uso até pedir login de novo |
| `Web.AbrirNavegador` | `true` | Abrir o navegador ao abrir pelo atalho da área de trabalho |

As variáveis de ambiente `MUCHILA_ROOT`, `MUCHILA_CLIENTE` e `MUCHILA_BANCO` passam por cima do arquivo, e
`MUCHILA_ADMIN_CONFIG` aponta para outro arquivo.

### Abrir para a rede do Radmin

1. Em `muchila-admin.json`, acrescente o IP do Radmin deste PC: `"Enderecos": [ "localhost", "26.139.39.123" ]`.
2. Rode `.\tools\Publicar-Painel.ps1` como administrador. Ele cria a regra "Mu Chila Admin (painel web, Radmin)": a porta 5170 aceita conexão **só da rede 26.x**.
3. Nos outros PCs: http://26.139.39.123:5170.

Não exponha o painel na internet (não há HTTPS). Para acessar de fora, use o Radmin.

## Ambiente de desenvolvimento (duas máquinas)

Cada desenvolvedor roda o painel no próprio PC, com **banco e pastas de teste**, sem tocar no servidor de verdade.

1. Instale:
   - Git;
   - .NET SDK 10 (x64);
   - SQL Server Developer ou Express, com as ferramentas de linha de comando (`sqlcmd`).
2. Clone o repositório e, num PowerShell na pasta dele: `.\tools\Preparar-Ambiente-Dev.ps1`. O script:
   - restaura o banco do kit (`DB\MuOnlineS14.bak`) neste SQL Server e aplica os scripts do painel e do site;
   - copia a pasta do servidor para `C:\MuChilaDev\MuServer` e as partes do cliente para `C:\MuChilaDev\Cliente`;
   - grava a configuração de desenvolvimento em `%ProgramData%\MuChilaAdmin\muchila-admin.json`;
   - baixa o Tailwind CSS, compila e cria o usuário **dev** (administrador) com uma senha temporária.

   Opções:
   - `-Instancia .\SQLEXPRESS` escolhe o SQL Server;
   - `-Pasta D:\Dev` muda a pasta das cópias;
   - `-RecriarBanco` restaura o banco de novo;
   - `-SemCopia` mantém as cópias como estão;
   - `-Php <pasta>` copia um PHP 8 para testar a página Notícias.
3. Rode: `dotnet run --project tools\MuChilaAdmin`, ou o `.exe` em `tools\MuChilaAdmin\bin\Debug\net10.0-windows`. Abra http://localhost:5170, entre com `dev` e a senha temporária e troque a senha.
4. Testes: `dotnet test tools\MuChilaAdmin.Testes`. São 74 testes:
   - senhas e bloqueio;
   - login, troca obrigatória, CSRF, papéis e cabeçalhos, num servidor de teste sem banco;
   - CSV e PDF;
   - gravação dos drops.

   Os testes antigos de arquivos (`MuChilaAdmin.exe --testar-lojas|--testar-drops|... <saida>`) rodam contra uma cópia indicada em `MUCHILA_ROOT`.

**Trabalhando em dupla:**

- Cada um num branch, com Pull Request para o `main`.
- Os dados de teste (`C:\MuChilaDev`, banco local) e o `muchila-admin.json` ficam fora do Git.
- Para trazer a versão nova do servidor para o seu PC de testes: `git pull` e rode de novo o `.\tools\Preparar-Ambiente-Dev.ps1` (ele refaz as cópias; o banco só é restaurado de novo com `-RecriarBanco`).
- Para publicar no servidor: depois do merge, o dono roda `.\tools\Publicar-Painel.ps1` no PC do servidor.

**Visual:**

- Tailwind CSS 4: as classes ficam nos `.razor`; o tema e os componentes, em `tools\MuChilaAdmin\Styles\app.css`.
- O build gera `wwwroot\css\app.css` quando o `tailwindcss.exe` está em `tools\MuChilaAdmin\.ferramentas` (o script de preparação baixa). Sem ele, o build usa o CSS que já está no repositório.
- Ícones: `wwwroot\img\icones.svg`, os mesmos do site.

## Estrutura do código

| Pasta | O que tem |
|---|---|
| `tools\MuChilaAdmin.Core\Infra` | Configuração e acesso ao banco |
| `tools\MuChilaAdmin.Core\Servidor` | Processos, recarga, vigia (`ResetWatcher*`), eventos, bônus, avisos |
| `tools\MuChilaAdmin.Core\Jogo` | Contas, itens, lojas, loja de cash, drops, monstros, taxas, EXP dinâmica, passe, itens novos |
| `tools\MuChilaAdmin.Core\Site` | Loja de itens, notícias, páginas, Zen e mensagens do site |
| `tools\MuChilaAdmin.Core\Painel` | Usuários e senhas, auditoria, alertas, bans, dashboard, relatórios (CSV/PDF) |
| `tools\MuChilaAdmin\Web` | Montagem do servidor web, segurança (cookie, papéis, cabeçalhos), serviços das telas e tarefas de fundo |
| `tools\MuChilaAdmin\Components` | As telas (Blazor): `Layout`, `UI` (componentes reaproveitados) e `Pages` por grupo do menu |
| `tools\MuChilaAdmin\Cli` | Linha de comando: `--vigia-reset`, `--criar-usuario`, `--redefinir-senha`, `--teste`, `--testar-*`... |

Regras para quem mexe:

- Toda ação que muda algo passa pelo `ExecutorAcoes`, que confere o papel, roda uma de cada vez, mostra o resultado e grava na auditoria.
- Erro de banco nunca aparece cru na tela (issue #38): a mensagem é curta e o detalhe vai para `painel-erros.log`, ao lado do executável.

## Linha de comando

| Comando | O que faz |
|---|---|
| `MuChilaAdmin.exe` | Liga o painel (se ainda não estiver no ar) e abre o navegador |
| `MuChilaAdmin.exe --web` | Liga o painel sem abrir o navegador (atalho da inicialização) |
| `MuChilaAdmin.exe --vigia-reset` | O vigia dos GameServers |
| `--criar-usuario <usuário> <admin\|moderador\|leitura> <arquivo>` | Cria um usuário do painel; a senha temporária vai para o arquivo |
| `--redefinir-senha <usuário> <arquivo>` | Nova senha temporária (e desbloqueia) |
| `--teste <arquivo>` | Conferência rápida: configuração, banco, tabelas do painel, servidores |
| `--vigia-sondar <arquivo>` | Estado do vigia sem mexer em nada |

Os outros comandos (`--forcar-logout`, `--dar-pontos`, `--zerar-master`, `--colocar-item`, `--invasao`...) estão descritos em [OPERACAO.md](OPERACAO.md).

## Problemas comuns

| Problema | Solução |
|---|---|
| "O painel não ligou: a porta 5170 já está em uso" | Outro programa usa a porta: troque `Web.Porta` no `muchila-admin.json` |
| "O painel não conseguiu acessar o banco de dados" | O SQL Server está ligado? Confira `Banco` no `muchila-admin.json`. Detalhe em `painel-erros.log` |
| A página não abre de outro PC | `Web.Enderecos` tem o IP do Radmin? A regra do firewall existe? (rode o `Publicar-Painel.ps1` como administrador) |
| Notícias: "PHP do site não encontrado" | O painel usa o PHP do site (`Site\php\php.exe`). No PC de desenvolvimento, rode o `Preparar-Ambiente-Dev.ps1` com `-Php` |
| Faixa "Ambiente de DESENVOLVIMENTO" no PC do servidor | O `muchila-admin.json` está com `"Ambiente": "desenvolvimento"`: apague o arquivo ou troque para `producao` |
