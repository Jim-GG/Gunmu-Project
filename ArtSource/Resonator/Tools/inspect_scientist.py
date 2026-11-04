import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(r'D:\UNITY工程\棍木项目\棍木项目\ArtSource\Resonator')
data={'file':bpy.data.filepath,'frame':bpy.context.scene.frame_current,'objects':[],'materials':[],'images':[]}
for o in bpy.context.scene.objects:
 d={'name':o.name,'type':o.type,'hide':o.hide_get(),'render':not o.hide_render,'location':list(o.location),'rotation':list(o.rotation_euler),'scale':list(o.scale),'parent':o.parent.name if o.parent else None,'dimensions':list(o.dimensions)}
 if o.type=='MESH':
  d.update(vertices=len(o.data.vertices),faces=len(o.data.polygons),materials=[m.name if m else None for m in o.data.materials],groups=[g.name for g in o.vertex_groups],modifiers=[{'name':m.name,'type':m.type,'object':m.object.name if hasattr(m,'object') and m.object else None} for m in o.modifiers],bounds=[list(o.matrix_world@Vector(c)) for c in o.bound_box])
 if o.type=='ARMATURE':d.update(bones=[b.name for b in o.data.bones])
 data['objects'].append(d)
for m in bpy.data.materials:
 data['materials'].append({'name':m.name,'nodes':[{'name':n.name,'type':n.bl_idname,'image':n.image.name if n.type=='TEX_IMAGE' and n.image else None} for n in m.node_tree.nodes] if m.use_nodes else []})
for im in bpy.data.images:data['images'].append({'name':im.name,'path':im.filepath,'size':list(im.size),'packed':bool(im.packed_file)})
(root/'Source'/'inspection.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Source'/'Scientist_InputSnapshot.blend'))
print('RESONATOR_INSPECTION_SAVED')
