from pathlib import Path
import os,shutil,json
r=Path.cwd();b=r/'AssetStaging/LevelFeedbackIntegration/BatchProject';b.mkdir(exist_ok=True)
linked=copied=0
for folder in ['Assets','Packages','ProjectSettings']:
 for p in (r/folder).rglob('*'):
  rel=p.relative_to(r);dest=b/rel
  if p.is_dir():dest.mkdir(parents=True,exist_ok=True);continue
  dest.parent.mkdir(parents=True,exist_ok=True)
  if p.suffix.lower() in ['.fbx','.png','.jpg','.jpeg','.tga','.exr','.wav','.mp3','.ogg']:
   try:os.link(p,dest);linked+=1;continue
   except OSError:pass
  shutil.copy2(p,dest);copied+=1
for p in (r/'AssetStaging').rglob('*'):
 if b in p.parents or p==b:continue
 if p.is_dir():(b/p.relative_to(r)).mkdir(parents=True,exist_ok=True)
(b/'Docs/Integration').mkdir(parents=True,exist_ok=True);(b/'Library').mkdir(exist_ok=True)
print(json.dumps(dict(batch=str(b),linked=linked,copied=copied)))
