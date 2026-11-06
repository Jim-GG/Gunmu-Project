"""Migrate the generated colour variants to shared white maps and editable tint."""
import bpy,json
from pathlib import Path
root=Path('D:/UNITY工程/棍木项目/棍木项目/ArtSource/ModernLab')
report=json.loads((root/'build_report.json').read_text(encoding='utf-8'))
for label,rgb in [('Lime',(.53,.94,.025)),('Red',(.97,.065,.035)),('Yellow',(.99,.79,.025))]:
 key='Lab_Paint_'+label;surface='Lab_Paint_GlossWhite' if label=='Yellow' else 'Lab_Paint_RoughWhite';material=bpy.data.materials[key]
 for node in material.node_tree.nodes:
  if node.type=='TEX_IMAGE' and node.image and node.image.name.endswith('_BaseColor'):node.image=bpy.data.images[surface+'_BaseColor']
  if node.type=='MIX_RGB':node.inputs[2].default_value=tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb)+(1,)
 old=bpy.data.images.get(key+'_BaseColor')
 if old and old.users==0:bpy.data.images.remove(old)
 report['materials'][key]['files']=list(report['materials'][surface]['files']);report['materials'][key]['tint_srgb']=list(rgb)
(root/'build_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.data.libraries.write(str(root/'ModernLab_Materials.blend'),{bpy.data.materials[k] for k in report['materials']},fake_user=True,compress=True)
bpy.context.window.scene=bpy.data.scenes['Additional Surface Review'];bpy.ops.wm.save_as_mainfile(filepath=str(root/'ModernLab_ModularKit.blend'),compress=True);bpy.ops.render.render(write_still=True)
print('MODERN_LAB_EDITABLE_TINT_SAVED',flush=True)
