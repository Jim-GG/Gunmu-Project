import bpy,numpy as np
from pathlib import Path
for p in Path('Assets/Game/VFX/Combat/Textures').glob('Smoke*'):
 if p.suffix.lower()!='.tga':continue
 im=bpy.data.images.load(str(p.resolve()));a=np.array(im.pixels[3::4]);print(p.name,'ALPHA',float(a.min()),float(a.max()),float(a.mean()));bpy.data.images.remove(im)

