import bpy,json
from pathlib import Path
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');report=json.loads((R/'export_manifest.json').read_text(encoding='utf-8'));s=bpy.context.scene;rig=bpy.data.objects['UDK7M_RIG'];rig.data.pose_position='POSE'
for desc in report['clips']:
 rig.animation_data.action=bpy.data.actions[desc['baked_action']];rig.animation_data.action_slot=rig.animation_data.action.slots[0]
 for sample in desc['benchmarks']:
  s.frame_set(sample['frame']);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();skin={}
  for o in s.objects:
   if o.type!='MESH':continue
   e=o.evaluated_get(dg);m=e.to_mesh();points=[e.matrix_world@v.co for v in list(m.vertices)[::200]];skin[o.name]=[[-p.x,p.z,-p.y] for p in points];e.to_mesh_clear()
  sample['skin_positions']=skin
(R/'export_manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('ALL_SKIN_BENCHMARKS_CAPTURED')
