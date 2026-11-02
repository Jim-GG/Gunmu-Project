import bpy,math,json
from pathlib import Path
from mathutils import Matrix,Vector
root=Path(__file__).resolve().parents[3]
scene=bpy.context.scene
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
arms=bpy.data.objects['Armature'];mech=bpy.data.objects['拳套'];camera=bpy.data.objects['Camera']
defaults={o.name:{b.name:b.matrix_basis.copy() for b in o.pose.bones} for o in [arms,mech]}
def assign(a):
 for o,slotname in [(arms,'OBLegacy Slot'),(mech,'OB拳套')]:
  ad=o.animation_data_create();ad.action=None
  for b in o.pose.bones:b.matrix_basis=defaults[o.name][b.name]
  ad.action=a;ad.use_nla=False
  sl=next((s for s in a.slots if s.identifier==slotname),None)
  if sl:ad.action_slot=sl
  for t in ad.nla_tracks:t.mute=True
  o.data.pose_position='POSE'
 scene.frame_set(-1);scene.frame_set(0);bpy.context.view_layer.update()
assign(bpy.data.actions['Idle']);reference=camera.matrix_world.copy();transform=Matrix.Scale(.0254,4)@Matrix.Translation(-reference.translation)
for o in [arms,mech]:o.data.pose_position='REST'
bpy.context.view_layer.update()
data=bpy.data.armatures.new('GauntletRig');rig=bpy.data.objects.new('GauntletRig',data);scene.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
info=[]
for o in [arms,mech]:
 for b in o.data.bones:
  name='Gauntlet_Mechanism' if o==mech else b.name;m=transform@o.matrix_world@b.matrix_local
  eb=data.edit_bones.new(name);eb.matrix=Matrix.LocRotScale(m.translation,m.to_quaternion(),Vector((1,1,1)));eb.length=max(.001,(transform@(o.matrix_world@b.tail_local)-m.translation).length)
  info.append((o,b.name,name,m.to_scale()))
for o,bn,n,s in info:
 parent=o.data.bones[bn].parent
 if parent:data.edit_bones[n].parent=data.edit_bones[parent.name]
data.edit_bones['Gauntlet_Mechanism'].parent=data.edit_bones['hand_L.R']
m=transform@reference;eb=data.edit_bones.new('ViewCameraMotion');eb.matrix=Matrix.LocRotScale(m.translation,m.to_quaternion(),Vector((1,1,1)));eb.length=.05;eb.parent=data.edit_bones['camera'];info.append((camera,None,'ViewCameraMotion',m.to_scale()))
bpy.ops.object.mode_set(mode='OBJECT')
for b in rig.pose.bones:b.rotation_mode='QUATERNION'
asset=bpy.data.objects.new('GauntletAsset',None);scene.collection.objects.link(asset);rig.parent=asset
anchor=bpy.data.objects.new('ViewCameraAnchor',None);scene.collection.objects.link(anchor);anchor.parent=asset;anchor.matrix_world=Matrix.LocRotScale(m.translation,m.to_quaternion(),Vector((1,1,1)))
a=next(a for a in bpy.data.actions if a.name.lower().replace('_',' ').strip()=='charge full');assign(a)
start,end=0,50;tracks={n:[[] for _ in range(10)] for _,_,n,_ in info};last={}
for f in range(start,end+1):
 scene.frame_set(f);bpy.context.view_layer.update();poses={}
 for o,bn,n,base in info:
  m=transform@(o.matrix_world if bn is None else o.matrix_world@o.pose.bones[bn].matrix);s=m.to_scale();poses[n]=Matrix.LocRotScale(m.translation,m.to_quaternion(),Vector(tuple(s[i]/base[i] for i in range(3))))
 for o,bn,n,base in info:
  b=data.bones[n];kw=dict(matrix=poses[n],matrix_local=b.matrix_local,invert=True)
  if b.parent:kw.update(parent_matrix=poses[b.parent.name],parent_matrix_local=b.parent.matrix_local)
  loc,q,s=b.convert_local_to_pose(**kw).decompose()
  if n in last and last[n].dot(q)<0:q.negate()
  last[n]=q.copy()
  for dst,value in zip(tracks[n],list(loc)+list(q)+list(s)):dst.extend((f-start,value))
action=bpy.data.actions.new('UNITY_charge full');slot=action.slots.new(id_type='OBJECT',name=rig.name);bag=action.layers.new('Baked').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
for n,channels in tracks.items():
 for i,values in enumerate(channels):
  prop='location' if i<3 else 'rotation_quaternion' if i<7 else 'scale';index=i if i<3 else i-3 if i<7 else i-7
  fc=bag.fcurves.new('pose.bones["'+n+'"].'+prop,index=index);fc.keyframe_points.add(end-start+1);fc.keyframe_points.foreach_set('co',values)
  for k in fc.keyframe_points:k.interpolation='LINEAR'
  fc.update()
rig.animation_data_create().action=action;rig.animation_data.action_slot=slot
scene.frame_start=0;scene.frame_end=end-start;scene.render.fps=30;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.object.select_all(action='DESELECT')
for o in [asset,rig,anchor]:o.select_set(True)
bpy.context.view_layer.objects.active=rig
out=root/'Assets/备份拳套/Gauntlet/Animations/Gauntlet_charge_full.fbx'
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'EMPTY','ARMATURE'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_step=1,bake_anim_simplify_factor=0)
print('CHARGE FULL EXPORTED',start,end,len(data.bones),flush=True)


