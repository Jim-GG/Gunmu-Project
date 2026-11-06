import bpy,json
from pathlib import Path
root=Path('D:/UNITY工程/棍木项目/棍木项目/ArtSource/ModernLab')
# Preserve the live window before replacing its older in-memory material version.
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Source/BeforeFinalMaterialLoad.blend'),copy=True)
original=json.loads((root/'Source/inspection.json').read_text(encoding='utf-8'))
scene=bpy.data.scenes['Modular Asset Kit']
for item in original['objects']:
 o=scene.objects.get(item['name']);assert o and len(o.data.vertices)==item['vertices'],'Live mesh changed; merge the saved copy before replacing it'
 assert all(abs(a-b)<.0001 for a,b in zip(o.location,item['location'])),'Live layout changed; merge the saved copy before replacing it'
bpy.ops.wm.open_mainfile(filepath=str(root/'ModernLab_ModularKit.blend'))
