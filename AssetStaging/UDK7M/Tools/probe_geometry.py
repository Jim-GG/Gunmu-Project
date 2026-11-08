import bpy,json
from pathlib import Path
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');s=bpy.context.scene;s.frame_set(0);dg=bpy.context.evaluated_depsgraph_get();out=[]
for o in s.objects:
 if o.type=='CURVE' and any(m.type=='HOOK' for m in o.modifiers):
  e=o.evaluated_get(dg);mesh=e.to_mesh();out.append({'name':o.name,'verts':len(mesh.vertices),'polys':len(mesh.polygons),'bevel_resolution':o.data.bevel_resolution,'resolution_u':o.data.resolution_u,'splines':[(p.type,len(p.bezier_points),len(p.points)) for p in o.data.splines],'first_faces':[list(p.vertices) for p in list(mesh.polygons)[:7]],'first_vertices':[list(v.co) for v in list(mesh.vertices)[:16]]});e.to_mesh_clear()
 if o.type=='MESH' and o.name.startswith('TRACK'):
  m=o.modifiers[0];socket=next(x for x in m.node_group.interface.items_tree if x.item_type=='SOCKET' and x.name=='Realize for Export');getattr(m.properties.inputs,socket.identifier).value=True;o.update_tag();bpy.context.view_layer.update();e=o.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=e.to_mesh();out.append({'name':o.name,'verts':len(mesh.vertices),'source_vertices':len(o.data.vertices),'polys':len(mesh.polygons),'first_vertices':[list(v.co) for v in list(mesh.vertices)[:6]]});e.to_mesh_clear()
(R/'geometry_probe.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8');print('GEOMETRY_PROBE_READY',[(o['name'],o['verts'],o.get('bevel_resolution')) for o in out],flush=True)
