# 零界首个可玩灰盒 Demo

场景：Assets/NullBoundaryDemo/Scenes/NullBoundary_FirstDescent.unity

在 Unity Project 窗口中打开上述场景，再按 Play。独立于 SampleScene，未替换原场景。当前使用程序生成的体块、角色占位、晶体形状与合成音效，主要验证完整流程与交互。

## 已实现流程

电梯从 -2 朝 -17 下行 → -8 附近闪灯与双冲击 → 钢缆断裂与竖井镜头 → 坠落、撞顶、摔落、黑屏 → 在苏联食堂事故轿厢内苏醒 → 自由探索结晶受害者 → 后厨冰箱活体滑出 → 走廊敲晶与追逐 → 进入实验室并关闭 A 门 → 波形相位校准 → 拾取拳套触发警报 → 一名、再两名共鸣者入场 → 清场后 C 门开启 → 走入结束走廊。

## 操作

- WASD：移动。
- Shift：奔跑。
- 鼠标：观察。
- 空格：跳跃；开场期间可跳过。
- E：朝向近距离交互物时使用。进入实验室后先使用入口内侧左边的红色控制器。
- 右键按住：拳套格挡；面向来袭敌人成功格挡可储存冲量。
- 左键：有冲量时机械压缩并释放；无冲量时普通击打。
- Esc：暂停；校准界面中返回实验室。
- 死亡后 R：重试当前检查点；暂停/结束画面也提供重试。

实验室左侧青色终端负责校准。拖动滑条让青色波形与灰白波形重合，稳定约 0.8 秒后确认。完成后，再交互试验台的拳套。

## 场景与脚本

- Runtime/DemoFlow.cs：整体流程、校准 UI、遭遇计数、检查点与灰盒 HUD。
- Runtime/DemoPlayer.cs：第一人称移动、视角、交互、格挡与拳套命中。
- Runtime/DemoEnemy.cs：敲击、警觉、导航追逐、攻击、受击与倒地。
- Runtime/DemoDoor.cs：液压门运动和关门完成事件。
- Runtime/DemoIntro.cs、DemoIntroTrack.cs、DemoIntroClip.cs：Timeline 驱动的开场演出。
- Runtime/DemoSound.cs：轻量合成占位音效。
- Editor/DemoSceneBuilder.cs：编辑器内生成灰盒、预制体、材质、导航与 Timeline。
- Timelines/ElevatorCrash.playable：六段开场，可在 Timeline 窗口查看。
- Prefabs/OrdinaryResonator.prefab：普通共鸣者基础占位。
- Art/DemoNavigation.asset：当前灰盒导航数据。

通过菜单 Null Boundary > Build Greybox Demo 可重建。重建会更新本生成器拥有的 Demo 资产，请先关闭 Demo 场景并另存手工改动；生成器会拒绝在 Demo 场景打开时覆盖。不会生成或覆盖 SampleScene。

## 检查点

1. 在事故轿厢内苏醒。
2. 后厨出口、走廊敲击遭遇前。
3. 已持有拳套、入口门关闭、重新开始完整 1+2 教学战斗。

清场只统计本轮实验室登记的三个敌人。冰箱活体与走廊追逐者不属于清场目标。

## 当前边界

- 角色、手臂、尸体、晶体与设施为灰盒占位，尚未接入正式模型与动作。
- 电梯使用预设运动与黑屏切换，顶板破坏为静态场景；未使用真实动力学坠落。
- 第一人称手臂演出较简化，尚无完整角色全身动画。
- 只有普通共鸣者与本 Demo 所需拳套能力；后续实验室能力不在这个场景中。
- 暂无独立 Windows 可执行文件，当前交付为可在 Unity 编辑器 Play 的工程场景。
- 详细设计见 Docs/Demo/首个Demo流程与实现方案.md。
- 运行检查与截图见 Docs/Demo/Verification/。实际验证结果以 smoke-report.json 为准。

