"""Author original, periodic PBR maps and bind the user's saved modular kit.
Run using Blender's bundled Python / numpy. No downloaded artwork is reused.
"""
import bpy, numpy as np, json, math, uuid, argparse, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path('D:/UNITY工程/棍木项目/棍木项目')
SOURCE=ROOT/'ArtSource/ModernLab'
OUT=ROOT/'Assets/Game/Environment/ModernLab'
TEX=OUT/'Textures'
for path in [TEX,OUT/'Models',SOURCE/'Preview'] :path.mkdir(parents=True,exist_ok=True)
N=2048
uv=np.linspace(0,1,N,dtype=np.float32)
U,V=np.meshgrid(uv,uv)
rng=np.random.default_rng(127)
stats={}

def periodic_noise(seed,octaves):
 r=np.random.default_rng(seed);a=np.zeros((N,N),np.float32)
 for frequency,weight in octaves:
  for i in range(3):
   kx=int(r.integers(max(1,frequency//2),frequency+1));ky=int(r.integers(max(1,frequency//2),frequency+1))
   a+=np.float32(weight/3)*np.sin(2*np.pi*(kx*U+ky*V)+r.uniform(0,2*np.pi))
 return a

def seam(distance,width):
 return np.exp(-np.square(distance/max(.00001,width))).astype(np.float32)

def grid(countx,county):
 x=np.mod(U*countx,1);y=np.mod(V*county,1)
 return np.minimum(x,1-x)/countx,np.minimum(y,1-y)/county

def save_image(name,channels,color=False):
 path=TEX/(name+'.png')
 data=np.asarray(channels,dtype=np.float32)
 if data.ndim==2:data=np.repeat(data[:,:,None],3,axis=2)
 if data.shape[2]==3:data=np.concatenate([data,np.ones((N,N,1),np.float32)],axis=2)
 # Explicit duplicate border texels make repetition independent of filtering modes.
 data[-1,:,:]=data[0,:,:];data[:,-1,:]=data[:,0,:]
 image=bpy.data.images.new(name,width=N,height=N,alpha=True,float_buffer=False)
 image.colorspace_settings.name='sRGB' if color else 'Non-Color';image.alpha_mode='STRAIGHT' if color else 'CHANNEL_PACKED'
 image.pixels.foreach_set(np.clip(data,0,1).ravel());image.filepath_raw=str(path);image.file_format='PNG';image.save()
 image.pack();return image

def normal_from_height(height):
 # Height is metres; each UV repetition covers 2 x 2 metres.
 du=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*(N-1)/4
 dv=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*(N-1)/4
 normals=np.stack([-du,-dv,np.ones_like(height)],axis=2)
 normals/=np.linalg.norm(normals,axis=2)[:,:,None]
 return normals*.5+.5

low=periodic_noise(2,[(2,.7),(5,.3)])
mid=periodic_noise(3,[(17,.5),(41,.3),(83,.2)])
micro=periodic_noise(4,[(173,.6),(311,.4)])
dx,dy=grid(2,1);joints=np.maximum(seam(dx,.00125),seam(dy,.00125))
edge=np.maximum(seam(dx,.0035),seam(dy,.0035))
dotx=np.sin(U*2*np.pi*256);doty=np.sin(V*2*np.pi*256)
stippling=(np.maximum(0,dotx)*np.maximum(0,doty))**5
white=np.empty((N,N,3),np.float32)
for i,base in enumerate([.815,.826,.827]):white[:,:,i]=base+low*.003+mid*.0015-joints*.46-edge*.012
wr=np.clip(.845+micro*.025+mid*.015+joints*.10,.72,.97)
wh=-joints*.00055+stippling*.000018+micro*.000008
wa=np.clip(1-edge*.07-joints*.10,.8,1)

dx,dy=grid(2,2);dj=np.maximum(seam(dx,.0016),seam(dy,.0016))
de=np.maximum(seam(dx,.004),seam(dy,.004))
fasteners=np.zeros((N,N),np.float32)
for x in [.045,.455,.545,.955]:
 for y in [.045,.455,.545,.955]:
  q=np.sqrt(np.minimum(abs(U-x),1-abs(U-x))**2+np.minimum(abs(V-y),1-abs(V-y))**2)
  fasteners=np.maximum(fasteners,seam(q,.0016))
dark=np.empty_like(white)
for i,base in enumerate([.208,.231,.242]):dark[:,:,i]=base+low*.004+mid*.003+stippling*.002-dj*.055-fasteners*.025
dr=np.clip(.68+mid*.025+micro*.025+dj*.15,.55,.9)
dh=-dj*.0008-fasteners*.00025+stippling*.000022+micro*.000012
da=np.clip(1-de*.1-dj*.1-fasteners*.06,.75,1)

# Continuous resin flooring: no ceramic grid, cracks or mirrored puddles.
floor=np.empty_like(white)
for i,base in enumerate([.472,.499,.506]):floor[:,:,i]=base+low*.004+mid*.002
fr=np.clip(.18+low*.018+mid*.014+micro*.008,.13,.27)
fh=micro*.000004+mid*.000002
fa=np.ones_like(fr)

materials={}
catalog=str(uuid.UUID('b30ece49-1811-4c39-a129-c053bf044051'))
def material(name,base,rough,height,ao,description,metallic=0,shared_surface=None,tint_color=(1,1,1)):
 color=bpy.data.images[shared_surface+'_BaseColor'] if shared_surface else save_image(name+'_BaseColor',base,True)
 normal=bpy.data.images[shared_surface+'_Normal'] if shared_surface else save_image(name+'_Normal',normal_from_height(height))
 mask=bpy.data.images[shared_surface+'_Mask'] if shared_surface else save_image(name+'_Mask',np.stack([np.full_like(rough,metallic),ao,np.zeros_like(rough),1-rough],axis=2))
 m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=tuple(float(x) for x in base[N//3,N//3])+ (1,)
 m.roughness=float(np.mean(rough));m.metallic=metallic
 t=m.node_tree;t.nodes.clear();out=t.nodes.new('ShaderNodeOutputMaterial');out.location=(750,80)
 bs=t.nodes.new('ShaderNodeBsdfPrincipled');bs.location=(430,80);bs.inputs['Metallic'].default_value=metallic;bs.inputs['IOR'].default_value=1.48
 t.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
 uvn=t.nodes.new('ShaderNodeUVMap');uvn.uv_map='';uvn.location=(-850,30)
 textures=[]
 for image,y in [(color,330),(normal,-100),(mask,-460)]:
  node=t.nodes.new('ShaderNodeTexImage');node.image=image;node.location=(-600,y);node.extension='REPEAT';t.links.new(uvn.outputs['UV'],node.inputs['Vector']);textures.append(node)
 tint=t.nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(1,1,1,1);tint.label='TINT / 主色调';tint.location=(30,300)
 tint.inputs[2].default_value=tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in tint_color)+(1,)
 t.links.new(textures[0].outputs['Color'],tint.inputs[1]);t.links.new(tint.outputs[0],bs.inputs['Base Color'])
 inv=t.nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;inv.location=(-210,-460);inv.label='Unity Smoothness -> Roughness'
 t.links.new(textures[2].outputs['Alpha'],inv.inputs[1]);t.links.new(inv.outputs[0],bs.inputs['Roughness'])
 nm=t.nodes.new('ShaderNodeNormalMap');nm.location=(120,-80);nm.inputs['Strength'].default_value=1;nm.uv_map='';t.links.new(textures[1].outputs['Color'],nm.inputs['Color']);t.links.new(nm.outputs[0],bs.inputs['Normal'])
 m['lab_surface']=name;m['texture_tile_metres']=2.;m['mask_channels']='R metallic / G AO / B unused / A smoothness';m['description_zh']=description
 m.asset_mark();m.asset_data.catalog_id=catalog;m.asset_data.description=description
 m.asset_data.tags.new('ModernLab');m.asset_data.tags.new('PBR');materials[name]=m
 stats[name]={'roughness_min':float(rough.min()),'roughness_mean':float(rough.mean()),'roughness_max':float(rough.max()),'metallic':metallic,'tile_metres':2,'size':[N,N],'normal':'OpenGL +Y','files':[i.filepath for i in (color,normal,mask)],'seamless_border':True}
 return m

WHITE=material('Lab_White_Matte',white,wr,wh,wa,'现代实验室哑光灰白墙板：1x2米板块、轻微细点纹、柔和暗缝。')
DARK=material('Lab_Dark_Panel',dark,dr,dh,da,'现代实验室深灰功能板：1x1米板块、细微压纹与小型固定点。')
FLOOR=material('Lab_Floor_Resin',floor,fr,fh,fa,'连续浅灰工业树脂地坪：低粗糙度、柔和反射、极轻表面起伏。')
brush=np.sin(2*np.pi*(U*809+np.sin(V*2*np.pi)*.10))*.6+np.sin(U*2*np.pi*997)*.4
metal=np.empty_like(white)
for i,base in enumerate([.64,.665,.68]):metal[:,:,i]=base+brush*.007+mid*.002
METAL=material('Lab_Rail_BrushedSteel',metal,np.clip(.285+brush*.025+mid*.015,.23,.36),brush*.000002,np.ones_like(fr),'楼梯扶手与护栏：细拉丝不锈钢，完整金属反射，克制的细小划纹。',1)

# Grey/white checker tiles, deliberately avoiding near-black squares.
dx,dy=grid(4,4);cj=np.maximum(seam(dx,.001),seam(dy,.001));parity=((np.floor(U*4).astype(np.int32)+np.floor(V*4).astype(np.int32))%2).astype(np.float32)
checker=np.empty_like(white)
for i,(light,dark_value) in enumerate(zip([.86,.865,.86],[.52,.55,.565])):checker[:,:,i]=light*(1-parity)+dark_value*parity+mid*.002-cj*.12
CHECKER=material('Lab_Floor_Checker',checker,np.clip(.25+parity*.025+micro*.012+cj*.25,.20,.65),-cj*.00045+micro*.000003,np.clip(1-cj*.12,.8,1),'灰白棋盘格地板：50厘米方砖，中灰格而非黑格，细接缝与柔和反光。')

# White modular acoustic ceiling: shallow panel joints and tiny perforations.
dx,dy=grid(4,4);tj=np.maximum(seam(dx,.0017),seam(dy,.0017));te=np.maximum(seam(dx,.006),seam(dy,.006))
hx=np.minimum(np.mod(U*128,1),1-np.mod(U*128,1));hy=np.minimum(np.mod(V*128,1),1-np.mod(V*128,1));holes=np.exp(-((hx*hx+hy*hy)/(.13*.13))**2)*(1-te)
ceiling=np.empty_like(white)
for i,base in enumerate([.83,.837,.835]):ceiling[:,:,i]=base+mid*.002-tj*.44-holes*.16
CEILING=material('Lab_Ceiling_Acoustic',ceiling,np.clip(.84+micro*.02-tj*.18,.6,.95),-tj*.001-holes*.00035,np.clip(1-tj*.14-holes*.03,.75,1),'白色模块吊顶：50厘米板格、细穿孔、哑光吸音表面。')

# Rounded vertical ribs, not pyramidal foam or a sharp sawtooth profile.
dx,dy=grid(1,1);rj=np.maximum(seam(dx,.003),seam(dy,.003));wave=(1-np.cos(U*2*np.pi*24))*.5
ribbed=np.empty_like(white)
for i,base in enumerate([.16,.176,.185]):ribbed[:,:,i]=base+mid*.003+micro*.0015-rj*.035
RIBBED=material('Lab_Wall_Ribbed',ribbed,np.clip(.60+mid*.035+micro*.025+rj*.12,.5,.82),wave*.006-rj*.0012+micro*.00001,np.clip(1-(1-wave)*.055-rj*.08,.8,1),'深灰竖向圆弧波纹墙：约8.3厘米肋距，连续起伏法线，粗糙涂层。')

paint=np.empty_like(white)
for i,base in enumerate([.92,.923,.92]):paint[:,:,i]=base+low*.0015+mid*.002
pr=np.clip(.88+mid*.025+micro*.025,.76,.96);ph=mid*.00007+micro*.00012
ROUGH=material('Lab_Paint_RoughWhite',paint,pr,ph,np.clip(1-abs(micro)*.02,.95,1),'纯白粗糙墙漆：细颗粒抹灰，宽而弱的高光，适合白墙与亮色墙裙组合。')
gr=np.clip(.22+mid*.015+micro*.012,.16,.30);gh=micro*.000006+mid*.000002
GLOSS=material('Lab_Paint_GlossWhite',paint,gr,gh,np.ones_like(fr),'纯白光滑涂层：细腻漆面、清晰柔和反射，非金属。')
accents={}
for label,rgb in [('Lime',(.53,.94,.025)),('Red',(.97,.065,.035)),('Yellow',(.99,.79,.025))]:
 colour=np.empty_like(paint)
 for i,value in enumerate(rgb):colour[:,:,i]=paint[:,:,i]*value
 glossy=label=='Yellow';surface='Lab_Paint_GlossWhite' if glossy else 'Lab_Paint_RoughWhite'
 accents[label]=material('Lab_Paint_'+label,colour,gr if glossy else pr,gh if glossy else ph,np.ones_like(fr),'高饱和'+label+'分区漆；与白漆共享全部贴图，可直接修改 TINT 颜色。',shared_surface=surface,tint_color=rgb)

scene=bpy.context.scene;scene.name='Modular Asset Kit'
source_objects=[o for o in scene.objects if o.type=='MESH']
report=[]
def role(o):
 if o.name.startswith('吊顶'):return 'Ceiling'
 if o.name.startswith('分区墙') or o.name.startswith('纯白样板'):return 'AccentWall'
 if o.name.startswith('平面.'):
  try:index=int(o.name.split('.')[-1])
  except:index=-1
  if 5<=index<=10:return 'WallWhite'
  if 11<=index<=16:return 'WallDark'
  if index>=17:return 'Floor'
 if o.name=='平面':return 'Floor'
 if o.name.startswith('立方体'):return 'Stairs'
 return 'Rail'

def uv_metres(o,rail=False):
 # Add a dedicated channel; leave the user's original UVs available for rollback.
 mesh=o.data;layer=mesh.uv_layers.get('LabUV') or mesh.uv_layers.new(name='LabUV');mesh.uv_layers.active=layer;layer.active_render=True
 r=o.matrix_world.to_3x3().normalized();s=o.scale
 points=[r@Vector(tuple(v.co[i]*s[i] for i in range(3))) for v in mesh.vertices]
 minimum=Vector(tuple(min(p[i] for p in points) for i in range(3)))
 for p in mesh.polygons:
  n=r@p.normal;axis=max(range(3),key=lambda i:abs(n[i]))
  for li in p.loop_indices:
   co=r@Vector(tuple(mesh.vertices[mesh.loops[li].vertex_index].co[i]*s[i] for i in range(3)))
   if axis==2:u,v=co.x,co.y
   elif axis==1:u,v=co.x,co.z
   else:u,v=co.y,co.z
   a,b=(minimum.x,minimum.y) if axis==2 else (minimum.x,minimum.z) if axis==1 else (minimum.y,minimum.z)
   layer.data[li].uv=((u-a)*.5,(v-b)*.5)

def rail_uv_modifier(o):
 # Skin-based handrails have no faces in the editable control mesh. Generate
 # a real UV layer after Skin/Subdivision, rather than leaving zero UVs there.
 group=bpy.data.node_groups.get('Lab Rail Surface UV')
 if group is None:
  group=bpy.data.node_groups.new('Lab Rail Surface UV','GeometryNodeTree')
  group.interface.new_socket(name='Geometry',in_out='INPUT',socket_type='NodeSocketGeometry');group.interface.new_socket(name='Geometry',in_out='OUTPUT',socket_type='NodeSocketGeometry')
  n=group.nodes;l=group.links;inp=n.new('NodeGroupInput');out=n.new('NodeGroupOutput');pos=n.new('GeometryNodeInputPosition');normal=n.new('GeometryNodeInputNormal')
  scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=.5;l.new(pos.outputs[0],scale.inputs[0]);p=n.new('ShaderNodeSeparateXYZ');l.new(scale.outputs[0],p.inputs[0])
  absolute=n.new('ShaderNodeVectorMath');absolute.operation='ABSOLUTE';l.new(normal.outputs[0],absolute.inputs[0]);ns=n.new('ShaderNodeSeparateXYZ');l.new(absolute.outputs[0],ns.inputs[0])
  xy=n.new('ShaderNodeCombineXYZ');xz=n.new('ShaderNodeCombineXYZ');yz=n.new('ShaderNodeCombineXYZ')
  for node,a,b in [(xy,'X','Y'),(xz,'X','Z'),(yz,'Y','Z')]:l.new(p.outputs[a],node.inputs['X']);l.new(p.outputs[b],node.inputs['Y'])
  x=n.new('ShaderNodeMath');x.operation='GREATER_THAN';l.new(ns.outputs['X'],x.inputs[0]);l.new(ns.outputs['Y'],x.inputs[1])
  maxxy=n.new('ShaderNodeMath');maxxy.operation='MAXIMUM';l.new(ns.outputs['X'],maxxy.inputs[0]);l.new(ns.outputs['Y'],maxxy.inputs[1])
  z=n.new('ShaderNodeMath');z.operation='GREATER_THAN';l.new(ns.outputs['Z'],z.inputs[0]);l.new(maxxy.outputs[0],z.inputs[1])
  switchx=n.new('GeometryNodeSwitch');switchx.input_type='VECTOR';l.new(x.outputs[0],switchx.inputs['Switch']);l.new(xz.outputs[0],switchx.inputs['False']);l.new(yz.outputs[0],switchx.inputs['True'])
  switchz=n.new('GeometryNodeSwitch');switchz.input_type='VECTOR';l.new(z.outputs[0],switchz.inputs['Switch']);l.new(switchx.outputs[0],switchz.inputs['False']);l.new(xy.outputs[0],switchz.inputs['True'])
  store=n.new('GeometryNodeStoreNamedAttribute');store.data_type='FLOAT2';store.domain='CORNER';store.inputs['Name'].default_value='LabUV';l.new(inp.outputs['Geometry'],store.inputs['Geometry']);l.new(switchz.outputs[0],store.inputs['Value']);l.new(store.outputs['Geometry'],out.inputs['Geometry'])
 modifier=o.modifiers.new('Lab Surface UV','NODES');modifier.node_group=group

def prepare_backfaces(mesh):
 # URP Lit does not flip back-face normals when culling is disabled. Export
 # oppositely wound faces and let back-face culling select the correctly lit side.
 import bmesh
 bm=bmesh.new();bm.from_mesh(mesh)
 result=bmesh.ops.duplicate(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces))
 faces=[e for e in result['geom'] if isinstance(e,bmesh.types.BMFace)]
 bmesh.ops.reverse_faces(bm,faces=faces);bm.normal_update();bm.to_mesh(mesh);bm.free()

for o in source_objects:
 kind=role(o);o.data=o.data.copy();o.data.materials.clear()
 if kind=='Stairs':
  o.data.materials.append(WHITE);o.data.materials.append(FLOOR)
  for p in o.data.polygons:p.material_index=1 if p.normal.z>.55 else 0
 else:
  mat=WHITE if kind=='WallWhite' else DARK if kind=='WallDark' else METAL if kind=='Rail' else FLOOR
  if kind=='WallWhite' and o.name in ['平面.007','平面.008']:mat=GLOSS
  if kind=='WallWhite' and o.name in ['平面.009','平面.010']:mat=ROUGH
  if kind=='WallDark' and o.name in ['平面.014','平面.015','平面.016']:mat=RIBBED
  if kind=='Floor' and o.name in ['平面.018','平面.019']:mat=CHECKER
  o.data.materials.append(mat)
 uv_metres(o,kind=='Rail');o['lab_role']=kind;o['source_name']=o.name
 if kind=='Rail':rail_uv_modifier(o)
 o.asset_mark();o.asset_data.description='Modular kit / '+kind+' / '+o.name
 report.append({'object':o.name,'role':kind,'materials':[m.name for m in o.data.materials],'vertices':len(o.data.vertices),'uv_channels':[u.name for u in o.data.uv_layers]})

# Add derived modules in their own scene; the original 29 meshes stay editable.
additional_objects=[]
for label,size in [('吊顶_6m',1),('吊顶_3m',.5)]:
 source=scene.objects['平面'];o=source.copy();o.data=source.data.copy();o.name=label;scene.collection.objects.link(o);o.location=(0,0,3.4);o.rotation_euler=(math.pi,0,0);o.scale=tuple(v*size for v in source.scale);o.data.materials.clear();o.data.materials.append(CEILING);uv_metres(o);o['lab_role']='Ceiling';additional_objects.append(o)
import bmesh
def accent_wall(name,white_mat,colour_mat=None,band=(0,1.1)):
 source=scene.objects['平面.006'];coords=[source.matrix_world@v.co for v in source.data.vertices];centre=Vector(((max(v.x for v in coords)+min(v.x for v in coords))*.5,(max(v.y for v in coords)+min(v.y for v in coords))*.5,min(v.z for v in coords)))
 mesh=bpy.data.meshes.new(name);mesh.from_pydata([tuple(v-centre) for v in coords],[],[list(p.vertices) for p in source.data.polygons]);mesh.materials.append(white_mat)
 o=bpy.data.objects.new(name,mesh);scene.collection.objects.link(o)
 if colour_mat:
  mesh.materials.append(colour_mat);bm=bmesh.new();bm.from_mesh(mesh)
  for z in band:
   if z>0:bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=(0,0,z),plane_no=(0,0,1),clear_inner=False,clear_outer=False)
  for face in bm.faces:face.material_index=1 if band[0]-.00001<=face.calc_center_median().z<=band[1]+.00001 else 0
  bm.to_mesh(mesh);bm.free()
 uv_metres(o);o['lab_role']='AccentWall';additional_objects.append(o)
accent_wall('分区墙_Lime',ROUGH,accents['Lime'],(0,1.1))
accent_wall('分区墙_Red',ROUGH,accents['Red'],(0,1.25))
accent_wall('分区墙_Yellow',GLOSS,accents['Yellow'],(1.1,1.65))
accent_wall('纯白样板_Rough',ROUGH);accent_wall('纯白样板_Gloss',GLOSS)
for o in additional_objects:
 o.asset_mark();o.asset_data.description='Derived modular asset / '+role(o)
 report.append({'object':o.name,'role':role(o),'materials':[m.name for m in o.data.materials],'vertices':len(o.data.vertices),'uv_channels':[u.name for u in o.data.uv_layers]})

# Deliver material datablocks as a separately appendable, packed .blend library.
SOURCE.joinpath('blender_assets.cats.txt').write_text('VERSION 1\n'+catalog+':Modern Lab/Surfaces:Modern Lab Surfaces\n',encoding='utf-8')
bpy.data.libraries.write(str(SOURCE/'ModernLab_Materials.blend'),set(materials.values()),fake_user=True,compress=True)

# FBX export evaluates modifiers on temporary objects and includes the new UV.
# Export each source object at its own origin; the layout in the .blend is retained.
exports=[]
bpy.ops.object.select_all(action='DESELECT')
for index,o in enumerate(source_objects+additional_objects):
 if o.hide_get():continue
 depsgraph=bpy.context.evaluated_depsgraph_get()
 mesh=bpy.data.meshes.new_from_object(o.evaluated_get(depsgraph),preserve_all_data_layers=True,depsgraph=depsgraph)
 temp=bpy.data.objects.new('Lab_'+role(o)+'_'+str(index).zfill(2),mesh);scene.collection.objects.link(temp);temp.matrix_world=o.matrix_world.copy();temp.location=(0,0,0)
 uv_metres(temp,role(o)=='Rail');temp.modifiers.new('Export triangulation','TRIANGULATE')
 # URP samples TEXCOORD0. Keep the user's original UV in the editable .blend,
 # but make the metric material UV the first channel in each temporary export.
 for layer in list(temp.data.uv_layers):
  if layer.name!='LabUV':temp.data.uv_layers.remove(layer)
 if role(o) in ['WallWhite','WallDark','AccentWall','Ceiling']:prepare_backfaces(temp.data)
 for item in bpy.context.selected_objects:item.select_set(False)
 temp.select_set(True);bpy.context.view_layer.objects.active=temp
 path=OUT/'Models'/(temp.name+'.fbx')
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP')
 exports.append({'fbx':path.name,'source':o.name,'role':role(o)})
 bpy.data.objects.remove(temp,do_unlink=True)
additional_scene=bpy.data.scenes.new('Additional Modules')
for o in additional_objects:
 additional_scene.collection.objects.link(o)
 for collection in list(o.users_collection):
  if collection!=additional_scene.collection:collection.objects.unlink(o)

# A separate scene demonstrates the three actual surfaces under shared lighting.
preview=bpy.data.scenes.new('Material Preview');bpy.context.window.scene=preview
def cube(name,loc,size,mat,bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(mat);uv_metres(o)
 if bevel:
  mod=o.modifiers.new('Edge radius','BEVEL');mod.width=bevel;mod.segments=3
  mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
 return o
def emission(name,color,strength):
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=strength;return m
LIGHT=emission('Preview_Light',(0.8,.92,1),4)
YELLOW=emission('Preview_Safety_Marking',(.82,.53,.07),0)
cube('Observation window',(-2.6,3.05,2.3),(1.8,.04,1.2),LIGHT)
cube('Reflective resin floor',(0,0,-.1),(9,9,.2),FLOOR)
cube('White back wall',(0,3.2,2.6),(8,.15,3.2),WHITE,.012)
cube('Dark lower wall',(0,3.08,.65),(8,.15,1.3),DARK,.008)
cube('Dark service column',(3.5,1.3,2.0),(.45,3.8,4),DARK,.012)
cube('White left wall',(-4,1,2.1),(.15,4.4,4.2),WHITE,.012)
cube('Dark service door',(.4,3.0,1.35),(1.6,.18,2.65),DARK,.015)
for x in [-.48,1.28]:cube('Door trim',(x,2.87,1.4),(.075,.12,2.8),WHITE,.007)
cube('Door top trim',(.4,2.87,2.81),(1.85,.12,.075),WHITE,.007)
for x in [-2.5,0,2.5]:
 cube('Ceiling light strip',(x,.6,3.75),(.11,4,.04),LIGHT,.01)
 data=bpy.data.lights.new('Soft ceiling','AREA');data.energy=420;data.shape='RECTANGLE';data.size=.32;data.size_y=3.8
 o=bpy.data.objects.new('Soft ceiling',data);preview.collection.objects.link(o);o.location=(x,.5,3.65)
for x in [-2.0,2.0]:cube('Safety line',(x,-.7,.006),(.025,5,.006),YELLOW)
for i,(mat,label) in enumerate([(WHITE,'MATTE WHITE'),(DARK,'DARK PANEL'),(FLOOR,'RESIN FLOOR')]):
 cube(label,(i*2.15-2.15,.5,1.0),(1.8,.18,1.8),mat,.025)
 # Labels are only in the demonstration scene, not part of exported assets.
 curve=bpy.data.curves.new(label,'FONT');curve.body=label;curve.size=.13;curve.align_x='CENTER';o=bpy.data.objects.new(label+' label',curve);preview.collection.objects.link(o);o.location=(i*2.15-2.15,.35,.02);o.rotation_euler=(math.radians(90),0,0);o.data.materials.append(DARK)
world=bpy.data.worlds.new('Neutral Studio');world.use_nodes=True;background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');background.inputs[0].default_value=(.18,.21,.25,1);background.inputs[1].default_value=.35;preview.world=world
data=bpy.data.lights.new('Broad key','AREA');data.energy=1050;data.shape='DISK';data.size=5;lamp=bpy.data.objects.new('Broad key',data);preview.collection.objects.link(lamp);lamp.location=(-3,-3,6);lamp.rotation_euler=(Vector((0,1,1))-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(7,-9,3.4));camera=bpy.context.object;camera.name='Material presentation';camera.rotation_euler=(Vector((0,1.0,1.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=43;preview.camera=camera
preview.render.engine='CYCLES';preview.cycles.samples=32;preview.cycles.use_denoising=True;preview.render.resolution_x=1400;preview.render.resolution_y=1000;preview.render.resolution_percentage=100
preview.view_settings.view_transform='AgX';preview.render.image_settings.file_format='PNG';preview.render.filepath=str(SOURCE/'Preview/ModernLab_Materials.png')
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
 for d in prefs.devices:d.use=d.type!='CPU'
 if any(d.type!='CPU' for d in prefs.devices):preview.cycles.device='GPU'
except Exception:pass

# Store both the user's layout and a presentation scene; launch in the asset layout.
bpy.context.window.scene=scene
for area in bpy.context.screen.areas:
 if area.type=='CONSOLE':area.type='VIEW_3D'
 if area.type=='VIEW_3D':
  area.spaces.active.shading.type='MATERIAL';area.spaces.active.overlay.show_wireframes=False
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ModernLab_ModularKit.blend'),compress=True)
result={'materials':stats,'objects':report,'exports':exports,'source_snapshot':'Source/ModularKit_InputSnapshot.blend','kit':'ModernLab_ModularKit.blend','material_library':'ModernLab_Materials.blend','preview_scene':'Material Preview'}
SOURCE.joinpath('build_report.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('MODERN_LAB_BUILD_OK',len(source_objects),len(exports),flush=True)
bpy.context.window.scene=preview;bpy.ops.render.render(write_still=True)
print('MODERN_LAB_RENDER_OK',flush=True)
exec(compile((SOURCE/'Tools/build_module_review.py').read_text(encoding='utf-8'),'build_module_review.py','exec'))
exec(compile((SOURCE/'Tools/build_additional_review.py').read_text(encoding='utf-8'),'build_additional_review.py','exec'))
