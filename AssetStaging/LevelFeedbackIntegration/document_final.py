# coding: utf-8
from pathlib import Path
p=Path('project_summary.md');s=p.read_text(encoding='utf-8-sig');a=s.index('- **外观与规则分离**');e=s.index('\n',a);s=s[:a]+'- **单一拳套状态机**：PlayerGauntletView 只实例化模型与配置相机动作，GauntletCombat 的 Playable 图负责原版动画、缓冲、过渡与逐帧 Hitbox。PunchPresentation 提供局部顿帧、镜头与准星反馈，音效和材质特效复用共享 GameActions 资源。'+s[e:];p.write_text(s,encoding='utf-8')
