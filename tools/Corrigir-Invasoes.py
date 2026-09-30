# Corrige o bloco 2 do InvasionManager.dat: o Value de cada mapa passa a ser o Value que o mesmo mapa tem no bloco 3
# (o GameServer só põe monstros do bloco 3 com o MESMO mapa e Value do sorteio). Mexe só no número do Value, mantendo o resto da linha.
import re,sys,shutil,datetime
p=sys.argv[1]; aplicar='--aplicar' in sys.argv
raw=open(p,'rb').read(); txt=raw.decode('latin1'); lines=txt.split('\r\n')
cur=None; b3={}; b2idx=[]
for i,l in enumerate(lines):
    s=l.split('//')[0].strip()
    if not s: continue
    if re.fullmatch(r'\d+',s) and cur is None: cur=int(s); continue
    if s.lower()=='end': cur=None; continue
    c=s.split()
    if cur==2: b2idx.append(i)
    if cur==3: b3.setdefault((int(c[0]),int(c[1]),int(c[5])),set()).add(int(c[2]))
mud=0
for i in b2idx:
    l=lines[i]; m=re.match(r'^(\s*(\d+)\s+(\d+)\s+(\d+)\s+)(\d+)(.*)$',l)
    idx,g,mp,v=int(m.group(2)),int(m.group(3)),int(m.group(4)),int(m.group(5))
    vals=b3.get((idx,g,mp),set())
    if v in vals or len(vals)!=1: continue
    nv=next(iter(vals)); lines[i]=m.group(1)+str(nv)+m.group(6); mud+=1
    print(f"  índice {idx} grupo {g} mapa {mp}: Value {v} -> {nv}")
print(f"{mud} linha(s) do bloco 2 {'corrigidas' if aplicar else 'a corrigir'}")
if aplicar and mud:
    shutil.copy2(p, p+'.bak-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    open(p,'wb').write('\r\n'.join(lines).encode('latin1'))
