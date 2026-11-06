"""Author UV-locked surface variation and the matching Blender layered materials."""
import bpy,json,math,numpy as np
from pathlib import Path
ROOT=Path('D:/UNITY工程/棍木项目/棍木项目');ART=ROOT/'ArtSource/ModernLab';TEX=ROOT/'Assets/Game/Environment/ModernLab/Textures'
N=2048;axis=np.linspace(0,1,N,dtype=np.float32);U,V=np.meshgrid(axis,axis);report={}
def smooth(a,b,x):
 t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)
def noise(seed,f,u=U,v=V):
 fx,fy=(f,f) if isinstance(f,int) else f;r=np.random.default_rng(seed);angle=r.random((fy,fx),dtype=np.float32)*(2*math.pi);gx=np.cos(angle);gy=np.sin(angle)
 x=np.mod(u,1)*fx;y=np.mod(v,1)*fy;ix=np.floor(x).astype(np.int32);iy=np.floor(y).astype(np.int32);tx=(x-ix).astype(np.float32);ty=(y-iy).astype(np.float32)
 sx=tx*tx*tx*(tx*(tx*6-15)+10);sy=ty*ty*ty*(ty*(ty*6-15)+10)
 def dot(dx,dy):return gx[(iy+dy)%fy,(ix+dx)%fx]*(tx-dx)+gy[(iy+dy)%fy,(ix+dx)%fx]*(ty-dy)
 a=dot(0,0)*(1-sx)+dot(1,0)*sx;b=dot(0,1)*(1-sx)+dot(1,1)*sx
 return np.clip(.5+(a*(1-sy)+b*sy)*.9,0,1).astype(np.float32)
WU=(U+(noise(81,4)-.5)*.055).astype(np.float32);WV=(V+(noise(82,4)-.5)*.055).astype(np.float32)
cloud=noise(1,5,WU,WV)*.55+noise(2,17,WU,WV)*.3+noise(3,43,WU,WV)*.15
patch=noise(4,32,WU,WV)-.5;grain=noise(5,160,WU,WV)-.5;fine=noise(6,450)-.5
pores=smooth(.73,.94,noise(7,300,WU,WV))
def spots(seed,count):
 out=np.zeros((N,N),np.float32);r=np.random.default_rng(seed)
 for i in range(count):
  x,y=r.integers(0,N,size=2)
  if cloud[y,x]<.41 and r.random()<.8:continue
  radius=float(r.uniform(.9,3.1) if r.random()<.965 else r.uniform(5,10));rx=radius*r.uniform(.65,1.4);ry=radius*r.uniform(.65,1.4);extent=int(max(rx,ry)*1.7)+2
  yy,xx=np.mgrid[-extent:extent+1,-extent:extent+1];angle=r.uniform(0,math.pi);px=xx*math.cos(angle)-yy*math.sin(angle);py=xx*math.sin(angle)+yy*math.cos(angle)
  shape=1-np.sqrt((px/rx)**2+(py/ry)**2);shape+=(r.random(shape.shape)-.5)*.38;shape=smooth(-.14,.16,shape).astype(np.float32)*r.uniform(.6,1)
  ys=(np.arange(y-extent,y+extent+1)%N);xs=(np.arange(x-extent,x+extent+1)%N);out[np.ix_(ys,xs)]=np.maximum(out[np.ix_(ys,xs)],shape)
 return out
def joints(nx,ny,width):
 x=np.mod(U*nx,1);y=np.mod(V*ny,1);dx=np.minimum(x,1-x)*2/nx;dy=np.minimum(y,1-y)*2/ny
 return np.maximum(np.exp(-(dx/width)**2),np.exp(-(dy/width)**2)).astype(np.float32)
def normal(height,tile=2):
 du=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*(height.shape[1]-1)/(2*tile);dv=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*(height.shape[0]-1)/(2*tile)
 n=np.stack([-du,-dv,np.ones_like(height)],axis=2);n/=np.linalg.norm(n,axis=2)[:,:,None];return n*.5+.5
def image(name,values,colour=False):
 values=np.asarray(values,np.float32)
 if values.ndim==2:values=np.repeat(values[:,:,None],3,axis=2)
 if values.shape[2]==3:values=np.concatenate([values,np.ones((*values.shape[:2],1),np.float32)],axis=2)
 values=np.clip(values,0,1);values[-1]=values[0];values[:,-1]=values[:,0];size=values.shape[0]
 im=bpy.data.images.get(name)
 if im is None:im=bpy.data.images.new(name,width=size,height=size,alpha=True)
 elif tuple(im.size)!=(size,size):im.scale(size,size)
 if im.packed_file:im.unpack(method='REMOVE')
 im.colorspace_settings.name='sRGB' if colour else 'Non-Color';im.alpha_mode='CHANNEL_PACKED';im.pixels.foreach_set(values.ravel());im.update();im.filepath_raw=str(TEX/(name+'.png'));im.file_format='PNG';im.save();im.pack();return im
def params(kind):
 rough=kind in ['mineral','coursed','ceiling','ribbed']
 amount={'mineral':0.,'coursed':0.,'ceiling':0.,'ribbed':0.,'matte':.05,'dark':.08,'gloss':.18,'steel':.12,'white_steel':.09,'resin':.10,'checker':.06}[kind]
 return dict(dust_amount=amount,dust_colour_strength=0. if rough else .01,dust_roughness=.45 if kind in ['gloss','resin','checker'] else .55, dust_tiling=2.,spot_amount=1.,normal_strength=1.,micro_strength=.6 if rough else .16,micro_tiling=8.,roughness_bias=0.,detail='Lab_Detail_Mineral_Normal' if rough else 'Lab_Detail_Coating_Normal')
def author(key,kind,seed):
 metallic=np.zeros((N,N),np.float32);ao=np.ones_like(metallic);h=np.zeros_like(metallic);sp=spots(seed+90,1000 if kind=='white_steel' else 160 if kind=='steel' else 70)
 variation=(cloud-.5)*.025+patch*.012
 # Fine, low-contrast roughness breakup. No thresholded cloud silhouettes.
 dust=(noise(seed,40,WU,WV)*.25+noise(seed+1,120,WU,WV)*.50+noise(seed+2,280,WU,WV)*.25)*.55
 wipe=noise(seed+3,(23,170),WU,WV);dust*=.65+wipe*.35
 if kind in ['mineral','coursed']:
  colour=np.stack([.865+variation,.873+variation,.864+variation],axis=2);h=patch*.00028+grain*.00032+fine*.00016-pores*.00038;r=.79+patch*.13+grain*.1+pores*.08
  if kind=='coursed':
   row=np.floor(V*8);x=np.mod(U*4+np.mod(row,2)*.5,1);y=np.mod(V*8,1);d=np.minimum(x,1-x)*.5;dy=np.minimum(y,1-y)*.25;j=np.maximum(np.exp(-(d/.0017)**2),np.exp(-(dy/.0017)**2));h-=j*.0013;colour-=j[:,:,None]*.09;ao-=j*.15;r+=j*.1
 elif kind in ['matte','dark']:
  j=joints(2,1 if kind=='matte' else 2,.002);base=(.815,.827,.826) if kind=='matte' else (.205,.225,.235);colour=np.stack([v+variation*.7 for v in base],axis=2);colour-=j[:,:,None]*(.38 if kind=='matte' else .065);h=-j*.00065+grain*.00013+fine*.000055-pores*.0001;r=(.65 if kind=='matte' else .49)+patch*.15+grain*.07;ao-=j*.14
 elif kind=='gloss':
  colour=np.stack([.9+variation*.4,.908+variation*.4,.905+variation*.4],axis=2);h=patch*.000024+grain*.000018+fine*.000009;r=.225+patch*.10+grain*.025
 elif kind in ['steel','white_steel']:
  brush=noise(seed+8,(7,700))-.5;h=brush*.000012+grain*.00001-sp*.000035;r=.30+patch*.12+brush*.10
  if kind=='steel':metallic[:]=1;colour=np.stack([.64+variation,.67+variation,.69+variation],axis=2)
  else:
   chips=spots(seed+110,240);metallic=chips;h=grain*.000025+fine*.00001-chips*.0005-sp*.00004;r=.37+patch*.12;colour=np.stack([.88+variation,.895+variation,.895+variation],axis=2);colour=colour*(1-chips[:,:,None])+np.array([.53,.56,.59],np.float32)*chips[:,:,None];r=r*(1-chips)+.48*chips
 elif kind=='resin':
  colour=np.stack([.47+variation*.6,.50+variation*.6,.51+variation*.6],axis=2);scuff=smooth(.67,.88,noise(seed+4,(16,130),WU,WV));h=patch*.00003+grain*.000012+scuff*.000008;r=.22+patch*.10+scuff*.16
 elif kind=='checker':
  parity=((np.floor(U*4).astype(np.int32)+np.floor(V*4).astype(np.int32))%2).astype(np.float32);j=joints(4,4,.0015);colour=np.empty((N,N,3),np.float32)
  for c,(a,b) in enumerate(zip([.86,.87,.86],[.52,.55,.565])):colour[:,:,c]=a*(1-parity)+b*parity+variation*.5-j*.09
  h=-j*.0006+grain*.000018+patch*.00003;r=.32+parity*.025+patch*.08+j*.46;ao-=j*.14
 elif kind=='ceiling':
  j=joints(4,4,.002);hx=np.minimum(np.mod(U*100,1),1-np.mod(U*100,1));hy=np.minimum(np.mod(V*100,1),1-np.mod(V*100,1));holes=np.exp(-((hx*hx+hy*hy)/.018)**2)*(1-j)
  colour=np.stack([.83+variation,.84+variation,.83+variation],axis=2)-j[:,:,None]*.35-holes[:,:,None]*.13;h=-j*.001-holes*.0007+grain*.00014+fine*.00009;r=.86+patch*.13+grain*.08;ao-=j*.15+holes*.045
 elif kind=='ribbed':
  j=joints(1,1,.003);wave=(1-np.cos(U*2*np.pi*24))*.5;colour=np.stack([.16+variation*.6,.18+variation*.6,.185+variation*.6],axis=2);h=wave*.006-j*.0008+grain*.00018+fine*.000065-pores*.00023;r=.72+patch*.16+grain*.10;ao-=(1-wave)*.055+j*.1
 else:raise ValueError(kind)
 r=np.clip(r,.10,.97);ao=np.clip(ao,.65,1);colour=np.clip(colour,.02,.96)
 base=image(key+'_BaseColor',np.concatenate([colour,sp[:,:,None]],axis=2),True);nm=image(key+'_Normal',normal(h));mask=image(key+'_Mask',np.stack([metallic,ao,dust,1-r],axis=2))
 p=params(kind);report[key]=dict(kind=kind,**p,roughness_min=float(r.min()),roughness_max=float(r.max()),roughness_mean=float(r.mean()),dust_mean=float(dust.mean()),spot_coverage=float((sp>.35).mean()),metallic_max=float(metallic.max()),normal_slope_rms=float(np.sqrt(np.mean(np.square(normal(h)[:,:,:2]*2-1)))),files=[x.filepath for x in [base,nm,mask]],tint=[1,1,1])
 print('SURFACE_V2',key,flush=True)
for key,kind,seed in [('Lab_White_Matte','matte',10),('Lab_Dark_Panel','dark',20),('Lab_Floor_Resin','resin',30),('Lab_Rail_BrushedSteel','steel',40),('Lab_Floor_Checker','checker',50),('Lab_Ceiling_Acoustic','ceiling',60),('Lab_Wall_Ribbed','ribbed',70),('Lab_Paint_RoughWhite','mineral',80),('Lab_Paint_GlossWhite','gloss',90),('Lab_White_Coursed','coursed',100),('Lab_Paint_WhiteSteel','white_steel',110)]:author(key,kind,seed)
for key,source,tint in [('Lab_Paint_Lime','Lab_Paint_RoughWhite',[.53,.94,.025]),('Lab_Paint_Red','Lab_Paint_RoughWhite',[.97,.065,.035]),('Lab_Paint_Yellow','Lab_Paint_GlossWhite',[.99,.79,.025])]:report[key]={**report[source], 'source':source,'tint':tint}
# The shared micro surface repeats every 25 cm, not every 2 metres.
q=np.linspace(0,1,1024,dtype=np.float32);uu,vv=np.meshgrid(q,q)
micro=noise(710,80,uu,vv)*.6+noise(711,190,uu,vv)*.4-.5
image('Lab_Detail_Mineral_Normal',normal(micro*.000085,.25));image('Lab_Detail_Coating_Normal',normal(micro*.000013,.25))

def material(key,p):
 m=bpy.data.materials.get(key) or bpy.data.materials.new(key);m.use_nodes=True;m.asset_mark();m.asset_data.description='V2 UV-locked layered PBR / '+p['kind'];m['lab_surface']=key;m['surface_revision']=2
 t=m.node_tree;t.nodes.clear();l=t.links
 def node(kind,name,x,y):
  n=t.nodes.new(kind);n.label=name;n.location=(x,y);return n
 def mathop(op,a,b,name=''):
  n=node('ShaderNodeMath',name,0,0);n.operation=op
  for i,v in enumerate([a,b]):
   if hasattr(v,'node'):l.new(v,n.inputs[i])
   else:n.inputs[i].default_value=v
  return n.outputs[0]
 def mix(a,b,factor,label,x,y):
  n=node('ShaderNodeMixRGB',label,x,y);n.blend_type='MIX'
  for i,v in enumerate([factor,a,b]):
   if hasattr(v,'node'):l.new(v,n.inputs[i])
   elif isinstance(v,(tuple,list)):n.inputs[i].default_value=tuple(v)
   else:n.inputs[i].default_value=(v,v,v,1) if i else v
  return n.outputs[0]
 uv=node('ShaderNodeUVMap','Model UV0 / fixed to surface',-1300,200)
 source=p.get('source',key);textures=[]
 for suffix,y in [('BaseColor',450),('Normal',100),('Mask',-250)]:
  n=node('ShaderNodeTexImage',suffix,-1050,y);n.image=bpy.data.images[source+'_'+suffix];n.extension='REPEAT';l.new(uv.outputs['UV'],n.inputs['Vector']);textures.append(n)
 base,norm,mask=textures;sep=node('ShaderNodeSeparateColor','R metal / G AO / B dust',-800,-250);l.new(mask.outputs['Color'],sep.inputs['Color'])
 tint=node('ShaderNodeRGB','TINT / 主色调',-1030,710);tint.outputs[0].default_value=tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in p['tint'])+(1,)
 mul=node('ShaderNodeMixRGB','Paint tint',-730,480);mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1;l.new(base.outputs['Color'],mul.inputs[1]);l.new(tint.outputs[0],mul.inputs[2])
 colour=mix(mul.outputs[0],base.outputs['Color'],sep.outputs['Red'],'Bare metal bypasses tint',-470,480)
 dust_scale=node('ShaderNodeVectorMath','Fine dust UV only',-1310,-350);dust_scale.operation='SCALE';dust_scale.inputs['Scale'].default_value=p['dust_tiling'];l.new(uv.outputs['UV'],dust_scale.inputs[0]);dust_tex=node('ShaderNodeTexImage','Fine dust mask B',-1060,-1120);dust_tex.image=bpy.data.images[source+'_Mask'];l.new(dust_scale.outputs[0],dust_tex.inputs['Vector']);dust_sep=node('ShaderNodeSeparateColor','Dust B only',-810,-1120);l.new(dust_tex.outputs['Color'],dust_sep.inputs[0])
 dust_value=node('ShaderNodeValue','DUST AMOUNT / 薄灰强度',-1050,800);dust_value.outputs[0].default_value=p['dust_amount']
 spot=mathop('MULTIPLY',base.outputs['Alpha'],p['spot_amount'],'Black spot amount');dust=mathop('MINIMUM',mathop('MULTIPLY',dust_sep.outputs['Blue'],dust_value.outputs[0]),1,'Dust coverage')
 colour=mix(colour,(.018,.021,.023,1),spot,'Neutral dirt after tint',-220,480)
 dust_colour=tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in [.56,.57,.56])+(1,)
 colour=mix(colour,dust_colour,mathop('MULTIPLY',dust,p['dust_colour_strength']),'Thin neutral dust',40,480)
 rough=mathop('SUBTRACT',1,mask.outputs['Alpha']);rough=mix(rough,.83,spot,'Spot roughness',-200,-100);rough=mix(rough,p['dust_roughness'],dust,'Dust roughness',70,-100)
 metal=mathop('MULTIPLY',sep.outputs['Red'],mathop('SUBTRACT',1,spot));metal=mathop('MULTIPLY',metal,mathop('SUBTRACT',1,dust))
 nmap=node('ShaderNodeNormalMap','Main surface normal',-440,100);nmap.inputs['Strength'].default_value=p['normal_strength'];l.new(norm.outputs['Color'],nmap.inputs['Color'])
 # Stack a shared UV detail bump on the main normal. Blender and Unity sample
 # the same detail normal; RNM mixing is implemented explicitly below.
 scale=node('ShaderNodeVectorMath','Micro UV repeat',-1300,-670);scale.operation='SCALE';scale.inputs['Scale'].default_value=p['micro_tiling'];l.new(uv.outputs['UV'],scale.inputs[0]);detail=node('ShaderNodeTexImage','Shared micro normal',-1050,-670);detail.image=bpy.data.images[p['detail']];l.new(scale.outputs[0],detail.inputs['Vector'])
 # RNM is most reliably built as vector math on decoded tangent normals.
 def vm(op,a,b=None):
  n=node('ShaderNodeVectorMath','RNM '+op,-400,-650);n.operation=op
  for i,v in enumerate([a,b]):
   if v is None:continue
   if hasattr(v,'node'):l.new(v,n.inputs[i])
   else:n.inputs[i].default_value=v
  return n.outputs[0] if op!='DOT_PRODUCT' else n.outputs['Value']
 a=vm('ADD',vm('SUBTRACT',vm('MULTIPLY',norm.outputs['Color'],(2,2,2)),(1,1,1)),(0,0,1))
 d=vm('SUBTRACT',vm('MULTIPLY',detail.outputs['Color'],(2,2,2)),(1,1,1));d=vm('MULTIPLY',d,(p['micro_strength'],p['micro_strength'],1));d=vm('NORMALIZE',d);b=vm('MULTIPLY',d,(-1,-1,1));dot=vm('DOT_PRODUCT',a,b)
 az=node('ShaderNodeSeparateXYZ','RNM denominator',-100,-650);l.new(a,az.inputs[0]);ratio=mathop('DIVIDE',dot,az.outputs['Z']);scaled=node('ShaderNodeVectorMath','RNM projection',80,-600);scaled.operation='SCALE';l.new(a,scaled.inputs[0]);l.new(ratio,scaled.inputs['Scale']);rnm=vm('NORMALIZE',vm('SUBTRACT',scaled.outputs[0],b));encoded=vm('MULTIPLY',vm('ADD',rnm,(1,1,1)),(.5,.5,.5))
 combined=node('ShaderNodeNormalMap','Combined tangent normal',320,80);l.new(encoded,combined.inputs['Color'])
 bs=node('ShaderNodeBsdfPrincipled','V2 Surface',580,400);l.new(colour,bs.inputs['Base Color']);l.new(rough,bs.inputs['Roughness']);l.new(metal,bs.inputs['Metallic']);l.new(combined.outputs[0],bs.inputs['Normal']);bs.inputs['IOR'].default_value=1.48
 out=node('ShaderNodeOutputMaterial','Surface output',970,400);l.new(bs.outputs[0],out.inputs['Surface'])
 # Lay out math helpers rather than leaving them all on top of one another.
 helpers=[n for n in t.nodes if n.type in ['MATH','VECT_MATH'] and n.label!='Micro UV repeat']
 for i,n in enumerate(helpers):n.location=(-740+(i%6)*190,-1050-(i//6)*170)
 m['v2_parameters']=json.dumps(p,ensure_ascii=False);return m
for key,p in report.items():material(key,p)
# Preserve 36 module identities; switch only the white upper slot of red/green
# wall modules to the horizontal coursed white material requested in references.
for scene_name in ['Additional Modules','Additional Surface Review']:
 for o in bpy.data.scenes[scene_name].objects:
  if '分区墙_Lime' in o.name or '分区墙_Red' in o.name:o.data.materials[0]=bpy.data.materials['Lab_White_Coursed']
(ART/'surface_v2_report.json').write_text(json.dumps({'revision':2,'uv_space':'UV0','tile_metres':2,'micro_tile_metres':.25,'mask':'R metal / G AO / B dust / A clean smoothness','base_alpha':'black spot coverage, opaque surface','materials':report},ensure_ascii=False,indent=2),encoding='utf-8')
(ART/'surface_v2_unity.json').write_text(json.dumps({'materials':[dict(name=k,**v) for k,v in report.items()]},ensure_ascii=False,indent=2),encoding='utf-8')
bpy.data.libraries.write(str(ART/'ModernLab_Materials.blend'),{bpy.data.materials[k] for k in report},fake_user=True,compress=True)
bpy.context.window.scene=bpy.data.scenes['Additional Surface Review'];bpy.ops.wm.save_as_mainfile(filepath=str(ART/'ModernLab_ModularKit.blend'),compress=True)
print('SURFACE_V2_AUTHORING_SAVED',len(report),flush=True)
