# 现代实验室四关（Modern Lab Trials）

基于 `AssetComplete` 分支的 ModernLab 模块化资产、编号 `MCU-7A` 无人机和共鸣者敌人制作的 4 个连续试玩关卡。

## 场景

| 场景 | 代号 | 主题 | 目标 |
| --- | --- | --- | --- |
| `MLT_01_ImpactCalibration` | ML-01 | 冲击校准 | 1 个碰撞接收器 + 2 名共鸣者 + 无人机升空 |
| `MLT_02_CrosswindGallery` | ML-02 | 横风长廊 | 2 个碰撞接收器 + 3 名共鸣者 + 无人机横渡 12 米缺口 |
| `MLT_03_VerticalShaft` | ML-03 | 垂直竖井 | 3 个碰撞接收器 + 3 名共鸣者 + 无人机升上西侧高台 |
| `MLT_04_SynthesisCore` | ML-04 | 合成核心 | 2 个碰撞接收器 + 4 名共鸣者 + 无人机越过中央深渊进入核心 |

四关均在 `ProjectSettings/EditorBuildSettings.asset` 中启用。进入 `MLT_01_ImpactCalibration` 按 Play 即可开始，通关后自动加载下一关；ML-04 核心触发最终完成界面。

## 操作

- `WASD` 移动，`Shift` 奔跑，`Space` 跳跃。
- 鼠标观察；左键短按/蓄力出拳；右键按住格挡。
- `R` 重开本关；`Esc` 暂停。
- 走到高台出口触发区自动加载下一关。

## 碰撞解谜

- `ImpactCrate` 是带 Rigidbody 的实验室箱体，实现 `IActionTarget`，可被现有拳套动作命中并获得冲量。
- `ImpactReceiver` 只接受速度超过阈值的 `ImpactCrate` 撞击；被撞后亮起并计入关卡目标。
- 接收器被激活后 `TrialDoor` 在全部目标完成时升起，`TrialExit` 触发下一关。

## 编号无人机

- 使用 `Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab` 的模型与材质，运行时添加 `NeutralDrone`。
- 每关一架，编号 `MCU-7A / 01` 到 `04`，头顶用 TextMesh 显示。
- 行为：在巡逻点之间游走；玩家进入范围时靠近；玩家站上甲板后：
  - 有 `liftTop` 的关卡（01/03/04）向上运送，到顶后可跳上高台；
  - 02 没有 liftTop，作为横渡平台沿缺口往返；玩家离开甲板时计一次“借力”。
- 站在甲板上按 `Space` 会额外获得向上冲量。
- 无人机不攻击玩家，也不会受伤。

## 主要文件

- `Assets/Game/ModernLabTrials/Runtime/NeutralDrone.cs`
- `Assets/Game/ModernLabTrials/Runtime/ImpactCrate.cs`
- `Assets/Game/ModernLabTrials/Runtime/ImpactReceiver.cs`
- `Assets/Game/ModernLabTrials/Runtime/TrialLevel.cs`
- `Assets/Game/ModernLabTrials/Runtime/TrialDoor.cs`
- `Assets/Game/ModernLabTrials/Runtime/TrialExit.cs`
- `Assets/Game/ModernLabTrials/Editor/ModernLabTrialBuilder.cs`
- `Assets/Game/ModernLabTrials/Editor/ModernLabTrialVerify.cs`
- `Assets/Game/ModernLabTrials/Editor/ModernLabTrialPreview.cs`
- `Assets/Game/ModernLabTrials/Prefabs/ModernLabPlayer.prefab`
- `Assets/Game/ModernLabTrials/Prefabs/ModernLabDrone.prefab`

## 重新生成 / 校验

- 生成四关：Unity 菜单 `Tools → Modern Lab Trials → Build Four Levels`。
- 场景校验：`Tools → Modern Lab Trials → Verify Four Levels`。
- 预览截图：`Tools → Modern Lab Trials → Capture Previews`。
- 构建测试包：`Tools → Modern Lab Trials → Build Test Player`。

重新生成会覆盖 `ModernLabTrials/Scenes` 下的四个场景，并重建玩家/无人机预制体。手工调整场景前请先备份。

## 验证结果

- 编辑模式场景校验：`MODERNLAB_VERIFY: PASS`。
- 独立开发包自动冒烟：`MODERNLAB_SELFTEST: PASS`，四关依次加载并到达最终完成界面；每关的接收器、敌人计数、无人机借力和出口均通过。
- 每关敌人 `NavMeshAgent.isOnNavMesh` 在冒烟测试中为 true；启动时可能仍打印“Failed to create agent because there is no valid NavMesh”，这是 Agent 在 NavMesh 补齐前 OnEnable 的警告，随后 `TrialNavMeshBootstrap` / `TrialLevel` 会重建 NavMesh 并重新启用 Agent。
- 尚未完成人工从 ML-01 到 ML-04 的完整手感验收；自动测试覆盖入口、目标、出口链路与最终界面，不等同于完整试玩。

## 已知边界

- 场景是功能灰盒：ModernLab 模块已接入，但灯光、反射、地面材质与房间细节仍需要按实际试玩调整。
- 敌人使用现有 `ResonatorAgent`，没有单独调整数值或新增敌人类型。
- 无人机甲板在运行时扩展为可站立尺寸；模型本身仍是原始 MCU-7A。
- 尚未完成人工从 ML-01 到 ML-04 的完整手感验收；自动冒烟测试覆盖关卡加载、目标追踪、出口链路与最终界面。
