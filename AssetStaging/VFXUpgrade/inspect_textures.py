import bpy, numpy as np, json, os, struct, zlib
from pathlib import Path
root=Path('D:/特效纹理');out=Path('AssetStaging/VFXUpgrade');out.mkdir(parents=True,exist_ok=True)
def png(p,a):
 a=np.asarray(a,dtype=np.uint8);h,w=a.shape[:2];c=0 if a.ndim==2 else 2 if a.shape[2]==3 else 6
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 raw=b''.join(b'\0'+a[y].tobytes() for y in range(h));Path(p).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,c,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
files=[]
for pattern,n in [('Smoke*',12),('Noise*',8),('electric*',6)]:
 fs=sorted(p for p in root.rglob(pattern) if p.suffix.lower() in ['.tga','.png']);files.extend(fs[:n])
sheet=np.zeros((5*192,6*192,3),np.uint8);legend=[]
for k,p in enumerate(files):
 im=bpy.data.images.load(str(p),check_existing=False);im.scale(192,192);a=np.array(im.pixels[:],dtype=np.float32).reshape(192,192,4)[::-1];sheet[(k//6)*192:(k//6+1)*192,(k%6)*192:(k%6+1)*192]=np.clip(a[:,:,:3]*255,0,255).astype(np.uint8);legend.append({'index':k,'name':p.name,'path':str(p)});bpy.data.images.remove(im)
png(out/'texture_contact_sheet.png',sheet);(out/'texture_candidates.json').write_text(json.dumps(legend,ensure_ascii=False,indent=2),encoding='utf8')
print('CONTACT_SHEET_READY',len(files))
