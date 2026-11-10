import bpy,json,collections
from pathlib import Path
src=Path('F:/3D Assets/模型资产/sketchfab/hand-with-gloves/source/Parry.blend')
bpy.ops.wm.open_mainfile(filepath=str(src));r=[]
for o in bpy.data.objects:
 if o.type in ('MESH','ARMATURE'):
  r.append(dict(name=o.name,type=o.type,vertices=len(o.data.vertices) if o.type=='MESH' else 0,materials=[m.name if m else '' for m in o.data.materials] if o.type=='MESH' else [],hidden=o.hide_viewport or o.hide_render))
Path('AssetStaging/GauntletStandalone/original_blend_audit.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(r,ensure_ascii=True,indent=2))
