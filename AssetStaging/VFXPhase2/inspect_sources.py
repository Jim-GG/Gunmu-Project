import bpy,numpy as np,json,struct,zlib
from pathlib import Path
out=Path('AssetStaging/VFXPhase2')
def png(p,a):
 a=np.asarray(a,dtype=np.uint8);h,w=a.shape[:2]
 def ch(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 p.write_bytes(b'\x89PNG\r\n\x1a\n'+ch(b'IHDR',struct.pack('>IIBBBBB',w,h,8,2,0,0,0))+ch(b'IDAT',zlib.compress(b''.join(b'\0'+r.tobytes() for r in a)))+ch(b'IEND',b''))
root=Path('D:/特效纹理/95张4K高清烟火爆炸序列图/95张4K高清烟火爆炸序列图');files=[root/f'Explosion_{i:02}_8x8.tga' for i in (1,3,5,9,14,19,24,30)];sheet=np.zeros((len(files)*160,4*160,3),np.uint8)
for k,p in enumerate(files):
 im=bpy.data.images.load(str(p));w,h=im.size;a=np.array(im.pixels[:],dtype=np.float32).reshape(h,w,4)
 for j,frame in enumerate((6,16,30,48)):
  x=frame%8;y=7-frame//8;crop=a[y*h//8:(y+1)*h//8,x*w//8:(x+1)*w//8];tmp=bpy.data.images.new('frame',width=w//8,height=h//8);tmp.pixels=crop.reshape(-1);tmp.scale(160,160);rgb=np.array(tmp.pixels[:]).reshape(160,160,4)[::-1,:,:3];sheet[k*160:(k+1)*160,j*160:(j+1)*160]=np.clip(rgb*255,0,255).astype(np.uint8);bpy.data.images.remove(tmp)
 bpy.data.images.remove(im)
png(out/'explosion_contact.png',sheet)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);bpy.ops.import_scene.fbx(filepath=str(Path('Assets/Game/VFX/Combat/击中后溢气流.fbx').resolve()));data=[]
for ob in bpy.context.scene.objects:
 if ob.type=='MESH':data.append({'name':ob.name,'vertices':len(ob.data.vertices),'faces':len(ob.data.polygons),'uvLayers':len(ob.data.uv_layers),'dimensions':list(ob.dimensions),'matrix':[list(r) for r in ob.matrix_world],'bounds':[list(v) for v in ob.bound_box]})
(out/'wind_mesh.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8');print('INSPECTION_READY')
