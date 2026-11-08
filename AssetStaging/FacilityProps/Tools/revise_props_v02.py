"""V02: black iron Soviet frame, two wooden crates and six functional exit designs.
Runs against the V01 review file. Writes only AssetStaging/FacilityProps/V02.
"""
import bpy, math, json, ast, random, re
import numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path('D:/UNITY工程/棍木项目/棍木项目');BASE=ROOT/'AssetStaging/FacilityProps';OUT=BASE/'V02';TEX=OUT/'Textures'
scene=bpy.context.scene;assets=bpy.data.collections['Assets · 1m crates and exit lights'];stage=bpy.data.collections['Review studio · not exported']
old_manifest=json.loads((BASE/'manifest.json').read_text(encoding='utf-8'));mat_config=old_manifest['materials'];parts=[];models={};r=random.Random(426);font=bpy.data.fonts['Arial Bold']
tree=ast.parse((BASE/'Tools/build_props.py').read_text(encoding='utf-8'))
helpers={'load_image','layered','simple','planar_uv','add','box','cylinder','label_quad','finish','emission_mat','sign_material'}
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in helpers],type_ignores=[]),'existing_prop_helpers','exec'))
white=bpy.data.materials['Lab_Paint_WhiteSteel'];dark=bpy.data.materials['Lab_Dark_Panel'];steel=bpy.data.materials['Lab_Rail_BrushedSteel'];green=bpy.data.materials['SV_Paint_Green'];ivory=bpy.data.materials['SV_Paint_Ivory'];rust=bpy.data.materials['SV_Metal_Rust'];rubber=bpy.data.materials['FP_Rubber_Gasket'];cyan=bpy.data.materials['FP_Cyan_Status']
for entry in old_manifest['models']:
 name=entry['name']
 if '_Exit_' in name:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
 else:
  obj=bpy.data.objects[name];models[name]={**{k:v for k,v in entry.items() if k!='name'},'object':obj}

# Tileable PBR fields. Colour, relief and roughness are baked, so Unity needs no procedural nodes.
N=1024;axis=np.arange(N,dtype=np.float32)/N;U,V=np.meshgrid(axis,axis)
def noise(seed,nx,ny=None):
 ny=ny or nx;g=np.random.default_rng(seed).random((ny,nx),dtype=np.float32);x=U*nx;y=V*ny;xi=x.astype(np.int32);yi=y.astype(np.int32);fx=x-xi;fy=y-yi;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
 return (g[yi%ny,xi%nx]*(1-fx)+g[yi%ny,(xi+1)%nx]*fx)*(1-fy)+(g[(yi+1)%ny,xi%nx]*(1-fx)+g[(yi+1)%ny,(xi+1)%nx]*fx)*fy
def smooth(lo,hi,x):
 t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)
def save(name,data,color=False):
 data=np.asarray(data,np.float32)
 if data.ndim==2:data=np.stack([data]*3,axis=2)
 if data.shape[2]==3:data=np.concatenate([data,np.ones((N,N,1),np.float32)],axis=2)
 im=bpy.data.images.new(name,width=N,height=N,alpha=True);im.colorspace_settings.name='sRGB' if color else 'Non-Color';im.alpha_mode='CHANNEL_PACKED';im.pixels.foreach_set(np.clip(data,0,1).ravel());im.filepath_raw=str(TEX/(name+'.png'));im.file_format='PNG';im.save();im.pack();return im
def normal(height):
 dx=(np.roll(height,-1,1)-np.roll(height,1,1))*N*.5;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*N*.5
 vec=np.stack([-dx,-dy,np.ones_like(dx)],axis=2);vec/=np.linalg.norm(vec,axis=2)[:,:,None];return vec*.5+.5
def surface(name,col,height,rough,metal=0,spots=0,scale=1,micro=.12):
 spot=np.broadcast_to(spots,(N,N));base=save(name+'_BaseColor',np.concatenate([col,spot[:,:,None]],axis=2),True);nm=save(name+'_Normal',normal(height));dust=.18+noise(197,37)*.12
 mask=save(name+'_Mask',np.stack([np.broadcast_to(metal,(N,N)),np.ones((N,N),np.float32),dust,1-np.clip(rough,.05,.98)],axis=2))
 p={'name':name,'textures':{'BaseMap':str(Path(base.filepath).relative_to(ROOT)),'BumpMap':str(Path(nm.filepath).relative_to(ROOT)),'SurfaceMask':str(Path(mask.filepath).relative_to(ROOT)),'MicroNormal':'Assets/Game/Environment/ModernLab/Textures/Lab_Detail_Coating_Normal.png'},'values':{'DustAmount':.30,'DustColourStrength':.025,'DustRoughness':.80,'DustTiling':2.5,'SpotAmount':1,'NormalStrength':1,'MicroStrength':micro,'MicroTiling':8,'RoughnessBias':0,'SurfaceTiling':scale},'colors':{'BaseColor':[1,1,1,1],'DustColour':[.20,.205,.19,1]}}
 return layered(name,p)
cloud=noise(81,5)*.65+noise(82,19)*.35;fine=noise(83,360)-.5
rust_mask=smooth(.78,.92,noise(84,53))*.45
iron_col=np.stack([.026+(cloud-.5)*.015,.031+(cloud-.5)*.017,.034+(cloud-.5)*.018],axis=2);iron_col=iron_col*(1-rust_mask[:,:,None])+np.array([.10,.041,.014])*rust_mask[:,:,None]
iron=surface('FP_Black_Iron',iron_col,fine*.000055,.55+(cloud-.5)*.15+rust_mask*.20,.83*(1-rust_mask),scale=2,micro=.15)
soviet=models['SV_Crate_1m']['object']
for i,mat in enumerate(soviet.data.materials):
 if mat.name=='SV_Paint_Yellow':soviet.data.materials[i]=iron
models['SV_Crate_1m']['materials']=[m.name for m in soviet.data.materials]
models['SV_Crate_1m']['description']='Revised green steel crate with black iron corners, cap plates and latch pulls; no orange trim'

def wood_fields(seed):
 warp=noise(seed,4,13)*.55+noise(seed+1,9,31)*.10
 growth=V*19+.65*np.sin(U*math.tau)+warp
 rings=(.5+.5*np.sin(growth*math.tau))**10
 fibres=noise(seed+2,6,285)*.55+noise(seed+3,18,520)*.45
 knots=np.zeros((N,N),np.float32)
 for cx,cy in [(.24,.32),(.76,.73)]:
  dx=np.minimum(abs(U-cx),1-abs(U-cx))/.15;dy=np.minimum(abs(V-cy),1-abs(V-cy))/.030;d=np.sqrt(dx*dx+dy*dy)
  knots+=np.exp(-d*d*2)*.50+np.exp(-d*d*.22)*(.5+.5*np.sin(d*13))*.15
 return rings,fibres,np.clip(knots,0,1)
rings,fibres,knots=wood_fields(220)
variation=(cloud-.5)*.075+(fibres-.5)*.065-rings*.027-knots*.10
ply_col=np.stack([.58+variation,.43+variation*.78,.27+variation*.48],axis=2)
ply=surface('FP_Modern_Birch_Plywood',ply_col,(fibres-.5)*.00006-rings*.000045-knots*.00010,.57+(fibres-.5)*.10+knots*.06,micro=.08)
frame_col=np.stack([.51+variation,.355+variation*.72,.20+variation*.42],axis=2)
pine=surface('FP_Modern_Frame_Pine',frame_col,(fibres-.5)*.00011-rings*.00007,.62+(fibres-.5)*.12,micro=.10)
rings,fibres,knots=wood_fields(330)
weather=noise(338,4,50)*.6+noise(339,12,180)*.4
old_var=(cloud-.5)*.055+(fibres-.5)*.065-rings*.025-knots*.10
timber=np.stack([.26+old_var,.205+old_var*.84,.132+old_var*.65],axis=2)
oldwood=surface('FP_Soviet_Aged_Timber',timber,(fibres-.5)*.00020-rings*.00010-knots*.00015,.80+(fibres-.5)*.10,spots=smooth(.75,.9,cloud)*.12,micro=.12)
loss=smooth(.65,.80,weather)*.65+knots*.12
wash=np.stack([.48+(cloud-.5)*.03,.49+(cloud-.5)*.035,.455+(cloud-.5)*.03],axis=2);wash=wash*(1-loss[:,:,None])+timber*loss[:,:,None]
whitewood=surface('FP_Soviet_Whitewashed_Wood',wash,(fibres-.5)*.00018-rings*.00007-loss*.00025,.74+loss*.12+(fibres-.5)*.08,spots=smooth(.70,.88,cloud)*.16,micro=.10)

def woodbox(name,loc,size,mat,grain='X',edge=.003):
 o=box(name,loc,size,mat,edge);uv=o.data.uv_layers['UV0'];g='XYZ'.index(grain);offset=(r.uniform(0,1),r.uniform(0,1))
 if mat==whitewood:
  o.data.materials.append(oldwood)
  for face in o.data.polygons:
   if max(abs(v) for v in face.normal)<.999:face.material_index=1
 for face in o.data.polygons:
  normal_axis=max(range(3),key=lambda i:abs(face.normal[i]));flat=[i for i in range(3) if i!=normal_axis];along=g if g in flat else flat[0];across=next(i for i in flat if i!=along)
  for li in face.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(co[along]+offset[0],co[across]+offset[1])
 return o

# Modern reference: plywood shipping case with solid timber frame, corner sheet brackets and feet.
woodbox('Continuous plywood case',(0,0,.545),(.92,.92,.85),ply,'X',.003)
for x in [-.456,.456]:
 for y in [-.456,.456]:woodbox('Pine corner stile',(x,y,.548),(.085,.085,.875),pine,'Z')
for side in [-1,1]:
 for z in [.155,.913]:
  woodbox('Front and rear framing rail',(0,side*.466,z),(.91,.074,.10),pine,'X')
  woodbox('Side framing rail',(side*.466,0,z),(.074,.84,.10),pine,'Y')
 for z in [.28,.55,.81]:
  for x in [-.456,.456]:cylinder('Flush frame screw',(x,side*.505,z),.005,.003,steel,vertices=6)
 for z in [.155,.913]:
  for x in [-.28,0,.28]:cylinder('Framing rail screw',(x,side*.505,z),.005,.003,steel,vertices=6)
woodbox('Inset plywood lid',(0,0,.974),(.914,.914,.020),ply,'X',.002)
for x in [-.455,.455]:
 for y in [-.455,.455]:
  for z in [.178,.907]:
   # Real thin L-shaped steel protectors, seated on the timber rather than floating blocks.
   box('Corner bracket front',(x,y+math.copysign(.048,y),z),(.106,.005,.126),steel,.002)
   box('Corner bracket side',(x+math.copysign(.048,x),y,z),(.005,.106,.126),steel,.002)
  box('Top bracket return',(x,y,.989),(.106,.106,.007),steel,.002)
for x in [-.37,0,.37]:
 for y in [-.35,.35]:woodbox('Fork clearance block',(x,y,.052),(.12,.17,.104),pine,'Y',.002)
modern_wood=finish('ML_WoodCrate_1m','ModernLab','Pale birch plywood shipping crate with timber rails, flush fasteners, steel corner brackets and forklift feet; no lettering',[1.02,1.02,1.0])
modern_wood['Pivot']='bottom centre'

# Soviet reference: old whitewashed horizontal planks, bare timber lid, black hardware; no markings.
woodbox('Timber core',(0,0,.428),(1.035,.835,.67),oldwood,'X',.004)
for side in [-1,1]:
 for row in range(4):
  z=.18+row*.166;woodbox('Whitewashed front plank',(0,side*.436,z),(1.07,.038,.164),whitewood,'X',.002)
  woodbox('Whitewashed side plank',(side*.535,0,z),(.038,.835,.164),whitewood,'Y',.002)
  # Small alternating exposed endgrain joints at the corners.
  if row%2==0:
   for x in [-.516,.516]:woodbox('Exposed finger joint',(x,side*.457,z),(.032,.003,.078),oldwood,'Z',.0005)
 for x in [-.32,.32]:
  box('Toggle mounting plate',(x,side*.468,.704),(.070,.018,.11),iron,.003)
  box('Latch pull',(x,side*.484,.706),(.037,.015,.065),steel,.003)
  cylinder('Latch hinge pin',(x,side*.493,.741),.009,.06,iron,axis=(1,0,0),vertices=8)
 # Side carrying handle is a short U with all feet attached.
 box('Carry handle grip',(side*.575,0,.489),(.026,.25,.033),rubber,.005)
 for y in [-.124,.124]:box('Carry handle mounting',(side*.556,y,.485),(.042,.032,.080),iron,.003)
 for x in [-.489,.489]:
  for z in [.18,.678]:cylinder('Old plank fastener',(x,side*.458,z),.0055,.003,iron,vertices=6)
for y in [-.335,-.168,0,.168,.335]:woodbox('Bare timber lid plank',(0,y,.777),(1.10,.166,.038),oldwood,'X',.002)
for x in [-.34,.34]:woodbox('Lid retaining batten',(x,0,.814),(.085,.89,.036),oldwood,'Y',.003)
for x in [-.37,.37]:woodbox('Old timber foot',(x,0,.042),(.10,.71,.084),oldwood,'Y',.003)
for side in [-1,1]:
 for x in [-.51,.51]:
  for z in [.16,.68]:
   box('Black iron angle plate',(x,side*.460,z),(.080,.005,.13),iron,.002)
   for dz in [-.04,.04]:cylinder('Angle plate rivet',(x,side*.466,z+dz),.0055,.004,rust,vertices=6)
soviet_wood=finish('SV_WoodCrate_Old','SovietFacility','Whitewashed plank crate with bare weathered timber lid, black iron corners and carry handles; deliberately no lettering',[1.18,.99,.84])
soviet_wood['Pivot']='bottom centre'

# Bake exit artwork with the supplied bitmap used directly as the modern pictogram.
logo=load_image(OUT/'References/Exit_Pictogram_User.png')
def exit_atlas(prefix,variant,direction='right'):
 aged=prefix=='SV';name=f'{prefix}_Exit_{variant}_{direction}'
 sc=bpy.data.scenes.new('BAKE '+name);bpy.context.window.scene=sc;sc.render.engine='CYCLES';sc.cycles.samples=1;sc.render.resolution_x=1024;sc.render.resolution_y=256;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.view_settings.view_transform='Standard';sc.view_settings.look='None'
 sc.world=bpy.data.worlds.new('Atlas world');sc.world.color=(0,0,0)
 ink=(.83,.82,.70) if aged else (.88,.94,.90);paper=(.016,.105,.044) if aged else (.013,.185,.102)
 def shape(name,points,color,z=.01):
  mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,y,z) for x,y in points],[],[tuple(range(len(points)))]);mesh.update();obj=bpy.data.objects.new(name,mesh);sc.collection.objects.link(obj);mesh.materials.append(emission_mat(name,color));return obj
 def rect(x,y,w,h,color,z=.01):return shape('Graphic rectangle',[(x,y),(x+w,y),(x+w,y+h),(x,y+h)],color,z)
 def text(body,x,y,size):
  curve=bpy.data.curves.new(body,'FONT');curve.body=body;curve.font=font;curve.size=size;curve.align_x='CENTER';curve.resolution_u=2;obj=bpy.data.objects.new(body,curve);sc.collection.objects.link(obj);obj.location=(x,y,.025);curve.materials.append(emission_mat('Lettering',ink))
 rect(0,0,4,1,paper,0)
 if aged:
  for i in range(95):rect(r.uniform(.06,3.93),r.uniform(.06,.93),r.uniform(.002,.012),r.uniform(.002,.006),(.022,.06,.028),.002)
 else:
  pict=rect(.16,.12,.76,.76,(1,1,1),.018);pict.data.materials.clear();pict.data.materials.append(emission_mat('User supplied pictogram',(1,1,1),logo));uv=pict.data.uv_layers.new(name='UVMap')
  # The supplied figure exits left. Flip only this icon's UV for a right arrow.
  coords=[(.04,.04),(.96,.04),(.96,.96),(.04,.96)]
  if variant=='Side' and direction=='right':coords=[(1-x,y) for x,y in coords]
  for i,p in enumerate(coords):uv.data[i].uv=p
 arrow=variant!='Door'
 if aged:text('ВЫХОД',1.65 if arrow else 2.,.295,.64 if arrow else .74)
 else:text('EXIT',2.0 if arrow else 2.40,.30,.64 if arrow else .72)
 if arrow:
  points=[(-.46,-.105),(.06,-.105),(.06,-.29),(.46,0),(.06,.29),(.06,.105),(-.46,.105)]
  angle=math.pi/2 if variant=='Ceiling' else math.pi if direction=='left' else 0
  arrow_scale=.82 if variant=='Ceiling' else 1.
  shape('Direction arrow',[(3.45+arrow_scale*(x*math.cos(angle)-y*math.sin(angle)),.50+arrow_scale*(x*math.sin(angle)+y*math.cos(angle))) for x,y in points],ink,.02)
 rect(.09,.075,3.82,.012,ink);rect(.09,.915,3.82,.012,ink)
 cam_data=bpy.data.cameras.new('Atlas camera');cam=bpy.data.objects.new('Atlas camera',cam_data);sc.collection.objects.link(cam);cam.location=(2,.5,5);cam_data.type='ORTHO';cam_data.ortho_scale=4;sc.camera=cam;path=TEX/(name+'.png');sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
 result=load_image(path);bpy.context.window.scene=scene;bpy.data.scenes.remove(sc);return result

side_alternates={}
for prefix in ['ML','SV']:
 for variant in ['Side','Ceiling','Door']:
  aged=prefix=='SV';front=sign_material(f'FP_{prefix}_Exit_{variant}',exit_atlas(prefix,variant),aged)
  shell=green if aged else white;edge=iron if aged else rubber
  box('Lightbox housing',(0,0,0),(.96,.10,.28),shell,.010)
  box('Recessed lens surround',(0,-.054,0),(.906,.010,.234),edge,.003)
  label_quad('Baked exit lettering',(0,-.060,0),.88,.22,front)
  for x in [-.456,.456]:
   for z in [-.108,.108]:cylinder('Flush cover screw',(x,-.055,z),.006,.003,iron if aged else steel,vertices=6)
  if variant=='Ceiling':
   box('Rear lens surround',(0,.054,0),(.906,.010,.234),edge,.003);label_quad('Rear exit lettering',(0,.060,0),.88,.22,front,'back')
   for x in [-.27,.27]:
    if aged:box('Steel hanging strap',(x,0,.311),(.022,.025,.342),iron,.003)
    else:cylinder('Suspension rod',(x,0,.311),.008,.342,steel,axis=(0,0,1),vertices=8)
    box('Suspension foot',(x,0,.145),(.055,.055,.020),shell,.003)
   box('Ceiling mounting canopy',(0,0,.494),(.66,.12,.028),shell,.005)
  else:
   box('Wall mounting plate',(0,.057,0),(.77,.020,.15),iron if aged else dark,.003)
   if variant=='Door':
    for x in [-.34,.34]:box('Lintel mounting tab',(x,.051,-.157),(.055,.022,.064),shell,.003)
  if aged:
   for x in [-.22,.22]:box('Lens retaining tab',(x,-.060,.124),(.038,.017,.030),ivory,.002)
  else:
   box('Upper dark trim',(0,0,.134),(.80,.072,.012),dark,.003);box('Test status lens',(.432,-.057,-.066),(.017,.004,.007),cyan,.001)
  name=f'{prefix}_Exit_{variant}';obj=finish(name,'SovietFacility' if aged else 'ModernLab',f'{variant} exit sign; '+('Russian text only, no person' if aged else 'Exact user-supplied running-person pictogram'),[.96,.12,.28])
  if variant=='Ceiling':
   obj.data.transform(Matrix.Translation((0,0,-.508)));obj['Pivot']='ceiling mounting surface';models[name]['collider']=[.96,.12,.648];models[name]['collider_center']=[0,0,-.324]
  else:obj['Pivot']='wall plate centre'
  obj['Variant']=variant;models[name]['mount']=variant;models[name]['has_pictogram']=not aged;models[name]['arrow']='up / forward' if variant=='Ceiling' else 'none' if variant=='Door' else 'right; alternate left material included'
  if variant=='Side':
   alternate=sign_material(f'FP_{prefix}_Exit_Side_Left',exit_atlas(prefix,variant,'left'),aged);alternate.use_fake_user=True
   side_alternates[name]={'default':front.name,'left':alternate.name};obj['Left Arrow Material']=alternate.name

# Organised review scenes: metal, wood, and a 2-column / 3-row sign comparison.
def review_scene(name):
 sc=bpy.data.scenes.new(name);sc.world=scene.world.copy();sc.unit_settings.system='METRIC';sc.unit_settings.scale_length=1;sc.render.engine='CYCLES';sc.cycles.samples=40;sc.cycles.use_denoising=True;sc.cycles.device=scene.cycles.device;sc.render.resolution_x=1600;sc.render.resolution_y=1100;sc.render.resolution_percentage=100;sc.view_settings.view_transform='AgX'
 col=bpy.data.collections.new(name+' assets');sc.collection.children.link(col)
 for original in stage.objects:
  copy=original.copy();copy.data=original.data.copy() if original.type in ['CAMERA','LIGHT'] else original.data;sc.collection.objects.link(copy)
  if copy.type=='CAMERA':sc.camera=copy
 return sc,col
scene.name='01 · Metal Crates · Black Iron Revision'
wood_scene,wood_col=review_scene('02 · Wooden Crates')
sign_scene,sign_col=review_scene('03 · Exit Signs · Six Designs')
for name,entry in models.items():
 obj=entry['object'];obj.hide_render=False
 if 'WoodCrate' in name:
  for col in list(obj.users_collection):col.objects.unlink(obj)
  wood_col.objects.link(obj);obj.location=(-.76,0,0) if name.startswith('ML') else (.76,0,0)
 elif '_Exit_' in name:
  for col in list(obj.users_collection):col.objects.unlink(obj)
  sign_col.objects.link(obj);row=['Side','Ceiling','Door'].index(entry['mount']);obj.location=(-.70 if name.startswith('ML') else .70,0,1.75-row*.66+(.508 if entry['mount']=='Ceiling' else 0))
 else:obj.location=(-.76,0,0) if name.startswith('ML') else (.76,0,0)
def camera(sc,position,target,lens):
 obj=sc.camera;obj.location=position;obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler();obj.data.lens=lens
camera(scene,(3.1,-5.3,2.8),(0,0,.48),56);camera(wood_scene,(3.0,-5.3,2.8),(0,0,.45),56)
camera(sign_scene,(0,-5.5,1.55),(0,0,1.1),57)
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 for sc in [scene,wood_scene,sign_scene]:sc.cycles.device='GPU'
except Exception:pass

# Independent FBXs remain outside Assets until explicit approval.
try:bpy.ops.export_scene.fbx.get_rna_type()
except Exception:bpy.ops.preferences.addon_enable(module='io_scene_fbx')
bpy.context.window.scene=scene
for name,entry in models.items():
 source=entry['object'];source.data.calc_loop_triangles();entry['triangles']=len(source.data.loop_triangles);entry['materials']=[m.name for m in source.data.materials]
 copy=bpy.data.objects.new(name+'_EXPORT',source.data.copy());scene.collection.objects.link(copy);copy.matrix_world=Matrix.Identity(4);copy.hide_render=True;copy.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.select_all(action='DESELECT');copy.select_set(True);bpy.context.view_layer.objects.active=copy
 assert len(copy.data.uv_layers)==1 and copy.data.uv_layers[0].name=='UV0'
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Exports'/(name+'.fbx')),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP');bpy.data.objects.remove(copy,do_unlink=True)
used={m.name for entry in models.values() for m in entry['object'].data.materials}|{v['left'] for v in side_alternates.values()}
manifest={'status':'AWAITING_V02_APPROVAL_NOT_IMPORTED','units':'metres','materials':{n:mat_config[n] for n in used},'models':[{k:v for k,v in entry.items() if k!='object'}|{'name':name,'pivot':entry['object'].get('Pivot','')} for name,entry in models.items()],'side_arrow_material_options':side_alternates,'exit_design_count':6,'wood_crate_text':False,'total_triangles':sum(e['triangles'] for e in models.values()),'notes':['Six sign bodies: side / ceiling forward / door no arrow, for each facility','Side bodies support left/right by swapping the included lens material','Hanging signs have two correctly readable faces and ceiling-plane pivots','Modern pictogram uses the user image directly; Soviet signs contain no person','All four crates retained; Soviet metal frame now black iron; wood crates have no text','Only staged files; Unity Assets unchanged']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.window.scene=wood_scene
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FacilityProps_Review_v02.blend'),compress=True)
for sc,filename in [(scene,'01_Metal_Crates_Black_Iron.png'),(wood_scene,'02_Wood_Crates.png'),(sign_scene,'03_Exit_Six_Designs.png')]:
 bpy.context.window.scene=sc;sc.render.filepath=str(OUT/'Preview'/filename);bpy.ops.render.render(write_still=True)
print('PROPS_V02_REVIEW_READY',len(models),manifest['total_triangles'],flush=True)
