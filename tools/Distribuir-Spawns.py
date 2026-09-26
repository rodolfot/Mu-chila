# Redistribui os spawns de monstros dos mapas de caça a partir dos arquivos originais do kit:
#  - grupos pequenos (GRUPO monstros numa caixa 5x5) espalhados por igual na área original de cada monstro
#    (centros por k-means sobre o chão livre, que ocupam o meio da área em vez das bordas);
#  - pontos de farm: caixas 7x7 com FARM monstros do tipo mais comum de cada região, longe da cidade.
# O total de monstros de cada mapa não muda (o farm sai da cota do próprio monstro), exceto os mapas em EXTRA.
# Só conta o chão alcançável andando a partir da cidade ou de um ponto de chegada de portal (Gate.txt).
# Atenção: rodar sem --mapas refaz os 43 mapas; as regras de portal/alcance (26/09) mudam um pouco a posição dos grupos.
# Ficam como no kit: chefes/raros (1-2 no mapa ou respawn >= 10 min), armadilhas (MoveRange 0), Crywolf e Refúgio de Balgass.
# Uso: python tools\Distribuir-Spawns.py              -> simula e mostra a cobertura antes/depois
#      python tools\Distribuir-Spawns.py --png <pasta> -> também desenha os mapas (cinza chão, verde cidade, amarelo farm, vermelho grupos)
#      python tools\Distribuir-Spawns.py --gravar      -> grava em C:\MuServer (backup .bak-*); depois Reload Monster no GameServer
#      --mapas 57,110 limita a esses mapas (os outros ficam como estão)
import os, re, sys, glob, shutil, datetime, collections
import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(AQUI, '..', '1 - MuServer Season 14 - Aprendiz Mu Online', 'Data', 'Monster', 'MonsterSetBase')
SERVIDOR = r'C:\MuServer\Data'
DESTINO = os.path.join(SERVIDOR, 'Monster', 'MonsterSetBase')
GRUPO, FARM = 3, 12                 # monstros por grupo e por ponto de farm (--grupo N muda o tamanho do grupo)
MEIA_GRUPO, MEIA_FARM = 2, 3        # caixas 5x5 e 7x7
MIN_LIVRE_GRUPO, MIN_LIVRE_FARM = 13, 40
FARM_POR_MONSTROS = 100             # 1 ponto de farm a cada 100 monstros agrupados no mapa (1 a 5; nenhum abaixo de 70)
DIST_FARM, DIST_CIDADE = 40, 18     # distância mínima entre farms e da zona segura (farm não fica na saída da cidade)
DIST_CIDADE_GRUPO = 3
ALIAS_TERRENO = {25: 24, 26: 24, 27: 24, 28: 24, 29: 24, 124: 123, 125: 123, 126: 123, 127: 123}
PULAR = {34, 42}                    # Crywolf e Refúgio de Balgass: spawns ligados a eventos
RAIO_COBERTURA = 10
DIST_PORTAL = 6                     # nenhum grupo ou farm a menos de 6 tiles de um portal (chegada ou saída)
# Ajustes por mapa (26/09): a área de cada monstro cresce N tiles em volta dos spawns do kit; MAPA_TODO = o mapa inteiro
MAPA_TODO = -1
AMPLIAR = {57: 10, 110: MAPA_TODO}  # Raklion: kit com pontos soltos; Nars: kit só nas bordas (3 monstros do mesmo nível)
# Monstros a mais (ou a menos) por mapa, divididos entre os monstros do mapa. A soma tem que ficar em 0: o GameServer só usa
# os índices de objeto 0-9169 para monstros (spawns + invasões); com +100 líquidos o último mapa (127) perdeu 66 monstros
# e a invasão Golden (66) deixa de caber.
# 26/09: Raklion/Nars ganharam área; depois, pelos registros de caçada (Hunting Record no log do GS), cerca de 13% dos monstros
# de mapas sem ninguém caçando (Deep Dungeon 1-5, Swamp of Darkness, Kalima 1-7) foram para os mapas onde o pessoal caça.
EXTRA = {
    57: 80, 110: 60, 37: 40, 8: 35, 80: 35, 123: 30, 7: 15,          # Raklion, Nars, Kanturu 1, Tarkan, Karutan 1, Kubera 1, Atlans
    116: -44, 117: -38, 118: -42, 119: -35, 120: -40, 122: -40,       # Deep Dungeon 1-5, Swamp of Darkness
    24: -8, 25: -8, 26: -8, 27: -8, 28: -8, 29: -8, 36: -8,           # Kalima 1-7
}
assert sum(EXTRA.values()) == 0

def info_monstros():
    info = {}
    for l in open(os.path.join(SERVIDOR, 'Monster', 'Monster.txt'), encoding='cp1252'):
        m = re.match(r'^\s*(\d+)\s+\d+\s+"([^"]*)"\s+(.*)$', l)
        if m:
            v = m.group(3).split()
            info[int(m.group(1))] = {'nome': m.group(2), 'nivel': int(v[0]), 'move': int(v[9]), 'regen': int(v[15])}
    return info

def terreno(mapa):
    f = os.path.join(SERVIDOR, 'Terrain', f'Terrain{ALIAS_TERRENO.get(mapa, mapa) + 1}.att')
    if not os.path.exists(f): return None
    b = open(f, 'rb').read()
    return np.frombuffer(b[3:], dtype=np.uint8).reshape(256, 256) if len(b) == 65539 else None   # [y][x]

def portais(mapa, so_chegada=False):
    # Gate.txt: Index Flag Map X1 Y1 X2 Y2 TargetGate ...; TargetGate 0 = ponto de chegada (quem usa o portal/move aparece ali)
    m = np.zeros((256, 256), bool)
    for l in open(os.path.join(SERVIDOR, 'Move', 'Gate.txt'), encoding='cp1252'):
        v = l.split()
        if len(v) >= 8 and v[0].isdigit() and v[2] == str(mapa) and (not so_chegada or v[7] == '0'):
            x1, x2 = sorted((int(v[3]), int(v[5]))); y1, y2 = sorted((int(v[4]), int(v[6])))
            m[y1:y2 + 1, x1:x2 + 1] = True
    return m

def configs(xml):
    out = []
    for sec in ('SPOT', 'MONSTER'):
        m = re.search(rf'(?s)<{sec}>(.*?)</{sec}>', xml)
        if not m: continue
        for c in re.finditer(r'<Config ([^>]*)/>', m.group(1)):
            a = {k: int(v) for k, v in re.findall(r'(\w+)="(-?\d+)"', c.group(1))}
            if 'BeginPosX' in a: x1, y1, x2, y2, q = a['BeginPosX'], a['BeginPosY'], a['EndPosX'], a['EndPosY'], a.get('Quantity', 1)
            elif 'PositionX' in a: x1, y1, x2, y2, q = a['PositionX'], a['PositionY'], a['PositionX'], a['PositionY'], 1
            else: continue
            x1, x2 = sorted((x1, x2)); y1, y2 = sorted((y1, y2))
            if x1 == x2 and y1 == y2: x1, y1, x2, y2 = x1 - 3, y1 - 3, x2 + 3, y2 + 3
            out.append({'sec': sec, 'cls': a['Class'], 'rect': (max(0, x1), max(0, y1), min(255, x2), min(255, y2)), 'q': q, 'elem': a.get('Element', 0), 'raw': c.group(0)})
    return out

def soma_caixa(mask, meia):
    # quantos tiles True existem na caixa (2*meia+1)^2 centrada em cada tile
    s = np.pad(mask.astype(np.int32), meia).cumsum(0).cumsum(1)
    s = np.pad(s, ((1, 0), (1, 0)))
    n = 2 * meia + 1
    return s[n:, n:] - s[:-n, n:] - s[n:, :-n] + s[:-n, :-n]

def dilatar(mask, r):
    out = mask.copy()
    for _ in range(r):
        p = np.pad(out, 1)
        out = (p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
               | p[:-2, :-2] | p[:-2, 2:] | p[2:, :-2] | p[2:, 2:])      # 8 vizinhos: quadrado de raio r
    return out

def alcancavel(andavel, sementes):
    # chão ligado (andando) a algum ponto de chegada de portal ou à cidade; o resto ninguém alcança
    vis = np.zeros_like(andavel); ok = np.zeros_like(andavel)
    for y0, x0 in zip(*np.nonzero(andavel)):
        if vis[y0, x0]: continue
        pilha = [(y0, x0)]; vis[y0, x0] = True; comp = []
        while pilha:
            y, x = pilha.pop(); comp.append((y, x))
            for yy, xx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= yy < 256 and 0 <= xx < 256 and andavel[yy, xx] and not vis[yy, xx]:
                    vis[yy, xx] = True; pilha.append((yy, xx))
        ys, xs = zip(*comp)
        if sementes[ys, xs].any(): ok[ys, xs] = True
    return ok

def kmeans(pts, k, semente, fixos=None):
    # k-means com centros "fixos" (grupos já colocados por outros monstros): os novos vão para onde ainda não há ninguém
    rnd = np.random.default_rng(semente)
    fixos = np.zeros((0, 2)) if fixos is None or not len(fixos) else np.asarray(fixos, float)
    d = ((pts[:, None, :] - fixos[None, :, :]) ** 2).sum(2).min(1).astype(float) if len(fixos) else None
    c = []
    if d is None:
        c.append(pts[rnd.integers(len(pts))]); d = ((pts - c[0]) ** 2).sum(1).astype(float)
    while len(c) < k:                                       # k-means++
        c.append(pts[rnd.choice(len(pts), p=d / d.sum()) if d.sum() > 0 else rnd.integers(len(pts))])
        d = np.minimum(d, ((pts - c[-1]) ** 2).sum(1))
    c = np.array(c, float); nf = len(fixos)
    for _ in range(40):
        todos = np.vstack([fixos, c]) if nf else c
        lab = ((pts[:, None, :] - todos[None, :, :]) ** 2).sum(2).argmin(1) - nf
        novo = np.array([pts[lab == j].mean(0) if (lab == j).any() else c[j] for j in range(k)])
        if np.allclose(novo, c): break
        c = novo
    return c

def cobertura(centros, valido):
    yy, xx = np.mgrid[0:256, 0:256]; perto = np.zeros((256, 256), bool); R = RAIO_COBERTURA
    for x, y in centros:
        x0, x1, y0, y1 = max(0, x - R), min(256, x + R + 1), max(0, y - R), min(256, y + R + 1)
        perto[y0:y1, x0:x1] |= (xx[y0:y1, x0:x1] - x) ** 2 + (yy[y0:y1, x0:x1] - y) ** 2 <= R * R
    return 100.0 * (perto & valido).sum() / max(1, valido.sum())

def planejar(mapa, xml_kit, xml_atual, info):
    att = terreno(mapa)
    if att is None: return None
    livre_monstro = (att & 0x0D) == 0
    cidade = (att & 0x01) != 0
    alcance = alcancavel((att & 0x0C) == 0, dilatar(portais(mapa, so_chegada=True), 2) | cidade)
    valido = livre_monstro & alcance
    caixa_g, caixa_f = soma_caixa(valido, MEIA_GRUPO), soma_caixa(valido, MEIA_FARM)
    perto_cidade, porta_cidade = dilatar(cidade, DIST_CIDADE), dilatar(cidade, DIST_CIDADE_GRUPO)
    perto_portal = dilatar(portais(mapa), DIST_PORTAL)
    ampliar = AMPLIAR.get(mapa, 0)
    por_cls = collections.defaultdict(list)
    for c in configs(xml_kit): por_cls[c['cls']].append(c)
    mover, intocados = {}, []
    area_total = np.zeros((256, 256), bool)
    for cls, lst in por_cls.items():
        n = sum(c['q'] for c in lst); mi = info.get(cls)
        if n <= 2 or not mi or mi['move'] == 0 or mi['regen'] >= 600:
            intocados.append(mi['nome'] if mi else f'#{cls}'); continue
        reg = np.zeros((256, 256), bool)
        for c in lst:
            x1, y1, x2, y2 = c['rect']; a = max(0, ampliar)
            reg[max(0, y1 - a):y2 + a + 1, max(0, x1 - a):x2 + a + 1] = True
        if ampliar == MAPA_TODO: reg[:] = True
        reg &= valido
        if (reg & (caixa_g >= MIN_LIVRE_GRUPO)).sum() == 0:
            intocados.append(f"{mi['nome']} (sem chão)"); continue
        mover[cls] = {'n': n, 'reg': reg, 'elem': lst[0]['elem'], 'nome': mi['nome'], 'nivel': mi['nivel'], 'raws': [c['raw'] for c in lst]}
        area_total |= reg
    if not mover: return None
    kit_total = sum(m['n'] for m in mover.values())
    extra = EXTRA.get(mapa, 0)
    for j, cls in enumerate(sorted(mover, key=lambda c: -mover[c]['n'])):      # monstros a mais, proporcionais ao kit
        mover[cls]['n'] += extra * mover[cls]['n'] // kit_total
    sobra = kit_total + extra - sum(m['n'] for m in mover.values())
    for cls in sorted(mover, key=lambda c: -mover[c]['n'])[:sobra]: mover[cls]['n'] += 1
    # ---- pontos de farm: os monstros mais numerosos, no ponto mais central da área de cada um
    total = sum(m['n'] for m in mover.values())
    nfarm = 0 if total < 70 else max(1, min(5, round(total / FARM_POR_MONSTROS)))   # mapa pequeno: o farm comeria boa parte dos monstros
    farms, usados = [], collections.Counter()
    fila = sorted(mover, key=lambda c: -mover[c]['n'])
    for rodada in range(2):
        for cls in fila:
            if len(farms) >= nfarm: break
            m = mover[cls]
            if m['n'] - FARM * (usados[cls] + 1) < GRUPO or usados[cls] > rodada: continue
            cand = m['reg'] & (caixa_f >= MIN_LIVRE_FARM) & ~perto_cidade & ~perto_portal
            ys, xs = np.nonzero(cand)
            if not len(xs): continue
            pts = np.stack([xs, ys], 1)
            longe = np.ones(len(pts), bool)
            for fx, fy, _ in farms: longe &= ((pts - (fx, fy)) ** 2).sum(1) >= DIST_FARM ** 2
            if not longe.any(): continue
            ry, rx = np.nonzero(m['reg']); centro = np.array([rx.mean(), ry.mean()])
            p = pts[longe][((pts[longe] - centro) ** 2).sum(1).argmin()]
            farms.append((int(p[0]), int(p[1]), cls)); usados[cls] += 1
    # ---- grupos espalhados
    bloqueio_farm = np.zeros((256, 256), bool)
    for fx, fy, _ in farms: bloqueio_farm[max(0, fy - 6):fy + 7, max(0, fx - 6):fx + 7] = True
    grupos = []
    for cls, m in sorted(mover.items(), key=lambda kv: (-kv[1]['reg'].sum(), kv[0])):
        resto = m['n'] - FARM * usados[cls]
        k = max(1, round(resto / GRUPO))
        ys, xs = np.nonzero(m['reg'])
        pts = np.stack([xs, ys], 1)
        k = min(k, len(pts))
        fixos = [(x, y) for x, y, *_ in grupos] + [(x, y) for x, y, _ in farms]
        centros = kmeans(pts, k, 20260926 + mapa * 1000 + cls, fixos)
        bons = m['reg'] & (caixa_g >= MIN_LIVRE_GRUPO) & ~bloqueio_farm & ~porta_cidade & ~perto_portal
        if not bons.any(): bons = m['reg'] & (caixa_g >= MIN_LIVRE_GRUPO)
        by, bx = np.nonzero(bons); bpts = np.stack([bx, by], 1); ocupado = set()
        for j, c in enumerate(centros):
            ordem = ((bpts - c) ** 2).sum(1).argsort()
            p = next((bpts[i] for i in ordem[:200] if (bpts[i][0], bpts[i][1]) not in ocupado), bpts[ordem[0]])
            ocupado.add((p[0], p[1]))
            q = resto // k + (1 if j < resto % k else 0)
            grupos.append((int(p[0]), int(p[1]), cls, q))
    antes = [((c['rect'][0] + c['rect'][2]) // 2, (c['rect'][1] + c['rect'][3]) // 2) for c in configs(xml_atual) if c['cls'] in mover]
    depois = [(x, y) for x, y, _, _ in grupos] + [(x, y) for x, y, _ in farms]
    return {'att': att, 'valido': valido & area_total, 'mover': mover, 'farms': farms, 'grupos': grupos, 'intocados': intocados, 'kit_total': kit_total,
            'cob_antes': cobertura(antes, valido & area_total), 'cob_depois': cobertura(depois, valido & area_total),
            'mapa_antes': cobertura(antes, valido), 'mapa_depois': cobertura(depois, valido)}

def gerar_xml(xml_kit, plano, info):
    remover = set(r for m in plano['mover'].values() for r in m['raws'])
    def limpa(mt):
        corpo = mt.group(2)
        for raw in remover: corpo = re.sub(r'(?m)^[ \t]*' + re.escape(raw) + r'[^\r\n]*\r?\n', '', corpo)
        return mt.group(1) + corpo + mt.group(3)
    xml = xml_kit
    for sec in ('SPOT', 'MONSTER'): xml = re.sub(rf'(?s)(<{sec}>)(.*?)(</{sec}>)', limpa, xml)
    linha = '\t\t<Config Class="{0}" Range="3" BeginPosX="{1}" BeginPosY="{2}" EndPosX="{3}" EndPosY="{4}" Direction="-1" Quantity="{5}" Element="{6}" /> <!-- "{7}" (Mu Chila: {8}) -->'
    novas = []
    for x, y, cls, q in plano['grupos']:
        m = plano['mover'][cls]
        novas.append(linha.format(cls, max(0, x - MEIA_GRUPO), max(0, y - MEIA_GRUPO), min(255, x + MEIA_GRUPO), min(255, y + MEIA_GRUPO), q, m['elem'], m['nome'], 'grupo'))
    for x, y, cls in plano['farms']:
        m = plano['mover'][cls]
        novas.append(linha.format(cls, max(0, x - MEIA_FARM), max(0, y - MEIA_FARM), min(255, x + MEIA_FARM), min(255, y + MEIA_FARM), FARM, m['elem'], m['nome'], 'farm'))
    if not re.search(r'</SPOT>', xml): raise SystemExit('arquivo sem seção SPOT')
    nl = '\r\n' if '\r\n' in xml else '\n'
    return re.sub(r'(?s)(<SPOT>.*?)(\r?\n)([ \t]*</SPOT>)', lambda mt: mt.group(1) + nl + nl.join(novas) + mt.group(2) + mt.group(3), xml, count=1)

def desenhar(plano, arq):
    from PIL import Image, ImageDraw
    att = plano['att']; img = np.zeros((256, 256, 3), np.uint8)
    img[(att & 0x0D) == 0] = (70, 70, 70); img[plano['valido']] = (105, 105, 105); img[(att & 0x01) != 0] = (40, 110, 40)
    S = 3; im = Image.fromarray(img).resize((768, 768), Image.NEAREST); d = ImageDraw.Draw(im)
    for x, y, cls in plano['farms']: d.rectangle([(x - MEIA_FARM) * S, (y - MEIA_FARM) * S, (x + MEIA_FARM + 1) * S, (y + MEIA_FARM + 1) * S], fill=(255, 210, 0))
    for x, y, cls, q in plano['grupos']: d.rectangle([(x - MEIA_GRUPO) * S, (y - MEIA_GRUPO) * S, (x + MEIA_GRUPO + 1) * S, (y + MEIA_GRUPO + 1) * S], fill=(230, 50, 50))
    im.save(arq)

if __name__ == '__main__':
    info = info_monstros(); gravar = '--gravar' in sys.argv
    if '--grupo' in sys.argv: GRUPO = int(sys.argv[sys.argv.index('--grupo') + 1])
    png = sys.argv[sys.argv.index('--png') + 1] if '--png' in sys.argv else None
    if png: os.makedirs(png, exist_ok=True)
    so_mapas = {int(x) for x in sys.argv[sys.argv.index('--mapas') + 1].split(',')} if '--mapas' in sys.argv else None
    stamp = datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
    tot_a = tot_d = 0
    print(f'{"mapa":<26}{"monstros":>9}{"grupos":>8}{"farms":>6}  cobertura (raio {RAIO_COBERTURA}) da área dos monstros | do mapa todo   farms')
    for arq_kit in sorted(glob.glob(os.path.join(KIT, '*.xml'))):
        nome = os.path.basename(arq_kit); mapa = int(nome[:3])
        if mapa in PULAR or (so_mapas and mapa not in so_mapas): continue
        destino = os.path.join(DESTINO, nome)
        if not os.path.exists(destino): continue
        xml_kit = open(arq_kit, encoding='utf-8').read(); xml_atual = open(destino, encoding='utf-8').read()
        plano = planejar(mapa, xml_kit, xml_atual, info)
        if not plano: continue
        n = sum(m['n'] for m in plano['mover'].values()); n2 = sum(q for *_, q in plano['grupos']) + FARM * len(plano['farms'])
        assert n == n2, (nome, n, n2)
        fs = ', '.join(f"{plano['mover'][c]['nome']} ({x},{y})" for x, y, c in plano['farms'])
        mon = f'{n}' if n == plano['kit_total'] else f"{plano['kit_total']}+{n - plano['kit_total']}"
        print(f'{nome[:-4]:<26}{mon:>9}{len(plano["grupos"]):>8}{len(plano["farms"]):>6}  {plano["cob_antes"]:5.1f}% -> {plano["cob_depois"]:5.1f}% | {plano["mapa_antes"]:5.1f}% -> {plano["mapa_depois"]:5.1f}%   {fs}')
        tot_a += plano['cob_antes'] * plano['valido'].sum(); tot_d += plano['cob_depois'] * plano['valido'].sum()
        if png: desenhar(plano, os.path.join(png, nome[:-4] + '.png'))
        if gravar:
            shutil.copy2(destino, f'{destino}.bak-{stamp}')
            with open(destino, 'w', encoding='utf-8', newline='') as f: f.write(gerar_xml(xml_kit, plano, info))
    if gravar: print(f'Gravado. Backups: *.bak-{stamp}. Aplique com Reload Monster no GameServer.')
