# Alterações de alto risco e o que depende de modificar o GameServer

> Mapa de decisão criado em **02/10/2026**. Serve para o @rodolfot/@Mario4712 decidirem o que vale investir, e para
> um próximo desenvolvedor (ou outro Claude) continuar sem redescobrir tudo. **Nada aqui foi implementado** — é análise.

## Contexto rápido (para quem pega isto frio)

- Servidor **MU Online Season 14** (kit "Allan JPA / AprendizMuOnline"). Repo `rodolfot/Mu-chila`, branch `main`.
- Painel web **Mu Chila Admin** em `tools/MuChilaAdmin` (Blazor) + `tools/MuChilaAdmin.Core` (regras/arquivos). Roda compilado em `C:\MuServer\MuChilaAdmin` e edita os arquivos em `C:\MuServer`.
- **"Vigia" (`ResetWatcher`)**: já modifica o **GameServer em runtime** lendo/escrevendo a memória do processo `S14.exe` por padrões de bytes. É **reversível** (não mexe no `.exe` em disco). Arquivos: `tools/MuChilaAdmin.Core/Servidor/ResetWatcher*.cs` (Avisos, Classe, Config, Invasoes, Moss, Passe). É o método aceito para "mexer no GameServer" com risco controlado.

## As duas paredes que geram "alto risco"

1. **Cliente** (`main.exe`, DLLs, texturas) — fechado e protegido pelo **GameGuard** (`GameGuard.des`, `NPPSK.DLL`, `NPX.DLL` na pasta do cliente). Trocar/alterar arquivos do cliente quebra o anti-cheat. Não temos o cliente-fonte.
2. **GameServer** — binário `S14.exe` (~2,8 MB). **Não temos o código-fonte** (confirmado: nenhum `.cpp/.h/.sln/.vcxproj` no repo/kit). Logo, lógica nova = engenharia reversa + patch de runtime (vigia) ou injeção. Ajuste de valor/horário = vigia (já fazemos).

## 1. Fatos técnicos das 3 perguntas de 02/10

| Pergunta | Resposta | Fato |
|----------|----------|------|
| Alterar **tamanho dos monstros**? | ❌ não por dados | `Data/Monster/Monster.txt` não tem coluna de escala (37 colunas, nenhuma de size); sem arquivo de escala. Tamanho é do modelo no cliente; bosses grandes têm escala fixa no `main.exe`. Contorno: usar um monstro que já é grande (ex.: Sapi 441-443 para "aranha gigante"). |
| Mudar o **limite de 10.000 monstros**? | ❌ sem config | Nenhum config desse limite; é vetor de tamanho fixo no `S14.exe`. Hoje há **~7.868** pontos de spawn (`Data/Monster/MonsterSetBase/*.xml`), ainda com folga. |
| **Imagens em alta resolução**? | ❌ inviável em massa | Cliente tem **~13.000 texturas** proprietárias: 10.489 `.ozj`, 2.442 `.ozt`, 145 `.ozg`, 131 `.ozb`. Engine antigo (limite de textura) + GameGuard checa integridade. Resolução de **tela** (1080p) já existe via `Resolucao - Configurar.bat` (issue #4). |

## 2. Tudo de alto risco já pedido

### Barrado pelo CLIENTE (GameGuard) — mod de GameServer NÃO resolve

| Pedido | Origem | Status |
|--------|--------|--------|
| Cliente/`main.exe` novo do zero | conversa | Inviável (engine proprietária + GameGuard) |
| Dividir nível em **Normal + Master** e mostrar **no jogo** | "Feature 1" / rel. #20 | Não feito (usuário mandou deixar quieto) |
| Aba de atributos (tecla C) mostrar level normal/master + **resets** no jogo | #20 | Contornado: aparece no **site** |
| Aumentar tamanho da tela **dentro do jogo** | #4 | Contornado: `.bat` externo |
| Traduzir o jogo **100%** (textos *hardcoded* no `main.exe`) | #41 | Parcial: Lang editável dá; `main.exe` não |
| Nome de classe errado na **criação de personagem** | #35 | Aberto — provável texto do cliente |
| **Tamanho dos monstros** | 02/10 | Inviável por dados |
| **Imagens em alta resolução** | 02/10 | Inviável (13k assets + GameGuard) |

### Depende do GAMESERVER (sem source) — ver seção 3

| Pedido | Origem |
|--------|--------|
| Qualquer monstro soltar **qualquer skill** | #49 |
| Pontos restantes **atualizarem na tela** após `/addstr` | #6, #40 |
| Aumentar o **limite de monstros** | 02/10 |

> **A maioria das 52 issues NÃO é alto risco** — são painel, site, banco, drops, eventos e regras por arquivos de dados.

## 3. O que fica possível SE modificarmos o GameServer

Sem source → **vigia (runtime, reversível)** para ajustes pontuais; **injeção/patch** para lógica nova (RE do `S14.exe`).
Caminho de **menor risco** para lógica nova: **conseguir o source do GameServer com o Allan JPA**.

### Nível 1 — pelo vigia (reversível) · risco MODERADO
- Ajustar fórmulas/constantes fora dos `.dat` (dano, defesa, XP, +luck/jewel).
- Ligar/desligar comportamentos por flag (PvP/PK, drop em safe zone).
- Afinar eventos/bosses além do arquivo.
- Corrigir valores fixos de reset/level (partes de #27, #28), quando for valor e não lógica.

### Nível 2 — patch de lógica / injeção · risco ALTO (sem source)
| Possível | Atende | Por que destrava |
|----------|--------|------------------|
| **Pontos restantes atualizarem na tela** após `/addstr` (`/f /a /v`...) | #6, #40 | o cliente já exibe; só falta o servidor enviar o pacote — **não toca o cliente** |
| **Qualquer monstro soltar qualquer skill** | #49 | patchar a IA de cast (hoje só 8 monstros: Dark Elf, Nightmare, Maya Hand, Maya, Selupan, Medic, Core Magriffy) |
| **Itens "mortos" ganharem efeito/uso** | #10, #11, #29, #50, #51 | vender/dropar é dado; o *efeito* do item é lógica |
| **Regras custom de reset/master** (resetar skills no master; XP master após 400) | #16, #27, #28 | progressão fixa no `.exe` |
| **Sistemas novos** (kill streak, loot por dano, drop garantido de boss, recompensa por ranking) | futuros | lógica server-side, independe do cliente |
| **IA custom de bosses** (curar, invocar, enfurecer < X% vida) | mini-bosses mais ricos | estende o sistema de mini-bosses |

### Nível 3 — estruturas/limites · risco ALTÍSSIMO
- Aumentar limite de monstros (10.000 → hoje ~7.868), players, slots de inventário. Mexe em vetores de tamanho fixo → pode corromper memória. **Recomendo evitar.**

### Nem com GameServer resolve (preso ao CLIENTE)
Nível Normal+Master **no jogo**; resets na aba (tecla C) **no jogo** (#20); tamanho de tela no jogo (#4); tradução de textos do `main.exe` (#41); classe errada na criação (#35); tamanho de monstro (modelo 3D); imagens HD (texturas).

## 4. Recomendação

1. **Mais valor / mais contido:** Nível 2 — **#6 (pontos na tela)** e **#49 (skill de qualquer monstro)**. Resolvem pedidos reais sem tocar o cliente.
2. **Evitar:** Nível 3 (limites/estruturas).
3. **Movimento estratégico:** pedir o **source do GameServer ao Allan JPA**. Transforma o Nível 2 de "gambiarra arriscada" em alteração normal (recompilar).

## 5. Apoio técnico para o próximo (onde olhar)

- GameServer: `3 - MuServer Mu Chila (servidor configurado)/GameServer/S14.exe` (sem source).
- Vigia / patches de runtime já prontos: `tools/MuChilaAdmin.Core/Servidor/ResetWatcher*.cs` (ver `ResetWatcher.Invasoes.cs` como exemplo de achar estrutura por padrão de bytes e escrever na memória).
- Sistema de skill de monstro (para #49): `Data/Monster/Skill/MonsterSkill.txt` (pares skill+unidade), `MonsterSkillUnit.txt`, `MonsterSkillElement.txt`; editor no painel em `tools/MuChilaAdmin.Core/Jogo/MonsterStats.cs` (`MonsterSkills`). A IA só faz 8 monstros soltarem skill real — ver conjunto `MonstrosQueSoltam`.
- Mini-bosses (base para IA custom de boss): `tools/MuChilaAdmin.Core/Servidor/MiniBosses.cs` + `InvasionManager.dat`.
- Mensagens do jogo: `Data/Lang/Portuguese.xml` (seção `<InvacionMsg>` para invasões). Parte dos textos vem do `Lang.mpr` (ver `LangPack.cs`, issue #41).
- Cliente (parede do GameGuard): `2 - Cliente Season 14 Full/` — `GameGuard.des`, `Main.dll`, texturas OZJ/OZT em `Data/`.
- Issues no GitHub `rodolfot/Mu-chila` (1–52 em 02/10/2026). Alto risco ligado a cliente: #4, #20, #35, #41 (+#6 parte cliente). Alto risco ligado a GameServer: #6, #40, #49.
