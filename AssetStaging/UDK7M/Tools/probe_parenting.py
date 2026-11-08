import bpy,json
import numpy as np
from pathlib import Path
from mathutils import Matrix
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');s=bpy.context.scene;rig=next(o for o in s.objects if o.type=='ARMATURE');rig.animation_data.use_nla=False
objects=[o for o in s.objects if o.type=='MESH' and o.name.startswith('MESH_')];out={}
action=bpy.data.actions['UDK7M_Idle'];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];s.frame_set(0)
for pose in ['REST','POSE']:
 rig.data.pose_position=pose;bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();er=rig.evaluated_get(dg)
 for o in objects:
  e=o.evaluated_get(dg);out.setdefault(o.name,{})[pose]={'object':list(map(list,e.matrix_world)),'bone':list(map(list,er.matrix_world@er.pose.bones[o.parent_bone].matrix)),'rest':list(map(list,er.matrix_world@rig.data.bones[o.parent_bone].matrix_local)),'owner':o.parent_bone}
for n,v in out.items():
 delta=Matrix(v['POSE']['bone'])@Matrix(v['REST']['rest']).inverted();expected=delta@Matrix(v['REST']['object']);actual=Matrix(v['POSE']['object']);v['error']=max(abs(expected[i][j]-actual[i][j]) for i in range(4) for j in range(4))
(R/'parenting_probe.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8');print([(n,v['error']) for n,v in out.items()])
