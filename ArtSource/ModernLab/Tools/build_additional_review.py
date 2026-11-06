"""Grey/white floor, acoustic ceiling, ribbed wall and coloured wall modules."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path('D:/UNITY工程/棍木项目/棍木项目/ArtSource/ModernLab')
source=bpy.data.scenes['Modular Asset Kit'];additional=bpy.data.scenes['Additional Modules']
scene=bpy.data.scenes.new('Additional Surface Review');bpy.context.window.scene=scene;layout=[]
def place(name,location):
 original=source.objects.get(name) or additional.objects[name];o=original.copy();o.data=original.data;scene.collection.objects.link(o);o.name='Surface Review / '+name;o.location=location
 layout.append({'source':name,'location':list(o.location),'role':o['lab_role']});return o
place('平面.018',(-3,0,0));place('平面.019',(3,0,0))
place('分区墙_Lime',(-3,3,0));place('分区墙_Red',(0,3,0));place('分区墙_Yellow',(3,3,0))
place('吊顶_6m',(-1.5,0,3.4));place('吊顶_3m',(3,1.5,3.4))
place('平面.014',(5.6,3,2));place('6*1.2护栏',(3,6,0))
world=bpy.data.worlds.new('Surface Review Studio');world.use_nodes=True;bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.24,.27,.3,1);bg.inputs[1].default_value=.22;scene.world=world
for x in [-3,0,3]:
 data=bpy.data.lights.new('Under ceiling light','AREA');data.energy=240;data.shape='RECTANGLE';data.size=.35;data.size_y=3
 lamp=bpy.data.objects.new('Under ceiling light',data);scene.collection.objects.link(lamp);lamp.location=(x,.1,3.18)
 bpy.ops.mesh.primitive_plane_add(size=2,location=(x,.1,3.23));o=bpy.context.object;o.name='Ceiling luminous strip';o.scale=(.08,1.45,1);o.rotation_euler.x=math.pi;o.data.materials.append(bpy.data.materials['Preview_Light'])
data=bpy.data.lights.new('Soft frontal fill','AREA');data.energy=450;data.shape='RECTANGLE';data.size=8;data.size_y=4;lamp=bpy.data.objects.new('Soft frontal fill',data);scene.collection.objects.link(lamp);lamp.location=(0,-4,5);lamp.rotation_euler=(Vector((0,2,1.2))-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(9,-13,2.25));camera=bpy.context.object;camera.name='Additional surface camera';target=Vector((.7,1.3,1.4));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=44;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True;scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(root/'Preview/ModernLab_AdditionalSurfaces.png');scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=-.3
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
 for d in prefs.devices:d.use=d.type!='CPU'
 if any(d.type!='CPU' for d in prefs.devices):scene.cycles.device='GPU'
except Exception:pass
(root/'addon_layout.json').write_text(json.dumps({'objects':layout,'camera':{'position':list(camera.location),'target':list(target),'fov':math.degrees(camera.data.angle)},'aspect':1.6},ensure_ascii=False,indent=2),encoding='utf-8')
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ModernLab_ModularKit.blend'),compress=True)
bpy.ops.render.render(write_still=True)
print('MODERN_LAB_ADDITIONAL_SURFACES_REVIEWED',len(layout),flush=True)
