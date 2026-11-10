# 项目与玩法设计摘要

> 晶振 V0.1 已整合最新 levels 与 UDK7M-BOSS-FIGHT；当前流程、目录归并与验证记录见 [合并记录](Docs/Integration/晶振V0.1.md)。当前以 levels 的地图和解谜为主线，拳套使用原版 GauntletCombat，并保留较长冲刺与风锥；详见 [关卡拳套整合](Docs/Integration/关卡表现整合.md)。

## 一、 玩法概括

本作是一款受《传送门》启发的 **3D 第一人称物理动量解谜 + 近身格斗游戏**。游戏把第一人称动作里的**“机械拳套（蓄力冲拳 + 架盾格挡）”**与物理空间中的**“动量锁定、叠加及偏转”**绑成同一套法则，并在此之上接了共鸣者敌人与清场战斗。核心理念有三点：

1. **万物皆可传导动量，玩家自身也是弹体**：方块、机关与玩家遵循同一套碰撞与动量规则。玩家既能用拳套击打方块、借机关把方块打去触发远端开关，也能用右键格挡高速来物，通过弹性碰撞把动量转移到自己身上，把自己当炮弹打出去。
2. **时间定格与空间矢量的自由重组**：借助子弹时间与动量锁定，把稍纵即逝的动态瞬间定格在半空。被锁定的物体既是静止踏板/掩体，也是“蓄能炮台”——它能继续接收玩家冲拳、摆锤或其他方块的撞击，速率层层累加、发射方向改写为最后一次受击方向。
3. **“格挡借力—反射充能—空中变向”的动作解谜闭环**：右键格挡不只是防御，还是弹性碰撞开关与充能引擎。面向来物格挡会保留甚至放大动量并增加拳套充能；未格挡则按塑性碰撞处理，双方大幅损失速度且不充能。

---

## 二、 核心机制

### 1. 机械拳套：蓄力冲拳与格挡碰撞
（[`PlayerChargedPunch`](Assets/Scripts/Player/PlayerChargedPunch.cs) / [`PlayerMovement`](Assets/Scripts/Player/PlayerMovement.cs) / [`PlayerGauntletView`](Assets/Scripts/Player/PlayerGauntletView.cs)）

- **左键蓄力冲拳**：使用原版 GauntletCombat 的 30 FPS 动作时钟，32 / 62 / 92 帧对应三档；短按走原版轻攻击，所有伤害等待实际 Hitbox 有效帧。能量用于跳过原版蓄力区间，晶体补满三格，与格挡共用。保留关卡原有 2.5 / 4.5 / 6.5 米冲刺及风锥，轻攻击不产生冲刺位移。
  - **空中惯性与高速变向**：冲拳自然结束会平滑保留突进惯性；已处于高速飞行状态时出拳不会减速，而是把当前速度矢量直接偏转到出拳方向。
  - **正交面施力与后坐力**：命中未锁定的动能方块时严格沿立方体六个正交面之一施加冲量，玩家自身同时获得向后上方的后坐力。
- **右键格挡**：完美格挡无伤，非完美格挡减伤 66%，成功格挡向共享能量池补一格；关卡物理碰撞继续保留格挡借力与反弹。
- **单一拳套状态机**：PlayerGauntletView 只实例化模型与配置相机动作，GauntletCombat 的 Playable 图负责原版动画、缓冲、过渡与逐帧 Hitbox。PunchPresentation 提供局部顿帧、镜头与准星反馈，音效和材质特效复用共享 GameActions 资源。

### 2. 动量锁定与子弹时间
（[`PlayerMovement`](Assets/Scripts/Player/PlayerMovement.cs) / [`KineticBox`](Assets/Scripts/Puzzle/KineticBox.cs)）

- **子弹时间（`Q`）**：全局时间大幅放缓，可锁定方块高亮，准星指向的目标进一步变色。
- **锁定与释放（`E`）**：瞄准方块按 `E` 定格在空间中并封存当时速率；锁定期间完全视作静态几何体，可站立、格挡，也能继续接收冲拳/摆锤/方块撞击，速率累加、方向由最后一次受击决定，并用 3D 箭头与表面颜色反馈。再次按 `E` 沿箭头方向一次性释放。
- 锁定可累积的最高速度有上限（默认 `40`），避免无限叠加导致穿模。

### 3. 场景道具与机关

| 道具 / 机关 | 对应脚本 | 机制功能定位 |
| :--- | :--- | :--- |
| **动能方块** | [`KineticBox`](Assets/Scripts/Puzzle/KineticBox.cs) | 核心物理载体。轴向对齐 + 低阻滑行，可被冲拳击打、被机关抛射、被远程锁定、方块之间传力。 |
| **运输管道** | [`KineticBoxDropper`](Assets/Scripts/Puzzle/KineticBoxDropper.cs) | 按间隔从管口冒出带初速度的动能方块，掉出界自动回收；预制体 `Prefabs/KineticBoxDropper.prefab` 只包含末端管段与脚本。 |
| **大炮** | [`Cannon`](Assets/Scripts/Puzzle/Cannon.cs) | 侧面填弹仓吸入玩家或动能方块，短暂蓄压后沿炮管方向（仰角/速度可配）发射；预制体 `Prefabs/Cannon.prefab`。 |
| **弹性墙** | [`BouncyWall`](Assets/Scripts/Puzzle/BouncyWall.cs) | 严格按碰撞法线反射。只有 `KineticBox` 与“按住右键格挡的玩家”会被弹，且玩家回满充能。 |
| **可切换角度的弹性墙** | [`BouncyWallAngleToggler`](Assets/Scripts/Puzzle/BouncyWallAngleToggler.cs) | 由按钮在“斜放/平放”两个预设欧拉角之间翻转一面弹性墙：斜放把方块弹向侧面，平放当弹床把玩家弹起来。 |
| **升降廊桥** | [`RisingBridge`](Assets/Scripts/Puzzle/RisingBridge.cs) | 被开关/按钮触发后把桥面从下方升到位；`jammed` 打勾时只抖一下升不起来，用来做“有座桥坏了”的关卡剧本。 |
| **大摆锤** | [`PendulumMechanism`](Assets/Scripts/Puzzle/PendulumMechanism.cs) | 物理单摆冲击机关。按真实摆角计算切线速度：最低点给水平冲击，上扬阶段给斜向上抛射。 |
| **跷跷板 + 起重机** | [`SeesawMechanism`](Assets/Scripts/Puzzle/SeesawMechanism.cs) | 高空重物砸落 → 另一端方块竖直弹射，落点按几何自动推算。 |
| **充能晶体** | [`DashCrystal`](Assets/Scripts/Puzzle/DashCrystal.cs) | 玩家触碰回满 3 格充能，冷却后再生。 |
| **机关按钮** | [`PuzzleButton`](Assets/Scripts/Puzzle/PuzzleButton.cs) | 按 `F` 或踩踏触发任意 `IPuzzleTriggerable`（摆锤、跷跷板、后续新机关）。 |
| **目标开关** | [`PuzzleSwitch`](Assets/Scripts/Puzzle/PuzzleSwitch.cs) | 被动能方块击中或玩家踩踏后激活变绿；可顺带位移一个目标（开门/抬升），并实现 `ILevelExitGate` 作为出口门禁。 |
| **关卡出口** | [`LevelExit`](Assets/Scripts/LevelFlow/LevelExit.cs) | 玩家进入触发区 → 检查全部门禁 → 加载下一关；留空场景名即最终关。 |
| **共鸣者敌人** | [`ResonatorEnemy`](Assets/Game/Enemies/Resonator/AI/Runtime/ResonatorEnemy.cs) | NavMesh 行为状态机（休眠/苏醒/狂暴/行走/奔跑/头槌/拳击/踢击/连招/受击/死亡）+ 逐帧命中盒 + 布娃娃。 |
| **战斗遭遇** | [`ResonatorEncounter`](Assets/Scripts/Enemies/ResonatorEncounter.cs) | 按出生点逐个放出共鸣者，清场后开门；实现 `ILevelExitGate`。 |

> 普通敌人受伤由原版 Hitbox 经 IActionDamageReceiver 结算，教学敌人由 ResonatorPunchTarget 保留专用击飞规则；玩家受伤/格挡/死亡走原有 `PlayerCombatController` + `GameStateManager`。

### 4. 操作

| 输入 | 行为 |
| :--- | :--- |
| `WASD` / `Shift` / `Space` | 移动 / 奔跑 / 跳跃 |
| 鼠标 | 观察 |
| 左键按住 / 松开 | 蓄力 / 冲拳 |
| 右键按住 | 格挡 |
| `Q` | 子弹时间 |
| `E` | 锁定 / 释放方块；靠近交互物时拾取或使用 |
| `F` | 触发机关按钮 |
| `R` | 重开当前房间（阵亡后同样可用） |
| `Esc` | 暂停 / 释放鼠标 |

---

## 三、 工程结构

- `Assets/Scripts/`：玩法框架（`Player/`、`Puzzle/`、`LevelFlow/`、`Enemies/`，根目录为全局契约）。
- `Assets/Game/Enemies/Resonator/`：共鸣者模型、19 个动作、AI（状态机 + 动画工作台）。
- `Assets/Game/Player/Gauntlet/`：拳套美术与 Viewmodel、动画控制器、预览场景。
- `Assets/Scenes/`：关卡场景。`Soviet_1` 是正式关卡（白模手工搭建），`Test_01_PunchThrough` / `Test_02_Combat` / `Test_03_Billiards` 是组件示例用的测试关卡。
- 关卡内容与流程见 `COMPONENTS.md` 的「当前关卡」一节。

## 四、 当前边界

- 正式关卡目前有 `Soviet_1` → `Soviet_TheCatwalk`、`Soviet_GetTheBox`（大炮 + 弹性墙循环）与 `Soviet_Traffic`（“中”字形 + 可切角度弹性墙 + 升降廊桥），均为白模（在 `Assets/Scenes/`）；另有三关作为组件使用示例（`Test_01`~`Test_03`）。
- 关卡包含共鸣者并连接 UDK7M BOSS；拳套、音效与材质反馈使用共享系统。
- 过场演出用 Timeline / Animator，或 `PuzzleSwitch` 的“移动目标 + 激活偏移”；暂无通用的位移组件。
- 关卡切换、暂停、死亡重开由 `GameStateManager` 统一管理，新场景载入后自动回到 `Running`。
