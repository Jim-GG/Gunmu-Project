import bpy,math,json,time
import numpy as np
from pathlib import Path
from mathutils import Matrix,Vector
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');T=R/'Textures';T.mkdir(exist_ok=True)
s=bpy.context.scene;rig=bpy.data.objects['UDK7M_RIG'];root=bpy.data.objects['UDK7M'];meshes=[bpy.data.objects['UDK7M_'+n] for n in ['Hull','Tracks','Hoses']]
report=json.loads((R/'animation_manifest.json').read_text(encoding='utf-8'));cache=np.load(R/'pose_cache.npz');names=report['bone_names'];validation=[]
for desc in report['clips']:
 action=bpy.data.actions[desc['baked_action']];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
 for benchmark in desc['benchmarks']:
  frame=benchmark['frame'];s.frame_set(frame);bpy.context.view_layer.update();e=rig.evaluated_get(bpy.context.evaluated_depsgraph_get());err=0
  for i,name in enumerate(names):err=max(err,(e.pose.bones[name].matrix.translation-Vector(cache[desc['id']][frame,i,:3,3])).length)
  assert err<.002,(desc['id'],frame,err)
  validation.append({'clip':desc['id'],'frame':frame,'max_bone_position_error_metres':err})
 print('FK_VALIDATED',desc['id'],flush=True)
rig.animation_data.action=None
for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
rig.data.pose_position='REST';s.frame_set(0);bpy.context.view_layer.update()
s.render.engine='CYCLES';s.cycles.samples=8;s.cycles.use_denoising=False
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 s.cycles.device='GPU'
except Exception:pass
s.render.bake.use_selected_to_active=False;s.render.bake.margin=8;s.render.bake.use_clear=True
def select(obj):
 if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
def image(name,size,color=False):
 im=bpy.data.images.new(name,width=size,height=size,alpha=True);im.colorspace_settings.name='sRGB' if color else 'Non-Color';im.alpha_mode='CHANNEL_PACKED';return im
def pixels(im):
 a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);return a.reshape(im.size[1],im.size[0],4)
def save(im):
 im.filepath_raw=str(T/(im.name+'.png'));im.file_format='PNG';im.save();im.pack();print('MAP_BAKED',im.name,list(im.size),flush=True)
atlases=[]
for obj,size in zip(meshes,[4096,1024,512]):
 select(obj)
 # Keep the source tread-detail UV while baking, then export only the new atlas as UV0.
 uv=obj.data.uv_layers.new(name='UDK_Atlas');obj.data.uv_layers.active=uv;uv.active_render=True
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.003,area_weight=0.);bpy.ops.object.mode_set(mode='OBJECT')
 # Copies isolate baking rewires from the saved author materials.
 for i,m in enumerate(obj.data.materials):obj.data.materials[i]=m.copy()
 mats=list(obj.data.materials);original={};targets={};emit_nodes={};bsdfs={}
 for mat in mats:
  n=mat.node_tree.nodes;l=mat.node_tree.links;bs=next(n for n in n if n.type=='BSDF_PRINCIPLED');out=next(n for n in n if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
  original[mat]=next(link.from_socket for link in l if link.to_socket==out.inputs['Surface']);bsdfs[mat]=bs
  target=n.new('ShaderNodeTexImage');target.name='BAKE_TARGET';n.active=target;targets[mat]=target;emit=n.new('ShaderNodeEmission');emit_nodes[mat]=emit
  for normal in [x for x in n if x.type=='NORMAL_MAP']:
   if not normal.uv_map and obj.name=='UDK7M_Tracks' and obj.data.uv_layers.get('TreadDetailUV'):normal.uv_map='TreadDetailUV'
 def route(mat,source,dest):
  if source.is_linked:mat.node_tree.links.new(source.links[0].from_socket,dest)
  else:dest.default_value=source.default_value
 def target(im):
  for mat in mats:targets[mat].image=im;mat.node_tree.nodes.active=targets[mat]
 def emit(channel):
  for mat in mats:
   bs=bsdfs[mat];em=emit_nodes[mat];l=mat.node_tree.links;out=next(n for n in mat.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
   for link in list(l):
    if link.to_socket in [em.inputs[0],em.inputs[1]]:l.remove(link)
   em.inputs[1].default_value=1
   if channel=='base':route(mat,bs.inputs['Base Color'],em.inputs[0])
   elif channel=='emission':route(mat,bs.inputs['Emission Color'],em.inputs[0]);route(mat,bs.inputs['Emission Strength'],em.inputs[1])
   else:
    combine=mat.node_tree.nodes.new('ShaderNodeCombineColor');combine.mode='RGB';route(mat,bs.inputs['Metallic'],combine.inputs['Red']);route(mat,bs.inputs['Roughness'],combine.inputs['Green']);l.new(combine.outputs[0],em.inputs[0])
   l.new(em.outputs[0],out.inputs['Surface'])
 base=image(obj.name+'_BaseColor',size,True);target(base);emit('base');bpy.ops.object.bake(type='EMIT');save(base)
 packed=image(obj.name+'_Packing',size);target(packed);emit('mask');bpy.ops.object.bake(type='EMIT');a=pixels(packed);a[:,:,3]=1-np.clip(a[:,:,1],0,1);a[:,:,1]=1;a[:,:,2]=0;mask=image(obj.name+'_Mask',size);mask.pixels.foreach_set(a.ravel());save(mask);bpy.data.images.remove(packed)
 emission=image(obj.name+'_Emission',size,True);target(emission);emit('emission');bpy.ops.object.bake(type='EMIT');a=pixels(emission);a[:,:,3]=0;emission.pixels.foreach_set(a.ravel());save(emission)
 for mat in mats:
  out=next(n for n in mat.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output);mat.node_tree.links.new(original[mat],out.inputs['Surface'])
 nm=image(obj.name+'_Normal',size);target(nm);bpy.ops.object.bake(type='NORMAL',normal_space='TANGENT');save(nm)
 # Single atlas material with all source dirt/rust already baked into its PBR channels.
 material=bpy.data.materials.new(obj.name+'_Baked');material.use_nodes=True;n=material.node_tree.nodes;l=material.node_tree.links;bs=n.get('Principled BSDF');uvnode=n.new('ShaderNodeUVMap');uvnode.uv_map='UV0'
 def tx(im):o=n.new('ShaderNodeTexImage');o.image=im;l.new(uvnode.outputs[0],o.inputs[0]);return o
 bt=tx(base);mt=tx(mask);nt=tx(nm);et=tx(emission);l.new(bt.outputs[0],bs.inputs['Base Color']);sep=n.new('ShaderNodeSeparateColor');l.new(mt.outputs[0],sep.inputs[0]);l.new(sep.outputs['Red'],bs.inputs['Metallic']);rough=n.new('ShaderNodeMath');rough.operation='SUBTRACT';rough.inputs[0].default_value=1;l.new(mt.outputs['Alpha'],rough.inputs[1]);l.new(rough.outputs[0],bs.inputs['Roughness']);normal=n.new('ShaderNodeNormalMap');normal.uv_map='UV0';l.new(nt.outputs[0],normal.inputs['Color']);l.new(normal.outputs[0],bs.inputs['Normal']);l.new(et.outputs[0],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=1
 # RNA collection proxies may compare unequal even for the same UV layer.
 # Copy coordinates before removing layers; never compare cached proxies.
 atlas_uv=np.array([tuple(v.uv) for v in obj.data.uv_layers['UDK_Atlas'].data],np.float32)
 while obj.data.uv_layers:obj.data.uv_layers.remove(obj.data.uv_layers[0])
 obj.data.uv_layers.new(name='UV0');obj.data.uv_layers[0].data.foreach_set('uv',atlas_uv.ravel());obj.data.uv_layers.active_index=0;obj.data.uv_layers[0].active_render=True
 for face in obj.data.polygons:face.material_index=0
 obj.data.materials.clear();obj.data.materials.append(material)
 atlases.append({'name':obj.name,'material':material.name,'size':size,'base':base.name+'.png','mask':mask.name+'.png','normal':nm.name+'.png','emission':emission.name+'.png'})
 print('ATLAS_COMPLETE',obj.name,size,flush=True)
report['atlases']=atlases;report['fk_validation']=validation
(R/'export_manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

# Export bind model and skeleton-only animation FBXs with an identical named hierarchy.
def selection(objects):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
def export(path,animated,objects):
 selection(objects)
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=animated,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',primary_bone_axis='Y',secondary_bone_axis='X',apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP')
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4)
rig.data.pose_position='REST';s.frame_start=0;s.frame_end=1;s.frame_set(0);export(R/'Exports/UDK7M_Model.fbx',False,[root,rig]+meshes)
rig.data.pose_position='POSE'
for desc in report['clips']:
 action=bpy.data.actions[desc['baked_action']];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];s.frame_start=0;s.frame_end=desc['frames']-1;s.frame_set(0);bpy.context.view_layer.update();export(R/'Exports'/('UDK7M_'+desc['id']+'.fbx'),True,[root,rig]);print('CLIP_EXPORTED',desc['id'],flush=True)
idle=next(d for d in report['clips'] if d['id']=='Idle');rig.animation_data.action=bpy.data.actions[idle['baked_action']];rig.animation_data.action_slot=rig.animation_data.action.slots[0];s.frame_start=0;s.frame_end=idle['frames']-1;s.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(R/'UDK7M_Portable_Animated.blend'),compress=True)
print('UDK7M_EXPORT_COMPLETE',len(report['clips']),sum(g['triangles'] for g in report['groups']),flush=True)
