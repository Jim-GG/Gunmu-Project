"""Create a non-destructive Abisennas infestation on the supplied rigged scientist.
Run in Blender 5.2 against Source/Scientist_InputSnapshot.blend. No original assets are overwritten.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[1]
scene=bpy.context.scene
info=json.loads((ROOT/'Source'/'geometry.json').read_text(encoding='utf8'))
origin=Vector(info['origin']); normal_scale=info['scale']
rig=bpy.data.objects['scientist_hurt_ARM.001']
body=bpy.data.objects['sci_body_dirty.smd.001']
skin=bpy.data.objects['../base_male/base_male.smd.001']
glasses=bpy.data.objects['Object_3']

# Separate authoring, hidden source geometry, and render-only presentation.
def collection(name):
 c=bpy.data.collections.new(name);scene.collection.children.link(c);return c
character=collection('01 | RESONATOR - Scientist')
library=collection('02 | Crystal Source Shapes')
studio=collection('03 | Preview Studio')
archive=collection('00 | Original Scene - archived helpers')
for o in list(scene.objects):
 if o.type in {'LIGHT','CAMERA','EMPTY'} and o not in {rig,body,skin,glasses}:
  o.hide_render=True;o.hide_set(True)
  for c in list(o.users_collection):c.objects.unlink(o)
  archive.objects.link(o)
for o in [rig,body,skin,glasses]:
 for c in list(o.users_collection):c.objects.unlink(o)
 character.objects.link(o)
root=bpy.data.objects.new('AB | Scientist Root - 1.80m',None);character.objects.link(root)
rig.parent=root
root.matrix_world=Matrix.Scale(normal_scale,4)@Matrix.Translation(-origin)
root['About']='Abisennas Resonator scientist. Geometry Nodes remain editable. Original rig and UVs retained.'
root['Controls']='Select clothing > AB Crystal Infestation modifier. Weight-paint AB_Growth / AB_Micro / AB_Length.'
rig.hide_set(True);rig.hide_render=True
bpy.context.view_layer.update()

# Stable rest-space masks remain attached to skinned vertices through the armature.
def gauss(p,c,r):return math.exp(-sum(((p[i]-c[i])/r[i])**2 for i in range(3))*1.7)
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def masks(p):
 back=gauss(p,(.035,.18,1.40),(.24,.16,.30))*smooth(.025,.12,p.y)
 shoulder=gauss(p,(.20,.065,1.48),(.18,.18,.17))
 forearm=gauss(p,(.48,.09,1.13),(.14,.13,.21))
 shin=gauss(p,(-.105,.005,.30),(.11,.15,.23))
 other=gauss(p,(-.46,.10,1.14),(.11,.12,.17))*.28
 main=max(back,shoulder*.95,forearm*.85,shin*.78)
 small=max(main,other,gauss(p,(-.17,.10,1.45),(.13,.14,.16))*.25)
 # A shoulder/back crown with smaller, subordinate limb growths.
 length=max(back*.34,shoulder*.41,forearm*.17,shin*.22,.035)
 return main,small,length
for o in [body,skin]:
 world=o.matrix_world.copy()
 groups={name:o.vertex_groups.new(name=name) for name in ['AB_Growth','AB_Micro','AB_Length']}
 infection=o.data.attributes.new('ab_infection','FLOAT','POINT')
 ground_dirt=o.data.attributes.new('ab_ground_dirt','FLOAT','POINT')
 for v in o.data.vertices:
  p=world@v.co
  macro,micro,length=masks(p)
  if o==skin:
   # Keep the head and eyes readable, allow fine crystalline lesions at the wrists.
   macro*=.4 if p.z<1.35 else 0;micro*=.65 if p.z<1.35 else 0;length*=.6
  for name,value in [('AB_Growth',macro),('AB_Micro',micro),('AB_Length',length)]:
   if value>.0005:groups[name].add([v.index],min(1,value),'REPLACE')
  infection.data[v.index].value=min(1,micro*1.65)
  ground_dirt.data[v.index].value=max(1-smooth(.10,.65,p.z),gauss(p,(0,-.04,.76),(.28,.22,.12))*.8,gauss(p,(.51,.08,1.08),(.12,.13,.13))*.8,gauss(p,(-.51,.08,1.08),(.12,.13,.13))*.8)
 o['Invasion mask']='AB_Growth: major clusters; AB_Micro: satellite fragments; AB_Length: local crystal size'

# Convenient node construction.
def node(tree,kind,name,x,y):
 n=tree.nodes.new(kind);n.name=name;n.label=name;n.location=(x,y);return n
def link(tree,a,out,b,inp):tree.links.new(a.outputs[out],b.inputs[inp])
def ramp(n,stops):
 r=n.color_ramp
 while len(r.elements)>2:r.elements.remove(r.elements[-1])
 for i,(pos,col) in enumerate(stops):
  e=r.elements[i] if i<2 else r.elements.new(pos);e.position=pos;e.color=col
 return n

def crystal_material(name,brightness=1):
 m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(.22,.13,.31,1)
 t=m.node_tree;t.nodes.clear()
 out=node(t,'ShaderNodeOutputMaterial','Crystal surface',900,100)
 bs=node(t,'ShaderNodeBsdfPrincipled','Smoky amethyst / fractured quartz',640,100)
 bs.inputs['Metallic'].default_value=.08;bs.inputs['Roughness'].default_value=.14
 bs.inputs['IOR'].default_value=1.48;bs.inputs['Transmission Weight'].default_value=.72
 bs.inputs['Coat Weight'].default_value=.38;bs.inputs['Coat Roughness'].default_value=.10
 a=node(t,'ShaderNodeAttribute','Embedded root-to-tip tint',-700,300);a.attribute_name='ab_tip'
 colors=[(0,(.065,.043,.085,1)),(.30,(.16,.115,.20,1)),(.72,(.37,.29,.43,1)),(1,(.66,.56,.70,1))]
 tint=ramp(node(t,'ShaderNodeValToRGB','Dark embedded roots / pale broken tips',-440,300),[(p,tuple(c[i]*brightness if i<3 else 1 for i in range(4))) for p,c in colors]);link(t,a,'Fac',tint,'Fac')
 tex=node(t,'ShaderNodeTexCoord','Crystal coordinates',-720,-100)
 vor=node(t,'ShaderNodeTexVoronoi','Hairline internal fractures',-460,-100);vor.feature='DISTANCE_TO_EDGE';vor.inputs['Scale'].default_value=33
 link(t,tex,'Object',vor,'Vector')
 edge=ramp(node(t,'ShaderNodeValToRGB','Thin mineral veins',-220,-100),[(.001,(.08,.08,.08,1)),(.004,(0,0,0,1))]);link(t,vor,'Distance',edge,'Fac')
 mix=node(t,'ShaderNodeMixRGB','Faint lavender inclusions',120,220);mix.inputs[2].default_value=(.65,.49,.68,1)
 link(t,edge,'Color',mix,0);link(t,tint,'Color',mix,1);link(t,mix,'Color',bs,'Base Color')
 bump=node(t,'ShaderNodeBump','Subtle fracture relief',360,-100);bump.inputs['Strength'].default_value=.13;bump.inputs['Distance'].default_value=.0008
 link(t,edge,'Color',bump,'Height');link(t,bump,'Normal',bs,'Normal');link(t,bs,'BSDF',out,'Surface')
 return m
crystal_mats=[crystal_material('AB | Crystal - smoky violet',1),crystal_material('AB | Crystal - pale facets',1.22),crystal_material('AB | Crystal - deep facets',.78)]

# Hand-shaped irregular six-sided prisms; the actual distribution is procedural.
def prototype(index):
 rng=random.Random(41+index);verts=[];faces=[];tips=[];N=6
 for level,(z,r) in enumerate([(-.055,.88),(.34,1.0),(.73,.78)]):
  for i in range(N):
   angle=2*math.pi*i/N+.13*index;rr=r*(.82+rng.random()*.25)
   verts.append((math.cos(angle)*.105*rr+z*.03*index,math.sin(angle)*.070*rr,z+(rng.random()-.5)*.045));tips.append(max(0,z))
 verts.append((.035+index*.012,-.015,1.0));tips.append(1)
 faces.append(tuple(reversed(range(N))))
 for ring in range(2):
  for i in range(N):
   a=ring*N+i;b=ring*N+(i+1)%N;c=(ring+1)*N+(i+1)%N;d=(ring+1)*N+i
   # A few diagonal facets break up the regular prism without smoothing edges.
   if (i+index+ring)%3==0:faces.extend([(a,b,d),(b,c,d)])
   else:faces.append((a,b,c,d))
 for i in range(N):faces.append((12+i,12+(i+1)%N,18))
 mesh=bpy.data.meshes.new('AB prism %d mesh'%index);mesh.from_pydata(verts,[],faces);mesh.update()
 for m in crystal_mats:mesh.materials.append(m)
 for p in mesh.polygons:p.material_index=rng.choices([0,1,2],[.63,.22,.15])[0];p.use_smooth=False
 attr=mesh.attributes.new('ab_tip','FLOAT','POINT')
 for i,value in enumerate(tips):attr.data[i].value=value
 o=bpy.data.objects.new('AB | Hidden prism source %d'%index,mesh);library.objects.link(o);o.hide_render=True;o.hide_set(True)
 bevel=o.modifiers.new('Hairline chipped facet edges','BEVEL');bevel.width=.004;bevel.segments=1;bevel.limit_method='ANGLE';bevel.angle_limit=.3
 return o
prototypes=[prototype(i) for i in range(3)]

# Reusable, labelled node group, shared by clothing and exposed hands.
g=bpy.data.node_groups.new('AB | Crystal Infestation - editable growth','GeometryNodeTree');g.is_modifier=True
def socket(name,typ,default=None,minv=None,maxv=None):
 s=g.interface.new_socket(name=name,in_out='INPUT',socket_type=typ)
 if default is not None:s.default_value=default
 if minv is not None:s.min_value=minv
 if maxv is not None:s.max_value=maxv
 return s
socket('Geometry','NodeSocketGeometry');g.interface.new_socket(name='Geometry',in_out='OUTPUT',socket_type='NodeSocketGeometry')
socket('Major Density','NodeSocketFloat',460,0,2000)
socket('Micro Density','NodeSocketFloat',1700,0,5000)
socket('Crystal Length','NodeSocketFloat',1,0,3)
socket('Crystal Width','NodeSocketFloat',1,0,3)
socket('Upward Growth','NodeSocketFloat',1.05,0,3)
socket('Seed','NodeSocketInt',17,0,10000)
socket('Show Crystals','NodeSocketBool',True)
ins=node(g,'NodeGroupInput','Artist controls',-1200,400);out=node(g,'NodeGroupOutput','Original character + crystals',1500,400)
join=node(g,'GeometryNodeJoinGeometry','Non-destructive result',1250,400);link(g,ins,'Geometry',join,'Geometry');link(g,join,'Geometry',out,'Geometry')
shapes=node(g,'GeometryNodeGeometryToInstance','Three faceted prism variations',-650,-850)
for i,o in enumerate(prototypes):
 n=node(g,'GeometryNodeObjectInfo','Prism shape %d'%i,-950-i*35,-650-i*180);n.transform_space='ORIGINAL';n.inputs['Object'].default_value=o
 link(g,n,'Geometry',shapes,'Geometry')
for row,(mask,control,factor) in enumerate([('AB_Growth','Major Density',1),('AB_Micro','Micro Density',.28)]):
 y=450-row*600
 attr=node(g,'GeometryNodeInputNamedAttribute',mask+' / weight paint',-1150,y-120);attr.data_type='FLOAT';attr.inputs['Name'].default_value=mask
 density=node(g,'ShaderNodeMath',control+' × painted mask',-900,y);density.operation='MULTIPLY';link(g,attr,'Attribute',density,0);link(g,ins,control,density,1)
 dist=node(g,'GeometryNodeDistributePointsOnFaces','Surface-attached '+control,-650,y);dist.distribute_method='RANDOM';link(g,ins,'Geometry',dist,'Mesh');link(g,density,0,dist,'Density');link(g,ins,'Seed',dist,'Seed')
 # Blend deformed surface normals with the characteristic upward growth direction.
 up=node(g,'ShaderNodeCombineXYZ','Upward growth bias',-620,y+210);link(g,ins,'Upward Growth',up,'Z')
 vadd=node(g,'ShaderNodeVectorMath','Normal + upwards',-400,y+200);vadd.operation='ADD';link(g,dist,'Normal',vadd,0);link(g,up,'Vector',vadd,1)
 align=node(g,'FunctionNodeAlignEulerToVector','Point sharp ends outward',-160,y+200);align.axis='Z';link(g,vadd,'Vector',align,'Vector')
 randomrot=node(g,'FunctionNodeRandomValue','Fan and twist variation',-400,y-190);randomrot.data_type='FLOAT_VECTOR';randomrot.inputs['Min'].default_value=(-.45,-.45,-math.pi);randomrot.inputs['Max'].default_value=(.45,.45,math.pi);link(g,ins,'Seed',randomrot,'Seed')
 rotate=node(g,'FunctionNodeRotateEuler','Irregular crystal fan',70,y+200);rotate.space='LOCAL';link(g,align,'Rotation',rotate,'Rotation');link(g,randomrot,'Value',rotate,'Rotate By')
 sizeattr=node(g,'GeometryNodeInputNamedAttribute','AB_Length / regional size',-650,y-320);sizeattr.data_type='FLOAT';sizeattr.inputs['Name'].default_value='AB_Length'
 mul=node(g,'ShaderNodeMath','Regional length × size control',-410,y-320);mul.operation='MULTIPLY';link(g,sizeattr,'Attribute',mul,0);link(g,ins,'Crystal Length',mul,1)
 variation=node(g,'FunctionNodeRandomValue','Unequal prism proportions',-190,y-300);variation.data_type='FLOAT_VECTOR';variation.inputs['Min'].default_value=(.75,.65,.50);variation.inputs['Max'].default_value=(1.35,1.15,1.3);link(g,ins,'Seed',variation,'Seed')
 vec=node(g,'ShaderNodeVectorMath','Regional scale',40,y-200);vec.operation='SCALE';link(g,variation,'Value',vec,0);link(g,mul,0,vec,'Scale')
 width=node(g,'ShaderNodeCombineXYZ','Shard widths',-180,y-460);link(g,ins,'Crystal Width',width,'X');link(g,ins,'Crystal Width',width,'Y');width.inputs['Z'].default_value=1
 wide=node(g,'ShaderNodeVectorMath','Width adjustment',270,y-190);wide.operation='MULTIPLY';link(g,vec,'Vector',wide,0);link(g,width,'Vector',wide,1)
 micro=node(g,'ShaderNodeVectorMath','Major / micro proportion',470,y-200);micro.operation='SCALE';micro.inputs['Scale'].default_value=factor;link(g,wide,'Vector',micro,0)
 randindex=node(g,'FunctionNodeRandomValue','Choose prism shape',280,y+350);randindex.data_type='INT';randindex.inputs['Min'].default_value=0;randindex.inputs['Max'].default_value=2;link(g,ins,'Seed',randindex,'Seed')
 instance=node(g,'GeometryNodeInstanceOnPoints','Grow prisms from skin',520,y);instance.inputs['Pick Instance'].default_value=True
 link(g,dist,'Points',instance,'Points');link(g,shapes,'Instances',instance,'Instance');link(g,randindex,'Value',instance,'Instance Index');link(g,rotate,'Rotation',instance,'Rotation');link(g,micro,'Vector',instance,'Scale')
 realize=node(g,'GeometryNodeRealizeInstances','Realized facets / tip attributes',760,y);link(g,instance,'Instances',realize,'Geometry')
 switch=node(g,'GeometryNodeSwitch','Toggle infestation',1000,y);switch.input_type='GEOMETRY';link(g,ins,'Show Crystals',switch,'Switch');link(g,realize,'Geometry',switch,'True');link(g,switch,'Output',join,'Geometry')
for o in [body,skin]:
 mod=o.modifiers.new('AB | Crystal Infestation - Geometry Nodes','NODES');mod.node_group=g

# Add grime over original UV textures, preserving seams, pockets, shirt and tie.
def weather_material(old,skin_mode=False):
 m=old.copy();m.name='AB | '+('Ashen skin - ' if skin_mode else 'Aged fabric - ')+old.name
 t=m.node_tree;bs=next(n for n in t.nodes if n.type=='BSDF_PRINCIPLED')
 source=bs.inputs['Base Color'].links[0].from_socket if bs.inputs['Base Color'].is_linked else None
 bs.location=(1300,100);next(n for n in t.nodes if n.type=='OUTPUT_MATERIAL').location=(1580,100)
 if source is None:
  rgb=node(t,'ShaderNodeRGB','Original pigment',-1400,500);rgb.outputs[0].default_value=bs.inputs['Base Color'].default_value;source=rgb.outputs[0]
 coord=node(t,'ShaderNodeTexCoord','Stable garment coordinates',-1500,-150)
 hue=node(t,'ShaderNodeHueSaturation','Faded original fabric',-1120,500);hue.inputs['Saturation'].default_value=.55 if skin_mode else .70;hue.inputs['Value'].default_value=.73 if skin_mode else .87;t.links.new(source,hue.inputs['Color'])
 large=node(t,'ShaderNodeTexNoise','Uneven old grime',-1230,150);large.inputs['Scale'].default_value=8.;large.inputs['Detail'].default_value=5.;large.inputs['Roughness'].default_value=.75;link(t,coord,'Object',large,'Vector')
 dirt=ramp(node(t,'ShaderNodeValToRGB','Dark organic stains',-970,160),[(.32,(.015,.015,.015,1)),(.66,(.65,.65,.65,1))]);link(t,large,'Fac',dirt,'Fac')
 mix=node(t,'ShaderNodeMixRGB','Embedded brown-grey grime',-680,470);mix.inputs[2].default_value=(.08,.063,.043,1) if not skin_mode else (.11,.095,.13,1);link(t,dirt,'Color',mix,0);link(t,hue,'Color',mix,1)
 hem=node(t,'ShaderNodeAttribute','Cuffs / hem / ground contact',-1220,-790);hem.attribute_name='ab_ground_dirt'
 hem_noise=node(t,'ShaderNodeMath','Broken-up contact stains',-940,-790);hem_noise.operation='MULTIPLY';link(t,hem,'Fac',hem_noise,0);link(t,large,'Fac',hem_noise,1)
 dirt_sum=node(t,'ShaderNodeMath','Layered dirt accumulation',-690,80);dirt_sum.operation='ADD';dirt_sum.use_clamp=True;link(t,hem_noise,0,dirt_sum,0);link(t,dirt,'Color',dirt_sum,1);link(t,dirt_sum,0,mix,0)
 medium=node(t,'ShaderNodeTexNoise','Powder and dust patches',-1200,-150);medium.inputs['Scale'].default_value=34;medium.inputs['Detail'].default_value=3;link(t,coord,'Object',medium,'Vector')
 dust=ramp(node(t,'ShaderNodeValToRGB','Fine settled dust',-940,-140),[(.48,(0,0,0,1)),(.72,(.33,.33,.33,1))]);link(t,medium,'Fac',dust,'Fac')
 dusty=node(t,'ShaderNodeMixRGB','Dry mineral dust',-410,470);dusty.inputs[2].default_value=(.30,.26,.19,1);link(t,dust,'Color',dusty,0);link(t,mix,'Color',dusty,1)
 infection=node(t,'ShaderNodeAttribute','Crystalline root damage',-900,-440);infection.attribute_name='ab_infection'
 mottled=node(t,'ShaderNodeMath','Mask × irregular grime',-630,-300);mottled.operation='MULTIPLY';link(t,infection,'Fac',mottled,0);link(t,large,'Fac',mottled,1)
 boost=node(t,'ShaderNodeMath','Stained growth margins',-380,-270);boost.operation='MULTIPLY';boost.inputs[1].default_value=1.5;boost.use_clamp=True;link(t,mottled,0,boost,0)
 infected=node(t,'ShaderNodeMixRGB','Purple-grey mineral infiltration',-100,450);infected.inputs[2].default_value=(.075,.054,.088,1);link(t,boost,0,infected,0);link(t,dusty,'Color',infected,1);link(t,infected,'Color',bs,'Base Color')
 fine=node(t,'ShaderNodeTexNoise','Fabric grain / dusty pores',-680,-600);fine.inputs['Scale'].default_value=220;fine.inputs['Detail'].default_value=2;link(t,coord,'Object',fine,'Vector')
 bump=node(t,'ShaderNodeBump','Dust and worn fabric micro-relief',880,-200);bump.inputs['Strength'].default_value=.16 if skin_mode else .24;bump.inputs['Distance'].default_value=.001 if skin_mode else .002
 if bs.inputs['Normal'].is_linked:t.links.new(bs.inputs['Normal'].links[0].from_socket,bump.inputs['Normal'])
 link(t,fine,'Fac',bump,'Height');link(t,bump,'Normal',bs,'Normal')
 rough=node(t,'ShaderNodeMapRange','Dry varied roughness',550,-420);rough.inputs['From Min'].default_value=0;rough.inputs['From Max'].default_value=1;rough.inputs['To Min'].default_value=.65;rough.inputs['To Max'].default_value=.94
 link(t,medium,'Fac',rough,'Value');link(t,rough,'Result',bs,'Roughness');bs.inputs['Metallic'].default_value=0
 return m
body.data.materials[0]=weather_material(body.data.materials[0])
skin.data.materials[0]=weather_material(skin.data.materials[0],True)

# Restrained studio so facets and white-coat dirt can be judged from every side.
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world=bpy.data.worlds.new('AB | Neutral studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.09,.105,.14,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.38
scene.view_settings.view_transform='AgX'
def point(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for name,pos,power,color,size in [('Key',(-3,-4,5),420,(1,.88,.75),4),('Fill',(3,-2,3),280,(.65,.75,1),3),('Rim',(1,3,4),650,(.83,.72,1),3),('Soft back',(-3,2,2.5),180,(1,.90,.78),2.5)]:
 d=bpy.data.lights.new('AB | '+name,'AREA');d.energy=power;d.color=color;d.shape='DISK';d.size=size
 o=bpy.data.objects.new(d.name,d);studio.objects.link(o);o.location=pos;point(o,(0,0,1))
mesh=bpy.data.meshes.new('Studio floor');mesh.from_pydata([(-200,-200,-.012),(200,-200,-.012),(200,200,-.012),(-200,200,-.012)],[],[(0,1,2,3)]);mesh.update()
floor=bpy.data.objects.new('AB | Studio ground',mesh);studio.objects.link(floor)
mat=bpy.data.materials.new('AB | Warm grey ground');mat.diffuse_color=(.115,.105,.095,1);mat.use_nodes=True;mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.115,.105,.095,1);mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.9;floor.data.materials.append(mat)
camd=bpy.data.cameras.new('AB | Preview Camera');cam=bpy.data.objects.new(camd.name,camd);studio.objects.link(cam);camd.type='ORTHO';camd.ortho_scale=2.75;scene.camera=cam
cam.location=(3,-5,2.7);point(cam,(0,.06,1.05))
# Make the saved file open on a useful uncluttered material preview.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   space=area.spaces.active;space.overlay.show_overlays=False;space.shading.type='MATERIAL'
   space.region_3d.view_location=(0,.05,1.05);space.region_3d.view_distance=3.1;space.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
  elif area.type=='PROPERTIES':
   try:area.spaces.active.context='MODIFIER'
   except TypeError:pass
if bpy.context.window and bpy.data.workspaces.get('Layout'):
 bpy.context.window.workspace=bpy.data.workspaces['Layout']
# Pack only actual existing image data; original embedded textures remain self-contained.
for im in bpy.data.images:
 if im.size[0]>0 and im.type=='IMAGE' and not im.packed_file:
  try:im.pack()
  except Exception:pass
text=bpy.data.texts.new('READ ME - Resonator');text.write('Select clothing: AB Crystal Infestation Geometry Nodes modifier. Density, length, width, seed, upward growth and visibility are adjustable. Weight-paint AB_Growth / AB_Micro / AB_Length. Original armature precedes infestation so crystals remain surface attached. Shader layers preserve source UV textures. Texture baking + realized/skinned export is still required for Unity; procedural Blender nodes do not transfer through FBX. Source snapshot is preserved separately.')
# Validate actual evaluated geometry rather than only the modifier's existence.
bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();evaluated=body.evaluated_get(dg);em=evaluated.to_mesh()
report={'source_body_vertices':len(body.data.vertices),'evaluated_body_vertices':len(em.vertices),'evaluated_body_faces':len(em.polygons),'geometry_nodes':g.name,'node_count':len(g.nodes),'mask_ranges':{},'materials':[m.name for m in em.materials if m],'scene_scale_metres':1.8}
assert len(em.vertices)>len(body.data.vertices)+300,'No crystals generated'
evaluated.to_mesh_clear()
(ROOT/'Source'/'build_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
scene.render.filepath=str(ROOT/'Preview'/'Scientist_Resonator_Hero.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Scientist_Resonator.blend'))
for name,pos,target in [('Hero',(3,-5,2.7),(0,.06,1.05)),('Front',(0,-6,1.12),(0,0,1.12)),('Side',(6,0,1.12),(0,0,1.12)),('Back',(0,6,1.12),(0,0,1.12))]:
 cam.location=pos;point(cam,target);scene.render.filepath=str(ROOT/'Preview'/('Scientist_Resonator_'+name+'.png'));bpy.ops.render.render(write_still=True)
print('RESONATOR_BUILD_COMPLETE',json.dumps(report))
