import bpy,json,math
import numpy as np
from pathlib import Path
from mathutils import Matrix
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');s=bpy.context.scene
rig=bpy.data.objects['UDK7M_RIG'];root=bpy.data.objects['UDK7M'];meshes=[bpy.data.objects['UDK7M_'+n] for n in ['Hull','Tracks','Hoses']]
# Recreate the deterministic atlas on the identical bind meshes. No texture rebake.
rig.animation_data.action=None;rig.data.pose_position='REST'
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
s.frame_set(0)
for obj in meshes:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 obj.data.uv_layers.new(name='UV0');obj.data.uv_layers.active_index=0;obj.data.uv_layers[0].active_render=True
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.003,area_weight=0.);bpy.ops.object.mode_set(mode='OBJECT')
 assert len(obj.data.uv_layers)==1 and len(obj.data.uv_layers[0].data)==len(obj.data.loops)
 print('UV_RESTORED',obj.name,len(obj.data.uv_layers[0].data),flush=True)
report=json.loads((R/'export_manifest.json').read_text(encoding='utf-8'));cache=np.load(R/'pose_cache.npz');root_index=report['bone_names'].index('Root')
for desc in report['clips']:
 desc['trajectory']=[[-float(v[0]),float(v[2]),-float(v[1])] for v in cache[desc['id']][:,root_index,:3,3]]
(R/'export_manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
text=(R/'Tools/bake_and_export.py').read_text(encoding='utf-8');tail=text[text.index('def selection(objects):'):].replace("Exports/UDK7M_Model.fbx","ExportsFixed/UDK7M_Model.fbx").replace("R/'Exports'","R/'ExportsFixed'").replace('UDK7M_Portable_Animated.blend','UDK7M_Portable_Final.blend');(R/'ExportsFixed').mkdir(exist_ok=True);exec(compile(tail,str(R/'Tools/bake_and_export.py'),'exec'))
