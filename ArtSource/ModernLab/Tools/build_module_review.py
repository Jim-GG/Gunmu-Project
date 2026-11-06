"""Review the user's actual meshes, including all four stair/handrail pairs."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path('D:/UNITY工程/棍木项目/棍木项目/ArtSource/ModernLab')
original=bpy.data.scenes['Modular Asset Kit']
review=bpy.data.scenes.new('Module Review');bpy.context.window.scene=review
layout=[]
def copy_source(name,delta):
 source=original.objects[name];o=source.copy();o.data=source.data;review.collection.objects.link(o);o.name='Review / '+name;o.location+=Vector(delta)
 layout.append({'source':name,'location':list(o.location),'role':o['lab_role']})
 return o
pairs=[('立方体','3*1.5楼梯护栏',(-5,0,0)),('立方体.001','3*1.2楼梯护栏上尽头',(0,-5.58835220336914,0)),('立方体.003','3*1.2楼梯护栏下尽头',(-5,-5.3720569610596,0)),('立方体.005','3*1.2楼梯护栏双尽头',(3,-10.3720569610596,0))]
for stair,rail,delta in pairs:copy_source(stair,delta);copy_source(rail,delta)
for name in ['6*1.2护栏','平面.001','平面.002','平面.003','平面.004']:copy_source(name,(13,-1,0))
for name,centre in [('平面',(2,0)),('平面.017',(8,0)),('平面.018',(2,6)),('平面.019',(13,-1))]:
 source=original.objects[name];copy_source(name,(centre[0]-source.location.x,centre[1]-source.location.y,0))
for name,x,y in [('平面.007',0,9),('平面.006',5,9),('平面.014',10,9),('平面.012',15,9)]:
 source=original.objects[name];copy_source(name,(x-source.location.x,y-source.location.y,0))
world=bpy.data.worlds.new('Module Review Studio');world.use_nodes=True;bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.32,.37,.42,1);bg.inputs[1].default_value=.45;review.world=world
def area(name,position,target,energy,size,size_y):
 data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='RECTANGLE';data.size=size;data.size_y=size_y
 o=bpy.data.objects.new(name,data);review.collection.objects.link(o);o.location=position;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('Large softbox',(2,-3,8),(4,2,1),2300,7,4)
area('Metal rim strip',(11,3,6),(7,1,1.2),2000,1,9)
area('Rear broad light',(0,8,7),(6,3,1.5),1700,6,4)
for position in [(4,-1,5),(12,2,5)]:
 # Emissive strips make the curved steel reflections readable.
 bpy.ops.mesh.primitive_plane_add(size=2,location=position);o=bpy.context.object;o.name='Review reflection strip';o.scale=(3,.2,1);o.data.materials.append(bpy.data.materials['Preview_Light'])
bpy.ops.object.camera_add(location=(-7,-13,9));camera=bpy.context.object;camera.name='Actual module overview';camera.rotation_euler=(Vector((7,2,1.1))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=43;review.camera=camera
review.render.engine='CYCLES';review.cycles.samples=48;review.cycles.use_denoising=True;review.render.resolution_x=1600;review.render.resolution_y=1100;review.render.resolution_percentage=100;review.render.image_settings.file_format='PNG';review.render.filepath=str(root/'Preview/ModernLab_ActualModules.png');review.view_settings.view_transform='AgX'
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
 for d in prefs.devices:d.use=d.type!='CPU'
 if any(d.type!='CPU' for d in prefs.devices):review.cycles.device='GPU'
except Exception:pass
report={'objects':layout,'camera':{'position':list(camera.location),'target':[7,2,1.1],'fov':math.degrees(camera.data.angle)},'source_scene':'Modular Asset Kit','review_scene':'Module Review'}
(root/'review_layout.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
# Open on the real assets. The source scene retains its original layout.
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ModernLab_ModularKit.blend'),compress=True)
bpy.ops.render.render(write_still=True)
camera.location=(-3.8,-5.5,3.8);camera.rotation_euler=(Vector((1.5,0,1.35))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=52
review.render.filepath=str(root/'Preview/ModernLab_ActualHandrail.png');bpy.ops.render.render(write_still=True)
print('MODERN_LAB_ACTUAL_MODULES_REVIEWED',len(layout),flush=True)
