"""Identical framing/light rigs for V1/V2 on actual wall and guardrail meshes."""
import bpy,math,json,sys,numpy as np
from pathlib import Path
from mathutils import Vector
root=Path('D:/UNITY工程/棍木项目/棍木项目/ArtSource/ModernLab');before='--before' in sys.argv;version='V1' if before else 'V2'
old=bpy.data.scenes.get('Surface V2 Inspection')
if old:
 bpy.context.window.scene=bpy.data.scenes['Modular Asset Kit'];bpy.data.scenes.remove(old)
scene=bpy.data.scenes.new('Surface V2 Inspection');bpy.context.window.scene=scene
source=bpy.data.scenes['Additional Modules'];kit=bpy.data.scenes['Modular Asset Kit']
def copy(o,name,loc,material=None):
 new=o.copy();new.data=o.data.copy();new.name=name;scene.collection.objects.link(new);new.location=loc
 if material:new.data.materials.clear();new.data.materials.append(material)
 return new
copy(source.objects['分区墙_Red'],'Rough red and white wall',(-3.1,0,0));copy(source.objects['分区墙_Yellow'],'Gloss white and yellow wall',(0,0,0))
paint=bpy.data.materials.get('Lab_Paint_WhiteSteel') or bpy.data.materials['Lab_Paint_GlossWhite']
copy(kit.objects['6*1.2护栏'],'Painted metal actual guardrail',(4.5,3,0),paint)
copy(kit.objects['平面'],'Actual resin floor',(-.5,-1,-.05))
world=bpy.data.worlds.new('V2 Inspection neutral');world.use_nodes=True;bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.18,.20,.22,1);bg.inputs[1].default_value=.30;scene.world=world
lights=[]
for name,loc,target,energy,sx,sy in [('Raking light',(-5,-.8,2.5),(-1,0,1.5),500,.4,3),('Long reflected softbox',(1,-2.5,2.2),(0,0,1.6),220,.15,3),('Metal side softbox',(5,-1,3),(4,0,1),350,2,.3)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='RECTANGLE';data.size=sx;data.size_y=sy;lamp=bpy.data.objects.new(name,data);scene.collection.objects.link(lamp);lamp.location=loc;lamp.rotation_euler=(Vector(target)-lamp.location).to_track_quat('-Z','Y').to_euler();lights.append({'name':name,'position':list(loc),'target':list(target),'energy':energy,'size':[sx,sy]})
bpy.ops.object.camera_add();camera=bpy.context.object;camera.name='Surface inspection camera';camera.data.lens=55;scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True;scene.render.resolution_x=1280;scene.render.resolution_y=960;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=0
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
 for d in prefs.devices:d.use=d.type!='CPU'
 if any(d.type!='CPU' for d in prefs.devices):scene.cycles.device='GPU'
except Exception:pass
probe_path=root/'surface_spot_probe.json';centre=4.8
if not before:
 image=bpy.data.images['Lab_Paint_WhiteSteel_BaseColor'];data=np.empty(image.size[0]*image.size[1]*4,np.float32);image.pixels.foreach_get(data);alpha=data.reshape(image.size[1],image.size[0],4)[:,:,3];row=int(.6*(image.size[1]-1));strength=alpha[row-3:row+4,:].mean(axis=0);strength[:int(.10*len(strength))]=0;strength[int(.85*len(strength)):]=0;centre=4.5+2*float(np.argmax(strength))/(image.size[0]-1);probe_path.write_text(json.dumps({'centre_x':centre}),encoding='utf-8')
elif probe_path.exists():centre=json.loads(probe_path.read_text())['centre_x']
shots=[('RoughWall',(-2.8,-1.25,1.25),(-2.9,0,1.25)),('DustyGloss',(.15,-1.6,1.6),(.2,0,1.6)),('SpottedMetal',(centre,-.6,1.35),(centre,0,1.20))]
for name,loc,target in shots:
 camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(root/'Preview'/('Surface_'+version+'_'+name+'.png'));bpy.ops.render.render(write_still=True)
if not before:
 # Save the inspection rig for user adjustment, with the rough wall in view.
 camera.location=shots[0][1];camera.rotation_euler=(Vector(shots[0][2])-camera.location).to_track_quat('-Z','Y').to_euler()
 for area in bpy.context.screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL';area.spaces.active.overlay.show_overlays=False
 bpy.ops.wm.save_as_mainfile(filepath=str(root/'ModernLab_ModularKit.blend'),compress=True)
 (root/'surface_inspection_layout.json').write_text(json.dumps({'lights':lights,'shots':[{'name':n,'position':list(p),'target':list(t),'fov':math.degrees(camera.data.angle)} for n,p,t in shots]},indent=2),encoding='utf-8')
print('SURFACE_INSPECTION_RENDERED',version,flush=True)
