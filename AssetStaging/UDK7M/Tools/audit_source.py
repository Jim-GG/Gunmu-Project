import bpy,json
from pathlib import Path
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M')
s=bpy.context.scene
def curves(action):
 if not action:return []
 if hasattr(action,'fcurves'):return list(action.fcurves)
 return [f for layer in action.layers for strip in layer.strips for bag in strip.channelbags for f in bag.fcurves]
report={'source':bpy.data.filepath,'scene':s.name,'frames':[s.frame_start,s.frame_end],'fps':s.render.fps/s.render.fps_base,'actions':[],'objects':[],'materials':[]}
for a in bpy.data.actions:
 fs=curves(a);report['actions'].append({'name':a.name,'frame_range':list(a.frame_range),'users':a.users,'fake_user':a.use_fake_user,'slots':[slot.identifier for slot in a.slots],'fcurves':len(fs),'paths':list({f.data_path for f in fs})[:35]})
for o in s.objects:
 if o.type not in ['MESH','CURVE','ARMATURE']:continue
 p={'name':o.name,'type':o.type,'hidden_render':o.hide_render,'collections':[c.name for c in o.users_collection],'parent':o.parent.name if o.parent else None,'parent_bone':o.parent_bone,'location':list(o.location),'modifiers':[{'name':m.name,'type':m.type} for m in o.modifiers],'materials':[m.name for m in o.data.materials] if hasattr(o.data,'materials') else []}
 if o.animation_data:p['action']=o.animation_data.action.name if o.animation_data.action else None;p['nla']=[{'name':t.name,'mute':t.mute,'strips':[{'name':n.name,'action':n.action.name if n.action else None,'start':n.frame_start,'end':n.frame_end} for n in t.strips]} for t in o.animation_data.nla_tracks]
 if o.type=='ARMATURE':p['bones']=[{'name':b.name,'parent':b.parent.name if b.parent else None,'deform':b.use_deform} for b in o.data.bones]
 if o.type=='MESH':o.data.calc_loop_triangles();p['triangles']=len(o.data.loop_triangles);p['uv']=[v.name for v in o.data.uv_layers]
 report['objects'].append(p)
for m in bpy.data.materials:
 if not m.use_nodes:continue
 report['materials'].append({'name':m.name,'users':m.users,'nodes':[{'name':n.name,'type':n.type,'image':n.image.name if n.type=='TEX_IMAGE' and n.image else None} for n in m.node_tree.nodes],'bsdf':[{i.name:str(i.default_value) if not i.is_linked else 'LINKED'} for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED' for i in n.inputs if i.name in ['Base Color','Metallic','Roughness','Normal','Emission Color','Emission Strength']]})
(R/'source_audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('UDK_AUDIT_READY',report['frames'],[(a['name'],a['frame_range'],a['fcurves']) for a in report['actions']],len(report['objects']),flush=True)
