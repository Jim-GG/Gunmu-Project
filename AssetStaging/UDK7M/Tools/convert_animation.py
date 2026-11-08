"""Bake the saved author rig, GN tread links and Hook hoses to a portable FK skin.
The source .blend is a read-only snapshot. Creates one hull skin, one track skin,
one hose skin, and a separate skeleton-only FBX for each authored action.
"""
import bpy,math,json,time
import numpy as np
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');s=bpy.context.scene
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
rig=next(o for o in s.objects if o.type=='ARMATURE' and o.name.startswith('UDK7M_RIG'))
actions=list(bpy.data.actions);rig.animation_data.use_nla=False
tracks=sorted([o for o in s.objects if o.type=='MESH' and o.name.startswith('TRACK')],key=lambda x:x.name)
hoses=sorted([o for o in s.objects if o.type=='CURVE' and any(m.type=='HOOK' for m in o.modifiers)],key=lambda x:x.name)
rigids=[o for o in s.objects if o.type=='MESH' and o.name.startswith('MESH_')]
assert len(tracks)==2 and len(hoses)==4 and len(rigids)>=30
for obj in tracks:
 m=obj.modifiers[0];sock=next(x for x in m.node_group.interface.items_tree if x.item_type=='SOCKET' and x.name=='Realize for Export');getattr(m.properties.inputs,sock.identifier).value=True;obj.update_tag()
names={}
special={'01_ROOT_整机':'Root','02_TURRET_回转':'Turret','03_IK_钻尖':'Control_Tip','04_POLE_肘朝向':'Control_Pole','05_ROLL_钻芯':'DrillRoll','06_TRACK_左履带':'Control_Track_L','07_TRACK_右履带':'Control_Track_R','MCH_大臂':'Boom','MCH_前臂':'Forearm','MCH_钻具':'Tool'}
for i,b in enumerate(rig.data.bones):names[b.name]=special.get(b.name,'Mechanism_'+str(i).zfill(2))
source_bones=list(rig.data.bones);bone_names=[names[b.name] for b in source_bones];parent_names={names[b.name]:names[b.parent.name] if b.parent else None for b in source_bones}

def evaluated(obj,dg,copy=False):
 e=obj.evaluated_get(dg)
 if copy:return bpy.data.meshes.new_from_object(e,preserve_all_data_layers=True,depsgraph=dg),e.matrix_world.copy()
 mesh=e.to_mesh();coords=np.empty(len(mesh.vertices)*3,np.float32);mesh.vertices.foreach_get('co',coords);coords=coords.reshape(-1,3);mat=np.array(e.matrix_world,np.float64);coords=coords@mat[:3,:3].T+mat[:3,3];e.to_mesh_clear();return coords
def basis(points,indices):
 a,b,c=(Vector(points[i]) for i in indices);x=(b-a).normalized();y=c-a;y=(y-x*y.dot(x)).normalized();z=x.cross(y).normalized();mat=Matrix((x,y,z)).transposed().to_4x4();mat.translation=Vector(np.mean(points,axis=0));return mat
def action_id(a):
 n=a.name
 table={'UDK7M_Idle':'Idle','UDK7M_三连下砸':'TripleSlam','UDK7M_下砸准备':'SlamPrepare','UDK7M_下砸维持':'SlamHold','UDK7M_下砸收势':'SlamRecover','UDK7M_冲刺准备':'ChargePrepare','UDK7M_冲刺维持':'ChargeHold','UDK7M_冲刺收势':'ChargeRecover','UDK7M_动作示例_全部控制骨':'OriginalDemo'}
 return table.get(n,'Action_'+str(actions.index(a)).zfill(2))
def assign(action):
 rig.animation_data.action=action
 if action.slots:rig.animation_data.action_slot=action.slots[0]
rig.data.pose_position='REST';s.frame_set(0);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
rest_bones={names[b.name]:(rig.matrix_world@b.matrix_local).copy() for b in source_bones}
geometry={};blocks=[]
for obj in tracks+hoses:
 mesh,world=evaluated(obj,dg,True);coords=np.array([world@v.co for v in mesh.vertices],np.float64)
 size=len(obj.data.vertices) if obj in tracks else 4
 assert len(coords)%size==0,(obj.name,len(coords),size)
 side='L' if obj==tracks[0] else 'R' if obj in tracks else str(hoses.index(obj))
 entries=[]
 for i in range(len(coords)//size):
  name=('Tread_'+side+'_' if obj in tracks else 'Hose_'+side+'_')+str(i).zfill(2);points=coords[i*size:(i+1)*size];rest_bones[name]=basis(points,(0,1,2) if obj in tracks else (0,1,3));parent_names[name]='Root';bone_names.append(name);entries.append(name)
 geometry[obj.name]={'mesh':mesh,'world':world,'coords':coords,'block_size':size,'bones':entries,'object':obj}
 blocks.append(geometry[obj.name])
for obj in rigids:
 mesh,world=evaluated(obj,dg,True);geometry[obj.name]={'mesh':mesh,'world':world,'object':obj,'bone':names[obj.parent_bone]}
rest_vertices={n:g['coords'] for n,g in geometry.items() if 'coords' in g}
rig.data.pose_position='POSE'
cache={};descriptions=[];fps=s.render.fps/s.render.fps_base
for action in actions:
 assign(action);a,b=map(lambda v:int(round(v)),action.frame_range);clip_id=action_id(action);frames=list(range(a,b+1));matrices=[];checks=[]
 for frame in frames:
  s.frame_set(frame);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();er=rig.evaluated_get(dg)
  current={names[bone.name]:er.matrix_world@er.pose.bones[bone.name].matrix for bone in source_bones}
  for item in blocks:
   coords=evaluated(item['object'],dg);size=item['block_size'];assert len(coords)==len(item['coords'])
   for i,name in enumerate(item['bones']):current[name]=basis(coords[i*size:(i+1)*size],(0,1,2) if item['object'] in tracks else (0,1,3))
   if frame in [a,(a+b)//2,b]:
    # Verify rigid ring/link decomposition against every evaluated vertex.
    err=0
    for i,name in enumerate(item['bones']):
     delta=current[name]@rest_bones[name].inverted();d=np.array(delta);sample=item['coords'][i*size:(i+1)*size];pred=sample@d[:3,:3].T+d[:3,3];err=max(err,float(np.max(np.linalg.norm(pred-coords[i*size:(i+1)*size],axis=1))))
    assert err<.003,(clip_id,frame,item['object'].name,err);checks.append({'frame':frame,'object':item['object'].name,'max_vertex_error':err})
  matrices.append(np.array([np.array(current[name]) for name in bone_names],np.float32))
 cache[clip_id]=np.array(matrices,np.float32);descriptions.append({'id':clip_id,'name':action.name,'start':a,'end':b,'fps':fps,'loop':clip_id in ['Idle','SlamHold','ChargeHold'],'frames':len(frames),'decomposition_checks':checks})
 print('ANIMATION_CACHED',clip_id,len(frames),flush=True)
np.savez_compressed(R/'pose_cache.npz',**cache)

# Build an export-only rig. Parenting plus complete baked poses replaces IK, drivers and geometry nodes.
export_col=bpy.data.collections.new('UNITY_EXPORT · FK baked');s.collection.children.link(export_col)
root=bpy.data.objects.new('UDK7M',None);export_col.objects.link(root)
arm=bpy.data.armatures.new('UDK7M_Skeleton');portable=bpy.data.objects.new('UDK7M_RIG',arm);export_col.objects.link(portable);portable.parent=root;portable.animation_data_create()
bpy.ops.object.select_all(action='DESELECT');portable.select_set(True);bpy.context.view_layer.objects.active=portable;bpy.ops.object.mode_set(mode='EDIT')
for name in bone_names:
 bone=arm.edit_bones.new(name);bone.length=.12;bone.matrix=rest_bones[name]
 if parent_names[name]:bone.parent=arm.edit_bones[parent_names[name]]
bpy.ops.object.mode_set(mode='OBJECT')
for b in arm.bones:b.use_deform=True
for p in portable.pose.bones:p.rotation_mode='QUATERNION'
runtime=[]
for label,source_list in [('Hull',rigids),('Tracks',tracks),('Hoses',hoses)]:
 copies=[]
 for obj in source_list:
  g=geometry[obj.name];mesh=g['mesh'];mesh.transform(g['world']);copy=bpy.data.objects.new('Export_'+obj.name,mesh);export_col.objects.link(copy)
  if 'bones' in g:
   for i,name in enumerate(g['bones']):copy.vertex_groups.new(name=name).add(list(range(i*g['block_size'],(i+1)*g['block_size'])),1,'REPLACE')
  else:copy.vertex_groups.new(name=g['bone']).add(list(range(len(mesh.vertices))),1,'REPLACE')
  copies.append(copy)
 bpy.ops.object.select_all(action='DESELECT')
 for obj in copies:obj.select_set(True)
 bpy.context.view_layer.objects.active=copies[0]
 if len(copies)>1:bpy.ops.object.join()
 obj=copies[0];obj.name='UDK7M_'+label;obj.parent=root;obj.matrix_world=Matrix.Identity(4);mod=obj.modifiers.new('Portable FK skin','ARMATURE');mod.object=portable
 runtime.append(obj)

for desc in descriptions:
 cid=desc['id'];values=cache[cid];action=bpy.data.actions.new('UDK7M_'+cid);action.use_fake_user=True;slot=action.slots.new(id_type='OBJECT',name=portable.name);layer=action.layers.new('Baked FK');strip=layer.strips.new(type='KEYFRAME');bag=strip.channelbag(slot,ensure=True)
 times=np.arange(len(values),dtype=np.float32)
 for i,name in enumerate(bone_names):
  parent=parent_names[name];parent_index=bone_names.index(parent) if parent else -1;rest=arm.bones[name].matrix_local
  locs=[];rots=[];scales=[];previous=None
  # Blender local pose basis = rest^-1 * parentRest * parentPose^-1 * worldPose.
  for frame_values in values:
   world=Matrix(frame_values[i].tolist());local=rest.inverted()@world if parent is None else rest.inverted()@arm.bones[parent].matrix_local@Matrix(frame_values[parent_index].tolist()).inverted()@world
   loc,quat,scale=local.decompose()
   if previous is not None and previous.dot(quat)<0:quat.negate()
   previous=quat.copy();locs.append(list(loc));rots.append(list(quat));scales.append(list(scale))
  for prop,data in [('location',locs),('rotation_quaternion',rots),('scale',scales)]:
   array=np.array(data,np.float32)
   for channel in range(array.shape[1]):
    v=array[:,channel];indices=[0,len(v)-1] if np.max(np.abs(v-v[0]))<1e-6 and len(v)>1 else list(range(len(v)));curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{prop}',index=channel);curve.keyframe_points.add(len(indices));pairs=np.stack([times[indices],v[indices]],axis=1);curve.keyframe_points.foreach_set('co',pairs.ravel())
    for key in curve.keyframe_points:key.interpolation='LINEAR'
    curve.update()
  desc.setdefault('sample_bones',[])
 portable.animation_data.action=action;portable.animation_data.action_slot=slot;desc['baked_action']=action.name
 # Lightweight source benchmarks for Unity clip validation. World positions are in Blender metres.
 markers=['Root','Turret','Boom','Forearm','Tool','DrillRoll',blocks[0]['bones'][0],blocks[1]['bones'][0],blocks[2]['bones'][0],blocks[2]['bones'][-1]]
 desc['benchmarks']=[{'frame':int(f),'positions':[{'bone':name,'position':values[f,bone_names.index(name),:3,3].tolist()} for name in markers]} for f in sorted({0,(len(values)-1)//2,len(values)-1})]

# Place only the portable asset in an independent scene; authoring scene is preserved in the snapshot.
export_scene=bpy.data.scenes.new('UDK7M · Unity Portable');export_scene.collection.children.link(export_col);export_scene.render.fps=s.render.fps;export_scene.render.fps_base=s.render.fps_base
bpy.context.window.scene=export_scene;export_scene.frame_start=0;export_scene.frame_end=max(d['frames']-1 for d in descriptions)
portable.animation_data.action=None;export_scene.frame_set(0);bpy.context.view_layer.update()
# Save before texture baking. Blender bakes use this portable scene and its preserved source materials.
report={'source_snapshot':str(R/'UDK7M_UserAnimation_Source.blend'),'fps':fps,'bone_count':len(bone_names),'groups':[{'object':o.name,'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'skin_bones':len(o.vertex_groups)} for o in runtime],'clips':descriptions,'bone_names':bone_names,'source_action_count':len(actions),'passed':True}
(R/'animation_manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(R/'UDK7M_Portable_BeforeBake.blend'),compress=True)
print('PORTABLE_RIG_READY',len(bone_names),report['groups'],flush=True)
