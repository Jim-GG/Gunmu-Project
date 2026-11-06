# 2026-11-06 Unity 目录整理

`Assets` 顶层统一为 `Game` 与 `Tools`。游戏内容按玩家、敌人、环境、Demo、共享系统、输入、场景与设置分组；动画工作台移至 `Tools/AnimationWorkbench/Editor`。

`MovePlan.json` 为原位置与新位置的精确对应清单。资源通过 Unity `AssetDatabase.MoveAsset` 移动，保留 `.meta`、资源与脚本 GUID。仅移除了原本空的“备份拳套”外层容器，以及本次创建时 Unity 自动添加后缀的空重名目录。无游戏资产被删除。

代码、导出脚本、验证脚本、当前资源清单及说明中的旧路径已更新。脚本类名、程序集、命名空间、运行逻辑与菜单入口均保留；Resources 加载名称不变。没有运行任何资产或场景重建工具。

验证记录：

- `Validation/BeforeFiles.json`：迁移前全部文件的 SHA-256 和 meta GUID。
- `Validation/BeforeNative.json`、`AfterNative.json`：Unity 资源、引用依赖、脚本类型、材质属性和场景状态快照。
- `Validation/MigrationResult.json`：17 项移动、53 处文件路径更新。
- `Validation/LayoutVerification.json`：381 项文件资产均保留，440 个原有资源/目录 GUID 保留（仅去除一个空旧容器的目录 GUID），157 组依赖集合不变，没有 Missing Script；除路径替换外，原游戏资产文件内容未改变。
- 现有 `SharedWorkbenchScenarios` 验证通过：四段拳套预览逐骨骼匹配 FBX，最大角误差 0；混合权重、事件隔离、命中盒原点和 charge-full 0–50 帧范围正确。
- `Validation/ResonatorWorkbenchReport.json`：敌人工作台验证通过；骨骼命中盒、根命中盒和混合角误差为 0，带缩放的根位移误差约 1.28e-8 米，攻击位移预览约 1.784 米。

整理期间用户继续调整的两份光滑漆材质，其编辑器内 `NormalStrength` / `MicroStrength` 与初始快照不同；这些实时手调值予以保留，没有恢复成旧值。磁盘材质文件的内容仍与迁移前相同。

迁移脚本属于本次整理记录，不应作为日常 Setup 工具重复运行。后续直接在新目录编辑、引用资源即可。
