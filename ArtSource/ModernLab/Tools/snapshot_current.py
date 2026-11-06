import bpy,json
from pathlib import Path
root=Path('D:/UNITY工程/棍木项目/棍木项目/ArtSource/ModernLab')
root.joinpath('Source').mkdir(parents=True,exist_ok=True)
report={'source_path':bpy.data.filepath,'version':bpy.app.version_string,'units':bpy.context.scene.unit_settings.system,'scale_length':bpy.context.scene.unit_settings.scale_length,'objects':[]}
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 points=[o.matrix_world@__import__('mathutils').Vector(p) for p in o.bound_box]
 report['objects'].append({'name':o.name,'location':list(o.location),'rotation':list(o.rotation_euler),'scale':list(o.scale),'dimensions':list(o.dimensions),'bounds_min':[min(p[i] for p in points) for i in range(3)],'bounds_max':[max(p[i] for p in points) for i in range(3)],'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'uvs':[u.name for u in o.data.uv_layers],'materials':[s.material.name if s.material else None for s in o.material_slots],'modifiers':[{'name':m.name,'type':m.type} for m in o.modifiers],'collections':[c.name for c in o.users_collection]})
root.joinpath('Source/inspection.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Source/ModularKit_InputSnapshot.blend'),copy=True)
print('MODERN_LAB_SNAPSHOT_OK',len(report['objects']))
