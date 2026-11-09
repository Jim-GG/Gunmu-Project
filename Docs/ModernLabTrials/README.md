# 现代实验室四关（Modern Lab Trials）

按客户最新要求重做的四个连续试玩关：**2 个解谜关 + 2 个战斗关**。复用 `ModernLab` 模块化资产、编号 `MCU-7A` 无人机、共鸣者敌人，以及 `CrystalParkour` 的晶体生长解谜机制。

## 场景

| 场景 | 代号 | 类型 | 主题 | 通过条件 |
| --- | --- | --- | --- | --- |
| `MLT_01_CrystalIntake` | ML-01 | 解谜 | 晶体取料 | 1 个碰撞接收器 + 1 处晶体生长 + 无人机升空 |
| `MLT_02_CrystalAscent` | ML-02 | 解谜 | 晶体攀升 | 2 个碰撞接收器 + 2 处晶体生长 + 无人机横渡裂谷 |
| `MLT_03_ResonanceArena` | ML-03 | 战斗 | 共鸣竞技场 | 清空场内的 6 名共鸣者 |
| `MLT_04_CoreBreach` | ML-04 | 战斗 | 核心突入 | 清空场内的 8 名共鸣者（终关） |

四关均在 `ProjectSettings/EditorBuildSettings.asset` 中启用。进入 `MLT_01_CrystalIntake` 按 Play 即可开始，通关后自动加载下一关；ML-04 清场后触发最终完成界面。

## 预览

![ML-01 晶体取料](Preview/MLT_01_CrystalIntake_view.png)

![ML-02 晶体攀升](Preview/MLT_02_CrystalAscent_view.png)

![ML-03 共鸣竞技场](Preview/MLT_03_ResonanceArena_view.png)

![ML-04 核心突入](Preview/MLT_04_CoreBreach_view.png)

## 编号无人机

- 模型与材质来自 `Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab`，运行时挂 `NeutralDrone`，每关一架，编号 `MCU-7A / 01`～`04`。
- **会打人**：进入 17 米且视线通畅时，机头炮口先亮起约 0.8 秒（可躲避的预警），随后发射 `DroneBolt` 弹丸；命中造成 8～12 点伤害，弹丸带少量重力下坠。
- **能踩**：甲板可站立；踩上去后玩家随无人机移动，按 `Space` 额外获得向上冲量。
- 解谜关的无人机悬停在 1.15 米（甲板约 1.5 米），**直接起跳够不到**，必须先敲裂地面让晶体平台长到 0.75 米再借力登机；解谜关无人机只巡逻不主动贴脸（`approachPlayer = false`），避免破坏“先解谜再登机”的节奏。
- 玩家在甲板上时无人机停止开火，保证登机后不会被自己的载具打死。

## 晶体解谜（从 CrystalParkour 搬运）

- 复用 `CrystalParkour.CrystalGrowthSite` / `CrystalSurface` / `CrystalTraversal`：走近发裂的地面，用拳套击打，晶体平台从地面长出（顶面约 0.75 米，可站立、可牵引反弹），成为登上无人机甲板、越过裂谷的台阶。
- 现代实验室玩家预制体已挂 `CrystalTraversal`；战斗关在 `TrialLevel.Start` 里直接禁用该组件，避免影响正面战斗手感。
- `CrystalTraversal` 新增 `respawnKeyEnabled` 开关：试玩关里关掉，`R` 交给 `TrialLevel` 重开整关；晶体跑酷 Demo 默认仍为开启，行为不变。

## 战斗关

- 敌人为现有 `ResonatorAgent`，使用 `Data/MLT_ResonatorTuning.asset`（停步距离 1.5、攻击起手距离 2.2）。
- 场地四角与中央布置 1.4～3.2 米见方的立柱做掩体，NavMesh 随场景烘焙。
- 通过条件只有一条：**清完场景内列出的全部共鸣者**，出口门随即升起。
- 无人机在战斗关更凶：ML-03 每 3 秒 10 点伤害，ML-04 每 2.4 秒 12 点伤害，且会主动靠近玩家。

## 操作

- `WASD` 移动，`Shift` 奔跑，`Space` 跳跃。
- 鼠标观察；左键短按/蓄力出拳（对晶体即生长、对箱子即冲击），右键按住格挡。
- `R` 重开本关；`Esc` 暂停。

## 主要文件

- `Assets/Game/ModernLabTrials/Runtime/NeutralDrone.cs`（巡逻 / 借力 / 攻击）
- `Assets/Game/ModernLabTrials/Runtime/DroneBolt.cs`（弹丸）
- `Assets/Game/ModernLabTrials/Runtime/TrialLevel.cs`（目标与 HUD）
- `Assets/Game/ModernLabTrials/Runtime/ImpactCrate.cs`、`ImpactReceiver.cs`、`TrialDoor.cs`、`TrialExit.cs`
- `Assets/Game/ModernLabTrials/Runtime/ModernLabSelfTest.cs`（自动冒烟）
- `Assets/Game/ModernLabTrials/Editor/ModernLabTrialBuilder.cs`（生成四关）
- `Assets/Game/ModernLabTrials/Editor/ModernLabTrialVerify.cs`、`ModernLabTrialPreview.cs`、`ModernLabTrialBuildTest.cs`

## 重新生成 / 校验

- 一键重建 + 校验 + 截图：Unity 菜单 `Tools → Modern Lab Trials → Build Four Levels`，再跑 `Verify Four Levels`、`Capture Previews`。
- 命令行（项目未打开时）：
  - `Unity.exe -batchmode -quit -projectPath <repo> -executeMethod ModernLabTrials.Editor.ModernLabTrialBuilder.BuildBatch -logFile Logs/build_levels.log`
  - `... ModernLabTrialBuilder.BuildVerifyCaptureBatch ...`（重建 + 校验 + 截图）
  - `... ModernLabTrialBuildTest.BuildBatch ...`
  - 运行 `builds/ModernLabTrials/ModernLabTrialsTest.exe -modernLabSelfTest` 跑自动冒烟。

重新生成会覆盖 `ModernLabTrials/Scenes` 下的四个场景，并重建玩家/无人机预制体；旧的 `ImpactCalibration / CrosswindGallery / VerticalShaft / SynthesisCore` 场景与其 NavMesh 资产会被自动删除。

## 验证结果

- 编辑模式场景校验：`MODERNLAB_VERIFY: PASS`（接收器/生长点/敌人/无人机/出口/NavMesh/缺失脚本逐项检查）。
- 预览截图：`Docs/ModernLabTrials/Preview/*.png`，四关各一张玩家视角。
- 独立开发包冒烟（`-modernLabSelfTest`）：`MODERNLAB_SELFTEST: PASS`，四关依次加载并通关，最终界面到达。
  - ML-01 receivers 1/1、enemies 2/2、growth 1/1、rides 1/1。
  - ML-02 receivers 2/2、enemies 3/3、growth 2/2、rides 1/1。
  - ML-03 enemies 6/6（无接收器/生长/借力目标）。
  - ML-04 enemies 8/8（终关，清场后进入完成界面）。
  - 每关均验证：玩家组件、至少一条目标、敌人 `isOnNavMesh`、无人机开火产生有效弹丸。

## 已知边界

- 场景仍是功能灰盒：模块、灯光、材质已接好，但细节和手感需要人工试玩后再调。
- 无人机攻击为直射弹丸，没有音效、命中特效和受击反馈。
- 战斗关难度按“6 人 / 8 人一次摆完”设定，尚未做数值平衡。
