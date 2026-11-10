from pathlib import Path
import json,hashlib
r=Path.cwd();b=r/'AssetStaging/LevelFeedbackIntegration/BatchProject';p=r/'Docs/Integration/authored-combat-integration.json';d=json.loads(p.read_text(encoding='utf-8'))
names=['Assets/Game/Shared/Actions/Runtime/GauntletPresentationSource.cs','Assets/Game/Shared/Actions/Runtime/LevelFeedbackProfile.cs','Assets/Game/Shared/Actions/Runtime/LevelPlayerPresentation.cs','Assets/Game/Shared/Actions/Runtime/PuzzlePresentation.cs','Assets/Game/Shared/Actions/Resources/LevelFeedback.asset','Assets/Tools/AssetPipeline/Editor/LevelFeedbackValidation.cs']
for name in names:
 assert (r/name).read_bytes()==(b/name).read_bytes(),name
 d['sources'][name]=hashlib.sha256((r/name).read_bytes()).hexdigest()
p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8');print('All new modules also match the verified snapshot')
