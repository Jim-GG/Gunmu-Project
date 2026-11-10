from pathlib import Path
p=Path('AssetStaging/LevelFeedbackIntegration/update_docs.py');p.write_bytes(b'# coding: utf-8\n'+p.read_bytes())
