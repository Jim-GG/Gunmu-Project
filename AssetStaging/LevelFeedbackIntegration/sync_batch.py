from pathlib import Path
import shutil,re
r=Path.cwd();b=r/'AssetStaging/LevelFeedbackIntegration/BatchProject'
for folder in ['Assets','Packages','ProjectSettings']:
 for p in (r/folder).rglob('*'):
  if not p.is_file() or p.suffix.lower() in ['.fbx','.png','.jpg','.jpeg','.tga','.exr','.wav','.mp3','.ogg']:continue
  dest=b/p.relative_to(r);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
p=b/'ProjectSettings/ProjectSettings.asset';s=p.read_text(encoding='utf-8-sig');s=re.sub(r'  runInBackground: \d','  runInBackground: 1',s);p.write_text(s,encoding='utf-8')
print('Updated isolated snapshot')
