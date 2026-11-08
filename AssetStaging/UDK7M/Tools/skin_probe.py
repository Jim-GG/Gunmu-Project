import bpy,json
import numpy as np
from pathlib import Path
from mathutils import Vector
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M');rig=bpy.data.objects['UDK7M_RIG'];s=bpy.context.scene
report=json.loads((R/'export_manifest.json').read_text(encoding='utf-8'));rig.data.pose_position='POSE';desc=next(d for d in report['clips'] if d['id']=='Idle');rig.animation_data.action=bpy.data.actions[desc['baked_action']];rig.animation_data.action_slot=rig.animation_data.action.slots[0];s.frame_set(0);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
out={}
for o in s.objects:
 if o.type!='MESH':continue
 e=o.evaluated_get(dg);m=e.to_mesh();coords=[e.matrix_world@v.co for v in m.vertices];out[o.name]=[[-v.x,v.z,-v.y] for v in coords[::30]];e.to_mesh_clear()
(R/'expected_skin.json').write_text(json.dumps(out),encoding='utf-8')
s.render.engine='CYCLES';s.cycles.samples=8;s.render.resolution_x=1000;s.render.resolution_y=800;s.render.resolution_percentage=100
world=bpy.data.worlds.new('Probe world');world.use_nodes=True;world.node_tree.nodes.get('Background').inputs[0].default_value=(.3,.32,.36,1);s.world=world
for pos,power in [((-6,-8,12),2500),((8,5,10),2000)]:
 data=bpy.data.lights.new('Probe softbox','AREA');data.energy=power;data.shape='DISK';data.size=8;o=bpy.data.objects.new(data.name,data);s.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,2.4))-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Probe camera');o=bpy.data.objects.new(data.name,data);s.collection.objects.link(o);o.location=(12,15,8);o.rotation_euler=(Vector((0,0,2.4))-o.location).to_track_quat('-Z','Y').to_euler();data.lens=48;s.camera=o;s.render.filepath=str(R/'Preview/Blender_Portable_Idle.png');bpy.ops.render.render(write_still=True)
