import bpy,json,math
from pathlib import Path
from mathutils import Vector
R=Path.cwd();S=R/'AssetStaging/GauntletStandalone';D=R/'Assets/Game/Player/Gauntlet/Models';bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(D/'Gauntlet_Model.fbx'))
src=next(o for o in bpy.data.objects if o.type=='MESH');me=src.data;group=src.vertex_groups['Gauntlet_Mechanism'].index
keep={v.index for v in me.vertices if any(g.group==group and g.weight>.99 for g in v.groups)}
faces=[p for p in me.polygons if all(i in keep for i in p.vertices)];cross=[p.index for p in me.polygons if any(i in keep for i in p.vertices) and not all(i in keep for i in p.vertices)];assert not cross, cross
used=sorted({i for p in faces for i in p.vertices});idx={v:i for i,v in enumerate(used)}
coords=[src.matrix_world@me.vertices[i].co for i in used];lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)]);center=(lo+hi)/2
mesh=bpy.data.meshes.new('Gauntlet_Standalone_Mesh');mesh.from_pydata([v-center for v in coords],[],[[idx[i] for i in p.vertices] for p in faces]);mesh.update()
obj=bpy.data.objects.new('Gauntlet_Standalone',mesh);bpy.context.collection.objects.link(obj)
for mat in me.materials:mesh.materials.append(mat)
for old,new in zip(faces,mesh.polygons):new.material_index=old.material_index;new.use_smooth=old.use_smooth
for uv in me.uv_layers:
 layer=mesh.uv_layers.new(name=uv.name)
 for old,new in zip(faces,mesh.polygons):
  for ol,nl in zip(old.loop_indices,new.loop_indices):layer.data[nl].uv=uv.data[ol].uv
normalmat=src.matrix_world.to_3x3().inverted().transposed();normals=[(normalmat@me.corner_normals[i].vector).normalized() for p in faces for i in p.loop_indices];mesh.normals_split_custom_set(normals)
for other in list(bpy.data.objects):
 if other!=obj:bpy.data.objects.remove(other,do_unlink=True)
# Rebuild the same atlas PBR inputs for the editable Blender source.
tex=R/'Assets/Game/Player/Gauntlet/Textures'
mat=mesh.materials[0];mat.use_nodes=True;n=mat.node_tree.nodes;n.clear();l=mat.node_tree.links
out=n.new('ShaderNodeOutputMaterial');out.location=(700,0);p=n.new('ShaderNodeBsdfPrincipled');p.location=(400,0);l.new(p.outputs['BSDF'],out.inputs['Surface'])
def image_node(name,y,noncolor=False):
 t=n.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(tex/name),check_existing=True);t.location=(-600,y)
 if noncolor:t.image.colorspace_settings.name='Non-Color'
 return t
base=image_node('Gauntlet_BaseColor.png',400);l.new(base.outputs['Color'],p.inputs['Base Color'])
metal=image_node('Gauntlet_MetallicSmoothness.png',100,True);sep=n.new('ShaderNodeSeparateColor');sep.location=(-300,100);l.new(metal.outputs['Color'],sep.inputs[0]);l.new(sep.outputs['Red'],p.inputs['Metallic']);inv=n.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;inv.location=(-100,-50);l.new(metal.outputs['Alpha'],inv.inputs[1]);l.new(inv.outputs[0],p.inputs['Roughness'])
normal=image_node('Gauntlet_Normal.png',-250,True);nm=n.new('ShaderNodeNormalMap');nm.location=(0,-250);l.new(normal.outputs['Color'],nm.inputs['Color']);l.new(nm.outputs['Normal'],p.inputs['Normal'])
em=image_node('Gauntlet_EmissionRegions.png',-550);l.new(em.outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=2
mat=mesh.materials[1];mat.use_nodes=True;p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(.62,.72,.77,1);p.inputs['Roughness'].default_value=.12;p.inputs['Metallic'].default_value=0;p.inputs['Alpha'].default_value=.14;mat.surface_render_method='DITHERED'
# Only one static mesh, with native metre scale and centered pivot.
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
bpy.ops.export_scene.fbx(filepath=str(D/'Gauntlet_Standalone.fbx'),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',mesh_smooth_type='OFF',path_mode='AUTO')
# Prepare a clean, framed Blender file, without cameras, lights or skeletons.
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_distance=max(hi-lo)*2.8;area.spaces.active.region_3d.view_location=Vector((0,0,0));area.spaces.active.region_3d.view_rotation=Vector((1,-1,.65)).to_track_quat('Z','Y')

for image in list(bpy.data.images):
 if image.users == 0:bpy.data.images.remove(image)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(S/'Gauntlet_Standalone.blend'),compress=True)
# Preview uses temporary studio objects; they are not saved in the source file.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=1000;scene.render.resolution_y=850;scene.render.resolution_percentage=100;scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.15,.19,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
size=max(hi-lo)
def light(name,loc,power,scale):
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size*scale;o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=Vector(loc)*size;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
light('Key',(1,-1.5,2),90,2);light('Fill',(-2,-.2,.7),50,2);light('Rim',(.5,2,1.2),110,1.3)
c=bpy.data.cameras.new('Preview');camera=bpy.data.objects.new('Preview',c);scene.collection.objects.link(camera);camera.location=Vector((1,-1,.65)).normalized()*size*2.6;camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler();c.type='ORTHO';c.ortho_scale=size*1.45;scene.camera=camera;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(S/'preview.png');bpy.ops.render.render(write_still=True)
report=dict(source='Assets/Game/Player/Gauntlet/Models/Gauntlet_Model.fbx',selection='Gauntlet_Mechanism weight > 0.99',vertices=len(mesh.vertices),triangles=sum(len(p.vertices)-2 for p in mesh.polygons),materials=[m.name for m in mesh.materials],uvLayers=[x.name for x in mesh.uv_layers],mixedBoundaryFaces=cross,dimensionsMetres=list(hi-lo),sourcePivot=list(center),armatureCount=0,output='Assets/Game/Player/Gauntlet/Models/Gauntlet_Standalone.fbx');(S/'export-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
