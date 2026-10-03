# Tarefa: liberar baú e venda dos itens que o jogador consegue obter

> Levantamento feito em **03/10/2026**. **Nada foi aplicado ainda.** Este documento é a ordem de serviço completa:
> contexto, o que mudar item por item, como aplicar, como publicar e como conferir. Quem pegar a tarefa não precisa
> refazer a análise.

## Objetivo

Hoje **1.106 itens** do jogo não vão para o baú ou não vendem ao NPC. **65** deles o jogador consegue obter (loja de
cash, lojas de NPC, drop, caixas e eventos), e são esses que importam. Os outros 1.041 (566 Muuns, 84 "(Bound)",
equipamentos presos etc.) não caem, não são vendidos e não saem de caixa: ficam como estão.

A tarefa muda **42 itens no cliente** (regras A e B) e **1 item no servidor** (Wizard's Ring). Deixa 8 itens como
estão, de propósito (regra C).

## Arquivos

| Arquivo | Para quê |
|---|---|
| [itens-sem-bau-ou-venda.csv](itens-sem-bau-ou-venda.csv) | Lista dos 1.106 itens (abre no Excel; separador `;`). Os 65 que o jogador obtém vêm primeiro. Colunas: código, seção, índice, nome, guarda no baú, vende ao NPC, larga, troca, loja pessoal, onde está a trava, se o jogador consegue obter, de onde vem, observação e as 7 permissões do cliente |
| [`tools/Listar-ItensSemBauOuVenda.ps1`](../tools/Listar-ItensSemBauOuVenda.ps1) | Gera o CSV a partir dos arquivos do repositório (só lê). Rode de novo no fim para conferir |
| [`tools/Liberar-ItemCliente.ps1`](../tools/Liberar-ItemCliente.ps1) | Já existe. Troca as 7 permissões de itens no cliente nos 3 idiomas, recalcula a soma de verificação e faz backup (`*.bak-*`, fora do git) |

## Como o jogo decide

Duas travas. **As duas precisam liberar.**

**1. Cliente:** 7 permissões por item em `2 - Cliente Season 14 Full\Data\Local\{Eng,Por,Spn}\item_*.bmd`. São
iguais nos 3 idiomas (conferido). Registros de 672 bytes, XOR `FC CF AB` recomeçando em cada registro, permissões nos
bytes 661–667. Ordem:

| Posição | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Permissão | largar | trocar | loja pessoal | **baú** | **vender ao NPC** | item valioso | consertar |

- **Posição 5 = vender e 7 = consertar: confirmadas.** Poções, maçã, flechas e Town Portal Scroll, que vendem em
  qualquer MU, são `1111100`. Itens com durabilidade (armas, anéis) têm a 7ª = 1. Nenhum item do cliente tem 5ª = 0
  e 7ª = 1.
- **Posição 4 = baú: deduzida, falta o teste no jogo (passo 0).** É a única ordem que explica tudo junto:
  - tickets, Reset Fruits e talismãs da loja de cash são `0001000` (no MU, item de cash não troca, mas vai ao baú);
  - os 84 itens "(Bound)" são `0000111` (vendem e consertam, não trocam nem vão ao baú);
  - o Vault Expansion Certificate é `0000100`;
  - o Lethal Wizard's Ring com `0001000` "não vendia nem largava" (issue #50).
- **Posição 6** é 1 em asas, joias e montarias: deve ser a confirmação de item valioso. Não mexer.

**2. Servidor:** `Data\Item\ItemMove.txt`, colunas `AllowDrop AllowSell AllowTrade AllowVault`. Item fora da lista =
tudo liberado. Só 3 itens têm `AllowVault = 0`, todos padrão do kit: 6675 Absolute Weapon of Archangel, 6676
Wizard's Ring e 7232 Cursed Castle Water. Só o Wizard's Ring está com jogadores.

## Decisões já tomadas (padrão; o dono pode mudar antes de mandar)

- **Regra A — item só da loja de cash:** passa a ir ao baú, para passar entre personagens da mesma conta. A venda
  continua bloqueada, igual aos outros itens de cash (tickets, Reset Fruits, talismãs). Muda só a posição 4.
- **Regra B — item de drop, caixa, evento ou loja de NPC:** passa a ir ao baú **e** a vender. Muda as posições 4 e 5;
  as outras ficam iguais. Os preços de venda são baixos (no máximo 9.000 Zen; vários com valor 0, que devem vender por
  0 Zen), então não abre fonte de Zen.
- **Regra C — não mexer:**
  - itens de missão: Elena's Letter (7598) e Switch Scroll (7621);
  - Vault Expansion Certificate (7331), que é feito para ser usado;
  - os 5 ícones de aposta do Moss Merchant (6727–6731).
- **Wizard's Ring (servidor):** `AllowVault` 0 → 1, mantendo `AllowTrade = 0`. Vale para todos os níveis: +1 é o
  Warrior's Ring e +2 o Champion's Ring, que personagens antigos ainda têm (issue #11).
- **Fora do escopo:** Muuns, "(Bound)" e os itens que o jogador não obtém.

## Passo 0 — confirmar a posição 4 no jogo (pedir ao dono)

O teste só pode ser feito no jogo. Peça ao dono, antes de aplicar:
1. pôr um **Devil Square Ticket** (`0001000`) no baú: **deve entrar**;
2. tentar pôr um **Seal of Ascension** (`0000000`) no baú: **não deve entrar**.

Se o ticket **não** entrar, a posição do baú é outra: pare e reavalie com o dono antes de mudar qualquer coisa.

## Passo 1 — cliente (regras A e B)

Na raiz do repositório, primeiro com `-Simular` (mostra o antes e o depois sem gravar), depois sem ele. A ferramenta
aplica a mesma permissão a todos os códigos da chamada, por isso os itens vão agrupados pelo resultado.

```powershell
# Regra A (25 itens de cash: 0000000 -> 0001000)
#   Seals, Master Seals, Max AG/SD Boost Aura, Scrolls, Package Box A/B/E/F e os 4 cartões de personagem
.\tools\Liberar-ItemCliente.ps1 -Permissoes 0001000 -Codigos 6699,6700,6701,6718,6719,6749,6750,6760,6761,7240,7241,7242,7243,7244,7245,7259,7265,7266,7302,7303,7306,7307,7337,7449,7655

# Regra B (17 itens): baú e venda
.\tools\Liberar-ItemCliente.ps1 -Permissoes 0001100 -Codigos 6695,7321,7322          # Eilte Transfer Skeleton Ring, Stardust, Kalt Stone (eram 0000100)
.\tools\Liberar-ItemCliente.ps1 -Permissoes 0001100 -Codigos 6762,7264               # Pet Unicorn, Talisman of Chaos Assembly (eram 0001000)
.\tools\Liberar-ItemCliente.ps1 -Permissoes 0001100 -Codigos 7257,7258,7280,7281     # Red/Golden Cherry Blossom Branch, Silver Key, Gold Key (eram 0000000)
.\tools\Liberar-ItemCliente.ps1 -Permissoes 1111100 -Codigos 7179                    # Box of Luck (era 1111000)
.\tools\Liberar-ItemCliente.ps1 -Permissoes 1001100 -Codigos 7189,7608               # Rena, Season Level Reward B (eram 1000100)
.\tools\Liberar-ItemCliente.ps1 -Permissoes 0111100 -Codigos 7238,7239               # Elite Healing/Mana Potion (eram 0111000)
.\tools\Liberar-ItemCliente.ps1 -Permissoes 0101100 -Codigos 7383,7612               # Card Piece, Energy Drink (eram 0100000)
.\tools\Liberar-ItemCliente.ps1 -Permissoes 1101100 -Codigos 7446                    # Piece of Summoning Scroll (era 1100000)
```

Ficam como estão, já certos pela regra A (vão ao baú; venda bloqueada como item de cash): os tickets, as Reset
Fruits, os talismãs de cash, os tickets de canal e de Varka, o Elite SD Potion e o Talisman of Luck.

Cada chamada deve terminar com `soma recalculada OK` nos 3 arquivos. Se aparecer `ERRADA`, restaure o `.bak-*` e pare.

## Passo 2 — servidor (Wizard's Ring)

Em `3 - MuServer Mu Chila (servidor configurado)\Data\Item\ItemMove.txt`, na linha do 6676, troque o `AllowVault` de
`0` para `1`, mantendo o resto:

```
6676      1           1           0            1            //Wizards Ring
```

O arquivo do repositório é uma cópia: o servidor lê `C:\MuServer\Data\Item\ItemMove.txt`, no PC do servidor (passo 3).

**Pendência a investigar (não bloqueia a tarefa):** na issue #11 o jogador disse que o Warrior's Ring e o Champion's
Ring também não vendiam, mas cliente (`0111110`) e servidor (`AllowSell = 1`) liberam a venda. Suspeita: o
`ItemValue.txt` só tem preço para o nível 0 (`6676 0 * 30000`), e os níveis +1 e +2 ficam sem preço. Peça ao dono para
testar a venda depois da mudança. Se não vender, considere adicionar linhas de preço para os níveis 1 e 2.

## Passo 3 — publicar (no PC do servidor, pelo dono)

1. `git pull` na pasta do repositório.
2. **Cliente:** `.\tools\Publicar-Launcher.ps1` (ou "Publicar atualização do cliente" no painel). Os jogadores recebem
   ao abrir o launcher.
3. **Servidor:** copiar `3 - MuServer Mu Chila (servidor configurado)\Data\Item\ItemMove.txt` por cima de
   `C:\MuServer\Data\Item\ItemMove.txt`. Depois:
   ```powershell
   .\tools\Recarregar-Servidor.ps1 -Servidor GameServer  -Item Item
   .\tools\Recarregar-Servidor.ps1 -Servidor CastleSiege -Item Item
   ```
   Se algum GameServer (VIP, BattleCore) não pegar a mudança, reinicie os GameServers pelo painel: Processos e
   jogadores → Parar todos → Iniciar todos.
   **Não rode o `Sincronizar-Servidor.ps1` antes de copiar:** ele espelha `C:\MuServer` no repositório e desfaria a
   mudança.

## Passo 4 — conferir

```powershell
.\tools\Listar-ItensSemBauOuVenda.ps1
```

Hoje ele mostra `na lista: 1106 | não guardam no baú: 929 | não vendem: 412 | o jogador consegue obter: 65`.
Depois das mudanças deve mostrar exatamente:

```
na lista: 1088 | não guardam no baú: 891 | não vendem: 400 | o jogador consegue obter: 48
```

Entre os 48 obtidos que sobram, os que não vão ao baú devem ser só os 8 da regra C. O Wizard's Ring (6676) deve sair da
lista. No jogo, o dono confere: um Seal no baú, Stardust no baú e vendido ao Hanzo, e um Warrior's Ring no baú.

## Passo 5 — documentação e issues

- **`docs/OPERACAO.md`, seção "Itens que não podem ser largados":** registrar a ordem das 7 permissões (tabela acima)
  e citar o `Listar-ItensSemBauOuVenda.ps1`.
- **`docs/ALTERACOES.md`:** uma linha para os 42 itens do `item_*.bmd` e outra para o `ItemMove.txt`.
- **`docs/PROBLEMAS-E-SOLUCOES.md`:** uma entrada "Itens que não vão ao baú ou não vendem".
- **Cabeçalho do `tools/Liberar-ItemCliente.ps1`:** incluir a ordem das permissões.
- **Issue #11 (Warrior's/Champion's Ring):** comentar o que foi feito e o que testar, como manda a regra do projeto.
- **Commit e push:** só quando o dono pedir.
