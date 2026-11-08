import json,shutil
from pathlib import Path
ROOT=Path('D:/UNITY工程/棍木项目/棍木项目');STAGE=ROOT/'AssetStaging/FacilityProps/V02'
cfg=json.loads((STAGE/'manifest.json').read_text(encoding='utf-8'))
def family(name):return 'SovietFacility' if any(k in name for k in ['Soviet','Black_Iron','FP_SV_']) else 'ModernLab'
for name,p in cfg['materials'].items():
 if 'source_material' in p:
  p['asset_path']=p['source_material'].replace('\\','/');continue
 target=ROOT/'Assets/Game/Environment'/family(name)
 p['asset_path']=str((target/'Materials/Props'/(name+'.mat')).relative_to(ROOT)).replace('\\','/')
 for key,path in list(p.get('textures',{}).items()):
  if path.replace('\\','/').startswith('Assets/'):continue
  source=ROOT/path;dest=target/'Textures/Props'/source.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest);p['textures'][key]=str(dest.relative_to(ROOT)).replace('\\','/')
 if p.get('texture'):
  source=ROOT/p['texture'];dest=target/'Textures/Props'/source.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest);p['texture']=str(dest.relative_to(ROOT)).replace('\\','/')
for model in cfg['models']:
 target=ROOT/'Assets/Game/Environment'/model['family'];source=STAGE/'Exports'/(model['name']+'.fbx');dest=target/'Models/Props'/source.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,dest)
 model['model_path']=str(dest.relative_to(ROOT)).replace('\\','/');model['prefab_path']=str((target/'Prefabs/Props'/(model['name']+'.prefab')).relative_to(ROOT)).replace('\\','/')
cfg['status']='APPROVED_FOR_UNITY_IMPORT'
(STAGE/'unity_import.json').write_text(json.dumps(cfg,ensure_ascii=False,indent=2),encoding='utf-8')
print('APPROVED_PROPS_COPIED',len(cfg['models']),len(cfg['materials']))
