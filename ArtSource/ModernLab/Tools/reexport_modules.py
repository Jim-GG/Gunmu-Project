"""Export the current saved materials and meshes without rebuilding textures."""
import bpy,ast,json
from pathlib import Path
from mathutils import Vector
root=Path('D:/UNITY工程/棍木项目/棍木项目');source=root/'ArtSource/ModernLab'
tree=ast.parse((source/'Tools/build_materials.py').read_text(encoding='utf-8'))
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['uv_metres','prepare_backfaces']],type_ignores=[]),'export_helpers','exec'))
report=json.loads((source/'build_report.json').read_text(encoding='utf-8'))
for item in report['exports']:
 owner=bpy.data.scenes['Modular Asset Kit'] if item['source'] in bpy.data.scenes['Modular Asset Kit'].objects else bpy.data.scenes['Additional Modules']
 bpy.context.window.scene=owner;bpy.context.view_layer.update();o=owner.objects[item['source']];depsgraph=bpy.context.evaluated_depsgraph_get()
 mesh=bpy.data.meshes.new_from_object(o.evaluated_get(depsgraph),preserve_all_data_layers=True,depsgraph=depsgraph);temp=bpy.data.objects.new(Path(item['fbx']).stem,mesh);owner.collection.objects.link(temp);temp.matrix_world=o.matrix_world.copy();temp.location=(0,0,0)
 uv_metres(temp,item['role']=='Rail')
 for layer in list(mesh.uv_layers):
  if layer.name!='LabUV':mesh.uv_layers.remove(layer)
 if item['role'] in ['WallWhite','WallDark','AccentWall','Ceiling']:prepare_backfaces(mesh)
 temp.modifiers.new('Export triangulation','TRIANGULATE')
 bpy.ops.object.select_all(action='DESELECT');temp.select_set(True);bpy.context.view_layer.objects.active=temp
 bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Game/Environment/ModernLab/Models'/item['fbx']),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP')
 bpy.data.objects.remove(temp,do_unlink=True)
print('MODERN_LAB_TWO_SIDED_EXPORT_OK',len(report['exports']),flush=True)
