import numpy as np, struct,zlib,json,shutil,xml.etree.ElementTree as ET
from pathlib import Path
OUT=Path('Assets/Game/VFX/Combat/Textures/Crater');OUT.mkdir(parents=True,exist_ok=True)
def png(path,a,bits=8):
 a=np.asarray(a);h,w=a.shape[:2];c=0 if a.ndim==2 else 2 if a.shape[2]==3 else 6
 a=a.astype('>u2' if bits==16 else np.uint8)
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 Path(path).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,bits,c,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(b'\0'+a[y].tobytes() for y in range(h))))+chunk(b'IEND',b''))
def smooth(a,b,x):
 t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)
def noise(n,size,rng):
 p=rng.random((size+1,size+1)).astype(np.float32);q=np.linspace(0,size,n,endpoint=False,dtype=np.float32);i=q.astype(int);f=q-i;f=f*f*(3-2*f)
 return ((p[i[:,None],i]*(1-f)+p[i[:,None],i+1]*f)*(1-f[:,None])+(p[i[:,None]+1,i]*(1-f)+p[i[:,None]+1,i+1]*f)*f[:,None])
previews=[]
for index in range(2):
 n=2048;rng=np.random.default_rng(7103+index*891);xx=np.linspace(-1.15,1.15,n,dtype=np.float32);x,y=np.meshgrid(xx,xx);y=y/(.92 if index else 1)
 coarse=noise(n,18,rng)-.5;fine=noise(n,170,rng)-.5;grain=noise(n,550,rng)-.5
 xw=x+coarse*.035;yw=y+noise(n,16,rng)*.03-.015;r=np.sqrt(xw*xw+yw*yw);theta=np.arctan2(yw,xw);edge=.98+.035*np.sin(theta*7+index)+.02*np.sin(theta*13+.7)
 mask=1-smooth(edge-.035,edge+.035,r);best=np.full((n,n),99,np.float32);second=best.copy();base=best.copy();seedid=np.zeros((n,n),np.int16);k=0
 for ring,count in [(0,1),(.23,9),(.45,15),(.67,22),(.86,28),(1.12,32)]:
  for j in range(count):
   a=(j+rng.uniform(-.28,.28))*np.pi*2/count+ring*.9;rr=ring+rng.uniform(-.035,.035);sx,sy=np.cos(a)*rr,np.sin(a)*rr;d=(xw-sx)**2+(yw-sy)**2
   take=d<best;second=np.where(take,best,np.minimum(second,d));best=np.minimum(best,d)
   level=.5-.32*np.exp(-(max(0,rr)/.4)**2)+.042*np.exp(-((rr-.8)/.18)**2)+rng.uniform(-.02,.02)
   plate=level+(xw-sx)*sx*.12+(yw-sy)*sy*.12
   base=np.where(take,plate,base);seedid=np.where(take,k,seedid);k+=1
 gap=np.sqrt(second)-np.sqrt(best)+fine*.007+grain*.004;fracture=smooth(.003,.014+np.maximum(coarse,0)*.008,gap)
 h=(.07+.12*np.minimum(r,1))*(1-fracture)+base*fracture
 h-=.13*np.exp(-(r/.16)**2);h+=coarse*.014+fine*.012+grain*.006;h=np.minimum(h,.58);h=.5*(1-mask)+h*mask
 # Additional thin radial fractures extend irregularly into the exterior.
 for j in range(11):
  a=rng.uniform(-np.pi,np.pi);d=np.abs(np.arctan2(np.sin(theta-a),np.cos(theta-a))+.02*np.sin(r*18+j))
  crack=(1-smooth(.001,.004,d))*smooth(.45,.7,r)*(1-smooth(.93,1.12,r));h-=crack*.06
 h=np.clip(h,0,1);ao=np.zeros_like(h)
 for radius in (5,16,42,85):
  for dx,dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)):
   sampled=np.roll(h,(dy*radius,dx*radius),(0,1));ao+=np.clip((sampled-h-.002)/(radius/n*2.3+.035),0,1)
 ao=np.exp(-ao/32*4.5)*(1-.22*mask*(1-fracture));ao*=.25+.75*smooth(.06,.3,r);ao=1-mask+ao*mask
 dy,dx=np.gradient(h*.5,4.6/n);normal=np.stack((-dx,dy,np.ones_like(h)),axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
 albedo=np.clip((.25+.75*smooth(.05,.28,r))*(.42+coarse*.08+fine*.025)*(.5+.5*ao),0,1);rgb=np.stack((albedo*1.025,albedo,albedo*.95),axis=-1)
 prefix=OUT/('DrillCrater_'+str(index+1));png(str(prefix)+'_Height.png',np.round(np.clip(h*2,0,1)*65535),16);png(str(prefix)+'_AO.png',np.round(ao*255));png(str(prefix)+'_Normal.png',np.round((normal*.5+.5)*255));png(str(prefix)+'_Mask.png',np.round(mask*255));png(str(prefix)+'_BaseColor.png',np.round(np.dstack((rgb,mask))*255))
 shade=np.clip((normal[:,:,0]*-.35+normal[:,:,1]*-.45+normal[:,:,2]*.8)*ao,0,1);previews.append(np.repeat(shade[::4,::4,None],3,axis=2));np.save('AssetStaging/VFXUpgrade/crater_height_'+str(index+1)+'.npy',h[::4,::4])
png('AssetStaging/VFXUpgrade/crater_preview.png',np.round(np.concatenate(previews,axis=1)*255))
# Use only a selected subset of the user-owned source library.
candidates=json.loads(Path('AssetStaging/VFXUpgrade/texture_candidates.json').read_text(encoding='utf8'));chosen=[1,5,7,12,13,20,22];imports=[]
for i in chosen:
 p=Path(candidates[i]['path']);dest=Path('Assets/Game/VFX/Combat/Textures')/p.name;shutil.copy2(p,dest);imports.append({'source':str(p),'destination':str(dest)})
Path('AssetStaging/VFXUpgrade/imported_sources.json').write_text(json.dumps(imports,ensure_ascii=False,indent=2),encoding='utf8')
source=Path('D:/AdobeSubstanceDesigner/工程/特效/贴花.sbs');shutil.copy2(source,'AssetStaging/VFXUpgrade/Reference_OriginalDecal.sbs');r=ET.parse(source).getroot();Path('AssetStaging/VFXUpgrade/reference_graph_summary.json').write_text(json.dumps({'source':str(source),'nodes':len(r.findall('.//compNode')),'dependencies':[x.get('v') for x in r.findall('./dependencies/dependency/filename')]},ensure_ascii=False,indent=2),encoding='utf8')
print('CRATER_MAPS_AND_TEXTURES_READY')


