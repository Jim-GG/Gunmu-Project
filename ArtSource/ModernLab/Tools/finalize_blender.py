import bpy,json,numpy as np,sys
from pathlib import Path
root=Path('D:/UNITY工程/棍木项目/棍木项目/ArtSource/ModernLab')
keys=list(json.loads((root/'build_report.json').read_text(encoding='utf-8'))['materials'])
for key in keys:
 for node in bpy.data.materials[key].node_tree.nodes:
  if node.type in ['UVMAP','NORMAL_MAP']:node.uv_map=''
preview=bpy.data.scenes['Material Preview'];bpy.context.window.scene=preview
if 'Observation window' not in preview.objects:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(-2.6,3.05,2.3));o=bpy.context.object;o.name='Observation window';o.dimensions=(1.8,.04,1.2);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(bpy.data.materials['Preview_Light'])
original=json.loads((root/'Source/inspection.json').read_text(encoding='utf-8'))
scene=bpy.data.scenes['Modular Asset Kit'];meshes=[o for o in scene.objects if o.type=='MESH']
bpy.context.window.scene=scene;bpy.context.view_layer.update()
assert len(meshes)==len(original['objects'])==29
for item in original['objects']:
 o=scene.objects[item['name']];assert len(o.data.vertices)==item['vertices'];assert all(abs(a-b)<.00001 for a,b in zip(o.dimensions,item['dimensions']));assert any(u.name=='LabUV' and u.active_render for u in o.data.uv_layers)
 checks=[u.name for u in o.data.uv_layers];assert 'LabUV' in checks
report={'passed':True,'preserved_objects':29,'unchanged_source_topology':True,'packed_images':[],'materials':{}}
for image in bpy.data.images:
 if not any(image.name.startswith(key) for key in keys):continue
 assert image.packed_file and tuple(image.size)==(2048,2048)
 pixels=np.empty(2048*2048*4,np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(2048,2048,4)
 assert np.isfinite(pixels).all()
 border=max(float(np.abs(pixels[0]-pixels[-1]).max()),float(np.abs(pixels[:,0]-pixels[:,-1]).max()))
 assert border<.0001,(image.name,border)
 report['packed_images'].append({'name':image.name,'seam_difference':border,'colour_space':image.colorspace_settings.name})
 if image.name.endswith('_Mask'):report['materials'][image.name[:-5]]={'mean_roughness':float((1-pixels[:,:,3]).mean()),'metallic_max':float(pixels[:,:,0].max())}
assert len(report['packed_images'])==27
for key,data in report['materials'].items():assert data['metallic_max']==(1 if key=='Lab_Rail_BrushedSteel' else 0)
rail_count=0
for o in meshes:
 if o.get('lab_role')!='Rail':continue
 rail_count+=1;assert o.data.materials[0].name=='Lab_Rail_BrushedSteel'
 evaluated=o.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
 uv=mesh.uv_layers.get('LabUV');assert uv and len(uv.data)>0
 assert max(v.uv.x for v in uv.data)-min(v.uv.x for v in uv.data)>.1 or max(v.uv.y for v in uv.data)-min(v.uv.y for v in uv.data)>.1
 bpy.data.meshes.remove(mesh)
assert rail_count==9;report['metallic_guardrails']=rail_count;report['evaluated_guardrail_uvs_valid']=True
extra=bpy.data.scenes['Additional Modules'];bpy.context.window.scene=extra;bpy.context.view_layer.update()
assert len([o for o in extra.objects if o.type=='MESH'])==7
for name,size in [('吊顶_6m',6),('吊顶_3m',3)]:assert all(abs(v-size)<.0001 for v in extra.objects[name].dimensions[:2])
for name in ['分区墙_Lime','分区墙_Red','分区墙_Yellow']:assert len(set(p.material_index for p in extra.objects[name].data.polygons))==2
report['additional_modules']=7;report['ceiling_sizes_metres']=[6,3];report['colour_band_faces_separate']=True
(root/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
if '--validate-only' in sys.argv:print('MODERN_LAB_SOURCE_VALIDATED',flush=True);sys.exit(0)
bpy.data.libraries.write(str(root/'ModernLab_Materials.blend'),{bpy.data.materials[k] for k in keys},fake_user=True,compress=True)
bpy.context.window.scene=scene;bpy.ops.wm.save_as_mainfile(filepath=str(root/'ModernLab_ModularKit.blend'),compress=True)
bpy.context.window.scene=preview;bpy.ops.render.render(write_still=True)
print('MODERN_LAB_FINAL_VALIDATED',flush=True)
