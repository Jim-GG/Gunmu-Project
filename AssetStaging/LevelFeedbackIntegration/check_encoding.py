from pathlib import Path
p=Path('AssetStaging/LevelFeedbackIntegration/update_docs.py');b=p.read_bytes();print(repr(b[:200]));
try:print(b.decode('utf-8-sig')[:60])
except Exception as e:print(repr(e))
p=Path('Assets/Scripts/Player/PlayerChargedPunch.cs');b=p.read_bytes();print(repr(b[-450:-350]));print('utf8',len(b.decode('utf-8-sig')))
