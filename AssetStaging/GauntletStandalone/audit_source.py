import bpy,json,collections
from pathlib import Path
R=Path.cwd();bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(R/'Assets/Game/Player/Gauntlet/Models/Gauntlet_Model.fbx'))
r=[]
for o in bpy.data.objects:
 if o.type!='MESH':continue
 counts=collections.Counter();faces=collections.Counter()
 for v in o.data.vertices:
  if v.groups:
   g=max(v.groups,key=lambda x:x.weight);counts[o.vertex_groups[g.group].name]+=1
 for p in o.data.polygons:faces[str(p.material_index)]+=1
 r.append(dict(name=o.name,vertices=len(o.data.vertices),polygons=len(o.data.polygons),groups=dict(counts),materials=[m.name for m in o.data.materials],faces=dict(faces),bounds=[list(x) for x in o.bound_box]))
(R/'AssetStaging/GauntletStandalone/source_audit.json').write_text(json.dumps(r,indent=2),encoding='utf-8');print(json.dumps(r,indent=2))
