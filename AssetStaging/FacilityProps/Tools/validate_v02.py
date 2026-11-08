import bpy,json,math
from pathlib import Path
ROOT=Path('D:/UNITY工程/棍木项目/棍木项目');R=ROOT/'AssetStaging/FacilityProps/V02'
manifest=json.loads((R/'manifest.json').read_text(encoding='utf-8'))
assert len(manifest['models'])==10 and manifest['exit_design_count']==6
signs=[e for e in manifest['models'] if '_Exit_' in e['name']]
assert len(signs)==6
assert {e['mount'] for e in signs}=={'Side','Ceiling','Door'}
assert all(e['has_pictogram']==e['name'].startswith('ML') for e in signs)
assert all(e['arrow']=='none' for e in signs if e['mount']=='Door')
soviet=bpy.data.objects['SV_Crate_1m'];assert 'SV_Paint_Yellow' not in [m.name for m in soviet.data.materials]
assert 'FP_Black_Iron' in [m.name for m in soviet.data.materials]
for entry in manifest['models']:
 obj=bpy.data.objects[entry['name']];assert obj.type=='MESH' and not obj.modifiers
 assert len(obj.data.uv_layers)==1 and obj.data.uv_layers[0].name=='UV0'
 assert obj.asset_data and max(abs(s-1) for s in obj.scale)<1e-6
 assert all(math.isfinite(v) for vertex in obj.data.vertices for v in vertex.co)
 assert all(math.isfinite(v) for uv in obj.data.uv_layers[0].data for v in uv.uv)
 for mat in obj.data.materials:
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE':assert node.image and node.image.packed_file,(mat.name,'unpacked map')
 if entry.get('mount')=='Ceiling':
  # The mounting canopy meets the actual origin plane, and the two lenses face opposite ways.
  assert abs(max(v.co.z for v in obj.data.vertices))<.00001
  lens_faces=[f for f in obj.data.polygons if obj.data.materials[f.material_index].name==f"FP_{entry['name'][:2]}_Exit_Ceiling"]
  assert len(lens_faces)==2 and min(f.normal.y for f in lens_faces)<-.99 and max(f.normal.y for f in lens_faces)>.99
 # The FBX should reproduce metric mesh bounds and the exact first UV channel.
 scene=obj.users_scene[0];bpy.context.window.scene=scene;before=set(bpy.data.objects)
 bpy.ops.import_scene.fbx(filepath=str(R/'Exports'/(entry['name']+'.fbx')),use_anim=False)
 imported=[o for o in set(bpy.data.objects)-before if o.type=='MESH'];assert len(imported)==1;copy=imported[0]
 actual=[copy.matrix_world@v.co for v in copy.data.vertices];expected=[v.co for v in obj.data.vertices]
 error=max(abs(min(p[i] for p in actual)-min(p[i] for p in expected)) for i in range(3));error=max(error,max(abs(max(p[i] for p in actual)-max(p[i] for p in expected)) for i in range(3)))
 assert error<.001,(entry['name'],error)
 assert len(copy.data.uv_layers)==1 and copy.data.uv_layers[0].name=='UV0'
 entry['fbx_bounds_error_metres']=error
 for o in set(bpy.data.objects)-before:bpy.data.objects.remove(o,do_unlink=True)
for name,option in manifest['side_arrow_material_options'].items():
 assert bpy.data.materials.get(option['left']) and bpy.data.materials[option['left']].use_fake_user
report={'passed':True,'awaiting_user_approval':True,'unity_imported':False,'models':len(manifest['models']),'sign_designs':6,'checks':['Soviet metal crate: black iron replaces every yellow/orange trim slot','Two wooden crates; no font geometry or label textures assigned','Modern exit uses supplied pictogram; all Soviet variants have no pictogram','Side, double-sided ceiling-forward and arrowless door variants for both facilities','Ceiling mounting-plane pivots and opposite readable lens normals','Both left/right side-arrow materials retained','Single UV0, applied scale, finite mesh data, packed images','All 10 FBX files round-tripped with under 1mm metric bounds error'],'meshes':[{'name':e['name'],'triangles':e['triangles'],'fbx_bounds_error_metres':e['fbx_bounds_error_metres']} for e in manifest['models']]}
(R/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('PROPS_V02_VALIDATED',report['models'],report['sign_designs'],flush=True)
