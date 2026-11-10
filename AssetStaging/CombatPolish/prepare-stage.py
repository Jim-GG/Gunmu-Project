from pathlib import Path
import subprocess,hashlib,json

def git_files(args):
    return [p for p in subprocess.check_output(['git','-c','core.quotepath=false']+args).decode('utf-8').split('\0') if p]
changed=git_files(['diff','--name-only','-z'])
blocked={'Assets/Game/CrystalParkour/Data/ParkourVolume.asset','Assets/Game/Scene/UDK7MBossArena_ThreePhase.unity','Assets/Game/VFX/Combat/PurpleBloodBurst.prefab'}
selected=[p for p in changed if not p.startswith('AssetStaging/') and p not in blocked]
prefixes=['Assets/Game/VFX/ScreenEffects','Assets/Game/VFX/Combat/Shaders','Assets/Game/Environment/\u6389\u843d\u77f3\u5757']
stems=['Assets/Game/Enemies/Resonator/AI/Runtime/CorpseImpactAudio.cs','Assets/Game/Enemies/Resonator/AI/Runtime/ResonatorVoice.cs','Assets/Game/Shared/Actions/Resources/CombatFeedback.asset','Assets/Game/Shared/Actions/Runtime/CombatFeedbackProfile.cs','Assets/Game/Shared/Actions/Runtime/GauntletCombatFeedback.cs','Assets/Game/VFX/Combat/Materials/FallingWarning_Transparent.mat','Assets/Game/VFX/Combat/Materials/TransientElectricSurface.mat','Assets/Game/VFX/Combat/ResonatorBloodMist.prefab','Assets/Game/VFX/Combat/Runtime/ElectricSurge.cs','Assets/Tools/AssetPipeline/Editor/CombatPolishSetup.cs','Assets/Tools/AssetPipeline/Editor/CombatPolishValidation.cs','Assets/Tools/AssetPipeline/Editor/RockGeometryUtility.cs','Assets/Game/Audio/SFX/Impact_Blunt_Large_V5_Stone_02.mp3','Assets/Game/Audio/SFX/\u5171\u9e23\u8005\u543c\u53eb.mp3','Assets/Game/Audio/SFX/\u62f3\u5957/\u4e09\u7ea7\u84c4\u529b\u51fa\u62f3.mp3']
stems += ['Assets/Game/Audio/SFX/\u62f3\u5957/\u6253\u51fb\u97f3\u6548-%02d.mp3'%i for i in range(1,11)]
exact=set(stems+[p+'.meta' for p in stems])
for p in git_files(['ls-files','--others','--exclude-standard','-z']):
    if p in exact or any(p==s+'.meta' or p.startswith(s+'/') for s in prefixes) or p.startswith('Docs/Integration/combat-polish') or p=='Docs/Integration/\u6218\u6597\u97f3\u753b\u4e0e\u5c4f\u5e55\u53cd\u9988.md':
        selected.append(p)
selected=sorted(set(selected))
Path('AssetStaging/CombatPolish/stage-paths.txt').write_bytes(('\0'.join(selected)+'\0').encode('utf-8'))
hashes={p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in selected if Path(p).suffix in ('.cs','.shader','.asset','.prefab')}
Path('Docs/Integration/combat-polish-source-hashes.json').write_text(json.dumps(hashes,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'files':len(selected),'selected':selected},ensure_ascii=True))
