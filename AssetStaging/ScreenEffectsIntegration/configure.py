from pathlib import Path
p=Path('Assets/Game/Shared/Actions/Resources/LevelFeedback.asset');s=p.read_text(encoding='utf-8-sig');s+='''  advancedScreenEffects: 1
  chargingHeatStart: 0.65
  chargingHeatScale: 0.18
  chargingHeatDistortion: 2.5
  chargingHeatOpacity: 0.3
  heavyWaveRadius: 1.2
  heavyDistortion: 6
  heavyChromatic: 2
  impactHeatScale: 0.5
  impactHeatDistortion: 3
  impactHeatOpacity: 0.45
  perfectGuardRadius: 0.35
  perfectGuardDistortion: 2.5
  perfectGuardChromatic: 1
  puzzleDistortion: 2
  puzzleChromatic: 0.8
''';p.write_text(s,encoding='utf-8')
p=Path('Assets/Game/VFX/Combat/Runtime/GauntletAdvancedPresentation.cs');s=p.read_text(encoding='utf-8-sig').replace('view = GetComponentInChildren<Camera>(true);','view = GetComponentInChildren<Camera>(true) ?? Camera.main;');p.write_text(s,encoding='utf-8')
