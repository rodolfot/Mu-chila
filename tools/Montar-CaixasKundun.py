# Monta as listas de equipamento das Box of Kundun +4/+5 sem os 4 melhores itens de cada classe.
# Ranking por classe (só itens que caem de monstro, sem variantes -J/Bound): sets por (ReqLevel, defesa); armas por (ReqLevel, nível do item, dano).
# Top 1-4 de cada classe: nunca entram (nem para outra classe que use o mesmo item).
# Box +5: os 2 melhores sets e as 2 melhores armas permitidos de cada classe. Box +4: os 2 seguintes.
# Uso: python tools\Montar-CaixasKundun.py            -> mostra o top 4 e o que cada caixa dá por classe
#      python tools\Montar-CaixasKundun.py --simular  -> mostra o arquivo que seria gravado
#      python tools\Montar-CaixasKundun.py --gravar   -> grava (com backup .bak-*); depois, Reload EventItemBag no GS e no CS
# Para refazer (ex.: mudou o Item.txt), restaure antes os arquivos originais das caixas: o script recusa arquivos já reorganizados.
import re, collections, sys
CL = ['DW', 'DK', 'FE', 'MG', 'DL', 'SU', 'RF', 'GL', 'RW']
secao = -1; itens = {}
for l in open(r'C:\MuServer\Data\Item\Item.txt', encoding='cp1252'):
    t = l.strip()
    if re.fullmatch(r'\d+', t): secao = int(t); continue
    if secao > 11: continue
    m = re.match(r'^(\d+)\s+(-?\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+"([^"]+)"\s+(.*)$', t)
    if not m: continue
    v = m.group(10).split(); idx = int(m.group(1))
    d = {'sec': secao, 'idx': idx, 'nome': m.group(9).strip(), 'skill': int(m.group(3)), 'drop': int(m.group(8)), 'lvl': int(v[0])}
    if secao < 6:
        if len(v) < 22: continue
        d.update(forca=int(v[2]) + int(v[6]), req=int(v[7]), cl=[int(x) for x in v[13:22]])
    else:
        if len(v) < 19: continue
        d.update(forca=int(v[1]), req=int(v[4]), cl=[int(x) for x in v[10:19]])
    itens[(secao, idx)] = d
especial = lambda n: re.search(r'-J$|\(Bound\)|Bound|Archangel', n)
# sets: agrupa seções 7-11 pelo índice; a armadura (8) define nome/req/defesa/classes
sets = {}
for (s, i), d in itens.items():
    if 7 <= s <= 11: sets.setdefault(i, {})[s] = d
setinfo = []
for i, p in sets.items():
    b = p.get(8) or next(iter(p.values()))
    nome = re.sub(r'\s*(Armor|Armour|Robe|Suit)\s*$', '', b['nome']).strip() or b['nome']
    if not b['drop'] or especial(b['nome']): continue
    setinfo.append({'nome': nome, 'idx': i, 'req': b['req'], 'def': b['forca'], 'cl': b['cl'], 'pecas': [p[s] for s in sorted(p) if p[s]['drop']]})
armas = [d for d in itens.values() if d['sec'] < 6 and d['drop'] and not especial(d['nome'])]
rank = {}   # (tipo, chave) -> {classe: posição}
porclasse = {}
for ci, c in enumerate(CL):
    ls, vistos = [], set()
    for s in sorted([s for s in setinfo if s['cl'][ci] > 0], key=lambda s: (-s['req'], -s['def'], -s['idx'])):
        if s['nome'] in vistos: continue
        vistos.add(s['nome']); ls.append(s)
    la = sorted([a for a in armas if a['cl'][ci] > 0], key=lambda a: (-a['req'], -a['lvl'], -a['forca']))
    porclasse[c] = (ls, la)
    for n, s in enumerate(ls, 1): rank.setdefault(('set', s['nome']), {})[c] = n
    for n, a in enumerate(la, 1): rank.setdefault(('arma', (a['sec'], a['idx'])), {})[c] = n
proibido = {k for k, r in rank.items() if min(r.values()) <= 4}
faixas = {'+5': (1, 2), '+4': (3, 4)}
saida = {}
for caixa, (a, b) in faixas.items():
    linhas, resumo = [], collections.OrderedDict()
    usados = set()
    for c in CL:
        ls, la = porclasse[c]
        ok_s = [s for s in ls if ('set', s['nome']) not in proibido]
        ok_a = [x for x in la if ('arma', (x['sec'], x['idx'])) not in proibido]
        sel_s, sel_a = ok_s[a - 1:b], ok_a[a - 1:b]
        resumo[c] = ([s['nome'] for s in sel_s], [x['nome'] for x in sel_a])
        for s in sel_s:
            if ('set', s['nome']) in usados: continue
            usados.add(('set', s['nome']))
            for p in s['pecas']: linhas.append((p, s['nome']))
        for x in sel_a:
            if ('arma', (x['sec'], x['idx'])) in usados: continue
            usados.add(('arma', (x['sec'], x['idx']))); linhas.append((x, None))
    saida[caixa] = (linhas, resumo)
if __name__ == '__main__' and '--simular' not in sys.argv and '--gravar' not in sys.argv:
    for c in CL:
        ls, la = porclasse[c]
        print(f"{c} top4 sets: {[s['nome'] for s in ls[:4]]} | top4 armas: {[x['nome'] for x in la[:4]]}")
    for caixa, (linhas, resumo) in saida.items():
        print(f"\n### Box of Kundun {caixa}: {len(linhas)} linhas de equipamento")
        for c, (s, a) in resumo.items(): print(f"  {c}: sets {s} | armas {a}" + ('   <<< SEM DROP' if not s and not a else ''))

# ---- gravação: python kundun45.py --gravar
NIVEIS = {'+4': (0, 4), '+5': (2, 6)}      # nível +N sorteado entre min e max
COPIAS_JOIAS = 5                            # linhas originais (joias/tickets) repetidas: ~1/3 das aberturas
ARQ = {'+4': r'C:\MuServer\Data\EventItemBag\010 - Box of Kundun 4.txt', '+5': r'C:\MuServer\Data\EventItemBag\011 - Box of Kundun 5.txt'}
def gerar(caixa, texto_original):
    linhas = texto_original.replace('\r\n', '\n').split('\n')
    # localiza o bloco "1" (lista de itens): cabeçalho até o comentário //Section, itens originais até "end"
    ini = next(i for i, l in enumerate(linhas) if l.strip() == '1')
    cab = next(i for i in range(ini, len(linhas)) if linhas[i].strip().startswith('//Section'))
    fim = next(i for i in range(cab, len(linhas)) if linhas[i].strip().lower() == 'end')
    originais = [l.strip() for l in linhas[cab + 1:fim] if l.strip() and not l.strip().startswith('//')]
    if any('Mu Chila' in l for l in linhas): raise SystemExit(f'{caixa}: arquivo já foi reorganizado; nada feito')
    fmt = lambda c: f"{c[0]:<12}{c[1]:<7}{c[2]:<11}{c[3]:<11}{c[4]:<8}{c[5]:<7}{c[6]:<9}{c[7]}"
    novo = [f'//Mu Chila {caixa}: joias/tickets originais repetidos {COPIAS_JOIAS}x (~1/3 das aberturas) + equipamento sem o top 4 de cada classe']
    for k in range(COPIAS_JOIAS): novo += [fmt(l.split()) for l in originais]
    lmin, lmax = NIVEIS[caixa]
    lst, resumo = saida[caixa]
    escritos = set()
    for c, (s, a) in resumo.items():
        novo.append(f"//{c}: sets {', '.join(s)} | armas {', '.join(a)}")
        for p, nome_set in lst:
            chave = (p['sec'], p['idx'])
            if chave in escritos or not (p['nome'] in a or nome_set in s): continue
            escritos.add(chave)
            novo.append(fmt([p['sec'], p['idx'], lmin, lmax, 1 if p['sec'] < 6 and p['skill'] else 0, 1, 1, 0]))
    assert len(escritos) == len(lst), (len(escritos), len(lst))
    return '\r\n'.join(linhas[:cab + 1] + novo + linhas[fim:]), len(originais) * COPIAS_JOIAS, len(lst)
if '--gravar' in sys.argv or '--simular' in sys.argv:
    import shutil, datetime
    stamp = datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
    for caixa, arq in ARQ.items():
        orig = open(arq, encoding='cp1252', newline='').read()
        novo, nj, ne = gerar(caixa, orig)
        print(f'{caixa}: {nj} linhas de joias/tickets + {ne} de equipamento = {nj + ne} linhas')
        if '--gravar' in sys.argv:
            shutil.copy2(arq, f'{arq}.bak-{stamp}')
            open(arq, 'w', encoding='cp1252', newline='').write(novo)
            print(f'  gravado; backup {arq}.bak-{stamp}')
        else:
            print('\n'.join(novo.split('\r\n')[:40]))
