"""Approval-stage facility props. Never writes to Unity Assets."""
import bpy, math, json, re, random
from pathlib import Path
from mathutils import Vector, Matrix
import numpy as np

ROOT=Path('D:/UNITY工程/棍木项目/棍木项目')
OUT=ROOT/'AssetStaging/FacilityProps';TEX=OUT/'Textures'
for sub in ['Textures','Preview','Exports']:(OUT/sub).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.name='01 · Facility Props Review'
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
assets=bpy.data.collections.new('Assets · 1m crates and exit lights');scene.collection.children.link(assets)
stage=bpy.data.collections.new('Review studio · not exported');scene.collection.children.link(stage)
font=bpy.data.fonts.load('C:/Windows/Fonts/arialbd.ttf')
font_regular=bpy.data.fonts.load('C:/Windows/Fonts/arial.ttf')
reused={};mat_config={};models={};parts=[];r=random.Random(426)

def load_image(path,color=True):
 im=bpy.data.images.load(str(path),check_existing=True);im.colorspace_settings.name='sRGB' if color else 'Non-Color';im.alpha_mode='CHANNEL_PACKED';im.pack();return im

# Read the current Unity material values, including the user's Inspector adjustments.
guid_paths={}
for folder in ['ModernLab','SovietFacility']:
 for path in (ROOT/'Assets/Game/Environment'/folder/'Textures').glob('*.png.meta'):
  match=re.search(r'^guid: (\w+)',path.read_text(encoding='utf-8'),re.M)
  if match:guid_paths[match[1]]=Path(str(path)[:-5])

def unity_material(family,name):
 path=ROOT/'Assets/Game/Environment'/family/'Materials'/(name+'.mat');text=path.read_text(encoding='utf-8')
 p={'source_material':str(path.relative_to(ROOT)),'name':name,'textures':{},'values':{},'colors':{}}
 for key in ['BaseMap','BumpMap','SurfaceMask','MicroNormal']:
  match=re.search(r'- _'+key+r':\s+m_Texture: \{fileID: \d+, guid: (\w+)',text)
  if match:p['textures'][key]=str(guid_paths[match[1]].relative_to(ROOT))
 for key in ['DustAmount','DustColourStrength','DustRoughness','DustTiling','SpotAmount','NormalStrength','MicroStrength','MicroTiling','RoughnessBias','SurfaceTiling']:
  match=re.search(r'- _'+key+r': ([\d.e+-]+)',text);p['values'][key]=float(match[1]) if match else 0
 for key in ['BaseColor','DustColour']:
  match=re.search(r'- _'+key+r': \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}',text)
  p['colors'][key]=list(map(float,match.groups())) if match else [1,1,1,1]
 reused[name]=p;return layered(name,p)

def layered(name,p):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();l=m.node_tree.links
 def node(t,label):o=n.new(t);o.label=label;o.name=label;return o
 def wire(a,b):
  if hasattr(a,'node'):l.new(a,b)
  else:b.default_value=a
 def mathop(op,a,b):
  o=node('ShaderNodeMath',op);o.operation=op;wire(a,o.inputs[0]);wire(b,o.inputs[1]);return o.outputs[0]
 def vec(op,a,b=None):
  o=node('ShaderNodeVectorMath',op);o.operation=op;wire(a,o.inputs[0])
  if b is not None:wire(b,o.inputs[1])
  return o.outputs['Value'] if op=='DOT_PRODUCT' else o.outputs[0]
 def mix(a,b,f,label):
  o=node('ShaderNodeMixRGB',label);wire(f,o.inputs[0]);wire(a,o.inputs[1]);wire(b,o.inputs[2]);return o.outputs[0]
 uv=node('ShaderNodeUVMap','UV0 · fixed to object');uv.uv_map='UV0';v=p['values']
 surface_uv=vec('MULTIPLY',uv.outputs[0],(v['SurfaceTiling'],)*3)
 def texture(key,coords):
  o=node('ShaderNodeTexImage',key);o.image=load_image(ROOT/p['textures'][key],key=='BaseMap');wire(coords,o.inputs[0]);return o
 base=texture('BaseMap',surface_uv);normal=texture('BumpMap',surface_uv);mask=texture('SurfaceMask',surface_uv)
 separate=node('ShaderNodeSeparateColor','Metal / AO / Dust');wire(mask.outputs[0],separate.inputs[0])
 dt=texture('SurfaceMask',vec('MULTIPLY',surface_uv,(v['DustTiling'],)*3));ds=node('ShaderNodeSeparateColor','Dust B');wire(dt.outputs[0],ds.inputs[0])
 dirt=mathop('MINIMUM',mathop('MULTIPLY',base.outputs['Alpha'],v['SpotAmount']),1)
 dust=mathop('MINIMUM',mathop('MULTIPLY',ds.outputs['Blue'],v['DustAmount']),1)
 tinted=vec('MULTIPLY',base.outputs[0],p['colors']['BaseColor'][:3]);clean=mix(tinted,base.outputs[0],separate.outputs['Red'],'Bare metal tint bypass')
 color=mix(clean,(.018,.021,.023,1),dirt,'Black stains');color=mix(color,tuple(p['colors']['DustColour']),mathop('MULTIPLY',dust,v['DustColourStrength']),'Thin dust')
 rough=mathop('MINIMUM',mathop('MAXIMUM',mathop('ADD',mathop('SUBTRACT',1,mask.outputs['Alpha']),v['RoughnessBias']),0),1)
 rough=mathop('ADD',mathop('MULTIPLY',rough,mathop('SUBTRACT',1,dirt)),mathop('MULTIPLY',dirt,.83));rough=mathop('ADD',mathop('MULTIPLY',rough,mathop('SUBTRACT',1,dust)),mathop('MULTIPLY',dust,v['DustRoughness']))
 metallic=mathop('MULTIPLY',separate.outputs['Red'],mathop('MULTIPLY',mathop('SUBTRACT',1,dirt),mathop('SUBTRACT',1,dust)))
 micro=texture('MicroNormal',vec('MULTIPLY',uv.outputs[0],(v['MicroTiling'],)*3))
 def normal_strength(encoded,strength):return vec('NORMALIZE',vec('MULTIPLY',vec('SUBTRACT',vec('MULTIPLY',encoded,(2,2,2)),(1,1,1)),(strength,strength,1)))
 a=vec('ADD',normal_strength(normal.outputs[0],v['NormalStrength']),(0,0,1));b=vec('MULTIPLY',normal_strength(micro.outputs[0],v['MicroStrength']),(-1,-1,1));az=node('ShaderNodeSeparateXYZ','RNM denominator');wire(a,az.inputs[0]);scale=node('ShaderNodeVectorMath','RNM projection');scale.operation='SCALE';wire(a,scale.inputs[0]);wire(mathop('DIVIDE',vec('DOT_PRODUCT',a,b),az.outputs['Z']),scale.inputs['Scale']);encoded=vec('MULTIPLY',vec('ADD',vec('NORMALIZE',vec('SUBTRACT',scale.outputs[0],b)),(1,1,1)),(.5,.5,.5))
 nm=node('ShaderNodeNormalMap','Layered tangent normal');nm.uv_map='UV0';wire(encoded,nm.inputs['Color'])
 bs=node('ShaderNodeBsdfPrincipled','ModernLabLayered match');wire(color,bs.inputs['Base Color']);wire(rough,bs.inputs['Roughness']);wire(metallic,bs.inputs['Metallic']);wire(nm.outputs[0],bs.inputs['Normal']);bs.inputs['IOR'].default_value=1.48
 out=node('ShaderNodeOutputMaterial','Surface');wire(bs.outputs[0],out.inputs[0])
 for i,o in enumerate(n):o.location=((i%9)*210,-(i//9)*190)
 m['Unity Shader']='ModernLab/ModernLabLayered';m['Unity Source']=p.get('source_material','');mat_config[name]=p;return m

white=unity_material('ModernLab','Lab_Paint_WhiteSteel')
dark=unity_material('ModernLab','Lab_Dark_Panel')
steel=unity_material('ModernLab','Lab_Rail_BrushedSteel')
yellow=unity_material('ModernLab','Lab_Paint_Yellow')
green=unity_material('SovietFacility','SV_Paint_Green')
oldyellow=unity_material('SovietFacility','SV_Paint_Yellow')
ivory=unity_material('SovietFacility','SV_Paint_Ivory')
rust=unity_material('SovietFacility','SV_Metal_Rust')

def simple(name,color,rough=.5,metal=0,emission=0):
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metal
 if emission:bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=emission
 mat_config[name]={'kind':'lit','color':[*color,1],'roughness':rough,'metallic':metal,'emission':emission};return m

rubber=simple('FP_Rubber_Gasket',(.014,.019,.021),.74)
cyan=simple('FP_Cyan_Status',(.02,.65,.75),.3,0,2)

def planar_uv(obj):
 uv=obj.data.uv_layers.get('UV0') or obj.data.uv_layers.new(name='UV0')
 for f in obj.data.polygons:
  axis=max(range(3),key=lambda i:abs(f.normal[i]))
  for li in f.loop_indices:
   co=obj.data.vertices[obj.data.loops[li].vertex_index].co
   uv.data[li].uv=Vector((co.x,co.y) if axis==2 else (co.x,co.z) if axis==1 else (co.y,co.z))/2
 for layer in list(obj.data.uv_layers):
  if layer.name!='UV0':obj.data.uv_layers.remove(layer)
 obj.data.uv_layers.active_index=0;uv.active_render=True

def add(obj,material):
 for c in list(obj.users_collection):c.objects.unlink(obj)
 assets.objects.link(obj);obj.data.materials.append(material);parts.append(obj);return obj

def box(name,loc,size,material,bevel=.006):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=add(bpy.context.object,material);o.name=name;o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  b=o.modifiers.new('Baked chamfer','BEVEL');b.width=bevel;b.segments=1;b.affect='EDGES';bpy.ops.object.modifier_apply(modifier=b.name)
 planar_uv(o);return o

def cylinder(name,loc,radius,depth,material,axis=(0,-1,0),vertices=8):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc);o=add(bpy.context.object,material);o.name=name;planar_uv(o);o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler();return o

def label_quad(name,loc,w,h,material,side='front'):
 verts=[(-w/2,0,-h/2),(w/2,0,-h/2),(w/2,0,h/2),(-w/2,0,h/2)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],[(0,1,2,3)]);mesh.update();o=bpy.data.objects.new(name,mesh);assets.objects.link(o);o.location=loc;mesh.materials.append(material);uv=mesh.uv_layers.new(name='UV0')
 for i,p in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[i].uv=p
 if side=='back':o.rotation_euler.z=math.pi
 parts.append(o);return o

def finish(name,family,description,collider):
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]
 if len(parts)>1:bpy.ops.object.join()
 o=parts[0];o.name=name;o.data.transform(o.matrix_world);o.matrix_world=Matrix.Identity(4);o.asset_mark();o.asset_data.description=description;o['Family']=family;o['Pivot']='bottom centre for crate; panel centre for sign';o.data.calc_loop_triangles()
 assert len(o.data.uv_layers)==1 and o.data.uv_layers[0].name=='UV0'
 models[name]={'object':o,'family':family,'description':description,'collider':collider,'triangles':len(o.data.loop_triangles),'materials':[m.name for m in o.data.materials]}
 print('MODEL_READY',name,models[name]['triangles'],flush=True);parts.clear();return o

# Flat graphics are rendered into small atlases, not left as text meshes on the props.
def emission_mat(name,color,image=None):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();bs=n.new('ShaderNodeEmission');bs.inputs[0].default_value=(*color,1);out=n.new('ShaderNodeOutputMaterial');m.node_tree.links.new(bs.outputs[0],out.inputs[0])
 if image:
  tx=n.new('ShaderNodeTexImage');tx.image=image;m.node_tree.links.new(tx.outputs[0],bs.inputs[0])
 return m

def atlas(name,kind,direction='right',source=None):
 sc=bpy.data.scenes.new('BAKE '+name);bpy.context.window.scene=sc
 sc.render.engine='CYCLES';sc.cycles.samples=1;sc.render.resolution_x=1024;sc.render.resolution_y=512 if kind=='crate' else 256;sc.render.resolution_percentage=100;sc.render.film_transparent=False;sc.view_settings.view_transform='Standard';sc.view_settings.look='None';sc.world=bpy.data.worlds.new('Atlas black');sc.world.color=(0,0,0)
 w=4.;h=2. if kind=='crate' else 1.;soviet=name.startswith('SV')
 ink=(.78,.76,.62) if soviet else (.018,.028,.036);paper=(.016,.11,.043) if soviet else (.018,.27,.19)
 def shape(label,verts,color,z=.012):
  mesh=bpy.data.meshes.new(label);mesh.from_pydata([(x,y,z) for x,y in verts],[],[tuple(range(len(verts)))]);mesh.update();o=bpy.data.objects.new(label,mesh);sc.collection.objects.link(o);o.data.materials.append(emission_mat(label,color));return o
 def rect(x,y,sx,sy,color,z=.012):return shape('Graphic rectangle',[(x,y),(x+sx,y),(x+sx,y+sy),(x,y+sy)],color,z)
 def text(body,x,y,size,color=ink,z=.025):
  curve=bpy.data.curves.new(body,'FONT');curve.body=body;curve.font=font;curve.size=size;curve.align_y='BOTTOM_BASELINE';curve.resolution_u=2;o=bpy.data.objects.new(body,curve);sc.collection.objects.link(o);o.location=(x,y,z);curve.materials.append(emission_mat('Ink',color));return o
 def arrow(cx,cy,size):
  pts=[(-.46,-.11),(.06,-.11),(.06,-.29),(.46,0),(.06,.29),(.06,.11),(-.46,.11)]
  angle=math.pi if direction=='left' else math.pi/2 if direction=='forward' else 0
  shape('Direction arrow',[(cx+size*(x*math.cos(angle)-y*math.sin(angle)),cy+size*(x*math.sin(angle)+y*math.cos(angle))) for x,y in pts],ink,.02)
 if kind=='crate':
  assert source
  # Background is the same coating albedo as the main body, with a distinct UV offset.
  im=load_image(ROOT/source['textures']['BaseMap']);o=rect(0,0,w,h,(1,1,1),0);o.data.materials.clear();o.data.materials.append(emission_mat('Coating base',(1,1,1),im));uv=o.data.uv_layers.new(name='UVMap')
  for i,p in enumerate([(0,0),(.43,0),(.43,.35),(0,.35)]):uv.data[i].uv=p
  text('ГРУЗ / 17' if soviet else 'SERVICE CARGO',.22,1.48,.31)
  text('НЕ КАНТОВАТЬ' if soviet else 'FACILITY LOGISTICS',.23,1.23,.11)
  rect(.22,1.12,3.54,.018,ink)
  if soviet:
   text('ИНСТИТУТ №4',.25,.78,.16);text('СЕКТОР Б',.25,.57,.13);text('МАССА 120 КГ',.25,.22,.12)
   text('17',2.74,.29,.83)
   for x in [2.60,3.22]:
    shape('Keep upright',[(x,.99),(x+.20,1.18),(x+.40,.99),(x+.28,.99),(x+.28,.80),(x+.12,.80),(x+.12,.99)],ink)
  else:
   text('042',.21,.29,.87);text('SEALED / EQUIPMENT',1.79,.76,.12);text('01—24 / HANDLING UNIT',1.79,.55,.105)
   rect(.22,.11,3.54,.07,(.80,.40,.035));rect(.22,1.85,.56,.055,(.02,.45,.56))
   for i in range(35):
    x=1.82+i*.052;rect(x,.22,.009+(i%3)*.006,.18 if i%4 else .21,ink)
 else:
  rect(0,0,w,h,paper,0)
  if soviet:
   # Very restrained colour ageing. Letter shapes stay clean and legible.
   for i in range(110):
    x=r.uniform(0,4);y=r.uniform(0,1);rect(x,y,r.uniform(.004,.017),r.uniform(.002,.009),(.04,.08,.032),.004)
  # Door and simplified running figure: silhouettes are baked, no raised glyph geometry.
  before_runner=set(sc.objects)
  rect(.20,.16,.065,.66,ink);rect(.20,.80,.43,.055,ink);rect(.59,.53,.05,.30,ink)
  shape('Runner body',[(.63,.61),(.76,.64),(.89,.45),(.79,.38),(.69,.52)],ink)
  shape('Runner rear leg',[(.79,.42),(.84,.35),(.63,.16),(.39,.16),(.39,.24),(.57,.24)],ink)
  shape('Runner front leg',[(.78,.40),(.86,.42),(.99,.20),(.92,.15)],ink)
  shape('Runner arm',[(.68,.58),(.72,.50),(.50,.45),(.43,.54),(.48,.59),(.55,.53)],ink)
  n=20;shape('Runner head',[(.68+.082*math.cos(i*math.tau/n),.74+.082*math.sin(i*math.tau/n)) for i in range(n)],ink)
  if direction=='left':
   for obj in set(sc.objects)-before_runner:
    for vertex in obj.data.vertices:vertex.co.x=1.20-vertex.co.x
  text('ВЫХОД' if soviet else 'EXIT',1.16,.34,.47 if soviet else .61)
  arrow(3.43,.51,.88)
  rect(.12,.09,3.76,.015,ink);rect(.12,.90,3.76,.015,ink)
 cam=bpy.data.cameras.new('Atlas camera');co=bpy.data.objects.new('Atlas camera',cam);sc.collection.objects.link(co);co.location=(w/2,h/2,5);cam.type='ORTHO';cam.ortho_scale=w;sc.camera=co
 path=TEX/(name+'.png');sc.render.filepath=str(path);sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.image_settings.color_depth='8';bpy.ops.render.render(write_still=True)
 result=load_image(path)
 if kind=='crate':
  # Alpha in the approved shader is a stain mask, not opacity.
  pixels=np.array(result.pixels[:],np.float32).reshape(result.size[1],result.size[0],4);pixels[:,:,3]=0;result.pixels.foreach_set(pixels.ravel());result.filepath_raw=str(path);result.file_format='PNG';result.save();result.pack()
 bpy.context.window.scene=scene;bpy.data.scenes.remove(sc);return result

def label_surface(name,source,base):
 p=json.loads(json.dumps(source));p.pop('source_material',None);p['name']=name;p['textures']['BaseMap']=str(Path(base.filepath).relative_to(ROOT));p['values']['SpotAmount']=0
 # Reuse coating normal/mask textures; UV is explicit on this label panel.
 return layered(name,p)

modern_atlas=atlas('ML_Cargo_Label','crate',source=reused['Lab_Paint_WhiteSteel']);modern_label=label_surface('FP_Modern_Cargo_Label',reused['Lab_Paint_WhiteSteel'],modern_atlas)
soviet_atlas=atlas('SV_Cargo_Label','crate',source=reused['SV_Paint_Green']);soviet_label=label_surface('FP_Soviet_Cargo_Label',reused['SV_Paint_Green'],soviet_atlas)

# Modern: one continuous white lid, recessed seams, grounded handles and dark corner protection.
box('Structural core',(0,0,.49),(.94,.94,.94),dark,.022)
for side in [-1,1]:
 box('White side shell',(side*.479,0,.49),(.034,.81,.79),white,.012)
 box('Front enamel plate',(0,side*.479,.49),(.80,.034,.72),white,.009)
 box('Recessed handle backing',(side*.499,0,.66),(.009,.29,.105),rubber,.008)
 box('Handle upper strap',(side*.505,0,.685),(.016,.28,.026),steel,.005)
 for y in [-.133,.133]:box('Handle end',(side*.503,y,.658),(.012,.025,.058),steel,.003)
 for x in [-.44,.44]:
  box('Flush latch housing',(x,side*.487,.84),(.075,.026,.11),dark,.005)
  box('Latch pull',(x,side*.505,.84),(.04,.012,.063),steel,.003)
 label_quad('Printed cargo panel',(0,side*.497,.48),.74,.37,modern_label,'front' if side==-1 else 'back')
for x in [-.455,.455]:
 for y in [-.455,.455]:
  box('Corner guard',(x,y,.50),(.09,.09,.86),dark,.018)
  box('Stacking foot',(x,y,.046),(.09,.09,.092),rubber,.008)
  box('Upper corner protector',(x,y,.952),(.09,.09,.096),dark,.014)
box('Lid seam gasket',(0,0,.921),(.94,.94,.018),rubber,.008)
box('Continuous white lid',(0,0,.947),(.91,.91,.046),white,.014)
for x in [-.30,.30]:box('Top protective runner',(x,0,.975),(.055,.75,.016),dark,.006)
for side in [-1,1]:
 for x in [-.32,.32]:
  o=box('Amber safety slash',(x,side*.501,.20),(.09,.004,.022),yellow,.001);o.rotation_euler.y=math.radians(-30)
 box('Small status lens',(.31,side*.504,.71),(.065,.006,.012),cyan,.002)
modern=finish('ML_Crate_1m','ModernLab','1m stackable facility cargo crate; printed 042 panel; enamel and dark protective corners',[1,1,1])

# Soviet: folded steel, reinforced corners, inset handles and restrained aged paint.
box('Old steel crate body',(0,0,.49),(.94,.94,.94),green,.014)
box('Lid gasket',(0,0,.907),(.96,.96,.018),rubber,.004)
box('Pressed steel lid',(0,0,.937),(.98,.98,.045),green,.01)
for x in [-.455,.455]:
 for y in [-.455,.455]:
  box('Yellow steel corner',(x,y,.50),(.085,.085,.89),oldyellow,.006)
  box('Stacking shoe',(x,y,.038),(.09,.09,.076),rust,.004)
  box('Reinforced corner cap',(x,y,.963),(.09,.09,.074),oldyellow,.006)
for side in [-1,1]:
 for z in [.12,.83]:box('Folded reinforcing band',(0,side*.479,z),(.84,.027,.065),green,.004)
 for z in [.20,.49,.78]:
  for x in [-.451,.451]:cylinder('Flush frame rivet',(x,side*.500,z),.011,.004,rust)
 label_quad('Russian crate stencil',(0,side*.472,.50),.74,.37,soviet_label,'front' if side==-1 else 'back')
 for x in [-.31,.31]:
  box('Lid toggle plate',(x,side*.495,.87),(.07,.026,.115),rust,.003)
  box('Latch handle',(x,side*.511,.875),(.04,.018,.069),oldyellow,.003)
 box('Side inset handle housing',(side*.477,0,.60),(.018,.34,.14),rubber,.006)
 box('Side carry handle',(side*.495,0,.617),(.024,.28,.035),green,.004)
 for y in [-.15,.15]:box('Handle mounting ear',(side*.487,y,.60),(.024,.035,.10),oldyellow,.003)
 for y in [-.31,.31]:box('Side stamped stiffener',(side*.479,y,.46),(.02,.035,.55),green,.004)
for y in [-.27,.27]:box('Pressed lid rib',(0,y,.967),(.73,.04,.017),green,.003)
soviet=finish('SV_Crate_1m','SovietFacility','1m reinforced painted steel cargo crate; Russian stencil; sparse paint loss and UV-locked dirt',[1,1,1])

def sign_material(name,image,aged):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;bs=n.get('Principled BSDF');tx=n.new('ShaderNodeTexImage');tx.image=image;uv=n.new('ShaderNodeUVMap');uv.uv_map='UV0';m.node_tree.links.new(uv.outputs[0],tx.inputs[0]);m.node_tree.links.new(tx.outputs[0],bs.inputs['Base Color']);m.node_tree.links.new(tx.outputs[0],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=1.1 if aged else 1.5;bs.inputs['Roughness'].default_value=.36 if aged else .25
 mat_config[name]={'kind':'sign_lit','texture':str(Path(image.filepath).relative_to(ROOT)),'roughness':.36 if aged else .25,'emission':1.1 if aged else 1.5};return m

for family in ['ML','SV']:
 for direction in ['right','left','forward']:
  aged=family=='SV';image=atlas(family+'_Exit_'+direction,'sign',direction);mat=sign_material('FP_'+family+'_Exit_'+direction,image,aged)
  # Positive Y is the wall side; front face is -Y. Mounting plate lies flush against a wall.
  shell=green if aged else white;edge=rust if aged else rubber
  box('Wall mounting spine',(0,.035,0),(.75,.020,.145),edge,.004)
  box('Lightbox casing',(0,-.005,0),(.96,.10,.28),shell,.010)
  box('Recessed lens gasket',(0,-.059,0),(.902,.009,.232),edge,.004)
  label_quad('Baked exit pictogram',(0,-.064,0),.88,.22,mat)
  for x in [-.455,.455]:
   for z in [-.108,.108]:cylinder('Flush lens screw',(x,-.058,z),.006,.004,rust if aged else steel,vertices=6)
  if not aged:
   box('Dark top trim',(0,-.003,.134),(.79,.072,.012),dark,.003)
   box('Self-test LED',(.43,-.060,-.055),(.018,.004,.007),cyan,.001)
  else:
   for x in [-.22,.22]:box('Lens retaining clip',(x,-.064,.124),(.04,.018,.031),ivory,.002)
  name=family+'_Exit_'+direction.capitalize();sign=finish(name,'SovietFacility' if aged else 'ModernLab','Russian ВЫХОД' if aged else 'Modern EXIT', [.96,.10,.28]);sign['Arrow']=direction

# Asset review layouts, separate from clean export transforms.
modern.location=(-.76,0,0);soviet.location=(.76,0,0)
for name,entry in models.items():
 if '_Exit_' in name:
  entry['object'].location=(-.76 if name.startswith('ML') else .76,.12,1.52)
  entry['object'].hide_render=not name.endswith('Right')

def studio_box(name,loc,size,mat):
 o=box(name,loc,size,mat,.01);parts.remove(o)
 for c in list(o.users_collection):c.objects.unlink(o)
 stage.objects.link(o);return o
ground=simple('PREVIEW_Ground',(.075,.085,.095),.52)
studio_box('Studio floor',(0,0,-.055),(200,200,.1),ground)
scene.world=bpy.data.worlds.new('Review studio world');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.21,.25,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.42
def area(name,loc,target,energy,size,color):
 data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='DISK';data.size=size;data.color=color;o=bpy.data.objects.new(name,data);stage.objects.link(o);o.location=loc;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();return o
area('Large softbox',(-3,-4,5),(0,0,.7),750,4,(.86,.93,1));area('Warm side strip',(4,-1,3),(0,0,.7),600,3,(1,.86,.69));area('Rear rim',(0,3,4),(0,0,.8),950,3,(.68,.84,1))
cam_data=bpy.data.cameras.new('Review camera');cam=bpy.data.objects.new('Review camera',cam_data);stage.objects.link(cam);scene.camera=cam;cam_data.lens=48
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True;scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='CUDA'
 scene.cycles.device='GPU'
except Exception:pass
def shot(filename,position,target,lens=48):
 cam.location=position;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam_data.lens=lens;scene.render.filepath=str(OUT/'Preview'/filename);bpy.ops.render.render(write_still=True)

# Export-stage meshes have UV0 first, applied transforms, original materials and bottom-centre pivots.
try:bpy.ops.export_scene.fbx.get_rna_type()
except Exception:bpy.ops.preferences.addon_enable(module='io_scene_fbx')
for name,entry in models.items():
 source=entry['object'];copy=bpy.data.objects.new(name+'_EXPORT',source.data.copy());scene.collection.objects.link(copy);copy.matrix_world=Matrix.Identity(4);copy.hide_render=True;copy.modifiers.new('Game triangles','TRIANGULATE');bpy.ops.object.select_all(action='DESELECT');copy.select_set(True);bpy.context.view_layer.objects.active=copy
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Exports'/(name+'.fbx')),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='STRIP')
 bpy.data.objects.remove(copy,do_unlink=True)
manifest={'status':'AWAITING_USER_APPROVAL_NOT_IMPORTED','units':'metres','materials':mat_config,'models':[{k:v for k,v in entry.items() if k!='object'}|{'name':name} for name,entry in models.items()],'total_triangles':sum(v['triangles'] for v in models.values()),'notes':['Two 1m cubes, six exit sign direction variants','All lettering and pictograms baked to 1024px atlases; no text geometry exported','One UV0 layer per mesh; same coating textures and parameters as current Unity materials','FBX exports are staged only; no Assets changes']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
cam.location=(3.1,-5.3,3.0);cam.rotation_euler=(Vector((0,0,.82))-cam.location).to_track_quat('-Z','Y').to_euler()
for screen in bpy.data.screens:
 for a in screen.areas:
  if a.type=='VIEW_3D':a.spaces.active.region_3d.view_perspective='CAMERA';a.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'FacilityProps_Review_v01.blend'),compress=True)
shot('01_Facility_Props_Overview.png',(3.1,-5.3,3.0),(0,0,.82))
models['SV_Crate_1m']['object'].hide_render=True;models['SV_Exit_Right']['object'].hide_render=True
shot('02_Modern_Crate.png',(1.55,-3.35,2.15),(-.76,0,.62),58)
models['SV_Crate_1m']['object'].hide_render=False;models['SV_Exit_Right']['object'].hide_render=False;models['ML_Crate_1m']['object'].hide_render=True;models['ML_Exit_Right']['object'].hide_render=True
shot('03_Soviet_Crate.png',(3.0,-3.6,2.25),(.76,0,.63),58)
models['ML_Crate_1m']['object'].hide_render=False;models['ML_Exit_Right']['object'].hide_render=False
shot('04_Exit_Lights.png',(0,-4.5,1.85),(0,.05,1.50),64)
print('FACILITY_PROPS_REVIEW_READY',manifest['total_triangles'],flush=True)
