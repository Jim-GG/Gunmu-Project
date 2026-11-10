# coding: utf-8
from pathlib import Path
import json,shutil,hashlib,subprocess,datetime
r=Path.cwd();b=r/'AssetStaging/LevelFeedbackIntegration/BatchProject';dest=r/'Docs/Integration'
for name,count in [('level-feedback-validation.json',26),('unity-play-smoke.json',32)]:
 p=b/'Docs/Integration'/name;d=json.loads(p.read_text(encoding='utf-8-sig'));assert d['passed'] and len(d['checks'])==count,d;shutil.copy2(p,dest/name)
p=b/'Docs/Integration/unity-scene-audit.json';d=json.loads(p.read_text(encoding='utf-8-sig'));assert len(d['reports'])==15 and all(not x.get('error') and not x.get('missingScripts') and not x.get('brokenReferences') for x in d['reports']);shutil.copy2(p,dest/p.name)
p=b/'AssetStaging/UDK7M/Boss/three_phase_validation.json';d=json.loads(p.read_text(encoding='utf-8-sig'));assert d['passed'] and len(d['checks'])==45;d['verifiedAt']=datetime.datetime.fromtimestamp(p.stat().st_mtime).isoformat();(dest/'boss-three-phase-regression.json').write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(b/'AssetStaging/LevelFeedbackIntegration/level-authored-charge.png',r/'AssetStaging/LevelFeedbackIntegration/level-authored-charge.png')
modified=subprocess.check_output(['git','diff','--name-only','--','Assets'],encoding='utf-8').splitlines();sources={}
for name in modified:
 if name.endswith('.cs'):
  source=r/name;snapshot=b/name;assert source.read_bytes()==snapshot.read_bytes(),name;sources[name]=hashlib.sha256(source.read_bytes()).hexdigest()
summary=dict(passed=True,isolated=True,unity=d.get('unity','6000.3.26f1'),checks=dict(authoredCombat=26,levelFlow=32,bossRegression=45,sceneAudit=15),sources=sources)
(dest/'authored-combat-integration.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
p=dest/'关卡表现整合.md';s=p.read_text(encoding='utf-8');s+='\n最终验证：原版拳套专项 26 项、完整关卡流程 32 项、BOSS 回归 45 项全部通过；15 个构建场景无丢失脚本或断开的对象引用。验证在隔离项目中执行，源文件哈希见 `authored-combat-integration.json`。\n';p.write_text(s,encoding='utf-8');print('All final checks passed and validated source hashes match.')
