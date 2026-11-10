import struct,zlib,numpy as np
from pathlib import Path
for p in Path('Assets/Game/VFX/Combat/Textures/Crater').glob('*_Height.png'):
 data=p.read_bytes();pos=8;compressed=b''
 while pos<len(data):
  n=struct.unpack('>I',data[pos:pos+4])[0];kind=data[pos+4:pos+8];payload=data[pos+8:pos+8+n];pos+=12+n
  if kind==b'IHDR':w,h,bit,c,_,_,_=struct.unpack('>IIBBBBB',payload)
  if kind==b'IDAT':compressed+=payload
 raw=zlib.decompress(compressed);rows=np.frombuffer(raw,dtype=np.uint8).reshape(h,w*2+1);assert np.all(rows[:,0]==0)
 a=rows[:,1:].copy().view('>u2').reshape(h,w);a=np.minimum(a.astype(np.uint32)*2,65535).astype('>u2')
 def ch(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 p.write_bytes(b'\x89PNG\r\n\x1a\n'+ch(b'IHDR',struct.pack('>IIBBBBB',w,h,16,0,0,0,0))+ch(b'IDAT',zlib.compress(b''.join(b'\0'+row.tobytes() for row in a)))+ch(b'IEND',b''))
 print(p)
p=Path('AssetStaging/VFXUpgrade/generate_craters.py');s=p.read_text();s=s.replace('np.round(h*65535),16','np.round(np.clip(h*2,0,1)*65535),16');p.write_text(s)
