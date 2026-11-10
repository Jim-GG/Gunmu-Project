from pathlib import Path
p=Path('Assets/Scripts/Player/PlayerChargedPunch.cs');s=p.read_text(encoding='utf-8-sig').replace('public void NotifySuccessfulBlock()=>AddEnergy(3);','public void NotifySuccessfulBlock()=>AddEnergy(1);');p.write_text(s,encoding='utf-8')
