import numpy as np,struct,zlib,shutil,json
from pathlib import Path
out=Path('Assets/Game/VFX/Combat/Advanced/Textures');n=512;yy,xx=np.mgrid[0:1:complex(n),0:1:complex(n)];rng=np.random.default_rng(9731);v=np.zeros((n,n))
for i in range(38):
 center=rng.random();width=rng.uniform(.0015,.006);shift=np.sin(yy*rng.uniform(4,12)+rng.random()*6.28)*.006;d=np.abs((xx-center+shift+.5)%1-.5);line=np.exp(-(d/width)**2);segments=(.35+.65*(.5+.5*np.sin(yy*rng.uniform(8,24)+i))**2);v=np.maximum(v,line*segments)
a=np.stack([v,v,v],axis=-1);a=(np.clip(a,0,1)*255).astype(np.uint8)
def png(p,a):
 h,w=a.shape[:2]
 def ch(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 p.write_bytes(b'\x89PNG\r\n\x1a\n'+ch(b'IHDR',struct.pack('>IIBBBBB',w,h,8,2,0,0,0))+ch(b'IDAT',zlib.compress(b''.join(b'\0'+r.tobytes() for r in a)))+ch(b'IEND',b''))
png(out/'WindLines.png',a);png(out/'EnergyRegionMask.png',np.full((64,64,3),255,np.uint8))
root=Path('D:/特效纹理/95张4K高清烟火爆炸序列图/95张4K高清烟火爆炸序列图');sources=[]
for name in ('Explosion_14_8x8.tga','Explosion_30_8x8.tga','Smoke24_8x8.TGA'):
 p=root/name;shutil.copy2(p,out/name);sources.append({'source':str(p),'destination':str(out/name)})
Path('AssetStaging/VFXPhase2/imported_sources.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf8')
