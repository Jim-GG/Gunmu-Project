import pathlib,subprocess,re,collections,json
r=pathlib.Path.cwd();paths=[pathlib.Path(p) for p in subprocess.check_output(['rg','--files','Assets','-g','*.meta'],encoding='utf-8').splitlines()];groups=collections.defaultdict(list)
for p in paths:
 s=p.read_text(encoding='utf-8-sig',errors='replace');m=re.search(r'^guid: (\w+)',s,re.M)
 if m:groups[m[1]].append(p.as_posix()[:-5])
dups={g:v for g,v in groups.items() if len(v)>1};orphans=[p.as_posix() for p in paths if not pathlib.Path(str(p)[:-5]).exists()]
d=dict(assetGuidCount=len(groups),duplicateGuids=dups,orphanMetas=orphans)
pathlib.Path('Docs/Integration/static-audit.json').write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(d,ensure_ascii=False))
