# Mu Chila

Servidor privado de **MU Online Season 14** para testes com amigos. É baseado no kit
"MuServer Season 14" (Aprendiz Mu Online / Allan JPA), com o emulador **MuDevs (versão FREE)**.
O kit foi instalado, corrigido e ajustado. O que mudou em relação ao kit original está em
[docs/ALTERACOES.md](docs/ALTERACOES.md).

| | |
|---|---|
| Nome no jogo | **Mu Chila** (grupo 1 da lista de servidores) |
| IP para os jogadores | `26.139.39.123`, rede Radmin VPN do dono do servidor |
| Banco de dados | SQL Server, instância `.\MUONLINE`, bancos `MuOnlineS14` e `BattleCore` |
| Pasta do servidor em uso | `C:\MuServer` (os executáveis e o launcher esperam esse caminho) |

## Estrutura do repositório

| Pasta | Conteúdo |
|---|---|
| `0 - Guia Facil/` | Guias originais do kit (servidor e cliente) |
| `1 - MuServer Season 14 - Aprendiz Mu Online/` | Kit do servidor **original**, sem alterações (referência) |
| `2 - Cliente Season 14 Full/` | **Cliente liberado para teste**: IP do Radmin, nome "Mu Chila", servidor na lista |
| `3 - MuServer Mu Chila (servidor configurado)/` | **Servidor configurado**: cópia de `C:\MuServer` sem logs |
| `5 - PrintScreans/`, `6 - ChangeLog/` | Imagens e changelog do kit original |
| `tools/` | Scripts de apoio (sincronizar servidor, recarregar sem reiniciar, ler o log do cliente) |
| `docs/` | Documentação |

## Início rápido

**Servidor (máquina do dono):**
1. A primeira instalação está em [docs/INSTALACAO.md](docs/INSTALACAO.md).
2. No dia a dia: ligue o Radmin VPN, abra `C:\MuServer\Startup - Iniciar Server.lnk`, clique em iniciar e espere tudo ficar verde.
3. Para criar contas, use `C:\MuServer\Criar Conta.bat`.

**Jogadores:**
1. Entre na rede Radmin VPN do dono do servidor.
2. Copie a pasta `2 - Cliente Season 14 Full` (ou o `.zip` que o dono enviar) e abra o `main.exe`. Ele pede permissão de administrador.
3. Escolha **Mu Chila** e faça o login em até **30 segundos**.

## Documentação

- [docs/INSTALACAO.md](docs/INSTALACAO.md): instalar do zero (SQL, ODBC, scripts, IPs, launcher).
- [docs/OPERACAO.md](docs/OPERACAO.md): ligar e desligar, recarregar sem reiniciar, contas, GM, eventos, caixas, resolução, distribuir o cliente.
- [docs/PROBLEMAS-E-SOLUCOES.md](docs/PROBLEMAS-E-SOLUCOES.md): erros já diagnosticados e pendências.
- [docs/ALTERACOES.md](docs/ALTERACOES.md): cada arquivo alterado em relação ao kit.

## Aviso

O cliente é da Webzen, e os executáveis do servidor são do emulador MuDevs e do kit Aprendiz Mu Online.
Este repositório serve para estudo e testes. Não é um serviço comercial.
