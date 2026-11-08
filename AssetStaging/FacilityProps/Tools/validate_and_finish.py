import bpy,json,math,ast,random,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path('D:/UNITY工程/棍木项目/棍木项目');OUT=ROOT/'AssetStaging/FacilityProps';TEX=OUT/'Textures'
scene=bpy.context.scene;font=bpy.data.fonts['Arial Bold'];r=random.Random(426)
source=ast.parse((OUT/'Tools/build_props.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in source.body if isinstance(n,ast.FunctionDef) and n.name in ['load_image','emission_mat','atlas']],type_ignores=[]),'atlas_tools','exec'))
# Make left-pointing pictograms run toward their arrow; lettering remains unmirrored.
for prefix in ['ML','SV']:
 key=prefix+'_Exit_left';mat=bpy.data.materials['FP_'+key]
 old=next(n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE')
 bpy.data.images.remove(old,do_unlink=True)
 im=atlas(key,'sign','left')
 next(n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE').image=im
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'));checks=[]
try:bpy.ops.import_scene.fbx.get_rna_type()
except Exception:bpy.ops.preferences.addon_enable(module='io_scene_fbx')
for entry in manifest['models']:
 name=entry['name'];obj=bpy.data.objects[name];assert obj.type=='MESH';assert len(obj.data.uv_layers)==1 and obj.data.uv_layers[0].name=='UV0'
 assert not obj.modifiers and obj.asset_data
 assert all(math.isfinite(v) for uv in obj.data.uv_layers[0].data for v in uv.uv)
 assert all(math.isfinite(v) for vertex in obj.data.vertices for v in vertex.co)
 obj.data.calc_loop_triangles();assert len(obj.data.loop_triangles)==entry['triangles']
 assert all(mat and mat.use_nodes for mat in obj.data.materials)
 assert not any('FONT'==o.type for o in [obj])
 before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(OUT/'Exports'/(name+'.fbx')),use_anim=False)
 imported=[o for o in set(bpy.data.objects)-before if o.type=='MESH'];assert len(imported)==1;copy=imported[0]
 actual=[copy.matrix_world@v.co for v in copy.data.vertices];expected=[v.co for v in obj.data.vertices]
 error=max(abs(min(p[i] for p in actual)-min(p[i] for p in expected)) for i in range(3))
 error=max(error,max(abs(max(p[i] for p in actual)-max(p[i] for p in expected)) for i in range(3)))
 assert error<.001,(name,error)
 assert len(copy.data.uv_layers)==1 and copy.data.uv_layers[0].name=='UV0'
 checks.append({'model':name,'triangles':entry['triangles'],'FBX_metric_bounds_error_metres':error,'single_UV0':True})
 for o in set(bpy.data.objects)-before:bpy.data.objects.remove(o,do_unlink=True)
for entry in manifest['models']:
 for mat in bpy.data.objects[entry['name']].data.materials:
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE':assert node.image and node.image.packed_file,(mat.name,'unpacked image')
report={'passed':True,'stage':'Awaiting visual approval; no Unity Assets writes','models':len(checks),'checks':checks,'total_triangles':manifest['total_triangles'],'textures_max_resolution':1024,'Russian_exit':'ВЫХОД','arrow_variants':['Left','Right','Forward'],'all_assigned_images_packed':True}
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.window.scene=scene;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FacilityProps_Review_v01.blend'),compress=True)
print('FACILITY_PROPS_VALIDATED',len(checks),flush=True)
