from pathlib import Path
import re,uuid
root=Path.cwd();script=root/'Assets/Game/Shared/Actions/Runtime/LevelFeedbackProfile.cs';meta=Path(str(script)+'.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
guid=re.search(r'guid: (\w+)',meta.read_text())[1]
clips={p.stem:p for p in (root/'Assets/Game/Audio').rglob('*') if p.suffix.lower() in ['.mp3','.wav','.ogg']}
def clip(name):
 p=clips[name];g=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1];return '{fileID: 8300000, guid: '+g+', type: 3}'
enum=Path('Assets/Game/Enemies/UDK7M/Runtime/UDK7MAudioProfile.cs').read_text(encoding='utf-8-sig');names=[x.strip() for x in re.search(r'enum UDK7MSound \{(.*?)\}',enum,re.S)[1].split(',')]
s='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+guid+', type: 3}\n  m_Name: LevelFeedback\n  m_EditorClassIdentifier: \n  actorSounds:\n'
for cue,name,vol in [('GauntletSwing','kick_swish',.5),('ParryImpact','handguard_impact_enemy',.7)]:
 s+=f'  - cue: {names.index(cue)}\n    clips:\n    - '+clip(name)+f'\n    volume: {vol}\n    cooldown: 0.12\n    spatialBlend: 0.15\n    minDistance: 3\n    maxDistance: 80\n    priority: 60\n'
for field,name in [('chargeStages','三段蓄力'),('fullChargeLoop','蓄力满循环音效'),('wounded','character_gets_wound_00'),('recharge','CrystalHealingMiningC_3'),('momentumLock','CrystalGenericCombined_02'),('momentumRelease','CrystalGenericCombined_03'),('mechanism','bullet-impact-plastic-01')]:s+='  '+field+': '+clip(name)+'\n'
s+='  puzzleVolume: 0.35\n  puzzleEffectScale: 0.22\n';p=root/'Assets/Game/Shared/Actions/Resources/LevelFeedback.asset';p.write_text(s,encoding='utf-8');meta=Path(str(p)+'.meta');
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
print('Level feedback profile created with existing audio assets')
