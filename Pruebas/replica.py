# Réplica en Python de la lógica de decisión de Candil (misma normalización, BM25, Naive Bayes y FAQ)
# para validar umbrales sin Unity. No llama a ningún modelo.
import json, math, sys, unicodedata
D=sys.argv[1]
STOP=set("el la los las un una unos unas de del al a en y o que se me mi mis te tu tus es son por para con lo le su sus ya si no hay muy mas pero como cual cuales cuando donde este esta esto eso esa ese yo vos sos soy ser puedo puede quiero tengo tiene hace hacer sirve".split())
def norm(s):
    s=s.lower(); out=[]
    for c in s:
        c={'á':'a','é':'e','í':'i','ó':'o','ú':'u','ü':'u','ñ':'n'}.get(c,c)
        out.append(c if c.isalnum() else ' ')
    return ''.join(out)
def tok(s):
    r=[]
    for p in norm(s).split():
        if len(p)<2 or p in STOP: continue
        if len(p)>5 and p.endswith('es'): p=p[:-2]
        elif len(p)>3 and p.endswith('s'): p=p[:-1]
        r.append(p)
    return r
docs=json.load(open(D+'/conocimiento.json'))['documentos']
faqs=json.load(open(D+'/faq.json'))['faqs']
exs=json.load(open(D+'/intenciones.json'))['ejemplos']
tfs=[];lens=[];df={}
for d in docs:
    t=tok(d['titulo']+' '+d['titulo']+' '+d['texto']); tf={}
    for x in t: tf[x]=tf.get(x,0)+1
    for x in tf: df[x]=df.get(x,0)+1
    tfs.append(tf);lens.append(len(t))
avg=sum(lens)/len(lens)
def search(q,k=3,boost=None,bf=1.25):
    hits=[]
    for i,d in enumerate(docs):
        s=0
        for term in tok(q):
            tf=tfs[i].get(term)
            if not tf: continue
            idf=math.log(1+(len(docs)-df[term]+0.5)/(df[term]+0.5))
            s+=idf*(tf*2.5)/(tf+1.5*(1-0.75+0.75*lens[i]/avg))
        if s>0 and boost and d['categoria']==boost: s*=bf
        if s>0: hits.append((s,d['id']))
    hits.sort(reverse=True); return hits[:k]
cd={};ct={};tt={};voc=set()
for e in exs:
    c=e['intencion']; cd[c]=cd.get(c,0)+1; ct.setdefault(c,{}); tt.setdefault(c,0)
    for x in tok(e['texto']): ct[c][x]=ct[c].get(x,0)+1; tt[c]+=1; voc.add(x)
def classify(q):
    t=tok(q)
    if not any(x in voc for x in t): return 'desconocida',0.0
    ls={}
    for c in cd:
        lp=math.log(cd[c]/len(exs))
        for x in t:
            if x in voc: lp+=math.log((ct[c].get(x,0)+1)/(tt[c]+len(voc)))
        ls[c]=lp
    m=max(ls.values()); best=max(ls,key=ls.get)
    return best,1/sum(math.exp(v-m) for v in ls.values())
def faq(q):
    qs=set(tok(q)); best=(0,None)
    for f in faqs:
        for v in f['variantes']:
            vs=set(tok(v))
            if not vs or not qs: continue
            j=len(qs&vs)/len(qs|vs)
            if j>best[0]: best=(j,f['id'])
    return best
def route(q,FAQ_T=0.6,RET_T=2.7,INT_T=0.4):
    j,fid=faq(q)
    if fid and j>=FAQ_T: return 'FAQ',fid,j,'faq'
    it,conf=classify(q); tr=conf>=INT_T
    if tr and it=='saludo': return 'SALUDO','',0,it
    if tr and it=='reporte_bug': return 'TICKET_BUG','',0,it
    boost=it if tr and it in('jugabilidad','tecnico','cuenta_compras') else None
    h=search(q,3,boost); top=h[0][0] if h else 0
    oos=tr and it=='fuera_de_alcance'
    if top<RET_T or (oos and top<RET_T*2): return 'ESCALADO',h[0][1] if h else '',top,it
    return 'RAG_LLM',h[0][1],top,it
if __name__=='__main__':
    tests=json.load(open(sys.argv[2]))
    ok=0
    for t in tests:
        r,doc,sc,it=route(t['q'])
        good = r==t['ruta'] and (t.get('doc') in (None,doc))
        ok+=good
        print(('OK  ' if good else 'MAL ')+f"{t['q'][:52]:52} -> {r:11} {doc:6} {sc:5.2f} {it:16} (esperado {t['ruta']} {t.get('doc','')})")
    print(f"\nAciertos de ruta y documento: {ok}/{len(tests)} = {100*ok/len(tests):.0f}%")
    import statistics
    print('tokens aprox KB completa:', sum(len(d['id'])+len(d['titulo'])+len(d['texto'])+6 for d in docs)//4, '| docs:',len(docs),'| promedio por doc:', statistics.mean((len(d['id'])+len(d['titulo'])+len(d['texto'])+6)//4 for d in docs))
