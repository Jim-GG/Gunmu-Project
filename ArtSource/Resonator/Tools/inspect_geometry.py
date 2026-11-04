import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path(r'D:\UNITY工程\棍木项目\棍木项目\ArtSource\Resonator')
scene=bpy.context.scene
rig=bpy.data.objects['scientist_hurt_ARM.001']
meshes=[o for o in scene.objects if o.type=='MESH']
points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));scale=1.8/(hi.z-lo.z)
info={'scale':scale,'origin':list(center),'rigRest':{},'rigPose':{},'objects':{}}
for b in rig.data.bones:info['rigRest'][b.name]={'head':list((rig.matrix_world@b.head_local-center)*scale),'tail':list((rig.matrix_world@b.tail_local-center)*scale)}
for b in rig.pose.bones:info['rigPose'][b.name]={'head':list((rig.matrix_world@b.head-center)*scale),'tail':list((rig.matrix_world@b.tail-center)*scale)}
for o in meshes:
 info['objects'][o.name]={'matrix': [list(r) for r in o.matrix_world], 'vertices':[list((o.matrix_world@v.co-center)*scale) for v in o.data.vertices]}
(root/'Source'/'geometry.json').write_text(json.dumps(info),encoding='utf8')
for o in list(scene.objects):
 if o.type in {'CAMERA','LIGHT'}:o.hide_render=True
scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.resolution_x=720;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Inspection World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.18,.20,.25,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
for name,loc,energy,size in [('key',(-4,-6,9),1600,5),('fill',(5,-2,5),900,4),('rim',(0,4,8),1800,3)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,3))-o.location).to_track_quat('-Z','Y').to_euler()
c=bpy.data.cameras.new('Inspect Camera');o=bpy.data.objects.new('Inspect Camera',c);scene.collection.objects.link(o);o.location=(8,-14,7);target=Vector((.1,-.2,3));o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();c.type='ORTHO';c.ortho_scale=7.4;scene.camera=o
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(root/'Preview'/'Scientist_Before.png');bpy.ops.render.render(write_still=True)
