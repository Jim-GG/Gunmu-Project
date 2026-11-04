"""Validate saved geometry-node controls without modifying the saved asset."""
import bpy,json,math
from pathlib import Path
from mathutils import Quaternion
root=Path(__file__).resolve().parents[1]
body=bpy.data.objects['sci_body_dirty.smd.001'];skin=bpy.data.objects['../base_male/base_male.smd.001'];rig=bpy.data.objects['scientist_hurt_ARM.001']
mod=body.modifiers[-1];g=mod.node_group
assert mod.type=='NODES' and body.modifiers[0].type=='ARMATURE'
assert len(rig.data.bones)>50
assert all(x.is_valid for x in g.links)

def control(name):
 s=next(x for x in g.interface.items_tree if x.name==name and x.in_out=='INPUT')
 # Blender 5.2 stores inputs as RNA socket values, older Blender uses ID properties.
 return getattr(mod.properties.inputs,s.identifier)
def evaluated():
 body.update_tag();bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();o=body.evaluated_get(dg);mesh=o.to_mesh();points=[tuple(v.co) for v in mesh.vertices];faces=len(mesh.polygons);o.to_mesh_clear();return points,faces
base,faces=evaluated();assert len(base)>len(body.data.vertices)+1000
show=control('Show Crystals');show.value=False
hidden,_=evaluated();assert len(hidden)==len(body.data.vertices),'Toggle did not disable generated crystals'
show.value=True;restored,_=evaluated();assert len(restored)==len(base)
major=control('Major Density');micro=control('Micro Density');oldmajor,oldmicro=major.value,micro.value
major.value=micro.value=0;empty,_=evaluated();assert len(empty)==len(body.data.vertices),'Density zero still generates crystals'
major.value,micro.value=oldmajor,oldmicro
length=control('Crystal Length');previous=length.value;length.value=.5
short,_=evaluated();assert len(short)==len(base) and any(sum((a-b)**2 for a,b in zip(p,q))>1e-8 for p,q in zip(short,base)),'Length control had no effect'
length.value=previous
seed=control('Seed');oldseed=seed.value;seed.value+=1;changed,_=evaluated();assert len(changed)!=len(base) or changed!=base,'Seed control had no effect';seed.value=oldseed
bone=rig.pose.bones['ValveBiped.Bip01_L_Forearm'];basis=bone.matrix_basis.copy();bone.rotation_mode='QUATERNION';bone.rotation_quaternion=Quaternion((0,0,1),.35)
posed,_=evaluated();assert posed!=base,'Armature did not deform the source'
bone.matrix_basis=basis;rig.update_tag();bpy.context.view_layer.update()
used=[]
for o in [body,skin,bpy.data.objects['Object_3']]:
 for mat in o.data.materials:
  if not mat or not mat.use_nodes:continue
  for n in mat.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:
    assert n.image.size[0]>0,'Missing used texture '+n.image.name
    used.append(n.image.name)
report={'passed':True,'body_base_vertices':len(body.data.vertices),'body_evaluated_vertices':len(base),'body_evaluated_faces':faces,'rig_bones':len(rig.data.bones),'controls':['visibility on/off','zero density','length adjustment','seed variation'],'armature_deforms_geometry':True,'used_textures_available':sorted(set(used)),'procedural_materials_require_baking_for_Unity':True}
(root/'Source'/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8');print('RESONATOR_VALIDATION',json.dumps(report))
