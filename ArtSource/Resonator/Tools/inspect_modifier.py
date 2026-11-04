import bpy
m=bpy.data.objects['sci_body_dirty.smd.001'].modifiers[-1]
for key in ['Socket_2','Socket_8']:
 p=getattr(m.properties.inputs,key)
 print('INPUT',key,[(x.identifier,x.type) for x in p.bl_rna.properties])
 for x in p.bl_rna.properties:
  if x.identifier!='rna_type':
   try:print(x.identifier,repr(getattr(p,x.identifier)))
   except:pass
