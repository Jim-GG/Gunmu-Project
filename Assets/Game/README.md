# 游戏资源目录

本目录统一收纳游戏内容，编辑器动画工作台独立放在 `Assets/Tools/AnimationWorkbench`。

| 目录 | 内容 |
| --- | --- |
| `Player` | 第一人称控制、拳套战斗、格挡、玩家预制体与训练功能 |
| `Player/Gauntlet` | 拳套模型、动画、材质、摄像机运动和预览场景 |
| `Enemies/Resonator` | 共鸣者模型、动画、材质、预制体；`AI` 内为行为、配置、动作轨道与测试场景 |
| `Environment/ModernLab` | 实验室模块、材质、贴图、Shader Graph、预制体及材质展示场景 |
| `Shared/Actions` | 玩家与敌人共用的动作运行系统，以及 `Resources/GauntletActions.asset` 和事件轨道 |
| `Demo` | 首次坠落 Demo 的流程、交互、Timeline 和场景 |
| `Scenes` | 通用场景；各系统的专用预览和测试场景仍放在对应系统的 `Scenes` 中 |
| `Scene` | 用户指定的新晶体跑酷试玩关卡 `CrystalImpactParkour.unity` |
| `CrystalParkour` | 晶体牵引反弹、风场、生长平台与无人机借力组件、配置和说明 |
| `Settings` | 渲染管线与相关配置 |
| `Input` | Input System 输入动作资源 |
| `Documentation` | Unity 模板原有说明与教程资源 |

常用入口：

- 玩家预制体：`Player/Prefabs/FirstPersonPlayer.prefab`
- 拳套动作配置：`Shared/Actions/Resources/GauntletActions.asset`
- 敌人配置：`Enemies/Resonator/AI/Data/Resonator_Default.asset`
- 战斗测试场景：`Enemies/Resonator/AI/Scenes/ResonatorCombatArena.unity`
- Demo 场景：`Demo/Scenes/NullBoundary_FirstDescent.unity`
- 实验室材质预览：`Environment/ModernLab/Scenes/ModernLabAdditionalSurfaces.unity`

2026-11-06 目录整理仅移动资源并更新路径，保留资源和脚本 GUID、类名、程序集、命名空间与菜单入口。没有重新生成动作、材质、预制体或关卡。

Blender 源工程仍位于项目根目录的 `ArtSource`，开发记录与验证脚本仍位于 `Docs`，避免作为游戏资源导入。迁移清单和检查结果位于 `Docs/ProjectStructure`。
