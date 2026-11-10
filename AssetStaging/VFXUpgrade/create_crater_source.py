import bpy,numpy as np, math
from pathlib import Path
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for variant in range(2):
 h=np.load('AssetStaging/VFXUpgrade/crater_height_'+str(variant+1)+'.npy');n=h.shape[0];stride=2;h=h[::stride,::stride];n=h.shape[0];v=[((x/(n-1)-.5)*4.6+variant*5,(y/(n-1)-.5)*4.6,float(h[y,x]-.5)*.5) for y in range(n) for x in range(n)];faces=[(y*n+x,y*n+x+1,(y+1)*n+x+1,(y+1)*n+x) for y in range(n-1) for x in range(n-1)]
 mesh=bpy.data.meshes.new('Procedural sunken drill crater');mesh.from_pydata(v,[],faces);mesh.update();o=bpy.data.objects.new('Drill crater source '+str(variant+1),mesh);bpy.context.collection.objects.link(o)
 mat=bpy.data.materials.new('Grey chipped concrete');mat.diffuse_color=(.35,.34,.31,1);mat.use_nodes=True;bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.35,.34,.31,1);bsdf.inputs['Roughness'].default_value=.85;o.data.materials.append(mat)
 for poly in mesh.polygons:poly.use_smooth=False
world=bpy.context.scene.world;world.color=(.15,.15,.15)
bpy.ops.object.camera_add(location=(6,-7,6));camera=bpy.context.object;camera.rotation_euler=((__import__('mathutils').Vector((2.3,0,0))-camera.location).to_track_quat('-Z','Y').to_euler());bpy.context.scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=12
bpy.ops.object.light_add(type='AREA',location=(0,-3,6));bpy.context.object.data.energy=1300;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=5
bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=24;bpy.context.scene.render.resolution_x=1280;bpy.context.scene.render.resolution_y=720;bpy.context.scene.render.resolution_percentage=100
bpy.ops.wm.save_as_mainfile(filepath=str(Path('AssetStaging/VFXUpgrade/DrillCrater_Sources.blend').resolve()));bpy.context.scene.render.filepath=str(Path('AssetStaging/VFXUpgrade/crater_source_render.png').resolve());bpy.ops.render.render(write_still=True)
