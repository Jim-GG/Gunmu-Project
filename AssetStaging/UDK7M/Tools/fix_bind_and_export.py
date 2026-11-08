import bpy,json,numpy as np
from pathlib import Path
from mathutils import Matrix
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');s=bpy.context.scene;rig=bpy.data.objects['UDK7M_RIG'];root=bpy.data.objects['UDK7M'];meshes=[bpy.data.objects['UDK7M_'+n] for n in ['Hull','Tracks','Hoses']]
rest=json.loads((R/'correct_rest.json').read_text(encoding='utf-8'));report=json.loads((R/'export_manifest.json').read_text(encoding='utf-8'));cache=np.load(R/'pose_cache.npz');bone_names=report['bone_names'];arm=rig.data
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for name in bone_names:
 b=arm.edit_bones[name];b.length=.12;b.matrix=Matrix(rest[name])
bpy.ops.object.mode_set(mode='OBJECT')
assert max(max(abs(arm.bones[n].matrix_local[i][j]-rest[n][i][j]) for i in range(4) for j in range(4)) for n in bone_names)<.0001
for desc in report['clips']:
 action=bpy.data.actions[desc['baked_action']];slot=action.slots[0];bag=action.layers[0].strips[0].channelbag(slot)
 for c in list(bag.fcurves):bag.fcurves.remove(c)
 values=cache[desc['id']];times=np.arange(len(values),dtype=np.float32)
 for i,name in enumerate(bone_names):
  parent=arm.bones[name].parent;pi=bone_names.index(parent.name) if parent else -1;restmat=arm.bones[name].matrix_local;locs=[];rots=[];scales=[];previous=None
  for fv in values:
   world=Matrix(fv[i].tolist());basis=restmat.inverted()@world if parent is None else restmat.inverted()@parent.matrix_local@Matrix(fv[pi].tolist()).inverted()@world
   loc,quat,scale=basis.decompose()
   if previous is not None and previous.dot(quat)<0:quat.negate()
   previous=quat.copy();locs.append(list(loc));rots.append(list(quat));scales.append(list(scale))
  for prop,data in [('location',locs),('rotation_quaternion',rots),('scale',scales)]:
   array=np.array(data,np.float32)
   for channel in range(array.shape[1]):
    v=array[:,channel];indices=[0,len(v)-1] if np.max(np.abs(v-v[0]))<1e-6 and len(v)>1 else list(range(len(v)));c=bag.fcurves.new(data_path=f'pose.bones["{name}"].{prop}',index=channel);c.keyframe_points.add(len(indices));c.keyframe_points.foreach_set('co',np.stack([times[indices],v[indices]],axis=1).ravel())
    for k in c.keyframe_points:k.interpolation='LINEAR'
    c.update()
 rig.animation_data.action=action;rig.animation_data.action_slot=slot
 for frame in [0,(len(values)-1)//2,len(values)-1]:
  s.frame_set(frame);bpy.context.view_layer.update();e=rig.evaluated_get(bpy.context.evaluated_depsgraph_get());error=max(float(np.max(np.abs(np.array(e.pose.bones[n].matrix)-values[frame,i]))) for i,n in enumerate(bone_names));assert error<.0001,(desc['id'],frame,error)
 print('FULL_MATRIX_VALIDATED',desc['id'],flush=True)
idle=next(d for d in report['clips'] if d['id']=='Idle');rig.animation_data.action=bpy.data.actions[idle['baked_action']];rig.animation_data.action_slot=rig.animation_data.action.slots[0];s.frame_set(0);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();points=json.loads((R/'source_skin_points.json').read_text());errors={}
for obj in meshes:
 e=obj.evaluated_get(dg);m=e.to_mesh();v=np.array([e.matrix_world@p.co for p in m.vertices]);q=np.array(points[obj.name]);err=max(float(np.min(np.linalg.norm(v-p,axis=1))) for p in q);assert err<.002,(obj.name,err);errors[obj.name]=err;e.to_mesh_clear()
report['skin_validation']=errors;report['binding_orientation_corrected']=True;(R/'export_manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('SKIN_VERTICES_VALIDATED',errors,flush=True)
text=(R/'Tools/bake_and_export.py').read_text(encoding='utf-8');tail=text[text.index('def selection(objects):'):].replace('Exports/UDK7M_Model.fbx','ExportsCorrected/UDK7M_Model.fbx').replace("R/'Exports'","R/'ExportsCorrected'").replace('UDK7M_Portable_Animated.blend','UDK7M_Portable_Corrected.blend');(R/'ExportsCorrected').mkdir(exist_ok=True);exec(compile(tail,'corrected_export','exec'))
